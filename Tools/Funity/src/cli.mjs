import { resolve } from "node:path";
import { buildArtifactsFromFile, buildArtifactsFromJson, writeArtifacts } from "./convert.mjs";
import {
  fetchBinaryAsset,
  fetchFigmaDocument,
  fetchImageFillUrls,
  fetchRenderedNodeUrls,
  parseFigmaReference
} from "./figma/api.mjs";
import { downloadImageAssets } from "./assets.mjs";
import { downloadRenderedNodeAssets } from "./render-assets.mjs";
import { loadFigmaDocument, pickScreenNode } from "./figma/load.mjs";

function parseArgs(argv) {
  const args = {};

  for (let index = 2; index < argv.length; index += 1) {
    const token = argv[index];
    if (!token.startsWith("--")) {
      continue;
    }

    const key = token.slice(2);
    const value = argv[index + 1];
    args[key] = value;
    index += 1;
  }

  return args;
}

function isTruthy(value) {
  return ["1", "true", "yes", "on"].includes(String(value ?? "").toLowerCase());
}

async function resolveArtifacts(args) {
  if (args.input) {
    return buildArtifactsFromFile(resolve(args.input), {
      screenName: args.screen,
      nodeId: args.nodeId
    });
  }

  if (args.figmaUrl || args.fileKey) {
    const reference = args.figmaUrl
      ? parseFigmaReference(args.figmaUrl)
      : { fileKey: args.fileKey, nodeId: args.nodeId };
    const json = await fetchFigmaDocument({
      fileKey: reference.fileKey,
      nodeId: args.nodeId || reference.nodeId,
      token: args.token
    });
    const baseArtifacts = buildArtifactsFromJson(json, {
      screenName: args.screen,
      nodeId: args.nodeId || reference.nodeId
    });
    const includeAssets = args.includeAssets === undefined ? true : isTruthy(args.includeAssets);
    const includeRenderedAssets =
      args.includeRenderedAssets === undefined ? true : isTruthy(args.includeRenderedAssets);
    const screenNode = pickScreenNode(loadFigmaDocument(json), {
      screenName: args.screen,
      nodeId: args.nodeId || reference.nodeId
    });
    const imageFillAssets = includeAssets
      ? await downloadImageAssets(baseArtifacts.scene, {
          resolveImageUrls: (imageRefs) =>
            fetchImageFillUrls({
              fileKey: reference.fileKey,
              token: args.token,
              imageRefs
            }),
          fetchBinary: fetchBinaryAsset
        })
      : [];
    const renderedNodeResult = includeRenderedAssets
      ? await downloadRenderedNodeAssets(screenNode, {
          resolveRenderedNodeUrls: (nodes, format) =>
            fetchRenderedNodeUrls({
              fileKey: reference.fileKey,
              token: args.token,
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

    return buildArtifactsFromJson(json, {
      screenName: args.screen,
      nodeId: args.nodeId || reference.nodeId,
      downloadedAssets,
      debugInfo: {
        selectedScreen: {
          nodeId: screenNode?.id ?? null,
          nodeName: screenNode?.name ?? null,
          nodeType: screenNode?.type ?? null
        },
        imageFillAssetCount: imageFillAssets.length,
        renderedNodeExport: renderedNodeResult.debug
      }
    });
  }

  throw new Error(
    "Missing input. Use --input, or provide --figmaUrl / --fileKey for direct Figma API import."
  );
}

async function main() {
  const args = parseArgs(process.argv);
  const outputPath = args.output ? resolve(args.output) : resolve("./dist/out");
  const artifacts = await resolveArtifacts(args);
  const writtenTo = writeArtifacts(outputPath, artifacts);

  process.stdout.write(
    [
      `Generated Unity artifacts for "${artifacts.scene.name}"`,
      `- ${resolve(writtenTo, "unity-import.cs")}`,
      `- ${resolve(writtenTo, "scene.yaml")}`,
      `- ${resolve(writtenTo, "screen-spec.md")}`,
      `- ${resolve(writtenTo, "asset-manifest.json")}`,
      ...(artifacts.assets?.length ? [`- ${resolve(writtenTo, "assets")}`] : [])
    ].join("\n")
  );
}

main();
