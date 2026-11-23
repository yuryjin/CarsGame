using UnityEngine;

namespace RacingGame
{
    public enum ForkRoute { Undecided, MainRoad, Repair, Shortcut }

    public sealed partial class AiDriver
    {
        public ForkRoute CurrentFork { get; private set; }
        private RaceManager race;

        private void UpdateForkDecision(float distance)
        {
            var fork = path.Fork;
            if (fork == null || participant == null || race == null || (participant.isPlayer && fork.Course != MountainFork.Volcano))
            {
                CurrentFork = ForkRoute.Undecided;
                return;
            }
            float along = path.SignedDelta(fork.ApproachDistance, distance);
            if (along < -fork.DecisionLead || along > fork.MainSpan + fork.ExitRunout)
            {
                CurrentFork = ForkRoute.Undecided;
                return;
            }
            if (CurrentFork != ForkRoute.Undecided || along > 1f) return;
            if (fork.Course == MountainFork.Volcano)
            {
                CurrentFork = fork.VolcanoRouteFor(participant);
                return;
            }
            var health = participant.Health;
            bool hurt = health != null && health.Health < health.maxHealth * .65f;
            bool trailing = race.PositionOf(participant) > (race.Participants.Count + 1) / 2;
            CurrentFork = hurt && fork.HasRepair ? ForkRoute.Repair
                : trailing && !hurt ? ForkRoute.Shortcut : ForkRoute.MainRoad;
        }
    }
}
