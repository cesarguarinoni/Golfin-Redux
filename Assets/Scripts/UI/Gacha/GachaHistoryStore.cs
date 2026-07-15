// Assets/Scripts/UI/Gacha/GachaHistoryStore.cs
// Mock history store — Stage 1. Returns ~12 hard-coded records (7 clubs + 5 balls),
// globally newest-first. Real pull records will replace this in a later stage.
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace GolfinRedux.UI.Gacha
{
    public static class GachaHistoryStore
    {
        // Singleton-lazy so unit tests can call it without Unity runtime.
        private static List<GachaHistoryRecord>? _records;

        public static IReadOnlyList<GachaHistoryRecord> All
        {
            get
            {
                if (_records == null) _records = BuildMock();
                return _records;
            }
        }

        /// <summary>Returns records matching the predicate, preserving newest-first order.</summary>
        public static IReadOnlyList<GachaHistoryRecord> Filter(Func<GachaHistoryRecord, bool> predicate)
            => All.Where(predicate).ToList();

        /// <summary>
        /// Internal seam: filter by reward type int value (0=Club,1=Ball,etc.) without
        /// needing to construct a typed delegate. Used by EditMode tests via reflection.
        /// </summary>
        internal static IReadOnlyList<GachaHistoryRecord> FilterByRewardTypeInt(int rewardTypeInt)
            => Filter(r => (int)r.RewardType == rewardTypeInt);

        /// <summary>Force reload (for tests).</summary>
        public static void Reload() => _records = null;

        // ── Mock data ─────────────────────────────────────────────────────────
        // Club IDs from Resources/Data/Clubs.csv:
        //   club_driver_gf, club_wood_gf, club_iron9_klyro, club_iron7_mireo,
        //   club_awedge_fyloe, club_pwedge_royal, club_putter_golfinx
        // Ball IDs from Assets/Data/Balls.csv: ball_golfin, ball_putt_ace
        // Banner IDs from Resources/Data/gacha_banners.csv:
        //   banner_standard_club1, banner_test_a, banner_test_b
        //
        // Records are in globally newest-first order by PulledUtc.
        private static List<GachaHistoryRecord> BuildMock()
        {
            return new List<GachaHistoryRecord>
            {
                // 2026-07-14T23:50 — club pull (x10, driver)
                new GachaHistoryRecord(
                    GachaRewardType.Club, "club_driver_gf", 1,
                    "banner_standard_club1", TicketType.Standard, 10, "2026-07-14T23:50:00Z"),

                // 2026-07-14T23:00 — ball pull (x10, putt_ace x3)
                new GachaHistoryRecord(
                    GachaRewardType.Ball, "ball_putt_ace", 3,
                    "banner_test_a", TicketType.Standard, 10, "2026-07-14T23:00:00Z"),

                // 2026-07-14T22:10 — club pull (x10, wood)
                new GachaHistoryRecord(
                    GachaRewardType.Club, "club_wood_gf", 1,
                    "banner_test_b", TicketType.Standard, 10, "2026-07-14T22:10:00Z"),

                // 2026-07-14T20:30 — ball pull (x10, golfin x5)
                new GachaHistoryRecord(
                    GachaRewardType.Ball, "ball_golfin", 5,
                    "banner_standard_club1", TicketType.Standard, 10, "2026-07-14T20:30:00Z"),

                // 2026-07-13T18:05 — club pull (x1, iron9)
                new GachaHistoryRecord(
                    GachaRewardType.Club, "club_iron9_klyro", 1,
                    "banner_test_a", TicketType.Standard, 1, "2026-07-13T18:05:00Z"),

                // 2026-07-12T11:30 — club pull (x1, iron7)
                new GachaHistoryRecord(
                    GachaRewardType.Club, "club_iron7_mireo", 1,
                    "banner_standard_club1", TicketType.Standard, 1, "2026-07-12T11:30:00Z"),

                // 2026-07-11T14:15 — ball pull (x1, putt_ace x1)
                new GachaHistoryRecord(
                    GachaRewardType.Ball, "ball_putt_ace", 1,
                    "banner_test_a", TicketType.Standard, 1, "2026-07-11T14:15:00Z"),

                // 2026-07-10T09:00 — club pull (x10, approach wedge)
                new GachaHistoryRecord(
                    GachaRewardType.Club, "club_awedge_fyloe", 1,
                    "banner_test_b", TicketType.Standard, 10, "2026-07-10T09:00:00Z"),

                // 2026-07-09T15:45 — club pull (x1, pitching wedge)
                new GachaHistoryRecord(
                    GachaRewardType.Club, "club_pwedge_royal", 1,
                    "banner_standard_club1", TicketType.Standard, 1, "2026-07-09T15:45:00Z"),

                // 2026-07-08T08:20 — club pull (x1, putter)
                new GachaHistoryRecord(
                    GachaRewardType.Club, "club_putter_golfinx", 1,
                    "banner_standard_club1", TicketType.Standard, 1, "2026-07-08T08:20:00Z"),

                // 2026-07-07T10:00 — ball pull (x10, golfin x10)
                new GachaHistoryRecord(
                    GachaRewardType.Ball, "ball_golfin", 10,
                    "banner_standard_club1", TicketType.Standard, 10, "2026-07-07T10:00:00Z"),

                // 2026-07-05T16:40 — ball pull (x1, putt_ace x2)
                new GachaHistoryRecord(
                    GachaRewardType.Ball, "ball_putt_ace", 2,
                    "banner_test_a", TicketType.Standard, 1, "2026-07-05T16:40:00Z"),
            };
        }
    }
}
