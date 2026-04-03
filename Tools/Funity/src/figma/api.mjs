const FIGMA_API_BASE = "https://api.figma.com/v1";
const JSON_CACHE = new Map();
const BINARY_CACHE = new Map();
const JSON_TTL_MS = 30_000;
const BINARY_TTL_MS = 30_000;

function ensureValue(value, message) {
  if (!value) {
    throw new Error(message);
  }
}

function normalizeNodeId(nodeId) {
  if (!nodeId) {
    return "";
  }

  const decoded = decodeURIComponent(String(nodeId).trim());
  return decoded.includes(":") ? decoded : decoded.replace(/-/g, ":");
}

export function parseFigmaReference(input) {
  const value = String(input ?? "").trim();
  ensureValue(value, "Missing Figma URL or file key.");

  if (!value.startsWith("http://") && !value.startsWith("https://")) {
    return {
      fileKey: value,
      nodeId: ""
    };
  }

  let url;
  try {
    url = new URL(value);
  } catch {
    throw new Error("Invalid Figma URL.");
  }

  const segments = url.pathname.split("/").filter(Boolean);
  const designIndex = segments.findIndex((segment) => segment === "design" || segment === "file");
  const fileKey = designIndex >= 0 ? segments[designIndex + 1] : "";
  const nodeId = normalizeNodeId(url.searchParams.get("node-id") ?? url.searchParams.get("node_id"));

  ensureValue(fileKey, "Could not extract a Figma file key from the provided URL.");

  return {
    fileKey,
    nodeId
  };
}

async function fetchFigmaJson(path, token) {
  ensureValue(token, "Missing Figma personal access token. Set FIGMA_TOKEN or provide one in the UI.");
  const cacheKey = `${token}:${path}`;
  const cached = JSON_CACHE.get(cacheKey);
  if (cached && cached.expiresAt > Date.now()) {
    return cached.value;
  }

  for (let attempt = 0; attempt < 3; attempt += 1) {
    const response = await fetch(`${FIGMA_API_BASE}${path}`, {
      headers: {
        "X-Figma-Token": token
      }
    });

    const payload = await response.json().catch(() => ({}));
    if (response.ok) {
      JSON_CACHE.set(cacheKey, {
        value: payload,
        expiresAt: Date.now() + JSON_TTL_MS
      });
      return payload;
    }

    if (response.status === 429 && attempt < 2) {
      const retryAfterHeader = Number(response.headers.get("retry-after"));
      const delayMs = Number.isFinite(retryAfterHeader) && retryAfterHeader > 0
        ? retryAfterHeader * 1000
        : 1200 * (attempt + 1);
      await new Promise((resolve) => setTimeout(resolve, delayMs));
      continue;
    }

    const details = payload?.err ?? payload?.message ?? response.statusText;
    throw new Error(`Figma API request failed (${response.status}): ${details}`);
  }

  throw new Error("Figma API request failed after retries.");
}

export async function fetchBinaryAsset(url) {
  const cached = BINARY_CACHE.get(url);
  if (cached && cached.expiresAt > Date.now()) {
    return cached.value;
  }

  for (let attempt = 0; attempt < 3; attempt += 1) {
    const response = await fetch(url);
    if (!response.ok) {
      if (response.status === 429 && attempt < 2) {
        const retryAfterHeader = Number(response.headers.get("retry-after"));
        const delayMs = Number.isFinite(retryAfterHeader) && retryAfterHeader > 0
          ? retryAfterHeader * 1000
          : 1200 * (attempt + 1);
        await new Promise((resolve) => setTimeout(resolve, delayMs));
        continue;
      }

      throw new Error(`Asset download failed (${response.status}): ${response.statusText}`);
    }

    const arrayBuffer = await response.arrayBuffer();
    const payload = {
      contentType: response.headers.get("content-type") || "application/octet-stream",
      base64: Buffer.from(arrayBuffer).toString("base64")
    };
    BINARY_CACHE.set(url, {
      value: payload,
      expiresAt: Date.now() + BINARY_TTL_MS
    });
    return payload;
  }

  throw new Error("Asset download failed after retries.");
}

export async function fetchFigmaDocument(options = {}) {
  const fileKey = options.fileKey?.trim();
  const nodeId = normalizeNodeId(options.nodeId);
  const token = options.token?.trim() || process.env.FIGMA_TOKEN || "";

  ensureValue(fileKey, "Missing Figma file key.");

  if (nodeId) {
    return fetchFigmaJson(
      `/files/${encodeURIComponent(fileKey)}/nodes?ids=${encodeURIComponent(nodeId)}`,
      token
    );
  }

  return fetchFigmaJson(`/files/${encodeURIComponent(fileKey)}`, token);
}

export async function fetchImageFillUrls(options = {}) {
  const fileKey = options.fileKey?.trim();
  const token = options.token?.trim() || process.env.FIGMA_TOKEN || "";

  ensureValue(fileKey, "Missing Figma file key.");

  const payload = await fetchFigmaJson(`/files/${encodeURIComponent(fileKey)}/images`, token);
  const allImages = payload.images ?? {};

  if (!options.imageRefs?.length) {
    return allImages;
  }

  return Object.fromEntries(options.imageRefs.map((imageRef) => [imageRef, allImages[imageRef] ?? null]));
}

export async function fetchRenderedNodeUrls(options = {}) {
  const fileKey = options.fileKey?.trim();
  const token = options.token?.trim() || process.env.FIGMA_TOKEN || "";
  const nodeIds = options.nodeIds ?? [];
  const format = options.format ?? "png";
  const scale = options.scale ?? 1;

  ensureValue(fileKey, "Missing Figma file key.");
  ensureValue(nodeIds.length, "Missing node IDs for rendered asset export.");

  const query = new URLSearchParams({
    ids: nodeIds.join(","),
    format,
    scale: String(scale)
  });

  if (format === "svg") {
    query.set("svg_outline_text", "false");
  }

  const payload = await fetchFigmaJson(`/images/${encodeURIComponent(fileKey)}?${query.toString()}`, token);
  return payload.images ?? {};
}
