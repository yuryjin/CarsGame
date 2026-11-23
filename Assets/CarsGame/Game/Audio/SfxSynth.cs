using UnityEngine;
using RacingGame.Core.Economy;

namespace RacingGame
{
    /// <summary>
    /// The game's sound effects. Where a recorded clip exists (<see cref="SfxLibrary"/>, built by
    /// Tools/audio/make_sfx.py) it is used; otherwise the procedural sound below plays, so a
    /// missing asset never silences the game. Synth clips are created once per session; playback
    /// goes through <see cref="SfxPlayer"/>.
    /// </summary>
    public static class SfxSynth
    {
        private const int Rate = 22050;
        private static AudioClip engine, shot, hit, lapDing, beep, go, finish, wreck, trainWhistle, crawlerTracks;

        public static AudioClip Engine => engine ??= Loop("sfx_engine", 1f, t =>
        {
            float f = 55f;
            float v = Mathf.Sin(2f * Mathf.PI * f * t) * 0.5f + Saw(f * 2f, t) * 0.25f + Saw(f * 3.01f, t) * 0.12f;
            return v * 0.5f;
        });

        public static AudioClip CrawlerTracks => crawlerTracks ??= Loop("sfx_crawler_tracks", 1f, t =>
        {
            float beat = (t * 11f) % 1f;
            float rumble = .34f * Mathf.Sin(2f * Mathf.PI * 43f * t) + .17f * Mathf.Sin(2f * Mathf.PI * 74f * t);
            float clank = Mathf.Exp(-beat * 11f) * (.29f * Mathf.Sin(2f * Mathf.PI * 335f * t) + .16f * Noise());
            return rumble * (.83f + .17f * Mathf.Cos(2f * Mathf.PI * beat)) + clank;
        });

        public static AudioClip Shot => SfxLibrary.Get("fire_cannon") ?? SynthShot;
        public static AudioClip Hit => SfxLibrary.Get("hit_metal_0") ?? SynthHit;
        public static AudioClip Wreck => SfxLibrary.Get("explosion") ?? SynthWreck;

        private static AudioClip SynthShot => shot ??= OneShot("sfx_shot", 0.14f, t =>
        {
            float env = Mathf.Exp(-t * 28f);
            return (Noise() * 0.8f + Mathf.Sin(2f * Mathf.PI * 90f * t) * 0.6f) * env;
        });

        private static AudioClip SynthHit => hit ??= OneShot("sfx_hit", 0.18f, t =>
        {
            float env = Mathf.Exp(-t * 18f);
            return (Noise() * 0.5f + Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.8f) * env;
        });

        private static AudioClip SynthWreck => wreck ??= OneShot("sfx_wreck", 0.6f, t =>
        {
            float env = Mathf.Exp(-t * 5f);
            return (Noise() * 0.7f + Mathf.Sin(2f * Mathf.PI * 40f * t) * 0.6f) * env;
        });

        public static AudioClip LapDing => lapDing ??= OneShot("sfx_lap", 0.25f, t => Mathf.Sin(2f * Mathf.PI * 1320f * t) * Mathf.Exp(-t * 12f) * 0.6f);
        public static AudioClip Beep => beep ??= OneShot("sfx_beep", 0.12f, t => Mathf.Sin(2f * Mathf.PI * 660f * t) * Mathf.Exp(-t * 20f) * 0.7f);
        public static AudioClip Go => go ??= OneShot("sfx_go", 0.45f, t => Mathf.Sin(2f * Mathf.PI * 880f * t) * Mathf.Exp(-t * 6f) * 0.8f);

        public static AudioClip TrainWhistle => trainWhistle ??= OneShot("sfx_train_whistle", 1.4f, t =>
        {
            float phase = 2f * Mathf.PI * (t + 0.005f * Mathf.Exp(-t * 9f));
            float pressure = Mathf.Clamp01(t / 0.12f) * Mathf.Clamp01((1.4f - t) / 0.30f);
            return pressure * (Mathf.Sin(330f * phase) * 0.42f + Mathf.Sin(414f * phase) * 0.31f + Mathf.Sin(493f * phase) * 0.22f);
        });

