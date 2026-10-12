using System.Collections.Generic;
using UnityEngine;

namespace MRShowroom
{
    /// <summary>A table top as placement sees it: a rectangle lying flat somewhere in the room.</summary>
    public struct TableCandidate
    {
        /// <summary>Centre of the top surface (world).</summary>
        public Vector3 center;
        /// <summary>The top's two edge directions (world, horizontal, unit length).</summary>
        public Vector3 axisX;
        public Vector3 axisZ;
        /// <summary>Half the top's size along each of those directions (m).</summary>
        public Vector2 halfExtents;
        /// <summary>Height of the top above the floor (m).</summary>
        public float height;
    }

    /// <summary>
    /// The geometry of choosing a spot. No SDK types in here, so the rules can be tested without a
    /// headset or a scanned room.
    /// </summary>
    public static class SurfaceMath
    {
        /// <summary>
        /// Picks the table to use: the one nearest the user (measured to the table's nearest edge, so
        /// a long table you are standing at beats a small one across the room), among those at a
        /// usable height and within reach. Returns -1 when none qualifies.
        /// </summary>
        public static int ChooseTable(IReadOnlyList<TableCandidate> tables, Vector3 head, float minHeight,
            float maxHeight, float maxDistance)
        {
            int best = -1;
            float bestDistance = maxDistance;
            for (int i = 0; i < tables.Count; i++)
            {
                TableCandidate table = tables[i];
                if (table.height < minHeight || table.height > maxHeight)
                {
                    continue;
                }

                float distance = DistanceToTop(table, head);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>Horizontal distance from a point to the nearest point of the table top. Zero above the table.</summary>
        public static float DistanceToTop(TableCandidate table, Vector3 point)
        {
            Vector3 offset = point - table.center;
            float x = Mathf.Max(Mathf.Abs(Vector3.Dot(offset, table.axisX)) - table.halfExtents.x, 0f);
            float z = Mathf.Max(Mathf.Abs(Vector3.Dot(offset, table.axisZ)) - table.halfExtents.y, 0f);
            return Mathf.Sqrt(x * x + z * z);
        }

        /// <summary>
        /// Where on the table the product goes, and which way it faces. On a small table that is the
        /// centre. On a big one it is a comfortable reach in front of the user, kept clear of the edge.
        /// The pose's forward points away from the user, squared up with the table when it is close to square already.
        /// </summary>
        /// <param name="clearance">Distance from the chosen point to the nearest table edge (m).</param>
        public static Pose PlaceOnTable(TableCandidate table, Vector3 head, float preferredReach, float edgeMargin,
            out float clearance)
        {
            Vector3 offset = head - table.center;
            var user = new Vector2(Vector3.Dot(offset, table.axisX), Vector3.Dot(offset, table.axisZ));

            // Aim from the user towards the middle of the table, stopping at arm's length.
            Vector2 point = Vector2.zero;
            float toCentre = user.magnitude;
            if (toCentre > preferredReach)
            {
                point = user - user / toCentre * preferredReach;
            }

            // Keep the whole product on the table. A table too small for the margin gets its centre.
            float limitX = Mathf.Max(table.halfExtents.x - edgeMargin, 0f);
            float limitZ = Mathf.Max(table.halfExtents.y - edgeMargin, 0f);
            point.x = Mathf.Clamp(point.x, -limitX, limitX);
            point.y = Mathf.Clamp(point.y, -limitZ, limitZ);

            clearance = Mathf.Max(Mathf.Min(table.halfExtents.x - Mathf.Abs(point.x),
                table.halfExtents.y - Mathf.Abs(point.y)), 0f);

            Vector3 position = table.center + table.axisX * point.x + table.axisZ * point.y;
            Vector3 away = position - head;
            away.y = 0f;
            away = away.sqrMagnitude < 0.0001f ? table.axisZ : away.normalized;

            return new Pose(position, Quaternion.LookRotation(SquareUp(away, table), Vector3.up));
        }

        /// <summary>A spot on the floor straight ahead of the user, facing away from them.</summary>
        public static Pose InFront(Vector3 head, Vector3 headForward, float distance, float floorY)
        {
            Vector3 forward = FlatForward(headForward);
            var position = new Vector3(head.x + forward.x * distance, floorY, head.z + forward.z * distance);
            return new Pose(position, Quaternion.LookRotation(forward, Vector3.up));
        }

        /// <summary>Pedestal height that puts the product at about waist level for this user.</summary>
        public static float PedestalHeight(float eyeHeight, float waistRatio, float min, float max)
        {
            return Mathf.Clamp(eyeHeight * waistRatio, min, max);
        }

        /// <summary>A pose at a point, facing away from the user.</summary>
        public static Pose FacingAwayFrom(Vector3 head, Vector3 point)
        {
            Vector3 away = point - head;
            away.y = 0f;
            away = away.sqrMagnitude < 0.0001f ? Vector3.forward : away.normalized;
            return new Pose(point, Quaternion.LookRotation(away, Vector3.up));
        }

        public static Vector3 FlatForward(Vector3 forward)
        {
            forward.y = 0f;
            return forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
        }

        // If the user is facing the table nearly square on, line the product up with the table's edge:
        // it reads as placed with care. Otherwise face it at the user.
        private static Vector3 SquareUp(Vector3 away, TableCandidate table)
        {
            const float snapCosine = 0.906f; // 25 degrees

            Vector3 best = away;
            float bestDot = snapCosine;
            for (int i = 0; i < 4; i++)
            {
                Vector3 axis = i < 2 ? table.axisX : table.axisZ;
                if (i % 2 == 1)
                {
                    axis = -axis;
                }

                float dot = Vector3.Dot(away, axis);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = axis;
                }
            }

            return best;
        }
    }
}
