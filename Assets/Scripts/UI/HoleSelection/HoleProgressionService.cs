using System.Collections.Generic;

namespace GolfinRedux.UI.HoleSelection
{
    /// <summary>
    /// Per-hole unlock + played state. POCO singleton — no MonoBehaviour, no DontDestroyOnLoad.
    /// In this task the only writers are the inspector debug component (HoleProgressionDebug)
    /// and tests. When real save state lands (Loop v2), this service becomes the read API
    /// over the save layer; nothing else changes for callers.
    /// </summary>
    public class HoleProgressionService
    {
        private static HoleProgressionService _instance;
        public static HoleProgressionService Instance => _instance ?? (_instance = new HoleProgressionService());

        private readonly Dictionary<int, bool> _unlockOverrides = new Dictionary<int, bool>();
        private readonly Dictionary<int, bool> _playedOverrides = new Dictionary<int, bool>();

        public bool IsUnlocked(int holeNumber)
        {
            if (_unlockOverrides.TryGetValue(holeNumber, out var v)) return v;
            return holeNumber == 1; // default: only Hole 1
        }

        public bool HasPlayed(int holeNumber)
        {
            return _playedOverrides.TryGetValue(holeNumber, out var v) && v;
        }

        public void SetUnlockedOverride(int holeNumber, bool unlocked) => _unlockOverrides[holeNumber] = unlocked;
        public void SetPlayedOverride(int holeNumber, bool played)     => _playedOverrides[holeNumber] = played;
    }
}
