// ─────────────────────────────────────────────────────────────────────────────
// gps_polish §D1 — the ONE motion helper.
//
// Every animated thing on the GPS surface goes through here, and every duration
// on the GPS surface is a constant on this class. That is the whole point: before
// this file the project had four hand-rolled tween loops (VersusResultModal's
// pop, DailyMissionPill's slide, GachaRevealModal's stagger, ToastController's
// fade), each with its own copy of the same cubic ease and its own private idea
// of how long "quick" is. A fifth copy per GPS screen was the alternative.
//
// SCOPE, and it is deliberate: this file is ADDITIVE. The four existing loops are
// NOT retrofitted onto it — that is `game_polish`, and doing it here would put a
// motion refactor of Versus / Gacha / the Home pill inside a GPS task's diff,
// where nobody reviewing GPS would be looking for it.
//
// NO TWEEN PACKAGE. No DOTween, no LeanTween, no Animator, no Timeline (SPEC
// § Out of scope). Coroutines and Mathf.Lerp, in the shape
// DailyMissionPillController.SlideRoutine already uses.
//
// THREE PROPERTIES THAT ARE LOAD-BEARING
//   1. UNSCALED TIME. Modals open while timeScale may be 0; a scaled tween would
//      hang a half-faded backdrop on screen forever.
//   2. INTERRUPTION-SAFE. A second call on the same target stops the first AND
//      settles it on its final value. A stopped coroutine runs no more lines, so
//      without that settle an interrupted fade strands a CanvasGroup at alpha
//      0.43 and the screen behind it stays half-visible.
//   3. FINAL VALUE ON DISABLE. Unity kills coroutines when the host is disabled.
//      A screen swapped out mid-push would come back with its content parked at
//      +W — off screen, permanently. UiMotionRunner (added on demand, hidden)
//      settles every live tween in OnDisable so a screen is always at rest when
//      it is next shown.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Golfin.UI.Polish
{
    /// <summary>
    /// Which curve a primitive runs on. game_polish_b §D2 — the ONE public-API addition of the
    /// `game_polish` track, and it is additive: every parameter that takes one is optional and
    /// defaults to <see cref="Ease.OutCubic"/>, which is the curve every GPS call site already
    /// got, so `gps_polish`'s behaviour is unchanged to the last frame.
    ///
    /// <para>It exists because <c>GachaRevealModalController</c> — the most-loved motion in the
    /// game, and one of the three §D2 retrofits — overshoots. Routing it through
    /// <see cref="UiMotion"/> without an overshoot curve would have meant either flattening the
    /// reveal (a visible regression on the exact motion the retrofit promised not to change) or
    /// leaving a fourth hand-rolled tween loop in the project, which is what
    /// <see cref="UiMotion"/> exists to end.</para>
    /// </summary>
    public enum Ease
    {
        /// <summary>Today's curve, and the default everywhere: <c>1 - (1-t)^3</c>.</summary>
        OutCubic,
        /// <summary>Overshoots past 1 and settles back. See <see cref="UiMotion.EaseOutBack"/>.</summary>
        OutBack,
        /// <summary>No easing — <c>t</c>. For a driver whose OWN maths shapes the frame (the
        /// gacha bag shake integrates a ramping frequency; easing its clock would double-ease it).</summary>
        Linear,
    }

    /// <summary>
    /// Shared UI motion primitives — fade, pop, slide, rise, count-up, stagger, pulse.
    /// Static; all state lives on the coroutine or on <see cref="UiMotionRunner"/>.
    /// </summary>
    public static class UiMotion
    {
        // ── Durations. THE ONLY COPIES. ──────────────────────────────────────
        /// <summary>Cross-fade between two things that occupy the same place.</summary>
        public const float FadeDur = 0.15f;

        /// <summary>A screen's content arriving through the boundary fade.</summary>
        public const float EntryDur = 0.25f;

        /// <summary>Modal / card pop-in (scale 0.9 → 1).</summary>
        public const float PopDur = 0.20f;

        /// <summary>The layered push between two GPS screens.</summary>
        public const float PushDur = 0.25f;

        /// <summary>How far a rising element starts below its rest position, in canvas px.</summary>
        public const float RiseDy = 16f;

        /// <summary>Integer count-up on a number that changed.</summary>
        public const float CountDur = 0.40f;

        /// <summary>A selection bump — 1.0 → <see cref="BumpPeak"/> → 1.0 (§D6).</summary>
        public const float BumpDur = 0.10f;

        /// <summary>How far a selection bump overshoots. The SPEC's own number.</summary>
        public const float BumpPeak = 1.06f;

        /// <summary>Delay between consecutive items of a staggered group.</summary>
        public const float StaggerDelay = 0.03f;

        /// <summary>Hard cap on staggered items — beyond this the last row would wait
        /// longer than the fetch it is celebrating. Items past the cap start together
        /// with the capped one rather than being skipped.</summary>
        public const int StaggerCap = 12;

        /// <summary>One full glow cycle (min → max → min).</summary>
        public const float PulseDur = 0.6f;

        /// <summary>
        /// Master switch. When false every helper settles its target on the final value
        /// IMMEDIATELY and starts no coroutine — the UI is identical, it just does not move.
        ///
        /// <para>Nothing wires this yet: the project has no reduced-motion / accessibility
        /// setting today (grepped at gps_polish time — no <c>ReducedMotion</c>, no
        /// <c>MotionEnabled</c>, nothing in Settings). It exists so that `game_polish`, which
        /// owns the Settings surface, has one line to change rather than a hunt through every
        /// call site.</para>
        /// </summary>
        public static bool Enabled { get; set; } = true;

        // ═════════════════════════════════════════════════════════════════════
        // Runner — start / stop / settle
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Finalizers, keyed by the enumerator each helper returns. The helper registers the
        /// final state at CREATION time so <see cref="Run"/> can settle the tween without
        /// knowing what kind of tween it is — that is what makes interruption generic.
        ///
        /// <para>A ConditionalWeakTable, not a Dictionary, and the difference is not academic: a
        /// Dictionary KEYED ON THE ENUMERATOR keeps that enumerator alive forever, and through its
        /// closure the CanvasGroup, the RectTransform and the whole screen behind them. Every
        /// routine that was created and then not handed to <see cref="Run"/> — a guard clause that
        /// returned early, a test that stepped the enumerator itself — would pin a screen's worth
        /// of objects for the life of the session. Weak keys make an unrun routine cost nothing.</para>
        /// </summary>
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IEnumerator, Action>
            Finalizers = new System.Runtime.CompilerServices.ConditionalWeakTable<IEnumerator, Action>();

        /// <summary>
        /// Run <paramref name="routine"/> on <paramref name="host"/>, replacing whatever was
        /// running on <paramref name="handle"/>.
        ///
        /// <para>The outgoing tween is SETTLED, not merely stopped. See the header, point 2.</para>
        ///
        /// <para>Falls back to settling immediately — no coroutine at all — when motion is off,
        /// when the host is null/destroyed, when the host is not active and enabled (Unity would
        /// refuse to start the coroutine and the target would never reach its final value), or
        /// outside play mode (an Editor coroutine's first segment runs and it then never
        /// advances, which would strand every builder- and test-driven call half-tweened).</para>
        /// </summary>
        public static void Run(MonoBehaviour host, ref Coroutine? handle, IEnumerator routine)
        {
            if (routine == null) return;

            if (handle != null && host != null)
            {
                host.StopCoroutine(handle);
                UiMotionRunner.Settle(host, handle);
            }
            handle = null;

            Action? finalize = TakeFinalizer(routine);

            if (!Enabled || host == null || !host.isActiveAndEnabled || !Application.isPlaying)
            {
                finalize?.Invoke();
                return;
            }

            UiMotionRunner? runner = UiMotionRunner.For(host, create: true);
            if (runner == null) { finalize?.Invoke(); return; }

            UiMotionRunner.Entry entry = runner.CreateEntry(finalize);
            handle = host.StartCoroutine(runner.Drive(routine, entry));
            runner.Bind(handle, entry);
        }

        /// <summary>Convenience overload for callers with no handle to keep (fire and forget,
        /// e.g. one staggered row). Still interruption-safe via the runner's disable hook.</summary>
        public static void Run(MonoBehaviour host, IEnumerator routine)
        {
            Coroutine? ignored = null;
            Run(host, ref ignored, routine);
        }

        /// <summary>Stop and settle whatever is on <paramref name="handle"/>. Idempotent.</summary>
        public static void Stop(MonoBehaviour host, ref Coroutine? handle)
        {
            if (handle == null) return;
            if (host != null)
            {
                host.StopCoroutine(handle);
                UiMotionRunner.Settle(host, handle);
            }
            handle = null;
        }

        /// <summary>Register the final state of a freshly created routine. Called by every
        /// helper below, never by a call site.</summary>
        private static IEnumerator Register(IEnumerator routine, Action finalize)
        {
            Finalizers.Remove(routine);
            Finalizers.Add(routine, finalize);
            return routine;
        }

        /// <summary>
        /// Hand the routine's final state to the caller, ONCE. A second <see cref="Run"/> of the
        /// same enumerator gets nothing, which is what stops an already-settled tween from being
        /// settled again over whatever the result handler has since written.
        /// </summary>
        private static Action? TakeFinalizer(IEnumerator routine)
        {
            if (!Finalizers.TryGetValue(routine, out Action f)) return null;
            Finalizers.Remove(routine);
            return f;
        }

        /// <summary>Test seam: whether this routine still owes a final state.</summary>
        internal static bool HasFinalizer(IEnumerator routine)
            => Finalizers.TryGetValue(routine, out _);

        // ═════════════════════════════════════════════════════════════════════
        // Easing
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Cubic ease-out — the shape <c>DailyMissionPillController.SlideRoutine</c>
        /// and <c>ModeCarouselController.LerpToTargetLayout</c> already use.</summary>
        public static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        /// <summary>Cubic ease-in — the leaving half of the same pair.</summary>
        public static float EaseIn(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        /// <summary>How far <see cref="Ease.OutBack"/> overshoots. NOT a new number: it is the
        /// <c>c1 = 1.70158f</c> that <c>GachaRevealModalController.EaseOutBack</c> has used since
        /// the reveal shipped, quoted here so the retrofit is a primitive swap and not a retune.</summary>
        public const float BackOvershoot = 1.70158f;

        /// <summary>
        /// Ease-out-back — overshoots past 1 and settles. The expression is
        /// <c>GachaRevealModalController.EaseOutBack</c>, character for character.
        ///
        /// <para>A caller easing with this MUST lerp unclamped, or the overshoot — the entire
        /// point of the curve — is clipped away and the result is an ordinary ease-out. Every
        /// primitive below does.</para>
        /// </summary>
        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = BackOvershoot;
            const float c3 = c1 + 1f;
            float p = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        /// <summary>
        /// One curve, by name. Public because a routine that shapes SEVERAL quantities from one
        /// clock needs the eased value itself, not a lerp of it — the gacha card pop moves a
        /// position (clamped), a scale (unclamped, so it overshoots), an arc offset (on the RAW
        /// clock) and an alpha (on the raw clock, over a different span) from one <c>t</c>. Such a
        /// routine drives through <see cref="Tween"/> on <see cref="Ease.Linear"/> and asks for the
        /// curve here, which is still one tween loop instead of a fourth hand-rolled one.
        /// </summary>
        public static float Curve(Ease ease, float t) => ease switch
        {
            Ease.OutBack => EaseOutBack(t),
            Ease.Linear  => Mathf.Clamp01(t),
            _            => EaseOut(t),
        };

        // ═════════════════════════════════════════════════════════════════════
        // Primitives
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Alpha lerp on a CanvasGroup, ease-out.</summary>
        public static IEnumerator Fade(CanvasGroup group, float from, float to, float dur = FadeDur)
        {
            if (group == null) return Register(Empty(), Noop);
            return Register(FadeRoutine(group, from, to, dur), () => { if (group != null) group.alpha = to; });
        }

        private static IEnumerator FadeRoutine(CanvasGroup group, float from, float to, float dur)
        {
            group.alpha = from;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                if (group == null) yield break;
                group.alpha = Mathf.Lerp(from, to, EaseOut(dur <= 0f ? 1f : elapsed / dur));
                yield return null;
            }
            if (group != null) group.alpha = to;
        }

        /// <summary>
        /// Pop-in: scale 0.9 → 1 with an independent alpha 0 → 1, the shape
        /// <c>VersusResultModalController</c> Stage 3 uses. Scale is ALWAYS settled to
        /// <see cref="Vector3.one"/>, including on interruption — a modal stranded at 0.94
        /// is a visibly wrong-sized panel that survives until the next rebuild.
        /// </summary>
        public static IEnumerator Pop(RectTransform rect, CanvasGroup? group, float dur = PopDur,
                                     Ease ease = Ease.OutCubic)
        {
            if (rect == null) return Register(Empty(), Noop);
            return Register(PopRoutine(rect, group, dur, ease), () =>
            {
                if (rect != null) rect.localScale = Vector3.one;
                if (group != null) group.alpha = 1f;
            });
        }

        private static IEnumerator PopRoutine(RectTransform rect, CanvasGroup? group, float dur, Ease ease)
        {
            const float fromScale = 0.9f;
            rect.localScale = new Vector3(fromScale, fromScale, 1f);
            if (group != null) group.alpha = 0f;

            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                if (rect == null) yield break;
                float e = Curve(ease, dur <= 0f ? 1f : elapsed / dur);
                // UNCLAMPED so Ease.OutBack actually overshoots; identical to Lerp for OutCubic
                // and Linear, whose curves never leave [0,1].
                float s = Mathf.LerpUnclamped(fromScale, 1f, e);
                rect.localScale = new Vector3(s, s, 1f);
                // Alpha is CLAMPED even when the scale overshoots — a panel is never more than
                // opaque, and an alpha of 1.09 mid-pop would clip to 1 anyway on some paths and
                // not on others.
                if (group != null) group.alpha = Mathf.Clamp01(e);
                yield return null;
            }
            if (rect != null) rect.localScale = Vector3.one;
            if (group != null) group.alpha = 1f;
        }

        /// <summary>The reverse of <see cref="Pop"/> — used by a modal's Hide.</summary>
        /// <param name="ease">SYMMETRY NOTE. <see cref="Ease.OutCubic"/> — the default — selects
        /// today's curve here, which is the ease-IN half of the cubic pair: a panel leaving should
        /// accelerate away, not decelerate into nothing. The name reads oddly for exactly one
        /// primitive and the alternative was a fourth enum member that only Unpop could use.</param>
        public static IEnumerator Unpop(RectTransform rect, CanvasGroup? group, float dur = FadeDur,
                                       Ease ease = Ease.OutCubic)
        {
            if (rect == null) return Register(Empty(), Noop);
            return Register(UnpopRoutine(rect, group, dur, ease), () =>
            {
                if (rect != null) rect.localScale = Vector3.one;
                if (group != null) group.alpha = 0f;
            });
        }

        private static IEnumerator UnpopRoutine(RectTransform rect, CanvasGroup? group, float dur, Ease ease)
        {
            const float toScale = 0.95f;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                if (rect == null) yield break;
                float raw = dur <= 0f ? 1f : elapsed / dur;
                float e = ease == Ease.OutCubic ? EaseIn(raw) : Curve(ease, raw);
                float s = Mathf.LerpUnclamped(1f, toScale, e);
                rect.localScale = new Vector3(s, s, 1f);
                if (group != null) group.alpha = Mathf.Clamp01(1f - e);
                yield return null;
            }
            // Scale settles at ONE, not at toScale: the panel is about to be deactivated and the
            // next Show must find it at rest. Alpha settles at 0 because that is what "hidden" is.
            if (rect != null) rect.localScale = Vector3.one;
            if (group != null) group.alpha = 0f;
        }

        /// <summary>Horizontal slide on anchoredPosition.x.</summary>
        /// <param name="ease">SYMMETRY NOTE, as <see cref="Unpop"/>. <see cref="Ease.OutCubic"/>
        /// — the default — defers to <paramref name="easeOut"/>, which is what every existing call
        /// site passes and how the push has always behaved. Any OTHER member overrides the bool.</param>
        public static IEnumerator Slide(RectTransform rect, float fromX, float toX,
                                        float dur = PushDur, bool easeOut = true,
                                        Ease ease = Ease.OutCubic)
        {
            if (rect == null) return Register(Empty(), Noop);
            return Register(SlideRoutine(rect, fromX, toX, dur, easeOut, ease), () =>
            {
                if (rect != null) rect.anchoredPosition = new Vector2(toX, rect.anchoredPosition.y);
            });
        }

        private static IEnumerator SlideRoutine(RectTransform rect, float fromX, float toX,
                                                float dur, bool easeOut, Ease ease)
        {
            float y = rect.anchoredPosition.y;
            rect.anchoredPosition = new Vector2(fromX, y);

            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                if (rect == null) yield break;
                float t = dur <= 0f ? 1f : elapsed / dur;
                float e = ease == Ease.OutCubic ? (easeOut ? EaseOut(t) : EaseIn(t)) : Curve(ease, t);
                rect.anchoredPosition = new Vector2(Mathf.LerpUnclamped(fromX, toX, e), y);
                yield return null;
            }
            if (rect != null) rect.anchoredPosition = new Vector2(toX, y);
        }

        /// <summary>
        /// Rise into place: y from (rest − dy) to rest, with alpha 0 → 1.
        ///
        /// <para>THE REST IS NOT FROZEN AT THE CALL. A rise is a transient offset laid over
        /// wherever the rect belongs, and "where it belongs" can legitimately change while the
        /// 250 ms run: Home's <c>BannerSlotBinder</c> drops the mode carousel into the hidden
        /// banner's place in the very OnEnable that starts the entry rise, the daily and loan
        /// pills re-seat themselves under the notice, a layout group re-lays out a staggered row.
        /// The first version of this routine wrote its captured rest back on every frame and
        /// again on its last line, so whichever of those wrote during the rise was simply undone
        /// 250 ms later — the carousel came back up 236 px, the pill snapped to its old y. Now any
        /// write to <c>anchoredPosition.y</c> that is not this routine's own is taken as the new
        /// rest (<see cref="RiseState.AbsorbExternal"/>): the rise carries on from there with
        /// whatever offset it had left and lands on the value the other writer meant. Last write
        /// wins, as it should — the rise is decoration, the placement is the fact.</para>
        ///
        /// <para>The mirror rule for READERS: a placement computed FROM a rising rect must read
        /// its rest, <see cref="RestY"/>, not its live y — mid-rise the live value is up to
        /// <see cref="RiseDy"/> low. <c>DailyMissionPillController.ComputeTargetY</c> and
        /// <c>BannerSlotBinder</c> do.</para>
        /// </summary>
        public static IEnumerator Rise(RectTransform rect, CanvasGroup? group,
                                       float dy = RiseDy, float dur = EntryDur,
                                       Ease ease = Ease.OutCubic)
        {
            if (rect == null) return Register(Empty(), Noop);
            var state = new RiseState(rect.anchoredPosition.y);
            return Register(RiseRoutine(rect, group, dy, dur, state, ease), () =>
            {
                if (rect != null)
                {
                    state.AbsorbExternal(rect);
                    state.Write(rect, state.RestY);
                    Rising.Remove(rect);
                }
                if (group != null) group.alpha = 1f;
            });
        }

        private static IEnumerator RiseRoutine(RectTransform rect, CanvasGroup? group,
                                               float dy, float dur, RiseState state, Ease ease)
        {
            // Registered on the first step, not at creation: a routine handed to Stagger waits
            // its beat before it moves anything, and a reader must not see an offset that has not
            // been applied yet. Absorb first — the beat is exactly the window a layout pass or a
            // placement fills.
            state.AbsorbExternal(rect);
            Rising.Remove(rect);
            Rising.Add(rect, state);
            state.Write(rect, state.RestY - dy);
            if (group != null) group.alpha = 0f;

            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                if (rect == null) yield break;
                float e = Curve(ease, dur <= 0f ? 1f : elapsed / dur);
                state.AbsorbExternal(rect);
                state.Write(rect, Mathf.LerpUnclamped(state.RestY - dy, state.RestY, e));
                if (group != null) group.alpha = Mathf.Clamp01(e);
                yield return null;
            }
            if (rect != null)
            {
                state.AbsorbExternal(rect);
                state.Write(rect, state.RestY);
                Rising.Remove(rect);
            }
            if (group != null) group.alpha = 1f;
        }

        /// <summary>
        /// The y a rect is rising TO — its rest — or its live y when no rise is in flight on it.
        /// Read this, never <c>anchoredPosition.y</c>, when placing something relative to a rect
        /// that <see cref="Rise"/> may be moving: for the 250 ms after a screen entry the live
        /// value is up to <see cref="RiseDy"/> below where the rect belongs, and a placement
        /// derived from it inherits the whole error.
        /// </summary>
        public static float RestY(RectTransform rect)
        {
            if (rect == null) return 0f;
            if (!Rising.TryGetValue(rect, out RiseState state)) return rect.anchoredPosition.y;
            state.AbsorbExternal(rect);
            return state.RestY;
        }

        /// <summary>Test seam: whether a rise currently owns this rect's y.</summary>
        internal static bool IsRising(RectTransform rect) => rect != null && Rising.TryGetValue(rect, out _);

        /// <summary>
        /// Rises in flight, by rect. Weak-keyed for the same reason <see cref="Finalizers"/> is: a
        /// rect destroyed mid-rise (a screen torn down under it) must not be pinned by a static.
        /// </summary>
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<RectTransform, RiseState>
            Rising = new System.Runtime.CompilerServices.ConditionalWeakTable<RectTransform, RiseState>();

        /// <summary>The one mutable fact a rise carries: where it lands, and what it last wrote.</summary>
        private sealed class RiseState
        {
            /// <summary>
            /// Below this, a difference between what was written and what reads back is storage
            /// noise, not a writer. `anchoredPosition` round-trips through localPosition and at a
            /// magnitude of a few hundred px one ulp is ~1e-4; the same floor
            /// <c>UiMotionTests.Rise_ReturnsToTheRestYItWasGivenAndFullAlpha</c> tolerates.
            /// Without it every frame would nudge the rest by an ulp and a session of entries
            /// would walk the content off by a pixel.
            /// </summary>
            private const float Noise = 0.01f;

            public float RestY;
            private float _lastWritten;

            public RiseState(float restY) { RestY = restY; _lastWritten = restY; }

            /// <summary>Fold in a move somebody else made since our last write — it is the new rest.</summary>
            public void AbsorbExternal(RectTransform rect)
            {
                float y = rect.anchoredPosition.y;
                if (Mathf.Abs(y - _lastWritten) > Noise) { RestY = y; _lastWritten = y; }
            }

            public void Write(RectTransform rect, float y)
            {
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
                _lastWritten = y;
            }
        }

        /// <summary>
        /// Integer count-up. <paramref name="format"/> is applied to the running value with
        /// <see cref="System.Globalization.CultureInfo.InvariantCulture"/> — the GPS screens
        /// render "N0" and a locale that swaps the thousands separator mid-tween would be a
        /// flicker.
        /// </summary>
        /// <param name="wrap">Optional composite format the counted number is dropped INTO —
        /// "{0} pts", "{0} / 24 earned", "Your balance: {0}". The GPS labels are rarely a bare
        /// number: <c>GIFTS RECEIVED</c>, the gift modal's balance line and the profile's badge
        /// count are all localized runs with the figure inside them, and counting up a label
        /// while dropping its surrounding words would be a worse bug than not counting at all.
        /// Null means the label IS the number.</param>
        /// <param name="culture">Overrides the InvariantCulture default when the caller renders
        /// its number with a different separator — the top bar counts RP with "." thousands, and
        /// a tween that drew "1,240" before settling on "1.240" would flicker the separator.</param>
        public static IEnumerator CountUp(TMP_Text label, int from, int to,
                                          float dur = CountDur, string format = "N0",
                                          string? wrap = null,
                                          System.IFormatProvider? culture = null)
        {
            if (label == null) return Register(Empty(), Noop);
            string final = Render(to, format, wrap, culture);
            return Register(CountUpRoutine(label, from, to, dur, format, wrap, culture),
                            () => { if (label != null) label.text = final; });
        }

        private static IEnumerator CountUpRoutine(TMP_Text label, int from, int to,
                                                  float dur, string format, string? wrap,
                                                  System.IFormatProvider? culture)
        {
            float elapsed = 0f;
            int last = int.MinValue;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                if (label == null) yield break;
                float e = EaseOut(dur <= 0f ? 1f : elapsed / dur);
                int v = Mathf.RoundToInt(Mathf.Lerp(from, to, e));
                // Only touch the mesh when the integer actually moved: a TMP_Text assignment
                // rebuilds the mesh, and at 60 fps a 0.4 s count over 12 points would rebuild
                // 24 times to draw 12 distinct values.
                if (v != last) { label.text = Render(v, format, wrap, culture); last = v; }
                yield return null;
            }
            if (label != null) label.text = Render(to, format, wrap, culture);
        }

        /// <summary>One value, formatted and (optionally) dropped into its surrounding run.</summary>
        public static string Render(int value, string format = "N0", string? wrap = null,
                                    System.IFormatProvider? culture = null)
        {
            var provider = culture ?? System.Globalization.CultureInfo.InvariantCulture;
            string n = value.ToString(format, provider);
            return string.IsNullOrEmpty(wrap) ? n : string.Format(provider, wrap!, n);
        }

        /// <summary>
        /// Selection bump — scale 1 → <paramref name="peak"/> → 1 (§D6). Half the duration up,
        /// half down, and ALWAYS settling on <see cref="Vector3.one"/>: an interrupted bump that
        /// stranded a chip at 1.04 would be a permanently mis-sized control, and these are the
        /// controls a player taps repeatedly.
        /// </summary>
        public static IEnumerator Bump(RectTransform rect, float peak = BumpPeak, float dur = BumpDur)
        {
            if (rect == null) return Register(Empty(), Noop);
            return Register(BumpRoutine(rect, peak, dur),
                            () => { if (rect != null) rect.localScale = Vector3.one; });
        }

        private static IEnumerator BumpRoutine(RectTransform rect, float peak, float dur)
        {
            float half = Mathf.Max(0.0001f, dur * 0.5f);
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                if (rect == null) yield break;
                float s = elapsed < half
                    ? Mathf.Lerp(1f, peak, EaseOut(elapsed / half))
                    : Mathf.Lerp(peak, 1f, EaseOut((elapsed - half) / half));
                rect.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            if (rect != null) rect.localScale = Vector3.one;
        }

        /// <summary>
        /// The generic scalar tween: <paramref name="apply"/> is called with the eased value each
        /// frame. Exists for the one animated quantity that is neither a position, an alpha nor a
        /// label — a progress bar's WIDTH, which has to go through
        /// <c>GpsUiColor.SetBarFill</c> because <c>Image.Type.Filled</c> throws the 9-slice away.
        ///
        /// <para>ONE delegate allocation, at creation, never inside the loop — A13's budget.</para>
        /// </summary>
        public static IEnumerator Tween(float from, float to, float dur, Action<float> apply,
                                        Ease ease = Ease.OutCubic)
        {
            if (apply == null) return Register(Empty(), Noop);
            return Register(TweenRoutine(from, to, dur, apply, ease), () => apply(to));
        }

        private static IEnumerator TweenRoutine(float from, float to, float dur, Action<float> apply, Ease ease)
        {
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                apply(Mathf.LerpUnclamped(from, to, Curve(ease, dur <= 0f ? 1f : elapsed / dur)));
                yield return null;
            }
            apply(to);
        }

        /// <summary>
        /// Fire <paramref name="perItem"/> for each index, <paramref name="delay"/> apart.
        /// Indices at or beyond <see cref="StaggerCap"/> all fire on the cap's beat rather than
        /// being dropped — a 30-row list still shows every row, it just stops getting later.
        /// </summary>
        public static IEnumerator Stagger(int count, Action<int> perItem, float delay = StaggerDelay)
        {
            if (perItem == null || count <= 0) return Register(Empty(), Noop);
            return Register(StaggerRoutine(count, perItem, delay), () =>
            {
                for (int i = 0; i < count; i++) perItem(i);
            });
        }

        private static IEnumerator StaggerRoutine(int count, Action<int> perItem, float delay)
        {
            float waited = 0f;
            int beat = 0;
            for (int i = 0; i < count; i++)
            {
                int wantBeat = Mathf.Min(i, StaggerCap - 1);
                while (beat < wantBeat)
                {
                    float target = delay;
                    waited = 0f;
                    while (waited < target)
                    {
                        waited += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    beat++;
                }
                perItem(i);
            }
        }

        /// <summary>
        /// Glow pulse: <paramref name="cycles"/> full min→max→min sweeps, resting at
        /// <paramref name="min"/>. The curve is the Home pill's <c>SetGlowAlpha</c> sine.
        /// </summary>
        public static IEnumerator Pulse(CanvasGroup glow, float min = 0f, float max = 1f,
                                        int cycles = 2, float dur = PulseDur)
        {
            if (glow == null) return Register(Empty(), Noop);
            return Register(PulseRoutine(glow, min, max, cycles, dur),
                            () => { if (glow != null) glow.alpha = min; });
        }

        private static IEnumerator PulseRoutine(CanvasGroup glow, float min, float max,
                                                int cycles, float dur)
        {
            float total = Mathf.Max(0f, dur) * Mathf.Max(1, cycles);
            float elapsed = 0f;
            while (elapsed < total)
            {
                elapsed += Time.unscaledDeltaTime;
                if (glow == null) yield break;
                float phase = dur <= 0f ? 1f : (elapsed % dur) / dur;
                // 0 → 1 → 0 over one cycle, smooth at both ends.
                float w = 0.5f - 0.5f * Mathf.Cos(phase * 2f * Mathf.PI);
                glow.alpha = Mathf.Lerp(min, max, w);
                yield return null;
            }
            if (glow != null) glow.alpha = min;
        }

        /// <summary>
        /// Sequence: run <paramref name="inner"/> to completion, then <paramref name="after"/>.
        ///
        /// <para>Exists because a naive wrapper enumerator would LOSE the inner routine's
        /// registered final state — <see cref="Run"/> looks the finalizer up by the enumerator it
        /// is handed, and that would be the wrapper. This composes both into one finalizer, so an
        /// interrupted or disabled sequence still settles the tween AND runs the tail (a modal's
        /// Hide really must deactivate its panel even when the screen under it went away first).</para>
        /// </summary>
        public static IEnumerator Then(IEnumerator inner, Action after)
        {
            if (inner == null) return Register(Empty(), after ?? Noop);
            Action? innerFinal = TakeFinalizer(inner);
            return Register(ThenRoutine(inner, after), () => { innerFinal?.Invoke(); after?.Invoke(); });
        }

        private static IEnumerator ThenRoutine(IEnumerator inner, Action? after)
        {
            while (inner.MoveNext()) yield return inner.Current;
            after?.Invoke();
        }

        // ── Shared no-ops ────────────────────────────────────────────────────

        private static void Noop() { }

        private static IEnumerator Empty() { yield break; }
    }
}
