using System.Collections.Generic;
using Golfin.Gameplay.Missions;
using Golfin.Gameplay.Session;
using NUnit.Framework;
using UnityEngine;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// <see cref="MissionSession"/> — the state it takes over and, more importantly, the state
    /// it gives back (missions_v1 §B2/§B3).
    ///
    /// THE CENTRAL PROPERTY IS ISOLATION. A mission overrides the bag, the stroke cap and the
    /// stamina cost; every one of those is shared with Practice, 1v1 and tournaments. A mission
    /// that leaked would not fail loudly — the next Practice round would just quietly be played
    /// with a mission's supplied clubs, capped at a mission's stroke limit, and the player
    /// would have no way to know why. So "nothing survives" is asserted directly, from both the
    /// clean exit (End) and the teardown (ResetSession), rather than inferred from Begin.
    /// </summary>
    public class MissionSessionTests
    {
        [SetUp]
        public void SetUp()
        {
            MissionSession.Clear();
            GameSession.ResetForNewHole();
        }

        [TearDown]
        public void TearDown() => MissionSession.Clear();

        private static MissionDefinition Valid(params string[] clubs)
        {
            var m = new MissionDefinition
            {
                Id = "7", HoleNumber = 1, Par = 4,
                StartAreaId = "TEE_BACK", StartKind = "tee", TeeLabel = "back",
                WindSpeedMph = 12f, WindRelDirDeg = 180f,
                StaminaDrain = 8f,
            };
            m.ClubIds.AddRange(clubs.Length > 0 ? clubs : new[] { "club_driver_bogeyb_common", "club_putter_bogeyb_common" });
            m.Goals.Add(new MissionGoal(MissionGoalType.SCORE, "0"));
            return m;
        }

        // ── Begin refuses what it cannot honour ─────────────────────────────────

        [Test]
        public void begin_refuses_an_empty_bag_and_changes_nothing()
        {
            var m = Valid();
            m.ClubIds.Clear();
            LogAssert_ExpectError();
            Assert.IsFalse(MissionSession.Begin(m));
            Assert.IsFalse(MissionSession.IsActive);
            Assert.IsFalse(MissionSessionBag.IsActive, "a refused Begin must not push a bag");
            Assert.IsFalse(GameSession.StrokeCapEnabled, "a refused Begin must not arm the cap");
        }

        [Test]
        public void begin_refuses_a_short_start_with_no_baked_coordinates()
        {
            // The Phase A state of every short area, and the reason the publish validator
            // refuses a mission that names one: a ball with nowhere to go.
            var m = Valid();
            m.StartKind = "short";
            m.StartAreaId = "GREEN";
            m.StartWorld = null;
            LogAssert_ExpectError();
            Assert.IsFalse(MissionSession.Begin(m));
            Assert.IsFalse(MissionSession.IsActive);
            Assert.IsFalse(MissionSessionBag.IsActive);
        }

        private static void LogAssert_ExpectError()
            => UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

        // ── The bag ─────────────────────────────────────────────────────────────

        [Test]
        public void begin_pushes_the_bag_and_end_pops_it()
        {
            Assert.IsNull(MissionSessionBag.Current, "no bag before a mission");
            Assert.IsTrue(MissionSession.Begin(Valid("a", "b")));
            Assert.AreEqual(2, MissionSessionBag.Current.Count);
            Assert.AreEqual(1, MissionSessionBag.Depth);

            MissionSession.End();
            Assert.IsNull(MissionSessionBag.Current, "the player's own bag is back");
            Assert.AreEqual(0, MissionSessionBag.Depth, "the push/pop stack is balanced");
        }

        [Test]
        public void beginning_a_second_mission_does_not_stack_two_bags()
        {
            // Begin ends any mission left standing, so a screen that starts a mission twice
            // leaves one bag pushed, not two.
            MissionSession.Begin(Valid("a"));
            MissionSession.Begin(Valid("b", "c"));
            Assert.AreEqual(1, MissionSessionBag.Depth);
            Assert.AreEqual(2, MissionSessionBag.Current.Count);
        }

        // ── The stroke cap ──────────────────────────────────────────────────────

        [Test]
        public void the_cap_is_the_TIGHTEST_goal_on_the_card()
        {
            var m = Valid();
            m.Goals.Clear();
            m.Goals.Add(new MissionGoal(MissionGoalType.SCORE, "1"));    // par 4 -> 5 strokes
            m.Goals.Add(new MissionGoal(MissionGoalType.SHOTS, "3"));    // par 4 -> -1 over par
            MissionSession.Begin(m);
            Assert.IsTrue(GameSession.StrokeCapEnabled);
            Assert.AreEqual(-1, GameSession.StrokeCapOverPar, "SHOTS 3 on a par 4 is the tighter of the two");
        }

        [Test]
        public void a_mission_with_no_stroke_goal_does_not_arm_the_cap()
        {
            var m = Valid();
            m.Goals.Clear();
            m.Goals.Add(new MissionGoal(MissionGoalType.NO_HAZARD, ""));
            MissionSession.Begin(m);
            Assert.IsFalse(GameSession.StrokeCapEnabled,
                "a NO_HAZARD mission can be failed on shot one and still be worth finishing");
        }

        // ── Isolation: nothing survives ─────────────────────────────────────────

        [Test]
        public void end_restores_the_stroke_cap()
        {
            MissionSession.Begin(Valid());
            Assert.IsTrue(GameSession.StrokeCapEnabled);
            MissionSession.End();
            Assert.IsFalse(GameSession.StrokeCapEnabled);
            Assert.AreEqual(0, GameSession.StrokeCapOverPar);
        }

        [Test]
        public void ResetSession_clears_a_mission_that_was_left_running()
        {
            // The back-to-Home path. GameSession cannot reference Missions (Missions references
            // GameSession), so this goes through the OnSessionReset event — and this test is
            // what proves the wiring is actually connected, not merely written.
            MissionSession.Begin(Valid());
            Assert.IsTrue(MissionSession.IsActive);

            GameSession.ResetSession();

            Assert.IsFalse(MissionSession.IsActive, "a mission must not survive a teardown");
            Assert.IsFalse(MissionSessionBag.IsActive, "nor may its bag");
            Assert.IsFalse(GameSession.StrokeCapEnabled, "nor its stroke cap");
        }

        [Test]
        public void practice_and_versus_and_tournaments_are_never_in_a_mission()
        {
            // §B2: "Practice / 1v1 / tournaments never enter MissionSession." There is nothing
            // to disable, because nothing runs unless Begin was called — and only the Mission
            // Selection screen calls it. This pins that every override is inert by default.
            GameSession.ResetSession();
            GameSession.IsVersus = true;
            Assert.IsFalse(MissionSession.IsActive);
            Assert.IsNull(MissionSessionBag.Current, "the 1v1 bag is the player's own");
            Assert.AreEqual(8f, MissionSession.DrainOverride(8f), "1v1 pays the configured drain");

            GameSession.IsVersus = false;
            GameSession.IsTournament = true;
            Assert.IsFalse(MissionSession.IsActive);
            Assert.AreEqual(8f, MissionSession.DrainOverride(8f), "a tournament pays the configured drain");
        }

        // ── Stamina ─────────────────────────────────────────────────────────────

        [Test]
        public void the_drain_override_is_the_missions_own_cost()
        {
            var m = Valid();
            m.StaminaDrain = 3f;      // a short-game mission
            MissionSession.Begin(m);
            Assert.AreEqual(3f, MissionSession.DrainOverride(8f));
            MissionSession.End();
            Assert.AreEqual(8f, MissionSession.DrainOverride(8f), "the configured drain is back");
        }

        // ── Wind ────────────────────────────────────────────────────────────────

        [Test]
        public void gusty_rerolls_between_shots_and_stays_in_band()
        {
            var m = Valid();
            m.WindGusty = true;
            MissionSession.Begin(m);

            var seen = new HashSet<float>();
            for (int shot = 0; shot < 12; shot++)
            {
                Assert.IsTrue(MissionSession.TryAdvanceGust(shot, out float mph));
                Assert.GreaterOrEqual(mph, 6f, "gusts stay in the 6-18 mph band");
                Assert.LessOrEqual(mph, 18f);
                seen.Add(mph);
            }
            Assert.Greater(seen.Count, 1, "a gust that never changes is not a gust");
        }

        [Test]
        public void gusts_are_deterministic_so_a_replay_is_the_same_round()
        {
            // A wind re-rolled from UnityEngine.Random would make the same mission a different
            // difficulty every attempt, which is not what "hard" should mean.
            var m = Valid();
            m.WindGusty = true;
            MissionSession.Begin(m);
            float first = MissionSession.RollGust(3);
            MissionSession.End();

            MissionSession.Begin(Valid());   // same id, so the same seed
            Assert.AreEqual(first, MissionSession.RollGust(3), 0.0001f);
        }

        [Test]
        public void a_non_gusty_preset_holds_its_speed()
        {
            MissionSession.Begin(Valid());   // HEAD_S-shaped: 12 mph, not gusty
            Assert.IsFalse(MissionSession.TryAdvanceGust(1, out float mph));
            Assert.AreEqual(12f, mph);
        }

        // ── The definition's own arithmetic ─────────────────────────────────────

        [Test]
        public void stroke_cap_conversions_are_relative_to_par()
        {
            var shots = new MissionGoal(MissionGoalType.SHOTS, "3");
            Assert.AreEqual(-1, shots.ImpliedStrokeCapOverPar(4), "3 shots on a par 4 is one under");
            Assert.AreEqual(0, shots.ImpliedStrokeCapOverPar(3), "3 shots on a par 3 is level");

            var score = new MissionGoal(MissionGoalType.SCORE, "1");
            Assert.AreEqual(1, score.ImpliedStrokeCapOverPar(4), "SCORE is already relative to par");

            Assert.IsNull(new MissionGoal(MissionGoalType.NO_HAZARD, "").ImpliedStrokeCapOverPar(4));
        }

        [Test]
        public void the_score_goal_text_key_handles_negative_params()
        {
            // "GOAL_SCORE_-1" is not a valid localization key; the minus becomes M.
            Assert.AreEqual("GOAL_SCORE_M1", new MissionGoal(MissionGoalType.SCORE, "-1").TextKey);
            Assert.AreEqual("GOAL_SCORE_0", new MissionGoal(MissionGoalType.SCORE, "0").TextKey);
            Assert.AreEqual("GOAL_NO_HAZARD", new MissionGoal(MissionGoalType.NO_HAZARD, "").TextKey);
        }
    }
}
