using UnityEngine;

namespace MRShowroom
{
    /// <summary>
    /// The guided journey, shaped like a playable ad: find a surface, hook, inspect, customise,
    /// explode, reveal, end card, then free exploration.
    ///
    /// Each phase switches one more thing on and shows one hint; nothing is ever taken away, so a
    /// user who is still turning the shoe over when the colours appear loses nothing. A phase ends
    /// when the user has done what it teaches, or when its time is up. Doing the next thing early
    /// (touching the box, pressing "See inside") skips ahead.
    /// </summary>
    public sealed class ShowroomFlow : MonoBehaviour
    {
        [SerializeField] private ShowroomTuning tuning;
        [SerializeField] private ProductStage stage;
        [SerializeField] private ProductController product;
        [SerializeField] private HookSequence hook;
        [SerializeField] private RevealSequence reveal;
        [SerializeField] private SwatchPanelController swatchPanel;
        [SerializeField] private EndCardController endCard;

        private int _grabs;
        private int _colorChanges;
        private bool _hotspotsShown;
        private bool _explodeSeen;
        private bool _guideOpened;
        private float _openTime;
        private bool _revealStarted;
        private bool _restarting;

        public ShowroomPhase Phase { get; private set; } = ShowroomPhase.FindSurface;

        /// <summary>Seconds spent in the current phase. Stops while the app is paused.</summary>
        public float PhaseTime { get; private set; }

        private void OnEnable()
        {
            stage.Placed += HandlePlaced;
            hook.Finished += HandleHookFinished;
            reveal.Finished += HandleRevealFinished;
            ShowroomEvents.Grabbed += HandleGrabbed;
            ShowroomEvents.ColorChanged += HandleColorChanged;
            ShowroomEvents.FinishChanged += HandleFinishChanged;
            ShowroomEvents.Exploded += HandleExploded;
            ShowroomEvents.CtaPressed += HandleCta;
        }

        private void OnDisable()
        {
            stage.Placed -= HandlePlaced;
            hook.Finished -= HandleHookFinished;
            reveal.Finished -= HandleRevealFinished;
            ShowroomEvents.Grabbed -= HandleGrabbed;
            ShowroomEvents.ColorChanged -= HandleColorChanged;
            ShowroomEvents.FinishChanged -= HandleFinishChanged;
            ShowroomEvents.Exploded -= HandleExploded;
            ShowroomEvents.CtaPressed -= HandleCta;
        }

        private void Start()
        {
            product.SetVisible(false);
            product.SetInteractable(false);
            product.ShowHotspots(false);

            // The surface may already have been found by the time this starts.
            if (stage.IsPlaced)
            {
                Enter(ShowroomPhase.Hook);
            }
        }

        private void Update()
        {
            PhaseTime += Time.deltaTime;

            switch (Phase)
            {
                case ShowroomPhase.Inspect:
                    if (!_hotspotsShown && (_grabs > 0 || PhaseTime >= tuning.hotspotDelay))
                    {
                        _hotspotsShown = true;
                        product.ShowHotspots(true);
                    }

                    bool handled = _grabs > 0 && !product.IsHeld && PhaseTime >= tuning.inspectMin;
                    if (handled || PhaseTime >= tuning.inspectMax)
                    {
                        Enter(ShowroomPhase.Customise);
                    }

                    break;

                case ShowroomPhase.Customise:
                    bool customised = _colorChanges >= tuning.customiseChanges && PhaseTime >= tuning.customiseMin;
                    if (customised || PhaseTime >= tuning.customiseMax)
                    {
                        Enter(ShowroomPhase.Explode);
                    }

                    break;

                case ShowroomPhase.Explode:
                    UpdateExplode();
                    break;

                case ShowroomPhase.Reveal:
                    // Wait for the parts to come back together before the walk starts.
                    if (!_revealStarted && !product.Explode.IsOpen && !product.Explode.IsMoving)
                    {
                        _revealStarted = true;
                        reveal.Play();
                    }

                    break;

                case ShowroomPhase.EndCard:
                    if (PhaseTime >= tuning.endCardHold || _grabs > 0)
                    {
                        Enter(ShowroomPhase.Free);
                    }

                    break;
            }
        }

        private void UpdateExplode()
        {
            ExplodeController explode = product.Explode;
            if (!_explodeSeen)
            {
                // Never tried it: open it for them, so nobody leaves without seeing inside.
                if (PhaseTime >= tuning.explodeMax && !product.IsHeld)
                {
                    _guideOpened = true;
                    explode.Explode(ExplodeTrigger.Guide);
                }

                return;
            }

            if (!explode.IsOpen)
            {
                // They closed it themselves.
                if (!explode.IsMoving)
                {
                    Enter(ShowroomPhase.Reveal);
                }

                return;
            }

            _openTime += Time.deltaTime;
            float hold = _guideOpened ? tuning.guideExplodeHold : tuning.explodedHold;
            if (_openTime >= hold && !product.IsHeld)
            {
                Enter(ShowroomPhase.Reveal);
            }
        }

