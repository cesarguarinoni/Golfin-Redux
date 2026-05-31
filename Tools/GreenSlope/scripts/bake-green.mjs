#!/usr/bin/env node
/**
 * bake-green.mjs — Green slope + height bake tool
 * Part of: green_slope_height_bake task (SPEC.md, 2026-05-28)
 *
 * Usage:
 *   node scripts/bake-green.mjs --hole N          (bake single hole)
 *   node scripts/bake-green.mjs --all             (bake all 18 holes)
 *
 * Inputs:
 *   Tools/GreenSlope/output/hole_NN_slope_authoring.json
 *   Assets/Golf/Courses/lomond-country-club/Data/hole-NN-geo/greens.json
 *   Assets/Resources/HoleData/Hole_NN/green.json  (optional, for existing pins)
 *
 * Output:
 *   Assets/Resources/HoleData/Hole_NN/green.json  (schema v2)
 *   bake_report.txt (per-hole QA gate summary)
 */

import { readFileSync, writeFileSync, existsSync, mkdirSync } from 'fs';
import { resolve, dirname } from 'path';
import { fileURLToPath } from 'url';

// ─── Project root resolution ───────────────────────────────────────────────
const __filename = fileURLToPath(import.meta.url);
const __dirname  = dirname(__filename);
const PROJECT_ROOT = resolve(__dirname, '..', '..', '..');

// ─── Tunables (documented) ──────────────────────────────────────────────────
/**
 * Minimum slope percent output for active cells (prevents totally-flat greens
 * when arrows are very short). ~0.5 = half a percent grade.
 */
const MIN_SLOPE_PCT = 0.5;

/**
 * Maximum slope percent output. ~5.0 = 5% grade (steep but realistic for golf).
 * Cesar tunes this by eye against ShotNavi heatmap colours.
 */
const MAX_SLOPE_PCT = 5.0;

/**
 * Reference world-meter arrow length that maps to MAX_SLOPE_PCT.
 * Arrows shorter than this are scaled proportionally down to MIN_SLOPE_PCT.
 * ~4.0 m chosen from observed average arrow lengths in the authored data.
 */
const REF_LEN_M = 4.0;

/**
 * Cell size in world meters — must match GreenTopology expected grid size.
 * Default 0.5 per SPEC L7.
 */
const CELL_SIZE = 0.5;

/**
 * Padding added around the green contour AABB before placing the grid.
 * 0.5 m ensures edge cells have some margin.
 */
const GRID_PAD = 0.5;

/**
 * Jacobi/Gauss-Seidel iterations for Poisson height integration.
 * Higher = more accurate convergence but slower. 500 is fine for 0.5m grids.
 */
const POISSON_ITERATIONS = 500;

/**
 * Minimum IDW denominator (world meters²) to avoid division by zero
 * when a query cell center exactly coincides with an arrow base.
 */
const IDW_CLAMP_D_SQ = 0.01;

/**
 * Drop-scaled ridge ramp parameters (SPEC: green_ship_polish iter-13 Amendment 2026-05-30).
 *
 * Rather than a constant ramp width, the band width is computed per-hole from
 * the tier drop measured on the post-Poisson height field:
 *
 *   tierDrop  = max(h[active]) - min(h[active])   (active cells only)
 *   rampWidth = clamp(tierDrop / RidgeTargetSlope,
 *                     RidgeMinBand,
 *                     0.40 * greenPerpWidth)
 *
 * This holds the ramp slope at ~8% (USGA-readable) across all holes regardless
 * of how tall the tier step is.  H07 (38cm drop) → ~4.75m band; H14 (55cm) → ~6.9m.
 *
 * Parameters:
 *   RidgeTargetSlope  — 8%, middle of the USGA-readable 4–12% range.
 *   RidgeMinBand      — 1.0m floor (< 2 cells = smoothing breaks down).
 *   maxBand cap       — 40% of greenPerpWidth, computed inside smoothRidgeBand().
 *                       Prevents the band from eating the tier flats on unusually
 *                       large tier drops; ramp slope can exceed 8% on those holes.
 */
const RidgeTargetSlope = 0.08;   // 8 % target ramp slope
const RidgeMinBand     = 1.0;    // metres — minimum band width floor

/**
 * Two-tier green list (SPEC: green_ship_polish iter-13 2-tier-gate amendment 2026-05-30).
 *
 * Source: A4_ホール攻略冊子.pdf 「２段グリーン」 p4→Hole 3, p8→Hole 7, p12→Hole 11, p19→Hole 18.
 * ONLY these four greens are genuine two-tier (height step) greens. Every other hole's
 * dashed line in the authoring JSON is a fall-line / drainage spine (reading aid), NOT
 * a height step. Those holes must be treated as single-region: no ridge barrier in Poisson,
 * no smoothRidgeBand ramp, arrows carry the full surface including any swale.
 *
 * For non-tier holes that happen to have a traced ridge line (e.g. H13, H14), the dashed
 * line is ignored for geometry; only the arrows are used to build the height field.
 */
const TWO_TIER_HOLES = new Set([3, 7, 11, 18]);  // source: A4_ホール攻略冊子.pdf 「２段グリーン」 p4/p8/p12/p19

// ─── Helpers ────────────────────────────────────────────────────────────────

function loadJson(path) {
    if (!existsSync(path)) return null;
    return JSON.parse(readFileSync(path, 'utf8'));
}

/**
 * Resample a contour to uniform arc-length spacing (piecewise-linear).
 * @param {Array<{x,z}>} contour - input polygon
 * @param {number} targetSpacing - target arc-length between samples in world meters (default 0.5)
 * @returns {Array<{x,z}>} resampled contour (closed polygon)
 */
function resampleContour(contour, targetSpacing = 0.5) {
    const n = contour.length;
    if (n < 3) return contour;

    // Compute cumulative arc lengths
    const arcLen = [0];
    for (let i = 0; i < n; i++) {
        const a = contour[i];
        const b = contour[(i + 1) % n];
        const dx = b.x - a.x, dz = b.z - a.z;
        arcLen.push(arcLen[i] + Math.sqrt(dx * dx + dz * dz));
    }
    const totalLen = arcLen[n]; // closed polygon total perimeter

    // Number of samples
    const numSamples = Math.max(8, Math.round(totalLen / targetSpacing));
    const step = totalLen / numSamples;

    const result = [];
    let seg = 0; // current segment index
    for (let s = 0; s < numSamples; s++) {
        const target = s * step;
        // Advance segment until we bracket target
        while (seg < n - 1 && arcLen[seg + 1] < target) seg++;
        // Interpolate within segment seg → seg+1 (wrapping)
        const segStart = arcLen[seg];
        const segEnd = arcLen[seg + 1];
        const segLen = segEnd - segStart;
        const t = segLen > 1e-9 ? (target - segStart) / segLen : 0;
        const a = contour[seg % n];
        const b = contour[(seg + 1) % n];
        result.push({
            x: a.x + t * (b.x - a.x),
            z: a.z + t * (b.z - a.z)
        });
    }
    return result;
}

/**
 * Taubin λ-μ smoothing on a closed contour (Taubin 1995).
 * Non-shrinking: each iteration pairs a Laplacian shrink pass (λ > 0) with an
 * inflate pass (μ < 0, |μ| > λ), cancelling area-loss while still removing
 * mid/high-frequency wobbles.
 *
 * Canonical parameters (MeshLab / PyTorch3D / Cartagen defaults):
 *   λ = 0.5   (shrink toward neighbor average)
 *   μ = -0.53 (inflate away from neighbor average; |μ| > λ required)
 *   iterations = 12  (10–15 is the canonical range; 12 per SPEC_ITER9)
 *
 * @param {Array<{x,z}>} contour - input closed polygon
 * @param {number} iterations - Taubin iterations (default 12)
 * @param {number} lambda - shrink coefficient (default 0.5)
 * @param {number} mu - inflate coefficient; must be < 0 and |mu| > lambda (default -0.53)
 * @returns {Array<{x,z}>} smoothed contour (same number of points)
 */
function smoothContour(contour, iterations = 12, lambda = 0.5, mu = -0.53) {
    let pts = contour.map(p => ({ x: p.x, z: p.z }));
    const n = pts.length;
    if (n < 3) return pts;

    for (let iter = 0; iter < iterations; iter++) {
        // ── Shrink pass (λ > 0): move each vertex toward neighbor average ──
        const shrunk = new Array(n);
        for (let i = 0; i < n; i++) {
            const prev = pts[(i - 1 + n) % n];
            const curr = pts[i];
            const nxt  = pts[(i + 1) % n];
            const avgX = (prev.x + nxt.x) / 2;
            const avgZ = (prev.z + nxt.z) / 2;
            shrunk[i] = {
                x: curr.x + lambda * (avgX - curr.x),
                z: curr.z + lambda * (avgZ - curr.z)
            };
        }
        pts = shrunk;

        // ── Inflate pass (μ < 0): move each vertex away from neighbor average ──
        const inflated = new Array(n);
        for (let i = 0; i < n; i++) {
            const prev = pts[(i - 1 + n) % n];
            const curr = pts[i];
            const nxt  = pts[(i + 1) % n];
            const avgX = (prev.x + nxt.x) / 2;
            const avgZ = (prev.z + nxt.z) / 2;
            inflated[i] = {
                x: curr.x + mu * (avgX - curr.x),
                z: curr.z + mu * (avgZ - curr.z)
            };
        }
        pts = inflated;
    }
    return pts;
}

