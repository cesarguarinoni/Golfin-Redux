/**
 * compute-transform.mjs
 *
 * Computes a best-fit affine transformation from control point pairs
 * (official map pixel coords → world lat/lon) and writes the result
 * into alignment.json.
 *
 * Usage:
 *   node scripts/compute-transform.mjs 1        # single hole
 *   node scripts/compute-transform.mjs --all    # all eligible holes
 */

import { readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const holesRoot = path.join(root, "output", "lomond-country-club", "holes");

const REF_LAT = 34.91; // Lomond Country Club latitude for meter conversion

// --- Linear algebra helpers (3×3) ---

/** Multiply 3×3 matrix A by 3×1 vector v → 3×1 result */
function mat3MulVec(A, v) {
  return [
    A[0][0] * v[0] + A[0][1] * v[1] + A[0][2] * v[2],
    A[1][0] * v[0] + A[1][1] * v[1] + A[1][2] * v[2],
    A[2][0] * v[0] + A[2][1] * v[1] + A[2][2] * v[2]
  ];
}

/** Multiply Aᵀ (3×N) by N×1 vector b → 3×1 result, where A is N×3 */
function matTMulVec(A, b) {
  const rows = A.length;
  const result = [0, 0, 0];
  for (let i = 0; i < 3; i++) {
    for (let j = 0; j < rows; j++) {
      result[i] += A[j][i] * b[j];
    }
  }
  return result;
}

/** Compute AᵀA for an N×3 matrix A → 3×3 result */
function matTMulMat(A) {
  const n = A.length;
  const result = [[0, 0, 0], [0, 0, 0], [0, 0, 0]];
  for (let i = 0; i < 3; i++) {
    for (let j = 0; j < 3; j++) {
      let sum = 0;
      for (let k = 0; k < n; k++) {
        sum += A[k][i] * A[k][j];
      }
      result[i][j] = sum;
    }
  }
  return result;
}

/** Invert a 3×3 matrix via Cramer's rule. Returns null if singular. */
function mat3Inverse(m) {
  const [a, b, c] = [m[0][0], m[0][1], m[0][2]];
  const [d, e, f] = [m[1][0], m[1][1], m[1][2]];
  const [g, h, i] = [m[2][0], m[2][1], m[2][2]];

  const det = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
  if (Math.abs(det) < 1e-15) {
    return null;
  }

  const invDet = 1 / det;
  return [
    [(e * i - f * h) * invDet, (c * h - b * i) * invDet, (b * f - c * e) * invDet],
    [(f * g - d * i) * invDet, (a * i - c * g) * invDet, (c * d - a * f) * invDet],
    [(d * h - e * g) * invDet, (b * g - a * h) * invDet, (a * e - b * d) * invDet]
  ];
}

/**
 * Solve least-squares: A x = b where A is N×3, b is N×1.
 * Returns 3×1 vector x = (AᵀA)⁻¹ Aᵀb.
 */
function leastSquares(A, b) {
  const AtA = matTMulMat(A);
  const Atb = matTMulVec(A, b);
  const inv = mat3Inverse(AtA);
  if (!inv) {
    throw new Error("Singular matrix — control points may be collinear.");
  }
  return mat3MulVec(inv, Atb);
}

// --- Distance helpers ---

function latLonErrorToMeters(dLat, dLon, refLat) {
  const dLatM = dLat * 111320;
  const dLonM = dLon * 111320 * Math.cos(refLat * Math.PI / 180);
  return Math.sqrt(dLatM * dLatM + dLonM * dLonM);
}

// --- Main logic ---

function computeTransformForAlignment(alignment) {
  const points = alignment.control_points;
  if (!points || points.length < 3) {
    return { ok: false, message: `Need at least 3 control points, have ${points?.length || 0}.` };
  }

  // Build the design matrix A (N×3) and target vectors for lon and lat
  const A = [];
  const bLon = [];
  const bLat = [];

  for (const cp of points) {
    const px = cp.official.x;
    const py = cp.official.y;
    A.push([px, py, 1]);
    bLon.push(cp.basemap.lon);
    bLat.push(cp.basemap.lat);
  }

  // Solve: lon = a*px + b*py + tx
  const [a, b, tx] = leastSquares(A, bLon);
  // Solve: lat = c*px + d*py + ty
  const [c, d, ty] = leastSquares(A, bLat);

  // Compute residuals
  const residuals = [];
  let sumError = 0;
  let maxError = 0;

  for (let i = 0; i < points.length; i++) {
    const cp = points[i];
    const px = cp.official.x;
    const py = cp.official.y;
    const predictedLon = a * px + b * py + tx;
    const predictedLat = c * px + d * py + ty;
    const dLon = predictedLon - cp.basemap.lon;
    const dLat = predictedLat - cp.basemap.lat;
    const errorM = latLonErrorToMeters(dLat, dLon, REF_LAT);

    residuals.push({ point_index: i, error_m: Math.round(errorM * 100) / 100 });
    sumError += errorM;
    if (errorM > maxError) {
      maxError = errorM;
    }
  }

  const meanResidual = Math.round((sumError / points.length) * 100) / 100;
  const maxResidual = Math.round(maxError * 100) / 100;

  return {
    ok: true,
    transform: {
      pixel_to_world_ready: true,
      method: "least_squares_affine",
      coefficients: { a, b, tx, c, d, ty },
      residuals,
      mean_residual_m: meanResidual,
      max_residual_m: maxResidual,
      computed_at: new Date().toISOString()
    },
    meanResidual,
    maxResidual,
    pointCount: points.length
  };
}

async function processHole(holeNumber) {
  const padded = String(holeNumber).padStart(2, "0");
  const alignmentPath = path.join(holesRoot, padded, "alignment.json");

  let alignment;
  try {
    alignment = JSON.parse(await readFile(alignmentPath, "utf8"));
  } catch {
    return { ok: false, message: `Hole ${holeNumber}: alignment.json not found.` };
  }

  if (alignment.status !== "ready_for_transform") {
    return { ok: false, message: `Hole ${holeNumber}: status is "${alignment.status}", not "ready_for_transform". Skipping.` };
  }

  const result = computeTransformForAlignment(alignment);
  if (!result.ok) {
    return result;
  }

  // Write back into alignment.json
  alignment.transform = result.transform;
  alignment.status = "transform_computed";
  await writeFile(alignmentPath, JSON.stringify(alignment, null, 2) + "\n", "utf8");

  const summary = `Hole ${holeNumber}: affine transform computed from ${result.pointCount} control points\n  Mean residual: ${result.meanResidual}m | Max residual: ${result.maxResidual}m`;
  return { ok: true, message: summary, ...result };
}

// --- CLI entry point ---

async function main() {
  const args = process.argv.slice(2);

  if (args.includes("--all")) {
    let processed = 0;
    let skipped = 0;
    for (let h = 1; h <= 18; h++) {
      const result = await processHole(h);
      if (result.ok) {
        console.log(result.message);
        processed++;
      } else {
        console.log(result.message);
        skipped++;
      }
    }
    console.log(`\nDone: ${processed} transformed, ${skipped} skipped.`);
    return;
  }

  const holeNumber = Number(args[0]);
  if (!Number.isInteger(holeNumber) || holeNumber < 1 || holeNumber > 18) {
    console.error("Usage: node scripts/compute-transform.mjs <holeNumber|--all>");
    process.exit(1);
  }

  const result = await processHole(holeNumber);
  console.log(result.message);
  if (!result.ok) {
    process.exit(1);
  }
}

// Also export for inline use from dev-server
export { processHole, computeTransformForAlignment };

// Only run CLI when invoked directly (not when imported)
const isMain = process.argv[1] && path.resolve(process.argv[1]) === path.resolve(fileURLToPath(import.meta.url));
if (isMain) {
  main();
}
