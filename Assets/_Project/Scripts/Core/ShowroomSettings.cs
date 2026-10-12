using System;
using UnityEngine;

namespace MRShowroom
{
    /// <summary>The few things the user can set that should survive a restart. Kept in PlayerPrefs.</summary>
    public static class ShowroomSettings
    {
        private const string LeftHandedKey = "MRShowroom.LeftHanded";

        private static bool _loaded;
        private static bool _leftHanded;

        public static event Action Changed;

        /// <summary>Left-handers get the colour panel on the left of the product.</summary>
        public static bool LeftHanded
        {
            get
            {
                Load();
                return _leftHanded;
            }
            set
            {
                Load();
                if (_leftHanded == value)
                {
                    return;
                }

                _leftHanded = value;
                PlayerPrefs.SetInt(LeftHandedKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        private static void Load()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            _leftHanded = PlayerPrefs.GetInt(LeftHandedKey, 0) == 1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _loaded = false;
            Changed = null;
        }
    }
}