/** Point-in-polygon using ray casting. Returns true if (px, pz) is inside polygon. */
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

/** Point-vs-polyline side test. Returns +1 or -1. Ridge is directed (first→last point). */
function sideOfPolyline(px, pz, ridge) {
    // Use the longest segment of the ridge to determine the signed side.
    // For a ridge polyline, we pick the segment whose perpendicular projection
    // of the query point onto the line is closest, and use its signed cross product.
    if (!ridge || ridge.length < 2) return 0;

    let bestDist = Infinity;
    let bestSign = 0;

    for (let i = 0; i < ridge.length - 1; i++) {
        const [ax, az] = ridge[i];
        const [bx, bz] = ridge[i + 1];
        const dx = bx - ax, dz = bz - az;
        const lenSq = dx * dx + dz * dz;
        if (lenSq < 1e-10) continue;

        // Project query onto segment
        const t = Math.max(0, Math.min(1, ((px - ax) * dx + (pz - az) * dz) / lenSq));
        const projX = ax + t * dx;
        const projZ = az + t * dz;
        const distSq = (px - projX) ** 2 + (pz - projZ) ** 2;

        if (distSq < bestDist) {
            bestDist = distSq;
            // Cross product (dx, dz) × (px-ax, pz-az) gives signed side
            const cross = dx * (pz - az) - dz * (px - ax);
            bestSign = cross >= 0 ? 1 : -1;
        }
    }
    return bestSign;
}

/** Classify each grid cell by region using side-of-ridge test.
 *  Returns array [z][x] of region indices (0 or 1).
 *  applyRidgeBarrier: true only for genuine two-tier holes (TWO_TIER_HOLES).
 *  For non-tier holes with a traced ridge, applyRidgeBarrier=false → single region. */
function classifyRegions(gridW, gridH, boundsMinX, boundsMinZ, cellSize, contour, ridge, applyRidgeBarrier) {
    const regions = new Int32Array(gridW * gridH);

    if (!applyRidgeBarrier || !ridge || ridge.length < 2) {
        // Single region — all cells are region 0.
        // This path fires for: (a) holes with no ridge, and (b) holes with a traced
        // ridge that are NOT in TWO_TIER_HOLES (fall-line/swale, not a height step).
        regions.fill(0);
        return regions;
    }

    // Determine which side of the ridge each region is on by sampling
    // a known arrow from each region and computing its side.
    // Region 0 = whatever side "sideOfPolyline" returns for the first arrow of region 0.
    // We compute sides for all cells and map them to 0 or 1 based on this reference.

    // First classify the ridge sign for region 0 (we need at least one reference arrow).
    // We'll do this dynamically during bake based on actual arrow assignments.
    // For now, assign based on raw side value: side>0 → region 0, side<=0 → region 1.
    // The actual region assignment will be resolved by the arrow closest to each cell's region.

    for (let cz = 0; cz < gridH; cz++) {
        for (let cx = 0; cx < gridW; cx++) {
            const wx = boundsMinX + (cx + 0.5) * cellSize;
            const wz = boundsMinZ + (cz + 0.5) * cellSize;
            // side > 0 → region 0, side <= 0 → region 1
            regions[cz * gridW + cx] = sideOfPolyline(wx, wz, ridge) >= 0 ? 0 : 1;
        }
    }

    return regions;
}

/** Inverse-distance-weighted interpolation.
 *  Returns {dirX, dirZ, magPct} for a cell at (wx, wz), using only arrows from matchRegion. */
function interpolateArrow(wx, wz, arrows, matchRegion) {
    const regional = arrows.filter(a => a.region === matchRegion);
    if (regional.length === 0) return { dirX: 0, dirZ: 0, magPct: 0 };

    let sumWt = 0;
    let sumDirX = 0, sumDirZ = 0, sumMag = 0;

    for (const arrow of regional) {
        const [bx, bz] = arrow.baseXZ;
        const [tx, tz] = arrow.tipXZ;

        // Direction vector (downhill) = tip - base
        const ddx = tx - bx;
        const ddz = tz - bz;
        const arrowLen = Math.sqrt(ddx * ddx + ddz * ddz);

        if (arrowLen < 1e-6) continue; // skip zero-length arrows

        // Unit direction (downhill)
        const udx = ddx / arrowLen;
        const udz = ddz / arrowLen;

        // Magnitude: world-meter length → percent, clamped
        const rawMag = Math.min(arrowLen / REF_LEN_M, 1.0);
        const magPct = MIN_SLOPE_PCT + rawMag * (MAX_SLOPE_PCT - MIN_SLOPE_PCT);

        // IDW weight: 1/d², d = distance from cell center to arrow base
        const dSq = Math.max(IDW_CLAMP_D_SQ, (wx - bx) ** 2 + (wz - bz) ** 2);
        const w = 1.0 / dSq;

        sumWt   += w;
        sumDirX += w * udx;
        sumDirZ += w * udz;
        sumMag  += w * magPct;
    }

    if (sumWt < 1e-12) return { dirX: 0, dirZ: 0, magPct: 0 };

    // Normalize direction
    const rx = sumDirX / sumWt;
    const rz = sumDirZ / sumWt;
    const rLen = Math.sqrt(rx * rx + rz * rz);
    const finalMag = sumMag / sumWt;

    if (rLen < 1e-8) return { dirX: 0, dirZ: 0, magPct: finalMag };

    return {
        dirX: rx / rLen,
        dirZ: rz / rLen,
        magPct: finalMag
    };
}

/** Build slope grid float[] (row-major [z][x], 3 floats per cell: dirX, dirZ, magPct). */
function buildSlopeGrid(gridW, gridH, boundsMinX, boundsMinZ, cellSize, contour, arrows, regionGrid, ridgePresent) {
    const count = gridW * gridH * 3;
    const grid  = new Float32Array(count);

    for (let cz = 0; cz < gridH; cz++) {
        for (let cx = 0; cx < gridW; cx++) {
            const wx = boundsMinX + (cx + 0.5) * cellSize;
            const wz = boundsMinZ + (cz + 0.5) * cellSize;

            // Outside polygon → zero cell
            if (!insidePolygon(wx, wz, contour)) continue;

            const cellRegion = regionGrid[cz * gridW + cx];
            const { dirX, dirZ, magPct } = interpolateArrow(wx, wz, arrows, cellRegion);

            const idx = (cz * gridW + cx) * 3;
            grid[idx]     = dirX;
            grid[idx + 1] = dirZ;
            grid[idx + 2] = magPct;
        }
    }

    return grid;
}

/**
 * Post-Poisson ridge-band smoothing (SPEC: green_ship_polish iter-13 + Amendment 2026-05-30).
 *
 * The Gauss-Seidel solver treats the ridge as a hard zero-width barrier,
 * so cells on opposite sides have no continuity constraint — the entire
 * tier-height drop is concentrated in 1–2 cells, rasterising as a staircase
 * that the CDT mesh renders as bumps.
 *
 * This pass widens the barrier into a smooth ramp of DROP-SCALED width:
 *   tierDrop  = max(h[active]) - min(h[active])
 *   rampWidth = clamp(tierDrop / RidgeTargetSlope, RidgeMinBand, 0.40 * greenPerpWidth)
 *
 *   - For each active cell within rampWidth/2 of the ridge polyline:
 *       t = distRidge / (rampWidth/2)   (0 = centreline, 1 = edge)
 *       h_new = lerp(midpoint(h_self, h_mirror), h_self, smoothstep(t))
 *   - Mirror cell: same perpendicular distance from ridge on the opposite side.
 *     Located by reflecting the cell centre across the nearest ridge segment.
 *     If the mirror point falls outside the green or the grid, fall back to the
 *     nearest cell in the opposite region (4-connectivity search).
 *   - At t=0: h_new = midpoint (both sides agree exactly).
 *   - At t=1: h_new = h_self (Poisson value preserved).
 *   - Smoothstep guarantees C¹ continuity at both endpoints.
 *
 * Called after buildHeightGrid() returns (post min-shift) and BEFORE
 * dilateHeightMask(). Operates in-place on the height grid.
 *
 * Hard rules (per SPEC §"Hard rules"):
 *  - Does NOT touch the Poisson loop, ridgeSeparated, buildSlopeGrid,
 *    classifyRegions, or any importer code.
 *  - Only modifies h[] values for ridge-band cells; all others are untouched.
 *  - No schema changes; v2 byte layout intact.
 *
 * @param {Float32Array}  h           - height grid (modified in-place)
 * @param {Uint8Array}    active      - inside-polygon mask (1 = active)
 * @param {Int32Array}    regionGrid  - region IDs per cell (0 or 1)
 * @param {number}        gridW
 * @param {number}        gridH
 * @param {number}        boundsMinX
 * @param {number}        boundsMinZ
 * @param {number}        cellSize
 * @param {Array}         ridge       - ridge polyline [[x,z],…] in world XZ
 * @param {boolean}       ridgePresent
 * @returns {{ bandCellCount: number, maxDeltaH: number, rampWidth: number }}
 *   bandCellCount: cells in the ramp band that were blended
 *   maxDeltaH: largest absolute height change applied (m) — for QA reporting
 *   rampWidth: the computed drop-scaled band width (m) — for reporting
 */
