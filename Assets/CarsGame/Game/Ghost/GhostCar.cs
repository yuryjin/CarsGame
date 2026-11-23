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
        private Transform overlapCar;
        private Renderer ghostRenderer;
        private MaterialPropertyBlock colorBlock;
        private Color fullTint;
        private float visibleAlpha;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), ColorProperty = Shader.PropertyToID("_Color");

        public static GhostCar Spawn(GameObject carPrefab, GhostRecord record, Color tint, Transform overlapCar = null)
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
            block.SetColor(BaseColor, tint); block.SetColor(ColorProperty, tint); renderer.SetPropertyBlock(block);
            int ghostLayer = LayerMask.NameToLayer("Ignore Raycast");
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = ghostLayer >= 0 ? ghostLayer : 0;
            var ghost = go.AddComponent<GhostCar>();
            ghost.Record = record;
            ghost.overlapCar = overlapCar; ghost.ghostRenderer = renderer; ghost.colorBlock = block;
            ghost.fullTint = tint; ghost.visibleAlpha = tint.a;
            ghost.sourcePrefab = carPrefab;
            ghost.RefreshModel();
            ghost.Seek(0f);
            return ghost;
        }

        internal void RefreshModel() => GetComponent<MeshFilter>().sharedMesh = GhostVisualMesh.Get(sourcePrefab, Record.BodyId);

        public void Begin() => Begin(RaceClock.Now);
        /// <summary>Start aligned to the race start (late-loaded ghosts join at the right elapsed time).</summary>
        public void Begin(double raceStartClock) { startClock = raceStartClock; running = true; }

        public void Seek(float seconds)
        {
            if (Record.EvaluatePose(seconds, out var p, out var rotation))
                transform.SetPositionAndRotation(p, rotation);
        }

        private void LateUpdate()
        {
            if (overlapCar == null || !running || PauseController.IsPaused) return;
            float distance = Vector3.Distance(transform.position, overlapCar.position);
            float target = Mathf.Lerp(fullTint.a * .10f, fullTint.a, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.6f, 5f, distance)));
            float alpha = Mathf.MoveTowards(visibleAlpha, target, fullTint.a * Time.unscaledDeltaTime * 4f);
            if (Mathf.Approximately(alpha, visibleAlpha)) return;
            visibleAlpha = alpha; var color = fullTint; color.a = alpha;
            colorBlock.SetColor(BaseColor, color); colorBlock.SetColor(ColorProperty, color); ghostRenderer.SetPropertyBlock(colorBlock);
        }

        private void Update()
        {
            if (!running || Record == null) return;
            float t = (float)(RaceClock.Now - startClock);
            Seek(t);
            if (t > Record.Duration + 5f) { running = false; gameObject.SetActive(false); }
        }
    }
}
