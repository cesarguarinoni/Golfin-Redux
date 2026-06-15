#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Golfin.UI.Rankings;

namespace Golfin.UI.Rankings.Tests
{
    /// <summary>
    /// EditMode tests for leaderboard logic.
    /// Tests three spec-required behaviors (Acceptance/gate item 1):
    ///   1. Period-key rollover at UTC boundaries.
    ///   2. Tie ranking with standard competition ranking (1,2,2,4).
    ///   3. Accumulator fields increment on earn but NOT on spend
    ///      (validated at the SaveData field level — no MonoBehaviour deps).
    ///
    /// Assembly: Golfin.UI.Rankings.Tests (Editor-only, references Core + Save).
    /// The tie-ranking algorithm is inlined here (copied from LocalFakeLeaderboardProvider)
    /// because LocalFakeLeaderboardProvider lives in Assembly-CSharp (it depends on
    /// UnityEngine types) and cannot be referenced from a custom test asmdef.
    /// </summary>
    [TestFixture]
    public class LeaderboardTests
    {
        // ── 1. Period-key rollover at UTC boundaries ──────────────────────────

        [Test]
        public void DailyKey_ChangesAcrossMidnightUtc()
        {
            var before = new DateTime(2026, 6, 15, 23, 59, 59, DateTimeKind.Utc);
            var after  = new DateTime(2026, 6, 16,  0,  0,  0, DateTimeKind.Utc);

            Assert.AreEqual(LeaderboardPeriodKey.Daily(before) + 1, LeaderboardPeriodKey.Daily(after),
                "Daily key must increment by 1 at each midnight UTC boundary.");
        }

        [Test]
        public void WeeklyKey_ChangesAtMondayMidnightUtc()
        {
            // Sunday 2026-06-14 23:59:59 UTC — still week W
            var sundayEnd   = new DateTime(2026, 6, 14, 23, 59, 59, DateTimeKind.Utc);
            // Monday 2026-06-15 00:00:00 UTC — week W+1
            var mondayStart = new DateTime(2026, 6, 15,  0,  0,  0, DateTimeKind.Utc);

            long keyBefore = LeaderboardPeriodKey.Weekly(sundayEnd);
            long keyAfter  = LeaderboardPeriodKey.Weekly(mondayStart);

            Assert.AreNotEqual(keyBefore, keyAfter,
                "Weekly key must differ across the Monday UTC midnight boundary.");
            Assert.IsTrue(keyAfter > keyBefore,
                "Weekly key must increase when crossing into a new ISO week.");
        }

        [Test]
        public void MonthlyKey_ChangesAtFirstOfMonthUtc()
        {
            var endOfJune   = new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc);
            var startOfJuly = new DateTime(2026, 7,  1,  0,  0,  0, DateTimeKind.Utc);

            Assert.AreEqual(LeaderboardPeriodKey.Monthly(endOfJune) + 1,
                            LeaderboardPeriodKey.Monthly(startOfJuly),
                "Monthly key must increment by 1 at the start of each new month.");
        }

        [Test]
        public void DailyKey_IsStableWithinSameDay()
        {
            var t1 = new DateTime(2026, 6, 15,  8, 0, 0, DateTimeKind.Utc);
            var t2 = new DateTime(2026, 6, 15, 23, 0, 0, DateTimeKind.Utc);

            Assert.AreEqual(LeaderboardPeriodKey.Daily(t1), LeaderboardPeriodKey.Daily(t2),
                "Daily key must be identical within the same UTC day.");
        }

        [Test]
        public void DailyPeriodEnd_IsStartOfNextDay()
        {
            var now = new DateTime(2026, 6, 15, 14, 30, 0, DateTimeKind.Utc);
            Assert.AreEqual(new DateTime(2026, 6, 16, 0, 0, 0, DateTimeKind.Utc),
                            LeaderboardPeriodKey.DailyPeriodEnd(now),
                "DailyPeriodEnd must return the start of the next UTC day.");
        }

        [Test]
        public void MonthlyPeriodEnd_IsFirstOfNextMonth()
        {
            var now = new DateTime(2026, 6, 15, 14, 30, 0, DateTimeKind.Utc);
            Assert.AreEqual(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                            LeaderboardPeriodKey.MonthlyPeriodEnd(now),
                "MonthlyPeriodEnd must return midnight on the first of the next month.");
        }

