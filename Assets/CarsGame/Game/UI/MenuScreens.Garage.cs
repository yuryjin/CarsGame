using System;
using TMPro;
using UnityEngine;
using RacingGame.Core.Economy;

namespace RacingGame
{
    public sealed partial class MenuScreens
    {
        private string previewBodyId;

        private void ShowSavingsProgress(Transform content)
        {
            var save = PlatformGlue.Save;
            var progress = save?.Data.progress;
            if (progress == null || string.IsNullOrEmpty(progress.bodyGoal)) return;
            var target = CarBodies.Get(progress.bodyGoal);
            long remaining = System.Math.Max(0, target.Cost - save.Data.Coins);
            string text = remaining > 0 ? Loc.Format("garage.savings_left", "name", Loc.Get(target.NameKey), "n", remaining)
                : Loc.Format("garage.savings_ready", "name", Loc.Get(target.NameKey));
            var row = Item(content, "BodySavings", 94f);
            Label(row, "Progress", text, 26f, Vector2.zero, Vector2.one, new Vector2(16f, 4f), new Vector2(-16f, -4f),
                remaining == 0 ? UiFactory.Positive : UiFactory.Accent);
        }

        private void BuildBodySavings(Transform content)
        {
            var save = PlatformGlue.Save;
            if (save == null) return;
            var p = save.Data.progress;
            ShowSavingsProgress(content);
            var preview = CarBodies.Get(previewBodyId);
            if (preview.Unlock != UnlockKind.Coins || CarBodies.Owns(p.cosmetics, preview.Id)) return;
            bool selected = p.bodyGoal == preview.Id;
            var row = Item(content, "SavingsTarget", 92f, false);
            UiFactory.Button(row, "SaveForBody", Loc.Format(selected ? "garage.savings_clear" : "garage.savings_pick", "name", Loc.Get(preview.NameKey)),
                () => { p.bodyGoal = selected ? "" : preview.Id; save.MarkDirty(); Show("garage"); }, Vector2.zero, Vector2.one,
                new Vector2(6f, 5f), new Vector2(-6f, -5f), 24f, UiFactory.Raised);
        }

        private void BuildMeasurements(Transform content)
        {
            var p = PlatformGlue.Save.Data.progress;
            var start = Item(content, "BodyMeasure", 104f, false);
            UiFactory.Button(start, "StartMeasurement", Loc.Get("measure.start"), () => flow.StartBodyMeasurement(previewBodyId),
                Vector2.zero, Vector2.one, new Vector2(6f, 7f), new Vector2(-6f, -7f), 26f, UiFactory.Raised);
            string conditions = Core.Save.BodyMeasurement.Conditions(p);
            if (p.measurements == null) return;
            foreach (var result in p.measurements)
            {
                if (result == null || result.conditions != conditions) continue;
                var row = Item(content, "Measurement_" + result.body, 134f);
                string metrics = Loc.Format("measure.values", "acc", UiFactory.FormatMilliseconds(result.accelerationMs),
                    "m", (result.brakingCm / 100f).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), "slalom", UiFactory.FormatMilliseconds(result.slalomMs));
                Label(row, "Metrics", Loc.Get(CarBodies.Get(result.body).NameKey) + "\n" + metrics, 24f, Vector2.zero,
                    Vector2.one, new Vector2(16f, 5f), new Vector2(-16f, -5f), UiFactory.Accent);
            }
        }

        private void BuildWeaponDrills(Transform content)
        {
            Section(content, "WeaponDrills", Loc.Get("drill.title"));
            foreach (var lesson in new[] { PracticeLesson.CannonTarget, PracticeLesson.BeamShield, PracticeLesson.PursuitMine })
            {
                var row = Item(content, "Drill" + lesson, 100f, false);
                UiFactory.Button(row, "StartDrill" + lesson, Loc.Get(GameFlow.PracticeTitleKey(lesson)),
                    () => flow.StartWeaponDrill(previewBodyId, lesson), Vector2.zero, Vector2.one,
                    new Vector2(6f, 7f), new Vector2(-6f, -7f), 25f, UiFactory.Raised);
            }
        }

