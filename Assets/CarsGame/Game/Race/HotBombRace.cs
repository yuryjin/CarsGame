using UnityEngine;

namespace RacingGame
{
    /// <summary>A single contact-pass bomb. Its fuse belongs to the round, never to the carrier.</summary>
    public sealed class HotBombRace : MonoBehaviour, ISimulated
    {
        public const float FuseSeconds = 18f;
        public const float PassCooldownSeconds = 1.5f;
        public const float BetweenBombsSeconds = 3f;
        public RaceParticipant Carrier { get; private set; }
        public RaceParticipant LastPasser { get; private set; }
        public int Passes { get; private set; }
        public int Explosions { get; private set; }
        public int SimulationOrder => 1100;
        public float SecondsLeft => Carrier == null ? 0f : Mathf.Max(0, expiresAt - SimulationDriver.Instance.Tick) * SimulationDriver.FixedStep;
        public float PassLockSeconds => Carrier == null ? 0f : Mathf.Max(0, passAfter - SimulationDriver.Instance.Tick) * SimulationDriver.FixedStep;
        private RaceManager race;
        private int expiresAt, passAfter, nextBombAt, nextCarrier;
        private bool firstBomb = true;
        private bool Running => isActiveAndEnabled && race != null && (race.State == RaceState.Racing || race.State == RaceState.Finishing) && !PauseController.IsPaused;

        public void Initialize(RaceManager manager)
        {
            race = manager;
            foreach (var p in race.Participants) p.HotBomb = this;
        }

        private void OnEnable() => SimulationDriver.Instance.Register(this);
        private void OnDisable() { if (SimulationDriver.Current != null) SimulationDriver.Current.Unregister(this); }
        private void OnDestroy() => Stop();

        public void Stop()
        {
            Carrier = LastPasser = null;
            if (race != null)
                foreach (var p in race.Participants) if (p != null && p.HotBomb == this) p.HotBomb = null;
            enabled = false;
        }

        private bool Eligible(RaceParticipant p) => p != null && p.HotBomb == this && p.gameObject.activeInHierarchy
            && p.Health != null && !p.Health.IsDead && !race.HasFinished(p);

        public void SimulationStep(int tick)
        {
            if (!Running) return;
            if (Carrier != null && !Eligible(Carrier))
            {
                Carrier = LastPasser = null;
                nextBombAt = tick + SimulationDriver.SecondsToTicks(BetweenBombsSeconds);
            }
            if (Carrier == null)
            {
                if (tick < nextBombAt) return;
                int eligible = 0;
                foreach (var p in race.Participants) if (Eligible(p)) eligible++;
                if (eligible < 2) return;
                if (firstBomb && Eligible(race.Player))
                {
                    Carrier = race.Player;
                    firstBomb = false;
                }
                else
                {
                    for (int i = 0; i < race.Participants.Count; i++)
                    {
                        var candidate = race.Participants[nextCarrier++ % race.Participants.Count];
                        if (Eligible(candidate)) { Carrier = candidate; break; }
                    }
                }
                LastPasser = null;
                expiresAt = tick + SimulationDriver.SecondsToTicks(FuseSeconds);
                passAfter = tick + SimulationDriver.SecondsToTicks(PassCooldownSeconds);
            }
            if (tick < expiresAt) return;
            var victim = Carrier;
            var attacker = LastPasser != null ? LastPasser.gameObject : null;
            Carrier = LastPasser = null;
            nextBombAt = tick + SimulationDriver.SecondsToTicks(BetweenBombsSeconds);
            Explosions++;
            victim.Health.Detonate(attacker);
        }

        /// <summary>Only physical car contacts call this. Duplicate callbacks cannot pass it straight back.</summary>
        public bool Touch(RaceParticipant from, RaceParticipant to)
        {
            if (!Running || from != Carrier || from == to || !Eligible(from) || !Eligible(to)) return false;
            int tick = SimulationDriver.Instance.Tick;
            if (tick < passAfter || tick >= expiresAt) return false;
            LastPasser = from;
            Carrier = to;
            passAfter = tick + SimulationDriver.SecondsToTicks(PassCooldownSeconds);
            Passes++;
            from.BombPasses++;
            return true;
        }

        /// <summary>Keep the existing driving line and corner braking; only choose a contact/escape lane.</summary>
        public bool TryGetTactic(RaceParticipant me, float laneLimit, out float lane, out float speedLimit)
        {
            lane = 0f; speedLimit = float.PositiveInfinity;
            if (!Running || Carrier == null || !Eligible(me) || !Eligible(Carrier) || me.Progress == null) return false;
            var path = me.Progress.Path;
            if (path == null) return false;
            if (me != Carrier)
            {
                float gap = path.SignedDelta(me.Progress.Distance, Carrier.Progress.Distance);
                if (Mathf.Abs(gap) > 25f || Vector3.Distance(me.transform.position, Carrier.transform.position) > 25f) return false;
                float dangerLane = path.Spline.LateralOffset(Carrier.transform.position, Carrier.Progress.Distance);
                lane = dangerLane >= 0f ? -laneLimit : laneLimit;
                return true;
            }
            RaceParticipant target = null;
            float best = 80f;
            foreach (var p in race.Participants)
            {
                if (p == me || !Eligible(p) || p.Progress == null) continue;
                float gap = Mathf.Abs(path.SignedDelta(me.Progress.Distance, p.Progress.Distance));
                if (gap >= best || Vector3.Distance(me.transform.position, p.transform.position) > 80f) continue;
                target = p; best = gap;
            }
            if (target == null) return false;
            lane = Mathf.Clamp(path.Spline.LateralOffset(target.transform.position, target.Progress.Distance), -laneLimit, laneLimit);
            // A carrier ahead of the pack lets the next car catch up instead of driving away with the bomb.
            if (path.SignedDelta(me.Progress.Distance, target.Progress.Distance) < -3f)
                speedLimit = Mathf.Max(18f, Mathf.Abs(target.Car.SpeedKmh) * 0.75f);
            return true;
        }
    }
}
