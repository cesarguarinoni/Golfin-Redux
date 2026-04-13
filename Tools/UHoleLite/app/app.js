const COURSE_ID = "lomond-country-club";

let courseData = null;
let currentHole = null;
let currentView = "illustration";
let overlayOpacity = 0.5;
let orientation = { rotation: 0, flipH: false, flipV: false };
let illustrationImg = null;
let zonesImg = null;
let treesImg = null;
let treesMask = null;
let showTrees = true;
let obImg = null;
let obMask = null;
let showOB = true;
let cartPathImg = null;
let cartPathMask = null;
let showCartPath = true;

// Layer system
let activeLayer = "terrain"; // "terrain" | "trees" | "cartpath" | "ob"
let heightmapImg = null;
let drawScale = 1;
let draggingTeeIndex = -1;
let teesModified = new Set();

// Zone painting state
let zoneGrid = null;
let zoneGridW = 0;
let zoneGridH = 0;
let zonePaintDirty = false;
let activeBrushZone = -1;
let brushSize = 10;
let isPainting = false;
let zoneUndoStack = [];
const MAX_UNDO = 30;
let paintMode = "brush"; // "brush" | "fill" | "eraser"

// Zoom & pan state
let zoomLevel = 1;
let panX = 0, panY = 0;
let isPanning = false;
let panStartX = 0, panStartY = 0;
let panStartPanX = 0, panStartPanY = 0;

const canvas = document.getElementById("hole-canvas");
const ctx = canvas.getContext("2d");

const ZONE_COLORS_RGB = [
  [0,   0,   0  ],
  [0,   204, 0  ],
  [128, 255, 64 ],
  [102, 136, 51 ],
  [51,  102, 34 ],
  [26,  51,  16 ],
  [221, 204, 136],
  [51,  102, 204],
  [153, 153, 153],
  [255, 51,  51 ],
  [255, 255, 255],
];

const ZONE_LEGEND = [
  { name: "Background", color: "#000000" },
  { name: "Fairway",    color: "#00CC00" },
  { name: "Green",      color: "#80FF40" },
  { name: "Semi-rough", color: "#668833" },
  { name: "Rough",      color: "#336622" },
  { name: "Trees",      color: "#1A3310" },
  { name: "Bunker",     color: "#DDCC88" },
  { name: "Water",      color: "#3366CC" },
  { name: "Cart path",  color: "#999999" },
  { name: "OB",         color: "#FF3333" },
  { name: "Tee box",    color: "#FFFFFF" },
];

// Layer → zone indices mapping
const LAYER_ZONES = {
  terrain:  [0, 1, 2, 3, 4, 6, 7, 10], // Background, Fairway, Green, Semi-rough, Rough, Bunker, Water, Tee box
  trees:    [5],
  cartpath: [8],
  ob:       [9],
};

// ── Init ────────────────────────────────────────────

async function init() {
  try {
    const res = await fetch("/api/course?id=" + COURSE_ID);
    if (!res.ok) throw new Error("Server returned " + res.status);
    courseData = await res.json();
  } catch (err) {
    document.getElementById("course-name").textContent = "Error: " + err.message;
    return;
  }

  const c = courseData.course;
  document.getElementById("course-name").textContent =
    c.display_name + " (" + c.native_name + ") · " + c.holes.length + " holes";

  buildHoleNav();
  buildZoneLegend();
  setupControls();
  setupCanvasInteraction();
  selectHole(1);
}

function buildHoleNav() {
  const nav = document.getElementById("hole-nav");
  nav.innerHTML = "";
  for (const hole of courseData.holes) {
    const ch = courseData.course.holes.find(h => h.number === hole.number);
    const btn = document.createElement("button");
    btn.className = "nav-link";
    btn.dataset.hole = hole.number;
    btn.innerHTML = "Hole " + hole.number +
      '<span class="par-label">P' + (ch?.par ?? "?") + "</span>";
    btn.addEventListener("click", () => selectHole(hole.number));
    nav.appendChild(btn);
  }
}

function buildZoneLegend() {
  const el = document.getElementById("zone-legend");
  // Build all brush items (will be shown/hidden based on active layer)
  el.innerHTML = ZONE_LEGEND.map((z, i) =>
    '<div class="legend-item" data-zone="' + i + '">' +
    '<span class="legend-swatch" style="background:' + z.color + '"></span>' +
    '<span class="legend-label">' + z.name + '</span>' +
    '</div>'
  ).join("");

  el.addEventListener("click", (e) => {
    const item = e.target.closest(".legend-item");
    if (!item) return;
    const zone = Number(item.dataset.zone);
    if (activeBrushZone === zone) {
      activeBrushZone = -1;
      item.classList.remove("is-active");
    } else {
      el.querySelectorAll(".legend-item").forEach(i => i.classList.remove("is-active"));
      item.classList.add("is-active");
      activeBrushZone = zone;
    }
    updateBrushUI();
  });

  // Build layer bar
  buildLayerBar();
  filterBrushesByLayer();
}

function buildLayerBar() {
  const bar = document.getElementById("layer-bar");
  const layers = [
    { id: "terrain",  label: "Terrain" },
    { id: "trees",    label: "Trees" },
    { id: "cartpath", label: "Cart Path" },
    { id: "ob",       label: "OB" },
  ];

  bar.innerHTML = '<h4 class="toolbar-title">Layers</h4>' +
    '<div class="mode-switcher">' +
    layers.map(l =>
      '<button class="mode-btn layer-btn' + (l.id === activeLayer ? ' is-active' : '') +
      '" data-layer="' + l.id + '">' + l.label + '</button>'
    ).join("") +
    '</div>' +
    '<div class="toolbar-spacer"></div>' +
    '<div class="layer-visibility">' +
    '<button id="btn-toggle-trees" class="is-active-toggle" title="Toggle Trees visibility">Trees</button>' +
    '<button id="btn-toggle-cartpath" class="is-active-toggle" title="Toggle Cart Path visibility">Cart Path</button>' +
    '<button id="btn-toggle-ob" class="is-active-toggle" title="Toggle OB visibility">OB</button>' +
    '</div>';

  bar.querySelectorAll(".layer-btn").forEach(btn => {
    btn.addEventListener("click", () => {
      bar.querySelectorAll(".layer-btn").forEach(b => b.classList.remove("is-active"));
      btn.classList.add("is-active");
      activeLayer = btn.dataset.layer;
      filterBrushesByLayer();
    });
  });

  // Visibility toggles
  document.getElementById("btn-toggle-trees").addEventListener("click", function () {
    showTrees = !showTrees;
    this.classList.toggle("is-active-toggle", showTrees);
    drawCanvas();
  });
  document.getElementById("btn-toggle-cartpath").addEventListener("click", function () {
    showCartPath = !showCartPath;
    this.classList.toggle("is-active-toggle", showCartPath);
    drawCanvas();
  });
  document.getElementById("btn-toggle-ob").addEventListener("click", function () {
    showOB = !showOB;
    this.classList.toggle("is-active-toggle", showOB);
    drawCanvas();
  });
}

function filterBrushesByLayer() {
  const el = document.getElementById("zone-legend");
  const allowedZones = LAYER_ZONES[activeLayer] || [];

  // Show/hide brush items based on layer
  el.querySelectorAll(".legend-item").forEach(item => {
    const zone = Number(item.dataset.zone);
    item.style.display = allowedZones.includes(zone) ? "" : "none";
  });

  // If current brush is not in the active layer, deselect it
  if (activeBrushZone >= 0 && !allowedZones.includes(activeBrushZone)) {
    activeBrushZone = -1;
    el.querySelectorAll(".legend-item").forEach(i => i.classList.remove("is-active"));
  }

  // Auto-select first brush if none active
  if (activeBrushZone < 0 && allowedZones.length > 0) {
    const firstZone = allowedZones[0];
    activeBrushZone = firstZone;
    el.querySelectorAll(".legend-item").forEach(i => {
      i.classList.toggle("is-active", Number(i.dataset.zone) === firstZone);
    });
  }

  // Auto-show the overlay for the active layer
  if (activeLayer === "trees" && !showTrees) {
    showTrees = true;
    document.getElementById("btn-toggle-trees").classList.add("is-active-toggle");
    drawCanvas();
  } else if (activeLayer === "cartpath" && !showCartPath) {
    showCartPath = true;
    document.getElementById("btn-toggle-cartpath").classList.add("is-active-toggle");
    drawCanvas();
  } else if (activeLayer === "ob" && !showOB) {
    showOB = true;
    document.getElementById("btn-toggle-ob").classList.add("is-active-toggle");
    drawCanvas();
  }

  updateBrushUI();
}

function updateBrushUI() {
  const toolbar = document.getElementById("brush-toolbar");
  const label = document.getElementById("active-brush-label");
  if (activeBrushZone >= 0) {
    toolbar.hidden = false;
    label.textContent = ZONE_LEGEND[activeBrushZone].name;
    label.style.background = ZONE_LEGEND[activeBrushZone].color;
    label.style.color = activeBrushZone === 0 || activeBrushZone === 5 || activeBrushZone === 4 ? "#fff" : "#000";
    canvas.classList.toggle("painting", currentView === "both");
  } else {
    toolbar.hidden = true;
    canvas.classList.remove("painting");
  }
  // Show smooth buttons only when the relevant mask is selected
  document.getElementById("btn-smooth-ob").hidden = activeBrushZone !== 9;
  document.getElementById("btn-smooth-cp").hidden = activeBrushZone !== 8;
}

function updateLegendVisibility() {
  const legend = document.getElementById("zone-legend");
  const layerBar = document.getElementById("layer-bar");
  const show = currentView === "both";
  legend.hidden = !show;
  if (layerBar) layerBar.hidden = !show;
  if (!show && activeBrushZone >= 0) {
    activeBrushZone = -1;
    legend.querySelectorAll(".legend-item").forEach(i => i.classList.remove("is-active"));
    updateBrushUI();
  }
  // Show brush toolbar only when zone painting is available
  if (!show) {
    document.getElementById("brush-toolbar").hidden = true;
    canvas.classList.remove("painting");
  } else if (activeBrushZone >= 0) {
    document.getElementById("brush-toolbar").hidden = false;
  }
}

