using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RacingGame.Core.Economy;

namespace RacingGame
{
    /// <summary>
    /// Livery stripes on the paint body: <see cref="LiveryClipper"/> cuts the stripe out of the body mesh once,
    /// and one extra renderer draws it with the body's own shader in the stripe colour. Hidden while the car
    /// is wrecked (the wreck tint only darkens the paint renderers).
    /// </summary>
    public sealed class CarLivery : MonoBehaviour
    {
        [Tooltip("Push-out along the face normal (m); large enough to avoid z-fighting on 24-bit depth.")]
        public float offset = 0.008f;

        private static Material liveryMaterial;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private MeshRenderer stripe;
        private Mesh mesh;
        private MaterialPropertyBlock block;
        private CarHealth health;
        private LiveryPattern pattern = LiveryPattern.None;

        public LiveryPattern Pattern => pattern;
        /// <summary>Triangles of the current stripe mesh (0 = none; test hook).</summary>
        public int TriangleCount { get; private set; }

        private void Awake()
        {
            health = GetComponent<CarHealth>();
            if (health != null) { health.OnDied += OnDied; health.OnRespawned += OnRespawned; }
        }

        private void OnDestroy()
        {
            if (health != null) { health.OnDied -= OnDied; health.OnRespawned -= OnRespawned; }
            if (mesh != null) Destroy(mesh);
        }

        private void OnDied(GameObject attacker) { if (stripe != null) stripe.enabled = false; }
        private void OnRespawned() { if (stripe != null) stripe.enabled = pattern != LiveryPattern.None; }

        public void Apply(LiveryPattern newPattern, Color color)
        {
            if (newPattern != pattern || stripe == null) Rebuild(newPattern);
            if (stripe == null) return;
            block ??= new MaterialPropertyBlock();
            block.Clear();
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            stripe.SetPropertyBlock(block);
            stripe.enabled = pattern != LiveryPattern.None && (health == null || !health.IsDead);
        }

        private void Rebuild(LiveryPattern newPattern)
        {
            pattern = newPattern;
            TriangleCount = 0;
            if (pattern == LiveryPattern.None) { if (stripe != null) stripe.enabled = false; return; }
            var body = FindBody();
            if (body == null) return;
            var source = body.GetComponent<MeshFilter>().sharedMesh;
            var verts = new List<Vector3>(source.vertexCount); source.GetVertices(verts);
            var tris = new List<int>();
            var mats = body.sharedMaterials;
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                var m = sub < mats.Length ? mats[sub] : null;
                if (m != null && m.name.Contains("CarPaint")) tris.AddRange(source.GetTriangles(sub));
            }
            var outV = new List<Vector3>(256); var outN = new List<Vector3>(256); var outT = new List<int>(384);
            TriangleCount = LiveryClipper.Build(verts, tris, LiveryClipper.RegionsFor(pattern, source.bounds), offset, outV, outN, outT);
            if (stripe == null)
            {
                var go = new GameObject("Livery");
                go.layer = body.gameObject.layer;
                go.transform.SetParent(body.transform, false);
                go.AddComponent<MeshFilter>();
                stripe = go.AddComponent<MeshRenderer>();
                stripe.shadowCastingMode = ShadowCastingMode.Off;
                stripe.lightProbeUsage = LightProbeUsage.Off;
                stripe.reflectionProbeUsage = ReflectionProbeUsage.Off;
                if (liveryMaterial == null)
                {
                    // Same shader as the paint so it lights identically; the name keeps CarPaint from adopting it.
                    liveryMaterial = new Material(body.sharedMaterial) { name = "Livery (runtime)" };
                }
                stripe.sharedMaterial = liveryMaterial;
            }
            if (mesh == null) { mesh = new Mesh { name = "LiveryMesh" }; stripe.GetComponent<MeshFilter>().sharedMesh = mesh; }
            mesh.Clear();
            mesh.SetVertices(outV); mesh.SetNormals(outN); mesh.SetTriangles(outT, 0);
            mesh.RecalculateBounds();
        }

        private MeshRenderer FindBody()
        {
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r == stripe) continue;
                var m = r.sharedMaterial;
                var mf = r.GetComponent<MeshFilter>();
                if (m != null && m.name.Contains("CarPaint") && mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable) return r;
            }
            return null;
        }
    }
}
