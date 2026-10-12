using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MRShowroom.Tests
{
    /// <summary>
    /// Walks the real scenes from launch to "Play again" with no headset: Bootstrap, Showroom,
    /// placement, then every phase of the flow, driving the product the way a hand would (through
    /// its GrabTarget) and the panels the way a poke would (through their buttons). It can't check
    /// tracking or rendering, but it does catch broken wiring: missing references, components that
    /// throw, a flow that stalls.
    /// </summary>
    public sealed class ShowroomFlowTests
    {
        private ShowroomFlow _flow;
        private ProductController _product;
        private ProductStage _stage;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            TestRig.BeginCapture();
            yield return TestRig.Boot();

            _flow = Object.FindFirstObjectByType<ShowroomFlow>();
            _product = Object.FindFirstObjectByType<ProductController>();
            _stage = Object.FindFirstObjectByType<ProductStage>();
            Assert.IsNotNull(_flow, "The Showroom scene has no ShowroomFlow.");
            Assert.IsNotNull(_product, "The Showroom scene has no ProductController.");
            Assert.IsNotNull(_stage, "The Showroom scene has no ProductStage.");

            // The flow is paced for a person. Run it fast.
            Time.timeScale = 8f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            TestRig.EndCapture();
            yield return null;
        }

        private IEnumerator WaitForPhase(ShowroomPhase phase, float timeout)
        {
            yield return TestRig.WaitUntil(() => _flow.Phase == phase, timeout, "the " + phase + " phase");
        }

        [UnityTest]
        public IEnumerator Launch_PlacesTheProduct_AndPlaysTheHook()
        {
            yield return TestRig.WaitUntil(() => _stage.IsPlaced, 15f, "the product to be placed");
            Assert.IsNotNull(Camera.main, "The camera rig should provide the main camera.");

            // The scene's editor room is the sample office, which has one desk: with no input at all the
            // product should find it, the way it would find a real desk in a scanned room.
            Assert.AreEqual(PlacementMethod.Table, _stage.Surface.method);
            Assert.That(_stage.RestPoint.position.y, Is.InRange(0.6f, 1.1f), "The product should be at desk height.");
            Assert.That(Vector3.Dot(_stage.transform.up, Vector3.up), Is.GreaterThan(0.999f), "The stage should be level.");

            yield return WaitForPhase(ShowroomPhase.Inspect, 20f);

            // With no headset the editor's stand-in head should be at eye height, looking at the product.
            Transform head = Camera.main.transform;
            Vector3 toProduct = (_stage.RestPoint.position - head.position).normalized;
            Assert.That(head.position.y, Is.GreaterThan(1.2f), "The stand-in head should be at eye height.");
            Assert.That(Vector3.Dot(head.forward, toProduct), Is.GreaterThan(0.8f),
                "The stand-in head should be looking at the product.");

            ProductView view = _product.View;
            Assert.IsNotNull(view, "The product should have been built from its definition.");
            Assert.IsTrue(view.Grab.IsGrabbable, "After the hook the product can be picked up.");
            Assert.That(view.transform.localPosition.magnitude, Is.LessThan(0.001f), "The product rests on its spot.");
            Assert.That(view.Parts.Length, Is.GreaterThanOrEqualTo(5), "The product should be split into parts.");

            yield return TestRig.Frames(10);
            TestRig.AssertNoProblems();
        }

        [UnityTest]
        public IEnumerator GuidedFlow_RunsFromInspectToFreeExploration_AndRestarts()
        {
            yield return WaitForPhase(ShowroomPhase.Inspect, 30f);
            ProductView view = _product.View;

            // Inspect: pick it up, move it, let go. It springs back to its spot.
            view.Grab.BeginHold();
            view.transform.position += new Vector3(0.1f, 0.2f, -0.1f);
            yield return TestRig.Frames(5);
            view.Grab.EndHold();
            yield return TestRig.WaitUntil(() => view.Rest.IsSettled, 10f, "the product to return to rest");
            Assert.That(view.transform.localPosition.magnitude, Is.LessThan(0.002f));

            // Customise: two colour changes move the flow on.
            yield return WaitForPhase(ShowroomPhase.Customise, 20f);
            _product.Colors.SetColor(0, 2);
            _product.Colors.SetColor(1, 3);
            Assert.AreEqual(2, _product.Colors.ColorIndex(0));
            Assert.AreEqual(3, _product.Colors.ColorIndex(1));
            Assert.AreEqual("Custom", _product.Colors.ColorwayName());

            // Explode: press "See inside", as a poke would.
            yield return WaitForPhase(ShowroomPhase.Explode, 20f);
            GameObject.Find("ExplodeButton").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(_product.Explode.IsOpen);
            yield return TestRig.WaitUntil(() => !_product.Explode.IsMoving, 10f, "the parts to move out");
            Assert.IsFalse(view.Grab.IsGrabbable, "Apart, the product isn't grabbed as a whole...");
            Assert.IsTrue(view.Parts[1].grab.IsGrabbable, "...its parts are.");

            // A part can be lifted out and snaps back to its place.
            ProductView.Part part = view.Parts[1];
            part.grab.BeginHold();
            part.body.position += Vector3.right * 0.15f;
            yield return TestRig.Frames(3);
            part.grab.EndHold();
            yield return TestRig.WaitUntil(() => part.rest.IsSettled, 10f, "the part to snap back");
            Assert.That(part.body.localPosition.magnitude, Is.LessThan(0.002f));

            // Reveal, end card.
            yield return WaitForPhase(ShowroomPhase.EndCard, 40f);
            Assert.IsFalse(_product.Explode.IsOpen, "The product is back together for the end card.");

            GameObject.Find("BuyButton").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("ShareButton").GetComponent<Button>().onClick.Invoke();

            yield return WaitForPhase(ShowroomPhase.Free, 20f);

            // Play again: back to the hook, colours back to the launch colourway.
            GameObject.Find("PlayAgainButton").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(ShowroomPhase.Hook, _flow.Phase);
            Assert.AreEqual(0, _product.Colors.MatchingPreset());
            yield return WaitForPhase(ShowroomPhase.Inspect, 20f);

            yield return TestRig.Frames(10);
            TestRig.AssertNoProblems();
        }

        [UnityTest]
        public IEnumerator PullingApart_WithTwoHands_OpensTheExplodedView()
        {
            yield return WaitForPhase(ShowroomPhase.Inspect, 30f);
            ProductView view = _product.View;
            ExplodeController explode = _product.Explode;

            // Before the flow has introduced it, two hands only resize.
            view.Grab.BeginHold();
            view.Grab.SetHoldPoints(2, 0.2f);
            yield return TestRig.Frames(2);
            view.Grab.SetHoldPoints(2, 0.6f);
            yield return TestRig.Frames(2);
            Assert.IsFalse(explode.IsOpen, "The pull gesture shouldn't fire before the explode phase.");
            view.Grab.EndHold();

            while (_flow.Phase < ShowroomPhase.Explode)
            {
                _flow.Advance();
                yield return null;
            }

            // Hands on it, then a little apart: not enough.
            view.Grab.BeginHold();
            view.Grab.SetHoldPoints(2, 0.2f);
            yield return TestRig.Frames(2);
            view.Grab.SetHoldPoints(2, 0.35f);
            yield return TestRig.Frames(2);
            Assert.IsFalse(explode.IsOpen, "15 cm isn't a pull-apart.");

            // 25 cm further apart than they started: it comes apart, and lets go of the hands.
            view.Grab.SetHoldPoints(2, 0.46f);
            yield return TestRig.Frames(2);
            Assert.IsTrue(explode.IsOpen);
            Assert.IsFalse(view.Grab.IsHeld, "The product is released when it comes apart.");

            yield return TestRig.WaitUntil(() => !explode.IsMoving, 10f, "the parts to move out");

            // The reverse: hold two parts and bring them together.
            ProductView.Part low = view.Parts[0];
            ProductView.Part high = view.Parts[view.Parts.Length - 1];
            low.grab.BeginHold();
            high.grab.BeginHold();
            yield return TestRig.Frames(2);
            high.body.position = low.body.position + Vector3.up * 0.05f;
            yield return TestRig.Frames(2);
            Assert.IsFalse(explode.IsOpen, "Bringing two parts together should put the product back together.");

            yield return TestRig.Frames(10);
            TestRig.AssertNoProblems();
        }

        [UnityTest]
        public IEnumerator HotspotCards_OpenInFrontOfTheProduct()
        {
            yield return WaitForPhase(ShowroomPhase.Inspect, 30f);
            ProductView view = _product.View;
            HotspotView[] hotspots = Object.FindObjectsByType<HotspotView>(FindObjectsSortMode.None);
            Assert.AreEqual(_product.Definition.hotspots.Length, hotspots.Length, "One dot per hotspot in the product's data.");

            // The dots come in a few seconds into the inspect phase; until then they can't be opened.
            yield return TestRig.WaitUntil(() =>
            {
                hotspots[0].Open();
                return hotspots[0].IsOpen;
            }, 30f, "the hotspots to appear");

            Transform head = Camera.main.transform;
            int open = 0;
            foreach (HotspotView hotspot in hotspots)
            {
                hotspot.Open();
                yield return TestRig.Frames(3);
                Assert.IsTrue(hotspot.IsOpen, hotspot.name + " should open.");

                // Wherever the dot is (one is under the sole), its card has to be nearer the viewer than
                // any of the product, or the product would cut through it.
                Transform card = hotspot.transform.Find("Card");
                Assert.IsNotNull(card, "The hotspot prefab should have a Card.");
                Vector3 towards = (head.position - hotspot.transform.position).normalized;
                float productReach = BoxMath.Reach(view.transform.localToWorldMatrix, view.LocalBounds,
                    hotspot.transform.position, towards);
                float cardReach = Vector3.Dot(card.position - hotspot.transform.position, towards);
                Assert.That(cardReach, Is.GreaterThan(Mathf.Max(productReach, 0f)),
                    hotspot.name + ": the card should float in front of the product.");
            }

            foreach (HotspotView hotspot in hotspots)
            {
                open += hotspot.IsOpen ? 1 : 0;
            }

            Assert.AreEqual(1, open, "Opening one card closes the others.");

            TestRig.AssertNoProblems();
        }

        [UnityTest]
        public IEnumerator Analytics_RecordsTheRun()
        {
            yield return WaitForPhase(ShowroomPhase.Inspect, 30f);
            var logger = Object.FindFirstObjectByType<AnalyticsLogger>();
            Assert.IsNotNull(logger);

            _product.View.Grab.BeginHold();
            yield return null;
            _product.View.Grab.EndHold();
            _product.Colors.SetColor(0, 1);
            _product.Explode.Explode(ExplodeTrigger.Button);
            yield return TestRig.Frames(3);

            AnalyticsLogger.SessionRecord record = logger.Current;
            Assert.AreEqual(_product.Definition.id, record.product);
            Assert.AreNotEqual(PlacementMethod.None.ToString(), record.placement);
            Assert.That(record.grabs, Is.GreaterThanOrEqualTo(1));
            Assert.AreEqual(1, record.colorChanges);
            Assert.AreEqual(1, record.explodeByButton);
            Assert.That(record.phases[(int)ShowroomPhase.Hook].seconds, Is.GreaterThan(0f));

            TestRig.AssertNoProblems();
        }
    }
}
