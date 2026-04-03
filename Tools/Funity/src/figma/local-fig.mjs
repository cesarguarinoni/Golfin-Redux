import zlib from "node:zlib";
import { compileSchema, decodeBinarySchema } from "kiwi-schema";

function readUInt16(buffer, offset) {
  return buffer.readUInt16LE(offset);
}

function readUInt32(buffer, offset) {
  return buffer.readUInt32LE(offset);
}

function findEndOfCentralDirectory(buffer) {
  const signature = 0x06054b50;
  const minimumSize = 22;
  const start = Math.max(0, buffer.length - 0xffff - minimumSize);

  for (let offset = buffer.length - minimumSize; offset >= start; offset -= 1) {
    if (buffer.readUInt32LE(offset) === signature) {
      return offset;
    }
  }

  throw new Error("Invalid .fig archive. ZIP central directory was not found.");
}

function readZipEntries(buffer) {
  const eocdOffset = findEndOfCentralDirectory(buffer);
  const centralDirectoryOffset = readUInt32(buffer, eocdOffset + 16);
  const totalEntries = readUInt16(buffer, eocdOffset + 10);
  const entries = new Map();
  let offset = centralDirectoryOffset;

  for (let index = 0; index < totalEntries; index += 1) {
    if (buffer.readUInt32LE(offset) !== 0x02014b50) {
      throw new Error("Invalid .fig archive. Central directory entry header was not recognized.");
    }

    const compressionMethod = readUInt16(buffer, offset + 10);
    const compressedSize = readUInt32(buffer, offset + 20);
    const uncompressedSize = readUInt32(buffer, offset + 24);
    const fileNameLength = readUInt16(buffer, offset + 28);
    const extraLength = readUInt16(buffer, offset + 30);
    const commentLength = readUInt16(buffer, offset + 32);
    const localHeaderOffset = readUInt32(buffer, offset + 42);
    const fileName = buffer
      .subarray(offset + 46, offset + 46 + fileNameLength)
      .toString("utf8");

    entries.set(fileName, {
      fileName,
      compressionMethod,
      compressedSize,
      uncompressedSize,
      localHeaderOffset
    });

    offset += 46 + fileNameLength + extraLength + commentLength;
  }

  return entries;
}

function extractZipEntry(buffer, entry) {
  const localHeaderOffset = entry.localHeaderOffset;
  if (buffer.readUInt32LE(localHeaderOffset) !== 0x04034b50) {
    throw new Error(`Invalid .fig archive. Local header for ${entry.fileName} was not recognized.`);
  }

  const fileNameLength = readUInt16(buffer, localHeaderOffset + 26);
  const extraLength = readUInt16(buffer, localHeaderOffset + 28);
  const dataStart = localHeaderOffset + 30 + fileNameLength + extraLength;
  const compressed = buffer.subarray(dataStart, dataStart + entry.compressedSize);

  if (entry.compressionMethod === 0) {
    return compressed;
  }

  if (entry.compressionMethod === 8) {
    return zlib.inflateRawSync(compressed);
  }

  throw new Error(
    `Unsupported .fig compression method ${entry.compressionMethod} for ${entry.fileName}.`
  );
}

function safeJsonParse(text, fallback) {
  try {
    return JSON.parse(text);
  } catch {
    return fallback;
  }
}

function guidToId(guid) {
  if (!guid) {
    return "";
  }

  return `${guid.sessionID ?? 0}:${guid.localID ?? 0}`;
}

function hashToHex(hash) {
  if (!hash) {
    return "";
  }

  const bytes = ArrayBuffer.isView(hash) ? hash : Object.values(hash);
  return [...bytes].map((value) => Number(value).toString(16).padStart(2, "0")).join("");
}

function matrixToRotation(matrix) {
  if (!matrix) {
    return 0;
  }

  const radians = Math.atan2(matrix.m10 ?? 0, matrix.m00 ?? 1);
  const degrees = (radians * 180) / Math.PI;
  return Number.isFinite(degrees) ? degrees : 0;
}

function mapConstraints(node) {
  if (!node.horizontalConstraint && !node.verticalConstraint) {
    return null;
  }

  return {
    horizontal: node.horizontalConstraint ?? "MIN",
    vertical: node.verticalConstraint ?? "MIN"
  };
}

function mapPaint(paint) {
  if (!paint) {
    return null;
  }

  return {
    type: paint.type ?? "SOLID",
    color: paint.color ?? null,
    opacity: paint.opacity ?? 1,
    visible: paint.visible !== false,
    blendMode: paint.blendMode ?? "NORMAL",
    imageRef: hashToHex(paint.image?.hash || paint.imageThumbnail?.hash),
    scaleMode: paint.imageScaleMode ?? "FILL",
    gradientStops: paint.stops ?? []
  };
}

function mapEffect(effect) {
  if (!effect) {
    return null;
  }

  return {
    type: effect.type ?? "DROP_SHADOW",
    visible: effect.visible !== false,
    radius: effect.radius ?? 0
  };
}

