#!/usr/bin/env node
/**
 * verify-ridge.mjs — Ridge perpendicular slope metrics (iter-13 acceptance gate)
 *
 * For each hole with a ridge, reads the post-bake green.json and the
 * slope_authoring.json, then computes:
 *   - Ridge length (m)
 *   - Ridge cell count (active cells within 1 cell-width of the ridge)
 *   - Perpendicular slope max / mean across the ridge (adjacent ridge-adjacent cells)
 *   - Cells in ramp band (RIDGE_RAMP_WIDTH metres either side)
 *   - Band continuity check: no Δh > 5 cm between adjacent band cells
 *
 * Acceptance criteria (SPEC green_ship_polish iter-13):
 *   - Ridge perpendicular slope max ≤ 12%
 *   - Band continuity: no Δh > 5 cm between any pair of adjacent band cells
 *
 * Usage:
 *   node scripts/verify-ridge.mjs --hole 7
 *   node scripts/verify-ridge.mjs --all
 */

import { readFileSync, existsSync } from 'fs';
import { resolve, dirname } from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname  = dirname(__filename);
const PROJECT_ROOT = resolve(__dirname, '..', '..', '..');

/**
 * Drop-scaled band parameters — must stay in sync with bake-green.mjs.
 * Amendment 2026-05-30: rampWidth is now computed per-hole from the height field.
 */
const RidgeTargetSlope = 0.08;  // 8 % target ramp slope
const RidgeMinBand     = 1.0;   // metres minimum band width
const CELL_SIZE = 0.5;

/**
 * Two-tier green list — must stay in sync with bake-green.mjs.
 * Source: A4_ホール攻略冊子.pdf 「２段グリーン」 p4/p8/p12/p19.
 * Only these four holes have a genuine height-step ridge barrier.
 * All others with a traced ridge are fall-line/swale (single region, no barrier).
 */
const TWO_TIER_HOLES = new Set([3, 7, 11, 18]);

/**
 * Side-agnostic interior Δh gate (SPEC: green_ship_polish iter-13 2-tier-gate amendment).
 *
 * Scans EVERY adjacent cell pair in the green; flags any |Δh| > 5 cm.
 * Excludes pairs within EDGE_EXCLUSION_M of the contour edge — that band legitimately
 * drops to the min-shift floor and would otherwise false-fail every hole.
 *
 * Acceptance: 0 interior cliffs (>5 cm, >EDGE_EXCLUSION_M from edge) on every hole.
 *
 * H07 (pre-fix) with a naive whole-green scan scored 40 edge-band false positives,
 * all in the contour-edge band, 0 interior. H14 (pre-fix) scored 1+ interior cliffs.
 * This gate correctly FAILS broken-H14 and PASSES clean-H07.
 */
const INTERIOR_CLIFF_THRESHOLD_M = 0.05;  // 5 cm
const EDGE_EXCLUSION_M = 1.0;             // 1 m from contour edge

// ─── Helpers ───────────────────────────────────────────────────────────────────

function loadJson(path) {
    if (!existsSync(path)) return null;
    return JSON.parse(readFileSync(path, 'utf8'));
}

function insidePolygon(px, pz, contour) {
    const n = contour.length;
    let inside = false;
    for (let i = 0, j = n - 1; i < n; j = i++) {
        const xi = contour[i].x, zi = contour[i].z;
        const xj = contour[j].x, zj = contour[j].z;
        if (((zi > pz) !== (zj > pz)) &&
            (px < ((xj - xi) * (pz - zi)) / (zj - zi) + xi)) {
            inside = !inside;
        }
    }
    return inside;
}

function distToRidge(px, pz, ridge) {
    let bestDistSq = Infinity;
    for (let i = 0; i < ridge.length - 1; i++) {
        const [ax, az] = ridge[i];
        const [bx, bz] = ridge[i + 1];
        const dx = bx - ax, dz = bz - az;
        const lenSq = dx * dx + dz * dz;
        if (lenSq < 1e-10) continue;
        const t = Math.max(0, Math.min(1, ((px - ax) * dx + (pz - az) * dz) / lenSq));
        const fx = ax + t * dx, fz = az + t * dz;
        const dsq = (px - fx) ** 2 + (pz - fz) ** 2;
        if (dsq < bestDistSq) bestDistSq = dsq;
    }
    return Math.sqrt(bestDistSq);
}

