using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace RacingGame
{
    /// <summary>
    /// On-screen buttons for phones, bound to gamepad control paths so the existing Driving map
    /// handles them with no extra code. Shown when a touchscreen exists, hidden on first key press.
    /// Layout: steer pair bottom-left, Fire (big) + Brake bottom-right — one-hand playable with
    /// auto-throttle.
    /// </summary>
    public sealed class TouchControlsPresenter : MonoBehaviour
    {
        public RaceManager race;
        public bool forceShow;

        private Canvas canvas;
        private bool built;
        private bool shown;
        private bool keyboardChosen;          // latched for the scene once a key is pressed
        private bool touchSeen;
        private PlayerDriverInput hooked;
        private bool previousAutoThrottle;

        private void Awake()
        {
            if (race == null) race = GetComponent<RaceManager>() ?? FindAnyObjectByType<RaceManager>();
        }

        private void OnEnable() { Loc.LanguageChanged += OnLanguageChanged; }

        private void OnDisable()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
            if (hooked != null) { hooked.autoThrottle = previousAutoThrottle; hooked = null; }
            if (canvas != null) canvas.gameObject.SetActive(false);
            shown = false;
        }

        // Labels are baked at build time: rebuild the canvas on language change.
        private void OnLanguageChanged()
        {
            if (!built) return;
            Destroy(canvas.gameObject); canvas = null; built = false; shown = false;
        }

        private void Update()
        {
            if (!forceShow && Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) keyboardChosen = true;
            var flow = GetComponent<GameFlow>();
            bool inRace = flow == null || flow.State == FlowState.Racing;
            // Browsers report a Touchscreen device even on mouse-only desktops: on desktop wait for a real touch.
            if (!touchSeen && Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) touchSeen = true;
            bool wantShow = forceShow || (inRace && !keyboardChosen && (Application.isMobilePlatform || touchSeen));
            if (wantShow && !built) Build();
            if (built && shown != wantShow) { shown = wantShow; canvas.gameObject.SetActive(wantShow); }

            var input = race != null && race.Player != null ? race.Player.GetComponent<PlayerDriverInput>() : null;
            if (shown && input != null && hooked != input) { hooked = input; previousAutoThrottle = input.autoThrottle; input.autoThrottle = true; }
            if (!shown && hooked != null) { hooked.autoThrottle = previousAutoThrottle; hooked = null; }
            if (!shown && input != null && PlatformGlue.Save != null && PlatformGlue.Save.Data.settings.autoThrottle && !input.autoThrottle) input.autoThrottle = true;
        }

        private void Build()
        {
            built = true; shown = true;
            canvas = UiFactory.CreateOverlayCanvas("TouchControls", 5);
            canvas.transform.SetParent(transform, false);
            var root = canvas.transform;
            Button(root, "Left", "◄", "<Gamepad>/dpad/left", new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(200f, 200f));
            Button(root, "Right", "►", "<Gamepad>/dpad/right", new Vector2(0f, 0f), new Vector2(260f, 40f), new Vector2(420f, 200f));
            Button(root, "Fire", Loc.Get("touch.fire"), "<Gamepad>/rightShoulder", new Vector2(1f, 0f), new Vector2(-260f, 40f), new Vector2(-40f, 260f));
            Button(root, "Brake", Loc.Get("touch.brake"), "<Gamepad>/buttonSouth", new Vector2(1f, 0f), new Vector2(-480f, 40f), new Vector2(-280f, 160f));
            Button(root, "Reset", "R", "<Gamepad>/buttonNorth", new Vector2(1f, 1f), new Vector2(-140f, -140f), new Vector2(-40f, -40f));
        }

        private static void Button(Transform parent, string name, string label, string controlPath, Vector2 anchor, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = UiFactory.Panel(parent, name, anchor, anchor, offsetMin, offsetMax, new Color(1f, 1f, 1f, 0.22f));
            rt.GetComponent<Image>().raycastTarget = true;
            var btn = rt.gameObject.AddComponent<OnScreenButton>();
            btn.controlPath = controlPath;
            UiFactory.Text(rt, "Label", label, 40f, TMPro.TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }
    }
}
