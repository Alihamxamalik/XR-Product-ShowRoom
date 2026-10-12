using UnityEngine;

namespace MRShowroom
{
    /// <summary>Easing curves for the code-driven animations. All take and return 0..1.</summary>
    public static class Ease
    {
        public static float OutCubic(float t)
        {
            t = 1f - Mathf.Clamp01(t);
            return 1f - t * t * t;
        }

        public static float InCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        public static float InQuad(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t;
        }

        public static float InOutSine(float t)
        {
            return 0.5f - 0.5f * Mathf.Cos(Mathf.Clamp01(t) * Mathf.PI);
        }

        /// <summary>Overshoots a little past 1 before settling.</summary>
        public static float OutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            t = Mathf.Clamp01(t) - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        /// <summary>A drop that bounces twice and settles.</summary>
        public static float OutBounce(float t)
        {
            const float n = 7.5625f;
            const float d = 2.75f;
            t = Mathf.Clamp01(t);
            if (t < 1f / d)
            {
                return n * t * t;
            }

            if (t < 2f / d)
            {
                t -= 1.5f / d;
                return n * t * t + 0.75f;
            }

            if (t < 2.5f / d)
            {
                t -= 2.25f / d;
                return n * t * t + 0.9375f;
            }

            t -= 2.625f / d;
            return n * t * t + 0.984375f;
        }

        /// <summary>Frame-rate independent approach factor: how far to move towards a target this frame.</summary>
        public static float Damp(float sharpness, float deltaTime)
        {
            return 1f - Mathf.Exp(-sharpness * deltaTime);
        }
    }
}
