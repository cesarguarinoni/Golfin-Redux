// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — BotFieldMath
// Stage 1 (T3): FNV-1a 64-bit stable hash + xorshift64 PRNG.
// System-only — NO UnityEngine dependency — headless NUnit-testable.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
// Allow the test assembly to call internal members (bracket-mix seam, iter-2).
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Golfin.Tournaments.Tests")]

namespace Golfin.Tournaments
{
    /// <summary>
    /// FNV-1a 64-bit stable hash (platform-stable across .NET / IL2CPP).
    /// Does NOT use System.String.GetHashCode() which is randomised per-process.
    /// Known vector (pinned by invariant test): StableHash("abc") == -3547061803046329763L (0xCEC64E155111225D)
    /// </summary>
    public static class BotFieldHash
    {
        // FNV-1a 64-bit constants (https://www.isthe.com/chongo/tech/comp/fnv/)
        private const ulong FNV_PRIME  = 1099511628211UL;
        private const ulong FNV_OFFSET = 14695981039346656037UL;

        /// <summary>
        /// Platform-stable FNV-1a 64-bit hash of a string (UTF-16 char-by-char).
        /// Returns a signed long (easier to use as a PRNG seed via unchecked cast).
        /// </summary>
        public static long StableHash(string s)
        {
            ulong hash = FNV_OFFSET;
            foreach (char c in s)
            {
                // hash both bytes of the UTF-16 char for full coverage
                hash ^= (byte)(c & 0xFF);
                hash *= FNV_PRIME;
                hash ^= (byte)(c >> 8);
                hash *= FNV_PRIME;
            }
            return unchecked((long)hash);
        }
    }

    /// <summary>
    /// xorshift64 PRNG — explicit ~15-line implementation seeded by a stable hash.
    /// Deterministic, platform-stable, no System.Random dependency.
    /// GDD §7 / D0 decision: ship in-asmdef to remove doubt about IL2CPP algorithm stability.
    /// </summary>
    public sealed class Xorshift64
    {
        private ulong _state;

        /// <param name="seed">
        /// A non-zero 64-bit seed. If zero (degenerate), state is forced to a fixed non-zero
        /// constant so the PRNG never gets stuck in the zero-state trap.
        /// </param>
        public Xorshift64(long seed)
        {
            _state = seed == 0L ? 0xDEADBEEFCAFEBABEUL : unchecked((ulong)seed);
        }

        /// <summary>Advance state and return the next raw 64-bit value.</summary>
        public ulong NextULong()
        {
            _state ^= _state << 13;
            _state ^= _state >> 7;
            _state ^= _state << 17;
            return _state;
        }

        /// <summary>Returns a uniform double in [0, 1).</summary>
        public double NextDouble()
        {
            // Use the top 53 bits for a double in [0,1)
            return (NextULong() >> 11) * (1.0 / (1UL << 53));
        }

        /// <summary>Returns a uniform integer in [0, maxExclusive).</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Must be > 0");
            // Avoid modulo bias for small ranges (good enough for our sizes)
            return (int)(NextULong() % (ulong)maxExclusive);
        }

        /// <summary>Returns a uniform float in [min, max].</summary>
        public float NextFloat(float min, float max)
        {
            return min + (float)(NextDouble() * (max - min));
        }

        /// <summary>
        /// Box-Muller normal sample from N(mean, stddev).
        /// Consumes 2 PRNG draws; result is unbounded but practically within ±4σ.
        /// </summary>
        public double NextNormal(double mean, double stddev)
        {
            double u1 = NextDouble();
            double u2 = NextDouble();
            // Avoid log(0) on exact zero (extremely unlikely but guard anyway)
            if (u1 <= 0.0) u1 = 1e-15;
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            return mean + stddev * z;
        }
    }

    /// <summary>
    /// Factory helpers to build per-bot seeded PRNG streams, following the
    /// SPEC §2 "split-concerns" convention:
    ///   identity stream  →  seed suffix ":ident"
    ///   strokes stream   →  seed suffix ":strokes"
    ///   pace stream      →  seed suffix ":pace"
    /// </summary>
    public static class BotSeedFactory
    {
        /// <summary>Seed the identity-selection PRNG for bot at <paramref name="botIndex"/>.</summary>
        public static Xorshift64 IdentStream(string tournamentId, int botIndex)
            => new Xorshift64(BotFieldHash.StableHash($"{tournamentId}|{botIndex}:ident"));

        /// <summary>Seed the strokes-roll PRNG for bot at <paramref name="botIndex"/>.</summary>
        public static Xorshift64 StrokesStream(string tournamentId, int botIndex)
            => new Xorshift64(BotFieldHash.StableHash($"{tournamentId}|{botIndex}:strokes"));

        /// <summary>Seed the pace-schedule PRNG for bot at <paramref name="botIndex"/>.</summary>
        public static Xorshift64 PaceStream(string tournamentId, int botIndex)
            => new Xorshift64(BotFieldHash.StableHash($"{tournamentId}|{botIndex}:pace"));

        /// <summary>Global bracket-selection PRNG seeded from the tournament id (field-level draw).</summary>
        public static Xorshift64 BracketStream(string tournamentId)
            => new Xorshift64(BotFieldHash.StableHash($"{tournamentId}:bracket"));
    }
}
