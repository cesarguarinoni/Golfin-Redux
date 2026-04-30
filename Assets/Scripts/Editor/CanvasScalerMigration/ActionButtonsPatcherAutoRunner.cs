using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot [InitializeOnLoad] auto-runner that:
///  1. Calls ActionButtonsDefaultSpritePatcher.PatchDefaultSprites() if LabScaffold is open.
///  2. Waits for play mode to become available, then captures a screenshot.
///  3. Self-deletes.
///
/// Created by: golfin-implementer (ARCHITECT_REVIEW_FAIL fix for 8_5_action_buttons).
/// This file is auto-deleted after running.
/// </summary>
[InitializeOnLoad]
public static class ActionButtonsPatcherAutoRunner
{
    const string RanKey = "ActionButtonsPatcherAutoRunner_Ran_v2";

    static ActionButtonsPatcherAutoRunner()
    {
        if (EditorPrefs.GetBool(RanKey, false)) return;
        EditorApplication.delayCall += RunAndSelfDelete;
    }

    static void RunAndSelfDelete()
    {
        EditorPrefs.SetBool(RanKey, true);

        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!activeScene.name.Contains("LabScaffold"))
        {
            Debug.LogWarning("[ActionButtonsPatcherAutoRunner] LabScaffold.unity is not the active scene. " +
                "Switch to it and reopen this project, or run GOLFIN/Build/Patch Action Button Default Sprites (8.5) manually.");
        }
        else
        {
            Debug.Log("[ActionButtonsPatcherAutoRunner] Patching default sprites on LabScaffold...");
            ActionButtonsDefaultSpritePatcher.PatchDefaultSprites();
        }

        // Capture screenshot (clear old flag so the capture fires)
        EditorPrefs.SetBool("ScreenshotAutoCapture_Ran", false);
        EditorApplication.delayCall += CaptureScreenshot;

        SelfDelete();
    }

    static void CaptureScreenshot()
    {
        try
        {
            EditorApplication.ExecuteMenuItem("GOLFIN/Screenshot/Capture Game View");
            Debug.Log("[ActionButtonsPatcherAutoRunner] Screenshot captured.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[ActionButtonsPatcherAutoRunner] Screenshot capture failed (may need play mode): " + e.Message);
        }
    }

    static void SelfDelete()
    {
        const string selfPath = "Assets/Scripts/Editor/CanvasScalerMigration/ActionButtonsPatcherAutoRunner.cs";
        if (System.IO.File.Exists(UnityEngine.Application.dataPath.Replace("/Assets", "/") + selfPath.Replace("Assets/", "")))
        {
            AssetDatabase.DeleteAsset(selfPath);
            Debug.Log("[ActionButtonsPatcherAutoRunner] Self-deleted.");
        }
    }
}
