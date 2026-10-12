using System;
using System.Collections.Generic;
using UnityEngine;

namespace MRShowroom
{
    /// <summary>
    /// Recolours the product. Each colour slot maps to a region of the tint mask; a change doesn't
    /// snap, it sweeps across the product as a moving front that the Product Lit shader draws
    /// between the slot's "from" and "to" colours. Also handles finishes, the brand's preset
    /// colourways and Randomise.
    /// </summary>
    public sealed class ColorCustomizer : MonoBehaviour
    {
        [SerializeField] private ShowroomTuning tuning;

        private static readonly int WorldToProductId = Shader.PropertyToID("_ProductWorldToLocal");
        private static readonly int SweepAxisId = Shader.PropertyToID("_SweepAxis");
        private static readonly int SweepRangeId = Shader.PropertyToID("_SweepRange");
        private static readonly int SheenPositionId = Shader.PropertyToID("_SheenPosition");

        private struct Slot
        {
            public int ColorIndex;
            public Color From;
            public Color To;
            // x = metallic, y = smoothness
            public Vector4 SurfaceFrom;
            public Vector4 SurfaceTo;
            public float Sweep;
            public bool Sweeping;
            public int FromId;
            public int ToId;
            public int SweepId;
            public int SurfaceFromId;
            public int SurfaceToId;
        }

        private readonly List<Material> _sources = new List<Material>(4);
        private readonly List<Material> _materials = new List<Material>(4);
        private readonly Vector3[] _corners = new Vector3[8];

        private ProductDefinition _definition;
        private ProductView _view;
        private Slot[] _slots = new Slot[0];
        private int _finish;
        private float _sheenTime = -1f;
        private float _sheenDuration = 1f;

        /// <summary>Raised whenever the look changes, however it changed.</summary>
        public event Action Changed;

        public int SlotCount => _slots.Length;
        public int FinishIndex => _finish;

        public void Initialise(ProductDefinition definition, ProductView view)
        {
            ReleaseMaterials();
            _definition = definition;
            _view = view;

            CreateMaterialInstances();

            _slots = new Slot[definition.colorSlots.Length];
            for (int i = 0; i < _slots.Length; i++)
            {
                string property = definition.colorSlots[i].shaderProperty;
                _slots[i].FromId = Shader.PropertyToID(property + "From");
                _slots[i].ToId = Shader.PropertyToID(property + "To");
                _slots[i].SweepId = Shader.PropertyToID(property + "Sweep");
                _slots[i].SurfaceFromId = Shader.PropertyToID(property + "SurfaceFrom");
                _slots[i].SurfaceToId = Shader.PropertyToID(property + "SurfaceTo");
            }

            Vector3 axis = definition.sweepAxis.sqrMagnitude > 0.0001f ? definition.sweepAxis.normalized : Vector3.right;
            for (int i = 0; i < _materials.Count; i++)
            {
                _materials[i].SetVector(SweepAxisId, axis);
                _materials[i].SetFloat(SheenPositionId, -1f);
            }

            ResetToDefault();
        }

        public int ColorIndex(int slot)
        {
            return slot >= 0 && slot < _slots.Length ? _slots[slot].ColorIndex : 0;
        }

        public Color CurrentColor(int slot)
        {
            return slot >= 0 && slot < _slots.Length ? _slots[slot].To : Color.white;
        }

        /// <summary>The preset the product currently matches, or -1 for a custom mix.</summary>
        public int MatchingPreset()
        {
            Colorway[] presets = _definition.presets;
            for (int p = 0; p < presets.Length; p++)
            {
                Colorway preset = presets[p];
                bool match = preset.finishIndex == _finish || _definition.finishes.Length == 0;
                for (int s = 0; match && s < _slots.Length; s++)
                {
                    match = s < preset.colorIndices.Length && preset.colorIndices[s] == _slots[s].ColorIndex;
                }

                if (match)
                {
                    return p;
                }
            }

            return -1;
        }

        public string ColorwayName()
        {
            int preset = MatchingPreset();
            return preset >= 0 ? _definition.presets[preset].name : "Custom";
        }

        public void SetColor(int slot, int colorIndex, bool animate = true)
        {
            if (slot < 0 || slot >= _slots.Length)
            {
                return;
            }

            ColorSlot data = _definition.colorSlots[slot];
            if (colorIndex < 0 || colorIndex >= data.colors.Length)
            {
                return;
            }

            bool changed = _slots[slot].ColorIndex != colorIndex;
            _slots[slot].ColorIndex = colorIndex;
            Begin(slot, data.colors[colorIndex].color, _slots[slot].SurfaceTo, animate && changed);

            if (changed)
            {
                ShowroomEvents.RaiseColorChanged(slot, colorIndex);
                if (animate)
                {
                    ShowroomEvents.RaiseSfx(Sfx.Sweep, _view.transform.position);
                }

                Changed?.Invoke();
            }
        }

