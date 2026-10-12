using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static MRShowroom.EditorTools.BuilderUtil;

namespace MRShowroom.EditorTools
{
    /// <summary>
    /// "A new product is a new model and one data asset." Select a model whose parts are separate
    /// meshes (outsole, midsole, upper and so on, split in Blender), run this, and it produces:
    ///   a product prefab, grabbable as a whole and part by part;
    ///   Product Lit copies of the model's materials, ready for a tint mask;
    ///   a product definition with one explode entry per part, to finish in the inspector.
    /// Assign the definition to the Product object in the Showroom scene to see it in the experience.
    /// </summary>
    internal static class ProductImportTool
    {
        [MenuItem("MR Showroom/Product/Create Product From Selected Model", priority = 40)]
        public static void CreateFromSelection()
        {
            GameObject model = Selection.activeGameObject;
            if (model == null)
            {
                EditorUtility.DisplayDialog("Create product", "Select a model first: an FBX in the Project window, " +
                    "or an instance of one in the scene.", "OK");
                return;
            }

            MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0)
            {
                EditorUtility.DisplayDialog("Create product", $"'{model.name}' has no meshes. Skinned meshes aren't " +
                    "supported: export the product as plain, separate meshes.", "OK");
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode
                || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            string name = ObjectNames.NicifyVariableName(model.name).Replace(" ", string.Empty);
            string artDir = $"{ArtDir}/Products/{name}";
            string prefabPath = $"{PrefabsDir}/Product/{name}.prefab";
            string definitionPath = $"{DataDir}/Products/{name}.asset";
            if (File.Exists(prefabPath) && !EditorUtility.DisplayDialog("Create product",
                    $"{prefabPath} already exists. Replace it?", "Replace", "Cancel"))
            {
                return;
            }

            // Everything needed is read off the model now: the scene it may be standing in is about to close.
            var sources = new List<ProductPrefabBuilder.PartSource>();
            var converted = new Dictionary<Material, Material>();
            var names = new HashSet<string>();
            Transform root = model.transform;
            foreach (MeshFilter filter in filters)
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || renderer == null)
                {
                    continue;
                }

                string partName = filter.name;
                for (int n = 2; !names.Add(partName); n++)
                {
                    partName = filter.name + n;
                }

                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = Convert(materials[i], artDir, converted);
                }

                Matrix4x4 local = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                sources.Add(new ProductPrefabBuilder.PartSource
                {
                    Name = partName,
                    Mesh = filter.sharedMesh,
                    Materials = materials,
                    Position = local.GetColumn(3),
                    Rotation = local.rotation,
                    Scale = local.lossyScale
                });
            }

            string previousScene = SceneManager.GetActiveScene().path;

            // The SDK's quick actions need a scene without a camera rig in it.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BrandAssetBuilder.Kit kit = BrandAssetBuilder.Build();
            ProductView view = ProductPrefabBuilder.Build(name, sources, kit.Tuning, prefabPath);

            ProductDefinition definition = LoadOrCreate(definitionPath, () =>
            {
                var created = ScriptableObject.CreateInstance<ProductDefinition>();
                created.id = name.ToLowerInvariant();
                created.displayName = ObjectNames.NicifyVariableName(model.name);
                created.parts = DefaultParts(view);
                return created;
            });
            definition.prefab = view;
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();

            if (!string.IsNullOrEmpty(previousScene))
            {
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            }

            Selection.activeObject = definition;
            EditorGUIUtility.PingObject(definition);
            Debug.Log($"[MR Showroom] Created {prefabPath} and {definitionPath} from '{model.name}' with {sources.Count} " +
                      "parts. Next: paint a tint mask (RGB = up to three colour regions) and assign it to the new " +
                      "materials, then fill in the colour slots, hotspots and explode directions on the definition.");
        }

        // The same texture and colour on the product shader, so the tint regions, the colour sweep and
        // occlusion work. One copy per source material, shared by every part that used it.
        private static Material Convert(Material source, string artDir, Dictionary<Material, Material> converted)
        {
            if (source == null)
            {
                source = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
            }

            if (converted.TryGetValue(source, out Material existing))
            {
                return existing;
            }

            string path = $"{artDir}/Materials/{source.name}.mat";
            Material material = LoadOrCreate(path, () =>
            {
                var created = new Material(RequireShader("MRShowroom/Product Lit")) { enableInstancing = true };
                if (source.HasProperty("_BaseMap"))
                {
                    created.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
                }
                else if (source.HasProperty("_MainTex"))
                {
                    created.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
                }

                if (source.HasProperty("_BaseColor"))
                {
                    created.SetColor("_BaseColor", source.GetColor("_BaseColor"));
                }
                else if (source.HasProperty("_Color"))
                {
                    created.SetColor("_BaseColor", source.GetColor("_Color"));
                }

                if (source.HasProperty("_Smoothness"))
                {
                    created.SetFloat("_Smoothness", source.GetFloat("_Smoothness"));
                }

                if (source.HasProperty("_Metallic"))
                {
                    created.SetFloat("_Metallic", source.GetFloat("_Metallic"));
                }

                return created;
            });

            converted.Add(source, material);
            return material;
        }

        // A starting point for the exploded view: every part moves straight out from the middle of the product.
        private static ExplodePart[] DefaultParts(ProductView view)
        {
            Vector3 centre = view.LocalBounds.center;
            float reach = Mathf.Max(view.LocalBounds.extents.magnitude, 0.05f);
            var parts = new ExplodePart[view.Parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                ProductView.Part part = view.Parts[i];
                Bounds bounds = part.renderers.Length > 0 && part.renderers[0] != null
                    ? part.renderers[0].bounds
                    : new Bounds(centre, Vector3.zero);
                Vector3 outwards = view.transform.InverseTransformPoint(bounds.center) - centre;
                if (outwards.sqrMagnitude < 0.000001f)
                {
                    outwards = Vector3.up;
                }

                parts[i] = new ExplodePart
                {
                    partName = part.name,
                    label = ObjectNames.NicifyVariableName(part.name),
                    direction = outwards.normalized,
                    distance = reach * 0.8f,
                    labelHeight = part.slot.InverseTransformPoint(bounds.center).y
                };
            }

            return parts;
        }
    }
}
