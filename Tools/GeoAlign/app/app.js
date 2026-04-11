/**
 * GeoAlign — Client-side geo-alignment tool
 *
 * Single-canvas app with two composited layers:
 *   Layer 1 (bottom): Hole illustration (fixed)
 *   Layer 2 (top): GSI satellite composite (transformable)
 *   Layer 3 (overlay): Control points + UI
 */

// ── State ────────────────────────────────────────────────

let courseId = "lomond-country-club";
let courseData = null;
let currentHole = null; // { number, par, name, ... }

// Images
let illustrationImg = null;
let gsiImg = null;
let gsiMeta = null; // { width, height, bounds, zoom, meters_per_pixel }

// Illustration display (fixed, centered in canvas)
let illX = 0, illY = 0, illW = 0, illH = 0;

// GSI transform state
let gsiOffsetX = 0, gsiOffsetY = 0;
let gsiRotation = 0; // radians
let gsiScale = 1.0;
let gsiOpacity = 0.5;

// Canvas
const canvas = document.getElementById("align-canvas");
const ctx = canvas.getContext("2d");
const stage = document.getElementById("canvas-stage");

// Modes
let currentMode = "navigate"; // "navigate" | "point"

// Control points
let controlPoints = []; // { id, illPx: {x,y}, world: {lat,lon} }
let nextCpId = 1;
let pendingIllPoint = null; // {x,y} waiting for GSI click
let draggingCp = null; // { cpIndex, type: "ill"|"gsi", startX, startY }
let hoveredCp = null; // { cpIndex, type }

// Transform result
let transformResult = null; // { a, b, tx, c, d, ty, residuals, ... }

// Drag state for navigate mode
let isDragging = false;
let dragStartX = 0, dragStartY = 0;
let dragStartOffsetX = 0, dragStartOffsetY = 0;

// Saved alignment status per hole
const savedHoles = new Set();

const CP_HIT_RADIUS = 12;
const ILL_POINT_COLOR = "#ffa040";
const GSI_POINT_COLOR = "#45bcff";
const PENDING_POINT_COLOR = "#ffff40";

// ── DOM refs ─────────────────────────────────────────────

const $holeNav = document.getElementById("hole-nav");
const $courseName = document.getElementById("course-name");
const $holeTitle = document.getElementById("hole-title");
const $saveBanner = document.getElementById("save-banner");
const $infoStatus = document.getElementById("info-status");
const $infoPoints = document.getElementById("info-points");
const $infoError = document.getElementById("info-error");
const $infoSaved = document.getElementById("info-saved");
const $cpCount = document.getElementById("cp-count");
const $cpTable = document.getElementById("cp-table");
const $opacitySlider = document.getElementById("gsi-opacity");
const $opacityInput = document.getElementById("opacity-input");
const $rotationSlider = document.getElementById("gsi-rotation");
const $rotationInput = document.getElementById("rotation-input");
const $scaleSlider = document.getElementById("gsi-scale");
const $scaleInput = document.getElementById("scale-input");
const $btnApply = document.getElementById("btn-apply-transform");

// ── Init ─────────────────────────────────────────────────

async function init() {
  try {
    const resp = await fetch(`/api/course?id=${courseId}`);
    courseData = await resp.json();

    const name = courseData.name || courseId;
    const nameJa = courseData.name_ja || "";
    const holeCount = courseData.holes?.length || 18;
    $courseName.textContent = `${name}${nameJa ? ` (${nameJa})` : ""} \u00B7 ${holeCount} holes`;

    // Check which holes have saved alignments
    for (let i = 1; i <= holeCount; i++) {
      try {
        const r = await fetch(`/api/load-alignment/${courseId}/${i}`);
        if (r.ok) savedHoles.add(i);
      } catch {}
    }

    buildHoleNav();
  } catch (err) {
    $courseName.textContent = "Failed to load course data";
    console.error(err);
  }

  setupEventListeners();
  resizeCanvas();
}

function buildHoleNav() {
  $holeNav.innerHTML = "";
  const holes = courseData.holes || [];
  for (const h of holes) {
    const btn = document.createElement("button");
    btn.className = "nav-link";
    btn.dataset.hole = h.number;

    const check = savedHoles.has(h.number) ? `<span class="check-icon">\u2713</span> ` : "";
    btn.innerHTML = `${check}Hole ${h.number}<span class="par-label">Par ${h.par}</span>`;

    btn.addEventListener("click", () => selectHole(h.number));
    $holeNav.appendChild(btn);
  }
}

async function selectHole(holeNumber) {
  // Update nav
  $holeNav.querySelectorAll(".nav-link").forEach(b => {
    b.classList.toggle("is-active", Number(b.dataset.hole) === holeNumber);
  });

  const holeData = courseData.holes?.find(h => h.number === holeNumber);
  if (!holeData) return;
  currentHole = holeData;

  $holeTitle.textContent = `Hole ${holeNumber} \u2014 Par ${holeData.par}`;

  // Reset state
  controlPoints = [];
  nextCpId = 1;
  pendingIllPoint = null;
  transformResult = null;
  resetGsiTransform();

  // Load illustration
  illustrationImg = null;
  gsiImg = null;
  gsiMeta = null;

  try {
    illustrationImg = await loadImage(`/api/illustration/${courseId}/${holeNumber}`);
  } catch (err) {
    console.error("Failed to load illustration:", err);
    $infoStatus.textContent = "Illustration not found";
    $infoStatus.className = "status-poor";
  }

  // Load GSI composite (cache-bust to pick up new tiles/grid size)
  try {
    const metaResp = await fetch(`/api/gsi-composite/${courseId}/${holeNumber}?t=${Date.now()}`);
    gsiMeta = await metaResp.json();
    gsiImg = await loadImage(gsiMeta.image_url + "?t=" + Date.now());
  } catch (err) {
    console.error("Failed to load GSI composite:", err);
  }

  // Try loading existing alignment
  try {
    const alignResp = await fetch(`/api/load-alignment/${courseId}/${holeNumber}`);
    if (alignResp.ok) {
      const alignment = await alignResp.json();
      restoreAlignment(alignment);
    }
  } catch {}

  fitIllustration();
  updateUI();
  draw();
}

