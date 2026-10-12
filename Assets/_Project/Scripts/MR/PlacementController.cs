using System.Collections;
using UnityEngine;
using UnityEngine.XR;

namespace MRShowroom.MR
{
    /// <summary>
    /// Finds the product a home, so the experience always starts. In order of preference:
    ///
    ///   1. a table in the scanned room, within reach          (no input needed)
    ///   2. the floor near the user, on a pedestal             (scanned room, no usable table)
    ///   3. wherever the user points and pinches               (no scanned room, depth raycast)
    ///   4. a pedestal at a fixed spot in front of the user    (nothing else available, or the user skips)
    ///
    /// Spatial data permission sits in front of 1 to 3: the room scan and the depth raycast both
    /// need it. If it is refused the product goes straight to 4, and "Move" still lets the user
    /// place it by touching a real surface, which needs no room data at all.
    ///
    /// The chosen spot is pinned with a spatial anchor so it stays on the table through recentring
    /// and tracking hiccups, and the product follows the user if they walk off.
    /// </summary>
    public sealed class PlacementController : MonoBehaviour
    {
        private enum Mode
        {
            Starting,
            Idle,
            Pointing,
            Touching
        }

        [SerializeField] private ShowroomTuning tuning;
        [SerializeField] private ProductStage stage;
        [SerializeField] private ProductController product;
        [SerializeField] private SurfaceFinder finder;
        [SerializeField] private PlacementPointer pointer;
        [SerializeField] private PassthroughController passthrough;
        [SerializeField] private RoomLighting lighting;

        [Header("Reticle")]
        [SerializeField] private Transform reticle;
        [SerializeField] private Renderer reticleRenderer;
        [SerializeField] private Color reticleValid = new Color(0.72f, 0.92f, 0.35f, 0.9f);
        [SerializeField] private Color reticleInvalid = new Color(1f, 0.45f, 0.4f, 0.6f);

        [Header("Behaviour")]
        [Tooltip("Pin the placement with a spatial anchor.")]
        [SerializeField] private bool anchorPlacement = true;
        [Tooltip("Surfaces steeper than this can't hold the product (cosine of the tilt; 0.7 is about 45 degrees).")]
        [SerializeField] private float minUpDot = 0.7f;
        [Tooltip("How long the line explaining the permission is shown before the system asks (s).")]
        [SerializeField] private float permissionReadTime = 1.2f;
        [Tooltip("How long to wait for the headset to report where the head is (s).")]
        [SerializeField] private float headTimeout = 2.5f;
        [Tooltip("A fingertip touching a table is this far above it (m).")]
        [SerializeField] private float touchOffset = 0.012f;
        [Tooltip("Pinches are ignored for this long after placing by hand starts, so the pinch that pressed " +
                 "the button isn't taken as the pinch that places the product (s).")]
        [SerializeField] private float armingTime = 0.5f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private MaterialPropertyBlock _block;
        private Transform _head;
        private Mode _mode = Mode.Starting;
        private float _modeTime;
        private float _awayTime;
        private bool _placedOnce;
        private bool _permissionAnswered;
        private bool _roomAttempted;
        private GameObject _anchorObject;
        private OVRSpatialAnchor _anchor;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            ShowReticle(false, default, Vector3.up, false);
        }

        private void OnEnable()
        {
            ShowroomEvents.ManualPlacementRequested += HandleManualRequested;
            ShowroomEvents.SpaceSetupRequested += HandleSpaceSetupRequested;
            ShowroomEvents.PlacementSkipRequested += HandleSkipRequested;
        }

        private void OnDisable()
        {
            ShowroomEvents.ManualPlacementRequested -= HandleManualRequested;
            ShowroomEvents.SpaceSetupRequested -= HandleSpaceSetupRequested;
            ShowroomEvents.PlacementSkipRequested -= HandleSkipRequested;
        }

