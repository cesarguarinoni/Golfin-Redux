const jsonInput = document.querySelector("#json-input");
const fileInput = document.querySelector("#file-input");
const figmaUrlInput = document.querySelector("#figma-url");
const fileKeyInput = document.querySelector("#file-key");
const figmaTokenInput = document.querySelector("#figma-token");
const clearSavedTokenButton = document.querySelector("#clear-saved-token");
const includeAssetsInput = document.querySelector("#include-assets");
const includeRenderedAssetsInput = document.querySelector("#include-rendered-assets");
const screenNameInput = document.querySelector("#screen-name");
const nodeIdInput = document.querySelector("#node-id");
const generateButton = document.querySelector("#generate");
const downloadAllButton = document.querySelector("#download-all");
const importFigmaButton = document.querySelector("#import-figma");
const loadExampleButton = document.querySelector("#load-example");
const clearConsoleButton = document.querySelector("#clear-console");
const clearCompareConsoleButton = document.querySelector("#clear-compare-console");
const generateFilterInfoInput = document.querySelector("#generate-filter-info");
const generateFilterWarnInput = document.querySelector("#generate-filter-warn");
const generateFilterErrorInput = document.querySelector("#generate-filter-error");
const compareFilterInfoInput = document.querySelector("#compare-filter-info");
const compareFilterWarnInput = document.querySelector("#compare-filter-warn");
const compareFilterErrorInput = document.querySelector("#compare-filter-error");
const summaryNode = document.querySelector("#summary");
const downloadsNode = document.querySelector("#downloads");
const outputViewer = document.querySelector("#output-viewer");
const consoleViewer = document.querySelector("#console-viewer");
const compareConsoleViewer = document.querySelector("#compare-console-viewer");
const generateWorkspace = document.querySelector("#generate-workspace");
const compareWorkspace = document.querySelector("#compare-workspace");
const modeGenerateButton = document.querySelector("#mode-generate");
const modeCompareButton = document.querySelector("#mode-compare");
const compareFigmaUrlInput = document.querySelector("#compare-figma-url");
const compareFigmaJsonFileInput = document.querySelector("#compare-figma-json-file");
const inspectCompareSourceButton = document.querySelector("#inspect-compare-source");
const compareScreenSelect = document.querySelector("#compare-screen-select");
const compareMatchFigmaSelect = document.querySelector("#compare-match-figma-select");
const compareMatchUnitySelect = document.querySelector("#compare-match-unity-select");
const saveNameMatchButton = document.querySelector("#save-name-match");
const compareNameMatchesNode = document.querySelector("#compare-name-matches");
const compareFigmaJsonInput = document.querySelector("#compare-figma-json");
const compareFigmaScreenInput = document.querySelector("#compare-figma-screen");
const compareFigmaNodeIdInput = document.querySelector("#compare-figma-node-id");
const compareUnityScreenInput = document.querySelector("#compare-unity-screen");
const compareSceneVersionInput = document.querySelector("#compare-scene-version");
const compareScreenshotInput = document.querySelector("#compare-screenshot");
const compareUnityDumpFileInput = document.querySelector("#compare-unity-dump-file");
const compareUnityDumpInput = document.querySelector("#compare-unity-dump");
const compareLayoutInput = document.querySelector("#compare-layout");
const compareTypographyInput = document.querySelector("#compare-typography");
const compareColorsInput = document.querySelector("#compare-colors");
const compareAssetsInput = document.querySelector("#compare-assets");
const startCompareButton = document.querySelector("#start-compare");
const comparePreview = document.querySelector("#compare-preview");
const tabs = [...document.querySelectorAll(".tab")];

let currentFiles = {};
let currentAssets = [];
let activeFile = "unity-import.cs";
let generateSourceFile = null;
let compareSourceFile = null;
let compareScreenCandidates = [];
let compareUnityCandidates = [];
let compareNameMatches = [];
const consoleEntries = [];
const consoleTargets = {
  generate: {
    viewer: consoleViewer,
    filters: {
      info: generateFilterInfoInput,
      warn: generateFilterWarnInput,
      error: generateFilterErrorInput
    }
  },
  compare: {
    viewer: compareConsoleViewer,
    filters: {
      info: compareFilterInfoInput,
      warn: compareFilterWarnInput,
      error: compareFilterErrorInput
    }
  }
};
const STORAGE_KEYS = {
  figmaUrl: "funity.figmaUrl",
  fileKey: "funity.fileKey",
  figmaToken: "funity.figmaToken",
  includeAssets: "funity.includeAssets",
  includeRenderedAssets: "funity.includeRenderedAssets",
  screenName: "funity.screenName",
  nodeId: "funity.nodeId",
  compareFigmaUrl: "funity.compareFigmaUrl",
  compareFigmaJson: "funity.compareFigmaJson",
  compareFigmaScreen: "funity.compareFigmaScreen",
  compareFigmaNodeId: "funity.compareFigmaNodeId",
  compareNameMatches: "funity.compareNameMatches",
  compareUnityScreen: "funity.compareUnityScreen",
  compareSceneVersion: "funity.compareSceneVersion",
  compareUnityDump: "funity.compareUnityDump"
};

