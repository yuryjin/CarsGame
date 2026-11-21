using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RacingGame
{
    public sealed partial class MenuScreens
    {
        private bool rewardDetailsExpanded;

        private void BuildSectorComparison(RectTransform content, int[] deltas)
        {
            var row = Item(content, "SectorComparison", 114f);
            Label(row, "SectorTitle", Loc.Get("results.sectors"), 23f, Top, Vector2.one,
                new Vector2(18f, -42f), new Vector2(-18f, -4f), UiFactory.Muted);
            var path = TrackPath.Instance;
            var culture = CultureInfo.GetCultureInfo(Loc.Language);
            for (int i = 0; i < deltas.Length; i++)
            {
                int place = path != null && path.reversed ? 2 - i : i;
                string name = path != null && path.Fork != null
                    ? Loc.Get(place == 0 ? "results.sector.bridge" : place == 1 ? "results.sector.tower" : "results.sector.pits")
                    : Loc.Format("results.sector.number", "n", i + 1);
                int tenths = Mathf.RoundToInt(deltas[i] / 100f);
                string sign = tenths < 0 ? "−" : tenths > 0 ? "+" : "";
                string seconds = sign + (Mathf.Abs(tenths) / 10m).ToString("0.0", culture);
                var color = tenths < 0 ? UiFactory.Positive : tenths > 0 ? UiFactory.Accent : UiFactory.Muted;
                Label(row, "Sector" + (i + 1), name + "\n" + Loc.Format("results.sector.delta", "t", seconds), 24f,
                    new Vector2(i / 3f, 0f), new Vector2((i + 1) / 3f, 1f), new Vector2(8f, 5f),
                    new Vector2(-8f, -40f), color, TextAlignmentOptions.Center);
            }
        }

        private void BuildRewardDetails(RectTransform content, RaceResultEntry me, bool mission)
        {
            var rows = new List<(string id, string key, int coins)> {
                ("Base", mission ? "results.payout.mission" : me != null && me.Finished
                    ? "results.payout.place" : "results.payout.progress", flow.LastPayout.BaseCoins)
            };
            void Add(string id, string key, int coins) { if (coins > 0) rows.Add((id, key, coins)); }
            Add("Combat", "results.payout.combat", flow.LastPayout.CombatCoins);
            Add("Turrets", "results.payout.turrets", flow.LastTurretReward);
            Add("FirstFinish", "results.payout.first", flow.LastFirstFinishBonus);
            Add("Daily", "results.payout.daily", flow.LastDailyBonus);
            Add("Ad", "results.payout.ad", flow.LastRewardDoubled ? flow.LastReward : 0);
            rows.Add(("Total", "results.payout.total", flow.LastTotalReward));

            var action = Item(content, "RewardDetailsAction", 68f, false);
            var detail = Item(content, "RewardBreakdown", 92f + rows.Count * 56f);
            string tier = Loc.Get("difficulty." + (RaceSetup.Tier != null ? RaceSetup.Tier.tierId : "normal"));
            string multiplier = flow.LastPayout.Multiplier.ToString("0.##", CultureInfo.InvariantCulture);
            var caption = Label(detail, "RewardTier", Loc.Format("results.payout.tier", "tier", tier, "n", multiplier), 24f,
                Top, Vector2.one, new Vector2(24f, -72f), new Vector2(-24f, -12f), UiFactory.Muted);
            caption.fontSizeMin = 22f;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i]; float top = -80f - i * 56f;
                Color color = row.id == "Total" ? UiFactory.Accent : Color.white;
                var name = Label(detail, "Reward" + row.id + "Label", Loc.Get(row.key), 24f, Top, Vector2.one,
                    new Vector2(24f, top - 52f), new Vector2(-150f, top), color);
                name.fontSizeMin = 22f;
                var amount = Label(detail, "Reward" + row.id + "Coins", "+" + row.coins, 26f, Vector2.one, Vector2.one,
                    new Vector2(-140f, top - 52f), new Vector2(-24f, top), color, TextAlignmentOptions.MidlineRight);
                amount.fontSizeMin = 22f;
            }
            Button toggle = null;
            toggle = UiFactory.Button(action, "RewardDetails", Loc.Get(rewardDetailsExpanded ? "results.payout.hide" : "results.payout.show"), () =>
            {
                rewardDetailsExpanded = !rewardDetailsExpanded;
                detail.gameObject.SetActive(rewardDetailsExpanded);
                toggle.GetComponentInChildren<TMP_Text>().text = Loc.Get(rewardDetailsExpanded ? "results.payout.hide" : "results.payout.show");
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            }, Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-4f, 0f), 26f, UiFactory.Raised);
            detail.gameObject.SetActive(rewardDetailsExpanded);
        }
    }
}
