using UnityEngine;

namespace MRShowroom
{
    /// <summary>
    /// Every sound in the experience. Effects are spatial and play from where the thing happened
    /// (the box, the shoe, the panel); the music bed is quiet, non-spatial, and only plays under
    /// the hook and the end card so free exploration is left to the room's own sound.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        [SerializeField] private BrandTheme theme;
        [Tooltip("Spatial sources shared by all effects.")]
        [SerializeField] private AudioSource[] voices = new AudioSource[0];
        [SerializeField] private AudioSource music;
        [SerializeField] private float musicFade = 1.2f;
        [Tooltip("Random pitch variation so repeated sounds don't sound stamped (+/-).")]
        [SerializeField] private float pitchSpread = 0.04f;

        private int _next;
        private float _musicTarget;

        private void OnEnable()
        {
            ShowroomEvents.SfxRequested += Play;
            ShowroomEvents.PhaseChanged += HandlePhase;
            ShowroomEvents.Grabbed += HandleGrabbed;
            ShowroomEvents.Released += HandleReleased;
            ShowroomEvents.Placed += HandlePlaced;
        }

        private void OnDisable()
        {
            ShowroomEvents.SfxRequested -= Play;
            ShowroomEvents.PhaseChanged -= HandlePhase;
            ShowroomEvents.Grabbed -= HandleGrabbed;
            ShowroomEvents.Released -= HandleReleased;
            ShowroomEvents.Placed -= HandlePlaced;
        }

        private void Start()
        {
            if (music != null && theme != null && theme.musicBed != null)
            {
                music.clip = theme.musicBed;
                music.loop = true;
                music.volume = 0f;
            }
        }

        public void Play(Sfx sfx, Vector3 position)
        {
            if (theme == null || voices.Length == 0)
            {
                return;
            }

            AudioClip clip = theme.Clip(sfx);
            if (clip == null)
            {
                return;
            }

            AudioSource voice = voices[_next];
            _next = (_next + 1) % voices.Length;
            voice.transform.position = position;
            voice.pitch = 1f + Random.Range(-pitchSpread, pitchSpread);
            voice.clip = clip;
            voice.Play();
        }

        private void HandlePhase(ShowroomPhase from, ShowroomPhase to)
        {
            _musicTarget = to == ShowroomPhase.Hook || to == ShowroomPhase.EndCard
                ? (theme != null ? theme.musicVolume : 0f)
                : 0f;
        }

        private void HandleGrabbed(GrabTarget target)
        {
            Play(Sfx.Grab, target.transform.position);
        }

        private void HandleReleased(GrabTarget target)
        {
            Play(Sfx.Release, target.transform.position);
        }

        private void HandlePlaced(PlacementMethod method)
        {
            Play(Sfx.Placed, transform.position);
        }

        private void Update()
        {
            if (music == null || music.clip == null)
            {
                return;
            }

            float volume = Mathf.MoveTowards(music.volume, _musicTarget,
                (theme.musicVolume / Mathf.Max(musicFade, 0.01f)) * Time.unscaledDeltaTime);
            if (!Mathf.Approximately(volume, music.volume))
            {
                music.volume = volume;
            }

            // Don't keep decoding a silent track.
            if (volume > 0f && !music.isPlaying)
            {
                music.Play();
            }
            else if (volume <= 0f && music.isPlaying)
            {
                music.Stop();
            }
        }
    }
}
