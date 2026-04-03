const SUPPORTED_SCREEN_TYPES = new Set(["FRAME", "COMPONENT", "INSTANCE", "SECTION", "GROUP"]);

function normalizeNodeId(nodeId) {
  return String(nodeId ?? "")
    .trim()
    .replace(/-/g, ":");
}

export function loadFigmaDocument(json) {
  if (json?.document) {
    return json.document;
  }

  if (json?.nodes) {
    const firstNode = Object.values(json.nodes)[0];
    if (firstNode?.document) {
      return firstNode.document;
    }
  }

  if (json?.type && json?.children) {
    return json;
  }

  throw new Error("Unsupported Figma JSON shape. Expected document, nodes[*].document, or a direct node.");
}

function walk(node, visit) {
  visit(node);
  for (const child of node.children ?? []) {
    walk(child, visit);
  }
}

export function listScreenNodes(document) {
  const matches = [];

  walk(document, (node) => {
    if (!SUPPORTED_SCREEN_TYPES.has(node.type)) {
      return;
    }

    matches.push({
      id: node.id ?? "",
      name: node.name ?? node.type ?? "Node",
      type: node.type,
      childCount: node.children?.length ?? 0
    });
  });

  return matches;
}

export function pickScreenNode(document, options = {}) {
  const matches = [];
  const requestedNodeId = normalizeNodeId(options.nodeId);

  walk(document, (node) => {
    if (!SUPPORTED_SCREEN_TYPES.has(node.type)) {
      return;
    }

    if (requestedNodeId && normalizeNodeId(node.id) === requestedNodeId) {
      matches.push(node);
      return;
    }

    if (options.screenName && node.name === options.screenName) {
      matches.push(node);
      return;
    }

    if (!options.nodeId && !options.screenName && node.type === "FRAME") {
      matches.push(node);
    }
  });

  if (matches.length === 0) {
    throw new Error("No matching screen node was found in the supplied Figma JSON.");
  }

  return matches[0];
}
