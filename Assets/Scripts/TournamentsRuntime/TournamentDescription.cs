// ─────────────────────────────────────────────────────────────────────────────
// TournamentsRuntime — TournamentDescription
// The one description-blurb ladder, shared by every surface that renders a
// tournament blurb. Sits beside TournamentDisplayName and mirrors its shape.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable

namespace Golfin.Tournaments
{
    /// <summary>
    /// Resolves the description blurb a player sees:
    /// <c>localize(DescriptionKey) → DescriptionJa (Japanese only) → DescriptionEn → ""</c>.
    ///
    /// <para>
    /// <b>Why it mirrors <see cref="TournamentDisplayName"/>.</b> Same problem, same shape:
    /// localization keys ship INSIDE the build, so a tournament written in the dashboard has no
    /// key and <c>LocalizationManager.Get</c> hands the key straight back when it cannot resolve
    /// one. The echo-check (<c>localized == key</c>) is the project's standard idiom for that —
    /// see the venue line in <c>TournamentSignupModalController.Populate</c>.
    /// </para>
    /// <para>
    /// <b>Rung 2 is JP-only, deliberately.</b> An English player must never be shown the Japanese
    /// blurb, <i>even when the English column is empty</i> — they fall to rung 4 and the whole
    /// info row collapses. That asymmetry is the same one <see cref="TournamentDisplayName"/>
    /// carries for <c>TitleJa</c>; it is not a gap to "fix" into a symmetric fallback.
    /// </para>
    /// <para>
    /// <b>The one place this differs from the name ladder:</b> there is no id rung. A blurb with
    /// nothing to say returns <see cref="string.Empty"/> and its row hides. It never renders a
    /// slug and never leaks a raw localization key.
    /// </para>
    /// </summary>
    public static class TournamentDescription
    {
        public static string Resolve(TournamentDefinition? def)
            => def == null
                ? string.Empty
                : Resolve(def.DescriptionKey, def.DescriptionEn, def.DescriptionJa);

        /// <summary>Ladder over raw parts — the form the tests exercise.</summary>
        public static string Resolve(string? descriptionKey, string? en, string? ja)
        {
            // 1. A localization entry that actually resolved. A shipped key is a real translation
            //    pair — both languages get proper copy from it — so it outranks operator columns
            //    in BOTH languages, not just the one that happens to be missing.
            if (!string.IsNullOrWhiteSpace(descriptionKey))
            {
                string localized = LocalizationManager.Get(descriptionKey!);
                if (!string.IsNullOrWhiteSpace(localized) && localized != descriptionKey)
                    return localized;
            }

            // 2. The operator's Japanese blurb — JP players ONLY.
            if (LocalizationManager.CurrentLanguage == Language.Japanese &&
                !string.IsNullOrWhiteSpace(ja))
                return ja!.Trim();

            // 3. The operator's English blurb.
            if (!string.IsNullOrWhiteSpace(en)) return en!.Trim();

            // 4. Nothing to say. The caller collapses the row rather than drawing an empty box.
            return string.Empty;
        }
    }
}
