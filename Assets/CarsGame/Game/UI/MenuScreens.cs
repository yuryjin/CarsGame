using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using RacingGame.Core.Economy;
using RacingGame.Core.Platform;
using RacingGame.Core.Track;

namespace RacingGame
{
    /// <summary>Localized runtime menus. Views derive progression and rewards from GameFlow and SaveService.</summary>
    [RequireComponent(typeof(GameFlow))]
    public sealed partial class MenuScreens : MonoBehaviour
    {
        private GameFlow flow;
        private RaceManager race;
        private Canvas canvas;
        private CanvasScaler menuScaler;
        private RectTransform portraitPanel;
        private TextMeshProUGUI portraitText;
        private RectTransform panel;
        private CanvasGroup transition;
        private string current = "", pendingScreen;
        public bool RaceSettingsOpen { get; private set; }
        private static TextMeshProUGUI toastText;
        private static RectTransform toastPanel;
        private static float toastUntil;
        private int tierIndex = -1;               // -1 = not chosen yet this session: derive from the save
        private TextMeshProUGUI rewardLabel;
        private float rewardShownAt;
        private const float RewardCountSeconds = 0.8f;
        private bool authInFlight;
        private float layoutWidth, layoutHeight;
        private Button rewardButton;
        private TextMeshProUGUI stickResponse;
        private int lastStickPercent = int.MinValue;
        private static readonly Vector2 Top = new Vector2(0f, 1f);
        private static readonly string[] LanguageCycle = { "", "ru", "en", "tr", "zh", "de", "es", "fr", "pt", "uk" };
        private static readonly string[] LanguageNames = { "", "Русский", "English", "Türkçe", "中文", "Deutsch", "Español", "Français", "Português", "Українська" };
        public string Current => current;
        private bool Compact => layoutHeight < 800f;
        private bool Narrow => layoutWidth < 1100f || Compact;

