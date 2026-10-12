using UnityEngine;

namespace MRShowroom.MR
{
    /// <summary>
    /// Aims the one directional light. It leans towards where the room's own light comes from (the
    /// window, when the room scan knows where one is) but never so far that the side of the product
    /// the user is looking at falls into shade: a product lit from behind looks dull, however true
    /// to the room it is. Kept soft, and never steep or shallow enough to look wrong in a room it
    /// can't actually see.
    /// </summary>
    public sealed class RoomLighting : MonoBehaviour
    {
        [SerializeField] private Light sun;
        [Tooltip("Height of the light above the horizon (deg).")]
        [SerializeField] private float pitch = 52f;
        [SerializeField] private float minPitch = 40f;
        [SerializeField] private float maxPitch = 68f;
        [Tooltip("With no window to go by, the light comes from over this shoulder: negative is the user's left (deg).")]
        [SerializeField] private float defaultSide = -35f;
        [Tooltip("The furthest round to the side the light may come from, measured from straight behind the user (deg).")]
        [SerializeField] private float maxSide = 65f;

        /// <summary>A key light from over the user's shoulder.</summary>
        public void UseDefault(Vector3 viewer, Vector3 target)
        {
            Aim(ViewDirection(viewer, target), defaultSide, pitch);
        }

        /// <summary>Light from the direction of <paramref name="source"/> (a window), as far as the user's view allows.</summary>
        public void AimFrom(Vector3 source, Vector3 target, Vector3 viewer)
        {
            Vector3 travel = target - source;
            var flat = new Vector3(travel.x, 0f, travel.z);
            if (flat.sqrMagnitude < 0.01f)
            {
                UseDefault(viewer, target);
                return;
            }

            Vector3 view = ViewDirection(viewer, target);
            // 0 = the light travels the way the user is looking (front lit); 180 = straight at them (back lit).
            float side = Vector3.SignedAngle(view, flat.normalized, Vector3.up);
            float height = Mathf.Atan2(-travel.y, flat.magnitude) * Mathf.Rad2Deg;
            Aim(view, Mathf.Clamp(side, -maxSide, maxSide), height);
        }

        private void Aim(Vector3 view, float side, float height)
        {
            if (sun == null)
            {
                return;
            }

            Vector3 travel = Quaternion.AngleAxis(side, Vector3.up) * view;
            float yaw = Mathf.Atan2(travel.x, travel.z) * Mathf.Rad2Deg;
            sun.transform.rotation = Quaternion.Euler(Mathf.Clamp(height, minPitch, maxPitch), yaw, 0f);
        }

        private static Vector3 ViewDirection(Vector3 viewer, Vector3 target)
        {
            Vector3 view = target - viewer;
            view.y = 0f;
            return view.sqrMagnitude < 0.0001f ? Vector3.forward : view.normalized;
        }
    }
}
