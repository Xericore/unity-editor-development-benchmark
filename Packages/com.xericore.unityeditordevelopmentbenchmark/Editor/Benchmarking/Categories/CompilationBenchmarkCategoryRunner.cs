using UnityEditor;
using UnityEditor.Compilation;
using UnityEditorDevelopmentBenchmark.Editor.Util;
using Debug = UnityEngine.Debug;

namespace UnityEditorDevelopmentBenchmark.Editor.Benchmarking.Categories
{
    /// <summary>
    /// Forces full script recompilations, one after another, timing each one via
    /// <see cref="BenchmarkCategoryTimeTracker"/>. Domain reload time triggered by these recompilations is
    /// recorded separately, by <see cref="BenchmarkRunner"/>'s own always-on
    /// <see cref="UnityEditorDevelopmentBenchmark.Editor.Util.AssemblyReloadTimer"/> listener - not by this
    /// category - since it isn't something this category can request as a separate, timeable step.
    /// </summary>
    public sealed class CompilationBenchmarkCategoryRunner : IBenchmarkCategoryRunner
    {
        private enum Step
        {
            WaitingBeforeRun,
            Requesting,
            WaitingForCompilationToStart,
            WaitingForCompilationToFinish
        }

        private const string _keyPrefix =
            "UnityEditorDevelopmentBenchmark.Benchmarking.Categories.CompilationBenchmarkCategoryRunner";

        private const string _stepKey = _keyPrefix + ".Step";

        private readonly PersistentRunCounter _runCounter = new PersistentRunCounter(_keyPrefix, defaultCount: 1);

        public BenchmarkCategory Category => BenchmarkCategory.Compilation;

        public void Begin(int runCount)
        {
            _runCounter.Count = runCount;
            _runCounter.Iteration = 0;

            SetStep(Step.WaitingBeforeRun);
        }

        public BenchmarkCategoryTickResult Tick(IBenchmarkRunnerContext context)
        {
            return GetStep() switch
            {
                Step.WaitingBeforeRun => TickWaitingBeforeRun(context),
                Step.Requesting => TickRequesting(context),
                Step.WaitingForCompilationToStart => TickWaitingForCompilationToStart(context),
                Step.WaitingForCompilationToFinish => TickWaitingForCompilationToFinish(context),
                _ => BenchmarkCategoryTickResult.Failed
            };
        }

        public void Abort()
        {
            // Nothing to clean up: forcing a recompile has no side effects that need undoing, and an in-flight
            // compilation isn't something we can (or need to) cancel.
        }

        private static BenchmarkCategoryTickResult TickWaitingBeforeRun(IBenchmarkRunnerContext context)
        {
            // Give other tools that read Library/Bee/fullprofile.json in response to the previous compilation
            // (e.g. the Compilation Visualizer package) a moment to finish, so our next forced recompile doesn't
            // start rewriting/locking the file out from under them and cause a sharing violation.
            if (context.ElapsedPhaseTime < context.PreparationDelaySeconds)
            {
                return BenchmarkCategoryTickResult.InProgress;
            }

            TransitionTo(Step.Requesting, context);
            return BenchmarkCategoryTickResult.InProgress;
        }

        private static BenchmarkCategoryTickResult TickRequesting(IBenchmarkRunnerContext context)
        {
            BenchmarkCategoryTimeTracker.Start(BenchmarkCategory.Compilation);
            RequestScriptCompilation();

            TransitionTo(Step.WaitingForCompilationToStart, context);
            return BenchmarkCategoryTickResult.InProgress;
        }

        private static BenchmarkCategoryTickResult TickWaitingForCompilationToStart(IBenchmarkRunnerContext context)
        {
            if (EditorApplication.isCompiling)
            {
                TransitionTo(Step.WaitingForCompilationToFinish, context);
                return BenchmarkCategoryTickResult.InProgress;
            }

            if (context.HasPhaseTimedOut())
            {
                Debug.LogWarning("Timeout while waiting for compilation to start.");
                return BenchmarkCategoryTickResult.Failed;
            }

            return BenchmarkCategoryTickResult.InProgress;
        }

        private BenchmarkCategoryTickResult TickWaitingForCompilationToFinish(IBenchmarkRunnerContext context)
        {
            if (EditorApplication.isCompiling)
            {
                if (context.HasPhaseTimedOut())
                {
                    Debug.LogWarning("Timeout while waiting for compilation to finish.");
                    return BenchmarkCategoryTickResult.Failed;
                }

                return BenchmarkCategoryTickResult.InProgress;
            }

            var compilationElapsed = BenchmarkCategoryTimeTracker.Stop(BenchmarkCategory.Compilation);

            _runCounter.Iteration++;

            Debug.Log($"Compilation finished ({_runCounter.Iteration}/{_runCounter.Count}), took {compilationElapsed}.");

            if (_runCounter.Iteration < _runCounter.Count)
            {
                TransitionTo(Step.WaitingBeforeRun, context);
                return BenchmarkCategoryTickResult.InProgress;
            }

            return BenchmarkCategoryTickResult.Completed;
        }

        private static void RequestScriptCompilation()
        {
#if UNITY_2021_1_OR_NEWER
            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
#elif UNITY_2019_3_OR_NEWER
            CompilationPipeline.RequestScriptCompilation();
#endif
        }

        private static void TransitionTo(Step step, IBenchmarkRunnerContext context)
        {
            SetStep(step);
            context.ResetPhaseTimer();
        }

        private static Step GetStep()
        {
            return (Step) SessionState.GetInt(_stepKey, (int) Step.WaitingBeforeRun);
        }

        private static void SetStep(Step step)
        {
            SessionState.SetInt(_stepKey, (int) step);
        }
    }
}
