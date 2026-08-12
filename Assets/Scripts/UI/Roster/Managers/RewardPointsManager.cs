#nullable enable
using Golfin.Economy;
using Golfin.Save;
using Golfin.UI.Rankings;
using UnityEngine;
using Golfin.Audio.Events;

namespace Golfin.Roster
{
    /// <summary>
    /// Manages player's Reward Points (R currency).
    /// Singleton pattern.
    ///
    /// Read-through facade over SaveDataHost.Data.rewardPoints.
    /// Mutations write through to SaveData and fire OnPointsChanged as before.
    ///
    /// PlayerPrefs write code has been removed (§ system refactors).
    /// Legacy PlayerPrefs read is handled once by SaveDataHost (one-time migration on first Awake
    /// when no save.json exists). RewardPointsManager never touches PlayerPrefs directly.
    /// </summary>
    public class RewardPointsManager : MonoBehaviour
    {
        public static RewardPointsManager Instance { get; private set; } = null!;

        /// <summary>
        /// Balance <see cref="ResetToDefault"/> restores. This is a DEV RESET VALUE, not a starting
        /// balance: the 50,000-point first-run seed was removed at the Slice-2 cutover (decision of
        /// record #6 — new accounts start at 0 RP and the server balance is authoritative), and the
        /// remaining figure is scaled to the post-rebalance economy, matching the debug panel's
        /// "Set 5k" preset. Reachable only with <c>PointsBackendEnabled</c> OFF.
        /// </summary>
        private const int DEBUG_RESET_POINTS = 5000;

        // Event for UI updates — same interface as before
        public event System.Action<int>? OnPointsChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (SaveDataHost.Instance == null)
            {
                Debug.LogError("[RewardPointsManager] SaveDataHost.Instance is null — check Script Execution Order.");
                return;
            }

            // NO FIRST-RUN SEED. The 50,000-point grant that used to live here was a testing
            // convenience and does NOT survive the Slice-2 cutover (SPEC decision of record #6): new
            // accounts start at 0 RP, and with PointsBackendEnabled ON the server ledger is
            // authoritative — the local save is a cache of it, not a source of truth. Test balances
            // are granted admin-side (Supabase / the dashboard points panel), never by the client.
            Debug.Log($"[RewardPointsManager] Loaded {GetPoints()} points (local cache)");
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null!;
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Get current reward points (read-through from SaveData).</summary>
        public int GetPoints()
        {
            if (SaveDataHost.Instance == null) return 0;
            return SaveDataHost.Instance.Data.rewardPoints;
        }

        /// <summary>Check if player can afford an amount.</summary>
        public bool CanAfford(int amount)
        {
            return GetPoints() >= amount;
        }

        /// <summary>
        /// Spend points. Returns true if successful.
        /// Writes through to SaveData; fires OnPointsChanged.
        /// </summary>
        public bool SpendPoints(int amount)
        {
            if (amount < 0)
            {
                Debug.LogError($"[RewardPointsManager] Cannot spend negative amount: {amount}");
                return false;
            }

            if (!CanAfford(amount))
            {
                Debug.LogWarning($"[RewardPointsManager] Cannot afford {amount}R (have {GetPoints()}R)");
                return false;
            }

            SaveDataHost.Instance.Data.rewardPoints -= amount;
            SaveDataHost.Instance.MarkDirty();
            OnPointsChanged?.Invoke(GetPoints());

            Debug.Log($"[RewardPointsManager] Spent {amount}R, now have {GetPoints()}R");
            return true;
        }

        /// <summary>
        /// Earn points. Writes through to SaveData; fires OnPointsChanged.
        /// Also accumulates into the leaderboard period buckets (Daily/Weekly/Monthly/Lifetime).
        /// SpendPoints does NOT touch these — earned ≠ balance.
        ///
        /// <paramref name="action"/> is a <see cref="PointsActions"/> id. With
        /// <c>PointsBackendEnabled</c> ON the earn is ALSO queued for the server ledger; the local
        /// balance, the leaderboard accumulators and the SFX above are unchanged either way — the
        /// queue reconciles afterwards rather than gating the payout on a round-trip (SPEC §4:
        /// queued earns, online-required spends).
        /// </summary>
        public void EarnPoints(int amount, string action)
        {
            if (string.IsNullOrEmpty(action))
            {
                Debug.LogError($"[RewardPointsManager] EarnPoints({amount}) called with no action — " +
                               "the server ledger cannot record it. Use EarnPointsLocalOnly for dev grants.");
            }

            if (!EarnLocal(amount)) return;

            EnqueueServerEarn(amount, action);
        }

        /// <summary>
        /// Dev-only grant that never reaches the server ledger. Callers MUST be flag-OFF-guarded —
        /// with the backend ON the server balance is authoritative and a local-only grant would be
        /// silently reverted by the next balance refresh.
        /// </summary>
        public void EarnPointsLocalOnly(int amount) => EarnLocal(amount);

