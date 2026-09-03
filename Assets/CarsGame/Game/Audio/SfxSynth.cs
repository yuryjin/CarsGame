using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Procedurally generated sound effects so the vertical slice has audio without any asset
    /// files. Clips are created once per session; playback goes through <see cref="SfxPlayer"/>.
    /// </summary>
    public static class SfxSynth
    {
        private const int Rate = 22050;
        private static AudioClip engine, shot, hit, lapDing, beep, go, finish, wreck;

        public static AudioClip Engine => engine ??= Loop("sfx_engine", 1f, t =>
        {
            float f = 55f;
            float v = Mathf.Sin(2f * Mathf.PI * f * t) * 0.5f + Saw(f * 2f, t) * 0.25f + Saw(f * 3.01f, t) * 0.12f;
            return v * 0.5f;
        });

        public static AudioClip Shot => shot ??= OneShot("sfx_shot", 0.14f, t =>
        {
            float env = Mathf.Exp(-t * 28f);
            return (Noise() * 0.8f + Mathf.Sin(2f * Mathf.PI * 90f * t) * 0.6f) * env;
        });

        public static AudioClip Hit => hit ??= OneShot("sfx_hit", 0.18f, t =>
        {
            float env = Mathf.Exp(-t * 18f);
            return (Noise() * 0.5f + Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.8f) * env;
        });

        public static AudioClip Wreck => wreck ??= OneShot("sfx_wreck", 0.6f, t =>
        {
            float env = Mathf.Exp(-t * 5f);
            return (Noise() * 0.7f + Mathf.Sin(2f * Mathf.PI * 40f * t) * 0.6f) * env;
        });

        public static AudioClip LapDing => lapDing ??= OneShot("sfx_lap", 0.25f, t => Mathf.Sin(2f * Mathf.PI * 1320f * t) * Mathf.Exp(-t * 12f) * 0.6f);
        public static AudioClip Beep => beep ??= OneShot("sfx_beep", 0.12f, t => Mathf.Sin(2f * Mathf.PI * 660f * t) * Mathf.Exp(-t * 20f) * 0.7f);
        public static AudioClip Go => go ??= OneShot("sfx_go", 0.45f, t => Mathf.Sin(2f * Mathf.PI * 880f * t) * Mathf.Exp(-t * 6f) * 0.8f);

        public static AudioClip Finish => finish ??= OneShot("sfx_finish", 0.9f, t =>
        {
            float[] notes = { 523f, 659f, 784f, 1047f };
            int i = Mathf.Min(3, (int)(t / 0.2f));
            float lt = t - i * 0.2f;
            return Mathf.Sin(2f * Mathf.PI * notes[i] * lt) * Mathf.Exp(-lt * 8f) * 0.7f;
        });

        private static float Saw(float f, float t) { float p = (t * f) % 1f; return p * 2f - 1f; }
        private static readonly System.Random rng = new System.Random(1234);
        private static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);

        private static AudioClip OneShot(string name, float seconds, System.Func<float, float> f) => Build(name, seconds, f, false);
        private static AudioClip Loop(string name, float seconds, System.Func<float, float> f) => Build(name, seconds, f, true);

        private static AudioClip Build(string name, float seconds, System.Func<float, float> f, bool loop)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
            if (loop)
            {
                // Cross-fade the tail into the head so the loop point does not click.
                int fade = Mathf.Min(n / 8, 2048);
                for (int i = 0; i < fade; i++)
                {
                    float w = i / (float)fade;
                    data[n - fade + i] = data[n - fade + i] * (1f - w) + data[i] * w;
                }
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
