using System;
using RacingGame.Core.Economy;
using RacingGame.Core.Save;
using UnityEngine;

namespace RacingGame
{
    public sealed partial class MenuScreens
    {
        private void BuildDriverProfile(RectTransform root)
        {
            var content = UiFactory.Scroll(Header(root, "driver.title"), "DriverScroll", Vector2.zero, Vector2.zero);
            var progress = PlatformGlue.Save.Data.progress;
            string empty = Loc.Get("driver.empty");
            string body = DriverProfile.Favorite(progress, out long finishes);
            var cup = Array.Find(RaceCups.All, entry => entry.Complete(progress));
            var gap = DriverProfile.Closest(progress.closestFinish, null);
            void Fact(string name, string key, string value)
            {
                var row = Item(content, name, Narrow ? 154f : 140f);
                Label(row, "Title", Loc.Get(key), 25f, new Vector2(0f, .52f), Vector2.one,
                    new Vector2(22f, 0f), new Vector2(-22f, -8f), UiFactory.Muted);
                Label(row, "Value", value, 31f, Vector2.zero, new Vector2(1f, .55f),
                    new Vector2(22f, 8f), new Vector2(-22f, 0f));
            }
            Fact("DriverBody", "driver.favorite", body == null ? empty : Loc.Get(CarBodies.Get(body).NameKey) + " · " + finishes);
            Fact("DriverCup", "driver.cup", cup == null ? empty : Loc.Get(cup.NameKey));
            var ev = gap == null ? null : Array.Find(flow.events, e => e != null && e.eventId == gap.eventId);
            string venue = ev != null ? Loc.Get(ev.nameKey) : gap != null ? UiFactory.Escape(gap.eventId) : "";
            Fact("DriverGap", "driver.closest", gap == null ? empty : UiFactory.FormatMilliseconds(gap.Difference)
                + " · " + venue + " · " + UiFactory.Escape(gap.rival ?? ""));
        }
    }
}
