using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEditorDevelopmentBenchmark.Editor.Util;
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
        /// <summary>
        /// Fired whenever a benchmark run ends, whether it completed normally or was aborted early (e.g. due to a
        /// timeout). Intended for UI (such as <see cref="BenchmarkRunnerEditorWindow"/>) that wants to refresh
        /// itself as soon as fresh results are available, rather than only by polling <see cref="IsRunning"/>.
        /// </summary>
        public static event Action BenchmarkFinished;

        /// <summary>
        /// Whether a benchmark run is currently in progress (as opposed to <see cref="BenchmarkState.None"/>).
        /// </summary>
        public static bool IsRunning => GetState() != BenchmarkState.None;

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
            PreparingLightmapBake,
            RequestingLightmapBake,
            CleaningUpLightmapBake,
            PreparingBuild,
            RequestingBuild,
            WaitingForBuildToSettle,
            CleaningUpBuild,
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
        private const string _lightmapBakeRunCountKey = "UnityEditorDevelopmentBenchmark.BenchmarkRunner.LightmapBakeRunCount";
        private const string _lightmapBakeRunIterationKey = "UnityEditorDevelopmentBenchmark.BenchmarkRunner.LightmapBakeRunIteration";
        private const string _lightmapOriginalScenePathsKey = "UnityEditorDevelopmentBenchmark.BenchmarkRunner.LightmapOriginalScenePaths";
        private const string _lightmapTempFolderPathKey = "UnityEditorDevelopmentBenchmark.BenchmarkRunner.LightmapTempFolderPath";
        private const string _buildRunCountKey = "UnityEditorDevelopmentBenchmark.BenchmarkRunner.BuildRunCount";
        private const string _buildRunIterationKey = "UnityEditorDevelopmentBenchmark.BenchmarkRunner.BuildRunIteration";
        private const string _buildFolderPathKey = "UnityEditorDevelopmentBenchmark.BenchmarkRunner.BuildFolderPath";

        private const int _defaultPlayModeSwitchCount = 3;
        private const int _defaultCompilationRunCount = 3;
        private const int _defaultAssetImportRunCount = 3;
        private const int _defaultLightmapBakeRunCount = 2;
        private const int _defaultBuildRunCount = 1;

        private const string _assetsFolderPath = "Assets";
        private const string _lightmapBenchmarkTempFolderPath = "Assets/Temp/LightmapBenchmarkTemp";

        /// <summary>
        /// Name of the temporary build output directory, created as a sibling of (not nested inside) the
        /// project's "Assets" folder, since player builds don't belong in the asset database.
        /// </summary>
        private const string _buildBenchmarkFolderName = "BenchmarkBuildsTemp";

        private const float _maxLoopTimeInSeconds = 10f;
        private const float _preparationDelayInSeconds = 1f;

        static BenchmarkRunner()
        {
            // Subscribed unconditionally (not just while a benchmark is in progress) and re-attached after every
            // domain reload, since AssemblyReloadTimer.Updated is itself what fires *because of* the domain
            // reload we want to measure; OnAssemblyReloadTimerUpdated discards the event if no benchmark is
            // currently running.
            AssemblyReloadTimer.Updated += OnAssemblyReloadTimerUpdated;

            // Re-attach after every domain reload (including the one triggered by EnterPlaymode) if a benchmark
            // is currently in progress.
            if (GetState() != BenchmarkState.None)
            {
                EditorApplication.update += Step;
            }
        }

        /// <summary>
        /// Domain reloads are triggered by Unity itself as part of the forced recompilations already requested by
        /// <see cref="StepRequestingCompilation"/>, rather than something this runner can request separately, so
        /// the <see cref="BenchmarkCategory.DomainReload"/> category is recorded by simply listening for
        /// <see cref="AssemblyReloadTimer"/> to reconstruct each domain reload's duration from the Bee profiler
        /// trace, whenever that happens to fire while a benchmark is in progress.
        /// </summary>
        private static void OnAssemblyReloadTimerUpdated()
        {
            if (GetState() == BenchmarkState.None)
            {
                return;
            }

            var domainReloadDuration = AssemblyReloadTimer.AssemblyReloadDuration;
            BenchmarkCategoryTimeTracker.AddDuration(BenchmarkCategory.DomainReload, domainReloadDuration);

            Debug.Log($"Domain reload finished, took {domainReloadDuration}.");
        }

        [MenuItem("Window/Analysis/Start Benchmark")]
        [UsedImplicitly]
        public static void StartBenchmark()
        {
            StartBenchmark(_defaultPlayModeSwitchCount, _defaultCompilationRunCount, _defaultAssetImportRunCount,
                _defaultLightmapBakeRunCount, _defaultBuildRunCount);
        }

        /// <param name="playModeSwitchCount">
        /// How many times to enter and exit play mode. Defaults to 3 when invoked from the menu; pass explicitly
        /// when invoking from the command line via -executeMethod.
        /// </param>
        [UsedImplicitly]
        public static void StartBenchmark(int playModeSwitchCount)
        {
            StartBenchmark(playModeSwitchCount, _defaultCompilationRunCount, _defaultAssetImportRunCount,
                _defaultLightmapBakeRunCount, _defaultBuildRunCount);
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
            StartBenchmark(playModeSwitchCount, compilationRunCount, _defaultAssetImportRunCount,
                _defaultLightmapBakeRunCount, _defaultBuildRunCount);
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
            StartBenchmark(playModeSwitchCount, compilationRunCount, assetImportRunCount,
                _defaultLightmapBakeRunCount, _defaultBuildRunCount);
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
        /// <param name="lightmapBakeRunCount">
        /// How many times to bake lightmaps for the scene assigned to "Lightmap Benchmark Scene" in
        /// Project Settings &gt; Development Benchmark. Defaults to 2. Ignored (the category is skipped) if no
        /// scene is assigned.
        /// </param>
        [UsedImplicitly]
        public static void StartBenchmark(int playModeSwitchCount, int compilationRunCount, int assetImportRunCount,
            int lightmapBakeRunCount)
        {
            StartBenchmark(playModeSwitchCount, compilationRunCount, assetImportRunCount, lightmapBakeRunCount,
                _defaultBuildRunCount);
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
        /// <param name="lightmapBakeRunCount">
        /// How many times to bake lightmaps for the scene assigned to "Lightmap Benchmark Scene" in
        /// Project Settings &gt; Development Benchmark. Defaults to 2. Ignored (the category is skipped) if no
        /// scene is assigned.
        /// </param>
        /// <param name="buildRunCount">
        /// How many times to build a player for the currently selected active build target
        /// (<see cref="EditorUserBuildSettings.activeBuildTarget"/>), using the scenes currently enabled in Build
        /// Settings, into a temporary directory next to (not inside) "Assets". Defaults to 1. Ignored (the
        /// category is skipped) if no scenes are enabled in Build Settings.
        /// </param>
        [UsedImplicitly]
        public static void StartBenchmark(int playModeSwitchCount, int compilationRunCount, int assetImportRunCount,
            int lightmapBakeRunCount, int buildRunCount)
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

            if (lightmapBakeRunCount < 1)
            {
                Debug.LogWarning($"lightmapBakeRunCount must be at least 1, got {lightmapBakeRunCount}. Using 1 instead.");
                lightmapBakeRunCount = 1;
            }

            if (buildRunCount < 1)
            {
                Debug.LogWarning($"buildRunCount must be at least 1, got {buildRunCount}. Using 1 instead.");
                buildRunCount = 1;
            }

            if (!TryDisableConsoleClearOnPlay())
            {
                Debug.LogWarning("Couldn't disable console clear on play. This may cause the benchmark to not show all logs. Please disable it manually in the Console window settings.");
            }

            Debug.Log($"<color=lime>Starting benchmark ({compilationRunCount} compilation run(s), {assetImportRunCount} asset import run(s), {lightmapBakeRunCount} lightmap bake run(s), {buildRunCount} build run(s), {playModeSwitchCount} play mode switch(es))...</color>");

            BenchmarkCategoryTimeTracker.Reset(BenchmarkCategory.PlayModeSwitch);
            BenchmarkCategoryTimeTracker.Reset(BenchmarkCategory.Compilation);
            BenchmarkCategoryTimeTracker.Reset(BenchmarkCategory.DomainReload);
            BenchmarkCategoryTimeTracker.Reset(BenchmarkCategory.AssetImport);
            BenchmarkCategoryTimeTracker.Reset(BenchmarkCategory.Build);
            BenchmarkCategoryTimeTracker.Reset(BenchmarkCategory.LightmapBaking);

            SetPlayModeSwitchCount(playModeSwitchCount);
            SetPlayModeSwitchIteration(0);

            SetCompilationRunCount(compilationRunCount);
            SetCompilationRunIteration(0);

            SetAssetImportRunCount(assetImportRunCount);
            SetAssetImportRunIteration(0);

            SetLightmapBakeRunCount(lightmapBakeRunCount);
            SetLightmapBakeRunIteration(0);

            SetBuildRunCount(buildRunCount);
            SetBuildRunIteration(0);

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

                case BenchmarkState.PreparingLightmapBake:
                    StepPreparingLightmapBake();
                    break;

                case BenchmarkState.RequestingLightmapBake:
                    StepRequestingLightmapBake();
                    break;

                case BenchmarkState.CleaningUpLightmapBake:
                    StepCleaningUpLightmapBake();
                    break;

                case BenchmarkState.PreparingBuild:
                    StepPreparingBuild();
                    break;

                case BenchmarkState.RequestingBuild:
                    StepRequestingBuild();
                    break;

                case BenchmarkState.WaitingForBuildToSettle:
                    StepWaitingForBuildToSettle();
                    break;

                case BenchmarkState.CleaningUpBuild:
                    StepCleaningUpBuild();
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
            SaveAndCloseOpenScenes(_originalScenePathsKey);
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

            RestoreOpenScenes(_originalScenePathsKey);
            TransitionTo(BenchmarkState.PreparingLightmapBake);
        }

        private static void StepPreparingLightmapBake()
        {
            var settings = DevelopmentBenchmarkSettings.GetOrCreateSettings();
            var sceneAsset = settings.LightmapBenchmarkScene;

            if (sceneAsset == null)
            {
                Debug.LogWarning("Skipping Lightmap Baking benchmark category: no scene is assigned to \"Lightmap Benchmark Scene\" in Project Settings > Development Benchmark.");
                TransitionTo(BenchmarkState.Preparing);
                return;
            }

            if (!TrySetupLightmapBenchmarkScene(sceneAsset))
            {
                Debug.LogWarning("Skipping Lightmap Baking benchmark category: failed to prepare a temporary copy of the assigned benchmark scene.");
                TransitionTo(BenchmarkState.Preparing);
                return;
            }

            SetLightmapBakeRunIteration(0);
            TransitionTo(BenchmarkState.RequestingLightmapBake);
        }

        private static void StepRequestingLightmapBake()
        {
            BenchmarkCategoryTimeTracker.Start(BenchmarkCategory.LightmapBaking);
            Lightmapping.Bake();
            var lightmapBakeElapsed = BenchmarkCategoryTimeTracker.Stop(BenchmarkCategory.LightmapBaking);

            var iteration = GetLightmapBakeRunIteration() + 1;
            var count = GetLightmapBakeRunCount();
            SetLightmapBakeRunIteration(iteration);

            Debug.Log($"Lightmap baking finished ({iteration}/{count}), took {lightmapBakeElapsed}.");

            if (iteration < count)
            {
                TransitionTo(BenchmarkState.RequestingLightmapBake);
                return;
            }

            TransitionTo(BenchmarkState.CleaningUpLightmapBake);
        }

        private static void StepCleaningUpLightmapBake()
        {
            CleanupLightmapBenchmarkScene();
            TransitionTo(BenchmarkState.PreparingBuild);
        }

        private static void StepPreparingBuild()
        {
            var scenePaths = GetEnabledBuildScenePaths();
            if (scenePaths.Length == 0)
            {
                Debug.LogWarning("Skipping Build benchmark category: no scenes are enabled in Build Settings.");
                TransitionTo(BenchmarkState.Preparing);
                return;
            }

            var buildFolderPath = EnsureBuildBenchmarkTempFolderExists();
            SessionState.SetString(_buildFolderPathKey, buildFolderPath);

            SetBuildRunIteration(0);
            TransitionTo(BenchmarkState.RequestingBuild);
        }

        private static void StepRequestingBuild()
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

            var iteration = GetBuildRunIteration() + 1;
            var count = GetBuildRunCount();
            SetBuildRunIteration(iteration);

            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogWarning($"Build did not succeed (result: {report.summary.result}); aborting Build benchmark category early.");
                TransitionTo(BenchmarkState.CleaningUpBuild);
                return;
            }

            Debug.Log($"Build finished ({iteration}/{count}), took {buildElapsed}.");

            TransitionTo(BenchmarkState.WaitingForBuildToSettle);
        }

        private static void StepWaitingForBuildToSettle()
        {
            // A player build can incidentally trigger (or itself involve) script compilation. Wait for that to
            // settle before requesting the next build run so they don't overlap.
            if (EditorApplication.isCompiling)
            {
                if (HasPhaseTimedOut())
                {
                    Debug.LogWarning("Timeout while waiting for compilation triggered by build to finish.");
                    Abort();
                }

                return;
            }

            var iteration = GetBuildRunIteration();
            var count = GetBuildRunCount();

            if (iteration < count)
            {
                TransitionTo(BenchmarkState.RequestingBuild);
                return;
            }

            TransitionTo(BenchmarkState.CleaningUpBuild);
        }

        private static void StepCleaningUpBuild()
        {
            CleanupBuildBenchmarkFolder();
            TransitionTo(BenchmarkState.Preparing);
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
        private static string GetBuildBenchmarkTempFolderPath()
        {
            var projectRootPath = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRootPath, _buildBenchmarkFolderName);
        }

        private static string EnsureBuildBenchmarkTempFolderExists()
        {
            var folderPath = GetBuildBenchmarkTempFolderPath();

            // In case a previous run's cleanup failed to run (e.g. the editor crashed mid-benchmark), make sure
            // we start from a clean slate rather than building on top of leftover output.
            if (Directory.Exists(folderPath))
            {
                Directory.Delete(folderPath, true);
            }

            Directory.CreateDirectory(folderPath);
            return folderPath;
        }

        private static void CleanupBuildBenchmarkFolder()
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

        /// <summary>
        /// Creates a temporary copy of <paramref name="sceneAsset"/> under
        /// <see cref="_lightmapBenchmarkTempFolderPath"/> (a directory distinct from the original scene's
        /// directory) and opens it, so the lightmap data this benchmark run generates is written next to the
        /// temporary copy instead of polluting the original scene's own lightmap data directory. The originally
        /// open scene(s) are recorded so <see cref="CleanupLightmapBenchmarkScene"/> can restore them afterwards.
        /// </summary>
        private static bool TrySetupLightmapBenchmarkScene(SceneAsset sceneAsset)
        {
            var originalScenePath = AssetDatabase.GetAssetPath(sceneAsset);
            if (string.IsNullOrEmpty(originalScenePath))
            {
                return false;
            }

            var tempFolderPath = EnsureLightmapBenchmarkTempFolderExists();
            var tempScenePath = $"{tempFolderPath}/{Path.GetFileName(originalScenePath)}";

            // In case a previous run's cleanup failed to run (e.g. the editor crashed mid-benchmark), make sure
            // we start from a clean slate rather than failing to copy over a leftover temporary scene.
            AssetDatabase.DeleteAsset(tempScenePath);

            if (!AssetDatabase.CopyAsset(originalScenePath, tempScenePath))
            {
                return false;
            }

            SaveAndCloseOpenScenes(_lightmapOriginalScenePathsKey);
            EditorSceneManager.OpenScene(tempScenePath, OpenSceneMode.Single);

            SessionState.SetString(_lightmapTempFolderPathKey, tempFolderPath);

            return true;
        }

        /// <summary>
        /// Restores whatever scene(s) were open before <see cref="TrySetupLightmapBenchmarkScene"/>, then deletes
        /// the temporary scene copy together with the lightmap data directory Unity generated next to it.
        /// </summary>
        private static void CleanupLightmapBenchmarkScene()
        {
            var tempFolderPath = SessionState.GetString(_lightmapTempFolderPathKey, string.Empty);
            SessionState.EraseString(_lightmapTempFolderPathKey);

            RestoreOpenScenes(_lightmapOriginalScenePathsKey);

            if (!string.IsNullOrEmpty(tempFolderPath) && AssetDatabase.IsValidFolder(tempFolderPath))
            {
                AssetDatabase.DeleteAsset(tempFolderPath);
            }

            // Also remove the parent "Assets/Temp" folder (and its .meta) if we're the ones who created it and
            // nothing else has since put anything else in there, so it doesn't linger behind after the benchmark.
            var parentTempFolderPath = _assetsFolderPath + "/Temp";
            if (AssetDatabase.IsValidFolder(parentTempFolderPath) && IsFolderEmpty(parentTempFolderPath))
            {
                AssetDatabase.DeleteAsset(parentTempFolderPath);
            }

            AssetDatabase.Refresh();
        }

        private static string EnsureLightmapBenchmarkTempFolderExists()
        {
            if (!AssetDatabase.IsValidFolder(_assetsFolderPath + "/Temp"))
            {
                AssetDatabase.CreateFolder(_assetsFolderPath, "Temp");
            }

            if (!AssetDatabase.IsValidFolder(_lightmapBenchmarkTempFolderPath))
            {
                AssetDatabase.CreateFolder(_assetsFolderPath + "/Temp", "LightmapBenchmarkTemp");
            }

            return _lightmapBenchmarkTempFolderPath;
        }

        /// <summary>
        /// Whether <paramref name="assetsRelativeFolderPath"/> (a folder path relative to the project, e.g.
        /// "Assets/Temp") contains no files or subfolders. Checked directly on disk rather than via
        /// <see cref="AssetDatabase"/>, since an empty folder is still a valid tracked asset (with its own
        /// .meta file) but wouldn't be returned by an asset search under itself.
        /// </summary>
        private static bool IsFolderEmpty(string assetsRelativeFolderPath)
        {
            var relativeToAssets = assetsRelativeFolderPath.Substring(_assetsFolderPath.Length).TrimStart('/', '\\');
            var absolutePath = Path.Combine(Application.dataPath, relativeToAssets);

            return !Directory.Exists(absolutePath) || Directory.GetFileSystemEntries(absolutePath).Length == 0;
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
        /// Closes every currently open scene and records their paths (under <paramref name="sessionKey"/>) so
        /// <see cref="RestoreOpenScenes"/> can reopen them later. Used before operations (forcing an asset
        /// reimport, opening a temporary scene for the lightmap baking benchmark) that would otherwise cause Unity
        /// to detect the open scene file(s) as "changed on disk" and prompt the user with a blocking modal dialog
        /// asking whether to reload them.
        /// </summary>
        private static void SaveAndCloseOpenScenes(string sessionKey)
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

            SessionState.SetString(sessionKey, string.Join(";", openScenePaths));

            // Same prompt the user would get anyway when Unity is about to discard/replace the open scene(s).
            // We proceed with the benchmark regardless of the user's choice (save, don't save).
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        /// <summary>
        /// Reopens whatever scene(s) were open before the matching <see cref="SaveAndCloseOpenScenes"/> call
        /// (identified by <paramref name="sessionKey"/>) closed them.
        /// </summary>
        /// <remarks>
        /// Resolves any unsaved modifications on the currently open scene(s) first (same prompt-avoidance as
        /// <see cref="SaveAndCloseOpenScenes"/>). This matters in particular after lightmap baking, which leaves
        /// the temporary benchmark scene dirty: without this, <see cref="EditorSceneManager.OpenScene"/> below
        /// would silently fail to switch away from it (or block on a modal save prompt), so the temporary scene
        /// would still be the active one by the time its containing folder gets deleted in
        /// <see cref="CleanupLightmapBenchmarkScene"/> - and the next <see cref="SaveAndCloseOpenScenes"/> call
        /// would then record that now-deleted temporary scene's path as the "originally open" scene to restore,
        /// causing a "Scene file not found" error on the next benchmark run.
        /// </remarks>
        private static void RestoreOpenScenes(string sessionKey)
        {
            var joinedPaths = SessionState.GetString(sessionKey, string.Empty);
            SessionState.EraseString(sessionKey);

            if (string.IsNullOrEmpty(joinedPaths))
            {
                return;
            }

            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

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

            foreach (var (category, total) in totals.OrderBy(pair => pair.Key.ToString()))
            {
                var color = BenchmarkCategoryTimeTracker.GetDurationColor(total.TotalSeconds, minSeconds, maxSeconds);
                var colorHex = ColorUtility.ToHtmlStringRGB(color);
                builder.AppendLine($"  <color=#{colorHex}>{category,-16} {total:hh\\:mm\\:ss\\.fff}</color>");
            }

            return builder.ToString();
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

            BenchmarkFinished?.Invoke();
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

        private static int GetLightmapBakeRunCount()
        {
            return SessionState.GetInt(_lightmapBakeRunCountKey, _defaultLightmapBakeRunCount);
        }

        private static void SetLightmapBakeRunCount(int count)
        {
            SessionState.SetInt(_lightmapBakeRunCountKey, count);
        }

        private static int GetLightmapBakeRunIteration()
        {
            return SessionState.GetInt(_lightmapBakeRunIterationKey, 0);
        }

        private static void SetLightmapBakeRunIteration(int iteration)
        {
            SessionState.SetInt(_lightmapBakeRunIterationKey, iteration);
        }

        private static int GetBuildRunCount()
        {
            return SessionState.GetInt(_buildRunCountKey, _defaultBuildRunCount);
        }

        private static void SetBuildRunCount(int count)
        {
            SessionState.SetInt(_buildRunCountKey, count);
        }

        private static int GetBuildRunIteration()
        {
            return SessionState.GetInt(_buildRunIterationKey, 0);
        }

        private static void SetBuildRunIteration(int iteration)
        {
            SessionState.SetInt(_buildRunIterationKey, iteration);
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
