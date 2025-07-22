using System;
using UnityEditor;
using UnityEditorDevelopmentBenchmark.Editor.Util;

namespace UnityEditorDevelopmentBenchmark.Editor.UserWaitTimeTrackers
{
    public class SwitchPlayModeUserWaitTimeTracker : UserWaitTimeTrackerBase
    {
        protected override string FriendlyName => "Switching Play Mode";
        
        public SwitchPlayModeUserWaitTimeTracker()
        {
            SwitchPlayModeUtil.BeforeEnteringPlayMode += BeforeEnteringPlayMode;
            SwitchPlayModeUtil.LateEnteredPlayMode += ReportTimeEvent;
            
            SwitchPlayModeUtil.BeforeLeavingPlayMode += BeforeLeavingPlayMode;
            SwitchPlayModeUtil.JustLeftPlayMode += ReportTimeEvent;
        }
        
        private static void BeforeEnteringPlayMode()
        {
            // We can't use a field like a stopwatch here,
            // because it will be reset when entering play mode.
            var startTime = (float)EditorApplication.timeSinceStartup;
            EditorPrefs.SetFloat("SwitchPlayModeUserWaitTimeTracker_StartTime", startTime);
        }
        
        private void ReportTimeEvent()
        {
            var startTime = EditorPrefs.GetFloat("SwitchPlayModeUserWaitTimeTracker_StartTime", (float)EditorApplication.timeSinceStartup);
            ReportSingleWaitEvent(TimeSpan.FromSeconds(EditorApplication.timeSinceStartup - startTime));
        }
        
        private static void BeforeLeavingPlayMode()
        {
            var startTime = (float)EditorApplication.timeSinceStartup;
            EditorPrefs.SetFloat("SwitchPlayModeUserWaitTimeTracker_StartTime", startTime);
        }
    }
}
