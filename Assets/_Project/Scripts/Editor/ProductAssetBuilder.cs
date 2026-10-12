using System.Collections.Generic;
using Oculus.Interaction.Editor.QuickActions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static MRShowroom.EditorTools.BuilderUtil;

namespace MRShowroom.EditorTools
{
    /// <summary>
    /// Creates the hero product and everything that ships with it: the AERO Pulse's meshes,
    /// materials and prefab, its box, the hotspot and part-label prefabs, and the product
    /// definition that ties them together. Existing data assets are kept, so values tuned in the
    /// inspector survive a re-run; meshes and prefabs are regenerated on request.
    /// </summary>
    internal static class ProductAssetBuilder
    {
        public const string ProductDir = ArtDir + "/Products/AeroPulse";
        public const string DefinitionPath = DataDir + "/Products/AeroPulse.asset";
        public const string ProductPrefabPath = PrefabsDir + "/Product/AeroPulse.prefab";
        public const string BoxPrefabPath = PrefabsDir + "/Product/AeroBox.prefab";
        public const string HotspotPrefabPath = PrefabsDir + "/UI/Hotspot.prefab";
        public const string LabelPrefabPath = PrefabsDir + "/UI/PartLabel.prefab";

        public sealed class Result
        {
            public BrandAssetBuilder.Kit Kit;
            public ProductDefinition Definition;
            public HotspotView Hotspot;
            public PartLabel Label;
        }

        /// <summary>
        /// Loads what <see cref="Build"/> made. Call it after opening a scene, never before: opening a
        /// scene unloads assets nothing refers to, and references taken earlier go stale.
        /// </summary>
        public static Result Load()
        {
            return new Result
            {
                Kit = BrandAssetBuilder.Build(),
                Definition = Definition(),
                Hotspot = AssetDatabase.LoadAssetAtPath<HotspotView>(HotspotPrefabPath),
                Label = AssetDatabase.LoadAssetAtPath<PartLabel>(LabelPrefabPath)
            };
        }

        public static ProductDefinition Definition()
        {
            return AssetDatabase.LoadAssetAtPath<ProductDefinition>(DefinitionPath);
        }

        /// <param name="rebuildPrefabs">Recreate prefabs and meshes even when they already exist.</param>
        public static Result Build(bool rebuildPrefabs)
        {
            // One empty scene for the whole job. Opening a scene makes Unity unload assets nothing in a scene
            // refers to, so no scene may be opened while the assets below are being created and linked.
            // It is also where the prefabs are assembled, with no rig for the SDK's quick actions to modify.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BrandAssetBuilder.Kit kit = BrandAssetBuilder.Build();

            foreach (string folder in new[]
                     {
                         ProductDir + "/Meshes", ProductDir + "/Materials", DataDir + "/Products",
                         PrefabsDir + "/Product", PrefabsDir + "/UI", PrefabsDir + "/Hints", PrefabsDir + "/Rig"
                     })
            {
                EnsureFolder(folder);
            }

            var product = AssetDatabase.LoadAssetAtPath<ProductView>(ProductPrefabPath);
            if (product == null || rebuildPrefabs)
            {
                product = BuildShoe(kit);
            }

            var box = AssetDatabase.LoadAssetAtPath<PackagingBox>(BoxPrefabPath);
            if (box == null || rebuildPrefabs)
            {
                box = BuildBox(kit);
            }

            var hotspot = AssetDatabase.LoadAssetAtPath<HotspotView>(HotspotPrefabPath);
            if (hotspot == null || rebuildPrefabs)
            {
                hotspot = BuildHotspot(kit);
            }

            var label = AssetDatabase.LoadAssetAtPath<PartLabel>(LabelPrefabPath);
            if (label == null || rebuildPrefabs)
            {
                label = BuildLabel(kit);
            }

            ProductDefinition definition = LoadOrCreate(DefinitionPath, AeroPulse);
            definition.prefab = product;
            definition.packaging = box;
            if (definition.footprint == null)
            {
                definition.footprint = kit.Footprint;
            }

            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();

            return new Result { Kit = kit, Definition = definition, Hotspot = hotspot, Label = label };
        }

        // ---- The shoe --------------------------------------------------------------------------