        public static AudioClip Finish => finish ??= OneShot("sfx_finish", 0.9f, t =>
        {
            float[] notes = { 523f, 659f, 784f, 1047f };
            int i = Mathf.Min(3, (int)(t / 0.2f));
            float lt = t - i * 0.2f;
            return Mathf.Sin(2f * Mathf.PI * notes[i] * lt) * Mathf.Exp(-lt * 8f) * 0.7f;
        });

        // ---------- player car loops (built on first race, not at menu load) ----------
        private static AudioClip engineRich, screech, wind, nitroHiss, buggyMotor, armoredMotor, tankMotor;

        public static AudioClip EngineFor(BodyClass body)
        {
            switch (body)
            {
                case BodyClass.Buggy:
                    return buggyMotor ??= Loop("motor_buggy", 1f, t =>
                        .33f * Saw(92f, t) + .19f * Mathf.Sin(2f * Mathf.PI * 276f * t) + .09f * Saw(184f, t));
                case BodyClass.Armored:
                    return armoredMotor ??= Loop("motor_armored", 1f, t =>
                        .39f * Mathf.Sin(2f * Mathf.PI * 46f * t) + .19f * Saw(92f, t)
                        + .13f * Mathf.Sin(2f * Mathf.PI * 138f * t) * (.5f + .5f * Mathf.Cos(2f * Mathf.PI * 23f * t)));
                case BodyClass.Tank:
                    return tankMotor ??= Loop("motor_tank", 1f, t =>
                        (.41f * Mathf.Sin(2f * Mathf.PI * 34f * t) + .26f * Saw(68f, t))
                        * (.74f + .26f * Mathf.Cos(2f * Mathf.PI * 8f * t)));
                default: return EngineRich;
            }
        }

        /// <summary>55 Hz firing note with six harmonics, a slightly detuned second voice and exhaust pulses.</summary>
        public static AudioClip EngineRich => SfxLibrary.Get("engine_loop") ?? SynthEngineRich;
        private static AudioClip SynthEngineRich => engineRich ??= Loop("sfx_engine_rich", 1f, t =>
        {
            const float f = 55f;
            float v = 0f;
            float[] amp = { 1f, 0.6f, 0.4f, 0.25f, 0.15f, 0.1f };
            for (int h = 0; h < amp.Length; h++) v += Mathf.Sin(2f * Mathf.PI * f * (h + 1) * t) * amp[h];
            v += Saw(f * 1.003f, t) * 0.25f;
            float pulse = Mathf.Pow(Mathf.Abs(Mathf.Sin(Mathf.PI * f * 2f * t)), 12f);
            v += pulse * 0.9f + Noise() * 0.04f;
            return v * 0.22f;
        });