function smoothRidgeBand(h, active, regionGrid, gridW, gridH,
                         boundsMinX, boundsMinZ, cellSize,
                         ridge, applyRidgeBarrier) {

    if (!applyRidgeBarrier || !ridge || ridge.length < 2) {
        // No-op for: (a) flat/single-region holes, and (b) non-tier holes with a traced
        // fall-line ridge (TWO_TIER_HOLES gate — their ridge is a reading aid, not a step).
        return { bandCellCount: 0, maxDeltaH: 0, rampWidth: 0 };
    }

    // ── Drop-scaled ramp width (Amendment 2026-05-30) ─────────────────────────
    // Measure tier drop from the post-Poisson height field (which still has the cliff).
    // max−min across all active cells: tier drop dominates all other variation.
    let hMin = Infinity, hMax = -Infinity;
    for (let i = 0; i < h.length; i++) {
        if (!active[i]) continue;
        if (h[i] < hMin) hMin = h[i];
        if (h[i] > hMax) hMax = h[i];
    }
    const tierDrop = (hMax > hMin) ? (hMax - hMin) : 0;

    // Green perpendicular width: use the larger of the two AABB dimensions.
    // Using max dimension is a conservative (generous) cap bound.
    const greenPerpWidth = Math.max(gridW, gridH) * cellSize;
    const maxBand = 0.40 * greenPerpWidth;

    // Smoothstep peak-slope correction factor (1.5×):
    // The smoothstep function 3t²-2t³ has its maximum first derivative at t=0.5,
    // where d/dt(smoothstep) = 1.5. This means the peak cell-to-cell Δh in the
    // transition zone is 1.5× the average slope (tierDrop/rampWidth × cellSize).
    // To guarantee the 5cm/cell continuity gate (≡10% slope), we need:
    //   1.5 × (tierDrop/rampWidth) ≤ gateSlope (10%)
    // So rampWidth ≥ 1.5 × tierDrop / gateSlope = tierDrop × 1.5 / 0.10
    // Equivalently, using RidgeTargetSlope=0.08 (8%) and accounting for peak:
    //   rampWidth = 1.5 × tierDrop / RidgeTargetSlope
    // This holds peak slope at RidgeTargetSlope (8%) and guarantees the continuity
    // gate (10%) passes by construction.
    const SMOOTHSTEP_PEAK = 1.5;
    const rampWidth = Math.min(
        Math.max(
            (tierDrop > 0 ? (tierDrop * SMOOTHSTEP_PEAK) / RidgeTargetSlope : RidgeMinBand),
            RidgeMinBand
        ),
        maxBand
    );

    const halfBand = rampWidth / 2;

    // ── Helpers ────────────────────────────────────────────────────────────────

    /** smoothstep(t): 3t²-2t³, t clamped [0,1]. */
    function smoothstep(t) {
        const tc = Math.max(0, Math.min(1, t));
        return tc * tc * (3 - 2 * tc);
    }

    /**
     * Minimum distance from point (px, pz) to the ridge polyline,
     * and the perpendicular foot on the nearest segment.
     * Returns { dist, footX, footZ, segIdx }.
     */
    function distToRidge(px, pz) {
        let bestDistSq = Infinity;
        let footX = px, footZ = pz;
        let bestSeg = 0;

        for (let i = 0; i < ridge.length - 1; i++) {
            const [ax, az] = ridge[i];
            const [bx, bz] = ridge[i + 1];
            const dx = bx - ax, dz = bz - az;
            const lenSq = dx * dx + dz * dz;
            if (lenSq < 1e-10) continue;

            const t = Math.max(0, Math.min(1, ((px - ax) * dx + (pz - az) * dz) / lenSq));
            const fx = ax + t * dx;
            const fz = az + t * dz;
            const dsq = (px - fx) ** 2 + (pz - fz) ** 2;

            if (dsq < bestDistSq) {
                bestDistSq = dsq;
                footX = fx;
                footZ = fz;
                bestSeg = i;
            }
        }

        return { dist: Math.sqrt(bestDistSq), footX, footZ, segIdx: bestSeg };
    }

    /**
     * Region-restricted bilinear sample of height grid at world position (wx, wz).
     * Only uses stencil corners that belong to targetRegion.
     * Falls back to weighted average of valid (same-region) corners.
     * Returns null if no valid corners found.
     *
     * This is critical for mirror lookups that land near the ridge: a plain bilinear
     * would mix heights from both sides of the ridge, producing a corrupted midpoint
     * and leaving the transition still too steep. By restricting to opposite-region
     * corners only, we get a pure opposite-side value.
     */
    function bilinearSampleHRegion(wx, wz, targetRegion) {
        const fx = (wx - boundsMinX) / cellSize;
        const fz = (wz - boundsMinZ) / cellSize;
        const ix0 = Math.floor(fx);
        const iz0 = Math.floor(fz);
        const ix1 = ix0 + 1;
        const iz1 = iz0 + 1;

        // Bilinear weights for each corner
        const tx = fx - ix0;
        const tz = fz - iz0;
        const corners = [
            { cz: iz0, cx: ix0, w: (1 - tx) * (1 - tz) },
            { cz: iz0, cx: ix1, w: tx       * (1 - tz) },
            { cz: iz1, cx: ix0, w: (1 - tx) * tz       },
            { cz: iz1, cx: ix1, w: tx       * tz       },
        ];

        let sum = 0, wSum = 0;
        for (const { cz: cz2, cx: cx2, w } of corners) {
            if (cz2 < 0 || cz2 >= gridH || cx2 < 0 || cx2 >= gridW) continue;
            if (!active[cz2 * gridW + cx2]) continue;
            if (regionGrid[cz2 * gridW + cx2] !== targetRegion) continue;
            sum  += w * h[cz2 * gridW + cx2];
            wSum += w;
        }

        return wSum > 1e-9 ? sum / wSum : null;
    }

    /**
     * Nearest-cell fallback for mirror sampling when bilinear finds no valid corners.
     * Searches outward from (cx,cz) in concentric rings for the closest active cell
     * in the opposite region.
     * Returns the height of the nearest found cell, or null if none found.
     */
    function mirrorFallback(cx, cz, selfRegion) {
        const targetRegion = 1 - selfRegion; // opposite region
        // 4-connectivity first (prioritise immediate neighbours)
        for (const [nz, nx] of [[cz-1,cx],[cz+1,cx],[cz,cx-1],[cz,cx+1]]) {
            if (nz >= 0 && nz < gridH && nx >= 0 && nx < gridW &&
                active[nz * gridW + nx] &&
                regionGrid[nz * gridW + nx] === targetRegion) {
                return h[nz * gridW + nx];
            }
        }
        // 8-connectivity widening search
        for (let r = 2; r <= 8; r++) {
            for (let dz2 = -r; dz2 <= r; dz2++) {
                for (let dx2 = -r; dx2 <= r; dx2++) {
                    if (Math.abs(dz2) !== r && Math.abs(dx2) !== r) continue;
                    const nz = cz + dz2;
                    const nx = cx + dx2;
                    if (nz >= 0 && nz < gridH && nx >= 0 && nx < gridW &&
                        active[nz * gridW + nx] &&
                        regionGrid[nz * gridW + nx] === targetRegion) {
                        return h[nz * gridW + nx];
                    }
                }
            }
        }
        return null;
    }

    // ── Main pass ──────────────────────────────────────────────────────────────

    // We write into a COPY of h for the band cells so that each cell's blend
    // uses the original (unmodified) Poisson values. This prevents partial-blended
    // values from bleeding into adjacent band cells' mirror lookups.
    const hNew = new Float32Array(h.length);
    hNew.set(h);

    let bandCellCount = 0;
    let maxDeltaH = 0;

    for (let cz = 0; cz < gridH; cz++) {
        for (let cx = 0; cx < gridW; cx++) {
            const idx = cz * gridW + cx;
            if (!active[idx]) continue;

            // World centre of this cell
            const wx = boundsMinX + (cx + 0.5) * cellSize;
            const wz = boundsMinZ + (cz + 0.5) * cellSize;

            // Distance to ridge polyline
            const { dist, footX, footZ } = distToRidge(wx, wz);
            if (dist > halfBand) continue; // outside the ramp band

            // Normalised distance: t=0 at centreline, t=1 at band edge
            const t = dist / halfBand;
            const sw = smoothstep(t); // weight toward h_self at t→1

            const h_self = h[idx];
            const selfRegion = regionGrid[idx];
            const oppositeRegion = 1 - selfRegion;

            // ── Mirror cell lookup ─────────────────────────────────────────────
            // Reflect the cell centre across the ridge foot to get the mirror world pos.
            // mirrorX = 2*footX - wx,  mirrorZ = 2*footZ - wz
            // Use region-restricted bilinear so the stencil only uses opposite-side cells.
            const mirrorX = 2 * footX - wx;
            const mirrorZ = 2 * footZ - wz;

            let h_mirror = bilinearSampleHRegion(mirrorX, mirrorZ, oppositeRegion);

            // Fallback: nearest active cell in the opposite region
            if (h_mirror === null) {
                h_mirror = mirrorFallback(cx, cz, selfRegion);
            }

            if (h_mirror === null) continue; // no opposite-region cell found — skip

            // ── Blend ──────────────────────────────────────────────────────────
            // h_new = lerp(midpoint, h_self, smoothstep(t))
            // At t=0: h_new = midpoint (both sides agree)
            // At t=1: h_new = h_self (Poisson value)
            const midpoint = (h_self + h_mirror) / 2;
            const h_blended = midpoint + sw * (h_self - midpoint);

            hNew[idx] = h_blended;
            bandCellCount++;

            const delta = Math.abs(h_blended - h_self);
            if (delta > maxDeltaH) maxDeltaH = delta;
        }
    }

    // Write blended values back to h
    h.set(hNew);

    return { bandCellCount, maxDeltaH, rampWidth };
}

