using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using RacingGame.Core.Economy;
using RacingGame.Core.Platform;

namespace RacingGame
{
    /// <summary>Localized runtime menus. Views derive progression and rewards from GameFlow and SaveService.</summary>
    [RequireComponent(typeof(GameFlow))]
    public sealed partial class MenuScreens : MonoBehaviour
    {
        private GameFlow flow;
        private RaceManager race;
        private Canvas canvas;
        private RectTransform portraitPanel;
        private TextMeshProUGUI portraitText;
        private RectTransform panel;
        private CanvasGroup transition;
        private string current = "", pendingScreen;
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
        private static readonly Vector2 Top = new Vector2(0f, 1f);
        private static readonly string[] LanguageCycle = { "", "ru", "en", "tr", "zh", "de", "es", "fr", "pt", "uk" };
        private static readonly string[] LanguageNames = { "", "Русский", "English", "Türkçe", "中文", "Deutsch", "Español", "Français", "Português", "Українська" };
        public string Current => current;
        private bool Narrow => layoutWidth < 1100f;

        private void Awake()
        {
            flow = GetComponent<GameFlow>(); race = GetComponent<RaceManager>();
            canvas = UiFactory.CreateOverlayCanvas("Menus", 30);
            canvas.transform.SetParent(transform, false);
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
            flow.StateChanged += OnFlowState; Loc.LanguageChanged += Rebuild; PlatformGlue.Ready += Rebuild;
        }

        private void OnDisable()
        {
            flow.StateChanged -= OnFlowState; Loc.LanguageChanged -= Rebuild; PlatformGlue.Ready -= Rebuild;
        }

        private void Update()
        {
            if (toastPanel != null && toastPanel.gameObject.activeSelf && Time.unscaledTime > toastUntil) toastPanel.gameObject.SetActive(false);
            bool portrait = PlatformGlue.IsReady && PlatformGlue.IsMobileDevice && Screen.height > Screen.width;
            if (portraitPanel.gameObject.activeSelf != portrait)
            {
                if (portrait) { portraitText.text = Loc.Get("ui.rotate_phone"); portraitPanel.SetAsLastSibling(); }
                portraitPanel.gameObject.SetActive(portrait);
            }
            if (transition != null) transition.alpha = Mathf.MoveTowards(transition.alpha, 1f, Time.unscaledDeltaTime * 7f);
            if (string.IsNullOrEmpty(current)) return;
            var rect = ((RectTransform)canvas.transform).rect;
            if (Mathf.Abs(rect.width - layoutWidth) > 1f || Mathf.Abs(rect.height - layoutHeight) > 1f) { Rebuild(); return; }
            if (current != "main" && (Keyboard.current?.escapeKey.wasPressedThisFrame == true || Gamepad.current?.buttonEast.wasPressedThisFrame == true))
            {
                if (current == "results") GoTo("main"); else Show("main");
            }
            if (rewardLabel != null)
            {
                float k = Mathf.Clamp01((Time.unscaledTime - rewardShownAt) / RewardCountSeconds);
                int target = flow.LastReward * (flow.LastRewardDoubled ? 2 : 1);
                rewardLabel.text = Loc.Format("results.reward", "n", Mathf.RoundToInt(target * (1f - (1f - k) * (1f - k))));
                if (k >= 1f) rewardLabel = null;
            }
            if (rewardButton != null)
            {
                rewardButton.interactable = !flow.IsDoublingReward;
                rewardButton.GetComponentInChildren<TextMeshProUGUI>().text = Loc.Get(flow.IsDoublingReward ? "common.loading" : "results.double");
            }
        }

        public static void Toast(string text)
        {
            if (toastText == null || toastPanel == null) return;
            toastText.text = text; toastPanel.SetAsLastSibling(); toastPanel.gameObject.SetActive(true); toastUntil = Time.unscaledTime + 3.5f;
        }

        public void GoTo(string screen) { pendingScreen = screen; flow.EnterMenu(); }
        private void OnFlowState(FlowState state)
        {
            switch (state)
            {
                case FlowState.Menu: string target = pendingScreen ?? "main"; pendingScreen = null; Show(target); break;
                case FlowState.Racing: Show(""); break;
                case FlowState.Results:
                    rewardShownAt = Time.unscaledTime;
                    Show("results");
                    foreach (var e in flow.LastUnlocked) Toast(Loc.Format("results.unlocked", "name", Loc.Get(e.nameKey)));
                    break;
            }
        }
        private void Rebuild() { Show(current); if (portraitText != null && Loc.Ready) portraitText.text = Loc.Get("ui.rotate_phone"); }

