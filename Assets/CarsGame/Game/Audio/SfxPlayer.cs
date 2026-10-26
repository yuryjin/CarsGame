using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Pooled AudioSources for one-shot effects (no per-shot allocations), in two buses: world
    /// sounds (partly spatial, placed at the event) and UI sounds (2D, their own voices, so a gun
    /// battle never steals the finish fanfare or a kill sting). Voices are handed out by
    /// <see cref="SfxVoicePicker"/>: the quietest sound gives way, and one clip holds at most a few
    /// voices. Priority is the loudness the listener actually hears, so the player's own shots beat
    /// a distant duel without the call sites saying who fired.
    /// </summary>
    public sealed class SfxPlayer : MonoBehaviour
    {
        private static SfxPlayer instance;
        public static SfxPlayer Current => instance;
        public static SfxPlayer Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<SfxPlayer>();
                    if (instance == null) instance = new GameObject("SfxPlayer").AddComponent<SfxPlayer>();
                }
                return instance;
            }
        }

        public int worldVoices = 16, uiVoices = 4;
        [Tooltip("Voices one clip may hold at once (world / UI).")]
        public int maxPerClip = 4, maxPerUiClip = 2;
        [Range(0f, 1f)] public float spatialBlend = 0.6f;
        public float minDistance = 8f, maxDistance = 90f;
        [Tooltip("World sounds farther than this from the listener are not played (0 = never cull).")]
        public float cullDistance = 180f;
        [Range(0f, 1f)] public float volume = 0.8f;

        private const float DuckFrom = 0.75f, DuckMinSeconds = 0.35f;
        private AudioSource[] world, ui;
        private float[] worldGain, uiGain;
        private float appliedVolume = -1f;
        private SfxVoicePicker worldPicker, uiPicker;
        private AudioListener listener;
        // Advances only while audio runs, so voices paused by AudioListener.pause stay reserved.
        private float clock;

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            world = MakeVoices("Voice_", worldVoices, spatialBlend);
            ui = MakeVoices("UiVoice_", uiVoices, 0f);
            worldGain = new float[world.Length];
            uiGain = new float[ui.Length];
            worldPicker = new SfxVoicePicker(world.Length, maxPerClip);
            uiPicker = new SfxVoicePicker(ui.Length, maxPerUiClip);
        }

        private AudioSource[] MakeVoices(string prefix, int count, float blend)
        {
            var sources = new AudioSource[Mathf.Max(1, count)];
            for (int i = 0; i < sources.Length; i++)
            {
                var go = new GameObject(prefix + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false; s.spatialBlend = blend; s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = minDistance; s.maxDistance = maxDistance;
                sources[i] = s;
            }
            return sources;
        }

        private void Update()
        {
            if (!AudioListener.pause) clock += Time.unscaledDeltaTime;
            if (world == null || ui == null) return;
            float master = Mathf.Clamp01(volume);
            if (master == appliedVolume) return;
            appliedVolume = master;
            for (int i = 0; i < world.Length; i++) world[i].volume = worldGain[i] * master;
            for (int i = 0; i < ui.Length; i++) ui[i].volume = uiGain[i] * master;
        }

        private void OnDestroy() { if (instance == this) instance = null; }

        /// <summary>A world sound at <paramref name="position"/>.</summary>
        public void Play(AudioClip clip, Vector3 position, float vol = 1f, float pitch = 1f)
        {
            if (clip == null || world == null) return;
            float heard = HeardGain(position);
            if (heard <= 0f) return;
            int i = worldPicker.Pick(clip, vol * heard, Seconds(clip, pitch), clock);
            if (i < 0) return;
            var s = world[i];
            s.transform.position = position;
            worldGain[i] = vol;
            Launch(s, clip, vol, pitch);
            DuckMusic(clip, vol * heard * Mathf.Clamp01(volume));
        }

        /// <summary>A 2D sound (interface, stings, the player's own cues).</summary>
        public void PlayUi(AudioClip clip, float vol = 1f, float pitch = 1f)
        {
            if (clip == null || ui == null) return;
            int i = uiPicker.Pick(clip, vol, Seconds(clip, pitch), clock);
            if (i < 0) return;
            uiGain[i] = vol;
            Launch(ui[i], clip, vol, pitch);
            DuckMusic(clip, vol * Mathf.Clamp01(volume));
        }

        /// <summary>A loud, long-ish effect (explosion near the player, finish, kill sting) dips the music.</summary>
        private static void DuckMusic(AudioClip clip, float heard)
        {
            if (heard < DuckFrom || SfxLibrary.LengthOf(clip, 0f) < DuckMinSeconds) return;
            var music = MusicPlayer.Current;
            if (music != null) music.Duck(Mathf.Clamp01(heard) * 0.5f);
        }

        private void Launch(AudioSource s, AudioClip clip, float vol, float pitch)
        {
            s.pitch = pitch; s.volume = vol * Mathf.Clamp01(volume); s.clip = clip; s.Play();
        }

        private static float Seconds(AudioClip clip, float pitch) => SfxLibrary.LengthOf(clip) / Mathf.Max(0.05f, Mathf.Abs(pitch));

        /// <summary>How much of a world sound reaches the listener (the 2D share plus the linear 3D rolloff).</summary>
        private float HeardGain(Vector3 position)
        {
            if (listener == null || !listener.isActiveAndEnabled) listener = FindAnyObjectByType<AudioListener>();
            if (listener == null) return 1f;
            float d = Vector3.Distance(listener.transform.position, position);
            if (cullDistance > 0f && d > cullDistance) return 0f;
            float rolloff = 1f - Mathf.Clamp01((d - minDistance) / Mathf.Max(0.01f, maxDistance - minDistance));
            return (1f - spatialBlend) + spatialBlend * rolloff;
        }
    }
}
