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
let brushSize = 5;
let isPainting = false;
let zoneUndoStack = [];
const MAX_UNDO = 30;
let fillMode = false;

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
}

function updateBrushUI() {
  const toolbar = document.getElementById("brush-toolbar");
  const label = document.getElementById("active-brush-label");
  if (activeBrushZone >= 0) {
    toolbar.hidden = false;
    label.textContent = ZONE_LEGEND[activeBrushZone].name;
    label.style.background = ZONE_LEGEND[activeBrushZone].color;
    label.style.color = activeBrushZone === 0 || activeBrushZone === 5 || activeBrushZone === 4 ? "#fff" : "#000";
    canvas.classList.toggle("painting", true);
  } else {
    toolbar.hidden = true;
    canvas.classList.remove("painting");
  }
}

function updateLegendVisibility() {
  const legend = document.getElementById("zone-legend");
  const show = currentView === "zones" || currentView === "both" || activeBrushZone >= 0;
  legend.hidden = !show;
  if (!show && activeBrushZone >= 0) {
    activeBrushZone = -1;
    legend.querySelectorAll(".legend-item").forEach(i => i.classList.remove("is-active"));
    updateBrushUI();
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
    zoneGrid = new Uint8Array(raw.length);
    treesMask = new Uint8Array(raw.length);
    obMask = new Uint8Array(raw.length);
    for (let i = 0; i < raw.length; i++) {
      const v = raw.charCodeAt(i);
      if (v === 9) { obMask[i] = 1; zoneGrid[i] = 0; }
      else if (v === 5) { treesMask[i] = 1; zoneGrid[i] = 0; }
      else { zoneGrid[i] = v; }
    }
    regenerateZonesImage();
  } catch {
    zoneGrid = null;
    treesMask = null;
    treesImg = null;
    obMask = null;
    obImg = null;
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

  if ((currentView === "zones" || currentView === "both") && zonesImg) {
    ctx.globalAlpha = currentView === "both" ? overlayOpacity : 1;
    ctx.drawImage(zonesImg, -hw, -hh, srcW * scale, srcH * scale);
  }

  if (currentView === "heightmap" && heightmapImg) {
    ctx.globalAlpha = 1;
    ctx.drawImage(heightmapImg, -hw, -hh, srcW * scale, srcH * scale);
  }

  // Trees overlay (over terrain, under OB)
  if (showTrees && treesImg && currentView !== "heightmap") {
    ctx.globalAlpha = 1;
    ctx.drawImage(treesImg, -hw, -hh, srcW * scale, srcH * scale);
  }

  // OB overlay (on top of everything)
  if (showOB && obImg && currentView !== "heightmap") {
    ctx.globalAlpha = 1;
    ctx.drawImage(obImg, -hw, -hh, srcW * scale, srcH * scale);
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
      else { zoneGrid[idx] = activeBrushZone; }
    }
  }

  zonePaintDirty = true;
  regenerateZonesImage();
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

function regenerateZonesImage() {
  if (!zoneGrid) return;

  // Main zones canvas
  const tempCanvas = document.createElement("canvas");
  tempCanvas.width = zoneGridW;
  tempCanvas.height = zoneGridH;
  const tempCtx = tempCanvas.getContext("2d");
  const imgData = tempCtx.createImageData(zoneGridW, zoneGridH);

  // Trees-only canvas
  const treesCanvas = document.createElement("canvas");
  treesCanvas.width = zoneGridW;
  treesCanvas.height = zoneGridH;
  const treesCtx = treesCanvas.getContext("2d");
  const treesData = treesCtx.createImageData(zoneGridW, zoneGridH);

  // OB-only canvas
  const obCanvas = document.createElement("canvas");
  obCanvas.width = zoneGridW;
  obCanvas.height = zoneGridH;
  const obCtx = obCanvas.getContext("2d");
  const obData = obCtx.createImageData(zoneGridW, zoneGridH);

  const treesC = ZONE_COLORS_RGB[5];
  const obC = ZONE_COLORS_RGB[9];
  for (let i = 0; i < zoneGrid.length; i++) {
    // Main zones layer
    const c = ZONE_COLORS_RGB[zoneGrid[i]] || [0, 0, 0];
    imgData.data[i * 4]     = c[0];
    imgData.data[i * 4 + 1] = c[1];
    imgData.data[i * 4 + 2] = c[2];
    imgData.data[i * 4 + 3] = 255;

    // Trees layer from mask
    if (treesMask && treesMask[i]) {
      treesData.data[i * 4]     = treesC[0];
      treesData.data[i * 4 + 1] = treesC[1];
      treesData.data[i * 4 + 2] = treesC[2];
      treesData.data[i * 4 + 3] = 255;
    }

    // OB layer from mask
    if (obMask && obMask[i]) {
      obData.data[i * 4]     = obC[0];
      obData.data[i * 4 + 1] = obC[1];
      obData.data[i * 4 + 2] = obC[2];
      obData.data[i * 4 + 3] = 255;
    }
  }

  tempCtx.putImageData(imgData, 0, 0);
  zonesImg = tempCanvas;

  treesCtx.putImageData(treesData, 0, 0);
  treesImg = treesCanvas;

  obCtx.putImageData(obData, 0, 0);
  obImg = obCanvas;

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

    if (activeBrushZone >= 0 && (currentView === "zones" || currentView === "both")) {
      if (zoneGrid) {
        zoneUndoStack.push({ grid: new Uint8Array(zoneGrid), trees: treesMask ? new Uint8Array(treesMask) : null, ob: obMask ? new Uint8Array(obMask) : null });
        if (zoneUndoStack.length > MAX_UNDO) zoneUndoStack.shift();
      }
      const norm = canvasToNormalized(x, y);
      if (norm) {
        if (fillMode) {
          floodFillZone(norm.x, norm.y);
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
      if (norm) paintZone(norm.x, norm.y);
      return;
    }

    const teeIdx = hitTestTee(x, y);
    const hasBrush = activeBrushZone >= 0 && (currentView === "zones" || currentView === "both");
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

  document.getElementById("btn-rotate-ccw").addEventListener("click", () => {
    orientation.rotation = (orientation.rotation + 270) % 360;
    updateOrientationUI(); drawCanvas();
  });
  document.getElementById("btn-rotate-cw").addEventListener("click", () => {
    orientation.rotation = (orientation.rotation + 90) % 360;
    updateOrientationUI(); drawCanvas();
  });
  document.getElementById("btn-flip-h").addEventListener("click", () => {
    orientation.flipH = !orientation.flipH;
    updateOrientationUI(); drawCanvas();
  });
  document.getElementById("btn-flip-v").addEventListener("click", () => {
    orientation.flipV = !orientation.flipV;
    updateOrientationUI(); drawCanvas();
  });
  document.getElementById("btn-reset").addEventListener("click", () => {
    orientation = { rotation: 0, flipH: false, flipV: false };
    updateOrientationUI(); drawCanvas();
  });

  document.getElementById("overlay-opacity").addEventListener("input", function () {
    overlayOpacity = this.value / 100;
    drawCanvas();
  });

  document.getElementById("btn-toggle-trees").addEventListener("click", function () {
    showTrees = !showTrees;
    this.classList.toggle("is-active-toggle", showTrees);
    drawCanvas();
  });

  document.getElementById("btn-toggle-ob").addEventListener("click", function () {
    showOB = !showOB;
    this.classList.toggle("is-active-toggle", showOB);
    drawCanvas();
  });

  document.getElementById("btn-save").addEventListener("click", saveAll);
  document.getElementById("btn-regen").addEventListener("click", regenHeightmap);

  // Brush controls
  document.getElementById("brush-size").addEventListener("input", function () {
    brushSize = Number(this.value);
    document.getElementById("brush-size-label").textContent = brushSize + "px";
  });

  document.querySelectorAll(".paint-mode-btn").forEach(btn => {
    btn.addEventListener("click", () => {
      document.querySelectorAll(".paint-mode-btn").forEach(b => b.classList.remove("is-active"));
      btn.classList.add("is-active");
      fillMode = btn.dataset.paintmode === "fill";
      document.getElementById("brush-size-group").style.display = fillMode ? "none" : "";
    });
  });

  document.getElementById("btn-clear-brush").addEventListener("click", () => {
    activeBrushZone = -1;
    document.querySelectorAll("#zone-legend .legend-item").forEach(i => i.classList.remove("is-active"));
    updateBrushUI();
  });

  // Undo (Ctrl+Z)
  window.addEventListener("keydown", (e) => {
    if ((e.ctrlKey || e.metaKey) && e.key === "z" && !e.shiftKey) {
      if (zoneUndoStack.length > 0 && zoneGrid) {
        const snap = zoneUndoStack.pop();
        zoneGrid = snap.grid;
        treesMask = snap.trees;
        obMask = snap.ob;
        zonePaintDirty = true;
        regenerateZonesImage();
        e.preventDefault();
      }
    }
  });

  window.addEventListener("resize", drawCanvas);
}

function updateOrientationUI() {
  document.getElementById("btn-flip-h").classList.toggle("is-active-toggle", orientation.flipH);
  document.getElementById("btn-flip-v").classList.toggle("is-active-toggle", orientation.flipV);
  document.getElementById("orientation-readout").textContent =
    "R:" + orientation.rotation + "° H:" + (orientation.flipH ? "Y" : "N") +
    " V:" + (orientation.flipV ? "Y" : "N");
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
      // Merge OB mask back into grid for saving
      let binary = "";
      for (let i = 0; i < zoneGrid.length; i++)
        binary += String.fromCharCode(obMask && obMask[i] ? 9 : treesMask && treesMask[i] ? 5 : zoneGrid[i]);
      const gridBase64 = btoa(binary);

      await fetch("/api/zones", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          courseId: COURSE_ID,
          holeNumber: currentHole.number,
          width: zoneGridW,
          height: zoneGridH,
          grid: gridBase64,
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
