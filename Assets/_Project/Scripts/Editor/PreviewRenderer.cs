using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MRShowroom.EditorTools
{
    /// <summary>
    /// Renders the product and the UI panels to image files, without a headset and without
    /// entering play mode. For checking generated art at a glance after a rebuild, and for
    /// thumbnails. Output goes to a "Previews" folder next to Assets (not into the project).
    /// </summary>
    internal static class PreviewRenderer
    {
        private const int Width = 1280;
        private const int Height = 900;

        [MenuItem("MR Showroom/Dev/Render Previews", priority = 100)]
        public static void RenderFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode
                || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Previews"));
            Render(folder);
            EditorUtility.RevealInFinder(folder);
        }

        /// <summary>
        /// Command line: Unity -batchmode -executeMethod MRShowroom.EditorTools.PreviewRenderer.RenderBatch
        /// -previewOut &lt;folder&gt;. Needs a graphics device, so not with -nographics.
        /// </summary>
        public static void RenderBatch()
        {
            try
            {
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Previews"));
                string[] args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i] == "-previewOut")
                    {
                        folder = args[i + 1];
                    }
                }

                Render(folder);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[MR Showroom] Preview rendering failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        public static void Render(string folder)
        {
            Directory.CreateDirectory(folder);
            RenderProduct(folder);
            RenderHands(folder);
            RenderPanels(folder);
            Debug.Log("[MR Showroom] Previews written to " + folder);
        }

        // ---- Product ---------------------------------------------------------------------------

        private static void RenderProduct(string folder)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ProductAssetBuilder.Result assets = ProductAssetBuilder.Load();
            ProductDefinition definition = assets.Definition;
            if (definition == null || definition.prefab == null)
            {
                Debug.LogWarning("[MR Showroom] No product to preview.");
                return;
            }

            SetUpStudio(assets.Kit, out Camera camera);

            var view = (ProductView)PrefabUtility.InstantiatePrefab(definition.prefab);
            var originals = new System.Collections.Generic.List<Material[]>();
            foreach (Renderer renderer in view.Renderers)
            {
                originals.Add(renderer.sharedMaterials);
            }

            Material[] dressed = Dress(view, originals, definition, 0);

            Bounds bounds = view.LocalBounds;
            Vector3 centre = bounds.center;
            float radius = bounds.extents.magnitude;

            Shoot(camera, centre, radius, new Vector3(8f, 0f, 0f), 1.5f, Path.Combine(folder, "product_side.png"));
            Shoot(camera, centre, radius, new Vector3(22f, -38f, 0f), 1.6f, Path.Combine(folder, "product_three_quarter.png"));
            Shoot(camera, centre, radius, new Vector3(20f, 142f, 0f), 1.6f, Path.Combine(folder, "product_back.png"));
            Shoot(camera, centre, radius, new Vector3(82f, 0f, 0f), 1.6f, Path.Combine(folder, "product_top.png"));
            Shoot(camera, centre, radius, new Vector3(-55f, -25f, 0f), 1.7f, Path.Combine(folder, "product_sole.png"));

            // A second colourway with the metallic finish, to check the tint regions and the reflection.
            Release(dressed);
            dressed = Dress(view, originals, definition, Mathf.Min(1, definition.presets.Length - 1));
            Shoot(camera, centre, radius, new Vector3(22f, -38f, 0f), 1.6f, Path.Combine(folder, "product_colourway2.png"));

            // Exploded: every slot moved out the way ExplodeController moves it.
            Bounds exploded = bounds;
            foreach (ProductView.Part part in view.Parts)
            {
                ExplodePart data = definition.FindPart(part.name);
                if (data == null)
                {
                    continue;
                }

                Vector3 offset = data.direction.normalized * data.distance;
                part.slot.localPosition += offset;
                exploded.Encapsulate(bounds.center + offset + bounds.extents);
                exploded.Encapsulate(bounds.center + offset - bounds.extents);
            }

            Shoot(camera, exploded.center, exploded.extents.magnitude, new Vector3(10f, -30f, 0f), 2.2f,
                Path.Combine(folder, "product_exploded.png"));

            Release(dressed);

            // The box.
            Object.DestroyImmediate(view.gameObject);
            if (definition.packaging != null)
            {
                var box = (PackagingBox)PrefabUtility.InstantiatePrefab(definition.packaging);
                Vector3 size = box.Size;
                Shoot(camera, new Vector3(0f, size.y * 0.5f, 0f), size.magnitude * 0.5f, new Vector3(24f, -35f, 0f), 2.6f,
                    Path.Combine(folder, "box.png"));
            }
        }

        // Gives the preview copy its own materials, coloured the way a preset would colour them at runtime.
        private static Material[] Dress(ProductView view, System.Collections.Generic.List<Material[]> originals,
            ProductDefinition definition, int preset)
        {
            var instances = new System.Collections.Generic.Dictionary<Material, Material>();
            for (int r = 0; r < view.Renderers.Length; r++)
            {
                Renderer renderer = view.Renderers[r];
                var shared = (Material[])originals[r].Clone();
                for (int i = 0; i < shared.Length; i++)
                {
                    if (!instances.TryGetValue(shared[i], out Material instance))
                    {
                        instance = new Material(shared[i]);
                        instances.Add(shared[i], instance);
                    }

                    shared[i] = instance;
                }

                renderer.sharedMaterials = shared;
            }

            Colorway colorway = definition.presets.Length > 0 ? definition.presets[Mathf.Max(preset, 0)] : null;
            foreach (Material material in instances.Values)
            {
                for (int s = 0; s < definition.colorSlots.Length; s++)
                {
                    ColorSlot slot = definition.colorSlots[s];
                    int index = colorway != null && s < colorway.colorIndices.Length ? colorway.colorIndices[s] : 0;
                    Color color = slot.colors.Length > 0 ? slot.colors[Mathf.Clamp(index, 0, slot.colors.Length - 1)].color : Color.white;
                    var surface = new Vector4(slot.metallic, slot.smoothness, 0f, 0f);
                    if (s == definition.finishSlot && colorway != null && colorway.finishIndex < definition.finishes.Length)
                    {
                        FinishOption finish = definition.finishes[colorway.finishIndex];
                        surface = new Vector4(finish.metallic, finish.smoothness, 0f, 0f);
                    }

                    material.SetColor(slot.shaderProperty + "From", color);
                    material.SetColor(slot.shaderProperty + "To", color);
                    material.SetFloat(slot.shaderProperty + "Sweep", 1f);
                    material.SetVector(slot.shaderProperty + "SurfaceFrom", surface);
                    material.SetVector(slot.shaderProperty + "SurfaceTo", surface);
                }
            }

            var list = new Material[instances.Count];
            instances.Values.CopyTo(list, 0);
            return list;
        }

        private static void Release(Material[] materials)
        {
            foreach (Material material in materials)
            {
                Object.DestroyImmediate(material);
            }
        }

        // ---- Ghost hands -----------------------------------------------------------------------

        // The hint hands in the three poses the hints use: open, gripping, pointing. The bend axes are
        // worked out from the skeleton when the prefab is built; this is where a wrong one would show.
        private static void RenderHands(string folder)
        {
            var poses = new[] { new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            foreach (string path in new[] { HintAssetBuilder.RightHandPath, HintAssetBuilder.LeftHandPath })
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                ProductAssetBuilder.Result assets = ProductAssetBuilder.Load();
                SetUpStudio(assets.Kit, out Camera camera);

                // Left to right: open, gripping, pointing. Fingers run away from the camera, back of the hand up.
                for (int i = 0; i < poses.Length; i++)
                {
                    GhostHand hand = HintAssetBuilder.Instantiate(path, null);
                    if (hand == null)
                    {
                        return;
                    }

                    hand.transform.position = new Vector3((i - 1) * 0.2f, 0.12f, 0f);
                    Pose(hand, poses[i].x, poses[i].y);
                }

                string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                Shoot(camera, new Vector3(0f, 0.12f, 0.03f), 0.26f, new Vector3(50f, 0f, 0f), 1.75f,
                    Path.Combine(folder, name + "_above.png"));
                Shoot(camera, new Vector3(0f, 0.12f, 0.03f), 0.26f, new Vector3(8f, 55f, 0f), 1.75f,
                    Path.Combine(folder, name + "_side.png"));
            }
        }

        // What GhostHand.SetPose does at runtime, done here from the serialized data because the
        // component's Awake hasn't run on an edit-time instance.
        private static void Pose(GhostHand hand, float grip, float index)
        {
            var serialized = new SerializedObject(hand);
            float finger = serialized.FindProperty("fingerCurl").floatValue;
            float thumb = serialized.FindProperty("thumbCurl").floatValue;
            Bend(serialized, "fingerJoints", "fingerAxes", finger * grip);
            Bend(serialized, "indexJoints", "indexAxes", finger * index);
            Bend(serialized, "thumbJoints", "thumbAxes", thumb * grip);

            SerializedProperty renderers = serialized.FindProperty("renderers");
            for (int i = 0; i < renderers.arraySize; i++)
            {
                var renderer = (Renderer)renderers.GetArrayElementAtIndex(i).objectReferenceValue;
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
        }

        private static void Bend(SerializedObject hand, string jointsField, string axesField, float degrees)
        {
            SerializedProperty joints = hand.FindProperty(jointsField);
            SerializedProperty axes = hand.FindProperty(axesField);
            for (int i = 0; i < joints.arraySize && i < axes.arraySize; i++)
            {
                var joint = (Transform)joints.GetArrayElementAtIndex(i).objectReferenceValue;
                if (joint != null)
                {
                    joint.localRotation *= Quaternion.AngleAxis(degrees, axes.GetArrayElementAtIndex(i).vector3Value);
                }
            }
        }

        // ---- Panels ----------------------------------------------------------------------------

        // Each world-space canvas in the Showroom scene, face on. At edit time they all sit at the origin,
        // so every other one is hidden while one is photographed.
        private static void RenderPanels(string folder)
        {
            if (!File.Exists(SceneBuilder.ShowroomPath))
            {
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(SceneBuilder.ShowroomPath, OpenSceneMode.Single);
            Canvas[] found = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            // The panels in the scene's UI group; hotspots and labels are prefabs and aren't in the scene yet.
            var canvases = new System.Collections.Generic.List<Canvas>();
            foreach (Canvas candidate in found)
            {
                if (candidate.isRootCanvas && candidate.renderMode == RenderMode.WorldSpace
                    && candidate.transform.parent != null)
                {
                    canvases.Add(candidate);
                }
            }

            var cameraObject = new GameObject("PreviewCamera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            // A mid grey stands in for a room behind the translucent panels.
            camera.backgroundColor = new Color(0.47f, 0.46f, 0.44f, 1f);
            camera.orthographic = true;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 5f;
            camera.cullingMask = 1 << LayerMask.NameToLayer("UI") | 1 << 0;

            foreach (Canvas canvas in canvases)
            {

                // Everything else in the scene sits at the origin too (the rig's hands, the pedestal), so
                // only this panel stays switched on. The scene is reopened afterwards, unsaved.
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    root.SetActive(root == canvas.transform.root.gameObject);
                }

                foreach (Transform sibling in canvas.transform.parent)
                {
                    sibling.gameObject.SetActive(sibling == canvas.transform);
                }

                var rect = (RectTransform)canvas.transform;
                Vector2 size = rect.rect.size * rect.lossyScale.x;
                Vector3 middle = rect.TransformPoint(rect.rect.center);
                camera.transform.SetPositionAndRotation(middle - rect.forward * 1f, rect.rotation);
                camera.orthographicSize = Mathf.Max(size.y, size.x * Height / Width) * 0.62f;
                Canvas.ForceUpdateCanvases();
                Capture(camera, Path.Combine(folder, "panel_" + canvas.name + ".png"));
            }

            Object.DestroyImmediate(cameraObject);
            // Reopen without saving: the canvases were only switched off for the photographs.
            EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
        }

        // ---- Shared ----------------------------------------------------------------------------

        private static void SetUpStudio(BrandAssetBuilder.Kit kit, out Camera camera)
        {
            var cameraObject = new GameObject("PreviewCamera");
            camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.47f, 0.46f, 0.44f, 1f);
            camera.fieldOfView = 30f;
            camera.nearClipPlane = 0.02f;
            camera.farClipPlane = 20f;

            var lightObject = new GameObject("PreviewLight");
            lightObject.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.98f, 0.95f);
            light.intensity = 0.62f;
            light.shadows = LightShadows.Soft;

            // A table to stand on and to catch the shadow.
            GameObject table = GameObject.CreatePrimitive(PrimitiveType.Plane);
            table.name = "PreviewTable";
            table.transform.localScale = Vector3.one * 0.4f;
            var material = new Material(BuilderUtil.RequireShader("Universal Render Pipeline/Simple Lit"));
            material.SetColor("_BaseColor", new Color(0.62f, 0.56f, 0.48f));
            table.GetComponent<Renderer>().sharedMaterial = material;

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.47f, 0.5f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.405f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.27f, 0.265f, 0.26f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = kit.Reflection;
            RenderSettings.reflectionIntensity = 1f;
        }

        /// <param name="euler">Where the camera looks from: pitch down, then yaw around the subject.</param>
        /// <param name="distance">In multiples of the subject's radius.</param>
        private static void Shoot(Camera camera, Vector3 centre, float radius, Vector3 euler, float distance, string path)
        {
            Quaternion rotation = Quaternion.Euler(euler);
            float range = radius * distance / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.5f;
            camera.transform.SetPositionAndRotation(centre - rotation * Vector3.forward * range, rotation);
            Capture(camera, path);
        }

        private static void Capture(Camera camera, string path)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4
            };
            camera.targetTexture = target;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;

            camera.targetTexture = null;
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            target.Release();
            Object.DestroyImmediate(target);
        }
    }
}