function updateOpacityVisibility() {
  var ctrl = document.querySelector(".opacity-control");
  if (ctrl) ctrl.style.display = currentView === "both" ? "" : "none";
}

// ── Hole Selection ──────────────────────────────────

async function selectHole(n) {
  currentHole = courseData.holes.find(h => h.number === n);
  if (!currentHole) return;

  draggingTeeIndex = -1;
  teesModified = new Set();
  zonePaintDirty = false;
  isPainting = false;
  zoneUndoStack = [];
  zoomLevel = 1;
  panX = 0;
  panY = 0;

  document.querySelectorAll("#hole-nav .nav-link").forEach(b => {
    b.classList.toggle("is-active", Number(b.dataset.hole) === n);
  });

  const ch = courseData.course.holes.find(h => h.number === n);
  document.getElementById("hole-title").textContent =
    "Hole " + n + " — Par " + (ch?.par ?? "?") + " · HDCP " + (ch?.hdcp ?? "?") +
    " · " + (ch?.tees?.back?.yards ?? "?") + "y";

  orientation = currentHole.orientation || { rotation: 0, flipH: false, flipV: false };
  updateOrientationUI();

  const si = document.getElementById("sidebar-info");
  si.innerHTML = '<div class="eyebrow">Hole ' + n + "</div>";
  if (ch?.tees) {
    si.innerHTML +=
      '<p><span class="tee-back">Back ' + ch.tees.back.yards + 'y</span> · ' +
      '<span class="tee-regular">Reg ' + ch.tees.regular.yards + 'y</span> · ' +
      '<span class="tee-front">Front ' + ch.tees.front.yards + 'y</span> · ' +
      '<span class="tee-ladies">Ladies ' + ch.tees.ladies.yards + "y</span></p>";
  }
  if (currentHole.terrainMeta) {
    const tm = currentHole.terrainMeta;
    si.innerHTML += "<p>Terrain: " + tm.terrain_width_m.toFixed(0) + "×" +
      tm.terrain_length_m.toFixed(0) + "m · Elev " + tm.max_elevation_m.toFixed(1) + "m</p>";
    if (tm.hints?.length) si.innerHTML += "<p>Hints: " + tm.hints.join(", ") + "</p>";
  }

  const pad = String(n).padStart(2, "0");
  illustrationImg = await loadImage("/output/" + COURSE_ID + "/holes/" + pad + "/illustration_raw.png");
  zonesImg = currentHole.hasZonesPng
    ? await loadImage("/output/" + COURSE_ID + "/holes/" + pad + "/zones.png")
    : null;
  heightmapImg = await loadImage("/api/heightmap?course=" + COURSE_ID + "&hole=" + n);

  await loadZoneGrid(n);

  updateStats();
  updateLegendVisibility();
  drawCanvas();
}

async function loadZoneGrid(holeNumber) {
  try {
    const res = await fetch("/api/zones-grid?course=" + COURSE_ID + "&hole=" + holeNumber);
    if (!res.ok) { zoneGrid = null; return; }
    const data = await res.json();
    zoneGridW = data.width;
    zoneGridH = data.height;
    const raw = atob(data.grid);
    const len = raw.length;
    zoneGrid = new Uint8Array(len);
    treesMask = new Uint8Array(len);
    obMask = new Uint8Array(len);
    cartPathMask = new Uint8Array(len);

    if (data.terrain_grid) {
      // Separate masks available — terrain is preserved under overlays
      const terrain = atob(data.terrain_grid);
      const trees = data.trees_mask ? atob(data.trees_mask) : null;
      const ob = data.ob_mask ? atob(data.ob_mask) : null;
      const cp = data.cart_path_mask ? atob(data.cart_path_mask) : null;
      for (let i = 0; i < len; i++) {
        zoneGrid[i] = terrain.charCodeAt(i);
        if (trees) treesMask[i] = trees.charCodeAt(i);
        if (ob) obMask[i] = ob.charCodeAt(i);
        if (cp) cartPathMask[i] = cp.charCodeAt(i);
      }
    } else {
      // Legacy merged grid — extract overlays, default terrain to rough
      for (let i = 0; i < len; i++) {
        const v = raw.charCodeAt(i);
        if (v === 9) { obMask[i] = 1; zoneGrid[i] = 4; }
        else if (v === 5) { treesMask[i] = 1; zoneGrid[i] = 4; }
        else if (v === 8) { cartPathMask[i] = 1; zoneGrid[i] = 4; }
        else { zoneGrid[i] = v; }
      }
    }
    regenerateZonesImage();
  } catch {
    zoneGrid = null;
    treesMask = null;
    treesImg = null;
    obMask = null;
    obImg = null;
    cartPathMask = null;
    cartPathImg = null;
  }
}

function loadImage(src) {
  return new Promise(resolve => {
    const img = new Image();
    img.onload = () => resolve(img);
    img.onerror = () => resolve(null);
    img.src = src + (src.includes("?") ? "&" : "?") + "_t=" + Date.now();
  });
}

// ── Canvas ──────────────────────────────────────────

function drawCanvas() {
  if (!illustrationImg) {
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    return;
  }

  const srcW = illustrationImg.width;
  const srcH = illustrationImg.height;
  const isRotated90 = orientation.rotation === 90 || orientation.rotation === 270;
  const drawW = isRotated90 ? srcH : srcW;
  const drawH = isRotated90 ? srcW : srcH;

  const stage = document.getElementById("canvas-stage");
  const stageW = stage.clientWidth;
  const stageH = stage.clientHeight;
  const scale = Math.min(stageW / drawW, stageH / drawH, 2);
  drawScale = scale;

  canvas.width = stageW;
  canvas.height = stageH;

  ctx.clearRect(0, 0, canvas.width, canvas.height);
  ctx.save();
  ctx.translate(canvas.width / 2 + panX, canvas.height / 2 + panY);
  ctx.scale(zoomLevel, zoomLevel);
  ctx.rotate((orientation.rotation * Math.PI) / 180);
  ctx.scale(orientation.flipH ? -1 : 1, orientation.flipV ? -1 : 1);

  const hw = srcW * scale / 2;
  const hh = srcH * scale / 2;

  if (currentView === "illustration" || currentView === "both") {
    ctx.globalAlpha = currentView === "both" ? (1 - overlayOpacity) : 1;
    ctx.drawImage(illustrationImg, -hw, -hh, srcW * scale, srcH * scale);
  }

  if ((currentView === "both") && zonesImg) {
    ctx.globalAlpha = currentView === "both" ? overlayOpacity : 1;
    ctx.drawImage(zonesImg, -hw, -hh, srcW * scale, srcH * scale);
  }

  if (currentView === "heightmap" && heightmapImg) {
    ctx.globalAlpha = 1;
    ctx.drawImage(heightmapImg, -hw, -hh, srcW * scale, srcH * scale);
  }

  // Overlay layers (only in Overlay view)
  if (currentView === "both") {
    if (showTrees && treesImg) {
      ctx.globalAlpha = 1;
      ctx.drawImage(treesImg, -hw, -hh, srcW * scale, srcH * scale);
    }
    if (showCartPath && cartPathImg) {
      ctx.globalAlpha = 1;
      ctx.drawImage(cartPathImg, -hw, -hh, srcW * scale, srcH * scale);
    }
    if (showOB && obImg) {
      ctx.globalAlpha = 1;
      ctx.drawImage(obImg, -hw, -hh, srcW * scale, srcH * scale);
    }
  }

  ctx.globalAlpha = 1;

  // Tee markers
  if (currentHole.tees?.tees) {
    const colors = { tee_back: "#4488ff", tee_regular: "#44cc44", tee_front: "#fff", tee_ladies: "#ff5555" };
    const tees = currentHole.tees.tees;
    for (let i = 0; i < tees.length; i++) {
      const tee = tees[i];
      if (!tee.normalized) continue;
      const px = (tee.normalized.x - 0.5) * srcW * scale;
      const py = (tee.normalized.y - 0.5) * srcH * scale;
      const isDragging = i === draggingTeeIndex;
      const radius = isDragging ? 7 : 5;

      if (isDragging) {
        ctx.beginPath();
        ctx.arc(px, py, 11, 0, Math.PI * 2);
        ctx.strokeStyle = colors[tee.type] || "#ff0";
        ctx.lineWidth = 1.5;
        ctx.globalAlpha = 0.5;
        ctx.stroke();
        ctx.globalAlpha = 1;
      }

      ctx.beginPath();
      ctx.arc(px, py, radius, 0, Math.PI * 2);
      ctx.fillStyle = colors[tee.type] || "#ff0";
      ctx.fill();
      ctx.strokeStyle = "#000";
      ctx.lineWidth = 1.5;
      ctx.stroke();

      if (tee.confidence === "manual") {
        ctx.fillStyle = "#000";
        ctx.font = "bold 7px sans-serif";
        ctx.textAlign = "center";
        ctx.textBaseline = "middle";
        ctx.fillText("M", px, py);
      }
    }
  }

  ctx.restore();
}

// ── Canvas Interaction (tee drag + zone painting) ───

function canvasToNormalized(canvasX, canvasY) {
  if (!illustrationImg) return null;
  const srcW = illustrationImg.width;
  const srcH = illustrationImg.height;

  let x = (canvasX - canvas.width / 2 - panX) / zoomLevel;
  let y = (canvasY - canvas.height / 2 - panY) / zoomLevel;

  const rad = -(orientation.rotation * Math.PI) / 180;
  const cos = Math.cos(rad);
  const sin = Math.sin(rad);
  const rx = x * cos - y * sin;
  const ry = x * sin + y * cos;

  const fx = orientation.flipH ? -rx : rx;
  const fy = orientation.flipV ? -ry : ry;

  const normX = fx / (srcW * drawScale) + 0.5;
  const normY = fy / (srcH * drawScale) + 0.5;

  return { x: normX, y: normY };
}

