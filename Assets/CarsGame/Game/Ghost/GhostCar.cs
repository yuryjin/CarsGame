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

        public static GhostCar Spawn(GameObject carPrefab, GhostRecord record, Color tint)
        {
            // Copy only the baked visuals: cloning a live car runs its Awake and registers it with
            // the race, while removing its controller fails RequireComponent dependency checks.
            var go = new GameObject("Ghost_" + record.PlayerName);
            // A tint's alpha cannot make an opaque car shader transparent. Use the
            // shipped alpha-blended material, shared across every ghost and submesh.
            if (translucentMaterial == null) translucentMaterial = Resources.Load<Material>("FX_Flash");
            var block = new MaterialPropertyBlock();
            foreach (var source in carPrefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = source.GetComponent<MeshRenderer>();
                if (renderer == null || source.sharedMesh == null) continue;
                var visual = new GameObject(source.name);
                visual.transform.SetParent(go.transform, false);
                var pose = carPrefab.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
                visual.transform.SetLocalPositionAndRotation(pose.GetColumn(3), pose.rotation);
                visual.transform.localScale = pose.lossyScale;
                visual.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                var r = visual.AddComponent<MeshRenderer>();
                if (translucentMaterial != null)
                {
                    var materials = new Material[source.sharedMesh.subMeshCount];
                    for (int i = 0; i < materials.Length; i++) materials[i] = translucentMaterial;
                    r.sharedMaterials = materials;
                }
                else r.sharedMaterials = renderer.sharedMaterials;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
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
