const state = {
  course: null,
  provenance: null,
  holes: [],
  basemap: null,
  selectedHoleNumber: 1,
  selectedPhotoTilePath: null,
  pendingOfficialPoint: null,
  viewport: {
    official: { scale: 1, x: 0, y: 0, rotation: 0 },
    basemap: { scale: 1, x: 0, y: 0, rotation: 0 }
  }
};

const statsEl = document.getElementById("stats");
const sourcesListEl = document.getElementById("sources-list");
const holesGridEl = document.getElementById("holes-grid");
const exportPreviewEl = document.getElementById("export-preview");
const alignmentGridEl = document.getElementById("alignment-grid");
const statusBannerEl = document.getElementById("status-banner");
const basemapSummaryEl = document.getElementById("basemap-summary");
const basemapPreviewEl = document.getElementById("basemap-preview");
const editorTitleEl = document.getElementById("editor-title");
const selectedHoleReadoutEl = document.getElementById("selected-hole-readout");
const editorHoleSelectEl = document.getElementById("editor-hole-select");
const editorTileSelectEl = document.getElementById("editor-tile-select");
const pendingPointEl = document.getElementById("pending-point");
const controlPointsListEl = document.getElementById("control-points-list");
const officialMapStageEl = document.getElementById("official-map-stage");
const basemapStageEl = document.getElementById("basemap-stage");
const resetOfficialViewEl = document.getElementById("reset-official-view");
const resetBasemapViewEl = document.getElementById("reset-basemap-view");
const rotateOfficialLeftEl = document.getElementById("rotate-official-left");
const rotateOfficialRightEl = document.getElementById("rotate-official-right");
const rotateBasemapLeftEl = document.getElementById("rotate-basemap-left");
const rotateBasemapRightEl = document.getElementById("rotate-basemap-right");
const officialViewReadoutEl = document.getElementById("official-view-readout");
const basemapViewReadoutEl = document.getElementById("basemap-view-readout");
const basemapCoordinateReadoutEl = document.getElementById("basemap-coordinate-readout");

document.getElementById("refresh-data").addEventListener("click", loadData);
document.getElementById("refetch-ingest").addEventListener("click", () => runIngest(true));
document.getElementById("refetch-basemap").addEventListener("click", () => fetchBasemap(true));
document.getElementById("init-alignment").addEventListener("click", initAlignment);
document.getElementById("save-alignment").addEventListener("click", saveAlignment);
resetOfficialViewEl.addEventListener("click", () => resetViewport("official"));
resetBasemapViewEl.addEventListener("click", () => resetViewport("basemap"));
rotateOfficialLeftEl.addEventListener("click", () => rotateViewport("official", -5));
rotateOfficialRightEl.addEventListener("click", () => rotateViewport("official", 5));
rotateBasemapLeftEl.addEventListener("click", () => rotateViewport("basemap", -5));
rotateBasemapRightEl.addEventListener("click", () => rotateViewport("basemap", 5));

for (const button of document.querySelectorAll(".nav-link")) {
  button.addEventListener("click", () => activatePanel(button.dataset.panel));
}

