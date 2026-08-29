using System.Collections.Generic;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.Missions;
using Golfin.Gameplay.Session;
using NUnit.Framework;
using UnityEngine;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// The mission goal evaluator, one pass-and-fail pair per goal type (missions_v1 §B4).
    ///
    /// WHY EVERY TYPE GETS BOTH CASES. A goal that never returns true is a mission nobody can
    /// clear; a goal that never returns false is a mission everybody clears. Both are invisible
    /// in play — the player just sees a tick or a cross and has no way to know it was wrong —
    /// and both cost real RP, because `goals_met` is what the claim is paid on. So neither
    /// direction is assumed from the other.
    ///
    /// The shots are SYNTHETIC `ShotRecord`s pushed through `GameSession.RecordShot`, which is
    /// the same path the real game writes. That is deliberate: the evaluator reads
    /// `GameSession.ShotHistory` precisely so that a goal can never disagree with the
    /// scorecard, and a test that bypassed it would not be testing that.
    /// </summary>
    public class MissionGoalEvaluatorTests
    {
        private static readonly Vector3 Pin = new Vector3(100f, 0f, 0f);

        [SetUp]
        public void SetUp() => GameSession.ResetForNewHole();

        [TearDown]
        public void TearDown()
        {
            MissionSession.Clear();
            GameSession.ResetForNewHole();
        }

        // ── Fixtures ────────────────────────────────────────────────────────────

        private static MissionDefinition Mission(MissionGoalType type, string param,
                                                 int par = 4, string startKind = "tee")
        {
            var m = new MissionDefinition { Id = "test", HoleNumber = 1, Par = par, StartKind = startKind };
            m.Goals.Add(new MissionGoal(type, param));
            m.ClubIds.Add("club_driver_bogeyb_common");
            return m;
        }

        private static void Shot(string club, string surface, string terminal = "AtRest",
                                 string obReason = null, float distanceM = 100f,
                                 Vector3 final = default, int penalty = 0)
        {
            int n = GameSession.ShotHistory.Count + 1;
            GameSession.RecordShot(new ShotRecord(
                n, club, Vector3.zero, final, distanceM, terminal, obReason, surface, penalty));
        }

        private static MissionResult Evaluate(MissionDefinition m, int strokes,
                                              BallState terminal = BallState.InCup)
        {
            var ev = new MissionGoalEvaluator(m, Pin);
            return ev.EvaluateFinal(new HoleCompletionData(terminal, strokes, 0, 1), "guid");
        }

        private static void AssertGoal(MissionDefinition m, int strokes, bool expected,
                                       BallState terminal = BallState.InCup)
        {
            var r = Evaluate(m, strokes, terminal);
            Assert.AreEqual(expected, r.Goals[0].Met,
                $"{m.Goals[0].Type} '{m.Goals[0].Param}' with {strokes} strokes");
        }

        // ── SCORE / SHOTS / PUTTS ───────────────────────────────────────────────

        [Test]
        public void SCORE_met_at_or_under_target()
        {
            Shot("Driver", "Fairway");
            AssertGoal(Mission(MissionGoalType.SCORE, "0"), strokes: 4, expected: true);   // par
            GameSession.ResetForNewHole(); Shot("Driver", "Fairway");
            AssertGoal(Mission(MissionGoalType.SCORE, "0"), strokes: 3, expected: true);   // birdie beats par
        }

        [Test]
        public void SCORE_failed_one_over()
        {
            Shot("Driver", "Fairway");
            AssertGoal(Mission(MissionGoalType.SCORE, "0"), strokes: 5, expected: false);
        }

        [Test]
        public void SHOTS_met_and_failed()
        {
            Shot("Putter", "Green");
            AssertGoal(Mission(MissionGoalType.SHOTS, "3"), strokes: 3, expected: true);
            AssertGoal(Mission(MissionGoalType.SHOTS, "3"), strokes: 4, expected: false);
        }

        [Test]
        public void PUTTS_counts_only_putter_shots()
        {
            Shot("Driver", "Fairway");
            Shot("Iron 7", "Green");
            Shot("Putter", "Green");
            Shot("Putter", "Green", terminal: "InCup");
            AssertGoal(Mission(MissionGoalType.PUTTS, "2"), strokes: 4, expected: true);
            AssertGoal(Mission(MissionGoalType.PUTTS, "1"), strokes: 4, expected: false);
        }

        // ── NO_HAZARD ───────────────────────────────────────────────────────────

        [Test]
        public void NO_HAZARD_met_on_a_clean_round()
        {
            Shot("Driver", "Fairway");
            Shot("Iron 7", "Green");
            AssertGoal(Mission(MissionGoalType.NO_HAZARD, ""), strokes: 3, expected: true);
        }

        [Test]
        public void NO_HAZARD_failed_by_an_OB_shot()
        {
            Shot("Driver", "Water", terminal: "OB", obReason: "Water", penalty: 1);
            AssertGoal(Mission(MissionGoalType.NO_HAZARD, ""), strokes: 4, expected: false);
        }

        [Test]
        public void NO_HAZARD_fails_PROGRESSIVELY_before_the_hole_ends()
        {
            // The whole point of the early verdict: the player is told the mission is gone
            // when it goes, not four shots later.
            var m = Mission(MissionGoalType.NO_HAZARD, "");
            var ev = new MissionGoalEvaluator(m, Pin);
            ev.Attach();
            Shot("Driver", "Fairway");
            Assert.IsNull(m.Goals[0].Met, "still reachable after a clean shot");
            Shot("Iron 7", "Water", terminal: "OB", obReason: "Water", penalty: 1);
            Assert.AreEqual(false, m.Goals[0].Met, "must fail the moment the ball is in the water");
            ev.Detach();
        }

        [Test]
        public void progressive_never_marks_a_goal_MET()
        {
            // "No hazard so far" is not "no hazard". A goal that flickered to met and back
            // would be worse than one that stayed undecided.
            var m = Mission(MissionGoalType.NO_HAZARD, "");
            var ev = new MissionGoalEvaluator(m, Pin);
            ev.Attach();
            Shot("Driver", "Fairway");
            Shot("Iron 7", "Green");
            Assert.IsNull(m.Goals[0].Met);
            ev.Detach();
        }

        // ── Surface goals ───────────────────────────────────────────────────────

        [Test]
        public void AVOID_met_and_failed()
        {
            Shot("Driver", "Fairway");
            AssertGoal(Mission(MissionGoalType.AVOID, "Rough"), strokes: 4, expected: true);
            GameSession.ResetForNewHole();
            Shot("Driver", "Rough");
            AssertGoal(Mission(MissionGoalType.AVOID, "Rough"), strokes: 4, expected: false);
        }

        [Test]
        public void AVOID_Bunker_matches_the_Sand_surface_type()
        {
            // The design writes "Bunker"; SurfaceType says "Sand". They have to meet somewhere.
            Shot("Driver", "Sand");
            AssertGoal(Mission(MissionGoalType.AVOID, "Bunker"), strokes: 4, expected: false);
        }

        [Test]
        public void AVOID_Rough_and_Semi_covers_both()
        {
            Shot("Driver", "Semirough");
            AssertGoal(Mission(MissionGoalType.AVOID, "Rough&Semi"), strokes: 4, expected: false);
            GameSession.ResetForNewHole();
            Shot("Driver", "Fairway");
            AssertGoal(Mission(MissionGoalType.AVOID, "Rough&Semi"), strokes: 4, expected: true);
        }

        [Test]
        public void LAND_TEE_looks_at_the_FIRST_shot_only()
        {
            Shot("Driver", "Rough");     // tee shot missed
            Shot("Iron 7", "Fairway");   // a later shot on the fairway must not rescue it
            AssertGoal(Mission(MissionGoalType.LAND_TEE, "Fairway"), strokes: 4, expected: false);
            GameSession.ResetForNewHole();
            Shot("Driver", "Fairway");
            AssertGoal(Mission(MissionGoalType.LAND_TEE, "Fairway"), strokes: 4, expected: true);
        }

        [Test]
        public void LAND_ANY_takes_any_shot()
        {
            Shot("Driver", "Rough");
            Shot("Iron 7", "Green");
            AssertGoal(Mission(MissionGoalType.LAND_ANY, "Green"), strokes: 4, expected: true);
            GameSession.ResetForNewHole();
            Shot("Driver", "Rough");
            AssertGoal(Mission(MissionGoalType.LAND_ANY, "Green"), strokes: 4, expected: false);
        }

        // ── GIR ─────────────────────────────────────────────────────────────────

        [Test]
        public void GIR_met_when_the_regulation_shot_finds_the_green()
        {
            Shot("Driver", "Fairway");
            Shot("Iron 7", "Green");     // par 4 -> regulation is shot 2
            AssertGoal(Mission(MissionGoalType.GIR, "", par: 4), strokes: 4, expected: true);
        }

        [Test]
        public void GIR_failed_when_it_does_not()
        {
            Shot("Driver", "Fairway");
            Shot("Iron 7", "Rough");
            Shot("Wedge", "Green");      // on in 3 is not GIR on a par 4
            AssertGoal(Mission(MissionGoalType.GIR, "", par: 4), strokes: 4, expected: false);
        }

        // ── Distance goals ──────────────────────────────────────────────────────

        [Test]
        public void DIST_compares_in_YARDS_not_metres()
        {
            // 150 m is 164 yd — comfortably over a 150 YARD goal. A metre/yard mix-up here
            // would silently make every distance goal 9 % harder than it reads.
            Shot("Driver", "Fairway", distanceM: 150f);
            AssertGoal(Mission(MissionGoalType.DIST, "150"), strokes: 4, expected: true);
            GameSession.ResetForNewHole();
            Shot("Driver", "Fairway", distanceM: 130f);   // 142 yd
            AssertGoal(Mission(MissionGoalType.DIST, "150"), strokes: 4, expected: false);
        }

        [Test]
        public void NEAR_PIN_measures_the_FIRST_shot_to_reach_the_green()
        {
            // 9 m from the pin is ~9.8 yd — inside a 10 yd goal.
            Shot("Driver", "Fairway");
            Shot("Iron 7", "Green", final: new Vector3(91f, 0f, 0f));
            AssertGoal(Mission(MissionGoalType.NEAR_PIN, "10"), strokes: 4, expected: true);

            GameSession.ResetForNewHole();
            Shot("Driver", "Fairway");
            Shot("Iron 7", "Green", final: new Vector3(70f, 0f, 0f));   // 30 m ~ 33 yd
            Shot("Putter", "Green", final: new Vector3(99f, 0f, 0f));   // a good putt does not count
            AssertGoal(Mission(MissionGoalType.NEAR_PIN, "10"), strokes: 4, expected: false);
        }

        // ── Club goals ──────────────────────────────────────────────────────────

        [Test]
        public void USE_CLUB_checks_the_tee_shot()
        {
            Shot("Driver", "Fairway");
            AssertGoal(Mission(MissionGoalType.USE_CLUB, "Driver"), strokes: 4, expected: true);
            GameSession.ResetForNewHole();
            Shot("Iron 7", "Fairway");
            AssertGoal(Mission(MissionGoalType.USE_CLUB, "Driver"), strokes: 4, expected: false);
        }

        [Test]
        public void AVOID_CLUB_checks_every_shot()
        {
            Shot("Iron 7", "Fairway");
            Shot("Putter", "Green", terminal: "InCup");
            AssertGoal(Mission(MissionGoalType.AVOID_CLUB, "Driver"), strokes: 2, expected: true);
            GameSession.ResetForNewHole();
            Shot("Iron 7", "Fairway");
            Shot("Driver", "Green");
            AssertGoal(Mission(MissionGoalType.AVOID_CLUB, "Driver"), strokes: 2, expected: false);
        }

        [Test]
        public void AVOID_CLUB_matches_a_wedge_abbreviation_to_its_label()
        {
            // The goal param is a catalog TYPE ("SW"); the shot record is display text.
            Shot("Sand Wedge", "Green");
            AssertGoal(Mission(MissionGoalType.AVOID_CLUB, "SW"), strokes: 2, expected: false);
        }

        // ── UP_DOWN ─────────────────────────────────────────────────────────────

        [Test]
        public void UP_DOWN_needs_a_short_start_and_two_strokes()
        {
            Shot("Wedge", "Green");
            Shot("Putter", "Green", terminal: "InCup");
            AssertGoal(Mission(MissionGoalType.UP_DOWN, "", startKind: "short"), strokes: 2, expected: true);
            AssertGoal(Mission(MissionGoalType.UP_DOWN, "", startKind: "short"), strokes: 3, expected: false);
            // From a TEE start "up and down" is not a thing that happened.
            AssertGoal(Mission(MissionGoalType.UP_DOWN, "", startKind: "tee"), strokes: 2, expected: false);
        }

        // ── The clear rule ──────────────────────────────────────────────────────

        [Test]
        public void one_missed_goal_fails_the_mission()
        {
            var m = new MissionDefinition { Id = "t", Par = 4, StartKind = "tee" };
            m.ClubIds.Add("c");
            m.Goals.Add(new MissionGoal(MissionGoalType.SCORE, "0"));
            m.Goals.Add(new MissionGoal(MissionGoalType.NO_HAZARD, ""));
            Shot("Driver", "Water", terminal: "OB", obReason: "Water", penalty: 1);

            var r = Evaluate(m, strokes: 4);
            Assert.AreEqual(true, r.Goals[0].Met, "score was made");
            Assert.AreEqual(false, r.Goals[1].Met, "hazard was hit");
            Assert.IsFalse(r.Cleared, "no partial credit — one missed goal fails the mission");
        }

        [Test]
        public void a_hole_that_ended_on_the_stroke_cap_is_never_cleared()
        {
            // The ball is not in the cup, so there is no score, whatever the goal list says.
            var m = Mission(MissionGoalType.NO_HAZARD, "");
            Shot("Driver", "Fairway");
            var r = Evaluate(m, strokes: 9, terminal: BallState.AtRest);
            Assert.IsTrue(r.FailedOnStrokeCap);
            Assert.IsFalse(r.Cleared);
        }

        [Test]
        public void every_goal_gets_an_explicit_verdict()
        {
            // A null verdict would reach the claim as "not met" anyway — silently — and the
            // modal would draw a blank where a cross belongs.
            var m = new MissionDefinition { Id = "t", Par = 4, StartKind = "tee" };
            m.ClubIds.Add("c");
            foreach (MissionGoalType type in System.Enum.GetValues(typeof(MissionGoalType)))
            {
                if (type == MissionGoalType.None) continue;
                m.Goals.Add(new MissionGoal(type, "2"));
            }
            Shot("Driver", "Fairway");
            var r = Evaluate(m, strokes: 4);
            foreach (var goal in r.Goals)
                Assert.IsNotNull(goal.Met, $"{goal.Type} was left undecided");
        }

        // ── The idempotency key ─────────────────────────────────────────────────

        [Test]
        public void the_claim_key_is_a_uuid()
        {
            // This test used to assert "mission:test:guid" — and that string is exactly why no
            // mission ever paid out. The server casts idempotency_key straight to a uuid and
            // 400s anything else, so the test was pinning the bug in place. What matters is the
            // CONTRACT, not the spelling.
            Shot("Driver", "Fairway");
            var r = Evaluate(Mission(MissionGoalType.SCORE, "0"), strokes: 4);
            Assert.IsTrue(System.Guid.TryParse(r.IdempotencyKey, out _),
                $"the claim key must parse as a UUID, got '{r.IdempotencyKey}'");
        }

        [Test]
        public void the_claim_key_is_stable_for_one_mission_and_session()
        {
            // A retry has to be the SAME claim, which is the whole point of an idempotency key.
            Shot("Driver", "Fairway");
            var a = Evaluate(Mission(MissionGoalType.SCORE, "0"), strokes: 4);
            Shot("Driver", "Fairway");
            var b = Evaluate(Mission(MissionGoalType.SCORE, "0"), strokes: 4);
            Assert.AreEqual(a.IdempotencyKey, b.IdempotencyKey);
        }

        [Test]
        public void a_different_session_gets_a_different_claim_key()
        {
            // ...and two attempts must never collide onto one key, or the second would be
            // swallowed by the server as a duplicate of the first. Driven through EvaluateFinal
            // rather than the hash helper, so it is the real path that is under test.
            var m = Mission(MissionGoalType.SCORE, "0");
            Shot("Driver", "Fairway");
            var a = new MissionGoalEvaluator(m, Pin)
                .EvaluateFinal(new HoleCompletionData(BallState.InCup, 4, 0, 1), "session-a");
            var b = new MissionGoalEvaluator(m, Pin)
                .EvaluateFinal(new HoleCompletionData(BallState.InCup, 4, 0, 1), "session-b");
            Assert.AreNotEqual(a.IdempotencyKey, b.IdempotencyKey);
        }
    }
}