function setStatus(message, isError = false) {
  logMessage(message, isError ? "error" : "info");
}

function buildStamp() {
  return new Date().toLocaleTimeString([], {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit"
  });
}

function formatConsoleEntry(entry) {
  return `[${entry.stamp}] [${entry.level}] ${entry.message}`;
}

function appendConsoleElement(target, entry) {
  const line = document.createElement("div");
  line.className = `console-line console-${entry.level}`;
  line.textContent = formatConsoleEntry(entry);
  target.appendChild(line);
}

function shouldRenderEntry(kind, level) {
  return consoleTargets[kind].filters[level]?.checked ?? true;
}

function renderConsole(kind) {
  const { viewer } = consoleTargets[kind];
  const previousScrollBottom = viewer.scrollHeight - viewer.scrollTop - viewer.clientHeight;
  viewer.innerHTML = "";

  const filteredEntries = consoleEntries.filter((entry) => shouldRenderEntry(kind, entry.level));
  for (const entry of filteredEntries) {
    appendConsoleElement(viewer, entry);
  }

  if (filteredEntries.length === 0) {
    const emptyLine = document.createElement("div");
    emptyLine.className = "console-line console-empty";
    emptyLine.textContent = "No console messages match the selected filters.";
    viewer.appendChild(emptyLine);
  }

  if (previousScrollBottom < 24) {
    viewer.scrollTop = viewer.scrollHeight;
  }
}

function renderAllConsoles() {
  renderConsole("generate");
  renderConsole("compare");
}

function logMessage(message, level = "info") {
  consoleEntries.push({
    stamp: buildStamp(),
    level,
    message
  });
  renderAllConsoles();
}

function clearLogs() {
  consoleEntries.length = 0;
  renderAllConsoles();
  logMessage("Console history cleared.");
}

function selectedCompareChecks() {
  return [
    compareLayoutInput.checked ? "layout" : null,
    compareTypographyInput.checked ? "typography" : null,
    compareColorsInput.checked ? "colors" : null,
    compareAssetsInput.checked ? "assets" : null
  ].filter(Boolean);
}

function renderComparePreview() {
  const checks = selectedCompareChecks();
  const screenshotName = compareScreenshotInput.files?.[0]?.name || "No screenshot attached";
  const compareSourceLabel = compareSourceFile
    ? `${compareSourceFile.name} (${compareSourceFile.kind.toUpperCase()})`
    : "None selected";
  comparePreview.textContent = [
    "Compare mode is configured and ready.",
    "",
    `Figma target: ${compareFigmaUrlInput.value.trim() || "Not set"}`,
    `Figma source file: ${compareSourceLabel}`,
    `Figma screen target: ${compareFigmaScreenInput.value.trim() || "Auto"}`,
    `Figma node ID: ${compareFigmaNodeIdInput.value.trim() || "Auto"}`,
    `Figma JSON chars: ${compareFigmaJsonInput.value.trim().length}`,
    `Unity screen: ${compareUnityScreenInput.value.trim() || "Not set"}`,
    `Scene version: ${compareSceneVersionInput.value.trim() || "Not set"}`,
    `Screenshot: ${screenshotName}`,
    `Checks: ${checks.length > 0 ? checks.join(", ") : "None selected"}`,
    `Unity dump length: ${compareUnityDumpInput.value.trim().length} characters`,
    "",
    "Planned pipeline:",
    "1. Resolve the target Figma frame or component.",
    "2. Ingest Unity evidence: screenshot plus hierarchy or prefab data.",
    "3. Compare layout, typography, colors, and assets.",
    "4. Produce a discrepancy report with suggested fixes.",
    "5. Forward the report to AI assist for prioritization and explanation."
  ].join("\n");
}

function renderCompareScreenOptions() {
  compareScreenSelect.innerHTML = "";
  compareMatchFigmaSelect.innerHTML = "";

  for (const target of [compareScreenSelect, compareMatchFigmaSelect]) {
    const placeholder = document.createElement("option");
    placeholder.value = "";
    placeholder.textContent =
      compareScreenCandidates.length > 0
        ? "Select a detected Figma screen"
        : "No detected screens yet";
    target.appendChild(placeholder);
  }

  for (const screen of compareScreenCandidates) {
    for (const target of [compareScreenSelect, compareMatchFigmaSelect]) {
      const option = document.createElement("option");
      option.value = screen.id;
      option.textContent = `${screen.name} [${screen.type}]`;
      option.dataset.screenName = screen.name;
      option.dataset.nodeId = screen.id;
      target.appendChild(option);
    }
  }
}

function safeParseJson(value, fallback) {
  try {
    return value ? JSON.parse(value) : fallback;
  } catch {
    return fallback;
  }
}

