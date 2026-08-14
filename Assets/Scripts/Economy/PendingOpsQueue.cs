// Order: reward_points_backend Slice 1 — persistent FIFO queue of unsent earns.
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Golfin.Economy
{
    /// <summary>
    /// The offline half of "online-required spends, queued earns" (SPEC decision of record #2).
    ///
    /// Strict FIFO. Ordering is not cosmetic: the server couples every earn to avatar XP and levels up
    /// on a running total, and <c>daily_cap</c> is evaluated against what has already landed today — so
    /// replaying out of order can produce a different level and a different set of capped/uncapped
    /// awards than the player actually earned. <see cref="PointsService"/> therefore stops the replay at
    /// the first failure rather than skipping past it.
    ///
    /// Every mutation persists immediately. The queue is the only local record of an earn the player has
    /// already been shown, so "enqueue then crash" must not lose it.
    /// </summary>
    public sealed class PendingOpsQueue
    {
        /// <summary>Bumped if the on-disk shape ever changes; an unknown version is discarded, not guessed at.</summary>
        public const int CurrentVersion = 1;

        /// <summary>Hard ceiling so a permanently-offline install cannot grow the file without bound.
        /// At the limit the OLDEST op is dropped (it is the one most likely already superseded).</summary>
        public const int MaxOps = 500;

        private readonly IPendingOpsStore _store;
        private readonly List<PendingPointsOp> _ops = new List<PendingPointsOp>();

        public PendingOpsQueue(IPendingOpsStore store)
        {
            _store = store ?? new InMemoryPendingOpsStore();
        }

        public int Count => _ops.Count;

        public IReadOnlyList<PendingPointsOp> Items => _ops;

        /// <summary>
        /// Fires after ANY change to the contents (enqueue, dequeue, remove, clear, load).
        ///
        /// rp_balance_sync §3.4: the number the player sees is <c>server balance + pending earns</c>,
        /// so whoever renders it has to recompute when the pending half moves — not only when the
        /// server half does. Without this, a fresh earn's points would vanish from the display the
        /// instant the queue drained them into the server total.
        /// </summary>
        public event Action OnChanged;

        /// <summary>
        /// Sum of the amounts of every queued earn — RP the player has already been shown and paid
        /// locally, but which the server ledger has not accepted yet.
        ///
        /// Non-positive amounts are skipped: those are catalog-fixed actions where the server picks
        /// the value (see <see cref="PendingPointsOp.ToEarnGameJson"/>), so the client has no honest
        /// figure to add and guessing one would make the counter jump on flush.
        /// </summary>
        public int PendingEarnTotal
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _ops.Count; i++)
                {
                    PendingPointsOp op = _ops[i];
                    if (op == null || op.Kind != PendingOpKind.Earn || op.Amount <= 0) continue;
                    total += op.Amount;
                }
                return total;
            }
        }

        // ── mutation ──────────────────────────────────────────────────────────────

        /// <summary>Mint an earn op, append it, and persist. The returned op carries the idempotency key
        /// that will be replayed verbatim until the server acknowledges it.</summary>
        public PendingPointsOp EnqueueEarn(string action, int amount)
        {
            var op = PendingPointsOp.NewEarn(action, amount);
            Enqueue(op);
            return op;
        }

        public void Enqueue(PendingPointsOp op)
        {
            if (op == null) return;

            _ops.Add(op);
            while (_ops.Count > MaxOps)
            {
                Debug.LogWarning($"[PendingOpsQueue] Over {MaxOps} pending ops — dropping the oldest ({_ops[0]}).");
                _ops.RemoveAt(0);
            }
            Save();
            RaiseChanged();
        }

        /// <summary>Oldest unsent op, or null when the queue is drained.</summary>
        public PendingPointsOp Peek() => _ops.Count > 0 ? _ops[0] : null;

        /// <summary>Drop the head (it reached the server). Persists.</summary>
        public PendingPointsOp Dequeue()
        {
            if (_ops.Count == 0) return null;
            var op = _ops[0];
            _ops.RemoveAt(0);
            Save();
            RaiseChanged();
            return op;
        }

        /// <summary>Remove by idempotency key regardless of position. Persists when something went.</summary>
        public bool Remove(string idempotencyKey)
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

        /// <summary>Notify listeners that the pending total may have moved. Never lets a bad subscriber
        /// take down the queue mutation that caused it.</summary>
        private void RaiseChanged()
        {
            try { OnChanged?.Invoke(); }
            catch (Exception ex) { Debug.LogError($"[PendingOpsQueue] OnChanged subscriber threw: {ex}"); }
        }

        // ── persistence ───────────────────────────────────────────────────────────

        public void Save()
        {
            var dto = new QueueFile { version = CurrentVersion, ops = _ops };
            _store.Write(JsonConvert.SerializeObject(dto, Formatting.Indented));
        }

        /// <summary>Replace the in-memory contents with what is on disk. Safe on a missing/corrupt file:
        /// it warns and starts empty rather than throwing into whatever called it.</summary>
        public void Load()
        {
            LoadCore();
            RaiseChanged();
        }

        private void LoadCore()
        {
            _ops.Clear();

            string json = _store.Read();
            if (string.IsNullOrWhiteSpace(json)) return;

            try
            {
                var dto = JsonConvert.DeserializeObject<QueueFile>(json);
                if (dto == null) return;

                if (dto.version != CurrentVersion)
                {
                    Debug.LogWarning($"[PendingOpsQueue] Unknown queue version {dto.version} " +
                                     $"(expected {CurrentVersion}) — discarding the file.");
                    return;
                }

                if (dto.ops == null) return;

                foreach (var op in dto.ops)
                {
                    // A key-less op cannot be replayed safely (no double-credit protection) — drop it
                    // rather than risk crediting twice.
                    if (op == null || string.IsNullOrEmpty(op.IdempotencyKey) || string.IsNullOrEmpty(op.Action))
                    {
                        Debug.LogWarning("[PendingOpsQueue] Dropping a malformed pending op (no key or action).");
                        continue;
                    }
                    _ops.Add(op);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PendingOpsQueue] Corrupt queue file, starting empty: {ex.Message}");
                _ops.Clear();
            }
        }

        [Serializable]
        private sealed class QueueFile
        {
            public int version;
            public List<PendingPointsOp> ops;
        }
    }
}
