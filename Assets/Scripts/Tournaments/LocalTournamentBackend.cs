// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — LocalTournamentBackend (T4)
// Implements ITournamentBackend (8 methods) via constructor-injected seams.
// No UnityEngine dependency — headless + NUnit-testable (same discipline as T3).
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Golfin.Core.Stamina;

namespace Golfin.Tournaments
{
    /// <summary>
    /// v1 implementation of <see cref="ITournamentBackend"/>.
    /// Replaces <see cref="StubTournamentBackend"/>.
    /// <para>
    /// All dependencies are injected by constructor; no singleton lookups inside
    /// this class. Production code composes real adapters; tests inject fakes.
    /// </para>
    /// <para>
    /// <b>Clock-trust-free re-sim model (Phase 3, D3=NO — for future server re-sim, GDD §8):</b>
    /// Tournament condition is drained once per <see cref="SubmitHoleResult"/> call
    /// (drain = <c>StaminaModel.DrainForHole()</c>, a CSV constant), never regen'd within
    /// the tournament event. The condition at hole N is therefore fully reproducible from the
    /// persisted entry timeline (<c>StartedUtc</c>, each <c>HoleResult</c>) plus the frozen
    /// <c>snapshot.Stamina</c> (tank = <c>MaxCondition(stamina)</c>). A future server re-sim
    /// never needs to trust client clocks — drain-count alone is sufficient to reconstruct
    /// the per-hole pool, and thus the per-hole degraded Strength/ClubControl via Option-C.
    /// </para>
    /// </summary>
    public sealed class LocalTournamentBackend : ITournamentBackend
    {
        // ── "Ending" window threshold (D2) — last 1 hour of the window ─────────
        private static readonly TimeSpan EndingThreshold = TimeSpan.FromHours(1.0);

        // ── Stamina fallback constants (D1-A: used when StaminaModel is not configured) ──
        // These keep existing StaminaModel-unaware EditMode tests green (SPEC §7 test 8).
        // Production path always has StaminaModel configured via StaminaRuntimeService at boot.
        private const float DefaultStaminaMax       = 100f;   // mirrors TournamentRoundContext.DefaultStaminaMax
        private const float DefaultDrainPerHole     = 5f;     // placeholder flat cost per hole

        // ── Constructor-injected deps ────────────────────────────────────────
        private readonly IReadOnlyList<TournamentDefinition>         _definitions;
        private readonly IReadOnlyDictionary<string, PrizeTable>     _prizeTables;
        private readonly IReadOnlyDictionary<string, BotFieldConfig> _botFields;
        private readonly BotFieldGenerator                           _botGen;
        private readonly ITournamentClock                            _clock;
        private readonly ITournamentEntryStore                       _store;
        private readonly IRewardPointsService                        _rp;
        private readonly IItemRewardService                          _items;
        private readonly IHoleParProvider                            _pars;
        private readonly ICharacterStatsProvider                     _stats;

        // ── Cached computed result (memo only — NOT the claim guard) ────────────
        // Keyed by tournamentId; built lazily in GetResults and reused.
        // Claim-once persistence is handled entirely by _store.IsClaimed / MarkClaimed
        // so that a new LocalTournamentBackend instance over the SAME store cannot
        // re-grant prizes (D-claim b, SPEC §6).
        private readonly Dictionary<string, TournamentResult> _resultMemo
            = new Dictionary<string, TournamentResult>(StringComparer.Ordinal);

        // ── Constructor ───────────────────────────────────────────────────────