function restoreAlignment(alignment) {
  if (!alignment.control_points) return;

  controlPoints = alignment.control_points.map(cp => ({
    id: cp.id,
    illPx: { x: cp.illustration_px.x, y: cp.illustration_px.y },
    world: { lat: cp.world.lat, lon: cp.world.lon },
  }));
  nextCpId = Math.max(...controlPoints.map(cp => cp.id), 0) + 1;

  if (controlPoints.length >= 3) {
    computeAffineTransform();
  }
}

// ── Image loading ────────────────────────────────────────

function loadImage(url) {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.onload = () => resolve(img);
    img.onerror = () => reject(new Error(`Failed to load: ${url}`));
    img.src = url;
  });
}

// ── Canvas sizing ────────────────────────────────────────

function resizeCanvas() {
  const rect = stage.getBoundingClientRect();
  canvas.width = rect.width;
  canvas.height = rect.height;
  fitIllustration();
  draw();
}

function fitIllustration() {
  if (!illustrationImg) return;
  const pad = 20;
  const availW = canvas.width - pad * 2;
  const availH = canvas.height - pad * 2;
  const imgRatio = illustrationImg.width / illustrationImg.height;
  const availRatio = availW / availH;

  if (imgRatio > availRatio) {
    illW = availW;
    illH = availW / imgRatio;
  } else {
    illH = availH;
    illW = availH * imgRatio;
  }

  illX = (canvas.width - illW) / 2;
  illY = (canvas.height - illH) / 2;
}

// ── Drawing ──────────────────────────────────────────────

function draw() {
  ctx.clearRect(0, 0, canvas.width, canvas.height);

  // Layer 1: Illustration (fixed)
  if (illustrationImg) {
    ctx.drawImage(illustrationImg, illX, illY, illW, illH);
  }

  // Layer 2: GSI satellite (transformed, with opacity)
  // Rotation pivots around viewport center; offset is applied after rotation
  if (gsiImg) {
    const vcx = canvas.width / 2;
    const vcy = canvas.height / 2;

    ctx.save();
    ctx.globalAlpha = gsiOpacity;
    ctx.translate(vcx, vcy);
    ctx.rotate(gsiRotation);
    ctx.translate(gsiOffsetX, gsiOffsetY);
    ctx.scale(gsiScale, gsiScale);
    ctx.drawImage(gsiImg, -gsiImg.width / 2, -gsiImg.height / 2);
    ctx.restore();
  }

  // Layer 3: Control points
  drawControlPoints();
}

function drawControlPoints() {
  for (const cp of controlPoints) {
    // Illustration point (orange)
    const illCanvas = illPxToCanvas(cp.illPx.x, cp.illPx.y);
    drawPoint(illCanvas.x, illCanvas.y, ILL_POINT_COLOR, `CP${cp.id}`);

    // GSI point (blue) — transform world to canvas
    const gsiCanvas = worldToCanvas(cp.world.lat, cp.world.lon);
    if (gsiCanvas) {
      drawPoint(gsiCanvas.x, gsiCanvas.y, GSI_POINT_COLOR, `CP${cp.id}`);

      // Dashed line connecting them
      ctx.save();
      ctx.setLineDash([4, 4]);
      ctx.strokeStyle = "rgba(255,255,255,0.3)";
      ctx.lineWidth = 1;
      ctx.beginPath();
      ctx.moveTo(illCanvas.x, illCanvas.y);
      ctx.lineTo(gsiCanvas.x, gsiCanvas.y);
      ctx.stroke();
      ctx.restore();
    }
  }

  // Pending illustration point (yellow)
  if (pendingIllPoint) {
    const canvasPt = illPxToCanvas(pendingIllPoint.x, pendingIllPoint.y);
    drawPoint(canvasPt.x, canvasPt.y, PENDING_POINT_COLOR, "?");
  }
}

function drawPoint(x, y, color, label) {
  const isHovered = hoveredCp &&
    Math.hypot(x - hoveredCp.canvasX, y - hoveredCp.canvasY) < 5;

  // Outer ring
  ctx.beginPath();
  ctx.arc(x, y, isHovered ? 10 : 7, 0, Math.PI * 2);
  ctx.fillStyle = color;
  ctx.globalAlpha = 0.3;
  ctx.fill();
  ctx.globalAlpha = 1;

  // Inner dot
  ctx.beginPath();
  ctx.arc(x, y, 4, 0, Math.PI * 2);
  ctx.fillStyle = color;
  ctx.fill();
  ctx.strokeStyle = "#fff";
  ctx.lineWidth = 1.5;
  ctx.stroke();

  // Label
  ctx.font = "bold 10px 'Segoe UI', sans-serif";
  ctx.fillStyle = "#fff";
  ctx.textAlign = "center";
  ctx.fillText(label, x, y - 12);
}

// ── Coordinate transforms ────────────────────────────────

/** Illustration pixel → canvas pixel */
function illPxToCanvas(px, py) {
  if (!illustrationImg) return { x: 0, y: 0 };
  return {
    x: illX + (px / illustrationImg.width) * illW,
    y: illY + (py / illustrationImg.height) * illH,
  };
}

