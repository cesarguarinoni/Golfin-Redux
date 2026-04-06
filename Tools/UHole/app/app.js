const ANCHOR_TYPES = [
  { type: "tee_back", label: "Back Tee" },
  { type: "tee_regular", label: "Regular Tee" },
  { type: "tee_front", label: "Front Tee" },
  { type: "tee_ladies", label: "Ladies Tee" },
  { type: "green_center", label: "Green Center" },
  { type: "pin", label: "Pin" }
];

const state = {
  course: null,
  provenance: null,
  holes: [],
  basemap: null,
  selectedHoleNumber: 1,
  editorMode: "align",           // "align" | "fine-tune" | "anchors"
  officialOpacity: 0.5,
  pendingControlPoint: null,     // queued official-side click for fine-tune mode
  selectedAnchorType: "tee_back",
  visualAlignment: {
    locked: false,
    basemapTransform: { x: 0, y: 0, scale: 1, rotation: 0 }
  },
  viewport: {
    official: { scale: 1, x: 0, y: 0, rotation: 0 },
    basemap: { scale: 1, x: 0, y: 0, rotation: 0 }
  }
};

// --- Cached DOM elements ---

const statsEl = document.getElementById("stats");
const sourcesListEl = document.getElementById("sources-list");
const holesGridEl = document.getElementById("holes-grid");
const exportPreviewEl = document.getElementById("export-preview");
const alignmentGridEl = document.getElementById("alignment-grid");
const statusBannerEl = document.getElementById("status-banner");
const basemapSummaryEl = document.getElementById("basemap-summary");
const basemapPreviewEl = document.getElementById("basemap-preview");
const editorHoleSelectEl = document.getElementById("editor-hole-select");
const stackedStageEl = document.getElementById("stacked-stage");
const layerBasemapEl = document.getElementById("layer-basemap");
const layerOfficialEl = document.getElementById("layer-official");
const layerMarkersEl = document.getElementById("layer-markers");
const opacitySliderEl = document.getElementById("opacity-slider");
const lockAlignmentEl = document.getElementById("lock-alignment");
const modeHintEl = document.getElementById("mode-hint");
const controlPointsListEl = document.getElementById("control-points-list");
const anchorsBarEl = document.getElementById("anchors-bar");
const anchorsListEl = document.getElementById("anchors-list");
const viewReadoutEl = document.getElementById("view-readout");
const basemapCoordinateReadoutEl = document.getElementById("basemap-coordinate-readout");

// --- Event wiring ---

document.getElementById("refresh-data").addEventListener("click", loadData);
document.getElementById("refetch-ingest").addEventListener("click", () => runIngest(true));
document.getElementById("refetch-basemap").addEventListener("click", () => fetchBasemap(true));
document.getElementById("init-alignment").addEventListener("click", initAlignment);
document.getElementById("save-alignment").addEventListener("click", saveAlignment);
document.getElementById("compute-transform").addEventListener("click", computeTransform);
document.getElementById("export-hole").addEventListener("click", exportHoleForUnity);
document.getElementById("rotate-left").addEventListener("click", () => handleRotate(-5));
document.getElementById("rotate-right").addEventListener("click", () => handleRotate(5));
document.getElementById("reset-view").addEventListener("click", handleResetView);

for (const btn of document.querySelectorAll(".mode-btn")) {
  btn.addEventListener("click", () => switchMode(btn.dataset.mode));
}

opacitySliderEl.addEventListener("input", () => {
  state.officialOpacity = Number(opacitySliderEl.value) / 100;
  layerOfficialEl.style.opacity = state.officialOpacity;
});

lockAlignmentEl.addEventListener("click", toggleLock);

initAnchorTypeDropdown();

for (const button of document.querySelectorAll(".nav-link")) {
  button.addEventListener("click", () => activatePanel(button.dataset.panel));
}

attachStackedInteractions();
await loadData();

// ===================================================================
//  DATA LOADING
// ===================================================================

async function loadData() {
  const base = "/output/lomond-country-club";
  const [courseResult, provenance, sourceCache] = await Promise.all([
    fetchJson(`${base}/course.json`, "/examples/lomond-country-club/course.json", true),
    fetchJson(`${base}/provenance.json`, "/examples/lomond-country-club/provenance.json"),
    fetchJson(`${base}/source/cache.json`, null).catch(() => null)
  ]);
  const course = courseResult.data;
  const usingFallbackCourse = courseResult.source === "fallback";

  const holes = [];
  let basemap = null;
  try {
    basemap = await fetchJson(`${base}/basemap/manifest.json`, null);
  } catch {
    basemap = null;
  }
  for (const holeRef of course.holes) {
    const outputPath = `${base}/${holeRef.path}`;
    const examplePath = `/examples/lomond-country-club/${holeRef.path}`;
    try {
      const hole = await fetchJson(outputPath, examplePath);
      const alignmentPath = hole.assets?.alignment_json ? `${base}/${hole.assets.alignment_json}` : null;
      let alignment = null;
      if (alignmentPath) {
        try {
          alignment = await fetchJson(alignmentPath, null);
        } catch {
          alignment = null;
        }
      }
      holes.push({ ...hole, alignment });
    } catch (error) {
      if (!usingFallbackCourse) {
        throw error;
      }
    }
  }

  state.course = course;
  state.provenance = provenance;
  state.holes = holes;
  state.basemap = basemap;
  if (!state.holes.find((hole) => hole.hole_number === state.selectedHoleNumber)) {
    state.selectedHoleNumber = state.holes[0]?.hole_number || 1;
  }

  // Restore visual alignment from saved data
  const selectedHole = getSelectedHole();
  if (selectedHole?.alignment?.visual_alignment?.locked) {
    const saved = selectedHole.alignment.visual_alignment;
    state.visualAlignment.locked = true;
    state.visualAlignment.basemapTransform = { ...saved.basemap_transform };
    state.viewport.basemap = {
      x: saved.basemap_transform.translate_x || 0,
      y: saved.basemap_transform.translate_y || 0,
      scale: saved.basemap_transform.scale || 1,
      rotation: saved.basemap_transform.rotation_deg || 0
    };
    lockAlignmentEl.textContent = "Unlock";
    lockAlignmentEl.classList.add("is-locked");
  }

  render();
  loadSavedViewport("official");
  loadSavedViewport("basemap");
  if (sourceCache) {
    const cachedImages = (sourceCache.holes || []).filter((item) => item.cached).length;
    setStatus(`Loaded course package from disk. Lomond cache has ${cachedImages} cached hole images available.`, "info");
  } else {
    setStatus("Loaded course package from the latest available export.", "info");
  }
}

