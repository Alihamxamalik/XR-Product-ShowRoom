using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MRShowroom
{
    /// <summary>
    /// A small glowing dot on a feature of the product. Poking it (or clicking it with a ray)
    /// opens a short label card; opening another closes this one. The whole thing always faces
    /// the viewer and keeps its real size as the product is scaled, and the card floats in front
    /// of the product, so a dot on the far side or underneath doesn't open its card inside it.
    /// </summary>
    public sealed class HotspotView : MonoBehaviour
    {
        [SerializeField] private Button dotButton;
        [SerializeField] private RectTransform dot;
        [SerializeField] private CanvasGroup dotGroup;
        [SerializeField] private CanvasGroup card;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Image icon;
        [Tooltip("The card closes by itself after this long (s).")]
        [SerializeField] private float openTime = 5f;
        [SerializeField] private float pulseSpeed = 2.2f;
        [SerializeField] private float pulseAmount = 0.16f;
        [SerializeField] private float fadeSpeed = 8f;
        [Tooltip("The open card keeps this far in front of the product, towards the viewer (m).")]
        [SerializeField] private float cardClearance = 0.012f;

        private static HotspotView _current;

        private Transform _head;
        private Transform _product;
        private Bounds _productBounds;
        private Vector3 _size = Vector3.one;
        private int _index;
        private bool _shown;
        private bool _open;
        private float _openFor;
        private float _dotAlpha;
        private float _cardAlpha;
        private float _phase;

        public bool IsOpen => _open;

        private void Awake()
        {
            _size = transform.localScale;
            dotButton.onClick.AddListener(Toggle);
            SetAlpha(dotGroup, 0f);
            SetAlpha(card, 0f);
        }

        private void OnDisable()
        {
            if (_current == this)
            {
                _current = null;
            }

            _open = false;
        }

        public void Bind(HotspotData data, int index)
        {
            _index = index;
            // Offset the pulse so the dots don't all breathe in step.
            _phase = index * 1.3f;
            titleText.text = data.title;
            bodyText.text = data.text;
            if (icon != null)
            {
                icon.sprite = data.icon;
                icon.gameObject.SetActive(data.icon != null);
            }
        }

        /// <summary>The product this hotspot sits on, as a box in <paramref name="root"/>'s space.</summary>
        public void SetProduct(Transform root, Bounds bounds)
        {
            _product = root;
            _productBounds = bounds;
        }

        /// <summary>Fades the dot in or out. A hidden hotspot can't be poked.</summary>
        public void SetShown(bool shown)
        {
            _shown = shown;
            if (!shown)
            {
                Close();
            }
        }

        public void Open()
        {
            if (_open || !_shown)
            {
                return;
            }

            if (_current != null && _current != this)
            {
                _current.Close();
            }

            _current = this;
            _open = true;
            _openFor = 0f;
            ShowroomEvents.RaiseSfx(Sfx.HotspotOpen, transform.position);
            ShowroomEvents.RaiseHotspotOpened(_index);
        }

        public void Close()
        {
            _open = false;
            if (_current == this)
            {
                _current = null;
            }
        }

        private void Toggle()
        {
            if (_open)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        private void Update()
        {
            if (_open)
            {
                _openFor += Time.deltaTime;
                if (_openFor >= openTime)
                {
                    Close();
                }
            }

            float blend = Ease.Damp(fadeSpeed, Time.deltaTime);
            _dotAlpha = Mathf.Lerp(_dotAlpha, _shown ? 1f : 0f, blend);
            _cardAlpha = Mathf.Lerp(_cardAlpha, _open ? 1f : 0f, blend);
            SetAlpha(dotGroup, _dotAlpha);
            SetAlpha(card, _cardAlpha);

            dotGroup.blocksRaycasts = _shown;
            dotButton.interactable = _shown;
            card.transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, _cardAlpha);

            if (dot != null)
            {
                float pulse = _open ? 1f : 1f + Mathf.Sin(Time.time * pulseSpeed + _phase) * pulseAmount;
                dot.localScale = Vector3.one * pulse;
            }
        }

        private void LateUpdate()
        {
            if (_head == null)
            {
                Camera main = Camera.main;
                if (main == null)
                {
                    return;
                }

                _head = main.transform;
            }

            // A canvas is read from its -Z side, so +Z points away from the viewer.
            Vector3 away = transform.position - _head.position;
            if (away.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(away, Vector3.up);
            }

            Transform parent = transform.parent;
            float parentScale = parent != null ? Mathf.Max(parent.lossyScale.x, 0.0001f) : 1f;
            transform.localScale = _size / parentScale;

            // The card comes straight towards the viewer until it is clear of the product, so it still
            // appears to grow out of its dot.
            if (_product == null || card == null || _cardAlpha <= 0f || away.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float inFront = BoxMath.Reach(_product.localToWorldMatrix, _productBounds, transform.position,
                -away.normalized);
            var rect = (RectTransform)card.transform;
            Vector3 place = rect.anchoredPosition3D;
            place.z = -(Mathf.Max(inFront, 0f) + cardClearance) / Mathf.Max(transform.lossyScale.z, 0.000001f);
            rect.anchoredPosition3D = place;
        }

        private static void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group == null)
            {
                return;
            }

            // Snap the ends so a faded-out group stops costing a draw.
            group.alpha = alpha < 0.01f ? 0f : alpha > 0.99f ? 1f : alpha;
        }
    }
}
