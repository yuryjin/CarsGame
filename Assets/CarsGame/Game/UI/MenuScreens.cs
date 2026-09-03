using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RacingGame.Core.Economy;
using RacingGame.Core.Localization;
using RacingGame.Core.Platform;

namespace RacingGame
{
    /// <summary>
    /// Runtime-built menu stack: main menu, race select (events × difficulty), garage, settings,
    /// leaderboard, results. One overlay canvas, one visible panel at a time; every label is a
    /// localization key and re-renders on language change.
    /// </summary>
    [RequireComponent(typeof(GameFlow))]
    public sealed class MenuScreens : MonoBehaviour
    {
        private GameFlow flow;
        private RaceManager race;
        private Canvas canvas;
        private readonly Dictionary<string, RectTransform> panels = new Dictionary<string, RectTransform>();
        private string current = "";
        private static TextMeshProUGUI toastText;
        private static float toastUntil;
        private int tierIndex = 1;
        private readonly StringBuilder sb = new StringBuilder(512);
        private static readonly string[] LanguageCycle = { "", "ru", "en", "tr", "zh", "de", "es", "fr", "pt", "uk" };

        public string Current => current;

        private void Awake()
        {
            flow = GetComponent<GameFlow>();
            race = GetComponent<RaceManager>();
            canvas = UiFactory.CreateOverlayCanvas("Menus", 10);
            canvas.transform.SetParent(transform, false);
            toastText = UiFactory.Text(canvas.transform, "Toast", "", 30f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-500f, -220f), new Vector2(500f, -160f), new Color(1f, 0.9f, 0.4f));
        }

        private void OnEnable()
        {
            flow.StateChanged += OnFlowState;
            Loc.LanguageChanged += Rebuild;
            PlatformGlue.Ready += Rebuild;
        }

        private void OnDisable()
        {
            flow.StateChanged -= OnFlowState;
            Loc.LanguageChanged -= Rebuild;
            PlatformGlue.Ready -= Rebuild;
        }

        private bool authInFlight;

        private void Update()
        {
            if (toastText != null && toastText.gameObject.activeSelf && Time.unscaledTime > toastUntil) toastText.gameObject.SetActive(false);
        }

        public static void Toast(string text)
        {
            if (toastText == null) return;
            toastText.text = text; toastText.gameObject.SetActive(true); toastUntil = Time.unscaledTime + 2.5f;
        }

        private string pendingScreen;

        /// <summary>Leave the race and open one menu screen directly (no intermediate main-menu build).</summary>
        public void GoTo(string screen) { pendingScreen = screen; flow.EnterMenu(); }

        private void OnFlowState(FlowState s)
        {
            switch (s)
            {
                case FlowState.Menu: { string target = pendingScreen ?? "main"; pendingScreen = null; Show(target); break; }
                case FlowState.Racing: Show(""); break;
                case FlowState.Results: Show("results"); break;
            }
        }

        private void Rebuild() { string c = current; Show(""); Show(c); }

