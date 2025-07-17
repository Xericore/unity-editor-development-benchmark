using System;
using UnityEngine;

namespace UnityEditorDevelopmentBenchmark.Editor
{
    public class CompilationUserWaitTimeTracker : UserWaitTimeTrackerBase
    {
        public override event Action<TimeSpan> UserWaited;

        public CompilationUserWaitTimeTracker()
        {
            LastWaitTimeData = new UserWaitTimeData("Compilation time", TimeSpan.Zero);
            Debug.Log("<color=green>CompilationUserWaitTimeTracker constructor called</color>");
            
            CompilationAndAssemblyReloadTimer.TotalDurationUpdated += CompilationAndAssemblyReloadTimerOnTotalDurationUpdated;
        }

        private void CompilationAndAssemblyReloadTimerOnTotalDurationUpdated(TimeSpan timeSpan)
        {
            Debug.Log("<color=yellow>CompilationUserWaitTimeTracker</color> total duration updated: " + timeSpan);
            LastWaitTimeData = new UserWaitTimeData("Compilation time", timeSpan);
            UserWaited?.Invoke(timeSpan);
        }
        
        ~CompilationUserWaitTimeTracker()
        {
            Debug.Log("<color=red>CompilationUserWaitTimeTracker</color> destructor called");
            CompilationAndAssemblyReloadTimer.TotalDurationUpdated -= CompilationAndAssemblyReloadTimerOnTotalDurationUpdated;
        }
    }
}