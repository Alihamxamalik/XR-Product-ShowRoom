using System;
using UnityEngine;

namespace MRShowroom
{
    /// <summary>
    /// One customisable region of a product (Upper, Sole, Laces). The region itself is painted into
    /// the product's tint mask; this says which shader properties drive it and which colours are on offer.
    /// </summary>
    [Serializable]
    public sealed class ColorSlot
    {
        public string name = "Upper";

        [Tooltip("Base name of the shader properties for this region. The Product Lit shader has _TintR, _TintG " +
                 "and _TintB, one per channel of the tint mask.")]
        public string shaderProperty = "_TintR";

        public ColorOption[] colors = new ColorOption[0];

        [Range(0f, 1f)] public float metallic;
        [Range(0f, 1f)] public float smoothness = 0.35f;
    }

    [Serializable]
    public struct ColorOption
    {
        public string name;
        public Color color;

        public ColorOption(string name, Color color)
        {
            this.name = name;
            this.color = color;
        }
    }

    /// <summary>A surface finish for one slot: matte, metallic and so on.</summary>
    [Serializable]
    public sealed class FinishOption
    {
        public string name = "Matte";
        [Range(0f, 1f)] public float metallic;
        [Range(0f, 1f)] public float smoothness = 0.35f;
    }

    /// <summary>A complete look: one colour per slot plus a finish.</summary>
    [Serializable]
    public sealed class Colorway
    {
        public string name = "Colourway";
        [Tooltip("Index into each slot's colour list, in slot order.")]
        public int[] colorIndices = new int[0];
        public int finishIndex;
    }
}
