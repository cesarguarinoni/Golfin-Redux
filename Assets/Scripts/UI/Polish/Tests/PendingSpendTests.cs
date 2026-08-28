// ─────────────────────────────────────────────────────────────────────────────
// transaction_feedback §6 — Golfin.UI.Polish.PendingSpend, the shared "waiting on
// the server" affordance.
//
// ASSEMBLY: Golfin.UI.Polish.Tests (named EditMode asmdef). PendingSpend lives in
// Assembly-CSharp — Assets/Scripts/UI has no .asmdef, which is exactly what the
// spec asked for ("same asmdef as ButtonPressFeedback") — and a named assembly
// cannot reference a predefined one, so the type is reached by REFLECTION. Same
// pattern Golfin.EconomyRuntime.Tests uses to reach RewardPointsManager next door.
//
// WHAT THESE GUARD: the restore. A pending scope that fails to restore leaves a
// dead button the player cannot retry from — strictly worse than the invisible
// round-trip this feature replaced. So: restore on the happy path, restore when
// the callback THROWS, and never restore twice over an answer that has since
// been written (the shop's OWNED chip is set after the scope is disposed).
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Polish.Tests
{
    [TestFixture]
    public class PendingSpendTests
    {
        private const string TypeName = "Golfin.UI.Polish.PendingSpend";

        private static Type ResolveType()
        {
            Assembly asm = AppDomain.CurrentDomain.GetAssemblies()
                .First(a => a.GetName().Name == "Assembly-CSharp");
            Type? t = asm.GetType(TypeName);
            if (t == null) throw new InvalidOperationException($"Type '{TypeName}' not found in Assembly-CSharp");
            return t;
        }

        private static IDisposable Begin(Button? button, TMP_Text? label = null, params Button[] alsoDisable)
        {
            Type t = ResolveType();
            MethodInfo begin = t.GetMethod("Begin", BindingFlags.Public | BindingFlags.Static)!;
            object scope = begin.Invoke(null, new object?[] { button, label, alsoDisable ?? Array.Empty<Button>() })!;
            return (IDisposable)scope;
        }

        private static string PendingLabel =>
            (string)ResolveType().GetField("PendingLabel", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;

        // ── Fixtures ────────────────────────────────────────────────────────────

        private GameObject _root = null!;

        private Button MakeButton(string name, bool interactable = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(_root.transform, false);
            var btn = go.GetComponent<Button>();
            btn.interactable = interactable;
            return btn;
        }

        private TMP_Text MakeLabel(string name, string text)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_root.transform, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            return tmp;
        }

        [SetUp]
        public void SetUp() => _root = new GameObject("PendingSpendTests_Root", typeof(RectTransform));

        [TearDown]
        public void TearDown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        }

        // ── Tests ───────────────────────────────────────────────────────────────

        [Test]
        public void Begin_DisablesTheButtonAndShowsTheEllipsis()
        {
            Button btn = MakeButton("Buy");
            TMP_Text label = MakeLabel("BuyLabel", "BUY");

            IDisposable scope = Begin(btn, label);

            Assert.IsFalse(btn.interactable, "Begin must disable the tapped button — the Disabled tint IS the affordance.");
            Assert.AreEqual(PendingLabel, label.text, "Begin must replace the label with the pending ellipsis.");

            scope.Dispose();
        }

        [Test]
        public void Dispose_RestoresInteractableAndLabel()
        {
            Button btn = MakeButton("Buy");
            TMP_Text label = MakeLabel("BuyLabel", "BUY");

            IDisposable scope = Begin(btn, label);
            scope.Dispose();

            Assert.IsTrue(btn.interactable, "Dispose must hand the button back.");
            Assert.AreEqual("BUY", label.text, "Dispose must put the original label text back.");
        }

        [Test]
        public void Dispose_RestoresAnAlreadyDisabledButtonToDisabled()
        {
            // The stamina row's BUY is disabled when stamina is full. Restoring must return the
            // CACHED state, not a blanket "true" that would hand back a button the screen had
            // deliberately shut.
            Button btn = MakeButton("Buy", interactable: false);

            IDisposable scope = Begin(btn);
            Assert.IsFalse(btn.interactable);
            scope.Dispose();

            Assert.IsFalse(btn.interactable, "Dispose must restore the cached state, not force interactable = true.");
        }

        [Test]
        public void AlsoDisable_IsDisabledAndRestoredWithTheMainButton()
        {
            Button confirm = MakeButton("Confirm");
            Button cancel  = MakeButton("Cancel");

            IDisposable scope = Begin(confirm, null, cancel);
            Assert.IsFalse(cancel.interactable, "A modal's CANCEL must be locked for the duration of the spend.");

            scope.Dispose();
            Assert.IsTrue(cancel.interactable, "Dispose must hand CANCEL back too.");
        }

        [Test]
        public void TheSameButtonPassedTwice_IsStillRestored()
        {
            // A prefab variant can wire the same component as both the PLAY button and the card's
            // tap surface. Caching that control's state twice must yield its PRE-TAP value both
            // times — a one-pass read-then-write would record the second as "already disabled" and
            // restore it disabled, killing the button the scope exists to hand back.
            Button btn = MakeButton("Play");

            IDisposable scope = Begin(btn, null, btn);
            Assert.IsFalse(btn.interactable);

            scope.Dispose();
            Assert.IsTrue(btn.interactable, "A control listed twice must still come back enabled.");
        }

        [Test]
        public void DoubleDispose_IsANoOp()
        {
            // The call sites dispose FIRST and then write the answer (OWNED + disabled). A second
            // Dispose that restored again would undo that answer.
            Button btn = MakeButton("Buy");
            TMP_Text label = MakeLabel("BuyLabel", "BUY");

            IDisposable scope = Begin(btn, label);
            scope.Dispose();

            // The result handler's write, exactly as GeneralShopScreenController does it.
            btn.interactable = false;
            label.text = "OWNED";

            scope.Dispose();

            Assert.IsFalse(btn.interactable, "A second Dispose must not re-enable a button the answer disabled.");
            Assert.AreEqual("OWNED", label.text, "A second Dispose must not overwrite the answer's label.");
        }

        [Test]
        public void ExceptionInsideTheScope_StillRestores()
        {
            Button btn = MakeButton("Buy");
            TMP_Text label = MakeLabel("BuyLabel", "BUY");

            Assert.Throws<InvalidOperationException>(() =>
            {
                using (Begin(btn, label))
                {
                    throw new InvalidOperationException("callback blew up");
                }
            });

            Assert.IsTrue(btn.interactable, "A throwing callback must still leave the button usable.");
            Assert.AreEqual("BUY", label.text, "A throwing callback must still leave the label readable.");
        }

        [Test]
        public void NullButton_IsTolerated()
        {
            // An unwired reference must not turn a working purchase into a NullReferenceException.
            Assert.DoesNotThrow(() =>
            {
                IDisposable scope = Begin(null, null);
                scope.Dispose();
            });
        }

        [Test]
        public void DestroyedButton_IsToleratedOnDispose()
        {
            // The shop's price_changed arm Rebuilds the grid, which destroys the tapped card.
            Button btn = MakeButton("Buy");
            IDisposable scope = Begin(btn);

            UnityEngine.Object.DestroyImmediate(btn.gameObject);

            Assert.DoesNotThrow(() => scope.Dispose(),
                "Dispose must survive a control destroyed while the spend was in flight.");
        }
    }
}
