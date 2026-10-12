using UnityEngine;

namespace MRShowroom
{
    /// <summary>
    /// Where the pretend head is when the editor has no headset. <see cref="EditorDesktopControls"/>
    /// writes it; the mixed reality layer feeds it to the camera rig as an emulated head pose.
    ///
    /// The rig itself is never moved to fake a head: its tracking space has to stay on the floor
    /// origin, because that is what a scanned (or sample) room is positioned against.
    /// </summary>
    public static class DesktopHead
    {
        public static bool Active { get; private set; }
        public static Vector3 Position { get; private set; }
        /// <summary>Degrees, positive looking down.</summary>
        public static float Pitch { get; private set; }
        public static float Yaw { get; private set; }

        public static void Set(Vector3 position, float pitch, float yaw)
        {
            Active = true;
            Position = position;
            Pitch = pitch;
            Yaw = yaw;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = false;
            Position = Vector3.zero;
            Pitch = 0f;
            Yaw = 0f;
        }
    }
}
