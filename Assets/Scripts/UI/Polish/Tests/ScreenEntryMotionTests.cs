// ─────────────────────────────────────────────────────────────────────────────
// game_polish_a §D2 — the SkipEntry hand-off.
//
// ONE BIT, AND IT IS THE WHOLE FEATURE. If SkipEntry leaks past the screen that
// was pushed, the NEXT screen to arrive through the fade will not rise, and a
// screen that quietly stopped animating is not something a video review catches
// — it looks like the feature was never applied there. If it is never armed, a
// pushed screen rises 16 px from alpha 0 on the frame it finishes sliding, which
// reads as a stutter at the end of an otherwise continuous move.
//
// ASSEMBLY: the reflection arrangement of UiMotionTests — LayeredPush lives in
// Assembly-CSharp and a named test assembly cannot reference it.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Golfin.UI.Polish.Tests
{
    [TestFixture]
    public class ScreenEntryMotionTests
    {
        static Type T  => Probe.Type("Golfin.UI.Polish.LayeredPush");
        static Type SEM => Probe.Type("Golfin.UI.Polish.ScreenEntryMotion");

        /// <summary>Add ScreenEntryMotion and wire it, entirely through reflection — the test
        /// assembly cannot reference Assembly-CSharp (see the header).</summary>
        static Component AddMotion(GameObject go, RectTransform[] content)
        {
            Component c = go.AddComponent(SEM);
            SEM.GetMethod("SetContent")!.Invoke(c, new object[] { content });
            return c;
        }

        static int ContentCount(Component c)
        {
            var list = (System.Collections.IEnumerable)SEM.GetProperty("Content")!.GetValue(c)!;
            int n = 0; foreach (var _ in list) n++; return n;
        }

        static void Arm(bool v) =>
            T.GetMethod("ArmSkipEntry", BindingFlags.NonPublic | BindingFlags.Static)!
             .Invoke(null, new object[] { v });

        static bool Consume() =>
            (bool)T.GetMethod("ConsumeSkipEntry", BindingFlags.NonPublic | BindingFlags.Static)!
                   .Invoke(null, null)!;

        static bool Entering() => (bool)T.GetProperty("EnteringViaPush")!.GetValue(null)!;

        [TearDown]
        public void Disarm() => Arm(false);

        [Test]
        public void SkipEntry_IsFalseByDefault()
        {
            Arm(false);
            Assert.IsFalse(Entering(), "a screen reached through the fade must rise");
        }

        [Test]
        public void SkipEntry_IsConsumedExactlyOnce()
        {
            Arm(true);
            Assert.IsTrue(Consume(),  "the first reader sees the armed flag");
            Assert.IsFalse(Consume(), "the second reader must NOT — a leaked flag silently stops " +
                                      "the next fade-path arrival from rising");
        }

        /// <summary>
        /// The PROPERTY does not consume, and that is deliberate rather than an oversight.
        ///
        /// <para>A screen can carry more than one component whose OnEnable wants to know — the same
        /// reason GpsScreenTransition documents for arming around the SetActive rather than
        /// clearing on first read. A first-caller-wins flag would give the honest answer to
        /// whichever component happened to run first and lie to the rest.</para>
        /// </summary>
        [Test]
        public void EnteringViaPush_DoesNotConsume()
        {
            Arm(true);
            Assert.IsTrue(Entering());
            Assert.IsTrue(Entering(), "reading the property must not clear it");
            Assert.IsTrue(Entering());
        }

        [Test]
        public void ScreenEntryMotion_WithNoContent_DoesNothingAndDoesNotThrow()
        {
            var go = new GameObject("EmptyScreen", typeof(RectTransform));
            try
            {
                go.SetActive(false);
                Component m = AddMotion(go, new RectTransform[0]);
                Assert.DoesNotThrow(() => go.SetActive(true),
                    "a screen the builder has not wired must be inert, not broken");
                Assert.AreEqual(0, ContentCount(m));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        /// <summary>
        /// Rest parity, at the component level: enabling a wired screen OUTSIDE play mode must
        /// leave its content exactly where it was. UiMotion.Run settles immediately when
        /// Application.isPlaying is false, so the rise's final state — rest Y, alpha 1 — is what a
        /// builder- or test-driven activation gets. A2 asserts the same thing in pixels; this
        /// asserts it in numbers, where a 0.4 px drift is visible and a screenshot's is not.
        /// </summary>
        [Test]
        public void EnablingAWiredScreen_LeavesContentAtRest()
        {
            var go = new GameObject("Screen", typeof(RectTransform));
            try
            {
                go.SetActive(false);
                var content = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup));
                content.transform.SetParent(go.transform, false);
                var rt = (RectTransform)content.transform;
                rt.anchoredPosition = new Vector2(12f, 340f);
                var cg = content.GetComponent<CanvasGroup>();

                AddMotion(go, new[] { rt });

                go.SetActive(true);

                Assert.AreEqual(12f,  rt.anchoredPosition.x, 0.001f, "x must not move at all");
                Assert.AreEqual(340f, rt.anchoredPosition.y, 0.001f, "y must settle back on rest");
                Assert.AreEqual(1f,   cg.alpha,              0.001f, "alpha must settle at 1");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void PushedScreen_DoesNotRise()
        {
            var go = new GameObject("Screen", typeof(RectTransform));
            try
            {
                go.SetActive(false);
                var content = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup));
                content.transform.SetParent(go.transform, false);
                var rt = (RectTransform)content.transform;
                rt.anchoredPosition = new Vector2(0f, 100f);
                var cg = content.GetComponent<CanvasGroup>();
                cg.alpha = 0.42f;              // a value the rise would overwrite with 1

                AddMotion(go, new[] { rt });

                Arm(true);                     // as LayeredPush.Push does around its SetActive
                go.SetActive(true);

                Assert.AreEqual(0.42f, cg.alpha, 0.001f,
                    "a pushed screen must be left ALONE — the push owns its state, and touching it " +
                    "here is the stutter §D2 exists to prevent");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
