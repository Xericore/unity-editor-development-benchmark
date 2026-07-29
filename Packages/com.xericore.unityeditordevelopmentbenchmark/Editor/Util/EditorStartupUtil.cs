using System;
using System.Globalization;
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

        public static TimeSpan LastStartupDuration { get; private set; }
        
        private const string _editorStartupUtilSessionStartedKey = "EditorStartupUtil_SessionStarted";

        /// <summary>
        /// Unlike <see cref="LastStartupDuration"/> (a plain static property, wiped by any domain reload) and
        /// <see cref="SessionState"/> (wiped by a process restart), this <see cref="EditorPrefs"/> key survives
        /// both, so callers that run some time after startup - possibly after a domain reload has already
        /// happened - can still reliably retrieve this session's startup duration via
        /// <see cref="TryGetPersistedLastStartupDuration"/>.
        /// </summary>
        private const string _lastStartupDurationTicksKey = "EditorStartupUtil_LastStartupDurationTicks";

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

            if (startupTime == DateTime.MinValue)
            {
                Debug.LogWarning("Could not determine Unity Editor startup time from the Editor log; skipping this session's startup duration.");
                return;
            }

            var startupDuration = DateTime.Now - startupTime;

            LastStartupDuration = startupDuration;

            EditorPrefs.SetString(_lastStartupDurationTicksKey, startupDuration.Ticks.ToString(CultureInfo.InvariantCulture));

            UserWaited?.Invoke(startupDuration);
        }

        /// <summary>
        /// Retrieves this session's startup duration from the <see cref="EditorPrefs"/>-backed store (see
        /// <see cref="_lastStartupDurationTicksKey"/>), which - unlike <see cref="LastStartupDuration"/> - survives
        /// domain reloads that may have happened between startup and the caller asking for it.
        /// </summary>
        public static bool TryGetPersistedLastStartupDuration(out TimeSpan duration)
        {
            var ticksString = EditorPrefs.GetString(_lastStartupDurationTicksKey, null);

            if (ticksString == null || !long.TryParse(ticksString, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
            {
                duration = TimeSpan.Zero;
                return false;
            }

            duration = TimeSpan.FromTicks(ticks);
            return true;
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