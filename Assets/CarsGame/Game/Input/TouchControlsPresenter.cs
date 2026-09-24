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

        /// <summary>True while the on-screen buttons are visible; the HUD moves its corner widgets out of their way.</summary>
        public static bool IsShown { get; private set; }
        public static event System.Action<bool> ShownChanged;
        private static void Publish(bool value) { if (IsShown == value) return; IsShown = value; ShownChanged?.Invoke(value); }

        private Canvas canvas;
        private bool built;
        private bool shown;
        private bool keyboardChosen;          // latched for the scene once a key is pressed
        private bool touchSeen;
        private PlayerDriverInput hooked;
        private bool previousAutoThrottle;
        private bool previousAutoSecondary;
        private TMPro.TextMeshProUGUI secondaryLabel;
        private string secondaryKey = "touch.secondary";

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
            Publish(false);
        }

        // Labels are baked at build time: rebuild the canvas on language change.
        private void OnLanguageChanged()
        {
            if (!built) return;
            canvas.gameObject.SetActive(false); Destroy(canvas.gameObject); canvas = null; built = false; shown = false;
            Publish(false);
        }

        private void Update()
        {
            if (!forceShow && Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) keyboardChosen = true;
            var flow = GetComponent<GameFlow>();
            bool inRace = flow == null || flow.State == FlowState.Racing;
            // Browsers report a Touchscreen device even on mouse-only desktops: on desktop wait for a real touch.
            if (!touchSeen && Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) touchSeen = true;
            bool wantShow = inRace && (forceShow || (!keyboardChosen && (Application.isMobilePlatform || touchSeen)));
            if (wantShow && !built) Build();
            shown = wantShow;
            if (built && canvas.gameObject.activeSelf != (wantShow && !PauseController.IsPaused)) canvas.gameObject.SetActive(wantShow && !PauseController.IsPaused);
            Publish(built && shown);

            var input = race != null && race.Player != null ? race.Player.GetComponent<PlayerDriverInput>() : null;
            if (shown && input != null && hooked != input) { hooked = input; previousAutoThrottle = input.autoThrottle; previousAutoSecondary = input.autoSecondary; input.autoThrottle = true; input.autoSecondary = true; }
            if (!shown && hooked != null) { hooked.autoThrottle = previousAutoThrottle; hooked.autoSecondary = previousAutoSecondary; hooked = null; }
            if (!shown && input != null && PlatformGlue.Save != null && PlatformGlue.Save.Data.settings.autoThrottle && !input.autoThrottle) input.autoThrottle = true;
            if (shown) UpdateSecondaryLabel(input != null ? input.GetComponent<CarWeapons>() : null);
        }

        // The second button says what a press does now: drop a mine, plant a charge, blow the planted one, freeze.
        private void UpdateSecondaryLabel(CarWeapons weapons)
        {
            if (secondaryLabel == null) return;
            var type = weapons != null ? weapons.secondaryType : SecondaryWeaponType.None;
            secondaryLabel.transform.parent.gameObject.SetActive(type != SecondaryWeaponType.None);
            if (type == SecondaryWeaponType.None) return;
            string key = type == SecondaryWeaponType.FreezeLauncher ? "touch.freeze"
                : type == SecondaryWeaponType.RemoteCharge ? (weapons.PlantedCharge != null ? "touch.detonate" : "touch.charge")
                : "touch.secondary";
            if (key == secondaryKey) return;
            secondaryKey = key;
            secondaryLabel.text = Loc.Get(key);
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
            secondaryLabel = Button(root, "Secondary", Loc.Get("touch.secondary"), "<Gamepad>/leftShoulder", new Vector2(1f, 0f), new Vector2(-480f, 180f), new Vector2(-280f, 300f));
            secondaryKey = "touch.secondary";
            secondaryLabel.transform.parent.gameObject.SetActive(false);
            Button(root, "Brake", Loc.Get("touch.brake"), "<Gamepad>/buttonSouth", new Vector2(1f, 0f), new Vector2(-480f, 40f), new Vector2(-280f, 160f));
            Button(root, "Reset", "↺", "<Gamepad>/buttonNorth", new Vector2(1f, 1f), new Vector2(-140f, -140f), new Vector2(-40f, -40f));
        }

        private static TMPro.TextMeshProUGUI Button(Transform parent, string name, string label, string controlPath, Vector2 anchor, Vector2 offsetMin, Vector2 offsetMax)
        {
            var face = UiFactory.Button(parent, name, label, null, anchor, anchor, offsetMin, offsetMax, 34f, name == "Fire" ? UiFactory.Accent : UiFactory.Surface);
            // OnScreenButton owns press/release; the uGUI button supplies visual feedback only.
            face.interactable = true; face.navigation = new Navigation { mode = Navigation.Mode.None };
            var btn = face.gameObject.AddComponent<OnScreenButton>();
            btn.controlPath = controlPath;
            return face.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        }
    }
}
