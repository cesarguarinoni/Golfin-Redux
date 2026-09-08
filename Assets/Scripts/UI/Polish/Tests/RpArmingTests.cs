// ─────────────────────────────────────────────────────────────────────────────
// game_polish_b §D3 / A5 — the completeness claim, made checkable.
//
// §D3 names seven screens to arm the count-up from. This task armed the two MANAGER methods
// instead (deviation D-4), on the argument that "the player caused it" is a property of
// RewardPointsManager, not a list of screens — and that a list of screens is exactly the shape
// that goes stale when the eighth one is written.
//
// That argument is only worth anything if the set of mutators stays closed. So this suite pins
// it: these five methods are the complete set of ways an RP balance moves, two of them arm and
// three deliberately do not. Add a sixth and this suite fails, which is the whole point — the
// failure is the question "should this one arm?" being asked at the moment someone adds it,
// rather than a year later when a spend is noticed snapping.
//
// It is a REFLECTION test over the shipping type (a named test assembly cannot reference
// Assembly-CSharp), the same pattern the other suites here use.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace Golfin.UI.Polish.Tests
{
    [TestFixture]
    public class RpArmingTests
    {
        static Type Rpm => Probe.Type("Golfin.Roster.RewardPointsManager");
        static Type Pum => Probe.Type("Golfin.UI.PersistentUIManager");
        static Type Gtm => Probe.Type("GolfinRedux.UI.Gacha.GachaTicketManager");

        const BindingFlags Pub = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        /// <summary>Every public instance method on the manager that CHANGES the balance.
        /// Readers (GetPoints, CanAfford) and the leaderboard accessors are not mutators.</summary>
        static readonly string[] ExpectedRpMutators =
        {
            "SpendPoints",          // player: a level-up, a purchase, an entry fee  — ARMS
            "EarnPoints",           // player: a reward, a prize                     — ARMS
            "EarnPointsLocalOnly",  // dev grant, flag-OFF only                      — does not arm
            "SetPoints",            // dev / local override                          — does not arm
            "ApplyServerBalance",   // the server's number, a refresh                — does not arm
        };

        static HashSet<string> DeclaredMutators()
        {
            var found = new HashSet<string>();
            foreach (MethodInfo m in Rpm.GetMethods(Pub))
            {
                if (m.IsSpecialName) continue;                        // property accessors
                string n = m.Name;
                bool mutates = n.Contains("Points") || n.Contains("Balance");
                if (!mutates) continue;
                if (n.StartsWith("Get", StringComparison.Ordinal)) continue;
                if (n.StartsWith("Can", StringComparison.Ordinal)) continue;
                found.Add(n);
            }
            return found;
        }

        [Test]
        public void TheSetOfRpMutators_IsStillClosed()
        {
            HashSet<string> found = DeclaredMutators();
            var unexpected = new List<string>(found);
            unexpected.RemoveAll(n => Array.IndexOf(ExpectedRpMutators, n) >= 0);
            var missing = new List<string>();
            foreach (string n in ExpectedRpMutators) if (!found.Contains(n)) missing.Add(n);

            CollectionAssert.IsEmpty(unexpected,
                "a NEW way to move the RP balance appeared: " + string.Join(", ", unexpected)
                + ". Decide whether it is player-caused (it should call ArmTopBarCountUp, like "
                + "SpendPoints and EarnPoints) or server-driven (it should not, like "
                + "ApplyServerBalance), then add it to ExpectedRpMutators. §D3/A5.");
            CollectionAssert.IsEmpty(missing,
                "an RP mutator this task reasoned about is GONE: " + string.Join(", ", missing)
                + " — §D3's arming was placed on the assumption it exists.");
        }

        [Test]
        public void TheArmHelper_IsOnTheManager_NotOnAListOfScreens()
        {
            MethodInfo? arm = Rpm.GetMethod("ArmTopBarCountUp",
                                            BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(arm,
                "RewardPointsManager.ArmTopBarCountUp is gone. §D3 put the arm on the manager "
                + "precisely so the eight call sites could not drift out of sync with a list.");
        }

        [Test]
        public void ThePublicArm_StillExists_ForCallersThatNeedIt()
        {
            Assert.NotNull(Pum.GetMethod("ArmRewardPointsCountUp", BindingFlags.Public | BindingFlags.Instance),
                "PersistentUIManager.ArmRewardPointsCountUp is gone — GPS arms through it too");
        }

        // ── tickets ──────────────────────────────────────────────────────────

        [Test]
        public void TheTicketPill_HasItsOwnArm()
        {
            // Tickets could not ride the RP arm: they go DOWN only through SetFromServer, which is
            // also how a background refresh arrives. A separate arm is what lets the pull say
            // "this one was the player" without the refresh saying it too.
            Assert.NotNull(Pum.GetMethod("ArmTicketCountUp", BindingFlags.Public | BindingFlags.Instance),
                "PersistentUIManager.ArmTicketCountUp is gone — the ticket pill snaps again (§D3)");
        }

        [Test]
        public void TheTicketMutators_AreStillTheTwoThisTaskReasonedAbout()
        {
            var found = new HashSet<string>();
            foreach (MethodInfo m in Gtm.GetMethods(Pub))
            {
                if (m.IsSpecialName) continue;
                if (m.Name is "AddTickets" or "SetFromServer") found.Add(m.Name);
            }
            CollectionAssert.AreEquivalent(new[] { "AddTickets", "SetFromServer" }, found,
                "GachaTicketManager's mutation surface changed. AddTickets arms the count-up "
                + "(a grant is player-caused); SetFromServer does NOT, because it is also how a "
                + "background refresh lands — GachaPullFlow arms around it instead, which is what "
                + "keeps a pull apart from another device's pull. Re-decide before editing this.");
        }
    }
}
