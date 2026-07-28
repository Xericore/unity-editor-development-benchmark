using UnityEditor;
using UnityEditorDevelopmentBenchmark.Editor.Util;
using Debug = UnityEngine.Debug;

namespace UnityEditorDevelopmentBenchmark.Editor.Benchmarking.Categories
{
    /// <summary>
    /// Enters and exits play mode, one or more times, timing each switch via
    /// <see cref="BenchmarkCategoryTimeTracker"/>. Always the last category <see cref="BenchmarkRunner"/> runs;
    /// deliberately doesn't log the overall benchmark total/breakdown itself when its last iteration completes -
    /// that's the orchestrator's job once this (or whichever category ends up being the last one) reports
    /// <see cref="BenchmarkCategoryTickResult.Completed"/>.
    /// </summary>
    public sealed class PlayModeSwitchBenchmarkCategoryRunner : IBenchmarkCategoryRunner
    {
        private enum Step
        {
            Preparing,
            WaitingForPlayMode,
            WaitingForExitPlayMode
        }

        private const string _keyPrefix =
            "UnityEditorDevelopmentBenchmark.Benchmarking.Categories.PlayModeSwitchBenchmarkCategoryRunner";

        private const string _stepKey = _keyPrefix + ".Step";

        private readonly PersistentRunCounter _runCounter = new(_keyPrefix, defaultCount: 1);

        public BenchmarkCategory Category => BenchmarkCategory.PlayModeSwitch;

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
                Step.WaitingForPlayMode => TickWaitingForPlayMode(context),
                Step.WaitingForExitPlayMode => TickWaitingForExitPlayMode(context),
                _ => BenchmarkCategoryTickResult.Failed
            };
        }

        public void Abort()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.ExitPlaymode();
            }
        }

        private static BenchmarkCategoryTickResult TickPreparing(IBenchmarkRunnerContext context)
        {
            if (context.ElapsedPhaseTime < context.PreparationDelaySeconds)
            {
                return BenchmarkCategoryTickResult.InProgress;
            }

            BenchmarkCategoryTimeTracker.Start(BenchmarkCategory.PlayModeSwitch);
            EditorApplication.EnterPlaymode();

            TransitionTo(Step.WaitingForPlayMode, context);
            return BenchmarkCategoryTickResult.InProgress;
        }

        private static BenchmarkCategoryTickResult TickWaitingForPlayMode(IBenchmarkRunnerContext context)
        {
            if (!EditorApplication.isPlaying)
            {
                if (context.HasPhaseTimedOut())
                {
                    Debug.LogWarning("Timeout while waiting for play mode to enter.");
                    return BenchmarkCategoryTickResult.Failed;
                }

                return BenchmarkCategoryTickResult.InProgress;
            }

            Debug.Log("Entered play mode.");

            EditorApplication.ExitPlaymode();

            TransitionTo(Step.WaitingForExitPlayMode, context);
            return BenchmarkCategoryTickResult.InProgress;
        }

        private BenchmarkCategoryTickResult TickWaitingForExitPlayMode(IBenchmarkRunnerContext context)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (context.HasPhaseTimedOut())
                {
                    Debug.LogWarning("Timeout while waiting for play mode to exit.");
                    return BenchmarkCategoryTickResult.Failed;
                }

                return BenchmarkCategoryTickResult.InProgress;
            }

            var switchElapsed = BenchmarkCategoryTimeTracker.Stop(BenchmarkCategory.PlayModeSwitch);

            _runCounter.Iteration++;

            Debug.Log($"Exited play mode ({_runCounter.Iteration}/{_runCounter.Count}), took {switchElapsed}.");

            if (_runCounter.Iteration < _runCounter.Count)
            {
                TransitionTo(Step.Preparing, context);
                return BenchmarkCategoryTickResult.InProgress;
            }

            return BenchmarkCategoryTickResult.Completed;
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
