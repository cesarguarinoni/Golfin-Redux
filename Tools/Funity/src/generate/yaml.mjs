function quote(value) {
  return JSON.stringify(value ?? "");
}

function emitNode(node, level = 0) {
  const pad = "  ".repeat(level);
  const lines = [
    `${pad}- id: ${quote(node.id)}`,
    `${pad}  name: ${quote(node.name)}`,
    `${pad}  type: ${quote(node.type)}`,
    `${pad}  layout:`,
    `${pad}    x: ${node.layout.x}`,
    `${pad}    y: ${node.layout.y}`,
    `${pad}    width: ${node.layout.width}`,
    `${pad}    height: ${node.layout.height}`,
    `${pad}    rotation: ${node.layout.rotation}`,
    `${pad}    layoutMode: ${quote(node.layout.layoutMode)}`,
    `${pad}  visuals:`,
    `${pad}    fillColor: ${quote(node.visuals.fillColor)}`,
    `${pad}    strokeColor: ${quote(node.visuals.strokeColor)}`,
    `${pad}    strokeWeight: ${node.visuals.strokeWeight}`,
    `${pad}    cornerRadius: ${node.visuals.cornerRadius}`,
    `${pad}    opacity: ${node.visuals.opacity}`
  ];

  if (node.text) {
    lines.push(`${pad}  text:`);
    lines.push(`${pad}    content: ${quote(node.text.content)}`);
    lines.push(`${pad}    fontFamily: ${quote(node.text.fontFamily)}`);
    lines.push(`${pad}    fontWeight: ${node.text.fontWeight}`);
    lines.push(`${pad}    fontSize: ${node.text.fontSize}`);
    lines.push(`${pad}    lineHeightPx: ${node.text.lineHeightPx}`);
    lines.push(`${pad}    color: ${quote(node.text.color)}`);
  }

  if (node.asset) {
    lines.push(`${pad}  asset:`);
    lines.push(`${pad}    kind: ${quote(node.asset.kind)}`);
  }

  if (node.children.length > 0) {
    lines.push(`${pad}  children:`);
    for (const child of node.children) {
      lines.push(...emitNode(child, level + 2));
    }
  } else {
    lines.push(`${pad}  children: []`);
  }

  return lines;
}

export function generateSceneYaml(scene) {
  const lines = [
    `screen: ${quote(scene.name)}`,
    "canvas:",
    `  width: ${scene.canvas.width}`,
    `  height: ${scene.canvas.height}`,
    "fonts:"
  ];

  for (const font of scene.fonts) {
    lines.push(`  - family: ${quote(font.family)}`);
    lines.push(`    weights: [${font.weights.join(", ")}]`);
  }

  lines.push("nodes:");
  lines.push(...emitNode(scene.root, 1));

  return lines.join("\n");
}
