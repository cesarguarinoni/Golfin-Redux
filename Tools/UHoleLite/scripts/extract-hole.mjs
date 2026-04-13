#!/usr/bin/env node
/**
 * extract-hole.mjs — Step 2: Crop illustration from GIF, remove legend, upscale
 *
 * Usage:
 *   node scripts/extract-hole.mjs lomond-country-club 1       # single hole
 *   node scripts/extract-hole.mjs lomond-country-club --all   # all 18
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import sharp from 'sharp';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');

// Known hole descriptions (from GIF info panels)
const HOLE_DESCRIPTIONS = {
  1: 'ティーショットは右側が広く、二段のフェアウェイセンターの傾斜部が狙い目です。２打目の落とし所が重要。',
  4: '最も短いショートホール。グリーン右から傾斜の為、右サイドから攻めるのがベスト。グリーン左斜面下に落ちると苦戦します。',
  8: '右ドッグレッグのロングホール。ティーショットはフェアウェイセンターやや左狙い。セカンド以降は左右が狭いので手堅くきざむのが無難。距離の出る人は２オン可能なホール。',
  13: '左ドッグレッグのロングホール。左は全てＯＢとなっている為、ティーショットはセンター狙いで行くと良い。セカンドからはグリーン左が崖になっている為、セカンドもセンター狙いに確実にきざんで、３打目勝負。※特設ティあり',
};

// ---------------------------------------------------------------------------
// Find the split column (rightmost column that is still mostly solid black)
// ---------------------------------------------------------------------------

async function findSplitColumn(imgBuffer, width, height) {
  // Get raw pixel data (RGBA)
  const { data } = await sharp(imgBuffer)
    .raw()
    .ensureAlpha()
    .toBuffer({ resolveWithObject: true });

  // Scan columns from left to right, find the rightmost one that is >80% black
  let splitCol = 0;

  for (let x = 0; x < Math.min(400, width); x++) {
    let blackCount = 0;
    for (let y = 0; y < height; y++) {
      const idx = (y * width + x) * 4;
      const r = data[idx], g = data[idx + 1], b = data[idx + 2];
      // Truly black: all channels < 15
      if (r < 15 && g < 15 && b < 15) blackCount++;
    }
    const blackRatio = blackCount / height;
    if (blackRatio > 0.80) {
      splitCol = x;
    }
  }

  // Add a small buffer to avoid border artifacts
  return splitCol + 3;
}

// ---------------------------------------------------------------------------
// Detect and fill the yardage legend in the bottom-right corner
// ---------------------------------------------------------------------------

async function removeLegend(imgBuffer, cropWidth, cropHeight) {
  // Get raw pixels
  const rawImg = sharp(imgBuffer).raw().ensureAlpha();
  const { data, info } = await rawImg.toBuffer({ resolveWithObject: true });
  const { width, height, channels } = info;

  // Detect the legend by finding tee-colored dots in the bottom-right quadrant.
  // The legend has red, green, blue, and white dots stacked vertically with
  // yardage text to the right of each dot.
  const searchX0 = Math.floor(width * 0.7);
  const searchY0 = Math.floor(height * 0.6);

  // Find colored dot clusters (red, blue, green)
  const dotPositions = [];
  for (let y = searchY0; y < height; y++) {
    for (let x = searchX0; x < width; x++) {
      const idx = (y * width + x) * channels;
      const r = data[idx], g = data[idx + 1], b = data[idx + 2];
      const isRed = r > 180 && g < 80 && b < 80;
      const isBlue = b > 150 && r < 80 && g < 80;
      const isGreen = g > 150 && r < 80 && b < 80;
      if (isRed || isBlue || isGreen) {
        dotPositions.push({ x, y });
      }
    }
  }

  if (dotPositions.length === 0) {
    // No legend dots found — return as-is
    return imgBuffer;
  }

  // Compute bounding box of the legend from dot positions
  const minDotX = Math.min(...dotPositions.map(p => p.x));
  const minDotY = Math.min(...dotPositions.map(p => p.y));
  const maxDotY = Math.max(...dotPositions.map(p => p.y));

  // Legend region: extend left of dots by 15px (padding), extend right to edge,
  // extend above first dot by 10px, extend below last dot by 15px (for text)
  const legendX = Math.max(0, minDotX - 15);
  const legendY = Math.max(0, minDotY - 10);
  const legendBottom = Math.min(height, maxDotY + 18);

  // For each row in the legend region, sample color from a strip to the left
  for (let y = legendY; y < legendBottom; y++) {
    let rSum = 0, gSum = 0, bSum = 0, sCount = 0;
    const sampleX0 = Math.max(0, legendX - 10);
    const sampleX1 = legendX;
    for (let x = sampleX0; x < sampleX1; x++) {
      const idx = (y * width + x) * channels;
      rSum += data[idx];
      gSum += data[idx + 1];
      bSum += data[idx + 2];
      sCount++;
    }
    const fillR = sCount > 0 ? Math.round(rSum / sCount) : 200;
    const fillG = sCount > 0 ? Math.round(gSum / sCount) : 200;
    const fillB = sCount > 0 ? Math.round(bSum / sCount) : 200;

    for (let x = legendX; x < width; x++) {
      const idx = (y * width + x) * channels;
      data[idx] = fillR;
      data[idx + 1] = fillG;
      data[idx + 2] = fillB;
      data[idx + 3] = 255;
    }
  }

  return sharp(data, { raw: { width, height, channels } }).png().toBuffer();
}

// ---------------------------------------------------------------------------
// Trim gold/tan border from bottom and right edges
// ---------------------------------------------------------------------------

async function trimBorders(imgBuffer) {
  const { data, info } = await sharp(imgBuffer)
    .raw().ensureAlpha()
    .toBuffer({ resolveWithObject: true });
  const { width, height, channels } = info;

  // Check bottom rows for uniform color (gold/tan border)
  let trimBottom = 0;
  for (let row = height - 1; row >= height - 5; row--) {
    // Sample a few pixels from this row
    const samples = [];
    for (let x = Math.floor(width * 0.2); x < Math.floor(width * 0.8); x += 20) {
      const idx = (row * width + x) * channels;
      samples.push({ r: data[idx], g: data[idx + 1], b: data[idx + 2] });
    }
    // Check if row is uniform (all samples similar) and not part of the illustration
    const avg = {
      r: samples.reduce((s, p) => s + p.r, 0) / samples.length,
      g: samples.reduce((s, p) => s + p.g, 0) / samples.length,
      b: samples.reduce((s, p) => s + p.b, 0) / samples.length,
    };
    const isUniform = samples.every(p =>
      Math.abs(p.r - avg.r) < 15 && Math.abs(p.g - avg.g) < 15 && Math.abs(p.b - avg.b) < 15
    );
    // Gold/tan border: warm color, relatively bright
    const isGold = avg.r > 100 && avg.g > 80 && avg.b < avg.r && isUniform;
    // Also check for black border
    const isBlack = avg.r < 20 && avg.g < 20 && avg.b < 20 && isUniform;
    if (isGold || isBlack) {
      trimBottom++;
    } else {
      break;
    }
  }

  // Check right columns similarly
  let trimRight = 0;
  for (let col = width - 1; col >= width - 5; col--) {
    const samples = [];
    for (let y = Math.floor(height * 0.2); y < Math.floor(height * 0.8); y += 20) {
      const idx = (y * width + col) * channels;
      samples.push({ r: data[idx], g: data[idx + 1], b: data[idx + 2] });
    }
    const avg = {
      r: samples.reduce((s, p) => s + p.r, 0) / samples.length,
      g: samples.reduce((s, p) => s + p.g, 0) / samples.length,
      b: samples.reduce((s, p) => s + p.b, 0) / samples.length,
    };
    const isUniform = samples.every(p =>
      Math.abs(p.r - avg.r) < 15 && Math.abs(p.g - avg.g) < 15 && Math.abs(p.b - avg.b) < 15
    );
    const isGold = avg.r > 100 && avg.g > 80 && avg.b < avg.r && isUniform;
    const isBlack = avg.r < 20 && avg.g < 20 && avg.b < 20 && isUniform;
    if (isGold || isBlack) {
      trimRight++;
    } else {
      break;
    }
  }

  if (trimBottom === 0 && trimRight === 0) return { buffer: imgBuffer, trimBottom: 0, trimRight: 0 };

  const newW = width - trimRight;
  const newH = height - trimBottom;
  const buffer = await sharp(imgBuffer)
    .extract({ left: 0, top: 0, width: newW, height: newH })
    .png()
    .toBuffer();
  return { buffer, trimBottom, trimRight };
}

// ---------------------------------------------------------------------------
// Process a single hole
// ---------------------------------------------------------------------------

async function processHole(courseId, holeNumber, config) {
  const nn = String(holeNumber).padStart(2, '0');
  const outputDir = path.join(ROOT, 'output', courseId);
  const sourceDir = path.join(outputDir, 'source');
  const holeDir = path.join(outputDir, 'holes', nn);

  const gifPath = path.join(sourceDir, `course_e${nn}.gif`);
  if (!fs.existsSync(gifPath)) {
    console.error(`  Source GIF not found: ${gifPath}`);
    return false;
  }

  fs.mkdirSync(holeDir, { recursive: true });

  // Load original GIF
  const gifBuffer = fs.readFileSync(gifPath);
  const metadata = await sharp(gifBuffer).metadata();
  const srcWidth = metadata.width;
  const srcHeight = metadata.height;

  // Step 1: Find split column
  const splitCol = await findSplitColumn(gifBuffer, srcWidth, srcHeight);

  // Step 2: Crop illustration (right side only)
  const cropX = splitCol;
  const cropY = 0;
  const cropW = srcWidth - splitCol;
  const cropH = srcHeight;

  let croppedBuffer = await sharp(gifBuffer)
    .extract({ left: cropX, top: cropY, width: cropW, height: cropH })
    .png()
    .toBuffer();

  // Step 3: Trim borders
  const { buffer: trimmedBuffer, trimBottom, trimRight } = await trimBorders(croppedBuffer);
  croppedBuffer = trimmedBuffer;

  const trimmedMeta = await sharp(croppedBuffer).metadata();
  const afterTrimW = trimmedMeta.width;
  const afterTrimH = trimmedMeta.height;

  // Save raw crop (before legend removal and upscale)
  const rawPath = path.join(holeDir, 'illustration_raw.png');
  fs.writeFileSync(rawPath, croppedBuffer);

  // Step 4: Remove yardage legend
  const cleanedBuffer = await removeLegend(croppedBuffer, afterTrimW, afterTrimH);

  // Step 5: Upscale to 2048px on longest side, maintaining aspect ratio
  // Matches ZONE_RES in classify-zones.mjs so texture and zone grid align 1:1
  const targetLongest = 2048;
  const finalBuffer = await sharp(cleanedBuffer)
    .resize(
      afterTrimW >= afterTrimH ? targetLongest : null,
      afterTrimH > afterTrimW ? targetLongest : null,
      {
        kernel: sharp.kernel.lanczos3,
        withoutEnlargement: false,
      }
    )
    .sharpen({ sigma: 0.8 })
    .png()
    .toFile(path.join(holeDir, 'illustration.png'));

  // Get final dimensions
  const finalMeta = await sharp(path.join(holeDir, 'illustration.png')).metadata();
  const finalWidth = finalMeta.width;
  const finalHeight = finalMeta.height;
  const aspectRatio = finalHeight / finalWidth;

  // Legend region — detected dynamically during removeLegend, use approximate for metadata
  const legendRegion = { note: 'auto-detected from tee dot colors' };

  // Write extract metadata
  const extractMeta = {
    hole_number: holeNumber,
    source_gif: `source/course_e${nn}.gif`,
    source_dimensions: { width: srcWidth, height: srcHeight },
    split_column: splitCol,
    crop: { x: cropX, y: cropY, width: cropW, height: cropH },
    trim: { bottom: trimBottom, right: trimRight },
    legend_region: legendRegion,
    final_dimensions: { width: finalWidth, height: finalHeight },
    aspect_ratio: parseFloat(aspectRatio.toFixed(3)),
  };

  fs.writeFileSync(
    path.join(holeDir, 'extract-meta.json'),
    JSON.stringify(extractMeta, null, 2),
    'utf-8'
  );

  return extractMeta;
}

// ---------------------------------------------------------------------------
// Update course.json with hole descriptions
// ---------------------------------------------------------------------------

function updateCourseDescriptions(courseId) {
  const coursePath = path.join(ROOT, 'output', courseId, 'course.json');
  if (!fs.existsSync(coursePath)) return;

  const course = JSON.parse(fs.readFileSync(coursePath, 'utf-8'));
  let updated = false;
  for (const hole of course.holes) {
    if (HOLE_DESCRIPTIONS[hole.number] && !hole.description_jp) {
      hole.description_jp = HOLE_DESCRIPTIONS[hole.number];
      updated = true;
    }
  }
  if (updated) {
    fs.writeFileSync(coursePath, JSON.stringify(course, null, 2), 'utf-8');
    console.log(`  Updated course.json with ${Object.keys(HOLE_DESCRIPTIONS).length} hole descriptions.`);
  }
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

async function main() {
  const courseId = process.argv[2];
  const holeArg = process.argv[3];

  if (!courseId || !holeArg) {
    console.error('Usage: node scripts/extract-hole.mjs <course-id> <hole-number|--all>');
    process.exit(1);
  }

  // Load config
  const configPath = path.join(ROOT, 'config', `${courseId}.json`);
  if (!fs.existsSync(configPath)) {
    console.error(`Config not found: ${configPath}`);
    process.exit(1);
  }
  const config = JSON.parse(fs.readFileSync(configPath, 'utf-8'));

  const holes = holeArg === '--all'
    ? Array.from({ length: 18 }, (_, i) => i + 1)
    : [parseInt(holeArg, 10)];

  if (holes.some(h => isNaN(h) || h < 1 || h > 18)) {
    console.error('Hole number must be 1-18 or --all');
    process.exit(1);
  }

  console.log(`Extracting ${holes.length} hole(s) for ${config.display_name}\n`);

  let successCount = 0;
  for (const h of holes) {
    process.stdout.write(`Hole ${h}/18 ... `);
    try {
      const meta = await processHole(courseId, h, config);
      if (meta) {
        console.log(`OK  split=${meta.split_column}  crop=${meta.crop.width}x${meta.crop.height}  final=${meta.final_dimensions.width}x${meta.final_dimensions.height}`);
        successCount++;
      }
    } catch (err) {
      console.error(`FAILED: ${err.message}`);
    }
  }

  // Update course.json with descriptions
  updateCourseDescriptions(courseId);

  console.log(`\nDone: ${successCount}/${holes.length} holes processed.`);
}

main().catch(err => {
  console.error('Fatal error:', err);
  process.exit(1);
});
