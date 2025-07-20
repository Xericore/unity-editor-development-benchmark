using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace UnityEditorDevelopmentBenchmark.Editor.UserWaitTimeTrackers
{
    public class BuildUserWaitTimeTracker : UserWaitTimeTrackerBase, IPostprocessBuildWithReport
    {
        protected override string FriendlyName => "Building";

        public int callbackOrder => 0;
        
        public void OnPostprocessBuild(BuildReport report)
        {
            ReportSingleWaitEvent(report.summary.totalTime);
        }
    }
}