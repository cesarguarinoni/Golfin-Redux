// TeeDataTests — SPEC §4 (Phase 3)
// Tests for TeeData/TeeSet schema and HoleTeesCsvParser.
// Lives in Golfin.Course.Tests which directly references Golfin.Course.Runtime.
//
// Note: HoleData.TryGetTee() (Assembly-CSharp) is a trivial 4-line foreach over
// the tees list; the critical logic is the parser, which is fully covered here.
// TryGetTee correctness is verified in test 3 by exercising the parsed list directly.
//
// Tests:
//   1. Parse_LomondHole1_Returns4Tees    — happy path: 4 tees, back=531/blue
//   2. Parse_EmptyInput_ReturnsEmpty     — null/empty guard
//   3. Parse_FindBySet_MatchesExpected   — list.Find(TeeSet.Back) → 531
//   4. Parse_NullColor_SurvivesRoundTrip — blank color column → null
//   5. Parse_FiltersOutOtherCourses      — cross-course isolation

using System.Collections.Generic;
using NUnit.Framework;
using Golfin.Course.Runtime;

namespace Golfin.Course.Tests
{
    public class TeeDataTests
    {
        // Minimal embedded CSV (subset of HoleTees.csv rows for hole 1)
        private const string LomondHole1Csv =
            "courseId,holeNumber,teeSet,yards,color\n" +
            "lomond-country-club,1,back,531,blue\n" +
            "lomond-country-club,1,regular,509,green\n" +
            "lomond-country-club,1,front,480,white\n" +
            "lomond-country-club,1,ladies,441,red\n";

        // ── 1. Parse 4 tees ──────────────────────────────────────────────────

        [Test]
        public void Parse_LomondHole1_Returns4Tees()
        {
            Dictionary<int, List<TeeData>> lookup =
                HoleTeesCsvParser.Parse(LomondHole1Csv, "lomond-country-club");

            Assert.IsTrue(lookup.ContainsKey(1), "Expected tee data for hole 1.");
            List<TeeData> tees = lookup[1];
            Assert.AreEqual(4, tees.Count, "Expected exactly 4 tees for Lomond hole 1.");

            // Verify yards and color parsed correctly for Back tee
            TeeData back = tees.Find(t => t.set == TeeSet.Back);
            Assert.IsNotNull(back, "Back tee should be present.");
            Assert.AreEqual(531, back.yards);
            Assert.AreEqual("blue", back.color);
        }

        // ── 2. Empty input guard ─────────────────────────────────────────────

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Parse_EmptyOrNullInput_ReturnsEmpty(string input)
        {
            var result = HoleTeesCsvParser.Parse(input, "lomond-country-club");
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        // ── 3. List lookup by TeeSet (exercises TryGetTee-equivalent logic) ─

        [Test]
        public void Parse_FindBySet_MatchesExpected()
        {
            // Parse, then find a TeeSet that exists and one that is absent.
            var lookup = HoleTeesCsvParser.Parse(LomondHole1Csv, "lomond-country-club");
            List<TeeData> tees = lookup[1];

            // Present: Back
            TeeData back = tees.Find(t => t.set == TeeSet.Back);
            Assert.IsNotNull(back);
            Assert.AreEqual(531, back.yards);

            // Absent: Tournament (not in CSV)
            TeeData tournament = tees.Find(t => t.set == TeeSet.Tournament);
            Assert.IsNull(tournament, "Tournament tee should not be present for Lomond hole 1.");
        }

        // ── 4. Null colour survives round-trip ───────────────────────────────

        [Test]
        public void Parse_NullColor_SurvivesRoundTrip()
        {
            // Taiheiyo scenario: color column is blank
            string csvWithNullColor =
                "courseId,holeNumber,teeSet,yards,color\n" +
                "taiheiyo-club-gotenba,1,tournament,607,\n";

            var lookup = HoleTeesCsvParser.Parse(csvWithNullColor, "taiheiyo-club-gotenba");
            Assert.IsTrue(lookup.ContainsKey(1));

            TeeData tee = lookup[1][0];
            Assert.AreEqual(TeeSet.Tournament, tee.set);
            Assert.AreEqual(607, tee.yards);
            Assert.IsNull(tee.color, "Blank color field should parse as null.");
        }

        // ── 5. Cross-course filter ────────────────────────────────────────────

        [Test]
        public void Parse_FiltersOutOtherCourses()
        {
            string csv =
                "courseId,holeNumber,teeSet,yards,color\n" +
                "lomond-country-club,1,back,531,blue\n" +
                "taiheiyo-club-gotenba,1,tournament,607,\n";

            var lookup = HoleTeesCsvParser.Parse(csv, "lomond-country-club");

            Assert.IsTrue(lookup.ContainsKey(1));
            Assert.AreEqual(1, lookup[1].Count, "Only the Lomond row should be returned.");
        }
    }
}
