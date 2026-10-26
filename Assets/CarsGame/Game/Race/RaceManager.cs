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
        [Tooltip("Duration of match in Arena mode (seconds).")]
        public float arenaDurationSeconds = 75f;

        public bool IsArenaMode => (RaceSetup.Event != null && RaceSetup.Event.mode == EventMode.Arena) || (spawner != null && spawner.Arena != null);
        public float MatchDuration => (RaceSetup.Event != null && RaceSetup.Event.mode == EventMode.Arena && RaceSetup.Event.matchDuration > 0f)
            ? RaceSetup.Event.matchDuration
            : arenaDurationSeconds;
        /// <summary>Co-op boss mission: the <see cref="MissionDirector"/> decides when it ends (<see cref="EndMission"/>).</summary>
        public bool IsMissionMode => RaceSetup.Event != null && RaceSetup.Event.mode == EventMode.Mission;
        public double RemainingTime => IsArenaMode ? Math.Max(0d, MatchDuration - RaceTime) : 0d;

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
        private readonly Dictionary<RaceParticipant, RaceResultEntry> finishEntries = new Dictionary<RaceParticipant, RaceResultEntry>();
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
        /// <summary>Wrecked for good (one-life race): classified DNF and no longer awaited at the finish.</summary>
        public static bool IsOut(RaceParticipant p) => p != null && p.Health != null && p.Health.IsDead && !p.Health.respawnOnDeath;
        // Finished or out. A wrecked player is never "done" here: the flow either revives them (rewarded
        // continue) or ends the race with EndForPlayerOut.
        private int DoneCount()
        {
            int n = 0;
            foreach (var p in participants) if (finishTimes.ContainsKey(p) || (IsOut(p) && !p.isPlayer)) n++;
            return n;
        }

        /// <summary>One-life race: the player declined to continue after a wreck. Unfinished cars are DNF.</summary>
        public void EndForPlayerOut()
        {
            if (State != RaceState.Racing && State != RaceState.Finishing) return;
            EndRace();
        }
        public bool HasFinished(RaceParticipant p) => finishTimes.ContainsKey(p);
        public bool CanShowResults => State == RaceState.Finishing && !IsArenaMode && !IsMissionMode
            && Player != null && HasFinished(Player);
        /// <summary>The player's clock stops at the line while the remaining field can keep racing.</summary>
        public double PlayerElapsedTime => Player != null && finishTimes.TryGetValue(Player, out double time) ? time : RaceTime;

        /// <summary>Skip the remaining spectator wait only after a legitimate player finish.</summary>
        public bool ShowResultsAfterFinish()
        {
            if (!CanShowResults) return false;
            EndRace();
            return true;
        }

        /// <summary>
        /// Boss mission over. A win: the whole team finishes together, placed by kills. A loss: everyone is DNF.
        /// </summary>
        public void EndMission(bool victory)
        {
            if (State != RaceState.Racing && State != RaceState.Finishing) return;
            if (victory)
            {
                double t = RaceClock.Now - RaceStartTime;
                foreach (var p in participants) if (p != null) finishTimes[p] = t;
            }
            EndRace();
        }

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
            bool isArena = (RaceSetup.Event != null && RaceSetup.Event.mode == EventMode.Arena);
            spawner.spawnOnStart = false;
            DetachLapHandlers();
            if (!spawner.Spawn(arena: isArena))
            {
                State = RaceState.Idle;
                Debug.LogError("[RaceManager] spawn failed; race stays Idle.", this);
                return;
            }
            participants.Clear();
            participants.AddRange(spawner.Participants);
            finishTimes.Clear();
            finishEntries.Clear();
            positions.Clear();
            Results = null;
            allAiFinishedAt = -1;

            foreach (var p in participants)
            {
                SetFrozen(p, true);
                if (tier != null && !p.isPlayer && p.Health != null) p.Health.SetMaxHealth(p.Health.maxHealth * tier.aiHealthMultiplier, true);
                if (p.Progress != null) p.Progress.BeginRace();
            }

            OnFieldSpawned?.Invoke();
            RaceClock.Reset();
            RaceClock.Paused = PauseController.IsPaused;
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
            participants.Clear(); finishTimes.Clear(); finishEntries.Clear(); positions.Clear();
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
            bool combatRanking = IsArenaMode || IsMissionMode;
            foreach (var p in participants)
            {
                SetFrozen(p, false);
                if (p.Progress != null)
                {
                    p.Progress.BeginRace();
                    if (!combatRanking)
                    {
                        var participant = p;
                        Action<int, double> handler = (lap, time) => OnLap(participant, lap);
                        lapHandlers[p] = handler;
                        p.Progress.LapCompleted += handler;
                    }
                }
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
            if (RaceSetup.Event != null && RaceSetup.Event.IsPractice) return;
            if (lap >= laps && !finishTimes.ContainsKey(p)) Finish(p);
        }

        private void Finish(RaceParticipant p)
        {
            if (State == RaceState.Finished) return;
            finishTimes[p] = RaceClock.Now - RaceStartTime;
            Rank();                                  // placement is final before anyone reads PositionOf
            var entry = new RaceResultEntry();
            ReadResult(p, entry);
            entry.KillsPerVictim = p.KillTally.ToPerVictimCounts();
            finishEntries[p] = entry;
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
            if (DoneCount() >= participants.Count) EndRace();
        }

        /// <summary>
        /// QA only (DebugBus "finish"): the player crosses the line now and the race ends; unfinished rivals are DNF.
        /// Gated here too: the page can reach any method by name through unityInstance.SendMessage.
        /// </summary>
        public void QaFinishNow()
        {
            if (!DebugBus.CheatsAllowed) return;
            if (State != RaceState.Racing && State != RaceState.Finishing) return;
            var player = Player;
            if (player != null && !finishTimes.ContainsKey(player)) Finish(player);
            EndRace();
        }

        private void UpdateRacing()
        {
            rankTimer += Time.unscaledDeltaTime;
            if (rankTimer >= 0.1f) { rankTimer = 0f; Rank(); }

            if (IsArenaMode)
            {
                if (RaceTime >= MatchDuration) EndRace();
                return;
            }
            if (IsMissionMode) return;   // no laps, no rubber band: the mission ends itself

            bandTimer += Time.unscaledDeltaTime;
            if (bandTimer >= 0.5f) { bandTimer = 0f; ApplyRubberBand(); }

            if (State == RaceState.Finishing && RaceClock.Now - playerFinishTime >= finishGraceSeconds) EndRace();

            // One-life race: everyone still in the race has finished (the player finishing ends it the usual way).
            if (participants.Count > 0 && DoneCount() >= participants.Count) { EndRace(); return; }

            // Everyone but the player is home: the player gets a longer grace, then is classified DNF.
            // Only when someone actually finished: a one-life player whose rivals were all wrecked races on alone.
            if (State == RaceState.Racing && participants.Count > 1 && finishTimes.Count > 0 && DoneCount() == participants.Count - 1 && Player != null && !finishTimes.ContainsKey(Player))
            {
                if (allAiFinishedAt < 0) allAiFinishedAt = RaceClock.Now;
                else if (RaceClock.Now - allAiFinishedAt >= finishGraceSeconds * 3f) EndRace();
            }
        }

        private void Rank()
        {
            rankBuffer.Clear();
            bool combatRanking = IsArenaMode || IsMissionMode;
            for (int i = 0; i < participants.Count; i++)
            {
                var p = participants[i];
                bool finished = finishTimes.TryGetValue(p, out double t);
                rankBuffer.Add(new RankEntry
                {
                    CarId = p.Car != null ? p.Car.CarId : i,
                    Finished = finished,
                    FinishTime = t,
                    TotalProgress = p.Progress != null ? p.Progress.TotalProgress : 0d,
                    Kills = p.Kills,
                    Deaths = p.Deaths
                });
            }
            if (rankPositions.Length < rankBuffer.Count) rankPositions = new int[rankBuffer.Count];
            if (combatRanking)
                RaceRanking.RankArena(rankBuffer, rankOrder, rankPositions);
            else
                RaceRanking.Rank(rankBuffer, rankOrder, rankPositions);

            bool changed = false;
            for (int i = 0; i < participants.Count; i++)
            {
                if (!positions.TryGetValue(participants[i], out int old) || old != rankPositions[i]) changed = true;
                positions[participants[i]] = rankPositions[i];
            }
            if (changed) OnPositionsChanged?.Invoke();
        }

        // Invisible help (Core.Sim.RubberBand): AI far behind get a little more top speed, AI far ahead ease
        // off a little but never on their last lap. Only top speed is touched (never grip or damage).
        private void ApplyRubberBand()
        {
            var player = Player;
            float strength = tier != null ? tier.rubberBandStrength : 0.6f;
            if (player == null || strength <= 0f) return;
            float playerProgress = player.Progress != null ? player.Progress.TotalProgress : 0f;
            foreach (var p in participants)
            {
                if (p.isPlayer || finishTimes.ContainsKey(p) || p.Progress == null) continue;
                var ai = p.GetComponent<AiDriver>();
                if (ai == null) continue;
                float gap = p.Progress.TotalProgress - playerProgress;          // + = AI ahead
                bool lastLap = p.Progress.LapsCompleted >= laps - 1;
                ai.speedMultiplier = Core.Sim.RubberBand.Multiplier(gap, lastLap, RaceTime, strength);
                if (RaceSetup.IsIntroRace && RaceTime < 15d && p == participants[0]) ai.speedMultiplier = Mathf.Min(ai.speedMultiplier, RaceSetup.IntroTargetSpeed);
                var gunner = p.GetComponent<AiGunner>();
                if (gunner != null) gunner.suppressed = gap > 120f;   // far ahead: stop shooting at the pack
            }
        }

        /// <summary>HUD and results share a snapshot; damage after crossing the line cannot lose a star.</summary>
        public void ReadResult(RaceParticipant p, RaceResultEntry entry)
        {
            finishEntries.TryGetValue(p, out var finishedEntry);
            entry.Name = p.displayName; entry.IsPlayer = p.isPlayer;
            entry.Position = PositionOf(p);
            entry.Finished = finishTimes.TryGetValue(p, out double time);
            entry.Eliminated = !entry.Finished && IsOut(p);
            entry.TotalTime = entry.Finished ? time : RaceTime;
            entry.BestLap = finishedEntry != null ? finishedEntry.BestLap : p.Progress != null ? p.Progress.BestLapTime : double.PositiveInfinity;
            entry.Laps = finishedEntry != null ? finishedEntry.Laps : p.Progress != null ? p.Progress.LapsCompleted : 0;
            entry.Kills = finishedEntry != null ? finishedEntry.Kills : p.Kills;
            entry.KillsPerVictim = finishedEntry != null ? finishedEntry.KillsPerVictim : null;
            entry.TurretPoints = finishedEntry != null ? finishedEntry.TurretPoints : p.TurretPoints;
            entry.Deaths = finishedEntry != null ? finishedEntry.Deaths : p.Deaths;
            entry.RepairsUsed = finishedEntry != null ? finishedEntry.RepairsUsed : p.Health != null ? p.Health.RepairsUsed : 0;
            entry.DriftBoosts = finishedEntry != null ? finishedEntry.DriftBoosts : p.DriftBoosts;
            entry.Shortcuts = finishedEntry != null ? finishedEntry.Shortcuts : p.Shortcuts;
            entry.CeilingBoosts = finishedEntry != null ? finishedEntry.CeilingBoosts : p.CeilingBoosts;
            entry.BombPasses = finishedEntry != null ? finishedEntry.BombPasses : p.BombPasses;
            entry.Resets = finishedEntry != null ? finishedEntry.Resets : p.Resets;
            entry.CleanFinalLap = finishedEntry != null ? finishedEntry.CleanFinalLap : p.CleanFinalLap;
            entry.Survived = entry.Deaths == 0;
        }

        private void EndRace()
        {
            if (State == RaceState.Finished) return;
            State = RaceState.Finished;
            DetachLapHandlers();
            Rank();
            bool isArena = IsArenaMode;
            var results = new RaceResults
            {
                IsArena = isArena,
                LapsRequired = isArena || IsMissionMode ? 0 : laps,
                RaceDuration = RaceClock.Now - RaceStartTime
            };
            foreach (var p in participants)
            {
                var entry = new RaceResultEntry();
                ReadResult(p, entry);
                if (!finishEntries.ContainsKey(p)) entry.KillsPerVictim = p.KillTally.ToPerVictimCounts();
                if (isArena)
                {
                    entry.Finished = true;
                    entry.Eliminated = false;
                    entry.Survived = p.Deaths == 0;
                }
                results.Entries.Add(entry);
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