/** Canvas pixel → illustration pixel */
function canvasToIllPx(cx, cy) {
  if (!illustrationImg) return null;
  const px = ((cx - illX) / illW) * illustrationImg.width;
  const py = ((cy - illY) / illH) * illustrationImg.height;
  if (px < 0 || px > illustrationImg.width || py < 0 || py > illustrationImg.height) return null;
  return { x: Math.round(px), y: Math.round(py) };
}

/** Canvas pixel → GSI world coordinates (lat/lon) */
function canvasToGsiWorld(canvasX, canvasY) {
  if (!gsiImg || !gsiMeta) return null;

  // Inverse of draw transform: viewport-center → rotate → offset → scale → draw
  const vcx = canvas.width / 2;
  const vcy = canvas.height / 2;

  // 1. Undo viewport-center translation
  const dx = canvasX - vcx;
  const dy = canvasY - vcy;

  // 2. Undo rotation (around viewport center)
  const cos = Math.cos(-gsiRotation);
  const sin = Math.sin(-gsiRotation);
  const ux = dx * cos - dy * sin;
  const uy = dx * sin + dy * cos;

  // 3. Undo offset
  const ox = ux - gsiOffsetX;
  const oy = uy - gsiOffsetY;

  // 4. Undo scale → GSI pixel space (centered)
  const gsiPx = ox / gsiScale + gsiImg.width / 2;
  const gsiPy = oy / gsiScale + gsiImg.height / 2;

  if (gsiPx < 0 || gsiPx >= gsiMeta.width || gsiPy < 0 || gsiPy >= gsiMeta.height) return null;

  const lon = gsiMeta.bounds.west +
    (gsiPx / gsiMeta.width) * (gsiMeta.bounds.east - gsiMeta.bounds.west);
  const lat = gsiMeta.bounds.north -
    (gsiPy / gsiMeta.height) * (gsiMeta.bounds.north - gsiMeta.bounds.south);

  return { lat, lon };
}

/** World lat/lon → canvas pixel (via current GSI transform) */
function worldToCanvas(lat, lon) {
  if (!gsiImg || !gsiMeta) return null;

  // World → GSI composite pixel
  const gsiPx = ((lon - gsiMeta.bounds.west) / (gsiMeta.bounds.east - gsiMeta.bounds.west)) * gsiMeta.width;
  const gsiPy = ((gsiMeta.bounds.north - lat) / (gsiMeta.bounds.north - gsiMeta.bounds.south)) * gsiMeta.height;

  // Forward transform matching draw(): scale → offset → rotate → viewport-center
  const sx = (gsiPx - gsiImg.width / 2) * gsiScale + gsiOffsetX;
  const sy = (gsiPy - gsiImg.height / 2) * gsiScale + gsiOffsetY;

  const cos = Math.cos(gsiRotation);
  const sin = Math.sin(gsiRotation);
  const vcx = canvas.width / 2;
  const vcy = canvas.height / 2;

  return {
    x: vcx + sx * cos - sy * sin,
    y: vcy + sx * sin + sy * cos,
  };
}

// ── Affine transform ─────────────────────────────────────

function computeAffineTransform() {
  if (controlPoints.length < 3) {
    transformResult = null;
    $btnApply.disabled = true;
    return;
  }

  const n = controlPoints.length;

  // Build design matrix A (N x 3) and target vectors
  const A = [];
  const bLon = [];
  const bLat = [];

  for (const cp of controlPoints) {
    A.push([cp.illPx.x, cp.illPx.y, 1]);
    bLon.push(cp.world.lon);
    bLat.push(cp.world.lat);
  }

  try {
    const [a, b, tx] = leastSquares(A, bLon);
    const [c, d, ty] = leastSquares(A, bLat);

    // Compute residuals
    const residuals = [];
    let sumSqError = 0;
    let maxError = 0;

    for (let i = 0; i < n; i++) {
      const cp = controlPoints[i];
      const predLon = a * cp.illPx.x + b * cp.illPx.y + tx;
      const predLat = c * cp.illPx.x + d * cp.illPx.y + ty;
      const dLon = predLon - cp.world.lon;
      const dLat = predLat - cp.world.lat;
      const errorM = latLonToMeters(dLat, dLon);

      residuals.push({ point_id: cp.id, error_m: Math.round(errorM * 100) / 100 });
      sumSqError += errorM * errorM;
      if (errorM > maxError) maxError = errorM;
    }

    const rmsError = Math.sqrt(sumSqError / n);

    transformResult = {
      a, b, tx, c, d, ty,
      residuals,
      rms_error_m: Math.round(rmsError * 100) / 100,
      max_error_m: Math.round(maxError * 100) / 100,
      mean_error_m: Math.round((residuals.reduce((s, r) => s + r.error_m, 0) / n) * 100) / 100,
      point_count: n,
    };

    $btnApply.disabled = false;
  } catch (err) {
    console.error("Transform computation failed:", err);
    transformResult = null;
    $btnApply.disabled = true;
  }
}

function latLonToMeters(dLat, dLon) {
  const refLat = 34.91;
  const dLatM = dLat * 111320;
  const dLonM = dLon * 111320 * Math.cos(refLat * Math.PI / 180);
  return Math.sqrt(dLatM * dLatM + dLonM * dLonM);
}

// ── Linear algebra (3x3 least squares) ───────────────────

