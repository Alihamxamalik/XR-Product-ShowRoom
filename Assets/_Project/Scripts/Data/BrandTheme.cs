using TMPro;
using UnityEngine;

namespace MRShowroom
{
    /// <summary>The client's brand kit: logo, colours, typeface and sounds. One per brand.</summary>
    [CreateAssetMenu(menuName = "MR Showroom/Brand Theme", fileName = "BrandTheme")]
    public sealed class BrandTheme : ScriptableObject
    {
        [Header("Identity")]
        public string brandName = "BRAND";
        public string tagline;
        public Sprite logo;
        public TMP_FontAsset font;

        [Header("Colours")]
        [Tooltip("Buttons, highlights and glows.")]
        public Color primary = new Color(0.72f, 0.92f, 0.35f);
        [Tooltip("Text and icons drawn on top of the primary colour.")]
        public Color onPrimary = new Color(0.07f, 0.09f, 0.11f);
        public Color secondary = new Color(0.07f, 0.09f, 0.11f);
        [Tooltip("Card background. Kept translucent so the room shows through it.")]
        public Color panel = new Color(0.07f, 0.09f, 0.11f, 0.82f);
        public Color text = new Color(0.96f, 0.97f, 1f);
        public Color mutedText = new Color(0.66f, 0.71f, 0.78f);

        [Header("Sounds")]
        public AudioClip boxThud;
        public AudioClip lidPop;
        public AudioClip grab;
        public AudioClip release;
        public AudioClip swatchClick;
        public AudioClip sweep;
        public AudioClip explodeWhoosh;
        public AudioClip reassembleSnap;
        public AudioClip hotspotOpen;
        public AudioClip uiPress;
        public AudioClip step;
        public AudioClip placed;
        public AudioClip confirm;

        [Header("Music")]
        [Tooltip("Played under the hook and the end card only.")]
        public AudioClip musicBed;
        [Range(0f, 1f)] public float musicVolume = 0.3f;

        public AudioClip Clip(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.BoxThud: return boxThud;
                case Sfx.LidPop: return lidPop;
                case Sfx.Grab: return grab;
                case Sfx.Release: return release;
                case Sfx.SwatchClick: return swatchClick;
                case Sfx.Sweep: return sweep;
                case Sfx.ExplodeWhoosh: return explodeWhoosh;
                case Sfx.ReassembleSnap: return reassembleSnap;
                case Sfx.HotspotOpen: return hotspotOpen;
                case Sfx.UiPress: return uiPress;
                case Sfx.Step: return step;
                case Sfx.Placed: return placed;
                case Sfx.Confirm: return confirm;
                default: return null;
            }
        }
    }
}