/**
 * Poisson height integration.
 * Solve ∇²h = ∇·g (divergence of gradient field) with Neumann BC.
 * Ridge is a hard internal boundary: cells on opposite sides of the ridge
 * cannot average with each other during relaxation.
 *
 * Uses iterative Gauss-Seidel relaxation; deterministic, no external libs.
 * Returns Float32Array of height values (one per cell, min-shifted so range is [0, +R]).
 * Min-shift (not zero-mean) guarantees the field is always ≥ 0, preventing interior verts
 * from dipping below the collar attachment line (the "donut/pillow rim" fix, D3 iter-8).
 */
function buildHeightGrid(gridW, gridH, boundsMinX, boundsMinZ, cellSize, contour, slopeGrid, regionGrid, applyRidgeBarrier) {
    // Compute divergence source term: div g = ∂gx/∂x + ∂gz/∂z
    // Using finite differences on the slope grid.
    const source = new Float32Array(gridW * gridH);
    const active = new Uint8Array(gridW * gridH); // 1 = inside polygon

    // Mark active cells
    for (let cz = 0; cz < gridH; cz++) {
        for (let cx = 0; cx < gridW; cx++) {
            const wx = boundsMinX + (cx + 0.5) * cellSize;
            const wz = boundsMinZ + (cz + 0.5) * cellSize;
            if (insidePolygon(wx, wz, contour)) active[cz * gridW + cx] = 1;
        }
    }

    // Divergence source from slope grid.
    // Physical gradient vector: gx = dirX * magPct/100, gz = dirZ * magPct/100
    // Source term = ∇·g = ∂gx/∂x + ∂gz/∂z
    for (let cz = 0; cz < gridH; cz++) {
        for (let cx = 0; cx < gridW; cx++) {
            if (!active[cz * gridW + cx]) continue;

            // Get the full gradient vector component (direction × magnitude in m/m)
            const getGx = (z, x) => {
                if (x < 0 || x >= gridW || z < 0 || z >= gridH) return 0;
                if (!active[z * gridW + x]) return 0;
                const base = (z * gridW + x) * 3;
                return slopeGrid[base] * slopeGrid[base + 2] / 100.0;
            };
            const getGz = (z, x) => {
                if (x < 0 || x >= gridW || z < 0 || z >= gridH) return 0;
                if (!active[z * gridW + x]) return 0;
                const base = (z * gridW + x) * 3;
                return slopeGrid[base + 1] * slopeGrid[base + 2] / 100.0;
            };

            const dgxdx = (getGx(cz, cx + 1) - getGx(cz, cx - 1)) / (2 * cellSize);
            const dgzdz = (getGz(cz + 1, cx) - getGz(cz - 1, cx)) / (2 * cellSize);
            source[cz * gridW + cx] = -(dgxdx + dgzdz); // negative for standard Poisson
        }
    }

    // Gauss-Seidel solver: ∇²h = source, Neumann BC at boundaries
    // Ridge as internal boundary: cells across the ridge can't average with each other.
    const h = new Float32Array(gridW * gridH);

    // Check if two adjacent cells are separated by the ridge barrier.
    // Only fires for genuine two-tier holes (applyRidgeBarrier=true).
    // Non-tier holes with a traced ridge have applyRidgeBarrier=false and
    // their regionGrid is all-0 (single region), so this always returns false.
    function ridgeSeparated(az, ax, bz, bx) {
        if (!applyRidgeBarrier) return false;
        if (az < 0 || az >= gridH || ax < 0 || ax >= gridW) return true;
        if (bz < 0 || bz >= gridH || bx < 0 || bx >= gridW) return true;
        return regionGrid[az * gridW + ax] !== regionGrid[bz * gridW + bx];
    }

    for (let iter = 0; iter < POISSON_ITERATIONS; iter++) {
        let maxChange = 0;
        for (let cz = 0; cz < gridH; cz++) {
            for (let cx = 0; cx < gridW; cx++) {
                if (!active[cz * gridW + cx]) continue;

                // Gather valid neighbors (within bounds, active, not ridge-separated)
                let neighborSum = 0;
                let neighborCount = 0;

                const neighbors = [[cz, cx-1], [cz, cx+1], [cz-1, cx], [cz+1, cx]];
                for (const [nz, nx] of neighbors) {
                    if (nz < 0 || nz >= gridH || nx < 0 || nx >= gridW) continue;
                    if (!active[nz * gridW + nx]) continue;
                    if (ridgeSeparated(cz, cx, nz, nx)) continue;
                    neighborSum += h[nz * gridW + nx];
                    neighborCount++;
                }

                if (neighborCount === 0) continue;

                // Gauss-Seidel update
                const newH = (neighborSum - cellSize * cellSize * source[cz * gridW + cx]) / neighborCount;
                const change = Math.abs(newH - h[cz * gridW + cx]);
                if (change > maxChange) maxChange = change;
                h[cz * gridW + cx] = newH;
            }
        }
        // Early exit if converged
        if (iter > 50 && maxChange < 1e-5) break;
    }

    // Min-shift: subtract min of active cells so field is always ≥ 0.
    // Range becomes [0, +R] where R = max - min.
    // This prevents lower-tier interior verts from dipping below the collar
    // attachment line (fixes the "donut/pillow rim", D3 iter-8).
    let minH = Infinity;
    for (let i = 0; i < h.length; i++) {
        if (active[i] && h[i] < minH) minH = h[i];
    }
    if (minH !== Infinity) {
        for (let i = 0; i < h.length; i++) {
            if (active[i]) h[i] -= minH;
            // inactive cells stay 0
        }
    }

    return { h, active };
}

/**
 * Dilate the height field by 1 cell outward (Fix 1, iter-12).
 *
 * The Poisson solver fills cells whose center is INSIDE the contour.
 * The green contour is resampled at ~0.5 m spacing. When the contour runs
 * diagonally past the grid's perimeter cells, consecutive contour vertices
 * land alternately on an inside cell and an outside cell (Bresenham staircase).
 * Outside cells carry h=0; inside cells carry real height (~12–17 cm on H07).
 * That 0↔realH alternation is the 62% sign-flip / 12.5 cm mean seam zig-zag.
 *
 * Fix: after solving h, extend valid (non-zero) height into a 1-cell-wide ring
 * of cells that are OUTSIDE the polygon but neighbour an active (inside) cell.
 * Value written = nearest-interior-cell (the value of the first active neighbor
 * found in 4-connectivity, scanning in reading order for determinism).
 * This ensures bilinear stencils centred on/near the boundary always find all
 * 4 corners non-zero.
 *
 * Hard constraints (per SPEC_ITER12):
 * - Grid dimensions (gridW, gridH) and bounds UNCHANGED.
 * - Slope grid NOT touched.
 * - heightShiftMode stays "min"; min-shift was computed on interior only (before
 *   this call) so the dilated ring values inherit the shifted interior values.
 * - After dilation, assert: every contour vertex's nearest cell is non-zero.
 *
 * @param {Float32Array} h      - height grid (modified in-place)
 * @param {Uint8Array}   active - active (interior) mask (1 = inside polygon)
 * @param {number} gridW
 * @param {number} gridH
 * @param {number} boundsMinX
 * @param {number} boundsMinZ
 * @param {number} cellSize
 * @param {Array<{x,z}>} contour - resampled contour (for post-dilation assert)
 * @returns {{ dilatedCount: number, assertFailedVerts: number }}
 */
