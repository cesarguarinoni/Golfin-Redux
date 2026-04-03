using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class FunityCompareExporter
{
    [MenuItem("Funity/Export Selected UI For Compare")]
    public static void ExportSelectedUiForCompare()
    {
        var selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog(
                "Funity Compare Export",
                "Select a root GameObject in the Hierarchy first.",
                "OK"
            );
            return;
        }

        var defaultName = $"{SanitizeFileName(selected.name)}-funity-compare.txt";
        var targetPath = EditorUtility.SaveFilePanel(
            "Export Funity Compare Dump",
            Application.dataPath,
            defaultName,
            "txt"
        );

        if (string.IsNullOrEmpty(targetPath))
        {
            return;
        }

        var builder = new StringBuilder(8192);
        builder.AppendLine("# FUNITY UNITY COMPARE DUMP");
        builder.AppendLine($"root={selected.name}");
        builder.AppendLine($"scene={selected.scene.name}");
        builder.AppendLine($"exportedAt={System.DateTime.Now:O}");
        builder.AppendLine();

        AppendNode(selected.transform, builder, 0);

        File.WriteAllText(targetPath, builder.ToString(), Encoding.UTF8);
        EditorUtility.RevealInFinder(targetPath);
        Debug.Log($"Funity Compare dump exported to: {targetPath}");
    }

    private static void AppendNode(Transform current, StringBuilder builder, int depth)
    {
        var indent = new string(' ', depth * 2);
        var gameObject = current.gameObject;
        var rect = gameObject.GetComponent<RectTransform>();
        var image = gameObject.GetComponent<Image>();
        var rawImage = gameObject.GetComponent<RawImage>();
        var tmp = gameObject.GetComponent<TextMeshProUGUI>();
        var layoutComponents = CollectLayoutComponents(gameObject);

        builder.AppendLine($"{indent}- name={gameObject.name}");
        builder.AppendLine($"{indent}  active={gameObject.activeSelf}");

        if (rect != null)
        {
            builder.AppendLine($"{indent}  RectTransform.anchorMin={FormatVector2(rect.anchorMin)}");
            builder.AppendLine($"{indent}  RectTransform.anchorMax={FormatVector2(rect.anchorMax)}");
            builder.AppendLine($"{indent}  RectTransform.pivot={FormatVector2(rect.pivot)}");
            builder.AppendLine($"{indent}  RectTransform.anchoredPosition={FormatVector2(rect.anchoredPosition)}");
            builder.AppendLine($"{indent}  RectTransform.sizeDelta={FormatVector2(rect.sizeDelta)}");
            builder.AppendLine($"{indent}  RectTransform.offsetMin={FormatVector2(rect.offsetMin)}");
            builder.AppendLine($"{indent}  RectTransform.offsetMax={FormatVector2(rect.offsetMax)}");
        }

        if (tmp != null)
        {
            builder.AppendLine($"{indent}  TMP.text={EscapeInline(tmp.text)}");
            builder.AppendLine($"{indent}  TMP.font={EscapeInline(tmp.font != null ? tmp.font.name : "null")}");
            builder.AppendLine($"{indent}  font={EscapeInline(tmp.font != null ? tmp.font.name : "null")}");
            builder.AppendLine($"{indent}  TMP.fontSize={tmp.fontSize}");
            builder.AppendLine($"{indent}  TMP.color={ColorToHex(tmp.color)}");
            builder.AppendLine($"{indent}  TMP.alignment={tmp.alignment}");
        }

        if (image != null)
        {
            builder.AppendLine($"{indent}  Image.color={ColorToHex(image.color)}");
            builder.AppendLine($"{indent}  Image.sprite={EscapeInline(image.sprite != null ? image.sprite.name : "null")}");
            builder.AppendLine($"{indent}  Image.type={image.type}");
        }

        if (rawImage != null)
        {
            builder.AppendLine($"{indent}  RawImage.color={ColorToHex(rawImage.color)}");
            builder.AppendLine($"{indent}  RawImage.texture={EscapeInline(rawImage.texture != null ? rawImage.texture.name : "null")}");
        }

        foreach (var layout in layoutComponents)
        {
            builder.AppendLine($"{indent}  layout={layout}");
        }

        for (var index = 0; index < current.childCount; index += 1)
        {
            AppendNode(current.GetChild(index), builder, depth + 1);
        }
    }

    private static IEnumerable<string> CollectLayoutComponents(GameObject gameObject)
    {
        var components = new List<string>();

        if (gameObject.GetComponent<HorizontalLayoutGroup>() != null)
        {
            components.Add("HorizontalLayoutGroup");
        }

        if (gameObject.GetComponent<VerticalLayoutGroup>() != null)
        {
            components.Add("VerticalLayoutGroup");
        }

        if (gameObject.GetComponent<GridLayoutGroup>() != null)
        {
            components.Add("GridLayoutGroup");
        }

        if (gameObject.GetComponent<ContentSizeFitter>() != null)
        {
            components.Add("ContentSizeFitter");
        }

        if (gameObject.GetComponent<LayoutElement>() != null)
        {
            components.Add("LayoutElement");
        }

        return components;
    }

    private static string FormatVector2(Vector2 value)
    {
        return $"({value.x:0.###}, {value.y:0.###})";
    }

    private static string ColorToHex(Color color)
    {
        return $"#{ColorUtility.ToHtmlStringRGBA(color)}";
    }

    private static string EscapeInline(string value)
    {
        return (value ?? string.Empty).Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '-');
        }

        return value;
    }
}
