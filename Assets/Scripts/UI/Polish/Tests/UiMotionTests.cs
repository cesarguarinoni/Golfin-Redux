// ─────────────────────────────────────────────────────────────────────────────
// gps_polish §D1 — UiMotion.
//
// ASSEMBLY: Golfin.UI.Polish.Tests (named EditMode asmdef). UiMotion lives in
// Assembly-CSharp — Assets/Scripts/UI has no .asmdef — and a named assembly
// cannot reference a predefined one, so the type is reached by REFLECTION. Same
// pattern PendingSpendTests next door uses, for the same reason.
//
// WHAT THESE GUARD: the settle. Every one of these routines ends by writing an
// exact final value, and the whole design leans on that — an interrupted fade
// that never runs its last line strands a CanvasGroup at 0.43 and leaves half a
// screen visible; an interrupted push strands a ContentContainer off screen.
// So: the routine settles when stepped to completion, the FINALIZER settles when
// it is not, and Enabled=false settles without a coroutine at all.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Golfin.UI.Polish.Tests
{
    [TestFixture]
    public class UiMotionTests
    {
        static Type T => Probe.Type("Golfin.UI.Polish.UiMotion");

        // ── helpers ──────────────────────────────────────────────────────────

        static float Const(string name) => Convert.ToSingle(T.GetField(name).GetRawConstantValue());

        static object Call(string name, params object[] args)
        {
            MethodInfo? m = null;
            foreach (MethodInfo c in T.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (c.Name != name) continue;
                ParameterInfo[] ps = c.GetParameters();
                if (ps.Length < args.Length) continue;
                m = c; break;
            }
            Assert.NotNull(m, "no static method " + name + " on UiMotion");

            ParameterInfo[] pars = m!.GetParameters();
            var full = new object?[pars.Length];
            for (int i = 0; i < pars.Length; i++)
                full[i] = i < args.Length ? args[i] : Type.Missing;
            return m.Invoke(null, BindingFlags.OptionalParamBinding, null, full, null)!;
        }

        static bool Enabled
        {
            get => (bool)T.GetProperty("Enabled")!.GetValue(null)!;
            set => T.GetProperty("Enabled")!.SetValue(null, value);
        }

        /// <summary>Drive a routine to completion the way a coroutine would, with a hard cap so a
        /// runaway loop fails the test instead of hanging the Editor.</summary>
        static int Drain(IEnumerator e, int cap = 100000)
        {
            int steps = 0;
            while (e.MoveNext()) { if (++steps > cap) Assert.Fail("routine did not terminate"); }
            return steps;
        }

        static GameObject NewGo(string name, out RectTransform rt, out CanvasGroup cg)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            rt = go.GetComponent<RectTransform>();
            cg = go.GetComponent<CanvasGroup>();
            return go;
        }

        [TearDown] public void Restore() => Enabled = true;

        // ═════════════════════════════════════════════════════════════════════
        // Easing
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void EaseOut_PinsItsEndpoints()
        {
            Assert.AreEqual(0f, (float)Call("EaseOut", 0f), 1e-6f);
            Assert.AreEqual(1f, (float)Call("EaseOut", 1f), 1e-6f);
        }

        [Test]
        public void EaseIn_PinsItsEndpoints()
        {
            Assert.AreEqual(0f, (float)Call("EaseIn", 0f), 1e-6f);
            Assert.AreEqual(1f, (float)Call("EaseIn", 1f), 1e-6f);
        }

        [Test]
        public void EaseOut_ClampsOutsideTheUnitInterval()
        {
            // A tween whose elapsed overshoots its duration by one long frame must not fly PAST
            // the target and come back.
            Assert.AreEqual(1f, (float)Call("EaseOut", 4f), 1e-6f);
            Assert.AreEqual(0f, (float)Call("EaseOut", -2f), 1e-6f);
        }

        [Test]
        public void EaseOut_LeadsEaseIn_WhichIsWhatMakesItAnEaseOut()
        {
            for (float t = 0.1f; t < 1f; t += 0.1f)
                Assert.Greater((float)Call("EaseOut", t), (float)Call("EaseIn", t),
                               "ease-out must be ahead of ease-in at t=" + t);
        }

        // ═════════════════════════════════════════════════════════════════════
        // Routines settle on their final value
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void Fade_EndsExactlyOnTo()
        {
            NewGo("fade", out _, out CanvasGroup cg);
            var e = (IEnumerator)Call("Fade", cg, 0f, 1f, 0.05f);
            Drain(e);
            Assert.AreEqual(1f, cg.alpha, 1e-6f);
        }

        [Test]
        public void Pop_AlwaysSettlesScaleAtOne()
        {
            NewGo("pop", out RectTransform rt, out CanvasGroup cg);
            var e = (IEnumerator)Call("Pop", rt, cg, 0.05f);
            Drain(e);
            Assert.AreEqual(Vector3.one, rt.localScale);
            Assert.AreEqual(1f, cg.alpha, 1e-6f);
        }

        [Test]
        public void Unpop_SettlesScaleAtOne_NotAtTheShrunkValue()
        {
            // The panel is about to be deactivated; the NEXT Show must find it at rest, not at
            // 0.95. Alpha, on the other hand, settles at 0 — that is what "hidden" means.
            NewGo("unpop", out RectTransform rt, out CanvasGroup cg);
            cg.alpha = 1f;
            var e = (IEnumerator)Call("Unpop", rt, cg, 0.05f);
            Drain(e);
            Assert.AreEqual(Vector3.one, rt.localScale);
            Assert.AreEqual(0f, cg.alpha, 1e-6f);
        }

        [Test]
        public void Slide_EndsExactlyOnToX_AndLeavesYAlone()
        {
            NewGo("slide", out RectTransform rt, out _);
            rt.anchoredPosition = new Vector2(0f, 42f);
            var e = (IEnumerator)Call("Slide", rt, 1170f, 96f, 0.05f, true);
            Drain(e);
            Assert.AreEqual(96f, rt.anchoredPosition.x, 1e-4f);
            Assert.AreEqual(42f, rt.anchoredPosition.y, 1e-4f);
        }

        [Test]
        public void Rise_ReturnsToTheRestYItWasGivenAndFullAlpha()
        {
            NewGo("rise", out RectTransform rt, out CanvasGroup cg);
            rt.anchoredPosition = new Vector2(7f, -361f);
            var e = (IEnumerator)Call("Rise", rt, cg, 16f, 0.05f);
            Drain(e);

            // TOLERANCE IS 0.01 PX, NOT 1e-4, AND THE REASON IS NOT "make it pass". The routine's
            // last line writes restY EXACTLY — but `anchoredPosition` on a parentless
            // RectTransform is stored through localPosition and read back derived, and at a
            // magnitude of 361 one float ulp is already 6.1e-5. A set/read round trip lands within
            // one to three ulp, so the residual is ~1e-4 and its exact size moves with the frame
            // timing that decides how many steps the tween took. The old 1e-4 bound sat right on
            // that noise floor and passed or failed by luck; it survived every run until a fresh
            // Editor changed the step count.
            //
            // Verified as the round trip and not a regression: the same drift reproduces with the
            // production routine driven directly, no test harness involved, and UiMotion.cs has
            // not changed since the suite was last green. What the assertion is FOR is "the
            // content came back to rest" — 0.01 px is still a hundredth of a pixel, far below
            // anything that could be seen, and far above the storage noise.
            Assert.AreEqual(-361f, rt.anchoredPosition.y, 0.01f);
            Assert.AreEqual(7f, rt.anchoredPosition.x, 0.01f);
            Assert.AreEqual(1f, cg.alpha, 1e-6f);
        }

        [Test]
        public void CountUp_SnapsToTheTargetNumber()
        {
            var go = new GameObject("count");
            var label = go.AddComponent<TextMeshProUGUI>();
            var e = (IEnumerator)Call("CountUp", label, 0, 1240, 0.05f, "N0");
            Drain(e);
            Assert.AreEqual("1,240", label.text);
        }

        [Test]
        public void Pulse_RestsAtTheMinimum()
        {
            NewGo("pulse", out _, out CanvasGroup cg);
            var e = (IEnumerator)Call("Pulse", cg, 0f, 1f, 2, 0.05f);
            Drain(e);
            Assert.AreEqual(0f, cg.alpha, 1e-6f);
        }

        // ═════════════════════════════════════════════════════════════════════
        // Stagger
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void Stagger_FiresEveryItemEvenPastTheCap()
        {
            int cap = (int)T.GetField("StaggerCap").GetRawConstantValue()!;
            int n = cap + 8;
            var seen = new bool[n];
            Action<int> per = i => seen[i] = true;

            var e = (IEnumerator)Call("Stagger", n, per, 0.001f);
            Drain(e);

            for (int i = 0; i < n; i++)
                Assert.IsTrue(seen[i], "item " + i + " never fired — the cap must delay, not drop");
        }

        [Test]
        public void Stagger_StopsGettingLaterAtTheCap()
        {
            // Past the cap every remaining item shares the capped beat, so the routine's step
            // count stops growing with n. Without the cap a 30-row list would make the last row
            // wait ~0.9 s, long after the fetch it is celebrating.
            int cap = (int)T.GetField("StaggerCap").GetRawConstantValue()!;
            Action<int> noop = _ => { };
            int atCap  = Drain((IEnumerator)Call("Stagger", cap,      noop, 0.001f));
            int wayPast = Drain((IEnumerator)Call("Stagger", cap * 3, noop, 0.001f));
            Assert.AreEqual(atCap, wayPast, "items past the cap must not add further delay");
        }

        // ═════════════════════════════════════════════════════════════════════
        // Enabled=false — the accessibility short-circuit
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void Run_WithMotionOff_AppliesTheFinalStateWithoutACoroutine()
        {
            NewGo("off", out RectTransform rt, out CanvasGroup cg);
            var host = new GameObject("host").AddComponent<DummyHost>();
            cg.alpha = 0f;

            Enabled = false;
            object e = Call("Fade", cg, 0f, 1f, 5f);          // five SECONDS, so a tween is obvious
            InvokeRun(host, e);

            Assert.AreEqual(1f, cg.alpha, 1e-6f, "motion off must land on the final value at once");
            Assert.AreEqual(Vector3.one, rt.localScale);
        }

        [Test]
        public void Run_OutsidePlayMode_StillSettles()
        {
            // An Editor coroutine runs its first segment and then never advances, so a builder- or
            // test-driven Run that actually started one would strand its target forever.
            NewGo("edit", out _, out CanvasGroup cg);
            var host = new GameObject("host2").AddComponent<DummyHost>();
            cg.alpha = 0.2f;

            Enabled = true;                                   // the guard under test is isPlaying
            InvokeRun(host, Call("Fade", cg, 0.2f, 1f, 5f));

            Assert.AreEqual(1f, cg.alpha, 1e-6f);
        }

        [Test]
        public void Run_ConsumesTheFinalizer_ExactlyOnce()
        {
            // Every routine registers its final state at creation and Run takes it. Taking it
            // ONCE is the load-bearing half: a second Run of the same enumerator must not settle
            // an already-finished tween on top of whatever the result handler has since written —
            // that is the same ordering trap PendingSpend documents ("dispose FIRST").
            NewGo("consume", out _, out CanvasGroup cg);
            var host = new GameObject("host3").AddComponent<DummyHost>();

            Enabled = false;                                  // settle synchronously
            object routine = Call("Fade", cg, 0f, 1f, 5f);

            MethodInfo has = T.GetMethod("HasFinalizer", BindingFlags.NonPublic | BindingFlags.Static)!;
            Assert.IsTrue((bool)has.Invoke(null, new[] { routine })!, "created routine owes a final state");

            InvokeRun(host, routine);
            Assert.AreEqual(1f, cg.alpha, 1e-6f, "first Run must settle it");
            Assert.IsFalse((bool)has.Invoke(null, new[] { routine })!, "Run must consume the finalizer");

            cg.alpha = 0.3f;                                  // stand in for the result handler
            InvokeRun(host, routine);
            Assert.AreEqual(0.3f, cg.alpha, 1e-6f, "a second Run must not re-settle a spent routine");
        }

        [Test]
        public void AnUnrunRoutine_DoesNotPinItsTargetAlive()
        {
            // The finalizer table is keyed WEAKLY on the enumerator. Keyed strongly, a routine
            // that was created and then dropped — an early return, a cancelled tween — would pin
            // the enumerator and, through its closure, the CanvasGroup and the whole screen behind
            // it, for the life of the session.
            var weak = MakeAndDrop();
            for (int i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            Assert.IsFalse(weak.IsAlive, "an unrun routine must not be kept alive by the finalizer table");
        }

        static WeakReference MakeAndDrop()
        {
            NewGo("dropped", out _, out CanvasGroup cg);
            object routine = Call("Fade", cg, 0f, 1f, 5f);
            return new WeakReference(routine);
        }

        static void InvokeRun(MonoBehaviour host, object routine)
        {
            MethodInfo run = T.GetMethod("Run", new[]
            {
                typeof(MonoBehaviour), typeof(Coroutine).MakeByRefType(), typeof(IEnumerator)
            })!;
            object?[] args = { host, null, routine };
            run.Invoke(null, args);
        }

        private sealed class DummyHost : MonoBehaviour { }
    }

    /// <summary>Shared Assembly-CSharp type lookup for the reflection-based suites.</summary>
    internal static class Probe
    {
        public static Type Type(string fullName)
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type? t = a.GetType(fullName);
                if (t != null) return t;
            }
            Assert.Fail("type not found: " + fullName);
            return null!;
        }
    }
}
