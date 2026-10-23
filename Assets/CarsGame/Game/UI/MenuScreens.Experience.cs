using System;
using TMPro;
using UnityEngine;
using RacingGame.Core.Economy;

namespace RacingGame
{
    public sealed partial class MenuScreens
    {
        private int loadoutSlot;
        private ArsenalGroup arsenalGroup;

        private void BuildLoadout(Transform content)
        {
            var save = PlatformGlue.Save;
            if (save == null) return;
            var p = save.Data.progress;
            Section(content, "LoadoutTitle", Loc.Get("loadout.title"));
            var mode = Item(content, "ArsenalMode", 92f, false);
            UiFactory.Button(mode, "ToggleArsenal", Loc.Get(p.fullArsenal ? "loadout.full" : "loadout.compact"),
                () => { p.fullArsenal = !p.fullArsenal; save.MarkDirty(); Show("garage"); },
                Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f), 24f, UiFactory.Raised);
            for (int i = 0; i < 2; i++)
            {
                int slot = i;
                var type = (PrimaryWeaponType)(i == 0 ? p.equippedPrimary : p.reservePrimary);
                var row = Item(content, "LoadoutSlot" + i, 100f);
                UiFactory.Button(row, "ChooseSlot" + i, (i + 1) + "  ·  " + Loc.Get(PrimaryWeaponCatalog.Get(type).NameKey) + "  ›",
                    () => { loadoutSlot = slot; arsenalGroup = ArsenalGroup.All; Show("arsenal"); }, Vector2.zero, Vector2.one,
                    new Vector2(12f, 12f), new Vector2(-12f, -12f), 27f, UiFactory.Raised);
            }
            var hint = Item(content, "SwapHelp", 90f, false);
            Label(hint, "Text", Loc.Get("loadout.help"), 22f, Vector2.zero, Vector2.one, new Vector2(16f, 4f), new Vector2(-16f, -4f), UiFactory.Muted);
        }

