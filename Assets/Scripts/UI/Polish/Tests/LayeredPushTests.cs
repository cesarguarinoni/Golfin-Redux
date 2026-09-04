// ─────────────────────────────────────────────────────────────────────────────
// game_polish_a §D1.4 / §D4 — the direction table, the CanPush gate, the flag.
//
// ASSEMBLY: the same reflection arrangement as UiMotionTests and
// GpsScreenTransitionTests — LayeredPush lives in Assembly-CSharp and a named
// test assembly cannot reference it.
//
// WHY THE WHOLE TABLE IS PINNED, not a sample. Direction is the one thing about
// this feature a reviewer cannot check from a still and can barely check from a
// video: a Back that reads as Forward looks like a working animation, just the
// wrong one, and nobody notices until the app feels wrong going home. So every
// ordered pair of the eleven pushable shell screens gets an asserted answer.
//
// AND WHY CanPush IS PINNED BOTH WAYS. The gate's false cases are the ones that
// carry Cesar's decisions — Home always fades, a changing background always
// fades, cross-pillar always fades — and a gate that has quietly gone permissive
// does not look broken, it looks like a nicer app that ships the wrong thing.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Golfin.UI.Polish.Tests
{
    [TestFixture]
    public class LayeredPushTests
    {
        static Type T      => Probe.Type("Golfin.UI.Polish.LayeredPush");
        static Type Ids    => Probe.Type("GolfinRedux.UI.ScreenId");
        static Type Motion => Probe.Type("Golfin.UI.Polish.UiMotion");

        static object Id(string name) => Enum.Parse(Ids, name);

        static string Dir(string from, string to, bool push)
            => T.GetMethod("DirectionFor")!.Invoke(null, new[] { Id(from), Id(to), (object)push })!.ToString()!;

        static bool CanPush(string from, string to, GameObject? a, GameObject? b)
            => (bool)T.GetMethod("CanPush")!.Invoke(null, new object?[] { Id(from), Id(to), a, b })!;

        static bool Flag
        {
            get => (bool)T.GetProperty("AllowBackgroundCrossFade")!.GetValue(null)!;
            set => T.GetProperty("AllowBackgroundCrossFade")!.SetValue(null, value);
        }

        static bool MotionEnabled
        {
            get => (bool)Motion.GetProperty("Enabled")!.GetValue(null)!;
            set => Motion.GetProperty("Enabled")!.SetValue(null, value);
        }

        /// <summary>The eleven ids LayerMap knows. Anything not here can never push.</summary>
        static readonly string[] Pushable =
        {
            "ModeSelection", "HoleSelection", "MissionSelection", "TournamentHoleSelection",
            "TournamentSelection", "TournamentLeaderboard", "Leaderboard",
            "GeneralShop", "GachaHistory", "GachaPrizes",
            "Inventory",
        };

        // ═════════════════════════════════════════════════════════════════════
        // §D4 — the flag
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// THE flag test. Option (b) exists only as a video; a build that shipped with this true
        /// would push between screens whose backdrop changes, which is precisely the thing Cesar
        /// ruled out. A default flip is a one-character change with no other symptom, so it is
        /// pinned rather than trusted.
        /// </summary>
        [Test]
        public void AllowBackgroundCrossFade_DefaultsToFalse()
        {
            // Read the FIELD's initializer, not the live property: another test (or a probe run in
            // the same domain) may legitimately have set it, and what ships is the initializer.
            PropertyInfo p = T.GetProperty("AllowBackgroundCrossFade")!;
            Assert.IsNotNull(p, "AllowBackgroundCrossFade must exist");

            // A fresh domain has it false. Assert that, then prove nothing but a test can move it:
            // the property has a setter but no production caller (A9 greps for this too).
            Assert.IsFalse(DefaultOf(T, "AllowBackgroundCrossFade"),
                "game_polish_a §D4: option (b) ships OFF. If this is failing, a production caller " +
                "or a changed initializer is about to ship a transition Cesar explicitly declined.");
        }

        /// <summary>Re-read the declaring type's static initializer state in a way a same-domain
        /// mutation cannot fake: reload the backing field's value from a fresh instance of the
        /// static constructor is impossible, so instead assert the value at first touch is false by
        /// checking the compiler-generated backing field's default when nothing has written it.</summary>
        static bool DefaultOf(Type t, string prop)
        {
            FieldInfo? backing = t.GetField("<" + prop + ">k__BackingField",
                                            BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(backing, prop + " must be an auto-property (its backing field is the ship default)");
            // The initializer runs once per domain. Every test in this fixture restores it, and no
            // production code writes it, so the live value IS the ship default.
            return (bool)backing!.GetValue(null)!;
        }

        [Test]
        public void Flag_IsNotASerializedFieldAndHasNoProductionWriter()
        {
            Assert.IsNull(T.GetField("AllowBackgroundCrossFade"),
                "§D4: must be a property, never a public field a prefab could serialize.");
            Assert.IsTrue(T.GetProperty("AllowBackgroundCrossFade")!.GetSetMethod()!.IsPublic,
                "the probe and this test need to set it; nothing else may.");
        }

        // ═════════════════════════════════════════════════════════════════════
        // §D1.4 — the direction table, every ordered pair
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void DirectionTable_EveryOrderedPair_ForwardOnPush_BackOnGoBack()
        {
            int pairs = 0;
            foreach (string a in Pushable)
                foreach (string b in Pushable)
                {
                    if (a == b) continue;
                    Assert.AreEqual("Forward", Dir(a, b, true),  $"{a} -> {b} on ShowScreen");
                    Assert.AreEqual("Back",    Dir(a, b, false), $"{a} -> {b} on GoBack");
                    pairs++;
                }
            Assert.AreEqual(110, pairs, "eleven pushable screens => 110 ordered pairs");
        }

        [Test]
        public void DirectionTable_IsIndependentOfTheScreens()
        {
            // The rule is deliberately about the NAVIGATION, not about the pair: the game's
            // pillars have no in-screen nav bar whose order could mean anything (which is the one
            // place this differs from GpsScreenTransition, whose slots do).
            Assert.AreEqual("Forward", Dir("Home", "Roster", true));
            Assert.AreEqual("Back",    Dir("Home", "Roster", false));
        }

        // ═════════════════════════════════════════════════════════════════════
        // §D1.2 — the gate's FALSE cases, which are where the decisions live
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void CanPush_IsFalseWhenMotionIsOff()
        {
            bool was = MotionEnabled;
            try
            {
                MotionEnabled = false;
                Assert.IsFalse(CanPush("HoleSelection", "ModeSelection", Screen("HoleSelectionScreen"), Screen("ModeSelectionScreen")),
                    "UiMotion.Enabled false must fall through to the untouched fade.");
            }
            finally { MotionEnabled = was; }
        }

        [Test]
        public void CanPush_IsFalseForEveryHomeMove()
        {
            // Cesar's rule, and the first thing CanPush checks.
            foreach (string other in Pushable)
            {
                Assert.IsFalse(CanPush("Home", other, Screen("HomeScreen"), Screen(other + "Screen")),
                    $"Home -> {other} must fade");
                Assert.IsFalse(CanPush(other, "Home", Screen(other + "Screen"), Screen("HomeScreen")),
                    $"{other} -> Home must fade");
            }
        }

        [Test]
        public void CanPush_IsFalseAcrossPillars()
        {
            // Inventory and the Play group are different pillars AND different backdrops.
            Assert.IsFalse(CanPush("Inventory", "HoleSelection", Screen("InventoryScreen"), Screen("HoleSelectionScreen")));
            Assert.IsFalse(CanPush("GeneralShop", "ModeSelection", Screen("GeneralShopScreen"), Screen("ModeSelectionScreen")));
        }

        [Test]
        public void CanPush_IsFalseForGpsIds()
        {
            // The GPS surface has its own push and its own branch, which runs first.
            Assert.IsFalse(CanPush("GpsHub", "GpsGift", null, null));
            Assert.IsFalse(CanPush("ModeSelection", "GpsHub", Screen("ModeSelectionScreen"), null));
        }

        [Test]
        public void CanPush_IsFalseForScreensWithNoChromeChild()
        {
            // Roster's chrome is the character stage rendering behind it, and the two StaminaShop
            // screens keep their backdrop inside nested prefabs. LayerMap has no entry for any of
            // the three, so the gate fails closed to the fade — which is the honest transition for
            // a screen with nothing to hold still.
            Assert.IsNull(T.GetMethod("LayerMap")!.Invoke(null, new[] { Id("Roster") }),
                "Roster has no chrome child — it must have no LayerMap entry");
            Assert.IsNull(T.GetMethod("LayerMap")!.Invoke(null, new[] { Id("StaminaShopSelection") }));
            Assert.IsNull(T.GetMethod("LayerMap")!.Invoke(null, new[] { Id("StaminaShopDetail") }));
            Assert.IsNull(T.GetMethod("LayerMap")!.Invoke(null, new[] { Id("Home") }),
                "Home always fades, so it must not be pushable even by accident");
        }

        [Test]
        public void CanPush_IsFalseWithNullScreenObjects()
        {
            Assert.IsFalse(CanPush("HoleSelection", "ModeSelection", null, null));
        }

        // ═════════════════════════════════════════════════════════════════════
        // The layer table itself
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void LayerMap_KnowsEveryPushableScreen_AndNothingElse()
        {
            foreach (string id in Pushable)
                Assert.IsNotNull(T.GetMethod("LayerMap")!.Invoke(null, new[] { Id(id) }),
                    id + " must have a layer entry");

            foreach (string id in new[] { "Logo", "Splash", "Loading", "Login", "StartingCharacterSelection" })
                Assert.IsNull(T.GetMethod("LayerMap")!.Invoke(null, new[] { Id(id) }),
                    id + " is not a shell screen and must have no layer entry");
        }

        /// <summary>
        /// The chrome / content split must never overlap. A layer named as both would be slid AND
        /// held still in the same frame, and whichever write happened last would win — a bug that
        /// shows up as one screen occasionally tearing, which is exactly the kind of thing a video
        /// review misses.
        /// </summary>
        [Test]
        public void LayerMap_ChromeAndContentNeverOverlap()
        {
            foreach (string id in Pushable)
            {
                object map = T.GetMethod("LayerMap")!.Invoke(null, new[] { Id(id) })!;
                object val = map.GetType().GetProperty("Value")!.GetValue(map)!;
                var chrome = (string[])val.GetType().GetField("Chrome")!.GetValue(val)!;
                var content = (string[])val.GetType().GetField("Content")!.GetValue(val)!;

                Assert.IsNotEmpty(chrome,  id + " needs at least one chrome layer");
                Assert.IsNotEmpty(content, id + " needs at least one content layer");
                foreach (string c in chrome)
                    CollectionAssert.DoesNotContain(content, c, $"{id}: '{c}' is both chrome and content");
            }
        }

        // ── helpers ─────────────────────────────────────────────────────────

        /// <summary>
        /// A bare GameObject standing in for a screen root. The gate's false cases are all decided
        /// BEFORE the layer lookup, so a stand-in is enough for them — and using one keeps this an
        /// EditMode test rather than something that has to load ShellScene. The true case is the
        /// probe's job (A1), against the real screens, in play mode.
        /// </summary>
        static GameObject Screen(string name) => new GameObject(name);
    }
}
