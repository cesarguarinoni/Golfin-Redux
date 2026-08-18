// ─────────────────────────────────────────────────────────────────────────────
// TournamentsRuntime — TournamentSubmitQueue
//
// The offline half of per-hole submission. Play is NEVER blocked by the network:
// SubmitHoleResult persists locally first (exactly as it did before this phase),
// then enqueues here, and the queue drains on reconnect / sign-in / app resume.
//
// Deliberately a NEAR-TWIN of Golfin.Economy.PendingOpsQueue rather than a reuse
// of it: that queue's op type is an RP earn (action + amount + points idempotency
// key) and its file is the ledger's. What IS reused is the storage seam —
// IPendingOpsStore / FilePendingOpsStore give the same atomic .tmp+replace write,
// because this file is the only record of a hole the player has already played.
//
// FIFO IS NOT COSMETIC. The server sets status=finished, best_score and
// submitted_at when the LAST hole lands, and submitted_at is the final board's
// tiebreak. Replaying hole 6 before hole 3 would finish the entry on the wrong
// hole and stamp the wrong time, so the drain stops at the first failure rather
// than skipping past it — the same rule PointsService.ReplayPending follows.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Golfin.Economy;
using Newtonsoft.Json;
using UnityEngine;

namespace Golfin.Tournaments
{
    /// <summary>
    /// One completed hole waiting to reach the server.
    ///
    /// <para><b>The key is minted once.</b> <see cref="IdempotencyKey"/> is generated at enqueue and
    /// is then immutable for the life of the op — through disk round-trips, app restarts and every
    /// replay attempt. That is the whole contract: the server is idempotent per (entry, hole) and
    /// answers a replay with <c>replayed:true</c> instead of a second row. Regenerating the key on
    /// retry would defeat that, so the tests assert stability explicitly.</para>
    /// </summary>
    public sealed class PendingHoleSubmit
    {
        [JsonProperty("key")]        public string? IdempotencyKey;

        /// <summary>Game-facing tournament id — the <c>{slug}</c> in the URL.</summary>
        [JsonProperty("slug")]       public string? Slug;

        /// <summary>1-based index into the tournament's hole set, which is what the server validates
        /// against. Derived from <c>HoleResult.HoleId</c> at enqueue, never re-derived on replay —
        /// the schedule can change under a long-offline player, but the hole they actually played
        /// cannot.</summary>
        [JsonProperty("hole")]       public int     HoleNumber;

        [JsonProperty("strokes")]    public int     Strokes;

        [JsonProperty("createdAt")]  public long    CreatedAtUnix;

        /// <summary>Replay attempts so far. Diagnostics only — it never influences the key.</summary>
        [JsonProperty("attempts")]   public int     AttemptCount;

        /// <summary>Parameterless ctor for Newtonsoft.</summary>
        public PendingHoleSubmit() { }

        /// <summary>Mint a new op with a fresh key and the current UTC timestamp.</summary>
        public static PendingHoleSubmit New(string slug, int holeNumber, int strokes, long? nowUnix = null)
            => new PendingHoleSubmit
            {
                IdempotencyKey = Guid.NewGuid().ToString("D"),
                Slug           = slug,
                HoleNumber     = holeNumber,
                Strokes        = strokes,
                CreatedAtUnix  = nowUnix ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                AttemptCount   = 0
            };

        /// <summary>Request body for <c>POST /golfin/{slug}/submit-hole</c>.</summary>
        public string ToRequestJson() => TournamentNetJson.Write(new TournamentSubmitHoleRequestDto
        {
            HoleNumber     = HoleNumber,
            Strokes        = Strokes,
            IdempotencyKey = IdempotencyKey
        });

        public override string ToString()
            => $"{Slug}#h{HoleNumber} x{Strokes} key={IdempotencyKey} attempts={AttemptCount}";
    }

    /// <summary>
    /// Persistent FIFO queue of unsent hole submissions, one file for the whole install
    /// (tournaments are sequential for a player; a single ordered file keeps the replay trivially
    /// correct across two concurrently-entered tournaments as well).
    ///
    /// Every mutation persists immediately: "enqueue then crash" must not lose a hole the player
    /// has already been shown as played.
    /// </summary>
    public sealed class TournamentSubmitQueue
    {
        private const string Tag = "[TournamentSubmit]";

        /// <summary>Bumped if the on-disk shape ever changes; an unknown version is discarded, not
        /// guessed at (same rule as <c>PendingOpsQueue</c>).</summary>
        public const int CurrentVersion = 1;

        /// <summary>Hard ceiling so a permanently-offline install cannot grow the file without
        /// bound. At the limit the OLDEST op is dropped — a hole that old is past its tournament's
        /// window and the server would reject it anyway.</summary>
        public const int MaxOps = 500;

        public const string DefaultFileName = "tournament_pending_holes.json";

