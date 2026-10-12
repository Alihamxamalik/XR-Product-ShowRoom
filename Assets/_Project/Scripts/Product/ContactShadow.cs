using UnityEngine;

namespace MRShowroom
{
    /// <summary>
    /// The soft blob under the product that ties it to the table. It follows the product across
    /// the surface, and shrinks and fades as the product is lifted, the way a real contact shadow does.
    /// </summary>
    public sealed class ContactShadow : MonoBehaviour
    {
        [SerializeField] private ProductController product;
        [SerializeField] private Renderer blob;
        [Tooltip("Shadow size as a multiple of the product's footprint.")]
        [SerializeField] private float padding = 1.35f;
        [Tooltip("Height at which the shadow has faded out completely (m).")]
        [SerializeField] private float fadeHeight = 0.35f;
        [Tooltip("Size at that height, relative to the size on the surface.")]
        [SerializeField] [Range(0.2f, 1f)] private float liftedScale = 0.6f;
        [SerializeField] [Range(0f, 1f)] private float strength = 0.55f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private MaterialPropertyBlock _block;
        private Transform _surface;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _surface = transform.parent;
        }

        private void LateUpdate()
        {
            ProductView view = product != null ? product.View : null;
            bool visible = view != null && product.IsVisible && _surface != null;
            if (blob.enabled != visible)
            {
                blob.enabled = visible;
            }

            if (!visible)
            {
                return;
            }

            // Where the product is relative to the surface it rests on.
            Transform root = view.transform;
            Vector3 local = _surface.InverseTransformPoint(root.position);
            float height = Mathf.Max(local.y, 0f);
            float lift = Mathf.Clamp01(height / fadeHeight);

            Bounds bounds = view.LocalBounds;
            float scale = root.lossyScale.x * padding * Mathf.Lerp(1f, liftedScale, lift);
            // Opened up, the product covers more table.
            scale *= 1f + 0.35f * product.Explode.Amount;

            transform.localPosition = new Vector3(local.x, 0.001f, local.z);
            transform.localRotation = Quaternion.Euler(0f, root.localEulerAngles.y, 0f);
            transform.localScale = new Vector3(bounds.size.x * scale, 1f, bounds.size.z * scale * 1.25f);

            float alpha = strength * (1f - lift) * (1f - lift);
            _block.SetColor(BaseColorId, new Color(0f, 0f, 0f, alpha));
            blob.SetPropertyBlock(_block);
        }
    }
}
