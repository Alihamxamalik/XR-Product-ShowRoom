using System;
using System.Collections;
using UnityEngine;

namespace MRShowroom
{
    /// <summary>
    /// The brand moment: the product walks a small loop across the surface on its own, leaving
    /// glowing footprints that fade behind it, and ends exactly where it started. The loop is
    /// sized to the room the surface has, so it never steps off a small table.
    /// Picking the product up mid-walk ends the walk.
    /// </summary>
    public sealed class RevealSequence : MonoBehaviour
    {
        [SerializeField] private ProductController product;
        [SerializeField] private ProductStage stage;
        [Tooltip("Flat quads under the rest point, reused for every walk.")]
        [SerializeField] private Renderer[] footprints = new Renderer[0];

        [Header("Walk")]
        [SerializeField] private float duration = 5.5f;
        [SerializeField] private int steps = 8;
        [SerializeField] private float stepHeight = 0.028f;
        [Tooltip("How far the product rocks from heel to toe on each step (deg).")]
        [SerializeField] private float rock = 9f;
        [SerializeField] private float minRadius = 0.045f;
        [SerializeField] private float maxRadius = 0.12f;
        [Tooltip("Loop radius when the size of the surface isn't known (m).")]
        [SerializeField] private float defaultRadius = 0.07f;

        [Header("Footprints")]
        [SerializeField] private float footprintLife = 2.6f;
        [SerializeField] private Color footprintColor = new Color(0.72f, 0.92f, 0.35f, 0.9f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        private MaterialPropertyBlock _block;
        private float[] _age = new float[0];
        private Coroutine _routine;
        private int _next;

        public event Action Finished;

        public bool IsPlaying => _routine != null;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _age = new float[footprints.Length];
            for (int i = 0; i < footprints.Length; i++)
            {
                _age[i] = -1f;
                footprints[i].enabled = false;
            }
        }

        private void OnDisable()
        {
            _routine = null;
        }

        public void Play()
        {
            Cancel();
            _routine = StartCoroutine(Run());
        }

        /// <summary>Stops the walk where it is. The product's own spring takes it home.</summary>
        public void Cancel()
        {
            if (_routine == null)
            {
                return;
            }

            StopCoroutine(_routine);
            _routine = null;
            Release();
        }

        private IEnumerator Run()
        {
            ProductView view = product.View;
            Transform item = view.transform;
            if (view.Rest != null)
            {
                view.Rest.enabled = false;
            }

            float radius = LoopRadius(item.lossyScale.x);
            int lastStep = 0;

            for (float timer = 0f; timer < duration; timer += Time.deltaTime)
            {
                if (view.Grab != null && view.Grab.IsHeld)
                {
                    break;
                }

                // Ease into and out of the walk so it doesn't start or stop dead.
                float progress = Mathf.SmoothStep(0f, 1f, timer / duration);
                float angle = progress * 2f * Mathf.PI;

                // A circle that starts at the rest point heading along the product's toe (+X), curving away
                // from the user and coming back round to where it began.
                var ground = new Vector3(Mathf.Sin(angle) * radius, 0f, (1f - Mathf.Cos(angle)) * radius);
                float stride = progress * steps;
                float within = stride - Mathf.Floor(stride);

                item.localPosition = ground + new Vector3(0f, stepHeight * Mathf.Sin(within * Mathf.PI), 0f);
                item.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f)
                                     * Quaternion.Euler(0f, 0f, rock * Mathf.Sin(within * 2f * Mathf.PI));

                int step = Mathf.FloorToInt(stride);
                if (step > lastStep && step < steps)
                {
                    lastStep = step;
                    Stamp(ground, -angle * Mathf.Rad2Deg, item.localScale.x);
                    ShowroomEvents.RaiseSfx(Sfx.Step, item.position);
                }

                yield return null;
            }

            _routine = null;
            Release();
            Finished?.Invoke();
        }

        private void Release()
        {
            ProductView view = product.View;
            if (view != null && view.Rest != null)
            {
                // Wherever the walk left it, the spring brings it back to the rest point.
                view.Rest.enabled = true;
            }
        }

        // The loop reaches two radii out from the rest point, so it has to fit inside the clearance
        // around it, with room left over for the product itself.
        private float LoopRadius(float productScale)
        {
            float clearance = stage.Surface.clearance;
            if (clearance <= 0f)
            {
                return defaultRadius;
            }

            float halfWidth = product.View.LocalBounds.extents.z * productScale;
            return Mathf.Clamp((clearance - halfWidth) * 0.5f, minRadius, maxRadius);
        }

        private void Stamp(Vector3 ground, float yaw, float scale)
        {
            if (footprints.Length == 0)
            {
                return;
            }

            Renderer print = footprints[_next];
            _age[_next] = 0f;
            _next = (_next + 1) % footprints.Length;

            ProductDefinition definition = product.Definition;
            Transform quad = print.transform;
            quad.localPosition = new Vector3(ground.x, 0.002f, ground.z);
            quad.localRotation = Quaternion.Euler(0f, yaw, 0f);
            quad.localScale = new Vector3(definition.footprintSize.x * scale, 1f, definition.footprintSize.y * scale);
            print.enabled = true;
        }

        private void Update()
        {
            ProductDefinition definition = product.Definition;
            for (int i = 0; i < footprints.Length; i++)
            {
                if (_age[i] < 0f)
                {
                    continue;
                }

                _age[i] += Time.deltaTime;
                float life = _age[i] / footprintLife;
                if (life >= 1f)
                {
                    _age[i] = -1f;
                    footprints[i].enabled = false;
                    continue;
                }

                // Flash in quickly, then a long fade.
                float alpha = Mathf.Min(life * 12f, 1f) * (1f - life) * (1f - life);
                Color color = footprintColor;
                color.a *= alpha;
                _block.SetColor(BaseColorId, color);
                if (definition != null && definition.footprint != null)
                {
                    _block.SetTexture(BaseMapId, definition.footprint.texture);
                }

                footprints[i].SetPropertyBlock(_block);
            }
        }
    }
}
