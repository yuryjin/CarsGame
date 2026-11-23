using UnityEngine;
using UnityEngine.Rendering;

namespace RacingGame
{
    public sealed class DriftTireMarks : MonoBehaviour
    {
        public const int Limit = 128;
        public const float Lifetime = 4.5f;
        public int Count { get; private set; }
        private RaceManager race;
        private Mesh mesh;
        private readonly Vector3[] vertices = new Vector3[4 * Limit];
        private readonly float[] expires = new float[Limit];
        private readonly Vector3[] previous = new Vector3[2];
        private readonly bool[] hasPrevious = new bool[2];
        private int cursor;
        private float nextSample;

        public void Follow(RaceManager field) => race = field;

        private void MakeMesh()
        {
            var go = new GameObject("DriftTireMarks", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = AbyssTrack.SceneryMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            var triangles = new int[6 * Limit];
            var colors = new Color32[vertices.Length];
            for (int i = 0; i < Limit; i++)
            {
                int v = i * 4, t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
                for (int j = 0; j < 4; j++) colors[v + j] = new Color32(28, 28, 30, 255);
            }
            mesh = new Mesh { name = "Transient drift rubber" };
            mesh.MarkDynamic(); mesh.vertices = vertices; mesh.triangles = triangles; mesh.colors32 = colors;
            var normals = new Vector3[vertices.Length];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.normals = normals; go.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        private void LateUpdate()
        {
            if (race == null || race.Player == null || PauseController.IsPaused || Time.time < nextSample) return;
            float now = Time.time;
            nextSample = now + .07f;
            bool dirty = false;
            if (mesh != null && Count > 0)
                for (int i = 0; i < Limit; i++)
                    if (expires[i] > 0f && expires[i] <= now)
                    {
                        for (int v = 0; v < 4; v++) vertices[i * 4 + v] = Vector3.zero;
                        expires[i] = 0f; Count--; dirty = true;
                    }

            var car = race.Player.Car;
            for (int i = 0; i < 2; i++)
            {
                Transform wheel = i == 0 ? car.rearLeftWheel : car.rearRightWheel;
                if (race.State != RaceState.Racing || !car.IsDrifting ||
                    !car.TryGetTireContact(wheel, out var hit, out var surface) || surface != TireSurface.Asphalt)
                { hasPrevious[i] = false; continue; }
                Vector3 point = transform.InverseTransformPoint(hit.point + hit.normal * .023f);
                Vector3 delta = point - previous[i];
                if (hasPrevious[i] && delta.sqrMagnitude > .018f && delta.sqrMagnitude < 20f)
                {
                    if (mesh == null) MakeMesh();
                    Vector3 side = Vector3.Cross(transform.InverseTransformDirection(hit.normal), delta).normalized * .115f;
                    int v = cursor * 4;
                    vertices[v] = previous[i] - side; vertices[v + 1] = previous[i] + side;
                    vertices[v + 2] = point - side; vertices[v + 3] = point + side;
                    if (expires[cursor] == 0f) Count++;
                    expires[cursor] = now + Lifetime; cursor = (cursor + 1) % Limit; dirty = true;
                }
                hasPrevious[i] = true; previous[i] = point;
            }
            if (dirty) { mesh.vertices = vertices; mesh.RecalculateBounds(); }
        }

        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
