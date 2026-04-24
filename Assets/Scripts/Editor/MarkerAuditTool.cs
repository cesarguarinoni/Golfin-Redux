using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Phase A2: Audit Hole_01_Geo zone mesh GOs for Physics.Runtime.SurfaceMarker state.
/// GOLFIN > Tools > A2 - Marker Audit (Hole_01)
///
/// Instructions (spec-mandated):
///   1. CLOSE Unity completely. Reopen Unity. Do NOT run this without restarting first.
///   2. Menu: GOLFIN > Tools > A2 - Marker Audit (Hole_01)
///   3. Audit saves to Docs/DIAG/realtest-20260425/A2-Hole01-marker-audit.txt
/// </summary>
public static class MarkerAuditTool
{
    [MenuItem("GOLFIN/Tools/A2 - Marker Audit (Hole_01)")]
    public static void RunAudit()
    {
        string[] guids = AssetDatabase.FindAssets("t:Scene Hole_01_Geo");
        if (guids.Length == 0) { Debug.LogError("[A2Audit] Hole_01_Geo not found."); return; }

        string scenePath = null;
        foreach (var g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (!p.Contains("Video")) { scenePath = p; break; }
        }
        if (scenePath == null) { Debug.LogError("[A2Audit] Could not resolve Hole_01_Geo path."); return; }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        System.Type physSmType  = typeof(Golfin.Physics.Runtime.SurfaceMarker);
        System.Type courseSmType = System.Type.GetType("Golfin.Course.SurfaceMarker, Assembly-CSharp");

        int cntZero    = 0;
        int cntOne     = 0;
        int cntMulti   = 0;
        int cntBroken  = 0;
        int cntOnParent = 0;
        int totalColliders = 0;

        var sb = new StringBuilder();
        sb.AppendLine("# A2 — Hole_01_Geo Marker Audit");
        sb.AppendLine($"# Date: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"# Scene: {scenePath}");
        sb.AppendLine();

        var detailLines = new List<string>();

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var col in root.GetComponentsInChildren<Collider>(true))
            {
                if (!(col is MeshCollider) && !(col is BoxCollider)) continue;
                totalColliders++;

                string goPath = GetHierarchyPath(col.gameObject);

                // Count all components including missing-script ones
                int brokenOnGO = 0;
                var allComps = col.gameObject.GetComponents<Component>();
                foreach (var c in allComps)
                {
                    if (c == null) brokenOnGO++; // missing script shows as null
                }

                // Walk parents up to scene root, collecting all SurfaceMarker variants
                int validPhysCount = 0;
                bool physOnDirectGO = false;
                bool physOnParent   = false;
                var markerTypes = new List<string>();

                Transform t = col.transform;
                while (t != null && t.gameObject.scene.IsValid())
                {
                    // Valid Physics markers
                    var physMarkers = t.GetComponents<Golfin.Physics.Runtime.SurfaceMarker>();
                    foreach (var pm in physMarkers)
                    {
                        validPhysCount++;
                        markerTypes.Add($"Physics.{pm.Type}@{t.name}");
                        if (t == col.transform) physOnDirectGO = true;
                        else physOnParent = true;
                    }
                    // Missing-script components on this transform
                    var compsOnT = t.GetComponents<Component>();
                    foreach (var c in compsOnT)
                    {
                        if (c == null) markerTypes.Add($"MISSING_SCRIPT@{t.name}");
                    }
                    t = t.parent;
                }

                // Course marker
                var courseMark = col.GetComponentInParent(courseSmType);
                string courseType = "none";
                if (courseMark != null)
                {
                    try
                    {
                        var fi = courseSmType.GetField("surfaceType");
                        courseType = fi?.GetValue(courseMark)?.ToString() ?? "?";
                    }
                    catch { courseType = "err"; }
                }

                if (physOnParent) cntOnParent++;
                if (brokenOnGO > 0) cntBroken++;

                if (validPhysCount == 0) cntZero++;
                else if (validPhysCount == 1) cntOne++;
                else cntMulti++;

                string status = validPhysCount == 0 ? "ZERO_PHYS" :
                                validPhysCount > 1   ? "MULTI_PHYS" : "OK";
                if (brokenOnGO > 0) status += "+BROKEN";
                if (physOnParent) status += "+PARENT";

                detailLines.Add(
                    $"{status}\t{col.gameObject.name}\t" +
                    $"course={courseType}\t" +
                    $"physCount={validPhysCount}\t" +
                    $"broken={brokenOnGO}\t" +
                    $"markers=[{string.Join(",", markerTypes)}]\t" +
                    $"path={goPath}");
            }
        }

        // Summary table
        sb.AppendLine("## Summary");
        sb.AppendLine($"Total collider GOs: {totalColliders}");
        sb.AppendLine($"ZERO valid Physics markers in parent chain: {cntZero}");
        sb.AppendLine($"ONE valid Physics marker: {cntOne}");
        sb.AppendLine($"MULTIPLE valid Physics markers (duplicate): {cntMulti}");
        sb.AppendLine($"Has at least one broken/missing-script component: {cntBroken}");
        sb.AppendLine($"Physics marker on PARENT (not direct GO): {cntOnParent}");
        sb.AppendLine();
        sb.AppendLine("## Detail (status TAB goName TAB courseType TAB physCount TAB broken TAB markers TAB path)");
        foreach (var line in detailLines)
            sb.AppendLine(line);

        string diagDir = Path.Combine(Application.dataPath, "..", "Docs", "DIAG", "realtest-20260425");
        Directory.CreateDirectory(diagDir);
        string outPath = Path.Combine(diagDir, "A2-Hole01-marker-audit.txt");
        File.WriteAllText(outPath, sb.ToString());

        EditorSceneManager.CloseScene(scene, true);

        Debug.Log($"[A2Audit] Done. {totalColliders} collider GOs. Zero={cntZero} One={cntOne} Multi={cntMulti} Broken={cntBroken}. Saved: {outPath}");
        EditorUtility.DisplayDialog("A2 Marker Audit Complete",
            $"Total collider GOs: {totalColliders}\n" +
            $"Zero Physics markers: {cntZero}\n" +
            $"One Physics marker: {cntOne}\n" +
            $"Multi Physics markers: {cntMulti}\n" +
            $"Broken components: {cntBroken}\n\n" +
            $"Saved to:\n{outPath}", "OK");
    }

    static string GetHierarchyPath(GameObject go)
    {
        var parts = new List<string>();
        Transform t = go.transform;
        while (t != null) { parts.Insert(0, t.name); t = t.parent; }
        return string.Join("/", parts);
    }
}