        /// <param name="definitions">Loaded from TournamentCsvLoader.LoadTournaments().</param>
        /// <param name="prizeTables">Loaded from TournamentCsvLoader.LoadPrizeTables().</param>
        /// <param name="botFields">Loaded from TournamentCsvLoader.LoadBotFields().</param>
        /// <param name="botGen">T3 generator (built from parsed roster + bot_score_brackets).</param>
        /// <param name="clock">T1 clock seam — all UtcNow reads go here.</param>
        /// <param name="store">Entry persistence seam — InMemoryEntryStore for v1; T5 replaces.</param>
        /// <param name="rp">RP debit/grant seam — wraps RewardPointsManager.Instance.</param>
        /// <param name="items">Item grant seam — wraps SaveData.itemQuantities (via T5).</param>
        /// <param name="pars">Per-hole par seam — wraps HoleDatabaseLoader.</param>
        /// <param name="stats">
        /// Character stats provider seam — snapshot is captured at Register time.
        /// Production: <see cref="CharacterManagerStatsProvider"/>; tests: <see cref="FakeStatsProvider"/>.
        /// </param>
        public LocalTournamentBackend(
            IReadOnlyList<TournamentDefinition>         definitions,
            IReadOnlyDictionary<string, PrizeTable>     prizeTables,
            IReadOnlyDictionary<string, BotFieldConfig> botFields,
            BotFieldGenerator                           botGen,
            ITournamentClock                            clock,
            ITournamentEntryStore                       store,
            IRewardPointsService                        rp,
            IItemRewardService                          items,
            IHoleParProvider                            pars,
            ICharacterStatsProvider?                    stats = null)
        {
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _prizeTables = prizeTables  ?? throw new ArgumentNullException(nameof(prizeTables));
            _botFields   = botFields    ?? throw new ArgumentNullException(nameof(botFields));
            _botGen      = botGen       ?? throw new ArgumentNullException(nameof(botGen));
            _clock       = clock        ?? throw new ArgumentNullException(nameof(clock));
            _store       = store        ?? throw new ArgumentNullException(nameof(store));
            _rp          = rp           ?? throw new ArgumentNullException(nameof(rp));
            _items       = items        ?? throw new ArgumentNullException(nameof(items));
            _pars        = pars         ?? throw new ArgumentNullException(nameof(pars));
            // stats is optional — null means snapshots will be null (pre-amendment compatibility).
            // Production wires CharacterManagerStatsProvider; tests inject FakeStatsProvider.
            _stats       = stats!;
        }

        // ═════════════════════════════════════════════════════════════════════
        // 1. GetTournaments
        // ═════════════════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public IReadOnlyList<TournamentDefinition> GetTournaments()
        {
            // State derivation is exposed via DeriveState(); callers use it.
            // The definitions are returned as-is — they are immutable value objects.
            return _definitions;
        }

        // ═════════════════════════════════════════════════════════════════════
        // 2. GetTournament
        // ═════════════════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public TournamentDefinition GetTournament(string id)
        {
            foreach (var def in _definitions)
                if (StringEquals(def.Id, id)) return def;
            throw new KeyNotFoundException($"Tournament '{id}' not found.");
        }

        // ═════════════════════════════════════════════════════════════════════
        // 3. Register
        // ═════════════════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public EntryState Register(string id, long entryPaymentRP, string characterId)
        {
            // Idempotent: if already registered, return existing entry (no re-charge)
            var existing = _store.Load(id);
            if (existing != null) return existing;

            // Debit RP if fee > 0
            if (entryPaymentRP > 0)
            {
                if (!_rp.TrySpend(entryPaymentRP))
                    throw new InvalidOperationException(
                        $"Insufficient RP to register for tournament '{id}': " +
                        $"need {entryPaymentRP}, have {_rp.Balance}.");
            }

            // Freeze character stats at sign-up (SPEC §5 — capture point is Register,
            // not round-start, because sign-up can precede play by days).
            // _stats is null only when no provider was injected (legacy/pre-amendment).
            CharacterSnapshot? snapshot = _stats?.SnapshotFor(characterId);

            // Seed the tournament condition pool from the frozen snapshot (Phase 3, D1-A).
            // Guard with StaminaModel.IsConfigured so StaminaModel-unaware EditMode tests pass.
            // Null snapshot (legacy pre-amendment) → sentinel -1f (hydrates to full on use, D2).
            float conditionPool;
            if (snapshot != null && StaminaModel.IsConfigured)
                conditionPool = StaminaModel.MaxCondition(snapshot.Stamina);
            else if (snapshot == null)
                conditionPool = -1f;   // sentinel: no snapshot → hydrate to full on use
            else
                conditionPool = DefaultStaminaMax;   // unconfigured fallback

            var entry = new EntryState(
                tournamentId:       id,
                characterId:        characterId,
                snapshot:           snapshot,
                perHole:            new List<HoleResult>(),
                startedUtc:         _clock.UtcNow,
                lastHoleUtc:        null,
                status:             EntryStatus.InProgress,
                conditionRemaining: conditionPool);

            _store.Save(entry);
            return entry;
        }