        private static AudioClip asphaltRoll, sandRoll, bridgeRoll;
        public static AudioClip TireRoll(TireSurface surface)
        {
            if (surface == TireSurface.Bridge)
                return bridgeRoll ??= Loop("tire_bridge", 1f, t => Mathf.Exp(-((t * 8f) % 1f) * 21f)
                    * (.6f * Mathf.Sin(2f * Mathf.PI * 155f * t) + .17f * Noise()));
            if (surface == TireSurface.Sand)
                return sandRoll ??= Loop("tire_sand", 1f, t =>
                    Noise() * (.17f + .43f * Mathf.Pow(Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 19f * t)), 5f)));
            float smooth = 0f;
            return asphaltRoll ??= Loop("tire_asphalt", 1f, t => smooth = .79f * smooth + .21f * Noise());
        }

        /// <summary>Tyre squeal: band-limited noise (running average) plus a wavering 1.1 kHz tone.</summary>
        public static AudioClip Screech
        {
            get
            {
                if (screech != null) return screech;
                float a = 0f, b = 0f;
                screech = Loop("sfx_screech", 0.6f, t =>
                {
                    float w = Noise();
                    float lp = (w + a + b) / 3f; b = a; a = w;
                    float tone = Mathf.Sin(2f * Mathf.PI * (1100f + 40f * Mathf.Sin(2f * Mathf.PI * 7f * t)) * t);
                    return (lp - w * 0.2f) * 0.5f + tone * 0.3f;
                });
                return screech;
            }
        }

        /// <summary>Wind: leaky-integrated ("brown") noise.</summary>
        public static AudioClip Wind
        {
            get
            {
                if (wind != null) return wind;
                float brown = 0f;
                wind = Loop("sfx_wind", 1f, t => { brown = (brown + 0.04f * Noise()) / 1.02f; return brown * 3.5f; });
                return wind;
            }
        }

        /// <summary>Nitro kick: a noise burst over a rising sweep.</summary>
        public static AudioClip NitroHiss => SfxLibrary.Get("nitro") ?? SynthNitroHiss;
        private static AudioClip SynthNitroHiss => nitroHiss ??= OneShot("sfx_nitro", 1.2f, t =>
        {
            float env = Mathf.Min(1f, t / 0.05f) * Mathf.Exp(-t * 2.2f);
            float sweep = Mathf.Sin(2f * Mathf.PI * (300f * t + 250f * t * t));
            return (Noise() * 0.55f + sweep * 0.3f) * env;
        });

        // ---------- combat feedback ----------
        private static AudioClip[] hits;
        private static AudioClip hitMetal, killSting, heartbeat, shieldBreak;
        public static AudioClip ShieldBreak => SfxLibrary.Get("shield_break") ?? SynthShieldBreak;
        private static AudioClip SynthShieldBreak => shieldBreak ??= OneShot("sfx_shield_break", 0.24f, t =>
            (Mathf.Sin(2f * Mathf.PI * (1800f * t - 2200f * t * t)) + Noise() * 0.2f) * Mathf.Exp(-t * 15f) * 0.6f);

        /// <summary>Three hit sounds, so a burst of hits does not sound like a loop.</summary>
        public static AudioClip HitVariant(int i) => SfxLibrary.Get(HitNames[((i % HitNames.Length) + HitNames.Length) % HitNames.Length]) ?? SynthHitVariant(i);
        private static readonly string[] HitNames = { "hit_metal_0", "hit_metal_1", "hit_metal_2" };

        /// <summary>Three thuds with different body resonance.</summary>
        private static AudioClip SynthHitVariant(int i)
        {
            if (hits == null)
            {
                float[] body = { 60f, 90f, 140f };
                hits = new AudioClip[body.Length];
                for (int k = 0; k < body.Length; k++)
                {
                    float f = body[k];
                    hits[k] = OneShot("sfx_hit_" + k, 0.2f, t => (Noise() * 0.45f + Mathf.Sin(2f * Mathf.PI * f * t) * 0.85f) * Mathf.Exp(-t * 17f));
                }
            }
            return hits[((i % hits.Length) + hits.Length) % hits.Length];
        }

        /// <summary>Hit on the player's own car: a bright metallic clank on top of the thud.</summary>
        public static AudioClip HitMetal => SfxLibrary.Get("hit_heavy") ?? SynthHitMetal;
        private static AudioClip SynthHitMetal => hitMetal ??= OneShot("sfx_hit_metal", 0.3f, t =>
        {
            float ring = Mathf.Sin(2f * Mathf.PI * 820f * t) * 0.35f + Mathf.Sin(2f * Mathf.PI * 1310f * t) * 0.25f;
            return (ring * Mathf.Exp(-t * 11f) + (Noise() * 0.4f + Mathf.Sin(2f * Mathf.PI * 75f * t) * 0.7f) * Mathf.Exp(-t * 22f)) * 0.9f;
        });

        /// <summary>Kill confirmation: three quick rising notes.</summary>
        public static AudioClip KillSting => killSting ??= OneShot("sfx_kill", 0.42f, t =>
        {
            float[] notes = { 660f, 880f, 1320f };
            int i = Mathf.Min(2, (int)(t / 0.09f));
            float lt = t - i * 0.09f;
            float tone = Mathf.Sin(2f * Mathf.PI * notes[i] * lt) + 0.3f * Saw(notes[i], lt);
            return tone * Mathf.Exp(-lt * (i == 2 ? 7f : 14f)) * 0.55f;
        });

        /// <summary>Low HP: a double "lub-dub" at 55 Hz.</summary>
        public static AudioClip Heartbeat => heartbeat ??= OneShot("sfx_heartbeat", 0.45f, t =>
        {
            float a = Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Exp(-t * 22f);
            float lt = t - 0.18f;
            float b = lt > 0f ? Mathf.Sin(2f * Mathf.PI * 48f * lt) * Mathf.Exp(-lt * 26f) * 0.7f : 0f;
            return (a + b) * 0.9f;
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
