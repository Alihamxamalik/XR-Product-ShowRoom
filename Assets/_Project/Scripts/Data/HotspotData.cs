using System;
using UnityEngine;

namespace MRShowroom
{
    /// <summary>A glowing dot on a feature of the product. Poking it opens a short label card.</summary>
    [Serializable]
    public sealed class HotspotData
    {
        public string title = "Feature";
        [Tooltip("One short line.")]
        public string text;
        [Tooltip("Name of the part the dot rides on, so it stays with that part. Empty = the product itself.")]
        public string partName;
        [Tooltip("Where the dot sits, in the product's own space with the product assembled (m).")]
        public Vector3 localPosition;
        public Sprite icon;
    }
}
