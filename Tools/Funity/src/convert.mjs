import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { resolve } from "node:path";
import { loadFigmaDocument, pickScreenNode } from "./figma/load.mjs";
import { normalizeScreen } from "./normalize.mjs";
import { generateUnityImporter } from "./generate/csharp.mjs";
import { generateSceneYaml } from "./generate/yaml.mjs";
import { generateSpecMarkdown } from "./generate/spec.mjs";
import { buildAssetManifest } from "./assets.mjs";

export function buildArtifactsFromJson(json, options = {}) {
  const figmaDocument = loadFigmaDocument(json);
  const screenNode = pickScreenNode(figmaDocument, {
    screenName: options.screenName,
    nodeId: options.nodeId
  });
  const scene = normalizeScreen(screenNode);
  const assetManifest = buildAssetManifest(scene, options.downloadedAssets ?? []);
  const debugInfo = options.debugInfo ?? {
    selectedScreen: {
      nodeId: screenNode?.id ?? null,
      nodeName: screenNode?.name ?? null,
      nodeType: screenNode?.type ?? null
    },
    imageFillAssetCount: (options.downloadedAssets ?? []).filter((asset) => asset.kind !== "rendered-node").length,
    renderedNodeExport: {
      selectedScreen: {
        nodeId: screenNode?.id ?? null,
        nodeName: screenNode?.name ?? null,
        nodeType: screenNode?.type ?? null
      },
      candidateCount: 0,
      candidates: [],
      batches: [],
      note: "No rendered asset debug data was attached for this conversion."
    }
  };

  return {
    scene,
    assetManifest,
    files: {
      "unity-import.cs": generateUnityImporter(scene, assetManifest),
      "scene.yaml": generateSceneYaml(scene),
      "screen-spec.md": generateSpecMarkdown(scene, assetManifest),
      "asset-manifest.json": JSON.stringify(assetManifest, null, 2),
      "debug-export.json": JSON.stringify(debugInfo, null, 2)
    },
    assets: options.downloadedAssets ?? []
  };
}

export function buildArtifactsFromFile(inputPath, options = {}) {
  const raw = readFileSync(resolve(inputPath), "utf8");
  return buildArtifactsFromJson(JSON.parse(raw), options);
}

export function writeArtifacts(outputPath, artifacts) {
  const target = resolve(outputPath);
  mkdirSync(target, { recursive: true });
  const assetsDir = resolve(target, "assets");

  for (const [fileName, contents] of Object.entries(artifacts.files)) {
    writeFileSync(resolve(target, fileName), contents, "utf8");
  }

  for (const asset of artifacts.assets ?? []) {
    mkdirSync(assetsDir, { recursive: true });
    writeFileSync(resolve(assetsDir, asset.fileName), Buffer.from(asset.base64, "base64"));
  }

  return target;
}
