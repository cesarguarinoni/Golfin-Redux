// ─────────────────────────────────────────────────────────────────────────────
// screen_hints §3.2 — which tips a screen shows, as a pure function.
//
// Plain C#, no Unity, so the rules (a seen screen shows nothing; inactive tips
// are skipped AND not counted; a key another screen already showed is skipped —
// Architect default b; a row naming a control scheme shows for that scheme
// only) are pinned by ScreenHintResolverTests rather than by walking the app.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using Golfin.Gameplay.UI.Controls;

namespace GolfinRedux.UI
{
    public static class ScreenHintResolver
    {
        /// <summary>
        /// The tips to show for <paramref name="screen"/>, in <c>order</c> — or empty.
        ///
        /// <para>Filters, in this order: a screen in <c>state.seenScreens</c> → empty before
        /// anything else; the catalog rows for this screen that apply to <paramref name="scheme"/>
        /// (a blank <c>scheme</c> cell always, a named one only for its own scheme — the shot view's
        /// swing tip is Flick's, Pendulum's, Tap Timing's or Free Swing's, never the default's),
        /// sorted by <c>order</c>; a key with no <c>LoadingTips.csv</c> row is dropped; a row with
        /// <c>active=0</c> is dropped (§2.1 — Inventory is 1/2 today, not 1/3); a key in
        /// <c>state.seenKeys</c> is dropped (default b — no tip is ever read twice). Returns the
        /// <see cref="LoadingTip"/> rows so the modal has the sprite name as well as the key.</para>
        /// </summary>
        /// <param name="scheme">The player's control scheme at this entry
        /// (<c>ControlSchemeService.Current</c> for the presenter). Required on purpose: a default
        /// argument here would be the hard-wired scheme this parameter exists to remove.</param>
        public static List<LoadingTip> HintsFor(string screen,
            IReadOnlyList<ScreenHint> hints, IReadOnlyList<LoadingTip> tips, ScreenHintState state,
            ControlScheme scheme)
        {
            var result = new List<LoadingTip>();
            if (string.IsNullOrEmpty(screen) || hints == null || tips == null) return result;
            if (Contains(state.seenScreens, screen)) return result;

            var rows = new List<ScreenHint>();
            for (int i = 0; i < hints.Count; i++)
                if (string.Equals(hints[i].screen, screen, StringComparison.Ordinal) && hints[i].AppliesTo(scheme))
                    rows.Add(hints[i]);
            if (rows.Count == 0) return result;
            rows.Sort((a, b) => a.order.CompareTo(b.order));

            var taken = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Count; i++)
            {
                string key = rows[i].key;
                if (Contains(state.seenKeys, key)) continue;
                if (taken.Contains(key)) continue;   // the same key twice on one screen is one hint

                if (!TryFind(tips, key, out LoadingTip tip)) continue;
                if (!tip.active) continue;

                taken.Add(key);
                result.Add(tip);
            }

            return result;
        }

        static bool TryFind(IReadOnlyList<LoadingTip> tips, string key, out LoadingTip tip)
        {
            for (int i = 0; i < tips.Count; i++)
            {
                if (string.Equals(tips[i].key, key, StringComparison.Ordinal))
                {
                    tip = tips[i];
                    return true;
                }
            }
            tip = default;
            return false;
        }

        static bool Contains(string[]? list, string value)
            => list != null && Array.IndexOf(list, value) >= 0;
    }
}
