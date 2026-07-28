using UnityEditor;
using UnityEditorDevelopmentBenchmark.Editor.Util;
using Debug = UnityEngine.Debug;

namespace UnityEditorDevelopmentBenchmark.Editor.Benchmarking.Categories
{
    /// <summary>
    /// Forces full reimports of everything under the "Assets" folder (never "Packages"), one after another, timing
    /// each one via <see cref="BenchmarkCategoryTimeTracker"/>. Closes (and later restores) whatever scene(s) the
    /// user had open around the whole run, via <see cref="EditorSceneStash"/>, so Unity doesn't prompt to reload
    /// them as "changed on disk" partway through.
    /// </summary>
    public sealed class AssetImportBenchmarkCategoryRunner : IBenchmarkCategoryRunner
    {
        private enum Step
        {
            Preparing,
            Requesting,
            WaitingToSettle
        }

        private const string _keyPrefix =
            "UnityEditorDevelopmentBenchmark.Benchmarking.Categories.AssetImportBenchmarkCategoryRunner";

        private const string _stepKey = _keyPrefix + ".Step";
        private const string _originalScenePathsKey = _keyPrefix + ".OriginalScenePaths";
        private const string _assetsFolderPath = "Assets";

        private readonly PersistentRunCounter _runCounter = new PersistentRunCounter(_keyPrefix, defaultCount: 1);

        public BenchmarkCategory Category => BenchmarkCategory.AssetImport;

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
                _ => BenchmarkCategoryTickResult.Failed
            };
        }

        public void Abort()
        {
            // The scene(s) being restored here are whatever the user actually had open (the real scene), so
            // prompt to save as usual rather than silently discarding their changes. A no-op if this category
            // never started, or had already restored them normally.
            EditorSceneStash.Restore(_originalScenePathsKey, promptToSaveModifiedScenes: true);
        }

        private static BenchmarkCategoryTickResult TickPreparing(IBenchmarkRunnerContext context)
        {
            // The scene(s) being closed here are whatever the user actually had open (the real scene), so prompt
            // to save as usual rather than silently discarding their changes.
            EditorSceneStash.Stash(_originalScenePathsKey, promptToSaveModifiedScenes: true);

            TransitionTo(Step.Requesting, context);
            return BenchmarkCategoryTickResult.InProgress;
        }

        private BenchmarkCategoryTickResult TickRequesting(IBenchmarkRunnerContext context)
        {
            BenchmarkCategoryTimeTracker.Start(BenchmarkCategory.AssetImport);
            ReimportAssetsFolder();
            var assetImportElapsed = BenchmarkCategoryTimeTracker.Stop(BenchmarkCategory.AssetImport);

            _runCounter.Iteration++;

            Debug.Log($"Asset import finished ({_runCounter.Iteration}/{_runCounter.Count}), took {assetImportElapsed}.");

            TransitionTo(Step.WaitingToSettle, context);
            return BenchmarkCategoryTickResult.InProgress;
        }

        private BenchmarkCategoryTickResult TickWaitingToSettle(IBenchmarkRunnerContext context)
        {
            // Reimporting assets can incidentally trigger script compilation (e.g. if a .cs file under "Assets"
            // got reimported), which in turn can trigger a domain reload. Wait for that to settle before
            // requesting the next asset import run so they don't overlap.
            if (EditorApplication.isCompiling)
            {
                if (context.HasPhaseTimedOut())
                {
                    Debug.LogWarning("Timeout while waiting for compilation triggered by asset import to finish.");
                    return BenchmarkCategoryTickResult.Failed;
                }

                return BenchmarkCategoryTickResult.InProgress;
            }

            if (_runCounter.Iteration < _runCounter.Count)
            {
                TransitionTo(Step.Requesting, context);
                return BenchmarkCategoryTickResult.InProgress;
            }

            // Reopening the real scene here too, so prompt as usual.
            EditorSceneStash.Restore(_originalScenePathsKey, promptToSaveModifiedScenes: true);
            return BenchmarkCategoryTickResult.Completed;
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