function matTMulMat(A) {
  const n = A.length;
  const result = [[0, 0, 0], [0, 0, 0], [0, 0, 0]];
  for (let i = 0; i < 3; i++) {
    for (let j = 0; j < 3; j++) {
      let sum = 0;
      for (let k = 0; k < n; k++) sum += A[k][i] * A[k][j];
      result[i][j] = sum;
    }
  }
  return result;
}

function matTMulVec(A, b) {
  const result = [0, 0, 0];
  for (let i = 0; i < 3; i++) {
    for (let j = 0; j < A.length; j++) result[i] += A[j][i] * b[j];
  }
  return result;
}

function mat3Inverse(m) {
  const [a, b, c] = [m[0][0], m[0][1], m[0][2]];
  const [d, e, f] = [m[1][0], m[1][1], m[1][2]];
  const [g, h, i] = [m[2][0], m[2][1], m[2][2]];

  const det = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
  if (Math.abs(det) < 1e-15) throw new Error("Singular matrix");

  const inv = 1 / det;
  return [
    [(e * i - f * h) * inv, (c * h - b * i) * inv, (b * f - c * e) * inv],
    [(f * g - d * i) * inv, (a * i - c * g) * inv, (c * d - a * f) * inv],
    [(d * h - e * g) * inv, (b * g - a * h) * inv, (a * e - b * d) * inv],
  ];
}

function mat3MulVec(M, v) {
  return [
    M[0][0] * v[0] + M[0][1] * v[1] + M[0][2] * v[2],
    M[1][0] * v[0] + M[1][1] * v[1] + M[1][2] * v[2],
    M[2][0] * v[0] + M[2][1] * v[1] + M[2][2] * v[2],
  ];
}

function leastSquares(A, b) {
  const AtA = matTMulMat(A);
  const Atb = matTMulVec(A, b);
  const inv = mat3Inverse(AtA);
  return mat3MulVec(inv, Atb);
}

// ── Apply transform (snap GSI to alignment) ──────────────

function applyTransform() {
  if (!transformResult || !illustrationImg || !gsiImg || !gsiMeta) return;

  // Strategy: pick two illustration points, compute where they should be
  // on canvas (via illPxToCanvas) and where they are in GSI pixel space
  // (via affine → world → gsiPx). Then solve for the GSI display params
  // (rotation, scale, offset) that map the GSI pixels to those canvas positions.
  //
  // GSI display formula:
  //   r = gsiPx - gsiCenter
  //   canvasPt = displayCenter + R(θ) * S * r
  // where displayCenter = (canvas.width/2 + offsetX, canvas.height/2 + offsetY)

  const { a, b, tx, c, d, ty } = transformResult;

  // Two reference points in illustration space
  const p1_ill = { x: illustrationImg.width * 0.3, y: illustrationImg.height * 0.3 };
  const p2_ill = { x: illustrationImg.width * 0.7, y: illustrationImg.height * 0.7 };

  // Where they should appear on canvas
  const p1_canvas = illPxToCanvas(p1_ill.x, p1_ill.y);
  const p2_canvas = illPxToCanvas(p2_ill.x, p2_ill.y);

  // Where they fall in GSI pixel space (ill → world → gsiPx)
  function illToGsiPx(px, py) {
    const lon = a * px + b * py + tx;
    const lat = c * px + d * py + ty;
    return worldToGsiPx(lat, lon);
  }

  const p1_gsi = illToGsiPx(p1_ill.x, p1_ill.y);
  const p2_gsi = illToGsiPx(p2_ill.x, p2_ill.y);

  // Vectors between the two points
  const canvasDx = p2_canvas.x - p1_canvas.x;
  const canvasDy = p2_canvas.y - p1_canvas.y;
  const gsiDx = p2_gsi.x - p1_gsi.x;
  const gsiDy = p2_gsi.y - p1_gsi.y;

  const canvasAngle = Math.atan2(canvasDy, canvasDx);
  const gsiAngle = Math.atan2(gsiDy, gsiDx);

  // Rotation: we need R(θ) to rotate the GSI vector to match the canvas vector
  const rotation = canvasAngle - gsiAngle;

  // Scale: canvas distance / GSI distance
  const canvasDist = Math.hypot(canvasDx, canvasDy);
  const gsiDist = Math.hypot(gsiDx, gsiDy);
  const scale = canvasDist / gsiDist;

  // Offset: make p1_gsi land on p1_canvas after rotation+scale
  // canvasPt = (W/2 + offX, H/2 + offY) + R(θ) * S * (gsiPx - gsiCenter)
  const gsiCx = gsiImg.width / 2;
  const gsiCy = gsiImg.height / 2;
  const r1x = p1_gsi.x - gsiCx;
  const r1y = p1_gsi.y - gsiCy;
  const cos = Math.cos(rotation);
  const sin = Math.sin(rotation);
  const mapped1X = (r1x * scale) * cos - (r1y * scale) * sin;
  const mapped1Y = (r1x * scale) * sin + (r1y * scale) * cos;

  const offsetX = p1_canvas.x - (canvas.width / 2 + mapped1X);
  const offsetY = p1_canvas.y - (canvas.height / 2 + mapped1Y);

  // Apply
  gsiRotation = rotation;
  gsiScale = scale;
  gsiOffsetX = offsetX;
  gsiOffsetY = offsetY;

  updateSliders();
  draw();
}

function worldToGsiPx(lat, lon) {
  const px = ((lon - gsiMeta.bounds.west) / (gsiMeta.bounds.east - gsiMeta.bounds.west)) * gsiMeta.width;
  const py = ((gsiMeta.bounds.north - lat) / (gsiMeta.bounds.north - gsiMeta.bounds.south)) * gsiMeta.height;
  return { x: px, y: py };
}

// ── Hit testing ──────────────────────────────────────────

