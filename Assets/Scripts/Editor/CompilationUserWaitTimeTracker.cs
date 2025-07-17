using System;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.Compilation;

namespace UnityEditorDevelopmentBenchmark.Editor
{
    public class CompilationUserWaitTimeTracker : UserWaitTimeTrackerBase
    {
        private static Stopwatch _compilationStopwatch;
        
        public override event Action<TimeSpan> UserWaited;
        
        private const string _editorPrefsKeyLastWaitTime = "CompilationUserWaitTimeTracker_LastWaitTime";

        public override UserWaitTimeData LastWaitTimeData
        {
            get
            {
                var floatTime = EditorPrefs.GetFloat(_editorPrefsKeyLastWaitTime);
                
                return new UserWaitTimeData("Compilation", TimeSpan.FromMilliseconds(floatTime));
            }
        }

        public CompilationUserWaitTimeTracker()
        {
            _compilationStopwatch = new Stopwatch();

            CompilationPipeline.compilationStarted += CompilationStarted;
            CompilationPipeline.compilationFinished += CompilationFinished;
        }
        
        private void CompilationStarted(object obj)
        {
            _compilationStopwatch.Restart();
        }
        
        private void CompilationFinished(object obj)
        {
            _compilationStopwatch.Stop();
            
            EditorPrefs.SetFloat(_editorPrefsKeyLastWaitTime, _compilationStopwatch.ElapsedMilliseconds);
            
            UserWaited?.Invoke(LastWaitTimeData.WaitTime);
        }
        
        ~CompilationUserWaitTimeTracker()
        {
            CompilationPipeline.compilationStarted -= CompilationStarted;
            CompilationPipeline.compilationFinished -= CompilationFinished;
        }
    }
}