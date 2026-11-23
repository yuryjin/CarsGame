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
            if (fork == null || participant == null || participant.isPlayer || race == null)
            {
                CurrentFork = ForkRoute.Undecided;
                return;
            }
            float along = path.SignedDelta(fork.ApproachDistance, distance);
            if (along < -27f || along > fork.MainSpan + 16f)
            {
                CurrentFork = ForkRoute.Undecided;
                return;
            }
            if (CurrentFork != ForkRoute.Undecided || along > 1f) return;
            var health = participant.Health;
            bool hurt = health != null && health.Health < health.maxHealth * .65f;
            bool trailing = race.PositionOf(participant) > (race.Participants.Count + 1) / 2;
            CurrentFork = hurt && fork.HasRepair ? ForkRoute.Repair
                : trailing && !hurt ? ForkRoute.Shortcut : ForkRoute.MainRoad;
        }
    }
}
