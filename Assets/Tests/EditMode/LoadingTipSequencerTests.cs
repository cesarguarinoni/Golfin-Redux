// ─────────────────────────────────────────────────────────────────────────────
// loading_tips — the pool rules, tested where they live.
//
// REFLECTION, and why. LoadingTipSequencer/Catalog/Store are Assembly-CSharp by
// spec (§ Architecture context: "no new asmdef"), and a named test assembly
// cannot reference a predefined one — Unity forbids it in that direction. So the
// three types are reached through `Tips`, the same arrangement UiMotionTests,
// LayeredPushTests and PressFeedbackCoverageTests already use via their `Probe`.
// The shim is 70 lines once; the tests below read like ordinary tests.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace GolfinRedux.Tests.EditMode
{
    /// <summary>Typed access to the Assembly-CSharp tip types.</summary>
    internal static class Tips
    {
        public static readonly Type TipT   = Find("GolfinRedux.UI.LoadingTip");
        public static readonly Type StateT = Find("GolfinRedux.UI.LoadingTipState");
        public static readonly Type SeqT   = Find("GolfinRedux.UI.LoadingTipSequencer");
        public static readonly Type CatT   = Find("GolfinRedux.UI.LoadingTipCatalog");
        public static readonly Type StoreT = Find("GolfinRedux.UI.LoadingTipStore");

        public static Type Find(string fullName)
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type? t = a.GetType(fullName);
                if (t != null) return t;
            }
            Assert.Fail("type not found: " + fullName);
            return null!;
        }

        // ── LoadingTip ───────────────────────────────────────────────────────

        public static object Tip(string key, bool first, int order, string sprite, bool active = true)
        {
            object t = Activator.CreateInstance(TipT)!;
            TipT.GetField("key")!.SetValue(t, key);
            TipT.GetField("first")!.SetValue(t, first);
            TipT.GetField("order")!.SetValue(t, order);
            TipT.GetField("sprite")!.SetValue(t, sprite);
            TipT.GetField("active")!.SetValue(t, active);
            return t;
        }

        public static string Key(object tip)    => (string)TipT.GetField("key")!.GetValue(tip)!;
        public static string Sprite(object tip) => (string)TipT.GetField("sprite")!.GetValue(tip)!;
        public static bool   First(object tip)  => (bool)TipT.GetField("first")!.GetValue(tip)!;
        public static int    Order(object tip)  => (int)TipT.GetField("order")!.GetValue(tip)!;
        public static bool   Active(object tip) => (bool)TipT.GetField("active")!.GetValue(tip)!;

        /// <summary>A List&lt;LoadingTip&gt; the constructor will accept as IReadOnlyList.</summary>
        public static object Rows(params object[] tips)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(TipT))!;
            foreach (object t in tips) list.Add(t);
            return list;
        }

        // ── LoadingTipState ──────────────────────────────────────────────────

        public static object State(int pass = 0, int index = 0, string[]? recent = null)
        {
            object s = Activator.CreateInstance(StateT)!;
            StateT.GetField("firstPass")!.SetValue(s, pass);
            StateT.GetField("firstIndex")!.SetValue(s, index);
            StateT.GetField("recentKeys")!.SetValue(s, recent ?? Array.Empty<string>());
            return s;
        }

        public static int      Pass(object s)   => (int)StateT.GetField("firstPass")!.GetValue(s)!;
        public static int      Index(object s)  => (int)StateT.GetField("firstIndex")!.GetValue(s)!;
        public static string[] Recent(object s) => (string[])StateT.GetField("recentKeys")!.GetValue(s)!;

        // ── LoadingTipSequencer ──────────────────────────────────────────────

        public static object Seq(object rows, object state, Func<int, int>? rng = null)
            => Activator.CreateInstance(SeqT, new object?[] { rows, state, rng })!;

        public static object Current(object seq)    => SeqT.GetProperty("Current")!.GetValue(seq)!;
        public static object Advance(object seq)    => SeqT.GetMethod("Advance")!.Invoke(seq, null)!;
        public static object StateOf(object seq)    => SeqT.GetProperty("State")!.GetValue(seq)!;
        public static bool   HasHistory(object seq) => (bool)SeqT.GetProperty("HasHistory")!.GetValue(seq)!;

        // ── LoadingTipCatalog ────────────────────────────────────────────────

        public static IList Parse(string csv)
            => (IList)CatT.GetMethod("Parse")!.Invoke(null, new object[] { csv })!;

        // ── LoadingTipStore ──────────────────────────────────────────────────

        public static void  StoreSave(object state) => StoreT.GetMethod("Save")!.Invoke(null, new[] { state });
        public static object StoreLoad()            => StoreT.GetMethod("Load")!.Invoke(null, null)!;
        public static void  StoreClear()            => StoreT.GetMethod("Clear")!.Invoke(null, null);

        // ── Fixtures ─────────────────────────────────────────────────────────

        /// <summary>Eight first-pool rows + four general-only rows, the shipped shape in miniature.</summary>
        public static object Catalog12()
        {
            var rows = new List<object>();
            for (int i = 1; i <= 8; i++) rows.Add(Tip("F" + i, true, i, "Tip_F" + i));
            for (int i = 9; i <= 12; i++) rows.Add(Tip("G" + i, false, i, "Tip_G" + i));
            return Rows(rows.ToArray());
        }

        /// <summary>The first pool's keys, in order.</summary>
        public static string[] FirstEight => Enumerable.Range(1, 8).Select(i => "F" + i).ToArray();
    }

    [TestFixture]
    public class LoadingTipSequencerTests
    {
        /// <summary>Deterministic stand-in for UnityEngine.Random: walks 0,1,2,… over whatever
        /// candidate count it is handed, so a draw is reproducible without being constant.</summary>
        static Func<int, int> Rotating()
        {
            int n = 0;
            return count => count <= 0 ? 0 : (n++) % count;
        }

        [Test]
        public void FreshState_WalksTheFirstPoolTwice_ThenDrawsGeneral()
        {
            object seq = Tips.Seq(Tips.Catalog12(), Tips.State(), Rotating());

            Assert.That(Tips.HasHistory(seq), Is.False, "a cleared store is a fresh install");
            Assert.That(Tips.Key(Tips.Current(seq)), Is.EqualTo("F1"),
                "a fresh install opens on tip 1 — the double-advance guard's whole point");

            var seen = new List<string> { Tips.Key(Tips.Current(seq)) };
            for (int i = 0; i < 15; i++) seen.Add(Tips.Key(Tips.Advance(seq)));

            CollectionAssert.AreEqual(Tips.FirstEight.Concat(Tips.FirstEight).ToArray(), seen,
                "the first pool is walked end to end TWICE, in `order`");

            object state = Tips.StateOf(seq);
            Assert.That(Tips.Pass(state), Is.EqualTo(1), "still inside pass 2 after 15 advances");

            // The 16th advance leaves the first pool for good.
            string next = Tips.Key(Tips.Advance(seq));
            Assert.That(Tips.Pass(Tips.StateOf(seq)), Is.EqualTo(2), "pass 2 is done — general pool now");
            Assert.That(next, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void GeneralDraws_NeverRepeatWithinSixConsecutive()
        {
            object seq = Tips.Seq(Tips.Catalog12(), Tips.State(pass: 2), new System.Random(1234).Next2());

            var window = new List<string>();
            for (int i = 0; i < 2000; i++)
            {
                string k = Tips.Key(Tips.Advance(seq));
                Assert.That(window, Does.Not.Contain(k),
                    $"draw {i} repeated '{k}' inside the 5-key exclusion window");
                window.Add(k);
                if (window.Count > 5) window.RemoveAt(0);
            }
        }

        [Test]
        public void ThreeActiveRows_StillDrawSomething()
        {
            // The exclusion (5) is wider than the pool (3): it must shrink from the oldest end
            // rather than produce an empty candidate set.
            object rows = Tips.Rows(Tips.Tip("A", false, 1, "Tip_A"),
                                    Tips.Tip("B", false, 2, "Tip_B"),
                                    Tips.Tip("C", false, 3, "Tip_C"));
            object seq = Tips.Seq(rows, Tips.State(pass: 2), new System.Random(7).Next2());

            for (int i = 0; i < 200; i++)
            {
                string k = Tips.Key(Tips.Advance(seq));
                Assert.That(new[] { "A", "B", "C" }, Does.Contain(k), "draw " + i + " returned '" + k + "'");
            }
        }

        [Test]
        public void InactiveFirstPoolRow_IsNeverShown()
        {
            object rows = Tips.Rows(Tips.Tip("F1", true, 1, "Tip_F1"),
                                    Tips.Tip("F2", true, 2, "Tip_F2", active: false),
                                    Tips.Tip("F3", true, 3, "Tip_F3"));
            object seq = Tips.Seq(rows, Tips.State(), Rotating());

            var seen = new List<string> { Tips.Key(Tips.Current(seq)) };
            for (int i = 0; i < 12; i++) seen.Add(Tips.Key(Tips.Advance(seq)));

            CollectionAssert.DoesNotContain(seen, "F2", "an active=0 row is skipped everywhere (§2.1)");
            CollectionAssert.AreEqual(new[] { "F1", "F3", "F1", "F3" }, seen.Take(4).ToArray());
        }

        [Test]
        public void PersistedIndexPastTheEnd_Clamps()
        {
            // Four rows were deactivated in an update; the stored index points past the pool.
            object rows = Tips.Rows(Tips.Tip("F1", true, 1, "Tip_F1"),
                                    Tips.Tip("F2", true, 2, "Tip_F2"));
            object seq = Tips.Seq(rows, Tips.State(pass: 0, index: 6, recent: new[] { "F1" }), Rotating());

            Assert.That(Tips.Key(Tips.Current(seq)), Is.EqualTo("F1"), "clamps to 0 of the next pass");
            Assert.That(Tips.Pass(Tips.StateOf(seq)), Is.EqualTo(1));
        }

        [Test]
        public void EmptyGeneralPool_IsNullSafe()
        {
            object seq = Tips.Seq(Tips.Rows(), Tips.State(), Rotating());

            object current = Tips.Current(seq);
            Assert.That(Tips.Key(current), Is.Null.Or.Empty, "no rows = nothing to show, not an exception");
            Assert.DoesNotThrow(() => Tips.Advance(seq));
            Assert.That(Tips.Recent(Tips.StateOf(seq)), Is.Empty);
        }

        [Test]
        public void RecentRing_IsCappedAtFive_AndRoundTripsThroughTheStore()
        {
            object seq = Tips.Seq(Tips.Catalog12(), Tips.State(pass: 2), Rotating());
            for (int i = 0; i < 20; i++) Tips.Advance(seq);

            object state = Tips.StateOf(seq);
            string[] ring = Tips.Recent(state);
            Assert.That(ring.Length, Is.EqualTo(5), "the ring holds the last 5 keys, newest last");

            Tips.StoreClear();
            try
            {
                Tips.StoreSave(state);
                object loaded = Tips.StoreLoad();
                CollectionAssert.AreEqual(ring, Tips.Recent(loaded), "recentKeys survive save → load");
                Assert.That(Tips.Pass(loaded), Is.EqualTo(Tips.Pass(state)));
                Assert.That(Tips.Index(loaded), Is.EqualTo(Tips.Index(state)));
            }
            finally { Tips.StoreClear(); }
        }

        [Test]
        public void MissingStore_IsAFreshInstall()
        {
            Tips.StoreClear();
            object loaded = Tips.StoreLoad();
            Assert.That(Tips.Pass(loaded), Is.EqualTo(0));
            Assert.That(Tips.Index(loaded), Is.EqualTo(0));
            Assert.That(Tips.Recent(loaded), Is.Empty);
            Assert.That(Tips.HasHistory(Tips.Seq(Tips.Catalog12(), loaded, Rotating())), Is.False);
        }

        [Test]
        public void ResumedRun_OpensOnANewTip()
        {
            // Cesar 2026-09-09: a loading screen must never open on the tip the previous one
            // opened on. Run 1 shows F1 (fresh) then F2; run 2 must not open on F2.
            object run1 = Tips.Seq(Tips.Catalog12(), Tips.State(), Rotating());
            Assert.That(Tips.Key(Tips.Current(run1)), Is.EqualTo("F1"));
            string closedOn = Tips.Key(Tips.Advance(run1));
            Assert.That(closedOn, Is.EqualTo("F2"));

            object run2 = Tips.Seq(Tips.Catalog12(), Tips.StateOf(run1), Rotating());
            Assert.That(Tips.HasHistory(run2), Is.True, "the ring proves a tip was already shown");
            string opensOn = Tips.Key(Tips.Advance(run2));   // what ProTipCard.Initialize does
            Assert.That(opensOn, Is.EqualTo("F3"));
            Assert.That(opensOn, Is.Not.EqualTo(closedOn));
        }
    }

    internal static class RandomExt
    {
        /// <summary>System.Random as the injectable Func&lt;int,int&gt; — deterministic per seed.</summary>
        public static Func<int, int> Next2(this System.Random r) => n => n <= 0 ? 0 : r.Next(n);
    }
}