        // ═════════════════════════════════════════════════════════════════════
        // 4. GetMyEntry
        // ═════════════════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public EntryState? GetMyEntry(string id)
            => _store.Load(id);

        // ═════════════════════════════════════════════════════════════════════
        // 5. SubmitHoleResult
        // ═════════════════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public EntryState SubmitHoleResult(string id, HoleResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            var def = GetTournament(id);
            var entry = _store.Load(id);
            if (entry == null)
                throw new InvalidOperationException($"Cannot submit hole result: not registered for tournament '{id}'.");

            DateTime now = _clock.UtcNow;

            // Reject post-EndUtc submit (tournament window is closed)
            if (now > def.EndUtc)
                throw new InvalidOperationException(
                    $"Cannot submit hole result for tournament '{id}': window closed at {def.EndUtc:u}.");

            // Reject duplicate hole submission (same holeId already in PerHole)
            foreach (var existing in entry.PerHole)
                if (StringEquals(existing.HoleId, result.HoleId))
                    throw new InvalidOperationException(
                        $"Duplicate hole result: hole '{result.HoleId}' already submitted for tournament '{id}'.");

            // Append to PerHole (create a new list since PerHole is read-only)
            var updatedHoles = new List<HoleResult>(entry.PerHole) { result };
            int holeCount    = def.HoleSet.Count;
            bool finished    = updatedHoles.Count >= holeCount;

            // Drain the tournament condition pool by one hole's worth (Phase 3, D1-A).
            // Atomic with the hole-result persist: drain happens only when a hole is confirmed.
            // Guard with StaminaModel.IsConfigured so StaminaModel-unaware EditMode tests pass.
            // D3 = NO: no regen, drain-only. Pool starts from the current entry value,
            // treating sentinel -1f as full before draining.
            float currentCondition = entry.ConditionRemaining;
            if (currentCondition < 0f && entry.Snapshot != null)
            {
                // Sentinel → hydrate to full before draining
                currentCondition = StaminaModel.IsConfigured
                    ? StaminaModel.MaxCondition(entry.Snapshot.Stamina)
                    : DefaultStaminaMax;
            }
            else if (currentCondition < 0f)
            {
                currentCondition = DefaultStaminaMax;  // no snapshot → fallback full
            }

            float drain = StaminaModel.IsConfigured ? StaminaModel.DrainForHole() : DefaultDrainPerHole;
            float nextCondition = Math.Max(0f, currentCondition - drain);

            var updated = new EntryState(
                tournamentId:       id,
                characterId:        entry.CharacterId,
                snapshot:           entry.Snapshot,         // preserve frozen snapshot across hole submissions
                perHole:            updatedHoles,
                startedUtc:         entry.StartedUtc,
                lastHoleUtc:        now,
                status:             finished ? EntryStatus.Finished : EntryStatus.InProgress,
                conditionRemaining: nextCondition);

            _store.Save(updated);
            return updated;
        }

