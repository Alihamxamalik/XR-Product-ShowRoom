using System;
using UnityEngine;

namespace MRShowroom
{
    /// <summary>
    /// The exploded view. Parts move out along the directions authored in the product definition
    /// and labels fade in beside them, each pointing at its part; the reverse puts the product back
    /// together.
    ///
    /// Opened by pulling the product apart with two hands (hands on it, then 25 cm further apart)
    /// or by the "See inside" button. Closed by holding two parts and bringing them together, or
    /// by the button. The gesture reads hand data from the product's <see cref="GrabTarget"/>, so
    /// it works the same whatever is doing the grabbing.
    /// </summary>
    public sealed class ExplodeController : MonoBehaviour
    {
        [SerializeField] private ShowroomTuning tuning;
        [SerializeField] private PartLabel labelPrefab;
        [Tooltip("Labels start to appear once the parts are this far out (0-1).")]
        [SerializeField] [Range(0f, 0.95f)] private float labelsFrom = 0.55f;

        [Tooltip("Gap between a part and its label (m).")]
        [SerializeField] private float labelGap = 0.03f;
        [Tooltip("How far in from a part's edge its label's dot sits (m).")]
        [SerializeField] private float dotInset = 0.03f;
        [Tooltip("A label keeps this far above and below any part it sits beside (m).")]
        [SerializeField] private float labelClearance = 0.01f;
        [Tooltip("Least height between one label and the next (m).")]
        [SerializeField] private float labelSpacing = 0.024f;

        private struct Entry
        {
            public ProductView.Part Part;
            public Vector3 Assembled;
            public Vector3 Exploded;
            public PartLabel Label;
            public float LabelHeight;
            // The part where it rests, as a box in its slot's space.
            public Bounds Shape;
        }

        private Transform _head;

        private ProductView _view;
        private Entry[] _entries = new Entry[0];

        // Worked out afresh each frame the labels are showing; kept so that costs no allocations.
        private float[] _low = new float[0];
        private float[] _high = new float[0];
        private float[] _reach = new float[0];
        private float[] _height = new float[0];
        private Vector3[] _point = new Vector3[0];
        private int[] _order = new int[0];
        private bool _open;
        private float _value;
        private float _from;
        private float _progress = 1f;

        private bool _pulling;
        private float _pullStart;
        private bool _pushing;
        private float _pushStart;

        /// <summary>Raised when the view starts opening or closing, and again when it has finished moving.</summary>
        public event Action Changed;

        /// <summary>The pull gesture is only armed once the flow has introduced it, so resizing doesn't trip it early.</summary>
        public bool GestureEnabled { get; set; }

        /// <summary>True from the moment the view starts opening until it starts closing.</summary>
        public bool IsOpen => _open;

        public bool IsMoving => _progress < 1f;

        /// <summary>0 = assembled, 1 = fully apart.</summary>
        public float Amount => _value;

