using System;
using System.Collections.Generic;

namespace UnityEditorDevelopmentBenchmark.Editor
{
    public class UserWaitTimeAggregator
    {
        public event Action AnyWaitEventFired;
        
        private static UserWaitTimeAggregator _instance;
        public static UserWaitTimeAggregator Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new UserWaitTimeAggregator();
                return _instance;
            }
        }
        
        private readonly CompilationUserWaitTimeTracker _compilationUserWaitTimeTracker;

        private UserWaitTimeAggregator()
        {
            _compilationUserWaitTimeTracker = new CompilationUserWaitTimeTracker();
            
            _compilationUserWaitTimeTracker.UserWaited += OnCompilationUserWaitTimeTrackerOnUserWaited;
        }

        private void OnCompilationUserWaitTimeTrackerOnUserWaited(TimeSpan timeSpan)
        {
            AnyWaitEventFired?.Invoke();
        }

        public List<UserWaitTimeData> GetWaitTimeData()
        {
            var waitTimeData = new List<UserWaitTimeData>
            {
                _compilationUserWaitTimeTracker.LastWaitTimeData
            };

            return waitTimeData;
        }
        
        ~UserWaitTimeAggregator()
        {
            _compilationUserWaitTimeTracker.UserWaited -= OnCompilationUserWaitTimeTrackerOnUserWaited;
        }
    }
}