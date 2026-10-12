using System.Collections.Generic;
using System.IO;
using Meta.XR.EnvironmentDepth;
using Meta.XR.MRUtilityKit;
using MRShowroom.MR;
using Oculus.Interaction;
using Oculus.Interaction.OVR.Editor.QuickActions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static MRShowroom.EditorTools.BuilderUtil;

namespace MRShowroom.EditorTools
{
    /// <summary>
    /// Builds the two scenes from code so they can be recreated at any time:
    ///   Bootstrap  app-wide settings, then straight on to the showroom
    ///   Showroom   camera rig, passthrough, MRUK, occlusion, the stage the product stands on, UI panels
    /// </summary>
    internal static class SceneBuilder
    {
        public const string ScenesDir = Root + "/Scenes";
        public const string BootstrapPath = ScenesDir + "/Bootstrap.unity";
        public const string ShowroomPath = ScenesDir + "/Showroom.unity";

        /// <summary>
        /// Raise this when the builders change in a way existing scenes and prefabs need to pick up.
        /// The setup pipeline offers a rebuild when what is on disk was made by an older version.
        /// </summary>
        public const int ContentVersion = 1;

        private const string VersionFile = ScenesDir + "/.content-version";
        private const string MrukPrefab = "Packages/com.meta.xr.mrutilitykit/Core/Tools/MRUK.prefab";
        private const string SampleRooms = "Packages/com.meta.xr.mrutilitykit/Core/Rooms/Json/";

        // Parts of the SDK's all-in-one rig this experience must not have: the user stays in place.
        private static readonly string[] LocomotionObjects =
        {
            "Locomotor",
            "TeleportMicrogestureInteractor", "TurnerMicrogesture", "StepMicrogesture",
            "ControllerSlideInteractor", "ControllerLocomotionSlideActions",
            "LocomotionTeleportAudio"
        };

        // Sample rooms for testing placement at a desk: one table, two tables, none.
        private static readonly string[] EditorRooms =
        {
            SampleRooms + "MeshOffice1.json",
            SampleRooms + "MeshLivingRoom1.json",
            SampleRooms + "MeshBedroom3.json"
        };

        // Corner radii, in millimetres like everything else on the canvases.
        private const float PanelCorner = 18f;
        private const float ButtonCorner = 11f;

        private static BrandAssetBuilder.Kit _kit;
        private static BrandTheme _theme;

        /// <summary>The builder version the scenes on disk were made with, or 0.</summary>
        public static int BuiltVersion()
        {
            return File.Exists(VersionFile) && int.TryParse(File.ReadAllText(VersionFile).Trim(), out int version)
                ? version
                : 0;
        }

        /// <summary>
        /// Whether builds (and Play, which starts from Bootstrap) will find both scenes. Registering them
        /// is the last thing setup does, so this is also how an interrupted setup shows.
        /// </summary>
        public static bool ScenesInBuild()
        {
            bool bootstrap = false;
            bool showroom = false;
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                bootstrap |= scene.enabled && scene.path == BootstrapPath;
                showroom |= scene.enabled && scene.path == ShowroomPath;
            }

            return bootstrap && showroom;
        }

        /// <param name="overwrite">Rebuild scenes that already exist. Otherwise only missing ones are created.</param>
        public static void BuildAll(bool overwrite)
        {
            EnsureFolder(ScenesDir);

            if (overwrite || !File.Exists(BootstrapPath))
            {
                BuildBootstrap();
            }

            if (overwrite || !File.Exists(ShowroomPath))
            {
                BuildShowroom();
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapPath, true),
                new EditorBuildSettingsScene(ShowroomPath, true)
            };

