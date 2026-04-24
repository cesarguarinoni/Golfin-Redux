using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Golfin.Physics;
using Golfin.Physics.Runtime;

/// <summary>
/// B1 repair tool: for each hole scene, removes zombie/broken-script SurfaceMarker
/// components from Course-marked zone GOs and ensures every one has a single valid
/// Physics.Runtime.SurfaceMarker with the correct Type.
///
/// This replaces SyncPhysicsSurfaceMarkers.cs, which could only UPDATE existing markers
/// (not CREATE them) and was the Roslyn migration that deposited zombies.
///
/// Usage:
///   GOLFIN > Tools > Repair Physics Markers (Hole_01)   — single hole, quick
///   GOLFIN > Tools > Repair Physics Markers (All Holes) — batch, all 18 holes
/// </summary>
public static class PhysicsMarkerRepairTool
{
    const string DiagDir = "Docs/DIAG/realtest-20260425";

    // ── Menu items ────────────────────────────────────────────────────────────

    [MenuItem("GOLFIN/Tools/Repair Physics Markers (Hole_01)")]
    public static void RepairHole01Menu()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# B1 — Physics Marker Repair — Hole_01");
        sb.AppendLine($"# Date: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        RepairHole(1, sb);
        WriteReport(sb, "B1-repair-Hole_01.txt");
    }

    [MenuItem("GOLFIN/Tools/Repair Physics Markers (All Holes)")]
    [MenuItem("GOLFIN/Tools/Sync Physics Surface Markers (All Holes)")]
    public static void RepairAllHolesMenu()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# B1 — Physics Marker Repair — All Holes");
        sb.AppendLine($"# Date: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        int totalRepaired = 0, totalScenes = 0;
        for (int hole = 1; hole <= 18; hole++)
        {
            int count = RepairHole(hole, sb);
            if (count >= 0) { totalRepaired += count; totalScenes++; }
        }

        string summary = $"TOTAL: {totalScenes} scenes processed, {totalRepaired} GOs modified.";
        sb.AppendLine();
        sb.AppendLine(summary);
        Debug.Log($"[RepairTool] {summary}");
        WriteReport(sb, "B1-repair-All.txt");
    }

    // ── Core API (callable from tests / other tools) ──────────────────────────

    /// <summary>
    /// Repair one hole. Returns count of modified GOs, or -1 if scene not found.
    /// </summary>
    public static int RepairHole(int holeNumber, StringBuilder report = null)
    {
        string sceneName = $"Hole_{holeNumber:D2}_Geo";
        string[] guids = AssetDatabase.FindAssets($"t:Scene {sceneName}");
        if (guids.Length == 0)
        {
            Debug.LogWarning($"[RepairTool] Scene not found: {sceneName} — skipping.");
            return -1;
        }

        string scenePath = null;
        foreach (var g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            if (!p.Contains("Video")) { scenePath = p; break; }
        }
        if (scenePath == null) return -1;

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        report?.AppendLine($"## {sceneName}");

        int repaired = RepairScene(scene, report);

        if (repaired > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            if (!saved)
                Debug.LogError($"[RepairTool] SaveScene FAILED for {sceneName}! Changes NOT persisted to disk.");
            else
                Debug.Log($"[RepairTool] {sceneName}: {repaired} GOs modified, scene saved.");
        }
        else
        {
            Debug.Log($"[RepairTool] {sceneName}: nothing to repair.");
        }

        // Post-repair verification (always runs even if nothing was repaired)
        int validCount = 0, brokenCount = 0;
        VerifyScene(scene, ref validCount, ref brokenCount);

        string verifyResult = brokenCount == 0 ? "PASS" : "FAIL";
        string verifyLine = $"Post-repair {sceneName}: {validCount} GOs with valid Physics marker, {brokenCount} broken. [{verifyResult}]";
        Debug.Log($"[RepairTool] {verifyLine}");
        report?.AppendLine(verifyLine);
        report?.AppendLine();

        if (brokenCount > 0)
            Debug.LogError($"[RepairTool] Verification FAILED — {brokenCount} broken components remain in {sceneName}!");

        EditorSceneManager.CloseScene(scene, true);
        return repaired;
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    static int RepairScene(Scene scene, StringBuilder report)
    {
        System.Type courseSmType = System.Type.GetType("Golfin.Course.SurfaceMarker, Assembly-CSharp");
        FieldInfo stField = courseSmType?.GetField("surfaceType");
        if (courseSmType == null || stField == null)
        {
            Debug.LogWarning($"[RepairTool] Golfin.Course.SurfaceMarker not found — skipping {scene.name}.");
            return 0;
        }

        int zombiesRemoved = 0, markersAdded = 0, markersUpdated = 0, markersOk = 0;

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var courseComp in root.GetComponentsInChildren(courseSmType, true))
            {
                var go = ((Component)courseComp).gameObject;

                // Step 1: remove broken/missing-script components (includes zombie markers)
                int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                if (removed > 0)
                {
                    zombiesRemoved += removed;
                    EditorUtility.SetDirty(go);
                    report?.AppendLine($"  REMOVED {removed} zombie(s) from {GetPath(go)}");
                }

                // Step 2: ensure valid Physics.Runtime.SurfaceMarker with correct Type
                int courseTypeInt = (int)stField.GetValue(courseComp);
                SurfaceType physType = SurfaceMarkerMap.MapCourseToPhysics(courseTypeInt);

                var physMarker = go.GetComponent<SurfaceMarker>();
                if (physMarker == null)
                {
                    physMarker = go.AddComponent<SurfaceMarker>();
                    physMarker.Type = physType;
                    EditorUtility.SetDirty(go);
                    markersAdded++;
                    report?.AppendLine($"  ADDED marker Type={physType} to {GetPath(go)}");
                }
                else if (physMarker.Type != physType)
                {
                    physMarker.Type = physType;
                    EditorUtility.SetDirty(go);
                    markersUpdated++;
                    report?.AppendLine($"  UPDATED marker → Type={physType} on {GetPath(go)}");
                }
                else
                {
                    markersOk++;
                }
            }
        }

        string summary = $"Removed {zombiesRemoved} zombies, added {markersAdded}, updated {markersUpdated}, ok {markersOk}.";
        Debug.Log($"[RepairTool] {scene.name}: {summary}");
        report?.AppendLine($"  Summary: {summary}");
        return zombiesRemoved + markersAdded + markersUpdated;
    }

    static void VerifyScene(Scene scene, ref int validCount, ref int brokenCount)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var col in root.GetComponentsInChildren<Collider>(true))
            {
                if (!(col is MeshCollider) && !(col is BoxCollider)) continue;

                // Count missing-script components remaining on this GO
                foreach (var c in col.gameObject.GetComponents<Component>())
                    if (c == null) brokenCount++;

                // Valid Physics marker anywhere in parent chain (including self)
                if (col.GetComponentInParent<SurfaceMarker>() != null)
                    validCount++;
            }
        }
    }

    static void WriteReport(StringBuilder sb, string filename)
    {
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", DiagDir));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, filename);
        File.WriteAllText(path, sb.ToString());
        Debug.Log($"[RepairTool] Report saved: {path}");
    }

    static string GetPath(GameObject go)
    {
        var sb = new System.Text.StringBuilder(go.name);
        var t = go.transform.parent;
        while (t != null) { sb.Insert(0, t.name + "/"); t = t.parent; }
        return sb.ToString();
    }
}
