using System;
using UnityEngine;

namespace MRShowroom
{
    /// <summary>What the product is standing on.</summary>
    public struct SurfaceInfo
    {
        public PlacementMethod method;

        /// <summary>Free room around the product on the surface (m). Zero when unknown.</summary>
        public float clearance;

        /// <summary>Height of the pedestal under the product (m). Zero when it sits on a real surface.</summary>
        public float pedestalHeight;

        /// <summary>World height of the floor below, for the support drawn in the virtual studio.</summary>
        public float floorY;
    }

    /// <summary>
    /// The spot where the product lives. The mixed reality layer decides where that is (a real
    /// table, the floor, a pedestal) and calls <see cref="Place"/>; everything else positions
    /// itself relative to <see cref="RestPoint"/> and never needs to know how it was found.
    ///
    /// The stage's forward axis points away from the user as they stood at placement time, so
    /// its right is their right.
    /// </summary>
    public sealed class ProductStage : MonoBehaviour
    {
        [Tooltip("Where the product rests. A child of this object, raised by the pedestal height.")]
        [SerializeField] private Transform restPoint;
        [Tooltip("Drawn under the product when there is no real surface: a column with a round top.")]
        [SerializeField] private GameObject pedestal;
        [SerializeField] private Transform pedestalColumn;
        [SerializeField] private Transform pedestalTop;
        [SerializeField] private float pedestalTopThickness = 0.02f;
        [Tooltip("The invisible plane that catches the product's shadow on a real surface. Off over the pedestal, " +
                 "which is lit and shadowed like any other object.")]
        [SerializeField] private GameObject shadowCatcher;

        /// <summary>Raised just before the stage moves to a new spot.</summary>
        public event Action Moving;
        public event Action<SurfaceInfo> Placed;

        public Transform RestPoint => restPoint;
        public bool IsPlaced { get; private set; }
        public SurfaceInfo Surface { get; private set; }

        private void Awake()
        {
            if (pedestal != null)
            {
                pedestal.SetActive(false);
            }
        }

        private void OnEnable()
        {
            MixedReality.Changed += HandlePassthroughChanged;
        }

        private void OnDisable()
        {
            MixedReality.Changed -= HandlePassthroughChanged;
        }

        /// <param name="pose">Point on the surface (or on the floor, under a pedestal), facing away from the user.</param>
        public void Place(Pose pose, SurfaceInfo surface)
        {
            if (IsPlaced)
            {
                Moving?.Invoke();
            }

            transform.SetPositionAndRotation(pose.position, pose.rotation);
            Surface = surface;
            restPoint.localPosition = new Vector3(0f, surface.pedestalHeight, 0f);
            restPoint.localRotation = Quaternion.identity;
            UpdateSupport();

            IsPlaced = true;
            Placed?.Invoke(surface);
            ShowroomEvents.RaisePlaced(surface.method);
        }

        /// <summary>Keeps the stage on its anchor as tracking settles. Not a new placement: nothing is announced.</summary>
        public void Follow(Pose pose)
        {
            transform.SetPositionAndRotation(pose.position, pose.rotation);
        }

        private void HandlePassthroughChanged(bool active)
        {
            if (IsPlaced)
            {
                UpdateSupport();
            }
        }

        // In the real room a table needs nothing drawn under the product. In the virtual studio there
        // is no table to see, so the pedestal stands in for it and reaches down to the floor.
        private void UpdateSupport()
        {
            if (pedestal == null)
            {
                return;
            }

            float height = Surface.pedestalHeight;
            if (height <= 0f && !MixedReality.PassthroughActive)
            {
                height = restPoint.position.y - Surface.floorY;
            }

            bool show = height > 0.05f;
            pedestal.SetActive(show);
            if (shadowCatcher != null)
            {
                shadowCatcher.SetActive(!show);
            }

            if (!show)
            {
                return;
            }

            pedestal.transform.localPosition = restPoint.localPosition;
            float columnHeight = Mathf.Max(height - pedestalTopThickness, 0.01f);
            if (pedestalTop != null)
            {
                pedestalTop.localPosition = new Vector3(0f, -pedestalTopThickness * 0.5f, 0f);
            }

            if (pedestalColumn != null)
            {
                Vector3 scale = pedestalColumn.localScale;
                pedestalColumn.localScale = new Vector3(scale.x, columnHeight, scale.z);
                pedestalColumn.localPosition = new Vector3(0f, -pedestalTopThickness - columnHeight * 0.5f, 0f);
            }
        }
    }
}
