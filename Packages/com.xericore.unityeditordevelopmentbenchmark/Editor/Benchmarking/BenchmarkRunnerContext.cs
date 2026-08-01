using System.Globalization;
using UnityEditor;
using UnityEditorDevelopmentBenchmark.Editor.Benchmarking.Categories;

namespace UnityEditorDevelopmentBenchmark.Editor.Benchmarking
{
    /// <summary>
    /// <see cref="BenchmarkRunner"/>'s single implementation of <see cref="IBenchmarkRunnerContext"/>, wrapping
    /// the one shared <see cref="SessionState"/>-backed phase timer used both for top-level category transitions
    /// and for every category's own internal sub-step transitions - exactly one physical clock for the whole
    /// benchmark run, same as before the state machine was split across category classes.
    /// </summary>
    internal sealed class BenchmarkRunnerContext : IBenchmarkRunnerContext
    {
        private const string _phaseStartTimeKey = "UnityEditorDevelopmentBenchmark.BenchmarkRunner.PhaseStartTime";

        private const float _maxLoopTimeInSeconds = 600f;
        private const float _preparationDelayInSeconds = 1f;

        public float PreparationDelaySeconds => _preparationDelayInSeconds;

        public double ElapsedPhaseTime => EditorApplication.timeSinceStartup - GetPhaseStartTime();

        public bool HasPhaseTimedOut()
        {
            return ElapsedPhaseTime > _maxLoopTimeInSeconds;
        }

        public void ResetPhaseTimer()
        {
            SetPhaseStartTime(EditorApplication.timeSinceStartup);
        }

        private static double GetPhaseStartTime()
        {
            return double.Parse(SessionState.GetString(_phaseStartTimeKey, "0"), CultureInfo.InvariantCulture);
        }

        private static void SetPhaseStartTime(double time)
        {
            SessionState.SetString(_phaseStartTimeKey, time.ToString(CultureInfo.InvariantCulture));
        }
    }
}
