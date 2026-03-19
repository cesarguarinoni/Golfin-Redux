using UnityEngine;
using UnityEditor;
using System.IO;

public static class ScreenshotTool
{
    private static readonly string ScreenshotDir = "Assets/Screenshots";

    [MenuItem("GOLFIN/Screenshot/Capture Game View")]
    public static void CaptureGameView()
    {
        if (!Directory.Exists(ScreenshotDir))
            Directory.CreateDirectory(ScreenshotDir);

        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string path = $"{ScreenshotDir}/screenshot_{timestamp}.png";

        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"[ScreenshotTool] Saved to {path}");

        // Refresh so it appears in Project panel
        EditorApplication.delayCall += () => AssetDatabase.Refresh();
    }

    [MenuItem("GOLFIN/Screenshot/Capture Named")]
    public static void CaptureNamed()
    {
        if (!Directory.Exists(ScreenshotDir))
            Directory.CreateDirectory(ScreenshotDir);

        string path = EditorUtility.SaveFilePanel("Save Screenshot", ScreenshotDir, "screenshot", "png");
        if (!string.IsNullOrEmpty(path))
        {
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[ScreenshotTool] Saved to {path}");
            EditorApplication.delayCall += () => AssetDatabase.Refresh();
        }
    }
}
