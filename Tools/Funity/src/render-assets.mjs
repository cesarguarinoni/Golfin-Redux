const EXCLUDED_TYPES = new Set([
  "DOCUMENT",
  "CANVAS",
  "PAGE",
  "TEXT",
  "SLICE",
  "CONNECTOR",
  "STICKY",
  "SHAPE_WITH_TEXT"
]);

const SVG_FRIENDLY_TYPES = new Set([
  "VECTOR",
  "BOOLEAN_OPERATION",
  "STAR",
  "LINE",
  "POLYGON"
]);

function sanitizeSegment(value, fallback) {
  const sanitized = String(value ?? fallback ?? "asset")
    .replace(/[^\w.-]+/g, "-")
    .replace(/^-+|-+$/g, "");

  return sanitized || fallback || "asset";
}

function hasRenderableBounds(node) {
  const box = node?.absoluteBoundingBox;
  return Boolean(box && box.width > 0 && box.height > 0);
}

function hasVisibleStyleContent(node) {
  const fills = (node.fills ?? []).some((paint) => paint.visible !== false);
  const strokes = (node.strokes ?? []).some((paint) => paint.visible !== false);
  const effects = (node.effects ?? []).some((effect) => effect.visible !== false);
  return fills || strokes || effects;
}

function isRenderableNode(node, depth) {
  if (!node || EXCLUDED_TYPES.has(node.type) || node.visible === false || !hasRenderableBounds(node)) {
    return false;
  }

  if (depth === 0) {
    return false;
  }

  if (node.type === "COMPONENT" || node.type === "INSTANCE") {
    return true;
  }

  return (node.children?.length ?? 0) === 0 || hasVisibleStyleContent(node) || depth >= 1;
}

function inferFormat(node) {
  return SVG_FRIENDLY_TYPES.has(node.type) ? "svg" : "png";
}

export function collectRenderableNodes(node, output = [], depth = 0, seen = new Set()) {
  if (isRenderableNode(node, depth) && !seen.has(node.id)) {
    seen.add(node.id);
    output.push({
      nodeId: node.id,
      nodeName: node.name,
      nodeType: node.type,
      format: inferFormat(node)
    });
  }

  for (const child of node.children ?? []) {
    collectRenderableNodes(child, output, depth + 1, seen);
  }

  return output;
}

export async function downloadRenderedNodeAssets(screenNode, options) {
  const candidates = collectRenderableNodes(screenNode);
  const debug = {
    selectedScreen: {
      nodeId: screenNode?.id ?? null,
      nodeName: screenNode?.name ?? null,
      nodeType: screenNode?.type ?? null
    },
    candidateCount: candidates.length,
    candidates: candidates.map((candidate) => ({
      nodeId: candidate.nodeId,
      nodeName: candidate.nodeName,
      nodeType: candidate.nodeType,
      format: candidate.format
    })),
    batches: []
  };

  if (candidates.length === 0) {
    return {
      assets: [],
      debug
    };
  }

  const groups = new Map();
  for (const candidate of candidates) {
    const list = groups.get(candidate.format) ?? [];
    list.push(candidate);
    groups.set(candidate.format, list);
  }

  const results = [];
  for (const [format, nodes] of groups.entries()) {
    const urlMap = await options.resolveRenderedNodeUrls(nodes, format);
    debug.batches.push({
      format,
      requestedNodeIds: nodes.map((node) => node.nodeId),
      resolvedNodeIds: Object.entries(urlMap)
        .filter(([, url]) => Boolean(url))
        .map(([nodeId]) => nodeId)
    });

    for (const node of nodes) {
      const sourceUrl = urlMap[node.nodeId];
      if (!sourceUrl) {
        continue;
      }

      const binary = await options.fetchBinary(sourceUrl);
      const fileName = `${sanitizeSegment(node.nodeName, "layer")}-${sanitizeSegment(node.nodeId, "node")}.${format}`;

      results.push({
        kind: "rendered-node",
        nodeId: node.nodeId,
        nodeName: node.nodeName,
        nodeType: node.nodeType,
        format,
        fileName,
        contentType: binary.contentType,
        sourceUrl,
        base64: binary.base64
      });
    }
  }

  return {
    assets: results,
    debug
  };
}
