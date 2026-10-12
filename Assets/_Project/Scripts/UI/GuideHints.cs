using TMPro;
using UnityEngine;

namespace MRShowroom
{
    /// <summary>
    /// Teaches each phase without words getting in the way: a prompt of four words at most above
    /// the product, and a ghost hand that performs the gesture once beside it, then fades. Both
    /// stop the moment the user does the thing.
    /// </summary>
    public sealed class GuideHints : MonoBehaviour
    {
        private enum Gesture
        {
            None,
            Grab,
            Poke,
            Pull
        }

        [SerializeField] private ShowroomTuning tuning;
        [SerializeField] private ProductStage stage;
        [SerializeField] private TMP_Text prompt;
        [SerializeField] private GhostHand rightHand;
        [SerializeField] private GhostHand leftHand;
        [Tooltip("The colour panel; the poke hint is aimed at it.")]
        [SerializeField] private Transform panel;
        [Tooltip("Prompt height above the product's resting surface (m).")]
        [SerializeField] private float promptHeight = 0.3f;
        [Tooltip("A prompt stays this long unless the user acts first (s).")]
        [SerializeField] private float promptTime = 7f;
        [SerializeField] private float fadeSpeed = 5f;

        [Header("Wording (four words at most)")]
        [SerializeField] private string inspectPrompt = "Pick it up";
        [SerializeField] private string hotspotPrompt = "Tap the dots";
        [SerializeField] private string customisePrompt = "Make it yours";
        [SerializeField] private string explodePrompt = "Pull it apart";
        [SerializeField] private string freePrompt = "Explore freely";

        private Transform _head;
        private ShowroomPhase _phase = ShowroomPhase.FindSurface;
        private Gesture _gesture;
        private float _gestureTime;
        private float _promptLeft;
        private float _promptAlpha;
        private bool _hotspotHintShown;

        private void OnEnable()
        {
            ShowroomEvents.PhaseChanged += HandlePhase;
            ShowroomEvents.Grabbed += HandleGrabbed;
            ShowroomEvents.Released += HandleReleased;
            ShowroomEvents.ColorChanged += HandleColorChanged;
            ShowroomEvents.Exploded += HandleExploded;
            ShowroomEvents.HotspotOpened += HandleHotspotOpened;
        }

        private void OnDisable()
        {
            ShowroomEvents.PhaseChanged -= HandlePhase;
            ShowroomEvents.Grabbed -= HandleGrabbed;
            ShowroomEvents.Released -= HandleReleased;
            ShowroomEvents.ColorChanged -= HandleColorChanged;
            ShowroomEvents.Exploded -= HandleExploded;
            ShowroomEvents.HotspotOpened -= HandleHotspotOpened;
        }

        private void Start()
        {
            prompt.alpha = 0f;
            HideHands();
        }

        private void HandlePhase(ShowroomPhase from, ShowroomPhase to)
        {
            _phase = to;
            switch (to)
            {
                case ShowroomPhase.Hook:
                    _hotspotHintShown = false;
                    Show(null, Gesture.None);
                    break;
                case ShowroomPhase.Inspect:
                    Show(inspectPrompt, Gesture.Grab);
                    break;
                case ShowroomPhase.Customise:
                    Show(customisePrompt, Gesture.Poke);
                    break;
                case ShowroomPhase.Explode:
                    Show(explodePrompt, Gesture.Pull);
                    break;
                case ShowroomPhase.Free:
                    Show(freePrompt, Gesture.None);
                    break;
                default:
                    Show(null, Gesture.None);
                    break;
            }
        }

        private void HandleGrabbed(GrabTarget target)
        {
            if (_phase == ShowroomPhase.Inspect || _phase == ShowroomPhase.Free)
            {
                Show(null, Gesture.None);
            }
            else if (_gesture == Gesture.Pull || _gesture == Gesture.Grab)
            {
                // Hands are on it; the ghost would only be in the way. The prompt stays.
                _gesture = Gesture.None;
            }
        }

        private void HandleReleased(GrabTarget target)
        {
            // Once they have handled it, point out the dots, once.
            if (_phase == ShowroomPhase.Inspect && !_hotspotHintShown)
            {
                _hotspotHintShown = true;
                Show(hotspotPrompt, Gesture.None);
            }
        }

        private void HandleHotspotOpened(int index)
        {
            _hotspotHintShown = true;
            if (_phase == ShowroomPhase.Inspect)
            {
                Show(null, Gesture.None);
            }
        }

        private void HandleColorChanged(int slot, int color)
        {
            if (_phase == ShowroomPhase.Customise)
            {
                Show(null, Gesture.None);
            }
        }

        private void HandleExploded(ExplodeTrigger trigger)
        {
            Show(null, Gesture.None);
        }

        private void Show(string text, Gesture gesture)
        {
            if (string.IsNullOrEmpty(text))
            {
                _promptLeft = 0f;
            }
            else
            {
                prompt.text = text;
                _promptLeft = promptTime;
            }

            _gesture = gesture;
            _gestureTime = 0f;
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

            UpdatePrompt();
            UpdateHands();
        }

