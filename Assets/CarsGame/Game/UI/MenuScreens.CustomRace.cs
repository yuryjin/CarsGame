using System;
using System.Collections.Generic;
using RacingGame.Core.Daily;
using UnityEngine;

namespace RacingGame
{
    public sealed partial class MenuScreens
    {
        private int customCourse, customLaps = 1, customRivals = 2, customRule;
        private static readonly RaceTwist[] CustomRules = { RaceTwist.None, RaceTwist.MinesOnly, RaceTwist.OneLife, RaceTwist.HotBomb };
        private static readonly string[] CustomRuleKeys = { "format.custom.standard", "daily.twist.mines_only", "daily.twist.one_life", "events.e18.name" };

        private List<RaceEventDefinition> CustomCourses()
        {
            var options = new List<RaceEventDefinition>();
            var layouts = new HashSet<int>();
            foreach (var course in Events())
            {
                if (course.mode != EventMode.Race || course.format != CircuitFormat.Standard || !flow.IsUnlocked(course)) continue;
                int layout = (course.reversed ? 1 : 0) | (course.abyssTrack ? 2 : 0) | (course.spaceTrack ? 4 : 0)
                    | (course.starRingTrack ? 8 : 0) | (course.wastelandTrack ? 16 : 0);
                if (layouts.Add(layout)) options.Add(course);
            }
            return options;
        }

        private void CustomChoice(RectTransform content, string id, string value, Action<int> move)
        {
            var row = Item(content, id, 86f);
            Label(row, "Value", value, 28f, Vector2.zero, Vector2.one, new Vector2(20f, 2f), new Vector2(-195f, -2f));
            for (int i = -1; i <= 1; i += 2)
            {
                int direction = i;
                UiFactory.Button(row, direction < 0 ? "Previous" : "Next", direction < 0 ? "‹" : "›", () =>
                { move(direction); Show("customRace"); }, Vector2.right, Vector2.one,
                    new Vector2(direction < 0 ? -180f : -89f, 6f), new Vector2(direction < 0 ? -94f : -3f, -6f), 32f);
            }
        }

        private void BuildCustomRace(RectTransform frame)
        {
            var body = Header(frame, "format.custom", showWallet: false);
            var content = UiFactory.Scroll(body, "CustomRules", new Vector2(0f, 100f), Vector2.zero);
            var courses = CustomCourses();
            if (courses.Count == 0) return;
            customCourse = Mathf.Clamp(customCourse, 0, courses.Count - 1);
            var course = courses[customCourse];
            var rule = CustomRules[customRule];
            CustomChoice(content, "Course", Loc.Get("format.custom.course") + " · " + Loc.Get(course.nameKey),
                step => customCourse = (customCourse + courses.Count + step) % courses.Count);
            CustomChoice(content, "Laps", Loc.Format("events.laps", "n", customLaps),
                step => customLaps = (customLaps - 1 + step + 5) % 5 + 1);
            CustomChoice(content, "Rivals", Loc.Format("events.rivals", "n", customRivals),
                step => customRivals = (customRivals + step + 6) % 6);
            CustomChoice(content, "Rule", Loc.Get("format.custom.rule") + " · " + Loc.Get(CustomRuleKeys[customRule]),
                step => customRule = (customRule + step + CustomRules.Length) % CustomRules.Length);
            string error = EventRules.CustomError(course, customLaps, customRivals, rule);
            var note = Item(content, "Note", 94f, false);
            Label(note, "Text", Loc.Get(error ?? "format.custom.exhibition"), 23f, Vector2.zero, Vector2.one,
                new Vector2(16f, 0f), new Vector2(-16f, 0f), error == null ? UiFactory.Muted : UiFactory.Accent);
            var run = UiFactory.Button(body, "StartCustom", Loc.Get("menu.play") + " ›", () =>
            {
                var tiers = Tiers();
                flow.StartCustomRace(course, customLaps, customRivals, rule, tiers.Length > 0 ? tiers[ResolveTierIndex(tiers)] : null);
            }, Vector2.zero, Vector2.right, Vector2.zero, Vector2.up * 82f, 30f);
            run.interactable = error == null;
        }

        private void BuildCustomResults(RectTransform frame, RaceResults result)
        {
            var body = Header(frame, "results.title", showWallet: false);
            var content = UiFactory.Scroll(body, "CustomStandings", new Vector2(0f, 100f), Vector2.zero);
            foreach (var entry in result.Entries)
                BoardRow(content, "Entry" + entry.Position, entry.Position.ToString(), UiFactory.Escape(entry.Name),
                    entry.Finished ? UiFactory.FormatTime(entry.TotalTime) : Loc.Get("results.dnf"), entry.IsPlayer);
            UiFactory.Button(body, "ResultAction0", Loc.Get("results.retry"), () => flow.RetryRace(), Vector2.zero, new Vector2(.5f, 0f),
                Vector2.zero, new Vector2(-5f, 82f), 28f);
            UiFactory.Button(body, "EditCustom", Loc.Get("format.custom"), () => GoTo("customRace"), new Vector2(.5f, 0f), Vector2.right,
                new Vector2(5f, 0f), new Vector2(0f, 82f), 28f);
        }
    }
}
