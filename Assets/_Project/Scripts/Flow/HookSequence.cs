using System;
using System.Collections;
using UnityEngine;

namespace MRShowroom
{
    /// <summary>
    /// The hook: the branded box drops onto the surface and bounces, its lid pops off, and the
    /// product rises out of it, spinning under a sweep of light, then settles onto the surface.
    /// The room dims a little while this plays so the product is the brightest thing in view.
    /// Touching the box at any point skips to the product.
    /// </summary>
    public sealed class HookSequence : MonoBehaviour
    {
        [SerializeField] private ProductController product;
        [SerializeField] private ProductStage stage;

        [Header("Box")]
        [SerializeField] private float dropHeight = 0.55f;
        [SerializeField] private float dropTime = 0.95f;
        [SerializeField] private float pauseBeforeOpen = 0.45f;
        [SerializeField] private float lidTime = 0.6f;

        [Header("Product")]
        [SerializeField] private float riseTime = 1.2f;
        [SerializeField] private float riseHeight = 0.2f;
        [SerializeField] private float hoverTime = 0.7f;
        [SerializeField] private float settleTime = 0.75f;

        [Header("Room")]
        [Tooltip("How far the room is dimmed while the box opens (0-1).")]
        [SerializeField] [Range(0f, 1f)] private float spotlight = 0.6f;
        [SerializeField] private float spotlightFade = 0.5f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private PackagingBox _box;
        private MaterialPropertyBlock _block;
        private Coroutine _routine;
        private Color _glowColor = Color.white;
        private float _spotlightNow;
        private bool _thudPlayed;

        public event Action Finished;

        public bool IsPlaying => _routine != null;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
        }