        private void UpdatePrompt()
        {
            if (_promptLeft > 0f)
            {
                _promptLeft -= Time.deltaTime;
            }

            bool wanted = _promptLeft > 0f && stage.IsPlaced;
            _promptAlpha = Mathf.MoveTowards(_promptAlpha, wanted ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
            prompt.alpha = _promptAlpha;
            if (_promptAlpha <= 0f || !stage.IsPlaced)
            {
                return;
            }

            Transform label = prompt.transform;
            label.position = stage.RestPoint.position + Vector3.up * promptHeight;
            Vector3 away = label.position - _head.position;
            if (away.sqrMagnitude > 0.0001f)
            {
                label.rotation = Quaternion.LookRotation(away, Vector3.up);
            }
        }

        private void UpdateHands()
        {
            if (_gesture == Gesture.None || !stage.IsPlaced)
            {
                HideHands();
                return;
            }

            float duration = Mathf.Max(tuning.hintDuration, 0.5f);
            _gestureTime += Time.deltaTime;
            float t = _gestureTime / duration;
            if (t >= 1f)
            {
                _gesture = Gesture.None;
                HideHands();
                return;
            }

            // Fade in over the first part of the gesture and out over the last.
            float alpha = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((1f - t) / 0.2f);

            // A frame of reference built from where the user is standing: their right, and away from them.
            Vector3 centre = stage.RestPoint.position;
            Vector3 view = centre - _head.position;
            view.y = 0f;
            view = view.sqrMagnitude < 0.0001f ? stage.transform.forward : view.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, view);
            Vector3 up = Vector3.up;

            bool leftHanded = ShowroomSettings.LeftHanded;
            GhostHand lead = leftHanded ? leftHand : rightHand;
            GhostHand other = leftHanded ? rightHand : leftHand;
            float side = leftHanded ? -1f : 1f;

            switch (_gesture)
            {
                case Gesture.Grab:
                {
                    // Reach in from the side, close around the product, lift.
                    float reach = Ease.OutCubic(Mathf.InverseLerp(0f, 0.35f, t));
                    float grip = Mathf.InverseLerp(0.33f, 0.47f, t);
                    float lift = Ease.InOutSine(Mathf.InverseLerp(0.47f, 0.8f, t));

                    Vector3 start = centre + right * (0.24f * side) + up * 0.24f - view * 0.14f;
                    Vector3 hold = centre + up * 0.135f - view * 0.045f;
                    Vector3 position = Vector3.Lerp(start, hold, reach) + up * (0.14f * lift);
                    Quaternion rotation = Quaternion.LookRotation(view, up) * Quaternion.Euler(28f, -12f * side, 0f);

                    Place(lead, position, rotation, grip, grip, alpha);
                    Place(other, position, rotation, 0f, 0f, 0f);
                    break;
                }

                case Gesture.Poke:
                {
                    // Index finger out, two taps on the panel.
                    Vector3 target = panel != null ? panel.position : centre + right * (0.3f * side) + up * 0.15f;
                    Vector3 toward = panel != null ? panel.forward : view;
                    float reach = Ease.OutCubic(Mathf.InverseLerp(0f, 0.35f, t));
                    float tap = Mathf.InverseLerp(0.35f, 0.85f, t);
                    float press = Mathf.Abs(Mathf.Sin(tap * Mathf.PI * 2f));

                    // The hand comes in from below and from the outside, the way the user's own would, so the
                    // pointing finger is seen side on rather than end on.
                    Quaternion rotation = Quaternion.LookRotation(toward, up) * Quaternion.Euler(-32f, -20f * side, 0f);
                    float gap = Mathf.Lerp(0.16f, 0.025f, reach) - 0.022f * press;
                    Vector3 fingertip = target - toward * gap;
                    // The hand is positioned by its palm; the tip of the index finger is ahead of it and a
                    // little towards the thumb.
                    Vector3 position = fingertip - rotation * new Vector3(-0.02f * side, 0.004f, 0.105f);

                    Place(lead, position, rotation, 1f, 0f, alpha);
                    Place(other, position, rotation, 0f, 0f, 0f);
                    break;
                }

                case Gesture.Pull:
                {
                    // One hand at each end of the product, close, pull apart.
                    float reach = Ease.OutCubic(Mathf.InverseLerp(0f, 0.28f, t));
                    float grip = Mathf.InverseLerp(0.26f, 0.4f, t);
                    float pull = Ease.InOutSine(Mathf.InverseLerp(0.4f, 0.8f, t));

                    float half = Mathf.Lerp(0.26f, 0.13f, reach) + 0.15f * pull;
                    Vector3 middle = centre + up * 0.07f - view * 0.1f;
                    // Palms face each other: the back of each hand points outwards.
                    Quaternion rightRotation = Quaternion.LookRotation(view, right) * Quaternion.Euler(18f, 0f, 0f);
                    Quaternion leftRotation = Quaternion.LookRotation(view, -right) * Quaternion.Euler(18f, 0f, 0f);

                    Place(rightHand, middle + right * half, rightRotation, grip, grip, alpha);
                    Place(leftHand, middle - right * half, leftRotation, grip, grip, alpha);
                    break;
                }
            }
        }

        private static void Place(GhostHand hand, Vector3 position, Quaternion rotation, float grip, float index,
            float alpha)
        {
            if (hand == null)
            {
                return;
            }

            hand.SetAlpha(alpha);
            if (alpha <= 0f)
            {
                return;
            }

            hand.transform.SetPositionAndRotation(position, rotation);
            hand.SetPose(grip, index);
        }

        private void HideHands()
        {
            if (rightHand != null)
            {
                rightHand.SetAlpha(0f);
            }

            if (leftHand != null)
            {
                leftHand.SetAlpha(0f);
            }
        }
    }
}
