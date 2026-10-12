namespace MRShowroom
{
    /// <summary>The guided journey, in order. Each phase adds something; none takes anything away.</summary>
    public enum ShowroomPhase
    {
        FindSurface,
        Hook,
        Inspect,
        Customise,
        Explode,
        Reveal,
        EndCard,
        Free
    }

    public enum InputMode
    {
        Unknown,
        Hands,
        Controllers
    }

    public enum HoldEndReason
    {
        Released,
        /// <summary>The hand holding it stopped tracking: wait a moment before going back to rest.</summary>
        TrackingLost,
        /// <summary>The game took it away (exploded, reset, no longer grabbable).</summary>
        Cancelled
    }

    /// <summary>What opened or closed the exploded view. Logged, because it shows whether the gesture is discoverable.</summary>
    public enum ExplodeTrigger
    {
        Gesture,
        Button,
        /// <summary>The guided flow did it for a user who never tried.</summary>
        Guide
    }

    /// <summary>How the product ended up where it is. The order matches the fallback ladder.</summary>
    public enum PlacementMethod
    {
        None,
        /// <summary>On a table found in the scanned room.</summary>
        Table,
        /// <summary>Scanned room without a usable table: a pedestal on the floor near the user.</summary>
        Floor,
        /// <summary>The user pointed at a real surface and pinched.</summary>
        Pointed,
        /// <summary>The user touched a real surface and pinched. Needs no room data at all.</summary>
        Touch,
        /// <summary>A pedestal at a fixed spot in front of the user.</summary>
        Fixed
    }

    /// <summary>What the placement step is asking of the user, if anything.</summary>
    public enum PlacementPrompt
    {
        None,
        Searching,
        NeedPermission,
        PointAtSurface,
        TouchSurface
    }

    public enum CallToAction
    {
        BuyNow,
        Share,
        PlayAgain
    }

    public enum Sfx
    {
        BoxThud,
        LidPop,
        Grab,
        Release,
        SwatchClick,
        Sweep,
        ExplodeWhoosh,
        ReassembleSnap,
        HotspotOpen,
        UiPress,
        Step,
        Placed,
        Confirm
    }
}