function hitTestTee(canvasX, canvasY) {
  if (!currentHole?.tees?.tees || !illustrationImg) return -1;
  const srcW = illustrationImg.width;
  const srcH = illustrationImg.height;
  const tees = currentHole.tees.tees;
  const hitRadius = 12;

  for (let i = 0; i < tees.length; i++) {
    const tee = tees[i];
    if (!tee.normalized) continue;

    const imgX = (tee.normalized.x - 0.5) * srcW * drawScale;
    const imgY = (tee.normalized.y - 0.5) * srcH * drawScale;

    const fx = orientation.flipH ? -imgX : imgX;
    const fy = orientation.flipV ? -imgY : imgY;

    const rad = (orientation.rotation * Math.PI) / 180;
    const cos = Math.cos(rad);
    const sin = Math.sin(rad);
    const cx = (fx * cos - fy * sin) * zoomLevel + canvas.width / 2 + panX;
    const cy = (fx * sin + fy * cos) * zoomLevel + canvas.height / 2 + panY;

    const dx = canvasX - cx;
    const dy = canvasY - cy;
    if (dx * dx + dy * dy <= hitRadius * hitRadius) return i;
  }
  return -1;
}

// rAF throttle for paint updates
let _paintRafPending = false;
let _paintDirtyRect = null;

function schedulePaintRedraw(gx, gy) {
  const r = brushSize + 1;
  const rect = {
    x: Math.max(0, gx - r),
    y: Math.max(0, gy - r),
    w: Math.min(zoneGridW, gx + r + 1) - Math.max(0, gx - r),
    h: Math.min(zoneGridH, gy + r + 1) - Math.max(0, gy - r),
  };

  // Merge with pending dirty rect
  if (_paintDirtyRect) {
    const nx = Math.min(_paintDirtyRect.x, rect.x);
    const ny = Math.min(_paintDirtyRect.y, rect.y);
    const nx2 = Math.max(_paintDirtyRect.x + _paintDirtyRect.w, rect.x + rect.w);
    const ny2 = Math.max(_paintDirtyRect.y + _paintDirtyRect.h, rect.y + rect.h);
    _paintDirtyRect = { x: nx, y: ny, w: nx2 - nx, h: ny2 - ny };
  } else {
    _paintDirtyRect = rect;
  }

  if (!_paintRafPending) {
    _paintRafPending = true;
    requestAnimationFrame(() => {
      _paintRafPending = false;
      if (_paintDirtyRect) {
        regenerateZonesImage(_paintDirtyRect);
        _paintDirtyRect = null;
      }
    });
  }
}

function paintZone(normX, normY) {
  if (activeBrushZone < 0 || !zoneGrid) return;
  const gx = Math.round(normX * (zoneGridW - 1));
  const gy = Math.round(normY * (zoneGridH - 1));

  for (let dy = -brushSize; dy <= brushSize; dy++) {
    for (let dx = -brushSize; dx <= brushSize; dx++) {
      if (dx * dx + dy * dy > brushSize * brushSize) continue;
      const px = gx + dx;
      const py = gy + dy;
      if (px < 0 || px >= zoneGridW || py < 0 || py >= zoneGridH) continue;
      const idx = py * zoneGridW + px;
      if (activeBrushZone === 9) { obMask[idx] = 1; }
      else if (activeBrushZone === 5) { treesMask[idx] = 1; }
      else if (activeBrushZone === 8) { cartPathMask[idx] = 1; }
      else { zoneGrid[idx] = activeBrushZone; }
    }
  }

  zonePaintDirty = true;
  schedulePaintRedraw(gx, gy);
}

function eraseZone(normX, normY) {
  if (activeBrushZone < 0 || !zoneGrid) return;
  const gx = Math.round(normX * (zoneGridW - 1));
  const gy = Math.round(normY * (zoneGridH - 1));

  for (let dy = -brushSize; dy <= brushSize; dy++) {
    for (let dx = -brushSize; dx <= brushSize; dx++) {
      if (dx * dx + dy * dy > brushSize * brushSize) continue;
      const px = gx + dx;
      const py = gy + dy;
      if (px < 0 || px >= zoneGridW || py < 0 || py >= zoneGridH) continue;
      const idx = py * zoneGridW + px;
      if (activeBrushZone === 9) { obMask[idx] = 0; }
      else if (activeBrushZone === 5) { treesMask[idx] = 0; }
      else if (activeBrushZone === 8) { cartPathMask[idx] = 0; }
      else if (zoneGrid[idx] === activeBrushZone) { zoneGrid[idx] = 0; }
    }
  }

  zonePaintDirty = true;
  schedulePaintRedraw(gx, gy);
}

/**
 * Match an RGB pixel to the nearest zone color.
 * Tries exact match first, then falls back to closest Euclidean distance
 * (handles anti-aliased edges in the PNG).
 */
function matchZoneColor(r, g, b) {
  let bestZone = 0;
  let bestDist = Infinity;
  for (let z = 0; z < ZONE_COLORS_RGB.length; z++) {
    const [zr, zg, zb] = ZONE_COLORS_RGB[z];
    const dr = r - zr, dg = g - zg, db = b - zb;
    const dist = dr * dr + dg * dg + db * db;
    if (dist === 0) return z; // exact match
    if (dist < bestDist) { bestDist = dist; bestZone = z; }
  }
  return bestZone;
}

function floodFillZone(normX, normY) {
  if (activeBrushZone < 0 || !zoneGrid) return;
  const gx = Math.round(normX * (zoneGridW - 1));
  const gy = Math.round(normY * (zoneGridH - 1));
  if (gx < 0 || gx >= zoneGridW || gy < 0 || gy >= zoneGridH) return;

  // Determine which mask/grid to flood
  var mask = null;
  if (activeBrushZone === 9) mask = obMask;
  else if (activeBrushZone === 5) mask = treesMask;
  else if (activeBrushZone === 8) mask = cartPathMask;

  if (mask) {
    // Overlay flood: fill connected non-masked pixels
    const startIdx = gy * zoneGridW + gx;
    if (mask[startIdx]) return; // already set
    const targetZone = zoneGrid[startIdx];
    const visited = new Uint8Array(zoneGridW * zoneGridH);
    const queue = [startIdx];
    visited[startIdx] = 1;
    while (queue.length > 0) {
      const idx = queue.shift();
      mask[idx] = 1;
      const px = idx % zoneGridW;
      const py = (idx - px) / zoneGridW;
      for (const [dx, dy] of [[1,0],[-1,0],[0,1],[0,-1]]) {
        const nx = px + dx, ny = py + dy;
        if (nx < 0 || nx >= zoneGridW || ny < 0 || ny >= zoneGridH) continue;
        const nIdx = ny * zoneGridW + nx;
        if (visited[nIdx] || mask[nIdx] || zoneGrid[nIdx] !== targetZone) continue;
        visited[nIdx] = 1;
        queue.push(nIdx);
      }
    }
  } else {
    // Regular flood: operates on zoneGrid
    const targetZone = zoneGrid[gy * zoneGridW + gx];
    if (targetZone === activeBrushZone) return;
    const visited = new Uint8Array(zoneGridW * zoneGridH);
    const queue = [gx + gy * zoneGridW];
    visited[queue[0]] = 1;
    while (queue.length > 0) {
      const idx = queue.shift();
      zoneGrid[idx] = activeBrushZone;
      const px = idx % zoneGridW;
      const py = (idx - px) / zoneGridW;
      for (const [dx, dy] of [[1,0],[-1,0],[0,1],[0,-1]]) {
        const nx = px + dx, ny = py + dy;
        if (nx < 0 || nx >= zoneGridW || ny < 0 || ny >= zoneGridH) continue;
        const nIdx = ny * zoneGridW + nx;
        if (visited[nIdx] || zoneGrid[nIdx] !== targetZone) continue;
        visited[nIdx] = 1;
        queue.push(nIdx);
      }
    }
  }

  zonePaintDirty = true;
  regenerateZonesImage();
}

// Persistent layer canvases + ImageData (avoid re-allocation on every stroke)
let _zonesCanvas = null, _zonesCtx = null, _zonesImgData = null;
let _treesCanvas = null, _treesCtx = null, _treesImgData = null;
let _obCanvas = null, _obCtx = null, _obImgData = null;
let _cpCanvas = null, _cpCtx = null, _cpImgData = null;
let _layerW = 0, _layerH = 0;

function invalidateLayerCanvases() {
  _layerW = 0; _layerH = 0;
  _zonesCanvas = null;
}

function ensureLayerCanvases() {
  if (_layerW === zoneGridW && _layerH === zoneGridH && _zonesCanvas) return;
  _layerW = zoneGridW;
  _layerH = zoneGridH;

  _zonesCanvas = document.createElement("canvas");
  _zonesCanvas.width = _layerW; _zonesCanvas.height = _layerH;
  _zonesCtx = _zonesCanvas.getContext("2d");
  _zonesImgData = _zonesCtx.createImageData(_layerW, _layerH);

  _treesCanvas = document.createElement("canvas");
  _treesCanvas.width = _layerW; _treesCanvas.height = _layerH;
  _treesCtx = _treesCanvas.getContext("2d");
  _treesImgData = _treesCtx.createImageData(_layerW, _layerH);

  _obCanvas = document.createElement("canvas");
  _obCanvas.width = _layerW; _obCanvas.height = _layerH;
  _obCtx = _obCanvas.getContext("2d");
  _obImgData = _obCtx.createImageData(_layerW, _layerH);

  _cpCanvas = document.createElement("canvas");
  _cpCanvas.width = _layerW; _cpCanvas.height = _layerH;
  _cpCtx = _cpCanvas.getContext("2d");
  _cpImgData = _cpCtx.createImageData(_layerW, _layerH);
}

/**
 * Update a rectangular region of the layer canvases.
 * If no rect given, updates the entire grid (full rebuild).
 */
