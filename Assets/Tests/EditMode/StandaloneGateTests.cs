// Assets/Tests/EditMode/StandaloneGateTests.cs
// gps_standalone_shell §7 — EditMode unit tests for the PLAYLIFE shell's reachability gate.
//
// ASSEMBLY: GolfinRedux.Tests.EditMode (asmdef, autoReferenced:false). StandaloneGate, GpsGate,
// AuthGate and ScreenId all live in Assembly-CSharp, which an asmdef cannot reference — so
// everything here goes through System.Reflection, the same pattern as GpsGateTests.
//
// WHY THE TWO-ARG OVERLOADS EXIST AT ALL: StandaloneGate.Enabled is a compile-time const that is
// FALSE in the Editor by design — the inverse of GpsGate, which is true there. Cesar develops the
// GAME daily and a gate that switched itself on would delete Home and every golf screen out from
// under him. That makes the ENABLED branch — the entire behaviour of the shipped shell — the
// unreachable one from the Editor, so IsScreenAllowed(id, standalone) and Rewrite(id, standalone)
// take the build state as a parameter and these tests pass `true` explicitly.

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace GolfinRedux.Tests.EditMode
{
    public class StandaloneGateTests
    {
        const string GateTypeName = "GolfinRedux.UI.StandaloneGate";
        const string GpsGateTypeName = "Golfin.Gps.UI.GpsGate";
        const string AuthGateTypeName = "GolfinRedux.UI.AuthGate";
        const string ScreenIdTypeName = "GolfinRedux.UI.ScreenId";

        /// <summary>The boot + account screens the shell keeps. Mirrors AuthGate's pre-auth list,
        /// and <see cref="ShellPreAuthList_MatchesAuthGatesPreAuthList"/> asserts they agree.</summary>
        static readonly string[] PreAuthScreenNames =
            { "Logo", "Splash", "Loading", "Login", "SignUp", "EmailConfirmation",
              "CreateUsername", "ResetPassword" };

        /// <summary>The PLAYLIFE surface itself — read from GpsGate at runtime in
        /// <see cref="EveryGpsScreen_IsReachableInTheShell"/> rather than duplicated, so a GPS
        /// screen added later is covered here the day it joins that list.</summary>
        static readonly string[] GolfOnlyScreenNames =
            { "Home", "Roster", "Inventory", "HoleSelection", "ModeSelection", "MissionSelection",
              "Leaderboard", "TournamentSelection", "TournamentHoleSelection", "TournamentLeaderboard",
              "StaminaShopSelection", "StaminaShopDetail", "GeneralShop", "GachaHistory",
              "GachaPrizes", "StartingCharacterSelection" };

        Type _gate;
        Type _gpsGate;
        Type _screenId;
        MethodInfo _isScreenAllowed;
        MethodInfo _rewrite;
        MethodInfo _isShellScreen;
        MethodInfo _isGpsScreen;

        [SetUp]
        public void SetUp()
        {
            _gate = FindType(GateTypeName);
            _gpsGate = FindType(GpsGateTypeName);
            _screenId = FindType(ScreenIdTypeName);
            Assert.IsNotNull(_gate, $"{GateTypeName} not found — did Assembly-CSharp compile?");
            Assert.IsNotNull(_gpsGate, $"{GpsGateTypeName} not found.");
            Assert.IsNotNull(_screenId, $"{ScreenIdTypeName} not found.");

            _isScreenAllowed = Method(_gate, "IsScreenAllowed", _screenId, typeof(bool));
            _rewrite = Method(_gate, "Rewrite", _screenId, typeof(bool));
            _isShellScreen = _gate.GetMethod("IsShellScreen", BindingFlags.Static | BindingFlags.Public);
            _isGpsScreen = _gpsGate.GetMethod("IsGpsScreen", BindingFlags.Static | BindingFlags.Public);

            Assert.IsNotNull(_isScreenAllowed, "StandaloneGate.IsScreenAllowed(ScreenId, bool) not found.");
            Assert.IsNotNull(_rewrite, "StandaloneGate.Rewrite(ScreenId, bool) not found.");
            Assert.IsNotNull(_isShellScreen, "StandaloneGate.IsShellScreen(ScreenId) not found.");
            Assert.IsNotNull(_isGpsScreen, "GpsGate.IsGpsScreen(ScreenId) not found.");
        }

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        static MethodInfo Method(Type owner, string name, params Type[] args) =>
            owner.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                            null, args, null);

        object Screen(string name) => Enum.Parse(_screenId, name);
        bool Allowed(string screenName, bool standalone)
            => (bool)_isScreenAllowed.Invoke(null, new[] { Screen(screenName), (object)standalone });
        string Rewritten(string screenName, bool standalone)
            => _rewrite.Invoke(null, new[] { Screen(screenName), (object)standalone }).ToString();
        bool IsShell(string screenName)
            => (bool)_isShellScreen.Invoke(null, new[] { Screen(screenName) });
        bool IsGps(string screenName)
            => (bool)_isGpsScreen.Invoke(null, new[] { Screen(screenName) });

        string[] AllScreenNames() => Enum.GetNames(_screenId);

        // ── The table: every ScreenId, in both builds ────────────────────────────────

        [Test]
        public void GateOff_IsANoOp_ForEveryScreen()
        {
            // The game and the GPS variant must be untouched by this file existing. That is the
            // single most important assertion here: the shell is a third variant, not a change.
            foreach (var name in AllScreenNames())
            {
                Assert.IsTrue(Allowed(name, false), $"{name} must stay reachable when the gate is off.");
                Assert.AreEqual(name, Rewritten(name, false),
                                $"{name} must not be rewritten when the gate is off.");
            }
        }

        [Test]
        public void EveryPreAuthScreen_IsReachableInTheShell()
        {
            foreach (var name in PreAuthScreenNames)
                Assert.IsTrue(Allowed(name, true), $"{name} is part of the shell's account flow.");
        }

        [Test]
        public void EveryGpsScreen_IsReachableInTheShell()
        {
            // Read from GpsGate rather than a second hand-written list: the shell's surface IS the
            // GPS surface, and a GPS screen added later must not need this test edited to be covered.
            var gpsScreens = AllScreenNames().Where(IsGps).ToArray();
            Assert.Greater(gpsScreens.Length, 5, "GpsGate reported almost no GPS screens — reflection is wrong.");

            foreach (var name in gpsScreens)
                Assert.IsTrue(Allowed(name, true), $"{name} is GPS surface and must open in the shell.");
        }

        [Test]
        public void EveryGolfScreen_IsRefusedInTheShell()
        {
            foreach (var name in GolfOnlyScreenNames)
            {
                if (name == "Home") continue;   // rewritten, not refused — see the test below
                Assert.IsFalse(Allowed(name, true), $"{name} is golf content and must not open in the shell.");
            }
        }

        [Test]
        public void EveryScreenIsEitherShellOrRefused_NoThirdState()
        {
            // The deny-by-default property, asserted over the WHOLE enum rather than a sample:
            // a screen added later is refused in the shell until somebody lists it, and this test
            // is what proves the allowlist is actually consulted for every id.
            foreach (var name in AllScreenNames())
            {
                string landed = Rewritten(name, true);
                Assert.AreEqual(IsShell(landed), Allowed(name, true),
                                $"{name} -> {landed}: allowed and shell-membership disagree.");
            }
        }

        // ── Home is REWRITTEN, not refused (§D4) ─────────────────────────────────────

        [Test]
        public void Home_IsRewrittenToTheHub_InTheShell()
        {
            Assert.AreEqual("GpsHub", Rewritten("Home", true));
            Assert.IsTrue(Allowed("Home", true),
                          "Home must be ALLOWED in the shell: it is rewritten to the hub, not refused. " +
                          "Refusing it would strand the Welcome SKIP and every empty-history GoBack.");
        }

        [Test]
        public void Home_IsUntouched_InTheGame()
        {
            Assert.AreEqual("Home", Rewritten("Home", false));
        }

        [Test]
        public void Rewrite_IsIdempotent_AndTouchesNothingElse()
        {
            // GpsHub must not itself rewrite, or Navigate would loop.
            Assert.AreEqual("GpsHub", Rewritten("GpsHub", true));
            Assert.AreEqual("GpsHub", Rewritten(Rewritten("Home", true), true));

            foreach (var name in AllScreenNames().Where(n => n != "Home"))
                Assert.AreEqual(name, Rewritten(name, true), $"{name} must not be rewritten.");
        }

        // ── The two allowlists agree ─────────────────────────────────────────────────

        [Test]
        public void ShellPreAuthList_MatchesAuthGatesPreAuthList()
        {
            // The shell spells its pre-auth list out rather than delegating to AuthGate, because
            // "may this open with no session" and "does this screen exist in this product" are
            // different questions. Different questions, same answer TODAY — and this test is what
            // will say so out loud the day one of them changes.
            var authGate = FindType(AuthGateTypeName);
            Assert.IsNotNull(authGate, $"{AuthGateTypeName} not found.");
            var isPreAuth = authGate.GetMethod("IsPreAuthScreen",
                                               BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(isPreAuth, "AuthGate.IsPreAuthScreen(ScreenId) not found.");

            var authPreAuth = AllScreenNames()
                .Where(n => (bool)isPreAuth.Invoke(null, new[] { Screen(n) }))
                .OrderBy(n => n).ToArray();

            CollectionAssert.AreEqual(PreAuthScreenNames.OrderBy(n => n).ToArray(), authPreAuth,
                "AuthGate's pre-auth list changed. Reconcile StandaloneGate.PreAuthScreens with it " +
                "(a new pre-auth screen is almost certainly part of the shell's account flow too).");

            foreach (var name in authPreAuth)
                Assert.IsTrue(IsShell(name), $"{name} is pre-auth for AuthGate but not on the shell's list.");
        }

        // ── The const itself ─────────────────────────────────────────────────────────

        [Test]
        public void Enabled_IsFalseInTheEditor()
        {
            // The inverse of GpsGate, on purpose: the Editor is the GAME. If this ever flips true
            // in an ordinary Editor session, Home and the whole golf surface vanish from play mode.
            var enabled = _gate.GetField("Enabled", BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(enabled, "StandaloneGate.Enabled not found.");
            Assert.IsFalse((bool)enabled.GetRawConstantValue(),
                "StandaloneGate.Enabled must be false in the Editor — it is gated on GOLFIN_STANDALONE " +
                "alone, with no || UNITY_EDITOR. (If you are running with the define temporarily added " +
                "to the global player defines to test the shell, remove it before committing.)");
        }
    }
}
