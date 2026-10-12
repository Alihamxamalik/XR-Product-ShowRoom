using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace MRShowroom.EditorTools
{
    /// <summary>
    /// Builds start in the Bootstrap scene, so Play does too: whichever project scene is open,
    /// pressing Play runs the app the way a launch on the headset would.
    /// Scenes outside Assets/_Project/Scenes (test scenes, samples) are left alone.
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayFromBootstrap
    {
        private const string OwnsStartSceneKey = "MRShowroom.OwnsPlayModeStartScene";

        static PlayFromBootstrap()
        {
            EditorApplication.playModeStateChanged += HandlePlayModeChange;
        }

        private static void HandlePlayModeChange(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                Scene active = SceneManager.GetActiveScene();
                var bootstrap = AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneBuilder.BootstrapPath);
                bool ours = active.path.StartsWith(SceneBuilder.ScenesDir + "/");

                if (ours && bootstrap != null && active.path != SceneBuilder.BootstrapPath)
                {
                    EditorSceneManager.playModeStartScene = bootstrap;
                    SessionState.SetBool(OwnsStartSceneKey, true);
                }
            }
            else if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(OwnsStartSceneKey, false))
            {
                // Only undo what we set, so other tools that use the start scene keep working.
                EditorSceneManager.playModeStartScene = null;
                SessionState.EraseBool(OwnsStartSceneKey);
            }
        }
    }
}
