// ─────────────────────────────────────────────────────────────────────────────
// TournamentsRuntime — RemoteTournamentBackend (tournament_async_board, Phase 4)
//
// The swap ITournamentBackend was designed for: "Later: RemoteTournamentBackend
// (REST). UI code never changes." Tournaments become real async multiplayer —
// entry, per-hole submission and the leaderboard live on the server, so every
// player sees the SAME board.
//
// WRAP, DON'T FORK. Everything the server does not own — definitions, state
// derivation, the entry store, prize-band arithmetic — is DELEGATED to a wrapped
// LocalTournamentBackend. Only the four networked concerns are overridden. A fork
// would mean two rank ladders and two state machines drifting apart.
//
// THE SERVER RANKS. The board arrives sorted with rank, is_tie, thru and the
// organic bot reveal already computed (a faithful port of the local sim). Mapping
// below is verbatim, field for field. Re-ranking here would be a second source of
// truth that silently disagrees with everyone else's screen.
//
// PLAY IS NEVER BLOCKED BY THE NETWORK. SubmitHoleResult persists locally FIRST,
// exactly as it did before this phase, and only then enqueues. Entry is the one
// online-only action (decision of record: the fee is debited server-side).
//
// Lives in Assembly-CSharp, not Golfin.Tournaments: that assembly must never learn
// that a network exists, and Golfin.Net must never be added to its asmdef.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Golfin.Net;
using UnityEngine;

namespace Golfin.Tournaments
{
    // ═════════════════════════════════════════════════════════════════════════
    // Outcomes + view models
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>How an <see cref="RemoteTournamentBackend.RegisterAsync"/> attempt ended.</summary>
    public enum TournamentRegisterStatus
    {
        /// <summary>The server took the entry and debited the fee (or it was free).</summary>
        Entered,

        /// <summary>The player was already entered. No second charge, by construction: the server's
        /// spend key is a uuid5 of (user, slug).</summary>
        AlreadyEntered,

        /// <summary>The server refused for lack of Reward Points — 200 with
        /// <c>status:"insufficient"</c>, NOT an HTTP error.</summary>
        Insufficient,

        /// <summary>Unreachable / timed out / not configured. Entry is online-only by decision of
        /// record, so this is a "try again when you have signal", never a local fallback.</summary>
        Offline,

        /// <summary>The server rejected the request (window closed, bad slug, auth). Terminal.</summary>
        Rejected
    }

    /// <summary>Result of one register attempt, with the numbers the insufficient-funds UX shows.</summary>
    public readonly struct TournamentRegisterOutcome
    {
        public readonly TournamentRegisterStatus Status;

        /// <summary>Entry fee the server asked for (insufficient path only).</summary>
        public readonly long Requested;

        /// <summary>Balance the server holds (insufficient path only).</summary>
        public readonly long TotalPoints;

        /// <summary>The mirrored local entry on a success, else null.</summary>
        public readonly EntryState? Entry;

        public TournamentRegisterOutcome(
            TournamentRegisterStatus status, EntryState? entry = null, long requested = 0L, long totalPoints = 0L)
        {
            Status      = status;
            Entry       = entry;
            Requested   = requested;
            TotalPoints = totalPoints;
        }

        /// <summary>True when the player is entered and the caller may navigate into the round.</summary>
        public bool MayProceed
            => Status == TournamentRegisterStatus.Entered || Status == TournamentRegisterStatus.AlreadyEntered;
    }

