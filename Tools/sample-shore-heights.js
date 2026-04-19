'use strict';
const fs   = require('fs');
const path = require('path');

const holesRoot  = 'C:/Users/cesar/GolfinRedux/Tools/UHoleGeo/output/lomond-country-club/holes';
const exportRoot = 'C:/Users/cesar/GolfinRedux/Tools/UHoleGeo/output/lomond-country-club/export';

const OFFSETS_M      = [1, 2, 5, 10];
const MAX_RAMP_SLOPE = 0.35;   // same constant as tee adaptive-radius fix

// ── helpers ──────────────────────────────────────────────────────────────────

function inPoly(px, pz, poly) {
  let inside = false;
  for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
    if (((poly[i].z > pz) !== (poly[j].z > pz)) &&
        (px < (poly[j].x - poly[i].x) * (pz - poly[i].z) / (poly[j].z - poly[i].z) + poly[i].x))
      inside = !inside;
  }
  return inside;
}

function normalize2(dx, dz) {
  const len = Math.sqrt(dx * dx + dz * dz);
  if (len < 1e-9) return { dx: 0, dz: 0 };
  return { dx: dx / len, dz: dz / len };
}

function percentile(sorted, p) {
  const idx = Math.floor(sorted.length * p);
  return sorted[Math.min(idx, sorted.length - 1)];
}

function dropStats(arr) {
  if (arr.length === 0) return null;
  const s = [...arr].sort((a, b) => a - b);
  return {
    min:    s[0],
    median: percentile(s, 0.5),
    p90:    percentile(s, 0.9),
    max:    s[s.length - 1],
    count:  s.length,
  };
}

// ── per-course accumulator ────────────────────────────────────────────────────

const courseResults = [];   // { hole, bodyId, maxDrop, dRNeeded }

// ── main loop ─────────────────────────────────────────────────────────────────

