using System;
using UnityEngine;

namespace MRShowroom
{
    /// <summary>How one part of the product behaves in the exploded view.</summary>
    [Serializable]
    public sealed class ExplodePart
    {
        [Tooltip("Name of the part in the product prefab.")]
        public string partName;
        [Tooltip("Shown next to the part once it has moved out.")]
        public string label;
        [Tooltip("Which way the part moves, in the product's own space.")]
        public Vector3 direction = Vector3.up;
        [Tooltip("How far it moves (m).")]
        public float distance = 0.1f;
        [Tooltip("Height of the label above the part's origin (m): usually the middle of the part.")]
        public float labelHeight = 0.02f;
        [Tooltip("Whether this part can be picked up on its own once the product is apart.")]
        public bool grabbable = true;
    }
}
