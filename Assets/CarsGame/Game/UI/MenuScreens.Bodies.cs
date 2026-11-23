using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using RacingGame.Core.Economy;

namespace RacingGame
{
    /// <summary>
    /// Garage: the body picker. Four class tabs (buggy, sport, armored, tank), the driving style of the class and its
    /// five bodies with rating bars and a buy / equip button. Called from one line at the top of <c>BuildGarage</c>.
    /// </summary>
    public sealed partial class MenuScreens
    {
        private static readonly Color BarBack = new Color(0.2f, 0.25f, 0.33f);
        /// <summary>Open class tab; null = the class of the equipped body.</summary>
        private BodyClass? bodyTab;

        private void BuildBodies(Transform content)
        {
            var save = PlatformGlue.Save;
            if (save == null) return;
            var progress = save.Data.progress;
            progress.cosmetics ??= new List<string>();
            string equipped = CarBodies.Selected(progress.cosmetics, progress.body);
            var tab = bodyTab ?? CarBodies.Get(equipped).Class;

            Section(content, "BodiesTitle", Loc.Get("garage.bodies"));
            var tabs = Item(content, "BodyTabs", 84f, false);
            for (int i = 0; i < CarBodies.Classes.Length; i++)
            {
                var cls = CarBodies.Classes[i];
                UiFactory.Button(tabs, "Tab_" + cls, Loc.Get(CarBodies.ClassKey(cls)), cls == tab ? null : (Action)(() => { bodyTab = cls; PreviewBody(CarBodies.OfClass(cls)[0].Id); }),
                    new Vector2(i / 4f, 0f), new Vector2((i + 1f) / 4f, 1f), new Vector2(i == 0 ? 0f : 4f, 6f), new Vector2(i == 3 ? 0f : -4f, -6f), 25f, cls == tab ? UiFactory.Accent : UiFactory.Raised);
            }
            var style = Item(content, "BodyStyle", Narrow ? 110f : 84f, false);
            Label(style, "Text", Loc.Get(CarBodies.ClassKey(tab) + ".desc"), 23f, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f), UiFactory.Muted);

            long mastery = Core.Save.BodyMastery.Count(progress, tab);
            var badge = Item(content, "ClassMastery", 92f);
            string skill = Loc.Get("mastery." + tab.ToString().ToLowerInvariant());
            string status = mastery >= Core.Save.BodyMastery.BadgeAt ? Loc.Get("mastery.badge") : mastery + "/" + Core.Save.BodyMastery.BadgeAt;
            Label(badge, "Achievement", skill + " · " + status, 25f, Vector2.zero, Vector2.one,
                new Vector2(16f, 6f), new Vector2(-16f, -6f), mastery >= Core.Save.BodyMastery.BadgeAt ? UiFactory.Positive : UiFactory.Muted);

            foreach (var def in CarBodies.OfClass(tab)) BodyRow(content, def, progress.cosmetics, def.Id == equipped, save.Data.Coins);
        }

        private void BodyRow(Transform content, BodyDef def, IList<string> owned, bool equipped, long coins)
        {
            var row = Item(content, "Body_" + def.Id, Narrow ? 330f : 250f);
            float right = Narrow ? -30f : -290f;
            var icon = UiFactory.Panel(row, "InspectBody_" + def.Id, Top, Top, new Vector2(24f, -150f), new Vector2(144f, -24f), UiFactory.Raised);
            BodyIcon(icon, def.Class);
            var inspect = icon.gameObject.AddComponent<UnityEngine.UI.Button>();
            inspect.targetGraphic = icon.GetComponent<UnityEngine.UI.Image>();
            inspect.targetGraphic.raycastTarget = true;
            string inspectId = def.Id;
            inspect.onClick.AddListener(() => PreviewBody(inspectId));
            icon.gameObject.AddComponent<UiButtonFx>();
            Label(icon, "Inspect", Loc.Get("garage.inspect"), 18f, Vector2.zero, Vector2.one, new Vector2(2f, 0f), new Vector2(-2f, -96f), UiFactory.Accent, TextAlignmentOptions.Bottom);
            Label(row, "Name", Loc.Get(def.NameKey), 32f, Top, Vector2.one, new Vector2(164f, -64f), new Vector2(right, -16f)).fontStyle = FontStyles.Bold;
            float kg = 1250f * def.Stats.Mass, hp = 100f * def.Stats.Health;
            Label(row, "Mass", Loc.Format("body.mass", "kg", Mathf.RoundToInt(kg / 10f) * 10) + "   /   " + Loc.Format("body.hp", "hp", Mathf.RoundToInt(hp)),
                21f, Top, Vector2.one, new Vector2(164f, -98f), new Vector2(right, -62f), UiFactory.Muted);

            var bars = new[]
            {
                ("body.stat.speed", CarBodies.SpeedRating(def)), ("body.stat.accel", CarBodies.AccelRating(def)),
                ("body.stat.handling", CarBodies.HandlingRating(def)), ("body.stat.armor", CarBodies.ArmorRating(def)),
                ("body.stat.offroad", CarBodies.OffroadRating(def)),
            };
            for (int i = 0; i < bars.Length; i++)
            {
                float y = -110f - 26f * i;
                Label(row, "StatName" + i, Loc.Get(bars[i].Item1), 19f, Top, Top, new Vector2(164f, y - 24f), new Vector2(344f, y), UiFactory.Muted);
                var back = UiFactory.Panel(row, "StatBar" + i, Top, Vector2.one, new Vector2(350f, y - 18f), new Vector2(right, y - 6f), BarBack, false);
                UiFactory.Panel(back, "Fill", Vector2.zero, new Vector2(Mathf.Clamp01(bars[i].Item2), 1f), Vector2.zero, Vector2.zero, UiFactory.Accent, false);
            }

            // Wide: the button sits at the right; narrow: along the bottom under the bars.
            Vector2 min = Narrow ? Vector2.zero : new Vector2(1f, 0.5f), max = Narrow ? new Vector2(1f, 0f) : new Vector2(1f, 0.5f);
            Vector2 lo = Narrow ? new Vector2(24f, 16f) : new Vector2(-266f, -36f), hi = Narrow ? new Vector2(-24f, 80f) : new Vector2(-24f, 36f);
            string id = def.Id;
            if (equipped)
                UiFactory.Button(row, "Use_" + id, Loc.Get("garage.equipped"), null, min, max, lo, hi, 25f, UiFactory.Positive);
            else if (CarBodies.Owns(owned, id))
                UiFactory.Button(row, "Use_" + id, Loc.Get("garage.equip"), () => SelectBody(id), min, max, lo, hi, 25f, UiFactory.Accent);
            else if (def.Unlock == UnlockKind.Rewarded)
                UiFactory.Button(row, "Ad_" + id, Loc.Get(RewardedOffers.Busy ? "common.loading" : "garage.unlock_ad"), RewardedOffers.Available && !RewardedOffers.Busy ? () => UnlockBody(id) : (Action)null,
                    min, max, lo, hi, 23f, RewardedOffers.Available ? UiFactory.Positive : UiFactory.Raised);
            else
            {
                bool affordable = coins >= def.Cost;
                UiFactory.Button(row, "Buy_" + id, Loc.Format("garage.buy", "cost", def.Cost), affordable ? () => BuyBody(id) : (Action)null, min, max, lo, hi, 25f, affordable ? UiFactory.Accent : UiFactory.Raised);
            }
        }

