#nullable enable
// Assets/Scripts/UI/Gacha/GachaTicketManager.cs
// gacha_history Stage 1 — §3a Currency Manager (per-kind API)
// Previously backed SaveData.gachaTickets (schema v7, single int).
// Now reads/writes SaveData.ticketBalances (schema v8, per-kind List).
// v7→v8 migration moves the old balance into ticketBalances[Standard].
// Test grant = 10 (Awake seeds if Standard balance == 0); TODO: revert to 0 before ship.

using System.Collections.Generic;
using Golfin.Save;
using UnityEngine;

namespace GolfinRedux.UI.Gacha
{
    /// <summary>
    /// Manages the player's gacha ticket balances (per TicketType).
    /// Read-through facade over SaveDataHost.Data.ticketBalances.
    /// Singleton; survives scene loads (DontDestroyOnLoad).
    /// </summary>
    public class GachaTicketManager : MonoBehaviour
    {
        public static GachaTicketManager Instance { get; private set; } = null!;

        /// <summary>Test grant for dev. TODO: revert to 0 before ship (see paired TODO in SaveSchemaMigrator v6→v7 and v7→v8).</summary>
        private const int DEFAULT_STARTING_TICKETS = 10;

        /// <summary>
        /// Fired whenever ANY ticket balance changes.
        /// Args: (TicketType kind, int newBalance).
        /// </summary>
        public event System.Action<TicketType, int>? OnTicketsChanged;

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

            // Migration (v7→v8) seeds Standard balance from gachaTickets value.
            // Fresh saves created after v8 have an empty ticketBalances list here →
            // apply the test grant so the counter shows 10 on first run.
            // TODO: remove this Awake guard when reverting the test grant to 0.
            //       ALSO revert the paired seeds in SaveSchemaMigrator.cs (v6→v7 and v7→v8 blocks).
            //       All three sites must be reverted together.
            var data = SaveDataHost.Instance.Data;
            data.ticketBalances ??= new List<PersistedTicketBalance>();

            var entry = FindOrCreate(data, TicketType.Standard);
            if (entry.balance == 0)
            {
                entry.balance = DEFAULT_STARTING_TICKETS; // TODO: revert to 0 before ship
                SaveDataHost.Instance.MarkDirty();
            }

            Debug.Log($"[GachaTicketManager] Loaded {GetTickets(TicketType.Standard)} Standard tickets.");
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null!;
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Current balance for the given ticket kind (read-through from SaveData).</summary>
        public int GetTickets(TicketType kind)
        {
            if (SaveDataHost.Instance == null) return 0;
            var data = SaveDataHost.Instance.Data;
            if (data.ticketBalances == null) return 0;
            foreach (var b in data.ticketBalances)
                if (b.ticketTypeInt == (int)kind) return b.balance;
            return 0;
        }

        /// <summary>Returns true when balance for kind >= amount.</summary>
        public bool CanAfford(TicketType kind, int amount) => GetTickets(kind) >= amount;

        /// <summary>
        /// Add tickets of the given kind. Fires OnTicketsChanged.
        /// Writes through to SaveData immediately.
        /// </summary>
        public void AddTickets(TicketType kind, int amount)
        {
            if (amount < 0)
            {
                Debug.LogError($"[GachaTicketManager] Cannot add negative amount: {amount}");
                return;
            }
            var entry = FindOrCreate(SaveDataHost.Instance!.Data, kind);
            entry.balance += amount;
            SaveDataHost.Instance.MarkDirty();
            OnTicketsChanged?.Invoke(kind, entry.balance);
            Debug.Log($"[GachaTicketManager] Added {amount} {kind} tickets → balance {entry.balance}");
        }

        /// <summary>
        /// Spend tickets of the given kind. Returns true if affordable; false if not.
        /// Stage 1 stubs do NOT call this — pull buttons just show "Coming soon".
        /// </summary>
        public bool SpendTickets(TicketType kind, int amount)
        {
            if (amount < 0)
            {
                Debug.LogError($"[GachaTicketManager] Cannot spend negative amount: {amount}");
                return false;
            }
            if (!CanAfford(kind, amount))
            {
                Debug.LogWarning($"[GachaTicketManager] Insufficient {kind} tickets (have {GetTickets(kind)}, need {amount})");
                return false;
            }
            var entry = FindOrCreate(SaveDataHost.Instance!.Data, kind);
            entry.balance -= amount;
            SaveDataHost.Instance.MarkDirty();
            OnTicketsChanged?.Invoke(kind, entry.balance);
            Debug.Log($"[GachaTicketManager] Spent {amount} {kind} tickets → balance {entry.balance}");
            return true;
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private static PersistedTicketBalance FindOrCreate(SaveData data, TicketType kind)
        {
            data.ticketBalances ??= new List<PersistedTicketBalance>();
            foreach (var b in data.ticketBalances)
                if (b.ticketTypeInt == (int)kind) return b;
            var newEntry = new PersistedTicketBalance { ticketTypeInt = (int)kind, balance = 0 };
            data.ticketBalances.Add(newEntry);
            return newEntry;
        }
    }
}
