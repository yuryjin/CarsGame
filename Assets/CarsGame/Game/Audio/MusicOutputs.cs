using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace RacingGame
{
    internal static class MusicFiles
    {
        /// <summary>Music ships as MP3 in StreamingAssets/Music (plays in every browser, streams, no import).</summary>
        public static string Url(string track) => Application.streamingAssetsPath + "/Music/" + track + ".mp3";
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>
    /// WebGL: music plays in the page through &lt;audio&gt; elements (CarsMusic.jslib), streamed
    /// instead of being decoded whole into memory like an AudioClip. The page also pauses it for a
    /// hidden tab by itself, since Unity stops running there.
    /// </summary>
    internal sealed class WebMusicOutput : IMusicOutput
    {
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern void CarsMusic_Play(string url, int loop, float fade);
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern void CarsMusic_Stop(float fade);
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern void CarsMusic_Sting(string url, float volume);
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern void CarsMusic_Preload(string url);
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern void CarsMusic_SetVolume(float volume);
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern void CarsMusic_SetPaused(int paused);
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern void CarsMusic_Duck(float depth, float attack, float hold, float release);

        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern void CarsMusic_FinalLap(string url, int active, float tempo);
        public void SetFinalLap(bool active, float tempo) => CarsMusic_FinalLap(MusicFiles.Url("final_lap"), active ? 1 : 0, tempo);
        public void Play(string track, bool loop, float fadeSeconds) => CarsMusic_Play(MusicFiles.Url(track), loop ? 1 : 0, fadeSeconds);
        public void Stop(float fadeSeconds) => CarsMusic_Stop(fadeSeconds);
        public void Sting(string track, float volume) => CarsMusic_Sting(MusicFiles.Url(track), volume);
        public void Preload(string track) => CarsMusic_Preload(MusicFiles.Url(track));
        public void SetVolume(float volume) => CarsMusic_SetVolume(volume);
        public void SetPaused(bool paused) => CarsMusic_SetPaused(paused ? 1 : 0);
        public void Duck(float depth, float attack, float hold, float release) => CarsMusic_Duck(depth, attack, hold, release);
    }
#endif

    /// <summary>
    /// Editor/desktop: two AudioSources crossfade the music channel, a third plays stings. Clips are
    /// loaded from StreamingAssets on demand. Sources ignore <c>AudioListener.pause</c>: the
    /// director decides when music pauses (not under the pause menu, only quieter there).
    /// </summary>
    internal sealed class UnityMusicOutput : MonoBehaviour, IMusicOutput
    {
        private sealed class Channel
        {
            public AudioSource source;
            public string track;
            public float gain, target, rate;
        }

        private readonly Channel[] music = new Channel[2];
        private Channel sting, finalLap;
        private bool wantsFinalLap;
        private float finalTempo;
        private int current = -1;
        private string wanted;
        private bool wantedLoop;
        private float wantedFade;
        private float volume, duck = 1f, duckTarget = 1f, duckRate, duckHoldUntil, duckRelease;
        private bool paused;
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly HashSet<string> loading = new HashSet<string>();

        private void Awake()
        {
            for (int i = 0; i < music.Length; i++) music[i] = MakeChannel("Music_" + i);
            sting = MakeChannel("MusicSting");
            finalLap = MakeChannel("FinalLapPercussion");
        }

        private Channel MakeChannel(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false; s.spatialBlend = 0f; s.ignoreListenerPause = true; s.volume = 0f;
            return new Channel { source = s };
        }

        public void Play(string track, bool loop, float fadeSeconds)
        {
            wanted = track; wantedLoop = loop; wantedFade = fadeSeconds;
            if (current >= 0 && music[current].track == track && music[current].source.isPlaying)
            {
                Fade(music[current], 1f, fadeSeconds);
                return;
            }
            if (clips.TryGetValue(track, out var clip)) StartWanted(clip);
            else Preload(track);
        }

        private void StartWanted(AudioClip clip)
        {
            int next = current < 0 ? 0 : 1 - current;
            if (current >= 0) Fade(music[current], 0f, wantedFade);
            var ch = music[next];
            ch.track = wanted; ch.gain = 0f;
            ch.source.clip = clip; ch.source.loop = wantedLoop;
            ch.source.Play();
            if (paused) ch.source.Pause();
            Fade(ch, 1f, wantedFade);
            current = next;
            wanted = null;
        }

        public void Stop(float fadeSeconds)
        {
            wanted = null;
            if (current >= 0) Fade(music[current], 0f, fadeSeconds);
            current = -1;
        }

        public void Sting(string track, float volumeScale)
        {
            if (!clips.TryGetValue(track, out var clip)) { Preload(track); return; }   // not ready: skip rather than play late
            sting.track = track; sting.gain = sting.target = volumeScale;
            sting.source.clip = clip; sting.source.loop = false;
            sting.source.Play();
            if (paused) sting.source.Pause();
        }

        public void SetFinalLap(bool active, float tempo)
        {
            wantsFinalLap = active; finalTempo = tempo;
            if (!active) { finalLap.source.Stop(); finalLap.gain = finalLap.target = 0f; return; }
            if (!clips.TryGetValue("final_lap", out var clip)) { Preload("final_lap"); return; }
            finalLap.source.clip = clip;
            finalLap.source.loop = true;
            finalLap.source.pitch = tempo;
            finalLap.source.Play();
            // Align the extra percussion to the race loop where the platform has a playback clock.
            if (current >= 0) finalLap.source.time = music[current].source.time * tempo % clip.length;
            Fade(finalLap, .33f, .16f);
        }

        public void Preload(string track)
        {
            if (clips.ContainsKey(track) || !loading.Add(track)) return;
            StartCoroutine(Load(track));
        }

        private IEnumerator Load(string track)
        {
            string url = MusicFiles.Url(track);
            if (!url.Contains("://")) url = new System.Uri(url).AbsoluteUri;
            using (var req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
            {
                yield return req.SendWebRequest();
                loading.Remove(track);
                if (req.result != UnityWebRequest.Result.Success) { Debug.LogWarning("[Music] " + track + ": " + req.error); yield break; }
                var clip = DownloadHandlerAudioClip.GetContent(req);
                clip.name = track;
                clips[track] = clip;
            }
            if (wanted == track) StartWanted(clips[track]);
            if (track == "final_lap" && wantsFinalLap) SetFinalLap(true, finalTempo);
        }

        public void SetVolume(float value) => volume = value;

        public void SetPaused(bool value)
        {
            if (paused == value) return;
            paused = value;
            foreach (var ch in AllChannels())
            {
                if (ch.source.clip == null) continue;
                if (paused) ch.source.Pause(); else ch.source.UnPause();
            }
        }

        public void Duck(float depth, float attack, float hold, float release)
        {
            float to = Mathf.Clamp01(1f - depth);
            if (to >= duckTarget && Time.unscaledTime < duckHoldUntil) { duckHoldUntil = Mathf.Max(duckHoldUntil, Time.unscaledTime + attack + hold); return; }
            duckTarget = Mathf.Min(duckTarget, to);
            duckRate = (1f - duckTarget) / Mathf.Max(0.01f, attack);
            duckHoldUntil = Time.unscaledTime + attack + hold;
            duckRelease = Mathf.Max(0.01f, release);
        }

        private static void Fade(Channel ch, float to, float seconds)
        {
            ch.target = to;
            ch.rate = Mathf.Abs(to - ch.gain) / Mathf.Max(0.01f, seconds);
        }

        private IEnumerable<Channel> AllChannels() { yield return music[0]; yield return music[1]; yield return sting; yield return finalLap; }

        private void Update()
        {
            if (paused) return;
            float dt = Time.unscaledDeltaTime;
            if (Time.unscaledTime >= duckHoldUntil && duckTarget < 1f) { duckTarget = 1f; duckRate = (1f - duck) / duckRelease; }
            duck = Mathf.MoveTowards(duck, duckTarget, duckRate * dt);
            foreach (var ch in AllChannels())
            {
                ch.gain = Mathf.MoveTowards(ch.gain, ch.target, ch.rate * dt);
                ch.source.volume = ch.gain * volume * duck;
                if (ch != sting && ch.gain <= 0f && ch.target <= 0f && ch.source.isPlaying) ch.source.Stop();
            }
        }
    }
}