        private static ProductView BuildShoe(BrandAssetBuilder.Kit kit)
        {
            Shader shader = RequireShader("MRShowroom/Product Lit");

            // Three materials on one shader, the budget the design sets: knit, foam and rubber.
            var materials = new Material[3];
            materials[ShoeMeshBuilder.Fabric] = LoadOrCreate(ProductDir + "/Materials/AeroPulse_Fabric.mat", () =>
            {
                var material = new Material(shader) { enableInstancing = true };
                material.SetFloat("_Smoothness", 0.25f);
                material.SetFloat("_DetailStrength", 0.45f);
                material.SetColor("_RimColor", kit.Theme.primary);
                return material;
            });
            materials[ShoeMeshBuilder.Foam] = LoadOrCreate(ProductDir + "/Materials/AeroPulse_Foam.mat", () =>
            {
                var material = new Material(shader) { enableInstancing = true };
                material.SetFloat("_Smoothness", 0.4f);
                material.SetColor("_RimColor", kit.Theme.primary);
                return material;
            });

            Texture2D palette = Palette();
            materials[ShoeMeshBuilder.Rubber] = LoadOrCreate(ProductDir + "/Materials/AeroPulse_Rubber.mat", () =>
            {
                var material = new Material(shader) { enableInstancing = true };
                material.SetTexture("_BaseMap", palette);
                material.SetFloat("_Smoothness", 0.35f);
                material.SetColor("_RimColor", kit.Theme.primary);
                return material;
            });

            var sources = new List<ProductPrefabBuilder.PartSource>();
            foreach (ShoeMeshBuilder.Part part in ShoeMeshBuilder.Build())
            {
                Mesh mesh = CreateOrReplace($"{ProductDir}/Meshes/{part.Name}.asset", part.Mesh);
                sources.Add(new ProductPrefabBuilder.PartSource
                {
                    Name = part.Name,
                    Mesh = mesh,
                    Materials = new[] { materials[part.Material] }
                });
            }

            return ProductPrefabBuilder.Build("AeroPulse", sources, kit.Tuning, ProductPrefabPath);
        }

        // Base colours for the untinted parts, looked up by UV: outsole, plate, insole, spare.
        private static Texture2D Palette()
        {
            string path = ProductDir + "/Materials/Palette.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }

            var texture = new Texture2D(4, 1, TextureFormat.RGBA32, false);
            texture.SetPixels(new[]
            {
                new Color(0.16f, 0.17f, 0.185f), new Color(0.085f, 0.09f, 0.1f),
                new Color(0.8f, 0.81f, 0.82f), BrandAssetBuilder.Volt
            });
            texture.Apply();

            Texture2D saved = SaveTexture(path, texture, false);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path) ?? saved;
        }

