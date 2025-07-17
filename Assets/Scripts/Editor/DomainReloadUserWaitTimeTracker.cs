using System;
using UnityEditorDevelopmentBenchmark.Editor.Util;
using UnityEngine;

namespace UnityEditorDevelopmentBenchmark.Editor
{
    public class DomainReloadUserWaitTimeTracker : UserWaitTimeTrackerBase
    {
        public override event Action<TimeSpan> UserWaited;
        
        public override UserWaitTimeData LastWaitTimeData => new("Assembly Reload", AssemblyReloadTimer.AssemblyReloadDuration);

        public DomainReloadUserWaitTimeTracker()
        {
            AssemblyReloadTimer.Updated += AssemblyReloadTimerOnUpdated;
        }

        private void AssemblyReloadTimerOnUpdated()
        {
            UserWaited?.Invoke(AssemblyReloadTimer.AssemblyReloadDuration);
        }
        
        ~DomainReloadUserWaitTimeTracker()
        {
            AssemblyReloadTimer.Updated -= AssemblyReloadTimerOnUpdated;
        }
    }
}