async function fetchJson(primary, fallback, withMeta = false) {
  if (primary) {
    const primaryResponse = await fetch(primary);
    if (primaryResponse.ok) {
      const data = await primaryResponse.json();
      return withMeta ? { data, source: "primary" } : data;
    }
  }
  if (!fallback) {
    throw new Error(`Could not load ${primary}`);
  }
  const fallbackResponse = await fetch(fallback);
  if (!fallbackResponse.ok) {
    throw new Error(`Could not load ${primary} or ${fallback}`);
  }
  const data = await fallbackResponse.json();
  return withMeta ? { data, source: "fallback" } : data;
}

// ===================================================================
//  RENDER
// ===================================================================

function render() {
  renderStats();
  renderSources();
  renderBasemap();
  renderAlignment();
  renderAlignmentEditor();
  renderHoles();
  renderExport();
}

function renderStats() {
  const course = state.course;
  const stats = [
    { label: "Holes", value: course.hole_count },
    { label: "Par", value: course.par_total },
    { label: "Status", value: course.review_status },
    { label: "Yardage claims", value: course.yardage_claims.length }
  ];
  statsEl.innerHTML = stats
    .map((stat) => `
      <article class="stat-card">
        <div class="eyebrow">${stat.label}</div>
        <strong>${stat.value}</strong>
      </article>
    `)
    .join("");
}

function renderSources() {
  const provenance = state.provenance;
  sourcesListEl.innerHTML = provenance.sources
    .map((source) => `
      <article class="source-card">
        <div class="eyebrow">${source.category}</div>
        <h4>${source.source_id}</h4>
        <p><a href="${source.url}" target="_blank" rel="noreferrer">${source.url}</a></p>
        <p>Trust: <span class="chip">${source.trust_level}</span></p>
        <p>Use mode: ${source.use_mode}</p>
        <p>License: ${source.license_status}</p>
      </article>
    `)
    .join("");
}

function renderHoles() {
  holesGridEl.innerHTML = state.holes
    .map((hole) => {
      const back = hole.tee_sets.find((tee) => tee.name === "Back");
      return `
        <article class="hole-card">
          <div class="eyebrow">Hole ${hole.hole_number}</div>
          <h4>Par ${hole.par}</h4>
          <p>Stroke index ${hole.stroke_index}</p>
          <p>Back tee ${back ? `${back.yards} yd` : "n/a"}</p>
          <p><span class="chip">${hole.geometry_status}</span></p>
        </article>
      `;
    })
    .join("");
}

function renderAlignment() {
  alignmentGridEl.innerHTML = state.holes
    .map((hole) => {
      const alignment = hole.alignment;
      const status = alignment?.status || "missing";
      const controlPointCount = alignment?.control_points?.length || 0;
      const anchorCount = alignment?.anchors?.length || 0;
      const sourceUrl = alignment?.official_map?.source_url;
      const selectedClass = hole.hole_number === state.selectedHoleNumber ? "is-selected" : "";
      return `
        <article class="hole-card is-selectable ${selectedClass}" data-hole-card="${hole.hole_number}" role="button" tabindex="0" aria-pressed="${hole.hole_number === state.selectedHoleNumber}">
          <div class="eyebrow">Hole ${hole.hole_number}</div>
          <h4>${status.replaceAll("_", " ")}</h4>
          <p>CP: ${controlPointCount} / Anchors: ${anchorCount}</p>
          <p><span class="chip">${alignment?.visual_alignment?.locked ? "aligned" : alignment?.transform?.pixel_to_world_ready ? `transform (${alignment.transform.mean_residual_m}m)` : "pending"}</span></p>
          <p>${sourceUrl ? `<a href="${sourceUrl}" target="_blank" rel="noreferrer">Official map</a>` : "No official map link"}</p>
        </article>
      `;
    })
    .join("");

  for (const card of alignmentGridEl.querySelectorAll("[data-hole-card]")) {
    const selectHole = () => {
      state.selectedHoleNumber = Number(card.dataset.holeCard);
      state.pendingControlPoint = null;
      loadHoleVisualAlignment();
      renderAlignment();
      renderAlignmentEditor();
      loadSavedViewport("official");
      loadSavedViewport("basemap");
      scrollSelectedHoleCardIntoView();
    };
    card.addEventListener("click", selectHole);
    card.addEventListener("keydown", (event) => {
      if (event.key === "Enter" || event.key === " ") {
        event.preventDefault();
        selectHole();
      }
    });
  }
}

function renderBasemap() {
  const basemap = state.basemap;
  if (!basemap) {
    basemapSummaryEl.innerHTML = "<p>No base-map manifest yet. Use Refetch GSI Base Map to populate the local cache.</p>";
    basemapPreviewEl.innerHTML = "";
    return;
  }
  basemapSummaryEl.innerHTML = `
    <p><strong>Provider:</strong> ${basemap.provider}</p>
    <p><strong>Center:</strong> ${basemap.center.lat}, ${basemap.center.lon}</p>
    <p><strong>Confidence:</strong> ${basemap.center.confidence}</p>
    <p><strong>Attribution:</strong> ${basemap.attribution.short}</p>
    ${basemap.datasets.map((dataset) => `
      <p><span class="chip">${dataset.id}</span> ${dataset.tile_count} tiles at z${dataset.zoom}, ~${dataset.tile_span_m}m per tile edge</p>
    `).join("")}
  `;
  const photoDataset = basemap.datasets.find((dataset) => dataset.type === "imagery");
  const previewTiles = (photoDataset?.tiles || []).slice(0, 6);
  basemapPreviewEl.innerHTML = previewTiles
    .map((tile) => `
      <figure class="basemap-tile">
        <img src="/output/lomond-country-club/${tile.local_path}" alt="GSI tile z${tile.z} ${tile.x} ${tile.y}">
        <figcaption>z${tile.z} / ${tile.x} / ${tile.y}</figcaption>
      </figure>
    `)
    .join("");
}