function hitTestControlPoints(canvasX, canvasY) {
  // Check illustration points first (drawn on top)
  for (let i = controlPoints.length - 1; i >= 0; i--) {
    const cp = controlPoints[i];
    const illCanvas = illPxToCanvas(cp.illPx.x, cp.illPx.y);
    if (Math.hypot(canvasX - illCanvas.x, canvasY - illCanvas.y) < CP_HIT_RADIUS) {
      return { cpIndex: i, type: "ill", canvasX: illCanvas.x, canvasY: illCanvas.y };
    }
  }

  // Check GSI points
  for (let i = controlPoints.length - 1; i >= 0; i--) {
    const cp = controlPoints[i];
    const gsiCanvas = worldToCanvas(cp.world.lat, cp.world.lon);
    if (gsiCanvas && Math.hypot(canvasX - gsiCanvas.x, canvasY - gsiCanvas.y) < CP_HIT_RADIUS) {
      return { cpIndex: i, type: "gsi", canvasX: gsiCanvas.x, canvasY: gsiCanvas.y };
    }
  }

  return null;
}

// ── Event listeners ──────────────────────────────────────

function setupEventListeners() {
  window.addEventListener("resize", resizeCanvas);

  // Mode buttons
  document.querySelectorAll(".tool-mode-btn").forEach(btn => {
    btn.addEventListener("click", () => {
      currentMode = btn.dataset.mode;
      document.querySelectorAll(".tool-mode-btn").forEach(b => b.classList.remove("is-active"));
      btn.classList.add("is-active");
      canvas.className = currentMode === "point" ? "mode-point" : "";
      pendingIllPoint = null;
      draw();
    });
  });

  // Prevent sliders from jumping on track click — only allow dragging from the thumb.
  // On pointerdown: if the click is NOT on the thumb, save the current value and
  // restore it on the next input event (preventing the browser's jump-to-click).
  for (const slider of [$rotationSlider, $scaleSlider]) {
    let savedValue = null;
    slider.addEventListener("pointerdown", (e) => {
      const rect = slider.getBoundingClientRect();
      const thumbWidth = 24;
      const trackWidth = rect.width - thumbWidth;
      const min = Number(slider.min);
      const max = Number(slider.max);
      const ratio = (Number(slider.value) - min) / (max - min);
      const thumbCenter = rect.left + thumbWidth / 2 + ratio * trackWidth;
      if (Math.abs(e.clientX - thumbCenter) > thumbWidth) {
        // Clicked on track — save value so the next input event restores it
        savedValue = slider.value;
      }
    });
    slider.addEventListener("input", () => {
      if (savedValue !== null) {
        slider.value = savedValue;
        savedValue = null;
      }
    });
    slider.addEventListener("pointerup", () => { savedValue = null; });
    slider.addEventListener("pointercancel", () => { savedValue = null; });
    slider.addEventListener("mousedown", (e) => { e.stopPropagation(); });
  }

  // View sliders + number inputs (bidirectional sync)
  $opacitySlider.addEventListener("input", () => {
    gsiOpacity = $opacitySlider.value / 100;
    $opacityInput.value = $opacitySlider.value;
    draw();
  });
  $opacityInput.addEventListener("input", () => {
    const v = Math.max(0, Math.min(100, Number($opacityInput.value) || 0));
    gsiOpacity = v / 100;
    $opacitySlider.value = v;
    draw();
  });

  $rotationSlider.addEventListener("input", () => {
    gsiRotation = ($rotationSlider.value * Math.PI) / 180;
    $rotationInput.value = Number($rotationSlider.value).toFixed(1);
    draw();
  });
  $rotationInput.addEventListener("input", () => {
    const v = Math.max(-30, Math.min(30, Number($rotationInput.value) || 0));
    gsiRotation = (v * Math.PI) / 180;
    $rotationSlider.value = v;
    draw();
  });

  $scaleSlider.addEventListener("input", () => {
    gsiScale = $scaleSlider.value / 100;
    $scaleInput.value = gsiScale.toFixed(2);
    draw();
  });
  $scaleInput.addEventListener("input", () => {
    const v = Math.max(0.1, Math.min(5, Number($scaleInput.value) || 1));
    gsiScale = v;
    $scaleSlider.value = Math.round(v * 100);
    draw();
  });

  // Reset button
  document.getElementById("btn-reset-view").addEventListener("click", () => {
    resetGsiTransform();
    updateSliders();
    draw();
  });

  // Apply transform
  $btnApply.addEventListener("click", applyTransform);

  // Save / Load / Clear / Re-fetch
  document.getElementById("btn-save").addEventListener("click", saveAlignment);
  document.getElementById("btn-load").addEventListener("click", loadAlignment);
  document.getElementById("btn-clear").addEventListener("click", clearAlignment);
  document.getElementById("btn-refetch").addEventListener("click", refetchTiles);

  // Save / Load position
  document.getElementById("btn-save-pos").addEventListener("click", savePosition);
  document.getElementById("btn-load-pos").addEventListener("click", loadPosition);

  // Canvas mouse events
  canvas.addEventListener("mousedown", onCanvasMouseDown);
  canvas.addEventListener("mousemove", onCanvasMouseMove);
  canvas.addEventListener("mouseup", onCanvasMouseUp);
  canvas.addEventListener("mouseleave", onCanvasMouseUp);
  canvas.addEventListener("wheel", onCanvasWheel, { passive: false });
  canvas.addEventListener("contextmenu", onCanvasContextMenu);

  // Keyboard
  window.addEventListener("keydown", onKeyDown);
}

