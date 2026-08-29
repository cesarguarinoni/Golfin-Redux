#nullable enable
using System.Collections.Generic;

namespace Golfin.Save
{
    // ── Tournament entry DTOs ──────────────────────────────────────────────────
    // Flat, mutable, parameterless (Newtonsoft-friendly). Primitives only — no
    // Golfin.Tournaments references (asmdef is one-way: Tournaments → Save).
    // EntryStatus stored as int to keep Golfin.Save dep-free; cast on the Tournaments side.
    // DateTimes stored as ISO-8601 strings (diff-friendly; Newtonsoft handles both
    // string and DateTime — string chosen to keep Save obviously primitive).

    /// <summary>
    /// Flat DTO for one persisted tournament entry.
    /// Mirrors Golfin.Tournaments.EntryState; no runtime-type refs.
    /// </summary>
    public class PersistedTournamentEntry
    {
        public string tournamentId = "";
        public string characterId  = "";            // locked at sign-up
        public List<PersistedHoleResult> perHole = new List<PersistedHoleResult>();
        public string startedUtc   = "";            // ISO-8601
        public string lastHoleUtc  = "";            // "" = none (maps to DateTime?)
        public int    status;                       // (int)EntryStatus — enum order frozen
        public bool   claimed;                      // persisted claim-once flag (D2 = (b))
        public PersistedCharacterSnapshot snapshot  = new PersistedCharacterSnapshot();

        // ── Stamina condition pool (schema v5, Phase 3) ───────────────────────
        // conditionRemaining: remaining condition points in the tournament pool.
        // Default -1f = sentinel "unseeded": treated as full = MaxCondition(snapshot.Stamina) on use.
        // Drains per hole (DrainForHole()); never regens within the event (D3 = NO).
        // Separate pool from PersistedCharacter.conditionEnergy (live/solo pool, Phase 2).
        public float conditionRemaining = -1f;
    }

    /// <summary>
    /// Flat DTO for one persisted hole result.
    /// Mirrors Golfin.Tournaments.HoleResult; inputLog intentionally omitted (D1).
    /// </summary>
    public class PersistedHoleResult
    {
        public string holeId       = "";
        public int    strokes;
        public float  timeSeconds;
        public string completedUtc = "";            // ISO-8601
        public int    rngSeed;                      // kept for future server re-sim (GDD §8)
        // inputLog intentionally omitted in v1 — see D1 (rngSeed only)
    }

    /// <summary>
    /// Flat DTO mirroring Golfin.Tournaments.CharacterSnapshot 1:1.
    /// Frozen character stats at tournament sign-up.
    /// stamina = the STAT ceiling, not energy.
    /// </summary>
    public class PersistedCharacterSnapshot
    {
        public string characterId = "";
        public int    level;
        public int    strength;
        public int    clubControl;
        public int    recovery;
        public int    stamina;                      // the STAT, not energy
    }


    /// <summary>
    /// Single canonical save record for all game-state data.
    /// Serialized to JSON via Newtonsoft.Json (supports Dictionary natively).
    /// schemaVersion is bumped whenever the shape of this class changes.
    ///
    /// IMPORTANT: Do NOT serialize PlayerCharacterData, PlayerBallData, etc. directly.
    /// Use the flat DTO types (PersistedCharacter, etc.) to decouple storage from runtime.
    /// </summary>
    /// <summary>
    /// One mission's progress, as the SERVER last reported it (missions_v1 §B4).
    /// </summary>
    [System.Serializable]
    public class PersistedMissionProgress
    {
        /// <summary>The `missions` catalog row id — the campaign number as text.</summary>
        public string missionId = "";

        /// <summary>How many times cleared. 0 with <see cref="attempts"/> &gt; 0 is a real
        /// state: tried and failed, which the card renders differently from never opened.</summary>
        public int clears;

        public int attempts;

        /// <summary>Lowest stroke count on a CLEARED attempt. 0 = none yet.</summary>
        public int bestStrokes;
    }

    public class SaveData
    {
        /// <summary>
        /// On-disk schema version. The default is the OLDEST version this class can be read back
        /// as — a legacy JSON with no schemaVersion key must still run the whole migration chain.
        /// A brand-new save must NOT use this default: create it with <see cref="CreateFresh"/> so
        /// it is stamped at the current version. (Before 2026-08-26 a fresh save was written to disk
        /// stamped v2; on the NEXT boot the migrator saw a "legacy" save and ran every migration
        /// against it — which is how new players were granted the Legendary Royal Swing wedge by the
        /// v8→v9 backfill.)
        /// </summary>
        public int schemaVersion = 2;

        /// <summary>
        /// A brand-new save, stamped at the CURRENT schema version. Every field on a fresh
        /// SaveData already holds its current-schema default, so there is nothing to migrate —
        /// stamping it here is what keeps the next load from re-running the migration chain and
        /// applying legacy-player backfills to a brand-new player.
        /// </summary>
        public static SaveData CreateFresh() =>
            new SaveData { schemaVersion = SaveSchemaMigrator.CurrentSchemaVersion };

