// Assets/Tests/EditMode/NavBackMemoryTests.cs
// nav_back_memory — EditMode unit tests for ScreenManager's pillar model, the same-pillar
// history stack, GoBack's fallback chain, and NavigateToPillar (D1).
//
// ASSEMBLY: GolfinRedux.Tests.EditMode (asmdef, overrideReferences:false).
// ScreenManager lives in Assembly-CSharp — accessed via System.Reflection, matching the
// pattern established in GachaStage1Tests.cs / GachaStage2Tests.cs.
//
// Why this is testable at all: ScreenManager's navigation logic is pure C# over ScreenId.
// With no FadeController in the scene, ShowScreen applies instantly, so a bare
// ScreenManager on a throwaway GameObject exercises the real production code path.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class NavBackMemoryTests
    {
        private static readonly Type ScreenManagerType =
            Type.GetType("GolfinRedux.UI.ScreenManager, Assembly-CSharp");

        private static readonly Type ScreenIdType =
            Type.GetType("GolfinRedux.UI.ScreenId, Assembly-CSharp");

        private static readonly Type PillarType =
            Type.GetType("Golfin.UI.PersistentUIManager+Screen, Assembly-CSharp");

        private GameObject _host;
        private object _sm;

        private static object Id(string name)     => Enum.Parse(ScreenIdType, name);
        private static object Pillar(string name) => Enum.Parse(PillarType, name);

        [SetUp]
        public void SetUp()
        {
            Assert.NotNull(ScreenManagerType, "GolfinRedux.UI.ScreenManager not found in Assembly-CSharp");
            Assert.NotNull(ScreenIdType,      "GolfinRedux.UI.ScreenId not found in Assembly-CSharp");
            Assert.NotNull(PillarType,        "Golfin.UI.PersistentUIManager+Screen not found in Assembly-CSharp");

            // HideAndDontSave: an EditMode test runs in whatever scene is open — this keeps the
            // throwaway host out of it so the test never dirties the editor's scene.
            _host = new GameObject("NavBackMemoryTests_ScreenManager")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _sm   = _host.AddComponent(ScreenManagerType);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) UnityEngine.Object.DestroyImmediate(_host);
        }

        // ── Reflection helpers ────────────────────────────────────────────────

        private void Show(string screen, bool instant = true)
            => ScreenManagerType.GetMethod("ShowScreen", BindingFlags.Public | BindingFlags.Instance)
                                .Invoke(_sm, new object[] { Id(screen), instant });

        private bool GoBack(string fallback = null, bool instant = true)
        {
            object fb = fallback == null
                ? null
                : Activator.CreateInstance(typeof(Nullable<>).MakeGenericType(ScreenIdType), Id(fallback));
            return (bool)ScreenManagerType.GetMethod("GoBack", BindingFlags.Public | BindingFlags.Instance)
                                          .Invoke(_sm, new object[] { fb, instant });
        }

        private void NavigateToPillar(string pillar)
            => ScreenManagerType.GetMethod("NavigateToPillar", BindingFlags.Public | BindingFlags.Instance)
                                .Invoke(_sm, new object[] { Pillar(pillar) });

        private string Current()
            => ScreenManagerType.GetProperty("CurrentScreen", BindingFlags.Public | BindingFlags.Instance)
                                .GetValue(_sm).ToString();

        private IList History()
            => (IList)ScreenManagerType
                .GetField("_history", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_sm);

        private static string PillarOf(string screen)
        {
            object p = ScreenManagerType.GetMethod("PillarOf", BindingFlags.Public | BindingFlags.Static)
                                        .Invoke(null, new[] { Id(screen) });
            return p?.ToString();
        }

        private static string RootOf(string pillar)
            => ScreenManagerType.GetMethod("RootOf", BindingFlags.Public | BindingFlags.Static)
                                .Invoke(null, new[] { Pillar(pillar) }).ToString();

        // ── §1 Pillar model ───────────────────────────────────────────────────

        [Test]
        public void PillarOf_MatchesTheSpecTable()
        {
            Assert.AreEqual("Home",       PillarOf("Home"));

            Assert.AreEqual("MainPlay",   PillarOf("ModeSelection"));
            Assert.AreEqual("MainPlay",   PillarOf("HoleSelection"));
            Assert.AreEqual("MainPlay",   PillarOf("MissionSelection"));
            Assert.AreEqual("MainPlay",   PillarOf("TournamentSelection"));
            Assert.AreEqual("MainPlay",   PillarOf("TournamentHoleSelection"));
            Assert.AreEqual("MainPlay",   PillarOf("TournamentLeaderboard"));

            Assert.AreEqual("Characters", PillarOf("Roster"));
            Assert.AreEqual("Characters", PillarOf("StaminaShopSelection"));
            Assert.AreEqual("Characters", PillarOf("StaminaShopDetail"));

            Assert.AreEqual("Inventory",  PillarOf("Inventory"));

            Assert.AreEqual("Gacha",      PillarOf("GeneralShop"));
            Assert.AreEqual("Gacha",      PillarOf("GachaHistory"));
            Assert.AreEqual("Gacha",      PillarOf("GachaPrizes"));
        }

        [Test]
        public void PillarOf_IsNull_ForNonPillarScreens()
        {
            // Leaderboard has no nav slot: it rides the history stack but is never a
            // pillar's remembered screen.
            Assert.IsNull(PillarOf("Leaderboard"));

            foreach (string s in new[] { "Logo", "Splash", "Loading", "Login", "SignUp",
                                         "CreateUsername", "EmailConfirmation", "ResetPassword",
                                         "StartingCharacterSelection" })
                Assert.IsNull(PillarOf(s), $"{s} must not belong to a pillar");
        }

        [Test]
        public void RootOf_MatchesTheSpecTable()
        {
            Assert.AreEqual("Home",          RootOf("Home"));
            Assert.AreEqual("ModeSelection", RootOf("MainPlay"));
            Assert.AreEqual("Roster",        RootOf("Characters"));
            Assert.AreEqual("Inventory",     RootOf("Inventory"));
            Assert.AreEqual("GeneralShop",   RootOf("Gacha"));
        }

        // ── §2 History stack ──────────────────────────────────────────────────

        [Test]
        public void ForwardPushInsideOnePillar_StacksHistory()
        {
            Show("ModeSelection");
            Show("MissionSelection");
            Show("HoleSelection");

            Assert.AreEqual(2, History().Count);
            Assert.AreEqual("ModeSelection",    History()[0].ToString());
            Assert.AreEqual("MissionSelection", History()[1].ToString());
        }

        [Test]
        public void PillarChange_ClearsHistory()
        {
            Show("ModeSelection");
            Show("MissionSelection");
            Assert.AreEqual(1, History().Count);

            Show("Inventory");                 // lateral pillar move
            Assert.AreEqual(0, History().Count);
        }

        [Test]
        public void LeavingTheShell_ClearsHistory()
        {
            Show("ModeSelection");
            Show("HoleSelection");
            Assert.AreEqual(1, History().Count);

            Show("Loading");                   // hard boundary (gameplay hand-off)
            Assert.AreEqual(0, History().Count);
        }

        [Test]
        public void Leaderboard_RidesTheStack_FromAnyPillar()
        {
            Show("HoleSelection");
            Show("Leaderboard");

            Assert.AreEqual(1, History().Count);
            Assert.AreEqual("HoleSelection", History()[0].ToString());

            Assert.IsTrue(GoBack("Home"));
            Assert.AreEqual("HoleSelection", Current());
        }

        /// A15 — 20 forward pushes inside one pillar; the stack never exceeds 16 and GoBack
        /// still returns the most recent entries, newest first.
        [Test]
        public void A15_HistoryCapsAt16_AndPopsNewestFirst()
        {
            Show("ModeSelection");

            var pushed = new List<string>();
            for (int i = 0; i < 20; i++)
            {
                string next = (i % 2 == 0) ? "HoleSelection" : "ModeSelection";
                pushed.Add(Current());          // the screen this push will stack
                Show(next);
                Assert.LessOrEqual(History().Count, 16,
                    $"history exceeded the cap after push {i + 1}");
            }

            Assert.AreEqual(16, History().Count);

            // The 16 survivors are the LAST 16 pushed, in order.
            var expected = pushed.GetRange(pushed.Count - 16, 16);
            for (int i = 0; i < 16; i++)
                Assert.AreEqual(expected[i], History()[i].ToString(), $"history slot {i}");

            // GoBack walks them newest-first.
            for (int i = 15; i >= 0; i--)
            {
                Assert.IsTrue(GoBack("Home"), $"GoBack failed with {i + 1} entries left");
                Assert.AreEqual(expected[i], Current(), $"GoBack step {15 - i}");
            }

            Assert.AreEqual(0, History().Count);
        }

        /// A16 — GoBack skips history entries it cannot land on and continues to the next
        /// valid one. The gate-blocked branch (DemoGate / AuthGate) shares this loop; in
        /// EditMode both gates are constants (GOLFIN_DEMO undefined, AuthGate.HasSession
        /// short-circuits true when !Application.isPlaying), so the reachable skip branch —
        /// an entry equal to the current screen — is what exercises it here.
        [Test]
        public void A16_GoBack_SkipsUnusableEntries_AndLandsOnTheNextValidOne()
        {
            Show("ModeSelection");
            Show("MissionSelection");
            Show("HoleSelection");
            // history: [ModeSelection, MissionSelection], current HoleSelection

            // Seed a stale duplicate of the current screen on top — the shape a re-entry
            // leaves behind. GoBack must step over it rather than no-op.
            History().Add(Id("HoleSelection"));
            Assert.AreEqual(3, History().Count);

            Assert.IsTrue(GoBack("Home"));
            Assert.AreEqual("MissionSelection", Current());
            Assert.AreEqual(1, History().Count);
        }

        // ── §2 GoBack fallback chain ──────────────────────────────────────────

        [Test]
        public void GoBack_EmptyStack_UsesTheSerializedFallback()
        {
            Show("TournamentLeaderboard");     // e.g. arriving from a finished round
            Assert.AreEqual(0, History().Count);

            Assert.IsTrue(GoBack("TournamentSelection"));
            Assert.AreEqual("TournamentSelection", Current());
        }

        [Test]
        public void GoBack_EmptyStack_NoFallback_UsesThePillarRoot()
        {
            Show("StaminaShopDetail");
            Assert.IsTrue(GoBack());
            Assert.AreEqual("Roster", Current());   // RootOf(Characters)
        }

        [Test]
        public void GoBack_OnAPillarRoot_FallsBackToHome()
        {
            Show("GeneralShop");               // the Gacha root
            Assert.IsTrue(GoBack());
            Assert.AreEqual("Home", Current());
        }

        [Test]
        public void GoBack_OnHome_IsANoOp_AndNeverQuits()
        {
            Show("Home");
            Assert.IsFalse(GoBack(), "GoBack on the Home root must report 'nowhere to go'");
            Assert.AreEqual("Home", Current());
        }

        // ── §4 / D1 Nav bar ───────────────────────────────────────────────────

        [Test]
        public void D1_NavSlotOfTheCurrentPillar_GoesToItsRoot()
        {
            Show("ModeSelection");
            Show("HoleSelection");

            NavigateToPillar("MainPlay");
            Assert.AreEqual("ModeSelection", Current());
            Assert.AreEqual(0, History().Count, "a nav-slot jump is never a forward push");
        }

        [Test]
        public void D1_NavSlotOfAnotherPillar_ReopensItsLastScreen()
        {
            Show("ModeSelection");
            Show("HoleSelection");             // deepest MainPlay screen

            NavigateToPillar("Inventory");
            Assert.AreEqual("Inventory", Current());

            NavigateToPillar("MainPlay");
            Assert.AreEqual("HoleSelection", Current(), "MainPlay must reopen where the player left it");
        }

        [Test]
        public void D1_NavSlotOfAnUnvisitedPillar_OpensItsRoot()
        {
            Show("Home");
            NavigateToPillar("Gacha");
            Assert.AreEqual("GeneralShop", Current());
        }

        [Test]
        public void PillarMemory_SurvivesLeavingTheShell()
        {
            Show("ModeSelection");
            Show("HoleSelection");
            Show("Loading");                   // gameplay hand-off
            Show("Home");                      // QUIT lands on Home (D2)

            NavigateToPillar("MainPlay");
            Assert.AreEqual("HoleSelection", Current());
        }

        [Test]
        public void Leaderboard_IsNeverAPillarsRememberedScreen()
        {
            Show("Home");
            Show("Leaderboard");
            NavigateToPillar("Inventory");

            NavigateToPillar("Home");
            Assert.AreEqual("Home", Current(), "the Home slot must not reopen the Leaderboard");
        }
    }
}
