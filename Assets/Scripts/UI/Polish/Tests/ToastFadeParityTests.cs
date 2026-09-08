// ─────────────────────────────────────────────────────────────────────────────
// game_polish_c §C4 / §A6 — the toast fade is UiMotion.Fade, and it EASES.
//
// Cesar's call, 2026-09-08: ship the eased version. So this fixture no longer
// asks "is it identical to the old loop" — it is not, deliberately — and instead
// pins the four things that make the change a decision rather than a drift:
//
//   1. the curve IS UiMotion.EaseOut, value for value, on both real durations;
//   2. the endpoints are exact (from on the first frame, to on the last);
//   3. the frame count and durations are unchanged from the old loop — only the
//      shape between the endpoints moved;
//   4. the size of that shape change is 0.385 at t = 0.423, asserted, so the
//      one number this decision was made on cannot quietly stop being true.
//
// WHY (4) IS A TEST AND NOT A COMMENT. §A6's written tolerance is 0.01 and this
// change is 38x it. That gap is the whole decision, and a decision recorded only
// in prose is one somebody re-derives from scratch in a year — probably by
// "fixing" the fade back to linear because a spec line said 0.01. The number is
// asserted here, next to the code it justifies.
//
// A NOTE ON WHAT DID NOT CHANGE. The old loop never wrote `from`: it began one
// step in, at Lerp(from, to, dt/dur). UiMotion.Fade sets the start value on its
// first frame, so a toast re-Show()n mid-fade now starts from a defined alpha
// rather than wherever the interrupted fade had got to. Asserted in
// FirstFrame_IsTheStartValue.
//
// ASSEMBLY: Golfin.UI.Polish.Tests is a named assembly and cannot reference a
// predefined one, so UiMotion is reached by reflection through the shared Probe
// helper — the same arrangement UiMotionTests and LayeredPushTests already use.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Golfin.UI.Polish.Tests
{
    [TestFixture]
    public class ToastFadeParityTests
    {
        const float Dt = 1f / 60f;      // the RetrofitParityRecorder clock

        /// <summary>ToastController's two serialized durations.</summary>
        const float FadeIn = 0.3f, FadeOut = 0.5f;

        static float EaseOut(float t)
        {
            MethodInfo m = Probe.Type("Golfin.UI.Polish.UiMotion")
                                .GetMethod("EaseOut", BindingFlags.Public | BindingFlags.Static)!;
            Assert.NotNull(m, "UiMotion.EaseOut is gone");
            return (float)m.Invoke(null, new object[] { t })!;
        }

        /// <summary>
        /// The alpha sequence <c>UiMotion.FadeRoutine</c> produces on a fixed clock: the start
        /// value, then one eased sample per frame, then the settle. Transcribed from the routine
        /// rather than driven through it, because a coroutine that yields cannot be stepped in an
        /// EditMode test without a host — and the arithmetic is the thing under test.
        /// </summary>
        static List<float> Eased(float from, float to, float dur)
        {
            var log = new List<float> { from };
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Dt;
                log.Add(Mathf.Lerp(from, to, EaseOut(dur <= 0f ? 1f : elapsed / dur)));
            }
            log.Add(to);
            return log;
        }

        /// <summary>The OLD loop, transcribed from HEAD, on the same clock — kept only so the
        /// size of the change can be measured rather than asserted.</summary>
        static List<float> OldLinear(float from, float to, float dur)
        {
            var log = new List<float>();
            float t = 0f;
            while (t < dur)
            {
                t += Dt;
                log.Add(Mathf.Lerp(from, to, t / dur));
            }
            log.Add(to);
            return log;
        }

        // ── 1 · the curve ────────────────────────────────────────────────────

        [Test]
        public void FadeIn_RunsOnTheSharedEaseOut()
        {
            List<float> a = Eased(0f, 1f, FadeIn);
            for (int i = 1; i < a.Count - 1; i++)
            {
                float t = Mathf.Min(1f, i * Dt / FadeIn);
                Assert.That(a[i], Is.EqualTo(EaseOut(t)).Within(1e-6f),
                    $"frame {i}: the toast fade is no longer on UiMotion's ease-out");
            }
        }

        [Test]
        public void FadeOut_RunsOnTheSharedEaseOut()
        {
            List<float> a = Eased(1f, 0f, FadeOut);
            for (int i = 1; i < a.Count - 1; i++)
            {
                float t = Mathf.Min(1f, i * Dt / FadeOut);
                Assert.That(a[i], Is.EqualTo(1f - EaseOut(t)).Within(1e-6f), $"frame {i}");
            }
        }

        // ── 2 · the endpoints ────────────────────────────────────────────────

        [Test]
        public void FirstFrame_IsTheStartValue()
        {
            // The old loop began one step IN and never wrote `from`. This is the one behavioural
            // improvement the swap brings, so it is pinned rather than left to chance.
            Assert.That(Eased(0f, 1f, FadeIn)[0], Is.EqualTo(0f).Within(1e-6f));
            Assert.That(Eased(1f, 0f, FadeOut)[0], Is.EqualTo(1f).Within(1e-6f));
            Assert.That(OldLinear(0f, 1f, FadeIn)[0], Is.Not.EqualTo(0f).Within(1e-6f),
                "the old loop is supposed to have started one step in — if it did not, the note "
                + "in ToastController about the start value is wrong");
        }

        [Test]
        public void LastFrame_IsTheEndValue()
        {
            Assert.That(Eased(0f, 1f, FadeIn)[^1], Is.EqualTo(1f).Within(1e-6f));
            Assert.That(Eased(1f, 0f, FadeOut)[^1], Is.EqualTo(0f).Within(1e-6f));
        }

        // ── 3 · what did NOT change ──────────────────────────────────────────

        [Test]
        public void DurationAndFrameCount_AreUnchanged()
        {
            // 0.3 s at 1/60 is 18 steps; the eased log carries a start value the old one did not,
            // so it is one longer by construction and by nothing else.
            Assert.That(OldLinear(0f, 1f, FadeIn).Count, Is.EqualTo(19));
            Assert.That(Eased(0f, 1f, FadeIn).Count, Is.EqualTo(20));
            Assert.That(OldLinear(1f, 0f, FadeOut).Count, Is.EqualTo(31));
            Assert.That(Eased(1f, 0f, FadeOut).Count, Is.EqualTo(32));
        }

        // ── 4 · the size of the change, asserted ─────────────────────────────

        [Test]
        public void TheChangeIsTheEase_AndItIs0point385()
        {
            float worst = 0f, at = 0f;
            for (int i = 0; i <= 1000; i++)
            {
                float t = i / 1000f;
                float d = Mathf.Abs(EaseOut(t) - t);
                if (d > worst) { worst = d; at = t; }
            }
            Assert.That(worst, Is.EqualTo(0.385f).Within(0.002f),
                $"the eased-vs-linear divergence moved to {worst:0.###}; ToastController's header, "
                + "the report's §A6 and deviation D-1 all quote 0.385");
            Assert.That(at, Is.EqualTo(0.423f).Within(0.01f),
                $"the worst divergence moved to t={at:0.###} (was 0.423)");
            Assert.That(worst, Is.GreaterThan(0.01f),
                "this is the assertion that says §A6's written 0.01 tolerance does NOT hold and "
                + "was superseded by a decision — if it ever passes, something has quietly gone "
                + "back to linear");
        }

        // ── the call site ────────────────────────────────────────────────────

        [Test]
        public void ToastController_RoutesThroughUiMotionFade()
        {
            // Read off the shipped source: the arithmetic tests above would keep passing if the
            // call site were reverted to a hand-rolled loop, because they never touch it.
            //
            // COMMENTS ARE STRIPPED FIRST, and that is not a loophole — it is the difference
            // between the claim and the prose about the claim. The first version of this test
            // failed on ToastController's own header, which says the old loop "was a straight
            // Mathf.Lerp". A header that explains what the code no longer does is exactly what a
            // reader needs; a guard that forbids saying so would push the explanation out of the
            // file. So the check reads CODE.
            const string src = "Assets/Scripts/UI/Toast/ToastController.cs";
            Assert.IsTrue(System.IO.File.Exists(src), src + " not found");
            string code = Code(System.IO.File.ReadAllText(src));

            Assert.That(code, Does.Contain("UiMotion.Fade"),
                "ToastController no longer fades through UiMotion.Fade");
            Assert.That(code, Does.Not.Contain("Time.unscaledDeltaTime"),
                "a hand-rolled tween loop is back in ToastController");
            Assert.That(code, Does.Not.Contain("Mathf.Lerp"),
                "a hand-rolled lerp is back in ToastController");
        }

        /// <summary>Source with every `//` and `///` line dropped. Crude on purpose: this file has
        /// no block comments and no string literal containing a slash pair, and a real parser here
        /// would be more machinery than the question deserves.</summary>
        static string Code(string source)
        {
            var sb = new System.Text.StringBuilder();
            foreach (string line in source.Split('\n'))
                if (!line.TrimStart().StartsWith("//")) sb.AppendLine(line);
            return sb.ToString();
        }
    }
}
