using UnityEngine;

namespace MRShowroom
{
    /// <summary>
    /// The branded box the product arrives in. Only describes the box; <see cref="HookSequence"/>
    /// animates it. The origin is the centre of the box's base, so it can be dropped onto a surface.
    /// </summary>
    public sealed class PackagingBox : MonoBehaviour
    {
        [SerializeField] private Transform body;
        [SerializeField] private Transform lid;
        [Tooltip("Soft light under and around the box.")]
        [SerializeField] private Renderer glow;
        [Tooltip("Poking or grabbing the box skips straight to the product.")]
        [SerializeField] private GrabTarget grab;
        [SerializeField] private Vector3 size = new Vector3(0.36f, 0.14f, 0.22f);

        public Transform Body => body;
        public Transform Lid => lid;
        public Renderer Glow => glow;
        public GrabTarget Grab => grab;
        public Vector3 Size => size;
    }
}
