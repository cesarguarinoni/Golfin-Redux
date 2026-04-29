using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor utility to remove stale Pf_GOLFIN_Ball(Clone) GameObjects
/// that were accidentally baked into the scene file during play-mode sessions.
/// Run once after implementing BallAnimator.SpawnInstance parenting fix.
/// Menu: GOLFIN/Cleanup/Remove stale Pf_GOLFIN_Ball clones from active scene
/// </summary>
public static class CleanupStaleBallClones
{
    /// <summary>
    /// Silent (non-interactive) version for MCP script execution. Returns count of removed clones.
    /// </summary>
    public static int RemoveStaleBallClonesSilent()
    {
        var scene = SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        var toDelete = new System.Collections.Generic.List<GameObject>();
        foreach (var go in roots)
            if (go.name.StartsWith("Pf_GOLFIN_Ball"))
                toDelete.Add(go);

        if (toDelete.Count == 0)
        {
            Debug.Log("[CleanupStaleBallClones] No stale Pf_GOLFIN_Ball clones found at scene root.");
            return 0;
        }

        foreach (var go in toDelete)
        {
            Debug.Log($"[CleanupStaleBallClones] Removing: {go.name}");
            Object.DestroyImmediate(go);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[CleanupStaleBallClones] Removed {toDelete.Count} stale ball clone(s) from '{scene.name}'. Scene saved.");
        return toDelete.Count;
    }

    [MenuItem("GOLFIN/Cleanup/Remove stale Pf_GOLFIN_Ball clones from active scene")]
    static void RemoveStaleBallClonesFromActiveScene()
    {
        var scene = SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();

        var toDelete = new System.Collections.Generic.List<GameObject>();
        foreach (var go in roots)
        {
            if (go.name.StartsWith("Pf_GOLFIN_Ball"))
                toDelete.Add(go);
        }

        if (toDelete.Count == 0)
        {
            Debug.Log("[CleanupStaleBallClones] No stale Pf_GOLFIN_Ball clones found at scene root.");
            EditorUtility.DisplayDialog("Cleanup", "No stale Pf_GOLFIN_Ball clones found at scene root.", "OK");
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Remove stale ball clones",
            $"Found {toDelete.Count} Pf_GOLFIN_Ball* GameObjects at root of '{scene.name}'.\nRemove them?",
            "Remove",
            "Cancel");

        if (!confirmed) return;

        foreach (var go in toDelete)
        {
            Debug.Log($"[CleanupStaleBallClones] Removing: {go.name}");
            Object.DestroyImmediate(go);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[CleanupStaleBallClones] Removed {toDelete.Count} stale ball clone(s) from '{scene.name}'. Scene saved.");
        EditorUtility.DisplayDialog("Cleanup complete", $"Removed {toDelete.Count} stale Pf_GOLFIN_Ball clone(s). Scene saved.", "OK");
    }
}
