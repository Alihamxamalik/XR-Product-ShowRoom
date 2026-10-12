using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MRShowroom
{
    /// <summary>
    /// First thing that runs: app-wide settings, then straight on to the showroom. The scene is
    /// deliberately almost empty so the app gets from the launcher to passthrough as fast as it can.
    ///
    /// The spatial data permission and the display refresh rate are handled inside the Showroom
    /// scene rather than here, because both need the camera rig: the permission prompt is shown
    /// over passthrough with a line explaining why, and the refresh rate is set once the headset
    /// reports what it offers.
    /// </summary>
    public sealed class AppBootstrap : MonoBehaviour
    {
        public const string BootstrapScene = "Bootstrap";
        public const string ShowroomScene = "Showroom";

        [SerializeField] private string firstScene = ShowroomScene;

        private void Awake()
        {
            // The headset's compositor paces the frames; a software cap on top would only add judder.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        private IEnumerator Start()
        {
            yield return SceneManager.LoadSceneAsync(firstScene, LoadSceneMode.Single);
        }
    }
}
