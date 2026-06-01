import { mkdir, readFile, writeFile } from "node:fs/promises";
import { stat } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { degreeOffsetsFromMeters, enumerateTiles, lonToTileX, latToTileY, tileSpanMeters } from "./lib/tiles.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");

// ---------------------------------------------------------------------------
// CLI parameterization (2026-06-01): this fetcher was originally hardcoded to
// Lomond. It now accepts an optional course slug + center lat/lon so the same
// script pulls GSI basemap tiles (photo + DEM + DEM5A lidar) for ANY course.
//
//   node scripts/fetch-gsi-basemap.mjs                                  # Lomond (defaults)
//   node scripts/fetch-gsi-basemap.mjs lomond-country-club              # Lomond, explicit
//   node scripts/fetch-gsi-basemap.mjs taiheiyo-club-gotenba 35.2844768 138.8761690
//
// If lat/lon are omitted for a non-Lomond course, the script tries to read
// "center" from Tools/UHoleGeo/config/<courseId>.json. Pass --force to ignore
// the on-disk tile cache and re-download everything.
// ---------------------------------------------------------------------------

const positional = process.argv.slice(2).filter((a) => !a.startsWith("--"));
const forceRefetch = process.argv.includes("--force");

const courseId = positional[0] || "lomond-country-club";
const courseRoot = path.join(root, "output", courseId);
const basemapRoot = path.join(courseRoot, "basemap");
const photoRoot = path.join(basemapRoot, "gsi-photo-z17");
const demRoot = path.join(basemapRoot, "gsi-dem-z14");
const dem5aRoot = path.join(basemapRoot, "gsi-dem5a-z15");
const manifestPath = path.join(basemapRoot, "manifest.json");
const provenancePath = path.join(courseRoot, "provenance.json");

// Lomond's original hardcoded center, kept as the default for backward compat.
const LOMOND_CENTER = {
  lat: 34.9115,
  lon: 136.4370,
  source: "manual_verification_google_maps",
  confidence: "high",
  note: "Clubhouse verified at 34.9132, 136.4416 via Google Maps. Course center estimated from aerial imagery to be slightly west-northwest of clubhouse."
};

// Resolve the course center. Priority: CLI lat/lon > UHoleGeo config > Lomond default.
async function resolveCenter() {
  const cliLat = positional[1] !== undefined ? Number(positional[1]) : null;
  const cliLon = positional[2] !== undefined ? Number(positional[2]) : null;
  if (cliLat !== null && cliLon !== null && !Number.isNaN(cliLat) && !Number.isNaN(cliLon)) {
    return { lat: cliLat, lon: cliLon, source: "cli_argument", confidence: "high",
      note: "Center supplied on the command line." };
  }

  if (courseId === "lomond-country-club") return LOMOND_CENTER;

  // Try to read center from the UHoleGeo config for this course.
  const cfgPath = path.join(root, "..", "UHoleGeo", "config", `${courseId}.json`);
  try {
    const cfg = JSON.parse(await readFile(cfgPath, "utf8"));
    if (cfg.center && typeof cfg.center.lat === "number" && typeof cfg.center.lon === "number") {
      return { lat: cfg.center.lat, lon: cfg.center.lon, source: "uholegeo_config",
        confidence: "high", note: `Center read from ${path.relative(root, cfgPath)}.` };
    }
  } catch { /* no config — fall through to error */ }

  throw new Error(
    `No center for course "${courseId}". Pass lat/lon on the CLI ` +
    `(node scripts/fetch-gsi-basemap.mjs ${courseId} <lat> <lon>) ` +
    `or add a "center" block to Tools/UHoleGeo/config/${courseId}.json.`
  );
}

