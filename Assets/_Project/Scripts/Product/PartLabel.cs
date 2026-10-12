using TMPro;
using UnityEngine;

namespace MRShowroom
{
    /// <summary>
    /// The name of a part in the exploded view: the text beside the product, a dot on the part
    /// itself and a hairline joining the two, so it is clear which part is meant even when another
    /// sits in between. It faces the viewer and keeps its real size, however much the product has
    /// been scaled up. <see cref="ExplodeController"/> places it every frame it is showing.
    /// </summary>
    public sealed class PartLabel : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;
        [SerializeField] private Renderer dot;
        [SerializeField] private Renderer line;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Gap between the end of the line and the text (m).")]
        [SerializeField] private float textGap = 0.008f;
        [Tooltip("Width of the line, dark edge included (m).")]
        [SerializeField] private float lineWidth = 0.004f;

        private MaterialPropertyBlock _block;
        private Vector3 _size = Vector3.one;
        private Color _dotColor = Color.white;
        private Color _lineColor = Color.white;
        private float _alpha = -1f;
        private float _side;

        private void Awake()
        {
            _size = transform.localScale;
            _block = new MaterialPropertyBlock();
            _dotColor = ColorOf(dot);
            _lineColor = ColorOf(line);
        }

        public void SetText(string label)
        {
            if (text != null)
            {
                text.text = label;
            }
        }

        /// <summary>
        /// Puts the text at <paramref name="position"/>, running away from the product
        /// (<paramref name="side"/>: +1 to the viewer's right, -1 to their left), the dot on
        /// <paramref name="target"/> and the line between them.
        /// </summary>
        public void Place(Vector3 position, float side, Vector3 target, Vector3 viewer)
        {
            RunTowards(side);

            Transform root = transform;
            Vector3 away = position - viewer;
            root.SetPositionAndRotation(position,
                away.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(away, Vector3.up) : root.rotation);

            Transform parent = root.parent;
            float parentScale = parent != null ? Mathf.Max(parent.lossyScale.x, 0.0001f) : 1f;
            root.localScale = _size / parentScale;

            if (dot != null)
            {
                Vector3 beyond = target - viewer;
                dot.transform.SetPositionAndRotation(target,
                    beyond.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(beyond, Vector3.up) : root.rotation);
            }

            if (line == null)
            {
                return;
            }

            // A flat strip from the text to the dot, turned about its own length to face the viewer.
            Vector3 along = target - position;
            Vector3 middle = (position + target) * 0.5f;
            Vector3 across = Vector3.Cross(middle - viewer, along);
            bool show = along.sqrMagnitude > 0.000004f && across.sqrMagnitude > 1e-10f;
            if (line.enabled != show)
            {
                line.enabled = show;
            }

            if (!show)
            {
                return;
            }

            across.Normalize();
            Transform strip = line.transform;
            strip.SetPositionAndRotation(middle, Quaternion.LookRotation(Vector3.Cross(along, across), across));
            float scale = Mathf.Max(root.lossyScale.x, 0.0001f);
            strip.localScale = new Vector3(along.magnitude / scale, lineWidth / scale, 1f);
        }

        public void SetAlpha(float alpha)
        {
            if (Mathf.Approximately(alpha, _alpha))
            {
                return;
            }

            _alpha = alpha;
            bool visible = alpha > 0.01f;
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            if (text != null)
            {
                text.alpha = alpha;
            }

            Tint(dot, _dotColor, alpha);
            Tint(line, _lineColor, alpha);
        }

        // The text runs away from the line's end, whichever side of the product that is.
        private void RunTowards(float side)
        {
            side = side < 0f ? -1f : 1f;
            if (text == null || Mathf.Approximately(side, _side))
            {
                return;
            }

            _side = side;
            RectTransform rect = text.rectTransform;
            rect.pivot = new Vector2(side > 0f ? 0f : 1f, 0.5f);
            rect.localPosition = new Vector3(textGap * side, 0f, 0f);
            text.alignment = side > 0f ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
        }

        private void Tint(Renderer target, Color color, float alpha)
        {
            if (target == null)
            {
                return;
            }

            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            color.a *= alpha;
            _block.SetColor(BaseColorId, color);
            target.SetPropertyBlock(_block);
        }

        private static Color ColorOf(Renderer target)
        {
            Material material = target != null ? target.sharedMaterial : null;
            return material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
        }
    }
}
