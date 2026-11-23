using UnityEngine;
using UnityEngine.UI;

namespace RacingGame
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ThreatShapeGraphic : MaskableGraphic
    {
        public int Level { get; private set; }

        public void Show(int level, Color tint)
        {
            color = tint;
            if (Level == level) return;
            Level = level;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            float x = rect.center.x, half = rect.width * 0.44f, y = rect.yMin, h = rect.height;
            var left = new Vector2(x - half, y);
            var right = new Vector2(x + half, y);
            var tip = new Vector2(x, y + h);
            if (Level == 0)
            {
                var notch = new Vector2(x, y + h * 0.62f);
                Triangle(mesh, left, tip, notch);
                Triangle(mesh, notch, tip, right);
            }
            else if (Level == 1) Triangle(mesh, left, tip, right);
            else
            {
                Triangle(mesh, left, new Vector2(x, y + h * 0.44f), right);
                Vector2 lift = new Vector2(0f, h * 0.56f);
                Triangle(mesh, left + lift, tip, right + lift);
            }
        }

        private void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c)
        {
            int i = mesh.currentVertCount;
            mesh.AddVert(a, color, Vector2.zero);
            mesh.AddVert(b, color, Vector2.zero);
            mesh.AddVert(c, color, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2);
        }
    }
}