        private void Awake()
        {
            flow = GetComponent<GameFlow>(); race = GetComponent<RaceManager>();
            canvas = UiFactory.CreateOverlayCanvas("Menus", 30);
            canvas.transform.SetParent(transform, false);
            menuScaler = canvas.GetComponent<CanvasScaler>();
            menuScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            menuScaler.scaleFactor = UiFactory.ReadableScale(Screen.width, Screen.height);
            canvas.scaleFactor = menuScaler.scaleFactor;
            toastPanel = UiFactory.Card(canvas.transform, "Toast", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-400f, 24f), new Vector2(400f, 104f));
            toastText = Label(toastPanel, "Message", "", 26f, Vector2.zero, Vector2.one, new Vector2(24f, 12f), new Vector2(-24f, -12f), UiFactory.Accent, TextAlignmentOptions.Center);
            toastPanel.gameObject.SetActive(false);
            portraitPanel = UiFactory.Panel(canvas.transform, "PortraitHint", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.015f, 0.025f, 0.045f, 0.97f), false);
            portraitPanel.GetComponent<Image>().raycastTarget = true;
            portraitText = Label(portraitPanel, "RotatePhone", "", 42f, Vector2.zero, Vector2.one, new Vector2(36f, 36f), new Vector2(-36f, -36f), UiFactory.Accent, TextAlignmentOptions.Center);
            portraitPanel.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            flow.StateChanged += OnFlowState; Loc.LanguageChanged += Rebuild; Loc.LoadFailed += OnLanguageLoadFailed; PlatformGlue.Ready += Rebuild;
        }

        private void OnDisable()
        {
            HideSaveRecovery();
            flow.StateChanged -= OnFlowState; Loc.LanguageChanged -= Rebuild; Loc.LoadFailed -= OnLanguageLoadFailed; PlatformGlue.Ready -= Rebuild;
            PauseController.Set(PauseReason.Orientation, false);
        }

        private void Update()
        {
            UpdateSaveRecovery();
            if (current == "replay") UpdateReplayControls();
            menuScaler.scaleFactor = UiFactory.ReadableScale(Screen.width, Screen.height);
            if (toastPanel != null && toastPanel.gameObject.activeSelf && Time.unscaledTime > toastUntil) toastPanel.gameObject.SetActive(false);
            bool portrait = PlatformGlue.IsReady && PlatformGlue.IsMobileDevice && Screen.height > Screen.width;
            PauseController.Set(PauseReason.Orientation, portrait && flow.State == FlowState.Racing
                && race.State >= RaceState.Countdown && race.State < RaceState.Finished);
            if (portraitPanel.gameObject.activeSelf != portrait)
            {
                if (portrait) { portraitText.text = Loc.Get("ui.rotate_phone"); portraitPanel.SetAsLastSibling(); }
                portraitPanel.gameObject.SetActive(portrait);
            }
            if (transition != null) transition.alpha = Mathf.MoveTowards(transition.alpha, 1f, Time.unscaledDeltaTime * 7f);
            if (string.IsNullOrEmpty(current)) return;
            if (current == "settings" && stickResponse != null) RefreshStickResponse();
            if (current == "capture-key") { CaptureDrivingKey(); return; }
            if (RefreshDailyCard()) return;
            var rect = ((RectTransform)canvas.transform).rect;
            if (Mathf.Abs(rect.width - layoutWidth) > 1f || Mathf.Abs(rect.height - layoutHeight) > 1f) { Rebuild(); return; }
            if (flow.State != FlowState.Racing && current != "main" && (Keyboard.current?.escapeKey.wasPressedThisFrame == true || Gamepad.current?.buttonEast.wasPressedThisFrame == true))
            {
                Back();
            }
            if (rewardLabel != null)
            {
                float k = Mathf.Clamp01((Time.unscaledTime - rewardShownAt) / RewardCountSeconds);
                int target = flow.LastTotalReward;
                rewardLabel.text = Loc.Format("results.reward", "n", Mathf.RoundToInt(target * (1f - (1f - k) * (1f - k))));
                if (k >= 1f) rewardLabel = null;
            }
            if (rewardButton != null)
            {
                rewardButton.interactable = !flow.IsDoublingReward;
                rewardButton.GetComponentInChildren<TextMeshProUGUI>().text = flow.IsDoublingReward ? Loc.Get("common.loading") : Loc.Format("results.double_amount", "n", flow.LastReward);
            }
        }

        public static void Toast(string text)
        {
            if (toastText == null || toastPanel == null) return;
            toastText.text = text; toastPanel.SetAsLastSibling(); toastPanel.gameObject.SetActive(true); toastUntil = Time.unscaledTime + 3.5f;
        }

        public void GoTo(string screen) { pendingScreen = screen; flow.EnterMenu(); }
        internal void CoverTouchSettings(bool covered)
        {
            if (transition == null) return;
            transition.interactable = transition.blocksRaycasts = !covered;
            if (!covered && current == "settings")
            {
                var edit = Array.Find(panel.GetComponentsInChildren<Button>(), b => b.name == "EditTouchLayout");
                if (edit != null) EventSystem.current?.SetSelectedGameObject(edit.gameObject);
            }
        }
        public void OpenRaceSettings()
        {
            if (flow.State != FlowState.Racing || race.State < RaceState.Countdown || race.State >= RaceState.Finished) return;
            PauseController.Set(PauseReason.User, true);
            GetComponent<HudScreen>()?.CloseHelpIfOpen();
            RaceSettingsOpen = true;
            Show("settings");
        }

        public bool CloseRaceSettingsIfOpen()
        {
            if (current == "capture-key") { Show("keys"); return true; }
            if (current == "keys") { Show("settings"); return true; }
            if (GetComponent<TouchControlsPresenter>()?.CloseLayoutEditor() == true) return true;
            if (!RaceSettingsOpen) return false;
            RaceSettingsOpen = false;
            Show("");
            GetComponent<HudScreen>()?.FocusPauseControls();
            return true;
        }

        private void Back()
        {
            if (CloseRaceSettingsIfOpen()) return;
            if (current == "replay") { Show("leaders"); return; }
            if (current == "results") GoTo("main"); else Show(current == "arsenal" ? "garage" : current == "missionBriefing" ? "events" : "main");
        }
        private void OnFlowState(FlowState state)
        {
            RaceSettingsOpen = false;
            switch (state)
            {
                case FlowState.Menu: string target = pendingScreen ?? (flow.ReturningFromPractice ? "practice" : flow.ReturningFromTestDrive ? "garage" : "main"); pendingScreen = null; Show(target); break;
                case FlowState.Racing: rewardDetailsExpanded = false; Show(""); break;
                case FlowState.Results:
                    rewardShownAt = flow.ReturningToResults ? float.NegativeInfinity : Time.unscaledTime;
                    Show("results");
                    if (!flow.ReturningToResults)
                        foreach (var e in flow.LastUnlocked) Toast(Loc.Format("results.unlocked", "name", Loc.Get(e.nameKey)));
                    break;
            }
        }
        private void OnLanguageLoadFailed(string language)
        {
            if (Loc.Ready) Toast(Loc.Get("settings.language.failed"));
        }

        private void Rebuild() { Show(current); if (portraitText != null && Loc.Ready) portraitText.text = Loc.Get("ui.rotate_phone"); }

        public void Show(string name)
        {
            if (name == "replay" && !flow.ViewingReplay) name = "leaders";
            if (current == "replay" && name != "replay") flow.CloseReplay();
            var save = PlatformGlue.Save;
            if (save != null && !save.LocalVersionTooNew && !RaceSettingsOpen && RaceCups.GrantCompleted(save.Data.progress)) save.MarkDirty();
            if (save != null && !save.LocalVersionTooNew && !RaceSettingsOpen && PlayerProgression.NormalizeLoadout(save.Data.progress)) save.MarkDirty();
            if (current == "garage" && name != "garage" && !flow.IsTestDrive) previewBodyId = null;
            string selected = current == name ? EventSystem.current?.currentSelectedGameObject?.name : null;
            var scrollPositions = new Dictionary<string, float>();
            if (current == name && panel != null)
            {
                foreach (var oldScroll in panel.GetComponentsInChildren<ScrollRect>())
                    scrollPositions[oldScroll.name] = oldScroll.verticalNormalizedPosition;
            }
            if (panel != null) { panel.gameObject.SetActive(false); Destroy(panel.gameObject); }
            rewardButton = null; rewardLabel = null; current = name;
            stickResponse = null; lastStickPercent = int.MinValue;
            if (string.IsNullOrEmpty(name)) { EventSystem.current?.SetSelectedGameObject(null); return; }
            if (!PlatformGlue.IsReady) return;
            Canvas.ForceUpdateCanvases();
            var canvasRect = ((RectTransform)canvas.transform).rect;
            layoutWidth = canvasRect.width; layoutHeight = canvasRect.height;
            panel = UiFactory.Panel(canvas.transform, "Panel_" + name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.015f, 0.025f, 0.045f, 0.82f), false);
            panel.GetComponent<Image>().raycastTarget = true;
            transition = panel.gameObject.AddComponent<CanvasGroup>(); transition.alpha = 0f;
            if (name == "replay")
            {
                panel.GetComponent<Image>().color = Color.clear;
                BuildReplayControls();
                if (portraitPanel != null && portraitPanel.gameObject.activeSelf) portraitPanel.SetAsLastSibling();
                return;
            }
            float margin = Compact ? 12f : 32f;
            float width = Mathf.Min(name == "settings" ? 1320f : 1640f, layoutWidth - margin * 2f);
            var shell = UiFactory.Card(panel, "Shell", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-width * 0.5f, margin), new Vector2(width * 0.5f, -margin));
            shell.GetComponent<Image>().color = new Color(0.035f, 0.05f, 0.08f, 0.96f);
            var frame = UiFactory.Panel(shell, "Frame", Vector2.zero, Vector2.one, new Vector2(Compact ? 16f : 32f, Compact ? 8f : 16f), new Vector2(Compact ? -16f : -32f, Compact ? -8f : -24f));
            if (save != null && save.LocalVersionTooNew)
            {
                var body = Header(frame, "save.newer.title", false, false);
                Label(body, "SaveVersionMessage", Loc.Get("save.newer.message"), 32f, Vector2.zero, Vector2.one,
                    new Vector2(32f, 24f), new Vector2(-32f, -24f), UiFactory.Muted, TextAlignmentOptions.Center);
            }
            else switch (name)
            {
                case "main": BuildMain(frame); break;
                case "events": BuildEvents(frame); break;
                case "missionBriefing": BuildMissionBriefing(frame); break;
                case "garage": BuildGarage(frame); break;
                case "arsenal": BuildArsenal(frame); break;
                case "settings": BuildSettings(frame); break;
                case "keys": BuildKeySettings(frame); break;
                case "capture-key": BuildKeyCapture(frame); break;
                case "practice": BuildPractice(frame); break;
                case "leaders": BuildLeaders(frame); break;
                case "driver": BuildDriverProfile(frame); break;
                case "results": BuildResults(frame); break;
            }
            if (UiFactory.TextScale > 1f)
            {
                foreach (var text in frame.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (text.enableAutoSizing) text.fontSizeMax *= UiFactory.TextScale;
                    else text.fontSize *= UiFactory.TextScale;
                }
            }
            if (toastPanel != null) toastPanel.SetAsLastSibling();
            if (portraitPanel != null && portraitPanel.gameObject.activeSelf) portraitPanel.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            if (panel == null) return;
            foreach (var scroll in panel.GetComponentsInChildren<ScrollRect>())
            {
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = 1f;
            }
            var buttons = panel.GetComponentsInChildren<Button>();
            Button focus = Array.Find(buttons, b => b.interactable && b.name == selected)
                ?? (name == "results" ? Array.Find(buttons, b => b.interactable && b.name == "ResultAction0") : null)
                ?? Array.Find(buttons, b => b.interactable && b.name != "Back" && b.name != "ModelStyle");
            if (focus != null) EventSystem.current?.SetSelectedGameObject(focus.gameObject);
            // OnSelect reveals keyboard focus. During a refresh the player may
            // have scrolled away from that control, so restore their view last.
            foreach (var scroll in panel.GetComponentsInChildren<ScrollRect>())
                if (scrollPositions.TryGetValue(scroll.name, out float position))
                    scroll.verticalNormalizedPosition = position;
        }

        private string Coins() => Loc.Format("menu.coins", "n", PlatformGlue.Save != null ? PlatformGlue.Save.Data.Coins : 0);
        private static TextMeshProUGUI Label(Transform parent, string name, string value, float size, Vector2 min, Vector2 max, Vector2 lo, Vector2 hi, Color? color = null, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var text = UiFactory.Text(parent, name, value, size, alignment, min, max, lo, hi, color);
            UiFactory.SetOutline(text, 0f); text.enableAutoSizing = true; text.fontSizeMin = Mathf.Max(18f, size * 0.7f); text.fontSizeMax = size;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private RectTransform Header(RectTransform root, string key, bool back = true, bool showWallet = true, bool modelSwitch = false)
        {
            float right = 0f;
            if (modelSwitch && PlatformGlue.IsReady)
            {
                right = Compact ? 196f : 248f;
                UiFactory.Button(root, "ModelStyle", Loc.Get(PlatformGlue.ClassicCarModels ? "models.classic" : "models.new"),
                    () => { PlatformGlue.ToggleCarModels(); Show(current); }, Vector2.one, Vector2.one,
                    new Vector2(12f - right, Compact ? -68f : -74f), new Vector2(0f, Compact ? 0f : -6f), 25f, UiFactory.Raised);
            }
            if (Compact)
            {
                if (back) UiFactory.Button(root, "Back", "‹  " + Loc.Get("common.back"), Back, Top, Top, new Vector2(0f, -68f), new Vector2(150f, 0f), 26f, UiFactory.Raised);
                Label(root, "Title", Loc.Get(key), 40f, Top, Vector2.one, new Vector2(back ? 170f : 12f, -68f), new Vector2((showWallet ? -218f : -12f) - right, 0f)).fontStyle = FontStyles.Bold;
                if (showWallet) Label(root, "Coins", Coins(), 24f, Vector2.one, Vector2.one, new Vector2(-206f - right, -68f), new Vector2(-right, 0f), UiFactory.Accent, TextAlignmentOptions.MidlineRight);
                return UiFactory.Panel(root, "Body", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -86f));
            }
            if (back) UiFactory.Button(root, "Back", "‹  " + Loc.Get("common.back"), Back, Top, Top, new Vector2(0f, -74f), new Vector2(164f, -6f), 26f, UiFactory.Raised);
            else
                for (int i = 0; i < 3; i++)
                {
                    var stripe = UiFactory.Panel(root, "Mark" + i, Top, Top, new Vector2(6f + i * 22f, -66f), new Vector2(18f + i * 22f, -12f), UiFactory.Accent, false);
                    stripe.localRotation = Quaternion.Euler(0f, 0f, -15f);
                }
            var title = Label(root, "Title", Loc.Get(key), back ? 46f : 60f, Top, Vector2.one, new Vector2(back ? 190f : 94f, -82f), new Vector2((showWallet ? -300f : 0f) - right, 0f));
            title.fontStyle = FontStyles.Bold;
            if (showWallet)
            {
                var wallet = UiFactory.Card(root, "Wallet", Vector2.one, Vector2.one, new Vector2(-276f - right, -74f), new Vector2(-right, -6f));
                Label(wallet, "Coins", Coins(), 26f, Vector2.zero, Vector2.one, new Vector2(22f, 8f), new Vector2(-22f, -8f), UiFactory.Accent, TextAlignmentOptions.Center);
            }
            UiFactory.Panel(root, "Rule", Top, Vector2.one, new Vector2(0f, -111f), new Vector2(0f, -110f), new Color(0.4f, 0.5f, 0.65f, 0.3f), false);
            UiFactory.Panel(root, "Accent", Top, Top, new Vector2(0f, -113f), new Vector2(90f, -109f), UiFactory.Accent, false);
            var player = PlatformGlue.Platform?.Player;
            Label(root, "Player", player != null ? UiFactory.Escape(player.Value.Name) : "", 20f, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 30f), UiFactory.Muted, TextAlignmentOptions.BottomRight);
            return UiFactory.Panel(root, "Body", Vector2.zero, Vector2.one, new Vector2(0f, 38f), new Vector2(0f, -138f));
        }

        private static RectTransform Item(Transform content, string name, float height, bool card = true)
        {
            var row = card ? UiFactory.Card(content, name, Top, Vector2.one, Vector2.zero, Vector2.zero)
                : UiFactory.Panel(content, name, Top, Vector2.one, Vector2.zero, Vector2.zero);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return row;
        }

        private List<RaceEventDefinition> Events()
        {
            var list = new List<RaceEventDefinition>(flow.events ?? Array.Empty<RaceEventDefinition>());
            list.RemoveAll(e => e == null); list.Sort((a, b) => a.order.CompareTo(b.order)); return list;
        }

        private void BuildMain(RectTransform root)
        {
            var body = Header(root, "menu.title", false, modelSwitch: true);
            if (!PlatformGlue.IsReady)
            {
                Label(body, "Loading", Loc.Ready ? Loc.Get("common.loading") : "…", 36f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiFactory.Muted, TextAlignmentOptions.Center); return;
            }
            var content = UiFactory.Scroll(body, "MainScroll", Vector2.zero, Vector2.zero);
            var row = Item(content, "Home", Narrow ? 194f : 510f, false);
            var hero = UiFactory.Card(row, "RaceCard", Vector2.zero, new Vector2(Narrow ? 1f : 0.58f, 1f), Vector2.zero, new Vector2(Narrow ? 0f : -12f, 0f));
            var list = Events();
            var ev = FeaturedEvent(list);
            float inset = Narrow ? 26f : 36f;
            float textRight = Narrow ? -306f : -inset;
            Label(hero, "Eyebrow", Loc.Get("menu.next_race"), 26f, Top, Vector2.one,
                new Vector2(inset, Narrow ? -50f : -78f), new Vector2(textRight, Narrow ? -12f : -24f), UiFactory.Accent);
            var title = Label(hero, "EventName", ev != null ? Loc.Get(ev.nameKey) : Loc.Get("menu.play"), Narrow ? 46f : 64f, Top, Vector2.one,
                new Vector2(inset, Narrow ? -123f : -206f), new Vector2(textRight, Narrow ? -50f : -80f));
            title.fontStyle = FontStyles.Bold;
            var tiers = Tiers();
            int rivals = ev != null && ev.aiCountOverride >= 0 ? ev.aiCountOverride : tiers.Length > 0 ? tiers[ResolveTierIndex(tiers)].aiCount : 3;
            string detail = ev == null ? "" : ev.mode == EventMode.Mission ? MissionDetail(ev, rivals)
                : ev.mode == EventMode.Arena ? Loc.Format("arena.detail", "n", rivals)
                : Loc.Format("events.laps", "n", ev.laps) + "   ·   " + Loc.Format("events.rivals", "n", rivals);
            int maxStars = list.Count * 3;
            int earnedStars = PlatformGlue.Save != null ? PlatformGlue.Save.Data.TotalStars : 0;
            string stars = "<color=#FFCC40>" + Loc.Format("events.stars", "s", earnedStars, "max", maxStars) + "</color>";
            if (Narrow) detail += "   ·   " + stars;
            Label(hero, "Details", detail, 26f, Top, Vector2.one,
                new Vector2(inset, Narrow ? -184f : -264f), new Vector2(textRight, Narrow ? -123f : -210f), UiFactory.Muted);
            if (!Narrow)
            {
                string progress = Loc.Format("menu.unlocked_races", "n", list.FindAll(e => flow.IsUnlocked(e)).Count, "max", list.Count);
                Label(hero, "Progress", progress + "     " + stars, 26f, Top, Vector2.one,
                    new Vector2(inset, -352f), new Vector2(-inset, -274f), UiFactory.Muted);
            }
            var rightCenter = new Vector2(1f, 0.5f);
            var actions = UiFactory.Panel(hero, "RaceActions", Narrow ? rightCenter : Vector2.zero,
                Narrow ? rightCenter : new Vector2(1f, 0f), Narrow ? new Vector2(-286f, -86f) : new Vector2(inset, 32f),
                Narrow ? new Vector2(-inset, 86f) : new Vector2(-inset, 124f));
            UiFactory.Button(actions, "Play", Loc.Get("menu.play"), ev != null ? () => StartMenuEvent(ev, SelectedTier()) : (Action)null,
                Narrow ? new Vector2(0f, .5f) : Vector2.zero, Narrow ? Vector2.one : new Vector2(.5f, 1f),
                Narrow ? new Vector2(0f, 5f) : Vector2.zero, Narrow ? Vector2.zero : new Vector2(-7f, 0f), 32f);
            UiFactory.Button(actions, "ChooseRace", Loc.Get("events.title"), () => Show("events"),
                Narrow ? Vector2.zero : new Vector2(.5f, 0f), Narrow ? new Vector2(1f, .5f) : Vector2.one,
                Narrow ? Vector2.zero : new Vector2(7f, 0f), Narrow ? new Vector2(0f, -5f) : Vector2.zero, 28f, UiFactory.Raised);

            var links = Narrow ? Item(content, "Links", 270f, false)
                : UiFactory.Panel(row, "Links", new Vector2(0.58f, 0f), Vector2.one, new Vector2(12f, 0f), Vector2.zero);
            var destinations = new[] { ("garage", "menu.garage"), ("practice", "practice.title"), ("leaders", "menu.leaderboard"), ("settings", "menu.settings"), ("driver", "driver.title") };
            int columns = Narrow ? 2 : 1;
            int rows = (destinations.Length + columns - 1) / columns;
            for (int i = 0; i < destinations.Length; i++)
            {
                var (target, key) = destinations[i];
                int col = i % columns, line = i / columns;
                var min = new Vector2(col / (float)columns, 1f - (line + 1f) / rows);
                var max = new Vector2((col + 1f) / columns, 1f - line / (float)rows);
                var button = UiFactory.Button(links, target == "practice" ? "Practice" : target, Loc.Get(key), () => Show(target), min, max,
                    new Vector2(col == 0 ? 0f : 8f, line == rows - 1 ? 0f : 8f),
                    new Vector2(col == columns - 1 ? 0f : -8f, line == 0 ? 0f : -8f), 32f, UiFactory.Raised);
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.rectTransform.offsetMin = new Vector2(26f, 8f);
                label.rectTransform.offsetMax = new Vector2(-62f, -8f);
                Label(button.transform, "Arrow", "›", 34f, new Vector2(1f, 0f), Vector2.one,
                    new Vector2(-50f, 0f), new Vector2(-20f, 0f), UiFactory.Muted, TextAlignmentOptions.Center);
            }
            BuildDailyCard(content);
            BuildWeeklyGoals(content);
        }

        private RaceEventDefinition FeaturedEvent(List<RaceEventDefinition> list)
        {
            if (list.Count == 0) return null;
            var ev = list[0];
            for (int i = 1; i < list.Count; i++)
            {
                var best = PlatformGlue.Save?.Data.progress.bests.Find(b => b.eventId == ev.eventId);
                if (best == null || best.bestTimeMs <= 0 || !flow.IsUnlocked(list[i])) break;
                ev = list[i];
            }
            return ev;
        }

        private DifficultyTier[] Tiers() => Array.FindAll(flow.tiers ?? Array.Empty<DifficultyTier>(), t => t != null);

        /// <summary>
        /// The remembered difficulty, else Easy for a first race or a phone (touch driving is harder),
        /// else Normal. The choice is stored in settings.tier when the player picks a tab.
        /// </summary>
        private int ResolveTierIndex(DifficultyTier[] tiers)
        {
            if (tiers.Length == 0) return 0;
            if (tierIndex >= 0) return Mathf.Clamp(tierIndex, 0, tiers.Length - 1);
            var save = PlatformGlue.Save;
            string remembered = save != null ? save.Data.settings.tier : "";
            if (!string.IsNullOrEmpty(remembered))
            {
                int found = Array.FindIndex(tiers, t => t.tierId == remembered);
                if (found >= 0) return tierIndex = found;
            }
            bool newcomer = save == null || save.Data.Races == 0 || PlatformGlue.IsMobileDevice;
            return tierIndex = newcomer ? 0 : Mathf.Min(1, tiers.Length - 1);
        }

        private void SelectTier(DifficultyTier[] tiers, int index)
        {
            tierIndex = index;
            var save = PlatformGlue.Save;
            if (save != null && save.Data.settings.tier != tiers[index].tierId) { save.Data.settings.tier = tiers[index].tierId; save.MarkDirty(); }
            Show("events");
        }

        private static string MissionDetail(RaceEventDefinition ev, int allies) => ev.eventId == TrainMission.EventId
            ? Loc.Get("train.detail") : SettlementMission.Supports(ev.eventId) ? Loc.Get(ev.nameKey + ".goal") : Loc.Format("mission.detail", "n", allies);

        private static string EventFireDetail(RaceEventDefinition ev, float tierInterval, CultureInfo culture)
        {
            if (ev.twist == Core.Daily.RaceTwist.MinesOnly || ev.twist == Core.Daily.RaceTwist.HotBomb
                || ev.twist == Core.Daily.RaceTwist.TimeTrial)
                return Loc.Get("events.guns_disabled");
            if (ev.mode == EventMode.Mission && ev.eventId == TrainMission.EventId)
                return Loc.Format("train.guard_fire", "s", TrainEscort.BaseFireCooldown.ToString("0.##", culture));
            if (SettlementMission.Supports(ev.eventId)) return Loc.Get("mission.raider_fire");
            string key = ev.mode == EventMode.Mission ? "mission.ally_fire" : "events.rival_fire";
            return Loc.Format(key, "s", tierInterval.ToString("0.##", culture));
        }

        private void BuildEvents(RectTransform root)
        {
            var body = Header(root, "events.title");
            var tiers = Tiers();
            if (tiers.Length > 0)
            {
                tierIndex = ResolveTierIndex(tiers);
                for (int i = 0; i < tiers.Length; i++)
                {
                    int index = i;
                    UiFactory.Button(body, "Tier" + i, Loc.Get("difficulty." + tiers[i].tierId), () => SelectTier(tiers, index), new Vector2(i / (float)tiers.Length, 1f), new Vector2((i + 1f) / tiers.Length, 1f), new Vector2(4f, -68f), new Vector2(-8f, 0f), 27f, i == tierIndex ? UiFactory.Accent : UiFactory.Raised);
                }
            }
            var tier = tiers.Length > 0 ? tiers[tierIndex] : RaceSetup.Tier;
            var culture = CultureInfo.GetCultureInfo(Loc.Language);
            float fireInterval = tier != null ? tier.aiFireCooldown : race.spawner.aiFireCooldown;
            float multiplier = tier != null ? tier.rewardMultiplier : 1f;
            string reward = Loc.Format("events.race_reward", "m", multiplier.ToString("0.##", culture));
            var content = UiFactory.Scroll(body, "EventsScroll", Vector2.zero, new Vector2(0f, tiers.Length > 0 ? -90f : 0f));
            var list = Events();
            var path = TrackPath.Instance;
            var forward = path != null ? path.Spline : null;
            var reverse = forward?.Reversed();
            // Surface the modes a new player can actually enter before the locked career chain.
            // Keep the original indices for card numbers and campaign progression.
            foreach (bool available in new[] { true, false })
            {
                if (!available) BuildCups(content);
                if (!list.Exists(e => flow.IsUnlocked(e) == available)) continue;
                Section(content, available ? "AvailableEventsTitle" : "LockedEventsTitle",
                    Loc.Get(available ? "events.available" : "events.career_locked"));
                for (int i = 0; i < list.Count; i++)
                {
                    var ev = list[i]; bool unlocked = flow.IsUnlocked(ev);
                    if (unlocked != available) continue;
                    bool circuit = ev.mode == EventMode.Race && forward != null;
                    var row = Item(content, ev.eventId, (Narrow ? 376f : 354f) + (circuit ? 92f : 0f));
                    Label(row, "Number", (i + 1).ToString("00"), circuit ? 36f : 52f, circuit ? Top : Vector2.zero, Top,
                        circuit ? new Vector2(24f, -62f) : new Vector2(24f, 24f), new Vector2(116f, circuit ? -12f : -24f),
                        unlocked ? UiFactory.Accent : UiFactory.Muted, TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
                    Label(row, "Name", Loc.Get(ev.nameKey), 34f, Top, Vector2.one, new Vector2(144f, -60f), new Vector2(-236f, -12f)).fontStyle = FontStyles.Bold;
                    int rivals = ev.aiCountOverride >= 0 ? ev.aiCountOverride : tier != null ? tier.aiCount : race.spawner.aiCount;
                    string detail = ev.mode == EventMode.Mission ? MissionDetail(ev, rivals)
                        : ev.mode == EventMode.Arena ? Loc.Format("arena.detail", "n", rivals)
                        : Loc.Format("events.laps", "n", ev.laps) + "   /   " + Loc.Format("events.rivals", "n", rivals) + TwistSuffix(ev);
                    Label(row, "Stats", detail, 23f, Top, Vector2.one, new Vector2(144f, -114f), new Vector2(-236f, -60f), UiFactory.Muted);
                    Label(row, "Difficulty", EventFireDetail(ev, fireInterval, culture) + "\n" + reward, 23f, Top, Vector2.one,
                        new Vector2(144f, -178f), new Vector2(-236f, -114f));
                    var best = PlatformGlue.Save?.Data.progress.bests.Find(b => b.eventId == ev.eventId);
                    int starMask = best != null ? best.stars : 0;
                    string s1 = (starMask & 1) != 0 ? "<color=#FFCC40>★</color>" : "<color=#455568>★</color>";
                    string s2 = (starMask & 2) != 0 ? "<color=#FFCC40>★</color>" : "<color=#455568>★</color>";
                    string s3 = (starMask & 4) != 0 ? "<color=#FFCC40>★</color>" : "<color=#455568>★</color>";
                    string starDisplay = s1 + " " + StarGoalText.Description(ev, 0) + "\n" + s2 + " " + StarGoalText.Description(ev, 1) + "\n" + s3 + " " + StarGoalText.Description(ev, 2);
                    float starOffset = circuit ? -92f : 0f;
                    Label(row, "Stars", starDisplay, 22f, Top, Vector2.one, new Vector2(144f, -302f + starOffset), new Vector2(-236f, -178f + starOffset));
                    if (circuit) BuildCircuitPreview(row, ev, ev.reversed ? reverse : forward, path.startLineDistance);
                    var prerequisite = unlocked ? null : list.Find(e => e.eventId == ev.unlockAfterEventId);
                    string status = unlocked ? EventBestText(ev, best)
                        : prerequisite == null ? Loc.Get("events.locked")
                        : ev.unlockStars > 0 ? Loc.Format("events.locked_stars", "name", Loc.Get(prerequisite.nameKey),
                            "n", Mathf.Max(0, ev.unlockStars - (PlatformGlue.Save?.Data.TotalStars ?? 0)))
                        : Loc.Format("events.locked_after", "name", Loc.Get(prerequisite.nameKey));
                    Label(row, "Status", status, 22f, Vector2.zero, new Vector2(1f, 0f), new Vector2(144f, 12f), new Vector2(-236f, Narrow ? 64f : 44f), unlocked ? UiFactory.Positive : UiFactory.Muted);
                    UiFactory.Button(row, "Start_" + ev.eventId, unlocked ? Loc.Get("menu.play") + " ›" : "—", unlocked ? () => StartMenuEvent(ev, tier) : null, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-216f, -36f), new Vector2(-24f, 36f), 26f, unlocked ? UiFactory.Accent : UiFactory.Raised);
                }
            }
        }

        private static void BuildCircuitPreview(RectTransform row, RaceEventDefinition ev, TrackSpline spline, float grid)
        {
            if (ev.wastelandTrack)
            {
                var layout = new WastelandLayout();
                spline = new TrackSpline(layout.Points); grid = layout.StartLine;
            }
            else if (ev.starRingTrack)
            {
                var layout = new StarRingLayout();
                spline = new TrackSpline(layout.Points); grid = layout.StartLine;
            }
            if ((ev.wastelandTrack || ev.starRingTrack) && ev.reversed) spline = spline.Reversed();
            var map = UiFactory.Panel(row, "RouteMap", Top, Top, new Vector2(12f, -208f), new Vector2(134f, -65f));
            map.gameObject.AddComponent<CircuitMapGraphic>().SetRoute(spline, grid);
            string hint = ev.twist == Core.Daily.RaceTwist.HotBomb ? "bomb.pass"
                : ev.twist == Core.Daily.RaceTwist.MinesOnly ? "lesson.mines"
                : ev.wastelandTrack ? "route.bridge_jump" : ev.starRingTrack ? "lesson.wall"
                : ev.spaceTrack ? "lesson.space" : ev.abyssTrack ? "lesson.abyss"
                : ev.skillGoal == SkillGoal.DriftBoosts ? "route.drift_boost"
                : ev.eventId == "e1" ? "route.aim" : "lesson.shortcut";
            Label(row, "RouteHint", Loc.Get(hint), 23f, Top, Vector2.one,
                new Vector2(144f, -268f), new Vector2(-236f, -178f));
            if (ev.referenceSeconds <= 0) return;
            int duration = Mathf.RoundToInt(ev.referenceSeconds / 5f) * 5;
            string time = $"~{duration / 60}:{duration % 60:00}";
            Label(row, "ReferenceTime", time + "\n" + Loc.Get("body.classic") + " / " + Loc.Get("difficulty.normal"), 23f,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-225f, -134f), new Vector2(-19f, -43f),
                UiFactory.Muted, TextAlignmentOptions.Center);
        }

        private void BuildGarage(RectTransform root)
        {
            var body = Header(root, "garage.title", modelSwitch: true);
            RectTransform content;
            if (Compact)
            {
                content = UiFactory.Scroll(body, "GarageScroll", Vector2.zero, Vector2.zero);
                BuildShowcase(Item(content, "Showcase", 420f));
            }
            else
            {
            var showcase = UiFactory.Card(body, "Showcase", Narrow ? new Vector2(0f, 0.45f) : Vector2.zero,
                Narrow ? Vector2.one : new Vector2(0.44f, 1f), Vector2.zero, new Vector2(Narrow ? 0f : -16f, 0f));
            var catalog = UiFactory.Panel(body, "Catalog", Narrow ? Vector2.zero : new Vector2(0.44f, 0f),
                Narrow ? new Vector2(1f, 0.45f) : Vector2.one, new Vector2(Narrow ? 0f : 8f, 0f), new Vector2(0f, Narrow ? -16f : 0f));
            BuildShowcase(showcase);
            content = UiFactory.Scroll(catalog, "GarageScroll", Vector2.zero, Vector2.zero);
            }
            var save = PlatformGlue.Save;

            BuildBodySavings(content);
            BuildLoadout(content);
            BuildGarageSets(content);
            BuildWeaponDrills(content);
            BuildBodies(content);
            Section(content, "BuildsTitle", Loc.Get("garage.builds"));
            foreach (var build in new[] { CarBuild.Balanced, CarBuild.Speed, CarBuild.Survival, CarBuild.Firepower })
            {
                string key = "build." + build.ToString().ToLowerInvariant();
                bool selected = save != null && save.Data.progress.buildPreset == (int)build;
                var row = Item(content, "Build_" + build, Narrow ? 190f : 150f);
                Label(row, "Name", Loc.Get(key), 30f, Top, Vector2.one, new Vector2(30f, -55f), new Vector2(-270f, -12f)).fontStyle = FontStyles.Bold;
                Label(row, "Tradeoff", Loc.Get(key + ".desc"), 23f, Vector2.zero, Vector2.one, new Vector2(30f, 12f), new Vector2(-270f, -60f), UiFactory.Muted);
                UiFactory.Button(row, "Select_" + build, Loc.Get(selected ? "garage.equipped" : "garage.equip"),
                    selected || save == null ? null : () => SelectBuild(build), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-245f, -34f), new Vector2(-24f, 34f), 25f, selected ? UiFactory.Positive : UiFactory.Accent);
            }
            var loadout = Item(content, "Loadout", 92f, false);
            string primary = Loc.Get(PrimaryWeaponCatalog.Get((PrimaryWeaponType)(save?.Data.progress.equippedPrimary ?? 0)).NameKey);
            int secondary = save != null ? save.Data.progress.equippedSecondary : 0;
            string secondaryName = Loc.Get(secondary == 1 ? "garage.mines" : secondary == 2 ? "garage.freezelauncher" : secondary == 3 ? "garage.remotecharge" : "build.none");
            Label(loadout, "Current", Loc.Format("build.loadout", "primary", primary, "secondary", secondaryName) + "\n" + Loc.Get("build.owned_only"),
                23f, Vector2.zero, Vector2.one, new Vector2(20f, 6f), new Vector2(-20f, -6f), UiFactory.Muted);

            Section(content, "UpgradesTitle", Loc.Get("garage.upgrades"));
            foreach (var kind in new[] { UpgradeKind.Engine, UpgradeKind.Armor, UpgradeKind.Cannon })
            {
                string key = kind.ToString().ToLowerInvariant();
                int level = UpgradeCatalog.Level(save?.Data.progress.upgrades, (int)kind), cost = UpgradeCatalog.Cost(level);
                bool maxed = level >= UpgradeCatalog.MaxLevel;
                bool affordable = save != null && save.Data.Coins >= cost;
                var row = Item(content, key, Narrow ? 260f : 214f);
                Label(row, "Name", Loc.Get("garage." + key), 36f, Top, Vector2.one, new Vector2(30f, -66f), new Vector2(-330f, -20f)).fontStyle = FontStyles.Bold;
                Label(row, "Description", Loc.Get("garage." + key + ".desc"), 24f, Top, Vector2.one, new Vector2(30f, -118f), new Vector2(-330f, -70f), UiFactory.Muted);
                Label(row, "Level", Loc.Format("garage.level", "lvl", level, "max", UpgradeCatalog.MaxLevel), 23f, Top, Vector2.one, new Vector2(30f, -162f), new Vector2(-330f, -120f), UiFactory.Accent);
                var progress = UiFactory.Panel(row, "Progress", Vector2.zero, new Vector2(1f, 0f), new Vector2(30f, 30f), new Vector2(-330f, 44f));
                for (int i = 0; i < UpgradeCatalog.MaxLevel; i++) UiFactory.Panel(progress, "Level" + i, new Vector2(i / (float)UpgradeCatalog.MaxLevel, 0f), new Vector2((i + 1f) / UpgradeCatalog.MaxLevel, 1f), Vector2.zero, new Vector2(-6f, 0f), i < level ? UiFactory.Accent : UiFactory.Raised, false);
                UiFactory.Button(row, "Buy_" + key, maxed ? Loc.Get("garage.max") : Loc.Format("garage.buy", "cost", cost), maxed || !affordable ? null : () => Buy(kind), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-296f, -25f), new Vector2(-30f, 55f), 28f, maxed ? UiFactory.Raised : affordable ? UiFactory.Accent : UiFactory.Raised);
                if (!maxed && !affordable) Label(row, "Reason", Loc.Get("garage.not_enough"), 22f, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-296f, -82f), new Vector2(-30f, -32f), UiFactory.Muted, TextAlignmentOptions.Center);
                TrialRow(content, kind);
            }

            Section(content, "WeaponsTitle", Loc.Get("garage.weapons"));
            foreach (var kind in new[] { WeaponKind.Mines, WeaponKind.RemoteCharge, WeaponKind.FreezeLauncher, WeaponKind.MachineGun })
            {
                string key = kind.ToString().ToLowerInvariant();
                int level = WeaponCatalog.Level(save?.Data.progress.weapons, (int)kind), cost = WeaponCatalog.Cost(level);
                bool maxed = level >= WeaponCatalog.MaxLevel;
                bool affordable = save != null && save.Data.Coins >= cost;
                bool unlocked = level > 0;

                bool isEquipped = false;
                if (kind == WeaponKind.MachineGun)
                {
                    isEquipped = save != null && save.Data.progress.equippedPrimary == 1;
                }
                else if (kind == WeaponKind.Mines)
                {
                    isEquipped = save != null && save.Data.progress.equippedSecondary == (int)SecondaryWeaponType.Mines;
                }
                else if (kind == WeaponKind.FreezeLauncher)
                {
                    isEquipped = save != null && save.Data.progress.equippedSecondary == (int)SecondaryWeaponType.FreezeLauncher;
                }
                else if (kind == WeaponKind.RemoteCharge)
                {
                    isEquipped = save != null && save.Data.progress.equippedSecondary == (int)SecondaryWeaponType.RemoteCharge;
                }

                var row = Item(content, "Weapon_" + key, Narrow ? 320f : 244f);
                Label(row, "Name", Loc.Get("garage." + key), 36f, Top, Vector2.one, new Vector2(30f, -66f), new Vector2(-420f, -20f)).fontStyle = FontStyles.Bold;
                Label(row, "Description", Loc.Get("garage." + key + ".desc"), 24f, Top, Vector2.one, new Vector2(30f, -118f), new Vector2(-420f, -70f), UiFactory.Muted);
                string levelStr = level == 0 ? Loc.Get("garage.locked") : Loc.Format("garage.level", "lvl", level, "max", WeaponCatalog.MaxLevel);
                Label(row, "Level", levelStr, 23f, Top, Vector2.one, new Vector2(30f, -162f), new Vector2(-420f, -120f), unlocked ? UiFactory.Accent : UiFactory.Muted);
                var progress = UiFactory.Panel(row, "Progress", Vector2.zero, new Vector2(1f, 0f), new Vector2(30f, 30f), new Vector2(-420f, 44f));
                for (int i = 0; i < WeaponCatalog.MaxLevel; i++) UiFactory.Panel(progress, "Level" + i, new Vector2(i / (float)WeaponCatalog.MaxLevel, 0f), new Vector2((i + 1f) / WeaponCatalog.MaxLevel, 1f), Vector2.zero, new Vector2(-6f, 0f), i < level ? UiFactory.Accent : UiFactory.Raised, false);

                // Buy button
                UiFactory.Button(row, "Buy_" + key, maxed ? Loc.Get("garage.max") : Loc.Format("garage.buy", "cost", cost), maxed || !affordable ? null : () => BuyWeapon(kind), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-400f, -25f), new Vector2(-220f, 55f), 26f, maxed ? UiFactory.Raised : affordable ? UiFactory.Accent : UiFactory.Raised);
                if (!maxed && !affordable) Label(row, "Reason", Loc.Get("garage.not_enough"), 20f, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-400f, -82f), new Vector2(-220f, -32f), UiFactory.Muted, TextAlignmentOptions.Center);

                // Equip button
                string equipLabel = isEquipped ? Loc.Get(kind == WeaponKind.MachineGun ? "build.use_cannon" : "garage.equipped") : Loc.Get("garage.equip");
                Action equipAction = unlocked && (!isEquipped || kind == WeaponKind.MachineGun) ? () => EquipWeapon(kind) : null;
                UiFactory.Button(row, "Equip_" + key, equipLabel, equipAction, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-200f, -25f), new Vector2(-20f, 55f), 26f, isEquipped ? UiFactory.Positive : unlocked ? UiFactory.Accent : UiFactory.Raised);
            }
            BuildCosmetics(content);
        }

        public bool SelectBuild(CarBuild build)
        {
            var save = PlatformGlue.Save;
            if (save == null || (int)build < 0 || (int)build > 3) return false;
            PlayerProgression.SelectBuild(save.Data.progress, build);
            save.MarkDirty();
            SfxPlayer.Instance.PlayUi(SfxSynth.Beep);
            Show("garage");
            return true;
        }

        public bool Buy(UpgradeKind kind)
        {
            var save = PlatformGlue.Save;
            if (save == null) return false;
            if (save.Data.progress.upgrades == null || save.Data.progress.upgrades.Length < 3) save.Data.progress.upgrades = new int[3];
            if (UpgradeCatalog.TryBuy(save.Data.progress.upgrades, kind, save.Data.Coins, out int cost))
            {
                save.Local.coinsSpent += cost; save.MarkDirty(); SfxPlayer.Instance.PlayUi(SfxSynth.LapDing); Show("garage"); return true;
            }
            Toast(Loc.Get("garage.not_enough")); return false;
        }

        public bool BuyWeapon(WeaponKind kind)
        {
            var save = PlatformGlue.Save;
            if (save == null) return false;
            var owned = save.Data.progress.weapons;
            if (owned == null || owned.Length < 4)
            {
                // Keep the levels an old 3-slot save already has.
                save.Data.progress.weapons = new int[4];
                if (owned != null) Array.Copy(owned, save.Data.progress.weapons, owned.Length);
            }
            int prevLevel = WeaponCatalog.Level(save.Data.progress.weapons, (int)kind);
            if (WeaponCatalog.TryBuy(save.Data.progress.weapons, kind, save.Data.Coins, out int cost))
            {
                save.Local.coinsSpent += cost;
                // Auto-equip on first purchase if slot is empty/default
                if (prevLevel == 0)
                {
                    if (kind == WeaponKind.MachineGun) save.Data.progress.equippedPrimary = 1;
                    else if (kind == WeaponKind.Mines) save.Data.progress.equippedSecondary = (int)SecondaryWeaponType.Mines;
                    else if (kind == WeaponKind.FreezeLauncher) save.Data.progress.equippedSecondary = (int)SecondaryWeaponType.FreezeLauncher;
                    else if (kind == WeaponKind.RemoteCharge) save.Data.progress.equippedSecondary = (int)SecondaryWeaponType.RemoteCharge;
                }
                save.MarkDirty(); SfxPlayer.Instance.PlayUi(SfxSynth.LapDing); Show("garage"); return true;
            }
            Toast(Loc.Get("garage.not_enough")); return false;
        }

        public bool EquipWeapon(WeaponKind kind)
        {
            var save = PlatformGlue.Save;
            if (save == null) return false;
            int level = WeaponCatalog.Level(save.Data.progress.weapons, (int)kind);
            if (level <= 0) return false;

            if (kind == WeaponKind.MachineGun)
            {
                save.Data.progress.equippedPrimary = save.Data.progress.equippedPrimary == 1 ? 0 : 1;
            }
            else if (kind == WeaponKind.Mines)
            {
                save.Data.progress.equippedSecondary = (int)SecondaryWeaponType.Mines;
            }
            else if (kind == WeaponKind.FreezeLauncher)
            {
                save.Data.progress.equippedSecondary = (int)SecondaryWeaponType.FreezeLauncher;
            }
            else if (kind == WeaponKind.RemoteCharge)
            {
                save.Data.progress.equippedSecondary = (int)SecondaryWeaponType.RemoteCharge;
            }

            save.MarkDirty();
            SfxPlayer.Instance.PlayUi(SfxSynth.Beep);
            Show("garage");
            return true;
        }

        private void BuildSettings(RectTransform root)
        {
            var content = UiFactory.Scroll(Header(root, "settings.title", showWallet: false), "SettingsScroll", Vector2.zero, Vector2.zero);
            var save = PlatformGlue.Save;
            string lang = save != null ? save.Data.settings.lang : "";
            int index = Array.IndexOf(LanguageCycle, lang);
            string language = index <= 0 ? Loc.Get("settings.language.system") + " (" + Loc.Language + ")" : LanguageNames[index];
            if (save != null && save.LocalWriteFailed)
                UiFactory.Button(Item(content, "LocalSaveRetry", 86f, false), "RetryLocalSave", Loc.Get("save.local.retry"),
                    () => { saveWarningAcknowledged = false; UpdateSaveRecovery(); }, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 28f);
            Section(content, "LanguageAudioTitle", Loc.Get("settings.group.language_audio"));
            var value = SettingValue(content, "Language", "settings.language");
            UiFactory.Button(value, "LanguagePrevious", "‹", () => PlatformGlue.SetLanguageOverride(LanguageCycle[(Mathf.Max(0, index) + LanguageCycle.Length - 1) % LanguageCycle.Length]), Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(72f, 0f), 36f, UiFactory.Raised);
            Label(value, "LanguageName", language, 26f, Vector2.zero, Vector2.one, new Vector2(82f, 4f), new Vector2(-82f, -4f), Color.white, TextAlignmentOptions.Center);
            UiFactory.Button(value, "LanguageNext", "›", () => PlatformGlue.SetLanguageOverride(LanguageCycle[(Mathf.Max(0, index) + 1) % LanguageCycle.Length]), new Vector2(1f, 0f), Vector2.one, new Vector2(-72f, 0f), Vector2.zero, 36f, UiFactory.Raised);
            PercentRow(content, "Sound", "settings.sound", save != null ? save.Data.settings.sfx : 1f, level =>
            {
                if (save == null) return;
                // Freeze the legacy effective music level before moving the independent effects slider.
                if (!save.Data.settings.musicChosen)
                {
                    save.Data.settings.music = MusicDirector.MusicLevel(save.Data.settings);
                    save.Data.settings.musicChosen = true;
                }
                save.Data.settings.sfx = level; save.MarkDirty();
                SfxPlayer.Instance.volume = level * 0.8f;
            });
            // MusicPlayer observes changes, including an explicitly chosen 80% value.
            PercentRow(content, "Music", "settings.music", save != null ? MusicDirector.MusicLevel(save.Data.settings) : 0f, level =>
            { if (save == null) return; save.Data.settings.music = level; save.Data.settings.musicChosen = true; save.MarkDirty(); });
            Section(content, "ControlsTitle", Loc.Get("settings.group.controls"));
            var steeringSettings = save?.Data.settings;
            PercentRow(content, "StickDeadzone", "settings.stick_deadzone", PlayerDriverInput.StickDeadzone(steeringSettings), level =>
            {
                if (steeringSettings == null) return;
                steeringSettings.stickSensitivity = PlayerDriverInput.StickSensitivity(steeringSettings);
                steeringSettings.stickDeadzone = level; save.MarkDirty(); RefreshStickResponse();
            }, 0, 40, "Slider");
            PercentRow(content, "StickSensitivity", "settings.stick_sensitivity", PlayerDriverInput.StickSensitivity(steeringSettings), level =>
            {
                if (steeringSettings == null) return;
                steeringSettings.stickDeadzone = PlayerDriverInput.StickDeadzone(steeringSettings);
                steeringSettings.stickSensitivity = level; save.MarkDirty(); RefreshStickResponse();
            }, 50, 150, "Slider");
            var steerValue = SettingValue(content, "StickLive", "settings.stick_response");
            stickResponse = Label(steerValue, "StickResponse", "", 27f, Vector2.zero, new Vector2(.46f, 1f),
                Vector2.zero, Vector2.zero, Color.white, TextAlignmentOptions.Center);
            RefreshStickResponse();
            UiFactory.Button(steerValue, "RestoreStick", Loc.Get("settings.stick_reset"), () =>
            {
                if (steeringSettings == null) return;
                steeringSettings.stickSensitivity = steeringSettings.stickDeadzone = 0f;
                save.MarkDirty(); Show("settings");
            }, new Vector2(.46f, 0f), Vector2.one, new Vector2(4f, 0f), Vector2.zero, 25f, UiFactory.Raised);
            ToggleRow(content, "AutoThrottle", "settings.auto_throttle", save != null && save.Data.settings.autoThrottle, enabled =>
            {
                if (save == null) return;
                save.Data.settings.autoThrottle = enabled; save.MarkDirty();
                var driver = race.Player != null ? race.Player.GetComponent<PlayerDriverInput>() : null;
                if (driver != null) driver.autoThrottle = enabled || TouchControlsPresenter.IsShown;
                Show("settings");
            });
            ToggleRow(content, "TouchLarge", "settings.touch_large", save != null && save.Data.settings.touchLargeButtons, enabled =>
            { if (save == null) return; save.Data.settings.touchLargeButtons = enabled; save.MarkDirty(); Show("settings"); });
            ToggleRow(content, "TouchLeft", "settings.touch_left", save != null && save.Data.settings.touchLeftHanded, enabled =>
            { if (save == null) return; save.Data.settings.touchLeftHanded = enabled; save.MarkDirty(); Show("settings"); });
            var controls = SettingValue(content, "TouchLayout", "settings.touch_layout");
            UiFactory.Button(controls, "EditTouchLayout", Loc.Get("settings.move_buttons"),
                () => GetComponent<TouchControlsPresenter>()?.OpenLayoutEditor(),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 26f, UiFactory.Raised);
            if (!PlatformGlue.IsMobileDevice)
            {
                UiFactory.Button(Item(content, "KeySettings", 88f, false), "EditDrivingKeys", Loc.Get("keys.title"),
                    () => Show("keys"), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 28f, UiFactory.Raised);
                ToggleRow(content, "AutoAim", "settings.auto_aim", save == null || save.Data.settings.autoAim, enabled =>
                { if (save == null) return; save.Data.settings.autoAim = enabled; save.MarkDirty(); Show("settings"); });
            }
            var hints = SettingValue(content, "Hints", "settings.hints");
            UiFactory.Button(hints, "ReplayHints", Loc.Get("settings.hints.replay"), () =>
            {
                if (save == null) return;
                save.Data.settings.hintsSeen = 0;
                save.MarkDirty();
                Toast(Loc.Get("settings.hints.reset"));
            }, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 26f, UiFactory.Raised);
            Section(content, "DisplayTitle", Loc.Get("settings.group.display"));
            ToggleRow(content, "LargeText", "settings.large_text", save != null && save.Data.settings.largeText, enabled =>
            { if (save == null) return; save.Data.settings.largeText = enabled; save.MarkDirty(); Show("settings"); });
            ToggleRow(content, "CompactHud", "settings.compact_hud", save != null && save.Data.settings.compactHud, enabled =>
            { if (save == null) return; save.Data.settings.compactHud = enabled; save.MarkDirty(); Show("settings"); });
            if (PlatformGlue.HasQualityChoice && save != null)
            {
                var settings = save.Data.settings;
                var options = SettingValue(content, "Graphics", "settings.graphics");
                var keys = new[] { "settings.graphics.auto", "settings.graphics.low", "settings.graphics.high" };
                int selected = settings.qualityAuto ? 0 : settings.quality <= 0 ? 1 : 2;
                for (int i = 0; i < keys.Length; i++)
                {
                    int quality = i;
                    UiFactory.Button(options, "Quality" + i, Loc.Get(keys[i]), () => { settings.qualityAuto = quality == 0; settings.quality = quality == 2 ? 1 : 0; save.MarkDirty(); PlatformGlue.ApplyQuality(); Show("settings"); }, new Vector2(i / 3f, 0f), new Vector2((i + 1f) / 3f, 1f), new Vector2(3f, 0f), new Vector2(-3f, 0f), 24f, selected == i ? UiFactory.Accent : UiFactory.Raised);
                }
            }
            ToggleRow(content, "Shake", "settings.shake", save == null || save.Data.settings.cameraShake, enabled =>
            { if (save == null) return; save.Data.settings.cameraShake = enabled; save.MarkDirty(); Show("settings"); });
            ToggleRow(content, "ReducedFlashes", "settings.reduced_flashes", save != null && save.Data.settings.reducedCombatFlashes, enabled =>
            { if (save == null) return; save.Data.settings.reducedCombatFlashes = enabled; save.MarkDirty(); Show("settings"); });
            ToggleRow(content, "DynamicCamera", "settings.dynamic_camera", save == null || save.Data.settings.dynamicCamera, enabled =>
            { if (save == null) return; save.Data.settings.dynamicCamera = enabled; save.MarkDirty(); Show("settings"); });
            var distances = SettingValue(content, "CameraDistance", "settings.camera_distance");
            var values = new[] { "80%", "100%", "130%" };
            int currentDistance = Mathf.Clamp(save?.Data.settings.cameraDistance ?? 0, -1, 1);
            for (int i = 0; i < values.Length; i++)
            {
                int distance = i - 1;
                UiFactory.Button(distances, "CameraDistance" + i, values[i], () =>
                {
                    if (save == null) return;
                    save.Data.settings.cameraDistance = distance; save.MarkDirty(); Show("settings");
                }, new Vector2(i / 3f, 0f), new Vector2((i + 1f) / 3f, 1f), new Vector2(3f, 0f), new Vector2(-3f, 0f),
                    26f, currentDistance == distance ? UiFactory.Accent : UiFactory.Raised);
            }
        }

        private static RectTransform SettingValue(Transform content, string name, string key)
        {
            // The garage also uses this row inside a narrower catalog column.
            bool stacked = ((RectTransform)content).rect.width < 740f;
            var row = Item(content, name, stacked ? 152f : 96f, false);
            const float split = 0.44f;
            Label(row, "Name", Loc.Get(key), 30f, stacked ? Top : Vector2.zero, stacked ? Vector2.one : new Vector2(split, 1f),
                new Vector2(16f, stacked ? -64f : 6f), new Vector2(-12f, -6f));
            UiFactory.Panel(row, "Divider", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 2f), UiFactory.Raised, false);
            return UiFactory.Panel(row, "Value", stacked ? Vector2.zero : new Vector2(split, 0.5f), stacked ? new Vector2(1f, 0f) : new Vector2(1f, 0.5f),
                new Vector2(12f, stacked ? 8f : -36f), new Vector2(-12f, stacked ? 80f : 36f));
        }
        private void ToggleRow(Transform content, string name, string key, bool enabled, Action<bool> change)
        {
            var value = SettingValue(content, name, key);
            for (int i = 0; i < 2; i++)
            {
                bool state = i == 1;
                UiFactory.Button(value, name + (state ? "On" : "Off"), Loc.Get(state ? "settings.on" : "settings.off"), () => change(state), new Vector2(i * 0.5f, 0f), new Vector2((i + 1) * 0.5f, 1f), new Vector2(3f, 0f), new Vector2(-3f, 0f), 25f, state == enabled ? UiFactory.Accent : UiFactory.Raised);
            }
        }

        private void RefreshStickResponse()
        {
            var pad = Gamepad.current;
            int value = pad == null ? -101 : Mathf.RoundToInt(PlayerDriverInput.ReadStick(pad, PlatformGlue.Save?.Data.settings) * 100f);
            if (stickResponse == null || value == lastStickPercent) return;
            lastStickPercent = value;
            stickResponse.text = value == -101 ? "—" : (value > 0 ? "+" : "") + value + "%";
        }

        private void PercentRow(Transform content, string name, string key, float level, Action<float> change,
            int minimum = 0, int maximum = 100, string controlSuffix = "Volume")
        {
            var value = SettingValue(content, name, key);
            int percent = Mathf.Clamp(Mathf.RoundToInt(level * 100f), minimum, maximum);
            var label = Label(value, "VolumeValue", percent + "%", 27f, new Vector2(1f, 0f), Vector2.one,
                new Vector2(-92f, 0f), Vector2.zero, Color.white, TextAlignmentOptions.Center);
            var root = UiFactory.Panel(value, name + controlSuffix, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-104f, 0f), new Color(0f, 0f, 0f, 0f));
            var slider = BuildSlider(root, maximum, percent, true, minimum);
            slider.onValueChanged.AddListener(v => { label.text = Mathf.RoundToInt(v) + "%"; change(v / 100f); });
        }

        private static Slider BuildSlider(RectTransform root, float maximum, float initial, bool whole, float minimum = 0f)
        {
            root.GetComponent<Image>().raycastTarget = true;
            var rail = UiFactory.Panel(root, "Rail", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(28f, -5f), new Vector2(-28f, 5f), UiFactory.Raised);
            var fill = UiFactory.Panel(rail, "Fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiFactory.Accent);
            var handleArea = UiFactory.Panel(root, "HandleArea", Vector2.zero, Vector2.one, new Vector2(28f, 0f), new Vector2(-28f, 0f));
            // Slider stretches the handle vertically; offsets inset it inside the target instead of adding a second height.
            var handle = UiFactory.Panel(handleArea, "Handle", Vector2.zero, new Vector2(0f, 1f),
                new Vector2(-28f, 4f), new Vector2(28f, -4f), Color.white);
            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = minimum; slider.maxValue = maximum; slider.wholeNumbers = whole;
            slider.SetValueWithoutNotify(initial);
            var colors = slider.colors;
            colors.highlightedColor = colors.selectedColor = UiFactory.Accent;
            slider.colors = colors;
            root.gameObject.AddComponent<UiButtonFx>();
            return slider;
        }

        private void BuildLeaders(RectTransform root)
        {
            var content = UiFactory.Scroll(Header(root, "lb.title"), "LeadersScroll", Vector2.zero, Vector2.zero);
            Section(content, "LocalTitle", Loc.Get("lb.local"));
            int count = 0;
            foreach (var ev in Events())
            {
                var best = PlatformGlue.Save?.Data.progress.bests.Find(b => b.eventId == ev.eventId);
                string record = EventBestText(ev, best);
                if (record.Length == 0) continue;
                Action watch = null;
                if (ev.mode == EventMode.Race && PlatformGlue.Save.Data.personalGhosts.HasForEvent(ev.eventId, ev.laps))
                    watch = () => { if (flow.OpenReplay(ev)) Show("replay"); else Toast(Loc.Get("replay.unavailable")); };
                BoardRow(content, "Local_" + ev.eventId, (++count).ToString("00"), Loc.Get(ev.nameKey),
                    ev.mode == EventMode.Arena ? record : UiFactory.FormatTime(best.bestTimeMs / 1000.0), true,
                    ev.mode == EventMode.Arena ? null : RecordSetupText(best.setup), watch);
            }
            if (count == 0)
            {
                var empty = Item(content, "Empty", 194f);
                Label(empty, "Message", Loc.Get("lb.empty"), 32f, Top, Vector2.one, new Vector2(28f, -78f), new Vector2(-28f, -20f), UiFactory.Muted, TextAlignmentOptions.Center);
                UiFactory.Button(empty, "FirstRace", Loc.Get("menu.play") + " ›", () => Show("events"), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-200f, 26f), new Vector2(200f, 94f), 28f);
            }
            BuildAttemptHistory(content);
            var platform = PlatformGlue.Platform;
            var selected = BuildBoardSelector(content);
            if (!boardSelectorOpen) BuildFriendChallenge(content, selected);
            if (selected != null && !boardSelectorOpen && platform?.Sessions != null) BuildRivalCard(content, selected);
            if (platform != null && platform.HasFeature(PlatformFeature.Auth) && !platform.Player.IsAuthorized)
            {
                var auth = Item(content, "Account", Narrow ? 220f : 176f);
                Label(auth, "Hint", Loc.Get("lb.sign_in.hint"), 24f, Top, Vector2.one, new Vector2(28f, -90f), new Vector2(-28f, -18f), UiFactory.Muted);
                UiFactory.Button(auth, "SignIn", Loc.Get(authInFlight ? "common.loading" : "lb.sign_in"), authInFlight ? null : async () =>
                {
                    if (authInFlight) return; authInFlight = true; Show("leaders");
                    try
                    {
                        var result = await platform.RequestAuthAsync();
                        if (this == null) return;
                        if (result.Ok && result.Value) await PlatformGlue.SubmitAllBestsAsync();
                        else Toast(Loc.Get("common.unavailable"));
                    }
                    finally { if (this != null) { authInFlight = false; if (current == "leaders") Show("leaders"); } }
                }, Vector2.zero, new Vector2(1f, 0f), new Vector2(28f, 22f), new Vector2(-28f, 88f), 26f, UiFactory.Raised);
            }
            else if (!boardSelectorOpen && platform?.Leaderboards != null) FillBoards(content, platform.Leaderboards, selected);
        }
        private void BuildRivalCard(Transform content, RaceEventDefinition ev)
        {
            var best = PlatformGlue.Save?.Data.progress.bests.Find(b => b != null && b.eventId == ev.eventId);
            if (ev.mode != EventMode.Race || ev.twist == Core.Daily.RaceTwist.TimeTrial || best?.setup == null || !best.setup.IsKnown) return;
            var card = Item(content, "RivalMatch", 160f);
            var status = Label(card, "RivalStatus", Loc.Format("events.best", "t", UiFactory.FormatMilliseconds(best.bestTimeMs)), 26f,
                Top, Vector2.one, new Vector2(22f, -76f), new Vector2(-22f, -8f));
            Button find = null;
            find = UiFactory.Button(card, "FindRival", Loc.Get("rival.find"), async () =>
            {
                find.interactable = false; status.text = Loc.Get("common.loading");
                var pick = await flow.PreviewRivalAsync(ev);
                if (status == null || !status.gameObject.activeInHierarchy) return;
                find.interactable = true;
                if (pick == null) { status.text = Loc.Get("rival.empty"); return; }
                string delta = (pick.DifferenceMs < 0 ? "−" : pick.DifferenceMs > 0 ? "+" : "") + UiFactory.FormatMilliseconds(Math.Abs(pick.DifferenceMs));
                status.text = Loc.Format("rival.match", "t", UiFactory.FormatMilliseconds(pick.Replay.DurationMilliseconds), "d", delta);
            }, Vector2.zero, new Vector2(.64f, 0f), new Vector2(16f, 10f), new Vector2(-6f, 78f), 23f, UiFactory.Raised);
            UiFactory.Button(card, "StartMatchedRace", Loc.Get("menu.play"), () => flow.StartRace(ev, SelectedTier()),
                new Vector2(.64f, 0f), new Vector2(1f, 0f), new Vector2(6f, 10f), new Vector2(-16f, 78f), 25f);
        }

        private void BuildAttemptHistory(Transform content)
        {
            var attempts = PlatformGlue.Save?.Data.progress.recentAttempts;
            if (attempts == null || attempts.Count == 0) return;
            Section(content, "AttemptHistory", Loc.Get("record.recent"));
            for (int i = 0; i < attempts.Count; i++)
            {
                var attempt = attempts[i];
                var ev = Array.Find(flow.events, e => e != null && e.eventId == attempt.eventId);
                string name = ev != null ? Loc.Get(ev.nameKey) : attempt.eventId.StartsWith("d", StringComparison.Ordinal)
                    && int.TryParse(attempt.eventId.Substring(1), out _) ? Loc.Get("daily.title") : UiFactory.Escape(attempt.eventId);
                string time = attempt.hasArenaScore ? Loc.Format("arena.points", "n", attempt.arenaPoints) : UiFactory.FormatMilliseconds(attempt.timeMs);
                var older = Core.Save.RecentAttempts.PreviousComparable(attempts, i);
                if (older != null)
                {
                    long difference = attempt.hasArenaScore ? attempt.arenaPoints - older.arenaPoints : attempt.timeMs - older.timeMs;
                    string sign = difference < 0 ? "−" : difference > 0 ? "+" : "";
                    time += "\n" + sign + (attempt.hasArenaScore ? Loc.Format("arena.points", "n", Math.Abs(difference)) : UiFactory.FormatMilliseconds(Math.Abs(difference)));
                }
                BoardRow(content, "RecentAttempt" + i, (i + 1).ToString(), name, time, false, RecordSetupText(attempt.setup));
            }
        }

        private static void Section(Transform content, string name, string title)
        {
            var row = Item(content, name, 52f, false);
            Label(row, "Title", title, 26f, Vector2.zero, Vector2.one, new Vector2(4f, 0f), Vector2.zero, UiFactory.Accent).fontStyle = FontStyles.Bold;
        }
        private static string RecordSetupText(Core.Save.RecordSetup setup)
        {
            if (setup == null || !setup.IsKnown) return Loc.Get("record.unknown");
            string tier = setup.tier == "easy" || setup.tier == "normal" || setup.tier == "hard" ? Loc.Get("difficulty." + setup.tier) : Loc.Get("record.unknown");
            string body = Loc.Get(CarBodies.Get(setup.body).NameKey);
            string build = Loc.Get("build." + ((CarBuild)Mathf.Clamp(setup.build, 0, 3)).ToString().ToLowerInvariant());
            string upgrades = Loc.Format("record.upgrades", "engine", UpgradeCatalog.Level(setup.upgrades, 0),
                "armor", UpgradeCatalog.Level(setup.upgrades, 1), "cannon", UpgradeCatalog.Level(setup.upgrades, 2));
            return tier + " · " + body + " · " + build + "\n" + upgrades + (setup.continued ? " · " + Loc.Get("record.continued") : "");
        }

        private static void BoardRow(Transform content, string id, string rank, string name, string time, bool highlight, string detail = null, Action watch = null)
        {
            bool hasDetail = !string.IsNullOrEmpty(detail);
            var row = Item(content, id, hasDetail ? 180f : 90f);
            Label(row, "Rank", rank, 30f, Vector2.zero, new Vector2(0f, 1f), new Vector2(20f, 12f), new Vector2(112f, -12f), highlight ? UiFactory.Accent : UiFactory.Muted, TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
            Label(row, "Name", name, 28f, Vector2.zero, Vector2.one, new Vector2(140f, hasDetail ? 96f : 12f), new Vector2(-250f, -12f));
            Label(row, "Time", time, 30f, new Vector2(1f, 0f), Vector2.one, new Vector2(-240f, hasDetail ? 96f : 12f), new Vector2(-26f, -12f), highlight ? UiFactory.Positive : Color.white, TextAlignmentOptions.MidlineRight);
            if (hasDetail)
            {
                var setupLabel = Label(row, "RecordSetup", detail, 23f, Vector2.zero, new Vector2(1f, 0f), new Vector2(140f, 14f), new Vector2(watch == null ? -26f : -232f, 92f), UiFactory.Muted);
                setupLabel.fontSizeMin = 22f;
            }
            if (watch != null)
                UiFactory.Button(row, "Replay_" + id, Loc.Get("replay.watch"), watch, new Vector2(1f, 0f), new Vector2(1f, 0f),
                    new Vector2(-216f, 18f), new Vector2(-26f, 86f), 25f, UiFactory.Raised);
        }
        private async void FillCloudBoard(RectTransform content, ILeaderboards boards, RaceEventDefinition selected)
        {
            if (selected == null) return;
            Section(content, "CloudTitle", Loc.Get("lb.title") + " · " + Loc.Get(selected.nameKey));
            var status = Item(content, "CloudStatus", 90f);
            var label = Label(status, "Status", Loc.Get("common.loading"), 26f, Vector2.zero, Vector2.one, new Vector2(28f, 12f), new Vector2(-28f, -12f), UiFactory.Muted);
            string board = RaceSetup.BoardNameFor(selected);
            var result = await boards.GetAsync(board, 10, true, 3);
            if (content == null || !content.gameObject.activeInHierarchy) return;
            if (!result.Ok) { label.text = Loc.Get("common.unavailable"); return; }
            if (result.Value == null || result.Value.Count == 0) { label.text = Loc.Get("lb.empty"); return; }
            status.gameObject.SetActive(false); Destroy(status.gameObject);
            foreach (var entry in result.Value) BoardRow(content, "Cloud" + entry.Rank, entry.Rank.ToString("00"), UiFactory.Escape(entry.Name),
                board == Core.Sim.ArenaScore.Board ? Loc.Format("arena.points", "n", entry.Score) : UiFactory.FormatTime(entry.Score / 1000.0), false);
        }

        private static string EventBestText(RaceEventDefinition ev, Core.Save.EventBest best)
        {
            if (best == null) return "";
            if (ev.mode == EventMode.Arena) return best.hasArenaScore ? Loc.Format("arena.points", "n", best.arenaPoints) : "";
            return best.bestTimeMs > 0 ? Loc.Format("events.best", "t", UiFactory.FormatTime(best.bestTimeMs / 1000.0)) : "";
        }

        private static readonly Color Gold = new Color(1f, 0.8f, 0.25f), Silver = new Color(0.78f, 0.82f, 0.86f), Bronze = new Color(0.85f, 0.56f, 0.35f);
        private static Color PlaceColor(RaceResultEntry me)
        {
            if (me == null || !me.Finished) return UiFactory.Muted;
            return me.Position == 1 ? Gold : me.Position == 2 ? Silver : me.Position == 3 ? Bronze : UiFactory.Accent;
        }

        /// <summary>"New record! −0.83 s" / "+2.10 s to your record" / "Record set!" — empty for a DNF.</summary>
        private string RecordLine(RaceResultEntry me)
        {
            if (me == null || !me.Finished) return "";
            if (flow.CurrentEvent != null && flow.CurrentEvent.mode == EventMode.Arena)
                return (flow.LastWasPersonalBest ? Loc.Get("results.pb_first") + "  " : "") + Loc.Format("arena.points", "n", me.ArenaPoints.Total);
            if (!flow.LastHadPreviousBest) return Loc.Get("results.pb_first");
            if (!flow.LastWasPersonalBest && !flow.LastBestComparable)
                return Loc.Get(flow.LastBestSetupKnown ? "results.pb_other_setup" : "results.pb_unknown_setup");
            string seconds = (Math.Abs(flow.LastBestDeltaMs) / 1000.0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            return flow.LastWasPersonalBest ? Loc.Format("results.pb_new", "t", seconds) : Loc.Format("results.pb_behind", "t", seconds);
        }

        private static int CheapestUpgrade()
        {
            var levels = PlatformGlue.Save?.Data.progress.upgrades;
            int best = int.MaxValue;
            foreach (UpgradeKind kind in Enum.GetValues(typeof(UpgradeKind))) best = Math.Min(best, UpgradeCatalog.Cost(UpgradeCatalog.Level(levels, (int)kind)));
            var wLevels = PlatformGlue.Save?.Data.progress.weapons;
            foreach (WeaponKind kind in Enum.GetValues(typeof(WeaponKind))) best = Math.Min(best, WeaponCatalog.Cost(WeaponCatalog.Level(wLevels, (int)kind)));
            return best;
        }

        private void BuildResultStars(Transform content, RaceResultEntry me)
        {
            if (DailyRace.IsDaily(flow.CurrentEvent)) return;
            int currentStars = flow.LastStarsEarned;
            float rowHeight = UiFactory.TextScale > 1f ? 114f : 84f;
            var starsCard = Item(content, "StarsCard", 52f + 3f * rowHeight);
            Label(starsCard, "StarsTitle", Loc.Format("results.stars_this_race", "n", RaceEventDefinition.CountStars(currentStars)), 22f,
                Top, Vector2.one, new Vector2(24f, -42f), new Vector2(-24f, -2f), UiFactory.Accent).fontStyle = FontStyles.Bold;
            var bestEntry = PlatformGlue.Save?.Data.progress.bests.Find(b => b.eventId == (flow.CurrentEvent != null ? flow.CurrentEvent.eventId : ""));
            int totalEventStars = bestEntry != null ? bestEntry.stars : currentStars;

            for (int s = 0; s < 3; s++)
            {
                int bit = 1 << s;
                bool wonThisRace = (currentStars & bit) != 0;
                bool wonOverall = (totalEventStars & bit) != 0;
                // Gold always means earned in this race. A saved achievement is an outlined
                // star with an explicit caption, so a failed objective cannot look completed.
                float bottom = 12f + (2 - s) * rowHeight;
                var starRow = UiFactory.Panel(starsCard, "Star" + s, Vector2.zero, new Vector2(1f, 0f), new Vector2(24f, bottom), new Vector2(-24f, bottom + rowHeight - 4f));
                Label(starRow, "StarIcon", wonThisRace ? "★" : "☆", 34f, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(34f, 0f), wonThisRace ? UiFactory.Accent : UiFactory.Muted);
                string description = StarGoalText.Description(flow.CurrentEvent, s);
                if (!wonThisRace)
                {
                    string progress = StarGoalText.MissedProgress(flow.CurrentEvent, me, s);
                    if (progress.Length > 0) description += "  ·  " + progress;
                }
                Label(starRow, "StarDesc", description, 25f, new Vector2(0f, 0.4f), Vector2.one, new Vector2(46f, 0f), Vector2.zero, wonThisRace ? Color.white : UiFactory.Muted);
                string status = wonThisRace ? "results.star_earned" : wonOverall ? "results.star_previous" : "results.star_missed";
                Label(starRow, "StarStatus", Loc.Get(status), 22f, Vector2.zero, new Vector2(1f, 0.4f), new Vector2(46f, 0f), Vector2.zero, wonThisRace ? UiFactory.Positive : UiFactory.Muted);
            }
        }

        private void BuildResults(RectTransform root)
        {
            var results = flow.ResultsForMenu;
            if (results == null) { Show("main"); return; }
            var body = Header(root, "results.title");
            bool columns = root.rect.width >= 1280f;
            var overview = columns ? UiFactory.Panel(body, "Overview", Vector2.zero, new Vector2(0.48f, 1f), Vector2.zero, new Vector2(-16f, 0f)) : body;
            // Keep the next race, retry and garage available while reading long results.
            // Focusing these buttons must not scroll the reward out of view on arrival.
            var content = UiFactory.Scroll(overview, "ResultsScroll", new Vector2(0f, 104f), Vector2.zero);
            int reward = flow.LastTotalReward;
            var summary = Item(content, "Summary", 216f);
            ShowSavingsProgress(content);
            summary.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.22f);
            UiFactory.Panel(summary, "GoldRule", Top, Vector2.one, new Vector2(28f, -4f), new Vector2(-28f, -1f), UiFactory.Accent, false);
            var medal = UiFactory.Panel(summary, "Medal", Vector2.zero, new Vector2(0f, 1f), new Vector2(24f, 24f), new Vector2(194f, -24f), new Color(1f, 0.8f, 0.25f, 0.08f));
            var me = results.Player;
            bool missionEvent = flow.CurrentEvent != null && flow.CurrentEvent.mode == EventMode.Mission;
            string unfinishedKey = missionEvent ? "results.defeat" : "results.dnf";
            Label(medal, "Place", me == null ? "—" : me.Finished ? "#" + me.Position : Loc.Get(me.Eliminated ? "results.eliminated" : unfinishedKey), me != null && me.Finished ? 86f : 38f, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f), PlaceColor(me), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
            var rewardText = Label(summary, "Reward", Loc.Format("results.reward", "n", reward), 40f, Top, Vector2.one, new Vector2(234f, -80f), new Vector2(-28f, -20f), UiFactory.Accent);
            rewardText.fontStyle = FontStyles.Bold;
            if (Time.unscaledTime - rewardShownAt < RewardCountSeconds) rewardLabel = rewardText;   // count up once, not on every rebuild
            Label(summary, "Detail", flow.CurrentEvent != null && flow.CurrentEvent.eventId == TrainMission.EventId ? Loc.Get(TrainHud.ResultKey(TrainMission.Current?.Outcome ?? TrainOutcome.Running))
                : SettlementMission.Current != null ? Loc.Get(flow.CurrentEvent.nameKey + (me != null && me.Finished ? ".win" : ".loss"))
                : flow.LastRewardDoubled ? Loc.Get("results.ad_received") : missionEvent ? Loc.Get(me != null && me.Finished ? "mission.victory" : "mission.defeat")
                : flow.CurrentEvent != null ? Loc.Get(flow.CurrentEvent.nameKey) : Loc.Get("results.title"), 26f, Top, Vector2.one, new Vector2(234f, -124f), new Vector2(-28f, -84f), UiFactory.Muted);
            string record = RecordLine(me);
            if (record.Length > 0) Label(summary, "Record", record, 24f, Top, Vector2.one, new Vector2(234f, -170f), new Vector2(-28f, -128f), flow.LastWasPersonalBest ? UiFactory.Positive : UiFactory.Muted);

            if (me != null && me.Finished && flow.LastSectorDeltaMs != null)
                BuildSectorComparison(content, flow.LastSectorDeltaMs);
            var training = flow.LastTrainingSectors;
            if (training != null && training.LapMilliseconds > 0)
                BuildSectorComparison(content, training.milliseconds, training.LapMilliseconds);
            var fortress = MissionDirector.Current;
            if (fortress != null && fortress.Result != MissionDirector.Outcome.None)
            {
                var stats = fortress.FinalContribution;
                var line = Item(content, "MissionContribution", Narrow ? 108f : 80f);
                Label(line, "PersonalStats", Loc.Format("mission.contribution", "d", Mathf.RoundToInt(stats.CoreDamage), "s", stats.Stations, "a", stats.AlliesSaved),
                    24f, Vector2.zero, Vector2.one, new Vector2(18f, 4f), new Vector2(-18f, -4f));
            }
            BuildRewardDetails(content, me, missionEvent);
            BuildResultStars(content, me);
            var lesson = flow.MissedLesson;
            if (lesson != PracticeLesson.None)
            {
                var row = Item(content, "MissedGoalLesson", 80f, false);
                UiFactory.Button(row, "PracticeMissedGoal", Loc.Format("results.practice_goal", "lesson", Loc.Get(GameFlow.PracticeTitleKey(lesson))),
                    () => flow.StartPractice(lesson), Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f), 26f, UiFactory.Raised);
            }
            var next = RecommendedNext();
            BuildNextGoal(content, next);
            var actions = UiFactory.Panel(overview, "Actions", Vector2.zero, new Vector2(1f, 0f),
                Vector2.zero, new Vector2(0f, 88f));
            var keys = new[] { "results.next", "results.retry", "menu.garage" };
            bool canAdvance = next != null && next != flow.CurrentEvent;
            Action[] clicks = { canAdvance ? () => StartMenuEvent(next, RaceSetup.Tier) : () => flow.RetryRace(), canAdvance ? () => flow.RetryRace() : () => GoTo("events"), () => GoTo("garage") };
            int cheapest = CheapestUpgrade();
            bool canUpgrade = PlatformGlue.Save != null && cheapest < int.MaxValue && PlatformGlue.Save.Data.Coins >= cheapest;
            UiFactory.Button(actions, "ResultAction0", Loc.Get(canAdvance ? keys[0] : "results.retry") + "   ›", clicks[0],
                Vector2.zero, new Vector2(0.4f, 1f), new Vector2(4f, 4f), new Vector2(-4f, -4f), 28f);
            for (int i = 1; i < keys.Length; i++)
                UiFactory.Button(actions, "ResultAction" + i, Loc.Get(i == 1 && !canAdvance ? "events.title" : keys[i]), clicks[i],
                    new Vector2(0.4f + (i - 1) * 0.3f, 0f), new Vector2(0.4f + i * 0.3f, 1f),
                    new Vector2(4f, 4f), new Vector2(-4f, -4f), 26f, UiFactory.Raised);
            if (PlatformGlue.Save != null && cheapest < int.MaxValue)
            {
                var nudge = Item(content, "GarageNudge", 44f, false);
                string text = canUpgrade ? Loc.Get("results.upgrade_ready") : Loc.Format("results.upgrade_short", "n", cheapest - PlatformGlue.Save.Data.Coins);
                Label(nudge, "Text", text, 22f, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f), canUpgrade ? UiFactory.Positive : UiFactory.Muted, TextAlignmentOptions.MidlineRight);
            }
            ResultsExtras(content);
            var ads = PlatformGlue.Platform?.Ads;
            if (!flow.LastRewardDoubled && flow.LastRewardFinished && flow.LastReward > 0 && ads != null && ads.IsRewardedAvailable)
            {
                var ad = Item(content, "RewardAction", 68f, false);
                rewardButton = UiFactory.Button(ad, "Double", Loc.Format("results.double_amount", "n", flow.LastReward), () => flow.DoubleReward(), Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-8f, 0f), 26f, UiFactory.Raised);
                rewardButton.GetComponentInChildren<TextMeshProUGUI>().color = UiFactory.Positive;
            }
            var standings = columns ? UiFactory.Panel(body, "Standings", new Vector2(0.48f, 0f), Vector2.one, new Vector2(16f, 0f), Vector2.zero) : null;
            var board = columns ? UiFactory.Scroll(standings, "StandingsScroll", Vector2.zero, Vector2.zero) : content;
            Section(board, "StandingsTitle", Loc.Get("lb.title"));
            if (results.IsArena) Section(board, "TieRule", Loc.Get("arena.tie_rule"));
            foreach (var entry in results.Entries)
            {
                var row = Item(board, results.IsArena ? "ArenaResult" + entry.CarId : "Result" + entry.Position, results.IsArena ? 202f : 154f);
                if (entry.IsPlayer) row.GetComponent<Image>().color = new Color(0.19f, 0.17f, 0.105f);
                if (entry.IsPlayer) UiFactory.Panel(row, "Accent", Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 14f), new Vector2(4f, -14f), UiFactory.Accent, false);
                Label(row, "Rank", entry.Position.ToString("00"), 42f, Vector2.zero, new Vector2(0f, 1f), new Vector2(22f, 18f), new Vector2(112f, -18f), entry.IsPlayer ? UiFactory.Accent : UiFactory.Muted, TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
                Label(row, "Name", UiFactory.Escape(HudScreen.GetLocalizedRacerName(entry.Name, entry.IsPlayer)), 30f, Top, Vector2.one, new Vector2(120f, -66f), new Vector2(-26f, -14f)).fontStyle = FontStyles.Bold;
                string progress = entry.Finished ? Loc.Format("results.best_lap", "t", UiFactory.FormatTime(entry.BestLap))
                    : Loc.Format("results.laps_completed", "n", entry.Laps, "total", results.LapsRequired);
                string combat = Loc.Format("results.kills", "k", entry.Kills, "d", entry.Deaths);
                if (results.IsArena) combat += "   " + Loc.Format("arena.assists", "n", entry.Assists) + "\n" + HudScreen.ArenaBreakdown(entry.ArenaPoints);
                Label(row, "Detail", missionEvent || results.IsArena ? combat : progress + "   /   " + combat, 22f, Vector2.zero, Vector2.one, new Vector2(120f, 16f), new Vector2(-224f, -76f), UiFactory.Muted);
                Label(row, "Time", results.IsArena ? Loc.Format("arena.points", "n", entry.ArenaPoints.Total)
                    : entry.Finished ? UiFactory.FormatTime(entry.TotalTime) : Loc.Get(entry.Eliminated ? "results.eliminated" : unfinishedKey), 30f, new Vector2(1f, 0f), Vector2.one, new Vector2(-214f, 16f), new Vector2(-26f, -76f), entry.IsPlayer ? UiFactory.Positive : Color.white, TextAlignmentOptions.MidlineRight);
            }
        }
    }
}
