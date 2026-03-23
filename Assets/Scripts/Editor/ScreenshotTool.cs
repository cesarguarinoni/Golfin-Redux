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
        string pngPath = $"{ScreenshotDir}/screenshot_{timestamp}.png";

        ScreenCapture.CaptureScreenshot(pngPath);
        Debug.Log($"[ScreenshotTool] Captured to {pngPath}");

        // Schedule compression after Unity writes the file (two frames to be safe)
        EditorApplication.delayCall += () =>
        {
            EditorApplication.delayCall += () =>
            {
                CompressToJpg(pngPath, 800);
                AssetDatabase.Refresh();
            };
        };
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

    private static void CompressToJpg(string pngPath, int maxWidth)
    {
        if (!File.Exists(pngPath)) return;

        var bytes = File.ReadAllBytes(pngPath);
        var tex = new Texture2D(2, 2);
        tex.LoadImage(bytes);

        int newW = tex.width > maxWidth ? maxWidth : tex.width;
        float ratio = (float)newW / tex.width;
        int newH = Mathf.RoundToInt(tex.height * ratio);

        var rt = RenderTexture.GetTemporary(newW, newH);
        Graphics.Blit(tex, rt);
        var resized = new Texture2D(newW, newH, TextureFormat.RGB24, false);
        RenderTexture.active = rt;
        resized.ReadPixels(new Rect(0, 0, newW, newH), 0, 0);
        resized.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        var jpg = resized.EncodeToJPG(75);
        string jpgPath = pngPath.Replace(".png", ".jpg");
        File.WriteAllBytes(jpgPath, jpg);

        // Clean up the large PNG and its .meta
        File.Delete(pngPath);
        string metaPath = pngPath + ".meta";
        if (File.Exists(metaPath)) File.Delete(metaPath);

        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(resized);
        Debug.Log($"[ScreenshotTool] Compressed to {jpgPath} ({jpg.Length / 1024}KB)");
    }
}
