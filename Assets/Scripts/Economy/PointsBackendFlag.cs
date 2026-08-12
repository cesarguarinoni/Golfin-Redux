// Order: reward_points_backend Slice 1 — the kill switch. DEFAULT OFF; nothing calls the server while off.
using UnityEngine;

namespace Golfin.Economy
{
    /// <summary>
    /// <c>PointsBackendEnabled</c> (SPEC §4 Slice 1). While this is OFF the game is behaviourally
    /// identical to HEAD: <see cref="PointsService"/> makes no HTTP call, enqueues nothing, creates no
    /// GameObject and writes no file. That is the acceptance bar for this slice, so the gate lives here
    /// and is asserted directly by the EditMode tests.
    ///
    /// Resolution order (first hit wins):
    ///   1. an explicit runtime <see cref="Enabled"/> setter (debug UI, editor menu, a smoke harness);
    ///   2. the <c>GOLFIN_POINTS_BACKEND</c> scripting define — the switch for a DEVICE build, since
    ///      PlayerPrefs set in the Editor do not travel to the phone;
    ///   3. <see cref="DefaultEnabled"/> = false.
    ///
    /// The runtime setter persists to PlayerPrefs so an Editor toggle survives domain reloads and play
    /// mode. Slice 2 flips <see cref="DefaultEnabled"/> as part of the cutover; until then this file is
    /// the ONLY thing standing between the game and the live ledger.
    /// </summary>
    public static class PointsBackendFlag
    {
        /// <summary>Ships OFF. Slice 2 flips this in the same commit as the RP rebalance.</summary>
        public const bool DefaultEnabled = false;

        private const string PrefKey = "golfin.points.backend.enabled";

        private static bool? _cached;

        public static bool Enabled
        {
            get
            {
                if (_cached.HasValue) return _cached.Value;

                int fallback = CompiledDefault ? 1 : 0;
                _cached = PlayerPrefs.GetInt(PrefKey, fallback) == 1;
                return _cached.Value;
            }
            set
            {
                _cached = value;
                PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Debug.Log($"[PointsBackendFlag] PointsBackendEnabled = {value}.");
            }
        }

        /// <summary>What the flag resolves to with no stored override — define first, then the const.</summary>
        public static bool CompiledDefault
        {
            get
            {
#if GOLFIN_POINTS_BACKEND
                return true;
#else
                return DefaultEnabled;
#endif
            }
        }

        /// <summary>Drop any stored override and fall back to <see cref="CompiledDefault"/>.</summary>
        public static void ResetToDefault()
        {
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
            _cached = null;
        }

        /// <summary>Forget the cached read without touching PlayerPrefs (test isolation).</summary>
        public static void InvalidateCache() => _cached = null;
    }
}