function regenerateZonesImage(dirtyRect) {
  if (!zoneGrid) return;
  ensureLayerCanvases();

  const treesC = ZONE_COLORS_RGB[5];
  const obC = ZONE_COLORS_RGB[9];
  const cpC = ZONE_COLORS_RGB[8];

  const x0 = dirtyRect ? Math.max(0, dirtyRect.x) : 0;
  const y0 = dirtyRect ? Math.max(0, dirtyRect.y) : 0;
  const x1 = dirtyRect ? Math.min(zoneGridW, dirtyRect.x + dirtyRect.w) : zoneGridW;
  const y1 = dirtyRect ? Math.min(zoneGridH, dirtyRect.y + dirtyRect.h) : zoneGridH;

  const zd = _zonesImgData.data;
  const td = _treesImgData.data;
  const od = _obImgData.data;
  const cd = _cpImgData.data;

  for (let y = y0; y < y1; y++) {
    for (let x = x0; x < x1; x++) {
      const i = y * zoneGridW + x;
      const i4 = i * 4;

      const c = ZONE_COLORS_RGB[zoneGrid[i]] || [0, 0, 0];
      zd[i4] = c[0]; zd[i4+1] = c[1]; zd[i4+2] = c[2]; zd[i4+3] = 255;

      if (treesMask && treesMask[i]) {
        td[i4] = treesC[0]; td[i4+1] = treesC[1]; td[i4+2] = treesC[2]; td[i4+3] = 255;
      } else {
        td[i4+3] = 0;
      }

      if (obMask && obMask[i]) {
        od[i4] = obC[0]; od[i4+1] = obC[1]; od[i4+2] = obC[2]; od[i4+3] = 255;
      } else {
        od[i4+3] = 0;
      }

      if (cartPathMask && cartPathMask[i]) {
        cd[i4] = cpC[0]; cd[i4+1] = cpC[1]; cd[i4+2] = cpC[2]; cd[i4+3] = 255;
      } else {
        cd[i4+3] = 0;
      }
    }
  }

  if (dirtyRect) {
    const dw = x1 - x0, dh = y1 - y0;
    _zonesCtx.putImageData(_zonesImgData, 0, 0, x0, y0, dw, dh);
    _treesCtx.putImageData(_treesImgData, 0, 0, x0, y0, dw, dh);
    _obCtx.putImageData(_obImgData, 0, 0, x0, y0, dw, dh);
    _cpCtx.putImageData(_cpImgData, 0, 0, x0, y0, dw, dh);
  } else {
    _zonesCtx.putImageData(_zonesImgData, 0, 0);
    _treesCtx.putImageData(_treesImgData, 0, 0);
    _obCtx.putImageData(_obImgData, 0, 0);
    _cpCtx.putImageData(_cpImgData, 0, 0);
  }

  zonesImg = _zonesCanvas;
  treesImg = _treesCanvas;
  obImg = _obCanvas;
  cartPathImg = _cpCanvas;

  drawCanvas();
}

function setupCanvasInteraction() {
  let spaceHeld = false;
  window.addEventListener("keydown", (e) => { if (e.code === "Space") spaceHeld = true; });
  window.addEventListener("keyup",   (e) => { if (e.code === "Space") spaceHeld = false; });

  canvas.addEventListener("wheel", (e) => {
    e.preventDefault();
    const rect = canvas.getBoundingClientRect();
    const mx = e.clientX - rect.left;
    const my = e.clientY - rect.top;

    const oldZoom = zoomLevel;
    const factor = e.deltaY < 0 ? 1.25 : 1 / 1.25;
    zoomLevel = Math.min(20, Math.max(1, zoomLevel * factor));

    const cw2 = canvas.width / 2;
    const ch2 = canvas.height / 2;
    panX = mx - ((mx - cw2 - panX) / oldZoom) * zoomLevel - cw2;
    panY = my - ((my - ch2 - panY) / oldZoom) * zoomLevel - ch2;

    if (zoomLevel <= 1) { panX = 0; panY = 0; }

    drawCanvas();
  }, { passive: false });

  canvas.addEventListener("dblclick", () => {
    zoomLevel = 1;
    panX = 0;
    panY = 0;
    drawCanvas();
  });

  canvas.addEventListener("mousedown", (e) => {
    const rect = canvas.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const y = e.clientY - rect.top;

    if (e.button === 1 || spaceHeld) {
      isPanning = true;
      panStartX = e.clientX;
      panStartY = e.clientY;
      panStartPanX = panX;
      panStartPanY = panY;
      canvas.classList.add("panning");
      e.preventDefault();
      return;
    }

    const teeIdx = hitTestTee(x, y);
    if (teeIdx >= 0) {
      draggingTeeIndex = teeIdx;
      canvas.classList.add("dragging-tee");
      canvas.classList.remove("hovering-tee");
      drawCanvas();
      e.preventDefault();
      return;
    }

    if (activeBrushZone >= 0 && (currentView === "both")) {
      if (zoneGrid) {
        zoneUndoStack.push({ grid: new Uint8Array(zoneGrid), trees: treesMask ? new Uint8Array(treesMask) : null, ob: obMask ? new Uint8Array(obMask) : null, cp: cartPathMask ? new Uint8Array(cartPathMask) : null });
        if (zoneUndoStack.length > MAX_UNDO) zoneUndoStack.shift();
      }
      const norm = canvasToNormalized(x, y);
      if (norm) {
        if (paintMode === "fill") {
          floodFillZone(norm.x, norm.y);
        } else if (paintMode === "eraser") {
          isPainting = true;
          eraseZone(norm.x, norm.y);
        } else {
          isPainting = true;
          paintZone(norm.x, norm.y);
        }
      }
      e.preventDefault();
    }
  });

  canvas.addEventListener("mousemove", (e) => {
    const rect = canvas.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const y = e.clientY - rect.top;

    if (isPanning) {
      panX = panStartPanX + (e.clientX - panStartX);
      panY = panStartPanY + (e.clientY - panStartY);
      drawCanvas();
      return;
    }

    if (draggingTeeIndex >= 0) {
      const norm = canvasToNormalized(x, y);
      if (norm && currentHole?.tees?.tees[draggingTeeIndex]) {
        const tee = currentHole.tees.tees[draggingTeeIndex];
        tee.normalized = { x: parseFloat(norm.x.toFixed(3)), y: parseFloat(norm.y.toFixed(3)) };
        tee.confidence = "manual";
        teesModified.add(draggingTeeIndex);
        drawCanvas();
      }
      return;
    }

    if (isPainting && activeBrushZone >= 0) {
      const norm = canvasToNormalized(x, y);
      if (norm) {
        if (paintMode === "eraser") eraseZone(norm.x, norm.y);
        else paintZone(norm.x, norm.y);
      }
      return;
    }

    const teeIdx = hitTestTee(x, y);
    const hasBrush = activeBrushZone >= 0 && (currentView === "both");
    canvas.classList.toggle("hovering-tee", teeIdx >= 0);
    canvas.classList.toggle("painting", hasBrush && teeIdx < 0);
    updateTeeTooltip(teeIdx, x, y);
  });

  canvas.addEventListener("mouseup", () => {
    if (isPanning) {
      isPanning = false;
      canvas.classList.remove("panning");
      return;
    }
    if (draggingTeeIndex >= 0) {
      draggingTeeIndex = -1;
      canvas.classList.remove("dragging-tee");
      drawCanvas();
    }
    isPainting = false;
  });

  canvas.addEventListener("mouseleave", () => {
    if (isPanning) {
      isPanning = false;
      canvas.classList.remove("panning");
    }
    if (draggingTeeIndex >= 0) {
      draggingTeeIndex = -1;
      canvas.classList.remove("dragging-tee");
      drawCanvas();
    }
    isPainting = false;
    canvas.classList.remove("hovering-tee");
    hideTeeTooltip();
  });
}

function updateTeeTooltip(idx, x, y) {
  let tooltip = document.getElementById("tee-tooltip");
  if (idx < 0) { hideTeeTooltip(); return; }

  const tee = currentHole.tees.tees[idx];
  if (!tooltip) {
    tooltip = document.createElement("div");
    tooltip.id = "tee-tooltip";
    tooltip.className = "tee-tooltip";
    document.getElementById("canvas-stage").appendChild(tooltip);
  }

  const labels = { tee_back: "Back", tee_regular: "Regular", tee_front: "Front", tee_ladies: "Ladies" };
  tooltip.innerHTML =
    "<strong>" + (labels[tee.type] || tee.type) + " Tee</strong> " + (tee.yards || "") + "y<br>" +
    "pos: (" + tee.normalized.x.toFixed(3) + ", " + tee.normalized.y.toFixed(3) + ")" +
    (tee.confidence === "manual" ? ' <span class="chip" style="font-size:0.7rem">manual</span>' : "");

  tooltip.style.left = (x + 15) + "px";
  tooltip.style.top = (y - 10) + "px";
  tooltip.hidden = false;
}

function hideTeeTooltip() {
  const tooltip = document.getElementById("tee-tooltip");
  if (tooltip) tooltip.hidden = true;
}

// ── Controls ────────────────────────────────────────

