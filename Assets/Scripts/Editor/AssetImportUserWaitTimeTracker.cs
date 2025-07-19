using System;
using UnityEditor;
using UnityEditorDevelopmentBenchmark.Editor.Util;

namespace UnityEditorDevelopmentBenchmark.Editor
{
    public class AssetImportUserWaitTimeTracker : UserWaitTimeTrackerBase
    {
        protected override string FriendlyName => "Asset Import";
        
        public AssetImportUserWaitTimeTracker()
        {
            MyAssetPostprocessor.UserWaited += MyAssetPostprocessorOnUserWaited;
        }

        private void MyAssetPostprocessorOnUserWaited(TimeSpan waitTime)
        {
            ReportSingleWaitEvent(waitTime);
        }
        
        ~AssetImportUserWaitTimeTracker()
        {
            MyAssetPostprocessor.UserWaited -= MyAssetPostprocessorOnUserWaited;
        }
    }
}
