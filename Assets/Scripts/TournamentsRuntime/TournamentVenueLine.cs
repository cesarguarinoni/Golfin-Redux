// ─────────────────────────────────────────────────────────────────────────────
// TournamentsRuntime — TournamentVenueLine
// The one venue-line ladder, shared by every surface that renders a tournament's
// venue: the selection card, the signup modal and the result modal.
//
// Sibling of TournamentDisplayName, and written for the same reason: the ladder
// was copy-pasted into three controllers, and they had already drifted.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable

namespace Golfin.Tournaments
{
    /// <summary>
    /// Resolves the venue line a player sees: <c>localize("tourn.venue." + ClubId)</c>, falling
    /// back to <c>"{ClubId}  -  {N} {Holes}"</c> when no localization row exists.
    ///
    /// <para>
    /// <b>Why the localized string is returned VERBATIM.</b> Every <c>tourn.venue.*</c> row already
    /// carries its own hole count, in its own language — <c>"Kasumigaseki Country Club · 18 Holes"</c>
    /// and <c>"霞ヶ関カントリー倶楽部 · 18ホール"</c>. The three call sites this replaces appended a
    /// second count on top of it and guarded against the duplicate by sniffing the rendered string
    /// for the substring <c>"Holes"</c>. That guard is blind to <c>ホール</c>, so a Japanese player
    /// saw <b>「霞ヶ関カントリー倶楽部 · 18ホール  -  18 Holes」</b> — the count twice, the second
    /// time in the wrong language.
    /// </para>
    /// <para>
    /// Sniffing the output for a word was the wrong shape of fix, so it is gone rather than extended
    /// to a second language. A resolved row is authoritative and is not decorated. Only the
    /// fallback — an unlocalized club id, which a dashboard-created tournament on a new course can
    /// now produce — appends anything.
    /// </para>
    /// </summary>
    public static class TournamentVenueLine
    {
        /// <summary>Localization key prefix. <c>tourn.venue.kasumigaseki</c>, etc.</summary>
        public const string KeyPrefix = "tourn.venue.";

        /// <summary>
        /// Key for the word after the count on the FALLBACK path. Absent from the shipped table
        /// until the CSV is re-imported, and <see cref="HolesWord"/> echo-checks it and returns
        /// "Holes" until then — i.e. exactly today's behaviour, self-healing on the next import.
        /// </summary>
        public const string HolesSuffixKey = "tourn.venue.holes_suffix";

        /// <summary>Matches the separator the two modals already rendered (Figma row 2c).</summary>
        private const string Separator = "  -  ";

        public static string Resolve(TournamentDefinition? def)
            => def == null ? string.Empty : Resolve(def.ClubId, def.HoleSet?.Count ?? 0);

        /// <summary>Ladder over raw parts — the form the tests exercise.</summary>
        public static string Resolve(string? clubId, int holeCount)
        {
            string id  = (clubId ?? string.Empty).Trim();
            string key = KeyPrefix + id;

            // 1. A localization entry that actually resolved. Returned as-is: it already carries
            //    its own hole count, in its own language.
            if (!string.IsNullOrEmpty(id))
            {
                string localized = LocalizationManager.Get(key);
                if (!string.IsNullOrWhiteSpace(localized) && localized != key)
                    return localized.Trim();
            }

            // 2. No row for this club. Show the id rather than a raw key, with the count appended
            //    only when we actually have one.
            if (string.IsNullOrEmpty(id)) return string.Empty;
            if (holeCount <= 0) return id;

            return id + Separator + holeCount + " " + HolesWord();
        }

        /// <summary>
        /// The localized word for "Holes", or the English literal when the key has not shipped.
        /// Same echo-check idiom as <see cref="TournamentDisplayName"/>.
        /// </summary>
        internal static string HolesWord()
        {
            string word = LocalizationManager.Get(HolesSuffixKey);
            return string.IsNullOrWhiteSpace(word) || word == HolesSuffixKey ? "Holes" : word.Trim();
        }
    }
}
