namespace RacingGame
{
    /// <summary>Something the <see cref="SimulationDriver"/> steps once per fixed tick, in <see cref="SimulationOrder"/> order.</summary>
    public interface ISimulated
    {
        int SimulationOrder { get; }
        void SimulationStep(int tick);
    }
}