function renderExport() {
  exportPreviewEl.textContent = JSON.stringify(
    {
      course_id: state.course.course_id,
      hole_scene_names: state.holes.map((hole) => `Hole_${String(hole.hole_number).padStart(2, "0")}`),
      required_review_status: ["reviewed", "production_candidate"],
      generated_roots: ["TerrainRoot", "GameplaySurfaces", "Hazards", "Anchors", "DebugReferences"]
    },
    null,
    2
  );
}

// ===================================================================
//  ALIGNMENT EDITOR — stacked overlay
// ===================================================================

function renderAlignmentEditor() {
  const hole = getSelectedHole();
  if (!hole) return;

  const photoTiles = getPhotoTiles();

  // Hole selector
  editorHoleSelectEl.innerHTML = state.holes
    .map((item) => `<option value="${item.hole_number}" ${item.hole_number === hole.hole_number ? "selected" : ""}>Hole ${item.hole_number}</option>`)
    .join("");
  editorHoleSelectEl.onchange = () => {
    state.selectedHoleNumber = Number(editorHoleSelectEl.value);
    state.pendingControlPoint = null;
    loadHoleVisualAlignment();
    renderAlignment();
    renderAlignmentEditor();
    loadSavedViewport("official");
    loadSavedViewport("basemap");
    scrollSelectedHoleCardIntoView();
  };

  // Render basemap layer
  if (photoTiles.length) {
    layerBasemapEl.innerHTML = renderBasemapMosaicMarkup(photoTiles);
  } else {
    layerBasemapEl.innerHTML = "<p class=\"helper-copy\" style=\"padding:2rem;\">No GSI tiles. Use Refetch GSI Base Map.</p>";
  }

  // Render official map layer
  const officialImagePath = `/output/lomond-country-club/${hole.assets.official_references[0]}`;
  layerOfficialEl.innerHTML = renderStageMarkup({
    imagePath: officialImagePath,
    alt: `Official hole ${hole.hole_number} map`,
    action: "official"
  });

  // Apply opacity
  layerOfficialEl.style.opacity = state.officialOpacity;
  opacitySliderEl.value = Math.round(state.officialOpacity * 100);

  // Apply viewports
  applyViewport("basemap");
  applyViewport("official");

  // Update mode UI
  updateModeUI();
  updateTransformUI();
  updateAnchorsUI();
  updateMarkerPositions();

  // Basemap mousemove for coordinate readout
  for (const tileBtn of layerBasemapEl.querySelectorAll(".mosaic-tile-button")) {
    tileBtn.addEventListener("mousemove", (event) => {
      const tile = photoTiles.find((t) => t.local_path === tileBtn.dataset.tilePath);
      if (tile) updateBasemapCoordinateReadout(event, tile);
    });
  }
  layerBasemapEl.addEventListener("mouseleave", resetBasemapCoordinateReadout);
}

function renderStageMarkup({ imagePath, alt, action }) {
  return `
    <div class="stage-viewport" data-stage-viewport="${action}">
      <div class="stage-content">
        <button class="stage-button" data-stage-action="${action}" type="button">
          <img src="${imagePath}" alt="${alt}">
        </button>
      </div>
    </div>
  `;
}

function renderBasemapMosaicMarkup(tiles) {
  const uniqueX = [...new Set(tiles.map((tile) => tile.x))].sort((a, b) => a - b);
  const columns = uniqueX.length;
  const sortedTiles = [...tiles].sort((a, b) => (a.y - b.y) || (a.x - b.x));
  return `
    <div class="stage-viewport" data-stage-viewport="basemap">
      <div class="stage-content">
        <div class="mosaic-stage" style="grid-template-columns: repeat(${columns}, minmax(0, 1fr));">
          ${sortedTiles.map((tile) => `
            <div class="mosaic-tile" data-mosaic-tile="${tile.local_path}">
              <button class="mosaic-tile-button" data-stage-action="basemap-tile" data-tile-path="${tile.local_path}" type="button">
                <img src="/output/lomond-country-club/${tile.local_path}" alt="GSI tile ${tile.z}-${tile.x}-${tile.y}">
              </button>
            </div>
          `).join("")}
        </div>
      </div>
    </div>
  `;
}

// ===================================================================
//  MODE SYSTEM
// ===================================================================

function switchMode(mode) {
  state.editorMode = mode;
  state.pendingControlPoint = null;
  stackedStageEl.dataset.mode = mode;
  for (const btn of document.querySelectorAll(".mode-btn")) {
    btn.classList.toggle("is-active", btn.dataset.mode === mode);
  }
  // Pointer-events on official layer
  if (mode === "align") {
    layerOfficialEl.style.pointerEvents = "none";
  } else {
    layerOfficialEl.style.pointerEvents = "auto";
  }
  updateModeUI();
  updateMarkerPositions();
}

