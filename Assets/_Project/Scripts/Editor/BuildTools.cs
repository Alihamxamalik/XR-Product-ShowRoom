using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MRShowroom.EditorTools
{
    /// <summary>
    /// One-step builds for the daily loop: a development APK (profiler and the in-headset
    /// performance overlay on) for testing, and a release APK for sharing. Both go to a "Builds"
    /// folder next to Assets, which git ignores.
    /// </summary>
    internal static class BuildTools
    {
        [MenuItem("MR Showroom/Build/Development APK", priority = 60)]
        public static void BuildDevelopmentFromMenu()
        {
            Report(Build(DefaultPath("MRShowroom-dev.apk"), development: true), reveal: true);
        }

        [MenuItem("MR Showroom/Build/Release APK", priority = 61)]
        public static void BuildReleaseFromMenu()
        {
            Report(Build(DefaultPath("MRShowroom.apk"), development: false), reveal: true);
        }

        /// <summary>
        /// Command line: Unity -batchmode -executeMethod MRShowroom.EditorTools.BuildTools.BuildBatch
        /// [-buildOut path/to.apk] [-release]
        /// </summary>
        public static void BuildBatch()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                bool release = args.Contains("-release");
                string path = DefaultPath(release ? "MRShowroom.apk" : "MRShowroom-dev.apk");
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i] == "-buildOut")
                    {
                        path = args[i + 1];
                    }
                }

                BuildReport report = Build(path, !release);
                Report(report, reveal: false);
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogError("[MR Showroom] Build failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        private static string DefaultPath(string file)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", file));
        }

        private static BuildReport Build(string path, bool development)
        {
            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No scenes in the build. Run MR Showroom > Setup > Run Full Setup first.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = development ? BuildOptions.Development : BuildOptions.None
            };

            return BuildPipeline.BuildPlayer(options);
        }

        private static void Report(BuildReport report, bool reveal)
        {
            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                long bytes = File.Exists(summary.outputPath) ? new FileInfo(summary.outputPath).Length : 0;
                Debug.Log($"[MR Showroom] Build succeeded: {summary.outputPath} " +
                          $"({bytes / (1024f * 1024f):0.0} MB, {summary.totalTime.TotalSeconds:0} s). " +
                          "Install it with: adb install -r \"" + summary.outputPath + "\"");
                if (reveal)
                {
                    EditorUtility.RevealInFinder(summary.outputPath);
                }
            }
            else
            {
                Debug.LogError($"[MR Showroom] Build {summary.result}: {summary.totalErrors} error(s). See the Console.");
            }
        }
    }
}
