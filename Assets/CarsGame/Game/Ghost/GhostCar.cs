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
        private static Material translucentMaterial;
        private GameObject sourcePrefab;

        public static GhostCar Spawn(GameObject carPrefab, GhostRecord record, Color tint)
        {
            // Build the body on an inactive template, as in the garage. Its gameplay components
            // never run Awake/OnEnable; only visible meshes become part of the replay.
            var go = new GameObject("Ghost_" + record.PlayerName);
            // A tint's alpha cannot make an opaque car shader transparent. Use the
            // shipped alpha-blended material, shared across every ghost and submesh.
            if (translucentMaterial == null) translucentMaterial = Resources.Load<Material>("FX_Flash");
            go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = translucentMaterial != null ? translucentMaterial
                : carPrefab.GetComponentInChildren<MeshRenderer>()?.sharedMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", tint); block.SetColor("_Color", tint); renderer.SetPropertyBlock(block);
            int ghostLayer = LayerMask.NameToLayer("Ignore Raycast");
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = ghostLayer >= 0 ? ghostLayer : 0;
            var ghost = go.AddComponent<GhostCar>();
            ghost.Record = record;
            ghost.sourcePrefab = carPrefab;
            ghost.RefreshModel();
            record.EvaluatePose(0f, out var p, out var rotation);
            go.transform.SetPositionAndRotation(p, rotation);
            return ghost;
        }

        internal void RefreshModel() => GetComponent<MeshFilter>().sharedMesh = GhostVisualMesh.Get(sourcePrefab, Record.BodyId);

        public void Begin() => Begin(RaceClock.Now);
        /// <summary>Start aligned to the race start (late-loaded ghosts join at the right elapsed time).</summary>
        public void Begin(double raceStartClock) { startClock = raceStartClock; running = true; }

        private void Update()
        {
            if (!running || Record == null) return;
            float t = (float)(RaceClock.Now - startClock);
            if (!Record.EvaluatePose(t, out var p, out var rotation)) return;
            transform.SetPositionAndRotation(p, rotation);
            if (t > Record.Duration + 5f) { running = false; gameObject.SetActive(false); }
        }
    }
}