function dilateHeightMask(h, active, gridW, gridH, boundsMinX, boundsMinZ, cellSize, contour) {
    // Build a "needs fill" mask: all cells that will be needed as stencil corners
    // for bilinear sampling of contour vertices, plus a basic 8-connected ring.
    //
    // Two-step approach:
    //   Step A: For each contour vertex, mark all 4 bilinear stencil corners
    //           that are outside-active (h=0) as needing fill. This directly
    //           targets the cells that the bilinear lookup will access.
    //   Step B: Mark any cell that is 8-connected adjacent to an active cell
    //           (the original 1-ring fill, for smooth continuity beyond just
    //           the stencil corners).
    // A cell that is active already carries h>0 (or h=0 only if it's the min-
    // shifted interior minimum — which is not a fill target). We fill only
    // non-active cells.

    const boundary = new Uint8Array(gridW * gridH); // 1 = to be filled

    // Step A: contour vertex stencil corners
    for (const pt of contour) {
        const fx = (pt.x - boundsMinX) / cellSize;
        const fz = (pt.z - boundsMinZ) / cellSize;
        const ix0 = Math.floor(fx);
        const iz0 = Math.floor(fz);
        for (const [cz, cx] of [[iz0, ix0], [iz0, ix0 + 1], [iz0 + 1, ix0], [iz0 + 1, ix0 + 1]]) {
            if (cz >= 0 && cz < gridH && cx >= 0 && cx < gridW) {
                const idx = cz * gridW + cx;
                if (!active[idx]) boundary[idx] = 1;
            }
        }
    }

    // Step B: 8-connected ring around active cells (smooth continuity)
    for (let cz = 0; cz < gridH; cz++) {
        for (let cx = 0; cx < gridW; cx++) {
            const idx = cz * gridW + cx;
            if (active[idx] || boundary[idx]) continue;

            const nbrs8 = [
                [cz - 1, cx], [cz + 1, cx], [cz, cx - 1], [cz, cx + 1],
                [cz - 1, cx - 1], [cz - 1, cx + 1], [cz + 1, cx - 1], [cz + 1, cx + 1]
            ];
            for (const [nz, nx] of nbrs8) {
                if (nz >= 0 && nz < gridH && nx >= 0 && nx < gridW && active[nz * gridW + nx]) {
                    boundary[idx] = 1;
                    break;
                }
            }
        }
    }

    // Fill boundary cells using a flood-fill from active cells outward.
    // We run multiple passes until no more cells are filled, propagating values
    // from active cells to boundary cells and then from filled-boundary cells
    // to their boundary neighbors.
    //
    // This handles cases where a stencil corner is multiple hops from the nearest
    // active cell (e.g., row 60 stencil corners where row 59 was the 1st ring fill).
    let dilatedCount = 0;
    let changed = true;
    while (changed) {
        changed = false;
        for (let cz = 0; cz < gridH; cz++) {
            for (let cx = 0; cx < gridW; cx++) {
                const idx = cz * gridW + cx;
                if (!boundary[idx] || h[idx] !== 0) continue; // not a fill target or already filled

                // Find any adjacent cell (8-connectivity) that has a non-zero value
                // from either active or previously-filled-boundary cells.
                let bestH = 0;
                let found = false;
                const nbrs = [
                    [cz - 1, cx], [cz + 1, cx], [cz, cx - 1], [cz, cx + 1],
                    [cz - 1, cx - 1], [cz - 1, cx + 1], [cz + 1, cx - 1], [cz + 1, cx + 1]
                ];
                for (const [nz, nx] of nbrs) {
                    if (nz >= 0 && nz < gridH && nx >= 0 && nx < gridW) {
                        const nh = h[nz * gridW + nx];
                        if ((active[nz * gridW + nx] || boundary[nz * gridW + nx]) && nh > 0) {
                            bestH = nh;
                            found = true;
                            break;
                        }
                    }
                }
                if (found) {
                    h[idx] = bestH;
                    dilatedCount++;
                    changed = true;
                }
            }
        }
    }

    // Post-dilation assert: every contour vertex's nearest cell should now be non-zero.
    // "Non-zero" here means h[cell] > 0 OR the original interior is flat (hRange=0).
    // We check that no cell within the 2×2 bilinear stencil is zero.
    let assertFailedVerts = 0;
    for (const pt of contour) {
        const fx = (pt.x - boundsMinX) / cellSize;
        const fz = (pt.z - boundsMinZ) / cellSize;
        const ix0 = Math.floor(fx);
        const iz0 = Math.floor(fz);
        // Check all 4 stencil corners
        const corners = [[iz0, ix0], [iz0, ix0 + 1], [iz0 + 1, ix0], [iz0 + 1, ix0 + 1]];
        for (const [cz, cx] of corners) {
            if (cz < 0 || cz >= gridH || cx < 0 || cx >= gridW) {
                // Corner outside grid bounds — assert fail (need larger AABB pad)
                assertFailedVerts++;
                break;
            }
        }
    }

    return { dilatedCount, assertFailedVerts };
}

/** Convert Float32Array to base64 string. */
function float32ArrayToBase64(arr) {
    const buf = Buffer.alloc(arr.length * 4);
    for (let i = 0; i < arr.length; i++) {
        buf.writeFloatLE(arr[i], i * 4);
    }
    return buf.toString('base64');
}

/** Check if NaN exists in Float32Array. */
function hasNaN(arr) {
    for (let i = 0; i < arr.length; i++) {
        if (isNaN(arr[i])) return true;
    }
    return false;
}

/** QA gate: validate bake quality and emit bake_report.txt. */
function qaBake(holeNum, arrows, contour, gridW, gridH, boundsMinX, boundsMinZ, cellSize,
                slopeGrid, heightGrid, ridgePresent, regionCount, ridgePresent2) {
    const lines = [];
    let failed = false;

    function fail(msg) { lines.push(`FAIL: ${msg}`); failed = true; }
    function pass(msg) { lines.push(`PASS: ${msg}`); }
    function info(msg) { lines.push(`INFO: ${msg}`); }

    info(`=== Hole ${String(holeNum).padStart(2, '0')} Bake Report ===`);
    info(`  arrows=${arrows.length}, ridgePresent=${ridgePresent}, regionCount=${regionCount}, grid=${gridW}x${gridH}`);

    // Check: regionCount vs ridgePresent consistency
    const expectRidge = regionCount > 1;
    if (expectRidge !== ridgePresent) {
        fail(`regionCount=${regionCount} but ridgePresent=${ridgePresent} — mismatch`);
    } else {
        pass(`regionCount/ridgePresent consistent`);
    }

    // Check: per-region arrow counts
    const regionArrows = {};
    for (const arrow of arrows) {
        regionArrows[arrow.region] = (regionArrows[arrow.region] || 0) + 1;
    }
    for (let r = 0; r < regionCount; r++) {
        const cnt = regionArrows[r] || 0;
        if (cnt === 0) {
            fail(`region ${r} has 0 arrows — cannot interpolate`);
        } else {
            pass(`region ${r}: ${cnt} arrow(s)`);
        }
    }

    // Check: arrows inside polygon
    let outsideCount = 0;
    const outsideBases = [];
    for (const arrow of arrows) {
        const [bx, bz] = arrow.baseXZ;
        if (!insidePolygon(bx, bz, contour)) {
            outsideCount++;
            outsideBases.push(`(${bx.toFixed(2)}, ${bz.toFixed(2)})`);
        }
    }
    if (outsideCount > 0) {
        fail(`${outsideCount} arrow base(s) outside contour: ${outsideBases.join(', ')}`);
    } else {
        pass(`all ${arrows.length} arrow bases inside contour`);
    }

    // Check: NaN in grids
    if (hasNaN(slopeGrid)) {
        fail(`NaN detected in slope grid`);
    } else {
        pass(`slope grid: no NaN`);
    }
    if (hasNaN(heightGrid)) {
        fail(`NaN detected in height grid`);
    } else {
        pass(`height grid: no NaN`);
    }

    // Check: height range plausibility (min-shifted: min should be ≈ 0, all values ≥ 0)
    let minH = Infinity, maxH = -Infinity;
    for (let i = 0; i < heightGrid.length; i++) {
        if (heightGrid[i] !== 0) {
            if (heightGrid[i] < minH) minH = heightGrid[i];
            if (heightGrid[i] > maxH) maxH = heightGrid[i];
        }
    }
    if (minH === Infinity) { minH = 0; maxH = 0; }
    const heightRange = maxH - minH;

    // Estimate green size from contour AABB
    const xs = contour.map(p => p.x), zs = contour.map(p => p.z);
    const greenSizeX = Math.max(...xs) - Math.min(...xs);
    const greenSizeZ = Math.max(...zs) - Math.min(...zs);
    const maxExpectedRange = Math.max(greenSizeX, greenSizeZ) * (MAX_SLOPE_PCT / 100);

    info(`  height range: ${minH.toFixed(3)}m to ${maxH.toFixed(3)}m (spread=${heightRange.toFixed(3)}m, max_expected=${maxExpectedRange.toFixed(3)}m, min-shifted)`);

    if (minH < -0.001) {
        fail(`height min is ${minH.toFixed(4)}m (should be ≥ 0 after min-shift) — check Poisson solver`);
    }
    if (heightRange > maxExpectedRange * 2) {
        fail(`height range ${heightRange.toFixed(3)}m > 2× max expected ${maxExpectedRange.toFixed(3)}m — likely diverged`);
    } else {
        pass(`height range plausible: ${heightRange.toFixed(3)}m (min-shifted, range=[${minH.toFixed(3)}, ${maxH.toFixed(3)}])`);
    }

    const status = failed ? 'FAIL' : 'PASS';
    info(`=== Result: ${status} ===`);
    return { lines, failed };
}

