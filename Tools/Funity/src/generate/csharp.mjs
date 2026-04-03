function indent(level) {
  return "    ".repeat(level);
}

function escapeString(value) {
  return String(value ?? "").replace(/\\/g, "\\\\").replace(/"/g, '\\"').replace(/\r?\n/g, "\\n");
}

function findAssetFile(assetManifest, imageRef) {
  if (!assetManifest?.assets) {
    return null;
  }

  return assetManifest.assets.find((asset) => asset.imageRef === imageRef)?.fileName ?? null;
}

function emitNode(node, scene, assetManifest, level = 2) {
  const lines = [];
  const varName = `node_${node.id.replace(/[^\w]/g, "_")}`;
  const typeComment = node.type === "TEXT" ? "Text" : node.asset?.kind === "image" ? "Image" : "Panel";

  lines.push(`${indent(level)}var ${varName} = CreateNode("${escapeString(node.name)}", parent);`);
  lines.push(
    `${indent(level)}ApplyRect(${varName}, ${node.layout.x.toFixed(2)}f, ${node.layout.y.toFixed(2)}f, ${node.layout.width.toFixed(2)}f, ${node.layout.height.toFixed(2)}f);`
  );
  lines.push(
    `${indent(level)}ApplyVisual(${varName}, "${node.visuals.fillColor}", "${node.visuals.strokeColor}", ${node.visuals.strokeWeight.toFixed(2)}f, ${node.visuals.cornerRadius.toFixed(2)}f, ${node.visuals.opacity.toFixed(3)}f);`
  );
  lines.push(`${indent(level)}// Figma type: ${node.type} -> suggested Unity role: ${typeComment}`);

  if (node.text) {
    lines.push(
      `${indent(level)}ApplyText(${varName}, "${escapeString(node.text.content)}", "${escapeString(node.text.fontFamily)}", ${node.text.fontSize.toFixed(2)}f, ${node.text.fontWeight}, "${node.text.color}", ${node.text.lineHeightPx.toFixed(2)}f, "${node.text.textAlignHorizontal}", "${node.text.textAlignVertical}");`
    );
  }

  if (node.asset?.kind === "image") {
    const fileName = findAssetFile(assetManifest, node.asset.imageRef);
    lines.push(
      `${indent(level)}// Image fill detected. Bind sprite for imageRef "${escapeString(node.asset.imageRef)}" with scale mode "${escapeString(node.asset.scaleMode)}"${fileName ? ` from "assets/${escapeString(fileName)}"` : ""}.`
    );
  }

  if (node.asset?.kind === "gradient") {
    lines.push(`${indent(level)}// Gradient fill detected. Manual material or shader mapping will be required.`);
  }

  for (const child of node.children) {
    lines.push(`${indent(level)}{`);
    lines.push(`${indent(level + 1)}var parent = ${varName};`);
    lines.push(...emitNode(child, scene, assetManifest, level + 1));
    lines.push(`${indent(level)}}`);
  }

  return lines;
}

export function generateUnityImporter(scene, assetManifest) {
  const lines = [
    "using UnityEngine;",
    "using UnityEngine.UI;",
    "using TMPro;",
    "",
    "public static class GeneratedFigmaScreenImporter",
    "{",
    "    public static void Build(Transform root)",
    "    {",
    `        // Screen: ${scene.name}`,
    `        // Canvas size: ${scene.canvas.width} x ${scene.canvas.height}`,
    ...(assetManifest?.count ? [`        // Downloaded image assets: ${assetManifest.count}`] : []),
    "        var parent = root;",
    ...emitNode(scene.root, scene, assetManifest, 2),
    "    }",
    "",
    "    private static GameObject CreateNode(string name, Transform parent)",
    "    {",
    "        var go = new GameObject(name);",
    "        var rect = go.AddComponent<RectTransform>();",
    "        rect.SetParent(parent, false);",
    "        return go;",
    "    }",
    "",
    "    private static void ApplyRect(GameObject go, float x, float y, float width, float height)",
    "    {",
    "        var rect = go.GetComponent<RectTransform>();",
    "        rect.anchorMin = new Vector2(0f, 1f);",
    "        rect.anchorMax = new Vector2(0f, 1f);",
    "        rect.pivot = new Vector2(0f, 1f);",
    "        rect.sizeDelta = new Vector2(width, height);",
    "        rect.anchoredPosition = new Vector2(x, -y);",
    "    }",
    "",
    "    private static void ApplyVisual(GameObject go, string fill, string stroke, float strokeWeight, float cornerRadius, float opacity)",
    "    {",
    "        var color = ParseColor(fill);",
    "        if (color.a <= 0f)",
    "        {",
    "            return;",
    "        }",
    "",
    "        var image = go.GetComponent<Image>();",
    "        if (image == null)",
    "        {",
    "            image = go.AddComponent<Image>();",
    "        }",
    "",
    "        image.color = color;",
    "        // Corner radius and stroke need a sliced sprite or custom material to match Figma exactly.",
    "    }",
    "",
    "    private static void ApplyText(GameObject go, string content, string fontFamily, float fontSize, int fontWeight, string color, float lineHeight, string hAlign, string vAlign)",
    "    {",
    "        var text = go.GetComponent<TextMeshProUGUI>();",
    "        if (text == null)",
    "        {",
    "            text = go.AddComponent<TextMeshProUGUI>();",
    "        }",
    "",
    "        text.text = content;",
    "        text.fontSize = fontSize;",
    "        text.color = ParseColor(color);",
    "        text.enableWordWrapping = false;",
    "        text.alignment = MapAlignment(hAlign, vAlign);",
    "        text.lineSpacing = lineHeight - fontSize;",
    "        // Map fontFamily/fontWeight to real TMP font assets in your Unity project.",
    "    }",
    "",
    "    private static Color ParseColor(string hex)",
    "    {",
    "        if (ColorUtility.TryParseHtmlString(hex, out var color))",
    "        {",
    "            return color;",
    "        }",
    "",
    "        return Color.clear;",
    "    }",
    "",
    "    private static TextAlignmentOptions MapAlignment(string hAlign, string vAlign)",
    "    {",
    "        if (hAlign == \"CENTER\" && vAlign == \"CENTER\") return TextAlignmentOptions.Center;",
    "        if (hAlign == \"CENTER\") return TextAlignmentOptions.Top;",
    "        if (hAlign == \"RIGHT\") return TextAlignmentOptions.TopRight;",
    "        return TextAlignmentOptions.TopLeft;",
    "    }",
    "}"
  ];

  return lines.join("\n");
}