        private IEnumerator Start()
        {
            ShowroomEvents.SetPlacementPrompt(PlacementPrompt.Searching);

            // Wait until the headset says where the head is; placing relative to the floor origin would be wrong.
            float waited = 0f;
            while (waited < headTimeout && !HeadIsTracked())
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            while (_head == null)
            {
                FindHead();
                yield return null;
            }

            while (passthrough != null && !passthrough.IsReady)
            {
                yield return null;
            }

            if (!XRSettings.isDeviceActive)
            {
                // No headset: the editor, or a test run. Use a sample room if one is selected.
                yield return finder.LoadEditorRoom();
                PlaceAutomatically();
                yield break;
            }

            ShowroomEvents.SpaceSetupAvailable = Application.platform == RuntimePlatform.Android;

            if (!ScenePermission.Granted)
            {
                // Say why before the system asks.
                ShowroomEvents.SetPlacementPrompt(PlacementPrompt.NeedPermission);
                yield return new WaitForSecondsRealtime(permissionReadTime);
                yield return AskForPermission();
                ShowroomEvents.SetPlacementPrompt(PlacementPrompt.Searching);
            }

            if (!ScenePermission.Granted)
            {
                PlaceFixed();
                yield break;
            }

            yield return LoadRoomAndPlace();
        }

        private IEnumerator AskForPermission()
        {
            _permissionAnswered = false;
            ScenePermission.Request(granted => _permissionAnswered = true);

            // Normally the callback arrives when the user answers. If the request was dropped (another system
            // dialog was open) there is no callback, so stop waiting once the app has had focus for a while.
            float focused = 0f;
            while (!_permissionAnswered && !ScenePermission.Granted)
            {
                focused = OVRManager.hasInputFocus ? focused + Time.unscaledDeltaTime : 0f;
                if (focused > 6f)
                {
                    break;
                }

                yield return null;
            }
        }

        private IEnumerator LoadRoomAndPlace()
        {
            _roomAttempted = true;
            ShowroomEvents.SetPlacementPrompt(PlacementPrompt.Searching);
            yield return finder.LoadFromDevice();

            if (finder.HasRoom)
            {
                PlaceAutomatically();
            }
            else if (finder.CanRaycastEnvironment)
            {
                EnterManual();
            }
            else
            {
                PlaceFixed();
            }
        }

        private void Update()
        {
            if (_head == null)
            {
                return;
            }

            switch (_mode)
            {
                case Mode.Pointing:
                    UpdatePointing();
                    break;
                case Mode.Touching:
                    UpdateTouching();
                    break;
                case Mode.Idle:
                    FollowIfTooFar();
                    WatchForLatePermission();
                    break;
            }
        }

        private void LateUpdate()
        {
            // The anchor moves to stay on the real spot; the stage rides on it.
            if (_anchor != null && _anchor.Created && _anchor.Localized)
            {
                Transform pin = _anchorObject.transform;
                stage.Follow(new Pose(pin.position, pin.rotation));
            }
        }

        // ---- Automatic placement ---------------------------------------------------------------

        private void PlaceAutomatically()
        {
            Vector3 head = _head.position;
            if (finder.TryFindTable(head, out Pose pose, out SurfaceInfo surface)
                || finder.TryFindFloorSpot(head, _head.forward, out pose, out surface))
            {
                Place(pose, surface);
            }
            else
            {
                PlaceFixed();
            }
        }

        private void PlaceFixed()
        {
            float floor = finder.FloorY;
            Vector3 head = _head.position;
            Pose pose = SurfaceMath.InFront(head, _head.forward, tuning.fixedDistance, floor);
            Place(pose, new SurfaceInfo
            {
                method = PlacementMethod.Fixed,
                clearance = 0.15f,
                pedestalHeight = SurfaceMath.PedestalHeight(head.y - floor, tuning.waistHeightRatio,
                    tuning.pedestalMinHeight, tuning.pedestalMaxHeight),
                floorY = floor
            });
        }

