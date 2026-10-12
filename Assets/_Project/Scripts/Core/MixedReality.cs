using System;
using UnityEngine;

namespace MRShowroom
{
    /// <summary>
    /// Whether the real room is currently showing through. Set by the mixed reality layer
    /// (PassthroughController); read by anything that should only exist in the virtual studio.
    /// </summary>
    public static class MixedReality
    {
        public static event Action<bool> Changed;

        public static bool PassthroughActive { get; private set; }

        public static void SetPassthroughActive(bool active)
        {
            if (active == PassthroughActive)
            {
                return;
            }

            PassthroughActive = active;
            Changed?.Invoke(active);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            PassthroughActive = false;
            Changed = null;
        }
    }
}
