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
using System.Collections;
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

    // ═════════════════════════════════════════════════════════════════════════
    // push_arrival_hitch — the arrival, driven for real
    //
    // WHY THESE DRIVE THE COROUTINE INSTEAD OF ASSERTING ON THE CODE. Every one
    // of the four defects this task fixes was invisible to an assertion about
    // shape: the compositing bug (P0) was one `if` in front of a
    // SetAsLastSibling that a reader would have called correct; the first-frame
    // jump was arithmetic that was also correct, applied to a delta that was
    // not. So the enumerator is stepped by hand, frame by frame, and the
    // assertions read the same LastPush* numbers the probe's invariants read —
    // which means a green test here and a green invariant there cannot disagree
    // about what happened.
    //
    // EditMode, no play session: Push touches PersistentUIManager only through
    // `?.`, Canvas.ForceUpdateCanvases is legal outside play mode, and
    // UiMotion.Run finalizes immediately when !Application.isPlaying — so the
    // whole coroutine is steppable here.
    // ═════════════════════════════════════════════════════════════════════════
    [TestFixture]
    public class LayeredPushArrivalTests
    {
        static Type T   => Probe.Type("Golfin.UI.Polish.LayeredPush");
        static Type Ids => Probe.Type("GolfinRedux.UI.ScreenId");

        static object Id(string name) => Enum.Parse(Ids, name);
        static object Dir(string name) => Enum.Parse(T.GetNestedType("Dir")!, name);

        static object? P(string name) => T.GetProperty(name)!.GetValue(null);
        static float   Pf(string name) => (float)P(name)!;
        static bool    Pb(string name) => (bool)P(name)!;
        static float   Const(string name) => (float)T.GetField(name)!.GetRawConstantValue()!;

        static float PushDur => (float)Probe.Type("Golfin.UI.Polish.UiMotion")
                                             .GetProperty("PushDur")!.GetValue(null)!;

        /// <summary>The whole rig: a common parent, the ARRIVER authored FIRST (the occluded
        /// shape — ModeSelection at ScreensRoot 10 arriving over MissionSelection at 8), the
        /// leaver second, and one shared Sprite on both chrome layers so the pair is
        /// same-backdrop.</summary>
        sealed class Rig : IDisposable
        {
            public readonly GameObject Root, ToGo, FromGo;
            readonly Sprite _sprite;
            readonly Texture2D _tex;

            public Rig(string to, string from, bool sameBackground)
            {
                Root   = new GameObject("ScreensRoot", typeof(RectTransform));
                ToGo   = Build(to);
                FromGo = Build(from);
                ToGo.transform.SetParent(Root.transform, false);     // sibling 0 — the arriver
                FromGo.transform.SetParent(Root.transform, false);   // sibling 1 — the leaver

                _tex    = new Texture2D(4, 4);
                _sprite = Sprite.Create(_tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
                Paint(ToGo,   to,   _sprite);
                Paint(FromGo, from, sameBackground ? _sprite : null);
            }

            static GameObject Build(string id)
            {
                var go = new GameObject(id + "Screen", typeof(RectTransform));
                object map = T.GetMethod("LayerMap")!.Invoke(null, new[] { Id(id) })!;
                foreach (string n in (string[])map.GetType().GetField("Chrome")!.GetValue(map)!)
                    Kid(go, n).AddComponent<UnityEngine.UI.Image>();
                foreach (string n in (string[])map.GetType().GetField("Content")!.GetValue(map)!)
                    Kid(go, n);
                return go;
            }

            static void Paint(GameObject go, string id, Sprite? sprite)
            {
                object map = T.GetMethod("LayerMap")!.Invoke(null, new[] { Id(id) })!;
                foreach (string n in (string[])map.GetType().GetField("Chrome")!.GetValue(map)!)
                {
                    Transform? t = go.transform.Find(n);
                    var img = t != null ? t.GetComponent<UnityEngine.UI.Image>() : null;
                    if (img != null) img.sprite = sprite;
                }
            }

            static GameObject Kid(GameObject parent, string name)
            {
                var c = new GameObject(name, typeof(RectTransform));
                c.transform.SetParent(parent.transform, false);
                return c;
            }

            public RectTransform Content(GameObject screen, string id)
            {
                object map = T.GetMethod("LayerMap")!.Invoke(null, new[] { Id(id) })!;
                string first = ((string[])map.GetType().GetField("Content")!.GetValue(map)!)[0];
                return (RectTransform)screen.transform.Find(first)!;
            }

            public CanvasGroup? Chrome(GameObject screen, string id)
            {
                object map = T.GetMethod("LayerMap")!.Invoke(null, new[] { Id(id) })!;
                string first = ((string[])map.GetType().GetField("Chrome")!.GetValue(map)!)[0];
                Transform? t = screen.transform.Find(first);
                return t != null ? t.GetComponent<CanvasGroup>() : null;
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
                UnityEngine.Object.DestroyImmediate(_sprite);
                UnityEngine.Object.DestroyImmediate(_tex);
            }
        }

        static IEnumerator Start(Rig rig, string from, string to, string dir, Action apply)
            => (IEnumerator)T.GetMethod("Push")!.Invoke(null, new object[]
               { Id(from), Id(to), rig.FromGo, rig.ToGo, Dir(dir), apply })!;

        /// <summary>Step to completion, with a hard bound: the Editor's unscaledDeltaTime is not
        /// guaranteed to advance outside play mode, and a test that hangs is worse than one that
        /// fails. When the bound is hit the push is snapped exactly as an interrupting Navigate
        /// would snap it, which is a settle path worth exercising anyway.</summary>
        static void Drain(IEnumerator it, int maxFrames = 400)
        {
            int n = 0;
            while (it.MoveNext())
            {
                if (++n < maxFrames) continue;
                T.GetMethod("CompleteActiveNow")!.Invoke(null, null);
                while (it.MoveNext()) { }
                return;
            }
        }

        [TearDown]
        public void ClearAnyLivePush() => T.GetMethod("CompleteActiveNow")!.Invoke(null, null);

        // ── P0 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// THE BUG CESAR REPORTED. The arriver is authored EARLIER in ScreensRoot than the
        /// leaver, and every shell screen's chrome is an opaque full-screen Image — so before
        /// this fix the whole arriving screen slid for 250 ms underneath the leaver's backdrop
        /// and the player saw the leaver drift and then a hard cut.
        ///
        /// <para>Asserted from the LIVE transform mid-tween, not from the fact that
        /// SetAsLastSibling is called: it was called before too, behind an `if`.</para>
        /// </summary>
        [Test]
        public void ArriverIsDrawnOnTop_EvenWhenItIsTheEarlierSibling_AndTheOrderIsRestored()
        {
            using var rig = new Rig(to: "MissionSelection", from: "ModeSelection", sameBackground: true);
            Assert.AreEqual(0, rig.ToGo.transform.GetSiblingIndex(), "rig: the arriver starts underneath");

            bool applied = false;
            IEnumerator it = Start(rig, "ModeSelection", "MissionSelection", "Forward", () => applied = true);

            it.MoveNext();   // staging + the held frame
            Assert.AreEqual(rig.Root.transform.childCount - 1, rig.ToGo.transform.GetSiblingIndex(),
                "the arriver must be the LAST sibling for the whole push, or it slides under the " +
                "leaver's opaque backdrop (P0)");

            Drain(it);
            Assert.IsTrue(Pb("LastPushArriverOnTop"), "sampled every frame from inside the tween");
            Assert.AreEqual(0, rig.ToGo.transform.GetSiblingIndex(),
                "and the authored ScreensRoot order is restored at Settle");
            Assert.IsTrue(applied, "the deferred ApplyScreen still runs, last");
        }

        /// <summary>
        /// P0's other half. On top of an IDENTICAL backdrop the arriver's own chrome must be off
        /// for the whole push — drawn, it would cut the leaver's content away on frame 1, which
        /// is the same defect mirrored. The seam is covered by the leaver's chrome, which stays
        /// at 1 and is what <c>LastPushChromeAlphaMin</c> now samples.
        /// </summary>
        [Test]
        public void SameBackdrop_ArriverChromeIsHeldOff_AndRestoredAtSettle()
        {
            using var rig = new Rig(to: "MissionSelection", from: "ModeSelection", sameBackground: true);
            IEnumerator it = Start(rig, "ModeSelection", "MissionSelection", "Forward", () => { });

            it.MoveNext();
            CanvasGroup? toChrome   = rig.Chrome(rig.ToGo,   "MissionSelection");
            CanvasGroup? fromChrome = rig.Chrome(rig.FromGo, "ModeSelection");
            Assert.IsNotNull(toChrome); Assert.IsNotNull(fromChrome);
            Assert.AreEqual(0f, toChrome!.alpha, 0.0001f, "arriver chrome off while it is on top");
            Assert.AreEqual(1f, fromChrome!.alpha, 0.0001f, "the leaver's backdrop is the one being drawn");

            Drain(it);
            Assert.AreEqual(0f, Pf("LastPushArriverChromeAlphaMax"), 0.0001f);
            Assert.AreEqual(1f, Pf("LastPushChromeAlphaMin"), 0.0001f,
                "the leaver's chrome never dipped — the seam invariant, narrowed to the layer it is about");
            Assert.AreEqual(1f, toChrome.alpha, 0.0001f, "Rest() puts the arriver's chrome back");
        }

        // ── fix 1 ───────────────────────────────────────────────────────────

        /// <summary>
        /// THE HELD FRAME. The arriving screen is built by the SetActive — OnEnable, the first
        /// layout, the cards — and that frame has been measured at 50-70 ms. The first
        /// `elapsed += unscaledDeltaTime` used to eat it, putting the content a fifth of the way
        /// home in one step. Now the frame the screen is built in is a frame in which NOTHING
        /// moves: the content is still parked at rest + the full enter offset.
        /// </summary>
        [Test]
        public void TheFirstFrameIsHeld_TheContentHasNotMovedWhenTheScreenIsBuilt()
        {
            using var rig = new Rig(to: "MissionSelection", from: "ModeSelection", sameBackground: true);
            RectTransform content = rig.Content(rig.ToGo, "MissionSelection");
            float restX = content.anchoredPosition.x;

            IEnumerator it = Start(rig, "ModeSelection", "MissionSelection", "Forward", () => { });
            it.MoveNext();

            Assert.IsTrue(rig.ToGo.activeSelf, "the target IS activated before the held frame");
            Assert.AreEqual(0, (int)P("LastPushFrames")!, "no tween frame has run yet");
            Assert.AreEqual(restX + Pf("LastPushEnterOffset"), content.anchoredPosition.x, 0.01f,
                "the held frame must show the content exactly where staging left it");

            Drain(it);
            Assert.GreaterOrEqual(Pf("LastPushArrivalFrameMs"), 0f,
                "the arrival cost is recorded per pair so fix 4 has a number to move");
        }

        /// <summary>
        /// THE STEP CAP. The held frame absorbs the build, but one late hitch — a shader compile,
        /// a GC — would otherwise jump the ease-out several frames' worth in a single draw: the
        /// same visible defect at a lower rate. No frame may advance more than
        /// <c>MaxTweenStep</c> of real time, whatever the Editor's delta happens to be.
        /// </summary>
        [Test]
        public void NoSingleFrameAdvancesMoreThanTwoFramesOfTravel()
        {
            using var rig = new Rig(to: "MissionSelection", from: "ModeSelection", sameBackground: true);
            IEnumerator it = Start(rig, "ModeSelection", "MissionSelection", "Forward", () => { });
            Drain(it);

            float ceiling = Const("MaxTweenStep") / PushDur;
            Assert.LessOrEqual(Pf("LastPushMaxStepFrac"), ceiling + 0.0001f,
                $"a frame advanced more than {Const("MaxTweenStep"):0.###}s of a {PushDur:0.###}s tween");
            Assert.AreEqual(1f / 30f, Const("MaxTweenStep"), 0.0001f, "two frames at 60 fps");
        }

        // ── fix 3 ───────────────────────────────────────────────────────────

        /// <summary>
        /// PARALLAX NEEDS SOMETHING TO BE DEEP RELATIVE TO. Over one fixed backdrop, a leaver at
        /// 0.3 W and an arriver at 1.0 W read as a stutter, not as depth; the two contents move
        /// as one strip instead. The cross-fade path, where the room itself is changing, keeps
        /// the 0.3.
        /// </summary>
        [Test]
        public void SameBackdropPairsDriftTheLeaverAtFullWidth_CrossFadePairsKeepTheDepthCue()
        {
            using (var same = new Rig(to: "MissionSelection", from: "ModeSelection", sameBackground: true))
            {
                Drain(Start(same, "ModeSelection", "MissionSelection", "Forward", () => { }));
                Assert.AreEqual(Const("SameBackdropParallaxFactor"), Pf("LastPushParallaxFactor"), 0.0001f);
                Assert.AreEqual(1f, Const("SameBackdropParallaxFactor"), 0.0001f, "one strip, not two speeds");
            }

            using (var diff = new Rig(to: "TournamentSelection", from: "ModeSelection", sameBackground: false))
            {
                Drain(Start(diff, "ModeSelection", "TournamentSelection", "Forward", () => { }));
                Assert.AreEqual(Const("ParallaxFactor"), Pf("LastPushParallaxFactor"), 0.0001f,
                    "a pair whose backdrop cross-fades still gets the depth cue");
            }
        }

        // ── fix 2 ───────────────────────────────────────────────────────────

        /// <summary>
        /// The window the paint paths need, and why it is NOT <c>EnteringViaPush</c>.
        /// <c>EnteringViaPush</c> is armed around one SetActive because a 16 px rise decides
        /// itself in OnEnable; a list can be painted by a fetch that answers three frames into
        /// the slide, and that paint must not stagger either. So the flag the paint paths read
        /// covers the WHOLE arrival, and it must be false the moment the push is over.
        /// </summary>
        [Test]
        public void ArrivingViaPush_IsTrueForTheWholeSlide_AndFalseOnceItSettles()
        {
            Assert.IsFalse(Pb("ArrivingViaPush"), "nothing is arriving before the test starts");

            using var rig = new Rig(to: "MissionSelection", from: "ModeSelection", sameBackground: true);
            IEnumerator it = Start(rig, "ModeSelection", "MissionSelection", "Forward", () => { });

            it.MoveNext();
            Assert.IsTrue(Pb("ArrivingViaPush"), "true across the held frame");
            it.MoveNext();
            Assert.IsTrue(Pb("ArrivingViaPush"), "and across the tween");

            Drain(it);
            Assert.IsFalse(Pb("ArrivingViaPush"),
                "and false again at Settle — a flag left armed suppresses every later stagger");
        }
    }
}