function lineHeightValue(lineHeight, fontSize) {
  if (!lineHeight) {
    return fontSize ?? 14;
  }

  if (typeof lineHeight.value === "number") {
    return lineHeight.value;
  }

  return fontSize ?? 14;
}

function letterSpacingValue(letterSpacing) {
  if (!letterSpacing) {
    return 0;
  }

  return typeof letterSpacing.value === "number" ? letterSpacing.value : 0;
}

function mapNode(node) {
  const normalizedType =
    node.type === "SYMBOL"
      ? "COMPONENT"
      : node.type === "REGULAR_POLYGON"
        ? "POLYGON"
        : node.type ?? "FRAME";
  const x = node.transform?.m02 ?? 0;
  const y = node.transform?.m12 ?? 0;
  const width = node.size?.x ?? 0;
  const height = node.size?.y ?? 0;

  return {
    id: guidToId(node.guid),
    name: node.name ?? node.type ?? "Node",
    type: normalizedType,
    visible: node.visible !== false,
    rotation: matrixToRotation(node.transform),
    absoluteBoundingBox: {
      x,
      y,
      width,
      height
    },
    fills: (node.fillPaints ?? []).map(mapPaint).filter(Boolean),
    strokes: (node.strokePaints ?? []).map(mapPaint).filter(Boolean),
    strokeWeight: node.strokeWeight ?? 0,
    cornerRadius:
      node.cornerRadius ??
      (node.rectangleCornerRadiiIndependent
        ? Math.max(
            node.rectangleTopLeftCornerRadius ?? 0,
            node.rectangleTopRightCornerRadius ?? 0,
            node.rectangleBottomLeftCornerRadius ?? 0,
            node.rectangleBottomRightCornerRadius ?? 0
          )
        : 0),
    opacity: node.opacity ?? 1,
    effects: (node.effects ?? []).map(mapEffect).filter(Boolean),
    layoutMode:
      node.stackMode === "HORIZONTAL"
        ? "HORIZONTAL"
        : node.stackMode === "VERTICAL"
          ? "VERTICAL"
          : "NONE",
    constraints: mapConstraints(node),
    children: [],
    characters: node.textData?.characters ?? "",
    style:
      node.type === "TEXT"
        ? {
            fontFamily: node.fontName?.family ?? "Unknown",
            fontWeight:
              node.derivedTextData?.fontMetaData?.[0]?.fontWeight ??
              node.fontWeight ??
              400,
            fontSize: node.fontSize ?? 14,
            lineHeightPx: lineHeightValue(node.lineHeight, node.fontSize ?? 14),
            textAlignHorizontal: node.textAlignHorizontal ?? "LEFT",
            textAlignVertical: node.textAlignVertical ?? "TOP",
            letterSpacing: letterSpacingValue(node.letterSpacing)
          }
        : undefined
  };
}

function rebuildDocumentTree(message) {
  const createdNodes = (message.nodeChanges ?? []).filter(
    (node) => node.phase !== "REMOVED" && node.guid && node.type
  );
  const byId = new Map();
  const childBuckets = new Map();

  for (const sourceNode of createdNodes) {
    byId.set(guidToId(sourceNode.guid), mapNode(sourceNode));
  }

  for (const sourceNode of createdNodes) {
    const parentId = guidToId(sourceNode.parentIndex?.guid);
    if (!parentId) {
      continue;
    }

    const bucket = childBuckets.get(parentId) ?? [];
    bucket.push({
      position: String(sourceNode.parentIndex?.position ?? ""),
      node: byId.get(guidToId(sourceNode.guid))
    });
    childBuckets.set(parentId, bucket);
  }

  for (const [parentId, children] of childBuckets.entries()) {
    const parent = byId.get(parentId);
    if (!parent) {
      continue;
    }

    children.sort((left, right) => left.position.localeCompare(right.position));
    parent.children = children.map((entry) => entry.node).filter(Boolean);
  }

  const root = byId.get("0:0");
  if (!root) {
    throw new Error("Decoded .fig file did not contain a root DOCUMENT node.");
  }

  return root;
}

function inferImageContentType(bytes) {
  if (!bytes || bytes.length < 12) {
    return "application/octet-stream";
  }

  if (bytes[0] === 0x89 && bytes[1] === 0x50 && bytes[2] === 0x4e && bytes[3] === 0x47) {
    return "image/png";
  }

  if (bytes[0] === 0xff && bytes[1] === 0xd8) {
    return "image/jpeg";
  }

  if (bytes[0] === 0x47 && bytes[1] === 0x49 && bytes[2] === 0x46) {
    return "image/gif";
  }

  if (bytes[0] === 0x52 && bytes[1] === 0x49 && bytes[2] === 0x46 && bytes[3] === 0x46) {
    return "image/webp";
  }

  const header = bytes.subarray(0, 128).toString("utf8");
  if (header.includes("<svg")) {
    return "image/svg+xml";
  }

  return "application/octet-stream";
}

function inferImageExtension(contentType) {
  if (contentType === "image/png") return ".png";
  if (contentType === "image/jpeg") return ".jpg";
  if (contentType === "image/gif") return ".gif";
  if (contentType === "image/webp") return ".webp";
  if (contentType === "image/svg+xml") return ".svg";
  return ".bin";
}

