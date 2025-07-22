using System;
using UnityEditorDevelopmentBenchmark.Editor.Util;

namespace UnityEditorDevelopmentBenchmark.Editor.UserWaitTimeTrackers
{
    public class LightmapBakingUserWaitTimeTracker : UserWaitTimeTrackerBase
    {
        protected override string FriendlyName => "Lightmap Baking";
        
        public LightmapBakingUserWaitTimeTracker()
        {
            LightmapBakeTimeUtil.UserWaited += OnUserWaited;
        }

        private void OnUserWaited(TimeSpan waitTime)
        {
            ReportSingleWaitEvent(waitTime);
        }
    }
}