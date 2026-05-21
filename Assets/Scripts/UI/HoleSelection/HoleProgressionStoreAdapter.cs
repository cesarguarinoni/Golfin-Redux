using GolfinRedux.UI.HoleSelection;

namespace Golfin.Gameplay.Session
{
    /// <summary>
    /// Default IHoleProgressionStore: thin adapter over HoleProgressionService.Instance.
    /// Ships in Stage C1. Future save-layer milestone replaces the backing service
    /// without changing any caller.
    ///
    /// Placed in Assets/Scripts/UI/HoleSelection/ (Assembly-CSharp) so it can reference
    /// both HoleProgressionService (Assembly-CSharp) and implement the interface in
    /// Golfin.Gameplay.Session namespace (also Assembly-CSharp for classes outside asmdef dirs).
    /// </summary>
    public class HoleProgressionStoreAdapter : IHoleProgressionStore
    {
        public static readonly HoleProgressionStoreAdapter Default = new HoleProgressionStoreAdapter();

        readonly HoleProgressionService _svc = HoleProgressionService.Instance;

        public bool IsUnlocked(int holeNumber)    => _svc.IsUnlocked(holeNumber);
        public bool HasPlayed(int holeNumber)      => _svc.HasPlayed(holeNumber);
        public void MarkHolePlayed(int holeNumber) => _svc.SetPlayedOverride(holeNumber, true);
        public void UnlockHole(int holeNumber)     => _svc.SetUnlockedOverride(holeNumber, true);
    }
}
