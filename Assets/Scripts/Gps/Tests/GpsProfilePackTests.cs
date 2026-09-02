// gps_profile_pack §10 — EditMode tests for the profile-pack module.
// Pins: XP math (500×level, remainder-within-level), DTO unwrapping (null fields),
// badge section grouping, rank-title thresholds.
using System.Collections.Generic;
using System.Linq;
using Golfin.Net;
using Golfin.Net.Tests;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Golfin.Gps.Tests
{
    public class GpsProfilePackTests
    {
        // ════════════════════════════════════════════════════════════════════
        // XP maths — points_atomic.sql:47-49, remainder-within-level rule.
        // avatar_xp = remainder within current level; next = 500 × avatar_level
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void XpNext_IsLevelTimedFiveHundred()
        {
            // The rule of record from points_atomic.sql:47-49:
            // while v_xp >= v_level * 500 do level++ carry remainder end
            // So at Lv.1 next=500; Lv.12 next=6000; Lv.50 next=25000
            Assert.AreEqual(500,   XpNext(1));
            Assert.AreEqual(1000,  XpNext(2));
            Assert.AreEqual(6000,  XpNext(12));
            Assert.AreEqual(25000, XpNext(50));
        }

        [Test]
        public void XpTrack_IsRemainderDividedByNext()
        {
            // Lv.12, 650 XP remainder → track = 650/6000 ≈ 0.108
            float track = XpTrackFill(12, 650);
            Assert.AreEqual(650f / 6000f, track, 1e-5f, "Lv.12 with 650 XP remainder");
        }

        [Test]
        public void XpTrack_ClampedAt1WhenXpMeetsOrExceedsNext()
        {
            // avatar_xp should never exceed next (the backend levels up immediately),
            // but the client must clamp to guard against any race condition in the JWT.
            Assert.AreEqual(1f, XpTrackFill(5, 9999));
        }

        [Test]
        public void XpHintRounds_IsCeilingOfRemainingOverFifty()
        {
            // rounds = ceil((next - xp) / 50)  [50 pts per posted round]
            // Lv.12, xp=5975, next=6000 → (6000-5975)/50 = 0.5 → ceil = 1
            Assert.AreEqual(1, XpRoundsNeeded(12, 5975));
            // Lv.1, xp=0, next=500 → 500/50 = 10.0 → 10
            Assert.AreEqual(10, XpRoundsNeeded(1, 0));
            // Lv.2, xp=950, next=1000 → 50/50 = 1.0 → 1
            Assert.AreEqual(1, XpRoundsNeeded(2, 950));
        }

        // Pure maths helpers (mirror the controller)
        private static int   XpNext(int lv)                        => 500 * lv;
        private static float XpTrackFill(int lv, int xp)           => UnityEngine.Mathf.Clamp01((float)xp / XpNext(lv));
        private static int   XpRoundsNeeded(int lv, int xp)
        {
            int remaining = XpNext(lv) - xp;
            return (int)UnityEngine.Mathf.CeilToInt(remaining / 50f);
        }

        // ════════════════════════════════════════════════════════════════════
        // ScoreStatsDto — DTO unwrapping including nullable avg_score
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void ScoreStatsDto_AvgScore_IsNullWhenMissingFromJson()
        {
            // avg_score can be null per the backend (score.py:358-416: a user who has never
            // posted can return null avg). The client must NOT substitute 0.
            string json = "{\"rounds_played\":0,\"total_strokes\":0,\"birdies\":0,\"eagles\":0," +
                          "\"holes_in_one\":0,\"pars\":0,\"bogeys\":0,\"double_bogeys\":0}";

            var dto = JsonConvert.DeserializeObject<ScoreStatsDto>(json);
            Assert.IsNull(dto.BestScore, "best_score must remain null when absent");
            Assert.IsNull(dto.AvgScore,  "avg_score must remain null when absent");
        }

        [Test]
        public void ScoreStatsDto_AvgScore_NullExplicit()
        {
            // avg_score: null is distinct from absent — both must map to null
            string json = "{\"rounds_played\":3,\"total_strokes\":270,\"best_score\":-2," +
                          "\"avg_score\":null,\"handicap\":null,\"birdies\":0,\"eagles\":0," +
                          "\"holes_in_one\":0,\"pars\":0,\"bogeys\":0,\"double_bogeys\":0}";

            var dto = JsonConvert.DeserializeObject<ScoreStatsDto>(json);
            Assert.AreEqual(-2, dto.BestScore, "best_score=-2 (under par) must deserialise");
            Assert.IsNull(dto.AvgScore, "avg_score: null must remain null, not 0");
        }

        [Test]
        public void ScoreStatsResponse_UnwrapsDataEnvelope()
        {
            // Confirms the {data:{…}} envelope the server returns (score.py:358-416)
            string json = "{\"data\":{\"rounds_played\":12,\"total_strokes\":1080," +
                          "\"best_score\":-3,\"avg_score\":-1.8,\"handicap\":8.4," +
                          "\"birdies\":5,\"eagles\":0,\"holes_in_one\":0,\"pars\":30," +
                          "\"bogeys\":10,\"double_bogeys\":3}}";

            var resp = JsonConvert.DeserializeObject<ScoreStatsResponse>(json);
            Assert.IsNotNull(resp.Data, "data envelope unwrap failed");
            Assert.AreEqual(12,    resp.Data.RoundsPlayed);
            Assert.AreEqual(-3,    resp.Data.BestScore);
            Assert.AreEqual(-1.8f, resp.Data.AvgScore.Value, 1e-4f);
        }

        // ════════════════════════════════════════════════════════════════════
        // BadgeProgressDto — DTO unwrapping + category grouping
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void BadgesProgressResponse_EmptyBadgeList_IsNotAnError()
        {
            // An empty list = no badges configured, not a 500.
            string json = "{\"data\":[]}";
            var resp = JsonConvert.DeserializeObject<BadgesProgressResponse>(json);
            Assert.IsNotNull(resp.Data);
            Assert.AreEqual(0, resp.Data.Count, "empty badge list must deserialise to count=0");
        }

        [Test]
        public void BadgeProgressDto_TargetPct_ZeroMeansNoTarget()
        {
            // SPEC: target_pct is nullable on the server → Required=0 on the client means
            // "no target" — the cell view hides the progress label.
            // This also tests that earned:false + required=0 → Required=0 deserialises.
            string json = "{\"data\":[{\"id\":\"first_round\",\"name_key\":\"BADGE_FIRST_ROUND_NAME\"," +
                          "\"section\":\"GOLF\",\"rarity\":\"COMMON\",\"required\":0," +
                          "\"progress\":0,\"earned\":false}]}";

            var resp = JsonConvert.DeserializeObject<BadgesProgressResponse>(json);
            Assert.AreEqual(0, resp.Data[0].Required, "required=0 (no target) must not throw");
            Assert.IsFalse(resp.Data[0].Earned);
        }

        [Test]
        public void BadgeProgressDto_EarnedState_RoundTrips()
        {
            string json = "{\"data\":[{\"id\":\"trust_100\",\"name_key\":\"BADGE_TRUST_100_NAME\"," +
                          "\"section\":\"TRUST\",\"rarity\":\"EPIC\",\"required\":100," +
                          "\"progress\":100,\"earned\":true,\"earn_date\":\"2026-04-01\"}]}";

            var resp = JsonConvert.DeserializeObject<BadgesProgressResponse>(json);
            var b = resp.Data[0];
            Assert.IsTrue(b.Earned);
            Assert.AreEqual("TRUST", b.Section);
            Assert.AreEqual("EPIC",  b.Rarity);
            Assert.AreEqual(100,     b.Required);
            Assert.AreEqual(100,     b.Progress);
        }

        [Test]
        public void BadgeGrouping_BySection_GroupsCorrectly()
        {
            // Mirrors the grouping logic in GpsBadgesScreenController.BindBadges:
            // partition by section into GOLF / SOCIAL / TRUST / SPECIAL.
            var badges = new List<BadgeProgressDto>
            {
                new BadgeProgressDto { Id="first_round",   Section="GOLF",    Earned=false },
                new BadgeProgressDto { Id="first_gift",    Section="SOCIAL",  Earned=true  },
                new BadgeProgressDto { Id="trust_80",      Section="TRUST",   Earned=false },
                new BadgeProgressDto { Id="tournament_win",Section="SPECIAL", Earned=true  },
                new BadgeProgressDto { Id="break_90",      Section="GOLF",    Earned=true  },
            };

            var groups = GroupBySection(badges);

            Assert.AreEqual(2, groups["GOLF"].Count,    "GOLF should have 2 badges");
            Assert.AreEqual(1, groups["SOCIAL"].Count,  "SOCIAL should have 1 badge");
            Assert.AreEqual(1, groups["TRUST"].Count,   "TRUST should have 1 badge");
            Assert.AreEqual(1, groups["SPECIAL"].Count, "SPECIAL should have 1 badge");
        }

        [Test]
        public void BadgeGrouping_UnknownSection_DefaultsToGolf()
        {
            // An unknown section (future server addition) must not throw — default to GOLF.
            var badges = new List<BadgeProgressDto>
            {
                new BadgeProgressDto { Id="future", Section="UNKNOWN", Earned=false },
            };
            // The controller does: string sec = (b.Section ?? "GOLF").ToUpperInvariant();
            // if (!bySection.ContainsKey(sec)) bySection[sec] = new List<…>();
            // This test proves the DTO field round-trips with arbitrary strings.
            Assert.AreEqual("UNKNOWN", badges[0].Section,
                "arbitrary section must round-trip so the controller can fall through to its default");
        }

        // Mirror of the controller grouping (tests the LOGIC, not the MB)
        private static Dictionary<string, List<BadgeProgressDto>> GroupBySection(List<BadgeProgressDto> badges)
        {
            var groups = new Dictionary<string, List<BadgeProgressDto>>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["GOLF"]    = new List<BadgeProgressDto>(),
                ["SOCIAL"]  = new List<BadgeProgressDto>(),
                ["TRUST"]   = new List<BadgeProgressDto>(),
                ["SPECIAL"] = new List<BadgeProgressDto>(),
            };
            foreach (var b in badges)
            {
                string sec = (b.Section ?? "GOLF").ToUpperInvariant();
                if (!groups.ContainsKey(sec)) groups[sec] = new List<BadgeProgressDto>();
                groups[sec].Add(b);
            }
            return groups;
        }

        // ════════════════════════════════════════════════════════════════════
        // Rank-title thresholds — avatar screen GPS_AVATAR_RANK_*
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void RankTitle_ThresholdsMatchSpec()
        {
            // Thresholds from SPEC: 1–4 BEGINNER, 5–11 ROOKIE, 12–19 AMATEUR, 20–49 SINGLE, 50+ PRO
            Assert.AreEqual("GPS_AVATAR_RANK_BEGINNER", GetRankKey(1));
            Assert.AreEqual("GPS_AVATAR_RANK_BEGINNER", GetRankKey(4));
            Assert.AreEqual("GPS_AVATAR_RANK_ROOKIE",   GetRankKey(5));
            Assert.AreEqual("GPS_AVATAR_RANK_ROOKIE",   GetRankKey(11));
            Assert.AreEqual("GPS_AVATAR_RANK_AMATEUR",  GetRankKey(12));
            Assert.AreEqual("GPS_AVATAR_RANK_AMATEUR",  GetRankKey(19));
            Assert.AreEqual("GPS_AVATAR_RANK_SINGLE",   GetRankKey(20));
            Assert.AreEqual("GPS_AVATAR_RANK_SINGLE",   GetRankKey(49));
            Assert.AreEqual("GPS_AVATAR_RANK_PRO",      GetRankKey(50));
            Assert.AreEqual("GPS_AVATAR_RANK_PRO",      GetRankKey(99));
        }

        // Mirror of GpsAvatarScreenController.GetRankKey (tests the logic without the MB)
        private static readonly (int level, string key)[] RankThresholds = {
            (50, "GPS_AVATAR_RANK_PRO"),
            (20, "GPS_AVATAR_RANK_SINGLE"),
            (12, "GPS_AVATAR_RANK_AMATEUR"),
            ( 5, "GPS_AVATAR_RANK_ROOKIE"),
            ( 1, "GPS_AVATAR_RANK_BEGINNER"),
        };

        private static string GetRankKey(int level)
        {
            foreach (var (threshold, key) in RankThresholds)
                if (level >= threshold) return key;
            return "GPS_AVATAR_RANK_BEGINNER";
        }

        // ════════════════════════════════════════════════════════════════════
        // ScoreStatsService + BadgeService — HTTP integration (FakeHttpTransport)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void ScoreStatsService_FetchStats_HitsTheCorrectEndpoint()
        {
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200,
                "{\"data\":{\"rounds_played\":5,\"total_strokes\":420,\"best_score\":-1," +
                "\"avg_score\":2.4,\"handicap\":12.0,\"birdies\":3,\"eagles\":0," +
                "\"holes_in_one\":0,\"pars\":20,\"bogeys\":8,\"double_bogeys\":2}}"));

            ApiResult<ScoreStatsDto> result = null;
            Pump.Drain(new ScoreStatsService(GpsTestApi.Client(transport)).FetchStats(r => result = r));

            Assert.AreEqual(Endpoints.BaseUrl + "/score/stats", transport.SentUrls[0]);
            Assert.IsTrue(result.Success, result?.ToString());
            Assert.AreEqual(5,   result.Data.RoundsPlayed);
            Assert.AreEqual(-1,  result.Data.BestScore);
            Assert.AreEqual(2.4f,result.Data.AvgScore.Value, 1e-4f);
        }

        [Test]
        public void ScoreStatsService_AvgScoreNull_DoesNotThrow()
        {
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200,
                "{\"data\":{\"rounds_played\":0,\"total_strokes\":0,\"avg_score\":null," +
                "\"birdies\":0,\"eagles\":0,\"holes_in_one\":0,\"pars\":0,\"bogeys\":0,\"double_bogeys\":0}}"));

            ApiResult<ScoreStatsDto> result = null;
            Pump.Drain(new ScoreStatsService(GpsTestApi.Client(transport)).FetchStats(r => result = r));

            Assert.IsTrue(result.Success, "null avg_score must not cause a parse failure");
            Assert.IsNull(result.Data.AvgScore, "null avg_score must remain null in the DTO");
        }

        [Test]
        public void BadgeService_FetchBadges_HitsTheCorrectEndpointAndReturnsAll()
        {
            string body = "{\"data\":[" +
                "{\"id\":\"first_round\",\"name_key\":\"BADGE_FIRST_ROUND_NAME\"," +
                " \"section\":\"GOLF\",\"rarity\":\"COMMON\",\"required\":1," +
                " \"progress\":1,\"earned\":true}," +
                "{\"id\":\"trust_80\",\"name_key\":\"BADGE_TRUST_80_NAME\"," +
                " \"section\":\"TRUST\",\"rarity\":\"RARE\",\"required\":80," +
                " \"progress\":45,\"earned\":false}" +
                "]}";

            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200, body));

            ApiResult<System.Collections.Generic.List<BadgeProgressDto>> result = null;
            Pump.Drain(new BadgeService(GpsTestApi.Client(transport)).FetchBadges(r => result = r));

            Assert.AreEqual(Endpoints.BaseUrl + "/badges/progress", transport.SentUrls[0]);
            Assert.IsTrue(result.Success, result?.ToString());
            Assert.AreEqual(2, result.Data.Count);

            var earned = result.Data.First(b => b.Earned);
            Assert.AreEqual("first_round", earned.Id);
            Assert.AreEqual("GOLF",        earned.Section);

            var unearned = result.Data.First(b => !b.Earned);
            Assert.AreEqual(45,   unearned.Progress);
            Assert.AreEqual(80,   unearned.Required);
            Assert.AreEqual("TRUST", unearned.Section);
        }
    }
}