        public void PreviewBody(string id)
        {
            previewBodyId = CarBodies.Get(id).Id;
            bodyTab = CarBodies.Get(previewBodyId).Class;
            Show("garage");
            if (Compact && panel != null)
                foreach (var scroll in panel.GetComponentsInChildren<UnityEngine.UI.ScrollRect>())
                    if (scroll.name == "GarageScroll") scroll.verticalNormalizedPosition = 1f;
        }

        private void StepPreview(int direction)
        {
            int index = Array.FindIndex(CarBodies.All, b => b.Id == previewBodyId);
            PreviewBody(CarBodies.All[(index + direction + CarBodies.All.Length) % CarBodies.All.Length].Id);
        }

        private void BuildShowcase(RectTransform root)
        {
            var progress = PlatformGlue.Save?.Data.progress;
            if (progress == null) return;
            string equipped = CarBodies.Selected(progress.cosmetics, progress.body);
            previewBodyId ??= equipped;
            var def = CarBodies.Get(previewBodyId);
            var baseline = CarBodies.Get(equipped);
            bool selected = def.Id == equipped, owned = CarBodies.Owns(progress.cosmetics, def.Id);
            Label(root, "PreviewName", Loc.Get(def.NameKey), 34f, Top, Vector2.one,
                new Vector2(90f, -70f), new Vector2(-90f, -8f), null, TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
            UiFactory.Button(root, "PreviousBody", "‹", () => StepPreview(-1), Top, Top, new Vector2(12f, -76f), new Vector2(80f, -8f), 32f, UiFactory.Raised);
            UiFactory.Button(root, "NextBody", "›", () => StepPreview(1), Vector2.one, Vector2.one, new Vector2(-80f, -76f), new Vector2(-12f, -8f), 32f, UiFactory.Raised);

            var image = UiFactory.Panel(root, "CarView", Narrow ? new Vector2(0f, 0.23f) : Vector2.zero,
                Narrow ? new Vector2(0.51f, 1f) : Vector2.one, new Vector2(14f, Narrow ? 8f : 364f), new Vector2(-14f, -74f));
            // RawImage and Image cannot share a GameObject. The child also keeps the render target's aspect ratio.
            var view = new GameObject("Model", typeof(RectTransform));
            var rect = (RectTransform)view.transform;
            rect.SetParent(image, false); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var fit = view.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            fit.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 1.5f;
            var preview = view.AddComponent<GaragePreview>();
            preview.Initialize(race.spawner != null ? race.spawner.raceCarPrefab : null, def, progress);
            Label(image, "DragHint", Loc.Get("garage.rotate"), 22f, Vector2.zero, new Vector2(1f, 0f), new Vector2(84f, 4f), new Vector2(-84f, 44f), UiFactory.Muted, TextAlignmentOptions.Center);
            UiFactory.Button(image, "RotateLeft", "‹", () => preview.Rotate(-35f), Vector2.zero, Vector2.zero, new Vector2(8f, 4f), new Vector2(76f, 72f), 25f, UiFactory.Raised);
            UiFactory.Button(image, "RotateRight", "›", () => preview.Rotate(35f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-76f, 4f), new Vector2(-8f, 72f), 25f, UiFactory.Raised);

            var stats = UiFactory.Panel(root, "Comparison", Narrow ? new Vector2(0.51f, 0.23f) : Vector2.zero,
                Narrow ? Vector2.one : new Vector2(1f, 0f), new Vector2(20f, Narrow ? 0f : 104f), new Vector2(-20f, Narrow ? -72f : 350f));
            Label(stats, "ComparedTo", Loc.Format("garage.compare", "name", Loc.Get(baseline.NameKey)), 24f, Top, Vector2.one, new Vector2(0f, -32f), Vector2.zero, UiFactory.Muted);
            var keys = new[] { "body.stat.speed", "body.stat.accel", "body.stat.handling", "body.stat.armor", "body.stat.offroad" };
            var values = new[] { CarBodies.SpeedRating(def), CarBodies.AccelRating(def), CarBodies.HandlingRating(def), CarBodies.ArmorRating(def), CarBodies.OffroadRating(def) };
            var currentValues = new[] { CarBodies.SpeedRating(baseline), CarBodies.AccelRating(baseline), CarBodies.HandlingRating(baseline), CarBodies.ArmorRating(baseline), CarBodies.OffroadRating(baseline) };
            for (int i = 0; i < keys.Length; i++)
            {
                float y = -36f - i * (Narrow ? 26f : 34f);
                Label(stats, "Stat" + i, Loc.Get(keys[i]), 26f, Top, new Vector2(0.53f, 1f), new Vector2(0f, y - 28f), new Vector2(0f, y));
                var bar = UiFactory.Panel(stats, "Rating" + i, new Vector2(0.54f, 1f), new Vector2(1f, 1f), new Vector2(0f, y - 20f), new Vector2(-4f, y - 9f), BarBack, false);
                UiFactory.Panel(bar, "Value", Vector2.zero, new Vector2(values[i], 1f), Vector2.zero, Vector2.zero, UiFactory.Accent, false);
                UiFactory.Panel(bar, "Equipped", new Vector2(currentValues[i], 0f), new Vector2(currentValues[i], 1f), new Vector2(-2f, -3f), new Vector2(2f, 3f), Color.white, false);
                float change = values[i] - currentValues[i];
                string delta = selected || Mathf.Abs(change) < 0.005f ? "=" : change > 0f ? "+" : "−";
                Label(stats, "Delta" + i, delta, 21f, new Vector2(0.46f, 1f), new Vector2(0.53f, 1f), new Vector2(0f, y - 28f), new Vector2(0f, y), change > 0f ? UiFactory.Positive : change < 0f ? UiFactory.Accent : UiFactory.Muted, TextAlignmentOptions.Center);
            }
            string primary = Loc.Get(PrimaryWeaponCatalog.Get((PrimaryWeaponType)progress.equippedPrimary).NameKey);
            Label(stats, "MountedWeapon", primary, 24f, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 35f), UiFactory.Accent);

            Action purchase = null;
            string actionText;
            if (selected) actionText = Loc.Get("garage.equipped");
            else if (owned) { actionText = Loc.Get("garage.equip"); purchase = () => SelectBody(def.Id); }
            else if (def.Unlock == UnlockKind.Rewarded)
            {
                actionText = Loc.Get("garage.unlock_ad");
                if (RewardedOffers.Available && !RewardedOffers.Busy) purchase = () => UnlockBody(def.Id);
            }
            else
            {
                actionText = Loc.Format("garage.buy", "cost", def.Cost);
                if (PlatformGlue.Save.Data.Coins >= def.Cost) purchase = () => BuyBody(def.Id);
            }
            Label(root, "TestDriveNote", Loc.Get("garage.test_note"), 18f, Vector2.zero, new Vector2(1f, 0f), new Vector2(16f, 0f), new Vector2(-16f, 25f), UiFactory.Muted, TextAlignmentOptions.Center);
            UiFactory.Button(root, "ShowcaseEquip", actionText, purchase, Vector2.zero, new Vector2(0.5f, 0f), new Vector2(16f, 30f), new Vector2(-6f, 94f), 28f, selected ? UiFactory.Positive : UiFactory.Raised);
            UiFactory.Button(root, "TestDrive", Loc.Get("garage.test_drive"), () => flow.StartTestDrive(def.Id), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(6f, 30f), new Vector2(-16f, 94f), 28f);
        }
    }
}
