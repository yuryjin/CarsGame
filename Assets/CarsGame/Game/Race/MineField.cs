using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Where a map allows mines. The arena box implements it today; the planned dune map
    /// (Docs/AI/ArenaMode.md) implements it for its canyon floor without touching <see cref="MineField"/>.
    /// </summary>
    public interface IMineFieldSite
    {
        /// <summary>Adds up to <paramref name="count"/> ground poses, the same ones for the same seed.</summary>
        void GetMinePoses(int count, int seed, List<Pose> into);
    }

    /// <summary>
    /// Mines the map lays instead of a car: placed at race start from an <see cref="IMineFieldSite"/>
    /// and laid again at the same spot some seconds after one goes off, so the field lasts the whole match.
    /// Ticked by <see cref="RaceDirector"/>; a relay waits while a car sits on the spot.
    /// </summary>
    public sealed class MineField : MonoBehaviour
    {
        [Tooltip("Damage at the centre of a field mine's blast.")]
        public float damage = 30f;
        [Tooltip("A relaid mine never appears closer than this to a car (m).")]
        public float clearance = 6f;

        private struct Slot
        {
            public Pose Pose;
            public MineHazard Mine;
            public int Generation;
            public int RelayTick;   // -1 = mine in place
        }

        private readonly List<Slot> slots = new List<Slot>();
        private readonly List<Pose> poses = new List<Pose>();
        private int relayTicks;

        public int Count => slots.Count;

        /// <summary>Slots whose mine is currently on the ground.</summary>
        public int LiveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < slots.Count; i++) if (InPlace(slots[i])) n++;
                return n;
            }
        }

        public Vector3 SlotPosition(int index) => slots[index].Pose.position;

        /// <summary>Replace the field: lay <paramref name="count"/> mines where the site allows.</summary>
        public void Lay(RaceDirector director, IMineFieldSite site, int count, int seed, float relaySeconds)
        {
            Clear();
            if (director == null || site == null || count <= 0) return;
            relayTicks = SimulationDriver.SecondsToTicks(Mathf.Max(1f, relaySeconds));
            poses.Clear();
            site.GetMinePoses(count, seed, poses);
            int tick = director.Tick;
            for (int i = 0; i < poses.Count; i++)
            {
                var slot = new Slot { Pose = poses[i], RelayTick = -1 };
                Place(director, ref slot, tick);
                slots.Add(slot);
            }
        }

        /// <summary>Forget the slots (the director recalls the mines themselves).</summary>
        public void Clear()
        {
            for (int i = 0; i < slots.Count; i++)
                if (InPlace(slots[i])) slots[i].Mine.Deactivate();
            slots.Clear();
        }

        public void SimulationStep(RaceDirector director, int tick)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (InPlace(slot)) continue;
                if (slot.RelayTick < 0) { slot.RelayTick = tick + relayTicks; slots[i] = slot; continue; }
                if (tick < slot.RelayTick || CarNear(director, slot.Pose.position)) continue;
                Place(director, ref slot, tick);
                slots[i] = slot;
            }
        }

        private void Place(RaceDirector director, ref Slot slot, int tick)
        {
            slot.Mine = director.RequestMine(new MineSpawn
            {
                Position = slot.Pose.position,
                Rotation = slot.Pose.rotation,
                Damage = damage,
                Tick = tick,
                Kind = MineKind.Contact,
                Field = true
            });
            slot.Generation = slot.Mine.Generation;
            slot.RelayTick = -1;
        }

        private static bool InPlace(in Slot slot) => slot.Mine != null && slot.Mine.IsLive && slot.Mine.Generation == slot.Generation;

        private bool CarNear(RaceDirector director, Vector3 position)
        {
            var cars = director.Cars;
            float r2 = clearance * clearance;
            for (int i = 0; i < cars.Count; i++)
                if (cars[i] != null && cars[i].isActiveAndEnabled && (cars[i].transform.position - position).sqrMagnitude < r2) return true;
            return false;
        }
    }

    /// <summary>
    /// Minefield rows across the road, on straights only (a mine behind a blind bend is not fair).
    /// Each gate is three rows 14 m apart; a row mines two of three lanes and the free lane moves
    /// from row to row, so a gate is a slalom, never a wall.
    /// </summary>
    public sealed class TrackMineSite : IMineFieldSite
    {
        public const int RowsPerGate = 3;
        public const float RowSpacing = 14f;

        private readonly TrackPath track;
        private readonly float startExclusion;

        public TrackMineSite(TrackPath track, float startExclusion = 150f)
        {
            this.track = track;
            this.startExclusion = startExclusion;
        }

        public void GetMinePoses(int count, int seed, List<Pose> into)
        {
            if (track == null || count <= 0) return;
            var rng = new System.Random(seed);
            int perGate = RowsPerGate * 2;
            int gates = Mathf.Max(1, (count + perGate - 1) / perGate);
            float excluded = Mathf.Clamp(startExclusion, 0f, track.Length * 0.3f);
            float sector = (track.Length - excluded) / gates;
            float lane = Mathf.Min(3f, Mathf.Max(0.5f, track.halfWidth - 1.4f));
            float gateLength = RowSpacing * (RowsPerGate - 1);
            int placed = 0;
            for (int g = 0; g < gates && placed < count; g++)
            {
                float start = track.startLineDistance + excluded + sector * g;
                float gateStart = StraightestStart(start + sector * 0.2f, start + sector * 0.8f - gateLength, gateLength);
                int lastGap = -1;
                for (int row = 0; row < RowsPerGate && placed < count; row++)
                {
                    int gap = rng.Next(0, 3);
                    if (gap == lastGap) gap = (gap + 1 + rng.Next(0, 2)) % 3;
                    lastGap = gap;
                    float d = track.Wrap(gateStart + row * RowSpacing);
                    for (int l = 0; l < 3 && placed < count; l++)
                    {
                        if (l == gap) continue;
                        track.Pose(d, (l - 1) * lane, out var pos, out var rot);
                        into.Add(new Pose(pos, rot));
                        placed++;
                    }
                }
            }
        }

        private float StraightestStart(float from, float to, float length)
        {
            if (to <= from) return from;
            float best = from, bestTurn = float.MaxValue;
            for (float d = from; d <= to; d += 5f)
            {
                float turn = Mathf.Abs(Vector3.SignedAngle(track.Tangent(d - 20f), track.Tangent(d + length + 20f), Vector3.up));
                if (turn < bestTurn) { bestTurn = turn; best = d; }
            }
            return best;
        }
    }
}
