using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

/// <summary>
/// Reads current scene hierarchy values and generates a config file
/// that CreateUIScreen uses as source of truth.
///
/// Workflow:
///   1. Cesar tweaks fonts/positions in Unity Inspector
///   2. Tools → QA → Export Scene Values (this script)
///   3. Saves to Assets/Code/Data/screen_values.json
///   4. CreateUIScreen reads from that JSON instead of hardcoded values
///   5. Push to GitHub — Kai's scripts pick up the changes automatically
///
/// This means manual Unity tweaks survive code regeneration.
/// </summary>
public class SceneToCodeSync
{
    const string OutputPath = "Assets/Code/Data/screen_values.json";

    [MenuItem("Tools/QA/Export Scene Values")]
    public static void ExportSceneValues()
    {
        var root = GameObject.Find("Canvas");
        if (root == null)
        {
            Debug.LogWarning("[Sync] No Canvas found. Run 'Create GOLFIN UI Scene' first.");
            return;
        }

        var screens = new Dictionary<string, Dictionary<string, object>>();

        // Walk all screen panels
        foreach (Transform screenT in root.transform)
        {
            var screenData = new Dictionary<string, object>();
            ExportNode(screenT, screenData, "");
            screens[screenT.name] = screenData;
        }

        // Build JSON manually (avoiding dependency on Newtonsoft)
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"exported_at\": \"{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\",");
        sb.AppendLine($"  \"resolution\": [1170, 2532],");
        sb.AppendLine("  \"screens\": {");

        int screenIdx = 0;
        foreach (var screen in screens)
        {
            sb.AppendLine($"    \"{screen.Key}\": {{");
            int propIdx = 0;
            foreach (var prop in screen.Value)
            {
                string comma = propIdx < screen.Value.Count - 1 ? "," : "";
                sb.AppendLine($"      \"{prop.Key}\": {FormatValue(prop.Value)}{comma}");
                propIdx++;
            }
            string screenComma = screenIdx < screens.Count - 1 ? "," : "";
            sb.AppendLine($"    }}{screenComma}");
            screenIdx++;
        }

        sb.AppendLine("  }");
        sb.AppendLine("}");

        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
        File.WriteAllText(OutputPath, sb.ToString());
        AssetDatabase.Refresh();

