using System;

namespace UnityEditorDevelopmentBenchmark.Editor
{
    [Serializable]
    public class UserWaitTimeData
    {
        public string Name { get; }
        public TimeSpan WaitTime { get; }
        
        public UserWaitTimeData(string name, TimeSpan waitTime)
        {
            Name = name;
            WaitTime = waitTime;
        }
    }
}