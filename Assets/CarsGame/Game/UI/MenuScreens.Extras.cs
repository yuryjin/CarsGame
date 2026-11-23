using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using RacingGame.Core.Daily;
using RacingGame.Core.Economy;
using RacingGame.Core.Platform;

namespace RacingGame
{
    /// <summary>
    /// Menu parts for the daily race, rule twists, cosmetics and the rewarded "try it for one race" offer.
    /// Kept apart from the core screens; each builder is called from one line in <c>MenuScreens.cs</c>.
    /// </summary>
    public sealed partial class MenuScreens
    {
        private static readonly Color DailyColor = new Color(1f, 0.62f, 0.22f);

        public static string TwistKey(RaceTwist twist)
        {
            switch (twist)
            {
                case RaceTwist.MinesOnly: return "daily.twist.mines_only";
                case RaceTwist.DoubleDamage: return "daily.twist.double_damage";
                case RaceTwist.Night: return "daily.twist.night";
                case RaceTwist.OneLife: return "daily.twist.one_life";
                case RaceTwist.Arsenal: return "daily.twist.arsenal";
                case RaceTwist.Sprint: return "daily.twist.sprint";
                case RaceTwist.HotBomb: return "bomb.rules";
                case RaceTwist.TimeTrial: return "time_trial.rules";
                default: return null;
            }
        }

        /// <summary>"   /   Mines only" for an event with a twist, else "".</summary>
        private static string TwistSuffix(RaceEventDefinition ev)
        {
            string key = ev != null ? TwistKey(ev.twist) : null;
            return key == null ? "" : "   /   <color=#FF9E38>" + Loc.Get(key) + "</color>";
        }

        // ---------- main screen: daily race card ----------
        private TMP_Text dailyCountdown;
        private int dailyCardDay;
        private long dailyCountdownMinute;
        private float nextDailyRefresh;
        private string dailyFooterPrefix, dailyFooterSuffix;

        // Keep a menu left open overnight truthful without rebuilding it every second.
        // Show() preserves the selected control and scroll position when the day changes.
        private bool RefreshDailyCard()
        {
            if (dailyCountdown == null || !dailyCountdown.gameObject.activeInHierarchy || Time.unscaledTime < nextDailyRefresh) return false;
            nextDailyRefresh = Time.unscaledTime + 1f;
            long now = DailyRace.NowUnixMs;
            if (DailyChallenge.DayIndex(now) != dailyCardDay) { Rebuild(); return true; }
            UpdateDailyCountdown(now);
            return false;
        }

        private void UpdateDailyCountdown(long now)
        {
            long minutes = DailyChallenge.MsUntilNextDay(now) / 60000;
            if (minutes == dailyCountdownMinute) return;
            dailyCountdownMinute = minutes;
            dailyCountdown.text = dailyFooterPrefix + Loc.Format("daily.new_in", "h", minutes / 60, "m", minutes % 60) + dailyFooterSuffix;
        }

        private void BuildWeeklyGoals(Transform content)
        {
            var save = PlatformGlue.Save;
            if (save == null || save.Data.Races == 0) return;
            int week = Core.Save.WeeklyGoals.Week(DailyRace.NowUnixMs);
            int mask = Core.Save.WeeklyGoals.Mask(save.Data.progress, week);
            var card = Item(content, "WeeklyGoals", 240f);
            Label(card, "WeeklyTitle", Loc.Get("weekly.title"), 30f, Top, Vector2.one, new Vector2(22f, -60f), new Vector2(-22f, -8f)).fontStyle = FontStyles.Bold;
            for (int i = 0; i < 3; i++)
            {
                bool done = (mask & (1 << i)) != 0;
                Label(card, "Weekly" + i, (done ? "●  " : "–  ") + Loc.Get(Core.Save.WeeklyGoals.Key(week, (Core.Save.WeeklyDiscipline)i)),
                    23f, Top, Vector2.one, new Vector2(22f, -100f - 33f * i), new Vector2(-22f, -64f - 33f * i), done ? UiFactory.Positive : UiFactory.Muted);
            }
            string prize = Loc.Get(Core.Save.WeeklyGoals.Reward(week).Replace(':', '.'));
            Label(card, "WeeklyReward", Loc.Format("weekly.reward", "name", prize), 23f, Vector2.zero, new Vector2(1f, 0f),
                new Vector2(22f, 10f), new Vector2(-22f, 62f), mask == 7 ? UiFactory.Positive : UiFactory.Accent);
        }

