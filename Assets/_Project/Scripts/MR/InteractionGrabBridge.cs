using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;

namespace MRShowroom.MR
{
    /// <summary>
    /// Connects a <see cref="GrabTarget"/> to the Meta Interaction SDK. The SDK moves the object
    /// while it is grabbed (pinch, palm, two hands or controller grip); this turns its pointer
    /// events into BeginHold / EndHold, and reports how far apart two holding hands are, which is
    /// what the pull-apart gesture is measured from.
    ///
    /// It also covers lost hand tracking: the SDK simply stops moving the object, which holds it
    /// still; if the hand doesn't come back within a moment the hold is ended as "tracking lost"
    /// and the object goes home instead of hanging in the air.
    /// </summary>
    [RequireComponent(typeof(GrabTarget))]
    public sealed class InteractionGrabBridge : MonoBehaviour
    {
        [SerializeField] private GrabTarget target;
        [SerializeField] private Grabbable grabbable;
        [Tooltip("Object holding the HandGrabInteractable and GrabInteractable. Switched off when the target can't be picked up.")]
        [SerializeField] private GameObject interactables;
        [Tooltip("How long a hold survives with no tracked hand on it (s).")]
        [SerializeField] private float trackingLostTime = 0.5f;

        // Who is holding us, by interactor identifier. Null hand = a controller without a tracked hand.
        private readonly Dictionary<int, IHand> _holders = new Dictionary<int, IHand>(2);
        private bool _holding;
        private bool _lostTracking;
        private float _lostTime;

        private void Reset()
        {
            target = GetComponent<GrabTarget>();
            grabbable = GetComponent<Grabbable>();
        }

        private void OnEnable()
        {
            target.GrabbableChanged += HandleGrabbableChanged;
            target.ReleaseRequested += HandleReleaseRequested;
            grabbable.WhenPointerEventRaised += HandlePointerEvent;
            Apply(target.IsGrabbable);
        }

        private void OnDisable()
        {
            target.GrabbableChanged -= HandleGrabbableChanged;
            target.ReleaseRequested -= HandleReleaseRequested;
            grabbable.WhenPointerEventRaised -= HandlePointerEvent;
            _holders.Clear();
            _holding = false;
        }

        private void Update()
        {
            if (!_holding)
            {
                return;
            }

            ReportHoldPoints();

            if (AnyHolderTracked())
            {
                _lostTime = 0f;
                return;
            }

            _lostTime += Time.deltaTime;
            if (_lostTime >= trackingLostTime)
            {
                _lostTracking = true;
                CancelGrabs();
                _lostTracking = false;
            }
        }

        private void HandleGrabbableChanged(GrabTarget changed, bool canGrab)
        {
            Apply(canGrab);
        }

        private void HandleReleaseRequested(GrabTarget released)
        {
            CancelGrabs();
        }

        private void Apply(bool canGrab)
        {
            // Disabling the Grabbable cancels every pointer on it, which makes the interactors let go.
            grabbable.enabled = canGrab;
            if (interactables != null && interactables.activeSelf != canGrab)
            {
                interactables.SetActive(canGrab);
            }

            if (!canGrab)
            {
                target.SetHovered(false);
            }
        }

        private void CancelGrabs()
        {
            if (!grabbable.enabled)
            {
                return;
            }

            grabbable.enabled = false;
            grabbable.enabled = true;
        }

        private void HandlePointerEvent(PointerEvent evt)
        {
            switch (evt.Type)
            {
                case PointerEventType.Select:
                    _holders[evt.Identifier] = FindHand(evt.Data);
                    XRHaptics.NoteGrab(evt.Data);
                    if (!_holding)
                    {
                        _holding = true;
                        _lostTime = 0f;
                        target.SetHovered(false);
                        ReportHoldPoints();
                        target.BeginHold();
                    }

                    break;

                case PointerEventType.Unselect:
                case PointerEventType.Cancel:
                    _holders.Remove(evt.Identifier);

                    if (_holding && grabbable.SelectingPointsCount == 0)
                    {
                        _holding = false;
                        _holders.Clear();

                        HoldEndReason reason = HoldEndReason.Released;
                        if (_lostTracking)
                        {
                            reason = HoldEndReason.TrackingLost;
                        }
                        else if (!target.IsGrabbable)
                        {
                            reason = HoldEndReason.Cancelled;
                        }

                        target.EndHold(reason);
                    }

                    UpdateHover();
                    break;

                case PointerEventType.Hover:
                case PointerEventType.Unhover:
                    UpdateHover();
                    break;
            }
        }

        private void ReportHoldPoints()
        {
            List<Pose> points = grabbable.SelectingPoints;
            float spread = points.Count >= 2 ? Vector3.Distance(points[0].position, points[1].position) : 0f;
            target.SetHoldPoints(points.Count, spread);
        }

        // Grab hover: the product glows softly while a hand is in grab range.
        private void UpdateHover()
        {
            target.SetHovered(!_holding && target.IsGrabbable && grabbable.PointsCount > 0);
        }

        private bool AnyHolderTracked()
        {
            foreach (KeyValuePair<int, IHand> holder in _holders)
            {
                if (IsTracked(holder.Value))
                {
                    return true;
                }
            }

            return _holders.Count == 0;
        }

        private static bool IsTracked(IHand hand)
        {
            // No hand means a plain controller grab, which doesn't drop out the way hand tracking does.
            return hand == null || (hand.IsConnected && hand.IsTrackedDataValid);
        }

        // Only hand-grab interactors can lose a hand. A plain controller grab reports no hand at all, and
        // must not be mistaken for one that has stopped tracking.
        private static IHand FindHand(object interactor)
        {
            switch (interactor)
            {
                case HandGrabInteractor handGrab:
                    return handGrab.Hand;
                case DistanceHandGrabInteractor distanceGrab:
                    return distanceGrab.Hand;
                default:
                    return null;
            }
        }
    }
}
