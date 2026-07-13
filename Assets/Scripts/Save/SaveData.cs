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
    public class SaveData
    {
        public int schemaVersion = 2;

        public int rewardPoints;
        public string selectedCharacterId = "";

        public List<PersistedCharacter> ownedCharacters = new List<PersistedCharacter>();

        /// <summary>ballId → quantity (-1 = unlimited)</summary>
        public Dictionary<string, int> ballQuantities = new Dictionary<string, int>();

        /// <summary>itemId → quantity</summary>
        public Dictionary<string, int> itemQuantities = new Dictionary<string, int>();

        /// <summary>Serialized as List for JSON compatibility; hydrated to HashSet by SaveDataHost.</summary>
        public List<int> unlockedHoles = new List<int>();

        public List<int> playedHoles = new List<int>();

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

        // ── Gacha tickets (schema v7, gacha_screen Stage 1) ──────────────────
        // Currency for gacha pulls. Migration v6→v7 seeds 10 for test grant;
        // TODO: revert migration grant to 0 before ship.
        // GachaTicketManager is the runtime read-through facade.
        public int gachaTickets;
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

        // ── Stamina condition (schema v4, Phase 2) ────────────────────────────
        // conditionEnergy: current Condition pool (float). Defaults 0f; empty
        //   conditionUpdatedUtc causes hydration to treat it as full (fresh/pre-v4).
        // conditionUpdatedUtc: ISO-8601 UTC string of last authoritative write.
        //   "" (empty) = never written; matches the tournament-DTO string-date convention.
        public float  conditionEnergy     = 0f;
        public string conditionUpdatedUtc = "";
    }
}
