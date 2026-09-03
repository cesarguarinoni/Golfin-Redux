// ─────────────────────────────────────────────────────────────────────────────
// gps_polish continuation — the four things added after the push, pinned.
//
// R6 is the reason this file exists. The keyboard offset can only be SEEN on a
// phone, so the SPEC's acceptance for it would otherwise be "trust me". The fix
// is that the decision is a pure function of five numbers — screen height,
// keyboard height, the field's two edges and the canvas scale — and that
// function is exercised here with real iPhone 14 numbers. What cannot be tested
// in the Editor is reduced to "does `TouchScreenKeyboard.area` report what iOS
// says", which is the only part that genuinely needs the device pass.
//
// The rest guard the same property every UiMotion primitive leans on: the tween
// ENDS on an exact value, whether it was stepped to completion or interrupted.
//
// ASSEMBLY NOTE: as with UiMotionTests next door, these types live in
// Assembly-CSharp and a named assembly cannot reference a predefined one, so
// everything is reached by reflection.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Golfin.UI.Polish.Tests
{
    // ═════════════════════════════════════════════════════════════════════════
    // R6 · KeyboardInset.OffsetFor
    // ═════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class KeyboardInsetTests
    {
        static Type T => Probe.Type("Golfin.UI.Polish.KeyboardInset");

        /// <summary>iPhone 14 / 14 Pro at the resolution the GPS screens are authored against.</summary>
        const float ScreenH = 2532f;

        /// <summary>The canvas is 1170 wide against an 1170 reference, so scale is 1 — which is
        /// what makes canvas px and screen px the same number on this device and is exactly why
        /// the scale has to be a PARAMETER rather than an assumption.</summary>
        const float Scale = 1f;

        /// <summary>A representative iOS keyboard on a 2532-tall screen: ~336 pt at @3x.</summary>
        const float Keyboard = 1008f;

        static float Offset(float keyboard, float fieldBottom, float fieldTop,
                            float scale = Scale, float screen = ScreenH)
            => (float)T.GetMethod("OffsetFor", BindingFlags.Public | BindingFlags.Static)!
                       .Invoke(null, new object[] { screen, keyboard, fieldBottom, fieldTop, scale, 24f })!;

        [Test]
        public void NoKeyboard_IsAlwaysZero()
        {
            // The Editor path. TouchScreenKeyboard reports height 0 there, so every GPS screen
            // must be pixel-identical to HEAD in the Editor — which is what A2 measures.
            Assert.AreEqual(0f, Offset(keyboard: 0f, fieldBottom: 100f, fieldTop: 200f), 1e-4f);
        }

        [Test]
        public void FieldAlreadyAboveTheKeyboard_IsZero()
        {
            // Bottom edge at 1200 px from the bottom, keyboard 1008 tall plus a 24 margin: clear
            // by 168 px, so nothing moves. A screen that lurched for a field that was already
            // visible would be worse than not lifting at all.
            Assert.AreEqual(0f, Offset(Keyboard, fieldBottom: 1200f, fieldTop: 1300f), 1e-4f);
        }

        [Test]
        public void FieldUnderTheKeyboard_LiftsByExactlyTheShortfall()
        {
            // The Golf Profile nickname field: bottom edge ~792 px from the bottom of the screen.
            // 1008 + 24 − 792 = 240.
            Assert.AreEqual(240f, Offset(Keyboard, fieldBottom: 792f, fieldTop: 892f), 1e-4f);
        }

        [Test]
        public void TheLiftIsInCanvasPx_NotScreenPx()
        {
            // Same geometry at 2x. TWO conversions happen and both matter:
            //   • the 24 px margin is specified in CANVAS px, so on screen it is 48;
            //   • the answer is a container offset, so it is handed back in CANVAS px.
            // Shortfall on screen = 1008 + 48 − 792 = 264 px; as a canvas offset that is 132.
            // Moving the container by the raw 264 would double the lift and throw the field off
            // the top of the screen.
            float lift = Offset(Keyboard, fieldBottom: 792f, fieldTop: 892f, scale: 2f);
            Assert.AreEqual(132f, lift, 1e-4f);

            // The property the name is about, stated directly: the lift, put back into screen px,
            // is exactly the shortfall it was computed from.
            Assert.AreEqual(1008f + 48f - 792f, lift * 2f, 1e-4f);
        }

        [Test]
        public void ATallFieldNeverLeavesTheTopOfTheScreen()
        {
            // A field that is nearly as tall as the space above the keyboard: the naive shortfall
            // would push its own top off screen. Escaping the keyboard by scrolling the field out
            // of the top is not a fix, so the lift is capped at the available headroom.
            float top = ScreenH - 100f;                 // 100 px below the top edge
            float bottom = 200f;                        // deep under the keyboard
            float capped = Offset(Keyboard, fieldBottom: bottom, fieldTop: top);

            Assert.Less(capped, Keyboard + 24f - bottom, "uncapped shortfall was used");
            Assert.AreEqual(ScreenH - top - 24f, capped, 1e-4f);
            Assert.LessOrEqual(top + capped, ScreenH, "field top pushed off the screen");
        }

        [Test]
        public void AFieldWithNoHeadroomDoesNotMoveAtAll()
        {
            // Degenerate: the field's top is already at the very top of the screen. There is
            // nowhere to go, and returning a negative "lift" would push it DOWN into the keyboard.
            Assert.AreEqual(0f, Offset(Keyboard, fieldBottom: 100f, fieldTop: ScreenH), 1e-4f);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // R3 / R4 · the two new primitives
    // ═════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class UiMotionNewPrimitiveTests
    {
        static Type T => Probe.Type("Golfin.UI.Polish.UiMotion");

        static object Invoke(string name, params object?[] args)
        {
            foreach (MethodInfo m in T.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (m.Name != name) continue;
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length < args.Length) continue;
                var full = new object?[ps.Length];
                for (int i = 0; i < ps.Length; i++) full[i] = i < args.Length ? args[i] : Type.Missing;
                return m.Invoke(null, BindingFlags.OptionalParamBinding, null, full, null)!;
            }
            Assert.Fail("no static method " + name);
            return null!;
        }

        static int Drain(IEnumerator e, int cap = 100000)
        {
            int n = 0;
            while (e.MoveNext()) if (++n > cap) Assert.Fail("routine did not terminate");
            return n;
        }

        [Test]
        public void Bump_EndsAtScaleOne()
        {
            var go = new GameObject("bump", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            try
            {
                var e = (IEnumerator)Invoke("Bump", rt, 1.06f, 0.10f);
                Drain(e);
                // A chip stranded at 1.04 is a permanently mis-sized control, and these are the
                // controls a player taps most.
                Assert.AreEqual(Vector3.one, rt.localScale);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void Bump_OvershootsBeforeItComesBack()
        {
            var go = new GameObject("bump", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            try
            {
                // Stepped by hand so the MIDDLE of the bump is observable: an implementation that
                // only ever eased 1 → 1 would pass the endpoint test above and animate nothing.
                var e = (IEnumerator)Invoke("Bump", rt, 1.06f, 0.10f);
                float peak = 1f;
                while (e.MoveNext()) peak = Mathf.Max(peak, rt.localScale.x);
                Assert.Greater(peak, 1.0f, "the bump never grew");
                Assert.LessOrEqual(peak, 1.06f + 1e-4f, "the bump overshot its own peak");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void Tween_CallsApplyWithTheExactFinalValue()
        {
            float last = float.NaN;
            var e = (IEnumerator)Invoke("Tween", 10f, 42f, 0.2f, (Action<float>)(v => last = v));
            Drain(e);
            Assert.AreEqual(42f, last, 1e-4f);
        }

        [Test]
        public void Tween_IsMonotonicBetweenItsEndpoints()
        {
            // The vote bar grows from the old percentage to the new one; a curve that dipped below
            // the start would read as the bar shrinking before it grew.
            var seen = new System.Collections.Generic.List<float>();
            var e = (IEnumerator)Invoke("Tween", 20f, 60f, 0.2f, (Action<float>)(v => seen.Add(v)));
            while (e.MoveNext()) { }
            for (int i = 1; i < seen.Count; i++)
                Assert.GreaterOrEqual(seen[i], seen[i - 1] - 1e-4f, "the tween went backwards");
            Assert.GreaterOrEqual(seen[0], 20f - 1e-4f);
            Assert.AreEqual(60f, seen[seen.Count - 1], 1e-4f);
        }

        [Test]
        public void Render_DropsTheNumberIntoItsSurroundingRun()
        {
            // The GPS labels are rarely a bare number. A count-up that dropped "pts" would be a
            // worse bug than not counting at all.
            Assert.AreEqual("1,240", (string)Invoke("Render", 1240, "N0", null));
            Assert.AreEqual("1,240 pts", (string)Invoke("Render", 1240, "N0", "{0} pts"));
            Assert.AreEqual("7 / 24 earned", (string)Invoke("Render", 7, "N0", "{0} / 24 earned"));
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // R9 / A13 · do the tween loops allocate PER FRAME?
    //
    // The in-situ profiler pass measures the whole app during a push — the
    // arriving screen's first-activation frame, its four live requests, TMP
    // rebuilds, and the Editor profiler's own overhead — so it is an upper bound
    // and cannot attribute an allocation to the tween. THIS is the attribution:
    // the routine's own MoveNext, nothing else running, measured on the managed
    // heap. The SPEC's remedy list ("cache WaitForEndOfFrame, no closures in the
    // loop") is exactly what a non-zero result here would send you to fix.
    // ═════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class UiMotionAllocationTests
    {
        static Type T => Probe.Type("Golfin.UI.Polish.UiMotion");

        static object Invoke(string name, params object?[] args)
        {
            foreach (MethodInfo m in T.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (m.Name != name) continue;
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length < args.Length) continue;
                var full = new object?[ps.Length];
                for (int i = 0; i < ps.Length; i++) full[i] = i < args.Length ? args[i] : Type.Missing;
                return m.Invoke(null, BindingFlags.OptionalParamBinding, null, full, null)!;
            }
            Assert.Fail("no static method " + name);
            return null!;
        }

        /// <summary>
        /// Bytes the managed heap took while stepping <paramref name="routine"/>, ignoring the
        /// first few steps (the enumerator's own state machine is allocated on the first
        /// MoveNext, which is the once-at-start cost the SPEC explicitly allows).
        /// </summary>
        static long BytesPerFrame(IEnumerator routine, int warmupSteps = 3, int cap = 5000)
        {
            for (int i = 0; i < warmupSteps; i++) if (!routine.MoveNext()) return 0;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            long before = GC.GetTotalMemory(true);

            int steps = 0;
            while (routine.MoveNext()) { if (++steps > cap) break; }
            if (steps == 0) return 0;

            long after = GC.GetTotalMemory(false);
            return Math.Max(0, after - before) / steps;
        }

        [Test]
        public void Slide_TheLoopThePushRunsOn_AllocatesNothingPerFrame()
        {
            var go = new GameObject("slide", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            try
            {
                // The push's own loop, at its real duration. `yield return null` (not a fresh
                // WaitForSeconds), a struct Vector2 assignment, and no closure inside the body.
                long perFrame = BytesPerFrame((IEnumerator)Invoke("Slide", rt, 1170f, 0f, 0.25f, true));
                Assert.LessOrEqual(perFrame, 32L,
                    "the push's slide allocates " + perFrame + " B/frame — see A13's remedy list");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void Fade_TheLoopTheChromeCrossFadeRunsOn_AllocatesNothingPerFrame()
        {
            var go = new GameObject("fade", typeof(RectTransform), typeof(CanvasGroup));
            var cg = go.GetComponent<CanvasGroup>();
            try
            {
                long perFrame = BytesPerFrame((IEnumerator)Invoke("Fade", cg, 0f, 1f, 0.25f));
                Assert.LessOrEqual(perFrame, 32L, "the chrome fade allocates " + perFrame + " B/frame");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void Rise_TheEntryMotion_AllocatesNothingPerFrame()
        {
            var go = new GameObject("rise", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                long perFrame = BytesPerFrame((IEnumerator)Invoke(
                    "Rise", go.GetComponent<RectTransform>(), go.GetComponent<CanvasGroup>(), 16f, 0.25f));
                Assert.LessOrEqual(perFrame, 32L, "the entry rise allocates " + perFrame + " B/frame");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void Tween_TheBarFill_AllocatesNothingPerFrame()
        {
            float sink = 0f;
            Action<float> apply = v => sink = v;      // allocated ONCE, outside the loop
            long perFrame = BytesPerFrame((IEnumerator)Invoke("Tween", 0f, 100f, 0.4f, apply));
            Assert.LessOrEqual(perFrame, 32L, "the bar-fill tween allocates " + perFrame + " B/frame");
            Assert.AreEqual(100f, sink, 1e-4f);
        }

        [Test]
        public void CountUp_AllocatesOnlyWhenTheDrawnNumberChanges()
        {
            // The one loop that MUST allocate, and the reason it is bounded: a TMP assignment is
            // a string. It is gated on the INTEGER moving, so a 0.4 s count over 12 points writes
            // 12 strings and not 24 — one per distinct value drawn, never one per frame.
            var go = new GameObject("count", typeof(RectTransform));
            var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
            try
            {
                var e = (IEnumerator)Invoke("CountUp", tmp, 0, 12, 0.4f, "N0", null);
                var drawn = new System.Collections.Generic.HashSet<string>();
                int frames = 0;
                while (e.MoveNext()) { drawn.Add(tmp.text); frames++; }

                Assert.LessOrEqual(drawn.Count, 13, "more distinct strings than values counted");
                Assert.Greater(frames, drawn.Count,
                    "every frame drew a new value — the integer gate is not working");
                Assert.AreEqual("12", tmp.text);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // The GPS nav bar and the home indicator
    //
    // Added after the FIRST DEVICE PASS found what the Editor structurally
    // cannot: gps_polish §D9 inset the whole bar instead of only its content, so
    // on a phone with a home indicator the bar floated 102 px up the screen with
    // background showing underneath. At the 1170x2532 Editor reference
    // `Screen.safeArea` is the whole screen, the inset is zero, and every gate
    // saw a bar that had not moved.
    //
    // The geometry is now a pure function of three numbers, so the part that CAN
    // be pinned here is pinned here, and only "does iOS report the inset we think
    // it does" is left to the phone.
    // ═════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class GpsNavBarSafeAreaTests
    {
        static Type T => Probe.Type("Golfin.Gps.UI.GpsNavBarSafeArea");

        /// <summary>The authored bar, read off GpsHubScreen.prefab.</summary>
        const float BarHeight = 196f;

        /// <summary>The centre camera button, bottom-anchored at this y.</summary>
        const float CameraY = 155f;

        /// <summary>iPhone home indicator: 34 pt at @3x.</summary>
        const float Indicator = 102f;

        /// <summary>The four icon buttons are TOP-anchored 98 px below the top edge, 156 tall —
        /// so their lower edge sits (barHeight − 98 − 78) above the bar's bottom.</summary>
        const float IconCentreFromTop = 98f, IconSize = 156f;

        static (float height, float bottomChildY) For(float h, float y, float inset)
        {
            object l = T.GetMethod("For", BindingFlags.Public | BindingFlags.Static)!
                        .Invoke(null, new object[] { h, y, inset })!;
            Type lt = l.GetType();
            return ((float)lt.GetField("Height")!.GetValue(l)!,
                    (float)lt.GetField("BottomChildY")!.GetValue(l)!);
        }

        [Test]
        public void NoIndicator_LeavesTheAuthoredGeometryUntouched()
        {
            // The Editor case, and the reason this component cannot move a rest pixel there —
            // which is what keeps gps_polish's A2 0-px parity result valid.
            var (h, y) = For(BarHeight, CameraY, 0f);
            Assert.AreEqual(BarHeight, h, 1e-4f);
            Assert.AreEqual(CameraY, y, 1e-4f);
        }

        [Test]
        public void ANegativeInset_IsTreatedAsNone()
        {
            var (h, y) = For(BarHeight, CameraY, -50f);
            Assert.AreEqual(BarHeight, h, 1e-4f);
            Assert.AreEqual(CameraY, y, 1e-4f);
        }

        [Test]
        public void WithAnIndicator_TheBarGROWS_ItDoesNotMOVE()
        {
            // The whole point. The bar's pivot and anchor are both y = 0, so a taller bar extends
            // UPWARD and its bottom stays welded to the screen edge — the background still covers
            // the indicator instead of floating above it.
            var (h, _) = For(BarHeight, CameraY, Indicator);
            Assert.AreEqual(BarHeight + Indicator, h, 1e-4f,
                "the bar must absorb the inset as HEIGHT; anything else lifts it off the edge");
        }

        [Test]
        public void TheBottomAnchoredCameraButton_RidesUpWithTheIndicator()
        {
            // It is the one child anchored to the bottom, so it does not follow the rising top
            // edge the way the four icon buttons do and has to be moved explicitly.
            var (_, y) = For(BarHeight, CameraY, Indicator);
            Assert.AreEqual(CameraY + Indicator, y, 1e-4f);

            // And it must actually clear the indicator afterwards: centre 257, radius 119.
            Assert.Greater(y - 238f / 2f, Indicator,
                "the camera button's lower edge is still inside the home indicator");
        }

        [Test]
        public void TheTopAnchoredIconRow_ClearsTheIndicatorWithoutBeingTouched()
        {
            // The icons are anchored to the bar's TOP, so growing the bar lifts them for free.
            // Authored, their lower edge is 196 − 98 − 78 = 20 px above the bottom — inside a
            // 102 px indicator, which is the defect §D9 was trying to fix in the first place.
            float authoredIconBottom = BarHeight - IconCentreFromTop - IconSize / 2f;
            Assert.Less(authoredIconBottom, Indicator,
                "if this ever passes, the bar no longer needs a safe-area inset at all");

            var (h, _) = For(BarHeight, CameraY, Indicator);
            float insetIconBottom = h - IconCentreFromTop - IconSize / 2f;
            Assert.Greater(insetIconBottom, Indicator,
                "the icon row is still inside the home indicator after the inset");
            Assert.AreEqual(authoredIconBottom + Indicator, insetIconBottom, 1e-4f);
        }

        [Test]
        public void TheComponentIsRuntimeOnly_SoItCannotBakeGrownValuesIntoThePrefab()
        {
            // The trap this avoids: an [ExecuteAlways] version would rewrite the height in the
            // open prefab, a later save would serialise the GROWN value as the authored one, and
            // the next run would grow it again from there — cumulative asset drift.
            Assert.IsNull(T.GetCustomAttribute<ExecuteAlways>(),
                "GpsNavBarSafeArea must NOT be [ExecuteAlways] — see its header");
            Assert.IsNotNull(T.GetCustomAttribute<DisallowMultipleComponent>());
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // R1 / R5 · the cache-vs-fetch gate
    // ═════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class PaintGateTests
    {
        static Type T => Probe.Type("Golfin.Gps.UI.PaintGate");
        static Type K => Probe.Type("Golfin.Gps.UI.PaintKind");

        /// <summary>Constructed through the FULL parameter list, not the short one: C# default
        /// arguments are a call-site feature and `Activator.CreateInstance` does not fill them in,
        /// so a two-argument call stopped compiling-by-reflection the moment the gate grew its
        /// `staggers` flag (MissingMethodException, six tests, caught by the full sweep).</summary>
        object New(bool staggers = true)
            => Activator.CreateInstance(T, "[Test]", "site", staggers)!;

        object Kind(string name) => Enum.Parse(K, name);

        bool Should(object gate, string kind, int count)
            => (bool)T.GetMethod("Should")!.Invoke(gate, new object[] { Kind(kind), count })!;

        bool IsCold(object gate) => (bool)T.GetProperty("IsCold")!.GetValue(gate)!;

        void Rearm(object gate) => T.GetMethod("Rearm")!.Invoke(gate, null);

        [Test]
        public void AColdOpenIsColdUntilTheFetchLands()
        {
            object g = New();
            Rearm(g);
            Assert.IsTrue(IsCold(g), "a gate that has painted nothing is not cold");

            Assert.IsFalse(Should(g, "Cache", 0), "an empty cache paint must not stagger");
            Assert.IsTrue(IsCold(g), "an empty cache paint ended the cold state");

            Assert.IsTrue(Should(g, "Fetch", 3), "the first cold fetch paint did not stagger");
            Assert.IsFalse(IsCold(g), "the gate stayed cold after its fetch landed");
        }

        [Test]
        public void ACacheHitNeverStaggers_AndTheFetchBehindItDoesNotEither()
        {
            // The defect this closes: re-entering a screen re-animated rows that never left.
            object g = New();
            Rearm(g);
            Assert.IsFalse(Should(g, "Cache", 3));
            Assert.IsFalse(IsCold(g), "a cache hit is not a cold open");
            Assert.IsFalse(Should(g, "Fetch", 3), "the refresh behind a cache hit re-animated");
        }

        [Test]
        public void OnlyTheFIRSTColdFetchPaintStaggers()
        {
            // A second answer landing on a painted list must not re-flow rows the player is reading.
            object g = New();
            Rearm(g);
            Should(g, "Cache", 0);
            Assert.IsTrue(Should(g, "Fetch", 5));
            Assert.IsFalse(Should(g, "Fetch", 5));
        }

        [Test]
        public void AFailedFetchStillEndsTheColdState()
        {
            // Otherwise the shimmer sweeps forever over a list that is never coming — §D8 says it
            // hides in favour of the error/empty label.
            object g = New();
            Rearm(g);
            Should(g, "Cache", 0);
            Assert.IsFalse(Should(g, "Fetch", 0), "an empty fetch has nothing to stagger");
            Assert.IsFalse(IsCold(g), "an empty fetch left the placeholder up");
        }

        [Test]
        public void ARepaintIsNeitherAndChangesNothing()
        {
            // A language change or a filter switch repaints rows already on screen.
            object g = New();
            Rearm(g);
            Should(g, "Cache", 0);
            Assert.IsFalse(Should(g, "Repaint", 4));
            Assert.IsTrue(IsCold(g), "a repaint consumed the cold state the fetch still owns");
            Assert.IsTrue(Should(g, "Fetch", 4), "the real fetch lost its stagger to a repaint");
        }

        [Test]
        public void ASiteThatDoesNotStagger_StillReportsItsColdState()
        {
            // The gift catalog strip fades its panel but staggers nothing. It must still answer
            // "am I cold?" correctly — the panel reveal keys off exactly that.
            object g = New(staggers: false);
            Rearm(g);
            Assert.IsTrue(IsCold(g));
            Should(g, "Cache", 0);
            Assert.IsFalse(Should(g, "Fetch", 3), "a non-staggering site asked for a stagger");
            Assert.IsFalse(IsCold(g), "the fetch did not end the cold state");
        }

        [Test]
        public void RearmRestoresTheColdStateForTheNextScreenEntry()
        {
            object g = New();
            Rearm(g);
            Should(g, "Cache", 2);
            Assert.IsFalse(IsCold(g));

            Rearm(g);
            Assert.IsTrue(IsCold(g), "leaving and re-entering the screen did not re-arm the gate");
        }
    }
}
