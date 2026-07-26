using System.Globalization;
using System.Reflection;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditorDevelopmentBenchmark.Editor.Util;
using Debug = UnityEngine.Debug;

namespace UnityEditorDevelopmentBenchmark.Editor
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
            WaitingForCompilation,
            Preparing,
            WaitingForPlayMode,
            WaitingForExitPlayMode
        }

        private const string _stateKey = "UnityEditorDevelopmentBenchmark.BenchmarkRunner.State";
        private const string _phaseStartTimeKey = "UnityEditorDevelopmentBenchmark.BenchmarkRunner.PhaseStartTime";
        private const string _benchmarkStartTimeKey = "UnityEditorDevelopmentBenchmark.BenchmarkRunner.BenchmarkStartTime";

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

            if (!TryDisableConsoleClearOnPlay())
            {
                Debug.LogWarning("Couldn't disable console clear on play. This may cause the benchmark to not show all logs. Please disable it manually in the Console window settings.");
            }

            Debug.Log("<color=lime>Starting benchmark...</color>");

            SetState(BenchmarkState.WaitingForCompilation);
            SetBenchmarkStartTime(EditorApplication.timeSinceStartup);

            EditorApplication.update += Step;
        }

        private static void Step()
        {
            switch (GetState())
            {
                case BenchmarkState.WaitingForCompilation:
                    StepWaitingForCompilation();
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

        private static void StepWaitingForCompilation()
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
            TransitionTo(BenchmarkState.Preparing);
        }

        private static void StepPreparing()
        {
            if (EditorApplication.timeSinceStartup - GetPhaseStartTime() < _preparationDelayInSeconds)
            {
                return;
            }

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

            Debug.Log("Exited play mode.");

            var totalDuration = System.TimeSpan.FromSeconds(EditorApplication.timeSinceStartup - GetBenchmarkStartTime())
                                 + EditorStartupUtil.LastStartupDuration;

            Debug.Log("<color=red>Finished benchmark...</color>");
            Debug.Log($"Benchmark total time: {totalDuration}");

            Finish();
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

        private static double GetBenchmarkStartTime()
        {
            return double.Parse(SessionState.GetString(_benchmarkStartTimeKey, "0"), CultureInfo.InvariantCulture);
        }

        private static void SetBenchmarkStartTime(double time)
        {
            SessionState.SetString(_benchmarkStartTimeKey, time.ToString(CultureInfo.InvariantCulture));
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