function onCanvasMouseDown(e) {
  const rect = canvas.getBoundingClientRect();
  const x = e.clientX - rect.left;
  const y = e.clientY - rect.top;

  if (currentMode === "point") {
    // Check if clicking on an existing control point (for dragging)
    const hit = hitTestControlPoints(x, y);
    if (hit) {
      draggingCp = { ...hit, startX: x, startY: y };
      canvas.className = "dragging-cp";
      return;
    }

    // Place a new point
    if (!pendingIllPoint) {
      // First click: illustration point
      const illPx = canvasToIllPx(x, y);
      if (illPx) {
        pendingIllPoint = illPx;
        draw();
      }
    } else {
      // Second click: GSI point
      const world = canvasToGsiWorld(x, y);
      if (world) {
        controlPoints.push({
          id: nextCpId++,
          illPx: { ...pendingIllPoint },
          world,
        });
        pendingIllPoint = null;

        if (controlPoints.length >= 3) {
          computeAffineTransform();
        }
        updateUI();
        draw();
      }
    }
    return;
  }

  // Navigate mode: start drag
  if (currentMode === "navigate") {
    isDragging = true;
    dragStartX = x;
    dragStartY = y;
    dragStartOffsetX = gsiOffsetX;
    dragStartOffsetY = gsiOffsetY;
    canvas.className = "dragging";
  }
}

function onCanvasMouseMove(e) {
  const rect = canvas.getBoundingClientRect();
  const x = e.clientX - rect.left;
  const y = e.clientY - rect.top;

  // Dragging a control point
  if (draggingCp) {
    const cp = controlPoints[draggingCp.cpIndex];
    if (draggingCp.type === "ill") {
      const illPx = canvasToIllPx(x, y);
      if (illPx) {
        cp.illPx = illPx;
        if (controlPoints.length >= 3) computeAffineTransform();
        updateUI();
        draw();
      }
    } else {
      const world = canvasToGsiWorld(x, y);
      if (world) {
        cp.world = world;
        if (controlPoints.length >= 3) computeAffineTransform();
        updateUI();
        draw();
      }
    }
    return;
  }

  // Navigate mode: dragging GSI layer
  // Screen-space drag delta must be converted to offset space (pre-rotation)
  if (isDragging && currentMode === "navigate") {
    const sdx = x - dragStartX;
    const sdy = y - dragStartY;
    const cos = Math.cos(-gsiRotation);
    const sin = Math.sin(-gsiRotation);
    gsiOffsetX = dragStartOffsetX + (sdx * cos - sdy * sin);
    gsiOffsetY = dragStartOffsetY + (sdx * sin + sdy * cos);
    draw();
    return;
  }

  // Hover detection for control points
  if (currentMode === "point") {
    const hit = hitTestControlPoints(x, y);
    if (hit) {
      hoveredCp = hit;
      canvas.className = "hovering-cp";
    } else {
      hoveredCp = null;
      canvas.className = "mode-point";
    }
    draw();
  }
}

function onCanvasMouseUp() {
  isDragging = false;
  draggingCp = null;
  if (currentMode === "navigate") {
    canvas.className = "";
  } else {
    canvas.className = hoveredCp ? "hovering-cp" : "mode-point";
  }
}

function onCanvasWheel(e) {
  if (currentMode !== "navigate") return;
  e.preventDefault();

  const rect = canvas.getBoundingClientRect();
  const mouseX = e.clientX - rect.left;
  const mouseY = e.clientY - rect.top;

  const oldScale = gsiScale;
  const factor = e.deltaY > 0 ? 0.95 : 1.05;
  const newScale = Math.max(0.1, Math.min(5, oldScale * factor));

  // Adjust offset so the point under the mouse stays fixed.
  // Transform: canvas = vcx + R(θ) * (S * local + offset)
  // For fixed point P: offset' = offset + (S - S') * local
  // where local = (R(-θ) * (P - vcx) - offset) / S
  const vcx = canvas.width / 2;
  const vcy = canvas.height / 2;
  const cos = Math.cos(-gsiRotation);
  const sin = Math.sin(-gsiRotation);
  const dx = mouseX - vcx;
  const dy = mouseY - vcy;
  const ux = dx * cos - dy * sin;
  const uy = dx * sin + dy * cos;
  const localX = (ux - gsiOffsetX) / oldScale;
  const localY = (uy - gsiOffsetY) / oldScale;

  gsiScale = newScale;
  gsiOffsetX += (oldScale - newScale) * localX;
  gsiOffsetY += (oldScale - newScale) * localY;

  $scaleSlider.value = Math.round(gsiScale * 100);
  $scaleInput.value = gsiScale.toFixed(2);
  draw();
}

function onCanvasContextMenu(e) {
  e.preventDefault();
  const rect = canvas.getBoundingClientRect();
  const x = e.clientX - rect.left;
  const y = e.clientY - rect.top;

  const hit = hitTestControlPoints(x, y);
  if (hit) {
    deleteControlPoint(hit.cpIndex);
  }
}

