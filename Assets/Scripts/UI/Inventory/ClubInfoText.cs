// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Inventory — ClubInfoText
// The one club-description ladder, shared by every surface that renders a club
// blurb. Mirrors TournamentDescription (Assets/Scripts/TournamentsRuntime/).
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable

namespace Golfin.Inventory
{
    /// <summary>
    /// Resolves the club description a player sees: <c>infoJa (Japanese only) → info → ""</c>.
    ///
    /// <para>
    /// <b>Rung 1 is JP-only, deliberately.</b> Same asymmetry as
    /// <c>TournamentDescription</c>: an English player must never be shown the Japanese
    /// blurb, <i>even when the English column is empty</i> — they fall through to the empty
    /// string and the row collapses. That is not a gap to "fix" into a symmetric fallback.
    /// </para>
    /// <para>
    /// <b>Why there is no localization-key rung.</b> Tournament copy is authored in a
    /// dashboard and may carry a shipped key; club copy is authored in Clubs.csv and never
    /// has one. Adding an empty rung would only invite a raw key to leak into the panel.
    /// </para>
    /// </summary>
    public static class ClubInfoText
    {
        public static string Resolve(ClubDataRuntime? template)
            => template == null ? string.Empty : Resolve(template.info, template.infoJa);

        /// <summary>Ladder over raw parts — the form the tests exercise.</summary>
        public static string Resolve(string? info, string? infoJa)
        {
            // 1. The Japanese column — JP players ONLY.
            if (LocalizationManager.CurrentLanguage == Language.Japanese &&
                !string.IsNullOrWhiteSpace(infoJa))
                return infoJa!.Trim();

            // 2. The English column. A JP player with a blank info_ja lands here, which is the
            //    intended behaviour while the art/copy batches are still filling in.
            if (!string.IsNullOrWhiteSpace(info)) return info!.Trim();

            // 3. Nothing to say. The caller collapses the row rather than drawing an empty box.
            return string.Empty;
        }
    }
}
