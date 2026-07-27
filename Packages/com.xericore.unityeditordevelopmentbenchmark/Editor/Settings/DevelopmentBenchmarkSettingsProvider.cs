using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
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
                    var settings = DevelopmentBenchmarkSettings.GetSerializedSettings();

                    var root = new VisualElement
                    {
                        style = { marginLeft = 8, marginTop = 8 }
                    };
                    rootElement.Add(root);

                    var lightmapBenchmarkSceneField = new ObjectField("Lightmap Benchmark Scene")
                    {
                        objectType = typeof(SceneAsset),
                        allowSceneObjects = false,
                        bindingPath = "_lightmapBenchmarkScene"
                    };
                    root.Add(lightmapBenchmarkSceneField);

                    root.Bind(settings);

                    root.RegisterCallback<SerializedPropertyChangeEvent>(_ => DevelopmentBenchmarkSettings.Save());
                },
                keywords = new HashSet<string>(new[] { "Development", "Benchmark", "Wait Time" })
            };

            return provider;
        }
    }
}
