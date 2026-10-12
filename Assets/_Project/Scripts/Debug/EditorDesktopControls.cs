using UnityEngine;
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR;
#endif

namespace MRShowroom
{
    /// <summary>
    /// Editor-only stand-in for a headset, so the whole flow can be played with mouse and keyboard
    /// when no XR device (or simulator) is running. It gives you a head at eye height and lets you:
    ///   left mouse      grab the product or a part / click a button or a hotspot
    ///   mouse wheel     move what you hold nearer or further
    ///   Q / E, R / F    turn and tip what you hold
    ///   T / G           while holding: a second hand, moving apart (T) or together (G)
    ///   right mouse     look around                     W A S D   walk
    ///   Space           skip to the next phase          X   open / close the exploded view
    ///   C               random colours                  P   play again
    ///   M               move the product (point, click) H   swap the panel side
    /// It does nothing in builds, and nothing in the editor once a real XR device is active.
    /// </summary>
    public sealed class EditorDesktopControls : MonoBehaviour
    {
        // The fields stay in builds so the serialized layout matches the editor's; only the code is stripped.
#pragma warning disable 0414
        [SerializeField] private ShowroomFlow flow;
        [SerializeField] private ProductController product;
        [SerializeField] private ProductStage stage;
        [SerializeField] private float eyeHeight = 1.45f;
        [Tooltip("Initial look-down angle (deg).")]
        [SerializeField] private float lookDown = 30f;
        [Tooltip("When the product is placed, the head walks up to this distance from it (m).")]
        [SerializeField] private float viewingDistance = 0.62f;
        [SerializeField] private float walkSpeed = 1.2f;
        [Tooltip("Hand separation the pretend second hand starts at (m).")]
        [SerializeField] private float twoHandStart = 0.2f;
#pragma warning restore 0414

#if UNITY_EDITOR
        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private Camera _camera;
        private GrabTarget _held;
        private GrabTarget _hovered;
        private Vector3 _position;
        private float _depth;
        private float _yaw;
        private float _pitch;
        private Quaternion _heldRotation;
        private bool _walkUp;
        private float _spread;
        private Vector3 _spreadScale;

        private void OnEnable()
        {
            if (stage != null)
            {
                stage.Placed += HandlePlaced;
            }
        }

        private void OnDisable()
        {
            if (stage != null)
            {
                stage.Placed -= HandlePlaced;
            }
        }

        private void Start()
        {
            if (XRSettings.isDeviceActive)
            {
                enabled = false;
                return;
            }

            _pitch = lookDown;
            _position = new Vector3(0f, eyeHeight, 0f);
            DesktopHead.Set(_position, _pitch, _yaw);
            Debug.Log("[EditorDesktopControls] No XR device: mouse and keyboard controls are on (LMB grab/click, " +
                      "wheel depth, Q/E/R/F rotate, T/G second hand, RMB look, WASD walk, Space next phase, " +
                      "X explode, C colours, P play again, M move, H panel side).");
        }

        // A person would step up to the table the product lands on. So does the pretend head.
        private void HandlePlaced(SurfaceInfo surface)
        {
            _walkUp = true;
        }

        private void WalkUpToProduct()
        {
            _walkUp = false;
            Vector3 rest = stage.RestPoint.position;
            Vector3 back = _position - rest;
            back.y = 0f;
            back = back.sqrMagnitude < 0.0001f ? -stage.transform.forward : back.normalized;

            _position = rest + back * viewingDistance;
            _position.y = stage.Surface.floorY + eyeHeight;
            Vector3 look = rest + Vector3.up * 0.06f - _position;
            _yaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
            _pitch = Mathf.Atan2(-look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg;
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            // The head first: it has to follow the product even with no mouse attached (a test run).
            if (_walkUp)
            {
                WalkUpToProduct();
            }

            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _yaw += delta.x * 0.15f;
                _pitch = Mathf.Clamp(_pitch - delta.y * 0.15f, -60f, 80f);
            }

            if (keyboard != null)
            {
                Walk(keyboard);
            }

            DesktopHead.Set(_position, _pitch, _yaw);

            if (mouse == null)
            {
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return;
                }
            }

            Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());

            if (_held == null)
            {
                UpdateHover(ray);
                if (mouse.leftButton.wasPressedThisFrame && !ClickUi(mouse.position.ReadValue()))
                {
                    TryGrab(ray);
                }
            }
            else
            {
                DriveHeld(ray, mouse, keyboard);
            }

