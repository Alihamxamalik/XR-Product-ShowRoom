using UnityEngine;

namespace MRShowroom
{
    /// <summary>
    /// Keeps a panel beside the product, on the user's left or right as they see it, turned to
    /// face them. It re-aims only when the user has actually moved, so it sits still while they
    /// lean in to poke it.
    /// </summary>
    public sealed class DockedPanel : MonoBehaviour
    {
        public enum Side
        {
            /// <summary>The side of the user's dominant hand: right, or left for left-handers.</summary>
            Dominant,
            /// <summary>The other side.</summary>
            Opposite
        }

        [SerializeField] private ProductStage stage;
        [SerializeField] private ShowroomTuning tuning;
        [SerializeField] private Side side = Side.Dominant;
        [Tooltip("Extra distance to the side, on top of the tuning's panel offset (m).")]
        [SerializeField] private float extraOffset;
        [Tooltip("How far the top of the panel leans away from the user (deg).")]
        [SerializeField] private float lean = 12f;
        [SerializeField] private float sharpness = 7f;

        private Transform _head;
        private Vector3 _aimedFrom;
        private Vector3 _aimedAt;
        private Vector3 _targetPosition;
        private Quaternion _targetRotation = Quaternion.identity;
        private float _aimedSign;
        private bool _hasTarget;

        private void OnEnable()
        {
            _hasTarget = false;
        }

        private void LateUpdate()
        {
            if (!stage.IsPlaced)
            {
                return;
            }

            if (_head == null)
            {
                Camera main = Camera.main;
                if (main == null)
                {
                    return;
                }

                _head = main.transform;
            }

            Vector3 head = _head.position;
            Vector3 rest = stage.RestPoint.position;
            float sign = (side == Side.Dominant) == ShowroomSettings.LeftHanded ? -1f : 1f;

            bool moved = (head - _aimedFrom).sqrMagnitude > tuning.panelFollowThreshold * tuning.panelFollowThreshold
                         || (rest - _aimedAt).sqrMagnitude > 0.0004f
                         || !Mathf.Approximately(sign, _aimedSign);
            if (!_hasTarget || moved)
            {
                Aim(head, rest, sign);
                if (!_hasTarget)
                {
                    transform.SetPositionAndRotation(_targetPosition, _targetRotation);
                    _hasTarget = true;
                    return;
                }
            }

            float blend = Ease.Damp(sharpness, Time.unscaledDeltaTime);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, _targetPosition, blend),
                Quaternion.Slerp(transform.rotation, _targetRotation, blend));
        }

        private void Aim(Vector3 head, Vector3 rest, float sign)
        {
            _aimedFrom = head;
            _aimedAt = rest;
            _aimedSign = sign;

            Vector3 view = rest - head;
            view.y = 0f;
            view = view.sqrMagnitude < 0.0001f ? stage.transform.forward : view.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, view);

            // Beside the product and a touch nearer the user than it, so the panel is never behind it.
            _targetPosition = rest + right * (sign * (tuning.panelSideOffset + extraOffset))
                              + Vector3.up * tuning.panelHeight - view * 0.03f;

            Vector3 away = _targetPosition - head;
            away.y = 0f;
            away = away.sqrMagnitude < 0.0001f ? view : away.normalized;
            // A canvas is read from its -Z side, so +Z points away from the reader.
            _targetRotation = Quaternion.LookRotation(away, Vector3.up) * Quaternion.Euler(lean, 0f, 0f);
        }
    }
}
