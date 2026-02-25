using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Captures Unity Game View screenshots for Visual QA comparison.
/// Saves to ProjectRoot/QA/Screenshots/
/// 
/// Usage: 
///   Unity menu → Tools → QA → Capture All Screens
///   Or call ScreenshotCapture.CaptureScreen("ScreenName") from code
/// </summary>
public class ScreenshotCapture
{
    static string OutputDir => Path.Combine(Application.dataPath, "..", "QA", "Screenshots", "Unity");

    [MenuItem("Tools/QA/Capture All Screens")]
    public static void CaptureAllScreens()
    {
        Directory.CreateDirectory(OutputDir);

        // Find all ScreenBase instances in scene
        var screens = Object.FindObjectsByType<ScreenBase>(FindObjectsSortMode.None);
        
        if (screens.Length == 0)
        {
            Debug.LogWarning("[QA] No screens found. Run 'Tools → Create GOLFIN UI Scene' first.");
            return;
        }

        // Disable all screens first
        foreach (var s in screens)
        {
            var cg = s.GetComponent<CanvasGroup>();
            if (cg != null) { cg.alpha = 0f; cg.blocksRaycasts = false; }
        }

        // Capture each screen one at a time
        foreach (var s in screens)
        {
            string screenName = s.gameObject.name;
            var cg = s.GetComponent<CanvasGroup>();
            if (cg != null) { cg.alpha = 1f; cg.blocksRaycasts = true; }

            // Force a repaint
            Canvas.ForceUpdateCanvases();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

            string path = Path.Combine(OutputDir, $"{screenName}.png");
            ScreenCapture.CaptureScreenshot(path, 1); // 1x scale
            Debug.Log($"[QA] Captured: {path}");

            if (cg != null) { cg.alpha = 0f; cg.blocksRaycasts = false; }
        }

        // Re-enable first screen
        if (screens.Length > 0)
        {
            var cg = screens[0].GetComponent<CanvasGroup>();
            if (cg != null) { cg.alpha = 1f; cg.blocksRaycasts = true; }
        }

        Debug.Log($"[QA] All {screens.Length} screens captured to {OutputDir}");
        EditorUtility.RevealInFinder(OutputDir);
    }

    [MenuItem("Tools/QA/Capture Current Screen")]
    public static void CaptureCurrentScreen()
    {
        Directory.CreateDirectory(OutputDir);

        // Find active screen (visible CanvasGroup)
        var screens = Object.FindObjectsByType<ScreenBase>(FindObjectsSortMode.None);
        foreach (var s in screens)
        {
            var cg = s.GetComponent<CanvasGroup>();
            if (cg != null && cg.alpha > 0.5f)
            {
                string path = Path.Combine(OutputDir, $"{s.gameObject.name}.png");
                ScreenCapture.CaptureScreenshot(path, 1);
                Debug.Log($"[QA] Captured: {path}");
                return;
            }
        }
        Debug.LogWarning("[QA] No visible screen found.");
    }
}
