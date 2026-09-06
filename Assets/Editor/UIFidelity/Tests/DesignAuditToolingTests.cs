// Assets/Tests/EditMode/DesignAuditToolingTests.cs
// design_consistency_audit — the two instruments the whole audit rests on.
//
// WHY THESE EXIST. Every finding in DESIGN_CONSISTENCY_AUDIT.md is argued from either
// (a) UIFidelityLinter output or (b) a DesignAuditDumper JSON. If the LintRoot overload
// drifts from LintPrefab, half the screens are measured by a different ruler than the
// other half; if the rendered-px formula is wrong, EVERY size finding is wrong in the
// same direction and looks perfectly consistent while being uniformly false.
//
// ASSEMBLY: GolfinRedux.Tests.EditMode. Both instruments live in Assembly-CSharp-Editor,
// which an asmdef CANNOT reference, so every production call goes through reflection —
// the pattern of GachaClientRealPullTests, and for the same reason
// (feedback_tests_must_target_production_type: the seam under test is the SHIPPING one).
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class DesignAuditToolingTests
    {
        const BindingFlags Statics =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        static Type Find(string name) =>
            AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.Name == name);

        static Type DumperType => Find("DesignAuditDumper");
        static Type LinterType => Find("UIFidelityLinter");

        GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            _root = null;
        }

        [Test]
        public void Production_types_and_members_exist()
        {
            // Without this, every test below could pass vacuously by never reaching production code.
            Assert.IsNotNull(DumperType, "DesignAuditDumper not found");
            Assert.IsNotNull(LinterType, "UIFidelityLinter not found");
            Assert.IsNotNull(DumperType.GetMethod("RenderedPx", Statics), "RenderedPx missing");
            Assert.IsNotNull(DumperType.GetMethod("IsDefaultFont", Statics), "IsDefaultFont missing");
            var dump = DumperType.GetMethod("Dump", Statics);
            Assert.IsNotNull(dump, "Dump missing");
            // Pin the ARITY. Adding a parameter to Dump silently breaks every reflection call in
            // this fixture with TargetParameterCountException — which is exactly what happened when
            // `via` was added. Failing here names the cause instead of three unrelated-looking tests.
            Assert.AreEqual(4, dump.GetParameters().Length,
                "Dump's signature changed — update this fixture's Invoke calls to match");
            Assert.IsNotNull(LinterType.GetMethod("LintRoot", Statics), "LintRoot overload missing");
            Assert.IsNotNull(LinterType.GetMethod("LintPrefab", Statics), "LintPrefab missing");
        }

        // ── the rendered-px formula ──────────────────────────────────────────

        /// <summary>Canvas → parent(scale s) → TMP(fontSize f). Rendered px must be f × s.</summary>
        TextMeshProUGUI BuildNested(float parentScale, float fontSize)
        {
            _root = new GameObject("cv", typeof(Canvas), typeof(CanvasScaler));
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)_root.transform).sizeDelta = new Vector2(1170, 2532);

            var parent = new GameObject("scaled", typeof(RectTransform));
            parent.transform.SetParent(_root.transform, false);
            parent.transform.localScale = Vector3.one * parentScale;

            var labelGO = new GameObject("label", typeof(RectTransform));
            labelGO.transform.SetParent(parent.transform, false);
            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.enableAutoSizing = false;      // pin it: autosize would make fontSize a RESULT
            tmp.fontSize = fontSize;
            tmp.text = "measure me";
            return tmp;
        }

        static float RenderedPx(TextMeshProUGUI tmp, float scaleFactor) =>
            (float)DumperType.GetMethod("RenderedPx", Statics).Invoke(null, new object[] { tmp, scaleFactor });

        [Test]
        public void RenderedPx_HalvesUnderAHalfScaleParent()
        {
            // THE defect this pins: judging a size from the serialized number. A 40 under a 0.5
            // parent is a 20 on screen, and the audit's F/H dimensions would call it correct.
            var tmp = BuildNested(0.5f, 40f);
            Assert.AreEqual(20f, RenderedPx(tmp, 1f), 0.001f,
                            "a 40px label under a 0.5-scale parent renders at 20px");
        }

        [Test]
        public void RenderedPx_IsUnchangedAtUnitScale()
        {
            var tmp = BuildNested(1f, 45f);
            Assert.AreEqual(45f, RenderedPx(tmp, 1f), 0.001f);
        }

        [Test]
        public void RenderedPx_DividesOutTheCanvasScaleFactor()
        {
            // scaleFactor is what maps the design canvas onto the device; dividing it out is what
            // leaves the number in 1170x2532 design px, where a Figma px IS a Unity px.
            var tmp = BuildNested(1f, 66f);
            Assert.AreEqual(33f, RenderedPx(tmp, 2f), 0.001f);
        }

        [Test]
        public void RenderedPx_TreatsANonPositiveScaleFactorAsOne()
        {
            // A canvas reporting scaleFactor 0 must not turn every size finding into Infinity.
            var tmp = BuildNested(1f, 30f);
            Assert.AreEqual(30f, RenderedPx(tmp, 0f), 0.001f);
        }

        // ── default-font detection (shape i) ─────────────────────────────────

        [Test]
        public void IsDefaultFont_IsTrueOnlyForLiberationSans()
        {
            var tmp = BuildNested(1f, 30f);
            var m = DumperType.GetMethod("IsDefaultFont", Statics);

            // TMP's default on a fresh label is LiberationSans SDF unless the project overrides it;
            // assert against the NAME the dumper matches rather than assuming the default.
            string constName = (string)DumperType.GetField("DefaultFontName", Statics).GetValue(null);
            Assert.AreEqual("LiberationSans SDF", constName,
                            "the audit's shape (i) hunts for this exact asset name");

            bool actual = (bool)m.Invoke(null, new object[] { tmp });
            bool expected = tmp.font != null && tmp.font.name == constName;
            Assert.AreEqual(expected, actual, "IsDefaultFont must agree with the font asset's name");
        }

        [Test]
        public void IsDefaultFont_IsFalseForANullLabel()
        {
            var m = DumperType.GetMethod("IsDefaultFont", Statics);
            Assert.IsFalse((bool)m.Invoke(null, new object[] { null }));
        }

        // ── Outline / Shadow sibling detection (shapes iv, v) ────────────────

        [Test]
        public void Dump_CountsOutlineAndShadowSiblingsWithoutDoubleCounting()
        {
            // uGUI's Outline DERIVES from Shadow. A naive GetComponent<Shadow>() therefore counts an
            // Outline twice — once as a border defect and once as a fake drop shadow — and shapes
            // (iv) and (v) would both be inflated by the same objects.
            var tmp = BuildNested(1f, 30f);
            tmp.gameObject.AddComponent<Outline>();

            var img = new GameObject("img", typeof(RectTransform)).AddComponent<Image>();
            img.transform.SetParent(_root.transform, false);
            img.gameObject.AddComponent<Shadow>();

            string outPath = (string)DumperType.GetMethod("Dump", Statics)
                .Invoke(null, new object[] { _root, "UNITTEST_outline_shadow", "test", "unit-test" });
            Assert.IsTrue(System.IO.File.Exists(outPath), "Dump wrote no file: " + outPath);

            string json = System.IO.File.ReadAllText(outPath);
            StringAssert.Contains("\"outlineComponents\":1", json,
                                  "the Outline should be counted once, as an outline");
            StringAssert.Contains("\"shadowComponents\":1", json,
                                  "the Shadow should be counted once, and the Outline NOT counted again");
            System.IO.File.Delete(outPath);
        }

        [Test]
        public void Dump_IncludesInactiveChildrenTaggedActiveFalse()
        {
            // Hidden Inventory tabs and Settings submenus are exactly where an unreplaced default
            // font survives unnoticed; excluding them would hide the findings the audit is for.
            var tmp = BuildNested(1f, 30f);
            tmp.gameObject.SetActive(false);

            string outPath = (string)DumperType.GetMethod("Dump", Statics)
                .Invoke(null, new object[] { _root, "UNITTEST_inactive", "test", "unit-test" });
            string json = System.IO.File.ReadAllText(outPath);

            StringAssert.Contains("\"active\":false", json, "an inactive label must still be dumped");
            StringAssert.Contains("\"tmp\":1", json, "and must still be counted");
            System.IO.File.Delete(outPath);
        }

        [Test]
        public void Dump_OnANullRoot_ReturnsAnErrorInsteadOfThrowing()
        {
            string r = (string)DumperType.GetMethod("Dump", Statics)
                .Invoke(null, new object[] { null, "UNITTEST_null", null, null });
            StringAssert.StartsWith("ERROR", r);
        }

        // ── LintRoot / LintPrefab parity (A9) ────────────────────────────────

        [Test]
        public void LintRoot_ProducesTheSameFindingsAsLintPrefab_OnTheSamePrefab()
        {
            const string prefabPath = "Assets/Prefabs/UI/Shop/GeneralShopCard.prefab";
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) Assert.Ignore("prefab not present: " + prefabPath);

            string viaPrefab = (string)LinterType.GetMethod("LintPrefab", Statics)
                .Invoke(null, new object[] { prefabPath, null });

            // Rebuild LintPrefab's OWN harness, or the two are not comparable: it lints under a
            // WorldSpace canvas sized to the prefab (falling back to 1170x2532 for stretch roots),
            // and layout-driven findings depend on that.
            _root = new GameObject("__PARITY__");
            var canvasGO = new GameObject("cv", typeof(Canvas), typeof(CanvasScaler));
            canvasGO.transform.SetParent(_root.transform, false);
            canvasGO.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var prt = prefab.GetComponent<RectTransform>();
            var size = prt != null ? prt.sizeDelta : Vector2.zero;
            if (size.x < 10f || size.y < 10f) size = new Vector2(1170, 2532);
            ((RectTransform)canvasGO.transform).sizeDelta = size;
            var inst = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, canvasGO.transform);
            Canvas.ForceUpdateCanvases();

            string viaRoot = (string)LinterType.GetMethod("LintRoot", Statics)
                .Invoke(null, new object[] { inst, prefabPath, null });

            Assert.AreEqual(viaPrefab, viaRoot,
                "LintRoot and LintPrefab must run the same three layers and report identically — " +
                "if these diverge, ShellScene-hosted screens are measured by a different ruler " +
                "than prefab-hosted ones and no cross-screen shape table means anything.");
        }

        [Test]
        public void LintRoot_OnANullRoot_ReturnsAnErrorInsteadOfThrowing()
        {
            string r = (string)LinterType.GetMethod("LintRoot", Statics)
                .Invoke(null, new object[] { null, "UNITTEST_null_root", null });
            StringAssert.StartsWith("ROOT IS NULL", r);
        }
    }
}
