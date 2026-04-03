import { createServer } from "node:http";
import { readFileSync } from "node:fs";
import { extname, resolve } from "node:path";
import { buildArtifactsFromJson } from "./convert.mjs";
import { compareFigmaToUnity } from "./compare.mjs";
import {
  fetchBinaryAsset,
  fetchFigmaDocument,
  fetchImageFillUrls,
  fetchRenderedNodeUrls,
  parseFigmaReference
} from "./figma/api.mjs";
import { downloadImageAssets, resolveEmbeddedImageAssets } from "./assets.mjs";
import { downloadRenderedNodeAssets } from "./render-assets.mjs";
import { listScreenNodes, loadFigmaDocument, pickScreenNode } from "./figma/load.mjs";
import { decodeLocalFigArchive } from "./figma/local-fig.mjs";

const HOST = "127.0.0.1";
const PORT = 4173;
const PUBLIC_DIR = resolve("./public");
const EXAMPLE_PATH = resolve("./examples/figma-sample.json");

const MIME_TYPES = {
  ".html": "text/html; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8"
};

function sendJson(response, statusCode, payload) {
  response.writeHead(statusCode, { "Content-Type": "application/json; charset=utf-8" });
  response.end(JSON.stringify(payload));
}

function readJsonSafely(body) {
  return JSON.parse(body || "{}");
}

function sendFile(response, filePath) {
  const extension = extname(filePath);
  const contentType = MIME_TYPES[extension] ?? "text/plain; charset=utf-8";
  const body = readFileSync(filePath);
  response.writeHead(200, { "Content-Type": contentType });
  response.end(body);
}

function readBody(request) {
  return new Promise((resolveBody, rejectBody) => {
    const chunks = [];
    request.on("data", (chunk) => chunks.push(chunk));
    request.on("end", () => resolveBody(Buffer.concat(chunks).toString("utf8")));
    request.on("error", rejectBody);
  });
}

async function handleRequest(request, response) {
  const url = new URL(request.url, `http://${HOST}:${PORT}`);

  if (request.method === "GET" && url.pathname === "/api/health") {
    sendJson(response, 200, { ok: true });
    return;
  }

  if (request.method === "GET" && (url.pathname === "/" || url.pathname === "/index.html")) {
    sendFile(response, resolve(PUBLIC_DIR, "index.html"));
    return;
  }

  if (request.method === "GET" && url.pathname === "/api/example") {
    sendJson(response, 200, JSON.parse(readFileSync(EXAMPLE_PATH, "utf8")));
    return;
  }

  if (request.method === "POST" && url.pathname === "/api/convert") {
    try {
      const body = await readBody(request);
      const payload = readJsonSafely(body);
      const source = await resolveSource(payload);
      const baseArtifacts = buildArtifactsFromJson(source.json, {
        screenName: payload.screenName,
        nodeId: payload.nodeId
      });
      const screenNode = pickScreenNode(loadFigmaDocument(source.json), {
        screenName: payload.screenName,
        nodeId: payload.nodeId
      });
      const imageFillAssets =
        payload.includeAssets
          ? source.embeddedImages
            ? resolveEmbeddedImageAssets(baseArtifacts.scene, source.embeddedImages)
            : source.fileKey
              ? await downloadImageAssets(baseArtifacts.scene, {
                  resolveImageUrls: (imageRefs) =>
                    fetchImageFillUrls({
                      fileKey: source.fileKey,
                      token: payload.figmaToken,
                      imageRefs
                    }),
                  fetchBinary: fetchBinaryAsset
                })
              : []
          : [];
      const renderedNodeResult =
        payload.includeRenderedAssets !== false && source.fileKey
          ? await downloadRenderedNodeAssets(screenNode, {
              resolveRenderedNodeUrls: (nodes, format) =>
                fetchRenderedNodeUrls({
                  fileKey: source.fileKey,
                  token: payload.figmaToken,
                  nodeIds: nodes.map((node) => node.nodeId),
                  format
                }),
              fetchBinary: fetchBinaryAsset
            })
          : {
              assets: [],
              debug: {
                selectedScreen: {
                  nodeId: screenNode?.id ?? null,
                  nodeName: screenNode?.name ?? null,
                  nodeType: screenNode?.type ?? null
                },
                candidateCount: 0,
                candidates: [],
                batches: []
              }
            };
      const downloadedAssets = [...imageFillAssets, ...renderedNodeResult.assets];
      const artifacts = buildArtifactsFromJson(source.json, {
        screenName: payload.screenName,
        nodeId: payload.nodeId,
        downloadedAssets,
        debugInfo: {
          selectedScreen: {
            nodeId: screenNode?.id ?? null,
            nodeName: screenNode?.name ?? null,
            nodeType: screenNode?.type ?? null
          },
          imageFillAssetCount: imageFillAssets.length,
          renderedNodeExport: renderedNodeResult.debug,
          localFig: source.archiveInfo ?? null
        }
      });

      sendJson(response, 200, artifacts);
      return;
    } catch (error) {
      sendJson(response, 400, {
        error: error instanceof Error ? error.message : "Unknown conversion error."
      });
      return;
    }
  }

  if (request.method === "POST" && url.pathname === "/api/import-figma") {
    try {
      const body = await readBody(request);
      const payload = readJsonSafely(body);
      const reference = payload.figmaUrl?.trim()
        ? parseFigmaReference(payload.figmaUrl)
        : {
            fileKey: payload.fileKey?.trim() ?? "",
            nodeId: payload.nodeId?.trim() ?? ""
          };

      const figmaJson = await fetchFigmaDocument({
        fileKey: reference.fileKey,
        nodeId: payload.nodeId?.trim() || reference.nodeId,
        token: payload.figmaToken
      });

      sendJson(response, 200, {
        fileKey: reference.fileKey,
        nodeId: payload.nodeId?.trim() || reference.nodeId,
        rawJson: JSON.stringify(figmaJson, null, 2)
      });
      return;
    } catch (error) {
      sendJson(response, 400, {
        error: error instanceof Error ? error.message : "Figma import failed."
      });
      return;
    }
  }

  if (request.method === "POST" && url.pathname === "/api/compare") {
    try {
      const body = await readBody(request);
      const payload = readJsonSafely(body);
      const source = await resolveSource({
        rawJson: payload.rawJson,
        figmaUrl: payload.figmaUrl,
        fileKey: payload.fileKey,
        nodeId: payload.nodeId,
        figmaToken: payload.figmaToken,
        localFigBase64: payload.localFigBase64,
        localFigName: payload.localFigName
      });

      const report = compareFigmaToUnity({
        figmaJson: source.json,
        screenName: payload.screenName,
        nodeId: payload.nodeId,
        unityDump: payload.unityDump,
        screenshotName: payload.screenshotName,
        checks: payload.checks
      });

      sendJson(response, 200, report);
      return;
    } catch (error) {
      sendJson(response, 400, {
        error: error instanceof Error ? error.message : "Compare failed."
      });
      return;
    }
  }

  if (request.method === "POST" && url.pathname === "/api/inspect-source") {
    try {
      const body = await readBody(request);
      const payload = readJsonSafely(body);
      const source = await resolveSource(payload);
      const document = loadFigmaDocument(source.json);
      const screens = listScreenNodes(document);

      sendJson(response, 200, {
        screens,
        archiveInfo: source.archiveInfo ?? null
      });
      return;
    } catch (error) {
      sendJson(response, 400, {
        error: error instanceof Error ? error.message : "Source inspection failed."
      });
      return;
    }
  }

  try {
    sendFile(response, resolve(PUBLIC_DIR, `.${url.pathname}`));
  } catch {
    if (!response.headersSent && !response.writableEnded) {
      response.writeHead(404, { "Content-Type": "text/plain; charset=utf-8" });
      response.end("Not found");
    }
  }
}

