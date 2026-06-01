// Active course. Resolved from (1) ?course= URL param, (2) last choice saved
// in localStorage, (3) default Lomond. The picker persists the choice and
// reloads, so this is read once at boot.
const DEFAULT_COURSE = "lomond-country-club";
function resolveCourseId() {
  try {
    const fromUrl = new URLSearchParams(location.search).get("course");
    if (fromUrl) { localStorage.setItem("uholegeo.course", fromUrl); return fromUrl; }
    const saved = localStorage.getItem("uholegeo.course");
    if (saved) return saved;
  } catch { /* localStorage unavailable */ }
  return DEFAULT_COURSE;
}
let COURSE_ID = resolveCourseId();

let courseData = null;
let currentHole = null;
let currentView = "satellite";
let overlayOpacity = 0.5;
let satelliteImg = null;
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
let bridges = null;
let showBridges = true;
let hoveredBridgeIdx = -1;

// Layer system
let activeLayer = "terrain";
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
let paintMode = "brush";

// Zoom & pan state
let zoomLevel = 1;
let panX = 0, panY = 0;
let isPanning = false;
let panStartX = 0, panStartY = 0;
let panStartPanX = 0, panStartPanY = 0;

// Canvas rotation (0/90/180/270 degrees, CW). Visual only — the satellite
// PNG stays north-up on disk. Saved per-hole in hole-bounds.json.
let canvasRotation = 0;

function setRotation(deg) {
  canvasRotation = ((deg % 360) + 360) % 360;
  document.querySelectorAll(".rot-btn").forEach(b => {
    b.classList.toggle("is-active", Number(b.dataset.rot) === canvasRotation);
  });
  if (currentHole?.holeBounds) {
    currentHole.holeBounds.canvas_rotation = canvasRotation;
    fetch("/api/hole-bounds?course=" + COURSE_ID + "&hole=" + currentHole.number, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(currentHole.holeBounds),
    });
  }
  drawCanvas();
}

// Leaflet map state
let leafletMap = null;
let boundsRect = null;
let mapInitialized = false;

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

const LAYER_ZONES = {
  terrain:  [0, 1, 2, 3, 4, 6, 7, 10],
  trees:    [5],
  cartpath: [8],
  ob:       [9],
};

// ── Init ────────────────────────────────────────────

async function init() {
  await setupCoursePicker();
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
  setupMapOverlay();
  selectHole(1);
}

// Populate the course picker from /api/courses, mark the active course, and
// reload on change (cleanest way to swap all per-course state). localStorage is
// the source of truth so it survives launching from the .bat with no URL args.
async function setupCoursePicker() {
  const sel = document.getElementById("course-select");
  if (!sel) return;
  try {
    const res = await fetch("/api/courses");
    const data = await res.json();
    const courses = data.courses || [];
    sel.innerHTML = "";
    for (const c of courses) {
      const opt = document.createElement("option");
      opt.value = c.id;
      opt.textContent = c.display_name + (c.holes ? " \u00b7 " + c.holes + "h" : "");
      if (c.id === COURSE_ID) opt.selected = true;
      sel.appendChild(opt);
    }
    // Saved course no longer present? Fall back to the first listed course.
    if (courses.length > 0 && !courses.some(c => c.id === COURSE_ID)) {
      COURSE_ID = courses[0].id;
      sel.value = COURSE_ID;
      try { localStorage.setItem("uholegeo.course", COURSE_ID); } catch {}
    }
  } catch {
    sel.innerHTML = '<option value="' + COURSE_ID + '">' + COURSE_ID + "</option>";
  }
  sel.addEventListener("change", () => {
    const next = sel.value;
    if (!next || next === COURSE_ID) return;
    try { localStorage.setItem("uholegeo.course", next); } catch {}
    // Drop any ?course= param so localStorage stays the single source of truth,
    // then reload for a clean per-course state.
    const u = new URL(location.href);
    u.searchParams.delete("course");
    location.href = u.toString();
  });
}

function buildHoleNav() {
  const nav = document.getElementById("hole-nav");
  nav.innerHTML = "";
  for (const hole of courseData.holes) {
    const ch = courseData.course.holes.find(h => h.number === hole.number);
    const btn = document.createElement("button");
    btn.className = "nav-link";
    btn.dataset.hole = hole.number;
    const hasBounds = hole.hasHoleBounds;
    const bridgeCount = hole.bridges?.bridges?.length || 0;
    btn.innerHTML =
      '<span class="bounds-dot ' + (hasBounds ? 'has-bounds' : 'no-bounds') + '"></span>' +
      "Hole " + hole.number +
      '<span class="par-label">P' + (ch?.par ?? "?") + "</span>" +
      (bridgeCount > 0
        ? '<span class="par-label" style="background:rgba(199,125,255,0.25);color:#c77dff">\uD83C\uDF09 ' + bridgeCount + '</span>'
        : '');
    btn.addEventListener("click", () => selectHole(hole.number));
    nav.appendChild(btn);
  }
}

function buildZoneLegend() {
  const el = document.getElementById("zone-legend");
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
    '<div class="layer-visibility">' +
    '<button id="btn-toggle-trees" class="is-active-toggle" title="Toggle Trees visibility">Trees</button>' +
    '<button id="btn-toggle-cartpath" class="is-active-toggle" title="Toggle Cart Path visibility">Cart Path</button>' +
    '<button id="btn-toggle-ob" class="is-active-toggle" title="Toggle OB visibility">OB</button>' +
    '<button id="btn-toggle-bridges" class="is-active-toggle" title="Toggle Bridges visibility">Bridges</button>' +
    '</div>';

  bar.querySelectorAll(".layer-btn").forEach(btn => {
    btn.addEventListener("click", () => {
      bar.querySelectorAll(".layer-btn").forEach(b => b.classList.remove("is-active"));
      btn.classList.add("is-active");
      activeLayer = btn.dataset.layer;
      filterBrushesByLayer();
    });
  });

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
  document.getElementById("btn-toggle-bridges").addEventListener("click", function () {
    showBridges = !showBridges;
    this.classList.toggle("is-active-toggle", showBridges);
    hoveredBridgeIdx = -1;
    hideBridgeTooltip();
    drawCanvas();
  });
}

function filterBrushesByLayer() {
  const el = document.getElementById("zone-legend");
  const allowedZones = LAYER_ZONES[activeLayer] || [];

  el.querySelectorAll(".legend-item").forEach(item => {
    const zone = Number(item.dataset.zone);
    item.style.display = allowedZones.includes(zone) ? "" : "none";
  });

  if (activeBrushZone >= 0 && !allowedZones.includes(activeBrushZone)) {
    activeBrushZone = -1;
    el.querySelectorAll(".legend-item").forEach(i => i.classList.remove("is-active"));
  }

  if (activeBrushZone < 0 && allowedZones.length > 0) {
    const firstZone = allowedZones[0];
    activeBrushZone = firstZone;
    el.querySelectorAll(".legend-item").forEach(i => {
      i.classList.toggle("is-active", Number(i.dataset.zone) === firstZone);
    });
  }

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
  const _smOb = document.getElementById("btn-smooth-ob");
  if (_smOb) _smOb.hidden = activeBrushZone !== 9;
  const _smCp = document.getElementById("btn-smooth-cp");
  if (_smCp) _smCp.hidden = activeBrushZone !== 8;
  const terrainZones = LAYER_ZONES.terrain || [];
  const _smTer = document.getElementById("btn-smooth-terrain");
  if (_smTer) _smTer.hidden = !terrainZones.includes(activeBrushZone);
}

function updateLegendVisibility() {
  const legend = document.getElementById("zone-legend");
  const show = currentView === "both";
  legend.hidden = !show;
  if (!show && activeBrushZone >= 0) {
    activeBrushZone = -1;
    legend.querySelectorAll(".legend-item").forEach(i => i.classList.remove("is-active"));
    updateBrushUI();
  }
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
  bridges = null;
  hoveredBridgeIdx = -1;

  // Restore per-hole rotation
  canvasRotation = Number(currentHole.holeBounds?.canvas_rotation) || 0;
  document.querySelectorAll(".rot-btn").forEach(b => {
    b.classList.toggle("is-active", Number(b.dataset.rot) === canvasRotation);
  });

  document.querySelectorAll("#hole-nav .nav-link").forEach(b => {
    b.classList.toggle("is-active", Number(b.dataset.hole) === n);
  });

  const ch = courseData.course.holes.find(h => h.number === n);
  document.getElementById("hole-title").textContent =
    "Hole " + n + " — Par " + (ch?.par ?? "?") + " · HDCP " + (ch?.hdcp ?? "?") +
    " · " + (ch?.tees?.back?.yards ?? "?") + "y";

  const si = document.getElementById("sidebar-info");
  si.innerHTML = '<div class="eyebrow">Hole ' + n + "</div>";
  if (ch?.tees) {
    si.innerHTML +=
      '<p><span class="tee-back">Back ' + ch.tees.back.yards + 'y</span> · ' +
      '<span class="tee-regular">Reg ' + ch.tees.regular.yards + 'y</span> · ' +
      '<span class="tee-front">Front ' + ch.tees.front.yards + 'y</span> · ' +
      '<span class="tee-ladies">Ladies ' + ch.tees.ladies.yards + "y</span></p>";
  }
  if (currentHole.holeBounds) {
    const b = currentHole.holeBounds.bounds;
    si.innerHTML += "<p>Bounds: " + b.north.toFixed(4) + "N, " + b.south.toFixed(4) + "S</p>";
    si.innerHTML += "<p>" + b.west.toFixed(4) + "W, " + b.east.toFixed(4) + "E</p>";
    if (currentHole.holeBounds.image_dimensions) {
      const d = currentHole.holeBounds.image_dimensions;
      si.innerHTML += "<p>Image: " + d.width + "x" + d.height + "px</p>";
    }
  } else {
    si.innerHTML += '<p style="color:#ff6666">No bounds set — click "Set Bounds"</p>';
  }
  if (currentHole.terrainMeta) {
    const tm = currentHole.terrainMeta;
    si.innerHTML += "<p>Terrain: " + tm.terrain_width_m.toFixed(0) + "x" +
      tm.terrain_length_m.toFixed(0) + "m</p>";
  }

  const pad = String(n).padStart(2, "0");
  satelliteImg = currentHole.hasSatellite
    ? await loadImage("/api/satellite?course=" + COURSE_ID + "&hole=" + n)
    : null;
  heightmapImg = await loadImage("/api/heightmap?course=" + COURSE_ID + "&hole=" + n);

  await loadZoneGrid(n);
  await loadBridges(n);

  updateStats();
  updateLegendVisibility();
  drawCanvas();
}

