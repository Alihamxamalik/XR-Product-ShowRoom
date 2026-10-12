using System;
using System.Collections.Generic;
using UnityEngine;

namespace MRShowroom
{
    /// <summary>
    /// Builds the product from its <see cref="ProductDefinition"/> and is the one place the rest
    /// of the showroom talks to it: show or hide it, make it grabbable, recolour it, open it up.
    /// Swapping in another product is a call to <see cref="Load"/> with a different definition.
    /// </summary>
    public sealed class ProductController : MonoBehaviour
    {
        [SerializeField] private ProductDefinition definition;
        [SerializeField] private ShowroomTuning tuning;
        [SerializeField] private ProductStage stage;
        [SerializeField] private ColorCustomizer colors;
        [SerializeField] private ExplodeController explode;
        [SerializeField] private HotspotView hotspotPrefab;
        [Tooltip("How fast the hover glow fades in and out.")]
        [SerializeField] private float rimFadeSpeed = 9f;

        private static readonly int RimId = Shader.PropertyToID("_Rim");

        private readonly List<HotspotView> _hotspots = new List<HotspotView>(6);
        private MaterialPropertyBlock _block;
        private float[] _rim = new float[0];
        private bool _interactable;
        private bool _hotspotsWanted;
        private bool _visible = true;
        private bool _carry;
        private Vector3 _carryPosition;
        private Quaternion _carryRotation;

        /// <summary>Raised after a product has been built, including the first one.</summary>
        public event Action<ProductController> Loaded;

        public ProductDefinition Definition => definition;
        public ProductView View { get; private set; }
        public ColorCustomizer Colors => colors;
        public ExplodeController Explode => explode;
        public GrabTarget Grab => View != null ? View.Grab : null;
        public bool IsVisible => _visible;

        /// <summary>True while the product or any of its parts is in someone's hand.</summary>
        public bool IsHeld
        {
            get
            {
                if (View == null)
                {
                    return false;
                }

                if (View.Grab != null && View.Grab.IsHeld)
                {
                    return true;
                }

                ProductView.Part[] parts = View.Parts;
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i].grab != null && parts[i].grab.IsHeld)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (definition != null)
            {
                Build();
            }
        }

        private void OnEnable()
        {
            explode.Changed += RefreshInteraction;
            stage.Moving += HandleStageMoving;
            stage.Placed += HandleStagePlaced;
        }

        private void OnDisable()
        {
            explode.Changed -= RefreshInteraction;
            stage.Moving -= HandleStageMoving;
            stage.Placed -= HandleStagePlaced;
        }

        // When the stage moves to a new spot the product stays where it was for that instant, and its own
        // spring then carries it over: a glide instead of a jump.
        private void HandleStageMoving()
        {
            _carry = View != null && _visible;
            if (_carry)
            {
                _carryPosition = View.transform.position;
                _carryRotation = View.transform.rotation;
            }
        }

        private void HandleStagePlaced(SurfaceInfo surface)
        {
            if (!_carry || View == null)
            {
                return;
            }

            _carry = false;
            View.transform.SetPositionAndRotation(_carryPosition, _carryRotation);
            if (View.Rest != null)
            {
                View.Rest.WakeUp();
            }
        }

        public void Load(ProductDefinition next)
        {
            definition = next;
            Build();
        }

