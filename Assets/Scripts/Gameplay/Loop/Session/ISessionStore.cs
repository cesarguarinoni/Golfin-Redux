namespace Golfin.Gameplay.Session
{
    /// <summary>
    /// Read-only view over session state. Foundation #1 of Loop v2 (interface-first
    /// services). Stage B writers continue to use GameSession's static API; readers
    /// that want testability go through this interface. Implementation in Stage B
    /// is a thin static-bus wrapper; future replay/headless impls swap in here.
    /// </summary>
    public interface ISessionStore
    {
        int    CurrentHoleNumber     { get; }
        string SelectedCharacterId   { get; }
        int    EquippedBagSlot       { get; }
        int    TurnCount             { get; }
    }

    /// <summary>
    /// Default ISessionStore impl: thin wrapper over GameSession's static API.
    /// Useful for DI sites that want to swap in a replay/headless impl later.
    /// </summary>
    public sealed class GameSessionStore : ISessionStore
    {
        public int    CurrentHoleNumber   => GameSession.CurrentHoleNumber;
        public string SelectedCharacterId => GameSession.SelectedCharacterId;
        public int    EquippedBagSlot     => GameSession.EquippedBagSlot;
        public int    TurnCount           => GameSession.TurnCount;
    }
}
