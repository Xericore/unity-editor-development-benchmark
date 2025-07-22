using System;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace UnityEditorDevelopmentBenchmark.Editor.Serialization
{
    internal class TraceData : ScriptableSingleton<TraceData>, ISerializationCallbackReceiver
    {
        public SerializableDateTime
            compilationStarted;

        public SerializableDateTime
            compilationFinished;

        public SerializableDateTime
            beforeAssemblyReload;

        public SerializableDateTime
            afterAssemblyReload;

        public DateTime CompilationFinished;
        public DateTime CompilationStarted;
        public DateTime AfterAssemblyReload;
        public DateTime BeforeAssemblyReload;

        public void OnBeforeSerialize()
        {
            compilationStarted = CompilationStarted;
            compilationFinished = CompilationFinished;
            afterAssemblyReload = AfterAssemblyReload;
            beforeAssemblyReload = BeforeAssemblyReload;
        }

        public void OnAfterDeserialize()
        {
            CompilationStarted = compilationStarted;
            CompilationFinished = compilationFinished;
            AfterAssemblyReload = afterAssemblyReload;
            BeforeAssemblyReload = beforeAssemblyReload;
        }

        [InitializeOnLoadMethod]
        private static void InitCompilationEvents()
        {
#if UNITY_2019_1_OR_NEWER
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
#endif
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        }

        private static void OnCompilationStarted(object obj)
        {
            instance.CompilationStarted = DateTime.Now;
        }

        private static void OnCompilationFinished(object obj)
        {
            instance.CompilationFinished = DateTime.Now;
        }

        private static void OnBeforeAssemblyReload()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                instance.BeforeAssemblyReload = DateTime.Now;
            }
        }

        private static void OnAfterAssemblyReload()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                instance.AfterAssemblyReload = DateTime.Now;
            }
        }
    }
}