function decodeCanvas(canvasBuffer) {
  const prelude = canvasBuffer.subarray(0, 8).toString("utf8");
  const version = readUInt32(canvasBuffer, 8);
  const schemaChunkSize = readUInt32(canvasBuffer, 12);
  const schemaChunk = canvasBuffer.subarray(16, 16 + schemaChunkSize);
  const dataChunkSize = readUInt32(canvasBuffer, 16 + schemaChunkSize);
  const dataChunk = canvasBuffer.subarray(20 + schemaChunkSize, 20 + schemaChunkSize + dataChunkSize);

  const schemaBuffer = zlib.inflateRawSync(schemaChunk);
  const schema = decodeBinarySchema(schemaBuffer);
  const message = compileSchema(schema).decodeMessage(zlib.zstdDecompressSync(dataChunk));

  return {
    header: {
      prelude,
      version
    },
    schema,
    message
  };
}

function collectEmbeddedImages(entries, archiveBuffer) {
  const images = new Map();

  for (const [fileName, entry] of entries.entries()) {
    if (!fileName.startsWith("images/") || fileName.endsWith("/")) {
      continue;
    }

    const bytes = extractZipEntry(archiveBuffer, entry);
    const contentType = inferImageContentType(bytes);
    const extension = inferImageExtension(contentType);
    const imageRef = fileName.slice("images/".length);

    images.set(imageRef, {
      imageRef,
      archivePath: fileName,
      contentType,
      extension,
      base64: bytes.toString("base64")
    });
  }

  return images;
}

function metadataSummary(meta, archiveInfo) {
  const coords = meta?.client_meta?.render_coordinates;

  return {
    sourceType: "figma-local-fig",
    canvasSize: coords
      ? {
          width: coords.width,
          height: coords.height
        }
      : null,
    fileName: meta?.file_name ?? "Unknown",
    exportedAt: meta?.exported_at ?? null,
    archiveVersion: archiveInfo.canvasHeader?.version ?? null,
    imageCount: archiveInfo.imageCount ?? 0,
    notes: [
      "Imported from a local Figma .fig archive.",
      "Decoded from canvas.fig using the internal Kiwi schema payload."
    ]
  };
}

export function decodeLocalFigArchive(input) {
  const archiveBuffer = Buffer.isBuffer(input) ? input : Buffer.from(input);
  const entries = readZipEntries(archiveBuffer);
  const fileNames = [...entries.keys()];
  const metaBuffer = entries.has("meta.json") ? extractZipEntry(archiveBuffer, entries.get("meta.json")) : null;
  const canvasBuffer = entries.has("canvas.fig")
    ? extractZipEntry(archiveBuffer, entries.get("canvas.fig"))
    : null;
  const thumbnailBuffer = entries.has("thumbnail.png")
    ? extractZipEntry(archiveBuffer, entries.get("thumbnail.png"))
    : null;

  if (!canvasBuffer) {
    throw new Error("Invalid .fig archive. canvas.fig was not found.");
  }

  const meta = metaBuffer ? safeJsonParse(metaBuffer.toString("utf8"), null) : null;
  const decodedCanvas = decodeCanvas(canvasBuffer);
  const document = rebuildDocumentTree(decodedCanvas.message);
  const embeddedImages = collectEmbeddedImages(entries, archiveBuffer);

  return {
    json: {
      document
    },
    meta,
    thumbnailBase64: thumbnailBuffer ? thumbnailBuffer.toString("base64") : null,
    embeddedImages,
    archiveInfo: {
      fileType: "fig",
      entryCount: fileNames.length,
      fileNames,
      imageCount: embeddedImages.size,
      canvasHeader: decodedCanvas.header,
      schemaDefinitionCount: decodedCanvas.schema.definitions?.length ?? 0,
      messageType: decodedCanvas.message.type ?? null,
      nodeChangeCount: decodedCanvas.message.nodeChanges?.length ?? 0
    },
    metadata: metadataSummary(meta, {
      imageCount: embeddedImages.size,
      canvasHeader: decodedCanvas.header
    })
  };
}

export function inspectLocalFigArchive(input) {
  const decoded = decodeLocalFigArchive(input);

  return {
    fileType: "fig",
    entryCount: decoded.archiveInfo.entryCount,
    fileNames: decoded.archiveInfo.fileNames,
    meta: decoded.meta,
    hasCanvas: true,
    hasThumbnail: Boolean(decoded.thumbnailBase64),
    thumbnailBase64: decoded.thumbnailBase64,
    imageCount: decoded.archiveInfo.imageCount,
    imageEntries: decoded.archiveInfo.fileNames.filter(
      (name) => name.startsWith("images/") && !name.endsWith("/")
    ),
    canvasHeader: `${decoded.archiveInfo.canvasHeader.prelude}@v${decoded.archiveInfo.canvasHeader.version}`,
    canvasDecodeSupported: true,
    schemaDefinitionCount: decoded.archiveInfo.schemaDefinitionCount,
    nodeChangeCount: decoded.archiveInfo.nodeChangeCount
  };
}
