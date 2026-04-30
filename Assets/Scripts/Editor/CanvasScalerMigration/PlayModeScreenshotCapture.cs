// ONE-SHOT play-mode screenshot capture for 8_5_action_buttons review.
// Captures on first Update, then self-destructs this script file.
// DO NOT check in — auto-deleted after capture.
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Captures a screenshot on the first Update after play mode starts.
/// Used to get a fresh screenshot after the sprite-wiring fix for 8_5_action_buttons.
/// </summary>
public class PlayModeScreenshotCapture : MonoBehaviour
{
    const string ScreenshotDir = "Assets/Screenshots";
    bool _captured = false;
    int _frameCount = 0;

    void Update()
    {
        if (_captured) return;
        _frameCount++;
        // Wait 5 seconds worth of frames (assuming ~60fps = 300 frames)
        if (_frameCount < 300) return;

        _captured = true;
        if (!Directory.Exists(ScreenshotDir))
            Directory.CreateDirectory(ScreenshotDir);

        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string pngPath = $"{ScreenshotDir}/screenshot_{timestamp}_v2_8_5.png";
        ScreenCapture.CaptureScreenshot(pngPath, 1);
        Debug.Log($"[PlayModeScreenshotCapture] Screenshot captured to {pngPath}");

        Destroy(gameObject);
    }
}

// Editor-side auto-adder: adds the capture component on play mode start
[InitializeOnLoad]
public static class PlayModeScreenshotAutoAdder
{
    const string AddedKey = "PlayModeScreenshotCapture_Added_v2";

    static PlayModeScreenshotAutoAdder()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        if (EditorPrefs.GetBool(AddedKey, false)) return;
        EditorPrefs.SetBool(AddedKey, true);

        // Add the capture component to a new GO
        var go = new GameObject("__PlayModeScreenshotCapture__");
        go.AddComponent<PlayModeScreenshotCapture>();
        Debug.Log("[PlayModeScreenshotAutoAdder] Added screenshot capture component.");
    }
}