function extractUnityNameCandidates(text) {
  const lines = String(text ?? "")
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean);
  const candidates = new Set();
  const patterns = [
    /\bname\s*[:=]\s*("?)([A-Za-z0-9 _().\-/#]+)\1/i,
    /^GameObject:\s*([A-Za-z0-9 _().\-/#]+)/i,
    /^m_Name:\s*([A-Za-z0-9 _().\-/#]+)/i,
    /^[-*]?\s*([A-Za-z0-9 _().\-/#]{3,})$/
  ];

  for (const line of lines) {
    for (const pattern of patterns) {
      const match = line.match(pattern);
      const name = match?.[2] || match?.[1];
      if (!name) {
        continue;
      }

      const normalized = name.trim();
      if (
        normalized.length >= 3 &&
        normalized.length <= 80 &&
        !/^(recttransform|image|rawimage|canvasrenderer|textmeshpro|textmeshprougui)$/i.test(
          normalized
        )
      ) {
        candidates.add(normalized);
      }
    }
  }

  return [...candidates].slice(0, 100);
}

function renderUnityCandidateOptions() {
  compareMatchUnitySelect.innerHTML = "";

  const placeholder = document.createElement("option");
  placeholder.value = "";
  placeholder.textContent =
    compareUnityCandidates.length > 0
      ? "Select a detected Unity root"
      : "No detected Unity roots yet";
  compareMatchUnitySelect.appendChild(placeholder);

  for (const candidate of compareUnityCandidates) {
    const option = document.createElement("option");
    option.value = candidate;
    option.textContent = candidate;
    compareMatchUnitySelect.appendChild(option);
  }
}

function renderNameMatches() {
  compareNameMatchesNode.innerHTML = "";

  if (compareNameMatches.length === 0) {
    const empty = document.createElement("div");
    empty.className = "match-chip match-chip-empty";
    empty.textContent = "No saved matches yet";
    compareNameMatchesNode.appendChild(empty);
    return;
  }

  for (const match of compareNameMatches) {
    const chip = document.createElement("button");
    chip.type = "button";
    chip.className = "match-chip";
    chip.textContent = `${match.figmaName} -> ${match.unityName}`;
    chip.addEventListener("click", () => {
      compareFigmaScreenInput.value = match.figmaName;
      compareFigmaNodeIdInput.value = match.figmaNodeId || "";
      compareUnityScreenInput.value = match.unityName;
      persistInputs();
      renderComparePreview();
      logMessage(`Applied saved name match ${match.figmaName} -> ${match.unityName}.`);
    });
    compareNameMatchesNode.appendChild(chip);
  }
}

function saveNameMatch() {
  const figmaOption = compareMatchFigmaSelect.selectedOptions[0];
  const unityName = compareMatchUnitySelect.value.trim();

  if (!figmaOption?.value || !unityName) {
    logMessage("Select both a detected Figma screen and a detected Unity root before saving a match.", "error");
    return;
  }

  const nextMatch = {
    figmaName: figmaOption.dataset.screenName || "",
    figmaNodeId: figmaOption.dataset.nodeId || "",
    unityName
  };

  compareNameMatches = [
    nextMatch,
    ...compareNameMatches.filter(
      (match) =>
        !(
          match.figmaName === nextMatch.figmaName &&
          match.figmaNodeId === nextMatch.figmaNodeId &&
          match.unityName === nextMatch.unityName
        )
    )
  ].slice(0, 20);

  compareFigmaScreenInput.value = nextMatch.figmaName;
  compareFigmaNodeIdInput.value = nextMatch.figmaNodeId;
  compareUnityScreenInput.value = nextMatch.unityName;
  persistInputs();
  renderComparePreview();
  renderNameMatches();
  logMessage(`Saved name match ${nextMatch.figmaName} -> ${nextMatch.unityName}.`);
}

async function inspectCompareSource() {
  if (
    !compareFigmaUrlInput.value.trim() &&
    !compareFigmaJsonInput.value.trim() &&
    compareSourceFile?.kind !== "fig"
  ) {
    logMessage("Provide a Compare Figma source first before detecting screens.", "error");
    return;
  }

  inspectCompareSourceButton.disabled = true;
  logMessage("Inspecting Compare Figma source for screen candidates...");

  try {
    const response = await fetch("/api/inspect-source", {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        figmaUrl: compareFigmaUrlInput.value.trim(),
        rawJson: compareFigmaJsonInput.value.trim(),
        fileKey: fileKeyInput.value.trim(),
        figmaToken: figmaTokenInput.value.trim(),
        localFigBase64: compareSourceFile?.kind === "fig" ? compareSourceFile.base64 : "",
        localFigName: compareSourceFile?.kind === "fig" ? compareSourceFile.name : ""
      })
    });

    const payload = await response.json();
    if (!response.ok) {
      throw new Error(payload.error || "Could not inspect Compare source.");
    }

    compareScreenCandidates = payload.screens || [];
    renderCompareScreenOptions();
    logMessage(`Detected ${compareScreenCandidates.length} Figma screen candidate(s) for Compare.`);

    if (
      compareFigmaScreenInput.value.trim() &&
      compareMatchFigmaSelect.options.length > 1
    ) {
      const matched = [...compareMatchFigmaSelect.options].find(
        (option) => option.dataset.screenName === compareFigmaScreenInput.value.trim()
      );
      if (matched) {
        compareMatchFigmaSelect.value = matched.value;
      }
    }
  } catch (error) {
    compareScreenCandidates = [];
    renderCompareScreenOptions();
    logMessage(formatRequestError(error), "error");
  } finally {
    inspectCompareSourceButton.disabled = false;
  }
}

function startCompareAnalysis() {
  void runCompareAnalysis();
}

async function runCompareAnalysis() {
  const checks = selectedCompareChecks();
  const startedAt = performance.now();
  let slowCompareTimer = null;
  logMessage(
    `Compare requested for ${compareUnityScreenInput.value.trim() || "unnamed Unity screen"} against ${compareFigmaUrlInput.value.trim() || "unspecified Figma target"}.`
  );
  logMessage(`Requested checks: ${checks.length > 0 ? checks.join(", ") : "none"}.`);

  try {
    startCompareButton.disabled = true;
    comparePreview.textContent = "Running compare analysis...";
    slowCompareTimer = window.setTimeout(() => {
      logMessage(
        "Compare is still running after 8 seconds. Small JSON inputs should usually finish in a few seconds; large local .fig files can take longer.",
        "warn"
      );
    }, 8000);

    const controller = new AbortController();
    const timeoutId = window.setTimeout(() => controller.abort(), 90000);

    const response = await fetch("/api/compare", {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      signal: controller.signal,
      body: JSON.stringify({
        figmaUrl: compareFigmaUrlInput.value.trim(),
        rawJson: compareFigmaJsonInput.value.trim(),
        fileKey: fileKeyInput.value.trim(),
        figmaToken: figmaTokenInput.value.trim(),
        screenName: compareFigmaScreenInput.value.trim(),
        nodeId: compareFigmaNodeIdInput.value.trim(),
        unityDump: compareUnityDumpInput.value,
        screenshotName: compareScreenshotInput.files?.[0]?.name || "",
        checks,
        ...(compareSourceFile?.kind === "fig"
          ? {
              localFigBase64: compareSourceFile.base64,
              localFigName: compareSourceFile.name
            }
          : {})
      })
    });
    window.clearTimeout(timeoutId);

    const report = await response.json();
    if (!response.ok) {
      throw new Error(report.error || "Compare failed.");
    }

    const lines = [
      "Compare report",
      "",
      `Figma screen: ${report.summary.figmaScreen.name} (${report.summary.figmaScreen.nodeId})`,
      `Canvas: ${report.summary.figmaScreen.canvas.width} x ${report.summary.figmaScreen.canvas.height}`,
      `Checks: ${report.summary.checks.join(", ") || "none"}`,
      `Unity dump lines: ${report.summary.unityEvidence.lineCount}`,
      `Screenshot: ${report.summary.unityEvidence.screenshotName || "none"}`,
      `Findings: ${report.summary.findingCount}`,
      ""
    ];

    if (report.findings.length === 0) {
      lines.push("No obvious mismatches detected from the supplied evidence.");
    } else {
      report.findings.forEach((finding, index) => {
        lines.push(`${index + 1}. [${finding.severity.toUpperCase()}] ${finding.title}`);
        lines.push(`   Category: ${finding.category}`);
        lines.push(`   Evidence: ${finding.evidence}`);
        lines.push(`   Suggested fix: ${finding.suggestedFix}`);
        lines.push("");
      });
    }

    lines.push(`Next step: ${report.suggestedNextStep}`);
    comparePreview.textContent = lines.join("\n");
    logMessage(
      `Compare finished with ${report.summary.findingCount} finding(s) in ${((performance.now() - startedAt) / 1000).toFixed(1)}s.`
    );
  } catch (error) {
    comparePreview.textContent = `Compare failed.\n\n${formatRequestError(error)}`;
    logMessage(formatRequestError(error), "error");
  } finally {
    if (slowCompareTimer) {
      window.clearTimeout(slowCompareTimer);
    }
    startCompareButton.disabled = false;
  }
}

function setMode(mode) {
  const isGenerate = mode === "generate";
  generateWorkspace.classList.toggle("workspace-hidden", !isGenerate);
  compareWorkspace.classList.toggle("workspace-hidden", isGenerate);
  modeGenerateButton.classList.toggle("active", isGenerate);
  modeCompareButton.classList.toggle("active", !isGenerate);
  modeGenerateButton.setAttribute("aria-pressed", String(isGenerate));
  modeCompareButton.setAttribute("aria-pressed", String(!isGenerate));
  logMessage(`Switched to ${isGenerate ? "Generate" : "Compare"} mode.`);
}

function isNetworkLikeError(error) {
  return /networkerror|failed to fetch|load failed|fetch failed/i.test(String(error?.message ?? error));
}

function formatRequestError(error) {
  if (isNetworkLikeError(error)) {
    return "Funity could not reach the local server. Open http://127.0.0.1:4173 from the running server window, not the HTML file directly.";
  }

  if (error?.name === "AbortError") {
    return "Compare timed out after 90 seconds. This usually means the input was unusually large or the server needs to be restarted.";
  }

  return error?.message || "Request failed.";
}

function loadPersistedInputs() {
  figmaUrlInput.value = localStorage.getItem(STORAGE_KEYS.figmaUrl) || "";
  fileKeyInput.value = localStorage.getItem(STORAGE_KEYS.fileKey) || "";
  figmaTokenInput.value = localStorage.getItem(STORAGE_KEYS.figmaToken) || "";
  screenNameInput.value = localStorage.getItem(STORAGE_KEYS.screenName) || "";
  nodeIdInput.value = localStorage.getItem(STORAGE_KEYS.nodeId) || "";
  compareFigmaUrlInput.value = localStorage.getItem(STORAGE_KEYS.compareFigmaUrl) || "";
  compareFigmaJsonInput.value = localStorage.getItem(STORAGE_KEYS.compareFigmaJson) || "";
  compareFigmaScreenInput.value = localStorage.getItem(STORAGE_KEYS.compareFigmaScreen) || "";
  compareFigmaNodeIdInput.value = localStorage.getItem(STORAGE_KEYS.compareFigmaNodeId) || "";
  compareNameMatches = safeParseJson(localStorage.getItem(STORAGE_KEYS.compareNameMatches), []);
  compareUnityScreenInput.value = localStorage.getItem(STORAGE_KEYS.compareUnityScreen) || "";
  compareSceneVersionInput.value = localStorage.getItem(STORAGE_KEYS.compareSceneVersion) || "";
  compareUnityDumpInput.value = localStorage.getItem(STORAGE_KEYS.compareUnityDump) || "";

  const savedIncludeAssets = localStorage.getItem(STORAGE_KEYS.includeAssets);
  if (savedIncludeAssets !== null) {
    includeAssetsInput.checked = savedIncludeAssets === "true";
  }

  const savedIncludeRenderedAssets = localStorage.getItem(STORAGE_KEYS.includeRenderedAssets);
  if (savedIncludeRenderedAssets !== null) {
    includeRenderedAssetsInput.checked = savedIncludeRenderedAssets === "true";
  }
}

function persistInputs() {
  localStorage.setItem(STORAGE_KEYS.figmaUrl, figmaUrlInput.value.trim());
  localStorage.setItem(STORAGE_KEYS.fileKey, fileKeyInput.value.trim());
  localStorage.setItem(STORAGE_KEYS.figmaToken, figmaTokenInput.value.trim());
  localStorage.setItem(STORAGE_KEYS.screenName, screenNameInput.value.trim());
  localStorage.setItem(STORAGE_KEYS.nodeId, nodeIdInput.value.trim());
  localStorage.setItem(STORAGE_KEYS.includeAssets, String(includeAssetsInput.checked));
  localStorage.setItem(STORAGE_KEYS.includeRenderedAssets, String(includeRenderedAssetsInput.checked));
  localStorage.setItem(STORAGE_KEYS.compareFigmaUrl, compareFigmaUrlInput.value.trim());
  localStorage.setItem(STORAGE_KEYS.compareFigmaJson, compareFigmaJsonInput.value);
  localStorage.setItem(STORAGE_KEYS.compareFigmaScreen, compareFigmaScreenInput.value.trim());
  localStorage.setItem(STORAGE_KEYS.compareFigmaNodeId, compareFigmaNodeIdInput.value.trim());
  localStorage.setItem(STORAGE_KEYS.compareNameMatches, JSON.stringify(compareNameMatches));
  localStorage.setItem(STORAGE_KEYS.compareUnityScreen, compareUnityScreenInput.value.trim());
  localStorage.setItem(STORAGE_KEYS.compareSceneVersion, compareSceneVersionInput.value.trim());
  localStorage.setItem(STORAGE_KEYS.compareUnityDump, compareUnityDumpInput.value);
}

function readFileAsText(file) {
  return file.text();
}

function readFileAsBase64(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const result = String(reader.result ?? "");
      resolve(result.includes(",") ? result.split(",").pop() || "" : result);
    };
    reader.onerror = () => reject(reader.error || new Error(`Could not read ${file.name}.`));
    reader.readAsDataURL(file);
  });
}

function clearSavedToken() {
  localStorage.removeItem(STORAGE_KEYS.figmaToken);
  figmaTokenInput.value = "";
  setStatus("Saved Figma token cleared from this browser.");
}

function switchTab(fileName) {
  activeFile = fileName;
  outputViewer.textContent = currentFiles[fileName] ?? "";

  for (const tab of tabs) {
    tab.classList.toggle("active", tab.dataset.target === fileName);
  }
}

function downloadFile(fileName, contents) {
  const blob = new Blob([contents], { type: "text/plain;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}

function downloadBinaryFile(fileName, base64, contentType) {
  const binary = Uint8Array.from(atob(base64), (char) => char.charCodeAt(0));
  const blob = new Blob([binary], { type: contentType || "application/octet-stream" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}

async function downloadAllOutput() {
  const fileEntries = Object.entries(currentFiles);
  if (fileEntries.length === 0 && currentAssets.length === 0) {
    setStatus("Nothing has been generated yet.", true);
    return;
  }

  for (const [fileName, contents] of fileEntries) {
    downloadFile(fileName, contents);
    await new Promise((resolve) => setTimeout(resolve, 120));
  }

  for (const asset of currentAssets) {
    downloadBinaryFile(asset.fileName, asset.base64, asset.contentType);
    await new Promise((resolve) => setTimeout(resolve, 120));
  }

  setStatus(
    `Started downloading ${fileEntries.length} generated files and ${currentAssets.length} image asset${currentAssets.length === 1 ? "" : "s"} to your browser's default Downloads location.`
  );
}

function renderSummary(scene, assets) {
  const assetCount = assets?.length ?? 0;
  const imageFillCount = assets.filter((asset) => asset.kind !== "rendered-node").length;
  const renderedNodeCount = assets.filter((asset) => asset.kind === "rendered-node").length;
  summaryNode.classList.remove("empty");
  summaryNode.innerHTML = [
    `<strong>${scene.name}</strong>`,
    `Canvas: ${scene.canvas.width} x ${scene.canvas.height}`,
    `Fonts: ${scene.fonts.length > 0 ? scene.fonts.map((font) => `${font.family} (${font.weights.join(", ")})`).join(", ") : "None detected"}`,
    `Root children: ${scene.root.children.length}`,
    `Image fill assets downloaded: ${imageFillCount}`,
    `Rendered layer assets exported: ${renderedNodeCount}`,
    `Total downloaded assets: ${assetCount}`,
    ...(assetCount === 0 ? [`Check console for export/debug details.`] : []),
    `Browser downloads go to your browser's default Downloads folder. CLI downloads go to output/assets/.`
  ].join("<br />");
}

function renderDownloads(files) {
  downloadsNode.innerHTML = "";

  for (const [fileName, contents] of Object.entries(files)) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "download-button";
    button.textContent = `Download ${fileName}`;
    button.addEventListener("click", () => downloadFile(fileName, contents));
    downloadsNode.appendChild(button);
  }
}

function renderAssetDownloads(assets) {
  for (const asset of assets || []) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "download-button";
    button.textContent = `Download ${asset.fileName}`;
    button.addEventListener("click", () =>
      downloadBinaryFile(asset.fileName, asset.base64, asset.contentType)
    );
    downloadsNode.appendChild(button);
  }
}

function buildSourcePayload() {
  persistInputs();
  const payload = {
    rawJson: jsonInput.value.trim(),
    figmaUrl: figmaUrlInput.value.trim(),
    fileKey: fileKeyInput.value.trim(),
    figmaToken: figmaTokenInput.value.trim(),
    includeAssets: includeAssetsInput.checked,
    includeRenderedAssets: includeRenderedAssetsInput.checked,
    screenName: screenNameInput.value.trim(),
    nodeId: nodeIdInput.value.trim()
  };

  if (generateSourceFile?.kind === "fig") {
    payload.localFigBase64 = generateSourceFile.base64;
    payload.localFigName = generateSourceFile.name;
  }

  return payload;
}

async function generateArtifacts() {
  const requestPayload = buildSourcePayload();
  if (
    !requestPayload.rawJson &&
    !requestPayload.figmaUrl &&
    !requestPayload.fileKey &&
    !requestPayload.localFigBase64
  ) {
    setStatus("Paste JSON, upload a .fig file, or provide a Figma URL/file key first.", true);
    return;
  }

  setStatus("Generating Unity artifacts...");
  generateButton.disabled = true;

  try {
    const response = await fetch("/api/convert", {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify(requestPayload)
    });

    const responsePayload = await response.json();
    if (!response.ok) {
      throw new Error(responsePayload.error || "Conversion failed.");
    }

    currentFiles = responsePayload.files;
    currentAssets = responsePayload.assets || [];
    renderSummary(responsePayload.scene, currentAssets);
    renderDownloads(responsePayload.files);
    renderAssetDownloads(currentAssets);
    downloadAllButton.disabled = false;
    logMessage(`Selected screen: ${responsePayload.scene.name}. Downloaded ${currentAssets.length} asset(s).`);
    if (responsePayload.files?.["debug-export.json"]) {
      try {
        const debug = JSON.parse(responsePayload.files["debug-export.json"]);
        const selected = debug?.selectedScreen;
        if (selected) {
          logMessage(
            `Debug selected node: ${selected.nodeName || "unknown"} (${selected.nodeType || "unknown"}, ${selected.nodeId || "n/a"}).`
          );
        }
        const renderDebug = debug?.renderedNodeExport;
        if (renderDebug) {
          logMessage(`Renderable candidates: ${renderDebug.candidateCount ?? 0}.`);
          const resolved = (renderDebug.batches ?? []).reduce(
            (sum, batch) => sum + (batch.resolvedNodeIds?.length ?? 0),
            0
          );
          logMessage(`Rendered export URLs returned: ${resolved}.`);
        }
      } catch {
        logMessage("Debug export data could not be parsed.", "error");
      }
    }
    switchTab(currentFiles[activeFile] ? activeFile : "unity-import.cs");
    setStatus("Artifacts generated. Review them in the panel or download them.");
  } catch (error) {
    downloadAllButton.disabled = Object.keys(currentFiles).length === 0 && currentAssets.length === 0;
    setStatus(formatRequestError(error), true);
  } finally {
    generateButton.disabled = false;
  }
}

async function importFromFigma() {
  const figmaUrl = figmaUrlInput.value.trim();
  const fileKey = fileKeyInput.value.trim();
  if (!figmaUrl && !fileKey) {
    setStatus("Provide a Figma URL or file key first.", true);
    return;
  }

  setStatus("Importing from Figma API...");
  importFigmaButton.disabled = true;

  try {
    const response = await fetch("/api/import-figma", {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        figmaUrl,
        fileKey,
        nodeId: nodeIdInput.value.trim(),
        figmaToken: figmaTokenInput.value.trim()
      })
    });

    const payload = await response.json();
    if (!response.ok) {
      throw new Error(payload.error || "Figma import failed.");
    }

    generateSourceFile = null;
    jsonInput.value = payload.rawJson;
    fileKeyInput.value = payload.fileKey || fileKey;
    if (payload.nodeId && !nodeIdInput.value.trim()) {
      nodeIdInput.value = payload.nodeId;
    }
    setStatus("Figma JSON imported. You can inspect it or generate artifacts now.");
  } catch (error) {
    setStatus(formatRequestError(error), true);
  } finally {
    importFigmaButton.disabled = false;
  }
}

async function loadExample() {
  setStatus("Loading example...");

  try {
    const response = await fetch("/api/example");
    const payload = await response.json();
    generateSourceFile = null;
    jsonInput.value = JSON.stringify(payload, null, 2);
    screenNameInput.value = "Sample Screen";
    nodeIdInput.value = "";
    setStatus("Example loaded.");
  } catch {
    setStatus("Could not load example. Make sure the local Funity server is running.", true);
  }
}

async function checkServerHealth() {
  try {
    const response = await fetch("/api/health");
    if (!response.ok) {
      throw new Error("Health check failed.");
    }
  } catch (error) {
    setStatus(formatRequestError(error), true);
  }
}

fileInput.addEventListener("change", async (event) => {
  const file = event.target.files?.[0];
  if (!file) {
    generateSourceFile = null;
    return;
  }

  if (file.name.toLowerCase().endsWith(".fig")) {
    generateSourceFile = {
      name: file.name,
      kind: "fig",
      base64: await readFileAsBase64(file)
    };
    jsonInput.value = "";
    setStatus(`Loaded ${file.name} as a local Figma .fig archive.`);
    logMessage(
      `${file.name} selected for Generate as a local .fig archive. Funity can now decode canvas.fig and generate from local exports.`,
      "info"
    );
    return;
  }

  generateSourceFile = {
    name: file.name,
    kind: "json"
  };
  jsonInput.value = await readFileAsText(file);
  setStatus(`Loaded ${file.name}.`);
});

compareFigmaJsonFileInput.addEventListener("change", async (event) => {
  const file = event.target.files?.[0];
  if (!file) {
    compareSourceFile = null;
    renderComparePreview();
    return;
  }

  if (file.name.toLowerCase().endsWith(".figma")) {
    compareFigmaJsonInput.value = "";
    persistInputs();
    renderComparePreview();
    compareScreenCandidates = [];
    renderCompareScreenOptions();
    logMessage(
      ".figma file selected in Compare mode. Native .figma parsing is not implemented yet; please export or paste JSON for now.",
      "error"
    );
    return;
  }

  if (file.name.toLowerCase().endsWith(".fig")) {
    compareSourceFile = {
      name: file.name,
      kind: "fig",
      base64: await readFileAsBase64(file)
    };
    compareFigmaJsonInput.value = "";
    persistInputs();
    renderComparePreview();
    compareScreenCandidates = [];
    renderCompareScreenOptions();
    logMessage(
      `${file.name} selected for Compare as a local .fig archive. Funity can now decode canvas.fig for comparison input.`,
      "info"
    );
    void inspectCompareSource();
    return;
  }

  compareSourceFile = {
    name: file.name,
    kind: "json"
  };
  compareFigmaJsonInput.value = await readFileAsText(file);
  persistInputs();
  renderComparePreview();
  compareScreenCandidates = [];
  renderCompareScreenOptions();
  logMessage(`Loaded compare Figma source from ${file.name}.`);
  void inspectCompareSource();
});

compareUnityDumpFileInput.addEventListener("change", async (event) => {
  const file = event.target.files?.[0];
  if (!file) {
    renderComparePreview();
    return;
  }

  compareUnityDumpInput.value = await readFileAsText(file);
  compareUnityCandidates = extractUnityNameCandidates(compareUnityDumpInput.value);
  persistInputs();
  renderComparePreview();
  renderUnityCandidateOptions();
  logMessage(`Loaded Unity compare dump from ${file.name}.`);
});

compareScreenSelect.addEventListener("change", () => {
  const selectedOption = compareScreenSelect.selectedOptions[0];
  if (!selectedOption?.value) {
    return;
  }

  compareFigmaScreenInput.value = selectedOption.dataset.screenName || "";
  compareFigmaNodeIdInput.value = selectedOption.dataset.nodeId || "";
  persistInputs();
  renderComparePreview();
  logMessage(
    `Selected Compare target ${compareFigmaScreenInput.value} (${compareFigmaNodeIdInput.value}).`
  );
});

saveNameMatchButton.addEventListener("click", saveNameMatch);

compareScreenSelect.addEventListener("change", () => {
  const selectedOption = compareScreenSelect.selectedOptions[0];
  if (!selectedOption || !selectedOption.value) {
    return;
  }

  compareFigmaScreenInput.value = selectedOption.dataset.screenName || "";
  compareFigmaNodeIdInput.value = selectedOption.dataset.nodeId || "";
  persistInputs();
  renderComparePreview();
  logMessage(
    `Selected Compare target ${compareFigmaScreenInput.value} (${compareFigmaNodeIdInput.value}).`
  );
});

for (const input of [
  figmaUrlInput,
  fileKeyInput,
  figmaTokenInput,
  screenNameInput,
  nodeIdInput,
  compareFigmaUrlInput,
  compareFigmaJsonInput,
  compareFigmaScreenInput,
  compareFigmaNodeIdInput,
  compareUnityScreenInput,
  compareSceneVersionInput,
  compareUnityDumpInput
]) {
  input.addEventListener("input", () => {
    if (input === jsonInput && jsonInput.value.trim()) {
      generateSourceFile = null;
    }

    if (input === compareFigmaJsonInput && compareFigmaJsonInput.value.trim()) {
      compareSourceFile = null;
    }

    if (input === compareUnityDumpInput) {
      compareUnityCandidates = extractUnityNameCandidates(compareUnityDumpInput.value);
      renderUnityCandidateOptions();
    }

    persistInputs();
  });
}

includeAssetsInput.addEventListener("change", persistInputs);
includeRenderedAssetsInput.addEventListener("change", persistInputs);
clearSavedTokenButton.addEventListener("click", clearSavedToken);
clearConsoleButton.addEventListener("click", clearLogs);
clearCompareConsoleButton.addEventListener("click", clearLogs);
modeGenerateButton.addEventListener("click", () => setMode("generate"));
modeCompareButton.addEventListener("click", () => setMode("compare"));
startCompareButton.addEventListener("click", startCompareAnalysis);
inspectCompareSourceButton.addEventListener("click", () => {
  void inspectCompareSource();
});

generateButton.addEventListener("click", generateArtifacts);
downloadAllButton.addEventListener("click", downloadAllOutput);
importFigmaButton.addEventListener("click", importFromFigma);
loadExampleButton.addEventListener("click", loadExample);

for (const tab of tabs) {
  tab.addEventListener("click", () => switchTab(tab.dataset.target));
}

for (const input of [
  compareFigmaJsonInput,
  compareFigmaUrlInput,
  compareFigmaScreenInput,
  compareFigmaNodeIdInput,
  compareUnityScreenInput,
  compareSceneVersionInput,
  compareUnityDumpInput
]) {
  input.addEventListener("input", renderComparePreview);
}

for (const input of [
  compareLayoutInput,
  compareTypographyInput,
  compareColorsInput,
  compareAssetsInput,
  compareScreenshotInput
]) {
  input.addEventListener("change", renderComparePreview);
}

for (const input of [
  generateFilterInfoInput,
  generateFilterWarnInput,
  generateFilterErrorInput
]) {
  input.addEventListener("change", () => renderConsole("generate"));
}

for (const input of [
  compareFilterInfoInput,
  compareFilterWarnInput,
  compareFilterErrorInput
]) {
  input.addEventListener("change", () => renderConsole("compare"));
}

switchTab(activeFile);
renderCompareScreenOptions();
compareUnityCandidates = extractUnityNameCandidates(compareUnityDumpInput.value);
renderUnityCandidateOptions();
renderNameMatches();
logMessage("Funity console initialized.");
logMessage("Compare console initialized.");
setMode("generate");
loadPersistedInputs();
renderComparePreview();
checkServerHealth();
