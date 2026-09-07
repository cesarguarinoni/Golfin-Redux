using System;
using UnityEngine;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// The pure arithmetic behind the 4-slot selector carousel (selector_carousel §3).
    ///
    /// <para>Static and Unity-object-free on purpose: every rule that decides WHERE a card sits
    /// and WHICH item a snap lands on is testable in EditMode
    /// (<c>Golfin.Gameplay.Tests.SelectorCarouselMathTests</c>) without a scene, a canvas or a
    /// play-mode frame. <see cref="SelectorOverlayWidget"/> keeps only the state and the
    /// coroutine.</para>
    ///
    /// <para>Note on virtual indices: <see cref="ResolveSnapTarget"/> returns a VIRTUAL index —
    /// the continuous ring coordinate the carousel scrolls to — not a bag index. The item it
    /// lands on is <c>Mod(result, n)</c>. Returning the virtual index (which may be negative or
    /// ≥ n) is what makes a wrap animate the short way: scrolling to −1 with n = 8 shows item 7
    /// arriving from below, where scrolling to +7 would drag the whole ring the long way round.</para>
    /// </summary>
    public static class SelectorCarouselMath
    {
        /// <summary>True modulo — never negative. <paramref name="n"/> ≤ 0 returns 0.</summary>
        public static int Mod(int v, int n)
        {
            if (n <= 0) return 0;
            int r = v % n;
            return r < 0 ? r + n : r;
        }

        /// <summary>
        /// Bottom edge of pool slot <paramref name="j"/> inside the viewport, in canvas px from
        /// the viewport's bottom edge. j = 0 is the focus slot; it sits exactly at
        /// <paramref name="margin"/> when <paramref name="scroll"/> is a whole number.
        /// </summary>
        public static float SlotY(int j, float scroll, float pitch, float margin)
        {
            float frac = scroll - Mathf.Floor(scroll);
            return margin + (j - frac) * pitch;
        }

        /// <summary>Ease-out cubic — the same curve as <c>Golfin.UI.Polish.UiMotion</c>, which
        /// this asmdef cannot reference (see SPEC § Architecture context).</summary>
        public static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        /// <summary>Snap duration for a travel of <paramref name="deltaItems"/> slots, clamped.</summary>
        public static float SnapDuration(float deltaItems, float baseDur, float perItem, float min, float max)
        {
            if (max < min) { float t = min; min = max; max = t; }
            return Mathf.Clamp(baseDur + Mathf.Abs(deltaItems) * perItem, min, max);
        }

        /// <summary>
        /// Nearest virtual index to <c>scroll + velocity × lookahead</c>, with the fling clamped to
        /// ±<paramref name="maxFling"/> slots from <paramref name="scroll"/>, then walked outward in
        /// the direction of travel (+1 when stationary) until <paramref name="selectable"/> accepts
        /// <c>Mod(v, n)</c>. When <paramref name="wrap"/> is false the candidate is first clamped to
        /// <c>[0, n-1]</c> and the walk turns back inward rather than running off an end.
        /// </summary>
        /// <returns>
        /// The virtual index to scroll to, or <paramref name="fallback"/> when nothing is
        /// selectable. The K11 green gate always leaves at least one club eligible, so the
        /// fallback is unreachable in practice — it exists so this can never loop forever.
        /// </returns>
        public static int ResolveSnapTarget(float scroll, float velocityItemsPerSec, float lookaheadSec,
                                            int maxFling, int n, bool wrap, Func<int, bool> selectable,
                                            int fallback)
        {
            if (n <= 0 || selectable == null) return fallback;

            int here      = Mathf.RoundToInt(scroll);
            int candidate = Mathf.RoundToInt(scroll + velocityItemsPerSec * lookaheadSec);
            if (maxFling > 0) candidate = Mathf.Clamp(candidate, here - maxFling, here + maxFling);

            int dir = velocityItemsPerSec > 0f ?  1
                    : velocityItemsPerSec < 0f ? -1
                    : candidate > here         ?  1
                    : candidate < here         ? -1
                    :                             1;

            if (!wrap) candidate = Mathf.Clamp(candidate, 0, n - 1);

            // Outward in the direction of travel. With wrap this covers every item in one pass.
            for (int step = 0; step < n; step++)
            {
                int v = candidate + dir * step;
                if (!wrap && (v < 0 || v > n - 1)) break;
                if (selectable(Mod(v, n))) return v;
            }
            // Non-wrap only: the walk ran off an end, so come back inward.
            for (int step = 1; step < n; step++)
            {
                int v = candidate - dir * step;
                if (!wrap && (v < 0 || v > n - 1)) continue;
                if (selectable(Mod(v, n))) return v;
            }
            return fallback;
        }
    }
}
