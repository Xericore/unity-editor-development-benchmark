using System;
using System.Diagnostics;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;
using Debug = UnityEngine.Debug;

namespace UnityEditorDevelopmentBenchmark.Editor
{
    [InitializeOnLoad]
    public static class DomainReloadTimer
    {
        public static TimeSpan TotalDuration { get; private set; }
        
        private static readonly Stopwatch _compilationStopwatch;

        static DomainReloadTimer()
        {
            _compilationStopwatch = new Stopwatch();

            CompilationPipeline.compilationStarted += CompilationStarted;
            CompilationPipeline.compilationFinished += CompilationFinished;
            
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        }
        
        private static void CompilationStarted(object obj)
        {
            _compilationStopwatch.Restart();
        }

        private static void CompilationFinished(object obj)
        {
            _compilationStopwatch.Stop();
            Debug.Log("--------------------------------------------");
            Debug.Log($"Compilation took: {_compilationStopwatch.Elapsed}");
        }

        private static void OnAfterAssemblyReload()
        {
            UniTask.Void(WaitAndGetCompilationData);
        }

        private static async UniTaskVoid WaitAndGetCompilationData()
        {
            // TODO: Check sharing violation of json file with needle tools CompilationTimelineWindow.cs
            await UniTask.Delay(TimeSpan.FromSeconds(1));

            var compilationData = CompilationData.GetAll();

            var totalReloadSpan = compilationData.iterations
                .Select(item => item.AfterAssemblyReload - item.BeforeAssemblyReload)
                .Aggregate((result, item) => result + item);

            Debug.Log($"Assembly reloads took (from CompilationData): {totalReloadSpan}");

            var totalSpan = compilationData.iterations.Last().AfterAssemblyReload -
                            compilationData.iterations.First().CompilationStarted;

            Debug.Log($"Total time from Json: {totalSpan}");
            TotalDuration = totalSpan;
        }
    }
}