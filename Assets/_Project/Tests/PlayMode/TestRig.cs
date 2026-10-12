using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MRShowroom.Tests
{
    /// <summary>
    /// Shared plumbing for the play mode tests: boots the app the way a real launch does, and
    /// separates genuine failures from the XR SDK's own noise.
    /// </summary>
    internal static class TestRig
    {
        private static readonly List<string> Problems = new List<string>();

        /// <summary>
        /// With no headset attached the XR SDK logs errors of its own (no OpenXR runtime, no device).
        /// Those would fail every test, so only exceptions and failed assertions count.
        /// </summary>
        public static void BeginCapture()
        {
            Problems.Clear();
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += Capture;
        }

        public static void EndCapture()
        {
            Application.logMessageReceived -= Capture;
            LogAssert.ignoreFailingMessages = false;
            Time.timeScale = 1f;
        }

        public static void AssertNoProblems()
        {
            Assert.IsEmpty(Problems, "Exceptions or failed assertions were logged:\n" + string.Join("\n", Problems));
        }

        /// <summary>Launches from the Bootstrap scene and waits until the showroom is up.</summary>
        public static IEnumerator Boot()
        {
            yield return SceneManager.LoadSceneAsync(AppBootstrap.BootstrapScene, LoadSceneMode.Single);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == AppBootstrap.ShowroomScene, 30f,
                "the showroom to load");
            yield return null;
        }

        public static IEnumerator WaitUntil(System.Func<bool> condition, float timeout, string what)
        {
            float waited = 0f;
            while (!condition())
            {
                waited += Time.unscaledDeltaTime;
                if (waited > timeout)
                {
                    Assert.Fail($"Timed out after {timeout:0.#} s waiting for {what}.");
                }

                yield return null;
            }
        }

        public static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private static void Capture(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Assert)
            {
                Problems.Add(condition + "\n" + stackTrace);
            }
        }
    }
}
