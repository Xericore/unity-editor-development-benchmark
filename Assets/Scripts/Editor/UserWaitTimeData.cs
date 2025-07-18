using System;

namespace UnityEditorDevelopmentBenchmark.Editor
{
    [Serializable]
    public class UserWaitTimeData
    {
        public string Name { get; }
        public TimeSpan WaitTime { get; }
        public TimeSpan TotalWaitTime { get; }

        public UserWaitTimeData(string name, TimeSpan waitTime, TimeSpan totalWaitTime)
        {
            Name = name;
            WaitTime = waitTime;
            TotalWaitTime = totalWaitTime;
        }
    }
}