        // ═════════════════════════════════════════════════════════════════════
        // 6. GetLeaderboard
        // ═════════════════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public IReadOnlyList<TournamentLeaderboardEntry> GetLeaderboard(string id)
        {
            var def  = GetTournament(id);
            DateTime now  = _clock.UtcNow;
            bool provisional  = !IsResolved(def, now);
            var holePars = _pars.ParsFor(def.ClubId, def.HoleSet);

            // Roll bot field (deterministic from def.Id)
            IReadOnlyList<BotCard> botCards;
            if (_botFields.TryGetValue(def.BotFieldId, out var cfg))
                botCards = _botGen.RollField(def, cfg, holePars);
            else
                botCards = Array.Empty<BotCard>();

            // Load player entry (null if not registered)
            var playerEntry = _store.Load(id);

            // Build raw entries list with both bots and player
            var rawEntries = new List<RawEntry>(botCards.Count + 1);

            // Add bots
            foreach (var card in botCards)
            {
                var proj = _botGen.Project(card, now);

                // In final board, bots should be complete; in provisional, use revealed so far
                int strokes = provisional ? proj.RevealedStrokes : card.TotalStrokes;
                int thru    = proj.Thru;

                // Calculate bot time: endUtc - botStart (for final tiebreak)
                DateTime botStart = def.StartUtc.AddSeconds(card.StartOffsetSeconds);
                double   botTimeSec = Math.Max(0.0, (def.EndUtc - botStart).TotalSeconds);

                // For provisional: sort by score-to-par over completed holes (SPEC §5 D3)
                // For final: sort by total strokes
                int completedPar = 0;
                for (int h = 0; h < thru && h < holePars.Count; h++)
                    completedPar += holePars[h];

                rawEntries.Add(new RawEntry
                {
                    IsPlayer         = false,
                    DisplayName      = card.BotId,
                    CharacterId      = card.BotId,
                    Level            = 1,
                    Strokes          = strokes,
                    TotalStrokes     = card.TotalStrokes,
                    Thru             = thru,
                    TimeSeconds      = (float)botTimeSec,
                    CompletedPar     = completedPar,
                    ScoreToPar       = strokes - completedPar,
                    IsDNF            = false,   // bots never DNF (T3 invariant: all complete by EndUtc)
                    LastHoleUtc      = card.PerHoleCompletionUtc.Count > 0
                                        ? card.PerHoleCompletionUtc[card.PerHoleCompletionUtc.Count - 1]
                                        : def.StartUtc,
                    PerHoleStrokes   = card.PerHoleStrokes,
                });
            }

            // Add player
            if (playerEntry != null)
            {
                bool playerFinished = playerEntry.Status == EntryStatus.Finished;
                bool playerDNF      = now >= def.EndUtc && !playerFinished;

                int playerStrokes = 0;
                int playerPar     = 0;
                int playerThru    = playerEntry.PerHole.Count;
                float playerTime  = 0f;

                var playerPerHole = new List<int>();
                for (int h = 0; h < playerThru; h++)
                {
                    var hr = playerEntry.PerHole[h];
                    playerStrokes += hr.Strokes;
                    playerTime    += hr.TimeSeconds;
                    if (h < holePars.Count)
                        playerPar += holePars[h];
                    playerPerHole.Add(hr.Strokes);
                }

                rawEntries.Add(new RawEntry
                {
                    IsPlayer       = true,
                    DisplayName    = Golfin.Auth.PlayerIdentity.DisplayNameOr("YOU"),
                    CharacterId    = playerEntry.CharacterId,
                    Level          = 1,
                    Strokes        = playerStrokes,
                    TotalStrokes   = playerStrokes,
                    Thru           = playerThru,
                    TimeSeconds    = playerTime,
                    CompletedPar   = playerPar,
                    ScoreToPar     = playerStrokes - playerPar,
                    IsDNF          = playerDNF,
                    LastHoleUtc    = playerEntry.LastHoleUtc ?? playerEntry.StartedUtc,
                    PerHoleStrokes = playerPerHole,
                });
            }

            // Sort the entries
            if (provisional)
                rawEntries.Sort(CompareProvisional);
            else
                rawEntries.Sort(CompareFinal(holePars));

            // Assign ranks; DNF entries go below finishers, hidden from rank list
            return AssignRanks(rawEntries, provisional);
        }

