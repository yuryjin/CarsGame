using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RacingGame.Core.Ghost;
using RacingGame.Core.Save;

namespace RacingGame
{
    public sealed partial class GameFlow
    {
        public sealed class RivalChoice
        {
            public string EventId { get; internal set; }
            public int Laps { get; internal set; }
            public long TargetMs { get; internal set; }
            public GhostRecord Replay { get; internal set; }
            public long DifferenceMs => Replay.DurationMilliseconds - TargetMs;
        }

        private RivalChoice preparedRival;
        private int rivalSearch;

        private long RivalTarget(RaceEventDefinition ev, int hash, int laps, GhostRecord own)
        {
            if (own != null) return own.DurationMilliseconds;
            var best = PlatformGlue.Save?.Data.progress.bests.Find(b => b != null && b.eventId == ev?.eventId);
            return best?.setup != null && best.setup.IsKnown && best.setup.trackHash == hash && best.setup.laps == laps ? best.bestTimeMs : 0;
        }

        private RivalChoice ChooseRival(IReadOnlyList<string> payloads, RaceEventDefinition ev, int hash, int laps, long target)
        {
            GhostRecord winner = null;
            if (payloads != null) foreach (var payload in payloads)
            {
                var session = ParseSession(payload);
                if (session == null || session.laps != 0 && session.laps != laps) continue;
                var rec = GhostRecord.FromBase64(session.ghost);
                if (rec == null || rec.TrackHash != hash || rec.DurationMilliseconds <= 0) continue;
                // Old sessions carry no lap metadata. Confirm completed crossings on the actual circuit.
                if (session.laps == 0 && GhostSectorTimes.Measure(track, rec, laps) == null) continue;
                if (!string.IsNullOrEmpty(session.name)) rec.PlayerName = UiFactory.Escape(session.name);
                long difference = Math.Abs(rec.DurationMilliseconds - target);
                long previous = winner == null ? long.MaxValue : Math.Abs(winner.DurationMilliseconds - target);
                if (difference < previous || difference == previous && (rec.DurationMilliseconds < winner.DurationMilliseconds
                    || rec.DurationMilliseconds == winner.DurationMilliseconds && string.CompareOrdinal(rec.PlayerName, winner.PlayerName) < 0))
                    winner = rec;
            }
            return winner == null ? null : new RivalChoice { EventId = ev?.eventId, Laps = laps, TargetMs = target, Replay = winner };
        }

        public async Task<RivalChoice> PreviewRivalAsync(RaceEventDefinition ev)
        {
            var sessions = PlatformGlue.Platform?.Sessions;
            var best = ev != null ? PlatformGlue.Save?.Data.progress.bests.Find(b => b != null && b.eventId == ev.eventId) : null;
            if (State != FlowState.Menu || ViewingReplay || best?.setup == null || !best.setup.IsKnown || best.setup.laps != ev.laps
                || ev.mode != EventMode.Race || ev.twist == Core.Daily.RaceTwist.TimeTrial || sessions == null) return null;
            int hash = best.setup.trackHash, generation = raceGeneration, search = ++rivalSearch;
            preparedRival = null;
            var reply = await sessions.LoadOpponentsAsync(3, "{\"meta1\":" + SessionMeta(hash) + "}");
            if (!reply.Ok || this == null || State != FlowState.Menu || ViewingReplay || raceGeneration != generation || rivalSearch != search) return null;
            Teardown(); ConfigureCircuit(ev, true);
            try
            {
                if (TrackHash(ev) != hash) return null;
                var own = PlatformGlue.Save.Data.personalGhosts.Find(ev.eventId, hash, ev.laps);
                long target = RivalTarget(ev, hash, ev.laps, own);
                if (target > 0) preparedRival = ChooseRival(reply.Value, ev, hash, ev.laps, target);
                return preparedRival;
            }
            finally { Teardown(); }
        }

        private static int SessionMeta(int hash) => (hash & 0x7fffffff) % 1000000000;
    }
}
