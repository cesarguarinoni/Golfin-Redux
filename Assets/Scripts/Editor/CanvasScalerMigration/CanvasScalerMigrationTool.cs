#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Golfin.Editor.CanvasScalerMigration
{
    /// <summary>
    /// One-shot migration tool: moves in-scope CanvasScalers from
    /// (1080x1920, MatchWidthOrHeight, match=0.5) to (1170x2532, MatchWidthOrHeight, match=0).
    ///
    /// See Docs/Specs/Queued/CANVAS_SCALER_FIX_PLAN.md (Step 3).
    /// Hypothesis validated 2026-04-29 via CanvasScalerTest scene matrix.
    ///
    /// Scope: 5 physics-lab scenes (7 scalers total). Skips scalers that are
    /// already correct or use a different ScaleMode (Constant Pixel Size, etc.).
    /// Does NOT touch menu / shell / persistent UI canvases.
    ///
    /// Workflow:
    ///   1. GOLFIN/Canvas Scaler/Migrate (Dry Run)   -> reports what WOULD change.
    ///   2. Review the console output.
    ///   3. GOLFIN/Canvas Scaler/Migrate (Apply)     -> writes changes + saves scenes.
    ///
    /// Always run dry-run first. Always commit before Apply.
    /// </summary>
    public static class CanvasScalerMigrationTool
    {
        // -------- Scope (verified 2026-04-29) --------

        static readonly string[] TargetScenes =
        {
            "Assets/Scenes/Physics/LabScaffold.unity",         // 2 scalers
            "Assets/Scenes/Physics/ShotConeTest.unity",        // 1 scaler
            "Assets/Scenes/Physics/PhysicsLab_Range.unity",    // 1 scaler
            "Assets/Scenes/Physics/PhysicsLab_Hole1.unity",    // 2 scalers
            "Assets/Scenes/Physics/PhysicsLab_Dashboard.unity",// 1 scaler
        };
        const int ExpectedTotalScalers = 7;

        // -------- Match criteria (FROM) --------

        static readonly Vector2 FromRefRes = new Vector2(1080f, 1920f);
        const float FromMatch = 0.5f;
        const CanvasScaler.ScreenMatchMode FromMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        const CanvasScaler.ScaleMode FromScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        // -------- Target values (TO) --------

        static readonly Vector2 ToRefRes = new Vector2(1170f, 2532f);
        const float ToMatch = 0f;
        const CanvasScaler.ScreenMatchMode ToMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        const CanvasScaler.ScaleMode ToScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        // -------- Menu items --------

        [MenuItem("GOLFIN/Canvas Scaler/Migrate (Dry Run)", priority = 400)]
        public static void MigrateDryRun() => Run(apply: false);

        [MenuItem("GOLFIN/Canvas Scaler/Migrate (Apply)", priority = 401)]
        public static void MigrateApply()
        {
            if (!EditorUtility.DisplayDialog(
                    "Apply Canvas Scaler migration?",
                    "This will modify and save the 5 physics-lab scenes.\n\n" +
                    "Make sure you have committed first and are on a fresh branch (canvas-scaler-migration).\n\n" +
                    "Run Dry Run beforehand if you haven't yet.",
                    "Apply", "Cancel"))
            {
                return;
            }
            Run(apply: true);
        }

        // -------- Implementation --------

        static void Run(bool apply)
        {
            var startScenePath = SceneManager.GetActiveScene().path;
            int scalersConsidered = 0;
            int scalersChanged = 0;
            int scalersAlreadyCorrect = 0;
            int scalersSkipped = 0;
            var report = new StringBuilder();
            report.AppendLine($"=== Canvas Scaler Migration {(apply ? "APPLY" : "DRY RUN")} ===");
            report.AppendLine($"FROM: ref={FromRefRes.x:0}x{FromRefRes.y:0}, match={FromMatch}, mode=ScaleWithScreenSize/MatchWidthOrHeight");
            report.AppendLine($"TO  : ref={ToRefRes.x:0}x{ToRefRes.y:0}, match={ToMatch}, mode=ScaleWithScreenSize/MatchWidthOrHeight");
            report.AppendLine();

            foreach (var scenePath in TargetScenes)
            {
                if (!System.IO.File.Exists(scenePath))
                {
                    report.AppendLine($"[MISSING] {scenePath}  -- file not found, skipping");
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                report.AppendLine($"--- {scenePath}");

                var scalers = FindScalersInScene(scene);
                bool sceneDirty = false;

                foreach (var scaler in scalers)
                {
                    scalersConsidered++;
                    var path = GetGameObjectPath(scaler.gameObject);
                    var beforeMode = scaler.uiScaleMode;
                    var beforeRef = scaler.referenceResolution;
                    var beforeMatchMode = scaler.screenMatchMode;
                    var beforeMatchVal = scaler.matchWidthOrHeight;

                    // Already at target?
                    if (beforeMode == ToScaleMode &&
                        ApproxEq(beforeRef, ToRefRes) &&
                        beforeMatchMode == ToMatchMode &&
                        Mathf.Approximately(beforeMatchVal, ToMatch))
                    {
                        scalersAlreadyCorrect++;
                        report.AppendLine($"  [OK   ] {path}  -- already at target");
                        continue;
                    }

                    // Matches FROM criteria?
                    bool matchesFrom =
                        beforeMode == FromScaleMode &&
                        ApproxEq(beforeRef, FromRefRes) &&
                        beforeMatchMode == FromMatchMode &&
                        Mathf.Approximately(beforeMatchVal, FromMatch);

                    if (!matchesFrom)
                    {
                        scalersSkipped++;
                        report.AppendLine(
                            $"  [SKIP ] {path}  -- not in FROM state " +
                            $"(mode={beforeMode}, ref={beforeRef.x:0}x{beforeRef.y:0}, " +
                            $"matchMode={beforeMatchMode}, match={beforeMatchVal})");
                        continue;
                    }

                    // Apply.
                    if (apply)
                    {
                        Undo.RecordObject(scaler, "Migrate CanvasScaler to 1170x2532");
                        scaler.uiScaleMode = ToScaleMode;
                        scaler.referenceResolution = ToRefRes;
                        scaler.screenMatchMode = ToMatchMode;
                        scaler.matchWidthOrHeight = ToMatch;
                        EditorUtility.SetDirty(scaler);
                        sceneDirty = true;
                    }

                    scalersChanged++;
                    report.AppendLine(
                        $"  [{(apply ? "DONE " : "WOULD")}] {path}  " +
                        $"-- ref {beforeRef.x:0}x{beforeRef.y:0}->{ToRefRes.x:0}x{ToRefRes.y:0}, " +
                        $"match {beforeMatchVal}->{ToMatch}");
                }

                if (apply && sceneDirty)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    report.AppendLine($"  [SAVED] {scenePath}");
                }
            }

            // Restore previous scene if any.
            if (!string.IsNullOrEmpty(startScenePath) && System.IO.File.Exists(startScenePath))
            {
                EditorSceneManager.OpenScene(startScenePath, OpenSceneMode.Single);
            }

            report.AppendLine();
            report.AppendLine($"Considered : {scalersConsidered}");
            report.AppendLine($"Changed    : {scalersChanged}{(apply ? "" : " (would change)")}");
            report.AppendLine($"Already OK : {scalersAlreadyCorrect}");
            report.AppendLine($"Skipped    : {scalersSkipped}");
            if (scalersConsidered != ExpectedTotalScalers)
            {
                report.AppendLine();
                report.AppendLine(
                    $"WARNING: expected {ExpectedTotalScalers} scalers across the 5 scenes, " +
                    $"found {scalersConsidered}. Investigate before applying.");
            }

            if (apply && scalersChanged > 0)
            {
                report.AppendLine();
                report.AppendLine("=== POST-MIGRATION CHECKLIST ===");
                report.AppendLine("1. Diff git -- expect ONLY CanvasScaler edits in the 5 scene files.");
                report.AppendLine("2. Open LabScaffold play-mode at Game View 1170x2532. Verify:");
                report.AppendLine("   - Cone fires correctly (visible shrink to ~92% expected, no crashes)");
                report.AppendLine("   - Power gauge animates (text smaller but readable; bump TMP font if not)");
                report.AppendLine("   - Club handle sprite still positioned correctly");
                report.AppendLine("3. Open PhysicsLab_Hole1, _Range, _Dashboard, ShotConeTest -- play-mode smoke test each.");
                report.AppendLine("4. 8.3 topbar verification:");
                report.AppendLine("   - Capture fresh topbar-diff-v3.png at 1170x2532 game view.");
                report.AppendLine("   - Compare 1:1 with Figma source -- player card + hole card should match exactly now.");
                report.AppendLine("   - Apply ChipStack width fix: 248 -> 298 on PlayerCard + HoleCard.");
                report.AppendLine("5. Update Docs/Architecture/RUNTIME_BLUEPRINT.md with UI Coordinate System section.");
                report.AppendLine("6. Commit: 'canvas-scaler-migration: in-game scenes to 1170x2532 / Match=0'");
            }

            Debug.Log(report.ToString());
        }

        static List<CanvasScaler> FindScalersInScene(Scene scene)
        {
            var result = new List<CanvasScaler>();
            foreach (var root in scene.GetRootGameObjects())
            {
                result.AddRange(root.GetComponentsInChildren<CanvasScaler>(includeInactive: true));
            }
            return result;
        }

        static string GetGameObjectPath(GameObject go)
        {
            var path = go.name;
            var t = go.transform.parent;
            while (t != null)
            {
                path = t.name + "/" + path;
                t = t.parent;
            }
            return path;
        }

        static bool ApproxEq(Vector2 a, Vector2 b) =>
            Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y);
    }
}
#endif
