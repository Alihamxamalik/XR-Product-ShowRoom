using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static MRShowroom.EditorTools.BuilderUtil;

namespace MRShowroom.EditorTools
{
    /// <summary>
    /// Builds the ghost hands that demonstrate each gesture. They reuse the hand model that ships
    /// with the Interaction SDK, so there is no hand art to make or license. The model's own axes
    /// and each knuckle's bending axis are worked out from the skeleton here, so GhostHand never
    /// has to know how the model was exported.
    /// </summary>
    internal static class HintAssetBuilder
    {
        public const string RightHandPath = PrefabsDir + "/Hints/GhostHand_Right.prefab";
        public const string LeftHandPath = PrefabsDir + "/Hints/GhostHand_Left.prefab";

        private const string RightModel = "Packages/com.meta.xr.sdk.interaction/Runtime/Meshes/OpenXRRightHand.fbx";
        private const string LeftModel = "Packages/com.meta.xr.sdk.interaction/Runtime/Meshes/OpenXRLeftHand.fbx";
        private const string MaterialPath = PrefabsDir + "/Hints/GhostHand.mat";

        // Wrist to the tip of the middle finger, for an adult hand (m).
        private const float HandLength = 0.185f;

        private static readonly string[] Fingers = { "Middle", "Ring", "Little" };
        private static readonly string[] Knuckles = { "Proximal", "Intermediate", "Distal" };

        /// <summary>Call with a scene open; the hands are assembled in it and then removed.</summary>
        public static void Build(bool rebuildPrefabs)
        {
            EnsureFolder(PrefabsDir + "/Hints");
            Material material = LoadOrCreate(MaterialPath, () => new Material(RequireShader("MRShowroom/Ghost"))
            {
                enableInstancing = true
            });

            BuildHand(RightModel, RightHandPath, true, material, rebuildPrefabs);
            BuildHand(LeftModel, LeftHandPath, false, material, rebuildPrefabs);
        }

        /// <summary>Puts a ghost hand in the scene. Returns null if its prefab hasn't been built; the hints then run without it.</summary>
        public static GhostHand Instantiate(string path, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GhostHand>(path);
            return prefab != null ? (GhostHand)PrefabUtility.InstantiatePrefab(prefab, parent) : null;
        }

        private static void BuildHand(string modelPath, string prefabPath, bool right, Material material, bool rebuild)
        {
            if (!rebuild && AssetDatabase.LoadAssetAtPath<GhostHand>(prefabPath) != null)
            {
                return;
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogWarning($"[MR Showroom] Hand model not found at {modelPath}; the gesture hints will show " +
                                 "their prompts without a ghost hand.");
                return;
            }

            var root = new GameObject(right ? "GhostHand_Right" : "GhostHand_Left");
            GameObject hand = Object.Instantiate(model, root.transform);
            hand.name = "Model";

            Transform wrist = FindDeep(hand.transform, "XRHand_Wrist");
            Transform palm = FindDeep(hand.transform, "XRHand_Palm");
            Transform middleTip = FindDeep(hand.transform, "XRHand_MiddleTip");
            Transform indexBase = FindDeep(hand.transform, "XRHand_IndexProximal");
            Transform littleBase = FindDeep(hand.transform, "XRHand_LittleProximal");
            if (wrist == null || palm == null || middleTip == null || indexBase == null || littleBase == null)
            {
                Debug.LogWarning($"[MR Showroom] {modelPath} doesn't have the expected skeleton; skipped the ghost hand.");
                Object.DestroyImmediate(root);
                return;
            }

            // However the model was exported, make it the size of a hand: wrist to middle fingertip.
            float length = (middleTip.position - wrist.position).magnitude;
            if (length > 0.0001f)
            {
                hand.transform.localScale *= HandLength / length;
            }

            // The hand's own frame, read off the skeleton: fingers forward, back of the hand up.
            Vector3 forward = (middleTip.position - wrist.position).normalized;
            Vector3 towardsThumb = (indexBase.position - littleBase.position).normalized;
            Vector3 up = right ? Vector3.Cross(towardsThumb, forward) : Vector3.Cross(forward, towardsThumb);
            up = up.normalized;
            forward = Vector3.Cross(Vector3.Cross(up, forward), up).normalized;

            // Turn and shift the model so that frame becomes the prefab root's: +Z fingers, +Y back of hand, origin at the palm.
            Quaternion toRoot = Quaternion.Inverse(Quaternion.LookRotation(forward, up));
            hand.transform.rotation = toRoot * hand.transform.rotation;
            hand.transform.position -= palm.position - root.transform.position;

            var renderers = new List<Renderer>();
            foreach (SkinnedMeshRenderer skin in hand.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.sharedMaterial = material;
                skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                skin.receiveShadows = false;
                skin.updateWhenOffscreen = true;
                renderers.Add(skin);
            }

            // Fingers close towards the palm side of the hand; the thumb closes across it, towards the fingers.
            Vector3 palmSide = -root.transform.up;
            var fingerJoints = new List<Transform>();
            var fingerAxes = new List<Vector3>();
            foreach (string finger in Fingers)
            {
                Collect(hand.transform, finger, Knuckles, palmSide, fingerJoints, fingerAxes);
            }

            var indexJoints = new List<Transform>();
            var indexAxes = new List<Vector3>();
            Collect(hand.transform, "Index", Knuckles, palmSide, indexJoints, indexAxes);

            var thumbJoints = new List<Transform>();
            var thumbAxes = new List<Vector3>();
            Vector3 acrossPalm = (littleBase.position - indexBase.position).normalized + palmSide * 0.6f;
            Collect(hand.transform, "Thumb", new[] { "Metacarpal", "Proximal", "Distal" }, acrossPalm.normalized,
                thumbJoints, thumbAxes);

            var ghost = root.AddComponent<GhostHand>();
            using (var wire = new Wire(ghost))
            {
                wire.Refs("renderers", renderers).Refs("fingerJoints", fingerJoints).Refs("indexJoints", indexJoints)
                    .Refs("thumbJoints", thumbJoints);
                Vectors(wire.Find("fingerAxes"), fingerAxes);
                Vectors(wire.Find("indexAxes"), indexAxes);
                Vectors(wire.Find("thumbAxes"), thumbAxes);
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
        }

        // For each knuckle of a finger: the joint, and the axis (in the joint's own space) that swings
        // the rest of the finger towards the given direction.
        private static void Collect(Transform hand, string finger, string[] knuckles, Vector3 towards,
            List<Transform> joints, List<Vector3> axes)
        {
            for (int i = 0; i < knuckles.Length; i++)
            {
                Transform joint = FindDeep(hand, "XRHand_" + finger + knuckles[i]);
                if (joint == null || joint.childCount == 0)
                {
                    continue;
                }

                Vector3 bone = (joint.GetChild(0).position - joint.position).normalized;
                Vector3 axis = Vector3.Cross(bone, towards);
                if (axis.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                axis.Normalize();
                // Whichever way round the cross product came out, make a positive angle bend towards the target.
                if (Vector3.Dot(Quaternion.AngleAxis(20f, axis) * bone, towards) < Vector3.Dot(bone, towards))
                {
                    axis = -axis;
                }

                joints.Add(joint);
                axes.Add(joint.InverseTransformDirection(axis));
            }
        }

        private static void Vectors(SerializedProperty array, List<Vector3> values)
        {
            array.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                array.GetArrayElementAtIndex(i).vector3Value = values[i];
            }
        }
    }
}
