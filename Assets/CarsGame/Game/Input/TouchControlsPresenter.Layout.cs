using System;
using RacingGame.Core.Save;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RacingGame
{
    public sealed partial class TouchControlsPresenter
    {
        private RectTransform[] movable;
        private RectTransform editShade, editToolbar;
        private Rect savedArea;
        private TouchPosition[] shownPositions;
        private bool editing;

        public bool EditingLayout => editing;

        public void OpenLayoutEditor()
        {
            if (editing || PlatformGlue.Save == null) return;
            if (!built) Build();
            editing = true;
            GetComponent<MenuScreens>()?.CoverTouchSettings(true);
            canvas.gameObject.SetActive(true);
            canvas.sortingOrder = 45;
            editShade = UiFactory.Panel(canvas.transform, "LayoutBackdrop", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, new Color(.025f, .04f, .07f, .97f), false);
            editShade.GetComponent<Image>().raycastTarget = true;
            editShade.SetAsFirstSibling();
            editToolbar = UiFactory.Panel(canvas.transform, "LayoutToolbar", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            float scale = Mathf.Max(.1f, canvas.scaleFactor);
            float half = Mathf.Min(128f, Screen.width * .21f) / scale, h = 44f / scale;
            var top = new Vector2(.5f, 1f);
            var hint = UiFactory.Text(editToolbar, "DragInstructions", Loc.Get("settings.drag_controls"), 16f / scale,
                TextAlignmentOptions.Center, new Vector2(0f, .55f), new Vector2(1f, .55f), new Vector2(20f, -36f / scale), new Vector2(-20f, 36f / scale), UiFactory.Muted);
            hint.raycastTarget = false;
            UiFactory.Button(editToolbar, "ResetTouchLayout", Loc.Get("settings.reset_layout"), ResetLayout,
                top, top, new Vector2(-half - 4f / scale, -h - 6f / scale), new Vector2(-4f / scale, -6f / scale), 15f / scale, UiFactory.Raised);
            UiFactory.Button(editToolbar, "FinishTouchLayout", Loc.Get("common.ok"), () => CloseLayoutEditor(),
                top, top, new Vector2(4f / scale, -h - 6f / scale), new Vector2(half + 4f / scale, -6f / scale), 15f / scale);
            targetScale = -1f;
            foreach (var control in movable)
            {
                control.gameObject.SetActive(true);
                var touchButton = control.GetComponent<TouchButton>();
                if (touchButton != null) touchButton.enabled = false;
                control.GetComponent<Button>().enabled = false;
                var drag = control.GetComponent<TouchLayoutDrag>() ?? control.gameObject.AddComponent<TouchLayoutDrag>();
                drag.Owner = this; drag.enabled = true;
            }
            Canvas.ForceUpdateCanvases(); ApplyTouchTargets();
            EventSystem.current?.SetSelectedGameObject(null);
        }

        public bool CloseLayoutEditor()
        {
            if (!editing) return false;
            editing = false;
            foreach (var control in movable)
            {
                var drag = control.GetComponent<TouchLayoutDrag>(); if (drag != null) drag.enabled = false;
                var touchButton = control.GetComponent<TouchButton>();
                if (touchButton != null) touchButton.enabled = true;
                control.GetComponent<Button>().enabled = true;
            }
            editToolbar.gameObject.SetActive(false); editShade.gameObject.SetActive(false);
            Destroy(editToolbar.gameObject); Destroy(editShade.gameObject);
            canvas.sortingOrder = 5;
            canvas.gameObject.SetActive(false);
            targetScale = -1f;
            GetComponent<MenuScreens>()?.CoverTouchSettings(false);
            return true;
        }

        private void ResetLayout()
        {
            var settings = PlatformGlue.Save.Data.settings;
            if (settings.touchPositions != null)
                settings.touchPositions = Array.FindAll(settings.touchPositions, p => p.leftHanded != settings.touchLeftHanded);
            PlatformGlue.Save.MarkDirty();
            targetScale = -1f; ApplyTouchTargets();
        }

        private static Vector2 Center(RectTransform control) =>
            RectTransformUtility.WorldToScreenPoint(null, control.TransformPoint(control.rect.center));

        internal void MoveButton(RectTransform control, Vector2 center)
        {
            Rect safe = Screen.safeArea;
            var size = Vector2.Scale(control.rect.size, control.lossyScale) * .5f;
            center.x = Mathf.Clamp(center.x, safe.xMin + size.x, safe.xMax - size.x);
            center.y = Mathf.Clamp(center.y, safe.yMin + size.y, safe.yMax - size.y);
            control.anchoredPosition += (center - Center(control)) / Mathf.Max(.1f, canvas.scaleFactor);
        }

        internal void StoreButton(RectTransform control)
        {
            var settings = PlatformGlue.Save.Data.settings;
            var entries = settings.touchPositions ?? Array.Empty<TouchPosition>();
            var point = Rect.PointToNormalized(Screen.safeArea, Center(control));
            var value = new TouchPosition { button = control.name, leftHanded = settings.touchLeftHanded, x = point.x, y = point.y };
            int index = Array.FindIndex(entries, p => p.button == value.button && p.leftHanded == value.leftHanded);
            if (index < 0) { index = entries.Length; Array.Resize(ref entries, index + 1); }
            entries[index] = value; settings.touchPositions = entries;
            PlatformGlue.Save.MarkDirty();
        }

        private void RestorePositions()
        {
            var settings = PlatformGlue.Save?.Data.settings;
            if (settings?.touchPositions == null || movable == null) return;
            foreach (var entry in settings.touchPositions)
            {
                if (entry.leftHanded != settings.touchLeftHanded || float.IsNaN(entry.x) || float.IsNaN(entry.y)) continue;
                foreach (var control in movable)
                    if (control.name == entry.button) { MoveButton(control, Rect.NormalizedToPoint(Screen.safeArea, new Vector2(entry.x, entry.y))); break; }
            }
        }
    }
}
