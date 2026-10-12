using System;
using UnityEngine;

namespace MRShowroom
{
    /// <summary>
    /// Something that can be picked up: the whole product, or one of its parts once it is apart.
    /// It knows nothing about the Interaction SDK. The mixed reality layer (InteractionGrabBridge)
    /// reports grabs to it, and so do the editor's mouse controls, so everything above this works
    /// the same with hands, controllers or a mouse.
    /// </summary>
    public sealed class GrabTarget : MonoBehaviour
    {
        [SerializeField] private bool grabbable = true;

        public event Action<GrabTarget> HoldBegan;
        public event Action<GrabTarget, HoldEndReason> HoldEnded;
        public event Action<GrabTarget, bool> HoverChanged;
        public event Action<GrabTarget, bool> GrabbableChanged;
        /// <summary>Asks whatever is moving this object to let go, without making it ungrabbable.</summary>
        public event Action<GrabTarget> ReleaseRequested;

        public bool IsHeld { get; private set; }
        public bool IsHovered { get; private set; }

        /// <summary>How many hands are on it.</summary>
        public int HoldPoints { get; private set; }

        /// <summary>Distance between the two hands holding it (m). Zero unless two hands are on it.</summary>
        public float HoldSpread { get; private set; }

        public bool IsGrabbable
        {
            get => grabbable;
            set
            {
                if (grabbable == value)
                {
                    return;
                }

                grabbable = value;
                GrabbableChanged?.Invoke(this, value);

                if (!value)
                {
                    SetHovered(false);
                    // Normally the bridge has already ended the hold by now; this covers the mouse controls.
                    EndHold(HoldEndReason.Cancelled);
                }
            }
        }

        public void BeginHold()
        {
            if (IsHeld || !grabbable)
            {
                return;
            }

            IsHeld = true;
            HoldPoints = Mathf.Max(HoldPoints, 1);
            HoldBegan?.Invoke(this);
            ShowroomEvents.RaiseGrabbed(this);
        }

        public void EndHold(HoldEndReason reason = HoldEndReason.Released)
        {
            if (!IsHeld)
            {
                return;
            }

            IsHeld = false;
            HoldPoints = 0;
            HoldSpread = 0f;
            HoldEnded?.Invoke(this, reason);
            ShowroomEvents.RaiseReleased(this);
        }

        public void SetHovered(bool hovered)
        {
            if (IsHovered == hovered)
            {
                return;
            }

            IsHovered = hovered;
            HoverChanged?.Invoke(this, hovered);
        }

        /// <summary>Reported every frame by whatever is holding the object.</summary>
        public void SetHoldPoints(int count, float spread)
        {
            HoldPoints = count;
            HoldSpread = count >= 2 ? spread : 0f;
        }

        public void Release()
        {
            if (!IsHeld)
            {
                return;
            }

            ReleaseRequested?.Invoke(this);
            EndHold(HoldEndReason.Cancelled);
        }

        private void OnDisable()
        {
            SetHovered(false);
            EndHold(HoldEndReason.Cancelled);
        }
    }
}
