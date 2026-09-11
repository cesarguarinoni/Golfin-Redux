// ─────────────────────────────────────────────────────────────────────────────
// home_carousel_and_daily_timing — BannerSlotBinder's shift-down, re-measured.
//
// The Home mode carousel sat ON the Tee button on a Pro Max: the binder measured
// its drop once, at scene load, on the raw un-scaled canvas (2796 px tall) where
// the carousel's PROPORTIONAL anchors put it 60 px higher than on the scaled one
// (2536), and then applied that number forever. At 1170x2532 the two canvases
// coincide, so the Editor and an iPhone 14 never showed it.
//
// ASSEMBLY: BannerSlotBinder lives in Assembly-CSharp, reached by reflection —
// the same pattern as UiMotionTests next door.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Polish.Tests
{
    [TestFixture]
    public class BannerSlotShiftTests
    {
        static Type Binder => Probe.Type("Golfin.Banners.BannerSlotBinder");
        const BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;

        // Home's authored numbers (ShellScene): the section spans 0.2275→0.8764 of the screen
        // height with a −40 offset; the banner is 214 tall, pixel-anchored at y 298 from the
        // bottom; the drawn-edge trim is 2.
        const float RawHeight    = 2796f;   // a 15 Pro Max before CanvasScaler runs
        const float ScaledHeight = 2536f;   // the same phone after it
        const float Trim         = 2f;

        GameObject? _root;

        [TearDown]
        public void Cleanup() { if (_root != null) UnityEngine.Object.DestroyImmediate(_root); }

        (RectTransform parent, RectTransform slot, RectTransform target, Component binder) Build(float parentHeight)
        {
            _root = new GameObject("canvas", typeof(RectTransform), typeof(Canvas));
            var parent = new GameObject("HomeScreen", typeof(RectTransform)).GetComponent<RectTransform>();
            parent.SetParent(_root.transform, false);
            parent.anchorMin = parent.anchorMax = new Vector2(0.5f, 0.5f);
            parent.sizeDelta = new Vector2(1170f, parentHeight);

            var slot = new GameObject("PromoBanner", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            slot.SetParent(parent, false);
            slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 0f);
            slot.pivot = new Vector2(0.5f, 0f);
            slot.sizeDelta = new Vector2(970f, 214f);
            slot.anchoredPosition = new Vector2(0f, 298f);

            var target = new GameObject("ModeCarouselSection", typeof(RectTransform)).GetComponent<RectTransform>();
            target.SetParent(parent, false);
            target.anchorMin = new Vector2(0f, 0.22748815f);
            target.anchorMax = new Vector2(1f, 0.8763823f);
            target.pivot = new Vector2(0.5f, 0.5f);
            target.sizeDelta = Vector2.zero;
            target.anchoredPosition = new Vector2(0f, -40f);

            // Inactive so OnEnable (which needs BannerService) never runs; SetShiftedDown is
            // called directly, exactly as Hide() would.
            var host = new GameObject("binderHost", typeof(RectTransform));
            host.SetActive(false);
            host.transform.SetParent(parent, false);
            var binder = host.AddComponent(Binder);
            Binder.GetField("_image", Priv)!.SetValue(binder, slot.GetComponent<Image>());
            Binder.GetField("_shiftDownOnHide", Priv)!.SetValue(binder, new[] { target });
            Binder.GetField("_shiftDownTrim", Priv)!.SetValue(binder, Trim);
            return (parent, slot, target, binder);
        }

        static void Shift(Component binder, bool down)
            => Binder.GetMethod("SetShiftedDown", Priv)!.Invoke(binder, new object[] { down });

        static void Drain(System.Collections.IEnumerator e, int cap = 100000)
        {
            int steps = 0;
            while (e.MoveNext()) { if (++steps > cap) Assert.Fail("routine did not terminate"); }
        }

        static float Bottom(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return c[0].y;
        }

        [Test]
        public void ShiftDown_LandsTheTargetOnTheSlotBottomPlusTrim_OnTheScaledCanvas()
        {
            var (_, slot, target, binder) = Build(ScaledHeight);
            Shift(binder, true);
            Assert.AreEqual(Bottom(slot) + Trim, Bottom(target), 0.01f);
        }

        [Test]
        public void ShiftDown_ReMeasuresWhenTheCanvasChangesUnderIt()
        {
            // Frame 0: measured on the raw canvas. Then the scaler runs and the parent is 260 px
            // shorter — the proportional target has moved, the pixel-anchored slot has not. The
            // old cached number left the target 59 px low here (its bottom UNDER the Tee button).
            var (parent, slot, target, binder) = Build(RawHeight);
            Shift(binder, true);
            Assert.AreEqual(Bottom(slot) + Trim, Bottom(target), 0.01f, "raw canvas");

            parent.sizeDelta = new Vector2(1170f, ScaledHeight);
            Shift(binder, true);
            Assert.AreEqual(Bottom(slot) + Trim, Bottom(target), 0.01f, "scaled canvas, re-measured");
        }

        [Test]
        public void ShiftUp_RestoresTheAuthoredPosition()
        {
            var (_, _, target, binder) = Build(ScaledHeight);
            Shift(binder, true);
            Assert.AreNotEqual(-40f, target.anchoredPosition.y);
            Shift(binder, false);
            Assert.AreEqual(-40f, target.anchoredPosition.y, 0.01f);
        }

        [Test]
        public void ShiftDown_MeasuresAtRestWhileBothRectsAreRising()
        {
            // Home's entry rise holds the slot AND the target 16 px low in the OnEnable that
            // hides the banner. The drop must come out the same as at rest, and the base the
            // binder keeps for Show() must be the authored −40, not −56.
            var (_, slot, target, binder) = Build(ScaledHeight);
            var uiMotion = Probe.Type("Golfin.UI.Polish.UiMotion");
            var rise = uiMotion.GetMethod("Rise")!;
            object outCubic = Enum.ToObject(Probe.Type("Golfin.UI.Polish.Ease"), 0);
            var rs = (System.Collections.IEnumerator)rise.Invoke(null, new object?[] { slot, null, 16f, 0.25f, outCubic })!;
            var rtg = (System.Collections.IEnumerator)rise.Invoke(null, new object?[] { target, null, 16f, 0.25f, outCubic })!;
            rs.MoveNext(); rtg.MoveNext();
            Assert.Less(slot.anchoredPosition.y, 298f, "slot is mid-rise");
            Assert.Less(target.anchoredPosition.y, -40f, "target is mid-rise");

            Shift(binder, true);
            // Let both rises finish: the slot returns to its rest, the target lands on the shift.
            Drain(rs);
            Drain(rtg);

            Assert.AreEqual(298f, slot.anchoredPosition.y, 0.01f);
            Assert.AreEqual(Bottom(slot) + Trim, Bottom(target), 0.01f, "the drop was measured at rest");

            Shift(binder, false);
            Assert.AreEqual(-40f, target.anchoredPosition.y, 0.01f, "the base was captured at rest");
        }
    }
}
