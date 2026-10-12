using System;
using UnityEngine;

namespace MRShowroom
{
    /// <summary>
    /// Brings a released object back to its resting pose with a soft spring instead of letting it
    /// fall or teleporting it. The rest pose is local to the parent, so the product follows its
    /// spot on the table and each part follows its slot in the exploded view.
    /// </summary>
    public sealed class ReturnToRest : MonoBehaviour
    {
        [SerializeField] private GrabTarget target;
        [Tooltip("Spring speed (Hz).")]
        [SerializeField] private float frequency = 2.2f;
        [Tooltip("1 = no overshoot.")]
        [SerializeField] [Range(0.3f, 1f)] private float damping = 0.82f;
        [Tooltip("After a hold that ended because tracking was lost, wait this long before returning (s).")]
        [SerializeField] private float trackingLostHold = 0.5f;
        [Tooltip("Also bring the scale back. Off for the product itself, whose size the user chooses.")]
        [SerializeField] private bool restoreScale;

        private const float MaxStep = 1f / 60f;

        private Vector3 _restPosition;
        private Quaternion _restRotation = Quaternion.identity;
        private Vector3 _restScale = Vector3.one;
        private Vector3 _velocity;
        private float _wait;
        private bool _settled = true;
        private bool _restCaptured;

        public event Action<ReturnToRest> Settled;

        public bool IsSettled => _settled;

        private void Reset()
        {
            target = GetComponent<GrabTarget>();
        }

        private void Awake()
        {
            CaptureRest();
        }

        private void OnEnable()
        {
            if (target != null)
            {
                target.HoldBegan += HandleHoldBegan;
                target.HoldEnded += HandleHoldEnded;
            }

            // Whatever moved the object while this was off (an animation, usually), carry on from there.
            _velocity = Vector3.zero;
            _settled = false;
        }

        private void OnDisable()
        {
            if (target != null)
            {
                target.HoldBegan -= HandleHoldBegan;
                target.HoldEnded -= HandleHoldEnded;
            }
        }

        public void Configure(float springFrequency, float springDamping, float lostTrackingHold)
        {
            frequency = springFrequency;
            damping = springDamping;
            trackingLostHold = lostTrackingHold;
        }

        /// <summary>Uses the current local pose as the place to come back to.</summary>
        public void CaptureRest()
        {
            _restPosition = transform.localPosition;
            _restRotation = transform.localRotation;
            _restScale = transform.localScale;
            _restCaptured = true;
        }

        public void SnapToRest()
        {
            if (!_restCaptured)
            {
                CaptureRest();
            }

            transform.localPosition = _restPosition;
            transform.localRotation = _restRotation;
            if (restoreScale)
            {
                transform.localScale = _restScale;
            }

            _velocity = Vector3.zero;
            _wait = 0f;
            _settled = true;
        }

        /// <summary>Starts the spring again after the object, or its rest point, was moved by something else.</summary>
        public void WakeUp()
        {
            _velocity = Vector3.zero;
            _wait = 0f;
            _settled = false;
        }

        private void HandleHoldBegan(GrabTarget held)
        {
            _settled = false;
            _wait = 0f;
        }

        private void HandleHoldEnded(GrabTarget held, HoldEndReason reason)
        {
            _velocity = Vector3.zero;
            _wait = reason == HoldEndReason.TrackingLost ? trackingLostHold : 0f;
            _settled = false;
        }

        private void Update()
        {
            if (_settled || (target != null && target.IsHeld))
            {
                return;
            }

            float remaining = Time.deltaTime;
            if (_wait > 0f)
            {
                _wait -= remaining;
                return;
            }

            // Fixed-size steps keep the spring stable through a slow frame.
            Vector3 position = transform.localPosition;
            float omega = frequency * 2f * Mathf.PI;
            while (remaining > 0f)
            {
                float step = Mathf.Min(remaining, MaxStep);
                remaining -= step;
                Vector3 acceleration = (_restPosition - position) * (omega * omega) - _velocity * (2f * damping * omega);
                _velocity += acceleration * step;
                position += _velocity * step;
            }

            float blend = Ease.Damp(omega * 0.9f, Time.deltaTime);
            Quaternion rotation = Quaternion.Slerp(transform.localRotation, _restRotation, blend);
            transform.localPosition = position;
            transform.localRotation = rotation;
            if (restoreScale)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, _restScale, blend);
            }

            bool close = (position - _restPosition).sqrMagnitude < 0.0005f * 0.0005f
                         && _velocity.sqrMagnitude < 0.005f * 0.005f
                         && Quaternion.Angle(rotation, _restRotation) < 0.5f;
            if (close)
            {
                SnapToRest();
                Settled?.Invoke(this);
            }
        }
    }
}
