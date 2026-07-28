using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditorDevelopmentBenchmark.Editor.Util;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace UnityEditorDevelopmentBenchmark.Editor.Benchmarking.Categories
{
    /// <summary>
    /// Builds a player, one or more times, for the currently selected active build target
    /// (<see cref="EditorUserBuildSettings.activeBuildTarget"/>), using the scenes currently enabled in Build
    /// Settings, into a temporary directory next to (not inside) the project's "Assets" folder. Skipped if no
    /// scenes are enabled in Build Settings. A single failed build logs a warning and ends this category early
    /// (moving on to the next category) rather than aborting the whole benchmark run.
    /// </summary>
    public sealed class BuildBenchmarkCategoryRunner : IBenchmarkCategoryRunner
    {
        private enum Step
        {
            Preparing,
            Requesting,
            WaitingToSettle,
            CleaningUp
        }

        private const string _keyPrefix =
            "UnityEditorDevelopmentBenchmark.Benchmarking.Categories.BuildBenchmarkCategoryRunner";

        private const string _stepKey = _keyPrefix + ".Step";
        private const string _buildFolderPathKey = _keyPrefix + ".BuildFolderPath";

        /// <summary>
        /// Name of the temporary build output directory, created as a sibling of (not nested inside) the
        /// project's "Assets" folder, since player builds don't belong in the asset database.
        /// </summary>
        private const string _buildFolderName = "BenchmarkBuildsTemp";

        private readonly PersistentRunCounter _runCounter = new PersistentRunCounter(_keyPrefix, defaultCount: 1);

        public BenchmarkCategory Category => BenchmarkCategory.Build;

        public void Begin(int runCount)
        {
            _runCounter.Count = runCount;
            _runCounter.Iteration = 0;

            SetStep(Step.Preparing);
        }

        public BenchmarkCategoryTickResult Tick(IBenchmarkRunnerContext context)
        {
            return GetStep() switch
            {
                Step.Preparing => TickPreparing(context),
                Step.Requesting => TickRequesting(context),
                Step.WaitingToSettle => TickWaitingToSettle(context),
                Step.CleaningUp => TickCleaningUp(),
                _ => BenchmarkCategoryTickResult.Failed
            };
        }

        public void Abort()
        {
            CleanupBuildFolder();
        }

        private static BenchmarkCategoryTickResult TickPreparing(IBenchmarkRunnerContext context)
        {
            var scenePaths = GetEnabledBuildScenePaths();
            if (scenePaths.Length == 0)
            {
                Debug.LogWarning("Skipping Build benchmark category: no scenes are enabled in Build Settings.");
                return BenchmarkCategoryTickResult.Skipped;
            }

            var buildFolderPath = EnsureBuildFolderExists();
            SessionState.SetString(_buildFolderPathKey, buildFolderPath);

            TransitionTo(Step.Requesting, context);
            return BenchmarkCategoryTickResult.InProgress;
        }

        private BenchmarkCategoryTickResult TickRequesting(IBenchmarkRunnerContext context)
        {
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            var buildFolderPath = SessionState.GetString(_buildFolderPathKey, string.Empty);

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = GetEnabledBuildScenePaths(),
                locationPathName = GetBuildLocationPathName(buildFolderPath, buildTarget),
                target = buildTarget,
                targetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget),
                options = BuildOptions.None
            };

            BenchmarkCategoryTimeTracker.Start(BenchmarkCategory.Build);
            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            var buildElapsed = BenchmarkCategoryTimeTracker.Stop(BenchmarkCategory.Build);

            _runCounter.Iteration++;

            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogWarning($"Build did not succeed (result: {report.summary.result}); aborting Build " +
                                  "benchmark category early.");
                TransitionTo(Step.CleaningUp, context);
                return BenchmarkCategoryTickResult.InProgress;
            }

            Debug.Log($"Build finished ({_runCounter.Iteration}/{_runCounter.Count}), took {buildElapsed}.");

            TransitionTo(Step.WaitingToSettle, context);
            return BenchmarkCategoryTickResult.InProgress;
        }

        private BenchmarkCategoryTickResult TickWaitingToSettle(IBenchmarkRunnerContext context)
        {
            // A player build can incidentally trigger (or itself involve) script compilation. Wait for that to
            // settle before requesting the next build run so they don't overlap.
            if (EditorApplication.isCompiling)
            {
                if (context.HasPhaseTimedOut())
                {
                    Debug.LogWarning("Timeout while waiting for compilation triggered by build to finish.");
                    return BenchmarkCategoryTickResult.Failed;
                }

                return BenchmarkCategoryTickResult.InProgress;
            }

            if (_runCounter.Iteration < _runCounter.Count)
            {
                TransitionTo(Step.Requesting, context);
                return BenchmarkCategoryTickResult.InProgress;
            }

            TransitionTo(Step.CleaningUp, context);
            return BenchmarkCategoryTickResult.InProgress;
        }

        private static BenchmarkCategoryTickResult TickCleaningUp()
        {
            CleanupBuildFolder();
            return BenchmarkCategoryTickResult.Completed;
        }

        /// <summary>
        /// Paths (relative to the project, e.g. "Assets/Scenes/Main.unity") of every scene currently enabled in
        /// Build Settings, in the order they're listed there.
        /// </summary>
        private static string[] GetEnabledBuildScenePaths()
        {
            return EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
        }

        /// <summary>
        /// Directory the benchmark builds into, created as a sibling of (not nested inside) the project's
        /// "Assets" folder, next to "Library", "Packages" and "ProjectSettings".
        /// </summary>
        private static string GetBuildFolderPath()
        {
            var projectRootPath = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRootPath, _buildFolderName);
        }

        private static string EnsureBuildFolderExists()
        {
            var folderPath = GetBuildFolderPath();

            // In case a previous run's cleanup failed to run (e.g. the editor crashed mid-benchmark), make sure
            // we start from a clean slate rather than building on top of leftover output.
            if (Directory.Exists(folderPath))
            {
                Directory.Delete(folderPath, true);
            }

            Directory.CreateDirectory(folderPath);
            return folderPath;
        }

        /// <summary>A safe no-op if no build folder was ever created (or it was already cleaned up).</summary>
        private static void CleanupBuildFolder()
        {
            var buildFolderPath = SessionState.GetString(_buildFolderPathKey, string.Empty);
            SessionState.EraseString(_buildFolderPathKey);

            if (!string.IsNullOrEmpty(buildFolderPath) && Directory.Exists(buildFolderPath))
            {
                Directory.Delete(buildFolderPath, true);
            }
        }

        /// <summary>
        /// Full output path (file or directory, depending on <paramref name="buildTarget"/>) to pass as
        /// <see cref="BuildPlayerOptions.locationPathName"/> for a build into <paramref name="buildFolderPath"/>.
        /// </summary>
        private static string GetBuildLocationPathName(string buildFolderPath, BuildTarget buildTarget)
        {
            const string buildName = "Benchmark";

            switch (buildTarget)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return Path.Combine(buildFolderPath, buildName + ".exe");

                case BuildTarget.StandaloneOSX:
                    return Path.Combine(buildFolderPath, buildName + ".app");

                case BuildTarget.Android:
                    return Path.Combine(buildFolderPath,
                        EditorUserBuildSettings.exportAsGoogleAndroidProject ? buildName : buildName + ".apk");

                default:
                    // Covers targets that build into a bare file or directory name (e.g. StandaloneLinux64, iOS's
                    // Xcode project, WebGL's output folder).
                    return Path.Combine(buildFolderPath, buildName);
            }
        }

        private static void TransitionTo(Step step, IBenchmarkRunnerContext context)
        {
            SetStep(step);
            context.ResetPhaseTimer();
        }

        private static Step GetStep()
        {
            return (Step) SessionState.GetInt(_stepKey, (int) Step.Preparing);
        }

        private static void SetStep(Step step)
        {
            SessionState.SetInt(_stepKey, (int) step);
        }
    }
}
