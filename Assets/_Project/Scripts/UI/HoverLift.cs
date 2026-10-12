using UnityEngine;
using UnityEngine.EventSystems;

namespace MRShowroom
{
    /// <summary>
    /// Makes a button rise towards the finger (or ray) that is over it: a small grow and a few
    /// millimetres off the panel. The hover state that tells a first-time user "this can be pressed".
    /// </summary>
    public sealed class HoverLift : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private float scale = 1.07f;
        [Tooltip("How far the button comes off the panel, in canvas units (mm).")]
        [SerializeField] private float lift = 5f;
        [SerializeField] private float sharpness = 18f;

        private RectTransform _rect;
        private float _amount;
        private bool _hovered;

        private void Awake()
        {
            _rect = (RectTransform)transform;
        }

        private void OnDisable()
        {
            _hovered = false;
            _amount = 0f;
            Apply();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
        }

        private void Update()
        {
            float target = _hovered ? 1f : 0f;
            if (Mathf.Approximately(_amount, target))
            {
                return;
            }

            _amount = Mathf.Lerp(_amount, target, Ease.Damp(sharpness, Time.unscaledDeltaTime));
            if (Mathf.Abs(_amount - target) < 0.01f)
            {
                _amount = target;
            }

            Apply();
        }

        private void Apply()
        {
            if (_rect == null)
            {
                return;
            }

            // A canvas is read from its -Z side, so towards the user is negative Z.
            Vector3 position = _rect.anchoredPosition3D;
            position.z = -lift * _amount;
            _rect.anchoredPosition3D = position;
            _rect.localScale = Vector3.one * Mathf.Lerp(1f, scale, _amount);
        }
    }
}
