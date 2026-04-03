import { loadFigmaDocument, pickScreenNode } from "./figma/load.mjs";
import { normalizeScreen } from "./normalize.mjs";

function flatten(node, output = []) {
  output.push(node);
  for (const child of node.children ?? []) {
    flatten(child, output);
  }
  return output;
}

function parseUnityEvidence(unityDump) {
  const text = String(unityDump ?? "");
  const lower = text.toLowerCase();
  const lines = text
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean);

  const fonts = [...new Set([...text.matchAll(/\bfont(?:family)?\s*[:=]\s*([A-Za-z0-9 _-]+)/gi)].map((match) => match[1].trim()))];
  const colors = [...new Set([...text.matchAll(/#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})\b/g)].map((match) => match[0].toUpperCase()))];

  return {
    raw: text,
    lower,
    lines,
    fonts,
    colors,
    hasData: lines.length > 0
  };
}

function severity(priority) {
  return ["low", "medium", "high"][priority] ?? "medium";
}

function makeFinding(priority, category, title, evidence, suggestedFix) {
  return {
    severity: severity(priority),
    category,
    title,
    evidence,
    suggestedFix
  };
}

function countTextMatches(textNodes, unity) {
  return textNodes.filter((node) => {
    const content = node.text?.content?.trim();
    return content && unity.lower.includes(content.toLowerCase());
  }).length;
}

function hasLayoutSignals(unity) {
  return /\b(recttransform|anchoredposition|sizedelta|layoutgroup|horizontal|vertical|gridlayout|contentsizefitter|layoutelement)\b/i.test(
    unity.raw
  );
}

function hasAssetSignals(unity) {
  return /\b(sprite|image|rawimage|svg|icon)\b/i.test(unity.raw);
}

export function compareFigmaToUnity(options = {}) {
  const figmaDocument = loadFigmaDocument(options.figmaJson);
  const screenNode = pickScreenNode(figmaDocument, {
    screenName: options.screenName,
    nodeId: options.nodeId
  });
  const scene = normalizeScreen(screenNode);
  const unity = parseUnityEvidence(options.unityDump);
  const allNodes = flatten(scene.root);
  const textNodes = allNodes.filter((node) => node.text);
  const findings = [];
  const enabledChecks = new Set(options.checks ?? []);
  const matchedTextCount = countTextMatches(textNodes, unity);
  const layoutSignalsPresent = hasLayoutSignals(unity);
  const assetSignalsPresent = hasAssetSignals(unity);

  if (!unity.hasData) {
    findings.push(
      makeFinding(
        2,
        "input",
        "Missing Unity structure evidence",
        "No Unity hierarchy, prefab, or scene dump was provided, so structural comparison is limited.",
        "Paste a hierarchy dump, prefab YAML, or scene export from Unity to unlock deeper comparisons."
      )
    );
  }

  if (options.screenshotName) {
    findings.push(
      makeFinding(
        0,
        "evidence",
        "Unity screenshot attached",
        `Screenshot provided: ${options.screenshotName}.`,
        "Use this in the next iteration for visual diffing once screenshot analysis is wired in."
      )
    );
  } else {
    findings.push(
      makeFinding(
        1,
        "evidence",
        "No Unity screenshot attached",
        "Visual alignment, spacing, and color parity cannot be confirmed without a rendered Unity capture.",
        "Attach a screenshot from Unity for visual discrepancy checks."
      )
    );
  }

  if (enabledChecks.has("layout")) {
    const sceneNamePresent = unity.lower.includes(scene.name.toLowerCase());
    if (!sceneNamePresent) {
      const strongStructuralEvidence =
        layoutSignalsPresent ||
        matchedTextCount >= 2 ||
        scene.fonts.some((font) =>
          unity.fonts.some((unityFont) => unityFont.toLowerCase().includes(font.family.toLowerCase()))
        ) ||
        assetSignalsPresent;

      findings.push(
        makeFinding(
          strongStructuralEvidence ? 0 : 2,
          "layout",
          strongStructuralEvidence
            ? "Figma and Unity root names do not appear to match"
            : "Screen root not found in Unity evidence",
          strongStructuralEvidence
            ? `The Figma target root "${scene.name}" was not found by name, but the Unity dump does contain other matching UI signals, so this may just be a naming mismatch.`
            : `The Figma target root "${scene.name}" was not found in the provided Unity dump.`,
          strongStructuralEvidence
            ? "Treat this as a naming mismatch unless other layout findings also suggest the wrong Unity screen was exported."
            : "Confirm the correct Unity screen was exported and include the root object name in the hierarchy dump."
        )
      );
    }

    if (unity.hasData && !layoutSignalsPresent) {
      findings.push(
        makeFinding(
          1,
          "layout",
          "Unity dump lacks layout signals",
          "The provided Unity dump does not mention common layout properties like RectTransform, anchors, sizeDelta, or layout groups.",
          "Export a richer Unity hierarchy or prefab dump that includes RectTransform and layout component data."
        )
      );
    }
  }

  if (enabledChecks.has("typography")) {
    for (const font of scene.fonts) {
      const found = unity.fonts.some((unityFont) => unityFont.toLowerCase().includes(font.family.toLowerCase()));
      if (!found) {
        findings.push(
          makeFinding(
            2,
            "typography",
            `Missing font family in Unity evidence: ${font.family}`,
            `Figma uses ${font.family} with weights ${font.weights.join(", ")}, but it was not detected in the Unity dump.`,
            `Verify that ${font.family} is mapped to a TextMeshPro or UI Toolkit font asset in Unity.`
          )
        );
      }
    }

    const missingTexts = textNodes
      .map((node) => node.text.content)
      .filter((content) => content && !unity.lower.includes(content.toLowerCase()));

    if (missingTexts.length > 0) {
      findings.push(
        makeFinding(
          2,
          "typography",
          "Some Figma text content was not found in Unity evidence",
          `Missing text strings: ${missingTexts.slice(0, 8).join(" | ")}${missingTexts.length > 8 ? " ..." : ""}`,
          "Check whether the Unity screen is outdated, uses different copy, or the provided dump omitted text components."
        )
      );
    }
  }

  if (enabledChecks.has("colors")) {
    const figmaColors = [...new Set(allNodes.map((node) => node.visuals?.fillColor).filter(Boolean))];
    const missingColors = figmaColors.filter((color) => !unity.colors.includes(color));
    if (missingColors.length > 0) {
      findings.push(
        makeFinding(
          1,
          "colors",
          "Some Figma colors were not detected in Unity evidence",
          `Examples missing from the Unity dump: ${missingColors.slice(0, 6).join(", ")}${missingColors.length > 6 ? " ..." : ""}`,
          "Provide a richer Unity dump with color values or inspect the screenshot visually for palette drift."
        )
      );
    }
  }

  if (enabledChecks.has("assets")) {
    const imageNodes = allNodes.filter((node) => node.asset?.kind === "image");
    if (imageNodes.length > 0 && !assetSignalsPresent) {
      findings.push(
        makeFinding(
          1,
          "assets",
          "Figma has image assets but Unity evidence does not mention asset-bearing components",
          `Detected ${imageNodes.length} image-backed node(s) in Figma, but the Unity evidence did not mention sprites, images, raw images, or SVGs.`,
          "Verify that Figma assets were exported and bound to Unity Image/RawImage/SVG components."
        )
      );
    }
  }

  const summary = {
    figmaScreen: {
      name: scene.name,
      nodeId: screenNode.id,
      canvas: scene.canvas,
      fonts: scene.fonts
    },
    unityEvidence: {
      hasDump: unity.hasData,
      lineCount: unity.lines.length,
      screenshotName: options.screenshotName || null,
      detectedFonts: unity.fonts,
      detectedColors: unity.colors
    },
    checks: [...enabledChecks],
    findingCount: findings.length
  };

  return {
    summary,
    findings,
    suggestedNextStep:
      findings.length === 0
        ? "No obvious mismatches were detected from the supplied evidence. Add a Unity screenshot for stronger visual validation."
        : "Start with the highest-severity findings, then re-run Compare with richer Unity evidence and a screenshot."
  };
}
