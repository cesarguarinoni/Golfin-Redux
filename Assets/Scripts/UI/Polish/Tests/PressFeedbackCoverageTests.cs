// ─────────────────────────────────────────────────────────────────────────────
// game_polish_c §C1 — the REGRESSION GUARD, and the reason this task is not just
// a one-off sweep.
//
// Rule 11 ("every player-facing Button gets ButtonPressFeedback") has existed
// since 2026-05-22 and was honoured going forward and never once swept backwards:
// the live audit at HEAD found 410 player-facing buttons in a running shell and
// 166 with the component. A convention that nothing checks decays exactly like
// that — quietly, in the direction of whoever was in a hurry.
//
// So this fixture is the point of §C1, not its footnote. It re-runs the sweep's
// own scope rules over EVERY prefab under the roots, every time the EditMode
// suite runs, forever. A prefab authored next year with a bare Button turns this
// red on the commit that adds it, which is the only moment fixing it is cheap.
//
// IT SHARES ITS RULES WITH THE BUILDER rather than restating them. Both call
// PressFeedbackScope, so the guard cannot drift into demanding something the
// builder does not do, or into excusing something the builder fixes. Restating
// the exclusions here would have produced two lists that agree on the day they
// are written.
//
// ASSEMBLY: Golfin.UI.Polish.Tests is a named assembly and cannot reference
// Assembly-CSharp-Editor, so PressFeedbackScope is reached by reflection — the
// same arrangement UiMotionTests, LayeredPushTests and GpsScreenTransitionTests
// already use, via the shared Probe helper.
//
// TRIPWIRE (PIPELINE_HARDENING §20): PlantedDefect_IsSeen builds a prefab with a
// bare Button under one of the swept roots, asserts the audit reports it, and
// deletes it. A guard nobody has watched fail is a guard nobody has tested.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Polish.Tests
{
    [TestFixture]
    public class PressFeedbackCoverageTests
    {
        const string ScopeType = "Golfin.UI.Polish.EditorTools.PressFeedbackScope";

        /// <summary>The audit's first line, `total=.. inScope=.. covered=.. excluded=.. defects=..`,
        /// followed by one `DEFECT source :: path` line per uncovered in-scope button.</summary>
        static string Audit()
        {
            Type t = Probe.Type(ScopeType);
            MethodInfo? m = t.GetMethod("AuditPrefabsSummary", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(m, "PressFeedbackScope.AuditPrefabsSummary is gone — this guard is now inert");
            return (string)m!.Invoke(null, null)!;
        }

        static Dictionary<string, int> Summary(string audit)
        {
            var d = new Dictionary<string, int>();
            foreach (string pair in audit.Split('\n')[0].Trim().Split(' '))
            {
                string[] kv = pair.Split('=');
                if (kv.Length == 2 && int.TryParse(kv[1], out int v)) d[kv[0]] = v;
            }
            return d;
        }

        [Test]
        public void EveryPlayerFacingButtonInEveryPrefab_HasPressFeedback()
        {
            string audit = Audit();
            Dictionary<string, int> s = Summary(audit);
            Assert.That(s.ContainsKey("defects"), Is.True, "audit summary unreadable:\n" + audit);

            // The whole audit text goes in the failure message: a bare "expected 0, was 7" would
            // make the reader re-run the sweep by hand to find out WHICH seven.
            Assert.That(s["defects"], Is.EqualTo(0),
                "Rule 11: a player-facing Button with no ButtonPressFeedback.\n" +
                "Add the component beside it (or, if it is genuinely exempt, extend\n" +
                "PressFeedbackScope.ExclusionFor with the reason — never this test).\n\n" + audit);
        }

        [Test]
        public void TheAudit_CoversARealPopulation()
        {
            // A guard that silently stopped finding prefabs would report zero defects forever.
            // 200 is far below the ~500 in the tree and far above anything a broken glob returns.
            Dictionary<string, int> s = Summary(Audit());
            Assert.That(s["total"], Is.GreaterThan(200),
                "the audit found almost no buttons — has a root moved? " + string.Join(", ", s));
            Assert.That(s["inScope"], Is.GreaterThan(150));
        }

        /// <summary>
        /// §20 tripwire. Plants a prefab with one bare Button under a swept root, asserts the
        /// audit's defect count rises by exactly one and names it, then removes it and asserts the
        /// count returns. Runs in one test so a failure cannot leave the planted prefab behind.
        /// </summary>
        [Test]
        public void PlantedDefect_IsSeen()
        {
            const string dir = "Assets/Prefabs/UI/__ScopeTripwire";
            const string asset = dir + "/TripwireButton.prefab";

            int before = Summary(Audit())["defects"];
            var go = new GameObject("TripwireButton", typeof(RectTransform), typeof(CanvasRenderer),
                                    typeof(Image), typeof(Button));
            try
            {
                if (!AssetDatabase.IsValidFolder(dir))
                    AssetDatabase.CreateFolder("Assets/Prefabs/UI", "__ScopeTripwire");
                PrefabUtility.SaveAsPrefabAsset(go, asset);
                AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceSynchronousImport);

                string planted = Audit();
                Assert.That(Summary(planted)["defects"], Is.EqualTo(before + 1),
                    "the tripwire prefab was not counted — the audit is not seeing new prefabs:\n" + planted);
                Assert.That(planted, Does.Contain(asset),
                    "the defect line does not name the planted prefab:\n" + planted);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                AssetDatabase.DeleteAsset(asset);
                AssetDatabase.DeleteAsset(dir);
                AssetDatabase.Refresh();
            }

            Assert.That(Summary(Audit())["defects"], Is.EqualTo(before),
                "the tripwire prefab was not cleaned up");
        }

        /// <summary>
        /// Every exclusion the audit uses must carry a REASON. §C1.2: "Exclusions are named, not
        /// assumed." An empty-string reason would exclude a button while telling nobody why.
        /// </summary>
        [Test]
        public void EveryExclusion_HasAReason()
        {
            Type t = Probe.Type(ScopeType);
            MethodInfo? m = t.GetMethod("AuditPrefabs", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(m, "PressFeedbackScope.AuditPrefabs is gone");
            var rows = (System.Collections.IEnumerable)m!.Invoke(null, null)!;

            var nameless = new List<string>();
            int excluded = 0;
            foreach (object row in rows)
            {
                Type rt = row.GetType();
                string excl = (string)rt.GetField("Exclusion")!.GetValue(row)!;
                if (excl.Length == 0) continue;
                excluded++;
                if (excl.Trim().Length < 8)
                    nameless.Add((string)rt.GetField("Source")!.GetValue(row)! + " :: " + excl);
            }
            Assert.That(nameless, Is.Empty, "exclusions with no usable reason: " + string.Join("; ", nameless));
            Assert.That(excluded, Is.GreaterThanOrEqualTo(0));
        }
    }
}
