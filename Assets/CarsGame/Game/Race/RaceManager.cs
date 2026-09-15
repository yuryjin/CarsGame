using System;
using System.Collections.Generic;
using UnityEngine;
using RacingGame.Core.Sim;

namespace RacingGame
{
    public enum RaceState { Idle, Countdown, Racing, Finishing, Finished }

    /// <summary>
    /// Owns one race: spawns the field, runs the countdown with everyone frozen, releases the
    /// cars, ranks participants, detects finishes, applies rubber-banding to the AI and
    /// publishes <see cref="RaceResults"/>. All timing uses <see cref="RaceClock"/>.
    /// </summary>
    public sealed class RaceManager : MonoBehaviour
    {
        [Header("Setup")]
        public RaceSpawner spawner;
        public DifficultyTier tier;
        [Range(1, 20)] public int laps = 3;
        [Tooltip("-1 = AI count from the tier; the flow sets an event override here.")]
        public int aiCountOverride = -1;
        public bool autoStart = true;
        public float countdownSeconds = 3f;
        [Tooltip("After the player finishes, the rest of the field gets this long before the race ends.")]
        public float finishGraceSeconds = 20f;

        [Header("Rubber-banding")]
        public float rubberBandGapMetres = 150f;
        public float rubberBandMinMultiplier = 0.8f;
        public float rubberBandMaxMultiplier = 1.15f;

        public RaceState State { get; private set; } = RaceState.Idle;
        public double RaceStartTime { get; private set; }
        public double RaceTime => State >= RaceState.Racing ? RaceClock.Now - RaceStartTime : 0d;
        public int CountdownValue { get; private set; }
        public RaceResults Results { get; private set; }
        public IReadOnlyList<RaceParticipant> Participants => participants;
        public RaceParticipant Player => spawner != null ? spawner.Player : null;

        /// <summary>Field spawned and frozen on the grid, before the countdown (apply upgrades here).</summary>
        public event Action OnFieldSpawned;
        public event Action<int> OnCountdownTick;      // 3, 2, 1, then 0 = GO
        public event Action OnRaceStarted;
        public event Action OnPositionsChanged;
        public event Action<RaceParticipant> OnParticipantFinished;
        public event Action<RaceResults> OnRaceFinished;

        private readonly List<RaceParticipant> participants = new List<RaceParticipant>();
        private readonly Dictionary<RaceParticipant, double> finishTimes = new Dictionary<RaceParticipant, double>();
        private readonly Dictionary<RaceParticipant, int> positions = new Dictionary<RaceParticipant, int>();
        private readonly List<RankEntry> rankBuffer = new List<RankEntry>(8);
        private readonly List<int> rankOrder = new List<int>(8);
        private int[] rankPositions = new int[8];
        private double countdownStart;
        private int lastCountdownValue = -1;
        private double playerFinishTime;
        private double allAiFinishedAt = -1;
        private float rankTimer, bandTimer;

        public int PositionOf(RaceParticipant p) => positions.TryGetValue(p, out int pos) ? pos : participants.Count;
        public bool HasFinished(RaceParticipant p) => finishTimes.ContainsKey(p);

        private void Start()
        {
            if (spawner == null) spawner = GetComponent<RaceSpawner>() ?? FindAnyObjectByType<RaceSpawner>();
            if (autoStart) StartRace();
        }

        public void StartRace()
        {
            if (spawner == null) { Debug.LogError("[RaceManager] no RaceSpawner.", this); return; }
            if (tier != null)
            {
                spawner.aiCount = aiCountOverride >= 0 ? aiCountOverride : tier.aiCount;
                spawner.aiFireCooldown = tier.aiFireCooldown;
                if (tier.profiles != null && tier.profiles.Length > 0) spawner.profiles = tier.profiles;
            }
            spawner.spawnOnStart = false;
            DetachLapHandlers();
            if (!spawner.Spawn())
            {
                State = RaceState.Idle;
                Debug.LogError("[RaceManager] spawn failed; race stays Idle.", this);
                return;
            }
            participants.Clear();
            participants.AddRange(spawner.Participants);
            finishTimes.Clear();
            positions.Clear();
            Results = null;
            allAiFinishedAt = -1;

            foreach (var p in participants)
            {
                SetFrozen(p, true);
                if (tier != null && !p.isPlayer && p.Health != null) p.Health.SetMaxHealth(p.Health.maxHealth * tier.aiHealthMultiplier, true);
                p.Progress.BeginRace();
            }

            OnFieldSpawned?.Invoke();
            RaceClock.Reset();
            countdownStart = RaceClock.Now;
            lastCountdownValue = -1;
            State = RaceState.Countdown;
            Rank();
        }

