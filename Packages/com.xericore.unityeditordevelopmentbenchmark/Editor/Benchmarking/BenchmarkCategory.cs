using UnityEditorDevelopmentBenchmark.Editor.UserWaitTimeTrackers;

namespace UnityEditorDevelopmentBenchmark.Editor.Benchmarking
{
    /// <summary>
    /// Categories of editor wait time that <see cref="BenchmarkRunner"/> can measure, mirroring the categories
    /// already tracked live by the <see cref="UserWaitTimeTrackerBase"/> subclasses in this namespace.
    /// </summary>
    /// <remarks>
    /// Only <see cref="PlayModeSwitch"/> is currently driven by <see cref="BenchmarkRunner"/>. The remaining
    /// values are stubs for categories the benchmark does not yet exercise.
    /// </remarks>
    public enum BenchmarkCategory
    {
        /// <summary>
        /// Time spent entering and exiting play mode. Implemented, driven by <see cref="BenchmarkRunner"/>.
        /// </summary>
        PlayModeSwitch,

        /// <summary>
        /// Stub. Not yet driven by <see cref="BenchmarkRunner"/>.
        /// </summary>
        Compilation,

        /// <summary>
        /// Stub. Not yet driven by <see cref="BenchmarkRunner"/>.
        /// </summary>
        DomainReload,

        /// <summary>
        /// Stub. Not yet driven by <see cref="BenchmarkRunner"/>.
        /// </summary>
        AssetImport,

        /// <summary>
        /// Stub. Not yet driven by <see cref="BenchmarkRunner"/>.
        /// </summary>
        Build,

        /// <summary>
        /// Stub. Not yet driven by <see cref="BenchmarkRunner"/>.
        /// </summary>
        EditorStartup,

        /// <summary>
        /// Stub. Not yet driven by <see cref="BenchmarkRunner"/>.
        /// </summary>
        LightmapBaking
    }
}
