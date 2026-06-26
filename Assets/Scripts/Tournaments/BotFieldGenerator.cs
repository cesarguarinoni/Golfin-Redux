// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — BotFieldGenerator
// Stage 2+3 (T3): bracket→strokes, identity selection, pace schedule, projection.
// System-only — NO UnityEngine dependency — headless NUnit-testable.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Golfin.Tournaments
{
    // ─────────────────────────────────────────────────────────────────────────
    // Data types (parsed from injected CSV strings — no Resources.Load here)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>One row from fake_players.csv.</summary>
    public sealed class FakePlayerRow
    {
        public string Id          { get; }
        public string Username    { get; }
        public string CharacterId { get; }
        public int    Level       { get; }

        public FakePlayerRow(string id, string username, string characterId, int level)
        {
            Id          = id;
            Username    = username;
            CharacterId = characterId;
            Level       = level;
        }
    }

    /// <summary>One row from bot_score_brackets.csv.</summary>
    public sealed class BotScoreBracketRow
    {
        /// <summary>bot_difficulty.csv minLevel (bracket id key).</summary>
        public int    MinLevel       { get; }
        public double MeanDeltaPerHole { get; }
        public double StdevPerHole   { get; }

        public BotScoreBracketRow(int minLevel, double meanDelta, double stdev)
        {
            MinLevel         = minLevel;
            MeanDeltaPerHole = meanDelta;
            StdevPerHole     = stdev;
        }
    }

    /// <summary>
    /// Parser for fake_players.csv injected as a raw string.
    /// Does NOT call Resources.Load so it stays System-only / headless-testable.
    /// T4 (UnityEngine-side) calls Resources.Load and passes the TextAsset.text here.
    /// </summary>
    public static class FakePlayerRosterParser
    {
        /// <summary>
        /// Parse the CSV text (first line = header, skip comment lines beginning with '#').
        /// Returns an empty list if text is null/empty.
        /// </summary>
        public static IReadOnlyList<FakePlayerRow> Parse(string csvText)
        {
            var rows = new List<FakePlayerRow>();
            if (string.IsNullOrWhiteSpace(csvText)) return rows;

            bool headerSkipped = false;
            foreach (var rawLine in csvText.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (!headerSkipped) { headerSkipped = true; continue; }

                var parts = line.Split(',');
                if (parts.Length < 4) continue;
                if (!int.TryParse(parts[3].Trim(), out int level)) continue;
                rows.Add(new FakePlayerRow(
                    id:          parts[0].Trim(),
                    username:    parts[1].Trim(),
                    characterId: parts[2].Trim(),
                    level:       level
                ));
            }
            return rows;
        }
    }

    /// <summary>
    /// Parser for bot_score_brackets.csv injected as a raw string.
    /// Sorted ascending by MinLevel at parse time so bracket lookup is deterministic.
    /// </summary>
    public static class BotScoreBracketsParser
    {
        public static IReadOnlyList<BotScoreBracketRow> Parse(string csvText)
        {
            var rows = new List<BotScoreBracketRow>();
            if (string.IsNullOrWhiteSpace(csvText)) return rows;

            bool headerSkipped = false;
            foreach (var rawLine in csvText.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (!headerSkipped) { headerSkipped = true; continue; }

                var parts = line.Split(',');
                if (parts.Length < 3) continue;
                if (!int.TryParse(parts[0].Trim(), out int minLevel)) continue;
                if (!double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double meanDelta)) continue;
                if (!double.TryParse(parts[2].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double stdev)) continue;
                rows.Add(new BotScoreBracketRow(minLevel, meanDelta, stdev));
            }
            rows.Sort((a, b) => a.MinLevel.CompareTo(b.MinLevel));
            return rows;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BotProjection — output of Project()
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Read-time projection result (GDD §7 / SPEC §6).
    /// T4 maps this to TournamentLeaderboardEntry rows.
    /// </summary>
    public readonly struct BotProjection
    {
        /// <summary>Number of holes completed (revealed) at the queried time.</summary>
        public int Thru { get; }

        /// <summary>Sum of strokes for holes 0..Thru (exclusive). 0 if no holes revealed.</summary>
        public int RevealedStrokes { get; }

        /// <summary>True when the bot has completed all holes (thru == H).</summary>
        public bool Complete { get; }

        public BotProjection(int thru, int revealedStrokes, bool complete)
        {
            Thru            = thru;
            RevealedStrokes = revealedStrokes;
            Complete        = complete;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BotFieldGenerator — the main T3 engine
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Deterministic bot-field generator + organic-reveal projector (GDD §7 / T3).
    ///
    /// <para>Two public entry points:</para>
    /// <list type="bullet">
    ///   <item><see cref="RollField"/> — pre-roll the entire field from (def.Id, cfg, holePars).</item>
    ///   <item><see cref="Project"/> — pure read-time projection of a single card.</item>
    /// </list>
    ///
    /// <para>
    /// System-only: no UnityEngine. CSV data injected as parsed lists so T4
    /// (Unity side) can wire Resources.Load and pass in the lists at construction.
    /// </para>
    /// </summary>
    public sealed class BotFieldGenerator
    {
        /// <summary>Worst-hole cap (D3, Cesar 2026-06-25): reuse versusStrokeCapOverPar = +4.</summary>
        public const int StrokeCapOverPar = 4;

        private readonly IReadOnlyList<FakePlayerRow>       _roster;
        private readonly IReadOnlyList<BotScoreBracketRow>  _brackets;

        /// <param name="roster">Parsed rows from fake_players.csv.</param>
        /// <param name="scoreBrackets">Parsed rows from bot_score_brackets.csv (sorted by MinLevel asc).</param>
        public BotFieldGenerator(
            IReadOnlyList<FakePlayerRow>      roster,
            IReadOnlyList<BotScoreBracketRow> scoreBrackets)
        {
            _roster   = roster   ?? throw new ArgumentNullException(nameof(roster));
            _brackets = scoreBrackets ?? throw new ArgumentNullException(nameof(scoreBrackets));
        }

        // ─── RollField ───────────────────────────────────────────────────────

        /// <summary>
        /// Pre-roll the full bot field for <paramref name="def"/> deterministically.
        /// Same arguments → same field, always. (SPEC §7 Determinism invariant)
        /// </summary>
        /// <param name="def">Tournament definition (seed = def.Id).</param>
        /// <param name="cfg">Bot field configuration (count, bracket weights, pace params).</param>
        /// <param name="holePars">Par values for each hole in the tournament (caller resolves from def.HoleSet).</param>
        public IReadOnlyList<BotCard> RollField(
            TournamentDefinition          def,
            BotFieldConfig                cfg,
            IReadOnlyList<int>            holePars)
        {
            return RollField(def, cfg, holePars, out _);
        }

        /// <summary>
        /// Internal test seam: pre-roll the full bot field and also expose the sampled target
        /// bracket for each slot. The sampled target is what <see cref="SampleBracket"/> returned
        /// for each bot — NOT the picked identity's natural level bracket. Testing this distribution
        /// (rather than the picked identity level) is the only way to verify D2 (BracketWeights are
        /// honored), because nearest-bracket fallback makes the identity level differ from the target.
        /// Made internal so the test assembly can call it via InternalsVisibleTo without exposing it
        /// to production callers; the public API remains the single-return-value overload.
        /// </summary>
        internal IReadOnlyList<BotCard> RollField(
            TournamentDefinition          def,
            BotFieldConfig                cfg,
            IReadOnlyList<int>            holePars,
            out IReadOnlyList<string>     sampledBrackets)
        {
            if (def      == null) throw new ArgumentNullException(nameof(def));
            if (cfg      == null) throw new ArgumentNullException(nameof(cfg));
            if (holePars == null) throw new ArgumentNullException(nameof(holePars));

            int H = holePars.Count;
            if (H == 0) throw new ArgumentException("holePars must have at least one entry", nameof(holePars));

            var cards   = new List<BotCard>(cfg.BotCount);
            var sampled = new List<string>(cfg.BotCount);

            // Build per-bot: bracket draw → identity pick → strokes roll → pace schedule
            // Track used identities to guarantee no duplicates within one field
            var usedIds = new HashSet<string>();

            // Bracket keys (ordered by minLevel ascending) for index-based sampling
            var bracketKeys = BracketKeysSorted(cfg.BracketWeights);

            // Per-bracket identity pool: ids grouped by bracket, shuffled per bot stream
            // Built lazily — once per bracket, then consumed in order.
            var identityPools = BuildIdentityPools(bracketKeys, usedIds);

            // Global bracket-selection PRNG (shared across field, one seed)
            var bracketRng = BotSeedFactory.BracketStream(def.Id);

            for (int i = 0; i < cfg.BotCount; i++)
            {
                // --- Select target bracket (D2) ---
                string targetBracket = SampleBracket(cfg.BracketWeights, bracketKeys, bracketRng);
                sampled.Add(targetBracket); // expose for test seam

                // --- Pick identity (no-repeat, nearest-bracket fallback) ---
                var identRng = BotSeedFactory.IdentStream(def.Id, i);
                string botId = PickIdentity(targetBracket, bracketKeys, identityPools, identRng, usedIds);

                // --- Get the bracket's score distribution (slot's bracket = targetBracket, D2) ---
                var bracketRow = FindBracketRow(targetBracket);

                // --- Roll per-hole strokes (D1, D3) ---
                var strokesRng = BotSeedFactory.StrokesStream(def.Id, i);
                var perHoleStrokes = new int[H];
                int totalStrokes = 0;
                for (int h = 0; h < H; h++)
                {
                    int par   = holePars[h];
                    int lower = 1;
                    int upper = par + StrokeCapOverPar;
                    double raw = strokesRng.NextNormal(bracketRow.MeanDeltaPerHole, bracketRow.StdevPerHole);
                    int s = (int)Math.Round(par + raw);
                    s = Math.Max(lower, Math.Min(upper, s));
                    perHoleStrokes[h] = s;
                    totalStrokes += s;
                }

                // --- Roll pace schedule (SPEC §5) ---
                var paceRng = BotSeedFactory.PaceStream(def.Id, i);
                float startOffset = paceRng.NextFloat(cfg.StartOffsetMinSec, cfg.StartOffsetMaxSec);
                var completions = RollPaceSchedule(
                    paceRng,
                    def.StartUtc,
                    def.EndUtc,
                    startOffset,
                    cfg.PerHoleSpreadSec,
                    H);

                cards.Add(new BotCard(
                    botId:                botId,
                    perHoleStrokes:       perHoleStrokes,
                    totalStrokes:         totalStrokes,
                    startOffsetSeconds:   startOffset,
                    perHoleCompletionUtc: completions));
            }

            sampledBrackets = sampled;
            return cards;
        }

        // ─── Project ─────────────────────────────────────────────────────────

        /// <summary>
        /// Pure read-time projection of a single bot card at <paramref name="now"/>.
        /// Depends only on (card, now) — no mutable state. (SPEC §7 Projection purity)
        /// </summary>
        /// <remarks>
        /// B2 fix (iter-2): the former implementation used <c>else break</c> to short-circuit
        /// on the strictly-increasing pace invariant. This coupled Project correctness to the
        /// pace invariant — a non-monotonic schedule would cause under-counting. The fix is to
        /// always scan all H completions (O(H), H ≤ 18, negligible cost) so Project is correct
        /// independent of schedule monotonicity.
        /// </remarks>
        public BotProjection Project(BotCard card, DateTime now)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));

            int H    = card.PerHoleCompletionUtc.Count;
            int thru = 0;
            for (int h = 0; h < H; h++)
            {
                if (card.PerHoleCompletionUtc[h] <= now)
                    thru++;
                // No early break — always scan all H so correctness is independent of
                // pace schedule monotonicity (B2 fix, iter-2).
            }

            int revealed = 0;
            for (int h = 0; h < thru; h++)
                revealed += card.PerHoleStrokes[h];

            bool complete = (thru == H);
            return new BotProjection(thru, revealed, complete);
        }

        // ─── Internals ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns sorted bracket keys (by minLevel ascending, matching bot_difficulty.csv ids).
        /// Bracket id = minLevel string, e.g. "1", "10", "25", "50", "100", "180".
        /// </summary>
        private static List<string> BracketKeysSorted(IReadOnlyDictionary<string, float> weights)
        {
            var keys = new List<string>(weights.Keys);
            // sort numerically where possible, else lexicographic
            keys.Sort((a, b) =>
            {
                bool aNum = int.TryParse(a, out int ai);
                bool bNum = int.TryParse(b, out int bi);
                if (aNum && bNum) return ai.CompareTo(bi);
                return string.Compare(a, b, StringComparison.Ordinal);
            });
            return keys;
        }

        /// <summary>
        /// Builds per-bracket identity pools from the roster.
        /// Each row's bracket = highest minLevel ≤ level (bot_difficulty.csv definition).
        /// The usedIds set is shared so identities picked in one bracket can't reappear.
        /// </summary>
        private Dictionary<string, List<string>> BuildIdentityPools(
            List<string> bracketKeys,
            HashSet<string> usedIds)
        {
            var pools = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var k in bracketKeys)
                pools[k] = new List<string>();

            foreach (var row in _roster)
            {
                string bracket = BracketForLevel(row.Level);
                if (pools.TryGetValue(bracket, out var pool))
                    pool.Add(row.Id);
                // rows whose bracket is not in bracketKeys are silently skipped
            }
            return pools;
        }

        /// <summary>
        /// Returns the bracket key (minLevel string) for a player level.
        /// Highest minLevel ≤ level, using the _brackets list (sorted asc).
        /// Falls back to the lowest bracket if level < all minLevels.
        /// </summary>
        private string BracketForLevel(int level)
        {
            if (_brackets.Count == 0) return "1";
            string best = _brackets[0].MinLevel.ToString();
            foreach (var row in _brackets)
            {
                if (row.MinLevel <= level)
                    best = row.MinLevel.ToString();
                else
                    break;
            }
            return best;
        }

        /// <summary>
        /// Weighted bracket sample from BracketWeights using the shared bracket PRNG.
        /// </summary>
        private static string SampleBracket(
            IReadOnlyDictionary<string, float> weights,
            List<string> bracketKeys,
            Xorshift64 rng)
        {
            double total = 0.0;
            foreach (var k in bracketKeys)
                total += weights[k];

            double pick = rng.NextDouble() * total;
            double accum = 0.0;
            foreach (var k in bracketKeys)
            {
                accum += weights[k];
                if (pick < accum) return k;
            }
            return bracketKeys[bracketKeys.Count - 1]; // rounding fallback
        }

        /// <summary>
        /// Pick an identity from the target bracket pool (nearest-bracket fallback if exhausted).
        /// Removes chosen id from the pool and marks it used — guarantees no repeat in a field.
        /// </summary>
        private static string PickIdentity(
            string targetBracket,
            List<string> bracketKeys,
            Dictionary<string, List<string>> pools,
            Xorshift64 rng,
            HashSet<string> usedIds)
        {
            // Try target bracket first, then expand to adjacent brackets (nearest-bracket fallback)
            string? chosen = TryPickFromBracket(pools, targetBracket, rng, usedIds);
            if (chosen != null) return chosen;

            // Nearest-bracket fallback: search all other brackets
            // Search outward from target index
            int idx = bracketKeys.IndexOf(targetBracket);
            for (int delta = 1; delta <= bracketKeys.Count; delta++)
            {
                int lo = idx - delta;
                int hi = idx + delta;
                if (lo >= 0)
                {
                    chosen = TryPickFromBracket(pools, bracketKeys[lo], rng, usedIds);
                    if (chosen != null) return chosen;
                }
                if (hi < bracketKeys.Count)
                {
                    chosen = TryPickFromBracket(pools, bracketKeys[hi], rng, usedIds);
                    if (chosen != null) return chosen;
                }
            }

            // Should never reach here if roster.Count >= BotCount, but guard gracefully
            throw new InvalidOperationException(
                $"Identity pool exhausted for tournament. Roster has {pools.Sum(p => p.Value.Count)} " +
                $"identities but more bots were requested. Increase fake_players.csv or reduce BotCount.");
        }

        /// <summary>
        /// Attempt to pick one unused identity from the named bracket pool.
        /// Returns null if the pool is empty.
        /// The picked identity is removed from the pool and added to usedIds.
        /// </summary>
        private static string? TryPickFromBracket(
            Dictionary<string, List<string>> pools,
            string bracketKey,
            Xorshift64 rng,
            HashSet<string> usedIds)
        {
            if (!pools.TryGetValue(bracketKey, out var pool)) return null;
            if (pool.Count == 0) return null;

            // Pick a random index from remaining pool entries
            int idx = rng.NextInt(pool.Count);
            string id = pool[idx];
            // Swap-remove for O(1)
            pool[idx] = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);
            usedIds.Add(id);
            return id;
        }

        /// <summary>
        /// Find the BotScoreBracketRow whose MinLevel matches the bracket key string.
        /// Falls back to first row if no match.
        /// </summary>
        private BotScoreBracketRow FindBracketRow(string bracketKey)
        {
            if (_brackets.Count == 0)
                return new BotScoreBracketRow(1, 0.9, 0.7); // emergency default

            if (int.TryParse(bracketKey, out int minLvl))
            {
                foreach (var r in _brackets)
                    if (r.MinLevel == minLvl) return r;
            }
            return _brackets[0];
        }

        /// <summary>
        /// Roll a strictly-increasing pace schedule across [botStart, endUtc].
        /// Each completion in (startUtc, endUtc]; last clamped to endUtc if jitter overruns.
        /// </summary>
        /// <remarks>
        /// B1 fix (iter-2): the former re-enforce-from-end pass could push completions past
        /// endUtc when totalWindowSeconds &lt; H (adversarial short window). The fix is a two-phase
        /// approach:
        /// 1. Compress proportionally if overrun (preserves relative ordering), capping last = endUtc.
        /// 2. Forward strict-increase pass: each slot must be &gt; predecessor.
        /// 3. Backward clamp pass: each slot must be ≤ endUtc.
        /// 4. Forward strict-increase pass again to repair any equality introduced by backward clamp.
        /// The minimum supported window is H seconds (one second per hole). Below that, some
        /// completions will share endUtc, which the compress guard catches by falling through to
        /// the edge-case path where botStart &gt;= endUtc.
        /// </remarks>
        private static IReadOnlyList<DateTime> RollPaceSchedule(
            Xorshift64  paceRng,
            DateTime    startUtc,
            DateTime    endUtc,
            float       startOffsetSeconds,
            float       perHoleSpreadSec,
            int         H)
        {
            DateTime botStart = startUtc.AddSeconds(startOffsetSeconds);
            double totalWindowSeconds = (endUtc - botStart).TotalSeconds;
            if (totalWindowSeconds <= 0)
            {
                // Edge case: botStart >= endUtc — all holes complete at endUtc
                var flat = new DateTime[H];
                for (int h = 0; h < H; h++) flat[h] = endUtc;
                return flat;
            }

            double nominalStep = totalWindowSeconds / H;
            var completions = new DateTime[H];
            double cumulativeSeconds = 0.0;

            for (int h = 0; h < H; h++)
            {
                // jitter in [0, perHoleSpreadSec]
                double jitter = paceRng.NextDouble() * perHoleSpreadSec;
                cumulativeSeconds += nominalStep + jitter;
                completions[h] = botStart.AddSeconds(cumulativeSeconds);
            }

            // Phase 1: Compress proportionally if jitter pushed last completion past endUtc.
            // Proportional scaling preserves relative ordering and guarantees last ≤ endUtc.
            double lastSec = (completions[H - 1] - botStart).TotalSeconds;
            if (lastSec > totalWindowSeconds)
            {
                double scale = totalWindowSeconds / lastSec;
                for (int h = 0; h < H; h++)
                {
                    double rawSec = (completions[h] - botStart).TotalSeconds;
                    completions[h] = botStart.AddSeconds(rawSec * scale);
                }
                // Pin last exactly to endUtc (avoid float overshoot)
                completions[H - 1] = endUtc;
            }

            // Phase 2: Forward strict-increase pass.
            // Ensures each completion is strictly after the previous.
            // The minimum step is 1 second; if this pushes past endUtc we repair in phase 3.
            for (int h = 1; h < H; h++)
            {
                if (completions[h] <= completions[h - 1])
                    completions[h] = completions[h - 1].AddSeconds(1.0);
            }

            // Phase 3: Backward clamp pass — ensure no completion exceeds endUtc.
            // Work from last to first so we don't cascade forward.
            // If the last completion is already > endUtc, clamp it; if that makes it
            // equal to or before the predecessor we'll fix that in phase 4.
            for (int h = H - 1; h >= 0; h--)
            {
                if (completions[h] > endUtc)
                    completions[h] = endUtc;
            }

            // Phase 4: Forward strict-increase pass again to repair any equality the clamp introduced.
            // We work forward, accepting that very short windows compress all late holes toward endUtc.
            for (int h = 1; h < H; h++)
            {
                if (completions[h] <= completions[h - 1])
                {
                    // Attempt to push it 1s forward; if that would exceed endUtc, pin to endUtc
                    // (meaning multiple bots share the same completion time — only possible if the
                    // window is genuinely too short for H holes at 1s/hole minimum).
                    DateTime candidate = completions[h - 1].AddSeconds(1.0);
                    completions[h] = candidate <= endUtc ? candidate : endUtc;
                }
            }

            return completions;
        }
    }
}