        private void Build()
        {
            if (View != null)
            {
                Destroy(View.gameObject);
            }

            for (int i = 0; i < _hotspots.Count; i++)
            {
                if (_hotspots[i] != null)
                {
                    Destroy(_hotspots[i].gameObject);
                }
            }

            _hotspots.Clear();

            View = Instantiate(definition.prefab, stage.RestPoint);
            View.name = definition.displayName;
            View.transform.localPosition = Vector3.zero;
            View.transform.localRotation = Quaternion.identity;
            View.transform.localScale = Vector3.one;

            ProductView.Part[] parts = View.Parts;
            _rim = new float[parts.Length];

            if (View.Rest != null)
            {
                View.Rest.Configure(tuning.returnFrequency, tuning.returnDamping, tuning.trackingLostHold);
                View.Rest.CaptureRest();
            }

            // Parts snap back a little faster than the product itself: they have less far to go.
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].rest != null)
                {
                    parts[i].rest.Configure(tuning.returnFrequency * 1.4f, 1f, tuning.trackingLostHold);
                }
            }

            colors.Initialise(definition, View);
            explode.Initialise(definition, View);
            BuildHotspots();

            View.SetVisible(_visible);
            RefreshInteraction();
            Loaded?.Invoke(this);
        }

        private void BuildHotspots()
        {
            if (hotspotPrefab == null)
            {
                return;
            }

            HotspotData[] data = definition.hotspots;
            for (int i = 0; i < data.Length; i++)
            {
                ProductView.Part part = View.FindPart(data[i].partName);
                Transform parent = part != null ? part.slot : View.VisualRoot;

                HotspotView hotspot = Instantiate(hotspotPrefab, parent);
                hotspot.name = "Hotspot_" + data[i].title;
                // Authored in the product's space; stored in the part's so the dot rides with that part.
                hotspot.transform.localPosition =
                    parent.InverseTransformPoint(View.transform.TransformPoint(data[i].localPosition));
                hotspot.Bind(data[i], i);
                hotspot.SetProduct(View.transform, View.LocalBounds);
                _hotspots.Add(hotspot);
            }
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (View != null)
            {
                View.SetVisible(visible);
            }

            RefreshInteraction();
        }

        /// <summary>Whether the user may pick the product (or its parts) up right now.</summary>
        public void SetInteractable(bool interactable)
        {
            _interactable = interactable;
            RefreshInteraction();
        }

        public void ShowHotspots(bool show)
        {
            _hotspotsWanted = show;
            RefreshInteraction();
        }

        /// <summary>Back to how it came out of the box: default colours, assembled, original size, at rest.</summary>
        public void ResetProduct()
        {
            explode.SetImmediate(false);
            colors.ResetToDefault();
            ResetScale();
            SnapToRest();
        }

        public void ResetScale()
        {
            if (View != null)
            {
                View.transform.localScale = Vector3.one;
            }
        }

        public void SnapToRest()
        {
            if (View == null)
            {
                return;
            }

            if (View.Rest != null)
            {
                View.Rest.SnapToRest();
            }

            ProductView.Part[] parts = View.Parts;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].rest != null)
                {
                    parts[i].rest.SnapToRest();
                }
            }
        }

        // One rule for who can be grabbed: the whole product while it is together, its parts while it is
        // fully apart, nothing while it is moving between the two.
        private void RefreshInteraction()
        {
            if (View == null)
            {
                return;
            }

            bool live = _interactable && _visible;
            bool apart = explode.IsOpen && !explode.IsMoving;
            bool together = !explode.IsOpen && !explode.IsMoving;

            if (View.Grab != null)
            {
                View.Grab.IsGrabbable = live && together;
            }

            ProductView.Part[] parts = View.Parts;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].grab == null)
                {
                    continue;
                }

                ExplodePart data = definition.FindPart(parts[i].name);
                parts[i].grab.IsGrabbable = live && apart && (data == null || data.grabbable);
            }

            // Hotspots and part labels would crowd each other, so the dots rest while the product is apart.
            bool showHotspots = _hotspotsWanted && _visible && together;
            for (int i = 0; i < _hotspots.Count; i++)
            {
                _hotspots[i].SetShown(showHotspots);
            }
        }

        // Hover glow: a soft rim light on whatever a hand is close to. Written per renderer through a
        // property block, and the block is cleared again at zero so the renderers go back to batching.
        private void LateUpdate()
        {
            if (View == null)
            {
                return;
            }

            bool wholeHovered = View.Grab != null && View.Grab.IsHovered && !View.Grab.IsHeld;
            float blend = Ease.Damp(rimFadeSpeed, Time.unscaledDeltaTime);

            ProductView.Part[] parts = View.Parts;
            for (int i = 0; i < parts.Length; i++)
            {
                GrabTarget grab = parts[i].grab;
                bool hovered = wholeHovered || (grab != null && grab.IsHovered && !grab.IsHeld);
                float target = hovered ? 1f : 0f;
                float previous = _rim[i];
                if (Mathf.Approximately(previous, target))
                {
                    continue;
                }

                float value = Mathf.Lerp(previous, target, blend);
                if (Mathf.Abs(value - target) < 0.01f)
                {
                    value = target;
                }

                _rim[i] = value;
                Renderer[] renderers = parts[i].renderers;
                for (int r = 0; r < renderers.Length; r++)
                {
                    if (renderers[r] == null)
                    {
                        continue;
                    }

                    if (value <= 0f)
                    {
                        renderers[r].SetPropertyBlock(null);
                    }
                    else
                    {
                        _block.SetFloat(RimId, value);
                        renderers[r].SetPropertyBlock(_block);
                    }
                }
            }
        }
    }
}
