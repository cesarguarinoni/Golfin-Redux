// Assets/Tests/EditMode/GpsGateTests.cs
// punch_it_gps_variants — EditMode unit tests for the GPS reachability gate.
//
// ASSEMBLY: GolfinRedux.Tests.EditMode (asmdef, autoReferenced:false). GpsGate and ScreenId live
// in Assembly-CSharp, which an asmdef cannot reference — so everything here goes through
// System.Reflection, the same pattern as NavBackMemoryTests / GachaStage1Tests.
//
// WHY THE TWO-ARG OVERLOAD EXISTS AT ALL: GpsGate.Enabled is `true` under UNITY_EDITOR by design
// (Cesar develops GPS daily and the surface must not depend on the active build profile). That
// makes the disabled branch — the entire behaviour of a "punch it" player build — unreachable
// from the Editor. IsScreenAllowed(id, gpsEnabled) takes the build state as a parameter so these
// tests can assert what the SHIPPED non-GPS build does, which is the half that actually matters.

using System;
using System.Reflection;
using NUnit.Framework;

namespace GolfinRedux.Tests.EditMode
{
    public class GpsGateTests
    {
        const string GateTypeName = "Golfin.Gps.UI.GpsGate";
        const string ScreenIdTypeName = "GolfinRedux.UI.ScreenId";

        static readonly string[] GpsScreenNames =
            { "GpsHub", "ScoreUpload", "GpsProfile", "GpsAvatar", "GpsBadges",
              // auth_golf_profile — the post-signup capture + welcome tutorial.
              "GpsGolfProfile", "GpsWelcome" };

        // A sample of the rest of the app. These must be reachable in EVERY variant — the gate is
        // a deny-list, so anything not on the GPS list is allowed by construction, and a bug that
        // inverted it would take the whole game down with it.
        static readonly string[] NonGpsScreenNames =
            { "Home", "Logo", "Splash", "Loading", "HoleSelection", "Roster", "Inventory" };

        Type _gate;
        Type _screenId;
        MethodInfo _isScreenAllowedWithState;
        MethodInfo _isGpsScreen;

        [SetUp]
        public void SetUp()
        {
            _gate = FindType(GateTypeName);
            _screenId = FindType(ScreenIdTypeName);
            Assert.IsNotNull(_gate, $"{GateTypeName} not found — did Assembly-CSharp compile?");
            Assert.IsNotNull(_screenId, $"{ScreenIdTypeName} not found.");

            _isScreenAllowedWithState = _gate.GetMethod(
                "IsScreenAllowed",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new[] { _screenId, typeof(bool) },
                null);
            _isGpsScreen = _gate.GetMethod("IsGpsScreen", BindingFlags.Static | BindingFlags.Public);

            Assert.IsNotNull(_isScreenAllowedWithState, "IsScreenAllowed(ScreenId, bool) not found.");
            Assert.IsNotNull(_isGpsScreen, "IsGpsScreen(ScreenId) not found.");
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

        object Screen(string name) => Enum.Parse(_screenId, name);
        bool Allowed(string screenName, bool gpsEnabled)
            => (bool)_isScreenAllowedWithState.Invoke(null, new[] { Screen(screenName), (object)gpsEnabled });
        bool IsGps(string screenName)
            => (bool)_isGpsScreen.Invoke(null, new[] { Screen(screenName) });

        // ── The shipped "punch it" build: every GPS screen refuses to open ──────────────────
        [Test]
        public void GpsScreens_AreBlocked_WhenGpsDisabled()
        {
            foreach (var name in GpsScreenNames)
                Assert.IsFalse(Allowed(name, false),
                    $"{name} must be unreachable in a build without GOLFIN_GPS.");
        }

        [Test]
        public void GpsScreens_AreAllowed_WhenGpsEnabled()
        {
            foreach (var name in GpsScreenNames)
                Assert.IsTrue(Allowed(name, true),
                    $"{name} must be reachable in a GOLFIN_GPS build.");
        }

        // ── The rest of the app is never affected, in either variant ────────────────────────
        [Test]
        public void NonGpsScreens_AreAlwaysAllowed()
        {
            foreach (var name in NonGpsScreenNames)
            {
                Assert.IsTrue(Allowed(name, false), $"{name} must stay reachable without GPS.");
                Assert.IsTrue(Allowed(name, true), $"{name} must stay reachable with GPS.");
            }
        }

        // ── The list ScreenManager's chrome rule shares ─────────────────────────────────────
        [Test]
        public void IsGpsScreen_MatchesExactlyTheGpsScreens()
        {
            foreach (var name in GpsScreenNames)
                Assert.IsTrue(IsGps(name), $"{name} must be on the GPS list.");

            foreach (var name in NonGpsScreenNames)
                Assert.IsFalse(IsGps(name), $"{name} must NOT be on the GPS list.");
        }

        /// <summary>
        /// The deny-list's known hazard, asserted rather than left as a comment: if a new ScreenId
        /// whose name starts with "Gps" is added and NOT listed in GpsGate, it ships reachable in
        /// "punch it" builds and nothing complains. This test complains.
        /// </summary>
        [Test]
        public void EveryGpsNamedScreenId_IsOnTheGpsList()
        {
            foreach (var value in Enum.GetValues(_screenId))
            {
                string name = value.ToString();
                if (!name.StartsWith("Gps", StringComparison.Ordinal)) continue;
                Assert.IsTrue((bool)_isGpsScreen.Invoke(null, new[] { value }),
                    $"ScreenId.{name} looks like a GPS screen but is not in GpsGate.GpsScreens — " +
                    $"it would ship REACHABLE in a non-GPS build. Add it to the list.");
            }
        }

        /// <summary>The Editor must always have GPS on, whatever profile is active.</summary>
        [Test]
        public void Enabled_IsTrue_InTheEditor()
        {
            var enabled = _gate.GetField("Enabled", BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(enabled, "GpsGate.Enabled const not found.");
            Assert.IsTrue((bool)enabled.GetRawConstantValue(),
                "GpsGate.Enabled must be true under UNITY_EDITOR — GPS development cannot depend " +
                "on which build profile happens to be active.");
        }
    }
}