        // ═════════════════════════════════════════════════════════════════════
        // 7. GetResults
        // ═════════════════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public TournamentResult? GetResults(string id)
        {
            var def = GetTournament(id);
            DateTime now = _clock.UtcNow;

            if (!IsResolved(def, now)) return null;

            var entry = _store.Load(id);
            if (entry == null) return null;

            // Build the result (or use memo), then layer in the persisted claimed status
            if (!_resultMemo.TryGetValue(id, out var memo))
            {
                // Get final leaderboard to find player rank
                var board = GetLeaderboard(id);

                // Find player row
                TournamentLeaderboardEntry playerRow = default;
                bool found = false;
                foreach (var row in board)
                {
                    if (row.IsPlayer) { playerRow = row; found = true; break; }
                }
                if (!found) return null;

                // Resolve prize from band
                _prizeTables.TryGetValue(def.PrizeTableId, out var prizeTable);
                var (prizeRP, itemId) = ResolvePrize(playerRow.Rank, playerRow.IsTie, prizeTable, board);

                memo = new TournamentResult(
                    finalRank:    playerRow.Rank,
                    isTie:        playerRow.IsTie,
                    prizeRP:      prizeRP,
                    itemRewardId: itemId,
                    claimed:      false);          // claimed field is always set from store below

                _resultMemo[id] = memo;
            }

            // Always read claimed status from the store — this is what survives a backend rebuild
            bool isClaimed = _store.IsClaimed(id);
            if (isClaimed == memo.Claimed) return memo;   // avoid allocation when nothing changed

            return new TournamentResult(
                finalRank:    memo.FinalRank,
                isTie:        memo.IsTie,
                prizeRP:      memo.PrizeRP,
                itemRewardId: memo.ItemRewardId,
                claimed:      isClaimed);
        }

        // ═════════════════════════════════════════════════════════════════════
        // 8. ClaimPrize
        // ═════════════════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public void ClaimPrize(string id)
        {
            // Claim-once guard: check the STORE (not an in-memory dict) so the guard
            // survives building a new LocalTournamentBackend over the same store (D-claim b).
            if (_store.IsClaimed(id)) return;

            var result = GetResults(id);
            if (result == null) return;           // not resolved or not entered

            // Grant RP
            if (result.PrizeRP > 0)
                _rp.Grant(result.PrizeRP);

            // Grant item (if any)
            if (result.ItemRewardId != null)
                _items.Grant(result.ItemRewardId, 1);

            // Persist the claim — this is the durable gate
            _store.MarkClaimed(id);

            // Invalidate the memo so GetResults returns claimed:true immediately
            _resultMemo.Remove(id);
        }

