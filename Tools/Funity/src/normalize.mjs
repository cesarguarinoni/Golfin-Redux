function sanitizeName(name, fallback) {
  return (name || fallback || "Node").replace(/[^\w\s-]/g, "").trim() || fallback || "Node";
}

function px(value) {
  return typeof value === "number" ? value : 0;
}

function colorToHex(color, opacity = 1) {
  if (!color) {
    return "#00000000";
  }

  const r = Math.round((color.r ?? 0) * 255).toString(16).padStart(2, "0");
  const g = Math.round((color.g ?? 0) * 255).toString(16).padStart(2, "0");
  const b = Math.round((color.b ?? 0) * 255).toString(16).padStart(2, "0");
  const a = Math.round(opacity * 255).toString(16).padStart(2, "0");

  return `#${r}${g}${b}${a}`.toUpperCase();
}

function primaryPaint(node) {
  return (node.fills ?? []).find((paint) => paint.visible !== false) ?? null;
}

function normalizeText(node) {
  const style = node.style ?? {};
  const fill = primaryPaint(node);

  return {
    content: node.characters ?? "",
    fontFamily: style.fontFamily ?? "Unknown",
    fontWeight: style.fontWeight ?? 400,
    fontSize: style.fontSize ?? 14,
    lineHeightPx: style.lineHeightPx ?? style.fontSize ?? 14,
    textAlignHorizontal: style.textAlignHorizontal ?? "LEFT",
    textAlignVertical: style.textAlignVertical ?? "TOP",
    letterSpacing: style.letterSpacing ?? 0,
    color: colorToHex(fill?.color, fill?.opacity ?? 1)
  };
}

function normalizeVisuals(node) {
  const fill = primaryPaint(node);
  const strokes = node.strokes ?? [];
  const stroke = strokes.find((paint) => paint.visible !== false) ?? null;

  return {
    fillColor: colorToHex(fill?.color, fill?.opacity ?? 1),
    strokeColor: colorToHex(stroke?.color, stroke?.opacity ?? 1),
    strokeWeight: node.strokeWeight ?? 0,
    cornerRadius: node.cornerRadius ?? 0,
    opacity: node.opacity ?? 1
  };
}

function normalizeNode(node, parentBounds = null) {
  const absolute = node.absoluteBoundingBox ?? {};
  const x = px(absolute.x) - px(parentBounds?.x);
  const y = px(absolute.y) - px(parentBounds?.y);
  const width = px(absolute.width);
  const height = px(absolute.height);

  const base = {
    id: node.id ?? sanitizeName(node.name, "node"),
    name: sanitizeName(node.name, node.type),
    type: node.type,
    layout: {
      x,
      y,
      width,
      height,
      rotation: node.rotation ?? 0,
      layoutMode: node.layoutMode ?? "NONE",
      constraints: node.constraints ?? null
    },
    visuals: normalizeVisuals(node),
    text: node.type === "TEXT" ? normalizeText(node) : null,
    effects: (node.effects ?? []).map((effect) => ({
      type: effect.type,
      visible: effect.visible !== false,
      radius: effect.radius ?? 0
    })),
    asset: deriveAsset(node),
    children: []
  };

  base.children = (node.children ?? []).map((child) => normalizeNode(child, absolute));
  return base;
}

function deriveAsset(node) {
  const fill = primaryPaint(node);

  if (fill?.type === "IMAGE") {
    return {
      kind: "image",
      imageRef: fill.imageRef ?? null,
      scaleMode: fill.scaleMode ?? "FILL"
    };
  }

  if (fill?.type === "GRADIENT_LINEAR" || fill?.type === "GRADIENT_RADIAL") {
    return {
      kind: "gradient",
      gradientStops: fill.gradientStops ?? []
    };
  }

  return null;
}

function collectFonts(node, output = new Map()) {
  if (node.text?.fontFamily) {
    const existing = output.get(node.text.fontFamily);
    if (existing) {
      existing.weights.add(node.text.fontWeight);
    } else {
      output.set(node.text.fontFamily, {
        family: node.text.fontFamily,
        weights: new Set([node.text.fontWeight])
      });
    }
  }

  for (const child of node.children) {
    collectFonts(child, output);
  }

  return output;
}

function finalizeFonts(fontMap) {
  return [...fontMap.values()].map((entry) => ({
    family: entry.family,
    weights: [...entry.weights].sort((a, b) => a - b)
  }));
}

export function normalizeScreen(screenNode) {
  const scene = normalizeNode(screenNode);

  return {
    name: scene.name,
    root: scene,
    canvas: {
      width: scene.layout.width,
      height: scene.layout.height
    },
    fonts: finalizeFonts(collectFonts(scene)),
    metadata: {
      sourceType: "figma-json",
      notes: [
        "Coordinates are absolute-to-parent translations from Figma absoluteBoundingBox.",
        "Unity generator currently assumes top-left Figma coordinates and converts them to anchored positions."
      ]
    }
  };
}
