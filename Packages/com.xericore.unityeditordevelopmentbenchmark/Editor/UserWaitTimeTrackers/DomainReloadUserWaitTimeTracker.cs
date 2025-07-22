using UnityEditorDevelopmentBenchmark.Editor.Util;

namespace UnityEditorDevelopmentBenchmark.Editor.UserWaitTimeTrackers
{
    public class DomainReloadUserWaitTimeTracker : UserWaitTimeTrackerBase
    {
        protected override string FriendlyName => "Domain Reload";
        
        public DomainReloadUserWaitTimeTracker()
        {
            AssemblyReloadTimer.Updated += AssemblyReloadTimerOnUpdated;
        }

        private void AssemblyReloadTimerOnUpdated()
        {
            ReportSingleWaitEvent(AssemblyReloadTimer.AssemblyReloadDuration);
        }
        
        ~DomainReloadUserWaitTimeTracker()
        {
            AssemblyReloadTimer.Updated -= AssemblyReloadTimerOnUpdated;
        }
    }
}