await loadData();

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
  const selectedHole = getSelectedHole();
  state.selectedPhotoTilePath = selectedHole?.alignment?.selected_photo_tile || selectedHole?.alignment?.target_base_map?.selected_tile || state.selectedPhotoTilePath;
  render();
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
      const sourceUrl = alignment?.official_map?.source_url;
      const selectedClass = hole.hole_number === state.selectedHoleNumber ? "is-selected" : "";
      return `
        <article class="hole-card is-selectable ${selectedClass}" data-hole-card="${hole.hole_number}" role="button" tabindex="0" aria-pressed="${hole.hole_number === state.selectedHoleNumber}">
          <div class="eyebrow">Hole ${hole.hole_number}</div>
          <h4>${status.replaceAll("_", " ")}</h4>
          <p>Control points ${controlPointCount}</p>
          <p><span class="chip">${alignment?.transform?.pixel_to_world_ready ? "transform ready" : "transform pending"}</span></p>
          <p>${sourceUrl ? `<a href="${sourceUrl}" target="_blank" rel="noreferrer">Official map source</a>` : "Official map source not linked yet"}</p>
        </article>
      `;
    })
    .join("");

  for (const card of alignmentGridEl.querySelectorAll("[data-hole-card]")) {
    const selectHole = () => {
      state.selectedHoleNumber = Number(card.dataset.holeCard);
      state.pendingOfficialPoint = null;
      const hole = getSelectedHole();
      state.selectedPhotoTilePath = hole?.alignment?.selected_photo_tile || null;
      resetViewport("official");
      resetViewport("basemap");
      renderAlignment();
      renderAlignmentEditor();
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
    basemapSummaryEl.innerHTML = "<p>No base-map manifest yet. Use Refetch GSI Base Map to populate the local cache and download licensable imagery and DEM tiles.</p>";
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

function renderAlignmentEditor() {
  const hole = getSelectedHole();
  if (!hole) {
    editorTitleEl.textContent = "Select a hole";
    return;
  }

  const photoTiles = state.basemap?.datasets?.find((dataset) => dataset.type === "imagery")?.tiles || [];
  const selectedTile = photoTiles.find((tile) => tile.local_path === state.selectedPhotoTilePath) || photoTiles[0] || null;
  if (selectedTile && !state.selectedPhotoTilePath) {
    state.selectedPhotoTilePath = selectedTile.local_path;
  }

  editorTitleEl.textContent = `Hole ${hole.hole_number} control points`;
  selectedHoleReadoutEl.textContent = `Hole ${hole.hole_number}`;
  editorHoleSelectEl.innerHTML = state.holes
    .map((item) => `<option value="${item.hole_number}" ${item.hole_number === hole.hole_number ? "selected" : ""}>Hole ${item.hole_number}</option>`)
    .join("");
  editorTileSelectEl.innerHTML = photoTiles.length
    ? photoTiles.map((tile) => `<option value="${tile.local_path}" ${tile.local_path === state.selectedPhotoTilePath ? "selected" : ""}>z${tile.z} / ${tile.x} / ${tile.y}</option>`).join("")
    : "<option value=\"\">No GSI tiles loaded yet</option>";
  editorTileSelectEl.disabled = photoTiles.length === 0;

  editorHoleSelectEl.onchange = () => {
    state.selectedHoleNumber = Number(editorHoleSelectEl.value);
    state.pendingOfficialPoint = null;
    const hole = getSelectedHole();
    state.selectedPhotoTilePath = hole?.alignment?.selected_photo_tile || null;
    resetViewport("official");
    resetViewport("basemap");
    renderAlignment();
    renderAlignmentEditor();
    scrollSelectedHoleCardIntoView();
  };

  editorTileSelectEl.onchange = () => {
    state.selectedPhotoTilePath = editorTileSelectEl.value;
    renderAlignmentEditor();
    scrollFocusedBasemapTileIntoView();
  };

  const officialImagePath = `/output/lomond-country-club/${hole.assets.official_references[0]}`;
  officialMapStageEl.innerHTML = renderStageMarkup({
    imagePath: officialImagePath,
    alt: `Official hole ${hole.hole_number} map`,
    markers: [
      ...(hole.alignment?.control_points || []).map((point, index) => ({
        left: `${point.official.normalized_x * 100}%`,
        top: `${point.official.normalized_y * 100}%`,
        label: index + 1,
        pending: false
      })),
      ...(state.pendingOfficialPoint ? [{
        left: `${state.pendingOfficialPoint.normalized_x * 100}%`,
        top: `${state.pendingOfficialPoint.normalized_y * 100}%`,
        label: "?",
        pending: true
      }] : [])
    ],
    action: "official"
  });

  basemapStageEl.innerHTML = photoTiles.length
    ? renderBasemapMosaic({
        tiles: photoTiles,
        focusTilePath: state.selectedPhotoTilePath,
        points: hole.alignment?.control_points || []
      })
    : "<p class=\"helper-copy\">No cached GSI photo tile is available yet. Use Refetch GSI Base Map to populate the local base-map cache.</p>";

  applyViewport("official", officialMapStageEl);
  applyViewport("basemap", basemapStageEl);

  pendingPointEl.textContent = state.pendingOfficialPoint
    ? `Pt queued at ${Math.round(state.pendingOfficialPoint.x)}px, ${Math.round(state.pendingOfficialPoint.y)}px — now click base map`
    : "Click official map first";

  controlPointsListEl.innerHTML = (hole.alignment?.control_points || []).length
    ? hole.alignment.control_points.map((point, index) => `
        <span class="cp-chip" title="Official: ${Math.round(point.official.x)}px, ${Math.round(point.official.y)}px → Base: ${Math.round(point.basemap.x)}px, ${Math.round(point.basemap.y)}px">
          Pt ${index + 1} <button type="button" data-delete-point="${index}" class="cp-delete">×</button>
        </span>
      `).join("")
    : "";

  const officialButton = officialMapStageEl.querySelector("[data-stage-action='official']");
  if (officialButton) {
    officialButton.addEventListener("click", (event) => handleStageClick(event, "official"));
  }
  attachViewportInteractions("official", officialMapStageEl);

  for (const basemapButton of basemapStageEl.querySelectorAll("[data-stage-action='basemap-tile']")) {
    basemapButton.addEventListener("click", (event) => {
      const tile = photoTiles.find((item) => item.local_path === basemapButton.dataset.tilePath);
      handleStageClick(event, "basemap", tile || null);
    });
    basemapButton.addEventListener("mousemove", (event) => {
      const tile = photoTiles.find((item) => item.local_path === basemapButton.dataset.tilePath);
      updateBasemapCoordinateReadout(event, tile || null);
    });
  }
  basemapStageEl.addEventListener("mouseleave", resetBasemapCoordinateReadout);
  attachViewportInteractions("basemap", basemapStageEl);

  for (const deleteButton of controlPointsListEl.querySelectorAll("[data-delete-point]")) {
    deleteButton.addEventListener("click", () => {
      const pointIndex = Number(deleteButton.dataset.deletePoint);
      const controlPoints = [...(hole.alignment?.control_points || [])];
      controlPoints.splice(pointIndex, 1);
      hole.alignment.control_points = controlPoints;
      renderAlignment();
      renderAlignmentEditor();
    });
  }
}

function renderStageMarkup({ imagePath, alt, markers, action }) {
  return `
    <div class="stage-viewport" data-stage-viewport="${action}">
      <div class="stage-content">
        <button class="stage-button" data-stage-action="${action}" type="button">
          <img src="${imagePath}" alt="${alt}">
          ${markers.map((marker) => `
            <span class="stage-marker ${marker.pending ? "is-pending" : ""}" style="left:${marker.left}; top:${marker.top};">${marker.label}</span>
          `).join("")}
        </button>
      </div>
    </div>
  `;
}

function renderBasemapMosaic({ tiles, focusTilePath, points }) {
  const uniqueX = [...new Set(tiles.map((tile) => tile.x))].sort((a, b) => a - b);
  const columns = uniqueX.length;
  const sortedTiles = [...tiles].sort((a, b) => (a.y - b.y) || (a.x - b.x));

  return `
    <div class="stage-viewport" data-stage-viewport="basemap">
      <div class="stage-content">
        <div class="mosaic-stage" style="grid-template-columns: repeat(${columns}, minmax(0, 1fr));">
          ${sortedTiles.map((tile) => {
            const tilePoints = points
              .map((point, index) => ({ point, index }))
              .filter(({ point }) => point.basemap.local_path === tile.local_path);
            return `
              <div class="mosaic-tile ${tile.local_path === focusTilePath ? "is-focus" : ""}" data-mosaic-tile="${tile.local_path}">
                <button class="mosaic-tile-button" data-stage-action="basemap-tile" data-tile-path="${tile.local_path}" type="button">
                  <img src="/output/lomond-country-club/${tile.local_path}" alt="GSI tile ${tile.z}-${tile.x}-${tile.y}">
                  ${tilePoints.map(({ point, index }) => `
                    <span class="stage-marker" style="left:${point.basemap.normalized_x * 100}%; top:${point.basemap.normalized_y * 100}%;">${index + 1}</span>
                  `).join("")}
                </button>
              </div>
            `;
          }).join("")}
        </div>
      </div>
    </div>
  `;
}

function handleStageClick(event, stage, selectedTile = null) {
  const hole = getSelectedHole();
  if (!hole) {
    return;
  }

  const stageEl = event.currentTarget.closest(".image-stage");
  if (stageEl?.dataset.suppressClick === "true") {
    stageEl.dataset.suppressClick = "false";
    return;
  }

  const { x, y, normalized_x, normalized_y } = clickToContentCoords(event, stageEl, stage);

  if (stage === "official") {
    state.pendingOfficialPoint = { x, y, normalized_x, normalized_y };
    renderAlignmentEditor();
    return;
  }

  if (stage === "basemap" && state.pendingOfficialPoint && selectedTile) {
    const coordinates = basemapCoordinatesFromNormalized(normalized_x, normalized_y, selectedTile);
    const controlPoints = [...(hole.alignment?.control_points || [])];
    controlPoints.push({
      id: crypto.randomUUID(),
      official: { ...state.pendingOfficialPoint },
      basemap: {
        x,
        y,
        normalized_x,
        normalized_y,
        local_path: selectedTile.local_path,
        z: selectedTile.z,
        x_tile: selectedTile.x,
        y_tile: selectedTile.y,
        lat: coordinates?.lat ?? null,
        lon: coordinates?.lon ?? null
      }
    });

    hole.alignment.control_points = controlPoints;
    hole.alignment.selected_photo_tile = selectedTile.local_path;
    state.selectedPhotoTilePath = selectedTile.local_path;
    state.pendingOfficialPoint = null;
    renderAlignment();
    renderAlignmentEditor();
  }
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

function activatePanel(panelName) {
  for (const panel of document.querySelectorAll(".panel")) {
    panel.classList.toggle("is-visible", panel.id === `panel-${panelName}`);
  }

  for (const button of document.querySelectorAll(".nav-link")) {
    button.classList.toggle("is-active", button.dataset.panel === panelName);
  }
}

async function runIngest(force = false) {
  setStatus("Refetching the live Lomond site and replacing cached source files...", "info");

  const response = await fetch("/api/ingest/lomond", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
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
    headers: {
      "Content-Type": "application/json"
    },
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
    headers: {
      "Content-Type": "application/json"
    },
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

async function saveAlignment() {
  const hole = getSelectedHole();
  if (!hole) {
    return;
  }

  const response = await fetch("/api/alignment/save", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({
      holeNumber: hole.hole_number,
      selectedPhotoTile: state.selectedPhotoTilePath,
      controlPoints: hole.alignment?.control_points || [],
      transformReady: false
    })
  });

  const result = await response.json();
  if (!response.ok || !result.ok) {
    setStatus(`Alignment save failed.\n\n${result.message || result.stderr || "Unknown error"}`, "error");
    return;
  }

  await loadData();
  setStatus(result.message, "success");
}

function getSelectedHole() {
  return state.holes.find((hole) => hole.hole_number === state.selectedHoleNumber) || null;
}

function clamp01(value) {
  return Math.max(0, Math.min(1, value));
}

function scrollSelectedHoleCardIntoView() {
  const selectedCard = alignmentGridEl.querySelector(`[data-hole-card="${state.selectedHoleNumber}"]`);
  if (!selectedCard) {
    return;
  }

  selectedCard.scrollIntoView({
    behavior: "smooth",
    block: "nearest",
    inline: "nearest"
  });
}

function scrollFocusedBasemapTileIntoView() {
  if (!state.selectedPhotoTilePath) {
    return;
  }

  const focusedTile = basemapStageEl.querySelector(`[data-mosaic-tile="${CSS.escape(state.selectedPhotoTilePath)}"]`);
  if (!focusedTile) {
    return;
  }

  focusedTile.scrollIntoView({
    behavior: "smooth",
    block: "nearest",
    inline: "nearest"
  });
}

function updateBasemapCoordinateReadout(event, tile) {
  if (!tile?.bounds) {
    resetBasemapCoordinateReadout();
    return;
  }

  const { normalized_x, normalized_y } = clickToContentCoords(event, basemapStageEl, "basemap");
  const { lat, lon } = basemapCoordinatesFromNormalized(normalized_x, normalized_y, tile);
  basemapCoordinateReadoutEl.textContent = `Lat ${lat.toFixed(6)} / Lon ${lon.toFixed(6)}`;
}

function resetBasemapCoordinateReadout() {
  basemapCoordinateReadoutEl.textContent = "Lat -- / Lon --";
}

function basemapCoordinatesFromNormalized(normalizedX, normalizedY, tile) {
  if (!tile?.bounds) {
    return null;
  }

  const lon = tile.bounds.west + (tile.bounds.east - tile.bounds.west) * normalizedX;
  const lat = tile.bounds.north + (tile.bounds.south - tile.bounds.north) * normalizedY;
  return { lat, lon };
}

function resetViewport(kind) {
  state.viewport[kind] = { scale: 1, x: 0, y: 0, rotation: 0 };
  const stage = kind === "official" ? officialMapStageEl : basemapStageEl;
  applyViewport(kind, stage);
}

function applyViewport(kind, stageEl) {
  const viewportEl = stageEl.querySelector(`[data-stage-viewport="${kind}"]`);
  if (!viewportEl) {
    return;
  }
  const viewport = state.viewport[kind];
  viewportEl.style.transform = `translate(${viewport.x}px, ${viewport.y}px) scale(${viewport.scale}) rotate(${viewport.rotation}deg)`;
  updateViewReadout(kind);
}

function attachViewportInteractions(kind, stageEl) {
  const viewportEl = stageEl.querySelector(`[data-stage-viewport="${kind}"]`);
  if (!viewportEl) {
    return;
  }

  stageEl.onmousedown = (event) => {
    if (event.button === 1) {
      event.preventDefault();
    }
  };

  stageEl.onauxclick = (event) => {
    if (event.button === 1) {
      event.preventDefault();
    }
  };

  stageEl.onwheel = (event) => {
    event.preventDefault();
    const zoomFactor = event.deltaY < 0 ? 1.12 : 0.9;
    const current = state.viewport[kind];
    const nextScale = clampScale(current.scale * zoomFactor);
    const rect = stageEl.getBoundingClientRect();
    const pointerX = event.clientX - rect.left;
    const pointerY = event.clientY - rect.top;
    const localPoint = screenToLocal(pointerX, pointerY, current);

    current.scale = nextScale;
    const rotatedScaledPoint = localToScreenOffset(localPoint.x, localPoint.y, current);
    current.x = pointerX - rotatedScaledPoint.x;
    current.y = pointerY - rotatedScaledPoint.y;
    applyViewport(kind, stageEl);
  };

  let panState = null;

  stageEl.onpointerdown = (event) => {
    if (event.button !== 1) {
      return;
    }
    event.preventDefault();
    panState = {
      startX: event.clientX,
      startY: event.clientY,
      originX: state.viewport[kind].x,
      originY: state.viewport[kind].y,
      moved: false
    };
    stageEl.classList.add("is-panning");
    stageEl.setPointerCapture(event.pointerId);
  };

  stageEl.onpointermove = (event) => {
    if (!panState) {
      return;
    }
    const dx = event.clientX - panState.startX;
    const dy = event.clientY - panState.startY;
    if (Math.abs(dx) > 3 || Math.abs(dy) > 3) {
      panState.moved = true;
    }
    state.viewport[kind].x = panState.originX + dx;
    state.viewport[kind].y = panState.originY + dy;
    applyViewport(kind, stageEl);
  };

  stageEl.onpointerup = (event) => {
    if (!panState) {
      return;
    }
    if (panState.moved) {
      event.preventDefault();
      stageEl.dataset.suppressClick = "true";
    }
    panState = null;
    stageEl.classList.remove("is-panning");
    stageEl.releasePointerCapture?.(event.pointerId);
  };

  stageEl.onpointercancel = () => {
    panState = null;
    stageEl.classList.remove("is-panning");
  };
}

function clickToContentCoords(event, stageEl, kind) {
  const viewport = state.viewport[kind];
  const stageRect = stageEl.getBoundingClientRect();
  const localClick = screenToLocal(event.clientX - stageRect.left, event.clientY - stageRect.top, viewport);
  const viewportEl = stageEl.querySelector(`[data-stage-viewport="${kind}"]`);
  let contentX = 0;
  let contentY = 0;
  let el = event.currentTarget;
  while (el && el !== viewportEl) {
    contentX += el.offsetLeft;
    contentY += el.offsetTop;
    el = el.offsetParent;
  }
  const elW = event.currentTarget.offsetWidth;
  const elH = event.currentTarget.offsetHeight;
  const x = localClick.x - contentX;
  const y = localClick.y - contentY;
  return { x, y, normalized_x: clamp01(x / elW), normalized_y: clamp01(y / elH) };
}

function clampScale(value) {
  return Math.max(1, Math.min(12, value));
}

function rotateViewport(kind, delta) {
  const stage = kind === "official" ? officialMapStageEl : basemapStageEl;
  const viewport = state.viewport[kind];
  const rect = stage.getBoundingClientRect();
  const centerX = rect.width / 2;
  const centerY = rect.height / 2;
  const localCenter = screenToLocal(centerX, centerY, viewport);
  viewport.rotation += delta;
  const newOffset = localToScreenOffset(localCenter.x, localCenter.y, viewport);
  viewport.x = centerX - newOffset.x;
  viewport.y = centerY - newOffset.y;
  applyViewport(kind, stage);
}

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

function updateViewReadout(kind) {
  const viewport = state.viewport[kind];
  const text = `${Math.round(viewport.scale * 100)}% / ${Math.round(viewport.rotation)}deg`;
  if (kind === "official") {
    officialViewReadoutEl.textContent = text;
  } else {
    basemapViewReadoutEl.textContent = text;
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
