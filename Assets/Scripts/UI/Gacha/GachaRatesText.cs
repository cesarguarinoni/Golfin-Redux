// Assets/Scripts/UI/Gacha/GachaRatesText.cs
// gacha_ops_polish §2 — the RATES & RULES body, as text, with nothing Unity in it.
//
// THE HONEST-DISCLOSURE SURFACE. Every number on it is computed from the SAME published rows the
// server rolls from (`gacha_rates` × `gacha_pools`, overlaid by ContentService), which is the whole
// reason this is generated at show time rather than authored: a rate change published in the admin
// changes what the player is told at the next open, with no build, and there is no second copy of
// the odds to drift.
//
// It is a PURE FUNCTION on purpose — entry + rates + pool + a name resolver in, a list of lines
// out. No MonoBehaviour, no Resources, no catalog singletons, so the formatting, the ordering and
// the four conditionals are driven directly from EditMode with hand-built rows.
//
// ⚠️ THE CALLER FILTERS, NOT THIS. `GachaPoolCatalog.ForPool` hands back deactivated rows and rows
// whose `min_build` this build has not reached; both must be gone BEFORE Build sees them, because
// a weight that cannot be rolled must not sit in the denominator. That is the same filter
// `GachaBannerCatalog.IsRollable` applies and the same one the admin's `effectiveOdds` gets from
// the panel (it is handed `isActive` rows only) — which is what makes "the modal agrees with the
// admin to the second decimal" a property of the inputs rather than a coincidence.
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Golfin.Roster;
using UnityEngine;

namespace GolfinRedux.UI.Gacha
{
    /// <summary>
    /// Builds the RATES &amp; RULES body — featured, rates by rarity, per-item effective odds,
    /// the guarantee lines and the footer — as a list of already-localised, already-formatted
    /// lines. TMP rich text (<c>&lt;color&gt;</c>) carries the rarity tint; nothing here knows
    /// what a Text component is.
    /// </summary>
    public static class GachaRatesText
    {
        /// <summary>Indent in front of a per-item odds line, under its rarity heading.</summary>
        private const string ItemIndent = "   ";

        /// <summary>Two spaces before a number: the columns are read as "name … percent", and a
        /// tab would need a TMP tab stop the scroll body does not define.</summary>
        private const string Gap = "  ";

        /// <summary>
        /// The body of the modal, top to bottom. An empty string is a spacer line, so the caller
        /// can join with <c>\n</c> and get the section breaks for free.
        /// </summary>
        /// <param name="entry">The banner the player tapped RULES on. Supplies the featured ids
        /// and the two guarantee clauses.</param>
        /// <param name="rates">The banner's pool's rate rows. Only <c>rateBp &gt; 0</c> tiers are
        /// printed: a tier at 0 is never rolled and listing it at "0.00%" reads as a bug.</param>
        /// <param name="pool">The pool's ROLLABLE entries — see the file header on filtering.</param>
        /// <param name="resolveName">(kind, refId) → the prize's display name, or null/blank when
        /// this build cannot name it. A DELEGATE rather than an interface so an EditMode test can
        /// drive the seam with a lambda: the test assembly is an asmdef and cannot reference
        /// Assembly-CSharp, so it could never implement an interface declared here.</param>
        public static List<string> Build(GachaBannerEntry? entry,
                                         IReadOnlyList<GachaRateEntry>? rates,
                                         IReadOnlyList<GachaPoolEntry>? pool,
                                         Func<string, string, string?>? resolveName)
        {
            var lines = new List<string>();
            if (entry == null) return lines;

            rates ??= Array.Empty<GachaRateEntry>();
            pool  ??= Array.Empty<GachaPoolEntry>();

            AppendFeatured(lines, entry, pool, resolveName);
            AppendRates(lines, rates, pool, resolveName);
            AppendGuarantees(lines, entry, pool);
            AppendFooter(lines);

            return lines;
        }

        // ── 1. Featured ───────────────────────────────────────────────────────

