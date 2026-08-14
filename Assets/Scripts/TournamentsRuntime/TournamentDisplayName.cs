// ─────────────────────────────────────────────────────────────────────────────
// TournamentsRuntime — TournamentDisplayName
// The one display-name ladder, shared by every surface that renders a tournament
// name: the selection card, the signup modal and the result modal.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable

namespace Golfin.Tournaments
{
    /// <summary>
    /// Resolves the name a player sees: <c>localize(NameKey) → Title → Id</c>.
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
    /// The echo-check (<c>localized == key</c>) is the same trick the venue line already uses at
    /// <c>TournamentSignupModalController:261-263</c>.
    /// </para>
    /// </summary>
    public static class TournamentDisplayName
    {
        public static string Resolve(TournamentDefinition? def)
            => def == null ? string.Empty : Resolve(def.NameKey, def.Title, def.Id);

        /// <summary>Ladder over raw parts — the form the tests exercise.</summary>
        public static string Resolve(string? nameKey, string? title, string? id)
        {
            // 1. A localization entry that actually resolved.
            if (!string.IsNullOrWhiteSpace(nameKey))
            {
                string localized = LocalizationManager.Get(nameKey!);
                if (!string.IsNullOrWhiteSpace(localized) && localized != nameKey)
                    return localized;
            }

            // 2. The server's display title — the brand-led case.
            if (!string.IsNullOrWhiteSpace(title)) return title!.Trim();

            // 3. The slug. Ugly, but it identifies the tournament instead of leaking a key.
            return id ?? string.Empty;
        }
    }
}
