using System;
using System.Collections.Generic;
using System.Linq;

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
        private readonly DomainReloadUserWaitTimeTracker _domainReloadUserWaitTimeTracker;

        private UserWaitTimeAggregator()
        {
            _compilationUserWaitTimeTracker = new CompilationUserWaitTimeTracker();
            _domainReloadUserWaitTimeTracker = new DomainReloadUserWaitTimeTracker();
            
            _compilationUserWaitTimeTracker.UserWaited += OnCompilationUserWaitTimeTrackerOnUserWaited;
            _domainReloadUserWaitTimeTracker.UserWaited += OnCompilationUserWaitTimeTrackerOnUserWaited;
        }

        private void OnCompilationUserWaitTimeTrackerOnUserWaited(TimeSpan timeSpan)
        {
            AnyWaitEventFired?.Invoke();
        }

        public List<UserWaitTimeData> GetWaitTimeData()
        {
            var waitTimeData = new List<UserWaitTimeData>
            {
                _compilationUserWaitTimeTracker.LastWaitTimeData,
                _domainReloadUserWaitTimeTracker.LastWaitTimeData
            };
            
            waitTimeData.Add(GetTotalWaitTimeData(waitTimeData));

            return waitTimeData;
        }

        private UserWaitTimeData GetTotalWaitTimeData(List<UserWaitTimeData> waitTimeData)
        {
            var totalWaitTime = waitTimeData.Aggregate(TimeSpan.Zero, (current, data) => current + data.WaitTime);

            return new UserWaitTimeData("Total", totalWaitTime);
        }

        ~UserWaitTimeAggregator()
        {
            _compilationUserWaitTimeTracker.UserWaited -= OnCompilationUserWaitTimeTrackerOnUserWaited;
            _domainReloadUserWaitTimeTracker.UserWaited -= OnCompilationUserWaitTimeTrackerOnUserWaited;
        }
    }
}