        private void OnDisable()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
                Finish(false);
            }
        }

        public void Play()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
            }

            _routine = StartCoroutine(Run());
        }

        /// <summary>Jumps to the end: the box is gone and the product is resting on the surface.</summary>
        public void Skip()
        {
            if (_routine == null)
            {
                return;
            }

            StopCoroutine(_routine);
            _routine = null;
            Finish(true);
        }

        private IEnumerator Run()
        {
            ProductView view = product.View;
            Transform item = view.transform;

            product.SetInteractable(false);
            product.SetVisible(false);
            if (view.Rest != null)
            {
                view.Rest.enabled = false;
            }

            PackagingBox box = PrepareBox();
            float timer;

            if (box != null)
            {
                // Drop and bounce.
                _thudPlayed = false;
                Transform crate = box.transform;
                for (timer = 0f; timer < dropTime; timer += Time.deltaTime)
                {
                    float t = timer / dropTime;
                    float fall = Ease.OutBounce(t);
                    crate.localPosition = new Vector3(0f, dropHeight * (1f - fall), 0f);
                    crate.localScale = Vector3.one * Mathf.Min(t * 6f, 1f);
                    SetSpotlight(Mathf.Min(timer / spotlightFade, 1f) * spotlight);
                    SetGlow(box, 0.6f + 0.4f * fall);

                    // First contact is where the bounce curve first reaches the ground.
                    if (!_thudPlayed && t >= 1f / 2.75f)
                    {
                        _thudPlayed = true;
                        ShowroomEvents.RaiseSfx(Sfx.BoxThud, crate.position);
                    }

                    yield return null;
                }

                crate.localPosition = Vector3.zero;
                crate.localScale = Vector3.one;
                SetSpotlight(spotlight);

                for (timer = 0f; timer < pauseBeforeOpen; timer += Time.deltaTime)
                {
                    SetGlow(box, 1f + 0.25f * Mathf.Sin(timer * 14f));
                    yield return null;
                }

                // Lid pops up and tumbles away behind the box.
                ShowroomEvents.RaiseSfx(Sfx.LidPop, crate.position);
                Transform lid = box.Lid;
                Vector3 lidStart = lid.localPosition;
                Quaternion lidStartRotation = lid.localRotation;
                for (timer = 0f; timer < lidTime; timer += Time.deltaTime)
                {
                    float t = timer / lidTime;
                    float arc = Ease.OutCubic(t);
                    lid.localPosition = lidStart + new Vector3(0f, 0.32f * arc - 0.1f * t * t, 0.3f * arc);
                    lid.localRotation = lidStartRotation * Quaternion.Euler(-150f * arc, 0f, 12f * arc);
                    lid.localScale = Vector3.one * (1f - Ease.InCubic(t));
                    SetGlow(box, 1.6f - t);
                    yield return null;
                }

                lid.gameObject.SetActive(false);
                lid.localPosition = lidStart;
                lid.localRotation = lidStartRotation;
                lid.localScale = Vector3.one;
            }

            // The product rises out, turning once, as the box sinks away beneath it.
            product.SetVisible(true);
            product.Colors.PlaySheen(riseTime);
            for (timer = 0f; timer < riseTime; timer += Time.deltaTime)
            {
                float t = timer / riseTime;
                item.localPosition = new Vector3(0f, Mathf.Lerp(0.02f, riseHeight, Ease.OutCubic(t)), 0f);
                item.localRotation = Quaternion.Euler(0f, -360f * Ease.InOutSine(t), 0f);
                item.localScale = Vector3.one * Mathf.Lerp(0.55f, 1f, Ease.OutBack(Mathf.Min(t * 1.6f, 1f)));

                if (box != null)
                {
                    float sink = Mathf.Clamp01(t / 0.6f);
                    box.Body.localScale = Vector3.one * (1f - Ease.InCubic(sink));
                    SetGlow(box, 1f - sink);
                }

                yield return null;
            }

            if (box != null)
            {
                box.gameObject.SetActive(false);
            }

            item.localRotation = Quaternion.identity;
            item.localScale = Vector3.one;

            // Hang in the air for a beat with a slow bob, then come down.
            for (timer = 0f; timer < hoverTime; timer += Time.deltaTime)
            {
                item.localPosition = new Vector3(0f, riseHeight + 0.008f * Mathf.Sin(timer * 6f), 0f);
                yield return null;
            }

            Vector3 hoverPosition = item.localPosition;
            for (timer = 0f; timer < settleTime; timer += Time.deltaTime)
            {
                float t = timer / settleTime;
                item.localPosition = Vector3.Lerp(hoverPosition, Vector3.zero, Ease.InOutSine(t));
                SetSpotlight(spotlight * (1f - t));
                yield return null;
            }

            _routine = null;
            Finish(true);
        }

        private PackagingBox PrepareBox()
        {
            PackagingBox prefab = product.Definition.packaging;
            if (prefab == null)
            {
                return null;
            }

            if (_box == null || _box.name != prefab.name)
            {
                if (_box != null)
                {
                    Destroy(_box.gameObject);
                }

                _box = Instantiate(prefab, stage.RestPoint);
                _box.name = prefab.name;
                if (_box.Grab != null)
                {
                    _box.Grab.HoldBegan += HandleBoxTouched;
                }

                if (_box.Glow != null && _box.Glow.sharedMaterial != null
                                      && _box.Glow.sharedMaterial.HasProperty(BaseColorId))
                {
                    _glowColor = _box.Glow.sharedMaterial.GetColor(BaseColorId);
                }
            }

            _box.gameObject.SetActive(true);
            _box.transform.localPosition = new Vector3(0f, dropHeight, 0f);
            _box.transform.localRotation = Quaternion.identity;
            _box.transform.localScale = Vector3.zero;
            _box.Body.localScale = Vector3.one;
            _box.Lid.gameObject.SetActive(true);
            _box.Lid.localScale = Vector3.one;
            if (_box.Grab != null)
            {
                _box.Grab.IsGrabbable = true;
            }

            return _box;
        }

        private void HandleBoxTouched(GrabTarget box)
        {
            Skip();
        }

        private void Finish(bool announce)
        {
            if (_box != null)
            {
                if (_box.Grab != null)
                {
                    _box.Grab.IsGrabbable = false;
                }

                _box.gameObject.SetActive(false);
            }

            SetSpotlight(0f);

            ProductView view = product.View;
            if (view != null)
            {
                view.transform.localPosition = Vector3.zero;
                view.transform.localRotation = Quaternion.identity;
                view.transform.localScale = Vector3.one;
                if (view.Rest != null)
                {
                    view.Rest.enabled = true;
                    view.Rest.SnapToRest();
                }
            }

            product.SetVisible(true);
            if (announce)
            {
                Finished?.Invoke();
            }
        }

        private void SetSpotlight(float amount)
        {
            if (Mathf.Approximately(amount, _spotlightNow))
            {
                return;
            }

            _spotlightNow = amount;
            ShowroomEvents.RaiseSpotlight(amount);
        }

        private void SetGlow(PackagingBox box, float intensity)
        {
            if (box.Glow == null)
            {
                return;
            }

            Color color = _glowColor;
            color.a = Mathf.Clamp01(color.a * Mathf.Max(intensity, 0f));
            _block.SetColor(BaseColorId, color);
            box.Glow.SetPropertyBlock(_block);
        }
    }
}
