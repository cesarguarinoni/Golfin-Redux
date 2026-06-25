// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — HoleResult
// Per-hole result submitted to ITournamentBackend.SubmitHoleResult().
// MUST carry rngSeed + inputLog (GDD §8 anti-cheat, SPEC §0.3 forward-compat).
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;

namespace Golfin.Tournaments
{
    /// <summary>
    /// Result of one completed hole, submitted via
    /// <see cref="ITournamentBackend.SubmitHoleResult"/>.
    /// <para>
    /// Anti-cheat fields (<see cref="RngSeed"/>, <see cref="InputLog"/>):
    /// v1 writes them but never validates them. A future remote backend will
    /// re-simulate the deterministic physics from <c>rngSeed</c> + <c>inputLog</c>
    /// and reject mismatches (GDD §8). Designing the shape in now prevents a
    /// save-schema break when the server lands.
    /// </para>
    /// <para>
    /// Countback (GDD §6.1) reads <c>perHole[].Strokes</c> in the entry — no extra
    /// field needed beyond what is already here.
    /// </para>
    /// </summary>
    public sealed class HoleResult
    {
        /// <summary>
        /// Stable hole id (matches an entry in
        /// <see cref="TournamentDefinition.HoleSet"/>).
        /// </summary>
        public string HoleId { get; }

        /// <summary>Total strokes taken on this hole. Primary sort key (ascending).</summary>
        public int Strokes { get; }

        /// <summary>
        /// Wall-clock seconds from tee-off to hole-out on this hole.
        /// Used for the tiebreak ladder (GDD §6.1 step 3: total completion time).
        /// </summary>
        public float TimeSeconds { get; }

        /// <summary>UTC instant when the player completed this hole.</summary>
        public DateTime CompletedUtc { get; }

        // ── Anti-cheat (GDD §8, forward-compat) ─────────────────────────────

        /// <summary>
        /// RNG seed used for the deterministic physics simulation of this hole.
        /// v1 records it for future server re-simulation; v1 never validates.
        /// </summary>
        public int RngSeed { get; }

        /// <summary>
        /// Ordered list of shot commands that produced this hole result.
        /// v1 records it for future server verification; v1 never reads it.
        /// May be empty (cheap v1 implementation); never null.
        /// </summary>
        public IReadOnlyList<ShotCommand> InputLog { get; }

        public HoleResult(
            string holeId,
            int strokes,
            float timeSeconds,
            DateTime completedUtc,
            int rngSeed,
            IReadOnlyList<ShotCommand> inputLog)
        {
            HoleId       = holeId;
            Strokes      = strokes;
            TimeSeconds  = timeSeconds;
            CompletedUtc = completedUtc;
            RngSeed      = rngSeed;
            InputLog     = inputLog ?? new List<ShotCommand>();
        }
    }
}
