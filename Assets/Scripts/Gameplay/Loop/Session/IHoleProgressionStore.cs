namespace Golfin.Gameplay.Session
{
    /// <summary>
    /// Read + write interface over per-hole progression state.
    /// Foundation #1 of Loop v2 (interface-first services).
    ///
    /// Default implementation is HoleProgressionStoreAdapter which delegates to
    /// HoleProgressionService.Instance. Swap the default in EditMode tests to
    /// inject a fake and verify progression writes without touching the singleton.
    /// </summary>
    public interface IHoleProgressionStore
    {
        bool IsUnlocked(int holeNumber);
        bool HasPlayed(int holeNumber);
        void MarkHolePlayed(int holeNumber);
        void UnlockHole(int holeNumber);
    }
}
