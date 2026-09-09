// ─────────────────────────────────────────────────────────────────────────────
// game_polish_b §D2 — the `Ease` parameter, and the promise attached to it.
//
// The promise is that adding it changed nothing: OutCubic is the curve every existing
// call site already ran, so `gps_polish` behaves to the frame as it did. That is the
// half of this suite that matters most, and it is checked by VALUE rather than by
// reading the diff — Ease.OutCubic must equal EaseOut everywhere, and Unpop and Slide,
// whose OutCubic defers to an ease-IN or to their own bool, must keep doing so.
//
// The other half pins what the new members are FOR: OutBack has to overshoot, because a
// curve that does not is just an ease-out with extra steps and the gacha reveal would
// have been silently flattened by the retrofit.
//
// ASSEMBLY: Golfin.UI.Polish.Tests, reaching Assembly-CSharp by reflection — the pattern
// UiMotionTests next door established and for the same reason (a named assembly cannot
// reference a predefined one).
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
    public class UiMotionEaseTests
    {
        static Type T => Probe.Type("Golfin.UI.Polish.UiMotion");
        static Type EaseT => Probe.Type("Golfin.UI.Polish.Ease");

        static object Ease(string name) => Enum.Parse(EaseT, name);

        static float Curve(string ease, float t)
            => (float)T.GetMethod("Curve", BindingFlags.Public | BindingFlags.Static)!
                       .Invoke(null, new object[] { Ease(ease), t })!;

        static float EaseOut(float t)
            => (float)T.GetMethod("EaseOut", BindingFlags.Public | BindingFlags.Static)!
                       .Invoke(null, new object[] { t })!;

        static float EaseIn(float t)
            => (float)T.GetMethod("EaseIn", BindingFlags.Public | BindingFlags.Static)!
                       .Invoke(null, new object[] { t })!;

        static int Drain(IEnumerator e, int cap = 100000)
        {
            int n = 0;
            while (e.MoveNext()) if (++n > cap) Assert.Fail("routine did not terminate");
            return n;
        }

        /// <summary>
        /// Refuse to assert on an INTERMEDIATE frame when the editor's clock is too coarse to
        /// produce one.
        ///
        /// <para>These routines integrate Time.unscaledDeltaTime, and in EditMode that is whatever
        /// the editor's last frame took — normally ~1.1 s on this machine, but it spikes far
        /// higher after a long operation such as a scene reload or a two-minute test run. When
        /// dt exceeds the duration the tween correctly completes in ONE step, and a test that
        /// wanted to see it half-done fails while nothing is wrong. That is exactly what happened
        /// on 2026-09-08: five tests across three suites failed together, every one of them
        /// reporting a tween that had already finished, and all five passed on an immediate
        /// re-run with no code change.</para>
        ///
        /// <para>Ignoring is the honest outcome — the property is not observable here — where
        /// failing would be a false alarm and passing would be a lie. The two suites next door
        /// (UiMotionAllocationTests, UiMotionNewPrimitiveTests) have the same fragility and are
        /// flagged in the report; they are not this task's to change.</para>
        /// </summary>
        static void RequireAFrameWithin(float dur)
        {
            float dt = Time.unscaledDeltaTime;
            if (dt >= dur * 0.5f)
                Assert.Ignore($"editor frame clock is {dt:F3}s against a {dur:F3}s tween — " +
                              "no intermediate frame exists to assert on (see RequireAFrameWithin)");
        }

        // ═════════════════════════════════════════════════════════════════════
        // The promise: OutCubic is today's curve
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void OutCubic_IsExactlyEaseOut_AtEveryPoint()
        {
            for (float t = 0f; t <= 1.0001f; t += 0.02f)
                Assert.AreEqual(EaseOut(t), Curve("OutCubic", t), 1e-6f, "t=" + t);
        }

        [Test]
        public void TheEnumHasExactlyTheThreeMembersTheSpecNames()
        {
            CollectionAssert.AreEquivalent(new[] { "OutCubic", "OutBack", "Linear" },
                                           Enum.GetNames(EaseT));
        }

        // ═════════════════════════════════════════════════════════════════════
        // OutBack — the reason the enum exists
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void OutBack_PinsItsEndpoints()
        {
            Assert.AreEqual(0f, Curve("OutBack", 0f), 1e-6f);
            Assert.AreEqual(1f, Curve("OutBack", 1f), 1e-6f);
        }

        [Test]
        public void OutBack_ActuallyOvershoots()
        {
            // The whole point. A curve that never exceeds 1 is an ease-out wearing a different
            // name, and the gacha retrofit would have flattened the reveal without anyone seeing
            // a failing test.
            float peak = 0f;
            for (float t = 0f; t <= 1f; t += 0.005f) peak = Mathf.Max(peak, Curve("OutBack", t));
            Assert.Greater(peak, 1.05f, "OutBack must overshoot past 1");
            Assert.Less(peak, 1.15f, "OutBack overshoot is c1=1.70158, not something larger");
        }

        [Test]
        public void OutBack_IsTheCurveTheGachaRevealShipped()
        {
            // Character for character with the expression GachaRevealModalController carried
            // before §D2 deleted it. If this drifts, the reveal changed.
            const float c1 = 1.70158f, c3 = c1 + 1f;
            for (float t = 0f; t <= 1.0001f; t += 0.01f)
            {
                float p = Mathf.Clamp01(t) - 1f;
                float expected = 1f + c3 * p * p * p + c1 * p * p;
                Assert.AreEqual(expected, Curve("OutBack", t), 1e-6f, "t=" + t);
            }
        }

        [Test]
        public void BackOvershoot_IsTheQuotedConstant()
            => Assert.AreEqual(1.70158f,
                               Convert.ToSingle(T.GetField("BackOvershoot")!.GetRawConstantValue()), 1e-7f);

        // ═════════════════════════════════════════════════════════════════════
        // Linear
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void Linear_IsTheIdentity_AndItsMidpointIsAHalf()
        {
            Assert.AreEqual(0.5f, Curve("Linear", 0.5f), 1e-6f);
            for (float t = 0f; t <= 1.0001f; t += 0.05f)
                Assert.AreEqual(Mathf.Clamp01(t), Curve("Linear", t), 1e-6f, "t=" + t);
        }

        [Test]
        public void EveryCurve_ClampsOutsideTheUnitInterval()
        {
            foreach (string e in new[] { "OutCubic", "OutBack", "Linear" })
            {
                Assert.AreEqual(1f, Curve(e, 4f), 1e-6f, e + " above 1");
                Assert.AreEqual(0f, Curve(e, -2f), 1e-6f, e + " below 0");
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // The primitives still settle, on every curve
        // ═════════════════════════════════════════════════════════════════════

        static GameObject NewGo(out RectTransform rt, out CanvasGroup cg)
        {
            var go = new GameObject("EaseTarget", typeof(RectTransform), typeof(CanvasGroup));
            rt = go.GetComponent<RectTransform>();
            cg = go.GetComponent<CanvasGroup>();
            return go;
        }

        [Test]
        public void Pop_SettlesOnOne_OnEveryCurve()
        {
            foreach (string e in new[] { "OutCubic", "OutBack", "Linear" })
            {
                GameObject go = NewGo(out RectTransform rt, out CanvasGroup cg);
                var pop = (IEnumerator)T.GetMethod("Pop")!.Invoke(null,
                    new object[] { rt, cg, 0.2f, Ease(e) })!;
                Drain(pop);
                Assert.AreEqual(1f, rt.localScale.x, 1e-5f, e + " scale");
                Assert.AreEqual(1f, cg.alpha, 1e-5f, e + " alpha");
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Pop_OnOutBack_NeverDrivesAlphaOutOfRange()
        {
            // The scale overshoots on purpose; the alpha must not. A CanvasGroup handed 1.09 is
            // a value no other code path can produce, and it clamps on some paths and not others.
            GameObject go = NewGo(out RectTransform rt, out CanvasGroup cg);
            var pop = (IEnumerator)T.GetMethod("Pop")!.Invoke(null,
                new object[] { rt, cg, 0.2f, Ease("OutBack") })!;
            while (pop.MoveNext())
            {
                Assert.GreaterOrEqual(cg.alpha, 0f);
                Assert.LessOrEqual(cg.alpha, 1f);
            }
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void Tween_LerpsUnclamped_SoOutBackOvershootSurvives()
        {
            // Guards the one substantive change §D2 made inside the primitives. With a clamped
            // lerp this peak would be exactly 1 and the reveal would be flat.
            // 30 s, not 0.3: the overshoot lives in the MIDDLE of the curve, so this test needs a
            // frame that is not the last one. A long duration buys that on all but the worst clock,
            // and RequireAFrameWithin refuses rather than lies on those.
            RequireAFrameWithin(30f);
            float peak = 0f;
            var tw = (IEnumerator)T.GetMethod("Tween")!.Invoke(null,
                new object[] { 0f, 1f, 30f, (Action<float>)(v => peak = Mathf.Max(peak, v)),
                               Ease("OutBack") })!;
            Drain(tw);
            Assert.Greater(peak, 1.0f, "Tween must lerp unclamped for OutBack to overshoot");
        }

        [Test]
        public void Unpop_DefaultCurve_IsStillEaseIn()
        {
            // Unpop's OutCubic means "the ease-IN half of the cubic pair" — a panel leaving should
            // accelerate away. Documented in the API and pinned here so it cannot quietly flip.
            RequireAFrameWithin(10f);
            GameObject go = NewGo(out RectTransform rt, out CanvasGroup cg);
            var un = (IEnumerator)T.GetMethod("Unpop")!.Invoke(null,
                new object[] { rt, cg, 10f, Ease("OutCubic") })!;
            un.MoveNext();                     // one frame in
            float travelled = (1f - rt.localScale.x) / (1f - 0.95f);
            un.MoveNext();
            // An ease-IN is BEHIND linear early on; an ease-out would already be most of the way.
            Assert.Less(travelled, 0.5f, "Unpop's default must still be the ease-in half");
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void Slide_DefaultCurve_StillDefersToItsEaseOutBool()
        {
            RequireAFrameWithin(10f);

            // MEASURED AGAINST LINEAR, not against a constant — polish_regressions_0909 R0.
            //
            // This used to assert `x < 1f`, which silently assumed the first frame advanced only a
            // sliver of the 10 s tween. In EditMode `Time.unscaledDeltaTime` is the EDITOR's last
            // frame delta, and during a 2.5-minute suite a single frame of several seconds is
            // ordinary: at dt = 3.7 s the ease-in position is ~3.7, the assert failed, and the
            // build went red on a test whose subject had not changed. RequireAFrameWithin did not
            // catch it because its threshold (half the duration) is far looser than what `x < 1f`
            // actually needed (dt < 2.15 s).
            //
            // The property under test does not depend on dt at all: for the SAME elapsed fraction,
            // ease-in trails the linear position and ease-out leads it. Asserting that directly is
            // both exact and immune to whatever the editor's clock did.
            float dt = Time.unscaledDeltaTime;
            float linear = 100f * Mathf.Clamp01(dt / 10f);

            foreach (bool easeOut in new[] { true, false })
            {
                var go = new GameObject("SlideTarget", typeof(RectTransform));
                var rt = go.GetComponent<RectTransform>();
                var sl = (IEnumerator)T.GetMethod("Slide")!.Invoke(null,
                    new object[] { rt, 0f, 100f, 10f, easeOut, Ease("OutCubic") })!;
                sl.MoveNext();
                float x = rt.anchoredPosition.x;
                sl.MoveNext();
                // ease-out leads, ease-in lags — the bool must still choose between them.
                if (easeOut) Assert.Greater(x, linear, $"easeOut:true must LEAD linear ({linear:F3})");
                else         Assert.Less(x, linear,    $"easeOut:false must LAG linear ({linear:F3})");
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
