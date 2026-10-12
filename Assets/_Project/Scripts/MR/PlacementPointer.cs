using Oculus.Interaction.Input;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
using UnityEngine.XR;
#endif
// UnityEngine.XR has a Hand of its own.
using Hand = Oculus.Interaction.Input.Hand;

namespace MRShowroom.MR
{
    /// <summary>
    /// "Where is the user pointing, and did they just confirm?" for placing the product by hand.
    /// With hands that is the hand's pointer ray and an index pinch; with controllers, the
    /// controller's forward and the trigger. It also reports the fingertip (or controller tip),
    /// for placing by touching a real surface. In the editor without a headset the mouse stands in.
    /// </summary>
    public sealed class PlacementPointer : MonoBehaviour
    {
        [Tooltip("How far ahead of a controller's grip its tip is (m).")]
        [SerializeField] private float controllerTip = 0.06f;

        private Hand[] _hands = new Hand[0];
        private OVRCameraRig _rig;
        private bool _leftWasDown;
        private bool _rightWasDown;
        private float _nextSearch;

        /// <param name="ray">From the hand or controller, in world space.</param>
        /// <param name="tip">Index fingertip, or the tip of the controller.</param>
        /// <param name="confirmed">True on the frame the pinch or trigger went down.</param>
        public bool TryGet(out Ray ray, out Vector3 tip, out bool confirmed)
        {
#if UNITY_EDITOR
            if (!XRSettings.isDeviceActive)
            {
                return TryGetMouse(out ray, out tip, out confirmed);
            }
#endif
            bool controllers = (OVRInput.GetActiveController() & OVRInput.Controller.Touch) != 0;

            bool rightValid = Read(Handedness.Right, controllers, out Ray rightRay, out Vector3 rightTip, out bool rightDown);
            bool leftValid = Read(Handedness.Left, controllers, out Ray leftRay, out Vector3 leftTip, out bool leftDown);
            bool rightPressed = rightValid && rightDown && !_rightWasDown;
            bool leftPressed = leftValid && leftDown && !_leftWasDown;
            _rightWasDown = rightValid && rightDown;
            _leftWasDown = leftValid && leftDown;

            // The hand that just confirmed wins; otherwise the dominant hand leads.
            bool useLeft;
            if (rightPressed != leftPressed)
            {
                useLeft = leftPressed;
            }
            else if (rightValid != leftValid)
            {
                useLeft = leftValid;
            }
            else
            {
                useLeft = ShowroomSettings.LeftHanded;
            }

            ray = useLeft ? leftRay : rightRay;
            tip = useLeft ? leftTip : rightTip;
            confirmed = useLeft ? leftPressed : rightPressed;
            return useLeft ? leftValid : rightValid;
        }

        private bool Read(Handedness side, bool controllers, out Ray ray, out Vector3 tip, out bool down)
        {
            ray = default;
            tip = default;
            down = false;

            if (controllers)
            {
                OVRInput.Controller controller = side == Handedness.Left
                    ? OVRInput.Controller.LTouch
                    : OVRInput.Controller.RTouch;
                if (!OVRInput.GetControllerPositionTracked(controller))
                {
                    return false;
                }

                Vector3 position = OVRInput.GetLocalControllerPosition(controller);
                Quaternion rotation = OVRInput.GetLocalControllerRotation(controller);
                // Controller poses come in tracking space; the rig's tracking space puts them in the world.
                if (_rig == null)
                {
                    _rig = FindFirstObjectByType<OVRCameraRig>();
                }

                Transform trackingSpace = _rig != null ? _rig.trackingSpace : null;
                if (trackingSpace != null)
                {
                    position = trackingSpace.TransformPoint(position);
                    rotation = trackingSpace.rotation * rotation;
                }

                Vector3 forward = rotation * Vector3.forward;
                ray = new Ray(position, forward);
                tip = position + forward * controllerTip;
                down = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, controller);
                return true;
            }

            Hand hand = FindHand(side);
            if (hand == null || !hand.IsPointerPoseValid || !hand.GetPointerPose(out Pose pointer))
            {
                return false;
            }

            ray = new Ray(pointer.position, pointer.forward);
            tip = hand.GetJointPose(HandJointId.HandIndexTip, out Pose fingertip) ? fingertip.position : pointer.position;
            down = hand.GetIndexFingerIsPinching();
            return true;
        }

        // The rig holds several hands per side (tracked, controller-driven, synthetic). The tracked one is
        // whichever is connected with valid data and isn't the synthetic copy used for drawing.
        private Hand FindHand(Handedness side)
        {
            if (Time.unscaledTime >= _nextSearch)
            {
                _nextSearch = Time.unscaledTime + 2f;
                _hands = FindObjectsByType<Hand>(FindObjectsSortMode.None);
            }

            for (int i = 0; i < _hands.Length; i++)
            {
                Hand hand = _hands[i];
                if (hand != null && !(hand is SyntheticHand) && hand.Handedness == side && hand.IsConnected
                    && hand.IsTrackedDataValid)
                {
                    return hand;
                }
            }

            return null;
        }

#if UNITY_EDITOR
        private static bool TryGetMouse(out Ray ray, out Vector3 tip, out bool confirmed)
        {
            ray = default;
            tip = default;
            confirmed = false;

            Camera camera = Camera.main;
            Mouse mouse = Mouse.current;
            if (camera == null || mouse == null)
            {
                return false;
            }

            ray = camera.ScreenPointToRay(mouse.position.ReadValue());
            tip = ray.GetPoint(0.5f);
            confirmed = mouse.leftButton.wasPressedThisFrame;
            return true;
        }
#endif
    }
}
