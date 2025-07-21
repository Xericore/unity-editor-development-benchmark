using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorDevelopmentBenchmark.Editor.UserWaitTimeTrackers;

namespace UnityEditorDevelopmentBenchmark.Editor
{
    [InitializeOnLoad]
    public static class UserWaitTimeAggregator
    {
        public static event Action AnyWaitEventFired;
        
        private static readonly CompilationUserWaitTimeTracker _compilationUserWaitTimeTracker;
        private static readonly DomainReloadUserWaitTimeTracker _domainReloadUserWaitTimeTracker;
        private static readonly AssetImportUserWaitTimeTracker _assetImportUserWaitTimeTracker;
        private static readonly SwitchPlayModeUserWaitTimeTracker _switchPlayModeUserWaitTimeTracker;
        private static readonly BuildUserWaitTimeTracker _buildUserWaitTimeTracker;
        private static readonly EditorStartupUserWaitTimeTracker _editorStartupUserWaitTimeTracker;

        static UserWaitTimeAggregator()
        {
            _compilationUserWaitTimeTracker = new CompilationUserWaitTimeTracker();
            _domainReloadUserWaitTimeTracker = new DomainReloadUserWaitTimeTracker();
            _assetImportUserWaitTimeTracker = new AssetImportUserWaitTimeTracker();
            _switchPlayModeUserWaitTimeTracker = new SwitchPlayModeUserWaitTimeTracker();
            _buildUserWaitTimeTracker = new BuildUserWaitTimeTracker();
            _editorStartupUserWaitTimeTracker = new EditorStartupUserWaitTimeTracker();
            
            _compilationUserWaitTimeTracker.UserWaited += OnCompilationUserWaitTimeTrackerOnUserWaited;
            _domainReloadUserWaitTimeTracker.UserWaited += OnCompilationUserWaitTimeTrackerOnUserWaited;
            _assetImportUserWaitTimeTracker.UserWaited += OnCompilationUserWaitTimeTrackerOnUserWaited;
            _switchPlayModeUserWaitTimeTracker.UserWaited += OnCompilationUserWaitTimeTrackerOnUserWaited;
            _buildUserWaitTimeTracker.UserWaited += OnCompilationUserWaitTimeTrackerOnUserWaited;
            _editorStartupUserWaitTimeTracker.UserWaited += OnCompilationUserWaitTimeTrackerOnUserWaited;
        }

        private static void OnCompilationUserWaitTimeTrackerOnUserWaited(TimeSpan timeSpan)
        {
            AnyWaitEventFired?.Invoke();
        }

        public static List<UserWaitTimeData> GetWaitTimeData()
        {
            var waitTimeData = new List<UserWaitTimeData>
            {
                _compilationUserWaitTimeTracker.LastWaitTimeData,
                _domainReloadUserWaitTimeTracker.LastWaitTimeData,
                _assetImportUserWaitTimeTracker.LastWaitTimeData,
                _switchPlayModeUserWaitTimeTracker.LastWaitTimeData,
                _buildUserWaitTimeTracker.LastWaitTimeData,
                _editorStartupUserWaitTimeTracker.LastWaitTimeData
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
        
        public static void ResetTotalWaitTime()
        {
            _compilationUserWaitTimeTracker.ResetTotalWaitTime();
            _domainReloadUserWaitTimeTracker.ResetTotalWaitTime();
            _assetImportUserWaitTimeTracker.ResetTotalWaitTime();
            _switchPlayModeUserWaitTimeTracker.ResetTotalWaitTime();
            _buildUserWaitTimeTracker.ResetTotalWaitTime();
            _editorStartupUserWaitTimeTracker.ResetTotalWaitTime();
            
            AnyWaitEventFired?.Invoke();
        }
    }
}