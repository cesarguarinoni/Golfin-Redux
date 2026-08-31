#nullable enable
// Assets/Scripts/UI/Gacha/GachaTicketManager.cs
// gacha_history Stage 1 — §3a Currency Manager (per-kind API)
// gacha_client_real_pull §4.4 — THE SERVER LEDGER IS THE TRUTH.
//
// SaveData.ticketBalances is no longer a balance: it is a DISPLAY CACHE of the last value the
// server reported, kept only so the counter has something to draw before /gacha/tickets answers.
// Three things went with that change and all three are deliberate:
//   • SpendTickets is DELETED. A pull is debited by golfin_gacha_pull() inside the same
//     transaction that rolls it; a client that could also decrement would be a second, unauthored
//     ledger, and the two would drift on the first failed request.
//   • The dev grant of 10 is GONE from all three sites (here and both SaveSchemaMigrator blocks).
//     The ledger starts at 0 for everyone by decision (plan §9) — a client that seeds itself
//     tickets shows a balance the server will refuse to spend.
//   • The blob no longer PROJECTS tickets (InventoryProjector), so the additive max-merge cannot
//     resurrect a pre-spend balance the way it would for a stack of balls.
// AddTickets stays because the grants queue still applies old queued ticket rows; nothing new
// calls it, and after such a drain the counter is re-read from the ledger anyway.

using System.Collections.Generic;
using Golfin.Economy;
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

            // NO SEED. The dev grant of 10 that used to live here (and in both SaveSchemaMigrator
            // blocks) is gone: the ledger is the truth and it starts at 0 for every player, so a
            // client that granted itself ten would show a balance /gacha/pull refuses to spend.
            var data = SaveDataHost.Instance.Data;
            data.ticketBalances ??= new List<PersistedTicketBalance>();

            Debug.Log($"[GachaTicketManager] Last known Standard balance: " +
                      $"{GetTickets(TicketType.Standard)} (display cache — the server ledger is the truth).");

            // A drain that applied a queued ticket grant wrote a GUESS into the cache above. Re-read
            // the ledger so the counter converges on it rather than on that arithmetic (§4.4).
            Golfin.InventorySync.InventorySyncService.OnTicketGrantsApplied += OnTicketGrantsApplied;

            // The ledger read is hung off AUTH, not off Awake, and the pair below is the same one
            // ServerBalanceSyncBehaviour uses for the RP balance: SignedIn covers a fresh login,
            // and the Start() check covers a RETURNING player, who is already authenticated from
            // the saved session and for whom SignedIn will never fire.
            Golfin.Auth.AuthService.SignedIn += OnSignedIn;
        }

        private void Start()
        {
            if (Golfin.Auth.AuthService.Instance.Session.IsAuthenticated) RefreshFromServer();
        }

        private void OnSignedIn(Golfin.Auth.AuthSession session) => RefreshFromServer();

        private void OnDestroy()
        {
            Golfin.InventorySync.InventorySyncService.OnTicketGrantsApplied -= OnTicketGrantsApplied;
            Golfin.Auth.AuthService.SignedIn -= OnSignedIn;

            if (Instance == this)
                Instance = null!;
        }

        private void OnTicketGrantsApplied(int count)
        {
            Debug.Log($"[GachaTicketManager] {count} ticket grant(s) applied by a drain — re-reading " +
                      "the ledger so the counter converges on it.");
            RefreshFromServer();
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
        /// Record the balance the SERVER just reported for one ticket type
        /// (gacha_client_real_pull §4.4).
        ///
        /// <para>
        /// This is the only way a balance goes DOWN. It writes the server's number verbatim — it
        /// never subtracts, never clamps and never compares against what the client thought it had,
        /// because every one of those would be the client having an opinion about a ledger it does
        /// not own.
        /// </para>
        /// </summary>
        public void SetFromServer(int ticketTypeInt, int balance)
        {
            if (SaveDataHost.Instance == null) return;

            if (balance < 0)
            {
                Debug.LogError($"[GachaTicketManager] Server reported a negative balance ({balance}) " +
                               $"for ticket type {ticketTypeInt} — ignored.");
                return;
            }

            var entry = FindOrCreate(SaveDataHost.Instance.Data, (TicketType)ticketTypeInt);
            if (entry.balance == balance) return;   // no event, no disk write

            int before = entry.balance;
            entry.balance = balance;
            SaveDataHost.Instance.MarkDirty();
            OnTicketsChanged?.Invoke((TicketType)ticketTypeInt, balance);

            Debug.Log($"[GachaTicketManager] Ticket type {ticketTypeInt}: {before} → {balance} (server).");
        }

        /// <summary>
        /// Re-read every balance from <c>GET /gacha/tickets</c>.
        ///
        /// <para>
        /// AN ABSENT TYPE IS A REAL ZERO. The server answers by omission (a player who has never
        /// held a Gold ticket has no row), so every published type this build knows is set — to the
        /// server's number when it sent one, and to 0 when it did not. Setting only the types that
        /// came back would leave a stale balance on screen forever after a spend took one to zero.
        /// </para>
        /// <para>
        /// A FAILED READ CHANGES NOTHING. The service hands back null rather than an empty page on
        /// a timeout, precisely so an offline launch does not zero a real balance.
        /// </para>
        /// </summary>
        public void RefreshFromServer(System.Action? done = null)
        {
            GachaPullService.Instance.FetchTicketsAsync(page =>
            {
                if (page == null) { done?.Invoke(); return; }

                var reported = new Dictionary<int, int>();
                if (page.Balances != null)
                    foreach (var b in page.Balances)
                        if (b != null) reported[b.TicketType] = b.Balance;

                foreach (var type in TicketTypeCatalog.All)
                    SetFromServer(type.Id, reported.TryGetValue(type.Id, out int v) ? v : 0);

                // A type the server reports that this build has no published row for still moves
                // the save: the player holds it, and a later build will render it.
                foreach (var pair in reported)
                    if (TicketTypeCatalog.Get(pair.Key) == null) SetFromServer(pair.Key, pair.Value);

                done?.Invoke();
            });
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
