using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MRShowroom.Tests
{
    /// <summary>The rules for choosing a table and a spot on it, checked without a room or a headset.</summary>
    public sealed class SurfaceMathTests
    {
        private const float MinHeight = 0.6f;
        private const float MaxHeight = 1.1f;
        private const float MaxDistance = 2f;
        private const float Reach = 0.45f;
        private const float Margin = 0.18f;

        private static TableCandidate Table(Vector3 centre, float width, float depth)
        {
            return new TableCandidate
            {
                center = centre,
                axisX = Vector3.right,
                axisZ = Vector3.forward,
                halfExtents = new Vector2(width * 0.5f, depth * 0.5f),
                height = centre.y
            };
        }

        [Test]
        public void ChooseTable_PicksTheNearest()
        {
            var tables = new List<TableCandidate>
            {
                Table(new Vector3(0f, 0.75f, 1.6f), 1f, 0.6f),
                Table(new Vector3(0.2f, 0.75f, 0.7f), 1f, 0.6f)
            };

            Assert.AreEqual(1, SurfaceMath.ChooseTable(tables, new Vector3(0f, 1.6f, 0f), MinHeight, MaxHeight, MaxDistance));
        }

        [Test]
        public void ChooseTable_MeasuresToTheEdge_NotTheCentre()
        {
            // A long table the user is standing at, and a small one whose centre happens to be nearer.
            var tables = new List<TableCandidate>
            {
                Table(new Vector3(0f, 0.75f, 1.3f), 0.5f, 0.5f),
                Table(new Vector3(0f, 0.75f, 1.6f), 1.2f, 2.8f)
            };

            Assert.AreEqual(1, SurfaceMath.ChooseTable(tables, new Vector3(0f, 1.6f, 0f), MinHeight, MaxHeight, MaxDistance));
        }

        [Test]
        public void ChooseTable_IgnoresWrongHeightsAndFarTables()
        {
            var tables = new List<TableCandidate>
            {
                Table(new Vector3(0f, 0.35f, 0.6f), 1f, 0.6f),  // coffee table: too low
                Table(new Vector3(0f, 1.4f, 0.6f), 1f, 0.6f),   // shelf: too high
                Table(new Vector3(0f, 0.75f, 4f), 1f, 0.6f)     // right height, other side of the room
            };

            Assert.AreEqual(-1, SurfaceMath.ChooseTable(tables, new Vector3(0f, 1.6f, 0f), MinHeight, MaxHeight, MaxDistance));
        }

        [Test]
        public void PlaceOnTable_UsesTheCentreOfASmallTable()
        {
            TableCandidate table = Table(new Vector3(0f, 0.75f, 0.5f), 0.6f, 0.5f);
            Pose pose = SurfaceMath.PlaceOnTable(table, new Vector3(0f, 1.2f, 0.1f), Reach, Margin, out float clearance);

            Assert.That(Vector3.Distance(pose.position, table.center), Is.LessThan(0.001f));
            Assert.That(clearance, Is.EqualTo(0.25f).Within(0.001f));
        }

        [Test]
        public void PlaceOnTable_StaysWithinReachOnABigTable()
        {
            // Sitting at one end of a three-metre table.
            TableCandidate table = Table(new Vector3(0f, 0.75f, 1.7f), 1.2f, 3f);
            var head = new Vector3(0f, 1.2f, 0f);
            Pose pose = SurfaceMath.PlaceOnTable(table, head, Reach, Margin, out float clearance);

            float fromUser = Vector3.Distance(new Vector3(pose.position.x, 0f, pose.position.z), new Vector3(head.x, 0f, head.z));
            Assert.That(fromUser, Is.LessThan(0.7f), "The product should be within arm's reach.");
            Assert.That(pose.position.y, Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(clearance, Is.GreaterThanOrEqualTo(Margin - 0.001f), "The product should be clear of the edge.");
        }

        [Test]
        public void PlaceOnTable_FacesAwayFromTheUser()
        {
            TableCandidate table = Table(new Vector3(0f, 0.75f, 0.6f), 1.4f, 0.7f);
            Pose pose = SurfaceMath.PlaceOnTable(table, new Vector3(0.1f, 1.2f, 0f), Reach, Margin, out _);

            // Nearly square on to the table, so it lines up with the table's edge.
            Assert.That(Vector3.Dot(pose.forward, Vector3.forward), Is.GreaterThan(0.999f));
        }

        [Test]
        public void PlaceOnTable_FacesTheUserWhenTheyStandAtAnAngle()
        {
            TableCandidate table = Table(new Vector3(0f, 0.75f, 0f), 1.2f, 1.2f);
            var head = new Vector3(-1.2f, 1.6f, -1.2f);
            Pose pose = SurfaceMath.PlaceOnTable(table, head, Reach, Margin, out _);

            Vector3 away = pose.position - head;
            away.y = 0f;
            Assert.That(Vector3.Dot(pose.forward, away.normalized), Is.GreaterThan(0.999f));
        }

        [Test]
        public void InFront_PutsTheSpotOnTheFloorAhead()
        {
            Pose pose = SurfaceMath.InFront(new Vector3(1f, 1.6f, 2f), new Vector3(0f, -0.5f, 1f), 0.6f, 0f);

            Assert.That(Vector3.Distance(pose.position, new Vector3(1f, 0f, 2.6f)), Is.LessThan(0.001f));
        }

        [Test]
        public void PedestalHeight_IsWaistHigh_WithinLimits()
        {
            Assert.That(SurfaceMath.PedestalHeight(1.6f, 0.58f, 0.55f, 1f), Is.EqualTo(0.928f).Within(0.001f));
            Assert.That(SurfaceMath.PedestalHeight(1.1f, 0.58f, 0.55f, 1f), Is.EqualTo(0.638f).Within(0.001f));
            Assert.That(SurfaceMath.PedestalHeight(0.4f, 0.58f, 0.55f, 1f), Is.EqualTo(0.55f).Within(0.001f));
        }
    }
}
