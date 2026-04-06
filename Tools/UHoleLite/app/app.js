// UHole Lite — Hole Viewer & Orientation Editor

const COURSE_ID = "lomond-country-club";

let courseData = null;
let currentHole = null;
let currentView = "illustration"; // "illustration" | "zones" | "both"
let overlayOpacity = 0.5;

// Per-hole orientation state
let orientation = { rotation: 0, flipH: false, flipV: false };

// Loaded images
let illustrationImg = null;
let zonesImg = null;

const canvas = document.getElementById("hole-canvas");
const ctx = canvas.getContext("2d");

// ── Init ─────────────────────────────────────────────

async function init() {
  const res = await fetch(`/api/course?id=${COURSE_ID}`);
  courseData = await res.json();

  document.getElementById("course-name").textContent =
    `${courseData.course.display_name} — ${courseData.course.native_name}`;

  buildHoleList();
  setupControls();

  // Load hole 1 by default
  selectHole(1);
}

function buildHoleList() {
  const nav = document.getElementById("hole-list");
  nav.innerHTML = "";

  for (const hole of courseData.holes) {
    const courseHole = courseData.course.holes.find(h => h.number === hole.number);
    const par = courseHole?.par ?? "?";
    const btn = document.createElement("button");
    btn.className = "hole-btn";
    btn.dataset.hole = hole.number;
    btn.innerHTML = `Hole ${hole.number}<span class="par">Par ${par}</span>`;
    btn.addEventListener("click", () => selectHole(hole.number));
    nav.appendChild(btn);
  }
}

// ── Hole Selection ───────────────────────────────────

async function selectHole(holeNumber) {
  currentHole = courseData.holes.find(h => h.number === holeNumber);
  if (!currentHole) return;

  // Update sidebar active state
  document.querySelectorAll(".hole-btn").forEach(b => {
    b.classList.toggle("active", Number(b.dataset.hole) === holeNumber);
  });

  // Update header
  const courseHole = courseData.course.holes.find(h => h.number === holeNumber);
  document.getElementById("hole-title").textContent = `Hole ${holeNumber}`;

  const tees = courseHole?.tees || {};
  document.getElementById("hole-info").textContent =
    `Par ${courseHole?.par} · HDCP ${courseHole?.hdcp} · ` +
    `Back ${tees.back?.yards}y · Reg ${tees.regular?.yards}y · ` +
    `Front ${tees.front?.yards}y · Ladies ${tees.ladies?.yards}y`;

  // Load orientation
  const oriRes = await fetch(`/api/orientation?course=${COURSE_ID}&hole=${holeNumber}`);
  orientation = await oriRes.json();
  updateOrientationButtons();

  // Load images
  const pad = String(holeNumber).padStart(2, "0");
  illustrationImg = await loadImage(`/output/${COURSE_ID}/holes/${pad}/illustration_raw.png`);
  zonesImg = currentHole.hasZonesPng
    ? await loadImage(`/output/${COURSE_ID}/holes/${pad}/zones.png`)
    : null;

  // Update details
  updateDetails();

  // Draw
  drawCanvas();
}

function loadImage(src) {
  return new Promise((resolve) => {
    const img = new Image();
    img.onload = () => resolve(img);
    img.onerror = () => resolve(null);
    img.src = src;
  });
}

// ── Canvas Drawing ───────────────────────────────────

function drawCanvas() {
  if (!illustrationImg) return;

  const srcW = illustrationImg.width;
  const srcH = illustrationImg.height;

  // After rotation, dimensions may swap
  const isRotated90 = orientation.rotation === 90 || orientation.rotation === 270;
  const drawW = isRotated90 ? srcH : srcW;
  const drawH = isRotated90 ? srcW : srcH;

  // Fit canvas to container
  const container = document.getElementById("canvas-container");
  const containerW = container.clientWidth - 20;
  const containerH = container.clientHeight - 20;
  const scale = Math.min(containerW / drawW, containerH / drawH, 1.5);

  canvas.width = Math.round(drawW * scale);
  canvas.height = Math.round(drawH * scale);

  ctx.clearRect(0, 0, canvas.width, canvas.height);
  ctx.save();

  // Move to center, apply transforms
  ctx.translate(canvas.width / 2, canvas.height / 2);
  ctx.rotate((orientation.rotation * Math.PI) / 180);
  ctx.scale(
    orientation.flipH ? -1 : 1,
    orientation.flipV ? -1 : 1
  );

  // Draw illustration
  if (currentView === "illustration" || currentView === "both") {
    ctx.globalAlpha = currentView === "both" ? (1 - overlayOpacity) : 1;
    ctx.drawImage(illustrationImg, -srcW * scale / 2, -srcH * scale / 2, srcW * scale, srcH * scale);
  }

  // Draw zones overlay
  if ((currentView === "zones" || currentView === "both") && zonesImg) {
    ctx.globalAlpha = currentView === "both" ? overlayOpacity : 1;
    ctx.drawImage(zonesImg, -srcW * scale / 2, -srcH * scale / 2, srcW * scale, srcH * scale);
  }

  ctx.globalAlpha = 1;

  // Draw tee markers
  if (currentHole.tees?.tees) {
    const teeColors = {
      tee_back: "#4488ff",
      tee_regular: "#44cc44",
      tee_front: "#ffffff",
      tee_ladies: "#ff4444",
    };

    for (const tee of currentHole.tees.tees) {
      const px = (tee.normalized.x - 0.5) * srcW * scale;
      const py = (tee.normalized.y - 0.5) * srcH * scale;

      ctx.beginPath();
      ctx.arc(px, py, 6, 0, Math.PI * 2);
      ctx.fillStyle = teeColors[tee.type] || "#ffff00";
      ctx.fill();
      ctx.strokeStyle = "#000";
      ctx.lineWidth = 1.5;
      ctx.stroke();
    }
  }

  ctx.restore();

  // Draw orientation indicator in corner (unrotated)
  ctx.save();
  ctx.font = "11px monospace";
  ctx.fillStyle = "#4ecca3";
  ctx.textAlign = "left";
  const label = `rot:${orientation.rotation}° flipH:${orientation.flipH} flipV:${orientation.flipV}`;
  ctx.fillText(label, 8, canvas.height - 8);
  ctx.restore();
}

