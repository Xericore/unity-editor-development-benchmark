using System;
using System.Collections.Generic;

namespace UnityEditorDevelopmentBenchmark.Editor.Serialization
{
    [Serializable]
    internal class Args {
        public string name; 
        public int durationMS; 
        public string detail; 
    }

    [Serializable]
    internal class TraceEvent {
        public string cat; 
        public string pid; 
        public int tid; 
        public long ts; 
        public string ph; 
        public string name; 
        public Args args; 
        public int dur; 
        public string cname; 
    }

    [Serializable]
    internal class BeeProfilerData {
        public List<TraceEvent> traceEvents; 
    }
}