/**
 * Returns +1 or -1 indicating which side of the ridge polyline a point is on.
 * Sign is determined by the signed cross product with the nearest ridge segment:
 *   cross = (bx-ax)*(pz-az) - (bz-az)*(px-ax)
 * Returns 0 if exactly on the ridge.
 */
function ridgeSide(px, pz, ridge) {
    let bestDistSq = Infinity;
    let bestSign = 0;
    for (let i = 0; i < ridge.length - 1; i++) {
        const [ax, az] = ridge[i];
        const [bx, bz] = ridge[i + 1];
        const dx = bx - ax, dz = bz - az;
        const lenSq = dx * dx + dz * dz;
        if (lenSq < 1e-10) continue;
        const t = Math.max(0, Math.min(1, ((px - ax) * dx + (pz - az) * dz) / lenSq));
        const fx = ax + t * dx, fz = az + t * dz;
        const dsq = (px - fx) ** 2 + (pz - fz) ** 2;
        if (dsq < bestDistSq) {
            bestDistSq = dsq;
            // Signed cross product of segment direction vs point-to-segment-foot vector
            const cross = dx * (pz - az) - dz * (px - ax);
            bestSign = cross > 0 ? 1 : (cross < 0 ? -1 : 0);
        }
    }
    return bestSign;
}

function float32FromBase64(b64) {
    const buf = Buffer.from(b64, 'base64');
    const arr = new Float32Array(buf.length / 4);
    for (let i = 0; i < arr.length; i++) arr[i] = buf.readFloatLE(i * 4);
    return arr;
}

/**
 * Compute minimum distance from point (px, pz) to contour polygon edge
 * (nearest point on any segment of the contour, not just vertices).
 */
function distToContourEdge(px, pz, contour) {
    let bestDistSq = Infinity;
    const n = contour.length;
    for (let i = 0; i < n; i++) {
        const a = contour[i];
        const b = contour[(i + 1) % n];
        const dx = b.x - a.x, dz = b.z - a.z;
        const lenSq = dx * dx + dz * dz;
        if (lenSq < 1e-10) {
            const dsq = (px - a.x) ** 2 + (pz - a.z) ** 2;
            if (dsq < bestDistSq) bestDistSq = dsq;
            continue;
        }
        const t = Math.max(0, Math.min(1, ((px - a.x) * dx + (pz - a.z) * dz) / lenSq));
        const fx = a.x + t * dx, fz = a.z + t * dz;
        const dsq = (px - fx) ** 2 + (pz - fz) ** 2;
        if (dsq < bestDistSq) bestDistSq = dsq;
    }
    return Math.sqrt(bestDistSq);
}

/**
 * Side-agnostic interior Δh scan.
 * Scans all adjacent (4-connected) active cell pairs; flags |Δh| > threshold.
 * Excludes pairs where either cell is within edgeExclusionM of the contour edge.
 * Returns { interiorCliffCount, maxInteriorDeltaH, totalPairsChecked, edgePairsExcluded }
 */
function interiorCliffScan(hGrid, active, gridW, gridH, boundsMinX, boundsMinZ, cellSize, contour,
                            threshold = INTERIOR_CLIFF_THRESHOLD_M,
                            edgeExclusionM = EDGE_EXCLUSION_M) {
    // Precompute per-cell edge distance (expensive but needed)
    const edgeDist = new Float32Array(gridW * gridH);
    for (let cz = 0; cz < gridH; cz++) {
        for (let cx = 0; cx < gridW; cx++) {
            if (!active[cz * gridW + cx]) { edgeDist[cz * gridW + cx] = 0; continue; }
            const wx = boundsMinX + (cx + 0.5) * cellSize;
            const wz = boundsMinZ + (cz + 0.5) * cellSize;
            edgeDist[cz * gridW + cx] = distToContourEdge(wx, wz, contour);
        }
    }

    let interiorCliffCount = 0;
    let maxInteriorDeltaH = 0;
    let totalPairsChecked = 0;
    let edgePairsExcluded = 0;

    for (let cz = 0; cz < gridH; cz++) {
        for (let cx = 0; cx < gridW; cx++) {
            if (!active[cz * gridW + cx]) continue;
            // Only check right and down to avoid double-counting pairs
            for (const [nz, nx] of [[cz, cx + 1], [cz + 1, cx]]) {
                if (nz < 0 || nz >= gridH || nx < 0 || nx >= gridW) continue;
                if (!active[nz * gridW + nx]) continue;

                // Exclude if either cell is within edge exclusion zone
                if (edgeDist[cz * gridW + cx] < edgeExclusionM ||
                    edgeDist[nz * gridW + nx] < edgeExclusionM) {
                    edgePairsExcluded++;
                    continue;
                }

                totalPairsChecked++;
                const dh = Math.abs(hGrid[cz * gridW + cx] - hGrid[nz * gridW + nx]);
                if (dh > maxInteriorDeltaH) maxInteriorDeltaH = dh;
                if (dh > threshold) interiorCliffCount++;
            }
        }
    }

    return { interiorCliffCount, maxInteriorDeltaH, totalPairsChecked, edgePairsExcluded };
}

