using System.Collections;
using UnityEngine;
using UnityEngine.XR;

namespace MRShowroom.MR
{
    /// <summary>
    /// Makes the real room the background. The eye camera clears to transparent so the passthrough
    /// underlay shows through, the room fades in from black at launch instead of popping, and the
    /// hook can dim it for its spotlight moment. Where passthrough isn't available (the editor
    /// without a headset) it falls back to the virtual studio.
    /// </summary>
    public sealed class PassthroughController : MonoBehaviour
    {
        [SerializeField] private OVRManager manager;
        [SerializeField] private OVRPassthroughLayer passthroughLayer;
        [SerializeField] private Camera eyeCamera;
        [SerializeField] private Color virtualBackground = new Color(0.78f, 0.8f, 0.83f, 1f);
        [Tooltip("How long the room takes to fade in at launch (s).")]
        [SerializeField] private float fadeIn = 0.5f;
        [Tooltip("How long to wait for the headset before deciding passthrough isn't available (s).")]
        [SerializeField] private float startupTimeout = 3f;
        [Tooltip("Passthrough brightness at full spotlight (-1 to 0).")]
        [SerializeField] [Range(-1f, 0f)] private float spotlightBrightness = -0.35f;
        [SerializeField] [Range(-1f, 1f)] private float spotlightContrast = 0.1f;
        [SerializeField] [Range(-1f, 0f)] private float spotlightSaturation = -0.2f;

        private bool _active;
        private float _spotlight;

        /// <summary>True once the app knows whether it is running in mixed reality or the virtual studio.</summary>
        public bool IsReady { get; private set; }

        private void OnEnable()
        {
            ShowroomEvents.SpotlightChanged += HandleSpotlight;
        }

        private void OnDisable()
        {
            ShowroomEvents.SpotlightChanged -= HandleSpotlight;
        }

        private IEnumerator Start()
        {
            // Start in the virtual studio; passthrough support can only be asked once XR is running.
            Apply(false);

            float waited = 0f;
            while (!XRSettings.isDeviceActive && waited < startupTimeout)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            bool supported = XRSettings.isDeviceActive && OVRManager.IsInsightPassthroughSupported();
            if (!supported)
            {
                IsReady = true;
                yield break;
            }

            // Opacity zero over a transparent clear is black, so raising it is a fade from black.
            passthroughLayer.textureOpacity = 0f;
            Apply(true);
            IsReady = true;

            for (float timer = 0f; timer < fadeIn; timer += Time.unscaledDeltaTime)
            {
                passthroughLayer.textureOpacity = timer / fadeIn;
                yield return null;
            }

            passthroughLayer.textureOpacity = 1f;
        }

        private void Apply(bool passthrough)
        {
            _active = passthrough;

            if (manager != null)
            {
                manager.isInsightPassthroughEnabled = passthrough;
                // Nobody walks anywhere in this experience, so the boundary wall would only cut through the room.
                manager.shouldBoundaryVisibilityBeSuppressed = passthrough;
            }

            if (passthroughLayer != null)
            {
                passthroughLayer.enabled = passthrough;
            }

            if (eyeCamera != null)
            {
                eyeCamera.clearFlags = CameraClearFlags.SolidColor;
                eyeCamera.backgroundColor = passthrough ? Color.clear : virtualBackground;
            }

            MixedReality.SetPassthroughActive(passthrough);
        }

        private void HandleSpotlight(float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (Mathf.Approximately(amount, _spotlight))
            {
                return;
            }

            _spotlight = amount;
            if (!_active || passthroughLayer == null)
            {
                return;
            }

            if (amount <= 0f)
            {
                passthroughLayer.DisableColorMap();
            }
            else
            {
                passthroughLayer.SetBrightnessContrastSaturation(spotlightBrightness * amount,
                    spotlightContrast * amount, spotlightSaturation * amount);
            }
        }
    }
}
