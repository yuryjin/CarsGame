using UnityEngine;
using RacingGame.Core.Economy;

namespace RacingGame
{
    /// <summary>
    /// The player car's continuous sound: engine with four virtual gears (pitch climbs through each
    /// gear, dips on the shift), load from the throttle, a nitro lift; tyre squeal from side slip; wind
    /// that grows with speed. Three looping sources, clips synthesized once. Visual/audio only.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public sealed class EngineAudio : MonoBehaviour
    {
        public int gears = 4;
        public float idlePitch = 0.7f, redlinePitch = 1.9f;
        public float shiftDip = 0.12f, shiftDipSeconds = 0.08f;
        public float nitroPitch = 1.15f;
        [Tooltip("Side slip (m/s) where the squeal starts and where it is at full volume.")]
        public float screechFrom = 1.5f, screechFull = 5f;
        [Tooltip("Impact speed along the contact normal (m/s): a light knock from here, the heavy crunch from the second value.")]
        public float crashFrom = 3f, crashHeavy = 10f;

        private CarController car;
        private PlayerDriverInput input;
        private AudioSource engine, screech, wind;
        private int gear;
        private float dipUntil;
        private bool wasBoosting, running;
        private float nextCrash;
        private float screechGain;

        public float EnginePitch => engine != null ? engine.pitch : 0f;
        public float ScreechVolume => screech != null ? screech.volume : 0f;
        public int Gear => gear;

        private void Awake()
        {
            car = GetComponent<CarController>();
            input = GetComponent<PlayerDriverInput>();
            var body = GetComponent<CarBody>();
            engine = MakeSource("Engine", SfxSynth.EngineFor(body != null ? body.Class : BodyClass.Sport));
            screech = MakeSource("Screech", SfxSynth.Screech);
            wind = MakeSource("Wind", SfxSynth.Wind);
        }

        private AudioSource MakeSource(string name, AudioClip clip)
        {
            var go = new GameObject("Audio_" + name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip; s.loop = true; s.playOnAwake = false; s.spatialBlend = 0f; s.volume = 0f;
            return s;
        }

        /// <summary>Start/stop all loops (race start, results).</summary>
        public void SetRunning(bool on)
        {
            running = on;
            if (on)
            {
                var body = GetComponent<CarBody>();
                var clip = SfxSynth.EngineFor(body != null ? body.Class : BodyClass.Sport);
                if (engine.clip != clip) { engine.Stop(); engine.clip = clip; }
            }
            foreach (var s in new[] { engine, screech, wind })
            {
                if (s == null) continue;
                if (on && !s.isPlaying) s.Play();
                else if (!on && s.isPlaying) s.Stop();
            }
        }

        /// <summary>Bumps and crashes of the player's car (walls, other cars, landings); shots and mines have their own sounds.</summary>
        private void OnCollisionEnter(Collision c)
        {
            if (!running || Time.unscaledTime < nextCrash || c.contactCount == 0) return;
            if (c.collider.GetComponentInParent<Projectile>() != null || c.collider.GetComponentInParent<MineHazard>() != null) return;
            var contact = c.GetContact(0);
            float speed = Mathf.Abs(Vector3.Dot(c.relativeVelocity, contact.normal));
            if (speed < crashFrom) return;
            nextCrash = Time.unscaledTime + 0.15f;
            bool heavy = speed >= crashHeavy;
            var clip = SfxLibrary.Get(heavy ? "crash_heavy" : "crash_light") ?? SfxSynth.HitVariant(heavy ? 0 : 2);
            float vol = Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(crashFrom, crashHeavy * 1.5f, speed));
            SfxPlayer.Instance.Play(clip, contact.point, vol, heavy ? 0.9f : 1f + 0.1f * (Time.frameCount % 3));
        }

        private void Update()
        {
            if (!running || car == null) return;
            float master = SfxPlayer.Instance.volume;
            float speed = Mathf.Abs(car.SpeedKmh);
            float speedNorm = Mathf.Clamp01(speed / Mathf.Max(1f, car.maxSpeed));
            float throttle = car.inputEnabled ? Mathf.Max(0f, car.LastCommand.Throttle) : (input != null ? input.ThrottleLevel : 0f);

            // Virtual gearbox: each gear sweeps idle→redline over its share of the speed range.
            float g = speedNorm * gears;
            int newGear = Mathf.Min(gears - 1, (int)g);
            float rpm = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(g - newGear));
            if (speed < 3f) rpm = Mathf.Lerp(0.3f, 0.9f, throttle);            // standing (countdown): rev with the pedal
            if (newGear != gear) { if (newGear > gear) dipUntil = Time.unscaledTime + shiftDipSeconds; gear = newGear; }
            float pitch = Mathf.Lerp(idlePitch, redlinePitch, rpm);
            if (Time.unscaledTime < dipUntil) pitch -= shiftDip;
            bool boosting = car.IsBoosting;
            if (boosting) pitch *= nitroPitch;
            if (boosting && !wasBoosting) SfxPlayer.Instance.PlayUi(SfxSynth.NitroHiss, 0.7f);
            wasBoosting = boosting;
            engine.pitch = Mathf.Lerp(engine.pitch, pitch, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            engine.volume = (Mathf.Lerp(0.22f, 0.5f, throttle) + (boosting ? 0.08f : 0f)) * master;

            float slip = car.IsGrounded && speed > 15f ? car.SideSlipSpeed : 0f;
            float s01 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(screechFrom, screechFull, slip));
            // Smooth the tyre movement, then apply the user's master level immediately.
            screechGain = Mathf.Lerp(screechGain, s01 * 0.45f, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            screech.volume = screechGain * master;
            screech.pitch = 0.9f + 0.2f * s01;

            wind.volume = speedNorm * speedNorm * speedNorm * 0.3f * master;
            wind.pitch = 0.8f + 0.4f * speedNorm;
        }
    }
}
