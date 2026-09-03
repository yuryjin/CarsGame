using UnityEngine;

namespace RacingGame
{
    /// <summary>A handful of pooled AudioSources for one-shot effects (no per-shot allocations).</summary>
    public sealed class SfxPlayer : MonoBehaviour
    {
        private static SfxPlayer instance;
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

        public int voices = 8;
        [Range(0f, 1f)] public float volume = 0.8f;
        private AudioSource[] sources;
        private int next;

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            sources = new AudioSource[voices];
            for (int i = 0; i < voices; i++)
            {
                var go = new GameObject("Voice_" + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false; s.spatialBlend = 0.6f; s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 8f; s.maxDistance = 90f;
                sources[i] = s;
            }
        }

        private void OnDestroy() { if (instance == this) instance = null; }

        public void Play(AudioClip clip, Vector3 position, float vol = 1f, float pitch = 1f)
        {
            if (clip == null || sources == null) return;
            var s = sources[next]; next = (next + 1) % sources.Length;
            s.transform.position = position;
            s.pitch = pitch;
            s.volume = vol * volume;
            s.clip = clip;
            s.Play();
        }

        public void PlayUi(AudioClip clip, float vol = 1f, float pitch = 1f)
        {
            if (clip == null || sources == null) return;
            var s = sources[next]; next = (next + 1) % sources.Length;
            s.spatialBlend = 0f;
            s.transform.position = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            s.pitch = pitch; s.volume = vol * volume; s.clip = clip; s.Play();
            s.spatialBlend = 0.6f;
        }
    }
}
