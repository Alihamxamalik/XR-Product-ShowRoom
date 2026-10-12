using System.Collections.Generic;
using MRShowroom.MR;
using Oculus.Interaction;
using Oculus.Interaction.Editor.QuickActions;
using Oculus.Interaction.HandGrab;
using UnityEditor;
using UnityEngine;
using static MRShowroom.EditorTools.BuilderUtil;

namespace MRShowroom.EditorTools
{
    /// <summary>
    /// Turns a list of meshes into a product prefab the showroom can use: grabbable as a whole
    /// (one hand to move and turn, two to scale), with every part in its own slot so it can be
    /// exploded and picked up separately. Used for the built-in AERO Pulse and for any model
    /// brought in through MR Showroom > Product > Create Product From Selected Model.
    /// </summary>
    internal static class ProductPrefabBuilder
    {
        public sealed class PartSource
        {
            public string Name;
            public Mesh Mesh;
            public Material[] Materials;
            public Vector3 Position;
            public Quaternion Rotation = Quaternion.identity;
            public Vector3 Scale = Vector3.one;
        }

        // Grab volumes are bigger than what you see: a near miss should still pick the thing up.
        private const float GrabPadding = 1.25f;
        private const float MinGrabSize = 0.035f;

        /// <summary>Must be called with a scene open that has no camera rig in it; the SDK's quick actions would modify one.</summary>
        public static ProductView Build(string name, IReadOnlyList<PartSource> sources, ShowroomTuning tuning,
            string path)
        {
            var root = new GameObject(name);
            GameObject visual = Child("Visual", root.transform);

            var parts = new List<ProductView.Part>();
            var renderers = new List<Renderer>();
            var bounds = new Bounds();
            bool hasBounds = false;

            foreach (PartSource source in sources)
            {
                GameObject slot = Child("Slot_" + source.Name, visual.transform);
                slot.transform.localPosition = source.Position;
                slot.transform.localRotation = source.Rotation;
                slot.transform.localScale = source.Scale;

                GameObject body = MeshObject(source.Name, slot.transform, source.Mesh, null, castShadows: true);
                var renderer = body.GetComponent<MeshRenderer>();
                renderer.sharedMaterials = source.Materials;
                renderers.Add(renderer);

                AddBody(body);
                Bounds local = source.Mesh.bounds;
                AddGrabVolume(body, local);

                var grab = body.AddComponent<GrabTarget>();
                grab.IsGrabbable = false;
                var rest = body.AddComponent<ReturnToRest>();
                using (var wire = new Wire(rest))
                {
                    wire.Ref("target", grab).Bool("restoreScale", true);
                }

                // A part is held in one hand; it is never scaled on its own.
                AddGrab(body, grab, tuning, 1, 1f, 1f);

                parts.Add(new ProductView.Part
                {
                    name = source.Name,
                    slot = slot.transform,
                    body = body.transform,
                    grab = grab,
                    rest = rest,
                    renderers = new Renderer[] { renderer }
                });

                foreach (Vector3 corner in Corners(local))
                {
                    Vector3 point = root.transform.InverseTransformPoint(body.transform.TransformPoint(corner));
                    if (hasBounds)
                    {
                        bounds.Encapsulate(point);
                    }
                    else
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        hasBounds = true;
                    }
                }
            }

            // The whole product. Its own grab volume is the box around everything, a little generous.
            AddBody(root);
            AddGrabVolume(root, bounds);
            var rootGrab = root.AddComponent<GrabTarget>();
            var rootRest = root.AddComponent<ReturnToRest>();
            using (var wire = new Wire(rootRest))
            {
                wire.Ref("target", rootGrab).Bool("restoreScale", false)
                    .Float("frequency", tuning.returnFrequency).Float("damping", tuning.returnDamping)
                    .Float("trackingLostHold", tuning.trackingLostHold);
            }

            AddGrab(root, rootGrab, tuning, -1, tuning.minScale, tuning.maxScale);