async function main() {
  const inferredCourseCenter = await resolveCenter();
  console.log(`Course: ${courseId}`);
  console.log(`Center: ${inferredCourseCenter.lat}, ${inferredCourseCenter.lon} (${inferredCourseCenter.source})`);

  await mkdir(photoRoot, { recursive: true });
  await mkdir(demRoot, { recursive: true });
  await mkdir(dem5aRoot, { recursive: true });

  const photoZoom = 17;
  const demZoom = 14;
  const dem5aZoom = 15;
  const coverageMeters = {
    west: 2800,
    east: 2000,
    north: 2000,
    south: 2000
  };

  const photoRange = tileRangeForCoverage(inferredCourseCenter.lat, inferredCourseCenter.lon, photoZoom, coverageMeters);
  const demRange = tileRangeForCoverage(inferredCourseCenter.lat, inferredCourseCenter.lon, demZoom, coverageMeters);
  const dem5aRange = tileRangeForCoverage(inferredCourseCenter.lat, inferredCourseCenter.lon, dem5aZoom, coverageMeters);
  const photoTiles = enumerateTiles(photoRange, photoZoom);
  const demTiles = enumerateTiles(demRange, demZoom);
  const dem5aTiles = enumerateTiles(dem5aRange, dem5aZoom);

  console.log(`Downloading ${photoTiles.length} GSI photo tiles...`);
  const savedPhotoTiles = await downloadTiles({
    tiles: photoTiles,
    outputDir: photoRoot,
    extension: ".jpg",
    makeUrl: ({ z, x, y }) => `https://cyberjapandata.gsi.go.jp/xyz/seamlessphoto/${z}/${x}/${y}.jpg`
  });

  console.log(`Downloading ${demTiles.length} GSI DEM tiles...`);
  const savedDemTiles = await downloadTiles({
    tiles: demTiles,
    outputDir: demRoot,
    extension: ".txt",
    makeUrl: ({ z, x, y }) => `https://cyberjapandata.gsi.go.jp/xyz/dem/${z}/${x}/${y}.txt`
  });

  console.log(`Downloading ${dem5aTiles.length} GSI DEM5A (5m lidar) tiles...`);
  const { saved: savedDem5aTiles, missing: missingDem5a } = await downloadTilesWithMissing({
    tiles: dem5aTiles,
    outputDir: dem5aRoot,
    extension: ".png",
    makeUrl: ({ z, x, y }) => `https://cyberjapandata.gsi.go.jp/xyz/dem5a_png/${z}/${x}/${y}.png`
  });
  console.log(`  DEM5A: ${savedDem5aTiles.length} available, ${missingDem5a} missing (no lidar coverage)`);

  const manifest = {
    schema_version: "1.0.0",
    course_id: courseId,
    provider: "gsi",
    center: inferredCourseCenter,
    fetched_at: new Date().toISOString(),
    force_refetch: forceRefetch,
    attribution: {
      short: "GSI / GSI Tiles",
      detail: "Base map tiles fetched from GSI. Attribution should cite GSI or GSI Tiles and link to the GSI tile list page."
    },
    datasets: [
      {
        id: "gsi-photo-z17",
        label: "GSI seamless photo",
        type: "imagery",
        zoom: photoZoom,
        tile_span_m: Number(tileSpanMeters(inferredCourseCenter.lat, photoZoom).toFixed(2)),
        local_root: relativeFromCourse(photoRoot),
        tile_count: savedPhotoTiles.length,
        tiles: savedPhotoTiles
      },
      {
        id: "gsi-dem-z14",
        label: "GSI DEM",
        type: "elevation",
        zoom: demZoom,
        tile_span_m: Number(tileSpanMeters(inferredCourseCenter.lat, demZoom).toFixed(2)),
        local_root: relativeFromCourse(demRoot),
        tile_count: savedDemTiles.length,
        tiles: savedDemTiles
      },
      {
        id: "gsi-dem5a-z15",
        label: "GSI DEM5A (5m lidar)",
        type: "elevation_hires",
        zoom: dem5aZoom,
        tile_span_m: Number(tileSpanMeters(inferredCourseCenter.lat, dem5aZoom).toFixed(2)),
        local_root: relativeFromCourse(dem5aRoot),
        tile_count: savedDem5aTiles.length,
        tiles_available: savedDem5aTiles.length,
        tiles_missing: missingDem5a,
        tiles: savedDem5aTiles
      }
    ]
  };

  await writeFile(manifestPath, JSON.stringify(manifest, null, 2) + "\n", "utf8");
  await updateProvenance(manifest, inferredCourseCenter);
  await updateAlignmentTargets();

  console.log(`Saved base-map manifest to ${manifestPath}`);
}

