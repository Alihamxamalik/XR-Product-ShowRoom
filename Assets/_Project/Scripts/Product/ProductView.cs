using System;
using UnityEngine;

namespace MRShowroom
{
    /// <summary>
    /// Sits on the root of a product prefab and describes how it is put together, so the showroom
    /// never has to search a model by guesswork. Built by the product prefab builder.
    ///
    /// Layout: root (grabbed as a whole) > Visual > one slot per part > the part's body.
    /// The exploded view moves the slots; a part that is picked up moves its body, and springs
    /// back to its slot when released.
    /// </summary>
    public sealed class ProductView : MonoBehaviour
    {
        [Serializable]
        public sealed class Part
        {
            public string name;
            public Transform slot;
            public Transform body;
            public GrabTarget grab;
            public ReturnToRest rest;
            public Renderer[] renderers = new Renderer[0];
        }

        [SerializeField] private Transform visualRoot;
        [SerializeField] private GrabTarget grab;
        [SerializeField] private ReturnToRest rest;
        [SerializeField] private Part[] parts = new Part[0];
        [SerializeField] private Renderer[] renderers = new Renderer[0];
        [Tooltip("Bounds of the assembled product in the root's space.")]
        [SerializeField] private Bounds localBounds = new Bounds(Vector3.zero, Vector3.one * 0.2f);

        public Transform VisualRoot => visualRoot;
        public GrabTarget Grab => grab;
        public ReturnToRest Rest => rest;
        public Part[] Parts => parts;
        public Renderer[] Renderers => renderers;
        public Bounds LocalBounds => localBounds;

        public Part FindPart(string partName)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].name == partName)
                {
                    return parts[i];
                }
            }

            return null;
        }

        public void SetVisible(bool visible)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = visible;
                }
            }
        }
    }
}
