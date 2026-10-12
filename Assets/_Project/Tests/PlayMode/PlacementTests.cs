using System.Collections;
using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using MRShowroom.MR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MRShowroom.Tests
{
    /// <summary>
    /// The first three lines of the design document's test checklist, run against MRUK's sample
    /// rooms instead of real ones: the product lands on the right table in a room with one table,
    /// with several, and falls back to the floor in a room with none.
    /// </summary>
    public sealed class PlacementTests
    {
        private const string Rooms = "Packages/com.meta.xr.mrutilitykit/Core/Rooms/Json/";

        private SurfaceFinder _finder;
        private ShowroomTuning _tuning;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRig.BeginCapture();
            yield return SceneManager.LoadSceneAsync(AppBootstrap.ShowroomScene, LoadSceneMode.Single);
            yield return null;

            _finder = Object.FindFirstObjectByType<SurfaceFinder>();
            Assert.IsNotNull(_finder, "The Showroom scene has no SurfaceFinder.");
            var stage = Object.FindFirstObjectByType<ProductStage>();
            yield return TestRig.WaitUntil(() => stage.IsPlaced, 20f, "the first placement");

            _tuning = LoadTuning();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            TestRig.EndCapture();
            yield return null;
        }

        private static ShowroomTuning LoadTuning()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<ShowroomTuning>("Assets/_Project/Data/ShowroomTuning.asset");
#else
            return null;
#endif
        }

        private IEnumerator LoadRoom(string file)
        {
            string json = null;
#if UNITY_EDITOR
            json = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(Rooms + file)?.text;
#endif
            Assert.IsNotNull(json, "Sample room " + file + " not found.");
            yield return _finder.LoadFromJson(json);
            Assert.IsTrue(_finder.HasRoom, "The sample room should have loaded.");
        }

        private List<MRUKAnchor> TablesAtUsableHeight()
        {
            var tables = new List<MRUKAnchor>();
            foreach (MRUKAnchor anchor in _finder.Room.Anchors)
            {
                if (!anchor.HasAnyLabel(MRUKAnchor.SceneLabels.TABLE))
                {
                    continue;
                }

                float height = anchor.transform.position.y - _finder.FloorY;
                if (height >= _tuning.tableMinHeight && height <= _tuning.tableMaxHeight)
                {
                    tables.Add(anchor);
                }
            }

            return tables;
        }

        // Someone standing half a metre back from the middle of a table.
        private Vector3 HeadNear(MRUKAnchor table)
        {
            Vector3 top = table.transform.position;
            Vector3 towardsRoomCentre = _finder.Room.GetRoomBounds().center - top;
            towardsRoomCentre.y = 0f;
            Vector3 direction = towardsRoomCentre.sqrMagnitude > 0.01f ? towardsRoomCentre.normalized : Vector3.forward;
            return new Vector3(top.x, _finder.FloorY + 1.5f, top.z) + direction * 0.9f;
        }

        private void AssertOnTable(MRUKAnchor table, Vector3 head, Pose pose, SurfaceInfo surface)
        {
            Assert.AreEqual(PlacementMethod.Table, surface.method);
            Assert.AreEqual(0f, surface.pedestalHeight, "A real table needs no pedestal.");

            // On the table top: at its height, and inside its outline.
            Vector3 local = table.transform.InverseTransformPoint(pose.position);
            Assert.That(Mathf.Abs(local.z), Is.LessThan(0.02f), "The product should sit on the table top.");
            Assert.IsTrue(table.PlaneRect.HasValue || table.VolumeBounds.HasValue);
            if (table.PlaneRect.HasValue)
            {
                Rect rect = table.PlaneRect.Value;
                Assert.IsTrue(rect.Contains(new Vector2(local.x, local.y)), "The product should be inside the table's edge.");
            }

            // Within reach, and turned to face the user.
            Vector3 away = pose.position - head;
            away.y = 0f;
            Assert.That(away.magnitude, Is.LessThan(_tuning.maxTableDistance));
            Assert.That(Vector3.Dot(pose.forward, away.normalized), Is.GreaterThan(0.85f),
                "The product should face the user, give or take squaring up with the table.");
            Assert.That(Vector3.Dot(pose.up, Vector3.up), Is.GreaterThan(0.999f));
        }

        [UnityTest]
        public IEnumerator RoomWithOneTable_ProductLandsOnIt()
        {
            yield return LoadRoom("MeshOffice1.json");
            List<MRUKAnchor> tables = TablesAtUsableHeight();
            Assert.AreEqual(1, tables.Count, "This sample room is expected to have exactly one usable table.");

            Vector3 head = HeadNear(tables[0]);
            Assert.IsTrue(_finder.TryFindTable(head, out Pose pose, out SurfaceInfo surface));
            AssertOnTable(tables[0], head, pose, surface);
            TestRig.AssertNoProblems();
        }

        [UnityTest]
        public IEnumerator RoomWithSeveralTables_ProductLandsOnTheNearest()
        {
            yield return LoadRoom("MeshLivingRoom2.json");
            List<MRUKAnchor> tables = TablesAtUsableHeight();
            Assert.That(tables.Count, Is.GreaterThanOrEqualTo(2), "This sample room is expected to have several usable tables.");

            // Stand at each table in turn: the product must land on that one, not on another.
            foreach (MRUKAnchor table in tables)
            {
                Vector3 head = HeadNear(table);
                Assert.IsTrue(_finder.TryFindTable(head, out Pose pose, out SurfaceInfo surface),
                    "No table found from beside " + table.name);

                MRUKAnchor landedOn = null;
                float best = float.MaxValue;
                foreach (MRUKAnchor candidate in tables)
                {
                    float distance = Mathf.Abs(candidate.transform.InverseTransformPoint(pose.position).z)
                                     + candidate.GetDistanceToSurface(pose.position);
                    if (distance < best)
                    {
                        best = distance;
                        landedOn = candidate;
                    }
                }

                AssertOnTable(landedOn, head, pose, surface);
            }

            TestRig.AssertNoProblems();
        }

        [UnityTest]
        public IEnumerator RoomWithNoTable_ProductGoesOnAPedestalOnTheFloor()
        {
            yield return LoadRoom("MeshBedroom3.json");
            Assert.AreEqual(0, TablesAtUsableHeight().Count, "This sample room is expected to have no usable table.");

            Bounds room = _finder.Room.GetRoomBounds();
            var head = new Vector3(room.center.x, _finder.FloorY + 1.6f, room.center.z);
            Assert.IsFalse(_finder.TryFindTable(head, out _, out _));
            Assert.IsTrue(_finder.TryFindFloorSpot(head, Vector3.forward, out Pose pose, out SurfaceInfo surface),
                "There should be a free spot on the floor near the middle of the room.");

            Assert.AreEqual(PlacementMethod.Floor, surface.method);
            Assert.That(pose.position.y, Is.EqualTo(_finder.FloorY).Within(0.01f));
            Assert.That(surface.pedestalHeight, Is.InRange(_tuning.pedestalMinHeight, _tuning.pedestalMaxHeight));
            Assert.IsTrue(_finder.Room.IsPositionInRoom(pose.position + Vector3.up * 0.1f, false),
                "The pedestal must stand inside the room.");
            Assert.IsFalse(_finder.Room.IsPositionInSceneVolume(pose.position + Vector3.up * 0.3f, false, 0.1f),
                "The pedestal must not stand inside furniture.");
            TestRig.AssertNoProblems();
        }

        [UnityTest]
        public IEnumerator PointingAtTheFloor_FindsASurfaceFacingUp()
        {
            yield return LoadRoom("MeshOffice1.json");
            Bounds room = _finder.Room.GetRoomBounds();
            var origin = new Vector3(room.center.x, _finder.FloorY + 1.5f, room.center.z);

            Assert.IsTrue(_finder.Raycast(new Ray(origin, Vector3.down), 4f, out Vector3 point, out Vector3 normal),
                "A ray straight down should hit the floor or something standing on it.");
            Assert.That(point.y, Is.LessThan(origin.y));
            Assert.That(normal.y, Is.GreaterThan(0.7f));
            TestRig.AssertNoProblems();
        }
    }
}
