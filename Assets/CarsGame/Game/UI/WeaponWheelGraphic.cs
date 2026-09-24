using UnityEngine;
using UnityEngine.UI;

namespace RacingGame
{
    /// <summary>Ten primary weapon sectors, drawn without texture assets.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WeaponWheelGraphic : MaskableGraphic
    {
        private int selection;
        private bool unlocked;

        public void SetSelection(int value, bool machineGunUnlocked)
        {
            if (selection == value && unlocked == machineGunUnlocked) return;
            selection = value;
            unlocked = machineGunUnlocked;
            SetVerticesDirty();
        }

        public static Vector2 SlotDirection(int slot)
        {
            float angle = (90f - (slot - 1) * 360f / PrimaryWeaponCatalog.Count) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        public static int SlotAt(Vector2 pointer)
        {
            if (pointer.sqrMagnitude < 28f * 28f) return 0;
            float clockwise = Mathf.Repeat(90f - Mathf.Atan2(pointer.y, pointer.x) * Mathf.Rad2Deg + 18f, 360f);
            return Mathf.FloorToInt(clockwise / 36f) + 1;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Vector2 center = rectTransform.rect.center;
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.5f;
            for (int sector = 1; sector <= PrimaryWeaponCatalog.Count; sector++)
            {
                Color tint = sector == selection ? new Color(0.34f, 0.28f, 0.1f, 0.96f) : UiFactory.Surface;
                Color edge = sector == selection ? UiFactory.Accent : UiFactory.Raised;
                if (sector == 2 && !unlocked) { tint = UiFactory.Ink; edge = UiFactory.Raised; }
                float start = 90f - (sector - 1) * 36f - 16f;
                for (int i = 0; i < 12; i++)
                {
                    float a = (start + 32f * i / 12f) * Mathf.Deg2Rad;
                    float b = (start + 32f * (i + 1) / 12f) * Mathf.Deg2Rad;
                    Vector2 from = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Vector2 to = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                    Quad(vh, center, from, to, radius * 0.48f, radius - 4f, tint);
                    Quad(vh, center, from, to, radius - 4f, radius, edge);
                }
            }
        }

        private static void Quad(VertexHelper vh, Vector2 center, Vector2 a, Vector2 b, float inner, float outer, Color tint)
        {
            int index = vh.currentVertCount;
            vh.AddVert(center + a * inner, tint, Vector2.zero);
            vh.AddVert(center + a * outer, tint, Vector2.zero);
            vh.AddVert(center + b * outer, tint, Vector2.zero);
            vh.AddVert(center + b * inner, tint, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