        public void SetFinish(int finishIndex, bool animate = true)
        {
            FinishOption[] finishes = _definition.finishes;
            int slot = _definition.finishSlot;
            if (finishIndex < 0 || finishIndex >= finishes.Length || slot < 0 || slot >= _slots.Length)
            {
                return;
            }

            bool changed = _finish != finishIndex;
            _finish = finishIndex;
            FinishOption finish = finishes[finishIndex];
            Begin(slot, _slots[slot].To, new Vector4(finish.metallic, finish.smoothness, 0f, 0f), animate && changed);

            if (changed)
            {
                ShowroomEvents.RaiseFinishChanged(finishIndex);
                if (animate)
                {
                    ShowroomEvents.RaiseSfx(Sfx.Sweep, _view.transform.position);
                }

                Changed?.Invoke();
            }
        }

        public void ApplyPreset(int presetIndex, bool animate = true)
        {
            if (presetIndex < 0 || presetIndex >= _definition.presets.Length)
            {
                return;
            }

            Colorway preset = _definition.presets[presetIndex];
            for (int s = 0; s < _slots.Length && s < preset.colorIndices.Length; s++)
            {
                SetColor(s, preset.colorIndices[s], animate);
            }

            SetFinish(preset.finishIndex, animate);
        }

        public void Randomise()
        {
            for (int s = 0; s < _slots.Length; s++)
            {
                int count = _definition.colorSlots[s].colors.Length;
                if (count < 2)
                {
                    continue;
                }

                // Step by a random non-zero amount so every slot is guaranteed to change.
                int next = (_slots[s].ColorIndex + UnityEngine.Random.Range(1, count)) % count;
                SetColor(s, next);
            }
        }

        /// <summary>Back to the first preset (or each slot's first colour), without animation.</summary>
        public void ResetToDefault()
        {
            Colorway preset = _definition.presets.Length > 0 ? _definition.presets[0] : null;

            _finish = preset != null ? Mathf.Clamp(preset.finishIndex, 0, Mathf.Max(_definition.finishes.Length - 1, 0)) : 0;
            for (int s = 0; s < _slots.Length; s++)
            {
                ColorSlot data = _definition.colorSlots[s];
                int index = preset != null && s < preset.colorIndices.Length ? preset.colorIndices[s] : 0;
                index = Mathf.Clamp(index, 0, Mathf.Max(data.colors.Length - 1, 0));
                _slots[s].ColorIndex = index;

                var surface = new Vector4(data.metallic, data.smoothness, 0f, 0f);
                if (s == _definition.finishSlot && _definition.finishes.Length > 0)
                {
                    FinishOption finish = _definition.finishes[_finish];
                    surface = new Vector4(finish.metallic, finish.smoothness, 0f, 0f);
                }

                Color color = data.colors.Length > 0 ? data.colors[index].color : Color.white;
                Begin(s, color, surface, false);
            }

            Changed?.Invoke();
        }

        /// <summary>One bright band travelling across the product: the light sweep in the hook.</summary>
        public void PlaySheen(float duration)
        {
            UpdateSweepRange();
            _sheenDuration = Mathf.Max(duration, 0.05f);
            _sheenTime = 0f;
        }

        private void Begin(int slot, Color color, Vector4 surface, bool animate)
        {
            ref Slot state = ref _slots[slot];

            // A change that arrives mid-sweep finishes the old one first: the front can only blend two looks.
            state.From = state.To;
            state.SurfaceFrom = state.SurfaceTo;
            state.To = color;
            state.SurfaceTo = surface;

            if (animate)
            {
                UpdateSweepRange();
                state.Sweep = 0f;
                state.Sweeping = true;
            }
            else
            {
                state.From = color;
                state.SurfaceFrom = surface;
                state.Sweep = 1f;
                state.Sweeping = false;
            }

            Push(slot);
        }