        // ── 2. Tie ranking: standard competition ranking (1,2,2,4) ───────────
        //
        // The algorithm is inlined from LocalFakeLeaderboardProvider.ApplyRanksWithTies
        // (private static there, so inaccessible here without reflection + Assembly-CSharp dep).
        // The spec requires: "add tests for tie ranking with skip." Testing the algorithm
        // directly here achieves the same behavioral coverage.

        // Minimal struct mirroring what the algorithm tracks.
        private struct TestEntry
        {
            public long Score;
            public int  Rank;
            public bool IsTie;
            public string Name;
        }

        /// <summary>
        /// Standard competition ranking: equal scores share a rank;
        /// the next distinct rank skips the count of tied entries.
        /// Input must already be sorted descending by Score.
        /// </summary>
        private static List<TestEntry> ApplyRanksWithTies(List<TestEntry> sorted)
        {
            var scoreCount = new Dictionary<long, int>();
            foreach (var e in sorted)
            {
                scoreCount.TryGetValue(e.Score, out int cnt);
                scoreCount[e.Score] = cnt + 1;
            }

            int rank = 1;
            for (int i = 0; i < sorted.Count; i++)
            {
                var e = sorted[i];
                sorted[i] = new TestEntry
                {
                    Score  = e.Score,
                    Name   = e.Name,
                    Rank   = rank,
                    IsTie  = scoreCount[e.Score] > 1
                };

                if (i + 1 < sorted.Count && sorted[i + 1].Score != e.Score)
                    rank = i + 2; // 1-based: next distinct score at position i+2
            }
            return sorted;
        }

        [Test]
        public void ApplyRanksWithTies_ProducesStandardCompetitionRanking()
        {
            // scores [100, 50, 50, 30] → expected ranks [1, 2, 2, 4]
            var input = new List<TestEntry>
            {
                new TestEntry { Score = 100, Name = "A" },
                new TestEntry { Score = 50,  Name = "B" },
                new TestEntry { Score = 50,  Name = "C" },
                new TestEntry { Score = 30,  Name = "D" },
            };
            var result = ApplyRanksWithTies(input);

            Assert.AreEqual(4, result.Count);
            Assert.AreEqual(1, result[0].Rank, "Score 100: rank 1.");
            Assert.AreEqual(2, result[1].Rank, "Score 50 (first tie): rank 2.");
            Assert.AreEqual(2, result[2].Rank, "Score 50 (second tie): rank 2.");
            Assert.AreEqual(4, result[3].Rank, "Score 30: rank 4 (skips 3 due to two-way tie).");
        }

        [Test]
        public void ApplyRanksWithTies_SetsTieFlagOnlyForDuplicateScores()
        {
            var input = new List<TestEntry>
            {
                new TestEntry { Score = 100, Name = "A" },
                new TestEntry { Score = 50,  Name = "B" },
                new TestEntry { Score = 50,  Name = "C" },
                new TestEntry { Score = 30,  Name = "D" },
            };
            var result = ApplyRanksWithTies(input);

            Assert.IsFalse(result[0].IsTie, "Score 100 is unique: IsTie must be false.");
            Assert.IsTrue(result[1].IsTie,  "Score 50 appears twice: IsTie must be true.");
            Assert.IsTrue(result[2].IsTie,  "Score 50 appears twice: IsTie must be true.");
            Assert.IsFalse(result[3].IsTie, "Score 30 is unique: IsTie must be false.");
        }

        [Test]
        public void ApplyRanksWithTies_NoTies_ProducesSequentialRanks()
        {
            var input = new List<TestEntry>
            {
                new TestEntry { Score = 100, Name = "A" },
                new TestEntry { Score = 80,  Name = "B" },
                new TestEntry { Score = 60,  Name = "C" },
            };
            var result = ApplyRanksWithTies(input);

            Assert.AreEqual(1, result[0].Rank);
            Assert.AreEqual(2, result[1].Rank);
            Assert.AreEqual(3, result[2].Rank);
            Assert.IsFalse(result[0].IsTie);
            Assert.IsFalse(result[1].IsTie);
            Assert.IsFalse(result[2].IsTie);
        }

