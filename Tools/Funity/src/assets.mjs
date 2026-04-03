function sanitizeSegment(value, fallback) {
  const sanitized = String(value ?? fallback ?? "asset")
    .replace(/[^\w.-]+/g, "-")
    .replace(/^-+|-+$/g, "");

  return sanitized || fallback || "asset";
}

function inferExtension(contentType, url) {
  const type = String(contentType ?? "").toLowerCase();
  if (type.includes("png")) return ".png";
  if (type.includes("jpeg") || type.includes("jpg")) return ".jpg";
  if (type.includes("gif")) return ".gif";
  if (type.includes("svg")) return ".svg";
  if (type.includes("webp")) return ".webp";

  try {
    const parsed = new URL(url);
    const match = parsed.pathname.match(/\.(png|jpe?g|gif|svg|webp)$/i);
    if (match) {
      return `.${match[1].toLowerCase().replace("jpeg", "jpg")}`;
    }
  } catch {
    return ".bin";
  }

  return ".bin";
}

export function collectImageUsage(node, output = []) {
  if (node.asset?.kind === "image" && node.asset.imageRef) {
    output.push({
      nodeId: node.id,
      nodeName: node.name,
      imageRef: node.asset.imageRef,
      scaleMode: node.asset.scaleMode
    });
  }

  for (const child of node.children ?? []) {
    collectImageUsage(child, output);
  }

  return output;
}

export function buildAssetManifest(scene, downloadedAssets = []) {
  const byImageRef = Object.fromEntries(downloadedAssets.map((asset) => [asset.imageRef, asset]));

  return {
    count: downloadedAssets.length,
    downloadedCount: downloadedAssets.length,
    imageFillCount: downloadedAssets.filter((asset) => asset.kind !== "rendered-node").length,
    renderedNodeCount: downloadedAssets.filter((asset) => asset.kind === "rendered-node").length,
    assets: collectImageUsage(scene.root).map((usage, index) => {
      const downloaded = byImageRef[usage.imageRef];
      return {
        id: `${sanitizeSegment(usage.nodeName, "asset")}-${index + 1}`,
        nodeId: usage.nodeId,
        nodeName: usage.nodeName,
        imageRef: usage.imageRef,
        scaleMode: usage.scaleMode,
        fileName: downloaded?.fileName ?? null,
        contentType: downloaded?.contentType ?? null,
        sourceUrl: downloaded?.sourceUrl ?? null
      };
    }),
    downloads: downloadedAssets.map((asset, index) => ({
      id: `${sanitizeSegment(asset.nodeName ?? asset.fileName, "download")}-${index + 1}`,
      kind: asset.kind ?? "image-fill",
      nodeId: asset.nodeId ?? null,
      nodeName: asset.nodeName ?? null,
      nodeType: asset.nodeType ?? null,
      imageRef: asset.imageRef ?? null,
      format: asset.format ?? null,
      fileName: asset.fileName,
      contentType: asset.contentType ?? null,
      sourceUrl: asset.sourceUrl ?? null
    }))
  };
}

export async function downloadImageAssets(scene, options) {
  const usages = collectImageUsage(scene.root);
  if (usages.length === 0) {
    return [];
  }

  const uniqueImageRefs = [...new Set(usages.map((usage) => usage.imageRef))];
  const imageUrlMap = await options.resolveImageUrls(uniqueImageRefs);
  const results = [];

  for (const imageRef of uniqueImageRefs) {
    const usage = usages.find((entry) => entry.imageRef === imageRef);
    const sourceUrl = imageUrlMap[imageRef];
    if (!sourceUrl) {
      continue;
    }

    const binary = await options.fetchBinary(sourceUrl);
    const extension = inferExtension(binary.contentType, sourceUrl);
    const fileName = `${sanitizeSegment(usage.nodeName, "asset")}-${sanitizeSegment(usage.nodeId, "node")}${extension}`;

    results.push({
      nodeId: usage.nodeId,
      nodeName: usage.nodeName,
      imageRef,
      scaleMode: usage.scaleMode,
      fileName,
      contentType: binary.contentType,
      sourceUrl,
      base64: binary.base64
    });
  }

  return results;
}

export function resolveEmbeddedImageAssets(scene, embeddedImages) {
  if (!embeddedImages || embeddedImages.size === 0) {
    return [];
  }

  const usages = collectImageUsage(scene.root);
  const results = [];

  for (const usage of usages) {
    const embedded = embeddedImages.get(usage.imageRef);
    if (!embedded) {
      continue;
    }

    results.push({
      nodeId: usage.nodeId,
      nodeName: usage.nodeName,
      imageRef: usage.imageRef,
      scaleMode: usage.scaleMode,
      fileName: `${sanitizeSegment(usage.nodeName, "asset")}-${sanitizeSegment(usage.nodeId, "node")}${embedded.extension}`,
      contentType: embedded.contentType,
      sourceUrl: embedded.archivePath,
      base64: embedded.base64
    });
  }

  return results;
}