        /// <summary>The unchanged local earn: save-data write, leaderboard accumulation, event, SFX.
        /// Returns false when the amount was rejected.</summary>
        private bool EarnLocal(int amount)
        {
            if (amount < 0)
            {
                Debug.LogError($"[RewardPointsManager] Cannot earn negative amount: {amount}");
                return false;
            }

            SaveDataHost.Instance.Data.rewardPoints += amount;

            // ── Leaderboard accumulation ───────────────────────────────────────
            AccumulateLeaderboardRp(amount);

            SaveDataHost.Instance.MarkDirty();
            OnPointsChanged?.Invoke(GetPoints());

            // Order 350: RP earn SFX (one event per earn).
            SfxBus.Play(SfxId.RpEarn);

            Debug.Log($"[RewardPointsManager] Earned {amount}R, now have {GetPoints()}R");
            return true;
        }

        /// <summary>
        /// Mirror the earn into the persistent pending-ops queue and kick a replay.
        ///
        /// Fire-and-forget on purpose: a failed send leaves the op at the head of the queue with its
        /// idempotency key intact, so the next replay (reconnect, next earn, next login) delivers it
        /// exactly once. Nothing here can fail the local earn that already happened.
        /// </summary>
        private static void EnqueueServerEarn(int amount, string action)
        {
            if (!PointsBackendFlag.Enabled) return;
            if (amount <= 0 || string.IsNullOrEmpty(action)) return;

            PointsService service = PointsService.Instance;
            if (service.EnqueueEarn(action, amount) == null) return;

            service.ReplayPendingAsync();
        }

        /// <summary>
        /// Lazily rolls over stale periods and adds amount to all leaderboard accumulators.
        /// Called from EarnPoints only (SpendPoints must NOT call this).
        /// </summary>
        private void AccumulateLeaderboardRp(int amount)
        {
            if (SaveDataHost.Instance == null) return;
            var data = SaveDataHost.Instance.Data;

            // Use the network-authoritative time provider if available; fall back to device UTC.
            System.DateTime utcNow = NetworkTimeProvider.Instance.UtcNow;

            // Daily rollover
            long currentDailyKey = LeaderboardPeriodKey.Daily(utcNow);
            if (data.dailyPeriodKey != currentDailyKey)
            {
                data.rpDaily = 0;
                data.dailyPeriodKey = currentDailyKey;
            }

            // Weekly rollover
            long currentWeeklyKey = LeaderboardPeriodKey.Weekly(utcNow);
            if (data.weeklyPeriodKey != currentWeeklyKey)
            {
                data.rpWeekly = 0;
                data.weeklyPeriodKey = currentWeeklyKey;
            }

            // Monthly rollover
            long currentMonthlyKey = LeaderboardPeriodKey.Monthly(utcNow);
            if (data.monthlyPeriodKey != currentMonthlyKey)
            {
                data.rpMonthly = 0;
                data.monthlyPeriodKey = currentMonthlyKey;
            }

            // Accumulate
            data.lifetimeRpEarned += amount;
            data.rpDaily          += amount;
            data.rpWeekly         += amount;
            data.rpMonthly        += amount;
        }

        /// <summary>
        /// Set points directly (for testing).
        /// Writes through to SaveData; fires OnPointsChanged.
        ///
        /// FLAG-OFF ONLY. With <c>PointsBackendEnabled</c> ON the server ledger is authoritative, so
        /// a local write here would be silently reverted by the next balance refresh while looking
        /// like it worked — worse than refusing. Real balances are granted admin-side.
        /// </summary>
        public void SetPoints(int amount)
        {
            if (!AllowLocalOverride("SetPoints")) return;

            if (amount < 0)
            {
                Debug.LogError($"[RewardPointsManager] Cannot set negative points: {amount}");
                return;
            }

            SaveDataHost.Instance.Data.rewardPoints = amount;
            SaveDataHost.Instance.MarkDirty();
            OnPointsChanged?.Invoke(GetPoints());

            Debug.Log($"[RewardPointsManager] Set points to {GetPoints()}R");
        }

        /// <summary>Reset to the dev reset value (for testing). FLAG-OFF ONLY — see <see cref="SetPoints"/>.</summary>
        public void ResetToDefault()
        {
            if (!AllowLocalOverride("ResetToDefault")) return;

            SaveDataHost.Instance.Data.rewardPoints = DEBUG_RESET_POINTS;
            SaveDataHost.Instance.MarkDirty();
            OnPointsChanged?.Invoke(GetPoints());
        }

        /// <summary>
        /// Gate for the local-balance override helpers. These are guarded rather than deleted (SPEC
        /// §4 Slice 2) so the offline/flag-OFF development loop keeps working unchanged.
        /// </summary>
        private static bool AllowLocalOverride(string caller)
        {
            if (!PointsBackendFlag.Enabled) return true;

            Debug.LogWarning($"[RewardPointsManager] {caller} ignored — PointsBackendEnabled is ON and the " +
                             "server balance is authoritative. Grant RP admin-side (Supabase / dashboard " +
                             "points panel), or turn the flag off for local-only testing.");
            return false;
        }
    }
}
