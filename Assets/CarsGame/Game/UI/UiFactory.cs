using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RacingGame
{
    /// <summary>Small helpers to build the runtime UGUI/TMP hierarchy from code (no prefabs yet).</summary>
    public static class UiFactory
    {
        public static readonly Color Ink = new Color(0.035f, 0.05f, 0.08f);
        public static readonly Color Surface = new Color(0.065f, 0.085f, 0.12f, 0.97f);
        public static readonly Color Raised = new Color(0.13f, 0.17f, 0.23f);
        public static readonly Color Muted = new Color(0.68f, 0.75f, 0.83f);
        public static readonly Color Accent = new Color(1f, 0.8f, 0.25f);
        public static readonly Color Positive = new Color(0.25f, 0.9f, 0.7f);
        private static readonly Dictionary<(Material font, float outline), Material> textMaterials = new Dictionary<(Material, float), Material>();
        public static float TextScale => PlatformGlue.Save?.Data.settings.largeText == true ? 1.25f : 1f;

        /// <summary>Use shared immutable outline styles instead of TMP's per-label material instances.</summary>
        public static void SetOutline(TMP_Text text, float width)
        {
            var source = text.font.material;
            width = Mathf.Clamp01(width);
            var key = (source, width);
            if (!textMaterials.TryGetValue(key, out var material) || material == null)
            {
                material = new Material(source) { name = "UiTextOutline_" + width, hideFlags = HideFlags.HideAndDontSave };
                material.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
                material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0.8f));
                textMaterials[key] = material;
            }
            text.fontSharedMaterial = material;
            text.UpdateMeshPadding();
        }

        // Menus can scroll; keep their text and controls readable instead of fitting a desktop page
        // into a phone. World/HUD canvases retain their original coordinate system.
        public static float ReadableScale(int width, int height) => Mathf.Max(
            Mathf.Sqrt(Mathf.Max(1f, width) * Mathf.Max(1f, height) / (1920f * 1080f)),
            Mathf.Min(0.65f, Mathf.Max(1f, width) / 900f));

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

        private static Sprite rounded;

        /// <summary>9-slice rounded rectangle generated once (SDF in a 64² texture, 22 px border).</summary>
        public static Sprite RoundedSprite
        {
            get
            {
                if (rounded != null) return rounded;
                const int size = 64; const float radius = 20f, edge = 1.5f;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "UiRounded", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float qx = Mathf.Max(Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                        float qy = Mathf.Max(Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                        float dist = Mathf.Sqrt(qx * qx + qy * qy) - radius;
                        float a = Mathf.Clamp01(0.5f - dist / edge);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                tex.SetPixels32(px); tex.Apply(false, true);
                rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(22, 22, 22, 22));
                rounded.hideFlags = HideFlags.HideAndDontSave;
                return rounded;
            }
        }

        public static RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color? color = null, bool roundedCorners = true)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            if (color.HasValue)
            {
                var img = go.AddComponent<Image>(); img.color = color.Value; img.raycastTarget = false;
                if (roundedCorners) { img.sprite = RoundedSprite; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f; }
            }
            return rt;
        }

        /// <summary>Translucent card behind a group of controls so the moving backdrop stays visible around it.</summary>
        public static RectTransform Card(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, float alpha = 0.97f)
        {
            var rt = Panel(parent, name, anchorMin, anchorMax, offsetMin, offsetMax, new Color(Surface.r, Surface.g, Surface.b, alpha));
            var edge = rt.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0.45f, 0.58f, 0.75f, 0.18f);
            edge.effectDistance = new Vector2(1f, -1f);
            return rt;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, TextAlignmentOptions align,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            // Initialize TMP before parenting: an inactive HUD parent defers Awake and outline setup fails.
            var tmp = go.AddComponent<TextMeshProUGUI>();
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            tmp.text = text; tmp.fontSize = size; tmp.alignment = align; tmp.color = color ?? Color.white;
            tmp.raycastTarget = false; tmp.enableWordWrapping = true; tmp.overflowMode = TextOverflowModes.Overflow;
            SetOutline(tmp, 0.15f);
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
            Color face = color ?? Accent;
            var rt = Panel(parent, name, anchorMin, anchorMax, offsetMin, offsetMax, face);
            var img = rt.GetComponent<Image>(); img.raycastTarget = true;
            var btn = rt.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            colors.fadeDuration = 0.12f; btn.colors = colors;
            var text = Text(rt, "Label", label, fontSize, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(20f, 10f), new Vector2(-20f, -10f), face.grayscale > 0.5f ? Ink : Color.white);
            SetOutline(text, 0f);
            text.enableAutoSizing = true; text.fontSizeMax = fontSize; text.fontSizeMin = Mathf.Max(14f, fontSize * 0.5f);   // long translations shrink instead of escaping the button
            text.fontStyle = FontStyles.Bold;
            btn.interactable = onClick != null;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            rt.gameObject.AddComponent<UiButtonFx>();
            return btn;
        }

        /// <summary>Native scrolling, clipping and layout for lists of any length or viewport size.</summary>
        public static RectTransform Scroll(Transform parent, string name, Vector2 offsetMin, Vector2 offsetMax)
        {
            var root = Panel(parent, name, Vector2.zero, Vector2.one, offsetMin, offsetMax);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = Panel(root, "Viewport", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-18f, 0f), Color.clear, false);
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Panel(viewport, "Content", new Vector2(0f, 1f), Vector2.one, Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f; layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childControlHeight = true; layout.childForceExpandHeight = false;
            layout.childControlWidth = true; layout.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 42f;
            var track = Panel(root, "Scrollbar", new Vector2(1f, 0f), Vector2.one, new Vector2(-6f, 4f), new Vector2(0f, -4f), Raised);
            var handle = Panel(track, "Handle", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Muted);
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle; bar.targetGraphic = handle.GetComponent<Image>();
            track.GetComponent<Image>().raycastTarget = handle.GetComponent<Image>().raycastTarget = true;
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        /// <summary>Player names come from the platform: neutralise TMP rich-text tags.</summary>
        public static string Escape(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("<", "‹").Replace(">", "›");

        public static string FormatTime(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0d || seconds >= long.MaxValue / 1000d) return "--:--.---";
            // Match the HUD and stored best times: complete milliseconds, with one shared carry.
            return FormatMilliseconds((long)(seconds * 1000d));
        }

        public static string FormatMilliseconds(long ms)
        {
            if (ms < 0) return "--:--.---";
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}:{1:00}.{2:000}",
                ms / 60000, ms / 1000 % 60, ms % 1000);
        }
    }
}