        private static ProductDefinition AeroPulse()
        {
            var definition = ScriptableObject.CreateInstance<ProductDefinition>();
            definition.id = "aero-pulse";
            definition.displayName = "AERO Pulse";
            definition.tagline = "Run on air.";
            definition.price = "$160";
            definition.ctaUrl = string.Empty;
            definition.sweepAxis = Vector3.left; // toe to heel
            definition.footprintSize = new Vector2(ShoeProfile.Length * 1.14f, ShoeProfile.Length * 0.57f);

            // Colours a brand would actually ship: soft, a little dusty, nothing that glows against a real room.
            var cloud = new ColorOption("Cloud", new Color(0.93f, 0.93f, 0.91f));
            var volt = new ColorOption("Volt", new Color(0.76f, 0.88f, 0.38f));
            var coral = new ColorOption("Coral", new Color(0.93f, 0.49f, 0.42f));
            var sky = new ColorOption("Sky", new Color(0.46f, 0.66f, 0.89f));
            var graphite = new ColorOption("Graphite", new Color(0.21f, 0.22f, 0.245f));
            var gum = new ColorOption("Gum", new Color(0.74f, 0.57f, 0.38f));

            definition.colorSlots = new[]
            {
                new ColorSlot
                {
                    name = "Upper", shaderProperty = "_TintR", smoothness = 0.25f,
                    colors = new[] { cloud, volt, coral, sky, graphite }
                },
                new ColorSlot
                {
                    name = "Sole", shaderProperty = "_TintG", smoothness = 0.42f,
                    colors = new[] { cloud, gum, volt, coral, graphite }
                },
                new ColorSlot
                {
                    name = "Laces", shaderProperty = "_TintB", smoothness = 0.2f,
                    colors = new[] { cloud, graphite, volt, coral, sky }
                }
            };

            definition.finishSlot = 0;
            definition.finishes = new[]
            {
                new FinishOption { name = "Matte", metallic = 0f, smoothness = 0.25f },
                new FinishOption { name = "Metallic", metallic = 0.85f, smoothness = 0.7f }
            };

            definition.presets = new[]
            {
                new Colorway { name = "Launch", colorIndices = new[] { 0, 2, 1 }, finishIndex = 0 },
                new Colorway { name = "Night Run", colorIndices = new[] { 4, 2, 2 }, finishIndex = 1 },
                new Colorway { name = "Sunset", colorIndices = new[] { 2, 1, 0 }, finishIndex = 0 }
            };

            definition.hotspots = new[]
            {
                new HotspotData
                {
                    title = "Cushioned sole", text = "Responsive foam returns energy with every step.",
                    partName = "Midsole", localPosition = OnSide(0.30f, 0.024f, 0.006f)
                },
                new HotspotData
                {
                    title = "Breathable mesh", text = "Engineered knit keeps air moving.",
                    partName = "Upper",
                    localPosition = new Vector3(ShoeProfile.X(0.74f),
                        ShoeProfile.UpperBaseY(0.74f) + ShoeProfile.RoofHeight(0.74f) + 0.006f, ShoeProfile.CentreZ(0.74f))
                },
                new HotspotData
                {
                    title = "Carbon plate", text = "A full-length plate for snap at toe-off.",
                    partName = "Midsole", localPosition = OnSide(0.62f, 0.02f, 0.006f)
                },
                new HotspotData
                {
                    title = "Reflective heel", text = "Seen from 150 m at night.",
                    partName = "HeelCounter", localPosition = new Vector3(ShoeProfile.X(0f) - 0.008f, 0.062f, 0f)
                },
                new HotspotData
                {
                    title = "Grip outsole", text = "Rubber lugs that hold on wet roads.",
                    partName = "Outsole", localPosition = new Vector3(ShoeProfile.X(0.6f), -0.006f, ShoeProfile.CentreZ(0.6f))
                }
            };

            // Everything rises from the outsole, which stays put: nothing may sink into the user's table.
            definition.parts = new[]
            {
                Part("Outsole", "Rubber outsole", Vector3.up, 0f, 0.006f),
                Part("Midsole", "Foam midsole", Vector3.up, 0.055f, 0.024f),
                Part("Plate", "Carbon plate", Vector3.up, 0.115f, 0.022f),
                Part("Insole", "Cushioned insole", Vector3.up, 0.16f, 0.036f),
                Part("Upper", "Knit upper", Vector3.up, 0.215f, 0.095f),
                Part("HeelCounter", "Heel counter", new Vector3(-0.09f, 0.215f, 0f), 0.233f, 0.045f),
                Part("Tongue", "Padded tongue", new Vector3(0.02f, 0.31f, 0f), 0.311f, 0.1f),
                Part("Laces", "Flat laces", Vector3.up, 0.375f, 0.1f)
            };

            return definition;
        }

        private static ExplodePart Part(string name, string label, Vector3 direction, float distance, float labelHeight)
        {
            return new ExplodePart
            {
                partName = name, label = label, direction = direction, distance = distance, labelHeight = labelHeight
            };
        }

        // A point just off the shoe's -Z side, the side that faces the user.
        private static Vector3 OnSide(float u, float height, float offset)
        {
            return new Vector3(ShoeProfile.X(u), ShoeProfile.GroundY(u) + height,
                ShoeProfile.CentreZ(u) - ShoeProfile.HalfWidth(u) - offset);
        }

        // ---- The box ---------------------------------------------------------------------------