function onKeyDown(e) {
  // Don't handle keys when typing in an input
  if (e.target.tagName === "INPUT") return;

  if (e.key === "Escape") {
    pendingIllPoint = null;
    draw();
    return;
  }
  if (e.key === "n" || e.key === "N") {
    document.querySelector('.tool-mode-btn[data-mode="navigate"]').click();
    return;
  }
  if (e.key === "p" || e.key === "P") {
    document.querySelector('.tool-mode-btn[data-mode="point"]').click();
    return;
  }
  if ((e.key === "s" || e.key === "S") && (e.ctrlKey || e.metaKey)) {
    e.preventDefault();
    saveAlignment();
    return;
  }
  if (e.key === "Delete" && hoveredCp) {
    deleteControlPoint(hoveredCp.cpIndex);
    return;
  }

  // WASD — pan GSI layer (screen-space directions, rotated into offset space)
  const panStep = e.shiftKey ? 50 : 10;
  const pc = Math.cos(-gsiRotation), ps = Math.sin(-gsiRotation);
  if (e.key === "w" || e.key === "W") { gsiOffsetX +=  ps * panStep; gsiOffsetY -= pc * panStep; updateSliders(); draw(); return; }
  if (e.key === "s" || e.key === "S") { gsiOffsetX += -ps * panStep; gsiOffsetY += pc * panStep; updateSliders(); draw(); return; }
  if (e.key === "a" || e.key === "A") { gsiOffsetX -= pc * panStep; gsiOffsetY -= ps * panStep; updateSliders(); draw(); return; }
  if (e.key === "d" || e.key === "D") { gsiOffsetX += pc * panStep; gsiOffsetY += ps * panStep; updateSliders(); draw(); return; }

  // Q/E — rotate GSI layer
  const rotStep = e.shiftKey ? 1.0 : 0.2; // degrees
  if (e.key === "q" || e.key === "Q") {
    gsiRotation -= (rotStep * Math.PI) / 180;
    updateSliders();
    draw();
    return;
  }
  if (e.key === "e" || e.key === "E") {
    gsiRotation += (rotStep * Math.PI) / 180;
    updateSliders();
    draw();
    return;
  }
}

// ── Control point management ─────────────────────────────

function deleteControlPoint(index) {
  controlPoints.splice(index, 1);
  if (controlPoints.length >= 3) {
    computeAffineTransform();
  } else {
    transformResult = null;
    $btnApply.disabled = true;
  }
  hoveredCp = null;
  updateUI();
  draw();
}

function resetGsiTransform() {
  gsiOffsetX = 0;
  gsiOffsetY = 0;
  gsiRotation = 0;
  gsiScale = 1.0;
  gsiOpacity = 0.5;
}

function updateSliders() {
  $opacitySlider.value = Math.round(gsiOpacity * 100);
  $opacityInput.value = Math.round(gsiOpacity * 100);
  $rotationSlider.value = (gsiRotation * 180 / Math.PI).toFixed(1);
  $rotationInput.value = (gsiRotation * 180 / Math.PI).toFixed(1);
  $scaleSlider.value = Math.round(gsiScale * 100);
  $scaleInput.value = gsiScale.toFixed(2);
}

// ── UI updates ───────────────────────────────────────────

function updateUI() {
  // CP count chip
  $cpCount.textContent = `CP: ${controlPoints.length}`;

  // Sidebar info
  if (!currentHole) {
    $infoStatus.textContent = "Select a hole to begin.";
    $infoStatus.className = "status-none";
    $infoPoints.textContent = "";
    $infoError.textContent = "";
    $infoSaved.textContent = "";
  } else if (controlPoints.length === 0) {
    $infoStatus.textContent = "Not aligned";
    $infoStatus.className = "status-none";
    $infoPoints.textContent = "";
    $infoError.textContent = "";
  } else if (controlPoints.length < 3) {
    $infoStatus.textContent = `Aligning (${controlPoints.length} points)`;
    $infoStatus.className = "status-fair";
    $infoPoints.textContent = `Points: ${controlPoints.length} (need 3+)`;
    $infoError.textContent = "";
  } else if (transformResult) {
    const rms = transformResult.rms_error_m;
    let statusText, statusClass;
    if (rms < 2) { statusText = "Aligned \u2713 (Good)"; statusClass = "status-good"; }
    else if (rms < 5) { statusText = "Aligned (Fair)"; statusClass = "status-fair"; }
    else { statusText = "Aligned (Poor)"; statusClass = "status-poor"; }

    $infoStatus.textContent = statusText;
    $infoStatus.className = statusClass;
    $infoPoints.textContent = `Points: ${controlPoints.length}`;
    $infoError.textContent = `RMS Error: ${rms}m`;
    $infoError.className = statusClass;
  }

  // Control points table
  updateCpTable();
}

function updateCpTable() {
  // Remove all rows (keep header)
  const rows = $cpTable.querySelectorAll(".cp-row");
  rows.forEach(r => r.remove());

  for (let i = 0; i < controlPoints.length; i++) {
    const cp = controlPoints[i];
    const residual = transformResult?.residuals?.find(r => r.point_id === cp.id);
    const errorM = residual ? residual.error_m : null;

    let errorClass = "";
    let errorText = "\u2014";
    if (errorM !== null) {
      if (errorM < 2) errorClass = "cp-error-good";
      else if (errorM < 5) errorClass = "cp-error-fair";
      else errorClass = "cp-error-poor";
      errorText = `${errorM.toFixed(2)}m`;
    }

    const row = document.createElement("div");
    row.className = "cp-row";
    row.innerHTML = `
      <span class="cp-id">CP${cp.id}</span>
      <span class="cp-ill">(${cp.illPx.x}, ${cp.illPx.y})</span>
      <span class="cp-world">(${cp.world.lat.toFixed(6)}, ${cp.world.lon.toFixed(6)})</span>
      <span class="${errorClass}">${errorText}</span>
      <button class="cp-delete" data-index="${i}" title="Delete">\u00D7</button>
    `;

    row.querySelector(".cp-delete").addEventListener("click", () => deleteControlPoint(i));
    $cpTable.appendChild(row);
  }
}

// ── Save / Load / Clear ──────────────────────────────────