        /// <summary>Tear the field down (back to menu).</summary>
        public void AbortRace()
        {
            DetachLapHandlers();
            authoredConstraints.Clear();
            if (spawner != null) spawner.Clear();
            participants.Clear(); finishTimes.Clear(); positions.Clear();
            Results = null;
            State = RaceState.Idle;
        }

        private void Update()
        {
            switch (State)
            {
                case RaceState.Countdown: UpdateCountdown(); break;
                case RaceState.Racing:
                case RaceState.Finishing: UpdateRacing(); break;
            }
        }

        private void UpdateCountdown()
        {
            double elapsed = RaceClock.Now - countdownStart;
            int value = Mathf.Max(0, Mathf.CeilToInt((float)(countdownSeconds - elapsed)));
            if (value != lastCountdownValue)
            {
                lastCountdownValue = value;
                CountdownValue = value;
                OnCountdownTick?.Invoke(value);
            }
            if (elapsed >= countdownSeconds && State == RaceState.Countdown) Go();   // a tick listener may have aborted
        }

        private readonly Dictionary<RaceParticipant, Action<int, double>> lapHandlers = new Dictionary<RaceParticipant, Action<int, double>>();

        private void Go()
        {
            RaceStartTime = RaceClock.Now;
            foreach (var p in participants)
            {
                SetFrozen(p, false);
                p.Progress.BeginRace();
                var participant = p;
                Action<int, double> handler = (lap, time) => OnLap(participant, lap);
                lapHandlers[p] = handler;
                p.Progress.LapCompleted += handler;
            }
            authoredConstraints.Clear();   // restored; drop the references
            State = RaceState.Racing;
            OnRaceStarted?.Invoke();
        }

        private void DetachLapHandlers()
        {
            foreach (var kv in lapHandlers) if (kv.Key != null && kv.Key.Progress != null) kv.Key.Progress.LapCompleted -= kv.Value;
            lapHandlers.Clear();
        }

        private void OnDestroy() { DetachLapHandlers(); }

        private void OnLap(RaceParticipant p, int lap)
        {
            if (State == RaceState.Finished || State == RaceState.Idle) return;
            if (lap >= laps && !finishTimes.ContainsKey(p)) Finish(p);
        }

        private void Finish(RaceParticipant p)
        {
            if (State == RaceState.Finished) return;
            finishTimes[p] = RaceClock.Now - RaceStartTime;
            Rank();                                  // placement is final before anyone reads PositionOf
            OnParticipantFinished?.Invoke(p);
            if (p.isPlayer)
            {
                playerFinishTime = RaceClock.Now;
                if (p.Car != null) p.Car.inputEnabled = false;   // AI keeps racing; the player coasts
                if (State == RaceState.Racing) State = RaceState.Finishing;
            }
            else if (p.Car != null)
            {
                var ai = p.GetComponent<AiDriver>();
                if (ai != null) ai.speedMultiplier = 0.85f;      // finished AI cruise out of the way
            }
            Rank();
            if (finishTimes.Count >= participants.Count) EndRace();
        }

        private void UpdateRacing()
        {
            rankTimer += Time.unscaledDeltaTime;
            if (rankTimer >= 0.1f) { rankTimer = 0f; Rank(); }

            bandTimer += Time.unscaledDeltaTime;
            if (bandTimer >= 0.5f) { bandTimer = 0f; ApplyRubberBand(); }

            if (State == RaceState.Finishing && RaceClock.Now - playerFinishTime >= finishGraceSeconds) EndRace();

            // Everyone but the player is home: the player gets a longer grace, then is classified DNF.
            if (State == RaceState.Racing && participants.Count > 1 && finishTimes.Count == participants.Count - 1 && Player != null && !finishTimes.ContainsKey(Player))
            {
                if (allAiFinishedAt < 0) allAiFinishedAt = RaceClock.Now;
                else if (RaceClock.Now - allAiFinishedAt >= finishGraceSeconds * 3f) EndRace();
            }
        }

