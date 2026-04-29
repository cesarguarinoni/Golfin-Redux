using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Golfin.Gameplay.UI.ShotUI;

/// <summary>
/// Surgical patcher: wires the three unwired _default* sprite fields on the
/// already-built action button hierarchy in LabScaffold.unity.
/// Does NOT rebuild anything. Runs in-editor only.
/// Menu: GOLFIN/Build/Patch Action Button Default Sprites (8.5)
/// </summary>
public static class ActionButtonsDefaultSpritePatcher
{
    [MenuItem("GOLFIN/Build/Patch Action Button Default Sprites (8.5)")]
    public static void PatchDefaultSprites()
    {
        // ── Coerce PNGs to Sprite import type ─────────────────────────────────
        CoerceSprite("Assets/Resources/Clubs/Controls/S_Controls_Driver_GOLFIN.png");
        CoerceSprite("Assets/Resources/Balls/Thumbnails/S_Controls_Ball_GOLFIN.png");
        CoerceSprite("Assets/Resources/Balls/Full/Golfin.png");

        // ── Load sprites ──────────────────────────────────────────────────────
        Sprite driverPortrait = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Resources/Clubs/Controls/S_Controls_Driver_GOLFIN.png");
        Sprite ballThumbnail  = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Resources/Balls/Thumbnails/S_Controls_Ball_GOLFIN.png");
        Sprite ballFull       = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Resources/Balls/Full/Golfin.png");

        if (driverPortrait == null)
        {
            Debug.LogError("[ActionButtonsDefaultSpritePatcher] S_Controls_Driver_GOLFIN.png not found as Sprite.");
            return;
        }
        if (ballThumbnail == null)
        {
            Debug.LogError("[ActionButtonsDefaultSpritePatcher] S_Controls_Ball_GOLFIN.png not found as Sprite.");
            return;
        }
        if (ballFull == null)
        {
            Debug.LogError("[ActionButtonsDefaultSpritePatcher] Balls/Full/Golfin.png not found as Sprite.");
            return;
        }

        // ── Find the target GameObjects ────────────────────────────────────────
        // ClubButtonWidget lives on "DriverButton" under ActionButtons_Cluster
        var clubWidget = FindComponentInScene<ClubButtonWidget>("DriverButton");
        if (clubWidget == null)
        {
            Debug.LogError("[ActionButtonsDefaultSpritePatcher] ClubButtonWidget on 'DriverButton' not found. Is LabScaffold.unity open?");
            return;
        }

        // BallButtonWidget lives on "GolfinButton" under ActionButtons_Cluster
        var ballWidget = FindComponentInScene<BallButtonWidget>("GolfinButton");
        if (ballWidget == null)
        {
            Debug.LogError("[ActionButtonsDefaultSpritePatcher] BallButtonWidget on 'GolfinButton' not found.");
            return;
        }

        // SpinPanelWidget lives on "SpinPanel" under ShotUI_Canvas
        var spinWidget = FindComponentInScene<SpinPanelWidget>("SpinPanel");
        if (spinWidget == null)
        {
            Debug.LogError("[ActionButtonsDefaultSpritePatcher] SpinPanelWidget on 'SpinPanel' not found.");
            return;
        }

        // ── Wire via SerializedObject ─────────────────────────────────────────
        var clubSo = new SerializedObject(clubWidget);
        var clubProp = clubSo.FindProperty("_defaultPortrait");
        if (clubProp == null)
        {
            Debug.LogError("[ActionButtonsDefaultSpritePatcher] '_defaultPortrait' property not found on ClubButtonWidget.");
            return;
        }
        clubProp.objectReferenceValue = driverPortrait;
        clubSo.ApplyModifiedProperties();
        Debug.Log("[ActionButtonsDefaultSpritePatcher] ClubButtonWidget._defaultPortrait -> S_Controls_Driver_GOLFIN");

        var ballSo = new SerializedObject(ballWidget);
        var ballProp = ballSo.FindProperty("_defaultThumbnail");
        if (ballProp == null)
        {
            Debug.LogError("[ActionButtonsDefaultSpritePatcher] '_defaultThumbnail' property not found on BallButtonWidget.");
            return;
        }
        ballProp.objectReferenceValue = ballThumbnail;
        ballSo.ApplyModifiedProperties();
        Debug.Log("[ActionButtonsDefaultSpritePatcher] BallButtonWidget._defaultThumbnail -> S_Controls_Ball_GOLFIN");

        var spinSo = new SerializedObject(spinWidget);
        var spinProp = spinSo.FindProperty("_defaultBallSprite");
        if (spinProp != null)
        {
            spinProp.objectReferenceValue = ballFull;
            spinSo.ApplyModifiedProperties();
            Debug.Log("[ActionButtonsDefaultSpritePatcher] SpinPanelWidget._defaultBallSprite -> Balls/Full/Golfin");
        }
        else
        {
            // _defaultBallSprite may not exist on SpinPanelWidget — log and continue (non-blocking per spec)
            Debug.LogWarning("[ActionButtonsDefaultSpritePatcher] '_defaultBallSprite' property not found on SpinPanelWidget. Non-blocking — skipping.");
        }

        // ── Mark scene dirty and save ─────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        Debug.Log("[ActionButtonsDefaultSpritePatcher] DONE — default sprite refs wired and scene saved.");
    }

    static T FindComponentInScene<T>(string goName) where T : Component
    {
        // Search all root GameObjects and their children for a GO with the given name.
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots)
        {
            var found = root.transform.Find(goName);
            if (found != null)
            {
                var comp = found.GetComponent<T>();
                if (comp != null) return comp;
            }
            // Deep search (recursive)
            var deep = FindInChildren<T>(root.transform, goName);
            if (deep != null) return deep;
        }
        return null;
    }

    static T FindInChildren<T>(Transform parent, string goName) where T : Component
    {
        foreach (Transform child in parent)
        {
            if (child.name == goName)
            {
                var comp = child.GetComponent<T>();
                if (comp != null) return comp;
            }
            var found = FindInChildren<T>(child, goName);
            if (found != null) return found;
        }
        return null;
    }

    static void CoerceSprite(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[ActionButtonsDefaultSpritePatcher] CoerceSprite: {System.IO.Path.GetFileName(assetPath)} -> Sprite");
        }
    }
}
