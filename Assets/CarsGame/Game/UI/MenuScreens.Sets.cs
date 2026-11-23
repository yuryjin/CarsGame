using System;
using RacingGame.Core.Economy;
using RacingGame.Core.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RacingGame
{
    public sealed partial class MenuScreens
    {
        private void BuildGarageSets(Transform content)
        {
            var save = PlatformGlue.Save;
            if (save == null) return;
            Section(content, "GarageSets", Loc.Get("sets.title"));
            for (int i = 0; i < GarageSet.Slots; i++)
            {
                int slot = i;
                var set = GarageSet.At(save.Data.progress.garageSets, slot);
                var row = Item(content, "Set" + slot, 166f);
                var field = UiFactory.Panel(row, "SetName" + slot, new Vector2(0f, .51f), Vector2.one, new Vector2(12f, 5f), new Vector2(-12f, -10f), UiFactory.Raised);
                var input = LineInput(field, GarageSet.NameLimit, set?.name ?? "");
                input.placeholder = UiFactory.Text(input.textViewport, "Hint", Loc.Get("sets.name"), 24f, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiFactory.Muted);
                UiFactory.Button(row, "WriteSet" + slot, Loc.Get("sets.save"), () =>
                {
                    if (!SaveGarageSet(slot, input.text)) { Toast(Loc.Get("sets.name")); return; }
                    Show("garage");
                }, Vector2.zero, new Vector2(.5f, .50f), new Vector2(12f, 10f), new Vector2(-5f, -4f), 24f, UiFactory.Raised);
                UiFactory.Button(row, "UseSet" + slot, Loc.Get("garage.equip"), set == null ? null : (Action)(() =>
                {
                    RestoreGarageSet(set);
                    Show("garage");
                }), new Vector2(.5f, 0f), new Vector2(1f, .50f), new Vector2(5f, 10f), new Vector2(-12f, -4f), 24f);
            }
        }

        private bool SaveGarageSet(int index, string name)
        {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > GarageSet.NameLimit || Array.Exists(name.ToCharArray(), char.IsControl)) return false;
            var save = PlatformGlue.Save; var p = save.Data.progress;
            p.garageSets = GarageSet.Merge(p.garageSets, null);
            var previous = p.garageSets[index];
            p.garageSets[index] = new GarageSet
            {
                name = name, body = CarBodies.Selected(p.cosmetics, p.body), paint = PlayerCosmetics.SelectedPaint(p),
                livery = PlayerCosmetics.SelectedLivery(p), stripe = p.stripe,
                primary = p.equippedPrimary, reserve = p.reservePrimary, secondary = p.equippedSecondary, fullArsenal = p.fullArsenal,
                writtenAt = Math.Max(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), (previous?.writtenAt ?? 0) + 1)
            };
            save.MarkDirty(); return true;
        }

        private void RestoreGarageSet(GarageSet set)
        {
            var save = PlatformGlue.Save; var p = save.Data.progress;
            p.body = CarBodies.Selected(p.cosmetics, set.body); previewBodyId = p.body; bodyTab = CarBodies.Get(p.body).Class;
            p.paint = Cosmetics.OwnsPaint(p.cosmetics, set.paint) ? set.paint : Cosmetics.DefaultPaint;
            p.livery = Cosmetics.OwnsLivery(p.cosmetics, set.livery) ? set.livery : Cosmetics.DefaultLivery;
            p.stripe = Cosmetics.OwnsPaint(p.cosmetics, set.stripe) ? set.stripe : "";
            p.equippedPrimary = set.primary; p.reservePrimary = set.reserve; p.fullArsenal = set.fullArsenal;
            PlayerProgression.NormalizeLoadout(p);
            p.equippedSecondary = (int)PlayerProgression.OwnedSecondary(p.weapons, set.secondary);
            save.MarkDirty();
        }
    }
}
