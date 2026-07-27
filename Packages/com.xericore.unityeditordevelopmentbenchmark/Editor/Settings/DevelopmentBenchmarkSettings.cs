using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityEditorDevelopmentBenchmark.Editor
{
    /// <summary>
    /// Project-scoped settings for the Development Benchmark package. Persisted as a JSON file
    /// under ProjectSettings/ so the settings are shared across the project (and can be checked
    /// into version control) rather than being stored per-user via EditorPrefs.
    /// </summary>
    [System.Serializable]
    public class DevelopmentBenchmarkSettings : ScriptableObject
    {
        private const string SettingsPath = "ProjectSettings/DevelopmentBenchmarkSettings.asset";

        [SerializeField]
        private SceneAsset _lightmapBenchmarkScene;

        public SceneAsset LightmapBenchmarkScene => _lightmapBenchmarkScene;

        private static DevelopmentBenchmarkSettings _instance;

        public static DevelopmentBenchmarkSettings GetOrCreateSettings()
        {
            if (_instance != null)
                return _instance;

            _instance = CreateInstance<DevelopmentBenchmarkSettings>();

            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                EditorJsonUtility.FromJsonOverwrite(json, _instance);
            }

            return _instance;
        }

        public static void Save()
        {
            if (_instance == null)
                return;

            var json = EditorJsonUtility.ToJson(_instance, true);
            File.WriteAllText(SettingsPath, json);
        }

        public static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }
    }
}