        private static PackagingBox BuildBox(BrandAssetBuilder.Kit kit)
        {
            var size = new Vector3(0.35f, 0.13f, 0.2f);
            var bodySize = new Vector3(size.x, size.y * 0.82f, size.z);
            var lidSize = new Vector3(size.x * 1.03f, size.y * 0.2f, size.z * 1.04f);
            Mesh bodyMesh = CreateOrReplace(BrandAssetBuilder.PackagingDir + "/BoxBody.asset", BoxMesh(bodySize));
            Mesh lidMesh = CreateOrReplace(BrandAssetBuilder.PackagingDir + "/BoxLid.asset", BoxMesh(lidSize));

            Material card = LoadOrCreate(BrandAssetBuilder.PackagingDir + "/Box.mat", () =>
            {
                var material = new Material(RequireShader("Universal Render Pipeline/Simple Lit")) { enableInstancing = true };
                material.SetTexture("_BaseMap", kit.BoxAlbedo);
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Smoothness", 0.2f);
                return material;
            });
            Material lidCard = LoadOrCreate(BrandAssetBuilder.PackagingDir + "/Lid.mat", () =>
            {
                var material = new Material(RequireShader("Universal Render Pipeline/Simple Lit")) { enableInstancing = true };
                material.SetTexture("_BaseMap", kit.LidAlbedo);
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Smoothness", 0.2f);
                return material;
            });
            Color glowColor = kit.Theme.primary;
            glowColor.a = 0.55f;
            Material glow = UnlitMaterial(BrandAssetBuilder.VfxDir + "/BoxGlow.mat", glowColor, true, kit.SoftBlob);

            var root = new GameObject("AeroBox");

            // Under a pivot at the base, so the body can shrink towards the surface it stands on, not towards its own middle.
            GameObject bodyPivot = Child("BodyPivot", root.transform);
            GameObject body = MeshObject("Body", bodyPivot.transform, bodyMesh, card, castShadows: true);
            body.transform.localPosition = new Vector3(0f, bodySize.y * 0.5f, 0f);

            GameObject lid = MeshObject("Lid", root.transform, lidMesh, lidCard, castShadows: true);
            lid.transform.localPosition = new Vector3(0f, size.y * 0.9f, 0f);

            GameObject halo = MeshObject("Glow", root.transform, FlatQuad(), glow);
            halo.transform.localPosition = new Vector3(0f, 0.0015f, 0f);
            halo.transform.localScale = new Vector3(size.x * 2.1f, 1f, size.z * 2.6f);

            ProductPrefabBuilder.AddBody(root);
            ProductPrefabBuilder.AddGrabVolume(root, new Bounds(new Vector3(0f, size.y * 0.5f, 0f), size));
            var grab = root.AddComponent<GrabTarget>();
            grab.IsGrabbable = false;
            ProductPrefabBuilder.AddGrab(root, grab, kit.Tuning, 1, 1f, 1f);

            var box = root.AddComponent<PackagingBox>();
            using (var wire = new Wire(box))
            {
                wire.Ref("body", bodyPivot.transform).Ref("lid", lid.transform)
                    .Ref("glow", halo.GetComponent<Renderer>()).Ref("grab", grab).Vector("size", size);
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BoxPrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<PackagingBox>();
        }

        public static Mesh FlatQuad()
        {
            return LoadOrCreate(BrandAssetBuilder.VfxDir + "/FlatQuad.asset", FlatQuadMesh);
        }

        // ---- Hotspot ---------------------------------------------------------------------------

        // A world-space canvas whose origin is the dot, so placing the prefab places the dot. The label
        // card opens above it.
        private static HotspotView BuildHotspot(BrandAssetBuilder.Kit kit)
        {
            BrandTheme theme = kit.Theme;
            const float width = 176f;
            const float height = 128f;
            const float dotSize = 30f;
            const float dotCentre = 18f; // from the bottom edge

            Canvas canvas = WorldCanvas("Hotspot", null, new Vector2(width, height));
            var rect = (RectTransform)canvas.transform;
            rect.pivot = new Vector2(0.5f, dotCentre / height);

            // The dot: a ring with a solid centre, big enough to poke without aiming.
            GameObject dot = Child("Dot", canvas.transform, typeof(RectTransform), typeof(CanvasGroup), typeof(Image),
                typeof(Button));
            PlaceCentred((RectTransform)dot.transform, (width - dotSize) * 0.5f, height - dotCentre - dotSize * 0.5f,
                dotSize, dotSize);
            var hit = dot.GetComponent<Image>();
            hit.sprite = kit.Shape;
            hit.material = kit.UiMaterial;
            hit.color = new Color(theme.secondary.r, theme.secondary.g, theme.secondary.b, 0.55f);
            hit.raycastTarget = true;

            var button = dot.GetComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            Image ring = AddImage("Ring", dot.transform, kit.Ring, theme.primary, kit.UiMaterial);
            Stretch(ring.rectTransform);
            Image centre = AddImage("Centre", dot.transform, kit.Shape, theme.primary, kit.UiMaterial);
            Stretch(centre.rectTransform, dotSize * 0.3f);

            // The card.
            const float cardHeight = 84f;
            GameObject card = Child("Card", canvas.transform, typeof(RectTransform), typeof(CanvasGroup));
            var cardRect = (RectTransform)card.transform;
            Place(cardRect, 0f, 0f, width, cardHeight);
            // Grows from its bottom edge, out of the dot.
            cardRect.pivot = new Vector2(0.5f, 0f);
            cardRect.anchoredPosition = new Vector2(width * 0.5f, -cardHeight);

            Image background = AddImage("Background", card.transform, kit.Shape, theme.panel, kit.UiMaterial, 12f);
            Stretch(background.rectTransform);
            TextMeshProUGUI title = AddText("Title", card.transform, "Feature", 17f, theme.text,
                TextAlignmentOptions.TopLeft, 14f, 11f, width - 28f, 22f, false, FontStyles.Bold);
            TextMeshProUGUI text = AddText("Text", card.transform, "One short line.", 13.5f, theme.mutedText,
                TextAlignmentOptions.TopLeft, 14f, 35f, width - 28f, 40f, true);
            Image icon = AddImage("Icon", card.transform, null, theme.primary, kit.UiMaterial);
            Place(icon.rectTransform, width - 36f, 10f, 22f, 22f);
            icon.gameObject.SetActive(false);

            var view = canvas.gameObject.AddComponent<HotspotView>();
            using (var wire = new Wire(view))
            {
                wire.Ref("dotButton", button).Ref("dot", dot.transform)
                    .Ref("dotGroup", dot.GetComponent<CanvasGroup>()).Ref("card", card.GetComponent<CanvasGroup>())
                    .Ref("titleText", title).Ref("bodyText", text).Ref("icon", icon);
            }

            // The whole-canvas group isn't needed here: the dot and the card fade on their own.
            Object.DestroyImmediate(canvas.GetComponent<CanvasGroup>());
            MakeInteractable(canvas);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(canvas.gameObject, HotspotPrefabPath);
            Object.DestroyImmediate(canvas.gameObject);
            return prefab.GetComponent<HotspotView>();
        }

        // ---- Part label ------------------------------------------------------------------------

        private static PartLabel BuildLabel(BrandAssetBuilder.Kit kit)
        {
            BrandTheme theme = kit.Theme;
            var root = new GameObject("PartLabel");
            Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

            // The label stretches the line from the text to the dot; the dot is drawn over the line's end.
            Material lineMaterial = CalloutMaterial(BrandAssetBuilder.BrandDir + "/UI/CalloutLine.mat", theme.text,
                kit.CalloutLine);
            GameObject line = MeshObject("Line", root.transform, quad, lineMaterial);
            line.transform.localScale = new Vector3(0.03f, 0.004f, 1f);

            Material dotMaterial = CalloutMaterial(BrandAssetBuilder.BrandDir + "/UI/CalloutDot.mat", theme.primary,
                kit.CalloutDot);
            GameObject dot = MeshObject("Dot", root.transform, quad, dotMaterial);
            dot.transform.localScale = Vector3.one * 0.014f;
            dot.GetComponent<Renderer>().sortingOrder = 1;

            TextMeshPro text = WorldText("Text", root.transform, 15f, theme.text, TextAlignmentOptions.Left,
                new Vector2(220f, 24f));
            text.rectTransform.pivot = new Vector2(1f, 0.5f);
            text.alignment = TextAlignmentOptions.Right;
            text.transform.localPosition = new Vector3(-0.008f, 0f, 0f);
            text.fontStyle = FontStyles.Bold;
            text.text = "Part";
            UseOutline(text);

            var label = root.AddComponent<PartLabel>();
            using (var wire = new Wire(label))
            {
                wire.Ref("text", text).Ref("dot", dot.GetComponent<Renderer>()).Ref("line", line.GetComponent<Renderer>());
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, LabelPrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<PartLabel>();
        }

        private static Material CalloutMaterial(string path, Color color, Texture texture)
        {
            return LoadOrCreate(path, () =>
            {
                var material = new Material(RequireShader("MRShowroom/Callout")) { enableInstancing = true };
                material.SetColor("_BaseColor", color);
                material.SetTexture("_BaseMap", texture);
                return material;
            });
        }

        // ---- Shared ----------------------------------------------------------------------------

        private const string OutlineMaterialPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat";

        /// <summary>
        /// A dark outline keeps free-floating text readable over a bright room. Uses the outline preset
        /// that ships with TextMeshPro, as a shared asset: setting the outline on the text itself would
        /// create a material that exists only in the scene.
        /// </summary>
        public static void UseOutline(TMP_Text text)
        {
            var outline = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (outline != null && text.font == TMP_Settings.defaultFontAsset)
            {
                text.fontSharedMaterial = outline;
            }
        }

        /// <summary>Poke for direct touch, ray for pinch-at-a-distance. Returns the objects the SDK added.</summary>
        public static List<GameObject> MakeInteractable(Canvas canvas)
        {
            var created = new List<GameObject>();
            created.AddRange(QuickActionsAPI.AddPokeCanvasInteraction(canvas.gameObject, false));
            created.AddRange(QuickActionsAPI.AddRayCanvasInteraction(canvas.gameObject, false));
            return created;
        }
    }
}