        /// <summary>Moves on to the next phase now. For the editor's debug keys and for tests.</summary>
        public void Advance()
        {
            switch (Phase)
            {
                case ShowroomPhase.Hook:
                    hook.Skip();
                    break;
                case ShowroomPhase.Reveal:
                    reveal.Cancel();
                    Enter(ShowroomPhase.EndCard);
                    break;
                case ShowroomPhase.FindSurface:
                case ShowroomPhase.Free:
                    break;
                default:
                    Enter(Phase + 1);
                    break;
            }
        }

        /// <summary>Play again: same spot, everything back to how it came out of the box.</summary>
        public void Restart()
        {
            if (!stage.IsPlaced)
            {
                return;
            }

            // Cutting the hook or the walk short reports them as finished; that isn't a phase change here.
            _restarting = true;
            hook.Skip();
            reveal.Cancel();
            _restarting = false;

            ShowroomEvents.RaiseRestarted();
            Enter(ShowroomPhase.Hook);
        }

        private void Enter(ShowroomPhase next)
        {
            ShowroomPhase previous = Phase;
            Phase = next;
            PhaseTime = 0f;
            _grabs = 0;

            switch (next)
            {
                case ShowroomPhase.Hook:
                    _colorChanges = 0;
                    _hotspotsShown = false;
                    _explodeSeen = false;
                    _guideOpened = false;
                    _openTime = 0f;
                    _revealStarted = false;

                    swatchPanel.Hide();
                    swatchPanel.SetFreeMode(false);
                    endCard.Hide();
                    product.ShowHotspots(false);
                    product.Explode.GestureEnabled = false;
                    product.ResetProduct();
                    break;

                case ShowroomPhase.Inspect:
                    product.SetVisible(true);
                    product.SetInteractable(true);
                    break;

                case ShowroomPhase.Customise:
                    ShowEverythingUpTo(ShowroomPhase.Inspect);
                    swatchPanel.Show();
                    break;

                case ShowroomPhase.Explode:
                    ShowEverythingUpTo(ShowroomPhase.Customise);
                    product.Explode.GestureEnabled = true;
                    swatchPanel.SetExplodeHighlighted(true);
                    break;

                case ShowroomPhase.Reveal:
                    ShowEverythingUpTo(ShowroomPhase.Explode);
                    swatchPanel.SetExplodeHighlighted(false);
                    // The brand moment has the surface to itself: the panel is docked where the shoe is
                    // about to walk, and comes back with the end card.
                    swatchPanel.Hide();
                    product.ShowHotspots(false);
                    _revealStarted = false;
                    if (product.Explode.IsOpen)
                    {
                        product.Explode.Reassemble(ExplodeTrigger.Guide);
                    }

                    break;

                case ShowroomPhase.EndCard:
                    ShowEverythingUpTo(ShowroomPhase.Explode);
                    product.ShowHotspots(true);
                    swatchPanel.SetExplodeHighlighted(false);
                    endCard.Show();
                    break;

                case ShowroomPhase.Free:
                    swatchPanel.SetFreeMode(true);
                    break;
            }

            // Announce before starting anything that could finish (and change the phase again) straight away.
            ShowroomEvents.RaisePhaseChanged(previous, next);

            if (next == ShowroomPhase.Hook)
            {
                hook.Play();
            }
        }

        // Phases can be skipped, so every phase makes sure what the earlier ones would have switched on is on.
        private void ShowEverythingUpTo(ShowroomPhase phase)
        {
            product.SetVisible(true);
            product.SetInteractable(true);

            if (phase >= ShowroomPhase.Inspect && !_hotspotsShown)
            {
                _hotspotsShown = true;
                product.ShowHotspots(true);
            }

            if (phase >= ShowroomPhase.Customise)
            {
                swatchPanel.Show();
            }

            if (phase >= ShowroomPhase.Explode)
            {
                product.Explode.GestureEnabled = true;
            }
        }

        private void HandlePlaced(SurfaceInfo surface)
        {
            if (Phase == ShowroomPhase.FindSurface)
            {
                Enter(ShowroomPhase.Hook);
            }
        }

        private void HandleHookFinished()
        {
            if (Phase == ShowroomPhase.Hook && !_restarting)
            {
                Enter(ShowroomPhase.Inspect);
            }
        }

        private void HandleRevealFinished()
        {
            if (Phase == ShowroomPhase.Reveal && !_restarting)
            {
                Enter(ShowroomPhase.EndCard);
            }
        }

        private void HandleGrabbed(GrabTarget target)
        {
            _grabs++;
        }

        private void HandleColorChanged(int slot, int color)
        {
            _colorChanges++;
        }

        private void HandleFinishChanged(int finish)
        {
            _colorChanges++;
        }

        private void HandleExploded(ExplodeTrigger trigger)
        {
            _explodeSeen = true;
            _openTime = 0f;

            // "See inside" pressed before the flow got there: skip ahead to it.
            if (Phase == ShowroomPhase.Inspect || Phase == ShowroomPhase.Customise)
            {
                Enter(ShowroomPhase.Explode);
            }
        }

        private void HandleCta(CallToAction action)
        {
            if (action == CallToAction.PlayAgain)
            {
                Restart();
            }
        }
    }
}
