using System;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.Compilation;

namespace UnityEditorDevelopmentBenchmark.Editor
{
    public class CompilationUserWaitTimeTracker : UserWaitTimeTrackerBase
    {
        protected override string FriendlyName => "Compilation";
        
        private static Stopwatch _compilationStopwatch;
        
        public CompilationUserWaitTimeTracker()
        {
            _compilationStopwatch = new Stopwatch();

            CompilationPipeline.compilationStarted += CompilationStarted;
            CompilationPipeline.compilationFinished += CompilationFinished;
        }
        
        private static void CompilationStarted(object obj)
        {
            _compilationStopwatch.Restart();
        }
        
        private void CompilationFinished(object obj)
        {
            _compilationStopwatch.Stop();
            

            ReportSingleWaitEvent(_compilationStopwatch.Elapsed);
        }
        
        ~CompilationUserWaitTimeTracker()
        {
            CompilationPipeline.compilationStarted -= CompilationStarted;
            CompilationPipeline.compilationFinished -= CompilationFinished;
        }
    }
}