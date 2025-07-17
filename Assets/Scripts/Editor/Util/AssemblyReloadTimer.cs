using System;
using System.Diagnostics;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace UnityEditorDevelopmentBenchmark.Editor.Util
{
    [InitializeOnLoad]
    public static class AssemblyReloadTimer
    {
        public static event Action Updated;
        
        public static TimeSpan AssemblyReloadDuration { get; private set; }
        private static TimeSpan TotalDuration { get; set; }
        
        private static readonly Stopwatch _compilationStopwatch;

        static AssemblyReloadTimer()
        {
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
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

            AssemblyReloadDuration = compilationData.iterations
                .Select(item => item.AfterAssemblyReload - item.BeforeAssemblyReload)
                .Aggregate((result, item) => result + item);

            Debug.Log($"Assembly reloads took (from CompilationData): {AssemblyReloadDuration}");
            
            var totalSpan = compilationData.iterations.Last().AfterAssemblyReload -
                            compilationData.iterations.First().CompilationStarted;

            Debug.Log($"Total time from Json: {totalSpan}");
            TotalDuration = totalSpan;
            
            Updated?.Invoke();
        }
    }
}