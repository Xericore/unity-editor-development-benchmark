using System;
using UnityEditorDevelopmentBenchmark.Editor.Util;

namespace UnityEditorDevelopmentBenchmark.Editor.UserWaitTimeTrackers
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
