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
    }
}
