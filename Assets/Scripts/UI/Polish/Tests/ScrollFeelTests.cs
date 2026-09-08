// ─────────────────────────────────────────────────────────────────────────────
// game_polish_c §C2 — one scroll feel, pinned.
//
// The five fields are one decision, taken once in gps_polish §D9 and now applied
// to the game side: Elastic / 0.1 / inertia on / 0.135, with scrollSensitivity
// unified to 20. The values are read from GamePolishBuilder's own constants
// rather than retyped here, so the guard cannot disagree with the fixer.
//
// IT READS THE SHIPPED ASSETS AND THE SHIPPED SCENE, not the builder's output —
// the same argument ShimmerHostTests makes. The question is what the game will
// do, not what the builder would do if someone remembered to run it.
//
// THE SCENE HALF IS A TEXT SCAN, and that is a deliberate choice rather than
// laziness. Fourteen of the eighteen in-scope rects are in ShellScene, and
// loading a 25-root scene inside an EditMode test to read five floats risks
// exactly the failure this project has a memory about (scene_save_bakes_layout_
// churn): a test that opens the shell and something later marks it dirty. The
// YAML carries both shapes that matter — a ScrollRect component block, and a
// PrefabInstance override on one of the five properties — and both are asserted.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Polish.Tests
{
    [TestFixture]
    public class ScrollFeelTests
    {
        const string BuilderType = "Golfin.UI.Polish.EditorTools.GamePolishBuilder";
        const string SceneAsset  = "Assets/Scenes/ShellScene.unity";

        static T Const<T>(string name)
        {
            Type t = Probe.Type(BuilderType);
            FieldInfo? f = t.GetField(name, BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(f, BuilderType + "." + name + " is gone — this guard no longer pins the reference");
            return (T)f!.GetValue(null)!;
        }

        static float Elasticity   => Const<float>("ScrollElasticity");
        static float Deceleration => Const<float>("ScrollDeceleration");
        static float Sensitivity  => Const<float>("ScrollSensitivity");

        static string Exclusion(string path)
        {
            Type t = Probe.Type(BuilderType);
            MethodInfo? m = t.GetMethod("ScrollExclusion", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(m, "GamePolishBuilder.ScrollExclusion is gone");
            return (string)m!.Invoke(null, new object[] { path })!;
        }

        [Test]
        public void TheReferenceValues_AreTheGpsOnes()
        {
            // gps_polish §D9's numbers, quoted so a later edit to the builder's constants shows up
            // here as a decision rather than as a silent retune of every list in the game.
            Assert.That(Const<string>("ScrollMovementType"), Is.EqualTo("Elastic"));
            Assert.That(Elasticity,   Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(Deceleration, Is.EqualTo(0.135f).Within(1e-5f));
            Assert.That(Sensitivity,  Is.EqualTo(20f).Within(1e-5f));
        }

        [Test]
        public void EveryInScopeScrollRect_InEveryPrefab_IsAtTheReference()
        {
            var off = new List<string>();
            int checkedCount = 0;
            foreach (string root in new[] { "Assets/Prefabs", "Assets/Resources/Prefabs" })
            {
                if (!Directory.Exists(root)) continue;
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (Exclusion(path) != "") continue;
                    if (path.Contains("/Original/Gameplay/")) continue;     // in-game HUD, out of scope
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go == null) continue;
                    foreach (ScrollRect sr in go.GetComponentsInChildren<ScrollRect>(true))
                    {
                        if (sr == null) continue;
                        checkedCount++;
                        string why = Why(sr);
                        if (why != "") off.Add($"{path} :: {sr.name} — {why}");
                    }
                }
            }
            Assert.That(checkedCount, Is.GreaterThan(0), "no prefab ScrollRects found — has a root moved?");
            Assert.That(off, Is.Empty,
                "ScrollRect(s) off the §C2 reference. Re-run GOLFIN ▸ Game Polish ▸ Apply — one scroll feel:\n  " +
                string.Join("\n  ", off));
        }

        static string Why(ScrollRect sr)
        {
            var bad = new List<string>();
            if (sr.movementType != ScrollRect.MovementType.Elastic) bad.Add("movementType=" + sr.movementType);
            if (Mathf.Abs(sr.elasticity - Elasticity) > 1e-4f)       bad.Add("elasticity=" + sr.elasticity);
            if (!sr.inertia)                                          bad.Add("inertia off");
            if (Mathf.Abs(sr.decelerationRate - Deceleration) > 1e-4f) bad.Add("deceleration=" + sr.decelerationRate);
            if (Mathf.Abs(sr.scrollSensitivity - Sensitivity) > 1e-4f) bad.Add("sensitivity=" + sr.scrollSensitivity);
            return string.Join(", ", bad);
        }

        // ── the scene half ───────────────────────────────────────────────────

        /// <summary>
        /// Every ScrollRect COMPONENT BLOCK authored straight into ShellScene.
        ///
        /// <para>The path matters, not just the values: four of these are the auth screens, which
        /// §C2 excludes, and one is a GPS list. So the scan reconstructs each rect's scene path
        /// from the YAML — GameObject names joined through the RectTransform m_Father chain — and
        /// asks <c>GamePolishBuilder.ScrollExclusion</c> the same question the builder asked. A
        /// test that instead carried its own list of excused objects would be a second opinion
        /// about scope, and the first thing to go stale.</para>
        /// </summary>
        [Test]
        public void EverySceneAuthoredScrollRect_IsAtTheReference()
        {
            Assert.IsTrue(File.Exists(SceneAsset), SceneAsset + " not found");
            string yaml = File.ReadAllText(SceneAsset);
            const string scrollRectGuid = "1aa08ab6e0800fa44ae55d278d1423e3";

            SceneIndex idx = SceneIndex.Build(yaml);
            var off = new List<string>();
            int n = 0, excluded = 0;
            foreach ((string id, string block) in idx.Blocks("114"))
            {
                if (!block.Contains(scrollRectGuid)) continue;
                if (!block.Contains("m_MovementType:")) continue;   // a reference TO a ScrollRect, not one
                string path = idx.PathOfComponent(block);
                if (Exclusion(path) != "") { excluded++; continue; }
                n++;
                var bad = new List<string>();
                // ScrollRect.MovementType serialises as Unrestricted=0, Elastic=1, Clamped=2.
                // Elastic is 1, and it is worth pinning in a comment: a first read of this scene
                // treated 2 as Elastic and concluded the opposite of the truth about every list.
                if (Field(block, "m_MovementType") != "1") bad.Add("m_MovementType=" + Field(block, "m_MovementType"));
                if (!Near(Field(block, "m_Elasticity"), Elasticity)) bad.Add("m_Elasticity=" + Field(block, "m_Elasticity"));
                if (Field(block, "m_Inertia") != "1") bad.Add("m_Inertia=" + Field(block, "m_Inertia"));
                if (!Near(Field(block, "m_DecelerationRate"), Deceleration)) bad.Add("m_DecelerationRate=" + Field(block, "m_DecelerationRate"));
                if (!Near(Field(block, "m_ScrollSensitivity"), Sensitivity)) bad.Add("m_ScrollSensitivity=" + Field(block, "m_ScrollSensitivity"));
                if (bad.Count > 0) off.Add($"{path} — {string.Join(", ", bad)}");
            }
            Assert.That(n, Is.GreaterThan(5),
                $"found only {n} in-scope ScrollRect block(s) in ShellScene ({excluded} excluded) — " +
                "has the GUID or the path resolver broken?");
            Assert.That(off, Is.Empty, "scene-authored ScrollRect(s) off the reference:\n  " + string.Join("\n  ", off));
        }

        /// <summary>
        /// Enough of the scene YAML to answer "what is this component attached to, and where does
        /// it sit". Names only — no transforms are read, and nothing is loaded into the Editor.
        /// </summary>
        sealed class SceneIndex
        {
            readonly Dictionary<string, string> _blocks = new Dictionary<string, string>();   // id -> body
            readonly Dictionary<string, string> _kind   = new Dictionary<string, string>();   // id -> class id
            readonly Dictionary<string, string> _goName = new Dictionary<string, string>();   // GameObject id -> name
            readonly Dictionary<string, string> _rtOfGo = new Dictionary<string, string>();   // GameObject id -> RectTransform id
            readonly Dictionary<string, string> _father = new Dictionary<string, string>();   // RectTransform id -> father id
            readonly Dictionary<string, string> _goOfRt = new Dictionary<string, string>();   // RectTransform id -> GameObject id

            public static SceneIndex Build(string yaml)
            {
                var ix = new SceneIndex();
                foreach (Match m in Regex.Matches(yaml, @"^--- !u!(\d+) &(\d+).*?$", RegexOptions.Multiline))
                {
                    int start = m.Index + m.Length;
                    Match next = Regex.Match(yaml.Substring(start), @"^--- !u!", RegexOptions.Multiline);
                    string body = next.Success ? yaml.Substring(start, next.Index) : yaml.Substring(start);
                    string id = m.Groups[2].Value;
                    ix._blocks[id] = body;
                    ix._kind[id]   = m.Groups[1].Value;

                    if (m.Groups[1].Value == "1")
                        ix._goName[id] = Field(body, "m_Name");
                    else if (m.Groups[1].Value is "224" or "4")
                    {
                        string go = Id(Field(body, "m_GameObject"));
                        ix._rtOfGo[go] = id;
                        ix._goOfRt[id] = go;
                        ix._father[id] = Id(Field(body, "m_Father"));
                    }
                }
                return ix;
            }

            public IEnumerable<(string, string)> Blocks(string classId)
            {
                foreach (KeyValuePair<string, string> kv in _blocks)
                    if (_kind.TryGetValue(kv.Key, out string? k) && k == classId)
                        yield return (kv.Key, kv.Value);
            }

            /// <summary>"Canvas/ScreensRoot/RosterScreen/…/ScrollView" for a component block.</summary>
            public string PathOfComponent(string componentBody)
            {
                string go = Id(Field(componentBody, "m_GameObject"));
                if (!_rtOfGo.TryGetValue(go, out string? rt)) return Name(go);
                var parts = new List<string>();
                int guard = 0;
                while (rt != null && rt != "0" && guard++ < 64)
                {
                    parts.Insert(0, Name(_goOfRt.TryGetValue(rt, out string? g) ? g : "0"));
                    _father.TryGetValue(rt, out rt);
                }
                return string.Join("/", parts);
            }

            string Name(string goId) => _goName.TryGetValue(goId, out string? n) ? n : "?";
            static string Id(string raw)
            {
                Match m = Regex.Match(raw ?? "", @"fileID:\s*(-?\d+)");
                return m.Success ? m.Groups[1].Value : "0";
            }
            static string Field(string body, string key)
            {
                Match m = Regex.Match(body, @"^\s{2}" + key + @":\s*(.+)$", RegexOptions.Multiline);
                return m.Success ? m.Groups[1].Value.Trim() : "";
            }
        }

        /// <summary>
        /// A prefab INSTANCE in ShellScene must not override any of the five feel properties.
        /// This is the half a component scan cannot see, and it is the half that was actually
        /// wrong at HEAD: RankingsScreen's list was Elastic in the scene and Clamped in its
        /// prefab, GachaRatesModal's the other way round — two places holding one decision, so
        /// whichever a reader opened, they read something true and incomplete.
        /// </summary>
        [Test]
        public void NoPrefabInstance_OverridesTheFeelProperties()
        {
            Assert.IsTrue(File.Exists(SceneAsset), SceneAsset + " not found");
            string yaml = File.ReadAllText(SceneAsset);
            string[] props = { "m_MovementType", "m_Elasticity", "m_Inertia",
                               "m_DecelerationRate", "m_ScrollSensitivity" };

            var found = new List<string>();
            foreach (string p in props)
            {
                // A PrefabInstance modification is `propertyPath: m_Elasticity` inside
                // m_Modifications; a component block writes `  m_Elasticity: 0.1`.
                foreach (Match m in Regex.Matches(yaml, @"propertyPath:\s*" + p + @"\s*$", RegexOptions.Multiline))
                    found.Add(p);
            }
            Assert.That(found, Is.Empty,
                "ShellScene overrides ScrollRect feel on a prefab instance — the value now lives in " +
                "two places. Re-run GOLFIN ▸ Game Polish ▸ Apply — one scroll feel, which fixes the " +
                "asset and reverts the override. Overridden: " + string.Join(", ", found));
        }

        static string Field(string block, string key)
        {
            Match m = Regex.Match(block, @"^\s{2}" + key + @":\s*(.+)$", RegexOptions.Multiline);
            return m.Success ? m.Groups[1].Value.Trim() : "<absent>";
        }

        static bool Near(string raw, float want)
            => float.TryParse(raw, System.Globalization.NumberStyles.Float,
                              System.Globalization.CultureInfo.InvariantCulture, out float v)
               && Mathf.Abs(v - want) < 1e-4f;
    }
}
