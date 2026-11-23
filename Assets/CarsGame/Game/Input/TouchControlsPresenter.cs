using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace RacingGame
{
    /// <summary>
    /// On-screen buttons for phones, bound to gamepad control paths so the existing Driving map
    /// handles them with no extra code. A real touch selects touch controls; a key selects keyboard controls.
    /// Layout: steer pair bottom-left, Fire (big) + Brake bottom-right — one-hand playable with
    /// auto-throttle.
    /// </summary>
    public sealed partial class TouchControlsPresenter : MonoBehaviour
    {
        public RaceManager race;
        public bool forceShow;

        /// <summary>True while the on-screen buttons are visible; the HUD moves its corner widgets out of their way.</summary>
        public static bool IsShown { get; private set; }
        public static event System.Action<bool> ShownChanged;
        private static void Publish(bool value) { if (IsShown == value) return; IsShown = value; ShownChanged?.Invoke(value); }

        private Canvas canvas;
        private PointerEventData targetPointer;
        private readonly List<RaycastResult> pointerHits = new List<RaycastResult>(8);
        private bool built;
        private bool shown;
        private bool keyboardChosen;
        private bool touchSeen;
        private PlayerDriverInput hooked;
        private bool previousAutoThrottle;
        private bool previousAutoSecondary;
        private TMPro.TextMeshProUGUI secondaryLabel;
        private TMPro.TextMeshProUGUI fireLabel;
        private Button swapButton;
        private TMPro.TextMeshProUGUI launchLabel;
        private PerfectStart launch;
        private RectTransform resetButton, brakeButton, leftButton, rightButton, rearButton;
        private float targetScale = -1f;
        private float targetTextScale;
        private bool targetLarge, targetLeft, targetCompact;
        private string secondaryKey = "touch.secondary";

        private void Awake()
        {
            if (race == null) race = GetComponent<RaceManager>() ?? FindAnyObjectByType<RaceManager>();
        }

        private void OnEnable() { Loc.LanguageChanged += OnLanguageChanged; }

        private void OnDisable()
        {
            CloseLayoutEditor();
            Loc.LanguageChanged -= OnLanguageChanged;
            if (hooked != null) { hooked.autoThrottle = PlatformGlue.Save?.Data.settings.autoThrottle ?? previousAutoThrottle; hooked.autoSecondary = previousAutoSecondary; hooked = null; }
            if (canvas != null) canvas.gameObject.SetActive(false);
            shown = false;
            Publish(false);
        }

        // Labels are baked at build time: rebuild the canvas on language change.
        private void OnLanguageChanged()
        {
            if (!built) return;
            CloseLayoutEditor();
            canvas.gameObject.SetActive(false); Destroy(canvas.gameObject); canvas = null; built = false; shown = false;
            Publish(false);
        }

        private void Update()
        {
            if (editing) { ApplyTouchTargets(); return; }
            var flow = GetComponent<GameFlow>();
            bool inRace = flow == null || flow.State == FlowState.Racing;
            if (!forceShow && inRace && Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) keyboardChosen = true;
            // Browsers report a Touchscreen device even on mouse-only desktops: on desktop wait for a real touch.
            if (!touchSeen && Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) touchSeen = true;
            // Hybrid devices can return to touch after using a keyboard, without reloading the scene.
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) keyboardChosen = false;
            bool wantShow = inRace && (forceShow || (!keyboardChosen && (Application.isMobilePlatform || touchSeen)));
            if (wantShow && !built) Build();
            shown = wantShow;
            bool controlsActive = wantShow && !PauseController.IsPaused && (race == null || !race.CanShowResults);
            if (built && canvas.gameObject.activeSelf != controlsActive) canvas.gameObject.SetActive(controlsActive);
            Publish(built && shown);

            var input = race != null && race.Player != null ? race.Player.GetComponent<PlayerDriverInput>() : null;
            if (shown && input != null && hooked != input) { hooked = input; previousAutoThrottle = input.autoThrottle; previousAutoSecondary = input.autoSecondary; input.autoThrottle = true; input.autoSecondary = true; }
            if (!shown && hooked != null) { hooked.autoThrottle = PlatformGlue.Save?.Data.settings.autoThrottle ?? previousAutoThrottle; hooked.autoSecondary = previousAutoSecondary; hooked = null; }
            if (!shown && input != null && PlatformGlue.Save != null && PlatformGlue.Save.Data.settings.autoThrottle && !input.autoThrottle) input.autoThrottle = true;
            if (shown)
            {
                if (launch == null && race != null) launch = race.GetComponent<PerfectStart>();
                launchLabel.transform.parent.gameObject.SetActive(launch != null && launch.AcceptingInput);
                ApplyTouchTargets();
                if (controlsActive && input != null && race.State == RaceState.Racing) ReadTargetTouches(input);
                var weapons = input != null ? input.GetComponent<CarWeapons>() : null;
                var cannon = input != null ? input.GetComponent<CarCannon>() : null;
                bool primary = cannon != null && cannon.enabled && !cannon.locked;
                fireLabel.transform.parent.gameObject.SetActive(primary);
                swapButton.gameObject.SetActive(primary && weapons != null);
                UpdateSecondaryLabel(weapons);
            }
        }

        private void ReadTargetTouches(PlayerDriverInput input)
        {
            var screen = Touchscreen.current;
            if (screen == null) return;
            foreach (var finger in screen.touches)
            {
                if (!finger.press.wasPressedThisFrame) continue;
                Vector2 point = finger.position.ReadValue();
                var events = EventSystem.current;
                if (events != null)
                {
                    targetPointer ??= new PointerEventData(events);
                    targetPointer.position = point;
                    pointerHits.Clear(); events.RaycastAll(targetPointer, pointerHits);
                    if (pointerHits.Exists(h => h.gameObject.GetComponentInParent<Selectable>() != null
                        || h.gameObject.GetComponentInParent<TouchButton>() != null)) continue;
                }
                var camera = Camera.main;
                if (camera == null || !Physics.Raycast(camera.ScreenPointToRay(point), out var hit, camera.farClipPlane,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                var rival = hit.collider.GetComponentInParent<CarController>();
                if (rival != null) input.GetComponent<CarCannon>()?.PreferTouchTarget(rival);
            }
        }

        // The second button says what a press does now: drop a mine, plant a charge, blow the planted one, freeze.
        private void UpdateSecondaryLabel(CarWeapons weapons)
        {
            if (secondaryLabel == null) return;
            var type = weapons != null && weapons.enabled ? weapons.secondaryType : SecondaryWeaponType.None;
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
            leftButton = (RectTransform)Button(root, "Left", "◄", "<Gamepad>/dpad/left", new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(200f, 200f)).transform.parent;
            rightButton = (RectTransform)Button(root, "Right", "►", "<Gamepad>/dpad/right", new Vector2(0f, 0f), new Vector2(260f, 40f), new Vector2(420f, 200f)).transform.parent;
            fireLabel = Button(root, "Fire", Loc.Get("touch.fire"), "<Gamepad>/rightShoulder", new Vector2(1f, 0f), new Vector2(-260f, 40f), new Vector2(-40f, 260f));
            swapButton = UiFactory.Button(root, "SwapWeapon", Loc.Get("loadout.swap"), () => hooked?.CycleLoadout(),
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-260f, 280f), new Vector2(-40f, 370f), 26f, UiFactory.Surface);
            secondaryLabel = Button(root, "Secondary", Loc.Get("touch.secondary"), "<Gamepad>/leftShoulder", new Vector2(1f, 0f), new Vector2(-480f, 180f), new Vector2(-280f, 300f));
            secondaryKey = "touch.secondary";
            secondaryLabel.transform.parent.gameObject.SetActive(false);
            brakeButton = (RectTransform)Button(root, "Brake", Loc.Get("touch.brake"), "<Gamepad>/buttonSouth", new Vector2(1f, 0f), new Vector2(-480f, 40f), new Vector2(-280f, 160f)).transform.parent;
            resetButton = (RectTransform)Button(root, "Reset", Loc.Get("touch.reset"), "<Gamepad>/buttonNorth", new Vector2(1f, 1f), new Vector2(-140f, -140f), new Vector2(-40f, -40f)).transform.parent;
            rearButton = (RectTransform)Button(root, "RearView", Loc.Get("touch.rear_view"), "<Gamepad>/rightStickPress",
                Vector2.zero, Vector2.zero, Vector2.one).transform.parent;
            launchLabel = Button(root, "Launch", Loc.Get("touch.launch"), "<Gamepad>/rightTrigger",
                new Vector2(0.5f, 0f), new Vector2(-200f, 40f), new Vector2(200f, 190f));
            movable = new[] { leftButton, rightButton, brakeButton, (RectTransform)fireLabel.transform.parent,
                (RectTransform)secondaryLabel.transform.parent, (RectTransform)swapButton.transform,
                resetButton, rearButton, (RectTransform)launchLabel.transform.parent };
            targetScale = -1f;
        }

        private void ApplyTouchTargets()
        {
            float scale = Mathf.Max(0.1f, canvas.scaleFactor);
            var settings = PlatformGlue.Save?.Data.settings;
            bool large = settings != null && settings.touchLargeButtons;
            bool left = settings != null && settings.touchLeftHanded;
            bool compact = settings != null && settings.compactHud;
            float textScale = UiFactory.TextScale;
            if (Mathf.Approximately(scale, targetScale) && targetLarge == large && targetLeft == left && targetCompact == compact && targetTextScale == textScale
                && savedArea == Screen.safeArea && ReferenceEquals(shownPositions, settings?.touchPositions)) return;
            targetScale = scale;
            savedArea = Screen.safeArea;
            shownPositions = settings?.touchPositions;
            targetTextScale = textScale;
            targetLarge = large; targetLeft = left; targetCompact = compact;
            float size = large ? 1.25f : 1f;
            float Scaled(float value) => 40f + (value - 40f) * size;
            void Place(RectTransform control, Vector2 anchor, Vector2 lo, Vector2 hi)
            {
                control.anchorMin = control.anchorMax = anchor;
                control.offsetMin = lo; control.offsetMax = hi;
            }
            // Relocate the steering group without reversing the left/right order of its arrows.
            float steeringRight = Scaled(420f);
            float leftMin = left ? -steeringRight : 40f;
            Place(leftButton, left ? new Vector2(1f, 0f) : Vector2.zero,
                new Vector2(leftMin, 40f), new Vector2(leftMin + 160f * size, Scaled(200f)));
            float rightMin = leftMin + 220f * size;
            Place(rightButton, left ? new Vector2(1f, 0f) : Vector2.zero,
                new Vector2(rightMin, 40f), new Vector2(rightMin + 160f * size, Scaled(200f)));
            void Action(RectTransform control, float near, float far, float bottom, float top)
            {
                Place(control, left ? Vector2.zero : new Vector2(1f, 0f),
                    new Vector2(left ? Scaled(near) : -Scaled(far), Scaled(bottom)),
                    new Vector2(left ? Scaled(far) : -Scaled(near), Scaled(top)));
            }
            Action((RectTransform)fireLabel.transform.parent, 40f, 260f, 40f, 260f);
            Action(brakeButton, 280f, 480f, 40f, 160f);
            Action((RectTransform)secondaryLabel.transform.parent, 280f, 480f, 180f, 300f);
            Action((RectTransform)swapButton.transform, 40f, 260f, 280f, 370f);
            float edge = 44f / scale;
            float rearNear = steeringRight + 8f / scale, rearWidth = Mathf.Max(150f, 68f / scale);
            Place(rearButton, left ? new Vector2(1f, 0f) : Vector2.zero,
                new Vector2(left ? -rearNear - rearWidth : rearNear, 40f),
                new Vector2(left ? -rearNear : rearNear + rearWidth, 40f + Mathf.Max(edge, 100f)));
            var rearLabel = rearButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            rearLabel.fontSizeMin = 11f / scale; rearLabel.fontSizeMax = Mathf.Max(24f, 13f / scale) * textScale;
            float reset = Mathf.Max(100f, edge);
            float resetTop = (HudScreen.TopControlsBottomPixels(scale, compact) + 8f) / scale;
            float resetWidth = Mathf.Max(reset, 84f / scale);
            Place(resetButton, Vector2.one, new Vector2(-40f - resetWidth, -resetTop - reset), new Vector2(-40f, -resetTop));
            var resetLabel = resetButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            resetLabel.fontSizeMin = 12f / scale;
            resetLabel.fontSizeMax = Mathf.Max(26f, 14f / scale) * textScale;
            fireLabel.fontSizeMax = secondaryLabel.fontSizeMax = 34f * textScale;
            brakeButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().fontSizeMax = 34f * textScale;
            void MinimumHeight(RectTransform control)
            {
                var hi = control.offsetMax;
                hi.y = Mathf.Max(hi.y, control.offsetMin.y + edge);
                control.offsetMax = hi;
            }
            MinimumHeight(brakeButton);
            MinimumHeight((RectTransform)swapButton.transform);
            var secondary = (RectTransform)secondaryLabel.transform.parent;
            MinimumHeight(secondary);
            var launchRect = (RectTransform)launchLabel.transform.parent;
            launchRect.offsetMax = new Vector2(200f, 40f + Mathf.Max(150f, 64f / scale));
            launchLabel.enableWordWrapping = true;
            launchLabel.fontSizeMin = Mathf.Max(30f, 14f / scale);
            launchLabel.fontSizeMax = launchLabel.fontSizeMin * textScale;
            var swapLabel = swapButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            swapLabel.rectTransform.offsetMin = new Vector2(8f, 8f);
            swapLabel.rectTransform.offsetMax = new Vector2(-8f, -8f);
            swapLabel.enableWordWrapping = false;
            swapLabel.fontSizeMin = Mathf.Max(26f, 12f / scale);
            swapLabel.fontSizeMax = swapLabel.fontSizeMin * textScale;
            RestorePositions();
        }

        private static TMPro.TextMeshProUGUI Button(Transform parent, string name, string label, string controlPath, Vector2 anchor, Vector2 offsetMin, Vector2 offsetMax)
        {
            var face = UiFactory.Button(parent, name, label, null, anchor, anchor, offsetMin, offsetMax, 34f, name == "Fire" ? UiFactory.Accent : UiFactory.Surface);
            // OnScreenButton owns press/release; the uGUI button supplies visual feedback only.
            face.interactable = true; face.navigation = new Navigation { mode = Navigation.Mode.None };
            var btn = face.gameObject.AddComponent<TouchButton>();
            btn.controlPath = controlPath;
            return face.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        }
    }
}