function setupControls() {
  document.querySelectorAll(".view-btn").forEach(btn => {
    btn.addEventListener("click", () => {
      document.querySelectorAll(".view-btn").forEach(b => b.classList.remove("is-active"));
      btn.classList.add("is-active");
      currentView = btn.dataset.view;
      updateLegendVisibility();
      updateOpacityVisibility();
      drawCanvas();
    });
  });

  // Hide opacity control initially (only shown for Overlay view)
  updateOpacityVisibility();

  document.getElementById("overlay-opacity").addEventListener("input", function () {
    overlayOpacity = this.value / 100;
    drawCanvas();
  });

  // Tree/CartPath/OB toggles are set up in buildLayerBar()

  document.getElementById("btn-save").addEventListener("click", saveAll);
  document.getElementById("btn-save-as").addEventListener("click", saveAsSnapshot);
  document.getElementById("btn-load").addEventListener("click", () => {
    document.getElementById("load-file-input").value = "";
    document.getElementById("load-file-input").click();
  });
  document.getElementById("load-file-input").addEventListener("change", (e) => {
    if (e.target.files.length > 0) loadSnapshot(e.target.files[0]);
  });
  document.getElementById("btn-regen").addEventListener("click", regenHeightmap);

  // ── Export helpers ──────────────────────────────────

  function exportLayerPNG(suffix, renderFn) {
    if (!zoneGrid || !currentHole) return;
    const tmpCanvas = document.createElement("canvas");
    tmpCanvas.width = zoneGridW;
    tmpCanvas.height = zoneGridH;
    const tmpCtx = tmpCanvas.getContext("2d");
    const imgData = tmpCtx.createImageData(zoneGridW, zoneGridH);
    renderFn(imgData.data);
    tmpCtx.putImageData(imgData, 0, 0);
    const url = tmpCanvas.toDataURL("image/png");
    const a = document.createElement("a");
    a.href = url;
    a.download = "hole-" + String(currentHole.number).padStart(2, "0") + "-" + suffix + ".png";
    a.click();
    showBanner("Exported " + suffix + " PNG (" + zoneGridW + "×" + zoneGridH + ")");
  }

  // ─── Smooth OB Mask ────────────────────────────────────────────
  // Vectorize → RDP simplify → Chaikin smooth → rasterize back.

  function smoothOBMask() {
    if (!obMask || !zoneGridW || !zoneGridH) return;

    const w = zoneGridW, h = zoneGridH;

    // Push undo snapshot
    zoneUndoStack.push({
      grid: new Uint8Array(zoneGrid),
      trees: treesMask ? new Uint8Array(treesMask) : null,
      ob: obMask ? new Uint8Array(obMask) : null,
      cp: cartPathMask ? new Uint8Array(cartPathMask) : null
    });
    if (zoneUndoStack.length > MAX_UNDO) zoneUndoStack.shift();

    // OB is typically the background (82%+ of pixels, covering all edges).
    // We smooth the INNER boundary by tracing the non-OB "holes" inside
    // the OB mask, smoothing those contours, then filling everything
    // outside the smoothed holes as OB.

    // Build inverse mask: 1 = non-OB (playable area)
    const invMask = new Uint8Array(w * h);
    for (let i = 0; i < w * h; i++) invMask[i] = obMask[i] ? 0 : 1;

    // Step 1: Trace contours of non-OB regions
    const contours = traceOBContours(invMask, w, h);

    if (contours.length === 0) {
      showBanner("No OB boundary found to smooth");
      return;
    }

    // Step 2+3: Simplify + smooth (two passes — re-trace after first rasterize
    // to feed clean pixel boundaries into the second simplification)
    let currentContours = contours;
    for (let pass = 0; pass < 2; pass++) {
      const smoothed = [];
      for (const contour of currentContours) {
        if (contour.length < 6) continue;
        const simplified = rdpSimplify(contour, 12.0);
        const curved = chaikinSmooth(simplified, 3);
        smoothed.push(curved);
      }

      // Fill everything as OB, then carve out the smoothed non-OB holes
      obMask.fill(1);
      for (const poly of smoothed) {
        rasterizePolygon(poly, obMask, w, h, 0);
      }

      // Re-trace for next pass
      if (pass < 1) {
        for (let i = 0; i < w * h; i++) invMask[i] = obMask[i] ? 0 : 1;
        currentContours = traceOBContours(invMask, w, h);
      }
    }

    // Refresh display
    zonePaintDirty = true;
    regenerateZonesImage();
    drawCanvas();

    const totalPts = smoothed.reduce((s, p) => s + p.length, 0);
    const origPts = contours.reduce((s, p) => s + p.length, 0);
    showBanner(`Smoothed OB: ${contours.length} region(s), `
      + `${origPts} → ${totalPts} pts`);
  }

  function smoothCartPathMask() {
    if (!cartPathMask || !zoneGridW || !zoneGridH) return;

    const w = zoneGridW, h = zoneGridH;
    const ROAD_WIDTH_M = 2.5;

    const tm = currentHole.terrainMeta;
    const mppX = tm ? tm.terrain_width_m / w : 0.25;
    const mppY = tm ? tm.terrain_length_m / h : 0.25;
    const mpp = (mppX + mppY) / 2;
    const halfW = Math.max(2, Math.round((ROAD_WIDTH_M / 2) / mpp));

    // Push undo snapshot
    zoneUndoStack.push({
      grid: new Uint8Array(zoneGrid),
      trees: treesMask ? new Uint8Array(treesMask) : null,
      ob: obMask ? new Uint8Array(obMask) : null,
      cp: cartPathMask ? new Uint8Array(cartPathMask) : null
    });
    if (zoneUndoStack.length > MAX_UNDO) zoneUndoStack.shift();

    // --- Step 1: Find connected regions via flood fill ---
    const visited = new Uint8Array(w * h);
    const regions = [];

    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        if (!cartPathMask[y * w + x] || visited[y * w + x]) continue;
        const region = [];
        const stack = [[x, y]];
        while (stack.length) {
          const [cx, cy] = stack.pop();
          if (cx < 0 || cx >= w || cy < 0 || cy >= h) continue;
          const idx = cy * w + cx;
          if (visited[idx] || !cartPathMask[idx]) continue;
          visited[idx] = 1;
          region.push({ x: cx, y: cy });
          stack.push([cx+1,cy],[cx-1,cy],[cx,cy+1],[cx,cy-1]);
        }
        if (region.length > 20) regions.push(region);
      }
    }

    if (regions.length === 0) {
      showBanner("No cart path regions found");
      return;
    }

    // --- Step 2: For each region, trace contour + extract centerline ---
    cartPathMask.fill(0);
    let totalPaths = 0;

    for (const region of regions) {
      // Build a region mask
      const rMask = new Uint8Array(w * h);
      for (const p of region) rMask[p.y * w + p.x] = 1;

      // Trace outer contour
      const contour = traceOrderedContour(rMask, w, h);
      if (!contour || contour.length < 6) continue;

      // Find the two farthest contour points (path endpoints)
      let maxDist = 0, idxA = 0, idxB = 0;
      for (let i = 0; i < contour.length; i++) {
        for (let j = i + 1; j < contour.length; j++) {
          const dx2 = contour[i].x - contour[j].x;
          const dy2 = contour[i].y - contour[j].y;
          const d = dx2 * dx2 + dy2 * dy2;
          if (d > maxDist) { maxDist = d; idxA = i; idxB = j; }
        }
      }

      // Split contour into two chains at farthest points
      const chain1 = [], chain2 = [];
      for (let i = idxA; i !== idxB; i = (i + 1) % contour.length) {
        chain1.push(contour[i]);
      }
      chain1.push(contour[idxB]);

      for (let i = idxB; i !== idxA; i = (i + 1) % contour.length) {
        chain2.push(contour[i]);
      }
      chain2.push(contour[idxA]);
      chain2.reverse(); // both chains go A→B

      if (chain1.length < 2 || chain2.length < 2) continue;

      // Resample both chains to same number of points
      const numSamples = Math.max(Math.round(Math.sqrt(maxDist) / 2), 20);
      const resampled1 = resamplePolyline(chain1, numSamples);
      const resampled2 = resamplePolyline(chain2, numSamples);

      // Average corresponding points → centerline
      let centerline = [];
      for (let i = 0; i < numSamples; i++) {
        centerline.push({
          x: (resampled1[i].x + resampled2[i].x) / 2,
          y: (resampled1[i].y + resampled2[i].y) / 2
        });
      }

      // Light RDP + Chaikin for final cleanup
      centerline = rdpSimplify(centerline, 3.0);

      // Chaikin open-polyline (2 passes)
      for (let pass = 0; pass < 2; pass++) {
        if (centerline.length < 3) break;
        const sm = [centerline[0]];
        for (let i = 0; i < centerline.length - 1; i++) {
          const p0 = centerline[i], p1 = centerline[i + 1];
          sm.push({ x: p0.x * 0.75 + p1.x * 0.25, y: p0.y * 0.75 + p1.y * 0.25 });
          sm.push({ x: p0.x * 0.25 + p1.x * 0.75, y: p0.y * 0.25 + p1.y * 0.75 });
        }
        sm.push(centerline[centerline.length - 1]);
        centerline = sm;
      }

      // Stamp with circle brush + Bresenham interpolation
      for (let i = 0; i < centerline.length; i++) {
        const ax = Math.round(centerline[i].x), ay = Math.round(centerline[i].y);
        stampCircle(ax, ay, halfW, w, h, cartPathMask);
        if (i < centerline.length - 1) {
          const bx = Math.round(centerline[i+1].x), by = Math.round(centerline[i+1].y);
          const steps = Math.max(Math.abs(bx-ax), Math.abs(by-ay));
          for (let s = 1; s < steps; s++) {
            const t = s / steps;
            stampCircle(Math.round(ax+t*(bx-ax)), Math.round(ay+t*(by-ay)), halfW, w, h, cartPathMask);
          }
        }
      }
      totalPaths++;
    }

    zonePaintDirty = true;
    regenerateZonesImage();
    drawCanvas();
    showBanner(`Smoothed ${totalPaths} cart path(s) (${halfW*2+1}px = ${ROAD_WIDTH_M}m)`);
  }

  // ─── Moore Neighborhood Contour Trace ──────────────────────────

  function traceOBContours(mask, w, h) {
    const result = [];

    // Find border pixels: mask=1 with at least one 4-connected neighbor that is 0
    // (or at the grid edge). Collect them and trace ordered contours.
    const isBorder = new Uint8Array(w * h);
    const borderPixels = [];
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        if (!mask[y * w + x]) continue;
        // Check 4-connected neighbors for non-mask or grid edge
        if (x === 0 || x === w - 1 || y === 0 || y === h - 1 ||
            !mask[y * w + (x - 1)] || !mask[y * w + (x + 1)] ||
            !mask[(y - 1) * w + x] || !mask[(y + 1) * w + x]) {
          isBorder[y * w + x] = 1;
          borderPixels.push({ x, y });
        }
      }
    }

    if (borderPixels.length < 3) return result;

    // Walk border pixels using direction-aware 8-connected neighbor following
    const visited = new Uint8Array(w * h);

    // Find start pixel: topmost, then leftmost border pixel
    let startX = w, startY = h;
    for (const p of borderPixels) {
      if (p.y < startY || (p.y === startY && p.x < startX)) {
        startX = p.x; startY = p.y;
      }
    }

    const dx = [1, 1, 0, -1, -1, -1, 0, 1];
    const dy = [0, 1, 1, 1, 0, -1, -1, -1];

    // Trace each connected border loop
    for (const sp of borderPixels) {
      if (visited[sp.y * w + sp.x]) continue;

      const pts = [];
      let cx = sp.x, cy = sp.y;
      let prevDir = 0;
      visited[cy * w + cx] = 1;
      pts.push({ x: cx, y: cy });

      for (let step = 0; step < borderPixels.length + 10; step++) {
        let bestDir = -1, bestScore = -Infinity;
        for (let i = 0; i < 8; i++) {
          const nx = cx + dx[i], ny = cy + dy[i];
          if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
          if (!isBorder[ny * w + nx] || visited[ny * w + nx]) continue;
          let diff = i - prevDir;
          if (diff > 4) diff -= 8;
          if (diff < -4) diff += 8;
          const score = -Math.abs(diff);
          if (score > bestScore) { bestScore = score; bestDir = i; }
        }
        if (bestDir === -1) break;
        cx = cx + dx[bestDir];
        cy = cy + dy[bestDir];
        visited[cy * w + cx] = 1;
        pts.push({ x: cx, y: cy });
        prevDir = bestDir;
      }

      if (pts.length >= 10) result.push(pts);
    }

    return result;
  }

  // ─── RDP Simplify ──────────────────────────────────────────────

  function rdpSimplify(pts, epsilon) {
    if (pts.length < 3) return pts;
    return _rdpRecurse(pts, 0, pts.length - 1, epsilon);
  }

  function _rdpRecurse(pts, start, end, epsilon) {
    if (end - start < 2) return [pts[start], pts[end]];

    let maxDist = 0, maxIdx = start;
    const a = pts[start], b = pts[end];

    for (let i = start + 1; i < end; i++) {
      const d = _perpDist(pts[i], a, b);
      if (d > maxDist) { maxDist = d; maxIdx = i; }
    }

    if (maxDist > epsilon) {
      const left = _rdpRecurse(pts, start, maxIdx, epsilon);
      const right = _rdpRecurse(pts, maxIdx, end, epsilon);
      return left.concat(right.slice(1));
    } else {
      return [pts[start], pts[end]];
    }
  }

  function _perpDist(p, a, b) {
    const dx = b.x - a.x, dy = b.y - a.y;
    const lenSq = dx * dx + dy * dy;
    if (lenSq < 0.0001) {
      const ex = p.x - a.x, ey = p.y - a.y;
      return Math.sqrt(ex * ex + ey * ey);
    }
    const t = Math.max(0, Math.min(1,
      ((p.x - a.x) * dx + (p.y - a.y) * dy) / lenSq));
    const px = a.x + t * dx, py = a.y + t * dy;
    return Math.sqrt((p.x - px) ** 2 + (p.y - py) ** 2);
  }

  // ─── Chaikin Smooth ────────────────────────────────────────────

  function chaikinSmooth(pts, passes) {
    let current = pts;
    for (let pass = 0; pass < passes; pass++) {
      const n = current.length;
      const next = [];
      for (let i = 0; i < n; i++) {
        const p0 = current[i];
        const p1 = current[(i + 1) % n];
        next.push({
          x: p0.x * 0.75 + p1.x * 0.25,
          y: p0.y * 0.75 + p1.y * 0.25
        });
        next.push({
          x: p0.x * 0.25 + p1.x * 0.75,
          y: p0.y * 0.25 + p1.y * 0.75
        });
      }
      current = next;
    }
    return current;
  }

  // ─── Scanline Polygon Rasterizer ───────────────────────────────

  function rasterizePolygon(poly, mask, w, h, fillValue = 1) {
    if (poly.length < 3) return;

    let minY = h, maxY = 0;
    for (const p of poly) {
      if (p.y < minY) minY = p.y;
      if (p.y > maxY) maxY = p.y;
    }
    minY = Math.max(0, Math.floor(minY));
    maxY = Math.min(h - 1, Math.ceil(maxY));

    const n = poly.length;
    for (let y = minY; y <= maxY; y++) {
      const xIntersections = [];
      for (let i = 0; i < n; i++) {
        const a = poly[i];
        const b = poly[(i + 1) % n];
        if ((a.y <= y && b.y > y) || (b.y <= y && a.y > y)) {
          const t = (y - a.y) / (b.y - a.y);
          xIntersections.push(a.x + t * (b.x - a.x));
        }
      }
      xIntersections.sort((a, b) => a - b);

      for (let i = 0; i < xIntersections.length - 1; i += 2) {
        const x0 = Math.max(0, Math.round(xIntersections[i]));
        const x1 = Math.min(w - 1, Math.round(xIntersections[i + 1]));
        for (let x = x0; x <= x1; x++) {
          mask[y * w + x] = fillValue;
        }
      }
    }
  }

  // ─── Ordered Contour Trace ─────────────────────────────────────

  function traceOrderedContour(mask, w, h) {
    // Find start pixel: topmost, then leftmost border pixel
    let startX = -1, startY = -1;
    outer: for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        if (mask[y * w + x]) {
          if (x === 0 || x === w-1 || y === 0 || y === h-1 ||
              !mask[y*w+(x-1)] || !mask[y*w+(x+1)] ||
              !mask[(y-1)*w+x] || !mask[(y+1)*w+x]) {
            startX = x; startY = y;
            break outer;
          }
        }
      }
    }
    if (startX < 0) return null;

    const dx8 = [1,1,0,-1,-1,-1,0,1], dy8 = [0,1,1,1,0,-1,-1,-1];
    const pts = [{ x: startX, y: startY }];
    const vis = new Uint8Array(w * h);
    vis[startY * w + startX] = 1;
    let cx = startX, cy = startY, prevDir = 0;

    for (let step = 0; step < w * h; step++) {
      let found = false;
      for (let turn = 0; turn < 8; turn++) {
        const dir = ((prevDir - 2 + turn) % 8 + 8) % 8;
        const nx = cx + dx8[dir], ny = cy + dy8[dir];
        if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
        if (!mask[ny*w+nx] || vis[ny*w+nx]) continue;
        let isBorder = (nx===0||nx===w-1||ny===0||ny===h-1);
        if (!isBorder) {
          if (!mask[ny*w+(nx-1)]||!mask[ny*w+(nx+1)]||
              !mask[(ny-1)*w+nx]||!mask[(ny+1)*w+nx]) isBorder = true;
        }
        if (!isBorder) continue;
        vis[ny*w+nx] = 1;
        pts.push({ x: nx, y: ny });
        prevDir = dir; cx = nx; cy = ny;
        found = true;
        break;
      }
      if (!found) break;
    }
    return pts;
  }

  // ─── Resample Polyline ────────────────────────────────────────

  function resamplePolyline(chain, n) {
    if (chain.length < 2 || n < 2) return chain.slice(0, n);
    const arcLen = [0];
    for (let i = 1; i < chain.length; i++) {
      const dx = chain[i].x - chain[i-1].x, dy = chain[i].y - chain[i-1].y;
      arcLen.push(arcLen[i-1] + Math.sqrt(dx*dx + dy*dy));
    }
    const totalLen = arcLen[arcLen.length - 1];
    if (totalLen < 0.001) return Array.from({ length: n }, () => ({ ...chain[0] }));

    const result = [{ ...chain[0] }];
    let ci = 0;
    for (let i = 1; i < n - 1; i++) {
      const target = (i / (n - 1)) * totalLen;
      while (ci < chain.length - 2 && arcLen[ci + 1] < target) ci++;
      const segLen = arcLen[ci + 1] - arcLen[ci];
      const t = segLen > 0.001 ? (target - arcLen[ci]) / segLen : 0;
      result.push({
        x: chain[ci].x + t * (chain[ci+1].x - chain[ci].x),
        y: chain[ci].y + t * (chain[ci+1].y - chain[ci].y)
      });
    }
    result.push({ ...chain[chain.length - 1] });
    return result;
  }

  // ─── Circle Stamp ─────────────────────────────────────────────

  function stampCircle(cx, cy, r, w, h, mask) {
    for (let dy = -r; dy <= r; dy++) {
      for (let dx = -r; dx <= r; dx++) {
        if (dx*dx + dy*dy > r*r) continue;
        const px = cx+dx, py = cy+dy;
        if (px >= 0 && px < w && py >= 0 && py < h)
          mask[py * w + px] = 1;
      }
    }
  }

  function showBanner(msg) {
    const banner = document.getElementById("save-banner");
    banner.textContent = msg;
    setTimeout(() => { banner.textContent = "\u00a0"; }, 3000);
  }

  // Export All — merges all layers into one zone-colored PNG
  document.getElementById("export-all-btn").addEventListener("click", () => {
    exportLayerPNG("zones-all", (data) => {
      for (let i = 0; i < zoneGrid.length; i++) {
        const zone = (obMask && obMask[i]) ? 9
          : (treesMask && treesMask[i]) ? 5
          : (cartPathMask && cartPathMask[i]) ? 8
          : zoneGrid[i];
        const c = ZONE_COLORS_RGB[zone] || [0, 0, 0];
        data[i * 4] = c[0]; data[i * 4 + 1] = c[1]; data[i * 4 + 2] = c[2]; data[i * 4 + 3] = 255;
      }
    });
  });

  // Export Terrain — only the base zoneGrid (no overlay masks)
  document.getElementById("export-terrain-btn").addEventListener("click", () => {
    exportLayerPNG("terrain", (data) => {
      for (let i = 0; i < zoneGrid.length; i++) {
        const c = ZONE_COLORS_RGB[zoneGrid[i]] || [0, 0, 0];
        data[i * 4] = c[0]; data[i * 4 + 1] = c[1]; data[i * 4 + 2] = c[2]; data[i * 4 + 3] = 255;
      }
    });
  });

  // Export Trees — white where trees, black elsewhere
  document.getElementById("export-trees-btn").addEventListener("click", () => {
    exportLayerPNG("trees", (data) => {
      for (let i = 0; i < zoneGrid.length; i++) {
        const on = treesMask && treesMask[i];
        data[i * 4] = on ? 255 : 0; data[i * 4 + 1] = on ? 255 : 0; data[i * 4 + 2] = on ? 255 : 0; data[i * 4 + 3] = 255;
      }
    });
  });

  // Export Cart Path — white where cart path, black elsewhere
  document.getElementById("export-cartpath-btn").addEventListener("click", () => {
    exportLayerPNG("cartpath", (data) => {
      for (let i = 0; i < zoneGrid.length; i++) {
        const on = cartPathMask && cartPathMask[i];
        data[i * 4] = on ? 255 : 0; data[i * 4 + 1] = on ? 255 : 0; data[i * 4 + 2] = on ? 255 : 0; data[i * 4 + 3] = 255;
      }
    });
  });

  // Export OB — white where OB, black elsewhere
  document.getElementById("export-ob-btn").addEventListener("click", () => {
    exportLayerPNG("ob", (data) => {
      for (let i = 0; i < zoneGrid.length; i++) {
        const on = obMask && obMask[i];
        data[i * 4] = on ? 255 : 0; data[i * 4 + 1] = on ? 255 : 0; data[i * 4 + 2] = on ? 255 : 0; data[i * 4 + 3] = 255;
      }
    });
  });

  // ── Import helpers ──────────────────────────────────

  function importLayerPNG(fileInputId, applyFn) {
    const input = document.getElementById(fileInputId);
    input.value = "";
    input.click();
    input.onchange = (e) => {
      const file = e.target.files[0];
      if (!file) return;
      const img = new Image();
      img.onload = () => {
        const tmpCanvas = document.createElement("canvas");
        tmpCanvas.width = img.width;
        tmpCanvas.height = img.height;
        const tmpCtx = tmpCanvas.getContext("2d");
        tmpCtx.drawImage(img, 0, 0);
        const imageData = tmpCtx.getImageData(0, 0, img.width, img.height);

        // Push undo snapshot
        if (zoneGrid) {
          zoneUndoStack.push({ grid: new Uint8Array(zoneGrid), trees: treesMask ? new Uint8Array(treesMask) : null, ob: obMask ? new Uint8Array(obMask) : null, cp: cartPathMask ? new Uint8Array(cartPathMask) : null });
          if (zoneUndoStack.length > MAX_UNDO) zoneUndoStack.shift();
        }

        applyFn(imageData.data, img.width, img.height);

        zonePaintDirty = true;
        regenerateZonesImage();
        showBanner("Imported from " + file.name + " (" + img.width + "×" + img.height + ")");
      };
      img.src = URL.createObjectURL(file);
      e.target.value = "";
    };
  }

  // Import Terrain — replaces base zoneGrid, preserves overlay masks
  document.getElementById("import-terrain-btn").addEventListener("click", () => {
    importLayerPNG("import-terrain-file", (pixels, w, h) => {
      const newGrid = new Uint8Array(w * h);
      for (let i = 0; i < w * h; i++) {
        const zone = matchZoneColor(pixels[i * 4], pixels[i * 4 + 1], pixels[i * 4 + 2]);
        // Only import terrain zones (skip overlay zones 5, 8, 9)
        if (zone !== 5 && zone !== 8 && zone !== 9) newGrid[i] = zone;
      }
      zoneGrid = newGrid;
      // Resize masks if grid dimensions changed
      if (w !== zoneGridW || h !== zoneGridH) {
        treesMask = new Uint8Array(w * h);
        obMask = new Uint8Array(w * h);
        cartPathMask = new Uint8Array(w * h);
      }
      zoneGridW = w;
      zoneGridH = h;
    });
  });

  // Import Trees — opaque pixels become trees, transparent pixels clear trees
  document.getElementById("import-trees-btn").addEventListener("click", () => {
    importLayerPNG("import-trees-file", (pixels, w, h) => {
      if (w !== zoneGridW || h !== zoneGridH) {
        showBanner("Size mismatch: expected " + zoneGridW + "×" + zoneGridH + ", got " + w + "×" + h);
        return;
      }
      for (let i = 0; i < w * h; i++) {
        treesMask[i] = pixels[i * 4 + 3] > 128 ? 1 : 0; // alpha > 50% = tree
      }
    });
  });

  // Import Cart Path — opaque pixels become cart path
  document.getElementById("import-cartpath-btn").addEventListener("click", () => {
    importLayerPNG("import-cartpath-file", (pixels, w, h) => {
      if (w !== zoneGridW || h !== zoneGridH) {
        showBanner("Size mismatch: expected " + zoneGridW + "×" + zoneGridH + ", got " + w + "×" + h);
        return;
      }
      for (let i = 0; i < w * h; i++) {
        cartPathMask[i] = pixels[i * 4 + 3] > 128 ? 1 : 0;
      }
    });
  });

  // Import OB — opaque pixels become OB
  document.getElementById("import-ob-btn").addEventListener("click", () => {
    importLayerPNG("import-ob-file", (pixels, w, h) => {
      if (w !== zoneGridW || h !== zoneGridH) {
        showBanner("Size mismatch: expected " + zoneGridW + "×" + zoneGridH + ", got " + w + "×" + h);
        return;
      }
      for (let i = 0; i < w * h; i++) {
        obMask[i] = pixels[i * 4 + 3] > 128 ? 1 : 0;
      }
    });
  });

  // Brush controls
  document.getElementById("brush-size").addEventListener("input", function () {
    brushSize = Number(this.value);
    document.getElementById("brush-size-label").textContent = brushSize + "px";
  });

  document.querySelectorAll(".paint-mode-btn").forEach(btn => {
    btn.addEventListener("click", () => {
      document.querySelectorAll(".paint-mode-btn").forEach(b => b.classList.remove("is-active"));
      btn.classList.add("is-active");
      paintMode = btn.dataset.paintmode;
      document.getElementById("brush-size-group").style.display = paintMode === "fill" ? "none" : "";
    });
  });

  document.getElementById("btn-clear-brush").addEventListener("click", () => {
    activeBrushZone = -1;
    document.querySelectorAll("#zone-legend .legend-item").forEach(i => i.classList.remove("is-active"));
    updateBrushUI();
  });

  document.getElementById("btn-smooth-ob").addEventListener("click", () => {
    smoothOBMask();
  });

  document.getElementById("btn-smooth-cp").addEventListener("click", () => {
    smoothCartPathMask();
  });

  // Undo (Ctrl+Z)
  window.addEventListener("keydown", (e) => {
    if ((e.ctrlKey || e.metaKey) && e.key === "z" && !e.shiftKey) {
      if (zoneUndoStack.length > 0 && zoneGrid) {
        const snap = zoneUndoStack.pop();
        zoneGrid = snap.grid;
        treesMask = snap.trees;
        obMask = snap.ob;
        cartPathMask = snap.cp;
        zonePaintDirty = true;
        regenerateZonesImage();
        e.preventDefault();
      }
    }
  });

  window.addEventListener("resize", drawCanvas);
}