        private void Place(Pose pose, SurfaceInfo surface)
        {
            _mode = Mode.Idle;
            _awayTime = 0f;
            _placedOnce = true;
            ShowReticle(false, default, Vector3.up, false);
            ShowroomEvents.SetPlacementPrompt(PlacementPrompt.None);

            stage.Place(pose, surface);
            Pin(pose);

            if (lighting != null)
            {
                Vector3 rest = stage.RestPoint.position;
                if (finder.TryGetWindow(rest, out Vector3 window))
                {
                    lighting.AimFrom(window, rest, _head.position);
                }
                else
                {
                    lighting.UseDefault(_head.position, rest);
                }
            }

            Debug.Log($"[Placement] {surface.method} at {pose.position:F2}, pedestal {surface.pedestalHeight:F2} m.");
        }

        private void Pin(Pose pose)
        {
            if (_anchorObject != null)
            {
                Destroy(_anchorObject);
                _anchorObject = null;
                _anchor = null;
            }

            if (!anchorPlacement || !XRSettings.isDeviceActive)
            {
                return;
            }

            // If the headset can't create the anchor the component removes itself and the stage simply stays put.
            _anchorObject = new GameObject("PlacementAnchor");
            _anchorObject.transform.SetPositionAndRotation(pose.position, pose.rotation);
            _anchor = _anchorObject.AddComponent<OVRSpatialAnchor>();
        }

        // ---- Placement by hand -----------------------------------------------------------------

        private void EnterManual()
        {
            bool canPoint = finder.HasRoom || finder.CanRaycastEnvironment || !XRSettings.isDeviceActive;
            _mode = canPoint ? Mode.Pointing : Mode.Touching;
            _modeTime = 0f;
            ShowroomEvents.SetPlacementPrompt(canPoint ? PlacementPrompt.PointAtSurface : PlacementPrompt.TouchSurface);
        }

        private void UpdatePointing()
        {
            _modeTime += Time.unscaledDeltaTime;

            if (pointer.TryGet(out Ray ray, out _, out bool confirmed))
            {
                bool hit = RaycastSurfaces(ray, out Vector3 point, out Vector3 normal);
                bool usable = hit && normal.y >= minUpDot;
                ShowReticle(hit, point, normal, usable);

                // A pinch aimed at one of the panel's buttons is a button press, not a placement.
                if (confirmed && usable && _modeTime >= armingTime
                    && !PanelFader.BlocksRay(ray, Vector3.Distance(ray.origin, point)))
                {
                    PlaceAt(point, PlacementMethod.Pointed);
                    return;
                }
            }
            else
            {
                ShowReticle(false, default, Vector3.up, false);
            }

            GiveUpIfIgnored();
        }

        private void UpdateTouching()
        {
            _modeTime += Time.unscaledDeltaTime;

            if (pointer.TryGet(out _, out Vector3 tip, out bool confirmed))
            {
                Vector3 point = tip + Vector3.down * touchOffset;
                ShowReticle(true, point, Vector3.up, true);
                if (confirmed && _modeTime >= armingTime && !PanelFader.IsNear(tip, 0.06f))
                {
                    PlaceAt(point, PlacementMethod.Touch);
                    return;
                }
            }
            else
            {
                ShowReticle(false, default, Vector3.up, false);
            }

            GiveUpIfIgnored();
        }

        // The very first placement can't wait for ever: after a while the pedestal is used. A later "Move"
        // has no deadline, because the product already has a home.
        private void GiveUpIfIgnored()
        {
            if (!_placedOnce && _modeTime >= tuning.pointTimeout)
            {
                PlaceFixed();
            }
        }

