using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.XR;
#endif

namespace MRShowroom.MR
{
    /// <summary>
    /// Editor only: gives the camera rig the mouse-and-keyboard head from <see cref="DesktopHead"/>
    /// when no headset is driving it. Does nothing once a device is active, and nothing in builds.
    ///
    /// Two cases are covered. Where the Meta plugin is running without a headset, the rig places
    /// the eyes itself from OVRManager's head-pose offsets, so those are set. Where the plugin
    /// isn't running at all (the editor on macOS) nothing places the eyes, so they are placed here.
    /// </summary>
    public sealed class EditorHeadEmulation : MonoBehaviour
    {
#if UNITY_EDITOR
        private OVRCameraRig _rig;

        private void OnEnable()
        {
            Application.onBeforeRender += Apply;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= Apply;
        }

        private void Update()
        {
            Apply();
        }

        private void Apply()
        {
            if (!DesktopHead.Active || XRSettings.isDeviceActive)
            {
                return;
            }

            OVRManager manager = OVRManager.instance;
            if (manager != null)
            {
                manager.headPoseRelativeOffsetTranslation = DesktopHead.Position;
                // The rig applies this as Euler(-x, -y, z).
                manager.headPoseRelativeOffsetRotation = new Vector3(-DesktopHead.Pitch, -DesktopHead.Yaw, 0f);
            }

            if (_rig == null)
            {
                _rig = FindFirstObjectByType<OVRCameraRig>();
                if (_rig == null)
                {
                    return;
                }
            }

            // In the rig's tracking space, which stays on the floor origin: that is what a room is placed against.
            Quaternion rotation = Quaternion.Euler(DesktopHead.Pitch, DesktopHead.Yaw, 0f);
            Place(_rig.centerEyeAnchor, rotation);
            Place(_rig.leftEyeAnchor, rotation);
            Place(_rig.rightEyeAnchor, rotation);
        }

        private static void Place(Transform eye, Quaternion rotation)
        {
            if (eye != null)
            {
                eye.localPosition = DesktopHead.Position;
                eye.localRotation = rotation;
            }
        }
#endif
    }
}