function updateOrientationUI() {
  // Orientation buttons removed — this is now a no-op kept for compatibility
}

// ── Regen Heightmap ─────────────────────────────────

async function regenHeightmap() {
  if (!currentHole) return;
  const banner = document.getElementById("save-banner");
  const btn = document.getElementById("btn-regen");

  btn.disabled = true;
  btn.textContent = "⟳ Regenerating...";
  banner.textContent = "Regenerating heightmap for Hole " + currentHole.number + "...";
  banner.style.borderColor = "";
  banner.style.background = "";
  banner.style.color = "";

  try {
    const res = await fetch("/api/regen-heightmap", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        courseId: COURSE_ID,
        holeNumber: currentHole.number,
      }),
    });
    const result = await res.json();

    if (result.ok) {
      // Reload the heightmap image (cache-bust with timestamp)
      heightmapImg = await loadImage("/api/heightmap?course=" + COURSE_ID + "&hole=" + currentHole.number);
      drawCanvas();

      banner.textContent = "✓ Heightmap regenerated for Hole " + currentHole.number;
      if (result.terrainOutput) {
        banner.textContent += " — " + result.terrainOutput.split("\n").pop();
      }
    } else {
      banner.textContent = "✗ Regen failed: " + (result.message || "unknown error");
      banner.style.borderColor = "rgba(144,52,52,0.35)";
      banner.style.background = "rgba(144,52,52,0.15)";
      banner.style.color = "#ffb4b4";
    }
  } catch (err) {
    banner.textContent = "✗ Regen failed: " + err.message;
    banner.style.borderColor = "rgba(144,52,52,0.35)";
    banner.style.background = "rgba(144,52,52,0.15)";
    banner.style.color = "#ffb4b4";
  }

  btn.disabled = false;
  btn.textContent = "⟳ Regen Heightmap";
  setTimeout(() => { banner.textContent = "\u00a0"; banner.style.borderColor = ""; banner.style.background = ""; banner.style.color = ""; }, 5000);
}