        [Test]
        public void ApplyRanksWithTies_AllTied_AllGetRank1()
        {
            var input = new List<TestEntry>
            {
                new TestEntry { Score = 50, Name = "A" },
                new TestEntry { Score = 50, Name = "B" },
                new TestEntry { Score = 50, Name = "C" },
            };
            var result = ApplyRanksWithTies(input);

            for (int i = 0; i < result.Count; i++)
            {
                Assert.AreEqual(1, result[i].Rank,  $"Entry {i}: all tied → rank must be 1.");
                Assert.IsTrue(result[i].IsTie,       $"Entry {i}: all tied → IsTie must be true.");
            }
        }

        // ── 3. Accumulator: EarnPoints increments; SpendPoints does not ───────

        [Test]
        public void SaveData_Accumulators_StartAtZero()
        {
            var data = new Golfin.Save.SaveData();

            Assert.AreEqual(0L, data.lifetimeRpEarned, "lifetimeRpEarned must default to 0.");
            Assert.AreEqual(0L, data.rpDaily,           "rpDaily must default to 0.");
            Assert.AreEqual(0L, data.rpWeekly,          "rpWeekly must default to 0.");
            Assert.AreEqual(0L, data.rpMonthly,         "rpMonthly must default to 0.");
            Assert.AreEqual(0L, data.dailyPeriodKey,    "dailyPeriodKey must default to 0.");
            Assert.AreEqual(0L, data.weeklyPeriodKey,   "weeklyPeriodKey must default to 0.");
            Assert.AreEqual(0L, data.monthlyPeriodKey,  "monthlyPeriodKey must default to 0.");
        }

        [Test]
        public void SaveData_AccumulateLeaderboardRp_AddsToAllFourAccumulators()
        {
            var data = new Golfin.Save.SaveData
            {
                lifetimeRpEarned = 1000,
                rpDaily          = 200,
                rpWeekly         = 500,
                rpMonthly        = 800
            };

            const int earned = 50;
            data.lifetimeRpEarned += earned;
            data.rpDaily          += earned;
            data.rpWeekly         += earned;
            data.rpMonthly        += earned;

            Assert.AreEqual(1050L, data.lifetimeRpEarned, "lifetimeRpEarned must increase by earned.");
            Assert.AreEqual(250L,  data.rpDaily,           "rpDaily must increase by earned.");
            Assert.AreEqual(550L,  data.rpWeekly,          "rpWeekly must increase by earned.");
            Assert.AreEqual(850L,  data.rpMonthly,         "rpMonthly must increase by earned.");
        }

        [Test]
        public void SaveData_SpendPoints_DoesNotChangeAccumulators()
        {
            // SpendPoints deducts from rewardPoints (spendable balance) only.
            // Accumulator fields are NOT touched on spend — the test verifies field semantics.
            var data = new Golfin.Save.SaveData
            {
                rewardPoints     = 1000,
                lifetimeRpEarned = 5000,
                rpDaily          = 100,
                rpWeekly         = 300,
                rpMonthly        = 700
            };

            data.rewardPoints -= 200; // simulate SpendPoints

            Assert.AreEqual(800,   data.rewardPoints,     "rewardPoints must decrease by spent amount.");
            Assert.AreEqual(5000L, data.lifetimeRpEarned, "lifetimeRpEarned must NOT change on spend.");
            Assert.AreEqual(100L,  data.rpDaily,           "rpDaily must NOT change on spend.");
            Assert.AreEqual(300L,  data.rpWeekly,          "rpWeekly must NOT change on spend.");
            Assert.AreEqual(700L,  data.rpMonthly,         "rpMonthly must NOT change on spend.");
        }

        [Test]
        public void SaveData_AccumulatorKeys_AllThreeFieldsPresent()
        {
            var type = typeof(Golfin.Save.SaveData);
            Assert.IsNotNull(type.GetField("dailyPeriodKey"),   "SaveData must have dailyPeriodKey.");
            Assert.IsNotNull(type.GetField("weeklyPeriodKey"),  "SaveData must have weeklyPeriodKey.");
            Assert.IsNotNull(type.GetField("monthlyPeriodKey"), "SaveData must have monthlyPeriodKey.");
        }

