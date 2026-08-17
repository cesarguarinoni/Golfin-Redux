// ─────────────────────────────────────────────────────────────────────────────
// TournamentsRuntime — TournamentDisplayName
// The one display-name ladder, shared by every surface that renders a tournament
// name: the selection card, the signup modal and the result modal.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable

namespace Golfin.Tournaments
{
    /// <summary>
    /// Resolves the name a player sees:
    /// <c>localize(NameKey) → TitleJa (Japanese only) → Title → Id</c>.
    ///
    /// <para>
    /// <b>Why the ladder exists.</b> Localization keys ship INSIDE the build. A tournament created
    /// in the dashboard has no key, and <c>LocalizationManager.Get</c> returns the key back when it
    /// cannot resolve one — so the un-laddered call this replaced would have rendered the literal
    /// string <c>tourn.puma_summer_slam</c> on the card. The server therefore sends <c>title</c>
    /// and this falls through to it. Without this, "add a tournament without a new build" is false
    /// for its name (SPEC §1.1).
    /// </para>
    /// <para>
    /// <b>Why <c>TitleJa</c> sits BELOW the key and ABOVE the title.</b> A shipped key is a real
    /// translation pair — both languages get a proper name from it — so it still outranks an
    /// operator's single-language string. Below it, a JP player is served the operator's Japanese
    /// string in preference to the English one. An English player skips that rung ENTIRELY: they
    /// must never see the Japanese string, even when <c>Title</c> is empty and the fall-through
    /// therefore lands on the slug. A JP player with no <c>TitleJa</c> falls to <c>Title</c>;
    /// that is correct and intended, not a gap to paper over.
    /// </para>
    /// <para>
    /// The echo-check (<c>localized == key</c>) is the same trick the venue line already uses at
    /// <c>TournamentSignupModalController:261-263</c>.
    /// </para>
    /// </summary>
    public static class TournamentDisplayName
    {
        public static string Resolve(TournamentDefinition? def)
            => def == null ? string.Empty : Resolve(def.NameKey, def.Title, def.TitleJa, def.Id);

        /// <summary>
        /// Ladder over raw parts, with no Japanese title — kept so every existing caller and test
        /// compiles untouched. Delegates to the four-part form with <c>titleJa: null</c>.
        /// </summary>
        public static string Resolve(string? nameKey, string? title, string? id)
            => Resolve(nameKey, title, null, id);

        /// <summary>Ladder over raw parts — the form the tests exercise.</summary>
        public static string Resolve(string? nameKey, string? title, string? titleJa, string? id)
        {
            // 1. A localization entry that actually resolved.
            if (!string.IsNullOrWhiteSpace(nameKey))
            {
                string localized = LocalizationManager.Get(nameKey!);
                if (!string.IsNullOrWhiteSpace(localized) && localized != nameKey)
                    return localized;
            }

            // 2. The operator's Japanese title — JP players ONLY. Same comparison as
            //    LocalizedText.ApplyPerLanguageSize (LocalizedText.cs:58).
            if (LocalizationManager.CurrentLanguage == Language.Japanese &&
                !string.IsNullOrWhiteSpace(titleJa))
                return titleJa!.Trim();

            // 3. The server's display title — the brand-led case.
            if (!string.IsNullOrWhiteSpace(title)) return title!.Trim();

            // 4. The slug. Ugly, but it identifies the tournament instead of leaking a key.
            return id ?? string.Empty;
        }
    }
}
