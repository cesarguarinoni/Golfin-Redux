using System.Collections.Generic;
using Golfin.Gameplay.Missions;
using NUnit.Framework;

namespace GolfinRedux.Tests.EditMode
{
    /// <summary>
    /// The two mission unlock rules (missions_v1 §C2), and the catalog they read.
    ///
    /// WHY BOTH DIRECTIONS OF EVERY GATE. A gate that never opens is a campaign nobody can
    /// progress; a gate that never closes hands a day-one player the Legend tier. Neither is
    /// visible in play — the cards just look wrong in a way that reads as a design choice — so
    /// neither is inferred from the other.
    ///
    /// These run against the REAL bundled catalogs, not a fixture. A fixture would drift from
    /// the 40 missions that actually ship, and the 8-of-10 gate only means something against
    /// tiers that really do hold 10.
    /// </summary>
    public class MissionProgressionTests
    {
        private MissionProgressionService P => MissionProgressionService.Instance;

        [SetUp]
        public void SetUp()
        {
            MissionCatalog.Reload();
            MissionProgressionService.ResetForTests();
            P.ClearForTests();
        }

        [TearDown]
        public void TearDown() => P.ClearForTests();

        private static List<MissionDefinition> Tier(string tier)
        {
            var outList = new List<MissionDefinition>();
            foreach (var m in MissionCatalog.All) if (m.Tier == tier) outList.Add(m);
            return outList;
        }

        // ── The catalog itself ──────────────────────────────────────────────────

        [Test]
        public void the_bundled_catalog_carries_forty_missions_in_four_tiers_of_ten()
        {
            Assert.AreEqual(40, MissionCatalog.All.Count);
            Assert.AreEqual(4, MissionCatalog.Tiers.Count);
            foreach (var t in MissionCatalog.Tiers)
                Assert.AreEqual(10, Tier(t.Tier).Count, $"{t.Tier} should hold 10 missions");
        }

        [Test]
        public void every_mission_resolves_a_start_area_a_wind_and_a_loadout()
        {
            // The Phase B bake is what makes this true — before it, every short start was blank.
            // A mission missing any of the three is a card §C3 has to render un-playable.
            foreach (var m in MissionCatalog.All)
            {
                Assert.IsNotEmpty(m.StartKind, $"#{m.Id} has no start kind");
                Assert.IsNotEmpty(m.WindPresetId, $"#{m.Id} has no wind preset");
                Assert.IsNotEmpty(m.LoadoutId, $"#{m.Id} has no loadout");
                if (m.StartKind == "short")
                    Assert.IsTrue(m.StartWorld.HasValue, $"#{m.Id} starts at {m.StartAreaId} but it is not baked");
            }
        }

        [Test]
        public void no_mission_starts_on_an_unbaked_short_area()
        {
            // Hole 13's SAND is deliberately blank (no greenside bunker), so this also pins
            // that mission 37 was re-sited off it.
            foreach (var m in MissionCatalog.All)
                Assert.IsFalse(m.StartKind == "short" && !m.StartWorld.HasValue,
                    $"#{m.Id} ({m.Key}) starts on an unbaked {m.StartAreaId} on hole {m.HoleNumber}");
        }

        // ── Rule 1: the chain WITHIN a tier ─────────────────────────────────────

        [Test]
        public void the_first_mission_is_open_and_the_second_is_not()
        {
            var first = MissionCatalog.All[0];
            var second = MissionCatalog.All[1];
            Assert.AreEqual("start", first.Unlock);
            Assert.IsTrue(P.IsUnlocked(first), "mission 1 must be playable on a fresh save");
            Assert.IsFalse(P.IsUnlocked(second), "mission 2 must wait for mission 1");
        }

        [Test]
        public void clearing_a_mission_opens_the_next_one()
        {
            var first = MissionCatalog.All[0];
            var second = MissionCatalog.All[1];
            P.SeedForTests(first.Id, clears: 1);
            Assert.IsTrue(P.IsUnlocked(second));
        }

        [Test]
        public void an_ATTEMPT_does_not_open_the_next_one()
        {
            // clears 0 with attempts > 0 is "tried and failed" — a real state, and NOT a clear.
            var first = MissionCatalog.All[0];
            P.SeedForTests(first.Id, clears: 0, attempts: 4);
            Assert.IsTrue(P.HasFailed(first.Id));
            Assert.IsFalse(P.IsUnlocked(MissionCatalog.All[1]));
        }