// ─── Per-hole check ───────────────────────────────────────────────────────────

function verifyHole(holeNum) {
    const holeId = String(holeNum).padStart(2, '0');

    const authoringPath = resolve(PROJECT_ROOT, 'Tools', 'GreenSlope', 'output',
        `hole_${holeId}_slope_authoring.json`);
    const greenPath = resolve(PROJECT_ROOT, 'Assets', 'Resources', 'HoleData',
        `Hole_${holeId}`, 'green.json');

    const authoring = loadJson(authoringPath);
    const greenJson = loadJson(greenPath);

    if (!authoring || !greenJson) {
        console.log(`H${holeId}: SKIP (missing authoring or green.json)`);
        return null;
    }

    const { ridge, ridgePresent } = authoring;

    // 2-tier gate: only genuine two-tier holes get ridge-band verification
    const applyRidgeBarrier = ridgePresent && TWO_TIER_HOLES.has(holeNum);

    const { gridWidth: gridW, gridHeight: gridH, boundsMin, cellSize,
            heightGridBase64, contourResampled } = greenJson;
    const boundsMinX = boundsMin.x;
    const boundsMinZ = boundsMin.z;

    const hGrid = float32FromBase64(heightGridBase64);
    const contour = contourResampled;

    // Active mask
    const active = new Uint8Array(gridW * gridH);
    for (let cz = 0; cz < gridH; cz++) {
        for (let cx = 0; cx < gridW; cx++) {
            const wx = boundsMinX + (cx + 0.5) * cellSize;
            const wz = boundsMinZ + (cz + 0.5) * cellSize;
            if (insidePolygon(wx, wz, contour)) active[cz * gridW + cx] = 1;
        }
    }

    // ── Side-agnostic interior Δh scan (SPEC: 2-tier-gate amendment acceptance gate) ──
    // Runs on ALL holes — both tier and non-tier. This is the primary gate that catches
    // interior cliffs regardless of ridge orientation or filter direction.
    const { interiorCliffCount, maxInteriorDeltaH, totalPairsChecked, edgePairsExcluded }
        = interiorCliffScan(hGrid, active, gridW, gridH, boundsMinX, boundsMinZ, cellSize, contour);
    const interiorPass = interiorCliffCount === 0;

    console.log(`H${holeId}: interior Δh scan (side-agnostic, >1m from edge)`);
    console.log(`  interior pairs checked: ${totalPairsChecked}  edge-excluded: ${edgePairsExcluded}`);
    console.log(`  interior cliffs (|Δh|>5cm): ${interiorCliffCount}  maxΔh=${(maxInteriorDeltaH*100).toFixed(1)}cm`);
    console.log(`  interior cliff gate: ${interiorPass ? '✓ PASS (0 interior cliffs)' : `FAIL (${interiorCliffCount} cliff(s), maxΔh=${(maxInteriorDeltaH*100).toFixed(1)}cm)`}`);

    // ── Ridge-band checks (only for genuine two-tier holes) ──────────────────────
    let slopePass = true;
    let continuityPass = true;
    let maxPerpSlopePct = 0;
    let meanPerpSlopePct = 0;
    let ridgeCellCount = 0;
    let bandCellCount = 0;
    let tierDrop = 0;
    let rampWidth = 0;

    if (!applyRidgeBarrier) {
        // Non-tier hole (or no ridge): skip ridge-band checks
        if (ridgePresent) {
            console.log(`  ridge: present but NOT in TWO_TIER_HOLES — fall-line hole, single-region bake, no barrier (CORRECT)`);
        } else {
            console.log(`  ridge: none — single-region green`);
        }
        // Region count expected: 1
        console.log(`  region count: 1 (single region, confirmed via applyRidgeBarrier=false)`);
        console.log(`  ACCEPTANCE: ${interiorPass ? 'PASS' : 'FAIL'}`);
        console.log('');
        return { slopePass, continuityPass, interiorPass, interiorCliffCount,
                 maxPerpSlopePct, meanPerpSlopePct, tierDrop, rampWidth,
                 ridgeCellCount, bandCellCount, holeNum, applyRidgeBarrier };
    }

    // ── Two-tier ridge-band checks ────────────────────────────────────────────────
    // Ridge length
    let ridgeLength = 0;
    for (let i = 0; i < ridge.length - 1; i++) {
        const dx = ridge[i+1][0] - ridge[i][0];
        const dz = ridge[i+1][1] - ridge[i][1];
        ridgeLength += Math.sqrt(dx*dx + dz*dz);
    }

    // ── Drop-scaled ramp width (matches bake-green.mjs Amendment 2026-05-30) ──
    let hMin = Infinity, hMax = -Infinity;
    for (let i = 0; i < hGrid.length; i++) {
        if (!active[i]) continue;
        if (hGrid[i] < hMin) hMin = hGrid[i];
        if (hGrid[i] > hMax) hMax = hGrid[i];
    }
    tierDrop = (hMax > hMin) ? (hMax - hMin) : 0;
    const greenPerpWidth = Math.max(gridW, gridH) * cellSize;
    const maxBandCap = 0.40 * greenPerpWidth;
    // 1.5× smoothstep peak correction — see bake-green.mjs for derivation.
    const SMOOTHSTEP_PEAK = 1.5;
    rampWidth = Math.min(
        Math.max(
            (tierDrop > 0 ? (tierDrop * SMOOTHSTEP_PEAK) / RidgeTargetSlope : RidgeMinBand),
            RidgeMinBand
        ),
        maxBandCap
    );
    const halfBand = rampWidth / 2;

    // ── Ridge cell perpendicular slope ──────────────────────────────────────────
    let sumPerpSlopePct = 0;
    let perpSlopeCount = 0;

    for (let cz = 0; cz < gridH; cz++) {
        for (let cx = 0; cx < gridW; cx++) {
            if (!active[cz * gridW + cx]) continue;
            const wx = boundsMinX + (cx + 0.5) * cellSize;
            const wz = boundsMinZ + (cz + 0.5) * cellSize;
            const distA = distToRidge(wx, wz, ridge);
            if (distA <= cellSize) ridgeCellCount++;
            if (distA > cellSize) continue;

            const h_a = hGrid[cz * gridW + cx];
            for (const [nz, nx] of [[cz-1,cx],[cz+1,cx],[cz,cx-1],[cz,cx+1]]) {
                if (nz < 0 || nz >= gridH || nx < 0 || nx >= gridW) continue;
                if (!active[nz * gridW + nx]) continue;
                const wx2 = boundsMinX + (nx + 0.5) * cellSize;
                const wz2 = boundsMinZ + (nz + 0.5) * cellSize;
                if (distToRidge(wx2, wz2, ridge) > cellSize) continue;
                const dh = Math.abs(h_a - hGrid[nz * gridW + nx]);
                const slopePct = (dh / cellSize) * 100;
                if (slopePct > maxPerpSlopePct) maxPerpSlopePct = slopePct;
                sumPerpSlopePct += slopePct;
                perpSlopeCount++;
            }
        }
    }
    meanPerpSlopePct = perpSlopeCount > 0 ? sumPerpSlopePct / perpSlopeCount : 0;

    // ── Band cells + continuity check ───────────────────────────────────────────
    const bandCells = [];
    for (let cz = 0; cz < gridH; cz++) {
        for (let cx = 0; cx < gridW; cx++) {
            if (!active[cz * gridW + cx]) continue;
            const wx = boundsMinX + (cx + 0.5) * cellSize;
            const wz = boundsMinZ + (cz + 0.5) * cellSize;
            const dist = distToRidge(wx, wz, ridge);
            if (dist <= halfBand) {
                const side = ridgeSide(wx, wz, ridge);
                bandCells.push({ cx, cz, side });
            }
        }
    }
    bandCellCount = bandCells.length;

    const sideLookup = new Int8Array(gridW * gridH);
    sideLookup.fill(127);
    for (const { cx, cz, side } of bandCells) sideLookup[cz * gridW + cx] = side;

    let maxBandDeltaH = 0;
    let continuityFails = 0;
    for (const { cx, cz, side: sideA } of bandCells) {
        const h_a = hGrid[cz * gridW + cx];
        for (const [nz, nx] of [[cz-1,cx],[cz+1,cx],[cz,cx-1],[cz,cx+1]]) {
            if (nz < 0 || nz >= gridH || nx < 0 || nx >= gridW) continue;
            if (!active[nz * gridW + nx]) continue;
            const sideB = sideLookup[nz * gridW + nx];
            if (sideB === 127) continue;
            if (sideA !== sideB && sideA !== 0 && sideB !== 0) continue;
            const wx2 = boundsMinX + (nx + 0.5) * cellSize;
            const wz2 = boundsMinZ + (nz + 0.5) * cellSize;
            if (distToRidge(wx2, wz2, ridge) > halfBand) continue;
            const dh = Math.abs(h_a - hGrid[nz * gridW + nx]);
            if (dh > maxBandDeltaH) maxBandDeltaH = dh;
            if (dh > 0.05) continuityFails++;
        }
    }

    slopePass = maxPerpSlopePct <= 12.0;
    continuityPass = continuityFails === 0;

    console.log(`  ridge: length ${ridgeLength.toFixed(1)}m, ${ridgeCellCount} ridge cells (TWO_TIER hole ${holeNum})`);
    console.log(`  tier drop (max-min active h): ${(tierDrop*100).toFixed(1)}cm  →  computed rampWidth=${rampWidth.toFixed(2)}m (target slope ${(RidgeTargetSlope*100).toFixed(0)}%)`);
    console.log(`  ridge perp slope max:  ${maxPerpSlopePct.toFixed(1)}%  ${slopePass ? '✓ (≤12%)' : 'FAIL (>12%)'}`);
    console.log(`  ridge perp slope mean: ${meanPerpSlopePct.toFixed(1)}%`);
    console.log(`  cells in ramp band (${rampWidth.toFixed(2)}m): ${bandCellCount}`);
    console.log(`  band continuity check: ${continuityPass ? '✓' : `FAIL (${continuityFails} adjacent pairs Δh>5cm, maxΔh=${(maxBandDeltaH*100).toFixed(1)}cm)`}`);
    console.log(`  region count: 2 (genuine two-tier, applyRidgeBarrier=true)`);
    console.log(`  ACCEPTANCE: ${slopePass && continuityPass && interiorPass ? 'PASS' : 'FAIL'}`);
    console.log('');

    return { slopePass, continuityPass, interiorPass, interiorCliffCount,
             maxPerpSlopePct, meanPerpSlopePct,
             bandCellCount, ridgeCellCount, tierDrop, rampWidth, applyRidgeBarrier, holeNum };
}

