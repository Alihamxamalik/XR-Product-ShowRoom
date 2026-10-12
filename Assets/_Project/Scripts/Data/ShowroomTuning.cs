using UnityEngine;

namespace MRShowroom
{
    /// <summary>
    /// The numbers behind the experience: pacing, reach, thresholds. Kept in one asset so the feel
    /// can be tuned in the inspector (and per client) without touching code.
    /// </summary>
    [CreateAssetMenu(menuName = "MR Showroom/Tuning", fileName = "ShowroomTuning")]
    public sealed class ShowroomTuning : ScriptableObject
    {
        [Header("Flow (seconds)")]
        [Tooltip("Inspect lasts at least this long, and moves on once the product has been picked up and put down.")]
        public float inspectMin = 6f;
        [Tooltip("Inspect moves on after this long whatever the user did.")]
        public float inspectMax = 18f;
        [Tooltip("Hotspots appear this long into Inspect, or as soon as the product is first picked up.")]
        public float hotspotDelay = 4f;
        public float customiseMin = 6f;
        public float customiseMax = 20f;
        [Tooltip("Colour changes after which Customise is considered done.")]
        public int customiseChanges = 2;
        [Tooltip("If the user hasn't opened the exploded view by now, the guide opens it for them.")]
        public float explodeMax = 15f;
        [Tooltip("How long the exploded view stays open before the flow moves on, when the user opened it.")]
        public float explodedHold = 8f;
        [Tooltip("How long it stays open when the guide opened it.")]
        public float guideExplodeHold = 4f;
        [Tooltip("How long the end card is the focus before free exploration starts.")]
        public float endCardHold = 10f;
        [Tooltip("How long the ghost hand demonstrates each gesture.")]
        public float hintDuration = 3f;

        [Header("Placement")]
        [Tooltip("A table counts if its top is between these heights above the floor (m).")]
        public float tableMinHeight = 0.6f;
        public float tableMaxHeight = 1.1f;
        [Tooltip("Tables further away than this are ignored; everything has to stay within reach (m).")]
        public float maxTableDistance = 2f;
        [Tooltip("How far in front of the user the product sits on a large table (m).")]
        public float preferredReach = 0.45f;
        [Tooltip("Minimum distance kept from the table edge (m).")]
        public float edgeMargin = 0.18f;
        [Tooltip("Distance to the fallback pedestal (m).")]
        public float fixedDistance = 0.6f;
        [Tooltip("Pedestal height as a fraction of the user's eye height: roughly the waist.")]
        public float waistHeightRatio = 0.58f;
        public float pedestalMinHeight = 0.55f;
        public float pedestalMaxHeight = 1f;
        [Tooltip("When the user gets this far from the product, it moves to them (m).")]
        public float followDistance = 2f;
        [Tooltip("...after they have stayed away for this long (s).")]
        public float followDelay = 1.5f;
        [Tooltip("If the user is asked to point at a surface and doesn't, the pedestal is used after this long (s).")]
        public float pointTimeout = 20f;
        [Tooltip("How far the placement ray reaches (m).")]
        public float pointerRange = 4f;

        [Header("Handling")]
        public float minScale = 0.5f;
        public float maxScale = 3f;
        [Tooltip("Speed of the spring that brings a released product back to its spot (Hz).")]
        public float returnFrequency = 2.2f;
        [Tooltip("1 = no overshoot. A little less gives the return a soft settle.")]
        [Range(0.3f, 1f)] public float returnDamping = 0.82f;
        [Tooltip("When hand tracking is lost mid-hold, the product waits this long before going back (s).")]
        public float trackingLostHold = 0.5f;

        [Header("Customise")]
        [Tooltip("How long a colour takes to sweep across the product (s).")]
        public float sweepDuration = 0.4f;

        [Header("Exploded view")]
        [Tooltip("With two hands on the product, moving them this much further apart pulls it apart (m).")]
        public float explodePullDistance = 0.25f;
        [Tooltip("Holding two parts and bringing them this much closer puts it back together (m).")]
        public float reassemblePushDistance = 0.15f;
        public float explodeDuration = 0.8f;

        [Header("Panels")]
        [Tooltip("How far to the side of the product a docked panel sits (m).")]
        public float panelSideOffset = 0.34f;
        [Tooltip("Panel centre above the product's resting surface (m).")]
        public float panelHeight = 0.215f;
        [Tooltip("Docked panels only re-aim once the user has moved this far (m), so they don't swim around.")]
        public float panelFollowThreshold = 0.25f;
    }
}