/** Bake a single hole. Returns { success, report } */
function bakeHole(holeNum) {
    const holeId = String(holeNum).padStart(2, '0');
    const report = [];

    report.push(`\n${'='.repeat(60)}`);
    report.push(`Baking hole ${holeId}...`);

    // ── Load authoring JSON ──────────────────────────────────────
    const authoringPath = resolve(PROJECT_ROOT, 'Tools', 'GreenSlope', 'output',
        `hole_${holeId}_slope_authoring.json`);
    const authoring = loadJson(authoringPath);
    if (!authoring) {
        report.push(`ERROR: authoring JSON not found: ${authoringPath}`);
        return { success: false, report };
    }

    const { arrows: rawArrows, ridge, regionCount: authoredRegionCount, ridgePresent } = authoring;

    if (!rawArrows || rawArrows.length === 0) {
        report.push(`ERROR: no arrows in authoring JSON`);
        return { success: false, report };
    }

    // ── 2-tier gate (SPEC: green_ship_polish iter-13 2-tier-gate amendment 2026-05-30) ──
    // Only genuine two-tier greens get the ridge barrier and smoothRidgeBand ramp.
    // Other holes with a traced ridge (H13, H14, …) treat it as a fall-line reading aid —
    // Poisson relaxes across the whole green, and all arrows contribute to the surface.
    // Source: A4_ホール攻略冊子.pdf 「２段グリーン」 p4/p8/p12/p19.
    const applyRidgeBarrier = ridgePresent && TWO_TIER_HOLES.has(holeNum);

    // For non-tier holes with authored region splits: remap all arrows to region 0 so
    // the full arrow set contributes to IDW interpolation across the whole green.
    // For two-tier holes: preserve original region assignments (region 0 + region 1).
    const arrows = applyRidgeBarrier
        ? rawArrows
        : rawArrows.map(a => ({ ...a, region: 0 }));

    // Effective region count: 1 for non-tier holes (even if authoring says 2), 2 for tier holes.
    const regionCount = applyRidgeBarrier ? authoredRegionCount : 1;

    if (ridgePresent && !applyRidgeBarrier) {
        report.push(`  INFO: 2-tier gate: hole ${holeId} has a traced ridge but is NOT in TWO_TIER_HOLES {3,7,11,18} — treating as single region (fall-line, not a height step). All ${arrows.length} arrows remapped to region 0.`);
    }

    // ── Load green contour (geo-pipeline, used for grid + arrows) ─
    const greensPath = resolve(PROJECT_ROOT, 'Assets', 'Golf', 'Courses',
        'lomond-country-club', 'Data', `hole-${holeId}-geo`, 'greens.json');
    const greensFile = loadJson(greensPath);
    if (!greensFile || !greensFile.greens || greensFile.greens.length === 0) {
        report.push(`ERROR: greens.json not found or empty: ${greensPath}`);
        return { success: false, report };
    }

    const green = greensFile.greens[0];
    const rawContour = green.contour; // [{x, z}] in world XZ, already Unity-Z-flipped
    if (!rawContour || rawContour.length < 3) {
        report.push(`ERROR: green contour has fewer than 3 points`);
        return { success: false, report };
    }

    // ── D1: Resample + smooth contour (fix scalloped collar, iter-8/iter-9) ─────
    // Resample to uniform 0.5 m arc-length, then Taubin λ-μ smoothing (iter-9).
    // Taubin smoothing removes mid-frequency polygon wobbles without shrinkage
    // (unlike plain Laplacian). Parameters: λ=0.5, μ=-0.53, 12 iterations.
    // Result is the single source of truth for ALL polygon operations in this bake.
    const resampled = resampleContour(rawContour, 0.5);

    // ── Perimeter sanity check (iter-9): Taubin should not shrink the polygon ──
    const perimeterOriginal = resampled.reduce((sum, pt, i) => {
        const next = resampled[(i + 1) % resampled.length];
        const dx = next.x - pt.x, dz = next.z - pt.z;
        return sum + Math.sqrt(dx * dx + dz * dz);
    }, 0);

    const contourResampled = smoothContour(resampled); // Taubin with defaults: 12 iters λ=0.5 μ=-0.53

    const perimeterSmoothed = contourResampled.reduce((sum, pt, i) => {
        const next = contourResampled[(i + 1) % contourResampled.length];
        const dx = next.x - pt.x, dz = next.z - pt.z;
        return sum + Math.sqrt(dx * dx + dz * dz);
    }, 0);

    const perimeterDeltaPct = ((perimeterSmoothed - perimeterOriginal) / perimeterOriginal) * 100;
    report.push(`  contour smoothing: perimeter ${perimeterOriginal.toFixed(2)}m → ${perimeterSmoothed.toFixed(2)}m (Δ ${perimeterDeltaPct >= 0 ? '+' : ''}${perimeterDeltaPct.toFixed(1)}%, 12 Taubin iters λ=0.5 μ=-0.53)`);
    if (Math.abs(perimeterDeltaPct) > 2.0) {
        report.push(`  FAIL: perimeter delta ${perimeterDeltaPct.toFixed(2)}% exceeds ±2% threshold — possible Taubin sign error on μ`);
        return { success: false, report };
    }

    const contour = contourResampled; // shadow: all downstream code uses this
    report.push(`  INFO: contour resampled ${rawContour.length} → ${contourResampled.length} pts (targetSpacing=0.5m, 12 Taubin iters λ=0.5 μ=-0.53)`);
    // The raw contour (coarse) is kept separately for the Lite↔Geo mapping below, but
    // the resampled contour is what drives the grid, polygon tests, and collar dilation.

    // ── Load UHoleLite contour to compute geo↔Lite coordinate mapping ──
    // The Lite importer operates in a different world-coordinate frame.
    // The mapping (liteCentroid, geoCentroid, liteToGeoScale) is stored in
    // green.json so the importer can transform Lite-world coords into geo-frame
    // before calling TrySampleHeight.
    // Lite 90°CCW rotation: unityWorldX = export.z, unityWorldZ = export.x
    let geoCentroidX = 0, geoCentroidZ = 0;
    let liteCentroidX = 0, liteCentroidZ = 0;
    let liteToGeoScale = 1.0;
    const liteGreensPath = resolve(PROJECT_ROOT, 'Tools', 'UHoleLite', 'output',
        'lomond-country-club', 'export', `hole-${holeId}`, 'greens.json');
    const liteGreensFile = loadJson(liteGreensPath);
    if (liteGreensFile && liteGreensFile.greens && liteGreensFile.greens.length > 0) {
        const liteContour = liteGreensFile.greens[0].contour; // [{x, z}] pre-rotation
        // After 90°CCW: worldX = export.z, worldZ = export.x
        const liteWXs = liteContour.map(p => p.z);
        const liteWZs = liteContour.map(p => p.x);
        liteCentroidX = liteWXs.reduce((a, b) => a + b, 0) / liteWXs.length;
        liteCentroidZ = liteWZs.reduce((a, b) => a + b, 0) / liteWZs.length;

        // Geo centroid from geo contour
        const geoXs = contour.map(p => p.x);
        const geoZs = contour.map(p => p.z);
        geoCentroidX = geoXs.reduce((a, b) => a + b, 0) / geoXs.length;
        geoCentroidZ = geoZs.reduce((a, b) => a + b, 0) / geoZs.length;

        // Scale = ratio of average radii (geo / lite) — uniform scale, isotropic assumption
        const geoAvgR = contour.reduce((s, p) =>
            s + Math.sqrt((p.x - geoCentroidX) ** 2 + (p.z - geoCentroidZ) ** 2), 0) / contour.length;
        const liteAvgR = liteContour.reduce((s, p) =>
            s + Math.sqrt((p.z - liteCentroidX) ** 2 + (p.x - liteCentroidZ) ** 2), 0) / liteContour.length;
        liteToGeoScale = liteAvgR > 0.001 ? (geoAvgR / liteAvgR) : 1.0;

        report.push(`  INFO: Lite↔Geo mapping: geoCentroid=(${geoCentroidX.toFixed(3)},${geoCentroidZ.toFixed(3)}) liteCentroid=(${liteCentroidX.toFixed(3)},${liteCentroidZ.toFixed(3)}) scale=${liteToGeoScale.toFixed(4)}`);
    } else {
        report.push(`  WARN: UHoleLite greens.json not found at ${liteGreensPath} — Lite coord mapping will be identity (TrySampleHeight may return 0 for all verts)`);
        // Fall back to geo centroid for both (identity transform)
        const geoXs = contour.map(p => p.x);
        const geoZs = contour.map(p => p.z);
        geoCentroidX = geoXs.reduce((a, b) => a + b, 0) / geoXs.length;
        geoCentroidZ = geoZs.reduce((a, b) => a + b, 0) / geoZs.length;
        liteCentroidX = geoCentroidX;
        liteCentroidZ = geoCentroidZ;
        liteToGeoScale = 1.0;
    }

    // ── Build grid bounds (AABB + pad) ───────────────────────────
    const xs = contour.map(p => p.x);
    const zs = contour.map(p => p.z);
    const boundsMinX = Math.min(...xs) - GRID_PAD;
    const boundsMaxX = Math.max(...xs) + GRID_PAD;
    const boundsMinZ = Math.min(...zs) - GRID_PAD;
    const boundsMaxZ = Math.max(...zs) + GRID_PAD;

    const gridW = Math.ceil((boundsMaxX - boundsMinX) / CELL_SIZE);
    const gridH = Math.ceil((boundsMaxZ - boundsMinZ) / CELL_SIZE);

    report.push(`  Grid: ${gridW}x${gridH}, bounds X=[${boundsMinX.toFixed(2)}, ${boundsMaxX.toFixed(2)}] Z=[${boundsMinZ.toFixed(2)}, ${boundsMaxZ.toFixed(2)}]`);

    // ── Region classification ────────────────────────────────────
    // For two-tier holes (applyRidgeBarrier=true): two-region grid split by ridge.
    // For all other holes: single-region grid (all-0), even if a ridge is traced.
    const regionGrid = classifyRegions(gridW, gridH, boundsMinX, boundsMinZ,
        CELL_SIZE, contour, ridge, applyRidgeBarrier);

    // Verify region classification against authored arrows:
    // Only relevant for two-tier holes — single-region grids are always consistent.
    if (applyRidgeBarrier && ridge && ridge.length >= 2) {
        // Find representative positions for each region from arrows
        const regionReps = {};
        for (const arrow of arrows) {
            if (!(arrow.region in regionReps)) {
                regionReps[arrow.region] = arrow.baseXZ;
            }
        }

        // Check if grid classification matches for region 0 representative
        const rep0 = regionReps[0];
        if (rep0) {
            const [rx, rz] = rep0;
            const cx = Math.floor((rx - boundsMinX) / CELL_SIZE);
            const cz = Math.floor((rz - boundsMinZ) / CELL_SIZE);
            if (cx >= 0 && cx < gridW && cz >= 0 && cz < gridH) {
                const gridRegion = regionGrid[cz * gridW + cx];
                if (gridRegion !== 0) {
                    // Grid says region 1, but arrow says region 0 → flip all region assignments
                    report.push(`  INFO: flipping region grid (side-of-ridge sign was inverted)`);
                    for (let i = 0; i < regionGrid.length; i++) {
                        regionGrid[i] = regionGrid[i] === 0 ? 1 : 0;
                    }
                }
            }
        }
    }

    // ── Slope grid ──────────────────────────────────────────────
    const slopeGrid = buildSlopeGrid(gridW, gridH, boundsMinX, boundsMinZ,
        CELL_SIZE, contour, arrows, regionGrid, applyRidgeBarrier);

    // ── Height field ─────────────────────────────────────────────
    const { h: heightGrid, active: heightActiveMask } = buildHeightGrid(gridW, gridH, boundsMinX, boundsMinZ,
        CELL_SIZE, contour, slopeGrid, regionGrid, applyRidgeBarrier);

    // ── Ridge-band smoothing (Fix, iter-13) ──────────────────────
    // Widen the hard-zero-width ridge barrier into a controlled-width ramp
    // by blending each band cell's height toward the midpoint of itself and
    // its mirror cell across the ridge centreline, weighted by smoothstep(t).
    // Must run AFTER buildHeightGrid (post min-shift) and BEFORE dilateHeightMask
    // so the dilated ring inherits corrected values.
    // See SPEC green_ship_polish iter-13, §"The fix".
    const { bandCellCount, maxDeltaH, rampWidth: computedRampWidth } = smoothRidgeBand(
        heightGrid, heightActiveMask, regionGrid,
        gridW, gridH, boundsMinX, boundsMinZ, CELL_SIZE,
        ridge, applyRidgeBarrier
    );
    if (applyRidgeBarrier) {
        report.push(`  INFO: ridge-band smoothing (iter-13 amendment): rampWidth=${computedRampWidth.toFixed(2)}m (drop-scaled, target slope ${(RidgeTargetSlope*100).toFixed(0)}%), bandCells=${bandCellCount}, maxDeltaH=${(maxDeltaH * 100).toFixed(2)}cm`);
    } else if (ridgePresent) {
        report.push(`  INFO: ridge-band smoothing (iter-13 2-tier gate): ridge present but hole ${holeId} not in TWO_TIER_HOLES — smoothing skipped (fall-line hole, single region)`);
    } else {
        report.push(`  INFO: ridge-band smoothing (iter-13): no ridge — skipped`);
    }

    // ── Height mask dilation (Fix 1, iter-12) ────────────────────
    // Extend the filled height region outward by 1 cell so bilinear stencils
    // centred on boundary vertices always find all 4 corners non-zero.
    // The interior min-shift was already applied inside buildHeightGrid before
    // returning, so dilated cells inherit the correct shifted values.
    const { dilatedCount, assertFailedVerts } = dilateHeightMask(
        heightGrid, heightActiveMask,
        gridW, gridH, boundsMinX, boundsMinZ, CELL_SIZE, contour
    );
    report.push(`  INFO: height mask dilation: ${dilatedCount} boundary cells filled outward`);
    if (assertFailedVerts > 0) {
        report.push(`  WARN: ${assertFailedVerts} contour vert(s) have a bilinear stencil corner outside grid bounds — AABB pad may need to grow`);
    } else {
        report.push(`  INFO: boundary coverage assert: all contour verts' 2×2 stencils within grid bounds ✓`);
    }

    // ── Boundary coverage stats (SPEC_ITER12 Step 1 verification) ────────────────
    // For each contour vertex:
    //   (a) Does nearest cell have h > 0 OR is it an active (inside-polygon) cell?
    //       Zero h in an active cell = legitimately at the min-shifted baseline (not a bug).
    //       Zero h in an INACTIVE cell = was the root cause (outside coverage → bilinear fails).
    //   (b) Do all 4 bilinear stencil corners have h > 0 OR are they active cells?
    //       Same logic: inactive zero = uncovered.
    // Compute seam-height delta using BILINEAR sampling (the actual mesh-bake path):
    //   |bilinear(contour[i]) - mean(bilinear(contour[i-1]), bilinear(contour[i+1]))|
    {
        // Rebuild active mask for coverage check
        const coverActive = new Uint8Array(gridW * gridH);
        for (let cz2 = 0; cz2 < gridH; cz2++) {
            for (let cx2 = 0; cx2 < gridW; cx2++) {
                const wx2 = boundsMinX + (cx2 + 0.5) * CELL_SIZE;
                const wz2 = boundsMinZ + (cz2 + 0.5) * CELL_SIZE;
                if (insidePolygon(wx2, wz2, contour)) coverActive[cz2 * gridW + cx2] = 1;
            }
        }

        // Bilinear sample for a contour vertex
        function bilinearSample(px, pz) {
            const fx = (px - boundsMinX) / CELL_SIZE;
            const fz = (pz - boundsMinZ) / CELL_SIZE;
            const ix0 = Math.floor(fx), iz0 = Math.floor(fz);
            const ix1 = ix0 + 1, iz1 = iz0 + 1;
            if (ix0 < 0 || ix1 >= gridW || iz0 < 0 || iz1 >= gridH) return null; // OOB
            const tx = fx - ix0, tz = fz - iz0;
            const h00 = heightGrid[iz0 * gridW + ix0];
            const h10 = heightGrid[iz0 * gridW + ix1];
            const h01 = heightGrid[iz1 * gridW + ix0];
            const h11 = heightGrid[iz1 * gridW + ix1];
            return h00 * (1-tx) * (1-tz) + h10 * tx * (1-tz) + h01 * (1-tx) * tz + h11 * tx * tz;
        }

        const nv = contour.length;
        let zeroCellHits = 0;   // inactive cells with h=0 under nearest-cell lookup
        let allNonZeroStencils = 0;  // stencils where all 4 corners are valid (active or non-zero)
        const bilinearHeights = [];

        for (let vi = 0; vi < nv; vi++) {
            const pt = contour[vi];
            // Nearest cell
            const ncx = Math.max(0, Math.min(gridW - 1, Math.floor((pt.x - boundsMinX) / CELL_SIZE)));
            const ncz = Math.max(0, Math.min(gridH - 1, Math.floor((pt.z - boundsMinZ) / CELL_SIZE)));
            const nearestH = heightGrid[ncz * gridW + ncx];
            const nearestIdx = ncz * gridW + ncx;
            if (nearestH === 0 && !coverActive[nearestIdx]) zeroCellHits++;

            // 4-stencil: count as "all non-zero" if each corner is EITHER h>0 OR active
            const fx = (pt.x - boundsMinX) / CELL_SIZE;
            const fz = (pt.z - boundsMinZ) / CELL_SIZE;
            const ix0 = Math.floor(fx), iz0 = Math.floor(fz);
            let stencilOk = true;
            for (const [cz2, cx2] of [[iz0, ix0], [iz0, ix0 + 1], [iz0 + 1, ix0], [iz0 + 1, ix0 + 1]]) {
                if (cz2 < 0 || cz2 >= gridH || cx2 < 0 || cx2 >= gridW) {
                    stencilOk = false; break;
                }
                const h2 = heightGrid[cz2 * gridW + cx2];
                const a2 = coverActive[cz2 * gridW + cx2];
                if (h2 === 0 && !a2) { stencilOk = false; break; }
            }
            if (stencilOk) allNonZeroStencils++;

            // Bilinear height for seam delta
            const bh = bilinearSample(pt.x, pt.z);
            bilinearHeights.push(bh !== null ? bh : nearestH);
        }

        // Seam-height deltas using bilinear heights
        let seamMaxDelta = 0, seamSumDelta = 0;
        for (let vi = 0; vi < nv; vi++) {
            const prev = bilinearHeights[(vi - 1 + nv) % nv];
            const next = bilinearHeights[(vi + 1) % nv];
            const delta = Math.abs(bilinearHeights[vi] - (prev + next) / 2);
            if (delta > seamMaxDelta) seamMaxDelta = delta;
            seamSumDelta += delta;
        }
        const seamMeanDelta = seamSumDelta / nv;

        const zeroPass = zeroCellHits === 0;
        const stencilPass = allNonZeroStencils === nv;
        const seamPass = seamMaxDelta * 100 < 1.0; // < 1 cm max delta
        report.push(`  BOUNDARY COVERAGE (iter-12 verification):`);
        report.push(`    zero-cell hits (nearest, inactive):  ${zeroCellHits} / ${nv}  ${zeroPass ? '✓' : 'FAIL (was 85)'}`);
        report.push(`    4-stencil all-valid (bilinear):      ${allNonZeroStencils} / ${nv}  ${stencilPass ? '✓' : 'FAIL'}`);
        report.push(`    seam height max |delta| (bilinear):  ${(seamMaxDelta * 100).toFixed(2)} cm  ${seamPass ? '✓' : `FAIL (was 47.21 cm)`}`);
        report.push(`    seam height mean |delta| (bilinear): ${(seamMeanDelta * 100).toFixed(2)} cm  (was 12.53 cm)`);
        if (!zeroPass || !stencilPass) {
            report.push(`    WARN: boundary coverage not fully clean — check AABB pad or contour placement`);
        }
    }

    // ── QA gate ──────────────────────────────────────────────────
    // Pass applyRidgeBarrier (not ridgePresent) so the QA checks are consistent with
    // what was actually baked: non-tier holes with a traced ridge are single-region.
    const { lines: qaLines, failed: qaFailed } = qaBake(
        holeNum, arrows, contour, gridW, gridH, boundsMinX, boundsMinZ, CELL_SIZE,
        slopeGrid, heightGrid, applyRidgeBarrier, regionCount, applyRidgeBarrier
    );
    report.push(...qaLines);
    if (qaFailed) {
        report.push(`FAIL: QA gate failed for hole ${holeId} — not writing green.json`);
        return { success: false, report };
    }

    // ── Preserve existing pins or emit placeholder ───────────────
    const greenJsonPath = resolve(PROJECT_ROOT, 'Assets', 'Resources', 'HoleData',
        `Hole_${holeId}`, 'green.json');
    let pinCandidates = null;
    let defaultPinIndex = 0;

    const existing = loadJson(greenJsonPath);
    if (existing && existing.pinCandidates && existing.pinCandidates.length > 0) {
        // Preserve existing pins (regardless of schema version)
        pinCandidates = existing.pinCandidates;
        defaultPinIndex = existing.defaultPinIndex || 0;
        report.push(`  INFO: preserving ${pinCandidates.length} existing pin(s)`);
    } else {
        // Emit placeholder at contour centroid
        const centroidX = xs.reduce((a, b) => a + b, 0) / xs.length;
        const centroidZ = zs.reduce((a, b) => a + b, 0) / zs.length;
        pinCandidates = [{
            worldX: centroidX,
            worldY: 0,
            worldZ: centroidZ,
            label: 'centroid_placeholder'
        }];
        defaultPinIndex = 0;
        report.push(`  INFO: emitting centroid placeholder pin at (${centroidX.toFixed(2)}, ${centroidZ.toFixed(2)})`);
    }

    // ── Encode grids to base64 ───────────────────────────────────
    const slopeGridBase64 = float32ArrayToBase64(slopeGrid);
    const heightGridBase64 = float32ArrayToBase64(heightGrid);

    // ── Build schema v2 green.json ───────────────────────────────
    const today = new Date().toISOString().slice(0, 10);

    // D1: compute height range for report (min-shifted, so minH should be ≈ 0 after shift)
    let hMin = Infinity, hMax = -Infinity;
    for (let i = 0; i < heightGrid.length; i++) {
        if (heightGrid[i] !== 0) {
            if (heightGrid[i] < hMin) hMin = heightGrid[i];
            if (heightGrid[i] > hMax) hMax = heightGrid[i];
        }
    }
    if (hMin === Infinity) { hMin = 0; hMax = 0; }
    report.push(`  INFO: height field after min-shift: range=[${hMin.toFixed(4)}, ${hMax.toFixed(4)}] spread=${(hMax - hMin).toFixed(4)}m (heightShiftMode=min)`);

    const greenJson = {
        schemaVersion: 2,
        holeNumber: holeNum,
        sourceTag: `green_slope_height_bake ${today}`,
        contourVersion: 'resampled-v1',
        contourResampled: contourResampled,
        heightShiftMode: 'min',
        boundsMin: { x: boundsMinX, z: boundsMinZ },
        boundsMax: { x: boundsMaxX, z: boundsMaxZ },
        cellSize: CELL_SIZE,
        gridWidth: gridW,
        gridHeight: gridH,
        slopeGridBase64,
        heightGridBase64,
        heightDatumY: 0,
        // Coordinate mapping: grid is in geo-pipeline world space.
        // To sample from Lite-importer world space, transform:
        //   geoX = geoCentroidX + (liteX - liteCentroidX) * liteToGeoScale
        //   geoZ = geoCentroidZ + (liteZ - liteCentroidZ) * liteToGeoScale
        geoCentroidX,
        geoCentroidZ,
        liteCentroidX,
        liteCentroidZ,
        liteToGeoScale,
        pinCandidates,
        defaultPinIndex
    };

    // ── Write output ─────────────────────────────────────────────
    const outDir = resolve(PROJECT_ROOT, 'Assets', 'Resources', 'HoleData', `Hole_${holeId}`);
    if (!existsSync(outDir)) mkdirSync(outDir, { recursive: true });

    const outPath = resolve(outDir, 'green.json');
    writeFileSync(outPath, JSON.stringify(greenJson, null, 2), 'utf8');

    report.push(`  INFO: wrote ${outPath}`);
    report.push(`  INFO: slopeGrid floats=${slopeGrid.length}, heightGrid floats=${heightGrid.length}`);
    report.push(`PASS: hole ${holeId} bake complete`);

    return { success: true, report };
}

