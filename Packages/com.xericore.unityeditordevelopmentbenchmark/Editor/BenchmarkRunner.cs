using System.Diagnostics;
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
        
        public static void StartBenchmark()
        {
            Debug.Log("Starting benchmark...");
            _stopwatch = Stopwatch.StartNew();

            EditorApplication.EnterPlaymode();
            
            Debug.Log("Entered play mode.");
            
            EditorApplication.ExitPlaymode();
            
            Debug.Log("Exited play mode.");
            
            _stopwatch.Stop();

            var totalDuration = _stopwatch.Elapsed + EditorStartupUtil.LastStartupDuration;
            
            Debug.Log($"Benchmark total time: {totalDuration}");
        }
    }
}