async function saveAlignment() {
  if (!currentHole || controlPoints.length === 0) return;

  const data = {
    schema_version: "1.0.0",
    course_id: courseId,
    hole_number: currentHole.number,
    gsi_zoom: gsiMeta?.zoom || 18,
    illustration_dimensions: illustrationImg ? {
      width: illustrationImg.width,
      height: illustrationImg.height,
    } : null,
    control_points: controlPoints.map(cp => ({
      id: cp.id,
      illustration_px: { x: cp.illPx.x, y: cp.illPx.y },
      world: { lat: cp.world.lat, lon: cp.world.lon },
    })),
    transform: transformResult ? {
      method: "least_squares_affine",
      coefficients: {
        a: transformResult.a,
        b: transformResult.b,
        tx: transformResult.tx,
        c: transformResult.c,
        d: transformResult.d,
        ty: transformResult.ty,
      },
      residuals: transformResult.residuals,
      mean_residual_m: transformResult.mean_error_m,
      max_residual_m: transformResult.max_error_m,
      point_count: transformResult.point_count,
    } : null,
    terrain_bounds_latlon: computeTerrainBounds(),
    saved_at: new Date().toISOString(),
  };

  try {
    const resp = await fetch(`/api/save-alignment/${courseId}/${currentHole.number}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(data),
    });
    const result = await resp.json();
    if (result.ok) {
      showBanner(`Saved \u2713 \u2014 ${new Date().toLocaleTimeString()}`);
      savedHoles.add(currentHole.number);
      $infoSaved.textContent = `Last saved: ${new Date().toLocaleTimeString()}`;
      buildHoleNav();
      // Re-highlight active hole
      $holeNav.querySelectorAll(".nav-link").forEach(b => {
        b.classList.toggle("is-active", Number(b.dataset.hole) === currentHole.number);
      });
    }
  } catch (err) {
    console.error("Save failed:", err);
    showBanner("Save failed: " + err.message);
  }
}

async function loadAlignment() {
  if (!currentHole) return;

  try {
    const resp = await fetch(`/api/load-alignment/${courseId}/${currentHole.number}`);
    if (!resp.ok) {
      showBanner("No saved alignment found");
      return;
    }
    const alignment = await resp.json();
    controlPoints = [];
    nextCpId = 1;
    pendingIllPoint = null;
    transformResult = null;

    restoreAlignment(alignment);
    updateUI();
    draw();
    showBanner("Loaded alignment");
  } catch (err) {
    console.error("Load failed:", err);
  }
}

function clearAlignment() {
  controlPoints = [];
  nextCpId = 1;
  pendingIllPoint = null;
  transformResult = null;
  $btnApply.disabled = true;
  resetGsiTransform();
  updateSliders();
  updateUI();
  draw();
}

function computeTerrainBounds() {
  if (!transformResult || !illustrationImg) return null;

  const { a, b, tx, c, d, ty } = transformResult;
  const w = illustrationImg.width;
  const h = illustrationImg.height;

  const corners = [
    [0, 0], [w, 0], [w, h], [0, h]
  ].map(([px, py]) => ({
    lon: a * px + b * py + tx,
    lat: c * px + d * py + ty,
  }));

  return {
    north: Math.max(...corners.map(c => c.lat)),
    south: Math.min(...corners.map(c => c.lat)),
    east: Math.max(...corners.map(c => c.lon)),
    west: Math.min(...corners.map(c => c.lon)),
  };
}

// ── Save / Load GSI position ─────────────────────────────

let savedPosition = null;

function savePosition() {
  savedPosition = {
    offsetX: gsiOffsetX,
    offsetY: gsiOffsetY,
    rotation: gsiRotation,
    scale: gsiScale,
    opacity: gsiOpacity,
  };
  document.getElementById("btn-load-pos").disabled = false;
  showBanner("Position saved");
}

function loadPosition() {
  if (!savedPosition) return;
  gsiOffsetX = savedPosition.offsetX;
  gsiOffsetY = savedPosition.offsetY;
  gsiRotation = savedPosition.rotation;
  gsiScale = savedPosition.scale;
  gsiOpacity = savedPosition.opacity;
  updateSliders();
  draw();
  showBanner("Position restored");
}

// ── Re-fetch tiles ───────────────────────────────────────

async function refetchTiles() {
  const btn = document.getElementById("btn-refetch");
  btn.disabled = true;
  btn.textContent = "Fetching...";
  showBanner("Downloading GSI tiles... this may take a few minutes");

  try {
    const resp = await fetch("/api/fetch-tiles");
    const result = await resp.json();
    if (result.ok) {
      showBanner(`Tiles: ${result.downloaded} new, ${result.skipped} cached, ${result.failed} failed`);
      // Reload current hole's GSI composite
      if (currentHole) {
        compositeCache_client = {};
        try {
          const metaResp = await fetch(`/api/gsi-composite/${courseId}/${currentHole.number}`);
          gsiMeta = await metaResp.json();
          gsiImg = await loadImage(gsiMeta.image_url + "?t=" + Date.now());
          draw();
        } catch {}
      }
    } else {
      showBanner(result.message || "Fetch failed");
    }
  } catch (err) {
    showBanner("Fetch failed: " + err.message);
  }

  btn.disabled = false;
  btn.textContent = "Re-fetch Tiles";
}

// ── Banner ───────────────────────────────────────────────

let bannerTimeout = null;

function showBanner(msg) {
  $saveBanner.textContent = msg;
  $saveBanner.classList.add("visible");
  if (bannerTimeout) clearTimeout(bannerTimeout);
  bannerTimeout = setTimeout(() => {
    $saveBanner.classList.remove("visible");
  }, 3000);
}

// ── Boot ─────────────────────────────────────────────────

init();
