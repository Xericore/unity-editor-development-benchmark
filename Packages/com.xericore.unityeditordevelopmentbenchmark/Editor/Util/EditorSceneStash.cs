using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace UnityEditorDevelopmentBenchmark.Editor.Util
{
    /// <summary>
    /// Closes whatever scene(s) are currently open, remembering their paths under a caller-chosen
    /// <see cref="SessionState"/> key so they can be reopened later via <see cref="Restore"/> - without Unity
    /// prompting to save/reload the scene file(s) it's about to replace out from under the caller. Extracted out
    /// of individual benchmark categories (originally AssetImport and LightmapBaking) that both need to swap away
    /// from, and later back to, the user's real open scene(s).
    /// </summary>
    public static class EditorSceneStash
    {
        /// <summary>
        /// Closes every currently open scene and records their paths (under <paramref name="sessionKey"/>) so
        /// <see cref="Restore"/> can reopen them later.
        /// </summary>
        /// <param name="sessionKey">Identifies the matching <see cref="Restore"/> call.</param>
        /// <param name="promptToSaveModifiedScenes">
        /// Whether to show Unity's normal blocking "Do you want to save the changes..." modal if the currently
        /// open scene(s) are dirty (same prompt the user would get anyway when Unity is about to discard/replace
        /// them), rather than silently saving any unsaved modifications without asking. Pass <c>false</c> only
        /// when the currently open scene is known to be disposable (e.g. a temporary benchmark scene copy whose
        /// containing folder gets deleted right afterwards regardless of whether it got saved) and could
        /// otherwise show a confusingly identical-looking dialog about a scene the user never actually edited
        /// (e.g. because it shares its file name with the real scene it was copied from).
        /// </param>
        public static void Stash(string sessionKey, bool promptToSaveModifiedScenes)
        {
            var openScenePaths = new List<string>();
            for (var i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var path = EditorSceneManager.GetSceneAt(i).path;
                if (!string.IsNullOrEmpty(path))
                {
                    openScenePaths.Add(path);
                }
            }

            SessionState.SetString(sessionKey, string.Join(";", openScenePaths));

            if (promptToSaveModifiedScenes)
            {
                // We proceed regardless of the user's choice (save, don't save).
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            }
            else
            {
                SaveModifiedOpenScenesWithoutPrompting();
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        /// <summary>
        /// Reopens whatever scene(s) were open before the matching <see cref="Stash"/> call (identified by
        /// <paramref name="sessionKey"/>) closed them. A no-op if <paramref name="sessionKey"/> doesn't currently
        /// hold a stash (e.g. because <see cref="Stash"/> was never called, or this is a second, redundant call).
        /// </summary>
        /// <param name="sessionKey">Identifies the matching <see cref="Stash"/> call.</param>
        /// <param name="promptToSaveModifiedScenes">
        /// See <see cref="Stash"/>. Either way, resolving the currently open scene(s)' dirty state (by prompting
        /// or by saving silently) before reopening matters: without it, <see cref="EditorSceneManager.OpenScene"/>
        /// below would silently fail to switch away from a dirty scene (or block on a modal save prompt), leaving
        /// that scene as the active one even after this call returns - which can then cause a caller that deletes
        /// that scene's containing folder right afterwards to record its now-deleted path as part of the next
        /// <see cref="Stash"/> call, causing a "Scene file not found" error the next time <see cref="Restore"/>
        /// is called for that key.
        /// </param>
        public static void Restore(string sessionKey, bool promptToSaveModifiedScenes)
        {
            var joinedPaths = SessionState.GetString(sessionKey, string.Empty);
            SessionState.EraseString(sessionKey);

            if (string.IsNullOrEmpty(joinedPaths))
            {
                return;
            }

            if (promptToSaveModifiedScenes)
            {
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            }
            else
            {
                SaveModifiedOpenScenesWithoutPrompting();
            }

            var paths = joinedPaths.Split(';');
            for (var i = 0; i < paths.Length; i++)
            {
                if (string.IsNullOrEmpty(paths[i]))
                {
                    continue;
                }

                EditorSceneManager.OpenScene(paths[i], i == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive);
            }
        }

        /// <summary>
        /// Silently saves every currently open dirty scene to its own path (no confirmation dialog, unlike
        /// <see cref="EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo"/>), so a subsequent
        /// <see cref="EditorSceneManager.NewScene(NewSceneSetup, NewSceneMode)"/> or
        /// <see cref="EditorSceneManager.OpenScene"/> call can switch away from it without Unity blocking on (or
        /// silently no-oping because of) its unsaved changes.
        /// </summary>
        private static void SaveModifiedOpenScenesWithoutPrompting()
        {
            for (var i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (scene.isDirty)
                {
                    EditorSceneManager.SaveScene(scene);
                }
            }
        }
    }
}
