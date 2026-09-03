namespace RacingGame.Core.Sim
{
    /// <summary>Produces the <see cref="DriveCommand"/> for one car at a given simulation tick.</summary>
    public interface ICarInputSource
    {
        DriveCommand Sample(int tick);
    }
}
