using UnityEngine;

namespace RacingGame
{
    public sealed partial class MenuScreens
    {
        private RaceEventDefinition briefingEvent;
        private DifficultyTier briefingTier;
        private int briefingTarget;

        private void StartMenuEvent(RaceEventDefinition ev, DifficultyTier tier)
        {
            if (ev == null || ev.eventId != "m1") { flow.StartRace(ev, tier); return; }
            briefingEvent = ev;
            briefingTier = tier;
            briefingTarget = -1;
            if (flow.State == FlowState.Results) GoTo("missionBriefing");
            else Show("missionBriefing");
        }

        private void BuildMissionBriefing(RectTransform frame)
        {
            var body = Header(frame, "mission.boss", showWallet: false);
            var map = UiFactory.Card(body, "ModuleBriefing", Vector2.zero, Vector2.one, new Vector2(0f, 96f), Vector2.zero);
            BossModuleMap.Create(map, null, briefingTarget, selected => briefingTarget = selected);
            UiFactory.Button(body, "Deploy", Loc.Get("menu.play") + " ›", () =>
            {
                flow.StartRace(briefingEvent, briefingTier);
                var mission = MissionDirector.Current;
                if (mission != null && briefingTarget >= 0) mission.MarkSquadTarget(mission.Boss.Parts[briefingTarget]);
            }, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 82f), 30f);
        }
    }
}