            var view = root.AddComponent<ProductView>();
            using (var wire = new Wire(view))
            {
                wire.Ref("visualRoot", visual.transform).Ref("grab", rootGrab).Ref("rest", rootRest)
                    .Refs("renderers", renderers).Bounds("localBounds", bounds);

                SerializedProperty array = wire.Find("parts");
                array.arraySize = parts.Count;
                for (int i = 0; i < parts.Count; i++)
                {
                    SerializedProperty entry = array.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("name").stringValue = parts[i].name;
                    entry.FindPropertyRelative("slot").objectReferenceValue = parts[i].slot;
                    entry.FindPropertyRelative("body").objectReferenceValue = parts[i].body;
                    entry.FindPropertyRelative("grab").objectReferenceValue = parts[i].grab;
                    entry.FindPropertyRelative("rest").objectReferenceValue = parts[i].rest;
                    SerializedProperty partRenderers = entry.FindPropertyRelative("renderers");
                    partRenderers.arraySize = parts[i].renderers.Length;
                    for (int r = 0; r < parts[i].renderers.Length; r++)
                    {
                        partRenderers.GetArrayElementAtIndex(r).objectReferenceValue = parts[i].renderers[r];
                    }
                }
            }

            EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<ProductView>();
        }

        /// <summary>
        /// Adds the Interaction SDK's grab components (pinch and palm grab for hands, grip for
        /// controllers) and the bridge that reports them to the <see cref="GrabTarget"/>.
        /// </summary>
        /// <param name="maxGrabPoints">1 for one hand only, -1 for one or two.</param>
        public static void AddGrab(GameObject target, GrabTarget grab, ShowroomTuning tuning, int maxGrabPoints,
            float minScale, float maxScale)
        {
            List<GameObject> created = QuickActionsAPI.AddGrabInteraction(target);
            var grabbable = target.GetComponent<Grabbable>();
            if (grabbable == null || created.Count == 0)
            {
                throw new System.InvalidOperationException(
                    "The Interaction SDK did not add its grab components to " + target.name + ".");
            }

            // Nothing here is driven by physics: a released product springs home, it doesn't fall or fly.
            grabbable.InjectOptionalKinematicWhileSelected(false);
            grabbable.InjectOptionalThrowWhenUnselected(false);
            grabbable.MaxGrabPoints = maxGrabPoints;

            // Added now, so nothing is created on the first grab. The same transformer serves one hand
            // (move and turn) and two (move, turn and scale, within the limits).
            var transformer = target.AddComponent<GrabFreeTransformer>();
            transformer.InjectOptionalScaleConstraints(new TransformerUtils.ScaleConstraints
            {
                ConstraintsAreRelative = true,
                XAxis = Limit(minScale, maxScale),
                YAxis = Limit(minScale, maxScale),
                ZAxis = Limit(minScale, maxScale)
            });
            grabbable.InjectOptionalOneGrabTransformer(transformer);
            grabbable.InjectOptionalTwoGrabTransformer(transformer);
            EditorUtility.SetDirty(transformer);
            EditorUtility.SetDirty(grabbable);

            GameObject interactables = created[0];
            var handGrab = interactables.GetComponent<HandGrabInteractable>();
            if (handGrab != null)
            {
                handGrab.InjectOptionalMovementProvider(interactables.AddComponent<MoveTowardsTargetProvider>());
                EditorUtility.SetDirty(handGrab);
            }

            var bridge = target.AddComponent<InteractionGrabBridge>();
            using (var wire = new Wire(bridge))
            {
                wire.Ref("target", grab).Ref("grabbable", grabbable).Ref("interactables", interactables)
                    .Float("trackingLostTime", tuning.trackingLostHold);
            }
        }

        private static TransformerUtils.ConstrainedAxis Limit(float min, float max)
        {
            return new TransformerUtils.ConstrainedAxis
            {
                ConstrainAxis = true,
                AxisRange = new TransformerUtils.FloatRange { Min = min, Max = max }
            };
        }

        public static void AddBody(GameObject target)
        {
            var body = target.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;
        }

        public static void AddGrabVolume(GameObject target, Bounds bounds)
        {
            var box = target.AddComponent<BoxCollider>();
            box.center = bounds.center;
            Vector3 size = bounds.size * GrabPadding;
            box.size = new Vector3(Mathf.Max(size.x, MinGrabSize), Mathf.Max(size.y, MinGrabSize),
                Mathf.Max(size.z, MinGrabSize));
        }

        private static IEnumerable<Vector3> Corners(Bounds bounds)
        {
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        yield return bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                    }
                }
            }
        }
    }
}