async function loadZoneGrid(holeNumber) {
  try {
    const res = await fetch("/api/zones-grid?course=" + COURSE_ID + "&hole=" + holeNumber);
    if (!res.ok) {
      // No zones.json yet — initialize from satellite dimensions if available
      if (currentHole.holeBounds?.image_dimensions) {
        const d = currentHole.holeBounds.image_dimensions;
        zoneGridW = d.width;
        zoneGridH = d.height;
        const len = zoneGridW * zoneGridH;
        zoneGrid = new Uint8Array(len).fill(4); // all rough
        treesMask = new Uint8Array(len);
        obMask = new Uint8Array(len);
        cartPathMask = new Uint8Array(len);
        regenerateZonesImage();
      } else {
        zoneGrid = null;
      }
      return;
    }
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

async function loadBridges(holeNumber) {
  try {
    const res = await fetch(
      "/api/bridges?course=" + COURSE_ID + "&hole=" + holeNumber);
    if (res.ok) {
      const data = await res.json();
      bridges = data.bridges || [];
    } else {
      bridges = [];
    }
  } catch {
    bridges = [];
  }
}

// World meters (Unity frame) → normalized canvas coords [0, 1].
// The satellite image's (0,0) maps to the terrain's (-width/2, -length/2) corner
// (terrain is centered on world origin in HoleGeoImporter).
// Y is inverted because canvas Y=0 is north and Unity +Z is also north.
function worldToNormalized(worldX, worldZ) {
  const tm = currentHole?.terrainMeta;
  if (!tm) return null;
  const tw = tm.terrain_width_m;
  const tl = tm.terrain_length_m;
  const nx = (worldX + tw / 2) / tw;
  const ny = 1 - (worldZ + tl / 2) / tl;
  return { x: nx, y: ny };
}

function loadImage(src) {
  return new Promise(resolve => {
    const img = new Image();
    img.onload = () => resolve(img);
    img.onerror = () => resolve(null);
    img.src = src + (src.includes("?") ? "&" : "?") + "_t=" + Date.now();
  });
}

// ── Place Tees (manual tool) ────────────────────────

function placeTees() {
  if (!currentHole) { alert("Select a hole first."); return; }
  if (!zoneGridW || !zoneGridH) {
    alert("Set bounds and fetch the satellite first.");
    return;
  }

  // Find centroid of painted tee zone (10); fall back to image center.
  let sumX = 0, sumY = 0, count = 0;
  if (zoneGrid) {
    for (let i = 0; i < zoneGrid.length; i++) {
      if (zoneGrid[i] === 10) {
        const y = Math.floor(i / zoneGridW);
        const x = i - y * zoneGridW;
        sumX += x; sumY += y; count++;
      }
    }
  }
  const teeCx = count > 0 ? sumX / count : zoneGridW / 2;
  const teeCy = count > 0 ? sumY / count : zoneGridH / 2;

  // Direction from tee toward painted green (2). Falls back to "north" (up).
  let dirX = 0, dirY = -1;
  let gSumX = 0, gSumY = 0, gCount = 0;
  if (zoneGrid) {
    for (let i = 0; i < zoneGrid.length; i++) {
      if (zoneGrid[i] === 2) {
        const y = Math.floor(i / zoneGridW);
        const x = i - y * zoneGridW;
        gSumX += x; gSumY += y; gCount++;
      }
    }
  }
  if (gCount > 0) {
    const dx = gSumX / gCount - teeCx;
    const dy = gSumY / gCount - teeCy;
    const len = Math.hypot(dx, dy);
    if (len > 0.001) { dirX = dx / len; dirY = dy / len; }
  }

  // Tee data comes from course.json
  const ch = courseData.course.holes.find(h => h.number === currentHole.number);
  const tMeta = ch?.tees || {};
  const fallback = { yards: 0, color: "white" };

  // Step each tee 2m apart along the direction toward green (ladies closest).
  const stepM = 2.0;
  const mppX = currentHole.terrainMeta
    ? currentHole.terrainMeta.terrain_width_m / zoneGridW
    : 0.5;
  const mppY = currentHole.terrainMeta
    ? currentHole.terrainMeta.terrain_length_m / zoneGridH
    : 0.5;
  const stepPxX = stepM / mppX;
  const stepPxY = stepM / mppY;

  const teeOrder = [
    { key: "back",    type: "tee_back",    step: 0 },
    { key: "regular", type: "tee_regular", step: 1 },
    { key: "front",   type: "tee_front",   step: 2 },
    { key: "ladies",  type: "tee_ladies",  step: 3 },
  ];

  const newTees = teeOrder.map(t => {
    const meta = tMeta[t.key] || fallback;
    const px = teeCx + dirX * stepPxX * t.step;
    const py = teeCy + dirY * stepPxY * t.step;
    return {
      type: t.type,
      color: meta.color || "white",
      yards: meta.yards || 0,
      normalized: {
        x: parseFloat((px / (zoneGridW - 1)).toFixed(3)),
        y: parseFloat((py / (zoneGridH - 1)).toFixed(3)),
      },
      confidence: "manual",
    };
  });

  if (!currentHole.tees) currentHole.tees = {};
  currentHole.tees.tees = newTees;
  teesModified = new Set([0, 1, 2, 3]);

  drawCanvas();
  showBannerOutside(count > 0
    ? `Placed 4 tees at painted tee zone centroid (${count} tee pixels). Drag to refine, then Save.`
    : `No tee zone painted — placed 4 tees at image center. Drag to refine, then Save.`);
}

function showBannerOutside(msg) {
  const banner = document.getElementById("save-banner");
  if (banner) {
    banner.textContent = msg;
    setTimeout(() => { banner.textContent = "\u00a0"; }, 4000);
  }
}

// ── Canvas ──────────────────────────────────────────

function drawCanvas() {
  if (!satelliteImg && !zoneGrid) {
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    // Show placeholder text
    const stage = document.getElementById("canvas-stage");
    canvas.width = stage.clientWidth;
    canvas.height = stage.clientHeight;
    ctx.fillStyle = "#96aabc";
    ctx.font = "16px sans-serif";
    ctx.textAlign = "center";
    ctx.fillText('No satellite image — click "Set Bounds" to define hole area', canvas.width / 2, canvas.height / 2);
    return;
  }

  const srcW = satelliteImg ? satelliteImg.width : zoneGridW;
  const srcH = satelliteImg ? satelliteImg.height : zoneGridH;

  const stage = document.getElementById("canvas-stage");
  const stageW = stage.clientWidth;
  const stageH = stage.clientHeight;
  // When rotated 90/270, the image's effective bounding box swaps W/H
  const isSideways = canvasRotation === 90 || canvasRotation === 270;
  const effectiveW = isSideways ? srcH : srcW;
  const effectiveH = isSideways ? srcW : srcH;
  const scale = Math.min(stageW / effectiveW, stageH / effectiveH, 2);
  drawScale = scale;

  canvas.width = stageW;
  canvas.height = stageH;

  ctx.clearRect(0, 0, canvas.width, canvas.height);
  ctx.save();
  ctx.translate(canvas.width / 2 + panX, canvas.height / 2 + panY);
  ctx.scale(zoomLevel, zoomLevel);
  if (canvasRotation !== 0) ctx.rotate(canvasRotation * Math.PI / 180);

  const hw = srcW * scale / 2;
  const hh = srcH * scale / 2;

  if ((currentView === "satellite" || currentView === "both") && satelliteImg) {
    ctx.globalAlpha = currentView === "both" ? (1 - overlayOpacity) : 1;
    ctx.drawImage(satelliteImg, -hw, -hh, srcW * scale, srcH * scale);
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
  if (currentHole?.tees?.tees) {
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

  // Bridge markers — read-only, authored in Unity.
  if (showBridges && bridges && bridges.length > 0) {
    const srcW2 = satelliteImg ? satelliteImg.width : zoneGridW;
    const srcH2 = satelliteImg ? satelliteImg.height : zoneGridH;
    const tm = currentHole?.terrainMeta;
    if (srcW2 && srcH2 && tm) {
      const mppX = tm.terrain_width_m / srcW2;
      const mppY = tm.terrain_length_m / srcH2;

      for (let bi = 0; bi < bridges.length; bi++) {
        const b = bridges[bi];
        const center = worldToNormalized(b.x, b.z);
        const fA = worldToNormalized(b.anchor_forward.x, b.anchor_forward.z);
        const bA = worldToNormalized(b.anchor_backward.x, b.anchor_backward.z);
        if (!center || !fA || !bA) continue;

        const cx2 = (center.x - 0.5) * srcW2 * drawScale;
        const cy2 = (center.y - 0.5) * srcH2 * drawScale;
        const fAx = (fA.x - 0.5) * srcW2 * drawScale;
        const fAy = (fA.y - 0.5) * srcH2 * drawScale;
        const bAx = (bA.x - 0.5) * srcW2 * drawScale;
        const bAy = (bA.y - 0.5) * srcH2 * drawScale;

        const mpp = (mppX + mppY) / 2;
        const lenPx = (b.length_forward_m + b.length_backward_m) / mpp * drawScale;
        const widPx = (b.expected_path_width_m || 2.5) / mpp * drawScale;

        const isHover = bi === hoveredBridgeIdx;
        const stroke = "#c77dff";
        const fill = isHover ? "rgba(199,125,255,0.32)" : "rgba(199,125,255,0.18)";

        ctx.save();
        ctx.translate(cx2, cy2);
        ctx.rotate(b.yaw_deg * Math.PI / 180);
        ctx.fillStyle = fill;
        ctx.strokeStyle = stroke;
        ctx.lineWidth = isHover ? 2.5 : 1.5;
        ctx.beginPath();
        ctx.rect(-widPx / 2, -lenPx / 2, widPx, lenPx);
        ctx.fill();
        ctx.stroke();
        ctx.strokeStyle = "#ffffff";
        ctx.lineWidth = 1.5;
        ctx.beginPath();
        ctx.moveTo(0, 0);
        ctx.lineTo(0, -lenPx / 2 * 0.9);
        ctx.stroke();
        ctx.restore();

        for (const [ax, ay, label] of [[fAx, fAy, "F"], [bAx, bAy, "B"]]) {
          ctx.beginPath();
          ctx.arc(ax, ay, isHover ? 6 : 4, 0, Math.PI * 2);
          ctx.fillStyle = stroke;
          ctx.fill();
          ctx.strokeStyle = "#000";
          ctx.lineWidth = 1.2;
          ctx.stroke();
          if (isHover) {
            ctx.fillStyle = "#000";
            ctx.font = "bold 8px sans-serif";
            ctx.textAlign = "center";
            ctx.textBaseline = "middle";
            ctx.fillText(label, ax, ay);
          }
        }
      }
    }
  }

  ctx.restore();
}

// ── Canvas Interaction ──────────────────────────────

function canvasToNormalized(canvasX, canvasY) {
  const srcW = satelliteImg ? satelliteImg.width : zoneGridW;
  const srcH = satelliteImg ? satelliteImg.height : zoneGridH;
  if (!srcW || !srcH) return null;

  let x = (canvasX - canvas.width / 2 - panX) / zoomLevel;
  let y = (canvasY - canvas.height / 2 - panY) / zoomLevel;

  // Reverse the display rotation (canvasRotation is applied CW in drawCanvas)
  if (canvasRotation !== 0) {
    const rad = -canvasRotation * Math.PI / 180;
    const c = Math.cos(rad), s = Math.sin(rad);
    const rx = x * c - y * s;
    const ry = x * s + y * c;
    x = rx; y = ry;
  }

  const normX = x / (srcW * drawScale) + 0.5;
  const normY = y / (srcH * drawScale) + 0.5;

  return { x: normX, y: normY };
}

function hitTestTee(canvasX, canvasY) {
  if (!currentHole?.tees?.tees) return -1;
  const srcW = satelliteImg ? satelliteImg.width : zoneGridW;
  const srcH = satelliteImg ? satelliteImg.height : zoneGridH;
  if (!srcW || !srcH) return -1;
  const tees = currentHole.tees.tees;
  const hitRadius = 12;

  for (let i = 0; i < tees.length; i++) {
    const tee = tees[i];
    if (!tee.normalized) continue;

    let imgX = (tee.normalized.x - 0.5) * srcW * drawScale;
    let imgY = (tee.normalized.y - 0.5) * srcH * drawScale;

    // Apply same rotation as drawCanvas
    if (canvasRotation !== 0) {
      const rad = canvasRotation * Math.PI / 180;
      const c = Math.cos(rad), s = Math.sin(rad);
      const rx = imgX * c - imgY * s;
      const ry = imgX * s + imgY * c;
      imgX = rx; imgY = ry;
    }

    const cx = imgX * zoomLevel + canvas.width / 2 + panX;
    const cy = imgY * zoomLevel + canvas.height / 2 + panY;

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

function matchZoneColor(r, g, b) {
  let bestZone = 0;
  let bestDist = Infinity;
  for (let z = 0; z < ZONE_COLORS_RGB.length; z++) {
    const [zr, zg, zb] = ZONE_COLORS_RGB[z];
    const dr = r - zr, dg = g - zg, db = b - zb;
    const dist = dr * dr + dg * dg + db * db;
    if (dist === 0) return z;
    if (dist < bestDist) { bestDist = dist; bestZone = z; }
  }
  return bestZone;
}

function floodFillZone(normX, normY) {
  if (activeBrushZone < 0 || !zoneGrid) return;
  const gx = Math.round(normX * (zoneGridW - 1));
  const gy = Math.round(normY * (zoneGridH - 1));
  if (gx < 0 || gx >= zoneGridW || gy < 0 || gy >= zoneGridH) return;

  var mask = null;
  if (activeBrushZone === 9) mask = obMask;
  else if (activeBrushZone === 5) mask = treesMask;
  else if (activeBrushZone === 8) mask = cartPathMask;

  if (mask) {
    const startIdx = gy * zoneGridW + gx;
    if (mask[startIdx]) return;
    const targetZone = zoneGrid[startIdx];
    const visited = new Uint8Array(zoneGridW * zoneGridH);
    const queue = [startIdx];
    visited[startIdx] = 1;
    while (queue.length > 0) {
      const idx = queue.shift();
      mask[idx] = 1;
      const px = idx % zoneGridW;
      const py = (idx - px) / zoneGridW;
      for (const [ddx, ddy] of [[1,0],[-1,0],[0,1],[0,-1]]) {
        const nx = px + ddx, ny = py + ddy;
        if (nx < 0 || nx >= zoneGridW || ny < 0 || ny >= zoneGridH) continue;
        const nIdx = ny * zoneGridW + nx;
        if (visited[nIdx] || mask[nIdx] || zoneGrid[nIdx] !== targetZone) continue;
        visited[nIdx] = 1;
        queue.push(nIdx);
      }
    }
  } else {
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
      for (const [ddx, ddy] of [[1,0],[-1,0],[0,1],[0,-1]]) {
        const nx = px + ddx, ny = py + ddy;
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

// Persistent layer canvases
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
    const bridgeIdx = hitTestBridge(x, y);
    if (bridgeIdx !== hoveredBridgeIdx) {
      hoveredBridgeIdx = bridgeIdx;
      drawCanvas();
    }
    updateBridgeTooltip(bridgeIdx, x, y);
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
    hideBridgeTooltip();
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

function hitTestBridge(canvasX, canvasY) {
  if (!showBridges || !bridges || bridges.length === 0) return -1;
  const srcW = satelliteImg ? satelliteImg.width : zoneGridW;
  const srcH = satelliteImg ? satelliteImg.height : zoneGridH;
  const tm = currentHole?.terrainMeta;
  if (!srcW || !srcH || !tm) return -1;

  const hitRadius = 14;

  for (let i = 0; i < bridges.length; i++) {
    const b = bridges[i];
    const center = worldToNormalized(b.x, b.z);
    if (!center) continue;

    let imgX = (center.x - 0.5) * srcW * drawScale;
    let imgY = (center.y - 0.5) * srcH * drawScale;

    if (canvasRotation !== 0) {
      const rad = canvasRotation * Math.PI / 180;
      const c = Math.cos(rad), s = Math.sin(rad);
      const rx = imgX * c - imgY * s;
      const ry = imgX * s + imgY * c;
      imgX = rx; imgY = ry;
    }

    const cx = imgX * zoomLevel + canvas.width / 2 + panX;
    const cy = imgY * zoomLevel + canvas.height / 2 + panY;

    const dx = canvasX - cx, dy = canvasY - cy;
    if (dx * dx + dy * dy <= hitRadius * hitRadius) return i;
  }
  return -1;
}

function updateBridgeTooltip(idx, x, y) {
  let tooltip = document.getElementById("bridge-tooltip");
  if (idx < 0) { hideBridgeTooltip(); return; }
  const b = bridges[idx];
  if (!tooltip) {
    tooltip = document.createElement("div");
    tooltip.id = "bridge-tooltip";
    tooltip.className = "tee-tooltip";
    document.getElementById("canvas-stage").appendChild(tooltip);
  }
  tooltip.innerHTML =
    "<strong>Bridge: " + (b.id || "?") + "</strong><br>" +
    "yaw " + b.yaw_deg.toFixed(1) + "\u00b0, width " +
      (b.expected_path_width_m || 2.5).toFixed(1) + "m<br>" +
    "F: (" + b.anchor_forward.x.toFixed(1) + ", " +
             b.anchor_forward.z.toFixed(1) + ")<br>" +
    "B: (" + b.anchor_backward.x.toFixed(1) + ", " +
             b.anchor_backward.z.toFixed(1) + ")";
  tooltip.style.left = (x + 15) + "px";
  tooltip.style.top = (y - 10) + "px";
  tooltip.hidden = false;
}

function hideBridgeTooltip() {
  const t = document.getElementById("bridge-tooltip");
  if (t) t.hidden = true;
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

  // Rotation buttons
  document.querySelectorAll(".rot-btn").forEach(btn => {
    btn.addEventListener("click", () => setRotation(Number(btn.dataset.rot)));
  });

  // Q/E rotate by 90° (CCW/CW). Ignored while typing in inputs or during paint.
  window.addEventListener("keydown", (e) => {
    const tag = (e.target.tagName || "").toLowerCase();
    if (tag === "input" || tag === "textarea" || tag === "select") return;
    if (e.ctrlKey || e.metaKey || e.altKey) return;
    if (e.key === "q" || e.key === "Q") {
      setRotation((canvasRotation + 270) % 360);  // -90° CCW
      e.preventDefault();
    } else if (e.key === "e" || e.key === "E") {
      setRotation((canvasRotation + 90) % 360);   // +90° CW
      e.preventDefault();
    }
  });

  updateOpacityVisibility();

  const opacitySlider = document.getElementById("overlay-opacity");
  const opacityToggle = document.getElementById("btn-opacity-toggle");
  let lastNonZeroOpacity = 0.5;

  if (opacitySlider) opacitySlider.addEventListener("input", function () {
    overlayOpacity = this.value / 100;
    if (overlayOpacity > 0) lastNonZeroOpacity = overlayOpacity;
    if (opacityToggle) opacityToggle.textContent = overlayOpacity === 0 ? "0%" : (overlayOpacity === 1 ? "100%" : "·");
    drawCanvas();
  });

  if (opacityToggle) opacityToggle.addEventListener("click", function () {
    if (overlayOpacity > 0) {
      lastNonZeroOpacity = overlayOpacity;
      overlayOpacity = 0;
      opacitySlider.value = 0;
      this.textContent = "0%";
    } else {
      overlayOpacity = lastNonZeroOpacity;
      opacitySlider.value = Math.round(overlayOpacity * 100);
      this.textContent = overlayOpacity === 1 ? "100%" : "·";
    }
    drawCanvas();
  });

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
  document.getElementById("btn-raw-dem").addEventListener("click", viewRawDem);
  document.getElementById("btn-set-bounds").addEventListener("click", openMapOverlay);
  document.getElementById("btn-place-tees").addEventListener("click", placeTees);

  // ── Export helpers ──────────────────────────────────

  // Rotate a source canvas by canvasRotation degrees CW, returning a new canvas.
  function rotateCanvasCW(srcCanvas, degrees) {
    const d = ((degrees % 360) + 360) % 360;
    if (d === 0) return srcCanvas;
    const sideways = d === 90 || d === 270;
    const out = document.createElement("canvas");
    out.width = sideways ? srcCanvas.height : srcCanvas.width;
    out.height = sideways ? srcCanvas.width : srcCanvas.height;
    const octx = out.getContext("2d");
    octx.translate(out.width / 2, out.height / 2);
    octx.rotate(d * Math.PI / 180);
    octx.drawImage(srcCanvas, -srcCanvas.width / 2, -srcCanvas.height / 2);
    return out;
  }

  function exportLayerPNG(suffix, renderFn) {
    if (!zoneGrid || !currentHole) return;
    const tmpCanvas = document.createElement("canvas");
    tmpCanvas.width = zoneGridW;
    tmpCanvas.height = zoneGridH;
    const tmpCtx = tmpCanvas.getContext("2d");
    const imgData = tmpCtx.createImageData(zoneGridW, zoneGridH);
    renderFn(imgData.data);
    tmpCtx.putImageData(imgData, 0, 0);
    // Apply the current canvas rotation so the exported PNG matches the view
    const outCanvas = rotateCanvasCW(tmpCanvas, canvasRotation);
    const url = outCanvas.toDataURL("image/png");
    const a = document.createElement("a");
    a.href = url;
    a.download = "hole-" + String(currentHole.number).padStart(2, "0") + "-" + suffix + ".png";
    a.click();
    showBanner("Exported " + suffix + " PNG (" + outCanvas.width + "x" + outCanvas.height + ")" +
      (canvasRotation ? ", rotated " + canvasRotation + "°" : ""));
  }

  // ─── Smooth OB Mask ────────────────────────────────

  function smoothOBMask() {
    if (!obMask || !zoneGridW || !zoneGridH) return;
    const w = zoneGridW, h = zoneGridH;

    zoneUndoStack.push({
      grid: new Uint8Array(zoneGrid),
      trees: treesMask ? new Uint8Array(treesMask) : null,
      ob: obMask ? new Uint8Array(obMask) : null,
      cp: cartPathMask ? new Uint8Array(cartPathMask) : null
    });
    if (zoneUndoStack.length > MAX_UNDO) zoneUndoStack.shift();

    const invMask = new Uint8Array(w * h);
    for (let i = 0; i < w * h; i++) invMask[i] = obMask[i] ? 0 : 1;

    const contours = traceOBContours(invMask, w, h);
    if (contours.length === 0) { showBanner("No OB boundary found to smooth"); return; }

    let currentContours = contours;
    let smoothed;
    for (let pass = 0; pass < 2; pass++) {
      smoothed = [];
      for (const contour of currentContours) {
        if (contour.length < 6) continue;
        const simplified = rdpSimplify(contour, 12.0);
        const curved = chaikinSmooth(simplified, 3);
        smoothed.push(curved);
      }
      obMask.fill(1);
      for (const poly of smoothed) rasterizePolygon(poly, obMask, w, h, 0);

      if (pass < 1) {
        for (let i = 0; i < w * h; i++) invMask[i] = obMask[i] ? 0 : 1;
        currentContours = traceOBContours(invMask, w, h);
      }
    }

    zonePaintDirty = true;
    regenerateZonesImage();
    drawCanvas();

    const totalPts = smoothed.reduce((s, p) => s + p.length, 0);
    const origPts = contours.reduce((s, p) => s + p.length, 0);
    showBanner("Smoothed OB: " + contours.length + " region(s), " + origPts + " -> " + totalPts + " pts");
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

    zoneUndoStack.push({
      grid: new Uint8Array(zoneGrid),
      trees: treesMask ? new Uint8Array(treesMask) : null,
      ob: obMask ? new Uint8Array(obMask) : null,
      cp: cartPathMask ? new Uint8Array(cartPathMask) : null
    });
    if (zoneUndoStack.length > MAX_UNDO) zoneUndoStack.shift();

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

    if (regions.length === 0) { showBanner("No cart path regions found"); return; }

    cartPathMask.fill(0);
    let totalPaths = 0;

    for (const region of regions) {
      const rMask = new Uint8Array(w * h);
      for (const p of region) rMask[p.y * w + p.x] = 1;
      const contour = traceOrderedContour(rMask, w, h);
      if (!contour || contour.length < 6) continue;

      let maxDist = 0, idxA = 0, idxB = 0;
      for (let i = 0; i < contour.length; i++) {
        for (let j = i + 1; j < contour.length; j++) {
          const dx2 = contour[i].x - contour[j].x;
          const dy2 = contour[i].y - contour[j].y;
          const d = dx2 * dx2 + dy2 * dy2;
          if (d > maxDist) { maxDist = d; idxA = i; idxB = j; }
        }
      }

      const chain1 = [], chain2 = [];
      for (let i = idxA; i !== idxB; i = (i + 1) % contour.length) chain1.push(contour[i]);
      chain1.push(contour[idxB]);
      for (let i = idxB; i !== idxA; i = (i + 1) % contour.length) chain2.push(contour[i]);
      chain2.push(contour[idxA]);
      chain2.reverse();

      if (chain1.length < 2 || chain2.length < 2) continue;

      const numSamples = Math.max(Math.round(Math.sqrt(maxDist) / 2), 20);
      const resampled1 = resamplePolyline(chain1, numSamples);
      const resampled2 = resamplePolyline(chain2, numSamples);

      let centerline = [];
      for (let i = 0; i < numSamples; i++) {
        centerline.push({
          x: (resampled1[i].x + resampled2[i].x) / 2,
          y: (resampled1[i].y + resampled2[i].y) / 2
        });
      }

      centerline = rdpSimplify(centerline, 3.0);
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
    showBanner("Smoothed " + totalPaths + " cart path(s) (" + (halfW*2+1) + "px = " + ROAD_WIDTH_M + "m)");
  }

  function smoothTerrainZones() {
    if (!zoneGrid || !zoneGridW || !zoneGridH) return;
    const w = zoneGridW, h = zoneGridH;

    // Push undo
    zoneUndoStack.push({
      grid: new Uint8Array(zoneGrid),
      trees: treesMask ? new Uint8Array(treesMask) : null,
      ob: obMask ? new Uint8Array(obMask) : null,
      cp: cartPathMask ? new Uint8Array(cartPathMask) : null
    });
    if (zoneUndoStack.length > MAX_UNDO) zoneUndoStack.shift();

    // Terrain zones to smooth, in render order (back to front).
    // Zone 0 (background) and zone 4 (rough) are left untouched as base fills —
    // they cover large areas up to the image edge and smoothing their outer
    // boundary rounds off the corners of the image.
    // Only zones that represent distinct features GET smoothed.
    const renderOrder = [3, 1, 6, 7, 2, 10]; // semi-rough, fairway, bunker, water, green, tee
    const rdpEpsilon = 3.0;
    const chaikinPasses = 3;

    // Start from a copy of the current grid (preserves rough + background as-is)
    const newGrid = new Uint8Array(zoneGrid);

    // For each zone in render order, trace its contours, smooth, re-rasterize
    let totalOrigPts = 0, totalSmoothPts = 0;
    for (const zone of renderOrder) {
      // Build binary mask of this zone
      const mask = new Uint8Array(w * h);
      let hasAny = false;
      for (let i = 0; i < w * h; i++) {
        if (zoneGrid[i] === zone) { mask[i] = 1; hasAny = true; }
      }
      if (!hasAny) continue;

      // Find connected components via flood fill to trace each separately
      const visited = new Uint8Array(w * h);
      const regions = [];
      for (let y = 0; y < h; y++) {
        for (let x = 0; x < w; x++) {
          const idx = y * w + x;
          if (!mask[idx] || visited[idx]) continue;
          // Flood fill to get this region
          const stack = [idx];
          const region = [];
          visited[idx] = 1;
          while (stack.length > 0) {
            const ci = stack.pop();
            region.push(ci);
            const cx = ci % w, cy = (ci - cx) / w;
            for (const [dx, dy] of [[1,0],[-1,0],[0,1],[0,-1]]) {
              const nx = cx + dx, ny = cy + dy;
              if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
              const ni = ny * w + nx;
              if (!mask[ni] || visited[ni]) continue;
              visited[ni] = 1;
              stack.push(ni);
            }
          }
          if (region.length > 15) regions.push(region);
        }
      }

      for (const region of regions) {
        // Build region-local mask
        const rMask = new Uint8Array(w * h);
        for (const idx of region) rMask[idx] = 1;

        // Trace contour using Moore neighborhood
        const contour = traceOrderedContour(rMask, w, h);
        if (!contour || contour.length < 6) {
          // Too small to smooth — just paint as-is
          for (const idx of region) newGrid[idx] = zone;
          continue;
        }

        totalOrigPts += contour.length;

        // Close the contour by appending start point
        const closedContour = contour.concat([contour[0]]);

        // RDP simplify
        const simplified = rdpSimplify(closedContour, rdpEpsilon);
        if (simplified.length < 3) {
          for (const idx of region) newGrid[idx] = zone;
          continue;
        }

        // Chaikin smooth (closed polygon)
        const smoothed = chaikinSmooth(simplified, chaikinPasses);
        totalSmoothPts += smoothed.length;

        // Rasterize smoothed polygon
        // Dilate by 1px to prevent gap artifacts between adjacent zones
        rasterizePolygon(smoothed, newGrid, w, h, zone);
      }
    }

    // Apply
    for (let i = 0; i < w * h; i++) zoneGrid[i] = newGrid[i];

    zonePaintDirty = true;
    regenerateZonesImage();
    drawCanvas();
    showBanner("Smoothed terrain zones: " + totalOrigPts + " → " + totalSmoothPts + " contour points");
  }

  // ─── Contour Trace Helpers ─────────────────────────

  function traceOBContours(mask, w, h) {
    const result = [];
    const isBorder = new Uint8Array(w * h);
    const borderPixels = [];
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        if (!mask[y * w + x]) continue;
        if (x === 0 || x === w - 1 || y === 0 || y === h - 1 ||
            !mask[y * w + (x - 1)] || !mask[y * w + (x + 1)] ||
            !mask[(y - 1) * w + x] || !mask[(y + 1) * w + x]) {
          isBorder[y * w + x] = 1;
          borderPixels.push({ x, y });
        }
      }
    }
    if (borderPixels.length < 3) return result;

    const visited = new Uint8Array(w * h);
    const dx = [1, 1, 0, -1, -1, -1, 0, 1];
    const dy = [0, 1, 1, 1, 0, -1, -1, -1];

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
    }
    return [pts[start], pts[end]];
  }

  function _perpDist(p, a, b) {
    const dx = b.x - a.x, dy = b.y - a.y;
    const lenSq = dx * dx + dy * dy;
    if (lenSq < 0.0001) return Math.sqrt((p.x - a.x) ** 2 + (p.y - a.y) ** 2);
    const t = Math.max(0, Math.min(1, ((p.x - a.x) * dx + (p.y - a.y) * dy) / lenSq));
    const px = a.x + t * dx, py = a.y + t * dy;
    return Math.sqrt((p.x - px) ** 2 + (p.y - py) ** 2);
  }

  function chaikinSmooth(pts, passes) {
    let current = pts;
    for (let pass = 0; pass < passes; pass++) {
      const n = current.length;
      const next = [];
      for (let i = 0; i < n; i++) {
        const p0 = current[i];
        const p1 = current[(i + 1) % n];
        next.push({ x: p0.x * 0.75 + p1.x * 0.25, y: p0.y * 0.75 + p1.y * 0.25 });
        next.push({ x: p0.x * 0.25 + p1.x * 0.75, y: p0.y * 0.25 + p1.y * 0.75 });
      }
      current = next;
    }
    return current;
  }

  function rasterizePolygon(poly, mask, w, h, fillValue) {
    if (poly.length < 3) return;
    let minY = h, maxY = 0;
    for (const p of poly) { if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y; }
    minY = Math.max(0, Math.floor(minY));
    maxY = Math.min(h - 1, Math.ceil(maxY));
    const n = poly.length;
    for (let y = minY; y <= maxY; y++) {
      const xIntersections = [];
      for (let i = 0; i < n; i++) {
        const a = poly[i], b = poly[(i + 1) % n];
        if ((a.y <= y && b.y > y) || (b.y <= y && a.y > y)) {
          const t = (y - a.y) / (b.y - a.y);
          xIntersections.push(a.x + t * (b.x - a.x));
        }
      }
      xIntersections.sort((a, b) => a - b);
      for (let i = 0; i < xIntersections.length - 1; i += 2) {
        const x0 = Math.max(0, Math.round(xIntersections[i]));
        const x1 = Math.min(w - 1, Math.round(xIntersections[i + 1]));
        for (let x = x0; x <= x1; x++) mask[y * w + x] = fillValue;
      }
    }
  }

  function traceOrderedContour(mask, w, h) {
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
        let border = (nx===0||nx===w-1||ny===0||ny===h-1);
        if (!border) {
          if (!mask[ny*w+(nx-1)]||!mask[ny*w+(nx+1)]||
              !mask[(ny-1)*w+nx]||!mask[(ny+1)*w+nx]) border = true;
        }
        if (!border) continue;
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

  function stampCircle(cx, cy, r, w, h, mask) {
    for (let dy = -r; dy <= r; dy++) {
      for (let dx = -r; dx <= r; dx++) {
        if (dx*dx + dy*dy > r*r) continue;
        const px = cx+dx, py = cy+dy;
        if (px >= 0 && px < w && py >= 0 && py < h) mask[py * w + px] = 1;
      }
    }
  }

  function showBanner(msg) {
    const banner = document.getElementById("save-banner");
    banner.textContent = msg;
    banner.style.borderColor = "";
    banner.style.background = "";
    banner.style.color = "";
    setTimeout(() => { banner.textContent = "\u00a0"; }, 3000);
  }

  // Export the fetched satellite image, rotated to match the current view
  document.getElementById("export-satellite-btn").addEventListener("click", async () => {
    if (!currentHole?.hasSatellite) {
      showBanner("No satellite image yet — click Set Bounds first");
      return;
    }
    const pad = String(currentHole.number).padStart(2, "0");
    const url = "/api/satellite?course=" + COURSE_ID + "&hole=" + currentHole.number + "&_t=" + Date.now();
    const img = await loadImage(url);
    if (!img) { showBanner("Failed to load satellite image"); return; }
    const srcCanvas = document.createElement("canvas");
    srcCanvas.width = img.width;
    srcCanvas.height = img.height;
    srcCanvas.getContext("2d").drawImage(img, 0, 0);
    const outCanvas = rotateCanvasCW(srcCanvas, canvasRotation);
    const a = document.createElement("a");
    a.href = outCanvas.toDataURL("image/png");
    a.download = "hole-" + pad + "-satellite.png";
    a.click();
    showBanner("Exported satellite.png (" + outCanvas.width + "x" + outCanvas.height + ")" +
      (canvasRotation ? ", rotated " + canvasRotation + "°" : ""));
  });

  // Export All
  document.getElementById("export-all-btn").addEventListener("click", () => {
    exportLayerPNG("zones-all", (data) => {
      for (let i = 0; i < zoneGrid.length; i++) {
        const zone = (obMask && obMask[i]) ? 9 : (treesMask && treesMask[i]) ? 5 : (cartPathMask && cartPathMask[i]) ? 8 : zoneGrid[i];
        const c = ZONE_COLORS_RGB[zone] || [0, 0, 0];
        data[i * 4] = c[0]; data[i * 4 + 1] = c[1]; data[i * 4 + 2] = c[2]; data[i * 4 + 3] = 255;
      }
    });
  });

  document.getElementById("export-terrain-btn").addEventListener("click", () => {
    exportLayerPNG("terrain", (data) => {
      for (let i = 0; i < zoneGrid.length; i++) {
        const c = ZONE_COLORS_RGB[zoneGrid[i]] || [0, 0, 0];
        data[i * 4] = c[0]; data[i * 4 + 1] = c[1]; data[i * 4 + 2] = c[2]; data[i * 4 + 3] = 255;
      }
    });
  });

  document.getElementById("export-trees-btn").addEventListener("click", () => {
    exportLayerPNG("trees", (data) => {
      for (let i = 0; i < zoneGrid.length; i++) {
        const on = treesMask && treesMask[i];
        data[i * 4] = on ? 255 : 0; data[i * 4 + 1] = on ? 255 : 0; data[i * 4 + 2] = on ? 255 : 0; data[i * 4 + 3] = 255;
      }
    });
  });

  document.getElementById("export-cartpath-btn").addEventListener("click", () => {
    exportLayerPNG("cartpath", (data) => {
      for (let i = 0; i < zoneGrid.length; i++) {
        const on = cartPathMask && cartPathMask[i];
        data[i * 4] = on ? 255 : 0; data[i * 4 + 1] = on ? 255 : 0; data[i * 4 + 2] = on ? 255 : 0; data[i * 4 + 3] = 255;
      }
    });
  });

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
        // Draw incoming PNG into a canvas, then reverse the current canvas
        // rotation so the zone grid (stored north-up) matches the painting view.
        const rawCanvas = document.createElement("canvas");
        rawCanvas.width = img.width;
        rawCanvas.height = img.height;
        rawCanvas.getContext("2d").drawImage(img, 0, 0);
        // Reverse rotation: incoming was rotated CW by canvasRotation when exported,
        // so rotate CCW by the same amount (= CW by 360-canvasRotation).
        const reversed = rotateCanvasCW(rawCanvas, (360 - canvasRotation) % 360);
        const imageData = reversed.getContext("2d").getImageData(0, 0, reversed.width, reversed.height);

        if (zoneGrid) {
          zoneUndoStack.push({ grid: new Uint8Array(zoneGrid), trees: treesMask ? new Uint8Array(treesMask) : null, ob: obMask ? new Uint8Array(obMask) : null, cp: cartPathMask ? new Uint8Array(cartPathMask) : null });
          if (zoneUndoStack.length > MAX_UNDO) zoneUndoStack.shift();
        }

        applyFn(imageData.data, reversed.width, reversed.height);

        zonePaintDirty = true;
        regenerateZonesImage();
        showBanner("Imported from " + file.name + " (" + img.width + "x" + img.height + ")");
      };
      img.src = URL.createObjectURL(file);
      e.target.value = "";
    };
  }

  document.getElementById("import-terrain-btn").addEventListener("click", () => {
    importLayerPNG("import-terrain-file", (pixels, w, h) => {
      const newGrid = new Uint8Array(w * h);
      for (let i = 0; i < w * h; i++) {
        const zone = matchZoneColor(pixels[i * 4], pixels[i * 4 + 1], pixels[i * 4 + 2]);
        if (zone !== 5 && zone !== 8 && zone !== 9) newGrid[i] = zone;
      }
      zoneGrid = newGrid;
      if (w !== zoneGridW || h !== zoneGridH) {
        treesMask = new Uint8Array(w * h);
        obMask = new Uint8Array(w * h);
        cartPathMask = new Uint8Array(w * h);
      }
      zoneGridW = w;
      zoneGridH = h;
    });
  });

  document.getElementById("import-trees-btn").addEventListener("click", () => {
    importLayerPNG("import-trees-file", (pixels, w, h) => {
      if (w !== zoneGridW || h !== zoneGridH) { showBanner("Size mismatch: expected " + zoneGridW + "x" + zoneGridH + ", got " + w + "x" + h); return; }
      for (let i = 0; i < w * h; i++) treesMask[i] = pixels[i * 4 + 3] > 128 ? 1 : 0;
    });
  });

  document.getElementById("import-cartpath-btn").addEventListener("click", () => {
    importLayerPNG("import-cartpath-file", (pixels, w, h) => {
      if (w !== zoneGridW || h !== zoneGridH) { showBanner("Size mismatch: expected " + zoneGridW + "x" + zoneGridH + ", got " + w + "x" + h); return; }
      for (let i = 0; i < w * h; i++) cartPathMask[i] = pixels[i * 4 + 3] > 128 ? 1 : 0;
    });
  });

  document.getElementById("import-ob-btn").addEventListener("click", () => {
    importLayerPNG("import-ob-file", (pixels, w, h) => {
      if (w !== zoneGridW || h !== zoneGridH) { showBanner("Size mismatch: expected " + zoneGridW + "x" + zoneGridH + ", got " + w + "x" + h); return; }
      for (let i = 0; i < w * h; i++) obMask[i] = pixels[i * 4 + 3] > 128 ? 1 : 0;
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

  document.getElementById("btn-smooth-ob")?.addEventListener("click", () => smoothOBMask());
  document.getElementById("btn-smooth-cp")?.addEventListener("click", () => smoothCartPathMask());
  document.getElementById("btn-smooth-terrain")?.addEventListener("click", () => smoothTerrainZones());

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

// ── Regen Heightmap ─────────────────────────────────

async function regenHeightmap() {
  if (!currentHole) return;
  const banner = document.getElementById("save-banner");
  const btn = document.getElementById("btn-regen");

  btn.disabled = true;
  btn.textContent = "Regenerating...";
  banner.textContent = "Regenerating heightmap for Hole " + currentHole.number + "...";
  banner.style.borderColor = "";
  banner.style.background = "";
  banner.style.color = "";

  try {
    const res = await fetch("/api/regen-heightmap", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ courseId: COURSE_ID, holeNumber: currentHole.number }),
    });
    const result = await res.json();

    if (result.ok) {
      heightmapImg = await loadImage("/api/heightmap?course=" + COURSE_ID + "&hole=" + currentHole.number);
      drawCanvas();
      banner.textContent = "Heightmap regenerated for Hole " + currentHole.number;
    } else {
      banner.textContent = "Regen failed: " + (result.message || "unknown error");
      banner.style.borderColor = "rgba(144,52,52,0.35)";
      banner.style.background = "rgba(144,52,52,0.15)";
      banner.style.color = "#ffb4b4";
    }
  } catch (err) {
    banner.textContent = "Regen failed: " + err.message;
    banner.style.borderColor = "rgba(144,52,52,0.35)";
    banner.style.background = "rgba(144,52,52,0.15)";
    banner.style.color = "#ffb4b4";
  }

  btn.disabled = false;
  btn.textContent = "Regen Heightmap";
  setTimeout(() => { banner.textContent = "\u00a0"; banner.style.borderColor = ""; banner.style.background = ""; banner.style.color = ""; }, 5000);
}

// ── Raw DEM viewer ──────────────────────────────────
// Opens the untransformed GSI DEM5A heightmap for the current hole's bounds
// in a new tab. Useful for visually confirming the source elevation data
// before it's transformed by generate-terrain.mjs.

function viewRawDem() {
  if (!currentHole) return;
  const url = "/api/raw-dem?course=" + COURSE_ID + "&hole=" + currentHole.number +
    "&size=2049&rot=" + canvasRotation + "&_t=" + Date.now();
  window.open(url, "_blank", "noopener");
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
    if (teesModified.size > 0 && currentHole.tees?.tees) {
      const srcW = zoneGridW || 1024;
      const srcH = zoneGridH || 1024;

      for (const tee of currentHole.tees.tees) {
        if (tee.normalized) {
          tee.pixel = { x: Math.round(tee.normalized.x * srcW), y: Math.round(tee.normalized.y * srcH) };
        }
        if (tee.confidence === "manual") tee.confidence = "set";
      }

      await fetch("/api/tees", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ courseId: COURSE_ID, holeNumber: currentHole.number, tees: currentHole.tees.tees }),
      });
      parts.push(teesModified.size + " tee(s)");
      teesModified = new Set();
      drawCanvas();
    }

    if (zonePaintDirty && zoneGrid) {
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

    if (parts.length === 0) parts.push("nothing to save");
    banner.textContent = "Saved: " + parts.join(" + ") + " for Hole " + currentHole.number;
    setTimeout(() => { banner.textContent = "\u00a0"; }, 4000);
  } catch (err) {
    banner.textContent = "Save failed: " + err.message;
    banner.style.borderColor = "rgba(144,52,52,0.35)";
    banner.style.background = "rgba(144,52,52,0.15)";
    banner.style.color = "#ffb4b4";
  }
}

// ── Save As ─────────────────────────────────────────

function saveAsSnapshot() {
  if (!currentHole) return;
  const snapshot = {
    courseId: COURSE_ID,
    holeNumber: currentHole.number,
    tees: currentHole.tees ? JSON.parse(JSON.stringify(currentHole.tees)) : null,
    holeBounds: currentHole.holeBounds || null,
  };

  if (zoneGrid) {
    let mergedBin = "", terrainBin = "", treesBin = "", obBin = "", cpBin = "";
    for (let i = 0; i < zoneGrid.length; i++) {
      mergedBin += String.fromCharCode(obMask && obMask[i] ? 9 : treesMask && treesMask[i] ? 5 : cartPathMask && cartPathMask[i] ? 8 : zoneGrid[i]);
      terrainBin += String.fromCharCode(zoneGrid[i]);
      treesBin += String.fromCharCode(treesMask ? treesMask[i] : 0);
      obBin += String.fromCharCode(obMask ? obMask[i] : 0);
      cpBin += String.fromCharCode(cartPathMask ? cartPathMask[i] : 0);
    }
    snapshot.zones = {
      width: zoneGridW, height: zoneGridH,
      grid: btoa(mergedBin), terrain_grid: btoa(terrainBin),
      trees_mask: btoa(treesBin), ob_mask: btoa(obBin), cart_path_mask: btoa(cpBin),
    };
  }

  const blob = new Blob([JSON.stringify(snapshot, null, 2)], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = "hole-" + String(currentHole.number).padStart(2, "0") + "-geo-snapshot.json";
  a.click();
  URL.revokeObjectURL(url);

  const banner = document.getElementById("save-banner");
  banner.textContent = "Snapshot exported for Hole " + currentHole.number;
  setTimeout(() => { banner.textContent = "\u00a0"; }, 3000);
}

// ── Load ────────────────────────────────────────────

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

      if (zoneGrid) {
        zoneUndoStack.push({ grid: new Uint8Array(zoneGrid), trees: treesMask ? new Uint8Array(treesMask) : null, ob: obMask ? new Uint8Array(obMask) : null, cp: cartPathMask ? new Uint8Array(cartPathMask) : null });
        if (zoneUndoStack.length > MAX_UNDO) zoneUndoStack.shift();
      }

      if (snapshot.tees) {
        currentHole.tees = snapshot.tees;
        teesModified = new Set(snapshot.tees.tees ? snapshot.tees.tees.map((_, i) => i) : []);
      }

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

// ── Leaflet Map Overlay ─────────────────────────────

function setupMapOverlay() {
  document.getElementById("btn-map-cancel").addEventListener("click", closeMapOverlay);
  document.getElementById("btn-map-confirm").addEventListener("click", confirmMapBounds);
}

function openMapOverlay() {
  if (!currentHole) return;
  const overlay = document.getElementById("map-overlay");
  overlay.hidden = false;

  if (!mapInitialized) {
    // Wait for layout so #map-container has dimensions
    requestAnimationFrame(() => {
      initLeafletMap();
      mapInitialized = true;
      setupMapBounds();
      // Force Leaflet to recompute tile grid after DOM settles
      setTimeout(() => leafletMap.invalidateSize(), 50);
    });
    return;
  } else {
    leafletMap.invalidateSize();
  }

  setupMapBounds();
}

let cornerMarkers = [];
let satelliteOverlay = null;

function setupMapBounds() {
  let bounds;
  // Remove any old PNG overlay
  if (satelliteOverlay) { leafletMap.removeLayer(satelliteOverlay); satelliteOverlay = null; }

  if (currentHole.holeBounds) {
    const b = currentHole.holeBounds.bounds;
    bounds = L.latLngBounds([b.south, b.west], [b.north, b.east]);
    leafletMap.fitBounds(bounds.pad(0.3));
    document.getElementById("map-zoom-select").value = String(currentHole.holeBounds.gsi_zoom || 18);

    // If a satellite.png exists for this hole, overlay it so you can
    // visually verify it aligns with the GSI tiles underneath.
    if (currentHole.hasSatellite) {
      satelliteOverlay = L.imageOverlay(
        "/api/satellite?course=" + COURSE_ID + "&hole=" + currentHole.number + "&_t=" + Date.now(),
        bounds,
        { opacity: 0.6, interactive: false }
      ).addTo(leafletMap);
    }
  } else {
    const config = courseData.config;
    const center = config?.center || { lat: 34.907, lon: 136.432 };
    leafletMap.setView([center.lat, center.lon], 16);
    const offset = 0.002;
    bounds = L.latLngBounds(
      [center.lat - offset, center.lon - offset],
      [center.lat + offset, center.lon + offset]
    );
  }

  // Remove old
  if (boundsRect) { leafletMap.removeLayer(boundsRect); }
  cornerMarkers.forEach(m => leafletMap.removeLayer(m));
  cornerMarkers = [];

  boundsRect = L.rectangle(bounds, {
    color: '#45bcff', weight: 2, fillOpacity: 0.1, dashArray: '5,5',
    interactive: true, bubblingMouseEvents: false
  }).addTo(leafletMap);

  // Click-and-drag inside the rectangle to move the whole selection.
  boundsRect.on('mousedown', onRectDragStart);

  // Draggable corner markers: NW, NE, SW, SE
  // iconAnchor centers the visible square on the actual coordinate
  const makeIcon = () => L.divIcon({
    className: 'bounds-corner',
    iconSize: [14, 14],
    iconAnchor: [7, 7],
    html: '<div style="width:14px;height:14px;border:2px solid #45bcff;background:#fff;border-radius:3px;box-shadow:0 0 4px rgba(0,0,0,0.6);cursor:move"></div>'
  });

  const corners = [
    { key: 'nw', ll: [bounds.getNorth(), bounds.getWest()] },
    { key: 'ne', ll: [bounds.getNorth(), bounds.getEast()] },
    { key: 'sw', ll: [bounds.getSouth(), bounds.getWest()] },
    { key: 'se', ll: [bounds.getSouth(), bounds.getEast()] },
  ];

  corners.forEach(c => {
    const m = L.marker(c.ll, { icon: makeIcon(), draggable: true });
    m.cornerKey = c.key;
    m.on('drag', onCornerDrag);
    m.addTo(leafletMap);
    // Prevent mousedown from bubbling to the map (which would start a map pan)
    if (m._icon) {
      L.DomEvent.disableClickPropagation(m._icon);
      L.DomEvent.on(m._icon, 'mousedown touchstart', L.DomEvent.stopPropagation);
    }
    cornerMarkers.push(m);
  });

  updateMapInfo();
}

let _rectDragState = null;

function onRectDragStart(e) {
  // Don't initiate rect drag if user pressed on a corner marker
  if (e.originalEvent && e.originalEvent.target && e.originalEvent.target.closest('.leaflet-marker-icon')) return;

  L.DomEvent.stopPropagation(e.originalEvent);
  L.DomEvent.preventDefault(e.originalEvent);
  leafletMap.dragging.disable();

  const b = boundsRect.getBounds();
  _rectDragState = {
    startLL: e.latlng,
    startBounds: {
      north: b.getNorth(), south: b.getSouth(),
      east: b.getEast(), west: b.getWest()
    }
  };

  leafletMap.on('mousemove', onRectDragMove);
  leafletMap.on('mouseup', onRectDragEnd);
}

function onRectDragMove(e) {
  if (!_rectDragState) return;
  const dLat = e.latlng.lat - _rectDragState.startLL.lat;
  const dLng = e.latlng.lng - _rectDragState.startLL.lng;
  const s = _rectDragState.startBounds;
  const newBounds = L.latLngBounds(
    [s.south + dLat, s.west + dLng],
    [s.north + dLat, s.east + dLng]
  );
  boundsRect.setBounds(newBounds);
  cornerMarkers.forEach(cm => {
    if (cm.cornerKey === 'nw') cm.setLatLng([newBounds.getNorth(), newBounds.getWest()]);
    else if (cm.cornerKey === 'ne') cm.setLatLng([newBounds.getNorth(), newBounds.getEast()]);
    else if (cm.cornerKey === 'sw') cm.setLatLng([newBounds.getSouth(), newBounds.getWest()]);
    else if (cm.cornerKey === 'se') cm.setLatLng([newBounds.getSouth(), newBounds.getEast()]);
  });
  updateMapInfo();
}

function onRectDragEnd() {
  _rectDragState = null;
  leafletMap.off('mousemove', onRectDragMove);
  leafletMap.off('mouseup', onRectDragEnd);
  leafletMap.dragging.enable();
}

function onCornerDrag(e) {
  const m = e.target;
  const ll = m.getLatLng();
  const b = boundsRect.getBounds();
  let n = b.getNorth(), s = b.getSouth(), east = b.getEast(), w = b.getWest();

  if (m.cornerKey === 'nw') { n = ll.lat; w = ll.lng; }
  else if (m.cornerKey === 'ne') { n = ll.lat; east = ll.lng; }
  else if (m.cornerKey === 'sw') { s = ll.lat; w = ll.lng; }
  else if (m.cornerKey === 'se') { s = ll.lat; east = ll.lng; }

  // Enforce n > s, east > w
  if (n < s) [n, s] = [s, n];
  if (east < w) [east, w] = [w, east];

  const newBounds = L.latLngBounds([s, w], [n, east]);
  boundsRect.setBounds(newBounds);

  // Move other corner markers to keep rectangle coherent
  cornerMarkers.forEach(cm => {
    if (cm === m) return;
    if (cm.cornerKey === 'nw') cm.setLatLng([n, w]);
    else if (cm.cornerKey === 'ne') cm.setLatLng([n, east]);
    else if (cm.cornerKey === 'sw') cm.setLatLng([s, w]);
    else if (cm.cornerKey === 'se') cm.setLatLng([s, east]);
  });

  updateMapInfo();
}

function initLeafletMap() {
  leafletMap = L.map('map-container', {
    zoomControl: true,
  }).setView([34.907, 136.432], 16);

  // GSI satellite tile layer (Japan)
  const gsiSat = L.tileLayer('https://cyberjapandata.gsi.go.jp/xyz/seamlessphoto/{z}/{x}/{y}.jpg', {
    attribution: 'GSI Japan',
    maxZoom: 19,
    maxNativeZoom: 18,
  });

  // Esri world imagery (global fallback)
  const esriSat = L.tileLayer(
    'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
    { attribution: 'Esri', maxZoom: 19 }
  );

  // OSM street map (last resort)
  const osm = L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: 'OpenStreetMap', maxZoom: 19,
  });

  gsiSat.addTo(leafletMap);

  // Layer switcher
  L.control.layers(
    { 'GSI Japan': gsiSat, 'Esri World': esriSat, 'OSM Street': osm },
    null,
    { position: 'topright', collapsed: false }
  ).addTo(leafletMap);
}

function updateMapInfo() {
  if (!boundsRect) return;
  const b = boundsRect.getBounds();
  const n = b.getNorth().toFixed(5);
  const s = b.getSouth().toFixed(5);
  const e = b.getEast().toFixed(5);
  const w = b.getWest().toFixed(5);

  document.getElementById("map-bounds-info").textContent =
    "N: " + n + "  S: " + s + "  E: " + e + "  W: " + w;

  // Compute dimensions in meters (haversine)
  const widthM = haversine(b.getNorth(), b.getWest(), b.getNorth(), b.getEast());
  const heightM = haversine(b.getNorth(), b.getWest(), b.getSouth(), b.getWest());

  document.getElementById("map-dims-info").textContent =
    "Size: " + widthM.toFixed(0) + " x " + heightM.toFixed(0) + " m";
}

function haversine(lat1, lon1, lat2, lon2) {
  const R = 6371000;
  const dLat = (lat2 - lat1) * Math.PI / 180;
  const dLon = (lon2 - lon1) * Math.PI / 180;
  const a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
    Math.cos(lat1 * Math.PI / 180) * Math.cos(lat2 * Math.PI / 180) *
    Math.sin(dLon / 2) * Math.sin(dLon / 2);
  return R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
}

async function confirmMapBounds() {
  if (!boundsRect || !currentHole) return;
  const b = boundsRect.getBounds();
  const gsiZoom = Number(document.getElementById("map-zoom-select").value);
  console.log('[confirmMapBounds] Saving bounds:',
    'N=' + b.getNorth().toFixed(6),
    'S=' + b.getSouth().toFixed(6),
    'E=' + b.getEast().toFixed(6),
    'W=' + b.getWest().toFixed(6),
    'zoom=' + gsiZoom);
  console.log('[confirmMapBounds] Corner markers are at:');
  cornerMarkers.forEach(cm => {
    const p = cm.getLatLng();
    console.log('  ', cm.cornerKey, 'lat=' + p.lat.toFixed(6), 'lng=' + p.lng.toFixed(6));
  });

  // Compute exact global-pixel coords using Leaflet's own projection so the
  // server-side crop and the Leaflet overlay agree exactly.
  const nwPoint = leafletMap.project([b.getNorth(), b.getWest()], gsiZoom);
  const sePoint = leafletMap.project([b.getSouth(), b.getEast()], gsiZoom);
  const pxLeft   = nwPoint.x;
  const pxTop    = nwPoint.y;
  const pxRight  = sePoint.x;
  const pxBottom = sePoint.y;
  console.log('[confirmMapBounds] Leaflet pixel rect at zoom', gsiZoom,
    ': L=' + pxLeft.toFixed(2), 'T=' + pxTop.toFixed(2),
    'R=' + pxRight.toFixed(2), 'B=' + pxBottom.toFixed(2),
    'W=' + (pxRight - pxLeft).toFixed(2), 'H=' + (pxBottom - pxTop).toFixed(2));

  const holeBoundsData = {
    schema_version: "1.0.0",
    course_id: COURSE_ID,
    hole_number: currentHole.number,
    par: courseData.course.holes.find(h => h.number === currentHole.number)?.par || 4,
    bounds: {
      north: parseFloat(b.getNorth().toFixed(6)),
      south: parseFloat(b.getSouth().toFixed(6)),
      east: parseFloat(b.getEast().toFixed(6)),
      west: parseFloat(b.getWest().toFixed(6)),
    },
    // Leaflet-projected pixel rect in the global Web-Mercator pixel grid at gsi_zoom.
    // The server script uses these to crop — guaranteed to match what Leaflet
    // renders as tile positions, avoiding any Mercator-math drift.
    pixel_rect: {
      left: pxLeft,
      top: pxTop,
      right: pxRight,
      bottom: pxBottom,
    },
    gsi_zoom: gsiZoom,
  };

  // Add tees from course.json if available
  const ch = courseData.course.holes.find(h => h.number === currentHole.number);
  if (ch?.tees) {
    holeBoundsData.tees = ch.tees;
    holeBoundsData.championship_yards = ch.tees.back?.yards || 0;
  }

  const banner = document.getElementById("save-banner");
  const confirmBtn = document.getElementById("btn-map-confirm");
  confirmBtn.disabled = true;
  confirmBtn.textContent = "Saving...";

  try {
    // Save hole-bounds.json
    await fetch("/api/hole-bounds?course=" + COURSE_ID + "&hole=" + currentHole.number, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(holeBoundsData),
    });

    // Fetch satellite tiles
    confirmBtn.textContent = "Fetching tiles...";
    const fetchRes = await fetch("/api/fetch-satellite", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ courseId: COURSE_ID, holeNumber: currentHole.number }),
    });
    const fetchResult = await fetchRes.json();

    if (fetchResult.ok) {
      // Initialize empty zone grid matching satellite dimensions
      const boundsRes = await fetch("/api/hole-bounds?course=" + COURSE_ID + "&hole=" + currentHole.number);
      const updatedBounds = await boundsRes.json();
      if (updatedBounds.image_dimensions) {
        await fetch("/api/init-zones", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            courseId: COURSE_ID,
            holeNumber: currentHole.number,
            width: updatedBounds.image_dimensions.width,
            height: updatedBounds.image_dimensions.height,
          }),
        });
      }

      closeMapOverlay();
      // Reload course data and reselect hole
      const res = await fetch("/api/course?id=" + COURSE_ID);
      courseData = await res.json();
      buildHoleNav();
      await selectHole(currentHole.number);
      banner.textContent = "Bounds set and satellite fetched for Hole " + currentHole.number;
    } else {
      banner.textContent = "Fetch failed: " + (fetchResult.message || "unknown");
      banner.style.borderColor = "rgba(144,52,52,0.35)";
      banner.style.background = "rgba(144,52,52,0.15)";
      banner.style.color = "#ffb4b4";
    }
  } catch (err) {
    banner.textContent = "Error: " + err.message;
    banner.style.borderColor = "rgba(144,52,52,0.35)";
    banner.style.background = "rgba(144,52,52,0.15)";
    banner.style.color = "#ffb4b4";
  }

  confirmBtn.disabled = false;
  confirmBtn.textContent = "Confirm & Fetch";
  setTimeout(() => { banner.textContent = "\u00a0"; banner.style.borderColor = ""; banner.style.background = ""; banner.style.color = ""; }, 5000);
}

function closeMapOverlay() {
  document.getElementById("map-overlay").hidden = true;
}

// ── Stats Cards ─────────────────────────────────────

function updateStats() {
  const container = document.getElementById("hole-stats");
  container.innerHTML = "";

  if (!currentHole) return;

  const cards = [];

  if (currentHole.tees?.tees) {
    const found = currentHole.tees.tees.filter(t => t.confidence !== "missing").length;
    cards.push({ label: "Tee Markers", value: found + "/4", detail: "Drag to reposition" });
  }

  if (currentHole.terrainMeta) {
    const tm = currentHole.terrainMeta;
    cards.push({ label: "Terrain", value: tm.terrain_width_m.toFixed(0) + "x" + tm.terrain_length_m.toFixed(0) + "m",
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

  if (currentHole.holeBounds) {
    const b = currentHole.holeBounds.bounds;
    const widthM = haversine(b.north, b.west, b.north, b.east);
    const heightM = haversine(b.north, b.west, b.south, b.west);
    cards.push({ label: "Bounds", value: widthM.toFixed(0) + "x" + heightM.toFixed(0) + "m",
      detail: "Zoom " + (currentHole.holeBounds.gsi_zoom || "?") });
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