        /// <summary>Panels are rebuilt from scratch each time they open: cheap, and always current.</summary>
        public void Show(string name)
        {
            foreach (var kv in panels) if (kv.Value != null) Destroy(kv.Value.gameObject);
            panels.Clear();
            current = name;
            if (string.IsNullOrEmpty(name)) return;
            // Light veil only: the menu camera dolly behind the UI is part of the look. Cards sit behind the controls.
            var root = UiFactory.Panel(canvas.transform, "Panel_" + name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.02f, 0.03f, 0.06f, name == "results" ? 0.45f : 0.22f), roundedCorners: false);
            panels[name] = root;
            switch (name)
            {
                case "main": BuildMain(root); break;
                case "events": BuildEvents(root); break;
                case "garage": BuildGarage(root); break;
                case "settings": BuildSettings(root); break;
                case "leaders": BuildLeaders(root); break;
                case "results": BuildResults(root); break;
            }
        }

        private string Coins() => Loc.Format("menu.coins", "n", PlatformGlue.Save != null ? PlatformGlue.Save.Data.Coins : 0);

        private void Title(RectTransform root, string key)
        {
            UiFactory.Text(root, "TitleShadow", Loc.Get(key), 72f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-596f, -154f), new Vector2(604f, -44f), new Color(0f, 0f, 0f, 0.55f));
            UiFactory.Text(root, "Title", Loc.Get(key), 72f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-600f, -150f), new Vector2(600f, -40f), new Color(1f, 0.85f, 0.2f));
        }

        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        private static void CentreCard(RectTransform root, float xHalf, float yMin, float yMax) => UiFactory.Card(root, "Card", Centre, Centre, new Vector2(-xHalf, yMin), new Vector2(xHalf, yMax));

        private void BackButton(RectTransform root, string target = "main")
        {
            UiFactory.Button(root, "Back", Loc.Get("common.back"), () => Show(target), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -110f), new Vector2(230f, -40f), 30f, new Color(0.35f, 0.38f, 0.45f, 0.95f));
        }

        // ---------- main ----------
        private void BuildMain(RectTransform root)
        {
            Title(root, "menu.title");
            if (!PlatformGlue.IsReady)
            {
                // Save/strings still loading: no race can start before the real progression is in.
                UiFactory.Text(root, "Loading", Loc.Ready ? Loc.Get("common.loading") : "…", 40f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300f, -40f), new Vector2(300f, 40f));
                return;
            }
            UiFactory.Text(root, "Coins", Coins(), 32f, TextAlignmentOptions.TopRight, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-500f, -100f), new Vector2(-30f, -40f));
            CentreCard(root, 310f, -320f, 110f);
            float y = 40f;
            foreach (var (key, target) in new[] { ("menu.play", "events"), ("menu.garage", "garage"), ("menu.leaderboard", "leaders"), ("menu.settings", "settings") })
            {
                string t = target;
                UiFactory.Button(root, key, Loc.Get(key), () => Show(t), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-260f, y - 40f), new Vector2(260f, y + 40f), 40f);
                y -= 100f;
            }
            var p = PlatformGlue.Platform;
            UiFactory.Text(root, "Player", p != null ? UiFactory.Escape(p.Player.Name) : "", 24f, TextAlignmentOptions.BottomRight, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-500f, 20f), new Vector2(-30f, 60f), new Color(0.7f, 0.7f, 0.7f));
        }

        // ---------- events ----------
        private void BuildEvents(RectTransform root)
        {
            Title(root, "events.title");
            BackButton(root);
            var tiers = flow.tiers ?? new DifficultyTier[0];
            if (tiers.Length > 0)
            {
                tierIndex = Mathf.Clamp(tierIndex, 0, tiers.Length - 1);
                var tier = tiers[tierIndex];
                UiFactory.Button(root, "Tier", Loc.Get("difficulty." + tier.tierId), () => { tierIndex = (tierIndex + 1) % tiers.Length; Show("events"); },
                    new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-330f, -110f), new Vector2(-30f, -40f), 30f, new Color(0.35f, 0.55f, 0.85f, 0.95f));
            }
            var events = new List<RaceEventDefinition>(flow.events ?? new RaceEventDefinition[0]);
            events.RemoveAll(e => e == null);
            events.Sort((a, b) => a.order.CompareTo(b.order));
            CentreCard(root, 560f, 260f - events.Count * 100f + 30f, 330f);
            float y = 260f;
            var save = PlatformGlue.Save;
            foreach (var ev in events)
            {
                bool unlocked = flow.IsUnlocked(ev);
                var e = ev;
                int rivals = ev.aiCountOverride >= 0 ? ev.aiCountOverride : (tiers.Length > 0 ? tiers[tierIndex].aiCount : 3);
                string best = "";
                var b = save?.Data.progress.bests.Find(x => x.eventId == ev.eventId);
                if (b != null) best = "   " + Loc.Format("events.best", "t", UiFactory.FormatTime(b.bestTimeMs / 1000.0));
                string label = $"{Loc.Get(ev.nameKey)}   <size=60%>{Loc.Format("events.laps", "n", ev.laps)} · {Loc.Format("events.rivals", "n", rivals)}{best}</size>";
                var btn = UiFactory.Button(root, ev.eventId, label, unlocked ? () => flow.StartRace(e, tiers.Length > 0 ? tiers[tierIndex] : null) : null,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520f, y - 42f), new Vector2(520f, y + 42f), 34f, unlocked ? null : new Color(0.3f, 0.3f, 0.32f, 0.9f));
                if (!unlocked) UiFactory.Text(btn.transform, "Lock", Loc.Get("events.locked"), 20f, TextAlignmentOptions.BottomRight, Vector2.zero, Vector2.one, new Vector2(8f, 2f), new Vector2(-12f, -2f), new Color(1f, 0.8f, 0.5f));
                y -= 100f;
            }
        }

        // ---------- garage ----------
        private void BuildGarage(RectTransform root)
        {
            Title(root, "garage.title");
            BackButton(root);
            var save = PlatformGlue.Save;
            UiFactory.Text(root, "Coins", Coins(), 32f, TextAlignmentOptions.TopRight, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-500f, -100f), new Vector2(-30f, -40f));
            CentreCard(root, 560f, -160f, 280f);
            float y = 200f;
            foreach (var kind in new[] { UpgradeKind.Engine, UpgradeKind.Armor, UpgradeKind.Cannon })
            {
                string k = kind.ToString().ToLowerInvariant();
                int lvl = UpgradeCatalog.Level(save?.Data.progress.upgrades, (int)kind);
                int cost = UpgradeCatalog.Cost(lvl);
                UiFactory.Text(root, k, $"<b>{Loc.Get("garage." + k)}</b>   <size=70%>{Loc.Format("garage.level", "lvl", lvl, "max", UpgradeCatalog.MaxLevel)}</size>\n<size=60%>{Loc.Get("garage." + k + ".desc")}</size>", 34f, TextAlignmentOptions.MidlineLeft,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520f, y - 55f), new Vector2(160f, y + 55f));
                var kk = kind;
                bool maxed = lvl >= UpgradeCatalog.MaxLevel;
                UiFactory.Button(root, "Buy_" + k, maxed ? Loc.Get("garage.max") : Loc.Format("garage.buy", "cost", cost), maxed ? null : () => Buy(kk),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(200f, y - 38f), new Vector2(520f, y + 38f), 30f, maxed ? new Color(0.3f, 0.3f, 0.32f, 0.9f) : (Color?)null);
                y -= 140f;
            }
        }

        private void Buy(UpgradeKind kind)
        {
            var save = PlatformGlue.Save;
            if (save == null) return;
            if (save.Data.progress.upgrades == null || save.Data.progress.upgrades.Length < 3) save.Data.progress.upgrades = new int[3];
            if (UpgradeCatalog.TryBuy(save.Data.progress.upgrades, kind, save.Data.Coins, out int cost))
            {
                save.Local.coinsSpent += cost;
                save.MarkDirty();
                SfxPlayer.Instance.PlayUi(SfxSynth.LapDing);
                Show("garage");
            }
            else Toast(Loc.Get("garage.not_enough"));
        }

        // ---------- settings ----------
        private void BuildSettings(RectTransform root)
        {
            Title(root, "settings.title");
            BackButton(root);
            var save = PlatformGlue.Save;
            string lang = save != null ? save.Data.settings.lang : "";
            string langLabel = string.IsNullOrEmpty(lang) ? Loc.Get("settings.language.system") + $" ({Loc.Language})" : lang.ToUpperInvariant();
            CentreCard(root, 560f, -140f, 230f);
            Row(root, 160f, Loc.Get("settings.language"), langLabel, () =>
            {
                int i = System.Array.IndexOf(LanguageCycle, lang); i = (i + 1) % LanguageCycle.Length;
                PlatformGlue.SetLanguageOverride(LanguageCycle[i]);
                Show("settings");
            });
            bool sound = save == null || save.Data.settings.sfx > 0f;
            Row(root, 40f, Loc.Get("settings.sound"), Loc.Get(sound ? "settings.on" : "settings.off"), () =>
            {
                if (save == null) return;
                save.Data.settings.sfx = sound ? 0f : 1f; save.MarkDirty();
                SfxPlayer.Instance.volume = save.Data.settings.sfx * 0.8f;
                AudioListener.volume = save.Data.settings.sfx > 0f ? 1f : 0f;
                Show("settings");
            });
            bool auto = save != null && save.Data.settings.autoThrottle;
            Row(root, -80f, Loc.Get("settings.auto_throttle"), Loc.Get(auto ? "settings.on" : "settings.off"), () =>
            {
                if (save == null) return;
                save.Data.settings.autoThrottle = !auto; save.MarkDirty(); Show("settings");
            });
        }

        private void Row(RectTransform root, float y, string label, string value, System.Action onClick)
        {
            UiFactory.Text(root, "L_" + label, label, 34f, TextAlignmentOptions.MidlineLeft, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520f, y - 40f), new Vector2(100f, y + 40f));
            UiFactory.Button(root, "V_" + label, value, onClick, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(140f, y - 36f), new Vector2(520f, y + 36f), 28f);
        }

        // ---------- leaderboard ----------
        private void BuildLeaders(RectTransform root)
        {
            Title(root, "lb.title");
            BackButton(root);
            var save = PlatformGlue.Save;
            sb.Clear();
            sb.AppendLine("<b>" + Loc.Get("lb.local") + "</b>");
            bool any = false;
            if (save != null && flow.events != null)
                foreach (var ev in flow.events)
                {
                    if (ev == null) continue;
                    var b = save.Data.progress.bests.Find(x => x.eventId == ev.eventId);
                    if (b == null) continue;
                    any = true;
                    sb.AppendLine($"{Loc.Get(ev.nameKey),-16} {UiFactory.FormatTime(b.bestTimeMs / 1000.0)}");
                }
            if (!any) sb.AppendLine(Loc.Get("lb.empty"));
            CentreCard(root, 560f, -290f, 290f);
            var text = UiFactory.Text(root, "Board", sb.ToString(), 30f, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520f, -260f), new Vector2(520f, 260f));

            var p = PlatformGlue.Platform;
            if (p != null && p.HasFeature(PlatformFeature.Auth) && !p.Player.IsAuthorized)
            {
                UiFactory.Text(root, "Hint", Loc.Get("lb.sign_in.hint"), 22f, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-520f, 120f), new Vector2(520f, 180f), new Color(0.8f, 0.8f, 0.8f));
                UiFactory.Button(root, "SignIn", Loc.Get("lb.sign_in"), async () =>
                    {
                        if (authInFlight) return;
                        authInFlight = true;
                        try { var r = await p.RequestAuthAsync(); if (this == null) return; if (r.Ok && r.Value) { await PlatformGlue.SubmitAllBestsAsync(); if (this != null && current == "leaders") Show("leaders"); } }
                        finally { if (this != null) authInFlight = false; }
                    },
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-320f, 40f), new Vector2(320f, 110f), 28f);
            }
            else if (p != null && p.Leaderboards != null) FillCloudBoard(text, p.Leaderboards);
        }

        private async void FillCloudBoard(TextMeshProUGUI text, ILeaderboards boards)
        {
            var r = await boards.GetAsync(RaceSetup.BoardName, 10, true, 3);
            if (!r.Ok || text == null) return;
            var s = new StringBuilder(text.text);
            s.AppendLine().AppendLine("<b>" + Loc.Get("lb.title") + "</b>");
            foreach (var e in r.Value) s.AppendLine($"{e.Rank,3}. {UiFactory.Escape(e.Name),-16} {UiFactory.FormatTime(e.Score / 1000.0)}");
            text.text = s.ToString();
        }

        // ---------- results ----------
        private void BuildResults(RectTransform root)
        {
            var results = race.Results;
            if (results == null) { Show("main"); return; }
            Title(root, "results.title");
            CentreCard(root, 560f, -290f, 280f);
            sb.Clear();
            foreach (var e in results.Entries)
            {
                string me = e.IsPlayer ? " <color=#FFD84A>◄</color>" : "";
                string time = e.Finished ? UiFactory.FormatTime(e.TotalTime) : Loc.Get("results.dnf");
                sb.AppendLine($"{e.Position}. {UiFactory.Escape(e.Name),-8}  {time}   {Loc.Format("results.best_lap", "t", UiFactory.FormatTime(e.BestLap))}   {Loc.Format("results.kills", "k", e.Kills, "d", e.Deaths)}{me}");
            }
            UiFactory.Text(root, "Table", sb.ToString(), 30f, TextAlignmentOptions.TopLeft, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520f, -80f), new Vector2(520f, 250f));
            int reward = flow.LastReward * (flow.LastRewardDoubled ? 2 : 1);
            UiFactory.Text(root, "Reward", Loc.Format("results.reward", "n", reward) + "   " + Coins(), 36f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520f, -150f), new Vector2(520f, -90f), new Color(1f, 0.85f, 0.2f));
            var ads = PlatformGlue.Platform?.Ads;
            float x = -500f;
            if (!flow.LastRewardDoubled && flow.LastRewardFinished && flow.LastReward > 0 && ads != null && ads.IsRewardedAvailable)
            {
                UiFactory.Button(root, "Double", Loc.Get("results.double"), () => flow.DoubleReward(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -260f), new Vector2(x + 330f, -180f), 26f, new Color(0.35f, 0.75f, 0.35f, 0.95f));
                x += 350f;
            }
            UiFactory.Button(root, "Retry", Loc.Get("results.retry"), () => flow.RetryRace(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -260f), new Vector2(x + 220f, -180f), 28f);
            x += 240f;
            UiFactory.Button(root, "Next", Loc.Get("results.next"), () => GoTo("events"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -260f), new Vector2(x + 220f, -180f), 28f);
            x += 240f;
            UiFactory.Button(root, "Garage", Loc.Get("menu.garage"), () => GoTo("garage"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -260f), new Vector2(x + 220f, -180f), 28f, new Color(0.35f, 0.38f, 0.45f, 0.95f));
        }
    }
}