function updateModeUI() {
  const mode = state.editorMode;
  const hole = getSelectedHole();

  // Hint text
  if (mode === "align") {
    if (state.visualAlignment.locked) {
      modeHintEl.textContent = "Alignment locked. Unlock to adjust basemap position.";
    } else {
      modeHintEl.textContent = "Scroll to zoom basemap. Middle-drag to pan. Shift+scroll/drag for both layers.";
    }
  } else if (mode === "fine-tune") {
    if (state.pendingControlPoint) {
      modeHintEl.textContent = `Official pt queued (${Math.round(state.pendingControlPoint.x)}px, ${Math.round(state.pendingControlPoint.y)}px) — now click basemap area`;
    } else {
      modeHintEl.textContent = "Click to place control point (alternates official → basemap).";
    }
  } else if (mode === "anchors") {
    modeHintEl.textContent = "Click to place anchor. World coords from basemap tile bounds.";
  }

  // Sub-bar items visibility
  controlPointsListEl.style.display = mode === "fine-tune" ? "flex" : "none";
  anchorsBarEl.hidden = mode !== "anchors";

  // Lock button visibility
  lockAlignmentEl.style.display = mode === "align" ? "" : "none";

  // Control points chips (fine-tune mode)
  if (mode === "fine-tune") {
    const controlPoints = hole?.alignment?.control_points || [];
    controlPointsListEl.innerHTML = controlPoints.length
      ? controlPoints.map((point, index) => `
          <span class="cp-chip" title="Official: ${Math.round(point.official.x)}px → Base: ${Math.round(point.basemap.x)}px">
            Pt ${index + 1} <button type="button" data-delete-point="${index}" class="cp-delete">&times;</button>
          </span>
        `).join("")
      : "<span class=\"helper-copy\" style=\"font-size:0.82rem;\">No control points yet</span>";

    for (const deleteBtn of controlPointsListEl.querySelectorAll("[data-delete-point]")) {
      deleteBtn.addEventListener("click", () => {
        const idx = Number(deleteBtn.dataset.deletePoint);
        const cps = [...(hole.alignment?.control_points || [])];
        cps.splice(idx, 1);
        hole.alignment.control_points = cps;
        renderAlignment();
        renderAlignmentEditor();
      });
    }
  }
}

// ===================================================================
//  STACKED VIEWPORT INTERACTIONS
// ===================================================================

function attachStackedInteractions() {
  // Middle-click prevention
  stackedStageEl.onmousedown = (event) => {
    if (event.button === 1) event.preventDefault();
  };
  stackedStageEl.onauxclick = (event) => {
    if (event.button === 1) event.preventDefault();
  };

  // Wheel zoom
  stackedStageEl.onwheel = (event) => {
    event.preventDefault();
    const zoomFactor = event.deltaY < 0 ? 1.12 : 0.9;
    const rect = stackedStageEl.getBoundingClientRect();
    const pointerX = event.clientX - rect.left;
    const pointerY = event.clientY - rect.top;

    if (state.editorMode === "align" && !state.visualAlignment.locked && !event.shiftKey) {
      // Zoom basemap only
      zoomViewportAt("basemap", pointerX, pointerY, zoomFactor);
    } else if (state.editorMode === "align" && !state.visualAlignment.locked && event.shiftKey) {
      // Zoom both
      zoomViewportAt("basemap", pointerX, pointerY, zoomFactor);
      zoomViewportAt("official", pointerX, pointerY, zoomFactor);
    } else {
      // All other modes: zoom both
      zoomViewportAt("basemap", pointerX, pointerY, zoomFactor);
      zoomViewportAt("official", pointerX, pointerY, zoomFactor);
    }
  };

  // Pan (middle-click drag)
  let panState = null;

  stackedStageEl.onpointerdown = (event) => {
    if (event.button !== 1) return;
    event.preventDefault();
    const basemapOnly = state.editorMode === "align" && !state.visualAlignment.locked && !event.shiftKey;
    panState = {
      startX: event.clientX,
      startY: event.clientY,
      originBasemap: { x: state.viewport.basemap.x, y: state.viewport.basemap.y },
      originOfficial: { x: state.viewport.official.x, y: state.viewport.official.y },
      basemapOnly,
      moved: false
    };
    stackedStageEl.classList.add("is-panning");
    stackedStageEl.setPointerCapture(event.pointerId);
  };

  stackedStageEl.onpointermove = (event) => {
    if (!panState) return;
    const dx = event.clientX - panState.startX;
    const dy = event.clientY - panState.startY;
    if (Math.abs(dx) > 3 || Math.abs(dy) > 3) panState.moved = true;

    state.viewport.basemap.x = panState.originBasemap.x + dx;
    state.viewport.basemap.y = panState.originBasemap.y + dy;
    applyViewport("basemap");

    if (!panState.basemapOnly) {
      state.viewport.official.x = panState.originOfficial.x + dx;
      state.viewport.official.y = panState.originOfficial.y + dy;
      applyViewport("official");
    }
    updateMarkerPositions();
  };

  stackedStageEl.onpointerup = (event) => {
    if (!panState) return;
    if (panState.moved) {
      event.preventDefault();
      stackedStageEl.dataset.suppressClick = "true";
    }
    panState = null;
    stackedStageEl.classList.remove("is-panning");
    stackedStageEl.releasePointerCapture?.(event.pointerId);
  };

  stackedStageEl.onpointercancel = () => {
    panState = null;
    stackedStageEl.classList.remove("is-panning");
  };

  // Left-click for fine-tune and anchors modes
  stackedStageEl.addEventListener("click", handleStackedClick);
}

function zoomViewportAt(kind, pointerX, pointerY, zoomFactor) {
  const vp = state.viewport[kind];
  const nextScale = clampScale(vp.scale * zoomFactor);
  const localPoint = screenToLocal(pointerX, pointerY, vp);
  vp.scale = nextScale;
  const newOffset = localToScreenOffset(localPoint.x, localPoint.y, vp);
  vp.x = pointerX - newOffset.x;
  vp.y = pointerY - newOffset.y;
  applyViewport(kind);
}

function handleRotate(delta) {
  const mode = state.editorMode;
  const rect = stackedStageEl.getBoundingClientRect();
  const centerX = rect.width / 2;
  const centerY = rect.height / 2;

  if (mode === "align" && !state.visualAlignment.locked) {
    rotateViewportAroundCenter("basemap", delta, centerX, centerY);
  } else {
    rotateViewportAroundCenter("basemap", delta, centerX, centerY);
    rotateViewportAroundCenter("official", delta, centerX, centerY);
  }
  updateMarkerPositions();
}