        private void Update()
        {
            if (_view == null)
            {
                return;
            }

            bool active = false;
            float step = Time.deltaTime / Mathf.Max(tuning != null ? tuning.sweepDuration : 0.4f, 0.01f);
            for (int s = 0; s < _slots.Length; s++)
            {
                if (!_slots[s].Sweeping)
                {
                    continue;
                }

                active = true;
                _slots[s].Sweep = Mathf.Min(_slots[s].Sweep + step, 1f);
                if (_slots[s].Sweep >= 1f)
                {
                    _slots[s].Sweeping = false;
                    _slots[s].From = _slots[s].To;
                    _slots[s].SurfaceFrom = _slots[s].SurfaceTo;
                }

                Push(s);
            }

            if (_sheenTime >= 0f)
            {
                active = true;
                _sheenTime += Time.deltaTime;
                float t = _sheenTime / _sheenDuration;
                // Runs from just before the product to just past it, so the band enters and leaves cleanly.
                float position = t >= 1f ? -1f : Mathf.Lerp(-0.25f, 1.25f, t);
                for (int i = 0; i < _materials.Count; i++)
                {
                    _materials[i].SetFloat(SheenPositionId, position);
                }

                if (t >= 1f)
                {
                    _sheenTime = -1f;
                }
            }

            if (active)
            {
                // The front is measured in the product's own space, so it stays put while the product is turned.
                Matrix4x4 worldToProduct = _view.transform.worldToLocalMatrix;
                for (int i = 0; i < _materials.Count; i++)
                {
                    _materials[i].SetMatrix(WorldToProductId, worldToProduct);
                }
            }
        }

        private void Push(int slot)
        {
            Slot state = _slots[slot];
            for (int i = 0; i < _materials.Count; i++)
            {
                Material material = _materials[i];
                material.SetColor(state.FromId, state.From);
                material.SetColor(state.ToId, state.To);
                material.SetFloat(state.SweepId, state.Sweep);
                material.SetVector(state.SurfaceFromId, state.SurfaceFrom);
                material.SetVector(state.SurfaceToId, state.SurfaceTo);
            }
        }

        // Where the product starts and ends along the sweep axis right now (it is longer when exploded).
        private void UpdateSweepRange()
        {
            Transform root = _view.transform;
            Vector3 axis = _definition.sweepAxis.sqrMagnitude > 0.0001f ? _definition.sweepAxis.normalized : Vector3.right;
            float min = float.MaxValue;
            float max = float.MinValue;

            Renderer[] renderers = _view.Renderers;
            for (int r = 0; r < renderers.Length; r++)
            {
                if (renderers[r] == null)
                {
                    continue;
                }

                Bounds bounds = renderers[r].bounds;
                Vector3 centre = bounds.center;
                Vector3 extents = bounds.extents;
                int corner = 0;
                for (int x = -1; x <= 1; x += 2)
                {
                    for (int y = -1; y <= 1; y += 2)
                    {
                        for (int z = -1; z <= 1; z += 2)
                        {
                            _corners[corner++] = centre + Vector3.Scale(extents, new Vector3(x, y, z));
                        }
                    }
                }

                for (int c = 0; c < 8; c++)
                {
                    float along = Vector3.Dot(root.InverseTransformPoint(_corners[c]), axis);
                    min = Mathf.Min(min, along);
                    max = Mathf.Max(max, along);
                }
            }

            if (max <= min)
            {
                min = 0f;
                max = 1f;
            }

            var range = new Vector4(min, 1f / (max - min), 0f, 0f);
            Matrix4x4 worldToProduct = root.worldToLocalMatrix;
            for (int i = 0; i < _materials.Count; i++)
            {
                _materials[i].SetVector(SweepRangeId, range);
                _materials[i].SetMatrix(WorldToProductId, worldToProduct);
            }
        }

        // The product's renderers share a few materials. Each gets one private copy, so a colour change is
        // a handful of property writes and the asset on disk is never touched.
        private void CreateMaterialInstances()
        {
            Renderer[] renderers = _view.Renderers;
            for (int r = 0; r < renderers.Length; r++)
            {
                if (renderers[r] == null)
                {
                    continue;
                }

                Material[] shared = renderers[r].sharedMaterials;
                for (int m = 0; m < shared.Length; m++)
                {
                    if (shared[m] == null)
                    {
                        continue;
                    }

                    int index = _sources.IndexOf(shared[m]);
                    if (index < 0)
                    {
                        index = _sources.Count;
                        _sources.Add(shared[m]);
                        _materials.Add(new Material(shared[m]) { name = shared[m].name + " (Instance)" });
                    }

                    shared[m] = _materials[index];
                }

                renderers[r].sharedMaterials = shared;
            }
        }

        private void ReleaseMaterials()
        {
            for (int i = 0; i < _materials.Count; i++)
            {
                if (_materials[i] != null)
                {
                    Destroy(_materials[i]);
                }
            }

            _materials.Clear();
            _sources.Clear();
        }

        private void OnDestroy()
        {
            ReleaseMaterials();
        }
    }
}
