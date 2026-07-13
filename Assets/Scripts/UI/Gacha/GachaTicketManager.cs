#nullable enable
// Assets/Scripts/UI/Gacha/GachaTicketManager.cs
// gacha_screen Stage 1 — §3a Currency Manager
// Mirrors RewardPointsManager exactly (DontDestroyOnLoad singleton, read-through SaveData facade).
// Backed by SaveData.gachaTickets (schema v7).
// Test grant = 10 (Awake seeds if 0); TODO: revert to 0 before ship.

using Golfin.Save;
using UnityEngine;

namespace GolfinRedux.UI.Gacha
{
    /// <summary>
    /// Manages the player's gacha ticket balance.
    /// Read-through facade over SaveDataHost.Data.gachaTickets.
    /// Singleton; survives scene loads (DontDestroyOnLoad).
    /// </summary>
    public class GachaTicketManager : MonoBehaviour
    {
        public static GachaTicketManager Instance { get; private set; } = null!;

        /// <summary>Test grant so the counter and pull stubs are exercisable in dev. TODO: revert to 0 before ship.</summary>
        private const int DEFAULT_STARTING_TICKETS = 10;

        /// <summary>Fired whenever the ticket balance changes. Arg = new balance.</summary>
        public event System.Action<int>? OnTicketsChanged;

        // ── Lifecycle ──────────────────────────────────────────────────────────

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
                Debug.LogError("[GachaTicketManager] SaveDataHost.Instance is null — check Script Execution Order.");
                return;
            }

            // Migration (v6→v7) seeds gachaTickets=10 for existing saves.
            // Fresh saves that were created after v7 have gachaTickets=0 here →
            // apply the test grant so the counter shows 10 on first run.
            // TODO: remove this Awake guard when reverting the test grant to 0.
            //       ALSO revert the paired seed in SaveSchemaMigrator.cs (v6→v7 block,
            //       `data.gachaTickets = 10`). Both sites must be reverted together —
            //       reverting only one leaves emptied balances silently refilling to 10.
            if (SaveDataHost.Instance.Data.gachaTickets == 0)
            {
                SaveDataHost.Instance.Data.gachaTickets = DEFAULT_STARTING_TICKETS;
                SaveDataHost.Instance.MarkDirty();
            }

            Debug.Log($"[GachaTicketManager] Loaded {GetTickets()} tickets.");
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null!;
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Current ticket balance (read-through from SaveData).</summary>
        public int GetTickets()
        {
            if (SaveDataHost.Instance == null) return 0;
            return SaveDataHost.Instance.Data.gachaTickets;
        }

        /// <summary>Returns true when balance >= amount.</summary>
        public bool CanAfford(int amount) => GetTickets() >= amount;

        /// <summary>
        /// Add tickets. Fires OnTicketsChanged.
        /// Writes through to SaveData immediately.
        /// </summary>
        public void AddTickets(int amount)
        {
            if (amount < 0)
            {
                Debug.LogError($"[GachaTicketManager] Cannot add negative amount: {amount}");
                return;
            }
            SaveDataHost.Instance!.Data.gachaTickets += amount;
            SaveDataHost.Instance.MarkDirty();
            OnTicketsChanged?.Invoke(GetTickets());
            Debug.Log($"[GachaTicketManager] Added {amount} tickets → balance {GetTickets()}");
        }

        /// <summary>
        /// Spend tickets. Returns true if affordable; returns false and leaves balance unchanged otherwise.
        /// Stage 1 stubs do NOT call this — pull buttons just show "Coming soon".
        /// </summary>
        public bool SpendTickets(int amount)
        {
            if (amount < 0)
            {
                Debug.LogError($"[GachaTicketManager] Cannot spend negative amount: {amount}");
                return false;
            }
            if (!CanAfford(amount))
            {
                Debug.LogWarning($"[GachaTicketManager] Insufficient tickets (have {GetTickets()}, need {amount})");
                return false;
            }
            SaveDataHost.Instance!.Data.gachaTickets -= amount;
            SaveDataHost.Instance.MarkDirty();
            OnTicketsChanged?.Invoke(GetTickets());
            Debug.Log($"[GachaTicketManager] Spent {amount} tickets → balance {GetTickets()}");
            return true;
        }
    }
}