function rotateViewportAroundCenter(kind, delta, centerX, centerY) {
  const viewport = state.viewport[kind];
  const localCenter = screenToLocal(centerX, centerY, viewport);
  viewport.rotation += delta;
  const newOffset = localToScreenOffset(localCenter.x, localCenter.y, viewport);
  viewport.x = centerX - newOffset.x;
  viewport.y = centerY - newOffset.y;
  applyViewport(kind);
}

function handleResetView() {
  state.viewport.official = { scale: 1, x: 0, y: 0, rotation: 0 };
  state.viewport.basemap = { scale: 1, x: 0, y: 0, rotation: 0 };
  applyViewport("official");
  applyViewport("basemap");
  updateMarkerPositions();
}

// ===================================================================
//  CLICK HANDLER (fine-tune & anchors)
// ===================================================================

function handleStackedClick(event) {
  if (stackedStageEl.dataset.suppressClick === "true") {
    stackedStageEl.dataset.suppressClick = "false";
    return;
  }

  const mode = state.editorMode;
  if (mode === "align") return;

  const hole = getSelectedHole();
  if (!hole?.alignment) return;

  const rect = stackedStageEl.getBoundingClientRect();
  const screenX = event.clientX - rect.left;
  const screenY = event.clientY - rect.top;

  if (mode === "fine-tune") {
    handleFineTuneClick(hole, screenX, screenY);
  } else if (mode === "anchors") {
    handleAnchorsClick(hole, screenX, screenY);
  }
}

function handleFineTuneClick(hole, screenX, screenY) {
  if (!state.pendingControlPoint) {
    // First click: record official-side coordinates
    const localOfficial = screenToLocal(screenX, screenY, state.viewport.official);
    const buttonEl = layerOfficialEl.querySelector(".stage-button");
    if (!buttonEl) return;
    const bw = buttonEl.offsetWidth;
    const bh = buttonEl.offsetHeight;
    state.pendingControlPoint = {
      x: localOfficial.x,
      y: localOfficial.y,
      normalized_x: clamp01(localOfficial.x / bw),
      normalized_y: clamp01(localOfficial.y / bh)
    };
    updateModeUI();
    updateMarkerPositions();
    return;
  }

  // Second click: record basemap-side coordinates
  const localBasemap = screenToLocal(screenX, screenY, state.viewport.basemap);
  const tileResult = findTileAtMosaicPixel(localBasemap.x, localBasemap.y);
  if (!tileResult) {
    setStatus("Click landed outside the basemap tile grid.", "error");
    return;
  }

  const { tile, fx, fy, tileLocalX, tileLocalY } = tileResult;
  const coords = basemapCoordinatesFromNormalized(fx, fy, tile);

  const controlPoints = [...(hole.alignment?.control_points || [])];
  controlPoints.push({
    id: crypto.randomUUID(),
    official: { ...state.pendingControlPoint },
    basemap: {
      x: tileLocalX,
      y: tileLocalY,
      normalized_x: fx,
      normalized_y: fy,
      local_path: tile.local_path,
      z: tile.z,
      x_tile: tile.x,
      y_tile: tile.y,
      lat: coords?.lat ?? null,
      lon: coords?.lon ?? null
    }
  });

  hole.alignment.control_points = controlPoints;
  state.pendingControlPoint = null;
  renderAlignment();
  renderAlignmentEditor();
}

function handleAnchorsClick(hole, screenX, screenY) {
  // Convert click to basemap mosaic space
  const localBasemap = screenToLocal(screenX, screenY, state.viewport.basemap);
  const tileResult = findTileAtMosaicPixel(localBasemap.x, localBasemap.y);
  if (!tileResult) {
    setStatus("Click landed outside the basemap tile grid.", "error");
    return;
  }

  const { tile, fx, fy } = tileResult;
  const world = basemapCoordinatesFromNormalized(fx, fy, tile);
  if (!world) return;

  // Also get view-space position for record
  const localOfficial = screenToLocal(screenX, screenY, state.viewport.official);

  const anchorType = state.selectedAnchorType;
  const anchorDef = ANCHOR_TYPES.find((a) => a.type === anchorType);

  if (!hole.alignment.anchors) hole.alignment.anchors = [];

  const anchor = {
    type: anchorType,
    label: anchorDef?.label || anchorType,
    view_px: { x: Math.round(localOfficial.x * 100) / 100, y: Math.round(localOfficial.y * 100) / 100 },
    basemap_px: { x: Math.round(localBasemap.x * 100) / 100, y: Math.round(localBasemap.y * 100) / 100 },
    basemap_tile: {
      local_path: tile.local_path,
      z: tile.z,
      x: tile.x,
      y: tile.y
    },
    world: {
      lat: Math.round(world.lat * 1e7) / 1e7,
      lon: Math.round(world.lon * 1e7) / 1e7
    }
  };

  // Replace existing anchor of same type, or add new
  const existing = hole.alignment.anchors.findIndex((a) => a.type === anchorType);
  if (existing >= 0) {
    hole.alignment.anchors[existing] = anchor;
  } else {
    hole.alignment.anchors.push(anchor);
  }

  renderAlignmentEditor();
}

// ===================================================================
//  TILE LOOKUP
// ===================================================================

function getPhotoTiles() {
  return state.basemap?.datasets?.find((d) => d.type === "imagery")?.tiles || [];
}