        private bool RaycastSurfaces(Ray ray, out Vector3 point, out Vector3 normal)
        {
            if (finder.Raycast(ray, tuning.pointerRange, out point, out normal))
            {
                return true;
            }

#if UNITY_EDITOR
            // No headset and no sample room: let the mouse place on the floor plane, so the mode can be tried.
            if (!XRSettings.isDeviceActive && !finder.HasRoom)
            {
                var floor = new Plane(Vector3.up, new Vector3(0f, finder.FloorY, 0f));
                if (floor.Raycast(ray, out float distance) && distance <= tuning.pointerRange)
                {
                    point = ray.GetPoint(distance);
                    normal = Vector3.up;
                    return true;
                }
            }
#endif
            return false;
        }

        private void PlaceAt(Vector3 point, PlacementMethod method)
        {
            Place(SurfaceMath.FacingAwayFrom(_head.position, point), new SurfaceInfo
            {
                method = method,
                clearance = 0f,
                pedestalHeight = 0f,
                floorY = finder.FloorY
            });
        }

        private void ShowReticle(bool show, Vector3 point, Vector3 normal, bool usable)
        {
            if (reticle == null)
            {
                return;
            }

            if (reticle.gameObject.activeSelf != show)
            {
                reticle.gameObject.SetActive(show);
            }

            if (!show)
            {
                return;
            }

            reticle.SetPositionAndRotation(point + normal * 0.004f, Quaternion.FromToRotation(Vector3.up, normal));
            if (reticleRenderer != null)
            {
                _block.SetColor(BaseColorId, usable ? reticleValid : reticleInvalid);
                reticleRenderer.SetPropertyBlock(_block);
            }
        }

        // ---- Staying in reach ------------------------------------------------------------------

        // Nobody should have to walk back to the product. If they stay away from it, it comes to them.
        private void FollowIfTooFar()
        {
            if (!stage.IsPlaced || (product != null && product.IsHeld))
            {
                _awayTime = 0f;
                return;
            }

            Vector3 offset = _head.position - stage.RestPoint.position;
            offset.y = 0f;
            if (offset.sqrMagnitude <= tuning.followDistance * tuning.followDistance)
            {
                _awayTime = 0f;
                return;
            }

            _awayTime += Time.deltaTime;
            if (_awayTime >= tuning.followDelay)
            {
                PlaceAutomatically();
            }
        }

        // If the permission is granted later (from the system settings, or by a dialog that was slow to
        // open), take the better placement as soon as it is possible.
        private void WatchForLatePermission()
        {
            if (_roomAttempted || !XRSettings.isDeviceActive || !ScenePermission.Granted
                || (product != null && product.IsHeld))
            {
                return;
            }

            _roomAttempted = true;
            StartCoroutine(UpgradeToRoom());
        }

        private IEnumerator UpgradeToRoom()
        {
            yield return finder.LoadFromDevice();
            if (finder.HasRoom && _mode == Mode.Idle)
            {
                PlaceAutomatically();
            }
        }

        // ---- Requests from the UI --------------------------------------------------------------

        private void HandleManualRequested()
        {
            if (_head != null && _mode != Mode.Starting)
            {
                EnterManual();
            }
        }

        private void HandleSkipRequested()
        {
            if (_head != null)
            {
                PlaceFixed();
            }
        }

        private async void HandleSpaceSetupRequested()
        {
            if (!XRSettings.isDeviceActive)
            {
                return;
            }

            // The system takes over for the scan and hands back when the user is done or cancels.
            ShowReticle(false, default, Vector3.up, false);
            _mode = Mode.Starting;
            await OVRScene.RequestSpaceSetup();

            if (this != null && isActiveAndEnabled)
            {
                StartCoroutine(LoadRoomAndPlace());
            }
        }

        // ---- Head ------------------------------------------------------------------------------

        private void FindHead()
        {
            Camera main = Camera.main;
            _head = main != null ? main.transform : null;
        }

        private bool HeadIsTracked()
        {
            if (_head == null)
            {
                FindHead();
            }

            // With a floor-level origin a tracked head is never at the origin; without a headset there is nothing to wait for.
            return _head != null && (_head.position.y > 0.3f || !XRSettings.isDeviceActive && Time.timeSinceLevelLoad > 0.2f);
        }
    }
}
