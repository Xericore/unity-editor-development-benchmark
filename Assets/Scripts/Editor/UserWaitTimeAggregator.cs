using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

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

        private static UserWaitTimeData GetTotalWaitTimeData(List<UserWaitTimeData> waitTimeData)
        {
            var lastTotal = waitTimeData.Aggregate(TimeSpan.Zero, (current, data) => current + data.WaitTime);
            var totalTotal = waitTimeData.Aggregate(TimeSpan.Zero, (current, data) => current + data.TotalWaitTime);

            return new UserWaitTimeData("Total", lastTotal, totalTotal);
        }

        ~UserWaitTimeAggregator()
        {
            _compilationUserWaitTimeTracker.UserWaited -= OnCompilationUserWaitTimeTrackerOnUserWaited;
            _domainReloadUserWaitTimeTracker.UserWaited -= OnCompilationUserWaitTimeTrackerOnUserWaited;
        }

        public void ResetTotalWaitTime()
        {
            _compilationUserWaitTimeTracker.ResetTotalWaitTime();
            _domainReloadUserWaitTimeTracker.ResetTotalWaitTime();
            
            AnyWaitEventFired?.Invoke();
        }
    }
}