function findTileAtMosaicPixel(mx, my) {
  const photoTiles = getPhotoTiles();
  if (!photoTiles.length) return null;

  const firstTileBtn = layerBasemapEl.querySelector(".mosaic-tile-button");
  if (!firstTileBtn) return null;
  const tileW = firstTileBtn.offsetWidth;
  if (tileW <= 0) return null;

  const uniqueX = [...new Set(photoTiles.map((t) => t.x))].sort((a, b) => a - b);
  const uniqueY = [...new Set(photoTiles.map((t) => t.y))].sort((a, b) => a - b);

  const colIndex = Math.floor(mx / tileW);
  const rowIndex = Math.floor(my / tileW);

  if (colIndex < 0 || colIndex >= uniqueX.length || rowIndex < 0 || rowIndex >= uniqueY.length) return null;

  const tileX = uniqueX[colIndex];
  const tileY = uniqueY[rowIndex];
  const tile = photoTiles.find((t) => t.x === tileX && t.y === tileY);
  if (!tile) return null;

  const tileLocalX = mx - colIndex * tileW;
  const tileLocalY = my - rowIndex * tileW;
  const fx = clamp01(tileLocalX / tileW);
  const fy = clamp01(tileLocalY / tileW);

  return { tile, fx, fy, tileLocalX, tileLocalY, colIndex, rowIndex };
}

// ===================================================================
//  LOCK / UNLOCK
// ===================================================================

function toggleLock() {
  if (state.visualAlignment.locked) {
    // Unlock
    state.visualAlignment.locked = false;
    lockAlignmentEl.textContent = "Lock";
    lockAlignmentEl.classList.remove("is-locked");
  } else {
    // Lock: capture current basemap transform
    state.visualAlignment.locked = true;
    state.visualAlignment.basemapTransform = {
      translate_x: state.viewport.basemap.x,
      translate_y: state.viewport.basemap.y,
      scale: state.viewport.basemap.scale,
      rotation_deg: state.viewport.basemap.rotation
    };
    lockAlignmentEl.textContent = "Unlock";
    lockAlignmentEl.classList.add("is-locked");
  }
  updateModeUI();
}

function loadHoleVisualAlignment() {
  const hole = getSelectedHole();
  if (hole?.alignment?.visual_alignment?.locked) {
    const saved = hole.alignment.visual_alignment;
    state.visualAlignment.locked = true;
    state.visualAlignment.basemapTransform = { ...saved.basemap_transform };
    state.viewport.basemap = {
      x: saved.basemap_transform.translate_x || 0,
      y: saved.basemap_transform.translate_y || 0,
      scale: saved.basemap_transform.scale || 1,
      rotation: saved.basemap_transform.rotation_deg || 0
    };
    lockAlignmentEl.textContent = "Unlock";
    lockAlignmentEl.classList.add("is-locked");
  } else {
    state.visualAlignment.locked = false;
    state.visualAlignment.basemapTransform = { x: 0, y: 0, scale: 1, rotation: 0 };
    lockAlignmentEl.textContent = "Lock";
    lockAlignmentEl.classList.remove("is-locked");
  }
}

// ===================================================================
//  VIEWPORT APPLICATION
// ===================================================================

function applyViewport(kind) {
  const layerEl = kind === "official" ? layerOfficialEl : layerBasemapEl;
  const viewportEl = layerEl.querySelector(`[data-stage-viewport="${kind}"]`);
  if (!viewportEl) return;
  const viewport = state.viewport[kind];
  viewportEl.style.transform = `translate(${viewport.x}px, ${viewport.y}px) scale(${viewport.scale}) rotate(${viewport.rotation}deg)`;
  try { localStorage.setItem(vpKey(kind), JSON.stringify(viewport)); } catch { /* quota */ }
  updateViewReadout();
}

function vpKey(kind) {
  return `uhole-vp-${state.selectedHoleNumber}-${kind}`;
}

function loadSavedViewport(kind) {
  // Don't override basemap from localStorage if visual alignment is locked (use saved alignment)
  if (kind === "basemap" && state.visualAlignment.locked) {
    applyViewport(kind);
    return;
  }
  try {
    const saved = localStorage.getItem(vpKey(kind));
    if (saved) {
      state.viewport[kind] = { scale: 1, x: 0, y: 0, rotation: 0, ...JSON.parse(saved) };
    } else {
      state.viewport[kind] = { scale: 1, x: 0, y: 0, rotation: 0 };
    }
  } catch {
    state.viewport[kind] = { scale: 1, x: 0, y: 0, rotation: 0 };
  }
  applyViewport(kind);
}

function updateViewReadout() {
  const vpO = state.viewport.official;
  const vpB = state.viewport.basemap;
  viewReadoutEl.textContent = `Official: ${Math.round(vpO.scale * 100)}% ${Math.round(vpO.rotation)}° | Basemap: ${Math.round(vpB.scale * 100)}% ${Math.round(vpB.rotation)}°`;
}

// ===================================================================
//  MARKERS
// ===================================================================