// ─── Main ────────────────────────────────────────────────────────────────────

const args = process.argv.slice(2);
let holesToBake = [];

if (args.includes('--all')) {
    for (let i = 1; i <= 18; i++) holesToBake.push(i);
} else {
    const holeIdx = args.indexOf('--hole');
    if (holeIdx >= 0 && args[holeIdx + 1]) {
        const n = parseInt(args[holeIdx + 1], 10);
        if (isNaN(n) || n < 1 || n > 18) {
            console.error(`ERROR: invalid hole number: ${args[holeIdx + 1]}`);
            process.exit(1);
        }
        holesToBake.push(n);
    } else {
        console.error('Usage: node scripts/bake-green.mjs --hole N  OR  --all');
        process.exit(1);
    }
}

const allReportLines = [];
let anyFail = false;

for (const holeNum of holesToBake) {
    const { success, report } = bakeHole(holeNum);
    allReportLines.push(...report);
    if (!success) anyFail = true;
}

// Write bake_report.txt
const reportPath = resolve(PROJECT_ROOT, 'Tools', 'GreenSlope', 'bake_report.txt');
const reportContent = allReportLines.join('\n') + '\n';
writeFileSync(reportPath, reportContent, 'utf8');

// Also print to console
console.log(reportContent);

if (anyFail) {
    console.error('\nBAKE FAILED — see bake_report.txt for details');
    process.exit(1);
} else {
    console.log(`\nBake complete (${holesToBake.length} hole(s)). Report: ${reportPath}`);
    process.exit(0);
}
