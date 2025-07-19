using System;
using System.Diagnostics;
using UnityEditor;

namespace UnityEditorDevelopmentBenchmark.Editor.Util
{
    public class MyAssetPostprocessor : AssetPostprocessor
    {
        public static event Action<TimeSpan> UserWaited;

        private static TimeSpan TotalDuration => _assetStopwatch?.Elapsed ?? TimeSpan.Zero;
        
        private static readonly Stopwatch _assetStopwatch = new();

        private void OnPreprocessAsset()
        {
            if (!_assetStopwatch.IsRunning)
            {
                _assetStopwatch.Restart();
            }
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            _assetStopwatch.Stop();
            UserWaited?.Invoke(TotalDuration);
        }
    }
}