function updateMarkerPositions() {
  layerMarkersEl.innerHTML = "";
  const hole = getSelectedHole();
  if (!hole) return;

  const controlPoints = hole.alignment?.control_points || [];
  const anchors = hole.alignment?.anchors || [];
  const vpOfficial = state.viewport.official;
  const vpBasemap = state.viewport.basemap;

  // Control point markers on official layer
  const officialButtonEl = layerOfficialEl.querySelector(".stage-button");
  if (officialButtonEl) {
    const bw = officialButtonEl.offsetWidth;
    const bh = officialButtonEl.offsetHeight;

    for (let i = 0; i < controlPoints.length; i++) {
      const pt = controlPoints[i];
      placeMarker(layerMarkersEl, pt.official.normalized_x * bw, pt.official.normalized_y * bh, String(i + 1), false, vpOfficial);
    }

    // Pending control point
    if (state.pendingControlPoint) {
      placeMarker(layerMarkersEl,
        state.pendingControlPoint.normalized_x * bw,
        state.pendingControlPoint.normalized_y * bh,
        "?", true, vpOfficial);
    }
  }

  // Control point markers on basemap layer
  const photoTiles = getPhotoTiles();
  const firstTileBtn = layerBasemapEl.querySelector(".mosaic-tile-button");
  if (firstTileBtn && photoTiles.length) {
    const tileW = firstTileBtn.offsetWidth;
    const uniqueX = [...new Set(photoTiles.map((t) => t.x))].sort((a, b) => a - b);
    const uniqueY = [...new Set(photoTiles.map((t) => t.y))].sort((a, b) => a - b);

    for (let i = 0; i < controlPoints.length; i++) {
      const pt = controlPoints[i];
      const tile = photoTiles.find((t) => t.local_path === pt.basemap.local_path);
      if (!tile) continue;
      const colIdx = uniqueX.indexOf(tile.x);
      const rowIdx = uniqueY.indexOf(tile.y);
      const cx = (colIdx + pt.basemap.normalized_x) * tileW;
      const cy = (rowIdx + pt.basemap.normalized_y) * tileW;
      placeMarker(layerMarkersEl, cx, cy, String(i + 1), false, vpBasemap, "is-basemap-marker");
    }
  }

  // Anchor markers — render using basemap_px if available, fallback to view_px on official
  for (const anchor of anchors) {
    if (anchor.basemap_px) {
      placeMarker(layerMarkersEl, anchor.basemap_px.x, anchor.basemap_px.y,
        anchor.label.charAt(0), false, vpBasemap, "is-anchor");
    } else if (anchor.official_px) {
      // Legacy anchors placed on official map coords
      placeMarker(layerMarkersEl, anchor.official_px.x, anchor.official_px.y,
        anchor.label.charAt(0), false, vpOfficial, "is-anchor");
    }
  }
}

function placeMarker(layerEl, contentX, contentY, label, pending, viewport, extraClass = "") {
  const offset = localToScreenOffset(contentX, contentY, viewport);
  const span = document.createElement("span");
  span.className = "stage-marker" + (pending ? " is-pending" : "") + (extraClass ? ` ${extraClass}` : "");
  span.textContent = label;
  span.style.left = (viewport.x + offset.x) + "px";
  span.style.top = (viewport.y + offset.y) + "px";
  layerEl.appendChild(span);
}

// ===================================================================
//  TRANSFORM & ANCHORS UI
// ===================================================================

function updateTransformUI() {
  const hole = getSelectedHole();
  const btn = document.getElementById("compute-transform");
  const resultEl = document.getElementById("transform-result");

  if (!hole) {
    btn.hidden = true;
    resultEl.hidden = true;
    return;
  }

  const alignment = hole.alignment;
  const status = alignment?.status;
  const transform = alignment?.transform;

  btn.hidden = status !== "ready_for_transform";

  if (transform?.pixel_to_world_ready) {
    resultEl.hidden = false;
    resultEl.className = "transform-result is-ready";
    resultEl.textContent = `Transform: ${transform.mean_residual_m}m mean / ${transform.max_residual_m}m max`;
  } else {
    resultEl.hidden = true;
  }

  const exportBtn = document.getElementById("export-hole");
  exportBtn.hidden = status !== "transform_computed";
}

function initAnchorTypeDropdown() {
  const select = document.getElementById("anchor-type-select");
  select.innerHTML = ANCHOR_TYPES.map((a) =>
    `<option value="${a.type}">${a.label}</option>`
  ).join("");
  select.addEventListener("change", () => {
    state.selectedAnchorType = select.value;
  });
}

function updateAnchorsUI() {
  const hole = getSelectedHole();
  const anchors = hole?.alignment?.anchors || [];

  anchorsListEl.innerHTML = anchors.length
    ? anchors.map((anchor, i) =>
        `<span class="anchor-chip">
          ${anchor.label} (${anchor.world.lat.toFixed(5)}, ${anchor.world.lon.toFixed(5)})
          <button class="cp-delete" data-delete-anchor="${i}" type="button" aria-label="Delete anchor">&times;</button>
        </span>`
      ).join("")
    : "";

  for (const deleteBtn of anchorsListEl.querySelectorAll("[data-delete-anchor]")) {
    deleteBtn.addEventListener("click", () => {
      const idx = Number(deleteBtn.dataset.deleteAnchor);
      hole.alignment.anchors.splice(idx, 1);
      renderAlignmentEditor();
    });
  }
}

// ===================================================================
//  COORDINATE READOUT
// ===================================================================

function updateBasemapCoordinateReadout(event, tile) {
  if (!tile?.bounds) {
    resetBasemapCoordinateReadout();
    return;
  }
  const viewport = state.viewport.basemap;
  const rect = stackedStageEl.getBoundingClientRect();
  const localClick = screenToLocal(event.clientX - rect.left, event.clientY - rect.top, viewport);
  const tileResult = findTileAtMosaicPixel(localClick.x, localClick.y);
  if (!tileResult) {
    resetBasemapCoordinateReadout();
    return;
  }
  const coords = basemapCoordinatesFromNormalized(tileResult.fx, tileResult.fy, tileResult.tile);
  if (coords) {
    basemapCoordinateReadoutEl.textContent = `Lat ${coords.lat.toFixed(6)} / Lon ${coords.lon.toFixed(6)}`;
  }
}

function resetBasemapCoordinateReadout() {
  basemapCoordinateReadoutEl.textContent = "Lat -- / Lon --";
}

function basemapCoordinatesFromNormalized(normalizedX, normalizedY, tile) {
  if (!tile?.bounds) return null;
  const lon = tile.bounds.west + (tile.bounds.east - tile.bounds.west) * normalizedX;
  const lat = tile.bounds.north + (tile.bounds.south - tile.bounds.north) * normalizedY;
  return { lat, lon };
}

// ===================================================================
//  SAVE / LOAD
// ===================================================================

async function saveAlignment() {
  const hole = getSelectedHole();
  if (!hole) return;

  const body = {
    holeNumber: hole.hole_number,
    controlPoints: hole.alignment?.control_points || [],
    anchors: hole.alignment?.anchors || [],
    transformReady: false
  };

  // Include visual alignment if locked
  if (state.visualAlignment.locked) {
    body.visualAlignment = {
      locked: true,
      basemap_transform: { ...state.visualAlignment.basemapTransform }
    };
  }

  const response = await fetch("/api/alignment/save", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body)
  });

  const result = await response.json();
  if (!response.ok || !result.ok) {
    setStatus(`Alignment save failed.\n\n${result.message || result.stderr || "Unknown error"}`, "error");
    return;
  }

  await loadData();
  setStatus(result.message, "success");
}