        private void Rank()
        {
            rankBuffer.Clear();
            for (int i = 0; i < participants.Count; i++)
            {
                var p = participants[i];
                bool finished = finishTimes.TryGetValue(p, out double t);
                rankBuffer.Add(new RankEntry { CarId = p.Car != null ? p.Car.CarId : i, Finished = finished, FinishTime = t, TotalProgress = p.Progress.TotalProgress });
            }
            if (rankPositions.Length < rankBuffer.Count) rankPositions = new int[rankBuffer.Count];
            RaceRanking.Rank(rankBuffer, rankOrder, rankPositions);
            bool changed = false;
            for (int i = 0; i < participants.Count; i++)
            {
                if (!positions.TryGetValue(participants[i], out int old) || old != rankPositions[i]) changed = true;
                positions[participants[i]] = rankPositions[i];
            }
            if (changed) OnPositionsChanged?.Invoke();
        }

        // Invisible help: AI far ahead of the player ease off, AI far behind get a little more top
        // speed. Only top speed is touched (never grip or damage) so it never reads as cheating.
        private void ApplyRubberBand()
        {
            var player = Player;
            float strength = tier != null ? tier.rubberBandStrength : 0.6f;
            if (player == null || strength <= 0f) return;
            float playerProgress = player.Progress.TotalProgress;
            foreach (var p in participants)
            {
                if (p.isPlayer || finishTimes.ContainsKey(p)) continue;
                var ai = p.GetComponent<AiDriver>();
                if (ai == null) continue;
                float gap = p.Progress.TotalProgress - playerProgress;          // + = AI ahead
                float raw = 1f + Mathf.Clamp(-gap / rubberBandGapMetres, rubberBandMinMultiplier - 1f, rubberBandMaxMultiplier - 1f);
                ai.speedMultiplier = Mathf.Lerp(1f, raw, strength);
                var gunner = p.GetComponent<AiGunner>();
                if (gunner != null) gunner.suppressed = gap > 120f;   // far ahead: stop shooting at the pack
            }
        }

        private void EndRace()
        {
            if (State == RaceState.Finished) return;
            State = RaceState.Finished;
            DetachLapHandlers();
            Rank();
            var results = new RaceResults { LapsRequired = laps, RaceDuration = RaceClock.Now - RaceStartTime };
            foreach (var p in participants)
            {
                bool finished = finishTimes.TryGetValue(p, out double t);
                results.Entries.Add(new RaceResultEntry
                {
                    Name = p.displayName, IsPlayer = p.isPlayer, Position = PositionOf(p), Finished = finished,
                    TotalTime = finished ? t : results.RaceDuration, BestLap = p.Progress.BestLapTime,
                    Laps = p.Progress.LapsCompleted, Kills = p.Kills, Deaths = p.Deaths
                });
                if (p.Car != null) p.Car.inputEnabled = false;
            }
            results.Entries.Sort((a, b) => a.Position.CompareTo(b.Position));
            Results = results;
            OnRaceFinished?.Invoke(results);
        }

        private readonly Dictionary<RaceParticipant, RigidbodyConstraints> authoredConstraints = new Dictionary<RaceParticipant, RigidbodyConstraints>();

        private void SetFrozen(RaceParticipant p, bool frozen)
        {
            if (p.Car == null) return;
            p.Car.inputEnabled = !frozen;
            var rb = p.Car.Body;
            if (rb == null) return;
            if (frozen) { if (!authoredConstraints.ContainsKey(p)) authoredConstraints[p] = rb.constraints; rb.constraints = RigidbodyConstraints.FreezeAll; }
            else rb.constraints = authoredConstraints.TryGetValue(p, out var c) ? c : RigidbodyConstraints.None;
        }
    }
}
