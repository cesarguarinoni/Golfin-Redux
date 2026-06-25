// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — ShotCommand
// Minimal shot-log entry for anti-cheat forward-compat (GDD §8).
// v1 never reads it; a future server re-simulates the deterministic sim from
// the log and rejects mismatches. Shape is stable now to avoid a save-schema
// break later. SPEC §7 flag resolved: minimal ShotCommand struct.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable

namespace Golfin.Tournaments
{
    /// <summary>
    /// One shot recorded in a round for anti-cheat verification (GDD §8).
    /// v1 writes these into <see cref="HoleResult.InputLog"/> but never validates them.
    /// A future remote backend re-simulates from the log and can reject mismatches.
    /// </summary>
    public readonly struct ShotCommand
    {
        /// <summary>Index of the shot within the hole (0-based).</summary>
        public int ShotIndex { get; }

        /// <summary>
        /// Power value at the moment of commit (normalised 0–1).
        /// Maps to the shot-control power input.
        /// </summary>
        public float Power { get; }

        /// <summary>
        /// Accuracy value at the moment of commit (normalised –1..+1, 0 = perfect).
        /// </summary>
        public float Accuracy { get; }

        /// <summary>
        /// Club type identifier used for this shot (matches Golfin.Inventory club id).
        /// </summary>
        public string ClubId { get; }

        /// <summary>
        /// Device UTC timestamp at the moment the shot was committed.
        /// Used alongside rngSeed for server re-simulation.
        /// </summary>
        public System.DateTime CommittedUtc { get; }

        public ShotCommand(int shotIndex, float power, float accuracy, string clubId, System.DateTime committedUtc)
        {
            ShotIndex    = shotIndex;
            Power        = power;
            Accuracy     = accuracy;
            ClubId       = clubId;
            CommittedUtc = committedUtc;
        }
    }
}
