using UnityEngine;
using RacingGame.Core.Sim;

namespace RacingGame
{
    /// <summary>Chases a live rival in the arena using the existing car commands and gunner.</summary>
    [RequireComponent(typeof(CarController))]
    public sealed class ArenaDriver : MonoBehaviour, ICarInputSource
    {
        private ArenaLayout arena;
        private AiDriverProfile profile;
        private CarController car;
        private AiGunner gunner;
        private float stuckTime;

        private void Awake()
        {
            car = GetComponent<CarController>();
            car.playerControlled = false;
        }

        private void OnEnable() { if (car != null) car.Teleported += OnTeleported; }
        private void OnDisable() { if (car != null) car.Teleported -= OnTeleported; }
        private void OnTeleported() { stuckTime = 0f; }

        public void Initialize(ArenaLayout layout, AiDriverProfile driverProfile)
        {
            arena = layout;
            profile = driverProfile;
            car = GetComponent<CarController>();
            gunner = GetComponent<AiGunner>();
            stuckTime = 0f;
        }

        public DriveCommand Sample(int tick)
        {
            if (arena == null || car == null || !car.isActiveAndEnabled || !car.inputEnabled)
                return DriveCommand.None(tick);

            Vector3 localPosition = arena.transform.InverseTransformPoint(transform.position);
            bool nearWall = Mathf.Max(Mathf.Abs(localPosition.x), Mathf.Abs(localPosition.z)) > ArenaLayout.HalfSize - 6f;
            var target = TargetFinder.Find(car, transform.position + Vector3.up * 0.5f,
                transform.forward, 180f, 0.1f, ArenaLayout.HalfSize * 3f);
            Vector3 aim = !nearWall && target != null ? target.transform.position : arena.transform.position;
            Vector3 delta = aim - transform.position;
            delta.y = 0f;
            bool overturned = Vector3.Dot(transform.up, Vector3.up) < 0.2f;
            if (delta.sqrMagnitude < 4f && localPosition.y >= -2f && !overturned)
            {
                stuckTime = 0f;
                return new DriveCommand { Tick = tick, Brake = true };
            }

            Vector3 local = transform.InverseTransformDirection(delta.normalized);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            bool reverse = Mathf.Abs(angle) > 110f;
            float steer = Mathf.Clamp(angle / 35f - car.Body.angularVelocity.y * 0.2f, -1f, 1f);
            var p = profile != null ? profile : AiDriverProfile.Default;
            float desiredSpeed = Mathf.Min(60f, car.maxSpeed * p.topSpeedFactor)
                * Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(Mathf.Abs(angle) / 90f));
            bool brake = Mathf.Abs(car.SpeedKmh) > desiredSpeed + 5f;

            stuckTime = Mathf.Abs(car.SpeedKmh) < 2f || overturned ? stuckTime + SimulationDriver.FixedStep : 0f;
            bool reset = localPosition.y < -2f || stuckTime > 3f;
            if (reset) stuckTime = 0f;

            return new DriveCommand
            {
                Tick = tick,
                Throttle = brake ? 0f : (reverse ? -1f : 1f),
                Steer = reverse ? -steer : steer,
                Brake = brake,
                Fire = !reverse && gunner != null && gunner.WantsFire(tick),
                ResetEdge = reset
            };
        }
    }
}
