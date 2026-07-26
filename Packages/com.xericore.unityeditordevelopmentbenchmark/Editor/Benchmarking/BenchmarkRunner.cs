using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace UnityEditorDevelopmentBenchmark.Editor.Benchmarking
{
    /// <summary>
    /// To be called from the command line.
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
    [InitializeOnLoad]
    [UsedImplicitly]
    public static class BenchmarkRunner
    {
        private enum BenchmarkState
        {
            None,
            WaitingForInitialCompilation,
            WaitingBeforeCompilationRun,
            RequestingCompilation,
            WaitingForCompilationToStart,
            WaitingForCompilationToFinish,
            PreparingAssetImport,
            RequestingAssetImport,
            WaitingForAssetImportToSettle,
            Preparing,
            WaitingForPlayMode,
            WaitingForExitPlayMode
        }

        private const string _stateKey = "UnityEditorDevelopmentBenchmark.BenchmarkRunner.State";
        private const string _phaseStartTimeKey = "UnityEditorDevelopmentBenchmark.BenchmarkRunner.PhaseStartTime";
        private const string _playModeSwitchCountKey = "UnityEditorDevelopmentBenchmark.BenchmarkRunner.PlayModeSwitchCount";
        private const string _playModeSwitchIterationKey = "UnityEditorDevelopmentBenchmark.BenchmarkRunner.PlayModeSwitchIteration";
        private const string _compilationRunCountKey = "UnityEditorDevelopmentBenchmark.BenchmarkRunner.CompilationRunCount";
        private const string _compilationRunIterationKey = "UnityEditorDevelopmentBenchmark.BenchmarkRunner.CompilationRunIteration";
        private const string _assetImportRunCountKey = "UnityEditorDevelopmentBenchmark.BenchmarkRunner.AssetImportRunCount";
        private const string _assetImportRunIterationKey = "UnityEditorDevelopmentBenchmark.BenchmarkRunner.AssetImportRunIteration";
        private const string _originalScenePathsKey = "UnityEditorDevelopmentBenchmark.BenchmarkRunner.OriginalScenePaths";

        private const int _defaultPlayModeSwitchCount = 3;
        private const int _defaultCompilationRunCount = 3;
        private const int _defaultAssetImportRunCount = 3;

        private const string _assetsFolderPath = "Assets";

        private const float _maxLoopTimeInSeconds = 10f;
        private const float _preparationDelayInSeconds = 1f;

        static BenchmarkRunner()
        {
            // Re-attach after every domain reload (including the one triggered by EnterPlaymode) if a benchmark
            // is currently in progress.
            if (GetState() != BenchmarkState.None)
            {
                EditorApplication.update += Step;
            }
        }

        [MenuItem("Window/Analysis/Start Benchmark")]
        [UsedImplicitly]
        public static void StartBenchmark()
        {
            StartBenchmark(_defaultPlayModeSwitchCount, _defaultCompilationRunCount, _defaultAssetImportRunCount);
        }

        /// <param name="playModeSwitchCount">
        /// How many times to enter and exit play mode. Defaults to 3 when invoked from the menu; pass explicitly
        /// when invoking from the command line via -executeMethod.
        /// </param>
        [UsedImplicitly]
        public static void StartBenchmark(int playModeSwitchCount)
        {
            StartBenchmark(playModeSwitchCount, _defaultCompilationRunCount, _defaultAssetImportRunCount);
        }

        /// <param name="playModeSwitchCount">
        /// How many times to enter and exit play mode. Defaults to 3 when invoked from the menu; pass explicitly
        /// when invoking from the command line via -executeMethod.
        /// </param>
        /// <param name="compilationRunCount">
        /// How many times to force a full script recompilation (via
        /// <see cref="CompilationPipeline.RequestScriptCompilation()"/> with
        /// <see cref="RequestScriptCompilationOptions.CleanBuildCache"/> where available). Defaults to 3.
        /// </param>
        [UsedImplicitly]
        public static void StartBenchmark(int playModeSwitchCount, int compilationRunCount)
        {
            StartBenchmark(playModeSwitchCount, compilationRunCount, _defaultAssetImportRunCount);
        }

        /// <param name="playModeSwitchCount">
        /// How many times to enter and exit play mode. Defaults to 3 when invoked from the menu; pass explicitly
        /// when invoking from the command line via -executeMethod.
        /// </param>
        /// <param name="compilationRunCount">
        /// How many times to force a full script recompilation (via
        /// <see cref="CompilationPipeline.RequestScriptCompilation()"/> with
        /// <see cref="RequestScriptCompilationOptions.CleanBuildCache"/> where available). Defaults to 3.
        /// </param>
        /// <param name="assetImportRunCount">
        /// How many times to force a full reimport of everything under the "Assets" folder (never "Packages").
        /// Defaults to 3.
        /// </param>
        [UsedImplicitly]
        public static void StartBenchmark(int playModeSwitchCount, int compilationRunCount, int assetImportRunCount)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Cannot start benchmark while in play mode.");
                return;
            }

            if (GetState() != BenchmarkState.None)
            {
                Debug.LogWarning("A benchmark is already in progress.");
                return;
            }

            if (playModeSwitchCount < 1)
            {
                Debug.LogWarning($"playModeSwitchCount must be at least 1, got {playModeSwitchCount}. Using 1 instead.");
                playModeSwitchCount = 1;
            }

            if (compilationRunCount < 1)
            {
                Debug.LogWarning($"compilationRunCount must be at least 1, got {compilationRunCount}. Using 1 instead.");
                compilationRunCount = 1;
            }

            if (assetImportRunCount < 1)
            {
                Debug.LogWarning($"assetImportRunCount must be at least 1, got {assetImportRunCount}. Using 1 instead.");
                assetImportRunCount = 1;
            }

            if (!TryDisableConsoleClearOnPlay())
            {
                Debug.LogWarning("Couldn't disable console clear on play. This may cause the benchmark to not show all logs. Please disable it manually in the Console window settings.");
            }

            Debug.Log($"<color=lime>Starting benchmark ({compilationRunCount} compilation run(s), {assetImportRunCount} asset import run(s), {playModeSwitchCount} play mode switch(es))...</color>");

            BenchmarkCategoryTimeTracker.Reset(BenchmarkCategory.PlayModeSwitch);
            BenchmarkCategoryTimeTracker.Reset(BenchmarkCategory.Compilation);
            BenchmarkCategoryTimeTracker.Reset(BenchmarkCategory.AssetImport);

            SetPlayModeSwitchCount(playModeSwitchCount);
            SetPlayModeSwitchIteration(0);

            SetCompilationRunCount(compilationRunCount);
            SetCompilationRunIteration(0);

            SetAssetImportRunCount(assetImportRunCount);
            SetAssetImportRunIteration(0);

            SetState(BenchmarkState.WaitingForInitialCompilation);

            EditorApplication.update += Step;
        }

        private static void Step()
        {
            switch (GetState())
            {
                case BenchmarkState.WaitingForInitialCompilation:
                    StepWaitingForInitialCompilation();
                    break;

                case BenchmarkState.WaitingBeforeCompilationRun:
                    StepWaitingBeforeCompilationRun();
                    break;

                case BenchmarkState.RequestingCompilation:
                    StepRequestingCompilation();
                    break;

                case BenchmarkState.WaitingForCompilationToStart:
                    StepWaitingForCompilationToStart();
                    break;

                case BenchmarkState.WaitingForCompilationToFinish:
                    StepWaitingForCompilationToFinish();
                    break;

                case BenchmarkState.PreparingAssetImport:
                    StepPreparingAssetImport();
                    break;

                case BenchmarkState.RequestingAssetImport:
                    StepRequestingAssetImport();
                    break;

                case BenchmarkState.WaitingForAssetImportToSettle:
                    StepWaitingForAssetImportToSettle();
                    break;

                case BenchmarkState.Preparing:
                    StepPreparing();
                    break;

                case BenchmarkState.WaitingForPlayMode:
                    StepWaitingForPlayMode();
                    break;

                case BenchmarkState.WaitingForExitPlayMode:
                    StepWaitingForExitPlayMode();
                    break;

                case BenchmarkState.None:
                default:
                    EditorApplication.update -= Step;
                    break;
            }
        }

        private static void StepWaitingForInitialCompilation()
        {
            if (EditorApplication.isCompiling)
            {
                if (HasPhaseTimedOut())
                {
                    Debug.LogWarning("Timeout while waiting for compilation to finish.");
                    Abort();
                }

                return;
            }

            Debug.Log("Preparing benchmark...");
            TransitionTo(BenchmarkState.WaitingBeforeCompilationRun);
        }

        private static void StepWaitingBeforeCompilationRun()
        {
            // Give other tools that read Library/Bee/fullprofile.json in response to the previous compilation
            // (e.g. the Compilation Visualizer package) a moment to finish, so our next forced recompile doesn't
            // start rewriting/locking the file out from under them and cause a sharing violation.
            if (EditorApplication.timeSinceStartup - GetPhaseStartTime() < _preparationDelayInSeconds)
            {
                return;
            }

            TransitionTo(BenchmarkState.RequestingCompilation);
        }

        private static void StepRequestingCompilation()
        {
            BenchmarkCategoryTimeTracker.Start(BenchmarkCategory.Compilation);
            RequestScriptCompilation();

            TransitionTo(BenchmarkState.WaitingForCompilationToStart);
        }

        private static void StepWaitingForCompilationToStart()
        {
            if (EditorApplication.isCompiling)
            {
                TransitionTo(BenchmarkState.WaitingForCompilationToFinish);
                return;
            }

            if (HasPhaseTimedOut())
            {
                Debug.LogWarning("Timeout while waiting for compilation to start.");
                Abort();
            }
        }

        private static void StepWaitingForCompilationToFinish()
        {
            if (EditorApplication.isCompiling)
            {
                if (HasPhaseTimedOut())
                {
                    Debug.LogWarning("Timeout while waiting for compilation to finish.");
                    Abort();
                }

                return;
            }

            var compilationElapsed = BenchmarkCategoryTimeTracker.Stop(BenchmarkCategory.Compilation);

            var iteration = GetCompilationRunIteration() + 1;
            var count = GetCompilationRunCount();
            SetCompilationRunIteration(iteration);

            Debug.Log($"Compilation finished ({iteration}/{count}), took {compilationElapsed}.");

            if (iteration < count)
            {
                TransitionTo(BenchmarkState.WaitingBeforeCompilationRun);
                return;
            }

            TransitionTo(BenchmarkState.PreparingAssetImport);
        }

        private static void StepPreparingAssetImport()
        {
            CloseScenesForAssetImport();
            TransitionTo(BenchmarkState.RequestingAssetImport);
        }

        private static void RequestScriptCompilation()
        {
#if UNITY_2021_1_OR_NEWER
            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
#elif UNITY_2019_3_OR_NEWER
            CompilationPipeline.RequestScriptCompilation();
#endif
        }

        private static void StepRequestingAssetImport()
        {
            BenchmarkCategoryTimeTracker.Start(BenchmarkCategory.AssetImport);
            ReimportAssetsFolder();
            var assetImportElapsed = BenchmarkCategoryTimeTracker.Stop(BenchmarkCategory.AssetImport);

            var iteration = GetAssetImportRunIteration() + 1;
            var count = GetAssetImportRunCount();
            SetAssetImportRunIteration(iteration);

            Debug.Log($"Asset import finished ({iteration}/{count}), took {assetImportElapsed}.");

            TransitionTo(BenchmarkState.WaitingForAssetImportToSettle);
        }

        private static void StepWaitingForAssetImportToSettle()
        {
            // Reimporting assets can incidentally trigger script compilation (e.g. if a .cs file under "Assets"
            // got reimported), which in turn can trigger a domain reload. Wait for that to settle before
            // requesting the next asset import run so they don't overlap.
            if (EditorApplication.isCompiling)
            {
                if (HasPhaseTimedOut())
                {
                    Debug.LogWarning("Timeout while waiting for compilation triggered by asset import to finish.");
                    Abort();
                }

                return;
            }

            var iteration = GetAssetImportRunIteration();
            var count = GetAssetImportRunCount();

            if (iteration < count)
            {
                TransitionTo(BenchmarkState.RequestingAssetImport);
                return;
            }

            RestoreScenesAfterAssetImport();
            TransitionTo(BenchmarkState.Preparing);
        }

        /// <summary>
        /// Forces a full reimport of everything under the "Assets" folder, never touching "Packages".
        /// </summary>
        private static void ReimportAssetsFolder()
        {
            AssetDatabase.ImportAsset(_assetsFolderPath,
                ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate |
                ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>
        /// Closes every currently open scene before forcing a reimport, so Unity doesn't detect the open scene
        /// file(s) as "changed on disk" during the reimport and prompt the user with a blocking modal dialog
        /// asking whether to reload them.
        /// </summary>
        private static void CloseScenesForAssetImport()
        {
            var openScenePaths = new List<string>();
            for (var i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var path = EditorSceneManager.GetSceneAt(i).path;
                if (!string.IsNullOrEmpty(path))
                {
                    openScenePaths.Add(path);
                }
            }

            SessionState.SetString(_originalScenePathsKey, string.Join(";", openScenePaths));

            // Same prompt the user would get anyway when Unity is about to discard/replace the open scene(s).
            // We proceed with the benchmark regardless of the user's choice (save, don't save).
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        /// <summary>
        /// Reopens whatever scene(s) were open before <see cref="CloseScenesForAssetImport"/> closed them.
        /// </summary>
        private static void RestoreScenesAfterAssetImport()
        {
            var joinedPaths = SessionState.GetString(_originalScenePathsKey, string.Empty);
            SessionState.EraseString(_originalScenePathsKey);

            if (string.IsNullOrEmpty(joinedPaths))
            {
                return;
            }

            var paths = joinedPaths.Split(';');
            for (var i = 0; i < paths.Length; i++)
            {
                if (string.IsNullOrEmpty(paths[i]))
                {
                    continue;
                }

                EditorSceneManager.OpenScene(paths[i], i == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive);
            }
        }

        private static void StepPreparing()
        {
            if (EditorApplication.timeSinceStartup - GetPhaseStartTime() < _preparationDelayInSeconds)
            {
                return;
            }

            BenchmarkCategoryTimeTracker.Start(BenchmarkCategory.PlayModeSwitch);
            EditorApplication.EnterPlaymode();

            TransitionTo(BenchmarkState.WaitingForPlayMode);
        }

        private static void StepWaitingForPlayMode()
        {
            if (!EditorApplication.isPlaying)
            {
                if (HasPhaseTimedOut())
                {
                    Debug.LogWarning("Timeout while waiting for play mode to enter.");
                    Abort();
                }

                return;
            }

            Debug.Log("Entered play mode.");

            EditorApplication.ExitPlaymode();

            TransitionTo(BenchmarkState.WaitingForExitPlayMode);
        }

        private static void StepWaitingForExitPlayMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (HasPhaseTimedOut())
                {
                    Debug.LogWarning("Timeout while waiting for play mode to exit.");
                    Abort();
                }

                return;
            }

            var switchElapsed = BenchmarkCategoryTimeTracker.Stop(BenchmarkCategory.PlayModeSwitch);

            var iteration = GetPlayModeSwitchIteration() + 1;
            var count = GetPlayModeSwitchCount();
            SetPlayModeSwitchIteration(iteration);

            Debug.Log($"Exited play mode ({iteration}/{count}), took {switchElapsed}.");

            if (iteration < count)
            {
                TransitionTo(BenchmarkState.Preparing);
                return;
            }

            var totalDuration = BenchmarkCategoryTimeTracker.GetTotalDurationFromAllCategories();

            Debug.Log("<color=red>Finished benchmark...</color>");
            Debug.Log($"Benchmark total time: {totalDuration}");
            Debug.Log(BuildCategoryBreakdownLog(BenchmarkCategoryTimeTracker.GetAllTotals()));

            Finish();
        }

        private static string BuildCategoryBreakdownLog(IReadOnlyDictionary<BenchmarkCategory, TimeSpan> totals)
        {
            var minSeconds = totals.Values.Min(t => t.TotalSeconds);
            var maxSeconds = totals.Values.Max(t => t.TotalSeconds);

            var builder = new StringBuilder();
            builder.AppendLine("Category breakdown:");

            foreach (var (category, total) in totals.OrderByDescending(pair => pair.Value))
            {
                var color = GetDurationColor(total.TotalSeconds, minSeconds, maxSeconds);
                var colorHex = ColorUtility.ToHtmlStringRGB(color);
                builder.AppendLine($"  <color=#{colorHex}>{category,-16} {total:hh\\:mm\\:ss\\.fff}</color>");
            }

            return builder.ToString();
        }

        /// <summary>
        /// Lerps from green (shortest of the given categories) to red (longest), so durations can be compared
        /// visually relative to each other rather than against a fixed absolute scale.
        /// </summary>
        private static Color GetDurationColor(double seconds, double minSeconds, double maxSeconds)
        {
            if (Mathf.Approximately((float) minSeconds, (float) maxSeconds))
            {
                return Color.green;
            }

            var t = (float) ((seconds - minSeconds) / (maxSeconds - minSeconds));
            return Color.Lerp(Color.green, Color.red, t);
        }

        private static void TransitionTo(BenchmarkState state)
        {
            SetState(state);
            SetPhaseStartTime(EditorApplication.timeSinceStartup);
        }

        private static void Abort()
        {
            Finish();
        }

        private static void Finish()
        {
            SetState(BenchmarkState.None);
            EditorApplication.update -= Step;
        }

        private static bool HasPhaseTimedOut()
        {
            return EditorApplication.timeSinceStartup - GetPhaseStartTime() > _maxLoopTimeInSeconds;
        }

        private static BenchmarkState GetState()
        {
            return (BenchmarkState) SessionState.GetInt(_stateKey, (int) BenchmarkState.None);
        }

        private static void SetState(BenchmarkState state)
        {
            SessionState.SetInt(_stateKey, (int) state);
        }

        private static double GetPhaseStartTime()
        {
            return double.Parse(SessionState.GetString(_phaseStartTimeKey, "0"), CultureInfo.InvariantCulture);
        }

        private static void SetPhaseStartTime(double time)
        {
            SessionState.SetString(_phaseStartTimeKey, time.ToString(CultureInfo.InvariantCulture));
        }

        private static int GetPlayModeSwitchCount()
        {
            return SessionState.GetInt(_playModeSwitchCountKey, _defaultPlayModeSwitchCount);
        }

        private static void SetPlayModeSwitchCount(int count)
        {
            SessionState.SetInt(_playModeSwitchCountKey, count);
        }

        private static int GetPlayModeSwitchIteration()
        {
            return SessionState.GetInt(_playModeSwitchIterationKey, 0);
        }

        private static void SetPlayModeSwitchIteration(int iteration)
        {
            SessionState.SetInt(_playModeSwitchIterationKey, iteration);
        }

        private static int GetCompilationRunCount()
        {
            return SessionState.GetInt(_compilationRunCountKey, _defaultCompilationRunCount);
        }

        private static void SetCompilationRunCount(int count)
        {
            SessionState.SetInt(_compilationRunCountKey, count);
        }

        private static int GetCompilationRunIteration()
        {
            return SessionState.GetInt(_compilationRunIterationKey, 0);
        }

        private static void SetCompilationRunIteration(int iteration)
        {
            SessionState.SetInt(_compilationRunIterationKey, iteration);
        }

        private static int GetAssetImportRunCount()
        {
            return SessionState.GetInt(_assetImportRunCountKey, _defaultAssetImportRunCount);
        }

        private static void SetAssetImportRunCount(int count)
        {
            SessionState.SetInt(_assetImportRunCountKey, count);
        }

        private static int GetAssetImportRunIteration()
        {
            return SessionState.GetInt(_assetImportRunIterationKey, 0);
        }

        private static void SetAssetImportRunIteration(int iteration)
        {
            SessionState.SetInt(_assetImportRunIterationKey, iteration);
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