async function computeTransform() {
  const hole = getSelectedHole();
  if (!hole) return;

  const btn = document.getElementById("compute-transform");
  btn.disabled = true;
  btn.textContent = "Computing…";

  try {
    const response = await fetch("/api/alignment/compute-transform", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ holeNumber: hole.hole_number })
    });

    const result = await response.json();
    if (!response.ok || !result.ok) {
      setStatus(`Transform failed: ${result.message || "Unknown error"}`, "error");
      return;
    }

    await loadData();
    setStatus(`Hole ${hole.hole_number}: transform computed — Mean: ${result.meanResidual}m, Max: ${result.maxResidual}m`, "success");
    updateTransformUI();
  } finally {
    btn.disabled = false;
    btn.textContent = "Compute Transform";
  }
}

async function exportHoleForUnity() {
  const hole = getSelectedHole();
  if (!hole) return;

  const btn = document.getElementById("export-hole");
  btn.disabled = true;
  btn.textContent = "Exporting…";

  try {
    const response = await fetch("/api/export/hole", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ holeNumber: hole.hole_number })
    });

    const result = await response.json();
    if (!response.ok || !result.ok) {
      setStatus(`Export failed: ${result.message || "Unknown error"}`, "error");
      return;
    }

    setStatus(`Exported Hole ${hole.hole_number} → ${result.exportDir}`, "success");
  } finally {
    btn.disabled = false;
    btn.textContent = "Export for Unity";
  }
}

// ===================================================================
//  ACTIONS
// ===================================================================

async function runIngest(force = false) {
  setStatus("Refetching the live Lomond site and replacing cached source files...", "info");
  const response = await fetch("/api/ingest/lomond", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ courseId: "lomond-country-club", force })
  });
  const result = await response.json();
  if (!response.ok || !result.ok) {
    setStatus(`Ingestion failed.\n\n${result.stderr || result.stdout || "Unknown error"}`, "error");
    return;
  }
  await loadData();
  setStatus(`Lomond refetch completed.\n\n${result.stdout.trim()}`, "success");
}

async function initAlignment() {
  setStatus("Creating per-hole alignment workspaces...", "info");
  const response = await fetch("/api/alignment/init", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ courseId: "lomond-country-club" })
  });
  const result = await response.json();
  if (!response.ok || !result.ok) {
    setStatus(`Alignment initialization failed.\n\n${result.stderr || result.message || "Unknown error"}`, "error");
    return;
  }
  await loadData();
  setStatus(result.message, "success");
}

async function fetchBasemap(force = false) {
  setStatus("Refetching GSI imagery and DEM tiles for the course area...", "info");
  const response = await fetch("/api/basemap/fetch", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ courseId: "lomond-country-club", force })
  });
  const result = await response.json();
  if (!response.ok || !result.ok) {
    setStatus(`Base-map fetch failed.\n\n${result.stderr || result.stdout || "Unknown error"}`, "error");
    return;
  }
  await loadData();
  setStatus(`GSI refetch completed.\n\n${result.stdout.trim()}`, "success");
}

function activatePanel(panelName) {
  for (const panel of document.querySelectorAll(".panel")) {
    panel.classList.toggle("is-visible", panel.id === `panel-${panelName}`);
  }
  for (const button of document.querySelectorAll(".nav-link")) {
    button.classList.toggle("is-active", button.dataset.panel === panelName);
  }
}

// ===================================================================
//  MATH UTILITIES
// ===================================================================

function screenToLocal(screenX, screenY, viewport) {
  const translatedX = screenX - viewport.x;
  const translatedY = screenY - viewport.y;
  const radians = (-viewport.rotation * Math.PI) / 180;
  const cos = Math.cos(radians);
  const sin = Math.sin(radians);
  const rotatedX = translatedX * cos - translatedY * sin;
  const rotatedY = translatedX * sin + translatedY * cos;
  return {
    x: rotatedX / viewport.scale,
    y: rotatedY / viewport.scale
  };
}

function localToScreenOffset(localX, localY, viewport) {
  const scaledX = localX * viewport.scale;
  const scaledY = localY * viewport.scale;
  const radians = (viewport.rotation * Math.PI) / 180;
  const cos = Math.cos(radians);
  const sin = Math.sin(radians);
  return {
    x: scaledX * cos - scaledY * sin,
    y: scaledX * sin + scaledY * cos
  };
}

function clampScale(value) {
  return Math.max(0.1, Math.min(12, value));
}

function clamp01(value) {
  return Math.max(0, Math.min(1, value));
}

// ===================================================================
//  DOM HELPERS
// ===================================================================

function getSelectedHole() {
  return state.holes.find((hole) => hole.hole_number === state.selectedHoleNumber) || null;
}

function scrollSelectedHoleCardIntoView() {
  const selectedCard = alignmentGridEl.querySelector(`[data-hole-card="${state.selectedHoleNumber}"]`);
  if (selectedCard) {
    selectedCard.scrollIntoView({ behavior: "smooth", block: "nearest", inline: "nearest" });
  }
}

function setStatus(message, tone) {
  statusBannerEl.hidden = false;
  statusBannerEl.textContent = message;
  statusBannerEl.style.background = tone === "error"
    ? "rgba(144, 52, 52, 0.18)"
    : tone === "success"
      ? "rgba(58, 122, 90, 0.18)"
      : "rgba(8, 14, 22, 0.86)";
  statusBannerEl.style.borderColor = tone === "error"
    ? "rgba(255, 124, 124, 0.38)"
    : tone === "success"
      ? "rgba(143, 211, 255, 0.22)"
      : "rgba(171, 197, 224, 0.14)";
}