// ── Save All ────────────────────────────────────────

async function saveAll() {
  if (!currentHole) return;
  const banner = document.getElementById("save-banner");
  banner.style.borderColor = "";
  banner.style.background = "";
  banner.style.color = "";

  const parts = [];

  try {
    await fetch("/api/orientation", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ courseId: COURSE_ID, holeNumber: currentHole.number, orientation }),
    });
    currentHole.orientation = { ...orientation };
    parts.push("orientation");

    if (teesModified.size > 0 && currentHole.tees?.tees) {
      const srcW = currentHole.tees.source_dimensions?.width || illustrationImg?.width || 528;
      const srcH = currentHole.tees.source_dimensions?.height || illustrationImg?.height || 637;

      for (const tee of currentHole.tees.tees) {
        if (tee.normalized) {
          tee.pixel = {
            x: Math.round(tee.normalized.x * srcW),
            y: Math.round(tee.normalized.y * srcH),
          };
        }
        if (tee.confidence === "manual") tee.confidence = "set";
      }

      await fetch("/api/tees", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          courseId: COURSE_ID,
          holeNumber: currentHole.number,
          tees: currentHole.tees.tees,
        }),
      });
      parts.push(teesModified.size + " tee(s)");
      teesModified = new Set();
      drawCanvas();
    }

    if (zonePaintDirty && zoneGrid) {
      // Build merged grid for backward compat + separate overlay masks
      let mergedBin = "";
      let terrainBin = "";
      let treesBin = "";
      let obBin = "";
      let cpBin = "";
      for (let i = 0; i < zoneGrid.length; i++) {
        mergedBin += String.fromCharCode(obMask && obMask[i] ? 9 : treesMask && treesMask[i] ? 5 : cartPathMask && cartPathMask[i] ? 8 : zoneGrid[i]);
        terrainBin += String.fromCharCode(zoneGrid[i]);
        treesBin += String.fromCharCode(treesMask ? treesMask[i] : 0);
        obBin += String.fromCharCode(obMask ? obMask[i] : 0);
        cpBin += String.fromCharCode(cartPathMask ? cartPathMask[i] : 0);
      }

      await fetch("/api/zones", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          courseId: COURSE_ID,
          holeNumber: currentHole.number,
          width: zoneGridW,
          height: zoneGridH,
          grid: btoa(mergedBin),
          terrain_grid: btoa(terrainBin),
          trees_mask: btoa(treesBin),
          ob_mask: btoa(obBin),
          cart_path_mask: btoa(cpBin),
        }),
      });
      parts.push("zones painted");
      zonePaintDirty = false;
    }

    banner.textContent = "Saved: " + parts.join(" + ") + " for Hole " + currentHole.number;
    setTimeout(() => { banner.textContent = "\u00a0"; banner.style.borderColor = ""; banner.style.background = ""; banner.style.color = ""; }, 4000);
  } catch (err) {
    banner.textContent = "Save failed: " + err.message;
    banner.style.borderColor = "rgba(144,52,52,0.35)";
    banner.style.background = "rgba(144,52,52,0.15)";
    banner.style.color = "#ffb4b4";
  }
}

