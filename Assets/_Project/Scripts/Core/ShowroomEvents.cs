using System;
using UnityEngine;

namespace MRShowroom
{
    /// <summary>
    /// The one place the showroom's systems meet. Gameplay raises events here; audio, haptics,
    /// analytics, hints and the mixed reality layer listen, so none of them needs a reference to
    /// the others. Requests travel the other way: the UI asks, the mixed reality layer answers.
    /// </summary>
    public static class ShowroomEvents
    {
        public static event Action<ShowroomPhase, ShowroomPhase> PhaseChanged;
        public static event Action<PlacementMethod> Placed;
        public static event Action<PlacementPrompt> PlacementPromptChanged;
        public static event Action<GrabTarget> Grabbed;
        public static event Action<GrabTarget> Released;
        /// <summary>Slot index, colour index.</summary>
        public static event Action<int, int> ColorChanged;
        public static event Action<int> FinishChanged;
        public static event Action<ExplodeTrigger> Exploded;
        public static event Action<ExplodeTrigger> Reassembled;
        public static event Action<int> HotspotOpened;
        public static event Action<CallToAction> CtaPressed;
        public static event Action<InputMode> InputModeChanged;
        public static event Action<Sfx, Vector3> SfxRequested;
        /// <summary>0 = the room as it is, 1 = dimmed as far as the spotlight moment goes.</summary>
        public static event Action<float> SpotlightChanged;
        public static event Action Restarted;

        // Requests from the UI to whatever owns placement.
        public static event Action ManualPlacementRequested;
        public static event Action SpaceSetupRequested;
        public static event Action PlacementSkipRequested;

        public static InputMode CurrentInputMode { get; private set; }
        public static PlacementPrompt CurrentPlacementPrompt { get; private set; }
        /// <summary>True where the headset can open the system's room scan, so the UI knows whether to offer it.</summary>
        public static bool SpaceSetupAvailable { get; set; }

        public static void RaisePhaseChanged(ShowroomPhase from, ShowroomPhase to) => PhaseChanged?.Invoke(from, to);
        public static void RaisePlaced(PlacementMethod method) => Placed?.Invoke(method);
        public static void RaiseGrabbed(GrabTarget target) => Grabbed?.Invoke(target);
        public static void RaiseReleased(GrabTarget target) => Released?.Invoke(target);
        public static void RaiseColorChanged(int slot, int color) => ColorChanged?.Invoke(slot, color);
        public static void RaiseFinishChanged(int finish) => FinishChanged?.Invoke(finish);
        public static void RaiseExploded(ExplodeTrigger trigger) => Exploded?.Invoke(trigger);
        public static void RaiseReassembled(ExplodeTrigger trigger) => Reassembled?.Invoke(trigger);
        public static void RaiseHotspotOpened(int index) => HotspotOpened?.Invoke(index);
        public static void RaiseCta(CallToAction action) => CtaPressed?.Invoke(action);
        public static void RaiseSfx(Sfx sfx, Vector3 position) => SfxRequested?.Invoke(sfx, position);
        public static void RaiseSpotlight(float amount) => SpotlightChanged?.Invoke(amount);
        public static void RaiseRestarted() => Restarted?.Invoke();
        public static void RequestManualPlacement() => ManualPlacementRequested?.Invoke();
        public static void RequestSpaceSetup() => SpaceSetupRequested?.Invoke();
        public static void RequestPlacementSkip() => PlacementSkipRequested?.Invoke();

        public static void RaiseInputModeChanged(InputMode mode)
        {
            if (mode == CurrentInputMode)
            {
                return;
            }

            CurrentInputMode = mode;
            InputModeChanged?.Invoke(mode);
        }

        public static void SetPlacementPrompt(PlacementPrompt prompt)
        {
            if (prompt == CurrentPlacementPrompt)
            {
                return;
            }

            CurrentPlacementPrompt = prompt;
            PlacementPromptChanged?.Invoke(prompt);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            PhaseChanged = null;
            Placed = null;
            PlacementPromptChanged = null;
            Grabbed = null;
            Released = null;
            ColorChanged = null;
            FinishChanged = null;
            Exploded = null;
            Reassembled = null;
            HotspotOpened = null;
            CtaPressed = null;
            InputModeChanged = null;
            SfxRequested = null;
            SpotlightChanged = null;
            Restarted = null;
            ManualPlacementRequested = null;
            SpaceSetupRequested = null;
            PlacementSkipRequested = null;
            CurrentInputMode = InputMode.Unknown;
            CurrentPlacementPrompt = PlacementPrompt.None;
            SpaceSetupAvailable = false;
        }
    }
}