        public int rewardPoints;
        public string selectedCharacterId = "";

        // starting_character_selection (schema v10)
        /// <summary>Set once when player picks their first character; gates NeedsStarter.</summary>
        public string starterCharacterId = "";

        public List<PersistedCharacter> ownedCharacters = new List<PersistedCharacter>();

        /// <summary>ballId → quantity (-1 = unlimited)</summary>
        public Dictionary<string, int> ballQuantities = new Dictionary<string, int>();

        /// <summary>itemId → quantity</summary>
        public Dictionary<string, int> itemQuantities = new Dictionary<string, int>();

        /// <summary>Serialized as List for JSON compatibility; hydrated to HashSet by SaveDataHost.</summary>
        public List<int> unlockedHoles = new List<int>();

        public List<int> playedHoles = new List<int>();

        // ── Mission progress (missions_v1 §B4) ───────────────────────────────
        /// <summary>
        /// A LOCAL MIRROR of `mission_progress` on the server, and nothing more.
        ///
        /// ⚠️ IT IS WRITTEN ONLY FROM A CLAIM RESPONSE, NEVER FROM LOCAL PLAY. That is the
        /// whole discipline: the server decides what a clear is worth and how many the player
        /// has, and this is a cache of what it said so the Mission Selection screen can render
        /// before the round trip lands. A client that incremented `clears` itself would be
        /// back to the pre-`golfin_shop_purchase` world — deciding its own economy — and the
        /// screen would show a first-clear reward the server was never going to pay.
        ///
        /// Absent in saves before schema v12; an empty list is the correct state for a player
        /// who has never played a mission, so no migration has to invent anything.
        /// </summary>
        public List<PersistedMissionProgress> missionProgress = new List<PersistedMissionProgress>();

        // ── Leaderboard: RP earned per rolling period (UTC) ──────────────────
        // lifetimeRpEarned is monotonic (never reset). rpDaily/rpWeekly/rpMonthly
        // are lazily reset on period rollover (see LeaderboardPeriodKey).
        public long lifetimeRpEarned;
        public long rpDaily;
        public long rpWeekly;
        public long rpMonthly;

        // Period keys the accumulators currently belong to (UTC).
        // dailyPeriodKey   = floor(utcUnixSeconds / 86400)
        // weeklyPeriodKey  = Monday-anchored ISO week number (year*53 + weekOfYear)
        // monthlyPeriodKey = year*12 + (month-1)
        public long dailyPeriodKey;
        public long weeklyPeriodKey;
        public long monthlyPeriodKey;

        // ── Tournament entries (schema v3, added by T5) ───────────────────────
        /// <summary>
        /// All persisted tournament entries. Written by SaveBackedEntryStore (Golfin.Tournaments).
        /// One entry per tournament the player has registered for.
        /// Added empty in the v2→v3 migration; absent in v1/v2 saves defaults to empty list on load.
        /// </summary>
        public List<PersistedTournamentEntry> tournamentEntries = new List<PersistedTournamentEntry>();

        // ── Club ownership (schema v6, Order 610 Phase A) ─────────────────────
        // Ownership = membership in this list. Written by ClubManager (Assembly-CSharp)
        // via the pure ClubOwnershipService. Before v6 every club was auto-owned at
        // runtime and nothing persisted; now clubs are gated, persisted and grantable.
        /// <summary>All clubs the player owns. Membership == ownership (clubs are unique, no stacking).</summary>
        public List<PersistedClub> ownedClubs = new List<PersistedClub>();

        /// <summary>
        /// True once the club-ownership layer has seeded this save (starter set on a fresh save,
        /// or grandfather-all on a migrated pre-v6 save). Gates the one-time seed so it never re-runs.
        /// </summary>
        public bool clubOwnershipSeeded;

        /// <summary>
        /// Migrator signal (D-A3 = grandfather-all): set true when a pre-v6 save that was never
        /// club-seeded is migrated, telling ClubManager to seed the FULL current club DB once
        /// (existing players keep their bag). A brand-new SaveData never runs Migrate(), so it
        /// stays false → ClubManager seeds only the starter set. Consumed + cleared on first seed.
        /// </summary>
        public bool grandfatherClubs;

        // ── Wedge backfill (schema v9, Order 761) ────────────────────────────
        /// <summary>
        /// Migrator signal (Order 761): set true by v8→v9 migration for any already-seeded save
        /// so ClubManager grants+equips the default-bag wedge on next load. A brand-new SaveData
        /// never runs Migrate(), so it stays false → wedge is seeded via DefaultBagIds instead.
        /// Consumed + cleared by ClubManager.InitializeClubs() on first load.
        /// </summary>
        public bool wedgeBackfillPending;

