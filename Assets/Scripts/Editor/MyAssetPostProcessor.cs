using System;
using System.Diagnostics;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace UnityEditorDevelopmentBenchmark.Editor
{
    public class MyAssetPostprocessor : AssetPostprocessor
    {
        public TimeSpan TotalDuration => _assetStopwatch?.Elapsed ?? TimeSpan.Zero;
        
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
            Debug.Log($"Importing all assets took: {_assetStopwatch.Elapsed}");
        }
    }
}