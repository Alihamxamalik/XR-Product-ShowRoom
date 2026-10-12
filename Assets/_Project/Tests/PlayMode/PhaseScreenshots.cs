using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MRShowroom.Tests
{
    /// <summary>
    /// Not a test of behaviour: a way to look at the experience without a headset. Plays the flow
    /// and saves a picture of each phase, from where the user stands and from over their shoulder.
    /// It only runs when the MRSHOWROOM_SHOTS environment variable names a folder to write to, so
    /// a normal test run skips it.
    /// </summary>
    public sealed class PhaseScreenshots
    {
        private const int Width = 1400;
        private const int Height = 1000;

        private string _folder;
        private Camera _observer;

        [UnityTest]
        public IEnumerator CaptureEachPhase()
        {
            _folder = Environment.GetEnvironmentVariable("MRSHOWROOM_SHOTS");
            if (string.IsNullOrEmpty(_folder))
            {
                Assert.Ignore("Set MRSHOWROOM_SHOTS to a folder to capture a picture of each phase.");
            }

            Directory.CreateDirectory(_folder);
            TestRig.BeginCapture();
            yield return TestRig.Boot();

            var flow = UnityEngine.Object.FindFirstObjectByType<ShowroomFlow>();
            var product = UnityEngine.Object.FindFirstObjectByType<ProductController>();
            var stage = UnityEngine.Object.FindFirstObjectByType<ProductStage>();

            yield return TestRig.WaitUntil(() => stage.IsPlaced, 15f, "placement");
            yield return Wait(0.6f);
            yield return Shoot("1_hook_box_drop", stage);
            yield return Wait(1.6f);
            yield return Shoot("2_hook_reveal", stage);

            yield return TestRig.WaitUntil(() => flow.Phase == ShowroomPhase.Inspect, 20f, "inspect");
            yield return Wait(1.2f);
            yield return Shoot("3_inspect_hint", stage);
            yield return Wait(4f);
            yield return Shoot("4_inspect_hotspots", stage);

            // Open a hotspot, as a poke on its dot would: the one nearest the viewer, then the one furthest
            // from them, whose card has the whole product to clear.
            HotspotView[] hotspots = UnityEngine.Object.FindObjectsByType<HotspotView>(FindObjectsSortMode.None);
            if (hotspots.Length > 0)
            {
                Vector3 eye = Camera.main.transform.position;
                Array.Sort(hotspots, (a, b) => (a.transform.position - eye).sqrMagnitude
                    .CompareTo((b.transform.position - eye).sqrMagnitude));

                hotspots[0].Open();
                yield return Wait(0.6f);
                yield return Shoot("5_hotspot_open", stage);

                hotspots[hotspots.Length - 1].Open();
                yield return Wait(0.6f);
                yield return Shoot("5_hotspot_open_far_side", stage);
                hotspots[hotspots.Length - 1].Close();
            }

            while (flow.Phase < ShowroomPhase.Customise)
            {
                flow.Advance();
                yield return null;
            }

            yield return Wait(1.3f);
            yield return Shoot("6_customise_panel", stage);

            product.Colors.SetColor(0, 4);
            yield return Wait(0.2f);
            yield return Shoot("7_colour_sweep", stage);
            yield return Wait(0.6f);
            product.Colors.ApplyPreset(1);
            yield return Wait(0.8f);
            yield return Shoot("8_night_run", stage);

            while (flow.Phase < ShowroomPhase.Explode)
            {
                flow.Advance();
                yield return null;
            }

            yield return Wait(1.2f);
            yield return Shoot("9_explode_hint", stage);
            GameObject.Find("ExplodeButton").GetComponent<Button>().onClick.Invoke();
            yield return Wait(1.4f);
            yield return Shoot("10_exploded", stage);

            yield return TestRig.WaitUntil(() => flow.Phase == ShowroomPhase.Reveal, 30f, "reveal");
            yield return Wait(3.2f);
            yield return Shoot("11_reveal_walk", stage);

            yield return TestRig.WaitUntil(() => flow.Phase == ShowroomPhase.EndCard, 30f, "end card");
            yield return Wait(1.2f);
            yield return Shoot("12_end_card", stage);

            TestRig.EndCapture();
            TestRig.AssertNoProblems();
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float waited = 0f; waited < seconds; waited += Time.deltaTime)
            {
                yield return null;
            }
        }

        private IEnumerator Shoot(string name, ProductStage stage)
        {
            // Not WaitForEndOfFrame: without a window (a batch run) the end of a frame never comes.
            yield return null;

            Camera eye = Camera.main;
            Save(eye, Path.Combine(_folder, name + "_eye.png"));

            // Over the shoulder: further back and higher, so the product and both panels are in frame.
            if (_observer == null)
            {
                _observer = new GameObject("Observer").AddComponent<Camera>();
                _observer.enabled = false;
                _observer.clearFlags = eye.clearFlags;
                _observer.backgroundColor = eye.backgroundColor;
                _observer.fieldOfView = 50f;
                _observer.nearClipPlane = 0.05f;
            }

            Vector3 rest = stage.RestPoint.position;
            Vector3 back = eye.transform.position - rest;
            back.y = 0f;
            back.Normalize();
            Vector3 position = rest + back * 1.15f + Vector3.up * 0.62f;
            Vector3 target = rest + Vector3.up * 0.2f;
            _observer.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
            Save(_observer, Path.Combine(_folder, name + "_wide.png"));
        }

        private static void Save(Camera camera, string path)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            RenderTexture previousTarget = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = previousTarget;

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;

            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);
            target.Release();
            UnityEngine.Object.Destroy(target);
        }
    }
}