        // ── Gacha tickets (schema v7→v8) ─────────────────────────────────────
        // v7: single int gachaTickets (gacha_screen Stage 1).
        // v8: per-kind List<PersistedTicketBalance> ticketBalances (gacha_history Stage 1).
        // gachaTickets is RETAINED (Obsolete) so v7 JSON can be deserialized for the v7→v8
        // migration that reads it and moves the balance into ticketBalances.
        // After migration, gachaTickets is ignored at runtime; ticketBalances is canonical.
        // GachaTicketManager is the runtime read-through facade.

        [System.Obsolete("Use ticketBalances instead. Retained for v7→v8 migration read-through.")]
        public int gachaTickets;

        /// <summary>
        /// Per-kind ticket balances (schema v8, gacha_history Stage 1).
        /// Indexed by TicketType int value. Use GachaTicketManager for all reads/writes.
        /// Added empty in the v7→v8 migration; absent in pre-v8 saves defaults to empty list.
        /// </summary>
        public System.Collections.Generic.List<PersistedTicketBalance> ticketBalances
            = new System.Collections.Generic.List<PersistedTicketBalance>();

        // ── Admin grant ledger (schema v11, content_player_inventory Phase 4) ──
        /// <summary>
        /// Ids of every <c>golfin_pending_grants</c> row this save has already applied.
        ///
        /// <para>
        /// THE CLIENT HALF OF THE GRANT IDEMPOTENCY LOCK. The client applies a grant and THEN acks
        /// it, so there is a window — the app dies, the network drops — where a grant is applied and
        /// the server still lists it as pending. Without this ledger the next boot re-drains it and
        /// applies it again. (The other ordering, ack-then-apply, loses the grant outright in the
        /// same window, and a lost grant is worse than a redundant ack.)
        /// </para>
        /// <para>
        /// Append-only and unbounded by design: grants are an admin action measured in dozens per
        /// tester, not a per-session event, so there is nothing here worth pruning and pruning would
        /// re-open the double-apply window for whatever it pruned.
        /// </para>
        /// <para>
        /// Written by <c>Golfin.InventorySync.InventoryGrants.Apply</c>. Added empty in the v10→v11
        /// migration; absent in pre-v11 saves defaults to an empty list on load.
        /// </para>
        /// </summary>
        public System.Collections.Generic.List<string> appliedGrantIds
            = new System.Collections.Generic.List<string>();
    }

    /// <summary>
    /// Flat DTO for one persisted ticket balance (schema v8, gacha_history Stage 1).
    /// One entry per TicketType int value. ticketTypeInt mirrors (int)TicketType — enum order frozen,
    /// append-only. Golfin.Save cannot reference GolfinRedux.UI.Gacha (asmdef one-way), so we
    /// store the int and let GachaTicketManager cast at runtime.
    /// </summary>
    public class PersistedTicketBalance
    {
        /// <summary>Cast of TicketType enum value. Standard = 0.</summary>
        public int ticketTypeInt;
        public int balance;
    }

    /// <summary>
    /// Flat DTO for one persisted owned club.
    /// Mirrors the persisted subset of Golfin.Inventory.PlayerClubData (source of truth =
    /// Assets/Scripts/UI/Inventory/ClubData.cs); adding UI-only fields to PlayerClubData
    /// never causes migration pain here. No Golfin.Inventory refs — asmdef stays one-way.
    /// </summary>
    public class PersistedClub
    {
        public string clubId = "";
        public int    currentLevel;
        public int    currentDurability;
        public int    maxDurability;
        public int    equippedBagSlot;
        public int    totalSPEarned;
        public int    spentPower;
        public int    spentAccuracy;
        public int    spentLieResistance;
        public int    spentDurability;
    }

    /// <summary>
    /// Flat DTO for persisted per-character data.
    /// Mirrors the persisted subset of PlayerCharacterData; adding UI-only fields to
    /// PlayerCharacterData will never cause migration pain here.
    /// </summary>
    public class PersistedCharacter
    {
        public string characterId = "";
        public int currentLevel;
        public int spentStrength;
        public int spentClubControl;
        public int spentRecovery;
        public int spentStamina;
        public int totalSPEarned;
        public bool isSelected;

        // starting_character_selection (schema v10)
        /// <summary>True if player has been granted this character (owns it).</summary>
        public bool isOwned = false;

        // ── Stamina condition (schema v4, Phase 2) ────────────────────────────
        // conditionEnergy: current Condition pool (float). Defaults 0f; empty
        //   conditionUpdatedUtc causes hydration to treat it as full (fresh/pre-v4).
        // conditionUpdatedUtc: ISO-8601 UTC string of last authoritative write.
        //   "" (empty) = never written; matches the tournament-DTO string-date convention.
        public float  conditionEnergy     = 0f;
        public string conditionUpdatedUtc = "";
    }
}