        private void BuildArsenal(RectTransform root)
        {
            var save = PlatformGlue.Save;
            var content = UiFactory.Scroll(Header(root, "loadout.title"), "ArsenalScroll", Vector2.zero, Vector2.zero);
            if (save == null) return;
            var p = save.Data.progress;
            var selectedType = (PrimaryWeaponType)(loadoutSlot == 0 ? p.equippedPrimary : p.reservePrimary);
            Section(content, "SelectedSlot", (loadoutSlot + 1) + " · " + Loc.Get(PrimaryWeaponCatalog.Get(selectedType).NameKey));
            var actions = Item(content, "ArsenalActions", 90f, false);
            UiFactory.Button(actions, "BackToGarage", Loc.Get("menu.garage"), () => Show("garage"), Vector2.zero, new Vector2(0.5f, 1f),
                new Vector2(4f, 4f), new Vector2(-4f, -4f), 25f, UiFactory.Raised);
            UiFactory.Button(actions, "TryLoadout", Loc.Get("garage.test_drive"),
                () => flow.StartTestDrive(CarBodies.Selected(p.cosmetics, p.body)), new Vector2(0.5f, 0f), Vector2.one,
                new Vector2(4f, 4f), new Vector2(-4f, -4f), 25f);
            var filters = Item(content, "ArsenalFilters", 92f, false);
            for (int i = 0; i < 5; i++)
            {
                var group = (ArsenalGroup)i;
                UiFactory.Button(filters, "Filter_" + group, Loc.Get("arsenal." + group.ToString().ToLowerInvariant()),
                    () => { arsenalGroup = group; Show("arsenal"); }, new Vector2(i / 5f, 0f), new Vector2((i + 1) / 5f, 1f),
                    new Vector2(4f, 8f), new Vector2(-4f, -8f), 23f, arsenalGroup == group ? UiFactory.Accent : UiFactory.Raised);
            }
            for (int i = 0; i < PrimaryWeaponCatalog.Count; i++)
            {
                var type = (PrimaryWeaponType)i;
                var profile = PrimaryWeaponCatalog.Get(type);
                if (arsenalGroup != ArsenalGroup.All && WeaponGuide.Group(profile) != arsenalGroup) continue;
                bool owned = type != PrimaryWeaponType.MachineGun || WeaponCatalog.Level(p.weapons, (int)WeaponKind.MachineGun) > 0;
                bool selected = (loadoutSlot == 0 ? p.equippedPrimary : p.reservePrimary) == i;
                var row = Item(content, "Arsenal_" + i, 300f);
                Label(row, "Name", Loc.Get(profile.NameKey), 30f, Top, Vector2.one,
                    new Vector2(24f, -56f), new Vector2(-240f, -12f)).fontStyle = FontStyles.Bold;
                Label(row, "Role", Loc.Get(WeaponGuide.RoleKey(profile)), 22f, Top, Vector2.one,
                    new Vector2(24f, -130f), new Vector2(-240f, -62f), UiFactory.Muted);
                Label(row, "Stats", WeaponGuide.Stats(profile),
                    22f, Vector2.zero, Vector2.one, new Vector2(24f, 12f), new Vector2(-240f, -142f), UiFactory.Accent);
                UiFactory.Button(row, "EquipSlot_" + i, Loc.Get(selected ? "garage.equipped" : owned ? "garage.equip" : "garage.locked"),
                    owned && !selected ? () => SelectLoadout(type, loadoutSlot) : (Action)null,
                    new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-220f, -36f), new Vector2(-20f, 36f), 24f,
                    selected ? UiFactory.Positive : UiFactory.Accent);
            }
        }

        public bool SelectLoadout(PrimaryWeaponType type, int slot)
        {
            var save = PlatformGlue.Save;
            if (save == null || slot < 0 || slot > 1 || !PrimaryWeaponCatalog.IsValid(type)) return false;
            var p = save.Data.progress;
            if (type == PrimaryWeaponType.MachineGun && WeaponCatalog.Level(p.weapons, (int)WeaponKind.MachineGun) == 0) return false;
            // Selecting the other slot's weapon swaps them, keeping two distinct choices.
            int old = slot == 0 ? p.equippedPrimary : p.reservePrimary;
            if (slot == 0) { if (p.reservePrimary == (int)type) p.reservePrimary = old; p.equippedPrimary = (int)type; }
            else { if (p.equippedPrimary == (int)type) p.equippedPrimary = old; p.reservePrimary = (int)type; }
            save.MarkDirty(); Show("arsenal");
            return true;
        }

        private RaceEventDefinition CupNext(RaceCup cup)
        {
            var list = Events();
            var p = PlatformGlue.Save?.Data.progress;
            RaceEventDefinition missingStar = null, unlockStep = null;
            foreach (var id in cup.Events)
            {
                var ev = list.Find(e => e.eventId == id);
                if (ev == null) continue;
                if (!flow.IsUnlocked(ev))
                {
                    var prerequisite = list.Find(e => e.eventId == ev.unlockAfterEventId);
                    if (unlockStep == null && prerequisite != null && flow.IsUnlocked(prerequisite)) unlockStep = prerequisite;
                    continue;
                }
                var best = p?.bests.Find(b => b.eventId == id);
                if (best == null || best.bestTimeMs <= 0) return ev;
                if (missingStar == null && (best.stars & 7) != 7) missingStar = ev;
            }
            // A finish outside the podium records a time but does not open the next event.
            // Help open the rest of the cup before sending the player back to farm old stars.
            return unlockStep ?? missingStar;
        }

        private void BuildCups(Transform content)
        {
            Section(content, "CupsTitle", Loc.Get("cup.title"));
            foreach (var cup in RaceCups.All)
            {
                var p = PlatformGlue.Save?.Data.progress;
                var row = Item(content, "Cup_" + cup.Id, 200f);
                Label(row, "Name", Loc.Get(cup.NameKey), 30f, Top, Vector2.one, new Vector2(24f, -55f), new Vector2(-250f, -12f), UiFactory.Accent);
                Label(row, "Progress", CupDescription(cup), 23f, Vector2.zero, Vector2.one, new Vector2(24f, 18f), new Vector2(-250f, -66f), UiFactory.Muted);
                var next = CupNext(cup);
                UiFactory.Button(row, "CupStart_" + cup.Id, Loc.Get(cup.Complete(p) ? "cup.complete" : "common.continue"),
                    next != null && !cup.Complete(p) ? () => flow.StartRace(next, SelectedTier()) : (Action)null,
                    new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-230f, -36f), new Vector2(-24f, 36f), 24f, UiFactory.Raised);
            }
        }

        private DifficultyTier SelectedTier() { var tiers = Tiers(); return tiers.Length > 0 ? tiers[ResolveTierIndex(tiers)] : null; }
        private string CupDescription(RaceCup cup) => Loc.Format("cup.progress", "stars", Mathf.Min(RaceCup.TargetStars, cup.Stars(PlatformGlue.Save?.Data.progress)),
            "n", cup.Finishes(PlatformGlue.Save?.Data.progress), "reward", Loc.Get(cup.RewardNameKey));

        private RaceEventDefinition RecommendedNext()
        {
            var cup = RaceCups.ForEvent(flow.CurrentEvent != null ? flow.CurrentEvent.eventId : null);
            if (cup != null && !cup.Complete(PlatformGlue.Save?.Data.progress))
            {
                var next = CupNext(cup);
                if (next != null) return next;
            }
            return flow.NextEvent(flow.CurrentEvent);
        }

        private void BuildNextGoal(Transform content, RaceEventDefinition next)
        {
            if (next == null) return;
            var best = PlatformGlue.Save?.Data.progress.bests.Find(b => b.eventId == next.eventId);
            int mask = best?.stars ?? 0;
            int goal = (mask & 2) == 0 ? 1 : (mask & 1) == 0 ? 0 : 2;
            bool unlocksNext = best != null && flow.events != null && Array.Exists(flow.events, e => e != null
                && e.unlockAfterEventId == next.eventId && !flow.IsUnlocked(e));
            string target = unlocksNext ? Loc.Format("goals.place", "n", 3)
                : mask == 7 ? Loc.Get("experience.record") : StarGoalText.Description(next, goal);
            var row = Item(content, "NextGoal", 180f);
            Label(row, "Title", Loc.Get("experience.next"), 22f, Top, Vector2.one, new Vector2(24f, -42f), new Vector2(-24f, -8f), UiFactory.Accent);
            Label(row, "Goal", Loc.Get(next.nameKey) + "\n" + target, 27f, Vector2.zero, Vector2.one, new Vector2(24f, 18f), new Vector2(-24f, -52f));
            var cup = RaceCups.ForEvent(next.eventId);
            if (cup != null)
            {
                var reward = Item(content, "CupReward", 160f, false);
                Label(reward, "Text", Loc.Get(cup.NameKey) + "\n" + CupDescription(cup), 22f, Vector2.zero, Vector2.one,
                    new Vector2(12f, 8f), new Vector2(-12f, -8f), UiFactory.Positive);
            }
        }
    }
}
