#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityEditorDevelopmentBenchmark.Editor.Util
{
    /// Source: https://gist.github.com/Seneral/388ab29807cecca9f2ab0df6ca657ee9
	[InitializeOnLoad]
	public static class SwitchPlayModeUtil 
	{
		private static Scene _loadedScene;

		private static bool _serializationTest;
		private static bool _playmodeSwitchToEdit;
		private static bool _toggleLateEnteredPlaymode;

		public static Action BeforeEnteringPlayMode;
		public static Action JustEnteredPlayMode;
		public static Action LateEnteredPlayMode;
		public static Action BeforeLeavingPlayMode;
		public static Action JustLeftPlayMode;

		static SwitchPlayModeUtil () 
		{
			EditorApplication.playModeStateChanged -= PlaymodeStateChanged;
			EditorApplication.playModeStateChanged += PlaymodeStateChanged;
            
			EditorApplication.update -= Update;
			EditorApplication.update += Update;
		}
        
		private static void Update () 
		{
			if (_toggleLateEnteredPlaymode)
            {
                _toggleLateEnteredPlaymode = false;
                LateEnteredPlayMode?.Invoke();
            }
			_serializationTest = true;
		}

		private static void PlaymodeStateChanged (PlayModeStateChange playModeStateChange) 
		{
			if (!Application.isPlaying)
			{ 
				if (_playmodeSwitchToEdit)
                {
                    JustLeftPlayMode?.Invoke();
                    _playmodeSwitchToEdit = false;
                }
				else
                {
                    BeforeEnteringPlayMode?.Invoke();
                }
			}
			else
			{ 
				if (_serializationTest)
                {
                    BeforeLeavingPlayMode?.Invoke();
                    _playmodeSwitchToEdit = true;
                }
				else
                {
                    JustEnteredPlayMode?.Invoke();
                    _toggleLateEnteredPlaymode = true;
                }
			}
		}
	}
}
#endif