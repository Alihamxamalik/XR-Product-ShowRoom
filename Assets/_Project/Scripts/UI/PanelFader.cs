using System.Collections.Generic;
using UnityEngine;

namespace MRShowroom
{
    /// <summary>
    /// Shows and hides a world-space panel with a short fade and a slight grow, and makes sure a
    /// hidden panel can't be seen, poked or pointed at.
    /// </summary>
    public sealed class PanelFader : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup group;
        [Tooltip("The Interaction SDK's poke and ray surfaces for this canvas. Switched off with the panel.")]
        [SerializeField] private GameObject[] interaction = new GameObject[0];
        [SerializeField] private float speed = 8f;
        [SerializeField] private bool startShown;

        private static readonly List<PanelFader> Visible = new List<PanelFader>(4);

        private Vector3 _size = Vector3.one;
        private float _alpha;
        private bool _shown;

        public bool IsShown => _shown;

        /// <summary>
        /// True if the ray reaches a visible panel before it has travelled <paramref name="maxDistance"/>.
        /// Lets "pinch to place" ignore a pinch that was really aimed at a button.
        /// </summary>
        public static bool BlocksRay(Ray ray, float maxDistance)
        {
            for (int i = 0; i < Visible.Count; i++)
            {
                var rect = Visible[i].transform as RectTransform;
                if (rect == null)
                {
                    continue;
                }

                var plane = new Plane(rect.forward, rect.position);
                if (!plane.Raycast(ray, out float distance) || distance > maxDistance)
                {
                    continue;
                }

                Vector3 local = rect.InverseTransformPoint(ray.GetPoint(distance));
                if (rect.rect.Contains(new Vector2(local.x, local.y)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True if the point is within <paramref name="margin"/> metres of a visible panel.</summary>
        public static bool IsNear(Vector3 point, float margin)
        {
            for (int i = 0; i < Visible.Count; i++)
            {
                var rect = Visible[i].transform as RectTransform;
                if (rect == null)
                {
                    continue;
                }

                Vector3 local = rect.InverseTransformPoint(point);
                float scale = Mathf.Max(rect.lossyScale.x, 0.00001f);
                float pad = margin / scale;
                Rect area = rect.rect;
                if (local.x > area.xMin - pad && local.x < area.xMax + pad
                    && local.y > area.yMin - pad && local.y < area.yMax + pad && Mathf.Abs(local.z) < pad)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnDisable()
        {
            Visible.Remove(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Visible.Clear();
        }

        private void Awake()
        {
            _size = transform.localScale;
            _shown = startShown;
            _alpha = startShown ? 1f : 0f;
            if (startShown && !Visible.Contains(this))
            {
                Visible.Add(this);
            }

            Apply();
        }

        public void Show()
        {
            _shown = true;
            SetInteraction(true);
            if (!Visible.Contains(this))
            {
                Visible.Add(this);
            }
        }

        public void Hide()
        {
            _shown = false;
            SetInteraction(false);
            Visible.Remove(this);
        }

        private void Update()
        {
            float target = _shown ? 1f : 0f;
            if (Mathf.Approximately(_alpha, target))
            {
                return;
            }

            // Unscaled, so a panel can still open and close while the experience is paused.
            _alpha = Mathf.MoveTowards(_alpha, target, speed * Time.unscaledDeltaTime);
            Apply();
        }

        private void Apply()
        {
            bool visible = _alpha > 0.001f;
            if (canvas != null && canvas.enabled != visible)
            {
                canvas.enabled = visible;
            }

            if (group != null)
            {
                group.alpha = _alpha;
                group.blocksRaycasts = _shown;
                group.interactable = _shown;
            }

            transform.localScale = _size * Mathf.Lerp(0.88f, 1f, Ease.OutCubic(_alpha));
            if (!visible)
            {
                SetInteraction(false);
            }
        }

        private void SetInteraction(bool active)
        {
            for (int i = 0; i < interaction.Length; i++)
            {
                if (interaction[i] != null && interaction[i].activeSelf != active)
                {
                    interaction[i].SetActive(active);
                }
            }
        }
    }
}
