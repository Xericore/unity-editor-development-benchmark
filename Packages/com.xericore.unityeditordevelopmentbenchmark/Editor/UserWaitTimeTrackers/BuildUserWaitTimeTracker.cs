using System.Diagnostics;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace UnityEditorDevelopmentBenchmark.Editor.UserWaitTimeTrackers
{
    public class BuildUserWaitTimeTracker : UserWaitTimeTrackerBase, IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        protected override string FriendlyName => "Building";

        public int callbackOrder => 0;

        private Stopwatch _stopwatch;
        
        public void OnPreprocessBuild(BuildReport report)
        {
            _stopwatch = Stopwatch.StartNew();
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            // We're not using report.summary.totalTime here, because it just doesn't work.
            _stopwatch.Stop();
            ReportSingleWaitEvent(_stopwatch.Elapsed);
        }
    }
}