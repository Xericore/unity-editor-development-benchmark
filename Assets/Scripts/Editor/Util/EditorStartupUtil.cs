using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UnityEditorDevelopmentBenchmark.Editor.Util
{
    [InitializeOnLoad]
    public static class EditorStartupUtil
    {
        public static event Action<TimeSpan> UserWaited;
        
        private const string _editorStartupUtilSessionStartedKey = "EditorStartupUtil_SessionStarted";
        
        /// <summary>
        /// We need this to ensure that the event is fired after the first update of the editor.
        /// This is to make sure that all other InitializeOnLoad classes have been executed.
        /// </summary>
        private static bool _isFirstUpdateDone;
        
        static EditorStartupUtil()
        {
            EditorApplication.update += OnUpdate;
            EditorApplication.quitting += OnEditorQuitting;
        }

        private static void OnUpdate()
        {
            if (!_isFirstUpdateDone)
            {
                _isFirstUpdateDone = true;
                return;
            }
            
            var sessionStarted = EditorPrefs.GetBool(_editorStartupUtilSessionStartedKey, false);

            if (sessionStarted)
            {
                return;
            }
            
            EditorApplication.update -= OnUpdate;
            
            EditorPrefs.SetBool(_editorStartupUtilSessionStartedKey, true);

            var startupTime = GetUtcStartupTimeFromEditorLog();

            var startupDuration = DateTime.Now - startupTime;

            UserWaited?.Invoke(startupDuration);
        }

        private static DateTime GetUtcStartupTimeFromEditorLog()
        {
            try
            {
                var logPath = Application.consoleLogPath;
                var utcPattern = new Regex(@"(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d+Z)");

                using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);

                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    var match = utcPattern.Match(line);
                    if (!match.Success)
                    {
                        continue;
                    }

                    if (DateTime.TryParse(match.Groups[1].Value, out var startTime))
                    {
                        Debug.Log("Unity Editor startup time found in log: " + startTime);
                        return startTime;
                    }

                    return DateTime.MinValue;
                }

                return DateTime.MinValue;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error trying to get startup time from Editor log file: {e.Message}");
                return DateTime.MinValue;
            }
        }
        
        /// <summary>
        /// If Unity crashes, this method will not be called. This means that when Unity starts after a crash,
        /// we lose the startup time for that startup event. But the next time after the user closes Unity
        /// properly, this method will be called.
        /// </summary>
        private static void OnEditorQuitting()
        {
            EditorPrefs.SetBool(_editorStartupUtilSessionStartedKey, false);
        }
    }
}