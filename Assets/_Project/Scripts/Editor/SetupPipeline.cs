using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MRShowroom.EditorTools
{
    /// <summary>
    /// One-click project setup, in the order the design document lists it:
    /// switch to Android, import TextMeshPro's resources, apply project settings, run Meta's Project
    /// Setup Tool ("Fix All" and "Apply All"), then build the brand kit, the product and the scenes.
    ///
    /// Several of those steps make Unity recompile and reload scripts, which wipes static state. The
    /// current step is therefore kept in SessionState and the pipeline picks itself up again after
    /// each reload. Every step is safe to repeat.
    /// </summary>
    public static class SetupPipeline
    {
        private const string StepKey = "MRShowroom.Setup.Step";
        private const string OverwriteKey = "MRShowroom.Setup.Overwrite";
        private const string FixPassesKey = "MRShowroom.Setup.FixPasses";
        private const string TmpRequestedKey = "MRShowroom.Setup.TmpRequested";
        private const string OfferedKey = "MRShowroom.Setup.Offered";
        private const string TmpPackage = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
        private const string TmpSettings = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        private enum Step
        {
            Idle = 0,
            SwitchPlatform = 1,
            ImportTextMeshPro = 2,
            ApplySettings = 3,
            MetaFixAll = 4,
            BuildContent = 5,
            Finish = 6
        }

        private static MetaProjectSetup.Run _metaFix;
        private static double _stepStartedAt;

        private static Step Current
        {
            get => (Step)SessionState.GetInt(StepKey, (int)Step.Idle);
            set
            {
                SessionState.SetInt(StepKey, (int)value);
                _stepStartedAt = EditorApplication.timeSinceStartup;
            }
        }

        [MenuItem("MR Showroom/Setup/Run Full Setup", priority = 0)]
        public static void RunFromMenu()
        {
            if (!CanTouchScenes())
            {
                return;
            }

            if (!EditorUtility.DisplayDialog("MR Product Showroom setup",
                    "This will switch the project to Android, apply the project settings from the design document, " +
                    "run Meta's Project Setup Tool, and create any missing brand assets, product prefabs and scenes.\n\n" +
                    "Unity will reimport assets and recompile a few times; it can take several minutes.",
                    "Run setup", "Cancel"))
            {
                return;
            }

            Begin(overwrite: false);
        }

        [MenuItem("MR Showroom/Setup/Apply Project Settings Only", priority = 20)]
        public static void ApplySettingsFromMenu()
        {
            ProjectConfigurator.ApplyAll();
            Debug.Log("[MR Showroom] Project settings applied.");
        }

        [MenuItem("MR Showroom/Setup/Rebuild Scenes and Prefabs (overwrites)", priority = 21)]
        public static void RebuildFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Rebuild scenes and prefabs",
                    "The Bootstrap and Showroom scenes, the AERO Pulse's meshes and prefab, its box, and the hotspot, " +
                    "label and hint prefabs will be recreated from code. Manual changes to them will be lost. Data " +
                    "assets (the product definition, the brand theme, tuning, materials) are kept.",
                    "Rebuild", "Cancel"))
            {
                return;
            }

            if (!CanTouchScenes())
            {
                return;
            }

            BuildContent(overwrite: true);
        }

        // The builders open and replace scenes, so leave play mode and deal with unsaved work first.
        private static bool CanTouchScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("MR Product Showroom", "Exit play mode first.", "OK");
                return false;
            }

            return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        /// <summary>
        /// Command line entry point: Unity -batchmode -executeMethod MRShowroom.EditorTools.SetupPipeline.RunBatch
        /// Recreates the scenes and prefabs from code; add -keepContent to leave existing ones alone, as the
        /// first-run offer in the editor does.
        /// </summary>
        public static void RunBatch()
        {
            bool keepContent = Array.IndexOf(Environment.GetCommandLineArgs(), "-keepContent") >= 0;
            Begin(overwrite: !keepContent);
        }

        [InitializeOnLoadMethod]
        private static void ResumeAfterReload()
        {
            if (Current != Step.Idle)
            {
                _stepStartedAt = EditorApplication.timeSinceStartup;
                EditorApplication.delayCall += Advance;
            }
            else if (!Application.isBatchMode)
            {
                EditorApplication.delayCall += OfferFirstRun;
            }
        }

        // A project that has the files but not the project settings (or not the scenes) hasn't been set up;
        // one whose scenes aren't in the build settings had its setup cut short (a crash, a force quit),
        // because that is the last thing setup does; and one whose scenes were made by older builders is
        // out of date. Offer once per editor session.
        private static void OfferFirstRun()
        {
            if (Current != Step.Idle || SessionState.GetBool(OfferedKey, false))
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating
                                              || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += OfferFirstRun;
                return;
            }

            bool missing = !File.Exists(SceneBuilder.ShowroomPath) || !ProjectConfigurator.IsConfigured()
                                                                    || !SceneBuilder.ScenesInBuild();
            bool outdated = !missing && SceneBuilder.BuiltVersion() < SceneBuilder.ContentVersion;
            if (!missing && !outdated)
            {
                return;
            }

            SessionState.SetBool(OfferedKey, true);

            if (missing)
            {
                if (EditorUtility.DisplayDialog("MR Product Showroom",
                        "This project's setup hasn't been completed on this machine.\n\n" +
                        "Setup switches to Android, applies the project settings from the design document, runs " +
                        "Meta's Project Setup Tool, and creates any scenes or prefabs that are missing. It takes a " +
                        "few minutes, and Unity will reimport and recompile along the way.\n\n" +
                        "You can also start it later from MR Showroom > Setup > Run Full Setup.",
                        "Run setup", "Not now") && CanTouchScenes())
                {
                    Begin(overwrite: false);
                }
            }
            else if (EditorUtility.DisplayDialog("MR Product Showroom",
                         "The scenes and prefabs were built by an older version of the builders.\n\n" +
                         "Rebuilding recreates them from code; manual changes to them will be lost. Data assets " +
                         "(the product definition, the brand theme, tuning, materials) are kept.\n\n" +
                         "You can also do this later from MR Showroom > Setup > Rebuild Scenes and Prefabs.",
                         "Rebuild", "Not now") && CanTouchScenes())
            {
                Begin(overwrite: true);
            }
        }

        private static void Begin(bool overwrite)
        {
            SessionState.SetBool(OverwriteKey, overwrite);
            SessionState.SetInt(FixPassesKey, 0);
            SessionState.EraseBool(TmpRequestedKey);
            _metaFix = null;
            Current = Step.SwitchPlatform;
            Advance();
        }

        // Runs steps until one has to wait (for a compile, an import or an async fix), then reschedules itself.
        private static void Advance()
        {
            try
            {
                while (Current != Step.Idle)
                {
                    if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                    {
                        EditorApplication.delayCall += Advance;
                        return;
                    }

                    if (!RunStep(Current))
                    {
                        EditorApplication.delayCall += Advance;
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Current = Step.Idle;
                Debug.LogError("[MR Showroom] Setup stopped: " + e);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }

        /// <returns>False when the step is still waiting and should be polled again.</returns>
        private static bool RunStep(Step step)
        {
            switch (step)
            {
                case Step.SwitchPlatform:
                    Current = Step.ImportTextMeshPro;
                    if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                    {
                        Debug.Log("[MR Showroom] Switching build target to Android...");
                        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android,
                                BuildTarget.Android))
                        {
                            throw new InvalidOperationException(
                                "Could not switch to Android. Is the Android Build Support module installed?");
                        }
                    }

                    return true;

                case Step.ImportTextMeshPro:
                    if (File.Exists(TmpSettings))
                    {
                        Current = Step.ApplySettings;
                        return true;
                    }

                    if (!SessionState.GetBool(TmpRequestedKey, false))
                    {
                        SessionState.SetBool(TmpRequestedKey, true);
                        Debug.Log("[MR Showroom] Importing TextMeshPro essential resources...");
                        AssetDatabase.ImportPackage(TmpPackage, false);
                    }
                    else if (WaitedLongerThan(120))
                    {
                        throw new TimeoutException("TextMeshPro resources did not finish importing. Import them from " +
                                                   "Window > TextMeshPro > Import TMP Essential Resources and run setup again.");
                    }

                    return false;

                case Step.ApplySettings:
                    Debug.Log("[MR Showroom] Applying project settings...");
                    ProjectConfigurator.ApplyAll();
                    Current = Step.MetaFixAll;
                    return true;

                case Step.MetaFixAll:
                    if (_metaFix != null && !_metaFix.Completed && !WaitedLongerThan(180))
                    {
                        return false;
                    }

                    _metaFix = null;

                    // Two passes: a fix can trigger a script reload that cuts the first pass short (the handle
                    // is lost with it), and some fixes only become possible after others have landed.
                    int passes = SessionState.GetInt(FixPassesKey, 0);
                    if (passes < 2)
                    {
                        SessionState.SetInt(FixPassesKey, passes + 1);
                        Debug.Log($"[MR Showroom] Meta Project Setup Tool: Fix All + Apply All (Android), pass {passes + 1}...");
                        _metaFix = MetaProjectSetup.FixRequiredAndRecommended(BuildTargetGroup.Android);
                        _stepStartedAt = EditorApplication.timeSinceStartup;
                        if (_metaFix != null)
                        {
                            return false;
                        }

                        SessionState.SetInt(FixPassesKey, 2);
                        Debug.LogWarning("[MR Showroom] Meta's Project Setup Tool could not be run automatically with " +
                                         "this SDK version. Open Edit > Project Settings > Meta XR and press Fix All, " +
                                         "then Apply All, on the Android tab.");
                    }

                    // Meta's tool has the final say on what the store requires, but two things are this project's
                    // own and are put back if the tool changed them: the shadow settings the shadow catcher needs,
                    // and the OpenXR features the Depth API needs.
                    ProjectConfigurator.ApplyQualityAndUrp();
                    ProjectConfigurator.ApplyXR();

                    // Keeping the manifest in step with the settings is only an optional item in Meta's tool, so
                    // the passes above don't do it. A stale manifest would be missing the passthrough, scene and
                    // anchor entries.
                    if (OVRManifestPreprocessor.IsManifestOutdated(out _))
                    {
                        OVRManifestPreprocessor.GenerateOrUpdateAndroidManifest(true);
                    }

                    AssetDatabase.SaveAssets();
                    Current = Step.BuildContent;
                    return true;

                case Step.BuildContent:
                    BuildContent(SessionState.GetBool(OverwriteKey, false));
                    Current = Step.Finish;
                    return true;

                case Step.Finish:
                    Current = Step.Idle;
                    SessionState.EraseBool(TmpRequestedKey);
                    SessionState.EraseInt(FixPassesKey);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[MR Showroom] Setup finished. Open Assets/_Project/Scenes/Showroom.unity and press Play, " +
                              "or File > Build And Run with the headset connected.");
                    if (Application.isBatchMode)
                    {
                        EditorApplication.Exit(0);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("MR Product Showroom", "Setup finished.\n\nThe Showroom scene is " +
                            "open: press Play to try it with mouse and keyboard, or use Build And Run with the headset " +
                            "connected.\n\nMeta's Project Setup Tool is under Edit > Project Settings > Meta XR if you " +
                            "want to review it.", "OK");
                    }

                    return true;

                default:
                    Current = Step.Idle;
                    return true;
            }
        }

        private static void BuildContent(bool overwrite)
        {
            Debug.Log("[MR Showroom] Building the brand kit, the product and the scenes...");
            ProductAssetBuilder.Build(rebuildPrefabs: overwrite);
            HintAssetBuilder.Build(rebuildPrefabs: overwrite);
            SceneBuilder.BuildAll(overwrite);
            AssetDatabase.SaveAssets();
        }

        private static bool WaitedLongerThan(double seconds)
        {
            return EditorApplication.timeSinceStartup - _stepStartedAt > seconds;
        }
    }
}
