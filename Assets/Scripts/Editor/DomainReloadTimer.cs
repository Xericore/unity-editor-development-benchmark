using System.Diagnostics;
using UnityEditor;
using UnityEditor.Compilation;

namespace UnityEditorDevelopmentBenchmark.Editor
{
    [InitializeOnLoad]
    public static class DomainReloadTimer
    {
        private static readonly Stopwatch AssemblyReloadStopwatch;
        private static readonly Stopwatch CompilationStopwatch;
        private static readonly Stopwatch TotalStopwatch;

        static DomainReloadTimer()
        {
            AssemblyReloadStopwatch = new Stopwatch();
            CompilationStopwatch = new Stopwatch();
            TotalStopwatch = new Stopwatch();

            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
            
            CompilationPipeline.compilationStarted += CompilationStarted;
            CompilationPipeline.compilationFinished += CompilationFinished;
        }
        
        private static void OnBeforeAssemblyReload()
        {
            AssemblyReloadStopwatch.Restart();
        }

        private static void OnAfterAssemblyReload()
        {
            AssemblyReloadStopwatch.Stop();
            UnityEngine.Debug.Log($"Domain reload took: {AssemblyReloadStopwatch.Elapsed}");
            TotalStopwatch.Stop();
            UnityEngine.Debug.Log($"Total reload took: {TotalStopwatch.Elapsed}");
        }
        
        private static void CompilationStarted(object obj)
        {
            CompilationStopwatch.Restart();
            TotalStopwatch.Restart();
        }
        
        private static void CompilationFinished(object obj)
        {
            CompilationStopwatch.Stop();
            UnityEngine.Debug.Log($"Compilation reload took: {CompilationStopwatch.Elapsed}");
        }


    }
}