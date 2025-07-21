using System;
using UnityEditorDevelopmentBenchmark.Editor.Util;

namespace UnityEditorDevelopmentBenchmark.Editor.UserWaitTimeTrackers
{
    public class EditorStartupUserWaitTimeTracker : UserWaitTimeTrackerBase
    {
        protected override string FriendlyName => "Editor Startup";

        public EditorStartupUserWaitTimeTracker()
        {
            EditorStartupUtil.UserWaited += OnUserWaited;
        }

        private void OnUserWaited(TimeSpan waitTime)
        {
            ReportSingleWaitEvent(waitTime);
        }
    }
}