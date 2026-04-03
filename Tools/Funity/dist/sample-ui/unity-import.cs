using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class GeneratedFigmaScreenImporter
{
    public static void Build(Transform root)
    {
        // Screen: Sample Screen
        // Canvas size: 390 x 844
        var parent = root;
        var node_1_1 = CreateNode("Sample Screen", parent);
        ApplyRect(node_1_1, 0.00f, 0.00f, 390.00f, 844.00f);
        ApplyVisual(node_1_1, "#F8F4ECFF", "#00000000", 0.00f, 0.00f, 1.000f);
        // Figma type: FRAME -> suggested Unity role: Panel
        {
            var parent = node_1_1;
            var node_1_2 = CreateNode("Header", parent);
            ApplyRect(node_1_2, 24.00f, 24.00f, 342.00f, 96.00f);
            ApplyVisual(node_1_2, "#2E2621FF", "#00000000", 0.00f, 24.00f, 1.000f);
            // Figma type: FRAME -> suggested Unity role: Panel
            {
                var parent = node_1_2;
                var node_1_3 = CreateNode("Title", parent);
                ApplyRect(node_1_3, 20.00f, 24.00f, 180.00f, 32.00f);
                ApplyVisual(node_1_3, "#FFFAF4FF", "#00000000", 0.00f, 0.00f, 1.000f);
                // Figma type: TEXT -> suggested Unity role: Text
                ApplyText(node_1_3, "Welcome back", "Inter", 28.00f, 700, "#FFFAF4FF", 32.00f, "LEFT", "TOP");
            }
        }
        {
            var parent = node_1_1;
            var node_1_4 = CreateNode("CTA Button", parent);
            ApplyRect(node_1_4, 24.00f, 156.00f, 200.00f, 56.00f);
            ApplyVisual(node_1_4, "#DB6A35FF", "#00000000", 0.00f, 18.00f, 1.000f);
            // Figma type: FRAME -> suggested Unity role: Panel
            {
                var parent = node_1_4;
                var node_1_5 = CreateNode("CTA Label", parent);
                ApplyRect(node_1_5, 32.00f, 17.00f, 136.00f, 22.00f);
                ApplyVisual(node_1_5, "#FFFCF6FF", "#00000000", 0.00f, 0.00f, 1.000f);
                // Figma type: TEXT -> suggested Unity role: Text
                ApplyText(node_1_5, "Start Session", "Inter", 18.00f, 600, "#FFFCF6FF", 22.00f, "CENTER", "CENTER");
            }
        }
    }

    private static GameObject CreateNode(string name, Transform parent)
    {
        var go = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        return go;
    }

    private static void ApplyRect(GameObject go, float x, float y, float width, float height)
    {
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, -y);
    }

    private static void ApplyVisual(GameObject go, string fill, string stroke, float strokeWeight, float cornerRadius, float opacity)
    {
        var color = ParseColor(fill);
        if (color.a <= 0f)
        {
            return;
        }

        var image = go.GetComponent<Image>();
        if (image == null)
        {
            image = go.AddComponent<Image>();
        }

        image.color = color;
        // Corner radius and stroke need a sliced sprite or custom material to match Figma exactly.
    }

    private static void ApplyText(GameObject go, string content, string fontFamily, float fontSize, int fontWeight, string color, float lineHeight, string hAlign, string vAlign)
    {
        var text = go.GetComponent<TextMeshProUGUI>();
        if (text == null)
        {
            text = go.AddComponent<TextMeshProUGUI>();
        }

        text.text = content;
        text.fontSize = fontSize;
        text.color = ParseColor(color);
        text.enableWordWrapping = false;
        text.alignment = MapAlignment(hAlign, vAlign);
        text.lineSpacing = lineHeight - fontSize;
        // Map fontFamily/fontWeight to real TMP font assets in your Unity project.
    }

    private static Color ParseColor(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out var color))
        {
            return color;
        }

        return Color.clear;
    }

    private static TextAlignmentOptions MapAlignment(string hAlign, string vAlign)
    {
        if (hAlign == "CENTER" && vAlign == "CENTER") return TextAlignmentOptions.Center;
        if (hAlign == "CENTER") return TextAlignmentOptions.Top;
        if (hAlign == "RIGHT") return TextAlignmentOptions.TopRight;
        return TextAlignmentOptions.TopLeft;
    }
}