async function resolveSource(payload) {
  if (payload.localFigBase64) {
    const decoded = decodeLocalFigArchive(Buffer.from(payload.localFigBase64, "base64"));
    return {
      json: decoded.json,
      fileKey: "",
      embeddedImages: decoded.embeddedImages,
      archiveInfo: decoded.archiveInfo,
      meta: decoded.meta,
      metadata: decoded.metadata
    };
  }

  if (payload.rawJson) {
    return {
      json: typeof payload.rawJson === "string" ? JSON.parse(payload.rawJson) : payload.rawJson,
      fileKey: safeFileKeyFromPayload(payload)
    };
  }

  if (payload.figmaUrl || payload.fileKey) {
    const reference = payload.figmaUrl?.trim()
      ? parseFigmaReference(payload.figmaUrl)
      : {
          fileKey: payload.fileKey?.trim() ?? "",
          nodeId: payload.nodeId?.trim() ?? ""
        };

    return {
      json: await fetchFigmaDocument({
        fileKey: reference.fileKey,
        nodeId: payload.nodeId?.trim() || reference.nodeId,
        token: payload.figmaToken
      }),
      fileKey: reference.fileKey
    };
  }

  throw new Error("No input source provided. Paste JSON or provide a Figma URL/file key.");
}

function safeFileKeyFromPayload(payload) {
  if (payload.fileKey?.trim()) {
    return payload.fileKey.trim();
  }

  if (!payload.figmaUrl?.trim()) {
    return "";
  }

  try {
    return parseFigmaReference(payload.figmaUrl).fileKey;
  } catch {
    return "";
  }
}

createServer((request, response) => {
  handleRequest(request, response).catch((error) => {
    if (!response.headersSent && !response.writableEnded) {
      response.writeHead(500, { "Content-Type": "application/json; charset=utf-8" });
      response.end(
        JSON.stringify({
          error: error instanceof Error ? error.message : "Unexpected server error."
        })
      );
      return;
    }

    process.stderr.write(
      `${error instanceof Error ? error.stack || error.message : "Unexpected server error."}\n`
    );
  });
}).listen(PORT, HOST, () => {
  process.stdout.write(`Funity UI running at http://${HOST}:${PORT}\n`);
});
