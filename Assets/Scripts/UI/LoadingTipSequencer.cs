// ─────────────────────────────────────────────────────────────────────────────
// loading_tips §3.2 — WHICH tip comes next. Pure, no MonoBehaviour, no
// PlayerPrefs: the RNG is a Func<int,int> and the persisted state is a struct in
// and a struct out, which is the whole reason the pool rules are testable at all.
//
// TWO POOLS (Confluence "Loading Tips System", spec §2.1):
//   • FIRST POOL  — the 8 tutorial tips, in `order`, walked END TO END TWICE.
//     A new player therefore meets the swing, the aim, the grades and the map in
//     the designed sequence, and meets each of them twice before the game starts
//     surprising them.
//   • GENERAL POOL — every active row (the first-pool rows included), drawn
//     uniformly at random MINUS the last five keys shown. Five, not one: with
//     one, "GACHA, STORE, GACHA, STORE" is a legal sequence and reads as a bug.
//
// THE RING IS THE HISTORY, AND IT IS ALSO THE "HAS THIS PLAYER SEEN ANYTHING"
// FLAG. Every showing pushes its key, so an empty ring means a fresh install —
// which is what lets the card open a cold boot on TIP_SWING instead of skipping
// straight past it (§3.3's double-advance guard).
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;

namespace GolfinRedux.UI
{
    /// <summary>Everything that has to survive a quit. Persisted by
    /// <see cref="LoadingTipStore"/> as JSON in PlayerPrefs.</summary>
    [Serializable]
    public struct LoadingTipState
    {
        /// <summary>0 and 1 = walking the first pool for the first / second time;
        /// 2 = done with it, drawing from the general pool forever.</summary>
        public int firstPass;

        /// <summary>Index into the ACTIVE, order-sorted first pool.</summary>
        public int firstIndex;

        /// <summary>Newest last, at most <see cref="LoadingTipSequencer.RecentWindow"/>.</summary>
        public string[] recentKeys;
    }

    public sealed class LoadingTipSequencer
    {
        /// <summary>How many recent keys the general draw excludes (§2.1).</summary>
        public const int RecentWindow = 5;

        /// <summary>The first pool is walked this many times before the general pool starts.</summary>
        public const int FirstPasses = 2;

        readonly List<LoadingTip> _first;    // active, pool=first, sorted by order
        readonly List<LoadingTip> _general;  // every active row, first-pool rows included
        readonly Func<int, int> _rng;
        readonly List<string> _recent;

        int _pass;
        int _index;
        LoadingTip _current;

        /// <summary>Was anything ever shown to this player before this sequencer was built?
        /// The card uses it to decide whether its first show advances (§3.3).</summary>
        public bool HasHistory { get; }

        public LoadingTipSequencer(IReadOnlyList<LoadingTip>? rows, LoadingTipState state, Func<int, int>? rng)
        {
            _rng = rng ?? (n => UnityEngine.Random.Range(0, n));

            _first = new List<LoadingTip>();
            _general = new List<LoadingTip>();
            if (rows != null)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    LoadingTip r = rows[i];
                    if (!r.active || !r.IsValid) continue;   // §2.1 — inactive rows are skipped everywhere
                    _general.Add(r);
                    if (r.first) _first.Add(r);
                }
            }
            _first.Sort((a, b) => a.order.CompareTo(b.order));

            _recent = new List<string>(RecentWindow + 1);
            if (state.recentKeys != null)
            {
                for (int i = 0; i < state.recentKeys.Length; i++)
                    if (!string.IsNullOrEmpty(state.recentKeys[i])) _recent.Add(state.recentKeys[i]);
                TrimRecent();
            }
            HasHistory = _recent.Count > 0;

            _pass = Math.Max(0, state.firstPass);
            _index = Math.Max(0, state.firstIndex);

            // A first pool that shrank under a persisted index (a row deactivated in an
            // update) restarts the pass rather than throwing or silently showing row 0 twice.
            if (_first.Count == 0) _pass = FirstPasses;
            while (_pass < FirstPasses && _index >= _first.Count) { _index = 0; _pass++; }
            if (_pass >= FirstPasses) { _pass = FirstPasses; _index = 0; }

            _current = Resolve();
        }

        /// <summary>The tip the card is showing right now.</summary>
        public LoadingTip Current => _current;

        public LoadingTipState State => new LoadingTipState
        {
            firstPass  = _pass,
            firstIndex = _index,
            recentKeys = _recent.ToArray(),
        };

        /// <summary>Move one step and return the new <see cref="Current"/>. Called by the
        /// auto-cycle, by a tap, and once every time the loading screen is shown.</summary>
        public LoadingTip Advance()
        {
            if (_pass < FirstPasses)
            {
                _index++;
                if (_index >= _first.Count) { _index = 0; _pass++; }
            }

            _current = _pass < FirstPasses ? Show(_first[_index]) : Draw();
            return _current;
        }

        // ── Internals ────────────────────────────────────────────────────────

        /// <summary>What <see cref="Current"/> is for the state we were handed. In the first
        /// pool that is a lookup; in the general pool it is the last key drawn, or a fresh
        /// draw when the ring is empty or its head has since been deactivated (§3.2).</summary>
        LoadingTip Resolve()
        {
            if (_pass < FirstPasses && _first.Count > 0) return Show(_first[_index]);

            if (_recent.Count > 0)
            {
                string head = _recent[_recent.Count - 1];
                for (int i = 0; i < _general.Count; i++)
                    if (_general[i].key == head) return _general[i];
            }
            return Draw();
        }

        /// <summary>Uniform draw from the general pool minus the recent ring. The exclusion is
        /// trimmed from the OLDEST end until at least one candidate survives, so a three-row
        /// catalog still returns a tip instead of an empty set (§2.1).</summary>
        LoadingTip Draw()
        {
            if (_general.Count == 0) return default;   // null-safe: the card shows its header only

            var candidates = new List<LoadingTip>(_general.Count);
            for (int excluded = _recent.Count; ; excluded--)
            {
                candidates.Clear();
                for (int i = 0; i < _general.Count; i++)
                {
                    string key = _general[i].key;
                    bool blocked = false;
                    for (int r = _recent.Count - excluded; r < _recent.Count; r++)
                        if (_recent[r] == key) { blocked = true; break; }
                    if (!blocked) candidates.Add(_general[i]);
                }
                if (candidates.Count > 0 || excluded <= 0) break;
            }
            if (candidates.Count == 0) candidates.AddRange(_general);

            int pick = _rng(candidates.Count);
            if (pick < 0 || pick >= candidates.Count) pick = 0;   // a hostile Func never indexes out
            return Show(candidates[pick]);
        }

        /// <summary>Record a showing: push onto the ring unless it is already the head
        /// (rebuilding the sequencer from a persisted state must not double-count).</summary>
        LoadingTip Show(LoadingTip tip)
        {
            if (!tip.IsValid) return tip;
            if (_recent.Count == 0 || _recent[_recent.Count - 1] != tip.key)
            {
                _recent.Add(tip.key);
                TrimRecent();
            }
            return tip;
        }

        void TrimRecent()
        {
            while (_recent.Count > RecentWindow) _recent.RemoveAt(0);
        }
    }
}
