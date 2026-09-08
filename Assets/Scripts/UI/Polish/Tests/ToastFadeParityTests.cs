// ─────────────────────────────────────────────────────────────────────────────
// game_polish_c §C4 / §A6 — the toast fade is UiMotion now, and it is the SAME fade.
//
// §C4's gate is a per-frame alpha log of the old loop against the new one, worst
// difference <= 0.01. The old loop was:
//
//     t += Time.unscaledDeltaTime;
//     alpha = Mathf.Lerp(from, to, t / dur);
//     ... and alpha = to after the loop.
//
// The new call is UiMotion.Tween(from, to, dur, set, Ease.Linear), whose routine is
//
//     elapsed += Time.unscaledDeltaTime;
//     apply(Mathf.LerpUnclamped(from, to, Curve(Linear, elapsed / dur)));
//     ... and apply(to) after the loop,
//
// with Curve(Linear, t) == Mathf.Clamp01(t). Clamped-then-unclamped-lerp is
// arithmetically identical to Mathf.Lerp, so the two sequences are not merely
// within tolerance — they are the same floats in the same order. This fixture
// asserts that against a FIXED 1/60 clock, frame by frame, for both of the
// controller's real durations (_fadeIn 0.3, _fadeOut 0.5).
//
// AND IT PINS THE EASE, which is the part that would silently break. §C4's own
// words are "ToastController.Fade -> UiMotion.Fade"; UiMotion.Fade eases on cubic
// ease-out and the old loop was linear, and the two differ by 0.385 at t = 0.423 —
// thirty-eight times the tolerance the same sentence sets. If someone later
// "finishes the job" by switching the call to UiMotion.Fade, LinearIsNotEaseOut
// fails with that number in the message rather than the toast quietly changing
// how it appears.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
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

        /// <summary>The OLD loop, transcribed from HEAD, driven on the fixed clock.</summary>
        static List<float> Old(float from, float to, float dur)
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

        /// <summary>The NEW arithmetic: UiMotion.Tween's routine on Ease.Linear, same clock.
        /// Curve() and Ease are reached by reflection (named assembly ‑> Assembly-CSharp).</summary>
        static List<float> New(float from, float to, float dur)
        {
            Type um = Probe.Type("Golfin.UI.Polish.UiMotion");
            Type ease = Probe.Type("Golfin.UI.Polish.Ease");
            object linear = Enum.Parse(ease, "Linear");
            MethodInfo curve = um.GetMethod("Curve", BindingFlags.Public | BindingFlags.Static)!;
            Assert.NotNull(curve, "UiMotion.Curve is gone");

            var log = new List<float>();
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Dt;
                float c = (float)curve.Invoke(null, new object[] { linear, dur <= 0f ? 1f : elapsed / dur })!;
                log.Add(Mathf.LerpUnclamped(from, to, c));
            }
            log.Add(to);
            return log;
        }

        static float WorstDelta(List<float> a, List<float> b)
        {
            Assert.That(b.Count, Is.EqualTo(a.Count),
                $"frame COUNT differs ({a.Count} vs {b.Count}) — the two loops no longer step the same way");
            float worst = 0f;
            for (int i = 0; i < a.Count; i++) worst = Mathf.Max(worst, Mathf.Abs(a[i] - b[i]));
            return worst;
        }

        [Test]
        public void FadeIn_IsFrameIdentical()
        {
            float worst = WorstDelta(Old(0f, 1f, 0.3f), New(0f, 1f, 0.3f));
            Assert.That(worst, Is.LessThanOrEqualTo(0.01f), $"§A6 tolerance is 0.01; worst frame delta {worst:0.#####}");
            Assert.That(worst, Is.EqualTo(0f).Within(1e-6f), "the two curves should be bit-identical, not merely close");
        }

        [Test]
        public void FadeOut_IsFrameIdentical()
        {
            float worst = WorstDelta(Old(1f, 0f, 0.5f), New(1f, 0f, 0.5f));
            Assert.That(worst, Is.LessThanOrEqualTo(0.01f), $"§A6 tolerance is 0.01; worst frame delta {worst:0.#####}");
            Assert.That(worst, Is.EqualTo(0f).Within(1e-6f));
        }

        [Test]
        public void EveryFrame_IsLogged_NotJustTheEndpoints()
        {
            // A parity log of two frames would pass trivially. 0.3 s at 1/60 is 18 steps + the settle.
            Assert.That(Old(0f, 1f, 0.3f).Count, Is.EqualTo(19));
            Assert.That(New(0f, 1f, 0.3f).Count, Is.EqualTo(19));
        }

        [Test]
        public void LinearIsNotEaseOut_SoTheCallSiteMustStayOnTween()
        {
            Type um = Probe.Type("Golfin.UI.Polish.UiMotion");
            MethodInfo easeOut = um.GetMethod("EaseOut", BindingFlags.Public | BindingFlags.Static)!;
            Assert.NotNull(easeOut, "UiMotion.EaseOut is gone");

            float worst = 0f, at = 0f;
            for (int i = 0; i <= 1000; i++)
            {
                float t = i / 1000f;
                float d = Mathf.Abs((float)easeOut.Invoke(null, new object[] { t })! - t);
                if (d > worst) { worst = d; at = t; }
            }
            // ~0.385 at t ~= 0.423. Quoted so the deviation in the report is checkable.
            Assert.That(worst, Is.GreaterThan(0.3f),
                "ease-out and linear have converged?! The §C4 deviation's premise no longer holds.");
            Assert.That(at, Is.EqualTo(0.423f).Within(0.01f),
                $"worst ease-out/linear divergence moved to t={at:0.###} (was 0.423, value {worst:0.###})");
        }

        [Test]
        public void ToastController_DoesNotHandRollItsFade()
        {
            // The retrofit's actual claim: no `Mathf.Lerp` tween loop left in the file. Read off
            // the shipped source, because a test of the arithmetic above would keep passing if the
            // call site were reverted.
            const string src = "Assets/Scripts/UI/Toast/ToastController.cs";
            Assert.IsTrue(System.IO.File.Exists(src), src + " not found");
            string text = System.IO.File.ReadAllText(src);
            Assert.That(text, Does.Contain("UiMotion.Tween"), "ToastController no longer routes its fade through UiMotion");
            Assert.That(text, Does.Not.Contain("Time.unscaledDeltaTime"),
                "a hand-rolled tween loop is back in ToastController");
        }
    }
}