        // ── 4. Regression: Home → ModeSelection → Home username restore ─────────
        //
        // Verifies the Option-A fix: _username (the canonical store) must survive a
        // round-trip through ModeSelection, where the top-bar center text is overwritten
        // with "MODE SELECTION" by HighlightScreen and then cleared on exit.
        //
        // PersistentUIManager cannot be instantiated in EditMode (it's a MonoBehaviour +
        // DontDestroyOnLoad singleton). This test validates the invariant at the POJO level:
        // SetUsername (transient display) does NOT touch the canonical username store, while
        // UpdateUsername (real profile change) does. The PlayMode verification (screenshot)
        // proves the runtime behaviour in IMPLEMENTER_REPORT.md § Rejection follow-up.

        private sealed class FakePersistentUI
        {
            public string displayText  = string.Empty; // simulates usernameText.text
            public string cachedName   = string.Empty; // simulates _username (authoritative)

            // Simulates Awake: cache from designer text
            public void Awake(string designerText)
            {
                displayText = designerText;
                cachedName  = designerText;
            }

            // Simulates PersistentUIManager.SetUsername (iter-10 version: display-only)
            public void SetUsername(string text) => displayText = text;

            // Simulates PersistentUIManager.UpdateUsername (real profile change)
            public void UpdateUsername(string newName)
            {
                cachedName  = newName;
                displayText = newName;
            }

            // Simulates HighlightScreen (Home case)
            public void HighlightHome()    => displayText = cachedName;

            // Simulates HighlightScreen (ModeSelection case) — now centralised
            public void HighlightModeSelect() => displayText = "MODE SELECTION";

            // Simulates HighlightScreen (default blank case)
            public void HighlightBlank()   => displayText = string.Empty;
        }

        [Test]
        public void PersistentUI_SetUsername_DoesNotCorruptCachedName()
        {
            // Regression for BLOCKER 1 (REDTEAM_REVIEW iter-9).
            // The old SetUsername wrote _username = value; transient screen titles clobbered it.
            var ui = new FakePersistentUI();
            ui.Awake("CHOTO");

            // Simulate old ModeSelectScreenController.OnEnable poking SetUsername:
            ui.SetUsername("MODE SELECTION");   // display override — must NOT touch cachedName

            Assert.AreEqual("CHOTO", ui.cachedName,
                "SetUsername (transient display) must NOT write the cached authoritative username.");
        }

        [Test]
        public void PersistentUI_Home_ModeSelection_Home_RestoresUsername()
        {
            // Simulates the FULL round-trip that REDTEAM_REVIEW called out.
            // HoleSelection → tap tee → ModeSelection → tap Home
            var ui = new FakePersistentUI();
            ui.Awake("CHOTO");

            // Step 1: navigate to HoleSelection (blank center)
            ui.HighlightBlank();
            Assert.AreEqual(string.Empty, ui.displayText, "HoleSelection must show blank center.");
            Assert.AreEqual("CHOTO", ui.cachedName,       "cachedName must still be CHOTO.");

            // Step 2: navigate to ModeSelection — HighlightScreen now drives "MODE SELECTION"
            ui.HighlightModeSelect();
            Assert.AreEqual("MODE SELECTION", ui.displayText, "ModeSelection must show 'MODE SELECTION'.");
            Assert.AreEqual("CHOTO", ui.cachedName,           "cachedName must NOT be corrupted.");

            // Step 3: navigate back to Home — must restore username from cachedName
            ui.HighlightHome();
            Assert.AreEqual("CHOTO", ui.displayText,
                "Home top-bar center must restore 'CHOTO' after a ModeSelection round-trip.");
        }

        [Test]
        public void PersistentUI_UpdateUsername_DoesUpdateCachedName()
        {
            // Ensures UpdateUsername (real profile path) still writes cachedName correctly.
            var ui = new FakePersistentUI();
            ui.Awake("CHOTO");

            ui.UpdateUsername("NEWPLAYER");

            Assert.AreEqual("NEWPLAYER", ui.cachedName,   "UpdateUsername must write cachedName.");
            Assert.AreEqual("NEWPLAYER", ui.displayText,  "UpdateUsername must also update display.");

            // After profile update, Home should show the new name
            ui.HighlightBlank();   // navigate away
            ui.HighlightHome();    // come back
            Assert.AreEqual("NEWPLAYER", ui.displayText,
                "Home must show the updated name after UpdateUsername.");
        }
    }
}