    /// <summary>
    /// The caller's own row on the served board, plus the two pieces of board-level context the
    /// sticky row needs. Kept separate from <see cref="TournamentLeaderboardEntry"/> because
    /// <c>prize_rank</c> and <c>bots_active</c> are properties of the SERVER's answer, not of a row.
    /// </summary>
    public readonly struct TournamentPlayerRow
    {
        /// <summary>False when the payload had no <c>player</c> object (not entered).</summary>
        public readonly bool HasRow;

        /// <summary>The row, mapped verbatim like every other row.</summary>
        public readonly TournamentLeaderboardEntry Entry;

        /// <summary>Display rank (blended with bots while they are active). Null when unranked —
        /// entered but nothing submitted yet.</summary>
        public readonly int? Rank;

        /// <summary>Human-only rank; the one that actually pays. Bots are never paid.</summary>
        public readonly int? PrizeRank;

        /// <summary>True while the bot field is still on the board. ONE-WAY — once false for a
        /// tournament it never goes true again.</summary>
        public readonly bool BotsActive;

        public TournamentPlayerRow(
            bool hasRow, TournamentLeaderboardEntry entry, int? rank, int? prizeRank, bool botsActive)
        {
            HasRow     = hasRow;
            Entry      = entry;
            Rank       = rank;
            PrizeRank  = prizeRank;
            BotsActive = botsActive;
        }

        /// <summary>
        /// What the sticky row's rank label reads (Cesar's chosen presentation, SPEC §4).
        ///
        /// <para>While bots are active the display rank and the prize rank legitimately disagree —
        /// a player can be 14th on a board padded with bots and still 3rd among humans, which is
        /// the rank that pays. Showing only one of them is either discouraging or a lie, so the
        /// label shows BOTH: <c>#14 · PRIZE #3</c>. Once the field retires the two are equal and
        /// the label reverts to the plain rank.</para>
        ///
        /// <para>Pure and static so the format is gated by a test rather than by reading a
        /// screenshot.</para>
        /// </summary>
        public static string FormatRankLabel(int? rank, int? prizeRank, bool botsActive)
        {
            if (!rank.HasValue) return "--";                       // entered, nothing submitted yet
            if (!botsActive || !prizeRank.HasValue) return rank.Value.ToString();
            if (prizeRank.Value == rank.Value)     return rank.Value.ToString();
            return $"#{rank.Value} · PRIZE #{prizeRank.Value}";
        }

        /// <summary>The label for this row.</summary>
        public string RankLabel() => FormatRankLabel(Rank, PrizeRank, BotsActive);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Disk cache
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Raw-body disk mirror for the tournament board, one file per slug.
    ///
    /// Same discipline as <c>RemoteBannerSource</c> / <c>LeaderboardDiskCache</c>, and for the same
    /// reasons: cache the RAW body (so a later build that understands more fields can still read
    /// it), write it atomically via <c>.tmp</c> + replace (so a kill mid-write leaves the previous
    /// good cache rather than a truncated file), and return null on ANY failure.
    /// </summary>
    public static class TournamentBoardDiskCache
    {
        private const string Tag = "[TournamentBoard]";

        /// <summary><c>tournament_board_{slug}.json</c>, with anything path-hostile flattened.</summary>
        public static string CacheFileName(string slug) => "tournament_board_" + Sanitize(slug) + ".json";

        /// <summary><c>&lt;persistentDataPath&gt;/tournament_board_{slug}.json</c>.
        /// Touches <c>Application.persistentDataPath</c>, so main thread only.</summary>
        public static string CachePath(string slug) => Path.Combine(Application.persistentDataPath, CacheFileName(slug));

        public static string? ReadCache(string slug)
        {
            try
            {
                string path = CachePath(slug);
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} Could not read the '{slug}' board cache: {ex.Message}");
                return null;
            }
        }

        public static void WriteCache(string slug, string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            string path = CachePath(slug);
            try
            {
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string tmp = path + ".tmp";
                File.WriteAllText(tmp, json);

                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                // A cache we could not write is a slower next open, not a broken session.
                Debug.LogWarning($"{Tag} Could not write the '{slug}' board cache '{path}': {ex.Message}");
            }
        }

        public static void ClearCache(string slug)
        {
            try { if (File.Exists(CachePath(slug))) File.Delete(CachePath(slug)); }
            catch (Exception ex) { Debug.LogWarning($"{Tag} Could not delete the '{slug}' board cache: {ex.Message}"); }
        }

        /// <summary>A slug is server-controlled data that ends up in a file name. Anything outside
        /// <c>[A-Za-z0-9_-]</c> becomes <c>_</c> so a hostile or merely odd id cannot escape the
        /// directory.</summary>
        private static string Sanitize(string? slug)
        {
            if (string.IsNullOrEmpty(slug)) return "unknown";
            var chars = slug!.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
                if (!ok) chars[i] = '_';
            }
            return new string(chars);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // RemoteTournamentBackend
    // ═════════════════════════════════════════════════════════════════════════

    public sealed class RemoteTournamentBackend : ITournamentBackend, ITournamentStateDeriver
    {
        private const string Tag = "[TournamentRemote]";

        /// <summary>What the leaderboard screen renders for one tournament between refreshes.</summary>
        private sealed class BoardSnapshot
        {
            public IReadOnlyList<TournamentLeaderboardEntry> Entries = Array.Empty<TournamentLeaderboardEntry>();
            public TournamentPlayerRow Player;
            public bool Provisional = true;
            public bool BotsActive  = true;
        }

        // ── Wrapped local backend + seams ─────────────────────────────────────
        private readonly LocalTournamentBackend                  _local;
        private readonly ITournamentEntryStore                   _store;
        private readonly IRewardPointsService                    _rp;
        private readonly IItemRewardService                      _items;
        private readonly IReadOnlyDictionary<string, PrizeTable> _prizeTables;
        private readonly ITournamentClock                        _clock;
        private readonly TournamentSubmitQueue                   _queue;

        private readonly Dictionary<string, BoardSnapshot> _boards =
            new Dictionary<string, BoardSnapshot>(StringComparer.Ordinal);

        /// <summary>One in-flight refresh per slug. The screen calls Refresh from OnEnable, and a
        /// player bouncing in and out must not turn into a request per tap.</summary>
        private readonly HashSet<string> _inFlightBoards = new HashSet<string>(StringComparer.Ordinal);

        private bool _flushInFlight;

        /// <summary>
        /// Where fire-and-forget coroutines go. Defaults to <c>ApiClient.Instance.Run</c>, which
        /// needs a scene host — settable so an EditMode test can collect the routines and pump them
        /// itself instead of spawning a runner GameObject.
        /// </summary>
        public Action<IEnumerator>? CoroutineRunner { get; set; }

        /// <summary>Called after a successful entry so the nav-bar counter picks up the fee the
        /// SERVER debited. Defaults to the rp_balance_sync refresh; settable for tests.</summary>
        public Action? BalanceRefresh { get; set; }

        /// <summary>The pending-hole queue, exposed for diagnostics and tests.</summary>
        public TournamentSubmitQueue Queue => _queue;

        public RemoteTournamentBackend(
            LocalTournamentBackend                  local,
            ITournamentEntryStore                   store,
            IRewardPointsService                    rp,
            IItemRewardService                      items,
            IReadOnlyDictionary<string, PrizeTable> prizeTables,
            ITournamentClock                        clock,
            TournamentSubmitQueue?                  queue = null)
        {
            _local       = local       ?? throw new ArgumentNullException(nameof(local));
            _store       = store       ?? throw new ArgumentNullException(nameof(store));
            _rp          = rp          ?? throw new ArgumentNullException(nameof(rp));
            _items       = items       ?? throw new ArgumentNullException(nameof(items));
            _prizeTables = prizeTables ?? throw new ArgumentNullException(nameof(prizeTables));
            _clock       = clock       ?? throw new ArgumentNullException(nameof(clock));
            _queue       = queue       ?? new TournamentSubmitQueue();
            _queue.Load();
        }

        // ═════════════════════════════════════════════════════════════════════
        // Delegated — the server does not own these
        // ═════════════════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public IReadOnlyList<TournamentDefinition> GetTournaments() => _local.GetTournaments();

        /// <inheritdoc/>
        public TournamentDefinition GetTournament(string id) => _local.GetTournament(id);

        /// <inheritdoc/>
        public TournamentState DeriveState(TournamentDefinition def, DateTime now) => _local.DeriveState(def, now);

        /// <inheritdoc/>
        /// <remarks>The local store is the read model on purpose: the gameplay flow reads the entry
        /// synchronously mid-round and cannot wait on a socket. <see cref="ReconcileEntryRoutine"/>
        /// is what keeps it honest across devices.</remarks>
        public EntryState? GetMyEntry(string id) => _local.GetMyEntry(id);

        // ═════════════════════════════════════════════════════════════════════
        // Register
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The synchronous seam. It CANNOT report insufficient funds or a closed window (the
        /// interface has nowhere to put them), so the sign-up modal calls
        /// <see cref="RegisterAsync"/> instead and this exists for the remaining non-modal callers
        /// (dev entry button, harnesses).
        ///
        /// <para><b><paramref name="entryPaymentRP"/> is deliberately ignored.</b> The server debits
        /// the fee itself via <c>spend_pts</c> with a deterministic uuid5(user:slug) key. Debiting
        /// here as well — through <see cref="IRewardPointsService"/> or anything else — would charge
        /// the player twice for one entry, so the mirror is always created at a fee of 0 and the POST
        /// is what actually pays.</para>
        /// </summary>
        public EntryState Register(string id, long entryPaymentRP, string characterId)
        {
            var existing = _local.GetMyEntry(id);
            if (existing != null) return existing;

            if (entryPaymentRP > 0)
                Debug.LogWarning(
                    $"{Tag} Register('{id}') was called with a fee of {entryPaymentRP}RP on the REMOTE path. " +
                    "The server debits the entry fee itself (deterministic spend key) — the fee argument is " +
                    "ignored so the player is not charged twice. Prefer RegisterAsync, which can also " +
                    "report insufficient funds.");

            // Fee forced to 0: the local mirror must never touch IRewardPointsService here.
            EntryState entry = _local.Register(id, 0L, characterId);

            Run(EnterRoutine(id, characterId, null));
            return entry;
        }

        /// <summary>
        /// Enter <paramref name="id"/> server-side and report the outcome.
        ///
        /// The POST comes FIRST and the local mirror only follows a success — an unreachable server
        /// must not leave the player holding an entry the tournament has no record of.
        /// </summary>
        public void RegisterAsync(string id, string characterId, Action<TournamentRegisterOutcome>? onDone)
        {
            var existing = _local.GetMyEntry(id);
            if (existing != null)
            {
                onDone?.Invoke(new TournamentRegisterOutcome(TournamentRegisterStatus.AlreadyEntered, existing));
                return;
            }

            Run(EnterRoutine(id, characterId, onDone));
        }

        /// <summary>The coroutine behind both register paths. Pumped explicitly (rather than
        /// <c>yield return post</c>) to match <c>ApiClient</c>'s convention, so it also runs under a
        /// plain <c>while (MoveNext())</c> in an EditMode test.</summary>
        public IEnumerator EnterRoutine(string id, string characterId, Action<TournamentRegisterOutcome>? onDone)
        {
            string body = TournamentNetJson.Write(new TournamentEnterRequestDto { CharacterId = characterId });

            ApiResult<string>? result = null;
            IEnumerator post = ApiClient.Instance.Post<string>(
                Endpoints.TournamentEnter(id), body, r => result = r);
            while (post.MoveNext()) yield return post.Current;

            if (result == null || !result.Success)
            {
                var kind = result?.ErrorKind ?? ApiErrorKind.Network;
                bool offline = kind == ApiErrorKind.Network || kind == ApiErrorKind.Timeout
                            || kind == ApiErrorKind.NotConfigured || kind == ApiErrorKind.Disabled;

                Debug.LogWarning($"{Tag} enter('{id}') failed ({kind}, HTTP {result?.StatusCode ?? 0}): " +
                                 $"{result?.ErrorMessage}. Nothing was charged and no entry was created.");

                onDone?.Invoke(new TournamentRegisterOutcome(
                    offline ? TournamentRegisterStatus.Offline : TournamentRegisterStatus.Rejected));
                yield break;
            }

            var dto = TournamentNetJson.Read<TournamentEnterResponseDto>(result.RawBody, $"enter:{id}");
            if (dto == null)
            {
                Debug.LogWarning($"{Tag} enter('{id}') returned a body this build cannot read — treated as a rejection.");
                onDone?.Invoke(new TournamentRegisterOutcome(TournamentRegisterStatus.Rejected));
                yield break;
            }

            if (dto.IsInsufficient)
            {
                Debug.Log($"{Tag} enter('{id}') refused — insufficient points " +
                          $"(needed {dto.Requested}, holds {dto.TotalPoints}).");
                onDone?.Invoke(new TournamentRegisterOutcome(
                    TournamentRegisterStatus.Insufficient, null, dto.Requested, dto.TotalPoints));
                yield break;
            }

            if (!dto.Entered && !dto.AlreadyEntered)
            {
                Debug.LogWarning($"{Tag} enter('{id}') answered neither entered nor already_entered " +
                                 $"(status='{dto.Status}') — treated as a rejection.");
                onDone?.Invoke(new TournamentRegisterOutcome(TournamentRegisterStatus.Rejected));
                yield break;
            }

            // Mirror the entry locally so the gameplay flow — which reads it synchronously mid-round
            // — sees it immediately. Fee 0: the server already took the points.
            EntryState mirrored = _local.Register(id, 0L, characterId);

            // The fee left the ledger server-side, so the local counter is now stale by exactly the
            // entry fee. This is the pull that makes it right without a second debit.
            InvokeBalanceRefresh();

            onDone?.Invoke(new TournamentRegisterOutcome(
                dto.AlreadyEntered ? TournamentRegisterStatus.AlreadyEntered : TournamentRegisterStatus.Entered,
                mirrored));
        }

        // ═════════════════════════════════════════════════════════════════════
        // SubmitHoleResult
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Local persist FIRST, then enqueue. The order is the whole design: a player who holes out
        /// in a tunnel has finished that hole, and nothing about the network may take it back.
        /// </summary>
        public EntryState SubmitHoleResult(string id, HoleResult result)
        {
            EntryState updated = _local.SubmitHoleResult(id, result);

            int holeNumber = HoleNumberFor(id, result?.HoleId);
            if (holeNumber <= 0)
            {
                // The hole is not in this tournament's set, so the server would answer 400 forever.
                // Local play already succeeded; log loudly and do not poison the queue.
                Debug.LogError($"{Tag} '{result?.HoleId}' is not in the hole set for '{id}' — " +
                               "the result is saved locally but cannot be submitted to the server.");
                return updated;
            }

            _queue.Enqueue(id, holeNumber, result!.Strokes);
            FlushSubmitQueue();
            return updated;
        }

        /// <summary>1-based index of <paramref name="holeId"/> in the tournament's hole set — what
        /// the server validates against — or 0 when it is not in the set.</summary>
        internal int HoleNumberFor(string id, string? holeId)
        {
            if (string.IsNullOrEmpty(holeId)) return 0;

            TournamentDefinition def;
            try { def = _local.GetTournament(id); }
            catch (KeyNotFoundException) { return 0; }

            for (int i = 0; i < def.HoleSet.Count; i++)
                if (string.Equals(def.HoleSet[i], holeId, StringComparison.Ordinal)) return i + 1;

            return 0;
        }

        /// <summary>Fire-and-forget drain. Safe to call on every submit, reconnect, sign-in and
        /// resume — a second call while one is running is dropped, not queued.</summary>
        public void FlushSubmitQueue() => Run(FlushSubmitQueueRoutine(null));

        /// <summary>
        /// Drain the queue in strict FIFO, stopping at the first op that did not reach the server.
        ///
        /// <para><b>Drop rules (SPEC §3).</b> A 200 is done. <c>replayed:true</c> is ALSO done — it
        /// is the server saying "I already have this hole", which is exactly what an ambiguous
        /// timeout followed by a retry looks like. A 400 is a REJECTION (hole not in set, strokes
        /// out of range, window past the resolve-delay grace, implausible pace): retrying it forever
        /// would wedge the queue behind an op that can never succeed, so it is logged and dropped.
        /// Everything else — offline, 5xx, auth — keeps the op and stops the drain.</para>
        /// </summary>
        /// <param name="onDone">Receives the number of ops that left the queue.</param>
        public IEnumerator FlushSubmitQueueRoutine(Action<int>? onDone)
        {
            if (_flushInFlight)
            {
                onDone?.Invoke(0);
                yield break;
            }

            _flushInFlight = true;
            int drained = 0;

            try
            {
                while (true)
                {
                    PendingHoleSubmit? op = _queue.Peek();
                    if (op == null) break;

                    op.AttemptCount++;

                    ApiResult<string>? result = null;
                    IEnumerator post = ApiClient.Instance.Post<string>(
                        Endpoints.TournamentSubmitHole(op.Slug!), op.ToRequestJson(), r => result = r);
                    while (post.MoveNext()) yield return post.Current;

                    if (result != null && result.Success)
                    {
                        var dto = TournamentNetJson.Read<TournamentSubmitHoleResponseDto>(
                            result.RawBody, $"submit-hole:{op.Slug}");

                        if (dto != null && dto.Replayed)
                            Debug.Log($"{Tag} {op} was already on the server (replayed) — dropping the op.");

                        _queue.Dequeue();
                        drained++;
                        continue;
                    }

                    // A 400 is the server refusing this hole on its merits. It will refuse the same
                    // body every time, so keeping it would block every hole behind it forever.
                    if (result != null && result.StatusCode == 400)
                    {
                        Debug.LogWarning($"{Tag} Server REJECTED {op}: {result.ErrorMessage}. " +
                                         "Dropping the op — a 400 is a verdict, not a transient failure.");
                        _queue.Dequeue();
                        drained++;
                        continue;
                    }

                    // Transient. Stop here rather than skipping past: the server finishes the entry
                    // on the LAST hole and stamps submitted_at, which is the final board's tiebreak,
                    // so out-of-order replay would finish it on the wrong hole.
                    _queue.Save();   // persist the bumped attempt count
                    Debug.Log($"{Tag} {op} not delivered ({result?.ErrorKind.ToString() ?? "no result"}) — " +
                              $"{_queue.Count} hole(s) still queued; will retry on reconnect.");
                    break;
                }
            }
            finally
            {
                _flushInFlight = false;
            }

            onDone?.Invoke(drained);
        }

        // ═════════════════════════════════════════════════════════════════════
        // Entry reconcile (cross-device resume)
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Fire-and-forget reconcile. Driven from sign-in and app resume.</summary>
        public void ReconcileEntry(string id) => Run(ReconcileEntryRoutine(id, null));

        /// <summary>
        /// GET the caller's entry and fold it into the local store: the player who played holes 1–3
        /// on a phone must find holes 1–3 done on a tablet.
        ///
        /// <para><b>Server wins on conflict, local-only holes survive.</b> For a hole BOTH sides
        /// know, the server's strokes are authoritative. A hole only the client knows is one still
        /// sitting in the submit queue — dropping it would erase a hole the player actually played,
        /// so it is kept and the queue delivers it. The union is taken by hole number.</para>
        /// </summary>
        public IEnumerator ReconcileEntryRoutine(string id, Action<bool>? onDone)
        {
            ApiResult<string>? result = null;
            IEnumerator get = ApiClient.Instance.Get<string>(Endpoints.TournamentEntry(id), r => result = r);
            while (get.MoveNext()) yield return get.Current;

            if (result == null || !result.Success)
            {
                Debug.LogWarning($"{Tag} entry('{id}') fetch failed " +
                                 $"({result?.ErrorKind}, HTTP {result?.StatusCode ?? 0}) — keeping the local entry.");
                onDone?.Invoke(false);
                yield break;
            }

            var dto = TournamentNetJson.Read<TournamentEntryDto>(result.RawBody, $"entry:{id}");
            if (dto == null)
            {
                // {"data": null} — the server has no entry for this caller. NOT a reason to delete a
                // local one: the enter POST may simply not have landed yet.
                onDone?.Invoke(false);
                yield break;
            }

            bool changed = ApplyServerEntry(id, dto);
            onDone?.Invoke(changed);
        }

        /// <summary>The pure half of <see cref="ReconcileEntryRoutine"/> — no network, so a test can
        /// drive the merge directly.</summary>
        internal bool ApplyServerEntry(string id, TournamentEntryDto dto)
        {
            TournamentDefinition def;
            try { def = _local.GetTournament(id); }
            catch (KeyNotFoundException)
            {
                Debug.LogWarning($"{Tag} entry('{id}') arrived for a tournament this build does not know — ignored.");
                return false;
            }

            EntryState? localEntry = _store.Load(id);

            // Index the local holes by hole number so the union is by hole, not by list position.
            var merged = new SortedDictionary<int, HoleResult>();
            if (localEntry != null)
            {
                foreach (HoleResult hr in localEntry.PerHole)
                {
                    int n = HoleNumberFor(id, hr.HoleId);
                    if (n > 0) merged[n] = hr;
                }
            }

            int replaced = 0;
            if (dto.Holes != null)
            {
                foreach (TournamentHoleDto? h in dto.Holes)
                {
                    if (h == null) continue;
                    if (h.HoleNumber < 1 || h.HoleNumber > def.HoleSet.Count) continue;

                    string holeId = def.HoleSet[h.HoleNumber - 1];
                    DateTime completed = TournamentNetJson.ParseUtc(h.SubmittedAt) ?? _clock.UtcNow;

                    // Server wins for a hole both sides hold. TimeSeconds, the RNG seed and the input
                    // log are NOT part of the server contract, so whatever the local copy knows is
                    // preserved rather than zeroed — a re-sim would otherwise lose its evidence.
                    HoleResult? mine = merged.TryGetValue(h.HoleNumber, out HoleResult existing) ? existing : null;
                    if (mine == null || mine.Strokes != h.Strokes) replaced++;

                    merged[h.HoleNumber] = new HoleResult(
                        holeId:       holeId,
                        strokes:      h.Strokes,
                        timeSeconds:  mine?.TimeSeconds ?? 0f,
                        completedUtc: completed,
                        rngSeed:      mine?.RngSeed ?? 0,
                        inputLog:     mine?.InputLog ?? new List<ShotCommand>());
                }
            }

            var perHole = new List<HoleResult>(merged.Count);
            foreach (var kv in merged) perHole.Add(kv.Value);

            bool finished = perHole.Count >= def.HoleSet.Count
                            || string.Equals(dto.Status, "finished", StringComparison.OrdinalIgnoreCase);

            DateTime startedUtc = TournamentNetJson.ParseUtc(dto.EnteredAt)
                                  ?? localEntry?.StartedUtc
                                  ?? _clock.UtcNow;

            DateTime? lastHoleUtc = perHole.Count > 0 ? perHole[perHole.Count - 1].CompletedUtc : localEntry?.LastHoleUtc;

            var reconciled = new EntryState(
                tournamentId:       id,
                characterId:        !string.IsNullOrEmpty(dto.CharacterId) ? dto.CharacterId!
                                    : (localEntry?.CharacterId ?? string.Empty),
                snapshot:           localEntry?.Snapshot,   // frozen at sign-up; the server has no copy
                perHole:            perHole,
                startedUtc:         startedUtc,
                lastHoleUtc:        lastHoleUtc,
                status:             finished ? EntryStatus.Finished : EntryStatus.InProgress,
                conditionRemaining: localEntry?.ConditionRemaining ?? -1f);

            _store.Save(reconciled);

            Debug.Log($"{Tag} Reconciled '{id}' from the server: {perHole.Count} hole(s) " +
                      $"({replaced} taken from the server), status={reconciled.Status}.");
            return true;
        }

        // ═════════════════════════════════════════════════════════════════════
        // Leaderboard
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The board, verbatim from the last payload (or the disk cache loaded on first ask).
        ///
        /// <para>Empty — NOT a locally simulated board — when nothing has been fetched or cached.
        /// Falling back to the local sim here would show phantom bots that no other player can see,
        /// which is precisely the thing this phase exists to end. The screen leaves whatever rows
        /// are already up when handed an empty list.</para>
        /// </summary>
        public IReadOnlyList<TournamentLeaderboardEntry> GetLeaderboard(string id)
            => EnsureSnapshot(id)?.Entries ?? Array.Empty<TournamentLeaderboardEntry>();

        /// <summary>The caller's own row plus <c>prize_rank</c> / <c>bots_active</c> — what the
        /// sticky row binds to. <c>HasRow == false</c> when not entered or nothing is cached.</summary>
        public TournamentPlayerRow GetPlayerRow(string id)
            => EnsureSnapshot(id)?.Player ?? default;

        /// <summary>True while the tournament window is open, per the SERVER's answer.</summary>
        public bool IsProvisional(string id) => EnsureSnapshot(id)?.Provisional ?? true;

        /// <summary>Fire-and-forget refresh of one board. <paramref name="onDone"/> gets true only
        /// when a new snapshot was actually stored; false covers offline, a bad body, AND a
        /// duplicate call while one is in flight — all of which mean "nothing changed".</summary>
        public void RefreshLeaderboard(string id, Action<bool>? onDone = null)
            => Run(RefreshLeaderboardRoutine(id, onDone));

        /// <inheritdoc cref="RefreshLeaderboard"/>
        public IEnumerator RefreshLeaderboardRoutine(string id, Action<bool>? onDone = null)
        {
            if (!_inFlightBoards.Add(id))
            {
                onDone?.Invoke(false);
                yield break;
            }

            try
            {
                ApiResult<string>? result = null;
                IEnumerator get = ApiClient.Instance.Get<string>(Endpoints.TournamentLeaderboard(id), r => result = r);
                while (get.MoveNext()) yield return get.Current;

                if (result == null || !result.Success || string.IsNullOrWhiteSpace(result.RawBody))
                {
                    // Expected offline. Warning, not error: keeping the cached board is the design.
                    Debug.LogWarning($"{Tag} board('{id}') fetch failed " +
                                     $"({result?.ErrorKind}, HTTP {result?.StatusCode ?? 0}): {result?.ErrorMessage}. " +
                                     "Keeping the cached board.");
                    onDone?.Invoke(false);
                    yield break;
                }

                BoardSnapshot? snap = BuildSnapshot(result.RawBody!, id, "server");
                if (snap == null)
                {
                    onDone?.Invoke(false);
                    yield break;
                }

                _boards[id] = snap;

                // Mirrored AFTER a successful parse, so a body this build cannot render never
                // replaces a cache it can.
                TournamentBoardDiskCache.WriteCache(id, result.RawBody!);
                onDone?.Invoke(true);
            }
            finally
            {
                _inFlightBoards.Remove(id);
            }
        }

        /// <summary>In-memory snapshot, loading the disk cache once on first ask so a cold open
        /// renders the last board the player saw before the request that will replace it has even
        /// been sent.</summary>
        private BoardSnapshot? EnsureSnapshot(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_boards.TryGetValue(id, out var snap)) return snap;

            string? cached = TournamentBoardDiskCache.ReadCache(id);
            BoardSnapshot? built = string.IsNullOrWhiteSpace(cached) ? null : BuildSnapshot(cached!, id, "disk cache");

            // Cache the miss too, so a board with no cache does not hit the disk on every OnEnable.
            _boards[id] = built ?? new BoardSnapshot();
            return _boards[id];
        }

        /// <summary>Parse + map one raw body into a snapshot, or null when it is unusable.</summary>
        private static BoardSnapshot? BuildSnapshot(string json, string id, string source)
        {
            var dto = TournamentNetJson.Read<TournamentBoardDto>(json, $"board:{id} ({source})");
            if (dto == null) return null;

            return new BoardSnapshot
            {
                Entries     = MapEntries(dto),
                Player      = MapPlayer(dto),
                Provisional = dto.Provisional,
                BotsActive  = dto.BotsActive
            };
        }

        /// <summary>
        /// Payload rows → <see cref="TournamentLeaderboardEntry"/>, field for field.
        /// Rank, IsTie and Thru are COPIED, never recomputed — the server owns the ranking (SPEC §1).
        /// </summary>
        internal static IReadOnlyList<TournamentLeaderboardEntry> MapEntries(TournamentBoardDto dto)
        {
            if (dto.Entries == null || dto.Entries.Count == 0) return Array.Empty<TournamentLeaderboardEntry>();

            var list = new List<TournamentLeaderboardEntry>(dto.Entries.Count);
            foreach (TournamentBoardRowDto? row in dto.Entries)
            {
                if (row == null) continue;
                list.Add(MapRow(row, dto.Provisional));
            }
            return list;
        }

        /// <summary>The <c>player</c> object, with the two board-level flags the sticky row needs.</summary>
        internal static TournamentPlayerRow MapPlayer(TournamentBoardDto dto)
        {
            if (dto.Player == null) return default;

            return new TournamentPlayerRow(
                hasRow:     true,
                entry:      MapRow(dto.Player, dto.Provisional),
                rank:       dto.Player.Rank,
                prizeRank:  dto.Player.PrizeRank,
                botsActive: dto.BotsActive);
        }

        private static TournamentLeaderboardEntry MapRow(TournamentBoardRowDto row, bool provisional)
            => new TournamentLeaderboardEntry
            {
                // rank:null means unranked (entered, nothing submitted). 0 is what the widgets
                // already treat as "no rank to draw".
                Rank          = row.Rank ?? 0,
                IsTie         = row.IsTie,
                DisplayName   = row.DisplayName ?? string.Empty,
                CharacterId   = row.CharacterId ?? string.Empty,
                Level         = row.Level,
                Strokes       = row.Strokes,
                Thru          = row.Thru,

                // The server's tiebreak is submission order, and the time column is not displayed
                // (GDD §17.2 removed it), so there is no honest number to put here. 0, not a guess.
                TimeSeconds   = 0f,

                IsPlayer      = row.IsPlayer,
                IsDNF         = row.IsDnf,
                IsProvisional = provisional
            };

        // ═════════════════════════════════════════════════════════════════════
        // Results + prize
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The player's final standing, taken from the SERVER's final board.
        ///
        /// <para>The rank that pays is <c>player.prize_rank</c> — the human-only rank. Using the
        /// display rank would pay a player for a position bots were padding. The prize AMOUNT still
        /// comes from the served prize bands, through the same
        /// <c>LocalTournamentBackend.ResolvePrize</c> ladder (tie split-pool included) rather than a
        /// second copy of that arithmetic.</para>
        ///
        /// <para>Falls back to the wrapped local sim only when no server board has ever been seen for
        /// this tournament — better a stale answer than an empty result modal.</para>
        /// </summary>
        public TournamentResult? GetResults(string id)
        {
            TournamentDefinition def;
            try { def = _local.GetTournament(id); }
            catch (KeyNotFoundException) { return null; }

            if (!LocalTournamentBackend.IsResolved(def, _clock.UtcNow)) return null;

            EntryState? entry = _store.Load(id);
            if (entry == null) return null;

            BoardSnapshot? snap = EnsureSnapshot(id);
            TournamentPlayerRow player = snap?.Player ?? default;

            if (!player.HasRow || !(player.PrizeRank ?? player.Rank).HasValue)
            {
                Debug.LogWarning($"{Tag} No server board for '{id}' yet — falling back to the local " +
                                 "result so the modal has something to show.");
                return _local.GetResults(id);
            }

            int finalRank = (player.PrizeRank ?? player.Rank)!.Value;
            _prizeTables.TryGetValue(def.PrizeTableId, out var prizeTable);

            var (prizeRP, itemId) = LocalTournamentBackend.ResolvePrize(
                finalRank, player.Entry.IsTie, prizeTable, snap!.Entries);

            return new TournamentResult(
                finalRank:    finalRank,
                isTie:        player.Entry.IsTie,
                prizeRP:      prizeRP,
                itemRewardId: itemId,
                claimed:      _store.IsClaimed(id));
        }

        /// <summary>
        /// Grant the prize once.
        ///
        /// <para>NOTE — PHASE 5 CUTOVER POINT. The award still runs through the EXISTING client path
        /// (<c>IRewardPointsService.Grant</c> → the <c>tournament_prize</c> earn-game action, which
        /// is idempotency-keyed and capped server-side). Phase 5 adds the server-side resolver and
        /// payout, at which point this method stops granting anything and becomes a read of what the
        /// server already credited — the rank it pays on (<c>prize_rank</c>) is already the server's
        /// answer, which is what makes that swap a one-method change (decision of record #5).</para>
        /// </summary>
        public void ClaimPrize(string id)
        {
            if (_store.IsClaimed(id)) return;

            TournamentResult? result = GetResults(id);
            if (result == null) return;

            if (result.PrizeRP > 0)      _rp.Grant(result.PrizeRP);
            if (result.ItemRewardId != null) _items.Grant(result.ItemRewardId, 1);

            _store.MarkClaimed(id);
        }

        // ═════════════════════════════════════════════════════════════════════
        // Helpers
        // ═════════════════════════════════════════════════════════════════════

        private void Run(IEnumerator routine)
        {
            if (routine == null) return;

            Action<IEnumerator>? runner = CoroutineRunner;
            if (runner != null) { runner(routine); return; }

            ApiClient.Instance.Run(routine);
        }

        private void InvokeBalanceRefresh()
        {
            Action? refresh = BalanceRefresh;
            try
            {
                if (refresh != null) refresh();
                else Golfin.EconomyRuntime.ServerBalanceSyncBehaviour.RequestRefresh("tournament-entry");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} Balance refresh after entry threw: {ex.Message}");
            }
        }
    }
}
