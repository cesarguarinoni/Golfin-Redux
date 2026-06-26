#nullable enable
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
        public const int CurrentSchemaVersion = 3;

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
