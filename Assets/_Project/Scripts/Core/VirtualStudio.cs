using UnityEngine;

namespace MRShowroom
{
    /// <summary>
    /// The fallback set for when the real room can't be shown (no passthrough: the editor, or a
    /// headset without it). Shows its content only then; in mixed reality the room replaces it.
    /// Put this on an always-active parent and assign the set dressing as <see cref="content"/>.
    /// </summary>
    public sealed class VirtualStudio : MonoBehaviour
    {
        [SerializeField] private GameObject content;

        private void OnEnable()
        {
            MixedReality.Changed += Apply;
            Apply(MixedReality.PassthroughActive);
        }

        private void OnDisable()
        {
            MixedReality.Changed -= Apply;
        }

        private void Apply(bool passthroughActive)
        {
            if (content != null)
            {
                content.SetActive(!passthroughActive);
            }
        }
    }
}