        private readonly IPendingOpsStore _store;
        private readonly List<PendingHoleSubmit> _ops = new List<PendingHoleSubmit>();

        /// <summary><c>&lt;persistentDataPath&gt;/tournament_pending_holes.json</c>.
        /// Touches <c>Application.persistentDataPath</c>, so main thread only.</summary>
        public static string DefaultPath => Path.Combine(Application.persistentDataPath, DefaultFileName);

        /// <summary>Production ctor — the atomic file store next to the points queue.</summary>
        public TournamentSubmitQueue() : this(new FilePendingOpsStore(DefaultPath)) { }

        /// <summary>Injection ctor — tests pass <c>InMemoryPendingOpsStore</c>, or a temp-file store
        /// when the point IS the disk round-trip.</summary>
        public TournamentSubmitQueue(IPendingOpsStore store)
        {
            _store = store ?? new InMemoryPendingOpsStore();
        }

        public int Count => _ops.Count;

        public IReadOnlyList<PendingHoleSubmit> Items => _ops;

        /// <summary>Fires after any change to the contents (enqueue, dequeue, remove, clear, load).</summary>
        public event Action? OnChanged;

        // ── mutation ──────────────────────────────────────────────────────────

        /// <summary>Mint an op, append it, and persist. The returned op carries the idempotency key
        /// that will be replayed verbatim until the server acknowledges it.</summary>
        public PendingHoleSubmit Enqueue(string slug, int holeNumber, int strokes)
        {
            var op = PendingHoleSubmit.New(slug, holeNumber, strokes);
            Enqueue(op);
            return op;
        }

        public void Enqueue(PendingHoleSubmit op)
        {
            if (op == null) return;

            _ops.Add(op);
            while (_ops.Count > MaxOps)
            {
                Debug.LogWarning($"{Tag} Over {MaxOps} pending holes — dropping the oldest ({_ops[0]}).");
                _ops.RemoveAt(0);
            }
            Save();
            RaiseChanged();
        }

        /// <summary>Oldest unsent op, or null when the queue is drained.</summary>
        public PendingHoleSubmit? Peek() => _ops.Count > 0 ? _ops[0] : null;

        /// <summary>Drop the head (it reached the server, or the server rejected it). Persists.</summary>
        public PendingHoleSubmit? Dequeue()
        {
            if (_ops.Count == 0) return null;
            var op = _ops[0];
            _ops.RemoveAt(0);
            Save();
            RaiseChanged();
            return op;
        }

        /// <summary>Remove by idempotency key regardless of position. Persists when something went.</summary>
        public bool Remove(string? idempotencyKey)
        {
            if (string.IsNullOrEmpty(idempotencyKey)) return false;

            int i = _ops.FindIndex(o => o != null && o.IdempotencyKey == idempotencyKey);
            if (i < 0) return false;

            _ops.RemoveAt(i);
            Save();
            RaiseChanged();
            return true;
        }

        public void Clear()
        {
            _ops.Clear();
            Save();
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            try { OnChanged?.Invoke(); }
            catch (Exception ex) { Debug.LogError($"{Tag} OnChanged subscriber threw: {ex}"); }
        }

        // ── persistence ───────────────────────────────────────────────────────

        public void Save()
        {
            var dto = new QueueFile { version = CurrentVersion, ops = _ops };
            _store.Write(JsonConvert.SerializeObject(dto, Formatting.Indented));
        }

        /// <summary>Replace the in-memory contents with what is on disk. Safe on a missing or corrupt
        /// file: it warns and starts empty rather than throwing into whatever called it.</summary>
        public void Load()
        {
            LoadCore();
            RaiseChanged();
        }

        private void LoadCore()
        {
            _ops.Clear();

            string? json = _store.Read();
            if (string.IsNullOrWhiteSpace(json)) return;

            try
            {
                var dto = JsonConvert.DeserializeObject<QueueFile>(json!);
                if (dto == null) return;

                if (dto.version != CurrentVersion)
                {
                    Debug.LogWarning($"{Tag} Unknown queue version {dto.version} " +
                                     $"(expected {CurrentVersion}) — discarding the file.");
                    return;
                }

                if (dto.ops == null) return;

                foreach (var op in dto.ops)
                {
                    // A key-less or slug-less op cannot be replayed safely — drop it rather than
                    // risk a second row for a hole the server may already hold.
                    if (op == null || string.IsNullOrEmpty(op.IdempotencyKey) || string.IsNullOrEmpty(op.Slug))
                    {
                        Debug.LogWarning($"{Tag} Dropping a malformed pending hole (no key or slug).");
                        continue;
                    }
                    _ops.Add(op);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} Corrupt queue file, starting empty: {ex.Message}");
                _ops.Clear();
            }
        }

        [Serializable]
        private sealed class QueueFile
        {
            public int version;
            public List<PendingHoleSubmit>? ops;
        }
    }
}
