using RacingGame.Core.Economy;
using UnityEngine;

namespace RacingGame
{
    public sealed partial class MenuScreens
    {
        private RaceEventDefinition relayEvent;
        private DifficultyTier relayTier;

        private void BuildRelay(RectTransform frame)
        {
            var body = Header(frame, "format.relay", showWallet: false);
            var content = UiFactory.Scroll(body, "RelayScroll", new Vector2(0f, 100f), Vector2.zero);
            var help = Item(content, "Rule", 118f);
            Label(help, "Text", Loc.Get("format.relay.rule"), 26f, Vector2.zero, Vector2.one, new Vector2(20f, 6f), new Vector2(-20f, -6f));
            string first = RelayRace.First(relayEvent), second = RelayRace.Second(relayEvent, first);
            var available = RelayRace.Available();
            for (int i = 0; i < 2; i++)
            {
                bool opening = i == 0;
                string selected = opening ? first : second;
                var row = Item(content, opening ? "RelayFirst" : "RelaySecond", 92f);
                string name = Loc.Get(CarBodies.Get(selected).NameKey);
                Label(row, "Body", Loc.Get(opening ? "format.relay.first" : "format.relay.second") + " · " + name, 27f,
                    Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-200f, 0f));
                for (int direction = -1; direction <= 1; direction += 2)
                {
                    int step = direction;
                    UiFactory.Button(row, step < 0 ? "Previous" : "Next", step < 0 ? "‹" : "›", () =>
                    {
                        int index = available.FindIndex(b => b.Id == selected);
                        string value = available[(index + step + available.Count) % available.Count].Id;
                        if (opening) { relayEvent.RelayFirst = value; relayEvent.RelaySecond = value == second ? selected : second; }
                        else { relayEvent.RelaySecond = value; relayEvent.RelayFirst = value == first ? selected : first; }
                        Show("relay");
                    }, Vector2.right, Vector2.one, new Vector2(step < 0 ? -182f : -90f, 6f),
                        new Vector2(step < 0 ? -94f : -2f, -6f), 32f);
                }
            }
            UiFactory.Button(body, "StartRelay", Loc.Get("menu.play") + " ›", () => flow.StartRace(relayEvent, relayTier),
                Vector2.zero, Vector2.right, Vector2.zero, Vector2.up * 82f, 30f);
        }
    }
}
