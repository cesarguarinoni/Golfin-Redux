// ─────────────────────────────────────────────────────────────────────────────
// game_polish_b §D1 — every game modal pops, and it stays that way.
//
// The flag is set by an authoring pass (GamePolishBuilder.ApplyModals), which means it
// can be un-set by anything: a prefab reverted, a scene merge resolved the wrong way, a
// modal rebuilt by a later task from a template that predates this one. None of those
// produce an error — they produce a modal that quietly snaps while fourteen others pop,
// which is precisely the class of defect nobody notices until Cesar does.
//
// So this reads the SHIPPED ASSETS. Not the builder's report, not a list this file keeps
// in step by hand: the eight prefabs off disk, and ShellScene's own text for the seven
// authored into the scene.
//
// It also pins the two things §D1 depends on that are NOT the flag: the code default is
// still false (GpsScreenTransitionTests owns that too, and §D1.1 says do not touch it),
// and HoleCompleteWidget still carries its own pop — the modal whose controller overrides
// Show() as a no-op, so the flag alone would leave the most-seen result screen snapping.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Golfin.UI.Polish.Tests
{
    [TestFixture]
    public class ModalPopTests
    {
        const string SceneAsset = "Assets/Scenes/ShellScene.unity";

        /// <summary>The eight modals whose flag lives on a prefab asset. Mirrors
        /// GamePolishBuilder.ModalPrefabs — and the count test below fails if that list grows
        /// without this one, so the two cannot silently diverge.</summary>
        static readonly string[] Prefabs =
        {
            "Assets/Prefabs/UI/Modals/GachaRatesModal.prefab",
            "Assets/Prefabs/UI/Modals/GachaRevealModal.prefab",
            "Assets/Prefabs/UI/Modals/HoleCompleteModal.prefab",
            "Assets/Prefabs/UI/Modals/InGameSettingsModal.prefab",
            "Assets/Prefabs/UI/Modals/SchemeConfirmModal.prefab",
            "Assets/Prefabs/UI/Modals/StartingCharacterConfirmModal.prefab",
            "Assets/Prefabs/UI/Modals/TournamentResultModal.prefab",
            "Assets/Prefabs/UI/Modals/TournamentSignupModal.prefab",
        };

        static Type ModalT => Probe.Type("Golfin.UI.Modals.ModalController");

        // ═════════════════════════════════════════════════════════════════════
        // The eight prefab assets
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void EveryModalPrefab_Pops()
        {
            var flat = new List<string>();
            foreach (string path in Prefabs)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.NotNull(go, "modal prefab missing: " + path);

                Component? modal = null;
                foreach (Component c in go!.GetComponentsInChildren(ModalT, true)) { modal = c; break; }
                Assert.NotNull(modal, "no ModalController in " + path);

                var so = new SerializedObject(modal);
                SerializedProperty? p = so.FindProperty("animateShow");
                Assert.NotNull(p, "ModalController.animateShow was renamed — GamePolishBuilder is now a no-op");
                if (!p!.boolValue) flat.Add(Path.GetFileNameWithoutExtension(path));
            }
            CollectionAssert.IsEmpty(flat,
                "these modals snap instead of popping — re-run GOLFIN/Game Polish/Apply — modals pop: "
                + string.Join(", ", flat));
        }

        // ═════════════════════════════════════════════════════════════════════
        // The seven authored into ShellScene
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void NoModalInShellScene_IsLeftFlat()
        {
            // Read the scene's TEXT rather than opening it. Opening ShellScene in a test costs
            // seconds and mutates the Editor's open-scene state for everything that runs after;
            // the flag is one serialized line and the question — "is any of them still 0?" — is
            // answerable without loading a single GameObject.
            Assert.IsTrue(File.Exists(SceneAsset), SceneAsset + " not found");
            string[] lines = File.ReadAllLines(SceneAsset);

            var flat = new List<int>();
            int seen = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].Trim();
                if (!t.StartsWith("animateShow:")) continue;
                seen++;
                if (t.EndsWith("0")) flat.Add(i + 1);
            }

            Assert.Greater(seen, 0,
                "ShellScene carries no animateShow line at all — either the field was renamed or "
                + "the scene lost the modals this task flagged");
            CollectionAssert.IsEmpty(flat,
                "ShellScene has modals with animateShow: 0 at line(s) "
                + string.Join(", ", flat) + " — re-run GOLFIN/Game Polish/Apply — modals pop");
        }

        // ═════════════════════════════════════════════════════════════════════
        // The two things §D1 needs that are not the flag
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void TheCodeDefault_IsStillFalse()
        {
            // §D1.1: every modal in the game inherits ModalController, and a default of true
            // would put new motion on modals no task has looked at. The flag is an AUTHORING
            // decision, per modal, and it has to stay one.
            var go = new GameObject("DefaultCheck");
            go.SetActive(false);
            Component modal = go.AddComponent(ModalT);
            var so = new SerializedObject(modal);
            Assert.IsFalse(so.FindProperty("animateShow")!.boolValue,
                "ModalController.animateShow must default to FALSE");
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void HoleCompleteWidget_StillCarriesItsOwnPop()
        {
            // HoleCompleteModalController.Show() is a no-op — HoleCompleteWidget owns visibility
            // through _root.SetActive — so the flag alone does nothing for it. If these members
            // disappear, the most-seen result screen in the game goes back to snapping and no
            // other test in the suite would notice.
            Type w = Probe.Type("Golfin.Gameplay.UI.ShotUI.HoleCompleteWidget");
            const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;
            foreach (string member in new[] { "StartPop", "StartUnpop", "PopRoutine", "UnpopRoutine", "HideNow" })
                Assert.NotNull(w.GetMethod(member, Priv),
                    $"HoleCompleteWidget.{member} is gone — §D1.3's pop went with it");
        }

        [Test]
        public void HoleCompleteWidget_UsesTheSameConstantsAsUiMotion()
        {
            // The curve is COPIED, not called, because Golfin.Gameplay.UI cannot reference
            // Assembly-CSharp (the wall SelectorOverlayWidget documents). A copy drifts unless
            // something compares it, so: compare it.
            Type w = Probe.Type("Golfin.Gameplay.UI.ShotUI.HoleCompleteWidget");
            Type m = Probe.Type("Golfin.UI.Polish.UiMotion");

            float Widget(string n) => Convert.ToSingle(
                w.GetField(n, BindingFlags.NonPublic | BindingFlags.Static)!.GetRawConstantValue());
            float Motion(string n) => Convert.ToSingle(m.GetField(n)!.GetRawConstantValue());

            Assert.AreEqual(Motion("PopDur"), Widget("PopDur"), 1e-6f,
                "HoleCompleteWidget.PopDur has drifted from UiMotion.PopDur");
            Assert.AreEqual(Motion("FadeDur"), Widget("FadeDur"), 1e-6f,
                "HoleCompleteWidget.FadeDur has drifted from UiMotion.FadeDur");
            Assert.AreEqual(0.9f, Widget("PopFromScale"), 1e-6f, "UiMotion.Pop starts at 0.9");
            Assert.AreEqual(0.95f, Widget("UnpopToScale"), 1e-6f, "UiMotion.Unpop ends at 0.95");
        }

        [Test]
        public void HoleCompleteWidget_EaseMatchesUiMotion_PointForPoint()
        {
            Type w = Probe.Type("Golfin.Gameplay.UI.ShotUI.HoleCompleteWidget");
            Type m = Probe.Type("Golfin.UI.Polish.UiMotion");
            const BindingFlags S = BindingFlags.NonPublic | BindingFlags.Static;

            MethodInfo wOut = w.GetMethod("EaseOut", S)!, wIn = w.GetMethod("EaseIn", S)!;
            MethodInfo mOut = m.GetMethod("EaseOut")!,     mIn = m.GetMethod("EaseIn")!;

            for (float t = 0f; t <= 1.0001f; t += 0.05f)
            {
                Assert.AreEqual((float)mOut.Invoke(null, new object[] { t })!,
                                (float)wOut.Invoke(null, new object[] { t })!, 1e-6f, "EaseOut t=" + t);
                Assert.AreEqual((float)mIn.Invoke(null, new object[] { t })!,
                                (float)wIn.Invoke(null, new object[] { t })!, 1e-6f, "EaseIn t=" + t);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // The list above cannot drift from the builder's
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void ThisSuiteCoversEveryPrefabTheBuilderTouches()
        {
            Type b = Probe.Type("Golfin.UI.Polish.EditorTools.GamePolishBuilder");
            var field = b.GetField("ModalPrefabs", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(field, "GamePolishBuilder.ModalPrefabs is gone");
            var builderList = (string[])field!.GetValue(null)!;
            CollectionAssert.AreEquivalent(builderList, Prefabs,
                "GamePolishBuilder.ModalPrefabs and ModalPopTests.Prefabs have diverged — a modal "
                + "the builder flags but this suite never checks is a modal that can silently revert");
        }
    }
}
