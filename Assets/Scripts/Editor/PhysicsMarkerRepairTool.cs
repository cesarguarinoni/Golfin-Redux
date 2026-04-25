using System.Collections.Generic;
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
/// B1 repair tool: for each hole scene,
///   1. Removes zombie MonoBehaviour components (Roslyn-migration artifacts that have
///      m_Script: {fileID: 1992067906} — no GUID) by direct YAML editing on disk.
///   2. Adds/updates a valid Physics.Runtime.SurfaceMarker on every Course-marked GO.
///
/// Why YAML editing: Unity's m_Component array is read-only via SerializedObject, and
/// GameObjectUtility.RemoveMonoBehavioursWithMissingScript doesn't detect the non-null
/// wrong-fileID script references produced by Roslyn's Assembly-CSharp context.
///
/// Usage:
///   GOLFIN > Tools > Repair Physics Markers (Hole_01)   — single hole
///   GOLFIN > Tools > Repair Physics Markers (All Holes) — batch all 18 holes
/// </summary>
public static class PhysicsMarkerRepairTool
{
    const string DiagDir = "Docs/DIAG/realtest-20260425";
    // Zombie MonoBehaviours from Roslyn migration always have this m_Script fileID
    const string ZombieScriptLine = "  m_Script: {fileID: 1992067906}";

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

        int totalModified = 0, totalScenes = 0;
        for (int hole = 1; hole <= 18; hole++)
        {
            int count = RepairHole(hole, sb);
            if (count >= 0) { totalModified += count; totalScenes++; }
        }

