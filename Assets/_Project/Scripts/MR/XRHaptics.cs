using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;

namespace MRShowroom.MR
{
    /// <summary>
    /// Short controller pulses on grab, button press and reassembly. Does nothing with bare hands,
    /// where sound carries that feedback instead.
    /// </summary>
    public sealed class XRHaptics : MonoBehaviour
    {
        [SerializeField] private float grabAmplitude = 0.35f;
        [SerializeField] private float grabDuration = 0.04f;
        [SerializeField] private float pressAmplitude = 0.25f;
        [SerializeField] private float pressDuration = 0.025f;
        [SerializeField] private float snapAmplitude = 0.6f;
        [SerializeField] private float snapDuration = 0.07f;
        [SerializeField] private float thudAmplitude = 0.45f;
        [SerializeField] private float thudDuration = 0.09f;

        private static OVRInput.Controller _lastGrabController = OVRInput.Controller.RTouch;

        private float _leftTimer;
        private float _rightTimer;

        /// <summary>Remembers which side grabbed last so the pulse goes to the hand that did it.</summary>
        public static void NoteGrab(object interactor)
        {
            Handedness? side = null;
            if (interactor is HandGrabInteractor handGrab && handGrab.Hand != null)
            {
                side = handGrab.Hand.Handedness;
            }
            else if (interactor is Component component)
            {
                IController controller = component.GetComponentInParent<IController>();
                if (controller != null)
                {
                    side = controller.Handedness;
                }
                else
                {
                    IHand hand = component.GetComponentInParent<IHand>();
                    if (hand != null)
                    {
                        side = hand.Handedness;
                    }
                }
            }

            if (side.HasValue)
            {
                _lastGrabController = side.Value == Handedness.Left
                    ? OVRInput.Controller.LTouch
                    : OVRInput.Controller.RTouch;
            }
        }

        private void OnEnable()
        {
            ShowroomEvents.Grabbed += HandleGrabbed;
            ShowroomEvents.SfxRequested += HandleSfx;
        }

        private void OnDisable()
        {
            ShowroomEvents.Grabbed -= HandleGrabbed;
            ShowroomEvents.SfxRequested -= HandleSfx;
            Stop(OVRInput.Controller.LTouch);
            Stop(OVRInput.Controller.RTouch);
        }

        private void Update()
        {
            Tick(ref _leftTimer, OVRInput.Controller.LTouch);
            Tick(ref _rightTimer, OVRInput.Controller.RTouch);
        }

        private void HandleGrabbed(GrabTarget target)
        {
            Pulse(_lastGrabController, grabAmplitude, grabDuration);
        }

        private void HandleSfx(Sfx sfx, Vector3 position)
        {
            switch (sfx)
            {
                case Sfx.UiPress:
                case Sfx.SwatchClick:
                case Sfx.HotspotOpen:
                case Sfx.Confirm:
                    // The button doesn't say which hand pressed it, so both get the tick.
                    PulseBoth(pressAmplitude, pressDuration);
                    break;
                case Sfx.ReassembleSnap:
                case Sfx.ExplodeWhoosh:
                    PulseBoth(snapAmplitude, snapDuration);
                    break;
                case Sfx.BoxThud:
                    PulseBoth(thudAmplitude, thudDuration);
                    break;
            }
        }

        private void PulseBoth(float amplitude, float duration)
        {
            Pulse(OVRInput.Controller.LTouch, amplitude, duration);
            Pulse(OVRInput.Controller.RTouch, amplitude, duration);
        }

        private void Pulse(OVRInput.Controller controller, float amplitude, float duration)
        {
            if (ShowroomEvents.CurrentInputMode != InputMode.Controllers)
            {
                return;
            }

            OVRInput.SetControllerVibration(1f, amplitude, controller);
            if (controller == OVRInput.Controller.LTouch)
            {
                _leftTimer = duration;
            }
            else
            {
                _rightTimer = duration;
            }
        }

        // Vibration keeps going until told to stop, so every pulse needs its own stop.
        private static void Tick(ref float timer, OVRInput.Controller controller)
        {
            if (timer <= 0f)
            {
                return;
            }

            timer -= Time.unscaledDeltaTime;
            if (timer <= 0f)
            {
                Stop(controller);
            }
        }

        private static void Stop(OVRInput.Controller controller)
        {
            OVRInput.SetControllerVibration(0f, 0f, controller);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _lastGrabController = OVRInput.Controller.RTouch;
        }
    }
}
