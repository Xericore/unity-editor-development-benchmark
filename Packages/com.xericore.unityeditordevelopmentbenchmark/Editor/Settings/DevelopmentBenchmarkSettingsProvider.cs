using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace UnityEditorDevelopmentBenchmark.Editor
{
    /// <summary>
    /// Registers the "Development Benchmark" page under Project Settings, backed by
    /// <see cref="DevelopmentBenchmarkSettings"/> for project-scoped persistence.
    /// </summary>
    internal static class DevelopmentBenchmarkSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateDevelopmentBenchmarkSettingsProvider()
        {
            var provider = new SettingsProvider("Project/Development Benchmark", SettingsScope.Project)
            {
                label = "Development Benchmark",
                activateHandler = (searchContext, rootElement) =>
                {
                    var settings = DevelopmentBenchmarkSettings.GetOrCreateSettings();

                    var root = new VisualElement
                    {
                        style = { marginLeft = 8, marginTop = 8 }
                    };
                    rootElement.Add(root);

                    var lightmapBenchmarkSceneField = new ObjectField("Lightmap Benchmark Scene")
                    {
                        objectType = typeof(SceneAsset),
                        allowSceneObjects = false,
                        value = settings.LightmapBenchmarkScene
                    };

                    // Read/write the settings object directly rather than going through a SerializedObject/Bind,
                    // whose binding system applies changes to the backing property on a scheduled tick rather
                    // than synchronously with the field's ChangeEvent. That would risk saving a stale value if a
                    // domain reload (e.g. one triggered by starting a benchmark run) happened to land between the
                    // user's edit and the next binding tick.
                    lightmapBenchmarkSceneField.RegisterValueChangedCallback(evt =>
                    {
                        settings.LightmapBenchmarkScene = (SceneAsset) evt.newValue;
                        DevelopmentBenchmarkSettings.Save();
                    });

                    root.Add(lightmapBenchmarkSceneField);
                },
                keywords = new HashSet<string>(new[] { "Development", "Benchmark", "Wait Time" })
            };

            return provider;
        }
    }
}
