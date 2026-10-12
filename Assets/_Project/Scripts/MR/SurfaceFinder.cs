using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Meta.XR;
using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace MRShowroom.MR
{
    /// <summary>
    /// Reads the user's room through the Mixed Reality Utility Kit and answers the questions
    /// placement asks of it: is there a table within reach, where on the floor is free, and what
    /// does this ray hit. The table search runs once per placement, never per frame.
    ///
    /// In the editor without a headset it can load one of MRUK's sample rooms instead, so the same
    /// logic can be exercised at a desk.
    /// </summary>
    public sealed class SurfaceFinder : MonoBehaviour
    {
        public enum RoomState
        {
            NotLoaded,
            Loading,
            Ready,
            /// <summary>The headset has no scanned room (Space Setup was never run here).</summary>
            NoRoom,
            NoPermission,
            Failed
        }

        [SerializeField] private ShowroomTuning tuning;
        [SerializeField] private MRUK mruk;

        [Header("Editor without a headset")]
        [Tooltip("Sample rooms (JSON, as saved by MRUK) to stand in for a scanned room. Kept as paths, not " +
                 "references, so they never end up in a build.")]
        [SerializeField] private string[] editorRooms = new string[0];
        [Tooltip("Which sample room to load. -1 loads none, which tests the no-room path.")]
        [SerializeField] private int editorRoom;

        // Directions to try for a free spot on the floor, as angles off straight ahead.
        private static readonly float[] FloorSearchAngles = { 0f, 35f, -35f, 70f, -70f, 110f, -110f, 180f };

        private readonly List<TableCandidate> _tables = new List<TableCandidate>(8);
        private EnvironmentRaycastManager _environmentRaycast;

        public RoomState State { get; private set; } = RoomState.NotLoaded;

        public MRUKRoom Room { get; private set; }

        public bool HasRoom => State == RoomState.Ready && Room != null;

        /// <summary>World height of the floor. Zero (the tracking origin) until a room says otherwise.</summary>
        public float FloorY
        {
            get
            {
                if (HasRoom && Room.FloorAnchor != null)
                {
                    return Room.FloorAnchor.transform.position.y;
                }

                return 0f;
            }
        }

        /// <summary>True where the headset can raycast against the real room without a room scan.</summary>
        public bool CanRaycastEnvironment => ScenePermission.Granted && EnvironmentRaycastManager.IsSupported;

        public int EditorRoomCount => editorRooms.Length;

        public int EditorRoom
        {
            get => editorRoom;
            set => editorRoom = value;
        }

        /// <summary>Loads the room the headset has scanned. Never starts Space Setup by itself.</summary>
        public IEnumerator LoadFromDevice()
        {
            if (!ScenePermission.Granted)
            {
                State = RoomState.NoPermission;
                yield break;
            }

            State = RoomState.Loading;
            Task<MRUK.LoadDeviceResult> task = mruk.LoadSceneFromDevice(requestSceneCaptureIfNoDataFound: false);
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogWarning("[SurfaceFinder] Loading the room failed: " + task.Exception?.GetBaseException().Message);
                State = RoomState.Failed;
                yield break;
            }

            switch (task.Result)
            {
                case MRUK.LoadDeviceResult.Success:
                    Finish();
                    break;
                case MRUK.LoadDeviceResult.NoScenePermission:
                    State = RoomState.NoPermission;
                    break;
                case MRUK.LoadDeviceResult.NoRoomsFound:
                    State = RoomState.NoRoom;
                    break;
                default:
                    Debug.LogWarning("[SurfaceFinder] Loading the room returned " + task.Result + ".");
                    State = RoomState.Failed;
                    break;
            }
        }

        /// <summary>Loads the selected sample room. Does nothing outside the editor.</summary>
        public IEnumerator LoadEditorRoom()
        {
            string json = null;
#if UNITY_EDITOR
            if (editorRoom >= 0 && editorRoom < editorRooms.Length)
            {
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(editorRooms[editorRoom]);
                json = asset != null ? asset.text : null;
            }
#endif
            if (string.IsNullOrEmpty(json))
            {
                State = RoomState.NoRoom;
                yield break;
            }

            yield return LoadFromJson(json);
        }

        public IEnumerator LoadFromJson(string json)
        {
            State = RoomState.Loading;
            Task<MRUK.LoadDeviceResult> task = mruk.LoadSceneFromJsonString(json);
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted || task.IsCanceled || task.Result != MRUK.LoadDeviceResult.Success)
            {
                Debug.LogWarning("[SurfaceFinder] Loading the sample room failed.");
                State = RoomState.Failed;
                yield break;
            }

            Finish();
        }

        private void Finish()
        {
            Room = mruk.GetCurrentRoom();
            if (Room == null && mruk.Rooms.Count > 0)
            {
                Room = mruk.Rooms[0];
            }

            State = Room != null ? RoomState.Ready : RoomState.NoRoom;
        }

        /// <summary>The table nearest the user that is at a usable height and within reach, and where on it to put the product.</summary>
        public bool TryFindTable(Vector3 head, out Pose pose, out SurfaceInfo surface)
        {
            pose = default;
            surface = default;
            if (!HasRoom)
            {
                return false;
            }

            float floor = FloorY;
            _tables.Clear();
            List<MRUKAnchor> anchors = Room.Anchors;
            for (int i = 0; i < anchors.Count; i++)
            {
                if (anchors[i] != null && anchors[i].HasAnyLabel(MRUKAnchor.SceneLabels.TABLE)
                                       && TryDescribe(anchors[i], floor, out TableCandidate table))
                {
                    _tables.Add(table);
                }
            }

            int best = SurfaceMath.ChooseTable(_tables, head, tuning.tableMinHeight, tuning.tableMaxHeight,
                tuning.maxTableDistance);
            if (best < 0)
            {
                return false;
            }

            pose = SurfaceMath.PlaceOnTable(_tables[best], head, tuning.preferredReach, tuning.edgeMargin,
                out float clearance);
            surface = new SurfaceInfo
            {
                method = PlacementMethod.Table,
                clearance = clearance,
                pedestalHeight = 0f,
                floorY = floor
            };
            return true;
        }

        /// <summary>
        /// A spot on the floor near the user for the pedestal: inside the room, and clear of walls and furniture.
        /// </summary>
        public bool TryFindFloorSpot(Vector3 head, Vector3 headForward, out Pose pose, out SurfaceInfo surface)
        {
            pose = default;
            surface = default;
            if (!HasRoom)
            {
                return false;
            }

            float floor = FloorY;
            Vector3 forward = SurfaceMath.FlatForward(headForward);
            const float footprint = 0.3f;

            // Straight ahead first, then fanning out, then closer in.
            for (int pass = 0; pass < 2; pass++)
            {
                float distance = tuning.fixedDistance * (pass == 0 ? 1f : 0.7f);
                for (int i = 0; i < FloorSearchAngles.Length; i++)
                {
                    Vector3 direction = Quaternion.Euler(0f, FloorSearchAngles[i], 0f) * forward;
                    var spot = new Vector3(head.x + direction.x * distance, floor, head.z + direction.z * distance);

                    if (!Room.IsPositionInRoom(spot + Vector3.up * 0.1f, false)
                        || Room.IsPositionInSceneVolume(spot + Vector3.up * 0.3f, false, footprint))
                    {
                        continue;
                    }

                    pose = new Pose(spot, Quaternion.LookRotation(direction, Vector3.up));
                    surface = new SurfaceInfo
                    {
                        method = PlacementMethod.Floor,
                        clearance = footprint * 0.5f,
                        pedestalHeight = SurfaceMath.PedestalHeight(head.y - floor, tuning.waistHeightRatio,
                            tuning.pedestalMinHeight, tuning.pedestalMaxHeight),
                        floorY = floor
                    };
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// What a ray from the user's hand hits: the scanned room if there is one, otherwise the real
        /// room as the depth sensor sees it.
        /// </summary>
        public bool Raycast(Ray ray, float maxDistance, out Vector3 point, out Vector3 normal)
        {
            point = default;
            normal = Vector3.up;

            if (HasRoom)
            {
                // The global mesh is a rough shell of the whole room; the labelled anchors are cleaner to stand on.
                LabelFilter filter = LabelFilter.Excluded(MRUKAnchor.SceneLabels.GLOBAL_MESH
                                                          | MRUKAnchor.SceneLabels.CEILING);
                if (Room.Raycast(ray, maxDistance, filter, out RaycastHit hit))
                {
                    point = hit.point;
                    normal = hit.normal;
                    return true;
                }

                return false;
            }

            if (!CanRaycastEnvironment)
            {
                return false;
            }

            if (_environmentRaycast == null)
            {
                // Created on first use: the component warms the depth sensor up, which isn't free.
                _environmentRaycast = gameObject.AddComponent<EnvironmentRaycastManager>();
            }

            if (_environmentRaycast.Raycast(ray, out EnvironmentRaycastHit environmentHit, maxDistance))
            {
                point = environmentHit.point;
                // No usable normal means the sensor couldn't tell; assume the flat surface the user aimed at.
                normal = environmentHit.normalConfidence > 0f ? environmentHit.normal : Vector3.up;
                return true;
            }

            return false;
        }

        /// <summary>The window nearest a point, for lighting the product from the same side as the room.</summary>
        public bool TryGetWindow(Vector3 near, out Vector3 window)
        {
            window = default;
            if (!HasRoom)
            {
                return false;
            }

            float best = float.MaxValue;
            List<MRUKAnchor> anchors = Room.Anchors;
            for (int i = 0; i < anchors.Count; i++)
            {
                if (anchors[i] == null || !anchors[i].HasAnyLabel(MRUKAnchor.SceneLabels.WINDOW_FRAME))
                {
                    continue;
                }

                Vector3 centre = anchors[i].GetAnchorCenter();
                float distance = (centre - near).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    window = centre;
                }
            }

            return best < float.MaxValue;
        }

        // An MRUK anchor's forward axis points out of its surface, so for a table top it points up, and the
        // top lies in the anchor's own XY plane. A volume's origin is at the top of the volume.
        private static bool TryDescribe(MRUKAnchor anchor, float floor, out TableCandidate table)
        {
            table = default;
            Transform frame = anchor.transform;
            if (frame.forward.y < 0.9f)
            {
                return false;
            }

            Vector2 centre;
            Vector2 size;
            float lift = 0f;
            if (anchor.PlaneRect.HasValue)
            {
                Rect rect = anchor.PlaneRect.Value;
                centre = rect.center;
                size = rect.size;
            }
            else if (anchor.VolumeBounds.HasValue)
            {
                Bounds bounds = anchor.VolumeBounds.Value;
                centre = new Vector2(bounds.center.x, bounds.center.y);
                size = new Vector2(bounds.size.x, bounds.size.y);
                lift = bounds.max.z;
            }
            else
            {
                return false;
            }

            Vector3 axisX = frame.right;
            Vector3 axisZ = frame.up;
            axisX.y = 0f;
            axisZ.y = 0f;
            if (axisX.sqrMagnitude < 0.0001f || axisZ.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            Vector3 top = frame.TransformPoint(new Vector3(centre.x, centre.y, lift));
            table = new TableCandidate
            {
                center = top,
                axisX = axisX.normalized,
                axisZ = axisZ.normalized,
                halfExtents = size * 0.5f,
                height = top.y - floor
            };
            return true;
        }
    }
}
