using RacingGame.Core.Economy;
using RacingGame.Core.Sim;
using UnityEngine;

namespace RacingGame
{
    public sealed class TrainEscort : MonoBehaviour, ICarInputSource
    {
        public CarController Guard { get; private set; }
        public bool Released { get; private set; }
        public bool ApproachClear { get; private set; }
        public bool PlayerAtTail { get; private set; }

        private TrainRoute route;
        private RaceManager race;
        private CarController player;
        private CarHealth health;
        private AiGunner gunner;
        private int raceTicks, releaseTick;

        public void Initialize(TrainRoute route, RaceManager race)
        {
            this.route = route;
            this.race = race;
            var tail = route.Tail;
            var berth = tail.TransformPoint(new Vector3(-0.25f, 1.69f, -2f));
            var go = Instantiate(race.spawner.raceCarPrefab, berth, route.RampExit.rotation, tail);
            go.name = "TrainGuard";
            CarBody.Apply(go, CarBodies.Get("dune"));
            Teams.Set(go, Team.Enemy);
            var participant = go.GetComponent<RaceParticipant>();
            participant.displayName = Loc.Get("train.guard.name");
            participant.Site = race.spawner.Site;
            participant.Progress.enabled = false;
            participant.Weapons.SetPrimaryType(PrimaryWeaponType.MachineGun);
            participant.Weapons.secondaryType = SecondaryWeaponType.None;
            participant.Weapons.secondaryAmmoMax = 0;
            health = participant.Health;
            health.respawnOnDeath = false;
            health.showWorldBar = true;
            go.AddComponent<CarDamageSmoke>();
            go.GetComponent<CarPaint>().SetColor(new Color(0.79f, 0.18f, 0.055f));
            gunner = go.AddComponent<AiGunner>();
            gunner.Initialize(null, 1.6f);
            gunner.telegraphSeconds = BotAim.BaseTelegraph(RaceSetup.TierId);
            gunner.hardTier = RaceSetup.TierId == "hard";

            Guard = participant.Car;
            Guard.SetInputSource(this);
            Guard.enabled = false;
            Guard.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Guard.Body.isKinematic = true;
            var hud = race.GetComponent<HudScreen>();
            if (hud != null)
            {
                hud.KillFeed.RegisterRacerName(Guard.CarId, participant.displayName);
                health.HitResolved += (hit, result) => hud.ShowCombatHit(participant, hit, result);
            }
            race.OnRaceFinished += StopGuard;
        }

        public void Step(CarController player)
        {
            this.player = player;
            var tail = route.Tail;
            raceTicks++;
            PlayerAtTail = player != null && route.InCaptureZone(player.Body.position)
                && Vector3.Dot(player.Body.linearVelocity, tail.forward) > Mathf.Max(2f, route.Velocity.magnitude * 0.5f);
            if (health.IsDead) { ApproachClear = true; return; }
            if (!Released)
            {
                bool close = player != null && (player.Body.position - tail.position).sqrMagnitude < 65f * 65f;
                if (raceTicks >= SimulationDriver.SecondsToTicks(close ? 4f : 8f))
                {
                    releaseTick = raceTicks;
                    Released = true;
                    Guard.transform.SetParent(transform, true);
                    var body = Guard.Body;
                    body.isKinematic = false;
                    body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    body.linearVelocity = route.Velocity + route.RampExit.forward * 5.5f;
                }
                return;
            }

            var exit = route.RampExit;
            if (!Guard.enabled)
            {
                // Turn into the lane before tyre grip acts on the train's inherited sideways momentum.
                var angular = Guard.Body.angularVelocity;
                float yaw = Vector3.SignedAngle(Guard.transform.forward, tail.forward, Vector3.up) * Mathf.Deg2Rad;
                angular.y = Mathf.Clamp(yaw * 3.1f, -1.65f, 1.65f);
                Guard.Body.angularVelocity = angular;
                if (Vector3.Dot(Guard.Body.position - exit.position, exit.forward) > -0.8f
                    || raceTicks - releaseTick >= SimulationDriver.SecondsToTicks(2.5f))
                    Guard.enabled = true;
                else return;
            }
            bool guardAtRamp = (Guard.Body.position - exit.position).sqrMagnitude < 13f * 13f;
            bool passedGuard = PlayerAtTail && Vector3.Dot(player.Body.position - Guard.Body.position, tail.forward) > 6f;
            ApproachClear = !guardAtRamp || passedGuard;
        }

        public DriveCommand Sample(int tick)
        {
            if (race.State != RaceState.Racing || PauseController.IsPaused) return DriveCommand.None(tick);
            var tail = route.Tail;
            var exit = route.RampExit.position;
            var guard = Guard.Body;
            float lag = Vector3.Dot(exit - guard.position, tail.forward);
            float look = Mathf.Clamp(12f + Mathf.Abs(Guard.SpeedKmh) * 0.1f, 14f, 22f);
            var lane = route.AlongsideRoad(guard.position, look);
            var target = lane.position + lane.rotation * Vector3.forward * 2f;
            float speed = route.Velocity.magnitude + Mathf.Clamp(lag * 0.55f, -7f, 12f);
            if (player != null)
            {
                var offset = player.Body.position - exit;
                if (Vector3.Dot(offset, tail.right) > 4f && offset.sqrMagnitude < 38f * 38f
                    && Vector3.Dot(player.Body.position - guard.position, tail.forward) > -2f)
                {
                    target = player.Body.position + player.Body.linearVelocity * 0.85f;
                    speed = Mathf.Max(speed, player.Body.linearVelocity.magnitude);
                }
            }
            var local = Guard.transform.InverseTransformDirection(target - guard.position);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            speed *= Mathf.Lerp(1f, 0.55f, Mathf.Abs(angle) / 100f);
            float error = speed - Guard.SpeedKmh / 3.6f;
            return new DriveCommand
            {
                Tick = tick,
                Throttle = Mathf.Clamp01(error * 0.5f),
                Brake = error < -3f,
                Steer = Mathf.Clamp(angle / 28f - guard.angularVelocity.y * 0.22f, -1f, 1f),
                Fire = gunner.WantsFire(tick)
            };
        }

        private void StopGuard(RaceResults results)
        {
            Guard.enabled = false;
            Guard.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Guard.Body.isKinematic = true;
        }

        private void OnDisable()
        {
            if (race != null) race.OnRaceFinished -= StopGuard;
        }
    }
}
