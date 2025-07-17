using System;

namespace UnityEditorDevelopmentBenchmark.Editor
{
    public abstract class UserWaitTimeTrackerBase
    {
        public abstract event Action<TimeSpan> UserWaited;
        
        public UserWaitTimeData LastWaitTimeData { get; protected set; }
    }
}