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
        public const int CurrentSchemaVersion = 1;

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

            // v1 → v2 migration would go here:
            // if (data.schemaVersion < 2) { MigrateV1ToV2(data); data.schemaVersion = 2; }

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
