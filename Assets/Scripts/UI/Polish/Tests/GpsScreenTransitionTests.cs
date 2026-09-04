// ─────────────────────────────────────────────────────────────────────────────
// gps_polish §D2 — the direction table and the CanPush gate.
//
// ASSEMBLY: the same reflection arrangement as UiMotionTests — GpsScreenTransition
// lives in Assembly-CSharp and a named test assembly cannot reference it.
//
// WHY THE WHOLE TABLE IS PINNED, not a sample. Direction is the one thing about
// this feature a reviewer cannot check from a still and can barely check from a
// video: a Back that reads as Forward looks like a working animation, just the
// wrong one, and the mistake is invisible until somebody notices the app feels
// wrong going home. So every ordered pair of GPS screens gets an asserted answer.
//
// Also pinned: ModalController.animateShow defaults to FALSE. Every modal in the
// game inherits that class, and a default flip would put new motion on the
// level-up modal, the shop and the tournament gates from a GPS task's diff.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Golfin.UI.Polish.Tests
{
    [TestFixture]
    public class GpsScreenTransitionTests
    {
        static Type T   => Probe.Type("Golfin.Gps.UI.GpsScreenTransition");
        static Type Ids => Probe.Type("GolfinRedux.UI.ScreenId");
        static Type Gate => Probe.Type("Golfin.Gps.UI.GpsGate");
        static Type Motion => Probe.Type("Golfin.UI.Polish.UiMotion");

        static object Id(string name) => Enum.Parse(Ids, name);

        static string Dir(string from, string to, bool push)
            => T.GetMethod("DirectionFor")!.Invoke(null, new[] { Id(from), Id(to), (object)push })!.ToString()!;

        static int Slot(string id)
            => (int)T.GetMethod("NavSlot")!.Invoke(null, new[] { Id(id) })!;

        static bool CanPush(string from, string to, GameObject? a, GameObject? b)
            => (bool)T.GetMethod("CanPush")!.Invoke(null, new object?[] { Id(from), Id(to), a, b })!;

        static bool HasSplit(GameObject? go)
            => (bool)T.GetMethod("HasSplit")!.Invoke(null, new object?[] { go })!;

        /// <summary>Every screen GpsGate calls GPS, which is the set this feature applies to.</summary>
        static readonly string[] GpsScreens =
        {
            "GpsHub", "ScoreUpload", "GpsProfile", "GpsAvatar", "GpsBadges",
            "GpsGolfProfile", "GpsWelcome", "GpsGift", "GpsVote",
            // gps_checkin — the Rounds tab. Added because the size assertion below caught its
            // absence, which is exactly what that assertion is for: it sits at NavSlot 1, so
            // every ordered pair through it is now checked against the restated rule.
            "GpsRounds",
        };

        static bool MotionEnabled
        {
            get => (bool)Motion.GetProperty("Enabled")!.GetValue(null)!;
            set => Motion.GetProperty("Enabled")!.SetValue(null, value);
        }

        [TearDown] public void Restore() => MotionEnabled = true;

        /// <summary>A screen shaped the way the push needs: Background + ContentContainer.</summary>
        static GameObject Split(string name, bool withNav = true)
        {
            var root = new GameObject(name, typeof(RectTransform));
            Child(root, "Background");
            Child(root, "ContentContainer");
            if (withNav) Child(root, "GpsNavBar");
            return root;
        }

        static void Child(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
        }

        // ═════════════════════════════════════════════════════════════════════
        // The list this feature applies to must match GpsGate's
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void EveryScreenThisSuitePins_IsActuallyOnGpsGatesList()
        {
            // GpsGate is a DENY-LIST: a new GPS screen that nobody adds to it ships reachable in
            // a "punch it" build. If that list grows, this suite's table is incomplete and the new
            // screen has an unasserted direction — so the two are tied together here.
            MethodInfo isGps = Gate.GetMethod("IsGpsScreen")!;
            foreach (string s in GpsScreens)
                Assert.IsTrue((bool)isGps.Invoke(null, new[] { Id(s) })!,
                              s + " is pinned here but GpsGate does not call it a GPS screen");

            int gpsCount = 0;
            foreach (string name in Enum.GetNames(Ids))
                if ((bool)isGps.Invoke(null, new[] { Id(name) })!) gpsCount++;
            Assert.AreEqual(GpsScreens.Length, gpsCount,
                            "GpsGate's GPS surface changed size — add the new screen to this table");
        }

        // ═════════════════════════════════════════════════════════════════════
        // Direction table
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void EveryOrderedPair_HasAPinnedDirection()
        {
            foreach (string from in GpsScreens)
            foreach (string to in GpsScreens)
            {
                if (from == to) continue;
                string d = Dir(from, to, push: true);
                Assert.IsTrue(d == "Forward" || d == "Back", from + "->" + to + " gave " + d);
                Assert.AreEqual(Expected(from, to), d, from + " -> " + to);
            }
        }

        /// <summary>The table, restated independently of the implementation so the test is an
        /// assertion rather than an echo.</summary>
        static string Expected(string from, string to)
        {
            if (to == "GpsHub") return "Back";
            int a = Slot(from), b = Slot(to);
            if (a >= 0 && b >= 0) return b > a ? "Forward" : "Back";
            if (a < 0 && b >= 0) return "Back";
            return "Forward";
        }

        [Test]
        public void AnythingHeadedForTheHub_ReadsAsComingBack()
        {
            foreach (string from in GpsScreens)
            {
                if (from == "GpsHub") continue;
                Assert.AreEqual("Back", Dir(from, "GpsHub", push: true), from + " -> GpsHub");
            }
        }

        [Test]
        public void ANonPush_IsAlwaysBack()
        {
            // GoBack and the nav-bar pillar jump both arrive with push:false.
            foreach (string from in GpsScreens)
            foreach (string to in GpsScreens)
            {
                if (from == to) continue;
                Assert.AreEqual("Back", Dir(from, to, push: false), from + " -> " + to + " (push:false)");
            }
        }

        [Test]
        public void TheNavBarRow_FollowsTheBarsOwnLeftToRightOrder()
        {
            Assert.AreEqual("Forward", Dir("ScoreUpload", "GpsGift",    push: true));
            Assert.AreEqual("Forward", Dir("GpsGift",     "GpsVote",    push: true));
            Assert.AreEqual("Forward", Dir("GpsVote",     "GpsProfile", push: true));
            // and mirrored going left
            Assert.AreEqual("Back", Dir("GpsProfile", "GpsVote",    push: true));
            Assert.AreEqual("Back", Dir("GpsVote",    "GpsGift",    push: true));
            Assert.AreEqual("Back", Dir("GpsGift",    "ScoreUpload", push: true));
        }

        [Test]
        public void LeavingADeepSubScreenForTheNavRow_IsBack()
        {
            // Badges and Avatar have no slot of their own. Tapping the Profile slot from Badges is
            // the player's ONLY way out of Badges, and it must not read as going deeper.
            Assert.AreEqual("Back", Dir("GpsBadges", "GpsProfile", push: true));
            Assert.AreEqual("Back", Dir("GpsAvatar", "GpsProfile", push: true));
            // The other way round is going deeper.
            Assert.AreEqual("Forward", Dir("GpsProfile", "GpsBadges", push: true));
            Assert.AreEqual("Forward", Dir("GpsProfile", "GpsAvatar", push: true));
        }

        [Test]
        public void TheOnboardingPair_GoesForwardThenHome()
        {
            Assert.AreEqual("Forward", Dir("GpsGolfProfile", "GpsWelcome", push: true));
            Assert.AreEqual("Back",    Dir("GpsWelcome",     "GpsHub",     push: true));
        }

        [Test]
        public void NavSlots_AreOrderedAndUnique()
        {
            Assert.AreEqual(0, Slot("GpsHub"));
            Assert.Less(Slot("GpsHub"),      Slot("ScoreUpload"));
            Assert.Less(Slot("ScoreUpload"), Slot("GpsGift"));
            Assert.Less(Slot("GpsGift"),     Slot("GpsVote"));
            Assert.Less(Slot("GpsVote"),     Slot("GpsProfile"));
            // Deep sub-screens and the onboarding pair are deliberately not in the bar.
            foreach (string s in new[] { "GpsBadges", "GpsAvatar", "GpsGolfProfile", "GpsWelcome" })
                Assert.AreEqual(-1, Slot(s), s + " must not claim a nav slot");
        }

        // ═════════════════════════════════════════════════════════════════════
        // CanPush
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void ScoreUpload_NeverPushes_InEitherDirection()
        {
            // Its six step roots each carry their OWN background and sit beside ContentContainer,
            // so there is no single background to cross-fade. The fade is the honest transition.
            GameObject a = Split("A"), b = Split("B");
            foreach (string other in GpsScreens)
            {
                if (other == "ScoreUpload") continue;
                Assert.IsFalse(CanPush("ScoreUpload", other, a, b), "ScoreUpload -> " + other);
                Assert.IsFalse(CanPush(other, "ScoreUpload", a, b), other + " -> ScoreUpload");
            }
        }

        [Test]
        public void APrefabWithoutTheSplit_FallsBackToTheFade()
        {
            GameObject good = Split("good");
            var bare = new GameObject("bare", typeof(RectTransform));
            Assert.IsFalse(CanPush("GpsHub", "GpsProfile", bare, good), "leaver has no split");
            Assert.IsFalse(CanPush("GpsHub", "GpsProfile", good, bare), "target has no split");
            Assert.IsTrue(CanPush("GpsHub", "GpsProfile", good, Split("g2")));
        }

        [Test]
        public void HasSplit_NeedsBothLayers_NotOne()
        {
            var onlyBg = new GameObject("onlyBg", typeof(RectTransform));
            Child(onlyBg, "Background");
            Assert.IsFalse(HasSplit(onlyBg));

            var onlyContent = new GameObject("onlyContent", typeof(RectTransform));
            Child(onlyContent, "ContentContainer");
            Assert.IsFalse(HasSplit(onlyContent));

            Assert.IsTrue(HasSplit(Split("both")));
            Assert.IsFalse(HasSplit(null));
        }

        [Test]
        public void MotionOff_SendsEveryPairBackToTheFade()
        {
            GameObject a = Split("a"), b = Split("b");
            Assert.IsTrue(CanPush("GpsHub", "GpsProfile", a, b));
            MotionEnabled = false;
            Assert.IsFalse(CanPush("GpsHub", "GpsProfile", a, b));
        }

        [Test]
        public void ANonGpsEnd_NeverPushes()
        {
            // The boundary is the whole point: Home <-> GpsHub keeps the fade to black.
            GameObject a = Split("a"), b = Split("b");
            Assert.IsFalse(CanPush("Home", "GpsHub", a, b));
            Assert.IsFalse(CanPush("GpsHub", "Home", a, b));
            Assert.IsFalse(CanPush("GpsHub", "Login", a, b));
        }

        [Test]
        public void ASelfNavigation_NeverPushes()
        {
            GameObject a = Split("a");
            Assert.IsFalse(CanPush("GpsHub", "GpsHub", a, a));
        }

        [Test]
        public void FindLayer_ReachesTheNavBarThroughItsSafeAreaWrapper()
        {
            // gps_polish §D9 moves GpsNavBar into a stretched NavSafeArea wrapper. Every caller
            // goes through FindLayer for exactly this reason — a hard-coded Find("GpsNavBar")
            // would return null and log a warning instead of failing.
            var root = new GameObject("wrapped", typeof(RectTransform));
            Child(root, "Background");
            Child(root, "ContentContainer");
            var wrapper = new GameObject("NavSafeArea", typeof(RectTransform));
            wrapper.transform.SetParent(root.transform, false);
            var nav = new GameObject("GpsNavBar", typeof(RectTransform));
            nav.transform.SetParent(wrapper.transform, false);

            MethodInfo find = T.GetMethod("FindLayer")!;
            var found = (Transform?)find.Invoke(null, new object?[] { root, "GpsNavBar" });
            Assert.NotNull(found, "FindLayer must see through NavSafeArea");
            Assert.AreSame(nav.transform, found);

            // and still finds a bar that has NOT been wrapped
            var flat = Split("flat");
            Assert.NotNull((Transform?)find.Invoke(null, new object?[] { flat, "GpsNavBar" }));
        }
    }

    [TestFixture]
    public class ModalAnimateShowDefaultTests
    {
        [Test]
        public void AnimateShow_DefaultsToFalse()
        {
            // Every modal in the game inherits ModalController. A default of true would put new
            // motion on the level-up modal, the shop, the tournament gates and the versus result
            // from inside a GPS task's diff. Only GpsPolishBuilder turns it on, on three prefabs.
            Type t = Probe.Type("Golfin.UI.Modals.ModalController");
            var go = new GameObject("modal");
            Component c = go.AddComponent(t);

            FieldInfo f = t.GetField("animateShow", BindingFlags.NonPublic | BindingFlags.Instance)!;
            Assert.IsFalse((bool)f.GetValue(c)!, "animateShow must default to false");

            PropertyInfo p = t.GetProperty("AnimatesShow")!;
            Assert.IsFalse((bool)p.GetValue(c)!);
        }
    }
}
