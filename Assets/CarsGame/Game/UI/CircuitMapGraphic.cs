using RacingGame.Core.Track;
using UnityEngine;
using UnityEngine.UI;

namespace RacingGame
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CircuitMapGraphic : MaskableGraphic
    {
        private const int BranchSamples = 17;
        private readonly Vector3[] road = new Vector3[72];
        private Vector3[] branch;
        private Vector2 minimum, maximum, start, heading;
        private bool configured;

        public void SetRoute(TrackSpline spline, float startDistance, TrackFork fork = null)
        {
            if (fork == null) branch = null;
            else
            {
                branch ??= new Vector3[BranchSamples];
                for (int i = 0; i < branch.Length; i++) branch[i] = fork.Position(i / (float)(branch.Length - 1));
            }
            SetMainRoad(spline, startDistance);
        }

        internal void SetPreview(TrackSpline spline, float startDistance, TrackSpline mountainForward, MountainFork course)
        {
            if (mountainForward == null) branch = null;
            else
            {
                branch ??= new Vector3[BranchSamples];
                TrackFork.SampleCenterline(mountainForward, course, branch);
            }
            SetMainRoad(spline, startDistance);
        }

        private void SetMainRoad(TrackSpline spline, float startDistance)
        {
            raycastTarget = false;
            for (int i = 0; i < road.Length; i++)
            {
                road[i] = spline.Position(spline.Length * i / road.Length);
                var point = TopDown(road[i]);
                minimum = i == 0 ? point : Vector2.Min(minimum, point);
                maximum = i == 0 ? point : Vector2.Max(maximum, point);
            }
            if (branch != null)
                foreach (var sample in branch)
                {
                    var point = TopDown(sample);
                    minimum = Vector2.Min(minimum, point);
                    maximum = Vector2.Max(maximum, point);
                }
            start = TopDown(spline.Position(startDistance));
            heading = TopDown(spline.Tangent(startDistance)).normalized;
            configured = true;
            SetVerticesDirty();
        }

        private static Vector2 TopDown(Vector3 position) => new Vector2(position.x, position.z);

        private void Projection(out Vector2 origin, out float scale, out float margin)
        {
            Rect rect = rectTransform.rect;
            float pixelScale = Mathf.Max(0.1f, canvas != null ? canvas.scaleFactor : 1f);
            margin = Mathf.Min(8f / pixelScale, Mathf.Min(rect.width, rect.height) * 0.18f);
            Vector2 size = maximum - minimum;
            scale = Mathf.Min((rect.width - 2f * margin) / Mathf.Max(size.x, 1f),
                (rect.height - 2f * margin) / Mathf.Max(size.y, 1f));
            origin = rect.center - (minimum + maximum) * (0.5f * scale);
        }

        public Vector2 MapPosition(Vector3 world)
        {
            Projection(out var origin, out float scale, out _);
            return origin + TopDown(world) * scale;
        }

        private static void DrawPath(VertexHelper mesh, Vector3[] points, Vector2 origin, float scale, Color tint, bool closed)
        {
            int segments = closed ? points.Length : points.Length - 1;
            for (int i = 0; i < segments; i++)
            {
                Vector2 a = origin + TopDown(points[i]) * scale;
                Vector2 b = origin + TopDown(points[(i + 1) % points.Length]) * scale;
                Vector2 side = Vector2.Perpendicular(b - a).normalized * 2f;
                int index = mesh.currentVertCount;
                mesh.AddVert(a + side, tint, Vector2.zero);
                mesh.AddVert(b + side, tint, Vector2.zero);
                mesh.AddVert(b - side, tint, Vector2.zero);
                mesh.AddVert(a - side, tint, Vector2.zero);
                mesh.AddTriangle(index, index + 1, index + 2);
                mesh.AddTriangle(index, index + 2, index + 3);
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!configured) return;
            Projection(out var origin, out float scale, out float margin);
            DrawPath(mesh, road, origin, scale, UiFactory.Muted, true);
            if (branch != null) DrawPath(mesh, branch, origin, scale, UiFactory.Accent, false);
            Vector2 at = origin + start * scale;
            Vector2 wing = Vector2.Perpendicular(heading) * (margin * 0.62f);
            int arrow = mesh.currentVertCount;
            mesh.AddVert(at + heading * margin, UiFactory.Accent, Vector2.zero);
            mesh.AddVert(at - heading * margin * 0.65f + wing, UiFactory.Accent, Vector2.zero);
            mesh.AddVert(at - heading * margin * 0.65f - wing, UiFactory.Accent, Vector2.zero);
            mesh.AddTriangle(arrow, arrow + 1, arrow + 2);
        }
    }
}