        /// <summary>
        /// The banner's <c>featuredRefIds</c>, in the order the operator wrote them, each as
        /// "NAME  <i>rarity chip</i>".
        ///
        /// <para>
        /// An id is resolved through the POOL first — that is where its kind and the rarity it is
        /// rolled at live — and only then through the name resolver. An id in neither is SKIPPED
        /// silently and counted; one warning names all of them, because a featured list that
        /// half-renders is an operator problem and a per-id warning in a modal that reopens is a
        /// log flood.
        /// </para>
        /// </summary>
        private static void AppendFeatured(List<string> lines, GachaBannerEntry entry,
                                           IReadOnlyList<GachaPoolEntry> pool,
                                           Func<string, string, string?>? resolveName)
        {
            string[] featured = entry.FeaturedRefIds ?? Array.Empty<string>();
            if (featured.Length == 0) return;

            var rendered = new List<string>();
            var skipped  = new List<string>();

            foreach (string raw in featured)
            {
                string refId = (raw ?? string.Empty).Trim();
                if (refId.Length == 0) continue;

                GachaPoolEntry? match = null;
                foreach (var p in pool)
                    if (string.Equals(p.RefId, refId, StringComparison.Ordinal)) { match = p; break; }

                if (match == null) { skipped.Add(refId); continue; }

                if (!TryName(resolveName, match, out string name)) { skipped.Add(refId); continue; }

                rendered.Add(name + Gap + RarityChip(match.Rarity));
            }

            if (skipped.Count > 0)
                Debug.LogWarning($"[GachaRatesText] Banner '{entry.BannerId}' lists featured ref id(s) " +
                                 $"this build cannot resolve: {string.Join(", ", skipped)}. They are " +
                                 "omitted from the RATES modal; the rest of the list is unaffected.");

            if (rendered.Count == 0) return;   // nothing resolved — no empty FEATURED heading

            lines.Add(Loc("GACHA_RATES_FEATURED"));
            lines.AddRange(rendered);
            lines.Add(string.Empty);
        }

        // ── 2 + 3. Rates by rarity, and the per-item odds under each ──────────

        /// <summary>
        /// One heading per rated tier, RAREST FIRST, with each of that tier's prizes and its own
        /// effective odds underneath.
        ///
        /// <para>
        /// The per-item number is the spec-A formula and the admin panel's
        /// <c>effectiveOdds</c> verbatim: <c>rateBp/10000 × weight / Σ weight(tier)</c>, with
        /// negative weights clamped to zero exactly as that function does. A tier whose weights
        /// sum to zero prints its heading and no items rather than dividing by zero.
        /// </para>
        /// <para>
        /// Rarest first, not highest-percentage first: a disclosure screen is read top-down for
        /// "what is the best thing here and how unlikely is it", and the ladder order is the one
        /// the rarity chips already teach everywhere else in the game.
        /// </para>
        /// </summary>
        private static void AppendRates(List<string> lines,
                                        IReadOnlyList<GachaRateEntry> rates,
                                        IReadOnlyList<GachaPoolEntry> pool,
                                        Func<string, string, string?>? resolveName)
        {
            // Rate rows are summed per tier before anything is printed: a pool MAY carry two rows
            // for one rarity, and the admin's effectiveOdds adds them, so printing them separately
            // would show two half-rates that agree with nothing.
            var bpByRarity = new Dictionary<CharacterRarity, int>();
            foreach (var r in rates)
            {
                bpByRarity.TryGetValue(r.Rarity, out int soFar);
                bpByRarity[r.Rarity] = soFar + r.RateBp;
            }

            var weightByRarity = new Dictionary<CharacterRarity, int>();
            foreach (var p in pool)
            {
                weightByRarity.TryGetValue(p.Rarity, out int soFar);
                weightByRarity[p.Rarity] = soFar + Math.Max(0, p.Weight);
            }

            var tiers = new List<CharacterRarity>(bpByRarity.Keys);
            tiers.Sort((a, b) => ((int)b).CompareTo((int)a));   // rarest first

            foreach (var tier in tiers)
            {
                int bp = bpByRarity[tier];
                if (bp <= 0) continue;   // never rolled — listing it at 0.00% reads as a defect

                lines.Add(RarityChip(tier) + Gap + Percent(bp / 10000.0));

                weightByRarity.TryGetValue(tier, out int totalWeight);
                if (totalWeight <= 0) continue;

                foreach (var p in pool)
                {
                    if (p.Rarity != tier) continue;
                    if (!TryName(resolveName, p, out string name)) continue;

                    double share = Math.Max(0, p.Weight) / (double)totalWeight;
                    lines.Add(ItemIndent + name + Gap + Percent(bp / 10000.0 * share));
                }
            }

            if (lines.Count > 0 && lines[lines.Count - 1].Length > 0) lines.Add(string.Empty);
        }