        // A top-down sketch of the class (nose up), drawn with panels like the livery icons.
        private static void BodyIcon(RectTransform icon, BodyClass cls)
        {
            var paint = new Color(0.85f, 0.2f, 0.2f); var dark = new Color(0.1f, 0.11f, 0.13f); var c = new Vector2(0.5f, 0.5f);
            void P(string n, float x0, float y0, float x1, float y1, Color col) => UiFactory.Panel(icon, n, c, c, new Vector2(x0, y0), new Vector2(x1, y1), col, false);
            switch (cls)
            {
                case BodyClass.Buggy:
                    foreach (var (x, y) in new[] { (-30f, 22f), (18f, 22f), (-30f, -38f), (18f, -38f) }) P("Wheel", x, y, x + 12f, y + 20f, dark);
                    P("Tub", -14f, -40f, 14f, 38f, paint); P("CageL", -16f, -18f, -12f, 14f, dark); P("CageR", 12f, -18f, 16f, 14f, dark); P("Roof", -16f, -8f, 16f, 6f, dark);
                    break;
                case BodyClass.Armored:
                    foreach (var (x, y) in new[] { (-30f, 20f), (22f, 20f), (-30f, -36f), (22f, -36f) }) P("Wheel", x, y, x + 8f, y + 18f, dark);
                    P("Hull", -24f, -46f, 24f, 46f, paint); P("Cab", -18f, -30f, 18f, 12f, new Color(0.65f, 0.15f, 0.15f)); P("Ram", -22f, 46f, 22f, 51f, dark);
                    break;
                case BodyClass.Tank:
                    P("TrackL", -32f, -46f, -18f, 46f, dark); P("TrackR", 18f, -46f, 32f, 46f, dark); P("Hull", -18f, -44f, 18f, 42f, paint);
                    P("Turret", -14f, -18f, 14f, 10f, new Color(0.65f, 0.15f, 0.15f)); P("Barrel", -3f, 10f, 3f, 54f, dark);
                    break;
                default:
                    foreach (var (x, y) in new[] { (-26f, 22f), (18f, 22f), (-26f, -36f), (18f, -36f) }) P("Wheel", x, y, x + 8f, y + 16f, dark);
                    P("Body", -20f, -46f, 20f, 44f, paint); P("Glass", -14f, -14f, 14f, 16f, new Color(0.15f, 0.2f, 0.28f)); P("Wing", -22f, -48f, 22f, -42f, dark);
                    break;
            }
        }

        public bool SelectBody(string id)
        {
            var save = PlatformGlue.Save;
            if (save == null || !CarBodies.Owns(save.Data.progress.cosmetics, id)) return false;
            save.Data.progress.body = id;
            previewBodyId = id;
            save.MarkDirty(); SfxPlayer.Instance.PlayUi(SfxSynth.Beep); Show("garage");
            return true;
        }

        public bool BuyBody(string id)
        {
            var save = PlatformGlue.Save;
            if (save == null) return false;
            save.Data.progress.cosmetics ??= new List<string>();
            if (!CarBodies.TryBuy(save.Data.progress.cosmetics, id, save.Data.Coins, out int cost)) { Toast(Loc.Get("garage.not_enough")); return false; }
            save.Local.coinsSpent += cost;
            save.Data.progress.body = id;   // drive what you just bought
            previewBodyId = id;
            save.MarkDirty(); SfxPlayer.Instance.PlayUi(SfxSynth.LapDing); Show("garage");
            return true;
        }

        private async void UnlockBody(string id)
        {
            var save = PlatformGlue.Save;
            if (save == null) return;
            bool ok = await RewardedOffers.ShowAsync(() =>
            {
                save.Data.progress.cosmetics ??= new List<string>();
                if (CarBodies.GrantRewarded(save.Data.progress.cosmetics, id)) { save.Data.progress.body = id; previewBodyId = id; save.MarkDirty(); }
            });
            if (this == null) return;
            if (ok) SfxPlayer.Instance.PlayUi(SfxSynth.LapDing);
            if (current == "garage") Show("garage");
        }
    }
}