        // ── Rule 2: the 8-of-10 tier gate ───────────────────────────────────────

        [Test]
        public void the_first_tier_is_open_on_a_fresh_save_and_the_rest_are_not()
        {
            Assert.IsTrue(P.IsTierUnlocked("Beginner"));
            Assert.IsFalse(P.IsTierUnlocked("Amateur"));
            Assert.IsFalse(P.IsTierUnlocked("Pro"));
            Assert.IsFalse(P.IsTierUnlocked("Legend"));
        }

        [Test]
        public void seven_of_ten_is_not_enough_and_eight_is()
        {
            var beginner = Tier("Beginner");
            for (int i = 0; i < 7; i++) P.SeedForTests(beginner[i].Id, clears: 1);
            Assert.IsFalse(P.IsTierUnlocked("Amateur"), "7 of 10 must not open the next tier");
            Assert.AreEqual(1, P.ClearsNeededFor("Amateur"));

            P.SeedForTests(beginner[7].Id, clears: 1);
            Assert.IsTrue(P.IsTierUnlocked("Amateur"), "8 of 10 must open it");
            Assert.AreEqual(0, P.ClearsNeededFor("Amateur"));
        }

        [Test]
        public void opening_a_tier_does_not_open_the_one_after_it()
        {
            var beginner = Tier("Beginner");
            for (int i = 0; i < 8; i++) P.SeedForTests(beginner[i].Id, clears: 1);
            Assert.IsTrue(P.IsTierUnlocked("Amateur"));
            Assert.IsFalse(P.IsTierUnlocked("Pro"), "Pro needs 8 AMATEUR clears, not 8 Beginner ones");
        }

        [Test]
        public void a_mission_in_a_locked_tier_is_locked_even_when_its_own_chain_is_satisfied()
        {
            // The two rules are AND-ed. Clearing all ten Beginner missions satisfies mission
            // 11's `clear:10`, but Amateur is gated on 8 — which ten also satisfies, so gate
            // it with a tier whose predecessor is untouched instead.
            var amateur = Tier("Amateur");
            P.SeedForTests(amateur[9].Id, clears: 1);      // last Amateur cleared, somehow
            var firstPro = Tier("Pro")[0];
            Assert.IsFalse(P.IsTierUnlocked("Pro"), "1 of 10 Amateur clears does not open Pro");
            Assert.IsFalse(P.IsUnlocked(firstPro));
        }

        // ── NEXT ────────────────────────────────────────────────────────────────

        [Test]
        public void NEXT_is_mission_one_on_a_fresh_save()
        {
            Assert.AreEqual(MissionCatalog.All[0].Id, P.NextMission()?.Id);
        }

        [Test]
        public void NEXT_advances_as_missions_are_cleared()
        {
            P.SeedForTests(MissionCatalog.All[0].Id, clears: 1);
            Assert.AreEqual(MissionCatalog.All[1].Id, P.NextMission()?.Id);
            P.SeedForTests(MissionCatalog.All[1].Id, clears: 2);
            Assert.AreEqual(MissionCatalog.All[2].Id, P.NextMission()?.Id);
        }

        [Test]
        public void NEXT_skips_nothing_and_never_points_at_a_locked_mission()
        {
            var next = P.NextMission();
            Assert.IsNotNull(next);
            Assert.IsTrue(P.IsUnlocked(next), "NEXT must always be playable");
            Assert.IsFalse(P.HasCleared(next.Id), "NEXT must not be something already cleared");
        }

        // ── Counts the tier tabs render ─────────────────────────────────────────

        [Test]
        public void cleared_counts_come_from_the_catalog_not_the_save()
        {
            // A save row for a mission the catalog no longer carries must not inflate a tier's
            // count — that would keep a tier open its remaining missions cannot.
            P.SeedForTests("does_not_exist", clears: 5);
            Assert.AreEqual(0, P.ClearedInTier("Beginner"));

            P.SeedForTests(Tier("Beginner")[0].Id, clears: 1);
            Assert.AreEqual(1, P.ClearedInTier("Beginner"));
        }
    }
}