        private void BuildDailyCard(Transform content)
        {
            var save = PlatformGlue.Save;
            if (save == null || save.Data.Races == 0) return;   // the first session stays on the guided first race
            var ev = flow.DailyEvent;
            var spec = DailyChallenge.For(DailyRace.DayOf(ev));
            var d = save.Data.progress.daily;
            int today = DailyRace.DayOf(ev);
            var card = Item(content, "DailyCard", Narrow ? 370f : 250f);
            UiFactory.Panel(card, "Accent", Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 18f), new Vector2(5f, -18f), DailyColor, false);
            Label(card, "Eyebrow", Loc.Get("daily.title"), 24f, Top, Vector2.one, new Vector2(30f, -60f), new Vector2(-300f, -16f), DailyColor).fontStyle = FontStyles.Bold;
            string twist = TwistKey(spec.Twist) != null ? Loc.Get(TwistKey(spec.Twist)) : "";
            Label(card, "Twist", twist, 44f, Top, Vector2.one, new Vector2(30f, -122f), new Vector2(-300f, -58f)).fontStyle = FontStyles.Bold;
            Label(card, "Laps", Loc.Format("events.laps", "n", ev.laps) + "  ·  " + Loc.Get("daily.equal"), 24f, Top, Vector2.one, new Vector2(30f, -160f), new Vector2(-300f, -122f), UiFactory.Muted);

            var days = d != null ? d.finishDays : null;
            int streak = DailyChallenge.CurrentStreak(days, today);
            bool bonusTaken = d != null && d.Claimed(today);
            // Today's bonus follows the streak it would extend (yesterday's run + 1).
            string reward = bonusTaken ? Loc.Get("daily.done") : Loc.Format("daily.reward", "n", DailyChallenge.FirstFinishReward(DailyChallenge.StreakEndingAt(days, today - 1) + 1));
            string line = reward + (streak > 0 ? "   /   " + Loc.Format("daily.streak", "n", streak) : "");
            Label(card, "Reward", line, 24f, Top, Vector2.one, new Vector2(30f, -198f), new Vector2(-300f, -160f), bonusTaken ? UiFactory.Muted : UiFactory.Positive);

            var best = save.Data.progress.bests.Find(b => b.eventId == ev.eventId);
            dailyFooterPrefix = best != null && best.bestTimeMs > 0
                ? Loc.Format("events.best", "t", UiFactory.FormatTime(best.bestTimeMs / 1000.0)) + "   /   " : "";
            dailyFooterSuffix = PlatformGlue.Platform?.Sessions != null ? "\n" + Loc.Get("daily.ghosts") : "";
            // Narrow: the Play button takes the bottom strip, the foot text sits between it and the reward line.
            dailyCountdown = Label(card, "Foot", "", 21f, Vector2.zero, new Vector2(1f, 0f), new Vector2(30f, Narrow ? 88f : 12f), new Vector2(Narrow ? -30f : -300f, Narrow ? 164f : 58f), UiFactory.Muted, TextAlignmentOptions.BottomLeft);
            dailyCardDay = today; dailyCountdownMinute = -1;
            nextDailyRefresh = Time.unscaledTime + 1f;
            UpdateDailyCountdown(DailyRace.NowUnixMs);

