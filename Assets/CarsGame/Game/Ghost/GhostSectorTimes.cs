using RacingGame.Core.Ghost;
using UnityEngine;

namespace RacingGame
{
    public static class GhostSectorTimes
    {
        // Reconstruct splits from the recorded poses so existing personal ghosts work too.
        // Totals include every completed lap and the initial run from the grid to the line.
        public static int[] Measure(TrackPath path, GhostRecord record, int laps)
        {
            if (path == null || record == null || record.Samples.Count < 2 || laps < 1) return null;
            float third = path.Length / 3f;
            float[] gates = { path.Wrap(path.startLineDistance + third), path.Wrap(path.startLineDistance + 2f * third), path.Wrap(path.startLineDistance) };
            var totals = new int[3];
            var previous = record.Samples[0];
            int hint = -1, crossed = 0, lastSplit = 0;
            float from = path.Project(previous.Position, ref hint);
            bool forwardFinish = false;
            for (int i = 1; i < record.Samples.Count; i++)
            {
                var sample = record.Samples[i];
                bool cut = (sample.Flags & GhostRecord.TeleportFlag) != 0;
                if (cut) hint = -1;
                float to = path.Project(sample.Position, ref hint);
                float travel = path.SignedDelta(from, to);
                int sector = crossed % 3;
                float remaining = path.SignedDelta(from, gates[sector]);
                forwardFinish = !cut && travel >= 0f && travel < third;
                if (forwardFinish && travel > 0f && remaining >= 0f && remaining <= travel)
                {
                    int split = previous.TimeMilliseconds + Mathf.RoundToInt(
                        (sample.TimeMilliseconds - previous.TimeMilliseconds) * (remaining / travel));
                    totals[sector] += split - lastSplit;
                    lastSplit = split;
                    crossed++;
                }
                previous = sample;
                from = to;
            }

            // A legacy ghost can end a sampling interval short of the line. Use its saved finish
            // time only after the preceding gates and a genuine approach to the finish.
            float gap = path.SignedDelta(from, gates[2]);
            float tolerance = Mathf.Clamp(80f * record.SampleStep / record.TickRate, 1f, 20f);
            int expected = laps * 3;
            bool justBeforeLine = crossed == expected - 1 && forwardFinish && gap >= 0f && gap <= tolerance;
            if ((crossed != expected && !justBeforeLine) || Mathf.Abs(gap) > tolerance) return null;
            totals[2] += record.DurationMilliseconds - lastSplit;
            return totals;
        }
    }
}
