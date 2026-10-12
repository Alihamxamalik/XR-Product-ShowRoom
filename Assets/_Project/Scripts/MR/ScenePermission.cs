using System;
using UnityEngine;
using UnityEngine.Android;

namespace MRShowroom.MR
{
    /// <summary>
    /// The spatial data permission (USE_SCENE). The room scan, environment raycasting and depth
    /// occlusion all sit behind it, so the app asks once, itself, and learns the answer either way.
    ///
    /// OVRManager's "request on startup" option is deliberately left off: it gives no callback on
    /// refusal, and a second request made while its dialog is open fails silently.
    /// </summary>
    public static class ScenePermission
    {
        /// <summary>Granted, or not needed (in the editor and on anything that isn't a headset build).</summary>
        public static bool Granted
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.ScenePermission);
#else
                return true;
#endif
            }
        }

        /// <param name="answered">Called once with whether the permission was granted.</param>
        public static void Request(Action<bool> answered)
        {
            if (Granted)
            {
                answered(true);
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => answered(true);
            callbacks.PermissionDenied += _ => answered(false);
            Permission.RequestUserPermission(OVRPermissionsRequester.ScenePermission, callbacks);
#else
            answered(true);
#endif
        }
    }
}