            File.WriteAllText(VersionFile, ContentVersion.ToString());
            EditorSceneManager.OpenScene(ShowroomPath, OpenSceneMode.Single);
        }

        // ---- Bootstrap -------------------------------------------------------------------------

        private static void BuildBootstrap()
        {
            Scene scene = NewScene();

            // Something has to render for the frame or two before the showroom is up. Transparent black,
            // so the headset's own loading view carries on behind it.
            var cameraObject = new GameObject("Camera", typeof(Camera), typeof(UniversalAdditionalCameraData));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 0;

            new GameObject("Bootstrap", typeof(AppBootstrap));

            Save(scene, BootstrapPath);
        }

        // ---- Showroom --------------------------------------------------------------------------

        private static void BuildShowroom()
        {
            Scene scene = NewScene();
            ProductAssetBuilder.Result assets = ProductAssetBuilder.Load();
            if (assets.Definition == null || assets.Hotspot == null || assets.Label == null)
            {
                throw new System.InvalidOperationException("The product assets are missing. Build them first.");
            }

            _kit = assets.Kit;
            _theme = _kit.Theme;
            ShowroomTuning tuning = _kit.Tuning;

            // The SDK's quick action: OVRCameraRig (floor-level tracking) + the comprehensive interaction
            // rig (hand grab, poke, ray, synthetic hands; controllers take over when picked up).
            OVRQuickActionsAPI.AddOVRInteractionRig(false);
            var cameraRig = Object.FindFirstObjectByType<OVRCameraRig>();
            var manager = Object.FindFirstObjectByType<OVRManager>();
            if (cameraRig == null || manager == null)
            {
                throw new System.InvalidOperationException("The Interaction SDK did not create a camera rig.");
            }

            RemoveLocomotion(cameraRig.transform);

            Transform centerEye = cameraRig.centerEyeAnchor != null
                ? cameraRig.centerEyeAnchor
                : FindDeep(cameraRig.transform, "CenterEyeAnchor");
            Camera eye = centerEye.GetComponent<Camera>();
            // Transparent black: wherever nothing is drawn, the passthrough underlay shows through.
            eye.clearFlags = CameraClearFlags.SolidColor;
            eye.backgroundColor = Color.clear;
            // Hands and the product come close to the face; the default near plane would clip them.
            eye.nearClipPlane = 0.05f;
            eye.farClipPlane = 30f;
            if (eye.TryGetComponent(out UniversalAdditionalCameraData cameraData))
            {
                cameraData.renderPostProcessing = false;
                EditorUtility.SetDirty(cameraData);
            }

            EditorUtility.SetDirty(eye);

            // Passthrough is switched on at runtime by PassthroughController, once the headset confirms it.
            // The spatial data permission is requested by PlacementController, not here: see ScenePermission.
            var passthroughLayer = cameraRig.gameObject.AddComponent<OVRPassthroughLayer>();
            manager.isInsightPassthroughEnabled = false;
            manager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
            EditorUtility.SetDirty(manager);

            // World-space canvases get their poke / ray clicks through this module.
            new GameObject("EventSystem", typeof(EventSystem), typeof(PointableCanvasModule));

            Light sun = BuildLight();

            // ---- Mixed reality ----
            var mixedReality = new GameObject("MixedReality");

            var mrukPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MrukPrefab);
            if (mrukPrefab == null)
            {
                throw new System.InvalidOperationException("MRUK prefab not found at " + MrukPrefab + ".");
            }

            var mrukObject = (GameObject)PrefabUtility.InstantiatePrefab(mrukPrefab, mixedReality.transform);
            var mruk = mrukObject.GetComponent<MRUK>();
            // SurfaceFinder loads the room when placement is ready for it, after the permission is settled.
            mruk.SceneSettings.LoadSceneOnStartup = false;
            EditorUtility.SetDirty(mruk);
            PrefabUtility.RecordPrefabInstancePropertyModifications(mruk);

            // Starts disabled; OcclusionToggle enables it once depth is supported and permitted.
            GameObject depthObject = Child("EnvironmentDepth", mixedReality.transform);
            var depthManager = depthObject.AddComponent<EnvironmentDepthManager>();
            depthManager.enabled = false;

            GameObject services = Child("Services", mixedReality.transform, typeof(XRDisplaySetup),
                typeof(InputModeWatcher), typeof(XRHaptics), typeof(PlacementPointer), typeof(EditorHeadEmulation));

            var passthrough = services.AddComponent<PassthroughController>();
            using (var wire = new Wire(passthrough))
            {
                wire.Ref("manager", manager).Ref("passthroughLayer", passthroughLayer).Ref("eyeCamera", eye);
            }

            var lighting = services.AddComponent<RoomLighting>();
            using (var wire = new Wire(lighting))
            {
                wire.Ref("sun", sun);
            }

            using (var wire = new Wire(services.AddComponent<OcclusionToggle>()))
            {
                wire.Ref("depthManager", depthManager);
            }

            var finder = services.AddComponent<SurfaceFinder>();
            using (var wire = new Wire(finder))
            {
                wire.Ref("tuning", tuning).Ref("mruk", mruk);
                SerializedProperty rooms = wire.Find("editorRooms");
                rooms.arraySize = EditorRooms.Length;
                for (int i = 0; i < EditorRooms.Length; i++)
                {
                    rooms.GetArrayElementAtIndex(i).stringValue = EditorRooms[i];
                }
            }

            // ---- Stage and product ----
            ProductStage stage = BuildStage(out Renderer blob, out List<Renderer> footprints);

            var productObject = new GameObject("Product");
            var colors = productObject.AddComponent<ColorCustomizer>();
            var explode = productObject.AddComponent<ExplodeController>();
            var product = productObject.AddComponent<ProductController>();
            using (var wire = new Wire(colors))
            {
                wire.Ref("tuning", tuning);
            }

            using (var wire = new Wire(explode))
            {
                wire.Ref("tuning", tuning).Ref("labelPrefab", assets.Label);
            }

            using (var wire = new Wire(product))
            {
                wire.Ref("definition", assets.Definition).Ref("tuning", tuning).Ref("stage", stage)
                    .Ref("colors", colors).Ref("explode", explode).Ref("hotspotPrefab", assets.Hotspot);
            }

            using (var wire = new Wire(blob.gameObject.AddComponent<ContactShadow>()))
            {
                wire.Ref("product", product).Ref("blob", blob);
            }

            // ---- Placement ----
            GameObject reticle = BuildReticle();
            using (var wire = new Wire(services.AddComponent<PlacementController>()))
            {
                wire.Ref("tuning", tuning).Ref("stage", stage).Ref("product", product).Ref("finder", finder)
                    .Ref("pointer", services.GetComponent<PlacementPointer>()).Ref("passthrough", passthrough)
                    .Ref("lighting", lighting).Ref("reticle", reticle.transform)
                    .Ref("reticleRenderer", reticle.GetComponent<Renderer>())
                    .Color("reticleValid", new Color(_theme.primary.r, _theme.primary.g, _theme.primary.b, 0.9f));
            }

            // ---- UI ----
            var ui = new GameObject("UI");
            SwatchPanelController swatchPanel = BuildSwatchPanel(ui.transform, product, stage, tuning);
            EndCardController endCard = BuildEndCard(ui.transform, product, stage, tuning);
            BuildPlacementPanel(ui.transform);
            BuildHints(ui.transform, stage, tuning, swatchPanel.transform);

            // ---- Flow ----
            var flowObject = new GameObject("Flow");
            var hook = flowObject.AddComponent<HookSequence>();
            var reveal = flowObject.AddComponent<RevealSequence>();
            var flow = flowObject.AddComponent<ShowroomFlow>();
            using (var wire = new Wire(hook))
            {
                wire.Ref("product", product).Ref("stage", stage);
            }

            using (var wire = new Wire(reveal))
            {
                wire.Ref("product", product).Ref("stage", stage).Refs("footprints", footprints)
                    .Color("footprintColor", new Color(_theme.primary.r, _theme.primary.g, _theme.primary.b, 0.5f));
            }

            using (var wire = new Wire(flow))
            {
                wire.Ref("tuning", tuning).Ref("stage", stage).Ref("product", product).Ref("hook", hook)
                    .Ref("reveal", reveal).Ref("swatchPanel", swatchPanel).Ref("endCard", endCard);
            }

            using (var wire = new Wire(flowObject.AddComponent<AnalyticsLogger>()))
            {
                wire.Ref("product", product);
            }

            BuildAudio(flowObject.transform);
            BuildVirtualStudio();

            // ---- Debug ----
            var debug = new GameObject("Debug");
            using (var wire = new Wire(debug.AddComponent<EditorDesktopControls>()))
            {
                wire.Ref("flow", flow).Ref("product", product).Ref("stage", stage);
            }

            GameObject overlay = Child("PerfOverlay", debug.transform);
            TextMeshPro overlayText = WorldText("Text", overlay.transform, 9f, new Color(0.6f, 1f, 0.6f, 0.9f),
                TextAlignmentOptions.TopLeft, new Vector2(300f, 40f));
            using (var wire = new Wire(overlay.AddComponent<PerfOverlay>()))
            {
                wire.Ref("text", overlayText);
            }

            ApplyLighting();
            Save(scene, ShowroomPath);
        }

        private static void RemoveLocomotion(Transform rig)
        {
            foreach (Transform candidate in rig.GetComponentsInChildren<Transform>(true))
            {
                if (System.Array.IndexOf(LocomotionObjects, candidate.name) < 0 || !candidate.gameObject.activeSelf)
                {
                    continue;
                }

                candidate.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(candidate.gameObject);
            }

            if (FindDeep(rig, "Locomotor") == null)
            {
                Debug.LogWarning("[MR Showroom] The interaction rig has no 'Locomotor' object. The SDK's rig layout " +
                                 "may have changed; check that thumbstick and microgesture locomotion are off.");
            }
        }

        // One soft directional light. Its shadow lands on the shadow catcher under the product and nowhere else.
        private static Light BuildLight()
        {
            var lightObject = new GameObject("Directional Light", typeof(Light), typeof(UniversalAdditionalLightData));
            lightObject.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            var sun = lightObject.GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.98f, 0.95f);
            // Kept low enough that a white product under it stays just short of pure white: with no tone
            // mapping, anything brighter loses its shading and looks pasted onto the room.
            sun.intensity = 0.62f;
            sun.shadows = LightShadows.Soft;
            // On a real surface the shadow catcher sets how dark the shadow is; this is for the pedestal.
            sun.shadowStrength = 0.6f;
            sun.shadowBias = 0.02f;
            sun.shadowNormalBias = 0.3f;
            return sun;
        }

        // ---- Stage -----------------------------------------------------------------------------

        private static ProductStage BuildStage(out Renderer blob, out List<Renderer> footprints)
        {
            Mesh quad = ProductAssetBuilder.FlatQuad();
            Mesh cylinder = LoadOrCreate(BrandAssetBuilder.VfxDir + "/Cylinder.asset",
                () => CylinderMesh(Vector3.one));

            var stageObject = new GameObject("Stage");
            GameObject restPoint = Child("RestPoint", stageObject.transform);

            // The pedestal: a round top on a slim column. Lit and shadowed like any object.
            Material plinth = LitMaterial(BrandAssetBuilder.BrandDir + "/Pedestal.mat", new Color(0.88f, 0.89f, 0.9f), 0.25f);
            GameObject pedestal = Child("Pedestal", stageObject.transform);
            const float topThickness = 0.02f;
            GameObject top = MeshObject("Top", pedestal.transform, cylinder, plinth, castShadows: true);
            top.transform.localScale = new Vector3(0.46f, topThickness, 0.46f);
            top.GetComponent<MeshRenderer>().receiveShadows = true;
            GameObject column = MeshObject("Column", pedestal.transform, cylinder, plinth, castShadows: true);
            column.transform.localScale = new Vector3(0.16f, 1f, 0.16f);

            // On a real surface the product's shadow is caught by an invisible plane...
            Material catcher = LoadOrCreate(BrandAssetBuilder.VfxDir + "/ShadowCatcher.mat",
                () => new Material(RequireShader("MRShowroom/Shadow Catcher")));
            GameObject shadowCatcher = MeshObject("ShadowCatcher", restPoint.transform, quad, catcher);
            shadowCatcher.transform.localPosition = new Vector3(0f, 0.0008f, 0f);
            shadowCatcher.transform.localScale = new Vector3(1.4f, 1f, 1.4f);
            shadowCatcher.GetComponent<MeshRenderer>().receiveShadows = true;

            // ...and a soft blob underneath ties it down wherever it stands.
            Material blobMaterial = UnlitMaterial(BrandAssetBuilder.VfxDir + "/ContactShadow.mat",
                new Color(0f, 0f, 0f, 0.5f), true, _kit.SoftBlob);
            GameObject blobObject = MeshObject("ContactShadow", restPoint.transform, quad, blobMaterial);
            blob = blobObject.GetComponent<Renderer>();

            Material printMaterial = UnlitMaterial(BrandAssetBuilder.VfxDir + "/Footprint.mat",
                new Color(_theme.primary.r, _theme.primary.g, _theme.primary.b, 0.85f), true, _kit.Footprint.texture);
            GameObject prints = Child("Footprints", restPoint.transform);
            footprints = new List<Renderer>();
            for (int i = 0; i < 8; i++)
            {
                GameObject print = MeshObject("Footprint" + i, prints.transform, quad, printMaterial);
                print.GetComponent<Renderer>().enabled = false;
                footprints.Add(print.GetComponent<Renderer>());
            }

            var stage = stageObject.AddComponent<ProductStage>();
            using (var wire = new Wire(stage))
            {
                wire.Ref("restPoint", restPoint.transform).Ref("pedestal", pedestal)
                    .Ref("pedestalColumn", column.transform).Ref("pedestalTop", top.transform)
                    .Float("pedestalTopThickness", topThickness).Ref("shadowCatcher", shadowCatcher);
            }

            return stage;
        }

        private static GameObject BuildReticle()
        {
            Material material = UnlitMaterial(BrandAssetBuilder.VfxDir + "/Reticle.mat", Color.white, true, _kit.Reticle);
            GameObject reticle = MeshObject("PlacementReticle", null, ProductAssetBuilder.FlatQuad(), material);
            reticle.transform.localScale = new Vector3(0.22f, 1f, 0.22f);
            reticle.SetActive(false);
            return reticle;
        }

        // ---- Colour panel ----------------------------------------------------------------------

        private static SwatchPanelController BuildSwatchPanel(Transform parent, ProductController product,
            ProductStage stage, ShowroomTuning tuning)
        {
            const float width = 286f;
            const float height = 380f;
            const float left = 18f;

            Canvas canvas = WorldCanvas("SwatchPanel", parent, new Vector2(width, height));
            Transform panel = canvas.transform;
            Stretch(AddImage("Background", panel, _kit.Shape, _theme.panel, _kit.UiMaterial, PanelCorner, true).rectTransform);

            TextMeshProUGUI title = AddText("Title", panel, "Product", 22f, _theme.text, TextAlignmentOptions.TopLeft,
                left, 14f, 200f, 28f, false, FontStyles.Bold);
            Button swapSide = AddButton("SwapSideButton", panel, "<  >", 13f, width - left - 46f, 13f, 46f, 26f, false, out _);

            // One row per colour slot: name on the left, five swatches on the right.
            const int rowCount = 3;
            const int swatchCount = 5;
            const float swatchSize = 30f;
            const float swatchGap = 5.5f;
            const float swatchLeft = 96f;
            var rows = new List<SwatchRowView>();
            for (int r = 0; r < rowCount; r++)
            {
                float y = 54f + r * 40f;
                GameObject row = AddGroup("Row" + r, panel, 0f, y, width, 36f);
                TextMeshProUGUI label = AddText("Label", row.transform, "Slot", 20f, _theme.mutedText,
                    TextAlignmentOptions.MidlineLeft, left, 0f, 76f, 36f);

                var buttons = new List<Button>();
                var fills = new List<Image>();
                var rings = new List<GameObject>();
                for (int c = 0; c < swatchCount; c++)
                {
                    float x = swatchLeft + c * (swatchSize + swatchGap);
                    Button swatch = AddSwatch("Swatch" + c, row.transform, x, 3f, swatchSize, out Image fill, out GameObject ring);
                    buttons.Add(swatch);
                    fills.Add(fill);
                    rings.Add(ring);
                }

                var view = row.AddComponent<SwatchRowView>();
                using (var wire = new Wire(view))
                {
                    wire.Ref("label", label).Refs("buttons", buttons).Refs("fills", fills).Refs("rings", rings);
                }

                rows.Add(view);
            }

            // Finish.
            GameObject finishRow = AddGroup("FinishRow", panel, 0f, 178f, width, 34f);
            AddText("Label", finishRow.transform, "Finish", 20f, _theme.mutedText, TextAlignmentOptions.MidlineLeft,
                left, 0f, 76f, 34f);
            var finishButtons = new List<Button>();
            var finishLabels = new List<TMP_Text>();
            var finishRings = new List<GameObject>();
            for (int f = 0; f < 2; f++)
            {
                Button button = AddButton("Finish" + f, finishRow.transform, "Finish", 15f, swatchLeft + f * 88f, 1f,
                    84f, 32f, false, out TextMeshProUGUI label);
                finishButtons.Add(button);
                finishLabels.Add(label);
                finishRings.Add(AddSelection(button.transform));
            }

            // Presets.
            GameObject presetRow = AddGroup("PresetRow", panel, 0f, 220f, width, 60f);
            AddText("Label", presetRow.transform, "Colourways", 14f, _theme.mutedText, TextAlignmentOptions.MidlineLeft,
                left, 0f, 200f, 20f);
            var presetButtons = new List<Button>();
            var presetLabels = new List<TMP_Text>();
            var presetRings = new List<GameObject>();
            for (int p = 0; p < 3; p++)
            {
                Button button = AddButton("Preset" + p, presetRow.transform, "Preset", 14f, left + p * 84.5f, 24f,
                    80f, 32f, false, out TextMeshProUGUI label);
                presetButtons.Add(button);
                presetLabels.Add(label);
                presetRings.Add(AddSelection(button.transform));
            }

            // Actions.
            Button randomise = AddButton("RandomiseButton", panel, "Randomise", 16f, left, 290f, 120f, 38f, false, out _);
            Button explodeButton = AddButton("ExplodeButton", panel, "See inside", 16f, left + 126f, 290f, 124f, 38f,
                true, out TextMeshProUGUI explodeLabel);

            GameObject freeRow = AddGroup("FreeRow", panel, 0f, 336f, width, 34f);
            Button move = AddButton("MoveButton", freeRow.transform, "Move", 15f, left, 0f, 120f, 32f, false, out _);
            Button realSize = AddButton("RealSizeButton", freeRow.transform, "Real size", 15f, left + 126f, 0f, 124f,
                32f, false, out _);

            PanelFader fader = AddFader(canvas);
            using (var wire = new Wire(canvas.gameObject.AddComponent<DockedPanel>()))
            {
                wire.Ref("stage", stage).Ref("tuning", tuning);
                wire.Find("side").enumValueIndex = (int)DockedPanel.Side.Dominant;
            }

            var controller = canvas.gameObject.AddComponent<SwatchPanelController>();
            using (var wire = new Wire(controller))
            {
                wire.Ref("product", product).Ref("fader", fader).Ref("title", title)
                    .Refs("rows", rows)
                    .Ref("finishRow", finishRow).Refs("finishButtons", finishButtons)
                    .Refs("finishLabels", finishLabels).Refs("finishRings", finishRings)
                    .Ref("presetRow", presetRow).Refs("presetButtons", presetButtons)
                    .Refs("presetLabels", presetLabels).Refs("presetRings", presetRings)
                    .Ref("randomiseButton", randomise).Ref("explodeButton", explodeButton)
                    .Ref("explodeLabel", explodeLabel).Ref("swapSideButton", swapSide)
                    .Ref("freeRow", freeRow).Ref("moveButton", move).Ref("realSizeButton", realSize);
            }

            return controller;
        }

        // ---- End card --------------------------------------------------------------------------

        private static EndCardController BuildEndCard(Transform parent, ProductController product, ProductStage stage,
            ShowroomTuning tuning)
        {
            const float width = 256f;
            const float height = 282f;
            const float left = 20f;

            Canvas canvas = WorldCanvas("EndCard", parent, new Vector2(width, height));
            Transform panel = canvas.transform;
            Stretch(AddImage("Background", panel, _kit.Shape, _theme.panel, _kit.UiMaterial, PanelCorner, true).rectTransform);

            Image mark = AddImage("Mark", panel, _theme.logo, _theme.primary, _kit.UiMaterial);
            Place(mark.rectTransform, left, 16f, 26f, 26f);
            AddText("Brand", panel, _theme.brandName, 15f, _theme.mutedText, TextAlignmentOptions.MidlineLeft,
                left + 32f, 16f, 160f, 26f, false, FontStyles.Bold);

            TextMeshProUGUI nameText = AddText("Name", panel, "Product", 28f, _theme.text,
                TextAlignmentOptions.TopLeft, left, 48f, width - 2f * left, 34f, false, FontStyles.Bold);
            TextMeshProUGUI tagline = AddText("Tagline", panel, "Tagline", 14f, _theme.mutedText,
                TextAlignmentOptions.TopLeft, left, 84f, width - 2f * left, 20f);

            // The colourway the user made: one chip per slot, overlapping, with its name beside them.
            var chips = new List<Image>();
            for (int i = 0; i < 3; i++)
            {
                Image outline = AddImage("ChipEdge" + i, panel, _kit.Shape, _theme.secondary, _kit.UiMaterial);
                Place(outline.rectTransform, left + i * 20f - 2f, 112f - 2f, 32f, 32f);
                Image chip = AddImage("Chip" + i, outline.transform, _kit.Shape, Color.white, _kit.UiMaterial);
                Stretch(chip.rectTransform, 2f);
                chips.Add(chip);
            }

            TextMeshProUGUI colorway = AddText("Colorway", panel, "Launch", 18f, _theme.text,
                TextAlignmentOptions.MidlineLeft, left + 82f, 112f, 140f, 28f);
            TextMeshProUGUI price = AddText("Price", panel, "$0", 30f, _theme.primary, TextAlignmentOptions.TopLeft,
                left, 150f, 140f, 36f, false, FontStyles.Bold);

            Button buy = AddButton("BuyButton", panel, "Buy now", 19f, left, 192f, width - 2f * left, 42f, true, out _);
            Button share = AddButton("ShareButton", panel, "Share", 15f, left, 240f, 104f, 30f, false, out _);
            Button again = AddButton("PlayAgainButton", panel, "Play again", 15f, left + 112f, 240f, 104f, 30f, false,
                out _);

            // The confirmation floats just above the card.
            GameObject toast = Child("Toast", panel, typeof(RectTransform), typeof(CanvasGroup));
            Place((RectTransform)toast.transform, (width - 190f) * 0.5f, -40f, 190f, 32f);
            Stretch(AddImage("Background", toast.transform, _kit.Shape, _theme.primary, _kit.UiMaterial, 16f).rectTransform);
            TextMeshProUGUI toastText = AddText("Text", toast.transform, "Done", 15f, _theme.onPrimary,
                TextAlignmentOptions.Center, 0f, 0f, 190f, 32f, false, FontStyles.Bold);

            PanelFader fader = AddFader(canvas);
            using (var wire = new Wire(canvas.gameObject.AddComponent<DockedPanel>()))
            {
                wire.Ref("stage", stage).Ref("tuning", tuning);
                wire.Find("side").enumValueIndex = (int)DockedPanel.Side.Opposite;
            }

            var controller = canvas.gameObject.AddComponent<EndCardController>();
            using (var wire = new Wire(controller))
            {
                wire.Ref("product", product).Ref("fader", fader).Ref("nameText", nameText).Ref("taglineText", tagline)
                    .Ref("colorwayText", colorway).Ref("priceText", price).Refs("chips", chips)
                    .Ref("buyButton", buy).Ref("shareButton", share).Ref("againButton", again)
                    .Ref("toastText", toastText).Ref("toastGroup", toast.GetComponent<CanvasGroup>());
            }

            return controller;
        }

        // ---- Placement card --------------------------------------------------------------------

        private static void BuildPlacementPanel(Transform parent)
        {
            const float width = 286f;
            const float height = 150f;
            const float left = 20f;

            Canvas canvas = WorldCanvas("PlacementPanel", parent, new Vector2(width, height));
            Transform panel = canvas.transform;
            Stretch(AddImage("Background", panel, _kit.Shape, _theme.panel, _kit.UiMaterial, PanelCorner, true).rectTransform);

            TextMeshProUGUI message = AddText("Message", panel, "Finding your table", 23f, _theme.text,
                TextAlignmentOptions.TopLeft, left, 18f, width - 2f * left, 30f, false, FontStyles.Bold);
            TextMeshProUGUI detail = AddText("Detail", panel, string.Empty, 15f, _theme.mutedText,
                TextAlignmentOptions.TopLeft, left, 52f, width - 2f * left, 40f, true);

            Button scan = AddButton("ScanButton", panel, "Scan my room", 15f, left, 102f, 120f, 34f, false, out _);
            Button skip = AddButton("SkipButton", panel, "Place in front", 15f, left + 126f, 102f, 120f, 34f, true, out _);

            PanelFader fader = AddFader(canvas);
            using (var wire = new Wire(canvas.gameObject.AddComponent<PlacementPanel>()))
            {
                wire.Ref("fader", fader).Ref("message", message).Ref("detail", detail)
                    .Ref("scanButton", scan).Ref("skipButton", skip);
            }
        }

        // ---- Hints -----------------------------------------------------------------------------

        private static void BuildHints(Transform parent, ProductStage stage, ShowroomTuning tuning, Transform panel)
        {
            GameObject hints = Child("Hints", parent);
            TextMeshPro prompt = WorldText("Prompt", hints.transform, 27f, _theme.text, TextAlignmentOptions.Center,
                new Vector2(420f, 40f));
            prompt.fontStyle = FontStyles.Bold;
            prompt.text = "Pick it up";
            ProductAssetBuilder.UseOutline(prompt);

            GhostHand right = HintAssetBuilder.Instantiate(HintAssetBuilder.RightHandPath, hints.transform);
            GhostHand left = HintAssetBuilder.Instantiate(HintAssetBuilder.LeftHandPath, hints.transform);

            using (var wire = new Wire(hints.AddComponent<GuideHints>()))
            {
                wire.Ref("tuning", tuning).Ref("stage", stage).Ref("prompt", prompt)
                    .Ref("rightHand", right).Ref("leftHand", left).Ref("panel", panel);
            }
        }

        // ---- Audio -----------------------------------------------------------------------------

        private static void BuildAudio(Transform parent)
        {
            GameObject audio = Child("Audio", parent);

            var voices = new List<AudioSource>();
            for (int i = 0; i < 6; i++)
            {
                var voice = Child("Voice" + i, audio.transform).AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 1f;
                voice.dopplerLevel = 0f;
                voice.rolloffMode = AudioRolloffMode.Logarithmic;
                voice.minDistance = 0.4f;
                voice.maxDistance = 8f;
                voices.Add(voice);
            }

            var music = Child("Music", audio.transform).AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.spatialBlend = 0f;
            music.loop = true;
            music.volume = 0f;

            using (var wire = new Wire(audio.AddComponent<AudioDirector>()))
            {
                wire.Ref("theme", _theme).Refs("voices", voices).Ref("music", music);
            }
        }

        // ---- Virtual studio --------------------------------------------------------------------

        // The fallback set for when passthrough isn't available: a plain floor under a soft grey sky.
        // Hidden in mixed reality, where the real room takes its place.
        private static void BuildVirtualStudio()
        {
            var studio = new GameObject("VirtualStudio");
            GameObject content = Child("Content", studio.transform);

            Material floorMaterial = LitMaterial(BrandAssetBuilder.BrandDir + "/StudioFloor.mat",
                new Color(0.7f, 0.71f, 0.73f), 0.1f);
            GameObject floor = MeshObject("Floor", content.transform, ProductAssetBuilder.FlatQuad(), floorMaterial);
            floor.transform.localScale = new Vector3(12f, 1f, 12f);
            floor.GetComponent<MeshRenderer>().receiveShadows = true;

            using (var wire = new Wire(studio.AddComponent<VirtualStudio>()))
            {
                wire.Ref("content", content);
            }
        }

        // ---- UI pieces -------------------------------------------------------------------------

        /// <param name="primary">Filled with the brand colour: the one thing on the panel to press.</param>
        private static Button AddButton(string name, Transform parent, string label, float fontSize, float x, float y,
            float width, float height, bool primary, out TextMeshProUGUI labelText)
        {
            GameObject go = Child(name, parent, typeof(RectTransform), typeof(Image), typeof(Button), typeof(HoverLift));
            PlaceCentred((RectTransform)go.transform, x, y, width, height);

            var image = go.GetComponent<Image>();
            image.sprite = _kit.Shape;
            image.material = _kit.UiMaterial;
            image.color = primary ? _theme.primary : new Color(1f, 1f, 1f, 0.13f);
            image.raycastTarget = true;
            Round(image, ButtonCorner);

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = primary ? new Color(0.92f, 0.92f, 0.92f, 1f) : new Color(1f, 1f, 1f, 2f),
                pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };

            labelText = AddText("Label", go.transform, label, fontSize, primary ? _theme.onPrimary : _theme.text,
                TextAlignmentOptions.Center, 0f, 0f, width, height, false, FontStyles.Bold);
            Stretch(labelText.rectTransform);
            return button;
        }

        private static Button AddSwatch(string name, Transform parent, float x, float y, float size, out Image fill,
            out GameObject ring)
        {
            GameObject go = Child(name, parent, typeof(RectTransform), typeof(Image), typeof(Button), typeof(HoverLift));
            PlaceCentred((RectTransform)go.transform, x, y, size, size);

            // A faint edge so a dark swatch still reads against the panel.
            var edge = go.GetComponent<Image>();
            edge.sprite = _kit.Shape;
            edge.material = _kit.UiMaterial;
            edge.color = new Color(1f, 1f, 1f, 0.18f);
            edge.raycastTarget = true;

            // The chosen colour gets a ring: a disc in the brand colour, a little bigger than the swatch
            // and behind its fill.
            Image selection = AddImage("Selected", go.transform, _kit.Shape, _theme.primary, _kit.UiMaterial);
            Stretch(selection.rectTransform, -3.5f);
            ring = selection.gameObject;
            ring.SetActive(false);

            fill = AddImage("Fill", go.transform, _kit.Shape, Color.white, _kit.UiMaterial);
            Stretch(fill.rectTransform, 1.5f);

            var button = go.GetComponent<Button>();
            button.targetGraphic = fill;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = Color.white,
                pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            return button;
        }

        // The outline shown around the chosen finish or preset.
        private static GameObject AddSelection(Transform button)
        {
            Image outline = AddImage("Selected", button, _kit.Shape,
                new Color(_theme.primary.r, _theme.primary.g, _theme.primary.b, 0.3f), _kit.UiMaterial, ButtonCorner);
            Stretch(outline.rectTransform);
            outline.transform.SetAsFirstSibling();
            outline.gameObject.SetActive(false);
            return outline.gameObject;
        }

        // Poke for direct touch, ray for pinch-at-a-distance; both switch off with the panel.
        private static PanelFader AddFader(Canvas canvas)
        {
            List<GameObject> interaction = ProductAssetBuilder.MakeInteractable(canvas);
            var fader = canvas.gameObject.AddComponent<PanelFader>();
            using (var wire = new Wire(fader))
            {
                wire.Ref("canvas", canvas).Ref("group", canvas.GetComponent<CanvasGroup>())
                    .Refs("interaction", interaction);
            }

            return fader;
        }

        // ---- Shared ----------------------------------------------------------------------------

        // Opening a scene unloads assets that nothing in a scene refers to, so every builder loads the
        // assets it needs after calling this, never before.
        private static Scene NewScene()
        {
            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        // No skybox and no baked lighting: a three-colour ambient, the one directional light, and a neutral
        // studio for the product to reflect. Soft on purpose, so it sits well in most rooms.
        private static void ApplyLighting()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.47f, 0.5f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.405f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.27f, 0.265f, 0.26f);
            RenderSettings.fog = false;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = _kit.Reflection;
            RenderSettings.reflectionIntensity = 1f;
        }

        private static void Save(Scene scene, string path)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new System.InvalidOperationException("Could not save scene to " + path);
            }
        }
    }
}
