using UnityEngine;

namespace MRShowroom
{
    /// <summary>
    /// Everything the showroom needs to know about one product. A new client product is a new
    /// model turned into a prefab (MR Showroom > Product > Create Product From Selected Model)
    /// plus one of these assets; no code changes.
    /// </summary>
    [CreateAssetMenu(menuName = "MR Showroom/Product Definition", fileName = "Product")]
    public sealed class ProductDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "product";
        public string displayName = "Product";
        public string tagline;
        public string price = "$0";
        [Tooltip("Store page opened by Buy now. Leave empty for the mock confirmation used in the portfolio build.")]
        public string ctaUrl;

        [Header("Model")]
        public ProductView prefab;
        [Tooltip("The box the product arrives in during the hook. Leave empty to skip the unboxing.")]
        public PackagingBox packaging;
        [Tooltip("Direction a colour change sweeps across the product, in the product's own space.")]
        public Vector3 sweepAxis = Vector3.right;
        [Tooltip("Soft footprint left behind during the reveal walk.")]
        public Sprite footprint;
        [Tooltip("Length and width of one footprint (m).")]
        public Vector2 footprintSize = new Vector2(0.29f, 0.11f);

        [Header("Customisation")]
        public ColorSlot[] colorSlots = new ColorSlot[0];
        [Tooltip("Which colour slot the material finishes apply to.")]
        public int finishSlot;
        public FinishOption[] finishes = new FinishOption[0];
        [Tooltip("Colourways designed by the brand. The first one is what the product starts in.")]
        public Colorway[] presets = new Colorway[0];

        [Header("Inspection")]
        public HotspotData[] hotspots = new HotspotData[0];

        [Header("Exploded view")]
        public ExplodePart[] parts = new ExplodePart[0];

        public ExplodePart FindPart(string partName)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].partName == partName)
                {
                    return parts[i];
                }
            }

            return null;
        }
    }
}
