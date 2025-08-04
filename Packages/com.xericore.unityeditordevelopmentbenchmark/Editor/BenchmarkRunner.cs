using System.Collections;
using System.Diagnostics;
using System.Reflection;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditorDevelopmentBenchmark.Editor.Util;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace UnityEditorDevelopmentBenchmark.Editor
{
    /// <summary>
    /// To be called from the command line.
    /// </summary>
    [UsedImplicitly]
    public static class BenchmarkRunner
    {
        private static Stopwatch _stopwatch;
        
        private const float _maxLoopTimeInSeconds = 10f;
        
        [MenuItem("Window/Analysis/Start Benchmark")]
        [UsedImplicitly]
        public static void StartBenchmark()
        {
            AttachCoroutineToEditorUpdate(BenchmarkCoroutine());
        }

        private static void AttachCoroutineToEditorUpdate(IEnumerator routine)
        {
            EditorApplication.update += Step;
            return;

            void Step()
            {
                if (!routine.MoveNext())
                {
                    EditorApplication.update -= Step;
                }
            }
        }

        private static IEnumerator BenchmarkCoroutine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Cannot start benchmark while in play mode.");
                yield break;
            }

            if(!TryDisableConsoleClearOnPlay())
            {
                Debug.LogWarning("Couldn't disable console clear on play. This may cause the benchmark to not show all logs. Please disable it manually in the Console window settings.");
            }

            yield return WaitForCompilation();

            Debug.Log("Preparing benchmark...");
            yield return new WaitForSeconds(1);

            Debug.Log("<color=lime>Starting benchmark...</color>");
            _stopwatch = Stopwatch.StartNew();

            EditorApplication.EnterPlaymode();

            yield return WaitForPlayMode();

            Debug.Log("Entered play mode.");

            yield return new WaitForSeconds(1);

            EditorApplication.ExitPlaymode();

            Debug.Log("Exited play mode.");

            _stopwatch.Stop();

            var totalDuration = _stopwatch.Elapsed + EditorStartupUtil.LastStartupDuration;

            Debug.Log("<color=red>Finished benchmark...</color>");
            Debug.Log($"Benchmark total time: {totalDuration}");
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

        private static IEnumerator WaitForCompilation()
        {
            var startTime = (float) EditorApplication.timeSinceStartup;
            while (EditorApplication.isCompiling)
            {
                if ((float) EditorApplication.timeSinceStartup - startTime > _maxLoopTimeInSeconds)
                {
                    Debug.LogWarning("Timeout while waiting for compilation to finish.");
                    yield break;
                }

                yield return null;
            }
        }
        
        private static IEnumerator WaitForPlayMode()
        {
            var startTime = (float) EditorApplication.timeSinceStartup;
            while (!EditorApplication.isPlaying)
            {
                if ((float) EditorApplication.timeSinceStartup - startTime > _maxLoopTimeInSeconds)
                {
                    Debug.LogWarning("Timeout while waiting for play mode to enter.");
                    yield break;
                }

                yield return null;
            }
        }
    }
}