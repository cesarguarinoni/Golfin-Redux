// ─────────────────────────────────────────────────────────────────────────────
// game_polish_c §C1 — WHAT COUNTS AS A PLAYER-FACING BUTTON, in one place.
//
// Three things ask this question and they must not be able to disagree:
//   * GamePolishBuilder.ApplyPressFeedback, which adds the component;
//   * GamePolishProbeC, which audits the running shell;
//   * PressFeedbackCoverageTests, the regression guard that runs forever after.
// If the builder skipped a class of button that the test then demanded, the test
// would be red on a correct tree; if the test skipped a class the builder fixed,
// the guard would have a hole exactly where the sweep had been. One rule set,
// three callers.
//
// §C1.2: "Exclusions are named, not assumed." A button is exempt for one of four
// reasons, and every one of them is DECIDED FROM THE OBJECT rather than read off
// a list of paths — a path list goes stale the first time a screen is rebuilt,
// and "it was on the list" is not a reason.
//
//   1. Gps/          — out of this task's scope entirely (SPEC § Untouched).
//   2. debug / dev   — a control the player never sees.
//   3. template      — a row that exists to be cloned and is never enabled.
//   4. full-screen dismiss catcher — a Scrim or TapCatcher that fills the
//      screen. This one is NOT in §C1.2's list and is the sweep's own finding:
//      ButtonPressFeedback scales its own RectTransform, so putting it on a
//      full-screen scrim would shrink the WHOLE OVERLAY by 5 % on every tap —
//      the dim behind the gacha reveal would visibly jump away from the screen
//      edges. §C1.2's third case is "a Selectable that is not tappable by a
//      player (a scroll handle)"; this is the same shape — an element that is
//      not a widget — and it is measured (>= 85 % of the canvas on both axes),
//      not asserted from its name.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Polish.EditorTools
{
    public static class PressFeedbackScope
    {
        /// <summary>Prefab trees the sweep owns. `Assets/Resources/Prefabs` is here and it is not
        /// padding: <c>GachaBannerCard.prefab</c> lives there, is spawned at runtime into the shop,
        /// and carries three player-facing buttons that a sweep of `Assets/Prefabs/UI` alone would
        /// never have seen.</summary>
        public static readonly string[] Roots = { "Assets/Prefabs/UI", "Assets/Resources/Prefabs" };

        /// <summary>
        /// Prefab trees deliberately left alone, with the reason. Both are SPEC exclusions, not
        /// judgement calls: `Gps/` is on § Untouched, and the in-game HUD is on § Out of scope
        /// (Notion 2195 owns it).
        /// </summary>
        public static readonly (string Prefix, string Why)[] ExcludedTrees =
        {
            ("Assets/Prefabs/UI/Gps/",             "Gps/ — SPEC § Untouched"),
            ("Assets/Prefabs/Original/Gameplay/",  "in-game HUD — SPEC § Out of scope (Notion 2195)"),
        };

        public static bool PrefabInScope(string assetPath, out string why)
        {
            foreach ((string prefix, string reason) in ExcludedTrees)
                if (assetPath.Replace('\\', '/').StartsWith(prefix, StringComparison.Ordinal))
                { why = reason; return false; }
            why = "";
            return true;
        }

        /// <summary>The GPS screen roots, by scene-object name — the scene has no asset path to
        /// test against.</summary>
        public static readonly string[] GpsRoots =
        {
            "GpsHubScreen", "GpsHub", "GpsVoteScreen", "GpsGiftScreen", "GpsProfileScreen",
            "GpsRoundsScreen", "GpsCheckInScreen", "GpsVenueScreen", "ScoreUploadScreen",
            "GpsAvatarScreen", "GpsBadgesScreen", "GolfProfileScreen", "WelcomeTutorialScreen",
            "GpsNavBar", "NavSafeArea",
        };

        /// <summary>
        /// Why this button is exempt, or "" when it is in scope and must carry the component.
        /// <paramref name="canvasSize"/> is the root canvas rect; pass <see cref="Vector2.zero"/>
        /// when it is unknown and the full-screen-catcher rule is skipped rather than guessed.
        /// </summary>
        public static string ExclusionFor(Button b, string path, Vector2 canvasSize)
        {
            foreach (string g in GpsRoots)
                if (path.Contains("/" + g + "/") || path.StartsWith(g + "/", StringComparison.Ordinal))
                    return "Gps/ — SPEC § Untouched";

            string name = b.name;
            if (name.IndexOf("Debug", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("DevOnly", StringComparison.OrdinalIgnoreCase) >= 0)
                return "debug/dev control — never shown to a player";

            if (name.EndsWith("Template", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/Template"))
                return "template row — cloned, never enabled at runtime";

            if (canvasSize.x > 1f && canvasSize.y > 1f && b.transform is RectTransform rt)
            {
                Rect r = rt.rect;
                float sx = Mathf.Abs(rt.lossyScale.x) < 1e-4f ? 1f : 1f;   // rect is already local px
                if (r.width * sx >= canvasSize.x * 0.85f && r.height >= canvasSize.y * 0.85f)
                    return "full-screen dismiss catcher — a press pulse would scale the whole overlay";
            }
            return "";
        }

        // ═════════════════════════════════════════════════════════════════════
        // The prefab-asset audit — what PressFeedbackCoverageTests asserts on
        // ═════════════════════════════════════════════════════════════════════

        public sealed class Row
        {
            public string Source = "", Path = "", Exclusion = "";
            public bool HasFeedback;
            public bool InScope => Exclusion == "";
        }

        /// <summary>
        /// Every Button in every in-scope prefab asset, with a verdict.
        ///
        /// <para>PREFAB ASSETS ONLY, deliberately. It is what §"EditMode" asks for ("every prefab
        /// under Assets/Prefabs/UI/ minus Gps/") and it is the half that can be checked without a
        /// scene: it runs in a plain EditMode test, on every future prefab, forever. The scene half
        /// is checked by <c>GamePolishProbeC</c> against the RUNNING shell, which is stronger than
        /// any static read of the scene could be — it sees the runtime clones too.</para>
        /// </summary>
        public static List<Row> AuditPrefabs()
        {
            var rows = new List<Row>();
            foreach (string root in Roots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!PrefabInScope(path, out _)) continue;
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go == null) continue;

                    foreach (Button b in go.GetComponentsInChildren<Button>(true))
                    {
                        if (b == null) continue;
                        rows.Add(new Row
                        {
                            Source      = path,
                            Path        = PathIn(go.transform, b.transform),
                            Exclusion   = ExclusionFor(b, PathIn(go.transform, b.transform), CanvasSizeOf(go)),
                            HasFeedback = b.GetComponent<ButtonPressFeedback>() != null,
                        });
                    }
                }
            }
            rows.Sort((x, y) => string.CompareOrdinal(x.Source + "/" + x.Path, y.Source + "/" + y.Path));
            return rows;
        }

        /// <summary>A prefab has no canvas of its own; the shell's is 1170x2532 at scale 1, which
        /// is the frame every one of these prefabs is authored against.</summary>
        static Vector2 CanvasSizeOf(GameObject prefabRoot)
        {
            var c = prefabRoot.GetComponentInChildren<Canvas>(true);
            if (c != null && c.transform is RectTransform crt && crt.rect.width > 1f) return crt.rect.size;
            return new Vector2(1170f, 2532f);
        }

        public static string PathIn(Transform root, Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null && p != root; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        /// <summary>
        /// The audit as text, so a test in a NAMED assembly can call it by reflection.
        /// (Assembly-CSharp-Editor is a predefined assembly and an asmdef cannot reference it —
        /// the same arrangement <c>LayeredPushTests</c> and <c>UiMotionTests</c> already use.)
        /// First line is the machine-readable summary; the rest is one line per defect.
        /// </summary>
        public static string AuditPrefabsSummary()
        {
            List<Row> rows = AuditPrefabs();
            var defects = new List<Row>();
            int inScope = 0, covered = 0;
            foreach (Row r in rows)
            {
                if (!r.InScope) continue;
                inScope++;
                if (r.HasFeedback) covered++; else defects.Add(r);
            }
            var sb = new StringBuilder();
            sb.AppendLine($"total={rows.Count} inScope={inScope} covered={covered} " +
                          $"excluded={rows.Count - inScope} defects={defects.Count}");
            foreach (Row d in defects) sb.AppendLine($"DEFECT {d.Source} :: {d.Path}");
            return sb.ToString();
        }
    }
}
