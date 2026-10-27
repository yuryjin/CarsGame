using UnityEngine;

namespace RacingGame
{
    /// <summary>One bomb per bridge jump. The car routes its primary input here before the cannon.</summary>
    [RequireComponent(typeof(CarController))]
    public sealed class ArenaBombDrop : MonoBehaviour
    {
        public const float CooldownSeconds = 8f;
        private ArenaLayout arena;
        private CarController car;
        private CarHealth health;
        private RaceParticipant participant;
        private bool usedThisJump, approachedBridge;
        private int nextDropTick;
        private Transform preview;
        public int Drops { get; private set; }
        public bool InDropWindow => arena != null && arena.Running && car != null && car.inputEnabled
            && (health == null || !health.IsDead) && !car.IsGrounded && approachedBridge
            && arena.IsBombZone(transform.position);
        public bool Ready => InDropWindow && !usedThisJump
            && SimulationDriver.Current != null && SimulationDriver.Current.Tick >= nextDropTick;

        public void Initialize(ArenaLayout layout)
        {
            arena = layout;
            car = GetComponent<CarController>();
            health = GetComponent<CarHealth>();
            participant = GetComponent<RaceParticipant>();
            car.ArenaBomb = this;
        }

        private void OnEnable()
        {
            car = GetComponent<CarController>();
            car.Teleported += OnTeleported;
        }
        private void OnDisable()
        {
            if (car != null) car.Teleported -= OnTeleported;
            if (preview != null) preview.gameObject.SetActive(false);
        }
        private void OnTeleported() { usedThisJump = false; approachedBridge = false; }

        /// <returns>True while primary fire belongs to the bomb, even after this jump's drop.</returns>
        public bool Step(bool fire, int tick)
        {
            if (arena == null || car == null) return false;
            if (car.IsGrounded)
            {
                usedThisJump = false;
                approachedBridge = transform.position.y - arena.transform.position.y > ArenaLayout.BridgeHeight - 3f;
            }
            if (!InDropWindow) return false;
            if (fire && !usedThisJump && tick >= nextDropTick && arena.DropBomb(car, tick))
            {
                usedThisJump = true;
                nextDropTick = tick + SimulationDriver.SecondsToTicks(CooldownSeconds);
                Drops++;
            }
            return true;
        }

        private void LateUpdate()
        {
            bool show = participant != null && participant.isPlayer && Ready && !PauseController.IsPaused;
            if (show && preview == null)
            {
                preview = new GameObject("BombLandingPreview").transform;
                preview.SetParent(arena.transform, false);
                MissionArt.Attach(preview, MissionArt.Cached("StrikeRing", MissionArt.Ring), MissionLayers.SoftUnit, false);
            }
            if (preview == null) return;
            preview.gameObject.SetActive(show);
            if (show)
            {
                preview.position = arena.BombLanding(car) + Vector3.up * 0.3f;
                preview.localScale = new Vector3(ArenaLayout.BombRadius, 1f, ArenaLayout.BombRadius);
            }
        }

        private void OnDestroy()
        {
            if (car != null && car.ArenaBomb == this) car.ArenaBomb = null;
            if (preview == null) return;
            if (Application.isPlaying) Destroy(preview.gameObject); else DestroyImmediate(preview.gameObject);
        }
    }
}
