using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UnityEditorDevelopmentBenchmark.Editor.UserWaitTimeTrackers
{
    [InitializeOnLoad]
    public static class EditorStartupUserWaitTimeTracker
    {
        private static DateTime _startupTime;

        static EditorStartupUserWaitTimeTracker()
        {
            EditorApplication.update += OnFirstUpdate;
        }

        private static void OnFirstUpdate()
        {
            EditorApplication.update -= OnFirstUpdate;

            _startupTime = GetUtcStartupTimeFromEditorLog();

            var startupDuration = DateTime.Now - _startupTime;

            Debug.Log($"Unity Editor startup time: {startupDuration.TotalSeconds:F2} seconds");
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
    }
}