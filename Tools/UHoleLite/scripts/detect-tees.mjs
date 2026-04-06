#!/usr/bin/env node
/**
 * detect-tees.mjs — Step 4: Find colored tee marker dots in hole illustrations
 *
 * Usage:
 *   node scripts/detect-tees.mjs lomond-country-club 1      # single hole
 *   node scripts/detect-tees.mjs lomond-country-club --all   # all 18
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import sharp from 'sharp';
import { rgbToHsl, hueInRange } from './lib/colors.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');

// ---------------------------------------------------------------------------
// Tee color matchers (using HSL space)
// ---------------------------------------------------------------------------

const TEE_MATCHERS = {
  back: {
    type: 'tee_back',
    color: 'blue',
    match: (r, g, b) => {
      const { h, s, l } = rgbToHsl(r, g, b);
      return h >= 210 && h <= 260 && s > 0.50 && l > 0.15 && l < 0.70;
    },
  },
  regular: {
    type: 'tee_regular',
    color: 'green',
    match: (r, g, b) => {
      const { h, s, l } = rgbToHsl(r, g, b);
      // Bright, saturated green — must be vivid enough to distinguish from fairway
      return h >= 80 && h <= 160 && s > 0.55 && l > 0.30 && l < 0.65;
    },
  },
  front: {
    type: 'tee_front',
    color: 'white',
    match: (r, g, b) => {
      const { s, l } = rgbToHsl(r, g, b);
      return s < 0.15 && l > 0.85;
    },
  },
  ladies: {
    type: 'tee_ladies',
    color: 'red',
    match: (r, g, b) => {
      const { h, s, l } = rgbToHsl(r, g, b);
      return hueInRange(h, [340, 20]) && s > 0.60 && l > 0.15 && l < 0.65;
    },
  },
};

// ---------------------------------------------------------------------------
// Connected-component labeling (simple flood-fill)
// ---------------------------------------------------------------------------

function findClusters(matchMask, width, height, minSize, maxSize) {
  const visited = new Uint8Array(width * height);
  const clusters = [];

  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const idx = y * width + x;
      if (!matchMask[idx] || visited[idx]) continue;

      // BFS flood-fill
      const pixels = [];
      const queue = [{ x, y }];
      visited[idx] = 1;

      while (queue.length > 0) {
        const p = queue.shift();
        pixels.push(p);

        // 4-connected neighbors
        for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
          const nx = p.x + dx, ny = p.y + dy;
          if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
          const nIdx = ny * width + nx;
          if (!matchMask[nIdx] || visited[nIdx]) continue;
          visited[nIdx] = 1;
          queue.push({ x: nx, y: ny });
        }
      }

      if (pixels.length >= minSize && pixels.length <= maxSize) {
        // Compute centroid and bounding box
        let sumX = 0, sumY = 0;
        let minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity;
        for (const p of pixels) {
          sumX += p.x; sumY += p.y;
          minX = Math.min(minX, p.x); maxX = Math.max(maxX, p.x);
          minY = Math.min(minY, p.y); maxY = Math.max(maxY, p.y);
        }
        const cx = Math.round(sumX / pixels.length);
        const cy = Math.round(sumY / pixels.length);
        const bboxW = maxX - minX + 1;
        const bboxH = maxY - minY + 1;
        const aspectRatio = bboxW / bboxH;

        clusters.push({
          centroid: { x: cx, y: cy },
          size: pixels.length,
          bbox: { x: minX, y: minY, w: bboxW, h: bboxH },
          aspectRatio,
        });
      }
    }
  }

  return clusters;
}

// ---------------------------------------------------------------------------
// Detect tees for a single hole
// ---------------------------------------------------------------------------

async function detectTees(courseId, holeNumber) {
  const nn = String(holeNumber).padStart(2, '0');
  const holeDir = path.join(ROOT, 'output', courseId, 'holes', nn);
  const rawPath = path.join(holeDir, 'illustration_raw.png');

  if (!fs.existsSync(rawPath)) {
    console.error(`  illustration_raw.png not found for hole ${holeNumber}`);
    return null;
  }

  // Load course.json for yardage data
  const courseJson = JSON.parse(
    fs.readFileSync(path.join(ROOT, 'output', courseId, 'course.json'), 'utf-8')
  );
  const holeData = courseJson.holes.find(h => h.number === holeNumber);

  // Read raw pixel data
  const { data, info } = await sharp(rawPath)
    .ensureAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  const { width, height, channels } = info;

  const results = [];

  // Detect each tee color
  for (const [teeKey, matcher] of Object.entries(TEE_MATCHERS)) {
    // Build match mask
    const mask = new Uint8Array(width * height);
    for (let y = 0; y < height; y++) {
      for (let x = 0; x < width; x++) {
        const idx = (y * width + x) * channels;
        const r = data[idx], g = data[idx + 1], b = data[idx + 2];
        if (matcher.match(r, g, b)) {
          mask[y * width + x] = 1;
        }
      }
    }

    // Find clusters (tee dots are 15-250 pixels at raw resolution)
    const clusters = findClusters(mask, width, height, 15, 250);

    // Filter clusters
    const filtered = clusters.filter(c => {
      // Shape: roughly circular (aspect ratio 0.3 to 3.0)
      if (c.aspectRatio < 0.3 || c.aspectRatio > 3.0) return false;
      // Location: not in the very top 10% (that's the green end)
      // Relaxed for par 3 holes where tees can be mid-image
      const topCutoff = holeData && holeData.par === 3 ? 0.20 : 0.35;
      if (c.centroid.y < height * topCutoff) return false;
      return true;
    });

    results.push({
      key: teeKey,
      matcher,
      clusters: filtered,
    });
  }

  // Mutual validation: find the set of 4 tees that are closest to each other.
  // Start with the tee that has the fewest candidates (most constrained).
  // Then pick candidates for others that minimize spread.
  const teePositions = selectBestTees(results, width, height);

  // Build output
  const tees = [];
  const warnings = [];
  let allFound = true;

  for (const teeKey of ['back', 'regular', 'front', 'ladies']) {
    const pos = teePositions[teeKey];
    const yardKey = teeKey === 'back' ? 'back' : teeKey === 'regular' ? 'regular'
      : teeKey === 'front' ? 'front' : 'ladies';
    const yards = holeData ? holeData.tees[yardKey].yards : null;

    if (pos) {
      tees.push({
        type: `tee_${teeKey}`,
        color: TEE_MATCHERS[teeKey].color,
        yards,
        pixel: pos.centroid,
        normalized: {
          x: parseFloat((pos.centroid.x / width).toFixed(3)),
          y: parseFloat((pos.centroid.y / height).toFixed(3)),
        },
        cluster_size: pos.size,
        confidence: pos.confidence,
      });
    } else {
      allFound = false;
      warnings.push(`${teeKey} tee not found`);
      tees.push({
        type: `tee_${teeKey}`,
        color: TEE_MATCHERS[teeKey].color,
        yards,
        pixel: null,
        normalized: null,
        cluster_size: 0,
        confidence: 'missing',
      });
    }
  }

  // Compute validation metrics
  const foundTees = tees.filter(t => t.pixel !== null);
  let maxSpread = 0;
  let collinearScore = 0;

  if (foundTees.length >= 2) {
    for (let i = 0; i < foundTees.length; i++) {
      for (let j = i + 1; j < foundTees.length; j++) {
        const dx = foundTees[i].pixel.x - foundTees[j].pixel.x;
        const dy = foundTees[i].pixel.y - foundTees[j].pixel.y;
        maxSpread = Math.max(maxSpread, Math.sqrt(dx * dx + dy * dy));
      }
    }
  }

  if (foundTees.length >= 3) {
    collinearScore = computeCollinearity(foundTees.map(t => t.pixel));
  }

  if (maxSpread > 120) {
    warnings.push(`large spread: ${Math.round(maxSpread)}px`);
  }

  const output = {
    hole_number: holeNumber,
    source: 'illustration_raw.png',
    source_dimensions: { width, height },
    tees,
    validation: {
      all_found: allFound,
      max_spread_px: Math.round(maxSpread),
      collinear_score: parseFloat(collinearScore.toFixed(2)),
      warnings,
    },
  };

  const outPath = path.join(holeDir, 'tees.json');
  fs.writeFileSync(outPath, JSON.stringify(output, null, 2), 'utf-8');

  return output;
}

// ---------------------------------------------------------------------------
// Select best tee positions using mutual proximity
// ---------------------------------------------------------------------------

function selectBestTees(results, imgWidth, imgHeight) {
  // For each tee color, we have a list of candidate clusters.
  // We want to pick one candidate per color such that they're all close together.

  const candidates = {};
  for (const r of results) {
    candidates[r.key] = r.clusters;
  }

  // Find the tee with the fewest candidates to anchor the search
  const keys = Object.keys(candidates);
  keys.sort((a, b) => candidates[a].length - candidates[b].length);

  // If any tee has no candidates, handle gracefully
  const selected = {};

  // First pass: find blue and red (most distinctive colors)
  const blueCluster = pickMostIsolated(candidates.back, imgWidth, imgHeight);
  const redCluster = pickMostIsolated(candidates.ladies, imgWidth, imgHeight);

  // If we have both blue and red, use their midpoint as anchor
  let anchorX, anchorY;
  if (blueCluster && redCluster) {
    anchorX = (blueCluster.centroid.x + redCluster.centroid.x) / 2;
    anchorY = (blueCluster.centroid.y + redCluster.centroid.y) / 2;
    selected.back = { ...blueCluster, confidence: 'high' };
    selected.ladies = { ...redCluster, confidence: 'high' };
  } else if (blueCluster) {
    anchorX = blueCluster.centroid.x;
    anchorY = blueCluster.centroid.y;
    selected.back = { ...blueCluster, confidence: 'high' };
  } else if (redCluster) {
    anchorX = redCluster.centroid.x;
    anchorY = redCluster.centroid.y;
    selected.ladies = { ...redCluster, confidence: 'high' };
  }

  // Pick green and white tees closest to anchor
  if (anchorX !== undefined) {
    if (!selected.regular && candidates.regular.length > 0) {
      const best = pickClosest(candidates.regular, anchorX, anchorY);
      const dist = distance(best.centroid, { x: anchorX, y: anchorY });
      selected.regular = {
        ...best,
        confidence: dist < 80 ? 'high' : dist < 120 ? 'medium' : 'low',
      };
    }
    if (!selected.front && candidates.front.length > 0) {
      const best = pickClosest(candidates.front, anchorX, anchorY);
      const dist = distance(best.centroid, { x: anchorX, y: anchorY });
      selected.front = {
        ...best,
        confidence: dist < 80 ? 'high' : dist < 120 ? 'medium' : 'low',
      };
    }
    if (!selected.ladies && candidates.ladies.length > 0) {
      const best = pickClosest(candidates.ladies, anchorX, anchorY);
      const dist = distance(best.centroid, { x: anchorX, y: anchorY });
      selected.ladies = {
        ...best,
        confidence: dist < 80 ? 'high' : dist < 120 ? 'medium' : 'low',
      };
    }
    if (!selected.back && candidates.back.length > 0) {
      const best = pickClosest(candidates.back, anchorX, anchorY);
      const dist = distance(best.centroid, { x: anchorX, y: anchorY });
      selected.back = {
        ...best,
        confidence: dist < 80 ? 'high' : dist < 120 ? 'medium' : 'low',
      };
    }
  } else {
    // No anchor — just pick largest cluster for each
    for (const key of keys) {
      if (!selected[key] && candidates[key].length > 0) {
        selected[key] = { ...candidates[key][0], confidence: 'low' };
      }
    }
  }

  return selected;
}

function pickMostIsolated(clusters, imgWidth, imgHeight) {
  if (clusters.length === 0) return null;
  if (clusters.length === 1) return clusters[0];

  // Prefer clusters in the bottom half and of moderate size
  const scored = clusters.map(c => {
    let score = 0;
    // Prefer bottom half of image (where tees usually are)
    score += (c.centroid.y / imgHeight) * 10;
    // Prefer moderate size (30-100 is ideal for a tee dot)
    if (c.size >= 30 && c.size <= 100) score += 5;
    // Prefer circular shape
    if (c.aspectRatio >= 0.6 && c.aspectRatio <= 1.5) score += 3;
    return { cluster: c, score };
  });
  scored.sort((a, b) => b.score - a.score);
  return scored[0].cluster;
}

function pickClosest(clusters, ax, ay) {
  let best = clusters[0];
  let bestDist = distance(best.centroid, { x: ax, y: ay });
  for (let i = 1; i < clusters.length; i++) {
    const d = distance(clusters[i].centroid, { x: ax, y: ay });
    if (d < bestDist) {
      best = clusters[i];
      bestDist = d;
    }
  }
  return best;
}

function distance(a, b) {
  return Math.sqrt((a.x - b.x) ** 2 + (a.y - b.y) ** 2);
}

function computeCollinearity(points) {
  if (points.length < 3) return 1.0;
  // Fit a line through the points and compute R² as collinearity measure
  const n = points.length;
  let sumX = 0, sumY = 0, sumXX = 0, sumXY = 0, sumYY = 0;
  for (const p of points) {
    sumX += p.x; sumY += p.y;
    sumXX += p.x * p.x; sumXY += p.x * p.y; sumYY += p.y * p.y;
  }
  const meanX = sumX / n, meanY = sumY / n;
  let ssXX = 0, ssYY = 0, ssXY = 0;
  for (const p of points) {
    ssXX += (p.x - meanX) ** 2;
    ssYY += (p.y - meanY) ** 2;
    ssXY += (p.x - meanX) * (p.y - meanY);
  }
  if (ssXX === 0 || ssYY === 0) return 1.0;
  const r = ssXY / Math.sqrt(ssXX * ssYY);
  return Math.abs(r); // R value (not R²) — 1.0 = perfectly collinear
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

async function main() {
  const courseId = process.argv[2];
  const holeArg = process.argv[3];

  if (!courseId || !holeArg) {
    console.error('Usage: node scripts/detect-tees.mjs <course-id> <hole-number|--all>');
    process.exit(1);
  }

  const configPath = path.join(ROOT, 'config', `${courseId}.json`);
  if (!fs.existsSync(configPath)) {
    console.error(`Config not found: ${configPath}`);
    process.exit(1);
  }

  const holes = holeArg === '--all'
    ? Array.from({ length: 18 }, (_, i) => i + 1)
    : [parseInt(holeArg, 10)];

  if (holes.some(h => isNaN(h) || h < 1 || h > 18)) {
    console.error('Hole number must be 1-18 or --all');
    process.exit(1);
  }

  console.log(`Detecting tees for ${holes.length} hole(s)\n`);

  let totalFound = 0, totalMissing = 0;

  for (const h of holes) {
    console.log(`Detecting tees for hole ${h}...`);
    const result = await detectTees(courseId, h);

    if (!result) continue;

    for (const tee of result.tees) {
      const label = `  ${tee.color.padEnd(6)} (${tee.type.replace('tee_', '').padEnd(7)}):`;
      if (tee.pixel) {
        console.log(`${label} found at (${tee.pixel.x}, ${tee.pixel.y}) — ${tee.cluster_size}px cluster, confidence: ${tee.confidence}`);
        totalFound++;
      } else {
        console.log(`${label} NOT FOUND`);
        totalMissing++;
      }
    }

    const v = result.validation;
    const check = v.all_found ? '\u2713' : '\u2717';
    console.log(`  Validation: ${check} ${v.all_found ? 'all found' : 'MISSING TEES'}, spread=${v.max_spread_px}px, collinear=${v.collinear_score}`);
    if (v.warnings.length > 0) {
      console.log(`  Warnings: ${v.warnings.join(', ')}`);
    }
    const nn = String(h).padStart(2, '0');
    console.log(`  → Saved holes/${nn}/tees.json\n`);
  }

  console.log(`\nSummary: ${totalFound} tees found, ${totalMissing} missing (across ${holes.length} holes)`);
}

main().catch(err => {
  console.error('Fatal error:', err);
  process.exit(1);
});
