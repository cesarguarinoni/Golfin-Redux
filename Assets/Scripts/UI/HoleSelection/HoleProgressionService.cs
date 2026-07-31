#nullable enable
using System.Collections.Generic;
using Golfin.Save;

namespace GolfinRedux.UI.HoleSelection
{
    /// <summary>
    /// Per-hole unlock + played state. POCO singleton facade over SaveData.unlockedHoles / playedHoles.
    ///
    /// v2 implementation: reads/writes through SaveDataHost.Data when available.
    /// Falls back to in-memory dicts when SaveDataHost is absent (EditMode tests, pre-SaveLayer builds).
    ///
    /// This makes the Stage E REPLAY fix (OnReplay writes progression) persist across app restarts:
    /// hole unlocks now survive the next launch because they're written to save.json.
    /// </summary>
    public class HoleProgressionService
    {
        private static HoleProgressionService _instance = null!;
        public static HoleProgressionService Instance => _instance ?? (_instance = new HoleProgressionService());

        // In-memory fallback dicts (used when SaveDataHost is absent)
        private readonly Dictionary<int, bool> _unlockOverrides  = new Dictionary<int, bool>();
        private readonly Dictionary<int, bool> _playedOverrides   = new Dictionary<int, bool>();

        // ── Read API ──────────────────────────────────────────────────────────

        public bool IsUnlocked(int holeNumber)
        {
            // demo_build_slice §3.4: only Hole 1 is unlocked in the demo, regardless of save
            // state — and completing it never unlocks another (see HoleCompleteModalController).
            if (GolfinRedux.Demo.DemoGate.IsDemo)
                return holeNumber == 1;

            // SaveData is the authoritative source when available
            if (SaveDataHost.Instance != null)
            {
                var data = SaveDataHost.Instance.Data;
                return data.unlockedHoles.Contains(holeNumber);
            }

            // Fallback: in-memory overrides
            if (_unlockOverrides.TryGetValue(holeNumber, out var v)) return v;
            return holeNumber == 1; // default: only Hole 1
        }

        public bool HasPlayed(int holeNumber)
        {
            if (SaveDataHost.Instance != null)
                return SaveDataHost.Instance.Data.playedHoles.Contains(holeNumber);

            return _playedOverrides.TryGetValue(holeNumber, out var v) && v;
        }

        // ── Write API ─────────────────────────────────────────────────────────

        public void SetUnlockedOverride(int holeNumber, bool unlocked)
        {
            if (SaveDataHost.Instance != null)
            {
                var data = SaveDataHost.Instance.Data;
                if (unlocked)
                {
                    if (!data.unlockedHoles.Contains(holeNumber))
                    {
                        data.unlockedHoles.Add(holeNumber);
                        SaveDataHost.Instance.MarkDirty();
                    }
                }
                else
                {
                    if (data.unlockedHoles.Remove(holeNumber))
                        SaveDataHost.Instance.MarkDirty();
                }
                return;
            }

            // Fallback
            _unlockOverrides[holeNumber] = unlocked;
        }

        public void SetPlayedOverride(int holeNumber, bool played)
        {
            if (SaveDataHost.Instance != null)
            {
                var data = SaveDataHost.Instance.Data;
                if (played)
                {
                    if (!data.playedHoles.Contains(holeNumber))
                    {
                        data.playedHoles.Add(holeNumber);
                        SaveDataHost.Instance.MarkDirty();
                    }
                }
                else
                {
                    if (data.playedHoles.Remove(holeNumber))
                        SaveDataHost.Instance.MarkDirty();
                }
                return;
            }

            // Fallback
            _playedOverrides[holeNumber] = played;
        }
    }
}
