using System.Collections;
using Meta.XR.EnvironmentDepth;
using UnityEngine;
using UnityEngine.XR;

namespace MRShowroom.MR
{
    /// <summary>
    /// Depth occlusion: real things in front of the product (above all the user's own hands) hide
    /// it. Switches the Depth API on once the headset supports it and the user has allowed spatial
    /// data, and can switch it off again for the exploded view if that moment is over the frame
    /// budget. Only the product's shader samples depth, so the cost stays on the product.
    /// </summary>
    public sealed class OcclusionToggle : MonoBehaviour
    {
        [SerializeField] private EnvironmentDepthManager depthManager;
        [Tooltip("Soft occlusion blends the edge between real and virtual. Hard is cheaper and crisper.")]
        [SerializeField] private bool softOcclusion = true;
        [Tooltip("Turn occlusion off while the product is apart. Leave off unless the exploded view drops frames.")]
        [SerializeField] private bool offWhileExploded;
        [SerializeField] private float startupTimeout = 5f;

        private bool _available;
        private bool _exploded;

        /// <summary>True while real objects are hiding the product.</summary>
        public bool IsActive => depthManager != null && depthManager.enabled;

        private void OnEnable()
        {
            ShowroomEvents.Exploded += HandleExploded;
            ShowroomEvents.Reassembled += HandleReassembled;
        }

        private void OnDisable()
        {
            ShowroomEvents.Exploded -= HandleExploded;
            ShowroomEvents.Reassembled -= HandleReassembled;
        }

        private IEnumerator Start()
        {
            if (depthManager == null)
            {
                yield break;
            }

            depthManager.enabled = false;

            // Support can only be asked once XR is running; asked earlier, the answer is "no" and it sticks.
            float waited = 0f;
            while (!XRSettings.isDeviceActive && waited < startupTimeout)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!XRSettings.isDeviceActive || !EnvironmentDepthManager.IsSupported)
            {
                yield break;
            }

            // The depth sensor sits behind the same permission as the room scan.
            var poll = new WaitForSecondsRealtime(0.5f);
            while (!ScenePermission.Granted)
            {
                yield return poll;
            }

            _available = true;
            Refresh();
        }

        private void HandleExploded(ExplodeTrigger trigger)
        {
            _exploded = true;
            Refresh();
        }

        private void HandleReassembled(ExplodeTrigger trigger)
        {
            _exploded = false;
            Refresh();
        }

        private void Refresh()
        {
            if (!_available)
            {
                return;
            }

            bool wanted = !(offWhileExploded && _exploded);
            if (wanted)
            {
                depthManager.OcclusionShadersMode = softOcclusion
                    ? OcclusionShadersMode.SoftOcclusion
                    : OcclusionShadersMode.HardOcclusion;
            }

            if (depthManager.enabled != wanted)
            {
                depthManager.enabled = wanted;
            }
        }
    }
}
