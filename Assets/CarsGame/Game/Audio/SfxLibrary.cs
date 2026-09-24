using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Recorded effects in Resources/Sfx (built by Tools/audio/make_sfx.py from CC0 sources, see
    /// Tools/audio/SFX_SOURCES.md). Loaded on first use and cached, misses included, so callers can
    /// fall back to <see cref="SfxSynth"/> with a plain <c>??</c>.
    /// </summary>
    public static class SfxLibrary
    {
        public const string Folder = "Sfx/";
        private static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        /// <summary>
        /// Load every clip when the scene is up. WebGL decodes compressed clips asynchronously in the
        /// browser, so a clip first touched mid-race would miss its first shots; warming them in the
        /// menu has them decoded by the start line.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Warm()
        {
            foreach (var clip in Resources.LoadAll<AudioClip>(Folder.TrimEnd('/')))
            {
                cache[clip.name] = clip;
                if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            }
        }

        /// <summary>Clip length once its data is decoded; <paramref name="unknown"/> before that (asking earlier logs a WebGL warning).</summary>
        public static float LengthOf(AudioClip clip, float unknown = 0.5f) =>
            clip != null && clip.loadState == AudioDataLoadState.Loaded ? clip.length : unknown;

        public static AudioClip Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (!cache.TryGetValue(name, out var clip))
            {
                clip = Resources.Load<AudioClip>(Folder + name);
                cache[name] = clip;
            }
            return clip != null ? clip : null;   // a destroyed clip (domain reload off) counts as missing
        }
    }
}
