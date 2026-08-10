using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditorDevelopmentBenchmark.Editor.Benchmarking.Categories;
using UnityEditorDevelopmentBenchmark.Editor.Util;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace UnityEditorDevelopmentBenchmark.Editor.Benchmarking
{
    /// <summary>
    /// To be called from the command line via <see cref="StartBenchmarkHeadless"/>.
    /// </summary>
    /// <remarks>
    /// Entering play mode triggers a domain reload, which destroys any in-memory coroutine/closure state
    /// (including the <see cref="EditorApplication.update"/> subscription that would have driven it). Because of
    /// this, the benchmark can't be implemented as a single coroutine living across the play mode transition.
    /// Instead, progress is persisted in <see cref="SessionState"/> (which survives domain reloads but not editor
    /// restarts, unlike <see cref="EditorPrefs"/>, which is global across all projects on the machine) and the
    /// <see cref="EditorApplication.update"/> subscription is re-attached from an <see cref="InitializeOnLoad"/>
    /// static constructor every time the domain reloads.
    /// </remarks>
    /// <remarks>
    /// Acts as a thin orchestrator around one <see cref="IBenchmarkCategoryRunner"/> per <see cref="BenchmarkCategory"/>
    /// (see <see cref="_categoryRunners"/>): it drives whichever one is currently active via
    /// <see cref="EditorApplication.update"/>, advances to the next one when it reports
    /// <see cref="BenchmarkCategoryTickResult.Completed"/>/<see cref="BenchmarkCategoryTickResult.Skipped"/>, and
    /// aborts the whole run if one reports <see cref="BenchmarkCategoryTickResult.Failed"/> (a phase timing out).
    /// Everything specific to one category (its own sub-steps, temporary scenes/folders, etc.) lives in that
    /// category's own class under the <c>Categories</c> namespace instead of here.
    /// </remarks>
    /// <remarks>
    /// <see cref="BenchmarkCategory.EditorStartup"/> is neither an <see cref="IBenchmarkCategoryRunner"/> nor
    /// stepped via <see cref="EditorApplication.update"/>, for the same underlying reason as
    /// <see cref="BenchmarkCategory.DomainReload"/>: measuring it means measuring a full process cold-start (the
    /// editor exiting and a new one launching), which wipes <see cref="SessionState"/> - and even plain static
    /// fields - the instant the process exits. There's nothing to "drive" across that boundary from within this
    /// process. Instead, <see cref="StartBenchmark(BenchmarkRunOptions)"/> simply reads back whichever startup
    /// duration <see cref="EditorStartupUtil"/> already recorded (via its <see cref="EditorPrefs"/>-backed
    /// <see cref="EditorStartupUtil.TryGetPersistedLastStartupDuration"/>, which survives both domain reloads and
    /// process restarts) for the cold start that already happened when the editor launched to run this benchmark -
    /// a single one-shot sample per run, not an N-times-averaged measurement like the other categories.
    /// </remarks>
    [InitializeOnLoad]
    [UsedImplicitly]
    public static class BenchmarkRunner
    {
        /// <summary>
        /// Fired whenever a benchmark run ends, whether it completed normally or was aborted early (e.g. due to a
        /// timeout). Intended for UI (such as <see cref="BenchmarkRunnerEditorWindow"/>) that wants to refresh
        /// itself as soon as fresh results are available, rather than only by polling <see cref="IsRunning"/>.
        /// </summary>
        public static event Action BenchmarkFinished;

        /// <summary>
        /// Whether a benchmark run is currently in progress (as opposed to <see cref="OrchestratorState.None"/>).
        /// </summary>
        public static bool IsRunning => GetState() != OrchestratorState.None;

        private enum OrchestratorState
        {
            None,
            WaitingForInitialCompilation,
            RunningCategory
        }

        private const string _stateKey = "UnityEditorDevelopmentBenchmark.BenchmarkRunner.State";
        private const string _currentCategoryIndexKey = "UnityEditorDevelopmentBenchmark.BenchmarkRunner.CurrentCategoryIndex";
        private const string _runCountKeyPrefix = "UnityEditorDevelopmentBenchmark.BenchmarkRunner.RunCount.";
        private const string _headlessKey = "UnityEditorDevelopmentBenchmark.BenchmarkRunner.Headless";
        private const string _headlessAbortedKey = "UnityEditorDevelopmentBenchmark.BenchmarkRunner.HeadlessAborted";

        /// <summary>
        /// One runner per <see cref="BenchmarkCategory"/> this benchmark drives, in the order they run - the same
        /// relative order as <see cref="BenchmarkCategory"/>'s declaration (skipping <see cref="BenchmarkCategory.EditorStartup"/>
        /// and <see cref="BenchmarkCategory.DomainReload"/>, neither of which is stepped via an
        /// <see cref="IBenchmarkCategoryRunner"/> - see this class's remarks on both). Held as a static readonly
        /// array (recreated fresh after every domain reload, since these instances hold no state of their own -
        /// see <see cref="IBenchmarkCategoryRunner"/>) purely to fix that order in one obvious place, the same
        /// reasoning <see cref="UnityEditorDevelopmentBenchmark.Editor.UserWaitTimeAggregator"/> uses for its
        /// trackers.
        /// </summary>
        private static readonly IBenchmarkCategoryRunner[] _categoryRunners =
        {
            new AssetImportBenchmarkCategoryRunner(),
            new CompilationBenchmarkCategoryRunner(),
            new LightmapBakingBenchmarkCategoryRunner(),
            new PlayModeSwitchBenchmarkCategoryRunner(),
            new BuildBenchmarkCategoryRunner()
        };

        private static readonly IBenchmarkRunnerContext _context = new BenchmarkRunnerContext();

        static BenchmarkRunner()
        {
            // Subscribed unconditionally (not just while a benchmark is in progress) and re-attached after every
            // domain reload, since AssemblyReloadTimer.Updated is itself what fires *because of* the domain
            // reload we want to measure; OnAssemblyReloadTimerUpdated discards the event if no benchmark is
            // currently running.
            AssemblyReloadTimer.Updated += OnAssemblyReloadTimerUpdated;

            // Re-attach after every domain reload (including the one triggered by EnterPlaymode) if a benchmark
            // is currently in progress.
            if (GetState() != OrchestratorState.None)
            {
                EditorApplication.update += Step;
            }
        }

        /// <summary>
        /// Domain reloads are triggered by Unity itself as part of the forced recompilations already requested by
        /// <see cref="CompilationBenchmarkCategoryRunner"/>, rather than something this runner can request
        /// separately, so the <see cref="BenchmarkCategory.DomainReload"/> category is recorded by simply
        /// listening for <see cref="AssemblyReloadTimer"/> to reconstruct each domain reload's duration from the
        /// Bee profiler trace, whenever that happens to fire while a benchmark is in progress. This is a
        /// standalone, always-on listener rather than an <see cref="IBenchmarkCategoryRunner"/> like the other
        /// categories, since it isn't a phase you step through - it just piggybacks on whatever else is
        /// happening (mainly the Compilation category) at the time.
        /// </summary>
        private static void OnAssemblyReloadTimerUpdated()
        {
            if (GetState() == OrchestratorState.None)
            {
                return;
            }

            var domainReloadDuration = AssemblyReloadTimer.AssemblyReloadDuration;
            BenchmarkCategoryTimeTracker.AddDuration(BenchmarkCategory.DomainReload, domainReloadDuration);

            Debug.Log($"Domain reload finished, took {domainReloadDuration}.");
        }

        [UsedImplicitly]
        public static void StartBenchmark()
        {
            StartBenchmark(new BenchmarkRunOptions());
        }

        /// <summary>
        /// Same as <see cref="StartBenchmark()"/>, but additionally quits the editor once the run finishes -
        /// normally or aborted, with exit code 1 in the aborted case - rather than leaving it open. Meant to be
        /// invoked via <c>-executeMethod</c> from the command line (see <c>start_benchmark_macos.sh</c> /
        /// <c>start_benchmark_windows.bat</c>), so a scripted run doesn't leave the editor sitting open
        /// afterwards; deliberately not wired to a menu item, since quitting the editor out from under an
        /// interactive session would be surprising. The "headless" flag driving this is
        /// <see cref="SessionState"/>-backed (<see cref="_headlessKey"/>) rather than an in-memory subscription to
        /// <see cref="BenchmarkFinished"/>, since the benchmark's own <see cref="BenchmarkCategory.Compilation"/>
        /// category triggers domain reloads that would otherwise silently drop such a subscription before the run
        /// actually finishes.
        /// </summary>
        [UsedImplicitly]
        public static void StartBenchmarkHeadless()
        {
            if (!StartBenchmark(new BenchmarkRunOptions()))
            {
                Debug.LogError("Could not start benchmark in headless mode; exiting.");
                EditorApplication.Exit(1);
                return;
            }

            SessionState.SetBool(_headlessKey, true);
        }

        /// <summary>
        /// Returns whether the run actually started (<c>false</c> if the editor was already in/entering play mode,
        /// or a benchmark was already in progress).
        /// </summary>
        [UsedImplicitly]
        public static bool StartBenchmark(BenchmarkRunOptions options)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Cannot start benchmark while in play mode.");
                return false;
            }

            if (GetState() != OrchestratorState.None)
            {
                Debug.LogWarning("A benchmark is already in progress.");
                return false;
            }

            options ??= new BenchmarkRunOptions();

            options.PlayModeSwitchCount = ClampToAtLeastOne(options.PlayModeSwitchCount, nameof(options.PlayModeSwitchCount));
            options.CompilationRunCount = ClampToAtLeastOne(options.CompilationRunCount, nameof(options.CompilationRunCount));
            options.AssetImportRunCount = ClampToAtLeastOne(options.AssetImportRunCount, nameof(options.AssetImportRunCount));
            options.LightmapBakeRunCount = ClampToAtLeastOne(options.LightmapBakeRunCount, nameof(options.LightmapBakeRunCount));
            options.BuildRunCount = ClampToAtLeastOne(options.BuildRunCount, nameof(options.BuildRunCount));

            if (!TryDisableConsoleClearOnPlay())
            {
                Debug.LogWarning("Couldn't disable console clear on play. This may cause the benchmark to not show all logs. Please disable it manually in the Console window settings.");
            }

            Debug.Log($"<color=lime>Starting benchmark ({options.CompilationRunCount} compilation run(s), {options.AssetImportRunCount} asset import run(s), {options.LightmapBakeRunCount} lightmap bake run(s), {options.BuildRunCount} build run(s), {options.PlayModeSwitchCount} play mode switch(es))...</color>");

            foreach (BenchmarkCategory category in Enum.GetValues(typeof(BenchmarkCategory)))
            {
                BenchmarkCategoryTimeTracker.Reset(category);
            }

            if (EditorStartupUtil.TryGetPersistedLastStartupDuration(out var editorStartupDuration))
            {
                BenchmarkCategoryTimeTracker.AddDuration(BenchmarkCategory.EditorStartup, editorStartupDuration);
            }
            else
            {
                Debug.LogWarning("Could not determine editor startup duration for this session; EditorStartup category will report zero.");
            }

            SetRunCount(BenchmarkCategory.Compilation, options.CompilationRunCount);
            SetRunCount(BenchmarkCategory.AssetImport, options.AssetImportRunCount);
            SetRunCount(BenchmarkCategory.LightmapBaking, options.LightmapBakeRunCount);
            SetRunCount(BenchmarkCategory.Build, options.BuildRunCount);
            SetRunCount(BenchmarkCategory.PlayModeSwitch, options.PlayModeSwitchCount);

            SessionState.SetBool(_headlessAbortedKey, false);
            SetState(OrchestratorState.WaitingForInitialCompilation);
            _context.ResetPhaseTimer();

            EditorApplication.update += Step;

            return true;
        }

        /// <summary>
        /// Stops the currently in-progress benchmark run early (e.g. in response to the user clicking
        /// "Stop Benchmark" in <see cref="BenchmarkRunnerEditorWindow"/>), doing the same best-effort cleanup as a
        /// category reporting <see cref="BenchmarkCategoryTickResult.Failed"/> would. Categories that hadn't
        /// finished yet simply keep whatever partial total they'd accumulated so far.
        /// </summary>
        [UsedImplicitly]
        public static void StopBenchmark()
        {
            if (GetState() == OrchestratorState.None)
            {
                Debug.LogWarning("No benchmark is currently running.");
                return;
            }

            Debug.LogWarning("<color=orange>Benchmark stopped by user.</color>");

            AbortBenchmark();
        }

        private static void Step()
        {
            switch (GetState())
            {
                case OrchestratorState.WaitingForInitialCompilation:
                    StepWaitingForInitialCompilation();
                    break;

                case OrchestratorState.RunningCategory:
                    StepRunningCategory();
                    break;

                case OrchestratorState.None:
                default:
                    EditorApplication.update -= Step;
                    break;
            }
        }

        private static void StepWaitingForInitialCompilation()
        {
            if (EditorApplication.isCompiling)
            {
                if (_context.HasPhaseTimedOut())
                {
                    Debug.LogWarning("Timeout while waiting for compilation to finish.");
                    AbortBenchmark();
                }

                return;
            }

            Debug.Log("Preparing benchmark...");

            BeginCategory(0);
        }

        private static void StepRunningCategory()
        {
            var index = GetCurrentCategoryIndex();
            var runner = _categoryRunners[index];

            var result = runner.Tick(_context);

            switch (result)
            {
                case BenchmarkCategoryTickResult.InProgress:
                    break;

                case BenchmarkCategoryTickResult.Completed:
                case BenchmarkCategoryTickResult.Skipped:
                    AdvanceToNextCategoryOrFinish(index);
                    break;

                case BenchmarkCategoryTickResult.Failed:
                    AbortBenchmark();
                    break;
            }
        }

        private static void BeginCategory(int index)
        {
            SetCurrentCategoryIndex(index);

            var runner = _categoryRunners[index];
            runner.Begin(GetRunCount(runner.Category));

            SetState(OrchestratorState.RunningCategory);
            _context.ResetPhaseTimer();
        }

        private static void AdvanceToNextCategoryOrFinish(int completedIndex)
        {
            var nextIndex = completedIndex + 1;

            if (nextIndex < _categoryRunners.Length)
            {
                BeginCategory(nextIndex);
                return;
            }

            LogFinalBreakdown();
            Finish();
        }

        private static void LogFinalBreakdown()
        {
            var totalDuration = BenchmarkCategoryTimeTracker.GetTotalDurationFromAllCategories();

            Debug.Log("<color=red>Finished benchmark...</color>");
            Debug.Log($"Benchmark total time: {totalDuration}");
            Debug.Log(BuildCategoryBreakdownLog(BenchmarkCategoryTimeTracker.GetAllAverages()));
        }

        /// <summary>
        /// Logs each category's <em>average</em> duration (see <see cref="BenchmarkCategoryTimeTracker.GetAverage"/>),
        /// not its raw total - categories run a different number of times each (e.g. 3 play mode switches vs 1
        /// build by default), which would otherwise make their raw totals misleading to compare directly, both
        /// against each other and against <see cref="BenchmarkCategoryTimeTracker.GetDurationColor"/>'s color
        /// coding.
        /// </summary>
        private static string BuildCategoryBreakdownLog(IReadOnlyDictionary<BenchmarkCategory, TimeSpan> averages)
        {
            var minSeconds = averages.Values.Min(t => t.TotalSeconds);
            var maxSeconds = averages.Values.Max(t => t.TotalSeconds);

            var builder = new StringBuilder();
            builder.AppendLine("Category breakdown (average per run):");

            foreach (BenchmarkCategory category in Enum.GetValues(typeof(BenchmarkCategory)))
            {
                var average = averages[category];
                var color = BenchmarkCategoryTimeTracker.GetDurationColor(average.TotalSeconds, minSeconds, maxSeconds);
                var colorHex = ColorUtility.ToHtmlStringRGB(color);
                builder.AppendLine($"  <color=#{colorHex}>{category,-16} {average:hh\\:mm\\:ss\\.fff}</color>");
            }

            return builder.ToString();
        }

        /// <summary>
        /// Best-effort cleanup for a benchmark ending before it reaches its normal final state (via
        /// <see cref="StopBenchmark"/> or a category reporting <see cref="BenchmarkCategoryTickResult.Failed"/>).
        /// Calls <see cref="IBenchmarkCategoryRunner.Abort"/> on every category unconditionally (not just the
        /// currently active one) - every implementation is required to treat that as a safe no-op if it never
        /// started, or had already finished (and cleaned up after itself) normally, so this is safe to call
        /// regardless of which category was active when the run stopped.
        /// </summary>
        private static void AbortBenchmark()
        {
            foreach (var runner in _categoryRunners)
            {
                runner.Abort();
            }

            SessionState.SetBool(_headlessAbortedKey, true);
            Finish();
        }

        private static void Finish()
        {
            SetState(OrchestratorState.None);
            EditorApplication.update -= Step;

            BenchmarkFinished?.Invoke();

            if (SessionState.GetBool(_headlessKey, false))
            {
                var aborted = SessionState.GetBool(_headlessAbortedKey, false);
                SessionState.SetBool(_headlessKey, false);
                SessionState.SetBool(_headlessAbortedKey, false);

                Debug.Log(aborted
                    ? "<color=orange>Benchmark aborted; exiting editor with exit code 1.</color>"
                    : "<color=lime>Benchmark finished; exiting editor.</color>");

                EditorApplication.Exit(aborted ? 1 : 0);
            }
        }

        private static int ClampToAtLeastOne(int value, string parameterName)
        {
            if (value < 1)
            {
                Debug.LogWarning($"{parameterName} must be at least 1, got {value}. Using 1 instead.");
                return 1;
            }

            return value;
        }

        private static OrchestratorState GetState()
        {
            return (OrchestratorState) SessionState.GetInt(_stateKey, (int) OrchestratorState.None);
        }

        private static void SetState(OrchestratorState state)
        {
            SessionState.SetInt(_stateKey, (int) state);
        }

        private static int GetCurrentCategoryIndex()
        {
            return SessionState.GetInt(_currentCategoryIndexKey, 0);
        }

        private static void SetCurrentCategoryIndex(int index)
        {
            SessionState.SetInt(_currentCategoryIndexKey, index);
        }

        private static int GetRunCount(BenchmarkCategory category)
        {
            return SessionState.GetInt(_runCountKeyPrefix + category, 1);
        }

        private static void SetRunCount(BenchmarkCategory category, int count)
        {
            SessionState.SetInt(_runCountKeyPrefix + category, count);
        }

        private static bool TryDisableConsoleClearOnPlay()
        {
            var assembly = typeof(EditorWindow).Assembly;
            var consoleWindowType = assembly.GetType("UnityEditor.ConsoleWindow");
            var field = consoleWindowType.GetField("ms_ConsoleWindow", BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null)
            {
                return false;
            }

            var consoleInstance = field.GetValue(null);
            if (consoleInstance == null)
            {
                return false;
            }

            var clearOnPlayField =
                consoleWindowType.GetField("m_ClearOnPlay", BindingFlags.Instance | BindingFlags.NonPublic);
            if (clearOnPlayField == null)
            {
                return false;
            }

            clearOnPlayField.SetValue(consoleInstance, false);
            return true;
        }
    }
}
