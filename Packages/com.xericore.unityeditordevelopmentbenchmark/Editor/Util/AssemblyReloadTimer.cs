using System;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;
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
            _ = WaitAndGetCompilationData();
        }

        private static async Awaitable WaitAndGetCompilationData()
        {
            await Awaitable.NextFrameAsync();

            var compilationData = CompilationData.GetAll();

            if (compilationData?.iterations == null || compilationData.iterations.Count == 0)
            {
                // Can happen if Library/Bee/fullprofile.json wasn't available/parseable yet (e.g. another
                // compilation was requested again shortly after this one finished, before we got a chance to read
                // it), or if the trace didn't contain any usable compilation events. CompilationData.GetAll()
                // already logs details in that case, so just skip reporting this occurrence.
                Debug.LogWarning("Couldn't get compilation data; skipping this assembly reload duration report.");
                return;
            }

            AssemblyReloadDuration = compilationData.iterations
                .Select(item => item.AfterAssemblyReload - item.BeforeAssemblyReload)
                .Aggregate((result, item) => result + item);

            if(AssemblyReloadDuration > TimeSpan.FromDays(7))
            {
                // This can happen when Unity is starting up.
                // In that case, we don't want to report it as a user wait time.
                return;
            }
            
            Debug.Log($"Assembly reloads took (from CompilationData): {AssemblyReloadDuration}");
            
            var totalSpan = compilationData.iterations.Last().AfterAssemblyReload -
                            compilationData.iterations.First().CompilationStarted;

            Debug.Log($"Total time from Json: {totalSpan}");
            TotalDuration = totalSpan;
            
            Updated?.Invoke();
        }
    }
}