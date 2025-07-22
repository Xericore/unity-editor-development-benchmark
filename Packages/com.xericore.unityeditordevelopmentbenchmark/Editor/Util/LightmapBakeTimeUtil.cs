using System;
using System.Diagnostics;
using UnityEditor;

namespace UnityEditorDevelopmentBenchmark.Editor.Util
{
    [InitializeOnLoad]
    public static class LightmapBakeTimeUtil
    {
        public static event Action<TimeSpan> UserWaited;
        
        private static readonly Stopwatch _stopwatch;

        static LightmapBakeTimeUtil()
        {
            _stopwatch = new Stopwatch();
            
            Lightmapping.bakeStarted += OnBakeStarted;
            Lightmapping.bakeCompleted += OnBakeCompleted;
            Lightmapping.bakeCancelled += OnBakeCompleted;
        }

        private static void OnBakeStarted()
        {
            _stopwatch.Restart();
        }

        private static void OnBakeCompleted()
        {
            _stopwatch.Stop();
            UserWaited?.Invoke(_stopwatch.Elapsed);
        }
    }
}