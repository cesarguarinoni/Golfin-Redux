function flatten(node, depth = 0, rows = []) {
  rows.push({ node, depth });
  for (const child of node.children) {
    flatten(child, depth + 1, rows);
  }
  return rows;
}

export function generateSpecMarkdown(scene, assetManifest) {
  const rows = flatten(scene.root);
  const lines = [
    `# ${scene.name}`,
    "",
    "## Screen",
    "",
    `- Canvas: ${scene.canvas.width} x ${scene.canvas.height}`,
    `- Source: ${scene.metadata.sourceType}`,
    "",
    "## Fonts",
    ""
  ];

  if (scene.fonts.length === 0) {
    lines.push("- No text styles detected");
  } else {
    for (const font of scene.fonts) {
      lines.push(`- ${font.family}: weights ${font.weights.join(", ")}`);
    }
  }

  lines.push("");
  lines.push("## Assets");
  lines.push("");

  if (!assetManifest?.assets?.length) {
    lines.push("- No downloaded image assets");
  } else {
    for (const asset of assetManifest.assets) {
      lines.push(
        `- ${asset.nodeName}: ${asset.fileName ?? "missing download"} (${asset.imageRef}, ${asset.scaleMode})`
      );
    }
  }

  lines.push("");
  lines.push("## Hierarchy");
  lines.push("");

  for (const { node, depth } of rows) {
    const prefix = `${"  ".repeat(depth)}-`;
    const size = `${node.layout.width} x ${node.layout.height}`;
    const position = `(${node.layout.x}, ${node.layout.y})`;
    lines.push(`${prefix} ${node.name} [${node.type}] size ${size} at ${position}`);

    if (node.text) {
      lines.push(
        `${"  ".repeat(depth + 1)}text "${node.text.content}" / ${node.text.fontFamily} ${node.text.fontSize}px / ${node.text.color}`
      );
    }

    if (node.asset?.kind === "image") {
      lines.push(`${"  ".repeat(depth + 1)}imageRef ${node.asset.imageRef ?? "missing"}`);
    }

    if (node.asset?.kind === "gradient") {
      lines.push(`${"  ".repeat(depth + 1)}gradient fill requires custom Unity mapping`);
    }
  }

  lines.push("");
  lines.push("## Unity Mapping Notes");
  lines.push("");
  lines.push("- Convert Figma top-left coordinates to Unity anchored positions.");
  lines.push("- Map text nodes to TextMeshProUGUI or UI Toolkit Labels.");
  lines.push("- Map fills to Image components or VisualElement backgrounds.");
  lines.push("- Use the generated YAML as the deterministic scene contract.");

  return lines.join("\n");
}
