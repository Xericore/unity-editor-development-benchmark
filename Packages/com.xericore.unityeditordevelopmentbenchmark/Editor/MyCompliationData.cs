using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditorDevelopmentBenchmark.Editor.Serialization;
using UnityEngine;

namespace UnityEditorDevelopmentBenchmark.Editor
{
    internal class CompilationData
    {
        internal class IterativeCompilationData
        {
            public List<CompilationData> iterations;
        }

        public DateTime CompilationFinished;
        public DateTime CompilationStarted;
        public DateTime AfterAssemblyReload;
        public DateTime BeforeAssemblyReload;

        public List<AssemblyCompilationData> compilationData;

        public static IterativeCompilationData GetAll()
        {
            var data = ConvertBeeDataToCompilationData();
            if (data == null) return null;

            return new IterativeCompilationData
            {
                iterations = new List<CompilationData>
                {
                    data
                }
            };
        }

#if UNITY_2021_2_OR_NEWER
        private const string ProfilerJson = "Library/Bee/fullprofile.json";
#else
        const string ProfilerJson = "Library/Bee/profiler.json";
#endif

        private static CompilationData ConvertBeeDataToCompilationData()
        {
            if (!File.Exists(ProfilerJson)) return null;

            try
            {
                var allJsonText = File.ReadAllText(ProfilerJson);
                
                var beeData =
                    JsonUtility.FromJson<BeeProfilerData>(allJsonText);

                if (beeData.traceEvents == null || !beeData.traceEvents.Any()) return null;

                beeData.traceEvents = beeData.traceEvents
                    .Where(x => x.ts > 0
#if UNITY_2021_2_OR_NEWER
                                && x.pid != "0"
#endif
                    )
                    .OrderBy(x => x.ts)
                    .ToList();
                var ticksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;

                var firstTs = beeData.traceEvents.First().ts;
                var lastTs = beeData.traceEvents.Last().ts;
                var beeCompilationSpan = lastTs - firstTs;
                
                var unityCompilationSpan =
                    (TraceData.instance.CompilationFinished - TraceData.instance.CompilationStarted).Ticks /
                    ticksPerMicrosecond;
                
                var compilationSpanOffset = Math.Max(0, unityCompilationSpan - beeCompilationSpan);
                var offsetToFirstTs = TraceData.instance.CompilationStarted.Ticks / ticksPerMicrosecond - firstTs +
                                      compilationSpanOffset;
                
                var cc = new CompilationData
                {
                    CompilationStarted = TraceData.instance.CompilationStarted,
                    CompilationFinished = TraceData.instance.CompilationFinished,
                    AfterAssemblyReload = TraceData.instance.AfterAssemblyReload,
                    BeforeAssemblyReload = TraceData.instance.BeforeAssemblyReload,
                    
                    compilationData = beeData.traceEvents
                        .Where(x => x.name.Equals("Csc", StringComparison.Ordinal) && x.args.detail != null)
                        .Select(x => new AssemblyCompilationData
                        {
                            assembly = "Library/ScriptAssemblies/" +
                                       Path.GetFileName(x.args.detail.Split(' ').FirstOrDefault()),
                            StartTime = new DateTime((x.ts + offsetToFirstTs) * ticksPerMicrosecond),
                            EndTime = new DateTime((x.ts + offsetToFirstTs + x.dur) * ticksPerMicrosecond)
                        })
                        // hack to remove double Csc entries in trace file
                        .ToLookup(x => x.assembly, x => x)
                        .Select(x => x.First())
                        // end hack
                        .ToList()
                };

                // fix up incorrect reported compilation times
                if (cc.compilationData != null && cc.compilationData.Any())
                {
                    // fix reported start/end times for compilation
                    var minStart = cc.compilationData.Min(x => x.StartTime);
                    var maxEnd = cc.compilationData.Max(x => x.StartTime);
                    if (minStart < cc.CompilationStarted) cc.CompilationStarted = minStart;
                    if (maxEnd > cc.CompilationFinished) cc.CompilationFinished = maxEnd;
                }

                return cc;
            }
            catch (IOException e)
            {
                Debug.LogWarning(
                    $"IOException when trying to fetch compilation data: {e}. Please try again. If the issue persists: {bugReportString}");
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"Couldn't fetch compilation data; the format has probably changed. {bugReportString}\n{e}");
                return null;
            }
        }

        private static readonly string bugReportString =
            $"Please report a bug at <a href=\"{newIssueUrl}\">{newIssueUrl}</a> and include the package + Unity version.";

        private const string newIssueUrl = "https://github.com/Xericore/unity-editor-development-benchmark/issues/new";

        public static void Clear()
        {
            if (File.Exists(ProfilerJson))
                File.Delete(ProfilerJson);
        }
    }
}