        // ═════════════════════════════════════════════════════════════════════
        // Public helper — DeriveState
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Derive the <see cref="TournamentState"/> for a tournament at a given <paramref name="now"/>.
        /// Callers (UI screens) use this to bind the state badge.
        /// </summary>
        public TournamentState DeriveState(TournamentDefinition def, DateTime now)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));

            var entry  = _store.Load(def.Id);
            bool hasEntry   = entry != null;
            bool inProgress = hasEntry && entry!.Status == EntryStatus.InProgress;

            if (now < def.StartUtc) return TournamentState.Upcoming;

            if (now < def.EndUtc)
            {
                // Within the "Ending" window (last 1h by default, D2)
                bool ending = (def.EndUtc - now) <= EndingThreshold;
                if (ending)   return TournamentState.Ending;
                if (inProgress) return TournamentState.Playing;
                return TournamentState.Open;
            }

            // now >= EndUtc
            return hasEntry ? TournamentState.Ended : TournamentState.Closed;
        }

        // ═════════════════════════════════════════════════════════════════════
        // IsResolved helper
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// True when the tournament has passed its resolve gate:
        /// <c>now &gt;= endUtc + resolveDelay</c>.
        /// </summary>
        public static bool IsResolved(TournamentDefinition def, DateTime now)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            DateTime resolveAt = def.EndUtc.AddMinutes(def.ResolveDelayMinutes);
            return now >= resolveAt;
        }

        // ═════════════════════════════════════════════════════════════════════
        // Private — Leaderboard helpers
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Working record while building the leaderboard.</summary>
        private struct RawEntry
        {
            public bool     IsPlayer;
            public string   DisplayName;
            public string   CharacterId;
            public int      Level;
            public int      Strokes;          // revealed strokes (provisional) OR total (final)
            public int      TotalStrokes;     // always the full total (for final board)
            public int      Thru;
            public float    TimeSeconds;
            public int      CompletedPar;
            public int      ScoreToPar;       // strokes - completedPar (provisional sort key, D3)
            public bool     IsDNF;
            public DateTime LastHoleUtc;
            public IReadOnlyList<int>? PerHoleStrokes;
        }

        /// <summary>
        /// Provisional sort: score-to-par over completed holes ascending (D3).
        /// Tie-break by thru desc (more holes = more committed), then time asc.
        /// </summary>
        private static int CompareProvisional(RawEntry a, RawEntry b)
        {
            // DNF below finishers
            if (a.IsDNF != b.IsDNF) return a.IsDNF ? 1 : -1;

            int c = a.ScoreToPar.CompareTo(b.ScoreToPar);
            if (c != 0) return c;
            // More holes completed = ranked higher (secondary tiebreak)
            c = b.Thru.CompareTo(a.Thru);
            if (c != 0) return c;
            // Time ascending
            return a.TimeSeconds.CompareTo(b.TimeSeconds);
        }

        /// <summary>
        /// Final sort: §6 tie ladder — total strokes → countback → time → timestamp.
        /// </summary>
        private static Comparison<RawEntry> CompareFinal(IReadOnlyList<int> holePars)
        {
            return (a, b) =>
            {
                // DNF below finishers
                if (a.IsDNF != b.IsDNF) return a.IsDNF ? 1 : -1;
                if (a.IsDNF && b.IsDNF)
                {
                    // DNF tie-ladder: holes-done desc then strokes asc
                    int da = b.Thru.CompareTo(a.Thru);
                    if (da != 0) return da;
                    da = a.Strokes.CompareTo(b.Strokes);
                    if (da != 0) return da;
                    return a.LastHoleUtc.CompareTo(b.LastHoleUtc);
                }

                // Step 1: total strokes
                int c = a.TotalStrokes.CompareTo(b.TotalStrokes);
                if (c != 0) return c;

                // Step 2: countback (back-9 → back-6 → back-3 → back-1, front-9 → front-6 → front-3 → front-1)
                c = Countback(a, b, holePars);
                if (c != 0) return c;

                // Step 3: total completion time
                c = a.TimeSeconds.CompareTo(b.TimeSeconds);
                if (c != 0) return c;

                // Step 4: submission timestamp
                return a.LastHoleUtc.CompareTo(b.LastHoleUtc);
            };
        }

        /// <summary>
        /// §6 countback over the HoleSet (generalized to N holes):
        /// Back pass:  closing windows from the END of the round.
        ///             half = ceil(N/2); windows = CountbackWindows(half); startIdx = N - window.
        /// Front pass: closing windows from the END of the FRONT HALF.
        ///             Same windows; startIdx = half - window.
        ///             Skipped when N &lt;= half (i.e. N=1, no meaningful front half).
        ///
        /// This produces GDD §6.1 LOCKED for N=18 exactly:
        ///   back-9/6/3/1 (startIdx 9/12/15/17), front-9/6/3/1 (startIdx 0/3/6/8).
        /// For N=9: half=5, back-5/3/1 (startIdx 4/6/8), front-5/3/1 (startIdx 0/2/4).
        ///
        /// Returns negative if a beats b, 0 if still tied, positive if b beats a.
        /// </summary>
        private static int Countback(in RawEntry a, in RawEntry b, IReadOnlyList<int> holePars)
        {
            int N    = holePars.Count;
            int half = (N + 1) / 2;   // ceil(N/2): N=18→9, N=9→5, N=6→3, N=3→2

            // Back pass: closing sub-windows of the full round (from the end).
            foreach (int window in CountbackWindows(half))
            {
                int startIdx = N - window;
                int c = CountbackWindow(a, b, startIdx, window);
                if (c != 0) return c;
            }
            // Front pass: closing sub-windows of the front half (from the end of the front half).
            // Skip when there is no separate front half (N <= half means N=1).
            if (N > half)
            {
                foreach (int window in CountbackWindows(half))
                {
                    int startIdx = half - window;
                    int c = CountbackWindow(a, b, startIdx, window);
                    if (c != 0) return c;
                }
            }
            return 0;
        }

        /// <summary>
        /// Yields the standard golf countback window sizes descending from <paramref name="half"/>:
        /// [half, 6, 3, 1], but only those strictly less than <paramref name="half"/>.
        /// Examples:
        ///   half=9 → [9, 6, 3, 1]
        ///   half=5 → [5, 3, 1]   (6 &gt; 5, skipped)
        ///   half=3 → [3, 1]
        /// </summary>
        private static IEnumerable<int> CountbackWindows(int half)
        {
            yield return half;
            if (6 < half) yield return 6;
            if (3 < half) yield return 3;
            if (1 < half) yield return 1;
        }

        /// <summary>
        /// Compare a vs b over a contiguous window of holes [startIdx, startIdx+count).
        /// Returns negative if a beats b in that window, 0 if tied, positive if b wins.
        /// </summary>
        private static int CountbackWindow(in RawEntry a, in RawEntry b, int startIdx, int count)
        {
            int aStrokes = SumHoles(a.PerHoleStrokes, startIdx, count);
            int bStrokes = SumHoles(b.PerHoleStrokes, startIdx, count);
            return aStrokes.CompareTo(bStrokes);
        }

        private static int SumHoles(IReadOnlyList<int>? perHole, int start, int count)
        {
            if (perHole == null) return 0;
            int sum = 0;
            int end = Math.Min(start + count, perHole.Count);
            for (int i = start; i < end; i++)
                sum += perHole[i];
            return sum;
        }

        /// <summary>
        /// Assign ranks to sorted <paramref name="entries"/> and produce the final
        /// <see cref="TournamentLeaderboardEntry"/> list.
        /// <para>
        /// Finisher rows are ranked with ties; DNF rows are appended below (hidden from
        /// ranked rows per GDD §17.4 — but the player's own DNF is included with IsDNF=true).
        /// </para>
        /// </summary>
        private static IReadOnlyList<TournamentLeaderboardEntry> AssignRanks(
            List<RawEntry> sorted,
            bool provisional)
        {
            var result = new List<TournamentLeaderboardEntry>(sorted.Count);

            // Separate finishers from DNF
            var finishers = new List<RawEntry>();
            RawEntry? playerDnf = null;
            foreach (var e in sorted)
            {
                if (!e.IsDNF)
                    finishers.Add(e);
                else if (e.IsPlayer)
                    playerDnf = e;
                // Non-player DNF rows are hidden (GDD §17.4)
            }

            // Rank finishers
            int nextRank = 1;
            int i        = 0;
            while (i < finishers.Count)
            {
                int rank = nextRank;
                // Count tie group
                int j = i + 1;
                while (j < finishers.Count && finishers[j].TotalStrokes == finishers[i].TotalStrokes
                       && !provisional)
                    j++;
                // For provisional board, no tie detection on partial strokes — each entry ranked individually
                if (provisional) j = i + 1;

                bool isTie = (j - i) > 1;

                for (int k = i; k < j; k++)
                {
                    var e = finishers[k];
                    result.Add(new TournamentLeaderboardEntry
                    {
                        Rank          = rank,
                        IsTie         = isTie,
                        DisplayName   = e.DisplayName,
                        CharacterId   = e.CharacterId,
                        Level         = e.Level,
                        Strokes       = e.Strokes,
                        Thru          = e.Thru,
                        TimeSeconds   = e.TimeSeconds,
                        IsPlayer      = e.IsPlayer,
                        IsDNF         = false,
                        IsProvisional = provisional,
                    });
                }

                nextRank = rank + (j - i); // next rank skips tied entries
                i = j;
            }

            // Player DNF (sticky "you" row — visible even though non-player DNFs are hidden)
            if (playerDnf.HasValue)
            {
                var e = playerDnf.Value;
                result.Add(new TournamentLeaderboardEntry
                {
                    Rank          = nextRank,
                    IsTie         = false,
                    DisplayName   = e.DisplayName,
                    CharacterId   = e.CharacterId,
                    Level         = e.Level,
                    Strokes       = e.Strokes,
                    Thru          = e.Thru,
                    TimeSeconds   = e.TimeSeconds,
                    IsPlayer      = true,
                    IsDNF         = true,
                    IsProvisional = provisional,
                });
            }

            return result;
        }

        // ═════════════════════════════════════════════════════════════════════
        // Private — Prize resolution
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Resolve the RP and item prize for the player given their final rank.
        /// Handles split-pool for ties spanning band boundaries (§6.2).
        /// Indivisible items are duplicated to each tied player (D-Tie rule).
        /// </summary>
        private static (long prizeRP, string? itemId) ResolvePrize(
            int playerRank,
            bool isTie,
            PrizeTable? prizeTable,
            IReadOnlyList<TournamentLeaderboardEntry> board)
        {
            if (prizeTable == null || prizeTable.Bands.Count == 0)
                return (0L, null);

            if (!isTie)
            {
                // Simple band match
                foreach (var band in prizeTable.Bands)
                    if (playerRank >= band.RankFrom && playerRank <= band.RankTo)
                        return (band.RpReward, band.ItemRewardId);
                return (0L, null);
            }

            // Tie: pool the RP of all spanned bands, split evenly rounded up (player-favorable)
            // Find the tie group (all entries sharing the player rank)
            var tieGroup = new List<TournamentLeaderboardEntry>();
            foreach (var row in board)
                if (row.Rank == playerRank && row.IsTie)
                    tieGroup.Add(row);

            if (tieGroup.Count == 0)
                tieGroup.Add(new TournamentLeaderboardEntry { Rank = playerRank });

            int tieCount = tieGroup.Count;
            int highestRank = playerRank;
            int lowestRank  = playerRank + tieCount - 1;

            // Pool RP from all spanned bands
            long totalPool = 0L;
            string? itemId = null;

            foreach (var band in prizeTable.Bands)
            {
                // Check if this band overlaps with the tie span [highestRank, lowestRank]
                if (band.RankTo < highestRank) continue;
                if (band.RankFrom > lowestRank) continue;

                // Count positions in this band that fall within the tie span
                int overlapFrom = Math.Max(band.RankFrom, highestRank);
                int overlapTo   = Math.Min(band.RankTo,   lowestRank);
                int positions   = overlapTo - overlapFrom + 1;

                totalPool += band.RpReward * positions;

                // Item: use the highest-ranked band's item (indivisible — each tied player gets one copy)
                if (itemId == null && band.ItemRewardId != null)
                    itemId = band.ItemRewardId;
            }

            // Divide equally, rounded up (player-favorable)
            long share = tieCount > 0 ? DivideRoundUp(totalPool, tieCount) : 0L;
            return (share, itemId);
        }

        private static long DivideRoundUp(long numerator, int denominator)
        {
            if (denominator <= 0) return 0L;
            return (numerator + denominator - 1) / denominator;
        }

        // ═════════════════════════════════════════════════════════════════════
        // Private — utility
        // ═════════════════════════════════════════════════════════════════════

        private static bool StringEquals(string a, string b)
            => string.Equals(a, b, StringComparison.Ordinal);
    }
}
