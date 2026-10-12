using UnityEngine;

namespace MRShowroom
{
    /// <summary>Measures a box that is given in one object's space along directions in the world.</summary>
    public static class BoxMath
    {
        /// <summary>Half the size of the box along <paramref name="direction"/>.</summary>
        public static float Span(Matrix4x4 localToWorld, Vector3 extents, Vector3 direction)
        {
            return Mathf.Abs(Vector3.Dot(direction, localToWorld.GetColumn(0))) * extents.x
                + Mathf.Abs(Vector3.Dot(direction, localToWorld.GetColumn(1))) * extents.y
                + Mathf.Abs(Vector3.Dot(direction, localToWorld.GetColumn(2))) * extents.z;
        }

        /// <summary>How far the box reaches along <paramref name="direction"/>, measured from a point in the world.</summary>
        public static float Reach(Matrix4x4 localToWorld, Bounds box, Vector3 from, Vector3 direction)
        {
            Vector3 centre = localToWorld.MultiplyPoint3x4(box.center);
            return Vector3.Dot(centre - from, direction) + Span(localToWorld, box.extents, direction);
        }
    }
}
