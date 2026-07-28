using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorDevelopmentBenchmark.Editor.Util;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace UnityEditorDevelopmentBenchmark.Editor.Benchmarking.Categories
{
    /// <summary>
    /// Bakes lightmaps, one or more times, for the scene assigned to "Lightmap Benchmark Scene" in
    /// Project Settings &gt; Development Benchmark. Skipped if no scene is assigned, or if a temporary copy of it
    /// can't be prepared. The bake runs against a temporary copy of that scene (under
    /// <see cref="_tempFolderPath"/>, a directory distinct from the original scene's own directory), so the
    /// lightmap data this benchmark run generates doesn't pollute the original scene's own lightmap data
    /// directory; the copy (and its generated lightmap data) is deleted again once baking is done.
    /// </summary>
    public sealed class LightmapBakingBenchmarkCategoryRunner : IBenchmarkCategoryRunner
    {
        private enum Step
        {
            Preparing,
            Requesting,
            CleaningUp
        }

        private const string _keyPrefix =
            "UnityEditorDevelopmentBenchmark.Benchmarking.Categories.LightmapBakingBenchmarkCategoryRunner";

        private const string _stepKey = _keyPrefix + ".Step";
        private const string _originalScenePathsKey = _keyPrefix + ".OriginalScenePaths";
        private const string _tempFolderPathKey = _keyPrefix + ".TempFolderPath";

        private const string _assetsFolderPath = "Assets";
        private const string _tempFolderPath = "Assets/Temp/LightmapBenchmarkTemp";

        /// <summary>
        /// Appended to the original scene's file name (before its extension) for the temporary copy created by
        /// <see cref="TrySetupBenchmarkScene"/>, so it's clearly identifiable as a disposable benchmark copy
        /// rather than looking like the real scene (which it otherwise would, sitting under a different folder
        /// but with an identical name).
        /// </summary>
        private const string _tempSceneSuffix = "_temp_lightmapping";

        private readonly PersistentRunCounter _runCounter = new(_keyPrefix, defaultCount: 1);

        public BenchmarkCategory Category => BenchmarkCategory.LightmapBaking;

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
                Step.CleaningUp => TickCleaningUp(),
                _ => BenchmarkCategoryTickResult.Failed
            };
        }

        public void Abort()
        {
            CleanupBenchmarkScene();
        }

        private static BenchmarkCategoryTickResult TickPreparing(IBenchmarkRunnerContext context)
        {
            var settings = DevelopmentBenchmarkSettings.GetOrCreateSettings();
            var sceneAsset = settings.LightmapBenchmarkScene;

            if (sceneAsset == null)
            {
                Debug.LogWarning("Skipping Lightmap Baking benchmark category: no scene is assigned to " +
                                  "\"Lightmap Benchmark Scene\" in Project Settings > Development Benchmark.");
                return BenchmarkCategoryTickResult.Skipped;
            }

            if (!TrySetupBenchmarkScene(sceneAsset))
            {
                Debug.LogWarning("Skipping Lightmap Baking benchmark category: failed to prepare a temporary " +
                                  "copy of the assigned benchmark scene.");
                return BenchmarkCategoryTickResult.Skipped;
            }

            TransitionTo(Step.Requesting, context);
            return BenchmarkCategoryTickResult.InProgress;
        }

        private BenchmarkCategoryTickResult TickRequesting(IBenchmarkRunnerContext context)
        {
            BenchmarkCategoryTimeTracker.Start(BenchmarkCategory.LightmapBaking);
            Lightmapping.Bake();
            var lightmapBakeElapsed = BenchmarkCategoryTimeTracker.Stop(BenchmarkCategory.LightmapBaking);

            _runCounter.Iteration++;

            Debug.Log($"Lightmap baking finished ({_runCounter.Iteration}/{_runCounter.Count}), took {lightmapBakeElapsed}.");

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
            CleanupBenchmarkScene();
            return BenchmarkCategoryTickResult.Completed;
        }

        /// <summary>
        /// Creates a temporary copy of <paramref name="sceneAsset"/> under <see cref="_tempFolderPath"/> and
        /// opens it. The originally open scene(s) are recorded so <see cref="CleanupBenchmarkScene"/> can restore
        /// them afterwards.
        /// </summary>
        private static bool TrySetupBenchmarkScene(SceneAsset sceneAsset)
        {
            var originalScenePath = AssetDatabase.GetAssetPath(sceneAsset);
            if (string.IsNullOrEmpty(originalScenePath))
            {
                return false;
            }

            var tempFolderPath = EnsureTempFolderExists();
            var tempSceneFileName = Path.GetFileNameWithoutExtension(originalScenePath) + _tempSceneSuffix +
                                     Path.GetExtension(originalScenePath);
            var tempScenePath = $"{tempFolderPath}/{tempSceneFileName}";

            // In case a previous run's cleanup failed to run (e.g. the editor crashed mid-benchmark), make sure
            // we start from a clean slate rather than failing to copy over a leftover temporary scene.
            AssetDatabase.DeleteAsset(tempScenePath);

            if (!AssetDatabase.CopyAsset(originalScenePath, tempScenePath))
            {
                return false;
            }

            // Still closing the real scene at this point (the temporary copy doesn't exist yet), so prompt as
            // usual.
            EditorSceneStash.Stash(_originalScenePathsKey, promptToSaveModifiedScenes: true);
            EditorSceneManager.OpenScene(tempScenePath, OpenSceneMode.Single);

            SessionState.SetString(_tempFolderPathKey, tempFolderPath);

            return true;
        }

        /// <summary>
        /// Restores whatever scene(s) were open before <see cref="TrySetupBenchmarkScene"/>, then deletes the
        /// temporary scene copy together with the lightmap data directory Unity generated next to it. A safe
        /// no-op if <see cref="TrySetupBenchmarkScene"/> was never called (or already cleaned up) this run.
        /// </summary>
        private static void CleanupBenchmarkScene()
        {
            var tempFolderPath = SessionState.GetString(_tempFolderPathKey, string.Empty);
            SessionState.EraseString(_tempFolderPathKey);

            // The scene being closed here is the temporary lightmap benchmark copy (identifiable by its
            // "_temp_lightmapping" suffix), dirtied by Lightmapping.Bake() - not something the user edited, and
            // about to be deleted below regardless. Resolve its dirty state by saving it silently instead of
            // prompting, so the user isn't asked to save changes they never made (whether those changes end up
            // saved to the soon-to-be-deleted file or not makes no difference).
            EditorSceneStash.Restore(_originalScenePathsKey, promptToSaveModifiedScenes: false);

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

        private static string EnsureTempFolderExists()
        {
            if (!AssetDatabase.IsValidFolder(_assetsFolderPath + "/Temp"))
            {
                AssetDatabase.CreateFolder(_assetsFolderPath, "Temp");
            }

            if (!AssetDatabase.IsValidFolder(_tempFolderPath))
            {
                AssetDatabase.CreateFolder(_assetsFolderPath + "/Temp", "LightmapBenchmarkTemp");
            }

            return _tempFolderPath;
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
