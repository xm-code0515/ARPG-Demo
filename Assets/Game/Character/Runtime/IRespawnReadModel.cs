namespace Game.Character
{
    public interface IRespawnReadModel
    {
        bool IsDead { get; }
        float RemainingSeconds { get; }
    }
}
