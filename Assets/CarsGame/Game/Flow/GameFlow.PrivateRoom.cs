using RacingGame.Core.Sim;
using UnityEngine;

namespace RacingGame
{
    public enum RoomState { Offline, Connecting, Lobby, Playing, Results, Interrupted, Unavailable }

    public interface IPrivateRoomLink
    {
        RoomState State { get; }
        uint Code { get; }
        int Seat { get; }
        int Members { get; }
        bool Fortress { get; }
        int[] FinishOrder { get; }
        int FinishedSeats { get; }
        int[] Shots { get; }
        float[] CoreDamage { get; }
        int Outcome { get; }
        void Create(bool fortress);
        void Join(uint code);
        void StartMatch();
        void Leave();
        void ReportFinish(RaceResults result);
    }

    public sealed partial class GameFlow
    {
        public static IPrivateRoomLink Room { get; set; }
        public static bool RoomPlaying => Room?.State == RoomState.Playing;
        public static bool RoomReplica => RoomPlaying && Room.Seat != 0;
        public static bool IsRoomDriver(int seat) => RoomPlaying && (Room.Members & (1 << seat)) != 0;
        private RaceEventDefinition privateEvent, beforeRoomEvent;
        private DifficultyTier beforeRoomTier;

        public bool StartPrivateMatch(int seed)
        {
            if (!RoomPlaying || privateEvent != null || State != FlowState.Menu || !PlatformGlue.IsReady) return false;
            var definition = System.Array.Find(events, e => e != null && e.eventId == (Room.Fortress ? "m1" : "e1"));
            if (definition == null) return false;
            beforeRoomEvent = RaceSetup.Event;
            beforeRoomTier = RaceSetup.Tier;
            privateEvent = Instantiate(definition);
            privateEvent.eventId = Room.Fortress ? "room_m1" : "room_e1";
            privateEvent.aiCountOverride = 3;
            privateEvent.laps = 1;
            privateEvent.unlockAfterEventId = "";
            RaceDirector.Instance.Reseed(seed);
            StartRace(privateEvent, System.Array.Find(tiers, t => t != null && t.tierId == "normal"));
            return State == FlowState.Racing && race.Participants.Count == 4;
        }

        private void RestoreRoomEvent()
        {
            if (privateEvent == null) return;
            RaceSetup.Event = beforeRoomEvent;
            RaceSetup.Tier = beforeRoomTier;
            Destroy(privateEvent);
            privateEvent = null;
            if (RoomPlaying) Room.Leave();
        }
    }
}
