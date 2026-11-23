using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>The fugitive can leave the road line through the bridge; pursuers cut across to the merge on the main road.</summary>
    public sealed class PursuitRace : MonoBehaviour, ISimulated
    {
        public const float CaptureMetres = 7f;
        public const float CaptureSeconds = .6f;
        private RaceManager race;
        private readonly Dictionary<RaceParticipant, int> alongside = new Dictionary<RaceParticipant, int>();
        public int SimulationOrder => int.MaxValue - 5000;

        public void Follow(RaceManager owner) => race = owner;
        private void OnEnable() => SimulationDriver.Instance.Register(this);
        private void OnDisable() { if (SimulationDriver.Current != null) SimulationDriver.Current.Unregister(this); }

        public void SimulationStep(int tick)
        {
            if (race == null || race.State != RaceState.Racing || PauseController.IsPaused) return;
            var runner = race.Player;
            if (runner == null || race.HasFinished(runner)) return;
            var path = runner.Progress.Path;
            bool runnerOnBridge = path.OnFork(runner.transform.position);
            float runnerLane = path.Spline.LateralOffset(runner.transform.position, runner.Progress.Distance);
            foreach (var hunter in race.Participants)
            {
                if (hunter.isPlayer) continue;
                var ai = hunter.GetComponent<AiDriver>();
                float distance = Vector3.Distance(hunter.transform.position, runner.transform.position);
                if (ai != null)
                {
                    // Hold the fugitive's lane on approach. At the fork the two roads split physically.
                    ai.baseLaneOffset = distance < 42f ? Mathf.Clamp(runnerLane, -path.halfWidth + 2f, path.halfWidth - 2f) : hunter.laneOffset;
                    ai.speedMultiplier = .93f;
                }
                int held = 0;
                if (distance <= CaptureMetres && !hunter.Health.IsDead && !runner.Health.IsDead
                    && path.OnFork(hunter.transform.position) == runnerOnBridge)
                {
                    alongside.TryGetValue(hunter, out held);
                    held++;
                }
                alongside[hunter] = held;
                if (held < SimulationDriver.SecondsToTicks(CaptureSeconds)) continue;
                race.EndPursuit(hunter);
                break;
            }
        }
    }
}
