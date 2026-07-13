#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Golfin.Save
{
    /// <summary>
    /// Handles schema version migrations on load.
    ///
    /// Q-LOCK (§4 Q2): fail-hard if schemaVersion in file > schemaVersion in code.
    /// This prevents silent data loss from a future downgrade scenario.
    ///
    /// Migration path: v1 → v2 (etc.) are functions here. No version is ever removed.
    /// Reading older saves always works. Reading a newer save than the code understands = exception.
    /// </summary>
    public static class SaveSchemaMigrator
    {
        public const int CurrentSchemaVersion = 7;

        /// <summary>
        /// Apply any needed migrations to bring data from its on-disk schemaVersion
        /// up to CurrentSchemaVersion. Mutates data in-place.
        ///
        /// Throws SaveSchemaVersionException if file version > code version (Q-LOCK §4 Q2).
        /// </summary>
        public static void Migrate(SaveData data)
        {
            if (data.schemaVersion > CurrentSchemaVersion)
            {
                string msg = $"[SaveSchemaMigrator] Save file has schema version {data.schemaVersion} " +
                             $"but this build only understands version {CurrentSchemaVersion}. " +
                             $"Please update the game to load this save.";
                Debug.LogError(msg);
                throw new SaveSchemaVersionException(msg);
            }

            // v1 → v2: add leaderboard RP accumulators (default 0 is correct for new period)
            if (data.schemaVersion < 2)
            {
                // New fields default to 0 on JSON deserialization — no action needed.
                // lifetimeRpEarned, rpDaily, rpWeekly, rpMonthly,
                // dailyPeriodKey, weeklyPeriodKey, monthlyPeriodKey all default 0.
                data.schemaVersion = 2;
                Debug.Log("[SaveSchemaMigrator] Migrated v1 → v2 (leaderboard RP accumulators added, default 0).");
            }

            // v2 → v3: add tournament entries list (empty for pre-tournament saves).
            // The PersistedCharacterSnapshot field inside each entry is part of this same bump —
            // the frozen snapshot ships inside this single v2→v3 migration (no separate v3→v4).
            if (data.schemaVersion < 3)
            {
                // Defensive null-init: Newtonsoft leaves missing JSON key at field default (new List),
                // but guard against any edge-case deserializer that produces null.
                data.tournamentEntries ??= new System.Collections.Generic.List<PersistedTournamentEntry>();
                data.schemaVersion = 3;
                Debug.Log("[SaveSchemaMigrator] Migrated v2 → v3 (tournament entries list added, default empty).");
            }

            // v3 → v4: add conditionEnergy + conditionUpdatedUtc per character (Phase 2 stamina).
            // No data transform needed: new fields default 0f / "" which the hydration path
            // treats as "fresh / never written" → full pool on next load. Backward-safe.
            if (data.schemaVersion < 4)
            {
                // New fields default-initialize correctly via field initializers in PersistedCharacter.
                // No action needed beyond bumping the version.
                data.schemaVersion = 4;
                Debug.Log("[SaveSchemaMigrator] Migrated v3 → v4 (conditionEnergy + conditionUpdatedUtc added per character, default 0f/empty).");
            }

            // v4 → v5: add conditionRemaining per tournament entry (Phase 3 stamina).
            // No data transform needed: new field defaults to -1f (sentinel = "unseeded"),
            // which callers hydrate to full = MaxCondition(snapshot.Stamina) on use (D2).
            // The live/solo PersistedCharacter.conditionEnergy (Phase 2) is untouched.
            if (data.schemaVersion < 5)
            {
                // New field defaults correctly via field initializer in PersistedTournamentEntry.
                // No action needed beyond bumping the version.
                data.schemaVersion = 5;
                Debug.Log("[SaveSchemaMigrator] Migrated v4 → v5 (conditionRemaining added per tournament entry, default -1f sentinel).");
            }

            // v5 → v6: add club ownership (Order 610 Phase A).
            // D-A3 = grandfather-all: an existing pre-v6 save that was never club-seeded gets the
            // grandfatherClubs signal so ClubManager seeds the FULL current club DB once on next load
            // (nobody loses their bag). A brand-new SaveData() is never passed to Migrate() (SaveDataHost
            // only migrates loaded files), so it never reaches here → stays unseeded/non-grandfather →
            // ClubManager seeds only the starter set. clubOwnershipSeeded gates the grandfather so an
            // already-seeded save (fresh player, launch 2+) is left untouched.
            if (data.schemaVersion < 6)
            {
                data.ownedClubs ??= new List<PersistedClub>();
                if (!data.clubOwnershipSeeded)
                    data.grandfatherClubs = true;
                data.schemaVersion = 6;
                Debug.Log("[SaveSchemaMigrator] Migrated v5 → v6 (club ownership added; " +
                          $"grandfatherClubs={data.grandfatherClubs} for unseeded existing save).");
            }

            // v6 → v7: add gacha tickets (gacha_screen Stage 1).
            // Test grant = 10 so dev can exercise the counter and pull stubs immediately.
            // Applies to both existing saves (migration path) and effectively to fresh saves
            // (GachaTicketManager.Awake seeds 10 if gachaTickets == 0 after first load).
            // TODO: revert test grant to 0 before ship.
            //       ALSO revert the paired seed in GachaTicketManager.Awake
            //       (the `gachaTickets == 0 → DEFAULT_STARTING_TICKETS` guard, ~line 51).
            //       Both sites must be reverted together — reverting only one leaves
            //       emptied balances silently refilling to 10.
            if (data.schemaVersion < 7)
            {
                data.gachaTickets = 10; // TODO: revert to 0 before ship (test grant only)
                data.schemaVersion = 7;
                Debug.Log("[SaveSchemaMigrator] Migrated v6 → v7 (gachaTickets seeded to 10 — test grant).");
            }

            // Ensure schemaVersion is current after all migrations
            data.schemaVersion = CurrentSchemaVersion;
        }
    }

    /// <summary>
    /// Thrown when the save file has a schema version newer than the current code.
    /// Consumers (SaveDataHost) catch this and show an error / use defaults.
    /// </summary>
    public class SaveSchemaVersionException : System.Exception
    {
        public SaveSchemaVersionException(string message) : base(message) { }
    }
}
