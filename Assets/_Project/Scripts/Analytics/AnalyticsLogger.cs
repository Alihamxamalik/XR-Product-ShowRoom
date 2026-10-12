using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MRShowroom
{
    /// <summary>
    /// The numbers an ad client asks for, kept on the headset: how long each phase held the user,
    /// which colours they chose, whether they found the exploded view by themselves, and whether
    /// they reached the call to action. One JSON file per run in
    /// <c>Application.persistentDataPath/analytics</c>; nothing leaves the device.
    /// </summary>
    public sealed class AnalyticsLogger : MonoBehaviour
    {
        [Serializable]
        public sealed class PhaseTime
        {
            public string phase;
            public float seconds;
        }

        [Serializable]
        public sealed class ColorChoice
        {
            public string slot;
            public string color;
        }

        [Serializable]
        public sealed class SessionRecord
        {
            public string sessionId;
            public string startedUtc;
            public string appVersion;
            public string device;
            public string product;
            public string inputMode;
            public string placement;
            public float secondsToPlacement;
            public float totalSeconds;
            public List<PhaseTime> phases = new List<PhaseTime>();
            public bool reachedEndCard;
            public int grabs;
            public int hotspotsOpened;
            public int colorChanges;
            public int finishChanges;
            public string colorway;
            public string finish;
            public List<ColorChoice> finalColors = new List<ColorChoice>();
            public int explodeByGesture;
            public int explodeByButton;
            public int explodeByGuide;
            public int buyNow;
            public int share;
            public int playAgain;
        }

        [SerializeField] private ProductController product;
        [Tooltip("Also write the record to the log, which shows up in the Developer Hub's device logs.")]
        [SerializeField] private bool echoToLog = true;

        private SessionRecord _record;
        private ShowroomPhase _phase = ShowroomPhase.FindSurface;
        private string _path;
        private bool _dirty;

        /// <summary>The run being recorded right now.</summary>
        public SessionRecord Current => _record;

        /// <summary>Where the current run is (or will be) written.</summary>
        public string CurrentPath => _path;

        private void Awake()
        {
            Begin();
        }

        private void OnEnable()
        {
            ShowroomEvents.PhaseChanged += HandlePhase;
            ShowroomEvents.Placed += HandlePlaced;
            ShowroomEvents.Grabbed += HandleGrabbed;
            ShowroomEvents.ColorChanged += HandleColorChanged;
            ShowroomEvents.FinishChanged += HandleFinishChanged;
            ShowroomEvents.Exploded += HandleExploded;
            ShowroomEvents.HotspotOpened += HandleHotspot;
            ShowroomEvents.CtaPressed += HandleCta;
            ShowroomEvents.InputModeChanged += HandleInputMode;
            ShowroomEvents.Restarted += HandleRestarted;
        }

        private void OnDisable()
        {
            ShowroomEvents.PhaseChanged -= HandlePhase;
            ShowroomEvents.Placed -= HandlePlaced;
            ShowroomEvents.Grabbed -= HandleGrabbed;
            ShowroomEvents.ColorChanged -= HandleColorChanged;
            ShowroomEvents.FinishChanged -= HandleFinishChanged;
            ShowroomEvents.Exploded -= HandleExploded;
            ShowroomEvents.HotspotOpened -= HandleHotspot;
            ShowroomEvents.CtaPressed -= HandleCta;
            ShowroomEvents.InputModeChanged -= HandleInputMode;
            ShowroomEvents.Restarted -= HandleRestarted;
            Save();
        }

        private void OnApplicationPause(bool paused)
        {
            // The headset can kill a paused app without warning, so this is the last safe moment to write.
            if (paused)
            {
                Save();
            }
        }

        private void Begin()
        {
            DateTime now = DateTime.UtcNow;
            _record = new SessionRecord
            {
                sessionId = Guid.NewGuid().ToString("N"),
                startedUtc = now.ToString("o"),
                appVersion = Application.version,
                device = SystemInfo.deviceModel,
                product = product != null && product.Definition != null ? product.Definition.id : string.Empty,
                inputMode = ShowroomEvents.CurrentInputMode.ToString(),
                placement = PlacementMethod.None.ToString()
            };

            foreach (ShowroomPhase phase in Enum.GetValues(typeof(ShowroomPhase)))
            {
                _record.phases.Add(new PhaseTime { phase = phase.ToString() });
            }

            string folder = Path.Combine(Application.persistentDataPath, "analytics");
            _path = Path.Combine(folder, $"session-{now:yyyyMMdd-HHmmss}-{_record.sessionId.Substring(0, 6)}.json");
            _dirty = false;
        }

        private void Update()
        {
            // Scaled time: the clock stops while the headset is off or the menu is open.
            float delta = Time.deltaTime;
            _record.phases[(int)_phase].seconds += delta;
            _record.totalSeconds += delta;
        }

        private void HandlePhase(ShowroomPhase from, ShowroomPhase to)
        {
            _phase = to;
            _dirty = true;
            if (to == ShowroomPhase.EndCard)
            {
                _record.reachedEndCard = true;
                Save();
            }
        }

        private void HandlePlaced(PlacementMethod method)
        {
            if (_record.placement == PlacementMethod.None.ToString())
            {
                _record.secondsToPlacement = _record.totalSeconds;
            }

            _record.placement = method.ToString();
            _dirty = true;
        }

        private void HandleGrabbed(GrabTarget target)
        {
            _record.grabs++;
            _dirty = true;
        }

        private void HandleColorChanged(int slot, int color)
        {
            _record.colorChanges++;
            _dirty = true;
        }

        private void HandleFinishChanged(int finish)
        {
            _record.finishChanges++;
            _dirty = true;
        }

        private void HandleExploded(ExplodeTrigger trigger)
        {
            switch (trigger)
            {
                case ExplodeTrigger.Gesture:
                    _record.explodeByGesture++;
                    break;
                case ExplodeTrigger.Button:
                    _record.explodeByButton++;
                    break;
                default:
                    _record.explodeByGuide++;
                    break;
            }

            _dirty = true;
        }

        private void HandleHotspot(int index)
        {
            _record.hotspotsOpened++;
            _dirty = true;
        }

        private void HandleCta(CallToAction action)
        {
            switch (action)
            {
                case CallToAction.BuyNow:
                    _record.buyNow++;
                    break;
                case CallToAction.Share:
                    _record.share++;
                    break;
                case CallToAction.PlayAgain:
                    _record.playAgain++;
                    break;
            }

            _dirty = true;
            Save();
        }

        private void HandleInputMode(InputMode mode)
        {
            _record.inputMode = mode.ToString();
            _dirty = true;
        }

        // Play again closes this run and starts a new record, so each pass through the flow is its own file.
        private void HandleRestarted()
        {
            Save();
            string placement = _record.placement;
            Begin();
            _record.placement = placement;
            _phase = ShowroomPhase.FindSurface;
        }

        private void Save()
        {
            if (!_dirty || _record == null)
            {
                return;
            }

            _dirty = false;
            CaptureLook();

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                string json = JsonUtility.ToJson(_record, true);
                File.WriteAllText(_path, json);
                if (echoToLog)
                {
                    Debug.Log("[Analytics] " + _path + "\n" + json);
                }
            }
            catch (Exception e)
            {
                // Analytics must never take the experience down with it.
                Debug.LogWarning("[Analytics] Could not write the session record: " + e.Message);
            }
        }

        private void CaptureLook()
        {
            if (product == null || product.Definition == null || product.Colors == null || product.View == null)
            {
                return;
            }

            ProductDefinition definition = product.Definition;
            ColorCustomizer colors = product.Colors;
            _record.product = definition.id;
            _record.colorway = colors.ColorwayName();
            _record.finish = colors.FinishIndex < definition.finishes.Length
                ? definition.finishes[colors.FinishIndex].name
                : string.Empty;

            _record.finalColors.Clear();
            for (int s = 0; s < definition.colorSlots.Length && s < colors.SlotCount; s++)
            {
                ColorSlot slot = definition.colorSlots[s];
                int index = colors.ColorIndex(s);
                _record.finalColors.Add(new ColorChoice
                {
                    slot = slot.name,
                    color = index < slot.colors.Length ? slot.colors[index].name : string.Empty
                });
            }
        }
    }
}
