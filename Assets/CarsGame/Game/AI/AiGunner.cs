using UnityEngine;
using RacingGame.Core.Sim;

namespace RacingGame
{
    /// <summary>
    /// Decides when an AI car fires. Every 10 ticks it looks for a rival inside a narrow cone
    /// ahead with clear line of sight, then holds the trigger for one tick; the cannon's
    /// magazine and cooldown do the rest. Never fires in the first seconds after GO, at
    /// wrecked or invulnerable cars, or while rubber-banding says the AI is far ahead.
    /// </summary>
    public sealed class AiGunner : MonoBehaviour
    {
        [Tooltip("Seconds between shots (set from the difficulty tier).")]
        public float fireCooldown = 1.5f;
        public float coneDegrees = 9f;
        public float minRange = 6f, maxRange = 45f;
        [Tooltip("No shooting for this long after the race starts.")]
        public float startGraceSeconds = 3f;
        [Tooltip("Set by the race manager while this car is far ahead of the player.")]
        public bool suppressed;
        [Tooltip("Aim time before a shot (s): the target sees the warning this long. 0 = fire on sight.")]
        public float telegraphSeconds;
        [Tooltip("Cone from the profile's aggression: patient aces only take clean shots, rookies spray.")]
        public bool coneFromAggression = true;
        public float rookieConeDegrees = 12f, aceConeDegrees = 6f;
        [Tooltip("Aggression from which the gunner leads a moving target (aims where it will be).")]
        [Range(0f, 1f)] public float leadFromAggression = 0.7f;

        /// <summary>The car this gunner is lining up (during the telegraph), else null. Read by the target's HUD.</summary>
        public CarController AimTarget { get; private set; }
        private int fireAtTick = -1;

        public int ShotsRequested { get; private set; }
        public int SecondaryRequested { get; private set; }

        private CarController car;
        private CarCannon cannon;
        private CarHealth health;
        private AiDriverProfile profile;
        private int nextShotTick;
        private int nextSecondaryTick;
        private int burstTicksRemaining;
        private int enabledTick = -1;
        private RaceDirector director;

        private void Awake()
        {
            car = GetComponent<CarController>();
            cannon = GetComponent<CarCannon>();
            health = GetComponent<CarHealth>();
        }

        public void Initialize(AiDriverProfile driverProfile, float cooldown)
        {
            profile = driverProfile;
            fireCooldown = cooldown;
            enabledTick = -1;
            burstTicksRemaining = 0;
        }

        /// <summary>Called by <see cref="AiDriver"/> every tick; returns the Fire level for the command.</summary>
        public bool WantsFire(int tick)
        {
            if (car == null) car = GetComponent<CarController>();
            if (cannon == null) cannon = GetComponent<CarCannon>();
            if (health == null) health = GetComponent<CarHealth>();
            if (cannon == null || car == null) return false;
            if (car.Status != null && car.Status.IsJammed) { burstTicksRemaining = 0; return false; }
            if (car.Weapons != null && car.Weapons.IsOverheated) { burstTicksRemaining = 0; return false; }
            if (health != null && health.IsDead) { burstTicksRemaining = 0; return false; }

            if (suppressed) { burstTicksRemaining = 0; CancelAim(); return false; }
            if (burstTicksRemaining > 0)
            {
                if (FindTarget() == null) { burstTicksRemaining = 0; return false; }
                burstTicksRemaining--;
                return true;
            }

            if (enabledTick < 0) enabledTick = tick;
            if (startGraceSeconds > 0f && tick - enabledTick < SimulationDriver.SecondsToTicks(startGraceSeconds)) return false;
            if (suppressed || tick < nextShotTick || !cannon.IsReady || cannon.locked) { CancelAim(); return false; }
            if (director == null) director = RaceDirector.Current ?? RaceDirector.Instance;

            CarController target;
            if (AimTarget != null)
            {
                // Telegraph running: fire when it ends if anyone is still in the cone, else give up the shot.
                if (tick < fireAtTick) { if (tick % 5 == 0 && FindTarget() == null) CancelAim(); return false; }
                target = FindTarget();
                CancelAim();
                if (target == null) return false;
            }
            else
            {
                if (tick % 10 != 0) return false;
                target = FindTarget();
                if (target == null) return false;
                if (telegraphSeconds > 0f) { AimTarget = target; fireAtTick = tick + SimulationDriver.SecondsToTicks(telegraphSeconds); return false; }
            }

            float aggression = profile != null ? profile.aggression : 0.5f;
            float randScale = (director != null && director.Rng != null) ? director.Rng.Range(RngStreams.Stream.Ai, 0.8f, 1.3f) : 1f;
            float scale = Mathf.Lerp(1.5f, 0.7f, aggression) * randScale;
            nextShotTick = tick + SimulationDriver.SecondsToTicks(fireCooldown * scale);
            ShotsRequested++;

            var weapons = car.Weapons;
            if (weapons != null && weapons.primaryType == PrimaryWeaponType.MachineGun)
            {
                int shots = director != null && director.Rng != null ? director.Rng.Range(RngStreams.Stream.Ai, 3, 6) : 3;
                burstTicksRemaining = SimulationDriver.SecondsToTicks(cannon.fireInterval) * (shots - 1);
                nextShotTick += burstTicksRemaining;
            }
            return true;
        }

        /// <summary>Called by <see cref="AiDriver"/> every tick; returns the Secondary weapon fire command.</summary>
        public bool WantsSecondary(int tick)
        {
            if (car == null) car = GetComponent<CarController>();
            if (health == null) health = GetComponent<CarHealth>();
            if (car == null) return false;
            var weapons = car.Weapons;
            if (weapons == null || weapons.secondaryType == SecondaryWeaponType.None) return false;
            if (enabledTick < 0) enabledTick = tick;
            if (startGraceSeconds > 0f && tick - enabledTick < SimulationDriver.SecondsToTicks(startGraceSeconds)) return false;
            if (suppressed || tick < nextSecondaryTick || !weapons.IsSecondaryReady(tick)) return false;
            if (health != null && health.IsDead) return false;
            if (tick % 10 != 0) return false;

            if (director == null) director = RaceDirector.Current ?? RaceDirector.Instance;
            var pursuer = weapons.secondaryType == SecondaryWeaponType.Mines
                ? TargetFinder.FindPursuer(car, transform.position, -transform.forward, 15f, 25f, 3f)
                : FindTarget();
            if (pursuer == null) return false;

            float aggression = profile != null ? profile.aggression : 0.5f;
            float randScale = (director != null && director.Rng != null) ? director.Rng.Range(RngStreams.Stream.Ai, 0.9f, 1.2f) : 1f;
            float scale = Mathf.Lerp(1.5f, 0.8f, aggression) * randScale;
            nextSecondaryTick = tick + SimulationDriver.SecondsToTicks(weapons.secondaryCooldown * scale);
            SecondaryRequested++;
            return true;
        }

        private void CancelAim() { AimTarget = null; fireAtTick = -1; }

        private void OnDisable() { CancelAim(); }

        private CarController FindTarget()
        {
            var muzzle = cannon.muzzle != null ? cannon.muzzle : transform;
            float aggression = profile != null ? profile.aggression : 0.5f;
            float cone = coneFromAggression && profile != null ? Mathf.Lerp(rookieConeDegrees, aceConeDegrees, aggression) : coneDegrees;
            float lead = profile != null && aggression >= leadFromAggression ? cannon.shellSpeed : 0f;
            return TargetFinder.Find(car, muzzle.position, muzzle.forward, cone, minRange, maxRange, lead);
        }
    }
}
