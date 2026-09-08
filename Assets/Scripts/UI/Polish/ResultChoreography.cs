// ─────────────────────────────────────────────────────────────────────────────
// game_polish_b §D1.4 — the little sequence a result modal plays after it pops.
//
// All three result modals want the same shape: wait for the pop to land, then bring the
// numbers and the rows in, in an order. What they do NOT want, and what this class exists to
// make impossible, is DEAD TIME. §D1.4 is explicit: "Buttons stay interactable from the first
// frame ... a tap during the choreography completes it instantly." A result screen is a screen
// the player is trying to leave — they have seen the ball go in, they want the next hole — and
// half a second of un-skippable celebration is the single most irritating thing a polish task
// can add. So nothing here disables a control, and every control that leaves the modal calls
// CompleteNow first.
//
// WHY IT TRACKS ITS CHILDREN. The interruption story in UiMotion is per-handle: Run settles
// the tween on the handle it is given. A staggered group is N tweens on N handles, started
// from inside the sequence, and stopping the SEQUENCE would leave those N running — so a tap
// would snap the sequence to its end and then watch the rows animate anyway, over the top. So
// every child is started through Child() and remembered, and CompleteNow settles the lot.
//
// It is deliberately not a MonoBehaviour: the modals already have a host, and a component
// would need authoring on three prefabs to add a behaviour that is entirely code-driven.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Golfin.UI.Polish
{
    /// <summary>One result modal's post-pop sequence, skippable at any frame.</summary>
    public sealed class ResultChoreography
    {
        private readonly MonoBehaviour _host;
        private readonly List<Coroutine> _children = new List<Coroutine>();
        private Coroutine? _sequence;

        /// <summary>How long the sequence waits before its first beat: the modal's pop.
        /// Read from <see cref="UiMotion.PopDur"/> so the two cannot drift apart.</summary>
        public const float AfterPop = UiMotion.PopDur;

        public ResultChoreography(MonoBehaviour host) { _host = host; }

        /// <summary>True while the sequence is running — a control can ask before deciding
        /// whether its tap means "skip" or "act".</summary>
        public bool IsPlaying => _sequence != null;

        /// <summary>
        /// Start <paramref name="steps"/> after the pop has landed. <paramref name="settle"/> is
        /// the rest state of everything the sequence touches, and it runs on EVERY exit —
        /// completion, a skip, or the modal being torn down mid-flight.
        /// </summary>
        public void Play(IEnumerator steps, Action settle)
        {
            CompleteNow();
            UiMotion.Run(_host, ref _sequence, UiMotion.Then(Delayed(steps), () =>
            {
                settle?.Invoke();
                _sequence = null;
            }));
        }

        /// <summary>
        /// Start one tween as part of the running sequence. Anything begun this way is settled by
        /// <see cref="CompleteNow"/>; anything begun with a bare <c>UiMotion.Run</c> is not, and
        /// will keep animating over the top of a skip.
        /// </summary>
        public void Child(IEnumerator routine)
        {
            Coroutine? h = null;
            UiMotion.Run(_host, ref h, routine);
            if (h != null) _children.Add(h);
        }

        /// <summary>
        /// End it NOW, on the exact final state. Idempotent, and safe to call when nothing is
        /// running — which is what lets every button call it unconditionally rather than each
        /// one deciding whether a sequence is in flight.
        /// </summary>
        public void CompleteNow()
        {
            for (int i = 0; i < _children.Count; i++)
            {
                Coroutine? h = _children[i];
                UiMotion.Stop(_host, ref h);
            }
            _children.Clear();
            UiMotion.Stop(_host, ref _sequence);
            _sequence = null;
        }

        private IEnumerator Delayed(IEnumerator steps)
        {
            float waited = 0f;
            while (waited < AfterPop)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            while (steps.MoveNext()) yield return steps.Current;
        }

        // ── the beats the modals share ───────────────────────────────────────

        /// <summary>Wait, in unscaled seconds. Modals open while timeScale may be 0.</summary>
        public static IEnumerator Wait(float seconds)
        {
            float waited = 0f;
            while (waited < seconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// Rise a set of rows, <see cref="UiMotion.StaggerDelay"/> apart, taking them all to
        /// alpha 0 first so the group does not appear whole and then flicker down.
        /// </summary>
        public void StaggerRows(IList<GameObject?> rows)
        {
            var live = new List<RectTransform>();
            var groups = new List<CanvasGroup>();
            for (int i = 0; i < rows.Count; i++)
            {
                GameObject? go = rows[i];
                if (go == null || !go.activeSelf) continue;
                if (go.transform is not RectTransform rt) continue;
                live.Add(rt);
                groups.Add(Group(go));
            }
            for (int i = 0; i < groups.Count; i++) groups[i].alpha = 0f;

            int n = live.Count;
            if (n == 0) return;
            Child(UiMotion.Stagger(n, i =>
            {
                if (i < 0 || i >= n) return;
                Child(UiMotion.Rise(live[i], groups[i]));
            }));
        }

        /// <summary>Settle a staggered group: every row at rest, fully opaque.</summary>
        public static void SettleRows(IList<GameObject?> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                GameObject? go = rows[i];
                if (go == null) continue;
                Group(go).alpha = 1f;
            }
        }

        /// <summary>
        /// Count a label that is a number inside a run of text — "x12", "RANK #4", "1.240 + Trophy".
        /// <paramref name="wrap"/> is the composite the figure is dropped into; counting the number
        /// while dropping its surrounding words would be a worse bug than not counting at all.
        /// </summary>
        public void Count(TMP_Text? label, int to, string wrap = "{0}", string format = "N0")
        {
            if (label == null) return;
            Child(UiMotion.CountUp(label, 0, to, UiMotion.CountDur, format, wrap));
        }

        /// <summary>Pop a glyph — the outcome word, the rank badge.</summary>
        public void Pop(Component? target)
        {
            if (target == null) return;
            if (target.transform is not RectTransform rt) return;
            Child(UiMotion.Pop(rt, null));
        }

        /// <summary>Settle a popped glyph.</summary>
        public static void SettlePop(Component? target)
        {
            if (target != null && target.transform is RectTransform rt) rt.localScale = Vector3.one;
        }

        /// <summary>A CanvasGroup on demand. Runtime-added, never authored, so no prefab gains a
        /// component and no rest pixel moves (A3).</summary>
        public static CanvasGroup Group(GameObject go)
        {
            // `== null`, not `??`: GetComponent hands back a fake-null UnityEngine.Object.
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            return cg;
        }
    }
}