        Debug.Log($"[Sync] ✅ Exported scene values to {OutputPath}");
        Debug.Log($"[Sync] {screens.Count} screens, commit & push to preserve your changes.");
    }

    static void ExportNode(Transform t, Dictionary<string, object> data, string prefix)
    {
        string path = string.IsNullOrEmpty(prefix) ? t.name : $"{prefix}/{t.name}";

        var rt = t.GetComponent<RectTransform>();
        if (rt != null)
        {
            data[$"{path}.anchorMin"] = $"[{rt.anchorMin.x:F4}, {rt.anchorMin.y:F4}]";
            data[$"{path}.anchorMax"] = $"[{rt.anchorMax.x:F4}, {rt.anchorMax.y:F4}]";
            data[$"{path}.anchoredPosition"] = $"[{rt.anchoredPosition.x:F1}, {rt.anchoredPosition.y:F1}]";
            data[$"{path}.sizeDelta"] = $"[{rt.sizeDelta.x:F1}, {rt.sizeDelta.y:F1}]";
            data[$"{path}.pivot"] = $"[{rt.pivot.x:F2}, {rt.pivot.y:F2}]";
        }

        var tmp = t.GetComponent<TextMeshProUGUI>();
        if (tmp != null)
        {
            data[$"{path}.fontSize"] = tmp.fontSize;
            data[$"{path}.color"] = $"\"#{ColorUtility.ToHtmlStringRGBA(tmp.color)}\"";
            data[$"{path}.fontStyle"] = $"\"{tmp.fontStyle}\"";
            data[$"{path}.fontWeight"] = $"\"{tmp.fontWeight}\"";
            data[$"{path}.characterSpacing"] = tmp.characterSpacing;
            data[$"{path}.lineSpacing"] = tmp.lineSpacing;
            data[$"{path}.paragraphSpacing"] = tmp.paragraphSpacing;
            data[$"{path}.wordSpacing"] = tmp.wordSpacing;
            data[$"{path}.alignment"] = $"\"{tmp.alignment}\"";
            data[$"{path}.textWrappingMode"] = $"\"{tmp.textWrappingMode}\"";
            if (tmp.font != null)
            {
                data[$"{path}.font"] = $"\"{tmp.font.name}\"";
                data[$"{path}.fontAssetPath"] = $"\"{UnityEditor.AssetDatabase.GetAssetPath(tmp.font)}\"";
            }
            data[$"{path}.outlineWidth"] = tmp.outlineWidth;
            data[$"{path}.outlineColor"] = $"\"#{ColorUtility.ToHtmlStringRGBA(tmp.outlineColor)}\"";
            data[$"{path}.enableAutoSizing"] = tmp.enableAutoSizing;
            if (tmp.enableAutoSizing)
            {
                data[$"{path}.fontSizeMin"] = tmp.fontSizeMin;
                data[$"{path}.fontSizeMax"] = tmp.fontSizeMax;
            }
            data[$"{path}.text"] = $"\"{EscapeJson(tmp.text)}\"";
        }

        var img = t.GetComponent<Image>();
        if (img != null)
        {
            data[$"{path}.imageColor"] = $"\"#{ColorUtility.ToHtmlStringRGBA(img.color)}\"";
            if (img.sprite != null)
                data[$"{path}.sprite"] = $"\"{img.sprite.name}\"";
            data[$"{path}.imageType"] = $"\"{img.type}\"";
            data[$"{path}.preserveAspect"] = img.preserveAspect;
            data[$"{path}.raycastTarget"] = img.raycastTarget;
        }

        // Button component
        var btn = t.GetComponent<UnityEngine.UI.Button>();
        if (btn != null)
        {
            data[$"{path}.buttonInteractable"] = btn.interactable;
            var nav = btn.navigation;
            data[$"{path}.buttonNavMode"] = $"\"{nav.mode}\"";
        }

        // PressableButton (custom)
        var pressable = t.GetComponent<PressableButton>();
        if (pressable != null)
        {
            data[$"{path}.hasPressableButton"] = true;
        }

        // LayoutElement
        var le = t.GetComponent<UnityEngine.UI.LayoutElement>();
        if (le != null)
        {
            data[$"{path}.layoutPreferredWidth"] = le.preferredWidth;
            data[$"{path}.layoutPreferredHeight"] = le.preferredHeight;
            data[$"{path}.layoutMinWidth"] = le.minWidth;
            data[$"{path}.layoutMinHeight"] = le.minHeight;
            data[$"{path}.layoutFlexibleWidth"] = le.flexibleWidth;
            data[$"{path}.layoutFlexibleHeight"] = le.flexibleHeight;
        }

        // CanvasGroup
        var cg = t.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            data[$"{path}.canvasGroupAlpha"] = cg.alpha;
            data[$"{path}.canvasGroupInteractable"] = cg.interactable;
            data[$"{path}.canvasGroupBlocksRaycasts"] = cg.blocksRaycasts;
        }

        // Recurse children
        foreach (Transform child in t)
        {
            ExportNode(child, data, path);
        }
    }

    static string FormatValue(object val)
    {
        if (val is float f) return f.ToString("F2");
        if (val is string s)
        {
            // Already formatted strings (with quotes or brackets)
            if (s.StartsWith("\"") || s.StartsWith("[")) return s;
            return $"\"{s}\"";
        }
        return val.ToString();
    }

    static string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
    }

    /// <summary>
    /// Shows a diff between current scene values and the saved JSON.
    /// Useful to see what changed since last export.
    /// </summary>
    [MenuItem("Tools/QA/Show Scene Changes")]
    public static void ShowChanges()
    {
        if (!File.Exists(OutputPath))
        {
            Debug.Log("[Sync] No saved values. Run 'Export Scene Values' first.");
            return;
        }

        // Quick and dirty: re-export to temp, diff
        string saved = File.ReadAllText(OutputPath);
        string tempPath = "Temp/scene_values_current.json";

        // Export current
        ExportSceneValues();
        string current = File.ReadAllText(OutputPath);

        // Restore saved
        File.WriteAllText(OutputPath, saved);

        // Compare line by line
        var savedLines = saved.Split('\n');
        var currentLines = current.Split('\n');

        int diffs = 0;
        Debug.Log("[Sync] ── Changes since last export ──");

        var savedDict = new Dictionary<string, string>();
        foreach (var line in savedLines)
        {
            var m = Regex.Match(line.Trim(), "\"([^\"]+)\":\\s*(.+?)(?:,)?$");
            if (m.Success) savedDict[m.Groups[1].Value] = m.Groups[2].Value.TrimEnd(',');
        }

        foreach (var line in currentLines)
        {
            var m = Regex.Match(line.Trim(), "\"([^\"]+)\":\\s*(.+?)(?:,)?$");
            if (m.Success)
            {
                string key = m.Groups[1].Value;
                string val = m.Groups[2].Value.TrimEnd(',');
                if (savedDict.ContainsKey(key) && savedDict[key] != val)
                {
                    Debug.Log($"  CHANGED: {key}: {savedDict[key]} → {val}");
                    diffs++;
                }
                else if (!savedDict.ContainsKey(key))
                {
                    Debug.Log($"  NEW: {key}: {val}");
                    diffs++;
                }
            }
        }

        if (diffs == 0)
            Debug.Log("[Sync] No changes detected. ✅");
        else
            Debug.Log($"[Sync] {diffs} change(s) found. Run 'Export Scene Values' to save them.");
    }
}