            var tiers = Tiers();
            if (Narrow)
                UiFactory.Button(card, "PlayDaily", Loc.Get("menu.play") + " ›", () => flow.StartRace(ev, tiers.Length > 0 ? tiers[ResolveTierIndex(tiers)] : null), Vector2.zero, new Vector2(1f, 0f), new Vector2(30f, 14f), new Vector2(-30f, 80f), 30f);
            else
                UiFactory.Button(card, "PlayDaily", Loc.Get("menu.play") + " ›", () => flow.StartRace(ev, tiers.Length > 0 ? tiers[ResolveTierIndex(tiers)] : null), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-270f, -40f), new Vector2(-30f, 40f), 30f);
        }

        // ---------- garage: try an upgrade for one race ----------
        private void TrialRow(Transform content, UpgradeKind kind)
        {
            var save = PlatformGlue.Save;
            if (save == null) return;
            var levels = save.Data.progress.upgrades;
            bool active = RewardedOffers.TrialUpgrade == (int)kind;
            if (!active && (!RewardedOffers.CanTry(levels, kind) || !RewardedOffers.Available)) return;
            var row = Item(content, "Trial_" + kind, 64f, false);
            if (active)
            {
                Label(row, "Active", Loc.Get("garage.trial_active"), 22f, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f), UiFactory.Positive, TextAlignmentOptions.MidlineRight);
                return;
            }
            UiFactory.Button(row, "Try_" + kind, Loc.Get(RewardedOffers.Busy ? "common.loading" : "garage.try"), RewardedOffers.Busy ? null : async () =>
            {
                bool ok = await RewardedOffers.ShowAsync(() => RewardedOffers.GrantTrial(kind), () => flow.State == FlowState.Menu);
                if (this != null && current == "garage") Show("garage");
                if (ok) SfxPlayer.Instance.PlayUi(SfxSynth.LapDing);
            }, new Vector2(1f, 0f), Vector2.one, new Vector2(-300f, 4f), new Vector2(-30f, -4f), 22f, UiFactory.Positive);
        }

        // ---------- garage: paints and liveries ----------
        private void BuildCosmetics(Transform content)
        {
            var save = PlatformGlue.Save;
            if (save == null) return;
            var progress = save.Data.progress;
            progress.cosmetics ??= new List<string>();
            Section(content, "PaintsTitle", Loc.Get("garage.paints"));
            string paint = PlayerCosmetics.SelectedPaint(progress);
            CosmeticGrid(content, "Paint", Cosmetics.Paints.Length, i =>
            {
                var def = Cosmetics.Paints[i];
                return new CosmeticCell(def.Id, def.NameKey, def.Unlock, def.Cost, new Color(def.R, def.G, def.B),
                    Cosmetics.OwnsPaint(progress.cosmetics, def.Id), def.Id == paint);
            }, false);

            Section(content, "LiveriesTitle", Loc.Get("garage.liveries"));
            string livery = PlayerCosmetics.SelectedLivery(progress);
            CosmeticGrid(content, "Livery", Cosmetics.Liveries.Length, i =>
            {
                var def = Cosmetics.Liveries[i];
                return new CosmeticCell(def.Id, def.NameKey, def.Unlock, def.Cost, PlayerCosmetics.StripeColor(progress),
                    Cosmetics.OwnsLivery(progress.cosmetics, def.Id), def.Id == livery);
            }, true);

            if (livery != Cosmetics.DefaultLivery)
            {
                // Stripe colour: automatic contrast, or any owned paint.
                var options = new List<string> { "" };
                foreach (var p in Cosmetics.Paints) if (Cosmetics.OwnsPaint(progress.cosmetics, p.Id)) options.Add(p.Id);
                int at = Mathf.Max(0, options.IndexOf(progress.stripe ?? ""));
                var value = SettingValue(content, "StripeColour", "garage.stripe_color");
                void Step(int dir)
                {
                    var live = PlatformGlue.Save;   // a cloud merge may have replaced the data since the screen was built
                    if (live == null) return;
                    live.Data.progress.stripe = options[(at + dir + options.Count) % options.Count];
                    live.MarkDirty(); Show("garage");
                }
                UiFactory.Button(value, "StripePrevious", "‹", () => Step(-1), Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(64f, 0f), 36f, UiFactory.Raised);
                string name = options[at] == "" ? Loc.Get("garage.stripe_auto") : Loc.Get(Cosmetics.Paint(options[at]).NameKey);
                Label(value, "StripeName", name, 25f, Vector2.zero, Vector2.one, new Vector2(74f, 4f), new Vector2(-74f, -4f), PlayerCosmetics.StripeColor(progress), TextAlignmentOptions.Center);
                UiFactory.Button(value, "StripeNext", "›", () => Step(1), new Vector2(1f, 0f), Vector2.one, new Vector2(-64f, 0f), Vector2.zero, 36f, UiFactory.Raised);
            }
        }

        private readonly struct CosmeticCell
        {
            public readonly string Id, NameKey; public readonly UnlockKind Unlock; public readonly int Cost; public readonly Color Swatch; public readonly bool Owned, Selected;
            public CosmeticCell(string id, string nameKey, UnlockKind unlock, int cost, Color swatch, bool owned, bool selected)
            { Id = id; NameKey = nameKey; Unlock = unlock; Cost = cost; Swatch = swatch; Owned = owned; Selected = selected; }
        }

        private void CosmeticGrid(Transform content, string prefix, int count, Func<int, CosmeticCell> cellAt, bool livery)
        {
            int perRow = Narrow ? 2 : 3;
            for (int start = 0; start < count; start += perRow)
            {
                var row = Item(content, prefix + "Row" + start, Narrow ? 196f : 178f, false);
                for (int k = 0; k < perRow && start + k < count; k++)
                {
                    var cell = cellAt(start + k);
                    var box = UiFactory.Card(row, prefix + "_" + cell.Id, new Vector2(k / (float)perRow, 0f), new Vector2((k + 1f) / perRow, 1f), new Vector2(k == 0 ? 0f : 6f, 0f), new Vector2(k == perRow - 1 ? 0f : -6f, 0f));
                    var swatch = UiFactory.Panel(box, "Swatch", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -78f), new Vector2(76f, -22f), livery ? UiFactory.Raised : cell.Swatch);
                    if (livery) LiveryIcon(swatch, cell.Id, cell.Swatch);
                    Label(box, "Name", Loc.Get(cell.NameKey), 26f, Top, Vector2.one, new Vector2(90f, -78f), new Vector2(-16f, -22f)).fontStyle = FontStyles.Bold;
                    string id = cell.Id;
                    if (cell.Selected)
                        UiFactory.Button(box, "Use_" + id, Loc.Get("garage.equipped"), null, Vector2.zero, new Vector2(1f, 0f), new Vector2(16f, 16f), new Vector2(-16f, 76f), 23f, UiFactory.Positive);
                    else if (cell.Owned)
                        UiFactory.Button(box, "Use_" + id, Loc.Get("garage.equip"), () => SelectCosmetic(livery, id), Vector2.zero, new Vector2(1f, 0f), new Vector2(16f, 16f), new Vector2(-16f, 76f), 23f, UiFactory.Accent);
                    else if (cell.Unlock == UnlockKind.Rewarded)
                        UiFactory.Button(box, "Ad_" + id, Loc.Get(RewardedOffers.Busy ? "common.loading" : "garage.unlock_ad"), RewardedOffers.Available && !RewardedOffers.Busy ? () => UnlockCosmetic(livery, id) : (Action)null, Vector2.zero, new Vector2(1f, 0f), new Vector2(16f, 16f), new Vector2(-16f, 76f), 22f, RewardedOffers.Available ? UiFactory.Positive : UiFactory.Raised);
                    else
                    {
                        bool affordable = PlatformGlue.Save != null && PlatformGlue.Save.Data.Coins >= cell.Cost;
                        UiFactory.Button(box, "Buy_" + id, Loc.Format("garage.buy", "cost", cell.Cost), affordable ? () => BuyCosmetic(livery, id) : (Action)null, Vector2.zero, new Vector2(1f, 0f), new Vector2(16f, 16f), new Vector2(-16f, 76f), 23f, affordable ? UiFactory.Accent : UiFactory.Raised);
                    }
                }
            }
        }

        // A tiny drawing of the stripe layout on a car seen from above (nose up).
        private static void LiveryIcon(RectTransform swatch, string id, Color stripe)
        {
            var body = UiFactory.Panel(swatch, "Body", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-13f, -24f), new Vector2(13f, 24f), new Color(0.75f, 0.12f, 0.14f));
            switch (Cosmetics.Livery(id).Pattern)
            {
                case LiveryPattern.CenterStripe: UiFactory.Panel(body, "S", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-3f, 0f), new Vector2(3f, 0f), stripe, false); break;
                case LiveryPattern.TwinStripes:
                    UiFactory.Panel(body, "S1", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-8f, 0f), new Vector2(-4f, 0f), stripe, false);
                    UiFactory.Panel(body, "S2", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(4f, 0f), new Vector2(8f, 0f), stripe, false); break;
                case LiveryPattern.SideBand:
                    UiFactory.Panel(body, "S1", new Vector2(0f, 0.1f), new Vector2(0f, 0.9f), Vector2.zero, new Vector2(4f, 0f), stripe, false);
                    UiFactory.Panel(body, "S2", new Vector2(1f, 0.1f), new Vector2(1f, 0.9f), new Vector2(-4f, 0f), Vector2.zero, stripe, false); break;
                case LiveryPattern.Hood: UiFactory.Panel(body, "S", new Vector2(0.2f, 0.62f), new Vector2(0.8f, 1f), Vector2.zero, Vector2.zero, stripe, false); break;
                case LiveryPattern.Tail: UiFactory.Panel(body, "S", new Vector2(0f, 0f), new Vector2(1f, 0.22f), Vector2.zero, Vector2.zero, stripe, false); break;
            }
        }

        private void SelectCosmetic(bool livery, string id)
        {
            var save = PlatformGlue.Save;
            if (save == null) return;
            if (livery) save.Data.progress.livery = id; else save.Data.progress.paint = id;
            save.MarkDirty(); SfxPlayer.Instance.PlayUi(SfxSynth.Beep); Show("garage");
        }

        public bool BuyCosmetic(bool livery, string id)
        {
            var save = PlatformGlue.Save;
            if (save == null) return false;
            save.Data.progress.cosmetics ??= new List<string>();
            if (!Cosmetics.TryBuy(save.Data.progress.cosmetics, livery, id, save.Data.Coins, out int cost)) { Toast(Loc.Get("garage.not_enough")); return false; }
            save.Local.coinsSpent += cost;
            if (livery) save.Data.progress.livery = id; else save.Data.progress.paint = id;   // wear what you just bought
            save.MarkDirty(); SfxPlayer.Instance.PlayUi(SfxSynth.LapDing); Show("garage");
            return true;
        }

        private async void UnlockCosmetic(bool livery, string id)
        {
            var save = PlatformGlue.Save;
            if (save == null) return;
            bool ok = await RewardedOffers.ShowAsync(() =>
            {
                save.Data.progress.cosmetics ??= new List<string>();
                if (Cosmetics.GrantRewarded(save.Data.progress.cosmetics, livery, id))
                {
                    if (livery) save.Data.progress.livery = id; else save.Data.progress.paint = id;
                    save.MarkDirty();
                }
            });
            if (this == null) return;
            if (ok) SfxPlayer.Instance.PlayUi(SfxSynth.LapDing);
            if (current == "garage") Show("garage");
        }

        // ---------- results: daily bonus and the trial nudge ----------
        private void ResultsExtras(Transform content)
        {
            int day = DailyRace.DayOf(flow.CurrentEvent);
            bool claimed = day >= 0 && PlatformGlue.Save?.Data.progress.daily != null && PlatformGlue.Save.Data.progress.daily.Claimed(day);
            if (flow.LastDailyBonus > 0 || claimed)
            {
                var daily = Item(content, "DailyResult", 52f, false);
                string text = flow.LastDailyBonus > 0 ? Loc.Format("daily.reward", "n", flow.LastDailyBonus) : Loc.Get("daily.done");
                if (flow.LastDailyStreak > 0) text += "   /   " + Loc.Format("daily.streak", "n", flow.LastDailyStreak);
                Label(daily, "Text", text, 24f, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f), flow.LastDailyBonus > 0 ? UiFactory.Positive : UiFactory.Muted, TextAlignmentOptions.MidlineRight);
            }
            int trial = RewardedOffers.LastRaceTrial;
            var save = PlatformGlue.Save;
            if (trial >= 0 && save != null)
            {
                var kind = (UpgradeKind)trial;
                int level = UpgradeCatalog.Level(save.Data.progress.upgrades, trial);
                if (level < UpgradeCatalog.MaxLevel)
                {
                    int cost = UpgradeCatalog.Cost(level);
                    var row = Item(content, "TrialNudge", 76f, false);
                    string name = Loc.Get("garage." + kind.ToString().ToLowerInvariant());
                    Label(row, "Text", Loc.Format("results.trial", "name", name, "lvl", level + 1, "cost", cost), 22f, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-270f, 0f), UiFactory.Accent, TextAlignmentOptions.MidlineLeft);
                    bool affordable = save.Data.Coins >= cost;
                    UiFactory.Button(row, "BuyTrial", Loc.Format("garage.buy", "cost", cost), affordable ? () => { if (Buy(kind)) GoTo("garage"); } : (Action)null, new Vector2(1f, 0f), Vector2.one, new Vector2(-256f, 6f), new Vector2(-4f, -6f), 24f, affordable ? UiFactory.Accent : UiFactory.Raised);
                }
            }
        }

        // ---------- leaders: today's daily board ----------
        private string selectedBoard;
        private bool boardSelectorOpen;

        private RaceEventDefinition BuildBoardSelector(RectTransform content)
        {
            var list = Events();
            if (list.Count == 0) return null;
            string id = selectedBoard ?? RaceSetup.Event?.eventId;
            var selected = list.Find(e => e.eventId == id) ?? list[0];
            var action = Item(content, "BoardSelector", 84f, false);
            UiFactory.Button(action, "ChooseBoardEvent", Loc.Get("events.title") + " · " + Loc.Get(selected.nameKey),
                () => { boardSelectorOpen = !boardSelectorOpen; Show("leaders"); },
                Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f), 28f, UiFactory.Raised);
            if (boardSelectorOpen)
                foreach (var ev in list)
                {
                    var option = Item(content, "BoardOption_" + ev.eventId, 72f, false);
                    UiFactory.Button(option, "BoardEvent_" + ev.eventId, Loc.Get(ev.nameKey),
                        () => { selectedBoard = ev.eventId; boardSelectorOpen = false; Show("leaders"); },
                        Vector2.zero, Vector2.one, new Vector2(24f, 3f), new Vector2(-24f, -3f), 25f,
                        ev == selected ? UiFactory.Accent : UiFactory.Raised);
                }
            return selected;
        }

        /// <summary>Today's board first, then the event board, one after the other so their rows do not interleave.</summary>
        private async void FillBoards(RectTransform content, ILeaderboards boards, RaceEventDefinition selected)
        {
            await FillDailyBoard(content, boards);
            if (content != null && content.gameObject.activeInHierarchy) FillCloudBoard(content, boards, selected);
        }

        private async System.Threading.Tasks.Task FillDailyBoard(RectTransform content, ILeaderboards boards)
        {
            Section(content, "DailyTitle", Loc.Get("daily.board"));
            var status = Item(content, "DailyStatus", 90f);
            var label = Label(status, "Status", Loc.Get("common.loading"), 26f, Vector2.zero, Vector2.one, new Vector2(28f, 12f), new Vector2(-28f, -12f), UiFactory.Muted);
            int today = DailyRace.Today;
            var result = await boards.GetAsync(DailyRace.BoardName, 20, true, 3);
            if (content == null || !content.gameObject.activeInHierarchy) return;
            if (!result.Ok) { label.text = Loc.Get("common.unavailable"); return; }
            int shown = 0;
            if (result.Value != null)
                foreach (var entry in result.Value)
                {
                    DailyChallenge.DecodeScore(entry.Score, out int day, out long ms);
                    if (day != today) continue;   // older days stay on the board until beaten; only today's runs count
                    shown++;
                    BoardRow(content, "Daily" + entry.Rank, entry.Rank.ToString("00"), UiFactory.Escape(entry.Name), UiFactory.FormatTime(ms / 1000.0), entry.PlayerId == PlatformGlue.Platform?.Player.Id);
                }
            if (shown == 0) { label.text = Loc.Get("lb.empty"); return; }
            status.gameObject.SetActive(false); Destroy(status.gameObject);
        }
    }
}
