using RacingGame.Core.Daily;
using UnityEngine;

namespace RacingGame
{
    public sealed partial class GameFlow
    {
        private RaceEventDefinition customEvent;

        private void OnDestroy()
        {
            if (customEvent == null) return;
            if (RaceSetup.Event == customEvent) RaceSetup.Event = null;
            Destroy(customEvent);
        }

        public bool StartCustomRace(RaceEventDefinition course, int laps, int rivals, RaceTwist twist, DifficultyTier difficulty)
        {
            if (EventRules.CustomError(course, laps, rivals, twist) != null || !IsUnlocked(course)) return false;
            var ev = ScriptableObject.CreateInstance<RaceEventDefinition>();
            ev.hideFlags = HideFlags.DontSave;
            ev.IsCustom = true; ev.eventId = "custom"; ev.nameKey = "format.custom";
            ev.laps = laps; ev.aiCountOverride = rivals; ev.twist = twist;
            ev.reversed = course.reversed; ev.night = course.night; ev.sky = course.sky;
            ev.abyssTrack = course.abyssTrack; ev.spaceTrack = course.spaceTrack;
            ev.starRingTrack = course.starRingTrack; ev.wastelandTrack = course.wastelandTrack;
            StartRace(ev, difficulty);
            if (State != FlowState.Racing) { Destroy(ev); return false; }
            customEvent = ev;
            return true;
        }
    }
}