for (let n = 1; n <= 18; n++) {
  const pad   = String(n).padStart(2, '0');
  const wpath = path.join(exportRoot, `hole-${pad}`, 'water.json');
  if (!fs.existsSync(wpath)) continue;

  const waterData = JSON.parse(fs.readFileSync(wpath, 'utf8'));
  const bodies    = (waterData.water || []).filter(b => b.contour && b.contour.length >= 3);
  if (bodies.length === 0) continue;

  // ── terrain meta ──────────────────────────────────────────────────────────
  const metaPath = path.join(holesRoot, pad, 'terrain-meta.json');
  if (!fs.existsSync(metaPath)) {
    console.warn(`WARNING: terrain-meta.json not found for hole ${pad} — skipping`);
    continue;
  }
  const meta = JSON.parse(fs.readFileSync(metaPath, 'utf8'));
  const TW  = meta.terrain_width_m;
  const TL  = meta.terrain_length_m;
  const ER  = meta.max_elevation_m;   // elev_range_m: min is always 0
  const RES = meta.resolution;

  // ── heightmap ─────────────────────────────────────────────────────────────
  const rawPath = path.join(holesRoot, pad, 'heightmap.raw');
  const raw     = fs.readFileSync(rawPath);

  const cellW = TW / (RES - 1);
  const cellH = TL / (RES - 1);

  function h(row, col) {
    if (row < 0 || row >= RES || col < 0 || col >= RES) return null;
    return (raw.readUInt16BE((row * RES + col) * 2) / 65535) * ER;
  }

  function sampleWorld(wx, wz) {
    const col = Math.round((wx + TW / 2) / cellW);
    const row = Math.round((wz + TL / 2) / cellH);
    return h(row, col);
  }

  console.log(`\n=== Hole ${n} ===`);
  console.log(`Terrain: ${TW.toFixed(1)}m × ${TL.toFixed(1)}m, elev range ${ER.toFixed(1)}m`);

  let holeMaxDrop  = 0;
  let holeDRNeeded = 0;

  for (const body of bodies) {
    const contour = body.contour;
    const N       = contour.length;

    // ── 1. minTerrainH inside polygon (mirrors importer's nearSurfY) ──────
    let bboxMinX = Infinity, bboxMaxX = -Infinity;
    let bboxMinZ = Infinity, bboxMaxZ = -Infinity;
    for (const p of contour) {
      if (p.x < bboxMinX) bboxMinX = p.x;
      if (p.x > bboxMaxX) bboxMaxX = p.x;
      if (p.z < bboxMinZ) bboxMinZ = p.z;
      if (p.z > bboxMaxZ) bboxMaxZ = p.z;
    }
    const rowLo = Math.max(0, Math.floor((bboxMinZ + TL / 2) / cellH));
    const rowHi = Math.min(RES - 1, Math.ceil((bboxMaxZ + TL / 2) / cellH));
    const colLo = Math.max(0, Math.floor((bboxMinX + TW / 2) / cellW));
    const colHi = Math.min(RES - 1, Math.ceil((bboxMaxX + TW / 2) / cellW));

    let minTerrainH    = Infinity;
    let interiorCells  = 0;
    for (let row = rowLo; row <= rowHi; row++) {
      const wz = -TL / 2 + row * cellH;
      for (let col = colLo; col <= colHi; col++) {
        const wx = -TW / 2 + col * cellW;
        if (inPoly(wx, wz, contour)) {
          const v = h(row, col);
          if (v !== null) {
            if (v < minTerrainH) minTerrainH = v;
            interiorCells++;
          }
        }
      }
    }
    if (minTerrainH === Infinity) {
      console.log(`\n  Water body ${body.id} — no interior cells found, skipping`);
      continue;
    }
    const nearSurfY = minTerrainH - 0.05;   // matches DepressTerrainUnderOverlays

    // ── 2. outward normal sampling at each contour vertex ─────────────────
    const dropsByOffset = OFFSETS_M.map(() => []);
    let sampledVerts    = 0;

    for (let i = 0; i < N; i++) {
      const prev = contour[(i - 1 + N) % N];
      const next = contour[(i + 1) % N];
      const p    = contour[i];

      // tangent ≈ next − prev; rotate 90° CCW: (−tz, tx)
      const { dx: nx, dz: nz } = normalize2(-(next.z - prev.z), next.x - prev.x);

      // ensure normal points outward: test p + 0.5m × normal is outside poly
      let outX = nx, outZ = nz;
      if (inPoly(p.x + 0.5 * nx, p.z + 0.5 * nz, contour)) {
        outX = -nx;
        outZ = -nz;
      }

      let vertexSampled = false;
      for (let oi = 0; oi < OFFSETS_M.length; oi++) {
        const off = OFFSETS_M[oi];
        const sh  = sampleWorld(p.x + outX * off, p.z + outZ * off);
        if (sh === null) continue;            // outside heightmap
        const drop = sh - nearSurfY;
        if (drop < 0) continue;              // below water surface — discard
        dropsByOffset[oi].push(drop);
        vertexSampled = true;
      }
      if (vertexSampled) sampledVerts++;
    }

    // ── 3. report ─────────────────────────────────────────────────────────
    console.log(`\n  Water body ${body.id} (${interiorCells} interior cells, ${N} contour verts):`);
    console.log(`    nearSurfY = ${nearSurfY.toFixed(2)}m  (minTerrainH inside = ${minTerrainH.toFixed(2)}m)`);
    console.log(`    Outward sampling (${N} verts, ${sampledVerts} sampled):`);
    console.log(`      offset  min     median  p90     max`);

    // headline drop: use 5m-offset p90 (spec §3 step 4)
    let headlineDrop = 0;
    let maxBodyDrop  = 0;

    for (let oi = 0; oi < OFFSETS_M.length; oi++) {
      const off = OFFSETS_M[oi];
      const st  = dropStats(dropsByOffset[oi]);
      if (!st) {
        console.log(`      ${ (off + 'm').padEnd(6) }  (no positive-drop samples)`);
        continue;
      }
      console.log(
        `      ${ (off + 'm').padEnd(6) }` +
        `  ${ st.min.toFixed(2).padStart(5) }` +
        `   ${ st.median.toFixed(2).padStart(5) }` +
        `   ${ st.p90.toFixed(2).padStart(5) }` +
        `   ${ st.max.toFixed(2).padStart(5) }`
      );
      if (st.max > maxBodyDrop) maxBodyDrop = st.max;
      if (off === 5) headlineDrop = st.p90;
    }

    const dRNeeded = 1.5 * headlineDrop / MAX_RAMP_SLOPE;
    console.log(`    Adaptive radius needed at 5m-offset p90 drop (${ headlineDrop.toFixed(2) }m):`);
    console.log(`      dR_needed = 1.5 × ${ headlineDrop.toFixed(2) } / ${ MAX_RAMP_SLOPE } = ${ dRNeeded.toFixed(1) }m  ← cap recommendation`);

    if (maxBodyDrop > holeMaxDrop)  holeMaxDrop  = maxBodyDrop;
    if (dRNeeded   > holeDRNeeded)  holeDRNeeded = dRNeeded;

    courseResults.push({ hole: n, bodyId: body.id, maxDrop: maxBodyDrop, dRNeeded });
  }

  if (bodies.length > 1) {
    console.log(`\n  Hole ${n} summary: max drop ${holeMaxDrop.toFixed(2)}m, max dR_needed ${holeDRNeeded.toFixed(1)}m`);
  }
}