async function downloadTiles({ tiles, outputDir, extension, makeUrl }) {
  const saved = [];
  for (const tile of tiles) {
    const url = makeUrl(tile);
    const filename = `${tile.z}-${tile.x}-${tile.y}${extension}`;
    const localPath = path.join(outputDir, filename);
    let usedCache = false;

    if (!forceRefetch) {
      try {
        await stat(localPath);
        usedCache = true;
      } catch {
        usedCache = false;
      }
    }

    try {
      if (!usedCache) {
        const response = await fetch(url, {
          headers: {
            "User-Agent": "CourseIntakeBot/0.1 (+https://local.course-intake)"
          }
        });
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`);
        }
        const bytes = new Uint8Array(await response.arrayBuffer());
        await writeFile(localPath, bytes);
      }
      saved.push({
        z: tile.z,
        x: tile.x,
        y: tile.y,
        bounds: tile.bounds,
        local_path: relativeFromCourse(localPath),
        source_url: url,
        cached: usedCache
      });
    } catch (error) {
      console.warn(`Failed ${url}: ${error.message}`);
    }
  }
  return saved;
}

async function downloadTilesWithMissing({ tiles, outputDir, extension, makeUrl }) {
  const saved = [];
  let missing = 0;
  for (const tile of tiles) {
    const url = makeUrl(tile);
    const filename = `${tile.z}-${tile.x}-${tile.y}${extension}`;
    const localPath = path.join(outputDir, filename);
    let usedCache = false;

    if (!forceRefetch) {
      try {
        await stat(localPath);
        usedCache = true;
      } catch {
        usedCache = false;
      }
    }

    try {
      if (!usedCache) {
        const response = await fetch(url, {
          headers: {
            "User-Agent": "CourseIntakeBot/0.1 (+https://local.course-intake)"
          }
        });
        if (response.status === 404) {
          missing++;
          continue;
        }
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`);
        }
        const bytes = new Uint8Array(await response.arrayBuffer());
        await writeFile(localPath, bytes);
      }
      saved.push({
        z: tile.z,
        x: tile.x,
        y: tile.y,
        bounds: tile.bounds,
        local_path: relativeFromCourse(localPath),
        source_url: url,
        cached: usedCache
      });
    } catch (error) {
      console.warn(`  Failed ${url}: ${error.message}`);
      missing++;
    }
  }
  return { saved, missing };
}

async function updateProvenance(manifest, inferredCourseCenter) {
  let provenance = { sources: [] };
  try {
    provenance = JSON.parse(await readFile(provenancePath, "utf8"));
  } catch {
    provenance = { course_id: courseId, sources: [] };
  }

  const filtered = provenance.sources.filter((source) => !source.source_id.startsWith("gsi-"));
  filtered.push(
    {
      source_id: "gsi-seamlessphoto",
      category: "public_authoritative",
      url: "https://maps.gsi.go.jp/development/ichiran.html",
      license_status: "attribution_required",
      trust_level: "public_authoritative",
      use_mode: "alignment_base_map_and_reference_imagery",
      notes: [
        "GSI tile list indicates real-time app use is permitted with attribution.",
        "Course center is currently an inferred approximate centroid and should be validated."
      ]
    },
    {
      source_id: "gsi-dem",
      category: "public_authoritative",
      url: "https://maps.gsi.go.jp/development/demtile.html",
      license_status: "attribution_required",
      trust_level: "public_authoritative",
      use_mode: "elevation_reference"
    }
  );

  provenance.sources = filtered;
  provenance.base_map = {
    provider: manifest.provider,
    manifest_path: relativeFromCourse(manifestPath),
    inferred_center: inferredCourseCenter
  };

  await writeFile(provenancePath, JSON.stringify(provenance, null, 2) + "\n", "utf8");
}

async function updateAlignmentTargets() {
  for (let holeNumber = 1; holeNumber <= 18; holeNumber += 1) {
    const padded = String(holeNumber).padStart(2, "0");
    const alignmentPath = path.join(courseRoot, "holes", padded, "alignment.json");

    try {
      const alignment = JSON.parse(await readFile(alignmentPath, "utf8"));
      alignment.status = alignment.status === "needs_base_map" ? "ready_for_control_points" : alignment.status;
      alignment.target_base_map = {
        provider: "gsi",
        path: "basemap/manifest.json",
        license_checked: true
      };
      alignment.notes = [
        ...new Set([
          ...(alignment.notes || []),
          "Use the GSI photo dataset as the operator-visible base map during initial control point placement."
        ])
      ];
      await writeFile(alignmentPath, JSON.stringify(alignment, null, 2) + "\n", "utf8");
    } catch {
      continue;
    }
  }
}

function relativeFromCourse(targetPath) {
  return path.relative(courseRoot, targetPath).replaceAll("\\", "/");
}

function tileRangeForCoverage(lat, lon, zoom, coverageMeters) {
  const westOffset = degreeOffsetsFromMeters(lat, coverageMeters.west, 0);
  const eastOffset = degreeOffsetsFromMeters(lat, coverageMeters.east, 0);
  const northOffset = degreeOffsetsFromMeters(lat, 0, coverageMeters.north);
  const southOffset = degreeOffsetsFromMeters(lat, 0, coverageMeters.south);

  const north = lat + northOffset.dLat;
  const south = lat - southOffset.dLat;
  const east = lon + eastOffset.dLon;
  const west = lon - westOffset.dLon;

  return {
    minX: lonToTileX(west, zoom),
    maxX: lonToTileX(east, zoom),
    minY: latToTileY(north, zoom),
    maxY: latToTileY(south, zoom)
  };
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