        public void Show(string name)
        {
            string selected = current == name ? EventSystem.current?.currentSelectedGameObject?.name : null;
            var scrollPositions = new Dictionary<string, float>();
            if (current == name && panel != null)
            {
                foreach (var oldScroll in panel.GetComponentsInChildren<ScrollRect>())
                    scrollPositions[oldScroll.name] = oldScroll.verticalNormalizedPosition;
            }
            if (panel != null) { panel.gameObject.SetActive(false); Destroy(panel.gameObject); }
            rewardButton = null; rewardLabel = null; current = name;
            if (string.IsNullOrEmpty(name)) { EventSystem.current?.SetSelectedGameObject(null); return; }
            if (!PlatformGlue.IsReady) return;
            var canvasRect = ((RectTransform)canvas.transform).rect;
            layoutWidth = canvasRect.width; layoutHeight = canvasRect.height;
            panel = UiFactory.Panel(canvas.transform, "Panel_" + name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.015f, 0.025f, 0.045f, 0.82f), false);
            panel.GetComponent<Image>().raycastTarget = true;
            transition = panel.gameObject.AddComponent<CanvasGroup>(); transition.alpha = 0f;
            float width = Mathf.Min(1640f, layoutWidth - 64f);
            var shell = UiFactory.Card(panel, "Shell", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-width * 0.5f, 32f), new Vector2(width * 0.5f, -32f));
            shell.GetComponent<Image>().color = new Color(0.035f, 0.05f, 0.08f, 0.96f);
            var frame = UiFactory.Panel(shell, "Frame", Vector2.zero, Vector2.one, new Vector2(32f, 16f), new Vector2(-32f, -24f));
            switch (name)
            {
                case "main": BuildMain(frame); break;
                case "events": BuildEvents(frame); break;
                case "garage": BuildGarage(frame); break;
                case "settings": BuildSettings(frame); break;
                case "leaders": BuildLeaders(frame); break;
                case "results": BuildResults(frame); break;
            }
            if (toastPanel != null) toastPanel.SetAsLastSibling();
            if (portraitPanel != null && portraitPanel.gameObject.activeSelf) portraitPanel.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            if (panel == null) return;
            foreach (var scroll in panel.GetComponentsInChildren<ScrollRect>())
                scroll.verticalNormalizedPosition = scrollPositions.TryGetValue(scroll.name, out float position) ? position : 1f;
            var buttons = panel.GetComponentsInChildren<Button>();
            Button focus = Array.Find(buttons, b => b.interactable && b.name == selected)
                ?? Array.Find(buttons, b => b.interactable && b.name != "Back");
            if (focus != null) EventSystem.current?.SetSelectedGameObject(focus.gameObject);
        }

        private string Coins() => Loc.Format("menu.coins", "n", PlatformGlue.Save != null ? PlatformGlue.Save.Data.Coins : 0);
        private static TextMeshProUGUI Label(Transform parent, string name, string value, float size, Vector2 min, Vector2 max, Vector2 lo, Vector2 hi, Color? color = null, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var text = UiFactory.Text(parent, name, value, size, alignment, min, max, lo, hi, color);
            text.outlineWidth = 0f; text.enableAutoSizing = true; text.fontSizeMin = Mathf.Max(18f, size * 0.7f); text.fontSizeMax = size;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private RectTransform Header(RectTransform root, string key, bool back = true)
        {
            if (back) UiFactory.Button(root, "Back", "‹  " + Loc.Get("common.back"), () => { if (current == "results") GoTo("main"); else Show("main"); }, Top, Top, new Vector2(0f, -74f), new Vector2(164f, -6f), 26f, UiFactory.Raised);
            else
                for (int i = 0; i < 3; i++)
                {
                    var stripe = UiFactory.Panel(root, "Mark" + i, Top, Top, new Vector2(6f + i * 22f, -66f), new Vector2(18f + i * 22f, -12f), UiFactory.Accent, false);
                    stripe.localRotation = Quaternion.Euler(0f, 0f, -15f);
                }
            var title = Label(root, "Title", Loc.Get(key), back ? 46f : 60f, Top, Vector2.one, new Vector2(back ? 190f : 94f, -82f), new Vector2(-300f, 0f));
            title.fontStyle = FontStyles.Bold;
            var wallet = UiFactory.Card(root, "Wallet", Vector2.one, Vector2.one, new Vector2(-276f, -74f), new Vector2(0f, -6f));
            Label(wallet, "Coins", Coins(), 26f, Vector2.zero, Vector2.one, new Vector2(22f, 8f), new Vector2(-22f, -8f), UiFactory.Accent, TextAlignmentOptions.Center);
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
            var body = Header(root, "menu.title", false);
            if (!PlatformGlue.IsReady)
            {
                Label(body, "Loading", Loc.Ready ? Loc.Get("common.loading") : "…", 36f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiFactory.Muted, TextAlignmentOptions.Center); return;
            }
            var content = UiFactory.Scroll(body, "MainScroll", Vector2.zero, Vector2.zero);
            var row = Item(content, "Home", Narrow ? 430f : 580f, false);
            var hero = UiFactory.Card(row, "RaceCard", Vector2.zero, new Vector2(Narrow ? 1f : 0.58f, 1f), Vector2.zero, new Vector2(Narrow ? 0f : -12f, 0f));
            var list = Events();
            var ev = FeaturedEvent(list);
            Label(hero, "Eyebrow", Loc.Get("events.title"), 24f, Top, Vector2.one, new Vector2(36f, -76f), new Vector2(-36f, -28f), UiFactory.Accent);
            var title = Label(hero, "EventName", ev != null ? Loc.Get(ev.nameKey) : Loc.Get("menu.play"), 64f, Top, Vector2.one, new Vector2(36f, -226f), new Vector2(-36f, -98f));
            title.fontStyle = FontStyles.Bold;
            var tiers = Tiers();
            int rivals = ev != null && ev.aiCountOverride >= 0 ? ev.aiCountOverride : tiers.Length > 0 ? tiers[ResolveTierIndex(tiers)].aiCount : 3;
            string detail = ev != null ? Loc.Format("events.laps", "n", ev.laps) + "  /  " + Loc.Format("events.rivals", "n", rivals) : "";
            int maxStars = list.Count * 3;
            int earnedStars = PlatformGlue.Save != null ? PlatformGlue.Save.Data.TotalStars : 0;
            string stars = "<color=#FFCC40>" + Loc.Format("events.stars", "s", earnedStars, "max", maxStars) + "</color>";
            // The narrow hero card has no room for a separate stars row above the Play button.
            if (Narrow) detail += "  /  " + stars;
            Label(hero, "Details", detail, 26f, Top, Vector2.one, new Vector2(38f, -282f), new Vector2(-36f, -230f), UiFactory.Muted);
            if (!Narrow) Label(hero, "StarsCount", stars, 26f, Top, Vector2.one, new Vector2(38f, -326f), new Vector2(-36f, -282f), UiFactory.Accent);
            if (!Narrow)
            {
                Label(hero, "Progress", list.FindAll(e => flow.IsUnlocked(e)).Count.ToString("00") + " <color=#8B9AAD>/ " + list.Count.ToString("00") + "</color>", 64f, Vector2.zero, new Vector2(1f, 0f), new Vector2(38f, 162f), new Vector2(-38f, 248f));
                for (int i = 0; i < list.Count; i++) UiFactory.Panel(hero, "EventProgress" + i, new Vector2(i / (float)list.Count, 0f), new Vector2((i + 1f) / list.Count, 0f), new Vector2(i == 0 ? 38f : 5f, 148f), new Vector2(i == list.Count - 1 ? -38f : -5f, 152f), flow.IsUnlocked(list[i]) ? UiFactory.Accent : UiFactory.Raised, false);
            }
            UiFactory.Button(hero, "Play", Loc.Get(PlatformGlue.Save != null && PlatformGlue.Save.Data.Races == 0 ? "menu.play" : "events.title") + "   ›", PlayFromMain, Vector2.zero, new Vector2(1f, 0f), new Vector2(36f, 32f), new Vector2(-36f, 116f), 34f);
            var links = Narrow ? content : UiFactory.Panel(row, "Links", new Vector2(0.58f, 0f), Vector2.one, new Vector2(12f, 0f), Vector2.zero);
            var keys = new[] { "menu.garage", "menu.leaderboard", "menu.settings" };
            var targets = new[] { "garage", "leaders", "settings" };
            var captions = new[] { "garage.engine.desc", "lb.local", "settings.language" };
            for (int i = 0; i < keys.Length; i++)
            {
                var slot = Narrow ? Item(links, targets[i] + "Slot", 136f, false) : UiFactory.Panel(links, targets[i] + "Slot", new Vector2(0f, 1f - (i + 1f) / 3f), new Vector2(1f, 1f - i / 3f), new Vector2(0f, i == 2 ? 0f : 10f), new Vector2(0f, i == 0 ? 0f : -10f));
                string target = targets[i];
                var button = UiFactory.Button(slot, target, "", () => Show(target), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 32f, UiFactory.Surface);
                Label(button.transform, "Index", (i + 1).ToString("00"), 24f, Vector2.zero, Vector2.one, new Vector2(28f, 20f), new Vector2(-20f, -20f), UiFactory.Accent, TextAlignmentOptions.TopLeft);
                Label(button.transform, "Name", Loc.Get(keys[i]), 32f, Vector2.zero, Vector2.one, new Vector2(82f, 48f), new Vector2(-42f, -22f)).fontStyle = FontStyles.Bold;
                Label(button.transform, "Detail", Loc.Get(captions[i]), 22f, Vector2.zero, Vector2.one, new Vector2(82f, 18f), new Vector2(-42f, -82f), UiFactory.Muted);
                Label(button.transform, "Arrow", "›", 42f, new Vector2(1f, 0f), Vector2.one, new Vector2(-40f, 0f), new Vector2(-14f, 0f), UiFactory.Muted);
            }
            BuildDailyCard(content);
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

        // A brand-new player goes straight into the first race on the resolved (easy) tier; later the
        // big button opens the event list.
        private void PlayFromMain()
        {
            var save = PlatformGlue.Save;
            var list = Events();
            var tiers = Tiers();
            if (save != null && save.Data.Races == 0 && list.Count > 0)
                flow.StartRace(list[0], tiers.Length > 0 ? tiers[ResolveTierIndex(tiers)] : null);
            else Show("events");
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
            var content = UiFactory.Scroll(body, "EventsScroll", Vector2.zero, new Vector2(0f, tiers.Length > 0 ? -90f : 0f));
            var list = Events();
            for (int i = 0; i < list.Count; i++)
            {
                var ev = list[i]; bool unlocked = flow.IsUnlocked(ev);
                var row = Item(content, ev.eventId, Narrow ? 310f : 270f);
                Label(row, "Number", (i + 1).ToString("00"), 52f, Vector2.zero, new Vector2(0f, 1f), new Vector2(24f, 24f), new Vector2(116f, -24f), unlocked ? UiFactory.Accent : UiFactory.Muted, TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
                Label(row, "Name", Loc.Get(ev.nameKey), 34f, Top, Vector2.one, new Vector2(144f, -60f), new Vector2(-236f, -12f)).fontStyle = FontStyles.Bold;
                int rivals = ev.aiCountOverride >= 0 ? ev.aiCountOverride : tiers.Length > 0 ? tiers[tierIndex].aiCount : 3;
                string detail = Loc.Format("events.laps", "n", ev.laps) + "   /   " + Loc.Format("events.rivals", "n", rivals) + TwistSuffix(ev);
                Label(row, "Stats", detail, 23f, Top, Vector2.one, new Vector2(144f, -96f), new Vector2(-236f, -58f), UiFactory.Muted);
                var best = PlatformGlue.Save?.Data.progress.bests.Find(b => b.eventId == ev.eventId);
                int starMask = best != null ? best.stars : 0;
                string s1 = (starMask & 1) != 0 ? "<color=#FFCC40>★</color>" : "<color=#455568>★</color>";
                string s2 = (starMask & 2) != 0 ? "<color=#FFCC40>★</color>" : "<color=#455568>★</color>";
                string s3 = (starMask & 4) != 0 ? "<color=#FFCC40>★</color>" : "<color=#455568>★</color>";
                string starDisplay = s1 + " " + StarGoalText.Description(ev, 0) + "\n" + s2 + " " + StarGoalText.Description(ev, 1) + "\n" + s3 + " " + StarGoalText.Description(ev, 2);
                Label(row, "Stars", starDisplay, 22f, Top, Vector2.one, new Vector2(144f, -222f), new Vector2(-236f, -98f));
                string status = unlocked ? best != null && best.bestTimeMs > 0 ? Loc.Format("events.best", "t", UiFactory.FormatTime(best.bestTimeMs / 1000.0)) : "" : Loc.Get("events.locked");
                Label(row, "Status", status, 22f, Vector2.zero, new Vector2(1f, 0f), new Vector2(144f, 12f), new Vector2(-236f, Narrow ? 64f : 44f), unlocked ? UiFactory.Positive : UiFactory.Muted);
                UiFactory.Button(row, "Start_" + ev.eventId, unlocked ? Loc.Get("menu.play") + " ›" : "—", unlocked ? () => flow.StartRace(ev, tiers.Length > 0 ? tiers[tierIndex] : null) : null, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-216f, -36f), new Vector2(-24f, 36f), 26f, unlocked ? UiFactory.Accent : UiFactory.Raised);
            }
        }

        private void BuildGarage(RectTransform root)
        {
            var content = UiFactory.Scroll(Header(root, "garage.title"), "GarageScroll", Vector2.zero, Vector2.zero);
            var save = PlatformGlue.Save;

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
            string primary = Loc.Get(save?.Data.progress.equippedPrimary == 1 ? "garage.machinegun" : "garage.cannon");
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
            var content = UiFactory.Scroll(Header(root, "settings.title"), "SettingsScroll", Vector2.zero, Vector2.zero);
            var save = PlatformGlue.Save;
            string lang = save != null ? save.Data.settings.lang : "";
            int index = Array.IndexOf(LanguageCycle, lang);
            string language = index <= 0 ? Loc.Get("settings.language.system") + " (" + Loc.Language + ")" : LanguageNames[index];
            var row = SettingRow(content, "Language", "settings.language");
            var value = SettingValue(row);
            UiFactory.Button(value, "LanguagePrevious", "‹", () => PlatformGlue.SetLanguageOverride(LanguageCycle[(Mathf.Max(0, index) + LanguageCycle.Length - 1) % LanguageCycle.Length]), Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(64f, 0f), 36f, UiFactory.Raised);
            Label(value, "LanguageName", language, 25f, Vector2.zero, Vector2.one, new Vector2(74f, 4f), new Vector2(-74f, -4f), Color.white, TextAlignmentOptions.Center);
            UiFactory.Button(value, "LanguageNext", "›", () => PlatformGlue.SetLanguageOverride(LanguageCycle[(Mathf.Max(0, index) + 1) % LanguageCycle.Length]), new Vector2(1f, 0f), Vector2.one, new Vector2(-64f, 0f), Vector2.zero, 36f, UiFactory.Raised);
            ToggleRow(content, "Sound", "settings.sound", save == null || save.Data.settings.sfx > 0f, enabled =>
            {
                if (save == null) return;
                save.Data.settings.sfx = enabled ? 1f : 0f; save.MarkDirty();
                SfxPlayer.Instance.volume = save.Data.settings.sfx * 0.8f; Show("settings");
            });
            // MusicPlayer polls the setting; see MusicDirector.MusicLevel for saves from before this toggle.
            ToggleRow(content, "Music", "settings.music", save != null && MusicDirector.MusicLevel(save.Data.settings) > 0f, enabled =>
            { if (save == null) return; save.Data.settings.music = enabled ? 1f : 0f; save.MarkDirty(); Show("settings"); });
            ToggleRow(content, "AutoThrottle", "settings.auto_throttle", save != null && save.Data.settings.autoThrottle, enabled =>
            { if (save == null) return; save.Data.settings.autoThrottle = enabled; save.MarkDirty(); Show("settings"); });
            if (PlatformGlue.HasQualityChoice && save != null)
            {
                var settings = save.Data.settings;
                var options = SettingValue(SettingRow(content, "Graphics", "settings.graphics"));
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
        }
        private RectTransform SettingRow(Transform content, string name, string key)
        {
            var row = Item(content, name, Narrow ? 170f : 122f);
            Label(row, "Name", Loc.Get(key), 30f, Narrow ? Top : Vector2.zero, Vector2.one, new Vector2(28f, Narrow ? -64f : 16f), new Vector2(Narrow ? -28f : -550f, -16f));
            return row;
        }
        private RectTransform SettingValue(RectTransform row) => UiFactory.Panel(row, "Value", Narrow ? Vector2.zero : new Vector2(1f, 0.5f), Narrow ? new Vector2(1f, 0f) : new Vector2(1f, 0.5f), Narrow ? new Vector2(26f, 22f) : new Vector2(-528f, -34f), Narrow ? new Vector2(-26f, 88f) : new Vector2(-26f, 34f));
        private void ToggleRow(Transform content, string name, string key, bool enabled, Action<bool> change)
        {
            var value = SettingValue(SettingRow(content, name, key));
            for (int i = 0; i < 2; i++)
            {
                bool state = i == 1;
                UiFactory.Button(value, name + (state ? "On" : "Off"), Loc.Get(state ? "settings.on" : "settings.off"), () => change(state), new Vector2(i * 0.5f, 0f), new Vector2((i + 1) * 0.5f, 1f), new Vector2(3f, 0f), new Vector2(-3f, 0f), 25f, state == enabled ? UiFactory.Accent : UiFactory.Raised);
            }
        }

        private void BuildLeaders(RectTransform root)
        {
            var content = UiFactory.Scroll(Header(root, "lb.title"), "LeadersScroll", Vector2.zero, Vector2.zero);
            Section(content, "LocalTitle", Loc.Get("lb.local"));
            int count = 0;
            foreach (var ev in Events())
            {
                var best = PlatformGlue.Save?.Data.progress.bests.Find(b => b.eventId == ev.eventId);
                if (best == null || best.bestTimeMs <= 0) continue;
                BoardRow(content, "Local_" + ev.eventId, (++count).ToString("00"), Loc.Get(ev.nameKey), UiFactory.FormatTime(best.bestTimeMs / 1000.0), true);
            }
            if (count == 0)
            {
                var empty = Item(content, "Empty", 194f);
                Label(empty, "Message", Loc.Get("lb.empty"), 32f, Top, Vector2.one, new Vector2(28f, -78f), new Vector2(-28f, -20f), UiFactory.Muted, TextAlignmentOptions.Center);
                UiFactory.Button(empty, "FirstRace", Loc.Get("menu.play") + " ›", () => Show("events"), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-200f, 26f), new Vector2(200f, 94f), 28f);
            }
            var platform = PlatformGlue.Platform;
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
            else if (platform?.Leaderboards != null) FillBoards(content, platform.Leaderboards);
        }
        private static void Section(Transform content, string name, string title)
        {
            var row = Item(content, name, 52f, false);
            Label(row, "Title", title, 26f, Vector2.zero, Vector2.one, new Vector2(4f, 0f), Vector2.zero, UiFactory.Accent).fontStyle = FontStyles.Bold;
        }
        private static void BoardRow(Transform content, string id, string rank, string name, string time, bool highlight)
        {
            var row = Item(content, id, 90f);
            Label(row, "Rank", rank, 30f, Vector2.zero, new Vector2(0f, 1f), new Vector2(20f, 12f), new Vector2(112f, -12f), highlight ? UiFactory.Accent : UiFactory.Muted, TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
            Label(row, "Name", name, 28f, Vector2.zero, Vector2.one, new Vector2(140f, 12f), new Vector2(-250f, -12f));
            Label(row, "Time", time, 30f, new Vector2(1f, 0f), Vector2.one, new Vector2(-240f, 12f), new Vector2(-26f, -12f), highlight ? UiFactory.Positive : Color.white, TextAlignmentOptions.MidlineRight);
        }
        private async void FillCloudBoard(RectTransform content, ILeaderboards boards)
        {
            Section(content, "CloudTitle", Loc.Get("lb.title"));
            var status = Item(content, "CloudStatus", 90f);
            var label = Label(status, "Status", Loc.Get("common.loading"), 26f, Vector2.zero, Vector2.one, new Vector2(28f, 12f), new Vector2(-28f, -12f), UiFactory.Muted);
            var result = await boards.GetAsync(RaceSetup.BoardName, 10, true, 3);
            if (content == null || !content.gameObject.activeInHierarchy) return;
            if (!result.Ok) { label.text = Loc.Get("common.unavailable"); return; }
            if (result.Value == null || result.Value.Count == 0) { label.text = Loc.Get("lb.empty"); return; }
            status.gameObject.SetActive(false); Destroy(status.gameObject);
            foreach (var entry in result.Value) BoardRow(content, "Cloud" + entry.Rank, entry.Rank.ToString("00"), UiFactory.Escape(entry.Name), UiFactory.FormatTime(entry.Score / 1000.0), false);
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
            if (!flow.LastHadPreviousBest) return Loc.Get("results.pb_first");
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

        private void BuildResults(RectTransform root)
        {
            var results = race.Results;
            if (results == null) { Show("main"); return; }
            var body = Header(root, "results.title");
            bool columns = root.rect.width >= 1280f;
            var overview = columns ? UiFactory.Panel(body, "Overview", Vector2.zero, new Vector2(0.48f, 1f), Vector2.zero, new Vector2(-16f, 0f)) : body;
            var content = UiFactory.Scroll(overview, "ResultsScroll", Vector2.zero, Vector2.zero);
            int reward = flow.LastReward * (flow.LastRewardDoubled ? 2 : 1);
            var summary = Item(content, "Summary", 216f);
            summary.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.22f);
            UiFactory.Panel(summary, "GoldRule", Top, Vector2.one, new Vector2(28f, -4f), new Vector2(-28f, -1f), UiFactory.Accent, false);
            var medal = UiFactory.Panel(summary, "Medal", Vector2.zero, new Vector2(0f, 1f), new Vector2(24f, 24f), new Vector2(194f, -24f), new Color(1f, 0.8f, 0.25f, 0.08f));
            var me = results.Player;
            Label(medal, "Place", me != null ? "#" + me.Position : "—", 86f, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f), PlaceColor(me), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
            var rewardText = Label(summary, "Reward", Loc.Format("results.reward", "n", reward), 40f, Top, Vector2.one, new Vector2(234f, -80f), new Vector2(-28f, -20f), UiFactory.Accent);
            rewardText.fontStyle = FontStyles.Bold;
            if (Time.unscaledTime - rewardShownAt < RewardCountSeconds) rewardLabel = rewardText;   // count up once, not on every rebuild
            Label(summary, "Detail", flow.LastRewardDoubled ? Loc.Get("results.doubled") : flow.CurrentEvent != null ? Loc.Get(flow.CurrentEvent.nameKey) : Loc.Get("results.title"), 26f, Top, Vector2.one, new Vector2(234f, -124f), new Vector2(-28f, -84f), UiFactory.Muted);
            string record = RecordLine(me);
            if (record.Length > 0) Label(summary, "Record", record, 24f, Top, Vector2.one, new Vector2(234f, -170f), new Vector2(-28f, -128f), flow.LastWasPersonalBest ? UiFactory.Positive : UiFactory.Muted);

            var starsCard = Item(content, "StarsCard", 184f);
            Label(starsCard, "StarsTitle", Loc.Get("results.stars"), 22f, Top, Vector2.one, new Vector2(24f, -38f), new Vector2(-24f, -8f), UiFactory.Accent).fontStyle = FontStyles.Bold;
            int currentStars = flow.LastStarsEarned;
            var bestEntry = PlatformGlue.Save?.Data.progress.bests.Find(b => b.eventId == (flow.CurrentEvent != null ? flow.CurrentEvent.eventId : ""));
            int totalEventStars = bestEntry != null ? bestEntry.stars : currentStars;

            for (int s = 0; s < 3; s++)
            {
                int bit = 1 << s;
                bool wonThisRace = (currentStars & bit) != 0;
                bool wonOverall = (totalEventStars & bit) != 0;
                string starIcon = wonThisRace ? "<color=#FFCC40>★</color>" : wonOverall ? "<color=#FFD870>★</color>" : "<color=#455568>★</color>";
                float bottom = 12f + (2 - s) * 42f;
                var starRow = UiFactory.Panel(starsCard, "Star" + s, Vector2.zero, new Vector2(1f, 0f), new Vector2(24f, bottom), new Vector2(-24f, bottom + 40f));
                Label(starRow, "StarIcon", starIcon, 30f, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(34f, 0f));
                Label(starRow, "StarDesc", StarGoalText.Description(flow.CurrentEvent, s), 25f, Vector2.zero, Vector2.one, new Vector2(46f, 0f), Vector2.zero, wonOverall ? Color.white : UiFactory.Muted);
            }
            var actions = Item(content, "Actions", 172f, false);
            var keys = new[] { "results.next", "results.retry", "menu.garage" };
            var next = flow.NextEvent(flow.CurrentEvent);
            bool canAdvance = next != null && next != flow.CurrentEvent;
            Action[] clicks = { canAdvance ? () => flow.StartRace(next, RaceSetup.Tier) : () => flow.RetryRace(), canAdvance ? () => flow.RetryRace() : () => GoTo("events"), () => GoTo("garage") };
            int cheapest = CheapestUpgrade();
            bool canUpgrade = PlatformGlue.Save != null && cheapest < int.MaxValue && PlatformGlue.Save.Data.Coins >= cheapest;
            UiFactory.Button(actions, "ResultAction0", Loc.Get(canAdvance ? keys[0] : "results.retry") + "   ›", clicks[0], Top, Vector2.one, new Vector2(4f, -84f), new Vector2(-4f, 0f), 32f);
            for (int i = 1; i < keys.Length; i++)
                UiFactory.Button(actions, "ResultAction" + i, Loc.Get(i == 1 && !canAdvance ? "events.title" : keys[i]), clicks[i], new Vector2((i - 1) * 0.5f, 0f), new Vector2(i * 0.5f, 0f), new Vector2(4f, 0f), new Vector2(-4f, 72f), 28f, UiFactory.Raised);
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
                rewardButton = UiFactory.Button(ad, "Double", Loc.Get("results.double"), () => flow.DoubleReward(), Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-8f, 0f), 26f, UiFactory.Raised);
                rewardButton.GetComponentInChildren<TextMeshProUGUI>().color = UiFactory.Positive;
            }
            var standings = columns ? UiFactory.Panel(body, "Standings", new Vector2(0.48f, 0f), Vector2.one, new Vector2(16f, 0f), Vector2.zero) : null;
            var board = columns ? UiFactory.Scroll(standings, "StandingsScroll", Vector2.zero, Vector2.zero) : content;
            Section(board, "StandingsTitle", Loc.Get("lb.title"));
            foreach (var entry in results.Entries)
            {
                var row = Item(board, "Result" + entry.Position, 154f);
                if (entry.IsPlayer) row.GetComponent<Image>().color = new Color(0.19f, 0.17f, 0.105f);
                if (entry.IsPlayer) UiFactory.Panel(row, "Accent", Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 14f), new Vector2(4f, -14f), UiFactory.Accent, false);
                Label(row, "Rank", entry.Position.ToString("00"), 42f, Vector2.zero, new Vector2(0f, 1f), new Vector2(22f, 18f), new Vector2(112f, -18f), entry.IsPlayer ? UiFactory.Accent : UiFactory.Muted, TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
                Label(row, "Name", UiFactory.Escape(HudScreen.GetLocalizedRacerName(entry.Name, entry.IsPlayer)), 30f, Top, Vector2.one, new Vector2(120f, -66f), new Vector2(-26f, -14f)).fontStyle = FontStyles.Bold;
                Label(row, "Detail", Loc.Format("results.best_lap", "t", UiFactory.FormatTime(entry.BestLap)) + "   /   " + Loc.Format("results.kills", "k", entry.Kills, "d", entry.Deaths), 22f, Vector2.zero, Vector2.one, new Vector2(120f, 16f), new Vector2(-224f, -76f), UiFactory.Muted);
                Label(row, "Time", entry.Finished ? UiFactory.FormatTime(entry.TotalTime) : Loc.Get("results.dnf"), 30f, new Vector2(1f, 0f), Vector2.one, new Vector2(-214f, 16f), new Vector2(-26f, -76f), entry.IsPlayer ? UiFactory.Positive : Color.white, TextAlignmentOptions.MidlineRight);
            }
        }
    }
}
