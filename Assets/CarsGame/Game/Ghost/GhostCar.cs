using UnityEngine;
using RacingGame.Core.Ghost;

namespace RacingGame
{
    /// <summary>
    /// Replays a <see cref="GhostRecord"/>: kinematic, no colliders, translucent copy of the car
    /// visuals. Starts at GO and follows the race clock so it stays in sync with pauses.
    /// </summary>
    public sealed class GhostCar : MonoBehaviour
    {
        public GhostRecord Record { get; private set; }
        private double startClock;
        private bool running;

        public static GhostCar Spawn(GameObject carPrefab, GhostRecord record, Color tint)
        {
            var go = Instantiate(carPrefab);
            go.name = "Ghost_" + record.PlayerName;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) if (!(mb is GhostCar)) Destroy(mb);
            var rb = go.GetComponent<Rigidbody>(); if (rb != null) Destroy(rb);
            var block = new MaterialPropertyBlock();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                block.Clear(); block.SetColor("_BaseColor", tint); block.SetColor("_Color", tint); r.SetPropertyBlock(block);
            }
            int ghostLayer = LayerMask.NameToLayer("Ignore Raycast");
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = ghostLayer >= 0 ? ghostLayer : 0;
            var ghost = go.AddComponent<GhostCar>();
            ghost.Record = record;
            record.Evaluate(0f, out var p, out var yaw);
            go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
            return ghost;
        }

        public void Begin() => Begin(RaceClock.Now);
        /// <summary>Start aligned to the race start (late-loaded ghosts join at the right elapsed time).</summary>
        public void Begin(double raceStartClock) { startClock = raceStartClock; running = true; }

        private void Update()
        {
            if (!running || Record == null) return;
            float t = (float)(RaceClock.Now - startClock);
            if (!Record.Evaluate(t, out var p, out var yaw)) return;
            transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
            if (t > Record.Duration + 5f) { running = false; gameObject.SetActive(false); }
        }
    }
}
