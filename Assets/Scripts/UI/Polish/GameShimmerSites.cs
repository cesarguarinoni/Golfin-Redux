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

        // THERE IS NO SHOP SITE, deliberately. §D4 lists the General Shop's catalog cards, but
        // GeneralShopCatalog reads a BUNDLED Resources/Data/shop_catalog.csv plus a content
        // overlay, synchronously, on first access — the player never waits on a network for it,
        // so a placeholder there would be a loading animation over data that never left. A named
        // constant nobody may use is a trap, so the decision is recorded here rather than as a
        // dangling site. The shop still gets §D6's stagger on its first paint per entry.

        // THERE IS NO MISSIONS-DAILY SITE ANY MORE, and this is the second decision of the same
        // shape as the shop one above — recorded here rather than left as a dangling constant.
        //
        // game_polish_b §D4 put one here on reasoning that was locally correct: the daily IS
        // fetched and IS hidden until the server answers, so it is the one region on that screen
        // where a player waits in front of a blank space. What that reasoning left out is HOW
        // LONG. The wait is a single request that lands in roughly 200 ms, and the daily is cold
        // on EVERY visit (the gate is re-armed in OnEnable), so the placeholder ran on every
        // single entry to the screen — a highlight band sweeping left-to-right across a 978x374
        // block for a fifth of a second, with the real card popping in over it.
        //
        // Cesar, 2026-09-09, on the post-polish build: the daily card "visibly arrives as a
        // bubble from the left". A placeholder is worth what it saves the player from looking at;
        // over 200 ms it is not worth its own animation. The card now simply FADES IN when it
        // arrives (MissionSelectionScreenController.EndDailyWait -> GpsPaintMotion.FadeInPanel),
        // which is the same treatment every other §D4 panel gets and is invisible at that
        // duration in the way a sweeping band is not.
        //
        // The rule this leaves behind for the next site: a shimmer is for a wait the player can
        // SEE. Measure the wait before placing one.

        /// <summary>Every site, for the builder and for the tests that check the builder placed
        /// one host per site. A list that has to be maintained by hand is a list that drifts, so
        /// nothing else enumerates these.</summary>
        public static readonly string[] All =
        {
            RankingsList, RankingsTop3, TournamentCards, TournamentLeaderboard,
            GachaHistory,
        };
    }
}
