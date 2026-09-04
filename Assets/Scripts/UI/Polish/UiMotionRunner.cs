// ─────────────────────────────────────────────────────────────────────────────
// gps_polish §D1 — the disable hook behind UiMotion.
//
// Unity stops a MonoBehaviour's coroutines the moment the component is disabled
// or its GameObject deactivated, and it does so WITHOUT running the rest of the
// routine. Every ScreenManager.ApplyScreen call does exactly that to the screen
// being left. So a screen swapped out one frame into a 0.25 s push would come
// back with its ContentContainer parked at +978 px — off screen, and permanently,
// because nothing on the next entry moves it back.
//
// This component is the fix, and it is deliberately invisible: UiMotion adds it
// on demand, it holds nothing but a list of "what this tween's final state is",
// and OnDisable settles every live tween before Unity throws the coroutines away.
// No call site ever names it.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Golfin.UI.Polish
{
    /// <summary>
    /// Per-GameObject bookkeeping for <see cref="UiMotion"/>. Added automatically; never
    /// authored on a prefab, never referenced by a call site.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]           // hidden from Add Component — this is machinery
    public sealed class UiMotionRunner : MonoBehaviour
    {
        /// <summary>One live tween and the state it must end on.</summary>
        internal sealed class Entry
        {
            public Action? Finalize;
            public bool    Done;
        }

        private readonly List<Entry> _live = new List<Entry>();
        private readonly Dictionary<Coroutine, Entry> _byHandle = new Dictionary<Coroutine, Entry>();

        /// <summary>Live tween count. Read by tests and by the invariants probe.</summary>
        public int LiveCount => _live.Count;

        // ── UiMotion's entry points ──────────────────────────────────────────

        internal static UiMotionRunner? For(MonoBehaviour host, bool create)
        {
            if (host == null) return null;
            var runner = host.gameObject.GetComponent<UiMotionRunner>();
            if (runner == null && create) runner = host.gameObject.AddComponent<UiMotionRunner>();
            return runner;
        }

        internal Entry CreateEntry(Action? finalize) => new Entry { Finalize = finalize };

        /// <summary>The wrapper UiMotion actually starts. Owns the entry's lifetime.</summary>
        internal IEnumerator Drive(IEnumerator inner, Entry entry)
        {
            _live.Add(entry);
            while (true)
            {
                bool more;
                try { more = inner.MoveNext(); }
                catch { Forget(entry); throw; }
                if (!more) break;
                yield return inner.Current;
            }
            Forget(entry);
        }

        /// <summary>
        /// Associate the running coroutine with its entry.
        ///
        /// <para>Called AFTER <c>StartCoroutine</c> returns, which matters: Unity runs a
        /// coroutine's first segment synchronously, so a zero-length tween can already be
        /// finished by the time we get here. <see cref="Entry.Done"/> is how that case is
        /// recognised instead of leaking a handle that will never be cleared.</para>
        /// </summary>
        internal void Bind(Coroutine? handle, Entry entry)
        {
            if (handle == null || entry.Done) return;
            _byHandle[handle] = entry;
        }

        /// <summary>Settle the tween on <paramref name="handle"/> — for a caller that has just
        /// stopped it. Idempotent and null-safe.</summary>
        internal static void Settle(MonoBehaviour host, Coroutine? handle)
        {
            if (handle == null) return;
            For(host, create: false)?.SettleHandle(handle);
        }

        private void SettleHandle(Coroutine handle)
        {
            if (!_byHandle.TryGetValue(handle, out Entry entry)) return;
            _byHandle.Remove(handle);
            Complete(entry);
        }

        private void Forget(Entry entry)
        {
            entry.Done = true;
            _live.Remove(entry);
            // The handle map holds one or two entries per screen; a linear sweep beats keeping a
            // back-reference that would have to be nulled from three places.
            foreach (var kv in _byHandle)
            {
                if (!ReferenceEquals(kv.Value, entry)) continue;
                _byHandle.Remove(kv.Key);
                break;
            }
        }

        private void Complete(Entry entry)
        {
            if (entry.Done) return;
            entry.Done = true;
            _live.Remove(entry);
            entry.Finalize?.Invoke();
        }

        // ── The reason this component exists ─────────────────────────────────

        private void OnDisable()
        {
            if (_live.Count == 0) { _byHandle.Clear(); return; }

            // Copy first: a finalizer may legitimately touch this object (a modal's Unpop
            // deactivates its own panel), and mutating _live while walking it would throw.
            var pending = _live.ToArray();
            _live.Clear();
            _byHandle.Clear();
            foreach (var e in pending)
            {
                if (e.Done) continue;
                e.Done = true;
                e.Finalize?.Invoke();
            }
        }
    }
}