        string summary = $"TOTAL: {totalScenes} scenes processed, {totalModified} total changes.";
        sb.AppendLine();
        sb.AppendLine(summary);
        Debug.Log($"[RepairTool] {summary}");
        WriteReport(sb, "B1-repair-All.txt");
    }

    // ── Core API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Repair one hole. Returns total count of changes (zombies removed + markers added/updated).
    /// Returns -1 if scene not found.
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

        string diskPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, scenePath.Substring("Assets/".Length)));

        report?.AppendLine($"## {sceneName}");

        // ── Step 1: Remove zombie components from scene YAML on disk ──────────
        int zombiesRemoved = RemoveZombiesFromYAML(diskPath, report);
        if (zombiesRemoved > 0)
        {
            Debug.Log($"[RepairTool] {sceneName}: removed {zombiesRemoved} zombie blocks from YAML.");
            AssetDatabase.Refresh();
        }

        // ── Step 2: Open scene, add/update valid Physics markers ──────────────
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        int markersChanged = AddOrUpdateMarkers(scene, report);

        if (markersChanged > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            if (!saved)
                Debug.LogError($"[RepairTool] SaveScene FAILED for {sceneName}!");
            else
                Debug.Log($"[RepairTool] {sceneName}: {markersChanged} marker changes, scene saved.");
        }
        else if (zombiesRemoved > 0)
        {
            // YAML was cleaned but markers already in place — mark dirty to force save
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[RepairTool] {sceneName}: markers already correct, saved YAML-cleaned scene.");
        }
        else
        {
            Debug.Log($"[RepairTool] {sceneName}: nothing to repair.");
        }

        // ── Step 3: Verify ────────────────────────────────────────────────────
        int validCount = 0, brokenCount = 0;
        VerifyScene(scene, ref validCount, ref brokenCount);

        string result = brokenCount == 0 ? "PASS" : "FAIL";
        string verifyLine = $"Post-repair {sceneName}: {validCount} valid, {brokenCount} broken. [{result}]";
        Debug.Log($"[RepairTool] {verifyLine}");
        report?.AppendLine(verifyLine);
        report?.AppendLine();

        if (brokenCount > 0)
            Debug.LogError($"[RepairTool] Verification FAILED — {brokenCount} broken remain in {sceneName}!");

        EditorSceneManager.CloseScene(scene, true);
        return zombiesRemoved + markersChanged;
    }

    // ── YAML zombie removal ───────────────────────────────────────────────────

    /// <summary>
    /// Removes MonoBehaviour blocks whose m_Script uses the Roslyn zombie fileID
    /// ({fileID: 1992067906} with no GUID), plus removes their component list entries
    /// from their parent GameObjects. Works directly on the scene file on disk.
    /// Returns the count of zombie blocks removed.
    /// </summary>
    static int RemoveZombiesFromYAML(string diskPath, StringBuilder report)
    {
        if (!File.Exists(diskPath)) return 0;

        string text = File.ReadAllText(diskPath, Encoding.UTF8);
        string[] lines = text.Split('\n');

        // Pass 1: collect fileIDs of zombie MonoBehaviour blocks
        var zombieIds = new HashSet<string>();
        string currentId = null;
        bool inMonoBehaviour = false;

        foreach (var rawLine in lines)
        {
            string line = rawLine.TrimEnd('\r');

            if (line.StartsWith("--- !u!"))
            {
                currentId = null;
                inMonoBehaviour = false;
                int amp = line.LastIndexOf('&');
                if (amp >= 0) currentId = line.Substring(amp + 1).Trim();
            }
            else if (line == "MonoBehaviour:")
            {
                inMonoBehaviour = true;
            }
            else if (inMonoBehaviour && line == ZombieScriptLine)
            {
                // Exact match: m_Script: {fileID: 1992067906} on its own line,
                // meaning no GUID (valid scripts have guid:... after the fileID)
                if (currentId != null) zombieIds.Add(currentId);
            }
        }

        if (zombieIds.Count == 0) return 0;

        report?.AppendLine($"  YAML: found {zombieIds.Count} zombie block(s) to remove.");

        // Pass 2: rebuild file without zombie blocks and without their component refs
        var sb = new StringBuilder(text.Length);
        bool skipping = false;

        foreach (var rawLine in lines)
        {
            string line = rawLine.TrimEnd('\r');

            if (line.StartsWith("--- !u!"))
            {
                skipping = false;
                int amp = line.LastIndexOf('&');
                string id = amp >= 0 ? line.Substring(amp + 1).Trim() : null;
                if (id != null && zombieIds.Contains(id))
                {
                    skipping = true;
                    continue;
                }
            }

            if (skipping) continue;

            // Remove component list entries that reference zombie fileIDs
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("- component: {fileID: "))
            {
                int s = trimmed.IndexOf("fileID: ") + "fileID: ".Length;
                int e = trimmed.IndexOf('}', s);
                if (s > 0 && e > s)
                {
                    string refId = trimmed.Substring(s, e - s).Trim();
                    if (zombieIds.Contains(refId)) continue;
                }
            }

            sb.Append(rawLine.TrimEnd('\r')).Append('\n');
        }

        // Remove trailing newline added by the loop if original didn't have one
        string result = sb.ToString();
        if (!text.EndsWith("\n") && result.EndsWith("\n"))
            result = result.TrimEnd('\n');

        File.WriteAllText(diskPath, result, Encoding.UTF8);
        return zombieIds.Count;
    }

    // ── Unity API marker repair ───────────────────────────────────────────────

    static int AddOrUpdateMarkers(Scene scene, StringBuilder report)
    {
        System.Type courseSmType = System.Type.GetType("Golfin.Course.SurfaceMarker, Assembly-CSharp");
        FieldInfo stField = courseSmType?.GetField("surfaceType");
        if (courseSmType == null || stField == null)
        {
            Debug.LogWarning($"[RepairTool] Golfin.Course.SurfaceMarker not found — skipping {scene.name}.");
            return 0;
        }

        int added = 0, updated = 0, ok = 0;

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var courseComp in root.GetComponentsInChildren(courseSmType, true))
            {
                var go = ((Component)courseComp).gameObject;
                int courseTypeInt = (int)stField.GetValue(courseComp);
                SurfaceType physType = SurfaceMarkerMap.MapCourseToPhysics(courseTypeInt);

                var physMarker = go.GetComponent<SurfaceMarker>();
                if (physMarker == null)
                {
                    physMarker = go.AddComponent<SurfaceMarker>();
                    physMarker.Type = physType;
                    EditorUtility.SetDirty(go);
                    added++;
                    report?.AppendLine($"  ADDED Type={physType} to {GetPath(go)}");
                }
                else if (physMarker.Type != physType)
                {
                    physMarker.Type = physType;
                    EditorUtility.SetDirty(go);
                    updated++;
                    report?.AppendLine($"  UPDATED → Type={physType} on {GetPath(go)}");
                }
                else
                {
                    ok++;
                }
            }
        }

        string summary = $"Markers: added {added}, updated {updated}, ok {ok}.";
        Debug.Log($"[RepairTool] {scene.name}: {summary}");
        report?.AppendLine($"  {summary}");
        return added + updated;
    }

    static void VerifyScene(Scene scene, ref int validCount, ref int brokenCount)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var col in root.GetComponentsInChildren<Collider>(true))
            {
                if (!(col is MeshCollider) && !(col is BoxCollider)) continue;

                foreach (var c in col.gameObject.GetComponents<Component>())
                    if (c == null) brokenCount++;

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
