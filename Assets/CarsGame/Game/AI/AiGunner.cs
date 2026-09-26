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
        [Tooltip("Mines go down when a rival follows this close behind (m) and this far to the side at most.")]
        public float mineDropMin = 15f, mineDropMax = 25f, mineDropLateral = 3f;
        [Tooltip("Base aim time before a shot (s). A lane change stretches it. 0 still commits the aim point.")]
        public float telegraphSeconds;
        [Tooltip("Hard tier: a calm machine-gun target draws a burst of 3 instead of 2.")]
        public bool hardTier;
        [Tooltip("Cone from the profile's aggression: patient aces only take clean shots, rookies spray.")]
        public bool coneFromAggression = true;
        public float rookieConeDegrees = 12f, aceConeDegrees = 6f;
        [Tooltip("Aggression from which the gunner leads a moving target (aims where it will be).")]
        [Range(0f, 1f)] public float leadFromAggression = 0.7f;

        /// <summary>The car this gunner is lining up (during the telegraph), else null. Read by the target's HUD.
        /// A wrecked gunner never aims (its tick stops while dead, so the field could go stale).</summary>
        public CarController AimTarget
        {
            get { if (health == null) health = GetComponent<CarHealth>(); return health != null && health.IsDead ? null : aimTarget; }
            private set => aimTarget = value;
        }
        private CarController aimTarget;
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
        private int pendingBurstShots = 1;
        private bool aimHeld;
        private int enabledTick = -1;
        private RaceDirector director;
        private CarController lateralTarget;
        private int lateralSampleTick = -1;
        private float lateralOffset;
        private float sampledRate;
        private CarController sampledTarget;

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
            if (car.Status != null && car.Status.IsJammed) { burstTicksRemaining = 0; ReleaseAim(); return false; }
            if (car.Weapons != null && car.Weapons.IsOverheated) { burstTicksRemaining = 0; ReleaseAim(); return false; }
            if (health != null && health.IsDead) { burstTicksRemaining = 0; ReleaseAim(); return false; }

            if (suppressed) { burstTicksRemaining = 0; ReleaseAim(); return false; }
            if (burstTicksRemaining > 0)
            {
                // The rest of the burst stays on the frozen point, even if the target has left the cone.
                burstTicksRemaining--;
                return true;
            }
            // The tick after the last shot: the shell already left along the committed barrel.
            if (aimHeld && AimTarget == null) ReleaseAim();

            if (enabledTick < 0) enabledTick = tick;
            if (startGraceSeconds > 0f && tick - enabledTick < SimulationDriver.SecondsToTicks(startGraceSeconds)) return false;
            // Keep sampling across the cooldown so a lane change is measured over ~0.2 s, not since the last shot.
            if (tick % 10 == 0 && AimTarget == null) SampleLateral(tick);
            if (tick < nextShotTick || !cannon.IsReady || cannon.locked) { CancelAim(); return false; }
            if (director == null) director = RaceDirector.Current ?? RaceDirector.Instance;

            if (AimTarget != null)
            {
                // The warning is the dodge window: the barrel stays on the point captured when it started.
                // Only a wreck or a respawn shield calls the shot off.
                var aimed = AimTarget.GetComponent<CarHealth>();
                if (aimed != null && (aimed.IsDead || aimed.IsInvulnerable)) { ReleaseAim(); return false; }
                if (tick < fireAtTick) return false;
                CancelAim();
            }
            else
            {
                if (tick % 10 != 0) return false;
                var target = sampledTarget;
                if (target == null) return FireAtUnit(tick);
                var weaponsNow = car.Weapons;
                bool machineGun = weaponsNow != null && weaponsNow.primaryType == PrimaryWeaponType.MachineGun;
                var decision = BotAim.Evaluate(sampledRate, telegraphSeconds, machineGun, hardTier);
                cannon.CommitAim(cannon.SolveAimPoint(target, decision.StripLateralLead));
                aimHeld = true;
                pendingBurstShots = decision.BurstShots;
                if (decision.TelegraphSeconds > 0f)
                {
                    AimTarget = target;
                    fireAtTick = tick + SimulationDriver.SecondsToTicks(decision.TelegraphSeconds);
                    return false;
                }
            }

            float aggression = profile != null ? profile.aggression : 0.5f;
            float randScale = (director != null && director.Rng != null) ? director.Rng.Range(RngStreams.Stream.Ai, 0.8f, 1.3f) : 1f;
            float scale = Mathf.Lerp(1.5f, 0.7f, aggression) * randScale;
            nextShotTick = tick + SimulationDriver.SecondsToTicks(fireCooldown * scale);
            ShotsRequested++;

            var weapons = car.Weapons;
            if (weapons != null && weapons.primaryType == PrimaryWeaponType.MachineGun && pendingBurstShots > 1)
            {
                burstTicksRemaining = SimulationDriver.SecondsToTicks(cannon.fireInterval) * (pendingBurstShots - 1);
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
            if (suppressed || (health != null && health.IsDead)) return false;
            // A planted charge: the button is now the detonator. Checked every tick, so a rival crossing it is not missed.
            if (weapons.secondaryType == SecondaryWeaponType.RemoteCharge)
            {
                var planted = weapons.PlantedCharge;
                if (planted != null) return TargetFinder.ChargeWorthFiring(car, planted, tick);
            }
            if (tick < nextSecondaryTick || !weapons.IsSecondaryReady(tick)) return false;
            if (tick % 10 != 0) return false;

            if (director == null) director = RaceDirector.Current ?? RaceDirector.Instance;
            // Mines and charges go down in front of a rival in the mirror; the freeze launcher shoots ahead.
            // A charge is blown by hand later, so anyone roughly behind will do; a mine needs a car right on its line.
            var pursuer = weapons.secondaryType == SecondaryWeaponType.RemoteCharge
                ? TargetFinder.FindPursuer(car, transform.position, -transform.forward, 5f, Mathf.Max(30f, mineDropMax), Mathf.Max(6f, mineDropLateral))
                : weapons.secondaryType != SecondaryWeaponType.FreezeLauncher
                ? TargetFinder.FindPursuer(car, transform.position, -transform.forward, mineDropMin, mineDropMax, mineDropLateral)
                : FindTarget();
            if (pursuer == null) return false;

            float aggression = profile != null ? profile.aggression : 0.5f;
            float randScale = (director != null && director.Rng != null) ? director.Rng.Range(RngStreams.Stream.Ai, 0.9f, 1.2f) : 1f;
            float scale = Mathf.Lerp(1.5f, 0.8f, aggression) * randScale;
            nextSecondaryTick = tick + SimulationDriver.SecondsToTicks(weapons.secondaryCooldown * scale);
            SecondaryRequested++;
            return true;
        }

        // Mission: no car in the cone, but a drone, trooper or the boss is. No telegraph (units have no HUD to warn).
        private bool FireAtUnit(int tick)
        {
            var muzzle = cannon.muzzle != null ? cannon.muzzle : transform;
            var unit = TargetFinder.FindUnit(car, muzzle.position, muzzle.forward, coneDegrees * 1.6f, minRange * 0.5f, maxRange);
            if (unit == null) return false;
            cannon.CommitAim(cannon.SolveAimPoint(unit));
            aimHeld = true;
            if (director == null) director = RaceDirector.Current ?? RaceDirector.Instance;
            float randScale = director != null && director.Rng != null ? director.Rng.Range(RngStreams.Stream.Ai, 0.8f, 1.2f) : 1f;
            nextShotTick = tick + SimulationDriver.SecondsToTicks(fireCooldown * 0.8f * randScale);
            ShotsRequested++;
            return true;
        }

        private void CancelAim() { AimTarget = null; fireAtTick = -1; }

        private void ReleaseAim()
        {
            CancelAim();
            aimHeld = false;
            if (cannon != null) cannon.ClearAimCommit();
        }

        private void OnDisable() { ReleaseAim(); }

        /// <summary>
        /// Sideways speed of the current target in the shooter's frame, sampled on this tick.
        /// The first sighting of a target reports 0 so the warning still freezes a full lead.
        /// </summary>
        private void SampleLateral(int tick)
        {
            var target = FindTarget();
            sampledTarget = target;
            sampledRate = 0f;
            if (target == null)
            {
                lateralTarget = null;
                lateralSampleTick = -1;
                return;
            }
            Vector3 right = car.transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
            else right.Normalize();
            float offset = Vector3.Dot(target.transform.position - car.transform.position, right);
            if (target == lateralTarget && lateralSampleTick >= 0 && tick > lateralSampleTick)
            {
                float dt = (tick - lateralSampleTick) * SimulationDriver.FixedStep;
                if (dt > 1e-4f) sampledRate = (offset - lateralOffset) / dt;
            }
            lateralTarget = target;
            lateralOffset = offset;
            lateralSampleTick = tick;
        }

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
