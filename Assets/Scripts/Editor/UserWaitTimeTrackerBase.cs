using System;
using UnityEditor;

namespace UnityEditorDevelopmentBenchmark.Editor
{
    public abstract class UserWaitTimeTrackerBase
    {
        public event Action<TimeSpan> UserWaited;
        
        public UserWaitTimeData LastWaitTimeData
        {
            get
            {
                var floatTime = EditorPrefs.GetFloat(LastWaitTimeEditorPrefsKey);
                var totalFloatTime = EditorPrefs.GetFloat(TotalWaitTimeEditorPrefsKey);
                
                return new UserWaitTimeData(FriendlyName, TimeSpan.FromMilliseconds(floatTime),
                    TimeSpan.FromMilliseconds(totalFloatTime));
            }
        }
        
        private string LastWaitTimeEditorPrefsKey => $"{EditorPrefsPrefix}_LastWaitTime";
        private string TotalWaitTimeEditorPrefsKey => $"{EditorPrefsPrefix}_TotalWaitTime";

        private string EditorPrefsPrefix => $"{GetType().Name}";

        protected abstract string FriendlyName { get; }
        
        /// <summary>
        /// Must be called by inheriting class when a single wait event occurs.
        /// </summary>
        /// <param name="timeSpan"></param>
        protected void ReportSingleWaitEvent(TimeSpan timeSpan)
        {
            EditorPrefs.SetFloat(LastWaitTimeEditorPrefsKey, (float)timeSpan.TotalMilliseconds);
            
            UpdateTotal((float)timeSpan.TotalMilliseconds);
            
            UserWaited?.Invoke(timeSpan);
        }
        
        private void UpdateTotal(float elapsedMilliseconds)
        {
            var currentTotalMilliseconds = EditorPrefs.GetFloat(TotalWaitTimeEditorPrefsKey, 0);
            var newTotalMilliseconds = currentTotalMilliseconds + elapsedMilliseconds;
            
            EditorPrefs.SetFloat(TotalWaitTimeEditorPrefsKey, newTotalMilliseconds);
        }
        
        public void ResetTotalWaitTime()
        {
            EditorPrefs.SetFloat(TotalWaitTimeEditorPrefsKey, 0);
        }
    }
}