using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace UnityEditorDevelopmentBenchmark.Editor.Settings
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

                    // Settings fields will be added here as they are defined.

                    root.Bind(settings);

                    root.RegisterCallback<SerializedPropertyChangeEvent>(_ => DevelopmentBenchmarkSettings.Save());
                },
                keywords = new HashSet<string>(new[] { "Development", "Benchmark", "Wait Time" })
            };

            return provider;
        }
    }
}