        public void Initialise(ProductDefinition definition, ProductView view)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].Label != null)
                {
                    Destroy(_entries[i].Label.gameObject);
                }
            }

            _view = view;
            Transform root = view.transform;
            ProductView.Part[] parts = view.Parts;
            _entries = new Entry[parts.Length];
            _low = new float[parts.Length];
            _high = new float[parts.Length];
            _reach = new float[parts.Length];
            _height = new float[parts.Length];
            _point = new Vector3[parts.Length];
            _order = new int[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                ProductView.Part part = parts[i];
                ExplodePart data = definition.FindPart(part.name);
                Transform slotParent = part.slot.parent;

                var entry = new Entry
                {
                    Part = part,
                    Assembled = part.slot.localPosition,
                    Exploded = part.slot.localPosition,
                    Shape = ShapeOf(part)
                };

                if (data != null)
                {
                    Vector3 direction = data.direction.sqrMagnitude > 0.0001f ? data.direction.normalized : Vector3.up;
                    Vector3 offset = slotParent.InverseTransformVector(root.TransformVector(direction * data.distance));
                    entry.Exploded = entry.Assembled + offset;

                    if (labelPrefab != null && !string.IsNullOrEmpty(data.label))
                    {
                        entry.Label = Instantiate(labelPrefab, part.slot);
                        entry.LabelHeight = data.labelHeight;
                        entry.Label.SetText(data.label);
                        entry.Label.SetAlpha(0f);
                    }
                }

                _entries[i] = entry;
            }

            SetImmediate(false);
        }

        public void Explode(ExplodeTrigger trigger)
        {
            if (_open || _view == null)
            {
                return;
            }

            Begin(true);
            ShowroomEvents.RaiseSfx(Sfx.ExplodeWhoosh, _view.transform.position);
            ShowroomEvents.RaiseExploded(trigger);
        }

        public void Reassemble(ExplodeTrigger trigger)
        {
            if (!_open || _view == null)
            {
                return;
            }

            Begin(false);
            ShowroomEvents.RaiseSfx(Sfx.ReassembleSnap, _view.transform.position);
            ShowroomEvents.RaiseReassembled(trigger);
        }

        public void Toggle(ExplodeTrigger trigger)
        {
            if (_open)
            {
                Reassemble(trigger);
            }
            else
            {
                Explode(trigger);
            }
        }

        public void SetImmediate(bool open)
        {
            _open = open;
            _value = open ? 1f : 0f;
            _from = _value;
            _progress = 1f;
            _pulling = false;
            _pushing = false;
            Apply();
            Changed?.Invoke();
        }

        private void Begin(bool open)
        {
            _open = open;
            _from = _value;
            _progress = 0f;
            _pulling = false;
            _pushing = false;
            Changed?.Invoke();
        }

        private void Update()
        {
            if (_view == null)
            {
                return;
            }

            if (_progress < 1f)
            {
                float duration = Mathf.Max(tuning != null ? tuning.explodeDuration : 0.8f, 0.01f);
                _progress = Mathf.Min(_progress + Time.deltaTime / duration, 1f);
                _value = Mathf.Lerp(_from, _open ? 1f : 0f, Ease.OutCubic(_progress));
                Apply();
                if (_progress >= 1f)
                {
                    Changed?.Invoke();
                }

                return;
            }

            if (_open)
            {
                WatchForPush();
            }
            else
            {
                WatchForPull();
            }
        }

        private void Apply()
        {
            float labelAlpha = Mathf.InverseLerp(labelsFrom, 1f, _value);
            for (int i = 0; i < _entries.Length; i++)
            {
                Entry entry = _entries[i];
                entry.Part.slot.localPosition = Vector3.LerpUnclamped(entry.Assembled, entry.Exploded, _value);
                if (entry.Label != null)
                {
                    entry.Label.SetAlpha(labelAlpha);
                }
            }
        }

        // Each label's text sits level with its part, just beyond everything at that height, on the side
        // of the product away from the colour panel as the user sees it right now. So labels and panel
        // never share a side, and no text lands on a part, wherever the user stands and whichever hand
        // they use. Where another part sits in between, the label's line crosses it to reach its own.
        // Parts are measured where they rest, so carrying one about doesn't push the labels around.
        private void LateUpdate()
        {
            if (_view == null || Mathf.InverseLerp(labelsFrom, 1f, _value) <= 0.01f)
            {
                return;
            }

            if (_head == null)
            {
                Camera main = Camera.main;
                if (main == null)
                {
                    return;
                }

                _head = main.transform;
            }

            Vector3 centre = _view.transform.position;
            Vector3 view = centre - _head.position;
            view.y = 0f;
            view = view.sqrMagnitude < 0.0001f ? Vector3.forward : view.normalized;
            float side = ShowroomSettings.LeftHanded ? 1f : -1f;
            Vector3 outward = Vector3.Cross(Vector3.up, view) * side;

            int labels = 0;
            for (int i = 0; i < _entries.Length; i++)
            {
                Entry entry = _entries[i];
                Matrix4x4 matrix = entry.Part.slot.localToWorldMatrix;
                Vector3 middle = matrix.MultiplyPoint3x4(entry.Shape.center);
                float tall = BoxMath.Span(matrix, entry.Shape.extents, Vector3.up);
                float wide = BoxMath.Span(matrix, entry.Shape.extents, outward);
                _low[i] = middle.y - tall;
                _high[i] = middle.y + tall;
                _reach[i] = Vector3.Dot(middle - centre, outward) + wide;

                if (entry.Label != null)
                {
                    _height[i] = matrix.MultiplyPoint3x4(new Vector3(0f, entry.LabelHeight, 0f)).y;

                    // The dot goes on the part itself: a little way in from the edge nearest the text.
                    Vector3 point = middle + outward * (wide - Mathf.Min(dotInset, wide));
                    point.y = Mathf.Clamp(_height[i], _low[i], _high[i]);
                    _point[i] = point;
                    _order[labels++] = i;
                }
            }

            // Lowest label first, so each can be lifted clear of the one below it.
            for (int a = 1; a < labels; a++)
            {
                int moving = _order[a];
                int b = a - 1;
                while (b >= 0 && _height[_order[b]] > _height[moving])
                {
                    _order[b + 1] = _order[b];
                    b--;
                }

                _order[b + 1] = moving;
            }

            for (int a = 0; a < labels; a++)
            {
                int i = _order[a];
                if (a > 0)
                {
                    _height[i] = Mathf.Max(_height[i], _height[_order[a - 1]] + labelSpacing);
                }

                float height = _height[i];
                float reach = _reach[i];
                for (int j = 0; j < _entries.Length; j++)
                {
                    if (height > _low[j] - labelClearance && height < _high[j] + labelClearance)
                    {
                        reach = Mathf.Max(reach, _reach[j]);
                    }
                }

                Vector3 position = centre + outward * (reach + labelGap);
                position.y = height;
                _entries[i].Label.Place(position, side, _point[i], _head.position);
            }
        }

        // The part's renderers as one box in its slot's space, measured while the part is at rest.
        private static Bounds ShapeOf(ProductView.Part part)
        {
            Matrix4x4 toSlot = part.slot.worldToLocalMatrix;
            var shape = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;

            Renderer[] renderers = part.renderers;
            for (int r = 0; r < renderers.Length; r++)
            {
                if (renderers[r] == null)
                {
                    continue;
                }

                Bounds local = renderers[r].localBounds;
                Matrix4x4 matrix = toSlot * renderers[r].localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    var sign = new Vector3(
                        (corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f);
                    Vector3 point = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, sign));
                    if (any)
                    {
                        shape.Encapsulate(point);
                    }
                    else
                    {
                        shape = new Bounds(point, Vector3.zero);
                        any = true;
                    }
                }
            }

            return shape;
        }

        // Two hands on the product, then further apart. Measured from the closest the hands have been
        // during this hold, so adjusting your grip first doesn't count against you.
        private void WatchForPull()
        {
            GrabTarget grab = _view.Grab;
            if (!GestureEnabled || grab == null || !grab.IsHeld || grab.HoldPoints < 2 || grab.HoldSpread <= 0f)
            {
                _pulling = false;
                return;
            }

            if (!_pulling)
            {
                _pulling = true;
                _pullStart = grab.HoldSpread;
                return;
            }

            _pullStart = Mathf.Min(_pullStart, grab.HoldSpread);
            float needed = tuning != null ? tuning.explodePullDistance : 0.25f;
            if (grab.HoldSpread - _pullStart >= needed)
            {
                Explode(ExplodeTrigger.Gesture);
            }
        }

        // The reverse: two parts held, hands brought together.
        private void WatchForPush()
        {
            Transform first = null;
            Transform second = null;
            for (int i = 0; i < _entries.Length; i++)
            {
                GrabTarget grab = _entries[i].Part.grab;
                if (grab == null || !grab.IsHeld)
                {
                    continue;
                }

                if (first == null)
                {
                    first = _entries[i].Part.body;
                }
                else
                {
                    second = _entries[i].Part.body;
                    break;
                }
            }

            if (second == null)
            {
                _pushing = false;
                return;
            }

            float distance = Vector3.Distance(first.position, second.position);
            if (!_pushing)
            {
                _pushing = true;
                _pushStart = distance;
                return;
            }

            _pushStart = Mathf.Max(_pushStart, distance);
            float needed = tuning != null ? tuning.reassemblePushDistance : 0.15f;
            if (_pushStart - distance >= needed)
            {
                Reassemble(ExplodeTrigger.Gesture);
            }
        }
    }
}