            if (keyboard != null)
            {
                HandleKeys(keyboard);
            }
        }

        private void Walk(Keyboard keyboard)
        {
            float forward = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            float sideways = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            if (forward == 0f && sideways == 0f)
            {
                return;
            }

            Quaternion heading = Quaternion.Euler(0f, _yaw, 0f);
            _position += heading * new Vector3(sideways, 0f, forward) * (walkSpeed * Time.unscaledDeltaTime);
        }

        private void HandleKeys(Keyboard keyboard)
        {
            if (keyboard.spaceKey.wasPressedThisFrame && flow != null)
            {
                flow.Advance();
            }

            if (keyboard.pKey.wasPressedThisFrame && flow != null)
            {
                flow.Restart();
            }

            if (keyboard.mKey.wasPressedThisFrame)
            {
                ShowroomEvents.RequestManualPlacement();
            }

            if (keyboard.hKey.wasPressedThisFrame)
            {
                ShowroomSettings.LeftHanded = !ShowroomSettings.LeftHanded;
            }

            if (product == null || product.View == null)
            {
                return;
            }

            if (keyboard.xKey.wasPressedThisFrame && !product.Explode.IsMoving)
            {
                product.Explode.Toggle(ExplodeTrigger.Button);
            }

            if (keyboard.cKey.wasPressedThisFrame)
            {
                product.Colors.Randomise();
            }
        }

        // The nearest thing under the cursor that can be picked up right now.
        private GrabTarget FindTarget(Ray ray, out float distance)
        {
            distance = 0f;
            int count = Physics.RaycastNonAlloc(ray, _hits, 5f, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);
            GrabTarget best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].distance >= bestDistance)
                {
                    continue;
                }

                // A part's collider belongs to the part while the product is apart, and to the whole product otherwise.
                Transform node = _hits[i].collider.transform;
                while (node != null)
                {
                    if (node.TryGetComponent(out GrabTarget candidate) && candidate.IsGrabbable)
                    {
                        best = candidate;
                        bestDistance = _hits[i].distance;
                        break;
                    }

                    node = node.parent;
                }
            }

            distance = bestDistance;
            return best;
        }

        private void UpdateHover(Ray ray)
        {
            GrabTarget target = FindTarget(ray, out _);
            if (target == _hovered)
            {
                return;
            }

            if (_hovered != null)
            {
                _hovered.SetHovered(false);
            }

            _hovered = target;
            if (_hovered != null)
            {
                _hovered.SetHovered(true);
            }
        }

        private void TryGrab(Ray ray)
        {
            GrabTarget target = FindTarget(ray, out float distance);
            if (target == null)
            {
                return;
            }

            if (_hovered != null)
            {
                _hovered.SetHovered(false);
                _hovered = null;
            }

            _held = target;
            _depth = Vector3.Dot(target.transform.position - ray.origin, ray.direction);
            if (_depth <= 0.05f)
            {
                _depth = distance;
            }

            _heldRotation = target.transform.rotation;
            _spread = 0f;
            target.SetHoldPoints(1, 0f);
            target.BeginHold();
        }

        private void DriveHeld(Ray ray, Mouse mouse, Keyboard keyboard)
        {
            if (!_held.IsHeld)
            {
                // Something else ended the hold (the product came apart, the flow restarted).
                _held = null;
                return;
            }

            if (!mouse.leftButton.isPressed)
            {
                _held.EndHold();
                _held = null;
                return;
            }

            // Wheel units differ wildly between mice, trackpads and platforms, so cap the step per frame.
            float scroll = Mathf.Clamp(mouse.scroll.ReadValue().y * 0.01f, -0.03f, 0.03f);
            _depth = Mathf.Clamp(_depth + scroll, 0.2f, 3f);

            if (keyboard != null)
            {
                float turn = 120f * Time.unscaledDeltaTime;
                if (keyboard.qKey.isPressed)
                {
                    _heldRotation = Quaternion.AngleAxis(-turn, Vector3.up) * _heldRotation;
                }

                if (keyboard.eKey.isPressed)
                {
                    _heldRotation = Quaternion.AngleAxis(turn, Vector3.up) * _heldRotation;
                }

                if (keyboard.rKey.isPressed)
                {
                    _heldRotation = Quaternion.AngleAxis(turn, _camera.transform.right) * _heldRotation;
                }

                if (keyboard.fKey.isPressed)
                {
                    _heldRotation = Quaternion.AngleAxis(-turn, _camera.transform.right) * _heldRotation;
                }

                DriveSecondHand(keyboard);
            }

            _held.transform.SetPositionAndRotation(ray.GetPoint(_depth), _heldRotation);
        }

        // T and G act as a second hand: the hands start a fixed distance apart, and moving them scales the
        // product the way the two-hand transformer does on the headset. That is also what the pull-apart
        // gesture watches, so holding T long enough opens the exploded view.
        private void DriveSecondHand(Keyboard keyboard)
        {
            bool apart = keyboard.tKey.isPressed;
            bool together = keyboard.gKey.isPressed;
            if (!apart && !together)
            {
                if (_spread > 0f)
                {
                    _spread = 0f;
                    _held.SetHoldPoints(1, 0f);
                }

                return;
            }

            if (_spread <= 0f)
            {
                _spread = twoHandStart;
                _spreadScale = _held.transform.localScale;
            }

            _spread = Mathf.Max(_spread + (apart ? 0.25f : -0.25f) * Time.unscaledDeltaTime, 0.03f);
            _held.SetHoldPoints(2, _spread);

            bool wholeProduct = product != null && product.View != null && _held == product.View.Grab;
            if (wholeProduct)
            {
                float factor = Mathf.Clamp(_spreadScale.x * _spread / twoHandStart, 0.5f, 3f);
                _held.transform.localScale = Vector3.one * factor;
            }
        }

        // World-space canvases normally get their clicks from the XR pointer module. Without a headset,
        // raycast the mouse into them and deliver the click by hand.
        private bool ClickUi(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas.renderMode == RenderMode.WorldSpace)
                {
                    canvas.worldCamera = _camera;
                }
            }

            var pointer = new PointerEventData(eventSystem) { position = screenPosition };
            _uiHits.Clear();
            eventSystem.RaycastAll(pointer, _uiHits);
            if (_uiHits.Count == 0)
            {
                return false;
            }

            ExecuteEvents.ExecuteHierarchy(_uiHits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
            return true;
        }
#endif
    }
}