// ── Controls ─────────────────────────────────────────

function setupControls() {
  // View buttons
  document.querySelectorAll(".view-btn").forEach(btn => {
    btn.addEventListener("click", () => {
      document.querySelectorAll(".view-btn").forEach(b => b.classList.remove("active"));
      btn.classList.add("active");
      currentView = btn.dataset.view;
      drawCanvas();
    });
  });

  // Rotation
  document.getElementById("btn-rotate-ccw").addEventListener("click", () => {
    orientation.rotation = (orientation.rotation + 270) % 360;
    updateOrientationButtons();
    drawCanvas();
  });

  document.getElementById("btn-rotate-cw").addEventListener("click", () => {
    orientation.rotation = (orientation.rotation + 90) % 360;
    updateOrientationButtons();
    drawCanvas();
  });

  // Flip
  document.getElementById("btn-flip-h").addEventListener("click", () => {
    orientation.flipH = !orientation.flipH;
    updateOrientationButtons();
    drawCanvas();
  });

  document.getElementById("btn-flip-v").addEventListener("click", () => {
    orientation.flipV = !orientation.flipV;
    updateOrientationButtons();
    drawCanvas();
  });

  // Reset
  document.getElementById("btn-reset").addEventListener("click", () => {
    orientation = { rotation: 0, flipH: false, flipV: false };
    updateOrientationButtons();
    drawCanvas();
  });

  // Overlay opacity
  const slider = document.getElementById("overlay-opacity");
  slider.addEventListener("input", () => {
    overlayOpacity = slider.value / 100;
    document.getElementById("opacity-label").textContent = `${slider.value}%`;
    drawCanvas();
  });

  // Save
  document.getElementById("btn-save").addEventListener("click", saveOrientation);

  // Resize handler
  window.addEventListener("resize", drawCanvas);
}

function updateOrientationButtons() {
  document.getElementById("btn-flip-h").classList.toggle("active", orientation.flipH);
  document.getElementById("btn-flip-v").classList.toggle("active", orientation.flipV);
}

async function saveOrientation() {
  if (!currentHole) return;

  const statusEl = document.getElementById("save-status");
  statusEl.textContent = "Saving...";

  const res = await fetch("/api/orientation", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      courseId: COURSE_ID,
      holeNumber: currentHole.number,
      orientation,
    }),
  });

  const result = await res.json();
  statusEl.textContent = result.ok ? "✓ Saved" : "✗ Error";
  setTimeout(() => { statusEl.textContent = ""; }, 2000);
}

// ── Details Panel ────────────────────────────────────

function updateDetails() {
  // Tee info
  const teeEl = document.getElementById("tee-info");
  if (currentHole.tees?.tees) {
    const tees = currentHole.tees.tees;
    teeEl.innerHTML = tees.map(t =>
      `<span class="tee-${t.type.replace("tee_", "")}">${t.color} (${t.yards}y)</span>`
    ).join(" · ");
  } else {
    teeEl.textContent = "No tee data";
  }

  // Terrain info
  const terrainEl = document.getElementById("terrain-info");
  if (currentHole.terrainMeta) {
    const tm = currentHole.terrainMeta;
    terrainEl.textContent =
      `Terrain: ${tm.terrain_width_m?.toFixed(0)}×${tm.terrain_length_m?.toFixed(0)}m · ` +
      `Elev: 0–${tm.max_elevation_m?.toFixed(1)}m · ` +
      `Hints: ${tm.hints?.join(", ") || "none"}`;
  } else {
    terrainEl.textContent = "No terrain data";
  }

  // Zone stats
  const zoneEl = document.getElementById("zone-stats");
  if (currentHole.zoneStats?.zone_stats) {
    const stats = currentHole.zoneStats.zone_stats;
    const top = Object.entries(stats)
      .filter(([, v]) => v.percentage > 1)
      .sort((a, b) => b[1].percentage - a[1].percentage)
      .map(([k, v]) => `${k}: ${v.percentage.toFixed(1)}%`)
      .join(" · ");
    zoneEl.textContent = `Zones: ${top}`;
  } else {
    zoneEl.textContent = "No zone data";
  }
}

// ── Start ────────────────────────────────────────────

init();
