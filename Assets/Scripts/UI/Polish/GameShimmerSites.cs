// ─────────────────────────────────────────────────────────────────────────────
// game_polish_b §D4 — the GAME's cold-fetch sites.
//
// A table BESIDE the moved ShimmerHost, never inside it. ShimmerHost carries the seven GPS
// site constants the GPS session wrote, and §D0 is explicit that the moved files change in
// no way but their path — so the game's names live here. Two tables, one mechanism: both
// are just strings handed to ShimmerHost.Find, which matches on the `_site` field the
// builder stamps.
//
// WHAT EARNS A SITE. Only a list whose FIRST paint can genuinely be empty while the network
// decides. That is the rule GPS §D8 set and the reason the shimmer is honest: it is shown
// on a cold fetch and never on a cache hit, so a player who has seen this screen before
// gets their numbers instantly instead of a loading animation played over data that never
// left. Anything drawn from local data — the mode cards, the hole cards — has no cold state
// and gets no shimmer, however much it might look like a list.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable

namespace Golfin.UI.Polish
{
    /// <summary>Cold-fetch shimmer sites on the game surface. See <c>ShimmerHost</c> for GPS's.</summary>
    public static class GameShimmerSites
    {
        /// <summary>Rankings — the scrolling rows below the podium.</summary>
        public const string RankingsList = "rankings.list";

        /// <summary>Rankings — the three podium cards, which arrive with the same fetch but
        /// occupy their own region and would otherwise sit as three empty card frames.</summary>
        public const string RankingsTop3 = "rankings.top3";

        /// <summary>Tournament selection — the tournament cards.</summary>
        public const string TournamentCards = "tournament.cards";

        /// <summary>Tournament leaderboard — the standings rows.</summary>
        public const string TournamentLeaderboard = "tournament.leaderboard";

        /// <summary>Gacha history — page 1 of the pull log.</summary>
        public const string GachaHistory = "gacha.history";

        /// <summary>General shop — the catalog cards.</summary>
        public const string ShopCatalog = "shop.catalog";

        /// <summary>
        /// Mission selection — the DAILY card, and only it.
        ///
        /// <para>§D4 names "MissionSelection cards", but the cards are not the cold thing:
        /// <c>MissionCatalog.EnsureLoaded()</c> is local and synchronous, so the mission list is
        /// never waiting on anything. The DAILY is genuinely fetched — the controller's own words
        /// are "hidden, fetch, and shown only if the server answers" — which makes it the one
        /// region on this screen where a player waits in front of a blank space.</para>
        /// </summary>
        public const string MissionsDaily = "missions.daily";

        /// <summary>Every site, for the builder and for the tests that check the builder placed
        /// one host per site. A list that has to be maintained by hand is a list that drifts, so
        /// nothing else enumerates these.</summary>
        public static readonly string[] All =
        {
            RankingsList, RankingsTop3, TournamentCards, TournamentLeaderboard,
            GachaHistory, ShopCatalog, MissionsDaily,
        };
    }
}
