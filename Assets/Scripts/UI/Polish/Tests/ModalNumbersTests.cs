// ─────────────────────────────────────────────────────────────────────────────
// game_polish_b §D3 — the two rules the level-up panels depend on.
//
// Not "does the bar animate" — that is visible in the clip. These pin the two properties
// whose absence is a BUG rather than a missing flourish:
//
//   1. THE FIRST PAINT OF AN OPEN SNAPS. RefreshDisplay runs when the modal opens; animating
//      there would fill every bar from zero on arrival, a loading animation over data that was
//      already correct.
//   2. A BAR ALWAYS ENDS ON ITS EXACT VALUE. A stat bar stranded mid-tween is a WRONG STAT.
//      Interruption is the normal case on this panel — it redraws on every [+] tap — so the
//      settle is tested by interrupting deliberately, not by letting a tween finish.
//
// EditMode note: UiMotion.Run settles immediately outside play mode rather than starting a
// coroutine, which is exactly the fallback these assertions care about. What cannot be observed
// here is the intermediate motion, and it is not claimed.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Polish.Tests
{
    [TestFixture]
    public class ModalNumbersTests
    {
        static Type T => Probe.Type("Golfin.UI.Polish.ModalNumbers");

        sealed class Host : MonoBehaviour { }

        static object New(out GameObject go)
        {
            go = new GameObject("ModalNumbersHost");
            MonoBehaviour host = go.AddComponent<Host>();
            // The ctor is invoked EXPLICITLY rather than through Activator.CreateInstance(T, host):
            // that overload takes `params object[]`, and handing it a single UnityEngine.Object
            // resolved to the no-arg form, which threw "Default constructor not found" for a type
            // that deliberately has none.
            ConstructorInfo ctor = T.GetConstructor(new[] { typeof(MonoBehaviour) })!;
            Assert.NotNull(ctor, "ModalNumbers(MonoBehaviour) is gone");
            return ctor.Invoke(new object[] { host });
        }

        static void Painted(object n, bool v) => T.GetProperty("Painted")!.SetValue(n, v);
        static void Bar(object n, Image b, float to) => T.GetMethod("Bar")!.Invoke(n, new object[] { b, to });
        static void Number(object n, TMP_Text l, int to) => T.GetMethod("Number")!.Invoke(n, new object[] { l, to });
        static void BeginOpen(object n) => T.GetMethod("BeginOpen")!.Invoke(n, null);
        static void StopAll(object n) => T.GetMethod("StopAll")!.Invoke(n, null);

        static Image NewBar(GameObject parent, float start)
        {
            var go = new GameObject("Bar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent.transform);
            var img = go.GetComponent<Image>();
            img.type = Image.Type.Filled;
            img.fillAmount = start;
            return img;
        }

        [Test]
        public void FirstPaintOfAnOpen_Snaps()
        {
            object n = New(out GameObject go);
            Image bar = NewBar(go, 0f);
            BeginOpen(n);                       // Painted = false
            Bar(n, bar, 0.75f);
            Assert.AreEqual(0.75f, bar.fillAmount, 1e-5f,
                "the first paint of an open must land on the value, not animate toward it");
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void EveryBar_EndsOnItsExactValue()
        {
            object n = New(out GameObject go);
            Image bar = NewBar(go, 0.2f);
            Painted(n, true);
            Bar(n, bar, 0.9f);
            Assert.AreEqual(0.9f, bar.fillAmount, 1e-5f, "a bar must settle on its exact value");
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void AnInterruptedBar_StillEndsExact_WhichIsTheWholePoint()
        {
            // Four [+] taps in quick succession is the normal way this panel is used.
            object n = New(out GameObject go);
            Image bar = NewBar(go, 0f);
            Painted(n, true);
            foreach (float v in new[] { 0.2f, 0.4f, 0.6f, 0.8f }) Bar(n, bar, v);
            Assert.AreEqual(0.8f, bar.fillAmount, 1e-5f,
                "a bar interrupted three times must still end on the LAST value asked for — " +
                "anything else is a wrong stat left on screen");
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void BarsAreClamped_SoACapOfZeroCannotDriveThemOutOfRange()
        {
            object n = New(out GameObject go);
            Image bar = NewBar(go, 0.5f);
            Painted(n, true);
            Bar(n, bar, 4f);
            Assert.AreEqual(1f, bar.fillAmount, 1e-5f);
            Bar(n, bar, -2f);
            Assert.AreEqual(0f, bar.fillAmount, 1e-5f);
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void Numbers_SnapOnFirstPaint_AndSettleExactAfterwards()
        {
            object n = New(out GameObject go);
            var label = new GameObject("N").AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(go.transform);

            BeginOpen(n);
            Number(n, label, 11);
            Assert.AreEqual("11", label.text, "first paint must snap");

            Painted(n, true);
            Number(n, label, 14);
            Assert.AreEqual("14", label.text, "and a change must settle on the exact value");
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void StopAll_LeavesEverythingOnItsFinalValue()
        {
            // Called when the modal closes. A bar left mid-tween would be the state the NEXT open
            // starts from, which is how a wrong stat survives a close.
            object n = New(out GameObject go);
            Image bar = NewBar(go, 0f);
            Painted(n, true);
            Bar(n, bar, 0.65f);
            StopAll(n);
            Assert.AreEqual(0.65f, bar.fillAmount, 1e-5f);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
