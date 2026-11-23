using System.Collections.Generic;
using RacingGame.Core.Ghost;
using UnityEngine;

namespace RacingGame
{
    public static class GhostSectorTimes
    {
        // Drive the HUD from the same samples and interpolated gates used to measure a saved replay.
        public sealed class Run
        {
            private readonly TrackPath path;
            private readonly float third;
            private readonly float[] gates;
            private readonly long expected;
            private readonly List<int> splits = new List<int>(); // durations in lap order
            private readonly HashSet<int> interrupted = new HashSet<int>();
            private int nextSample, hint = -1, fromTime, lastSplit;
            private int gridCrossing = -1;
            private float from;
            private bool forwardFinish, finished, sectorInterrupted;

            public Run(TrackPath path, int laps)
            {
                this.path = path;
                expected = 3L * laps;
                third = path.Length / 3f;
                gates = new[] { path.Wrap(path.startLineDistance + third), path.Wrap(path.startLineDistance + 2f * third),
                    path.Wrap(path.startLineDistance) };
            }

            // Publish the final sector at the authoritative finish time. Earlier laps use line-crossing time.
            public int Count => !finished && splits.Count >= expected ? (int)expected - 1 : splits.Count;
            public int Duration(int index) => splits[index];

            public int[] BestDurations()
            {
                var best = new int[3];
                for (int i = 0; i < Count; i++)
                {
                    if (interrupted.Contains(i) || i == 0 && gridCrossing < 0) continue;
                    // A flying sector starts at the line, even on the opening lap from the grid.
                    int time = splits[i] - (i == 0 ? gridCrossing : 0);
                    int sector = i % 3;
                    if (time > 0 && (best[sector] == 0 || time < best[sector])) best[sector] = time;
                }
                return best;
            }

            public void Advance(GhostRecord record, bool stopped = false)
            {
                // AddPose may still replace the newest millisecond with a final pose or recovery cut.
                if (!finished && record != null) Read(record, record.Samples.Count - (stopped ? 0 : 1));
            }

            public bool Finish(GhostRecord record)
            {
                if (finished) return true;
                if (record == null || record.Samples.Count < 2 || expected < 3) return false;
                Read(record, record.Samples.Count);
                float gap = path.SignedDelta(from, gates[2]);
                float tolerance = Mathf.Clamp(80f * record.SampleStep / record.TickRate, 1f, 20f);
                if (Mathf.Abs(gap) > tolerance) return false;
                // V1 can end one sampling interval before the finish line.
                bool atLastGate = splits.Count == expected - 1 && forwardFinish && gap >= 0f;
                if (atLastGate) CompleteSector(record.DurationMilliseconds);
                else if (splits.Count == expected) splits[splits.Count - 1] += record.DurationMilliseconds - lastSplit;
                else return false;
                finished = true;
                return true;
            }

            private void Read(GhostRecord record, int limit)
            {
                for (; nextSample < limit; nextSample++)
                {
                    var sample = record.Samples[nextSample];
                    bool cut = (sample.Flags & GhostRecord.TeleportFlag) != 0;
                    if (cut) hint = -1;
                    sectorInterrupted |= cut;
                    float to = path.Project(sample.Position, ref hint);
                    if (nextSample == 0 && Mathf.Abs(path.SignedDelta(to, gates[2])) < .01f) gridCrossing = sample.TimeMilliseconds;
                    if (nextSample > 0)
                    {
                        float travel = path.SignedDelta(from, to);
                        float remaining = path.SignedDelta(from, gates[splits.Count % 3]);
                        forwardFinish = !cut && travel >= 0f && travel < third;
                        if (forwardFinish && travel > 0f)
                        {
                            int interval = sample.TimeMilliseconds - fromTime;
                            if (splits.Count == 0 && gridCrossing < 0)
                            {
                                float line = path.SignedDelta(from, gates[2]);
                                if (line >= 0f && line <= travel)
                                    gridCrossing = fromTime + Mathf.RoundToInt(interval * (line / travel));
                            }
                            if (remaining >= 0f && remaining <= travel)
                                CompleteSector(fromTime + Mathf.RoundToInt(interval * (remaining / travel)));
                        }
                    }
                    fromTime = sample.TimeMilliseconds;
                    from = to;
                }
            }

            private void CompleteSector(int time)
            {
                if (sectorInterrupted) interrupted.Add(splits.Count);
                sectorInterrupted = false;
                splits.Add(time - lastSplit);
                lastSplit = time;
            }
        }

        public static Run MeasureLaps(TrackPath path, GhostRecord record, int laps)
        {
            if (path == null || record == null || laps < 1) return null;
            var run = new Run(path, laps);
            return run.Finish(record) ? run : null;
        }

        // Totals include every completed lap and the run from the starting grid.
        public static int[] Measure(TrackPath path, GhostRecord record, int laps)
        {
            var run = MeasureLaps(path, record, laps);
            if (run == null) return null;
            var totals = new int[3];
            for (int i = 0; i < run.Count; i++) totals[i % 3] += run.Duration(i);
            return totals;
        }
    }
}