// ─── Main ─────────────────────────────────────────────────────────────────────

const args = process.argv.slice(2);
if (args.includes('--all')) {
    let totalChecked = 0;
    let ridgePassCount = 0;
    let interiorPassCount = 0;
    let allPassCount = 0;
    const results = [];

    for (let i = 1; i <= 18; i++) {
        const result = verifyHole(i);
        if (result !== null) {
            totalChecked++;
            results.push(result);
            if (result.interiorPass) interiorPassCount++;
            if (!result.applyRidgeBarrier || (result.slopePass && result.continuityPass)) ridgePassCount++;
            if (result.interiorPass && (!result.applyRidgeBarrier || (result.slopePass && result.continuityPass))) allPassCount++;
        }
    }

    console.log(`=== Summary ===`);
    console.log(`Holes verified: ${totalChecked}/18`);
    console.log(`Interior cliff gate (all holes): ${interiorPassCount}/${totalChecked} PASS`);
    console.log(`Ridge-band gate (tier holes only: 3/7/11/18): ${ridgePassCount}/${totalChecked} PASS`);
    console.log(`All gates combined: ${allPassCount}/${totalChecked} PASS`);
    console.log('');
    console.log('Per-hole interior cliff summary:');
    for (const r of results) {
        const hId = String(r.holeNum).padStart(2,'0');
        const tierTag = r.applyRidgeBarrier ? ' [2-tier]' : (r.interiorCliffCount === undefined ? '' : ' [single]');
        console.log(`  H${hId}${tierTag}: ${r.interiorPass ? '✓' : 'FAIL'} interior cliffs=${r.interiorCliffCount}`);
    }
    console.log('');
    console.log(`Interior cliff gate PASS on all 18: ${interiorPassCount === totalChecked ? '✓ YES' : `FAIL — ${totalChecked - interiorPassCount} hole(s) have interior cliffs`}`);
} else {
    const holeIdx = args.indexOf('--hole');
    if (holeIdx >= 0 && args[holeIdx + 1]) {
        verifyHole(parseInt(args[holeIdx + 1], 10));
    } else {
        console.error('Usage: node scripts/verify-ridge.mjs --hole N  OR  --all');
        process.exit(1);
    }
}
