#nullable enable
using Golfin.Save;
using UnityEngine;

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

        private const int DEFAULT_STARTING_POINTS = 50000;

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

            // If SaveDataHost hasn't seeded rewardPoints yet (fresh save with no migration),
            // apply the default starting points.
            if (SaveDataHost.Instance == null)
            {
                Debug.LogError("[RewardPointsManager] SaveDataHost.Instance is null — check Script Execution Order.");
                return;
            }

            if (SaveDataHost.Instance.Data.rewardPoints == 0)
            {
                SaveDataHost.Instance.Data.rewardPoints = DEFAULT_STARTING_POINTS;
                SaveDataHost.Instance.MarkDirty();
            }

            Debug.Log($"[RewardPointsManager] Loaded {GetPoints()} points");
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
        /// </summary>
        public void EarnPoints(int amount)
        {
            if (amount < 0)
            {
                Debug.LogError($"[RewardPointsManager] Cannot earn negative amount: {amount}");
                return;
            }

            SaveDataHost.Instance.Data.rewardPoints += amount;
            SaveDataHost.Instance.MarkDirty();
            OnPointsChanged?.Invoke(GetPoints());

            Debug.Log($"[RewardPointsManager] Earned {amount}R, now have {GetPoints()}R");
        }

        /// <summary>
        /// Set points directly (for testing or rewards).
        /// Writes through to SaveData; fires OnPointsChanged.
        /// </summary>
        public void SetPoints(int amount)
        {
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

        /// <summary>Reset to default (for testing).</summary>
        public void ResetToDefault()
        {
            SaveDataHost.Instance.Data.rewardPoints = DEFAULT_STARTING_POINTS;
            SaveDataHost.Instance.MarkDirty();
            OnPointsChanged?.Invoke(GetPoints());
        }
    }
}
