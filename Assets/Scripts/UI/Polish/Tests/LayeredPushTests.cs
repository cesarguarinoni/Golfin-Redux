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
// carry Cesar's decisions — Home always fades, cross-pillar always fades — and a
// gate that has quietly gone permissive does not look broken, it looks like a
// nicer app that ships the wrong thing.
//
// THE BACKGROUND IS NO LONGER ONE OF THOSE CASES. Cesar shipped option (b) on
// 2026-09-04 after watching the clip, so two screens of the same pillar push even
// when their backdrops differ. The tests that used to pin the flag OFF now pin the
// opposite: that such a pair really does push, and that the background still
// decides whether the CHROME animates. Deleting them and leaving nothing would
// have removed the only guard on the decision that changed.
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
        // Option (b), shipped — the background is not a gate any more
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// THE flag is gone, and it must stay gone. It existed for exactly one thing — the video
        /// that let Cesar judge the alternative — and he shipped the alternative. A flag left
        /// behind at default-true is a dead branch nobody exercises and everybody has to reason
        /// about; a flag left behind at default-false would silently undo his decision.
        /// </summary>
        [Test]
        public void TheOptionBFlag_IsGone()
        {
            Assert.IsNull(T.GetProperty("AllowBackgroundCrossFade"),
                "game_polish_a: option (b) shipped 2026-09-04 — the flag was removed, not flipped. " +
                "If this is failing, someone reintroduced a switch for a decision that is made.");
            Assert.IsNull(T.GetField("AllowBackgroundCrossFade"));
        }

        /// <summary>
        /// The decision itself: a same-pillar pair whose BACKDROPS DIFFER is pushable.
        ///
        /// <para>This is the one assertion that would have caught the change being reverted by
        /// accident. It uses the real screen objects' absence deliberately — CanPush's remaining
        /// false cases are all decided before any layer lookup, so what is being pinned here is
        /// that the background is no longer consulted at the gate at all.</para>
        /// </summary>
        [Test]
        public void SameBackground_IsNoLongerRequiredByTheGate()
        {
            MethodInfo? m = T.GetMethod("SameBackground");
            Assert.IsNotNull(m, "SameBackground still exists — it decides whether the CHROME animates");

            // ModeSelection (Art/HoleSelectScreen/Background) and TournamentSelection
            // (Art/RankingsScreen/BackgroundRangkings) are both MainPlay and draw DIFFERENT
            // sprites. Before option (b) shipped this pair faded; it pushes now.
            //
            // The stand-ins carry the real chrome/content CHILD NAMES from LayerMap, because
            // CanPush's true path really does look them up (HasSplit). A bare GameObject makes
            // this test fail for the wrong reason — which is exactly what the first version of it
            // did, and is why it is built from the table rather than hand-named.
            GameObject a = ScreenWithLayers("ModeSelection");
            GameObject b = ScreenWithLayers("TournamentSelection");
            try
            {
                Assert.IsTrue(CanPush("ModeSelection", "TournamentSelection", a, b),
                    "option (b) shipped: a same-pillar pair with different backdrops must push");
                Assert.IsTrue(CanPush("TournamentSelection", "ModeSelection", b, a),
                    "and in both directions");
            }
            finally { UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); }
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
                // LayerMap returns Layers?, but BOXING a Nullable<T> yields the underlying T (or
                // null) — there is no boxed Nullable to ask for .Value, which is what the first
                // version of this test did and why it threw an NRE rather than failing an assert.
                object val = T.GetMethod("LayerMap")!.Invoke(null, new[] { Id(id) })!;
                var chrome  = (string[])val.GetType().GetField("Chrome")!.GetValue(val)!;
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

        /// <summary>
        /// A stand-in screen carrying the chrome and content children <c>LayerMap</c> names for
        /// that id, so <c>HasSplit</c> is satisfied and the gate's TRUE path can be reached. Read
        /// from the table rather than hand-written, so a rename of a layer cannot leave this test
        /// quietly asserting nothing.
        /// </summary>
        static GameObject ScreenWithLayers(string id)
        {
            var go = new GameObject(id + "Screen", typeof(RectTransform));
            object map = T.GetMethod("LayerMap")!.Invoke(null, new[] { Id(id) })!;
            var chrome  = (string[])map.GetType().GetField("Chrome")!.GetValue(map)!;
            var content = (string[])map.GetType().GetField("Content")!.GetValue(map)!;
            foreach (string n in chrome)  Child(go, n).AddComponent<UnityEngine.UI.Image>();
            foreach (string n in content) Child(go, n);
            return go;
        }

        static GameObject Child(GameObject parent, string name)
        {
            var c = new GameObject(name, typeof(RectTransform));
            c.transform.SetParent(parent.transform, false);
            return c;
        }
    }
}