// ── COURSE SUMMARY ────────────────────────────────────────────────────────────

console.log('\n=== COURSE SUMMARY ===');

const holesWithWater = [...new Set(courseResults.map(r => r.hole))];
console.log('Holes with water: [' + holesWithWater.join(', ') + ']');

if (courseResults.length === 0) {
  console.log('No water bodies found with valid drops.');
  process.exit(0);
}

const sortedByDrop = [...courseResults].sort((a, b) => b.maxDrop - a.maxDrop);
const maxDrop      = sortedByDrop[0].maxDrop;
const maxDR        = Math.max(...courseResults.map(r => r.dRNeeded));
const maxDREntry   = courseResults.find(r => r.dRNeeded === maxDR);

console.log(`Max drop course-wide: ${maxDrop.toFixed(2)}m (Hole ${sortedByDrop[0].hole}, body ${sortedByDrop[0].bodyId})`);
console.log(`Max dR_needed:        ${maxDR.toFixed(1)}m (Hole ${maxDREntry.hole}, body ${maxDREntry.bodyId})`);

console.log('\nTop 5 worst spots:');
for (const r of sortedByDrop.slice(0, 5)) {
  console.log(`  Hole ${String(r.hole).padStart(2)}, body ${r.bodyId}: drop ${r.maxDrop.toFixed(2)}m, dR_needed ${r.dRNeeded.toFixed(1)}m`);
}

// Recommended cap: max dR_needed × 1.1, rounded up to nearest 5m
const rawCap = maxDR * 1.1;
const recCap = Math.ceil(rawCap / 5) * 5;
console.log(`\n→ Recommended ShoreMaxRadiusMeters cap for Phase 2 spec: ${recCap}m`);
console.log(`  (max dR_needed ${maxDR.toFixed(1)}m × 1.1 safety margin, rounded up to 5m)`);

if (maxDrop < 1) {
  console.log('\n⚠  Max drop < 1m course-wide — skip Phase 2, fixed 5m radius is fine.');
  console.log('   Re-investigate Hole 12 artifact (not a slope issue).');
} else if (maxDrop <= 5) {
  console.log(`\n✓  Max drop ${maxDrop.toFixed(2)}m (2–5m range) — apply Phase 2 with cap = ${recCap}m`);
} else {
  console.log(`\n✓  Max drop ${maxDrop.toFixed(2)}m (> 5m) — apply Phase 2 with cap = ${recCap}m`);
}
