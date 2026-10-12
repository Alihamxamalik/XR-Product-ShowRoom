using UnityEngine;

namespace MRShowroom
{
    /// <summary>
    /// A translucent hand used to demonstrate a gesture. It only knows how to pose itself: the
    /// fingers curl between open and closed, with the index finger controlled separately so it can
    /// grab or point. <see cref="GuideHints"/> moves it.
    ///
    /// Convention for whoever builds the prefab: the root sits at the palm, its +Z runs along the
    /// fingers and its +Y is the back of the hand. Each joint comes with the local axis it bends around.
    /// </summary>
    public sealed class GhostHand : MonoBehaviour
    {
        [SerializeField] private Renderer[] renderers = new Renderer[0];
        [Tooltip("Knuckles of the middle, ring and little fingers.")]
        [SerializeField] private Transform[] fingerJoints = new Transform[0];
        [SerializeField] private Vector3[] fingerAxes = new Vector3[0];
        [SerializeField] private Transform[] indexJoints = new Transform[0];
        [SerializeField] private Vector3[] indexAxes = new Vector3[0];
        [SerializeField] private Transform[] thumbJoints = new Transform[0];
        [SerializeField] private Vector3[] thumbAxes = new Vector3[0];
        [Tooltip("Bend of each finger joint when fully closed (deg).")]
        [SerializeField] private float fingerCurl = 52f;
        [SerializeField] private float thumbCurl = 24f;
        [SerializeField] [Range(0f, 1f)] private float maxAlpha = 0.6f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Quaternion[] _fingerOpen;
        private Quaternion[] _indexOpen;
        private Quaternion[] _thumbOpen;
        private MaterialPropertyBlock _block;
        private Color _color = Color.white;
        private float _alpha = -1f;

        private void Awake()
        {
            _fingerOpen = Capture(fingerJoints);
            _indexOpen = Capture(indexJoints);
            _thumbOpen = Capture(thumbJoints);
            _block = new MaterialPropertyBlock();
            if (renderers.Length > 0 && renderers[0] != null && renderers[0].sharedMaterial != null
                && renderers[0].sharedMaterial.HasProperty(BaseColorId))
            {
                _color = renderers[0].sharedMaterial.GetColor(BaseColorId);
            }

            SetAlpha(0f);
        }

        /// <param name="grip">0 = open hand, 1 = closed, for the middle, ring and little fingers and the thumb.</param>
        /// <param name="index">The same for the index finger. Leave at 0 with grip at 1 to point.</param>
        public void SetPose(float grip, float index)
        {
            Curl(fingerJoints, fingerAxes, _fingerOpen, fingerCurl * Mathf.Clamp01(grip));
            Curl(indexJoints, indexAxes, _indexOpen, fingerCurl * Mathf.Clamp01(index));
            Curl(thumbJoints, thumbAxes, _thumbOpen, thumbCurl * Mathf.Clamp01(grip));
        }

        public void SetAlpha(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            if (Mathf.Approximately(alpha, _alpha))
            {
                return;
            }

            _alpha = alpha;
            bool visible = alpha > 0.01f;
            Color color = _color;
            color.a = alpha * maxAlpha;
            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            _block.SetColor(BaseColorId, color);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                renderers[i].enabled = visible;
                if (visible)
                {
                    renderers[i].SetPropertyBlock(_block);
                }
            }
        }

        private static Quaternion[] Capture(Transform[] joints)
        {
            var rotations = new Quaternion[joints.Length];
            for (int i = 0; i < joints.Length; i++)
            {
                rotations[i] = joints[i] != null ? joints[i].localRotation : Quaternion.identity;
            }

            return rotations;
        }

        private static void Curl(Transform[] joints, Vector3[] axes, Quaternion[] open, float degrees)
        {
            if (open == null)
            {
                return;
            }

            for (int i = 0; i < joints.Length && i < axes.Length; i++)
            {
                if (joints[i] != null)
                {
                    joints[i].localRotation = open[i] * Quaternion.AngleAxis(degrees, axes[i]);
                }
            }
        }
    }
}
