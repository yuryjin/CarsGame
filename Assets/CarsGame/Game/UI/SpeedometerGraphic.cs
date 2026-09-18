using UnityEngine;
using UnityEngine.UI;

namespace RacingGame
{
    /// <summary>A compact dial drawn in one UI mesh; labels are supplied by the HUD.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SpeedometerGraphic : MaskableGraphic
    {
        public const float MaxSpeed = 240f;
        public static readonly Color Accent = new Color(1f, 0.81f, 0.22f);
        public static readonly Color BoostColor = new Color(0.25f, 0.85f, 1f);
        private float reading;

        public void SetSpeed(float speed, bool boosting)
        {
            float next = Mathf.Clamp01(Mathf.Abs(speed) / MaxSpeed);
            Color tint = boosting ? BoostColor : Accent;
            if (Mathf.Approximately(next, reading) && color == tint) return;
            reading = next;
            color = tint;
            SetVerticesDirty();
        }

        private static Vector2 Direction(float fraction)
        {
            float angle = Mathf.Lerp(200f, -20f, fraction) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            float scale = Mathf.Min(rect.width / 244f, rect.height / 224f);
            Vector2 center = rect.min + new Vector2(rect.width * 0.5f, 92f * scale);
            Arc(vh, center, 104f * scale, 3f * scale, 0f, 1f, new Color(0.25f, 0.32f, 0.4f));
            Arc(vh, center, 104f * scale, 3f * scale, 0.8f, 1f, new Color(1f, 0.36f, 0.3f));
            Arc(vh, center, 104f * scale, 4f * scale, 0f, reading, color);

            for (int i = 0; i <= 24; i++)
            {
                bool major = i % 6 == 0;
                Vector2 direction = Direction(i / 24f);
                Color tick = i >= 20 ? new Color(1f, 0.4f, 0.35f) : new Color(0.76f, 0.83f, 0.9f, major ? 1f : 0.55f);
                Line(vh, center + direction * (major ? 85f : 91f) * scale,
                    center + direction * 97f * scale, (major ? 2.5f : 1.5f) * scale, tick);
            }

            Vector2 needle = Direction(reading);
            Vector2 side = new Vector2(-needle.y, needle.x) * 2.5f * scale;
            Quad(vh, center - needle * 9f * scale - side, center + needle * 68f * scale,
                center + needle * 68f * scale, center - needle * 9f * scale + side, color);
            Line(vh, center - Vector2.right * 4f * scale, center + Vector2.right * 4f * scale, 8f * scale, color);
        }

        private static void Arc(VertexHelper vh, Vector2 center, float radius, float width, float from, float to, Color tint)
        {
            int steps = Mathf.CeilToInt((to - from) * 80f);
            for (int i = 0; i < steps; i++)
            {
                Vector2 a = Direction(Mathf.Lerp(from, to, i / (float)steps));
                Vector2 b = Direction(Mathf.Lerp(from, to, (i + 1f) / steps));
                Quad(vh, center + a * radius, center + b * radius,
                    center + b * (radius - width), center + a * (radius - width), tint);
            }
        }

        private static void Line(VertexHelper vh, Vector2 from, Vector2 to, float width, Color tint)
        {
            Vector2 d = (to - from).normalized;
            Vector2 side = new Vector2(-d.y, d.x) * (width * 0.5f);
            Quad(vh, from - side, to - side, to + side, from + side, tint);
        }

        private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int start = vh.currentVertCount;
            vh.AddVert(a, tint, Vector2.zero); vh.AddVert(b, tint, Vector2.zero);
            vh.AddVert(c, tint, Vector2.zero); vh.AddVert(d, tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
