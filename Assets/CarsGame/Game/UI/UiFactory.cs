using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RacingGame
{
    /// <summary>Small helpers to build the runtime UGUI/TMP hierarchy from code (no prefabs yet).</summary>
    public static class UiFactory
    {
        public static Canvas CreateOverlayCanvas(string name, int sortingOrder = 0)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            if (color.HasValue) { var img = go.AddComponent<Image>(); img.color = color.Value; img.raycastTarget = false; }
            return rt;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, TextAlignmentOptions align,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = size; tmp.alignment = align; tmp.color = color ?? Color.white;
            tmp.raycastTarget = false; tmp.enableWordWrapping = true; tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.outlineWidth = 0.15f; tmp.outlineColor = new Color(0f, 0f, 0f, 0.8f);
            return tmp;
        }

        public static Image Bar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color fill, out Image fillImage)
        {
            var bg = Panel(parent, name, anchorMin, anchorMax, offsetMin, offsetMax, new Color(0f, 0f, 0f, 0.6f)).GetComponent<Image>();
            var fillRt = Panel(bg.transform, "Fill", Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f), fill);
            fillImage = fillRt.GetComponent<Image>();
            fillImage.type = Image.Type.Filled; fillImage.fillMethod = Image.FillMethod.Horizontal; fillImage.fillOrigin = 0;
            return bg;
        }

        public static Button Button(Transform parent, string name, string label, System.Action onClick, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, float fontSize = 34f, Color? color = null)
        {
            var rt = Panel(parent, name, anchorMin, anchorMax, offsetMin, offsetMax, color ?? new Color(0.95f, 0.78f, 0.2f, 0.95f));
            var img = rt.GetComponent<Image>(); img.raycastTarget = true;
            var btn = rt.gameObject.AddComponent<Button>();
            var colors = btn.colors; colors.highlightedColor = new Color(1f, 1f, 1f, 1f); colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); btn.colors = colors;
            var text = Text(rt, "Label", label, fontSize, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f), new Color(0.1f, 0.08f, 0.02f));
            text.outlineWidth = 0f;
            text.enableAutoSizing = true; text.fontSizeMax = fontSize; text.fontSizeMin = Mathf.Max(14f, fontSize * 0.5f);   // long translations shrink instead of escaping the button
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        /// <summary>Player names come from the platform: neutralise TMP rich-text tags.</summary>
        public static string Escape(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("<", "‹").Replace(">", "›");

        public static string FormatTime(double seconds)
        {
            if (double.IsInfinity(seconds) || double.IsNaN(seconds) || seconds < 0) return "--:--.---";
            int m = (int)(seconds / 60d); double s = seconds - m * 60d;
            return $"{m}:{s:00.000}";
        }
    }
}
