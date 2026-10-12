using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MRShowroom.EditorTools
{
    /// <summary>
    /// Sets private serialized fields by name, and fails loudly when a name is wrong. The builders
    /// wire everything through this so a renamed field can't silently leave a reference empty.
    /// </summary>
    internal sealed class Wire : IDisposable
    {
        private readonly SerializedObject _target;

        public Wire(Object target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            _target = new SerializedObject(target);
        }

        public Wire Ref(string field, Object value)
        {
            Find(field).objectReferenceValue = value;
            return this;
        }

        public Wire Refs<T>(string field, IReadOnlyList<T> values) where T : Object
        {
            SerializedProperty array = Find(field);
            array.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            return this;
        }

        public Wire Float(string field, float value)
        {
            Find(field).floatValue = value;
            return this;
        }

        public Wire Int(string field, int value)
        {
            Find(field).intValue = value;
            return this;
        }

        public Wire Bool(string field, bool value)
        {
            Find(field).boolValue = value;
            return this;
        }

        public Wire Text(string field, string value)
        {
            Find(field).stringValue = value;
            return this;
        }

        public Wire Color(string field, Color value)
        {
            Find(field).colorValue = value;
            return this;
        }

        public Wire Vector(string field, Vector3 value)
        {
            Find(field).vector3Value = value;
            return this;
        }

        public Wire Bounds(string field, Bounds value)
        {
            Find(field).boundsValue = value;
            return this;
        }

        /// <summary>For nested data: a serialized property by its full path.</summary>
        public SerializedProperty Find(string field)
        {
            SerializedProperty property = _target.FindProperty(field);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{_target.targetObject.GetType().Name} has no serialized field named '{field}'.");
            }

            return property;
        }

        public void Dispose()
        {
            _target.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(_target.targetObject);
        }
    }

    internal static class BuilderUtil
    {
        public const string Root = "Assets/_Project";
        public const string ArtDir = Root + "/Art";
        public const string DataDir = Root + "/Data";
        public const string PrefabsDir = Root + "/Prefabs";

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        /// <summary>Loads the asset at a path, or creates it. Existing assets are never overwritten.</summary>
        public static T LoadOrCreate<T>(string path, Func<T> create) where T : Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            T created = create();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        /// <summary>Creates the asset at a path, replacing what was there but keeping its GUID so references survive.</summary>
        public static T CreateOrReplace<T>(string path, T asset) where T : Object
        {
            EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(asset, path);
                return asset;
            }

            asset.name = existing.name;
            EditorUtility.CopySerialized(asset, existing);
            Object.DestroyImmediate(asset);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        public static GameObject Child(string name, Transform parent, params Type[] components)
        {
            var child = new GameObject(name, components);
            child.transform.SetParent(parent, false);
            return child;
        }

        public static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == name)
                {
                    return candidate;
                }
            }

            return null;
        }

        public static Shader RequireShader(string name)
        {
            Shader shader = Shader.Find(name);
            if (shader == null)
            {
                throw new InvalidOperationException($"Shader '{name}' not found.");
            }

            return shader;
        }

        // ---- Materials -------------------------------------------------------------------------

        public static Material LitMaterial(string path, Color color, float smoothness)
        {
            return LoadOrCreate(path, () =>
            {
                var material = new Material(RequireShader("Universal Render Pipeline/Simple Lit"))
                {
                    enableInstancing = true
                };
                material.SetColor("_BaseColor", color);
                material.SetColor("_SpecColor", new Color(0.18f, 0.18f, 0.18f, 1f));
                material.SetFloat("_Smoothness", smoothness);
                return material;
            });
        }

        /// <param name="texture">Optional. For sprites and glows, its alpha shapes the quad.</param>
        public static Material UnlitMaterial(string path, Color color, bool transparent, Texture texture = null)
        {
            return LoadOrCreate(path, () =>
            {
                var material = new Material(RequireShader("Universal Render Pipeline/Unlit"))
                {
                    enableInstancing = true
                };
                material.SetColor("_BaseColor", color);
                if (texture != null)
                {
                    material.SetTexture("_BaseMap", texture);
                }

                if (transparent)
                {
                    MakeTransparent(material);
                }

                return material;
            });
        }

        public static void MakeTransparent(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            // Alpha accumulates properly, so transparent objects composite over passthrough.
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("SHADOWCASTER", false);
        }

        // ---- Meshes ----------------------------------------------------------------------------

        /// <summary>A 1 x 1 quad lying flat (in XZ), facing up. For shadows, footprints and reticles on a surface.</summary>
        public static Mesh FlatQuadMesh()
        {
            var mesh = new Mesh { name = "FlatQuad" };
            mesh.SetVertices(new List<Vector3>
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f)
            });
            mesh.SetUVs(0, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f)
            });
            mesh.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A box of the given size, centred on the origin. Each face's UVs keep the texture square and
        /// centred (running past 0-1 on the long side), so a clamped texture with a logo in the middle
        /// shows it undistorted on every face.
        /// </summary>
        public static Mesh BoxMesh(Vector3 size)
        {
            Vector3 h = size * 0.5f;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            void Face(Vector3 normal, Vector3 right, Vector3 up, float width, float height)
            {
                Vector3 centre = Vector3.Scale(normal, h);
                Vector3 r = right * (width * 0.5f);
                Vector3 u = up * (height * 0.5f);
                float shorter = Mathf.Min(width, height);
                float du = width / shorter * 0.5f;
                float dv = height / shorter * 0.5f;

                int start = vertices.Count;
                vertices.Add(centre - r - u);
                vertices.Add(centre - r + u);
                vertices.Add(centre + r + u);
                vertices.Add(centre + r - u);
                uvs.Add(new Vector2(0.5f - du, 0.5f - dv));
                uvs.Add(new Vector2(0.5f - du, 0.5f + dv));
                uvs.Add(new Vector2(0.5f + du, 0.5f + dv));
                uvs.Add(new Vector2(0.5f + du, 0.5f - dv));
                for (int i = 0; i < 4; i++)
                {
                    normals.Add(normal);
                }

                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            Face(Vector3.back, Vector3.right, Vector3.up, size.x, size.y);
            Face(Vector3.forward, Vector3.left, Vector3.up, size.x, size.y);
            Face(Vector3.left, Vector3.back, Vector3.up, size.z, size.y);
            Face(Vector3.right, Vector3.forward, Vector3.up, size.z, size.y);
            Face(Vector3.up, Vector3.right, Vector3.forward, size.x, size.z);
            Face(Vector3.down, Vector3.right, Vector3.back, size.x, size.z);

            var mesh = new Mesh { name = "Box" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>An upright cylinder (axis along Y) filling a box of the given size.</summary>
        public static Mesh CylinderMesh(Vector3 size, int segments = 32)
        {
            Vector3 h = size * 0.5f;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            // Side: smooth normals, one extra column to close the seam.
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices.Add(new Vector3(outward.x * h.x, -h.y, outward.z * h.z));
                vertices.Add(new Vector3(outward.x * h.x, h.y, outward.z * h.z));
                normals.Add(outward);
                normals.Add(outward);
            }

            for (int i = 0; i < segments; i++)
            {
                int bottom0 = i * 2;
                int top0 = bottom0 + 1;
                int bottom1 = bottom0 + 2;
                int top1 = bottom0 + 3;
                triangles.AddRange(new[] { bottom0, top0, bottom1, top0, top1, bottom1 });
            }

            // Caps: their own vertices so the rim stays a hard edge.
            foreach (float sign in new[] { 1f, -1f })
            {
                int centre = vertices.Count;
                vertices.Add(new Vector3(0f, sign * h.y, 0f));
                normals.Add(new Vector3(0f, sign, 0f));

                for (int i = 0; i < segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / segments;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * h.x, sign * h.y, Mathf.Sin(angle) * h.z));
                    normals.Add(new Vector3(0f, sign, 0f));
                }

                for (int i = 0; i < segments; i++)
                {
                    int current = centre + 1 + i;
                    int next = centre + 1 + (i + 1) % segments;
                    triangles.AddRange(sign > 0f ? new[] { centre, next, current } : new[] { centre, current, next });
                }
            }

            var mesh = new Mesh { name = "Cylinder" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material material,
            bool castShadows = false)
        {
            GameObject go = Child(name, parent, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        // ---- Textures --------------------------------------------------------------------------

        /// <summary>
        /// Writes a texture to disk as a PNG and imports it, as a sprite when <paramref name="sprite"/> is set.
        /// Returns the imported asset, not the texture that was passed in.
        /// </summary>
        public static Texture2D SaveTexture(string path, Texture2D texture, bool sprite, Vector4 border = default,
            float pixelsPerUnit = 100f, bool clamp = true)
        {
            EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (sprite)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = border;
                importer.spritePixelsPerUnit = pixelsPerUnit;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                // A full rectangle, not a mesh hugged to the alpha: sliced images need the whole quad.
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
            }

            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---- World-space UI --------------------------------------------------------------------
        // Canvases are authored in millimetres and scaled by 0.001, so a font size of 24 is 24 mm tall.

        public const float CanvasScale = 0.001f;

        public static Canvas WorldCanvas(string name, Transform parent, Vector2 sizeMillimetres)
        {
            GameObject go = Child(name, parent, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = sizeMillimetres;
            rect.localScale = Vector3.one * CanvasScale;
            return canvas;
        }

        /// <summary>Places a rect by its top-left corner, in canvas units measured from the parent's top-left.</summary>
        public static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>Places a rect by its centre, so it can be scaled (pulsed) about its middle.</summary>
        public static void PlaceCentred(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x + width * 0.5f, -(y + height * 0.5f));
            rect.sizeDelta = new Vector2(width, height);
        }

        public static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        public static GameObject AddGroup(string name, Transform parent, float x, float y, float width, float height)
        {
            GameObject go = Child(name, parent, typeof(RectTransform));
            Place((RectTransform)go.transform, x, y, width, height);
            return go;
        }

        /// <param name="corner">
        /// Corner radius in canvas units, for an image drawn with the shape sprite. Zero draws the sprite
        /// whole: a circle.
        /// </param>
        public static Image AddImage(string name, Transform parent, Sprite sprite, Color color, Material material,
            float corner = 0f, bool raycastTarget = false)
        {
            GameObject go = Child(name, parent, typeof(RectTransform), typeof(Image));
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.material = material;
            image.raycastTarget = raycastTarget;
            Round(image, corner);
            return image;
        }

        /// <summary>Slices the shape sprite so its corners come out at the given radius.</summary>
        public static void Round(Image image, float corner)
        {
            if (corner <= 0f)
            {
                image.type = Image.Type.Simple;
                return;
            }

            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = BrandAssetBuilder.ShapeCorner / corner;
        }

        public static TextMeshProUGUI AddText(string name, Transform parent, string text, float size, Color color,
            TextAlignmentOptions alignment, float x, float y, float width, float height, bool wrap = false,
            FontStyles style = FontStyles.Normal)
        {
            GameObject go = Child(name, parent, typeof(RectTransform));
            Place((RectTransform)go.transform, x, y, width, height);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.font = RequireFont();
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>A free-standing label in the world (not on a canvas), sized in millimetres like the canvases.</summary>
        public static TextMeshPro WorldText(string name, Transform parent, float sizeMillimetres, Color color,
            TextAlignmentOptions alignment, Vector2 boxMillimetres)
        {
            GameObject go = Child(name, parent);
            var label = go.AddComponent<TextMeshPro>();
            label.font = RequireFont();
            // World-space TMP: font size 10 is one unit tall. At a scale of 0.01, size N reads as N millimetres.
            label.rectTransform.sizeDelta = boxMillimetres * 0.1f;
            label.transform.localScale = Vector3.one * 0.01f;
            label.fontSize = sizeMillimetres;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.text = string.Empty;
            return label;
        }

        public static TMP_FontAsset RequireFont()
        {
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                throw new InvalidOperationException(
                    "TextMeshPro has no default font. Import the TMP Essential Resources first " +
                    "(Window > TextMeshPro > Import TMP Essential Resources).");
            }

            return font;
        }
    }
}