// ── Save As (export snapshot to file) ──────────────

function saveAsSnapshot() {
  if (!currentHole) return;
  const snapshot = {
    courseId: COURSE_ID,
    holeNumber: currentHole.number,
    orientation: { ...orientation },
    tees: currentHole.tees ? JSON.parse(JSON.stringify(currentHole.tees)) : null,
  };

  if (zoneGrid) {
    let mergedBin = "";
    let terrainBin = "";
    let treesBin = "";
    let obBin = "";
    let cpBin = "";
    for (let i = 0; i < zoneGrid.length; i++) {
      mergedBin += String.fromCharCode(obMask && obMask[i] ? 9 : treesMask && treesMask[i] ? 5 : cartPathMask && cartPathMask[i] ? 8 : zoneGrid[i]);
      terrainBin += String.fromCharCode(zoneGrid[i]);
      treesBin += String.fromCharCode(treesMask ? treesMask[i] : 0);
      obBin += String.fromCharCode(obMask ? obMask[i] : 0);
      cpBin += String.fromCharCode(cartPathMask ? cartPathMask[i] : 0);
    }
    snapshot.zones = {
      width: zoneGridW,
      height: zoneGridH,
      grid: btoa(mergedBin),
      terrain_grid: btoa(terrainBin),
      trees_mask: btoa(treesBin),
      ob_mask: btoa(obBin),
      cart_path_mask: btoa(cpBin),
    };
  }

  const blob = new Blob([JSON.stringify(snapshot, null, 2)], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = "hole-" + String(currentHole.number).padStart(2, "0") + "-snapshot.json";
  a.click();
  URL.revokeObjectURL(url);

  const banner = document.getElementById("save-banner");
  banner.textContent = "Snapshot exported for Hole " + currentHole.number;
  setTimeout(() => { banner.textContent = "\u00a0"; }, 3000);
}

// ── Load (import snapshot from file) ───────────────

function loadSnapshot(file) {
  if (!file) return;
  const reader = new FileReader();
  reader.onload = () => {
    try {
      const snapshot = JSON.parse(reader.result);
      const banner = document.getElementById("save-banner");

      if (!currentHole) {
        banner.textContent = "Select a hole before loading a snapshot.";
        banner.style.borderColor = "rgba(144,52,52,0.35)";
        banner.style.background = "rgba(144,52,52,0.15)";
        banner.style.color = "#ffb4b4";
        setTimeout(() => { banner.textContent = "\u00a0"; banner.style.borderColor = ""; banner.style.background = ""; banner.style.color = ""; }, 4000);
        return;
      }

      // Push undo snapshot before applying
      if (zoneGrid) {
        zoneUndoStack.push({ grid: new Uint8Array(zoneGrid), trees: treesMask ? new Uint8Array(treesMask) : null, ob: obMask ? new Uint8Array(obMask) : null, cp: cartPathMask ? new Uint8Array(cartPathMask) : null });
        if (zoneUndoStack.length > MAX_UNDO) zoneUndoStack.shift();
      }

      // Restore orientation
      if (snapshot.orientation) {
        orientation = snapshot.orientation;
        updateOrientationUI();
      }

      // Restore tees
      if (snapshot.tees) {
        currentHole.tees = snapshot.tees;
        teesModified = new Set(snapshot.tees.tees ? snapshot.tees.tees.map((_, i) => i) : []);
      }

      // Restore zones
      if (snapshot.zones) {
        zoneGridW = snapshot.zones.width;
        zoneGridH = snapshot.zones.height;
        const raw = atob(snapshot.zones.grid);
        const len = raw.length;
        zoneGrid = new Uint8Array(len);
        treesMask = new Uint8Array(len);
        obMask = new Uint8Array(len);
        cartPathMask = new Uint8Array(len);

        if (snapshot.zones.terrain_grid) {
          const terrain = atob(snapshot.zones.terrain_grid);
          const trees = snapshot.zones.trees_mask ? atob(snapshot.zones.trees_mask) : null;
          const ob = snapshot.zones.ob_mask ? atob(snapshot.zones.ob_mask) : null;
          const cp = snapshot.zones.cart_path_mask ? atob(snapshot.zones.cart_path_mask) : null;
          for (let i = 0; i < len; i++) {
            zoneGrid[i] = terrain.charCodeAt(i);
            if (trees) treesMask[i] = trees.charCodeAt(i);
            if (ob) obMask[i] = ob.charCodeAt(i);
            if (cp) cartPathMask[i] = cp.charCodeAt(i);
          }
        } else {
          for (let i = 0; i < len; i++) {
            const v = raw.charCodeAt(i);
            if (v === 9) { obMask[i] = 1; zoneGrid[i] = 4; }
            else if (v === 5) { treesMask[i] = 1; zoneGrid[i] = 4; }
            else if (v === 8) { cartPathMask[i] = 1; zoneGrid[i] = 4; }
            else { zoneGrid[i] = v; }
          }
        }
        zonePaintDirty = true;
        regenerateZonesImage();
      }

      drawCanvas();
      updateStats();

      banner.textContent = "Loaded snapshot from " + file.name;
      setTimeout(() => { banner.textContent = "\u00a0"; }, 4000);
    } catch (err) {
      const banner = document.getElementById("save-banner");
      banner.textContent = "Load failed: " + err.message;
      banner.style.borderColor = "rgba(144,52,52,0.35)";
      banner.style.background = "rgba(144,52,52,0.15)";
      banner.style.color = "#ffb4b4";
      setTimeout(() => { banner.textContent = "\u00a0"; banner.style.borderColor = ""; banner.style.background = ""; banner.style.color = ""; }, 5000);
    }
  };
  reader.readAsText(file);
}

// ── Stats Cards ─────────────────────────────────────

function updateStats() {
  const container = document.getElementById("hole-stats");
  container.innerHTML = "";

  if (!currentHole) return;

  const cards = [];

  if (currentHole.tees?.tees) {
    const found = currentHole.tees.tees.filter(t => t.confidence !== "missing").length;
    const manual = currentHole.tees.tees.filter(t => t.confidence === "manual").length;
    const detail = manual > 0 ? manual + " manually placed" : "All auto-detected";
    cards.push({ label: "Tee Markers", value: found + "/4", detail });
  }

  if (currentHole.terrainMeta) {
    const tm = currentHole.terrainMeta;
    cards.push({ label: "Terrain", value: tm.terrain_width_m.toFixed(0) + "×" + tm.terrain_length_m.toFixed(0) + "m",
      detail: "Elev range: " + tm.max_elevation_m.toFixed(1) + "m" });
  }

  if (currentHole.zoneStats) {
    const top3 = Object.entries(currentHole.zoneStats)
      .filter(([, v]) => v.percentage > 3)
      .sort((a, b) => b[1].percentage - a[1].percentage)
      .slice(0, 3)
      .map(([k, v]) => k + " " + v.percentage.toFixed(0) + "%")
      .join(", ");
    cards.push({ label: "Zones", value: Object.keys(currentHole.zoneStats).length + " types", detail: top3 });
  }

  if (currentHole.extractMeta) {
    const em = currentHole.extractMeta;
    cards.push({ label: "Image", value: em.final_dimensions.width + "×" + em.final_dimensions.height,
      detail: "Split at x=" + em.split_column });
  }

  for (const card of cards) {
    const div = document.createElement("div");
    div.className = "stat-card";
    div.innerHTML = '<div class="eyebrow">' + card.label + "</div>" +
      "<strong>" + card.value + "</strong>" +
      "<p>" + card.detail + "</p>";
    container.appendChild(div);
  }
}

// ── Boot ────────────────────────────────────────────

init();
