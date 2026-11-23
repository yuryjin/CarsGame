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

        private void BuildPractice(RectTransform root)
        {
            var content = UiFactory.Scroll(Header(root, "practice.title"), "PracticeScroll", Vector2.zero, Vector2.zero);
            var note = Item(content, "PracticeNote", 100f, false);
            Label(note, "Text", Loc.Get("practice.note"), 26f, Vector2.zero, Vector2.one, new Vector2(20f, 8f), new Vector2(-20f, -8f), UiFactory.Muted);
            foreach (var lesson in new[] { PracticeLesson.Drift, PracticeLesson.Shortcut, PracticeLesson.WallRide, PracticeLesson.Aim, PracticeLesson.Launch })
            {
                var selected = lesson;
                var row = Item(content, "Lesson" + lesson, 270f);
                Label(row, "Name", Loc.Get(GameFlow.PracticeTitleKey(lesson)), 32f, Top, Vector2.one,
                    new Vector2(24f, -66f), new Vector2(-24f, -12f)).fontStyle = FontStyles.Bold;
                string key = lesson == PracticeLesson.Drift ? (PlatformGlue.IsMobileDevice ? "lesson.drift.touch" : "lesson.drift.keys")
                    : lesson == PracticeLesson.Shortcut ? "lesson.shortcut" : lesson == PracticeLesson.WallRide ? "lesson.wall" : GameFlow.PracticeTitleKey(lesson) + ".help";
                Label(row, "Instruction", Loc.Get(key), 26f, Top, Vector2.one,
                    new Vector2(24f, -174f), new Vector2(-24f, -70f));
                UiFactory.Button(row, "Practice" + lesson, Loc.Get("practice.start"), () => flow.StartPractice(selected),
                    Vector2.zero, new Vector2(1f, 0f), new Vector2(24f, 16f), new Vector2(-24f, 84f), 27f);
            }
            var yesterday = Item(content, "YesterdayPractice", 260f);
            Label(yesterday, "Name", Loc.Get("practice.yesterday"), 32f, Top, Vector2.one,
                new Vector2(24f, -66f), new Vector2(-24f, -12f)).fontStyle = FontStyles.Bold;
            string info = Loc.Get("practice.yesterday.note");
            if (flow.LastYesterdayDay == DailyRace.Today - 1 && flow.LastYesterdayTimeMs > 0)
                info += "\n" + Loc.Format("practice.last_time", "t", UiFactory.FormatMilliseconds(flow.LastYesterdayTimeMs));
            Label(yesterday, "Instruction", info, 25f, Top, Vector2.one, new Vector2(24f, -164f), new Vector2(-24f, -68f));
            UiFactory.Button(yesterday, "PracticeYesterday", Loc.Get("practice.start"), flow.StartYesterdayPractice,
                Vector2.zero, new Vector2(1f, 0f), new Vector2(24f, 16f), new Vector2(-24f, 84f), 27f);
        }

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
                    () => { loadoutSlot = slot; arsenalGroup = p.weaponPins?.Exists(pin => pin != null && pin.slot == slot && pin.pinned) == true ? ArsenalGroup.Pinned : ArsenalGroup.All; Show("arsenal"); }, Vector2.zero, Vector2.one,
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
            BuildWeaponComparison(content, p);
            var filters = Item(content, "ArsenalFilters", 92f, false);
            int groups = (int)ArsenalGroup.Pinned + 1;
            for (int i = 0; i < groups; i++)
            {
                var group = (ArsenalGroup)i;
                UiFactory.Button(filters, "Filter_" + group, Loc.Get("arsenal." + group.ToString().ToLowerInvariant()),
                    () => { arsenalGroup = group; Show("arsenal"); }, new Vector2(i / (float)groups, 0f), new Vector2((i + 1f) / groups, 1f),
                    new Vector2(4f, 8f), new Vector2(-4f, -8f), 23f, arsenalGroup == group ? UiFactory.Accent : UiFactory.Raised);
            }
            for (int i = 0; i < PrimaryWeaponCatalog.Count; i++)
            {
                var type = (PrimaryWeaponType)i;
                var profile = PrimaryWeaponCatalog.Get(type);
                bool pinned = Core.Save.WeaponPin.Has(p.weaponPins, loadoutSlot, profile.NameKey);
                if (arsenalGroup == ArsenalGroup.Pinned ? !pinned : arsenalGroup != ArsenalGroup.All && WeaponGuide.Group(profile) != arsenalGroup) continue;
                bool owned = type != PrimaryWeaponType.MachineGun || WeaponCatalog.Level(p.weapons, (int)WeaponKind.MachineGun) > 0;
                bool selected = (loadoutSlot == 0 ? p.equippedPrimary : p.reservePrimary) == i;
                var row = Item(content, "Arsenal_" + i, Narrow ? 374f : 334f);
                Label(row, "Name", Loc.Get(profile.NameKey), 30f, Top, Vector2.one,
                    new Vector2(24f, -56f), new Vector2(-240f, -12f)).fontStyle = FontStyles.Bold;
                Label(row, "Role", Loc.Get(WeaponGuide.RoleKey(profile)), 22f, Top, Vector2.one,
                    new Vector2(24f, -130f), new Vector2(-240f, -62f), UiFactory.Muted);
                Label(row, "Stats", WeaponGuide.Stats(type, p),
                    22f, Vector2.zero, Vector2.one, new Vector2(24f, 12f), new Vector2(-240f, -142f), UiFactory.Accent);
                UiFactory.Button(row, "EquipSlot_" + i, Loc.Get(selected ? "garage.equipped" : owned ? "garage.equip" : "garage.locked"),
                    owned && !selected ? () => SelectLoadout(type, loadoutSlot) : (Action)null,
                    new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-220f, 50f), new Vector2(-20f, 122f), 24f,
                    selected ? UiFactory.Positive : UiFactory.Accent);
                UiFactory.Button(row, "CompareWeapon" + i, Loc.Get("arsenal.compare"), () =>
                {
                    if (choosingFirstWeapon) comparisonFirst = type; else comparisonSecond = type;
                    Show("arsenal"); var scroll = GameObject.Find("ArsenalScroll")?.GetComponent<UnityEngine.UI.ScrollRect>();
                    if (scroll != null) scroll.verticalNormalizedPosition = 1f;
                }, new Vector2(1f, .5f), new Vector2(1f, .5f), new Vector2(-220f, -28f), new Vector2(-20f, 44f), 24f, UiFactory.Raised);
                UiFactory.Button(row, "PinWeapon" + i, Loc.Get(pinned ? "arsenal.unpin" : "arsenal.pin"), () =>
                {
                    Core.Save.WeaponPin.Toggle(p, loadoutSlot, profile.NameKey);
                    save.MarkDirty(); Show("arsenal");
                }, new Vector2(1f, .5f), new Vector2(1f, .5f), new Vector2(-220f, -106f), new Vector2(-20f, -34f), 23f, pinned ? UiFactory.Positive : UiFactory.Raised);
            }
        }

        private PrimaryWeaponType? comparisonFirst, comparisonSecond;
        private bool choosingFirstWeapon;

        private void BuildWeaponComparison(Transform content, Core.Save.ProgressData p)
        {
            comparisonFirst ??= (PrimaryWeaponType)p.equippedPrimary;
            comparisonSecond ??= (PrimaryWeaponType)p.reservePrimary;
            Section(content, "CompareTitle", Loc.Get("arsenal.compare"));
            var row = Item(content, "Comparison", Narrow ? 480f : 340f);
            for (int i = 0; i < 2; i++)
            {
                bool left = i == 0;
                var type = left ? comparisonFirst.Value : comparisonSecond.Value;
                var side = UiFactory.Panel(row, "Comparison" + i, new Vector2(i * .5f, 0f), new Vector2((i + 1) * .5f, 1f),
                    new Vector2(9f, 7f), new Vector2(-9f, -7f));
                UiFactory.Button(side, "ChooseCompare" + i, Loc.Get(PrimaryWeaponCatalog.Get(type).NameKey),
                    () => { choosingFirstWeapon = left; Show("arsenal"); }, new Vector2(0f, 1f), Vector2.one,
                    new Vector2(0f, -74f), Vector2.zero, 24f, choosingFirstWeapon == left ? UiFactory.Accent : UiFactory.Raised);
                Label(side, "Stats", WeaponGuide.Stats(type, p), 22f, Vector2.zero, Vector2.one,
                    new Vector2(8f, 0f), new Vector2(-8f, -80f));
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

        private static string RivalRecord(string id)
        {
            var score = Core.Save.RivalScore.Total(PlatformGlue.Save.Data.progress, id);
            return Loc.Format("duel.record", "wins", score.wins, "losses", score.losses);
        }

        private void BuildRivalPreview(Transform content, RaceEventDefinition ev)
        {
            var rival = ev != null ? ev.featuredRival : null;
            if (rival == null) return;
            var row = Item(content, "Rival_" + ev.eventId, 145f);
            string key = ev.eventId == "e1" ? "duel.first" : ev.eventId == "e15" ? "duel.final" : "duel.rival";
            Label(row, "Intro", Loc.Format(key, "name", UiFactory.Escape(rival.rivalName)) + "\n" + RivalRecord(rival.rivalId), 26f, Vector2.zero, Vector2.one,
                new Vector2(18f, 6f), new Vector2(-18f, -6f), UiFactory.Accent);
        }

        private void ShowFeaturedDuel(Transform content, RaceResults results)
        {
            var profile = flow.CurrentEvent != null ? flow.CurrentEvent.featuredRival : null;
            if (profile == null || string.IsNullOrEmpty(profile.rivalId)) return;
            var rival = results.Entries.Find(entry => entry.RivalId == profile.rivalId);
            if (rival == null || results.Player == null) return;
            var row = Item(content, "FeaturedDuel", 178f);
            string outcome = !rival.Finished || !results.Player.Finished ? "duel.unclassified"
                : results.Player.Position < rival.Position ? "duel.won" : "duel.lost";
            string text = Loc.Format(outcome, "name", UiFactory.Escape(profile.rivalName));
            if (rival.Finished && results.Player.Finished)
                text += "\n" + Loc.Format("duel.places", "you", results.Player.Position, "name", UiFactory.Escape(profile.rivalName), "rival", rival.Position);
            text += "\n" + RivalRecord(profile.rivalId);
            Label(row, "Outcome", text, 27f, Vector2.zero, Vector2.one, new Vector2(18f, 6f), new Vector2(-18f, -6f));
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
            // A locked cup stage takes priority over replaying one for missing stars.
            return unlockStep ?? missingStar;
        }

        private void BuildChapter(Transform content)
        {
            var p = PlatformGlue.Save.Data.progress;
            int step = RaceCups.ChapterStep(p);
            bool complete = step == RaceCups.ChapterEvents.Length;
            var ev = Array.Find(flow.events, x => x != null && x.eventId == RaceCups.ChapterEvents[Math.Min(step, 2)]);
            if (ev == null) return;
            var row = Item(content, "CanyonChapter", Narrow ? 300f : 253f);
            Label(row, "Title", Loc.Get("chapter.title") + " · " + step + "/3", 30f, Top, Vector2.one,
                new Vector2(20f, -55f), new Vector2(-20f, -8f), UiFactory.Accent);
            string reward = Loc.Get(RaceCups.ChapterReward.Replace(':', '.'));
            string stages = string.Join(" › ", Array.ConvertAll(RaceCups.ChapterEvents, id =>
            {
                var member = Array.Find(flow.events, e => e != null && e.eventId == id);
                return member == null ? id : Loc.Get(member.nameKey);
            }));
            Label(row, "Route", stages + "\n" + Loc.Format("chapter.route", "reward", reward), 23f,
                Vector2.zero, Vector2.one, new Vector2(20f, 84f), new Vector2(-20f, -60f));
            UiFactory.Button(row, "ChapterStart", complete ? Loc.Get("cup.complete") : flow.IsUnlocked(ev) ? Loc.Get(ev.nameKey) + "  ›" : Loc.Get("chapter.entry"),
                !complete && flow.IsUnlocked(ev) ? () => flow.StartRace(ev, SelectedTier()) : (Action)null,
                Vector2.zero, new Vector2(1f, 0f), new Vector2(15f, 8f), new Vector2(-15f, 78f), 25f, complete ? UiFactory.Positive : UiFactory.Raised);
        }

        private void BuildCups(Transform content)
        {
            BuildChapter(content);
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
            var current = flow.CurrentEvent;
            var nextCareer = flow.NextEvent(current);
            var progress = PlatformGlue.Save?.Data.progress;
            if (current != null && nextCareer != null && nextCareer.unlockAfterEventId == current.eventId
                && progress?.bests.Exists(b => b != null && b.eventId == nextCareer.eventId && b.bestTimeMs > 0) != true)
                return nextCareer;
            var cup = RaceCups.ForEvent(current != null ? current.eventId : null);
            if (cup != null && !cup.Complete(progress))
            {
                var next = CupNext(cup);
                if (next != null) return next;
            }
            return nextCareer;
        }

        private void BuildNextGoal(Transform content, RaceEventDefinition next)
        {
            if (next == null) return;
            var best = PlatformGlue.Save?.Data.progress.bests.Find(b => b.eventId == next.eventId);
            int mask = best?.stars ?? 0;
            int goal = (mask & 2) == 0 ? 1 : (mask & 1) == 0 ? 0 : 2;
            bool unlocksNext = best != null && flow.events != null && Array.Exists(flow.events, e => e != null
                && e.unlockAfterEventId == next.eventId && !flow.IsUnlocked(e));
            string target = DailyRace.IsDaily(next) ? Loc.Get("experience.record") : unlocksNext ? Loc.Format("goals.place", "n", 3)
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