        // ── 4. The guarantees ─────────────────────────────────────────────────

        /// <summary>
        /// Pity, the x10 floor and the duplicate rule — each printed only when it is TRUE of this
        /// banner, because a guarantee line that is always shown stops being information.
        ///
        /// <para>
        /// The dupe line is keyed off the POOL, not off the banner: "duplicates pay RP" is true
        /// exactly when some entry carries a <c>dupeRp</c>, and a pool of pure consumables has no
        /// duplicate to convert.
        /// </para>
        /// </summary>
        private static void AppendGuarantees(List<string> lines, GachaBannerEntry entry,
                                             IReadOnlyList<GachaPoolEntry> pool)
        {
            int before = lines.Count;

            if (entry.PityThreshold > 0)
                lines.Add(string.Format(CultureInfo.InvariantCulture, Loc("GACHA_RATES_PITY"),
                                        RarityName(entry.PityMinRarity), entry.PityThreshold));

            if (entry.HasGuaranteeX10)
                lines.Add(string.Format(CultureInfo.InvariantCulture, Loc("GACHA_RATES_GUARANTEE_X10"),
                                        RarityName(entry.GuaranteeMinRarityX10)));

            bool anyDupe = false;
            foreach (var p in pool) if (p.DupeRp > 0) { anyDupe = true; break; }
            if (anyDupe) lines.Add(Loc("GACHA_RATES_DUPE"));

            if (lines.Count > before) lines.Add(string.Empty);
        }

        private static void AppendFooter(List<string> lines) => lines.Add(Loc("GACHA_RATES_FOOTER"));

        // ── Formatting ────────────────────────────────────────────────────────

        /// <summary>
        /// A probability in [0,1] as "2.00%". Two decimals and
        /// <see cref="CultureInfo.InvariantCulture"/> because this number is compared, digit for
        /// digit, against the admin panel's — a device on a comma-decimal locale printing "2,00%"
        /// would make that comparison meaningless.
        /// </summary>
        internal static string Percent(double p)
            => (p * 100.0).ToString("F2", CultureInfo.InvariantCulture) + "%";

        /// <summary>The rarity's name, tinted with the colour the roster, the bag and every card
        /// already use for it (<c>RarityHelper.GetRarityColor</c>) — one palette, one meaning.</summary>
        internal static string RarityChip(CharacterRarity rarity)
            => "<color=#" + ColorUtility.ToHtmlStringRGB(RarityHelper.GetRarityColor(rarity)) + ">" +
               RarityName(rarity) + "</color>";

        /// <summary>The rarity's display name, through the RARITY_* keys the roster already ships.
        /// Untinted — the two guarantee sentences read as prose, not as chips.</summary>
        internal static string RarityName(CharacterRarity rarity)
            => Loc("RARITY_" + rarity.ToString().ToUpperInvariant());

        private static bool TryName(Func<string, string, string?>? resolveName, GachaPoolEntry entry,
                                    out string name)
        {
            name = string.Empty;
            if (resolveName == null) return false;
            name = (resolveName(entry.Kind, entry.RefId) ?? string.Empty).Trim();
            return name.Length > 0;
        }

        private static string Loc(string key) => LocalizationManager.Get(key);
    }
}
