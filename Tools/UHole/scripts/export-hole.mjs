/**
 * export-hole.mjs
 *
 * Exports terrain heightmap, aerial tile manifest, anchors, and hole manifest
 * for a single hole, ready for Unity import.
 *
 * Usage:
 *   node scripts/export-hole.mjs 1
 */

import { readFile, writeFile, mkdir } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  tileBounds,
  lonToTileX,
  latToTileY,
  tileXToLon,
  tileYToLat,
  degreeOffsetsFromMeters
} from "./lib/tiles.mjs";
import { readDem5aTile, sampleDem5a } from "./lib/dem5a.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const courseRoot = path.join(root, "output", "lomond-country-club");

const HEIGHTMAP_RES = 129; // power-of-2-plus-1 for Unity terrain
const PADDING_M = 80;      // meters padding around hole bounds
const DEM_TILE_SIZE = 256;  // pixels per DEM tile
const DEM_ZOOM = 14;
const PHOTO_ZOOM = 17;

// --- Affine transform helpers ---

function pixelToWorld(px, py, coefficients) {
  const { a, b, tx, c, d, ty } = coefficients;
  return {
    lon: a * px + b * py + tx,
    lat: c * px + d * py + ty
  };
}

function latLonToMetersOffset(lat, lon, originLat, originLon) {
  const dLatM = (lat - originLat) * 111320;
  const dLonM = (lon - originLon) * 111320 * Math.cos(originLat * Math.PI / 180);
  return { x: dLonM, z: -dLatM }; // Unity: x = east, z = south (negative north)
}

// --- DEM parsing ---

async function parseDEMTile(filePath) {
  const text = await readFile(filePath, "utf8");
  const rows = text.trim().split("\n");
  const grid = [];
  for (const row of rows) {
    const cells = row.split(",").map((val) => {
      const trimmed = val.trim();
      if (trimmed === "e" || trimmed === "") return null;
      return parseFloat(trimmed);
    });
    grid.push(cells);
  }
  return grid;
}

function sampleDEM(demTiles, lat, lon) {
  for (const tile of demTiles) {
    const { bounds, grid } = tile;
    if (lon < bounds.west || lon > bounds.east || lat > bounds.north || lat < bounds.south) {
      continue;
    }
    // Fractional position within tile
    const fx = (lon - bounds.west) / (bounds.east - bounds.west);
    const fy = (bounds.north - lat) / (bounds.north - bounds.south);
    const col = Math.min(Math.floor(fx * DEM_TILE_SIZE), DEM_TILE_SIZE - 1);
    const row = Math.min(Math.floor(fy * DEM_TILE_SIZE), DEM_TILE_SIZE - 1);
    const val = grid[row]?.[col];
    if (val !== null && val !== undefined && !isNaN(val)) {
      return val;
    }
  }
  return null;
}

// --- Step 1: Compute hole world-space bounds ---

function computeHoleBounds(alignment, paddingM) {
  // Use ANCHORS for bounds when available (they define the actual hole extent:
  // tees, green, pin). Only fall back to control points if no anchors exist.
  // Control points are alignment references that may be on neighboring features.
  const anchors = alignment.anchors || [];
  const hasAnchors = anchors.some((a) => a.world);

  const points = hasAnchors
    ? anchors.filter((a) => a.world).map((a) => a.world)
    : (alignment.control_points || []).map((cp) => ({ lat: cp.basemap.lat, lon: cp.basemap.lon }));

  if (points.length === 0) {
    throw new Error("No anchors or control points to compute bounds from.");
  }

  let north = -Infinity, south = Infinity, east = -Infinity, west = Infinity;
  for (const { lat, lon } of points) {
    if (lat > north) north = lat;
    if (lat < south) south = lat;
    if (lon > east) east = lon;
    if (lon < west) west = lon;
  }

  // Add padding in meters
  const centerLat = (north + south) / 2;
  const { dLat, dLon } = degreeOffsetsFromMeters(centerLat, paddingM, paddingM);
  north += dLat;
  south -= dLat;
  east += dLon;
  west -= dLon;

  return { north, south, east, west };
}

// --- Step 2: Extract heightmap ---

async function extractHeightmap(bounds, manifestPath) {
  const manifest = JSON.parse(await readFile(manifestPath, "utf8"));

  // Look for DEM5A (hi-res) and z14 (fallback) datasets
  const dem5aDataset = manifest.datasets.find((ds) => ds.type === "elevation_hires");
  const demDataset = manifest.datasets.find((ds) => ds.type === "elevation");
  if (!demDataset && !dem5aDataset) {
    throw new Error("No elevation dataset in manifest.");
  }

  // Load DEM5A tiles (hi-res, preferred)
  const dem5aLoaded = [];
  if (dem5aDataset) {
    const overlapping5a = dem5aDataset.tiles.filter((tile) => {
      return tile.bounds.east > bounds.west &&
             tile.bounds.west < bounds.east &&
             tile.bounds.north > bounds.south &&
             tile.bounds.south < bounds.north;
    });
    for (const tile of overlapping5a) {
      try {
        const filePath = path.join(courseRoot, tile.local_path);
        const grid = await readDem5aTile(filePath);
        dem5aLoaded.push({ bounds: tile.bounds, grid });
      } catch (err) {
        console.warn(`  Skipping DEM5A tile ${tile.local_path}: ${err.message}`);
      }
    }
    if (dem5aLoaded.length > 0) {
      console.log(`  Using ${dem5aLoaded.length} DEM5A tiles (5m lidar) as primary elevation source`);
    }
  }

  // Load z14 DEM tiles (fallback)
  const demTiles = [];
  if (demDataset) {
    const overlapping = demDataset.tiles.filter((tile) => {
      return tile.bounds.east > bounds.west &&
             tile.bounds.west < bounds.east &&
             tile.bounds.north > bounds.south &&
             tile.bounds.south < bounds.north;
    });
    for (const tile of overlapping) {
      const filePath = path.join(courseRoot, tile.local_path);
      const grid = await parseDEMTile(filePath);
      demTiles.push({ bounds: tile.bounds, grid });
    }
  }

  if (dem5aLoaded.length === 0 && demTiles.length === 0) {
    throw new Error("No DEM tiles overlap the hole bounds.");
  }

  // Sample the heightmap grid — prefer DEM5A, fall back to z14
  const heightmap = new Float64Array(HEIGHTMAP_RES * HEIGHTMAP_RES);
  let minElev = Infinity;
  let maxElev = -Infinity;
  let noDataCount = 0;
  let dem5aHits = 0;
  let z14Hits = 0;

  for (let row = 0; row < HEIGHTMAP_RES; row++) {
    for (let col = 0; col < HEIGHTMAP_RES; col++) {
      const lat = bounds.north - (row / (HEIGHTMAP_RES - 1)) * (bounds.north - bounds.south);
      const lon = bounds.west + (col / (HEIGHTMAP_RES - 1)) * (bounds.east - bounds.west);

      // Try DEM5A first, then z14
      let elev = dem5aLoaded.length > 0 ? sampleDem5a(dem5aLoaded, lat, lon) : null;
      if (elev !== null) {
        dem5aHits++;
      } else {
        elev = sampleDEM(demTiles, lat, lon);
        if (elev !== null) z14Hits++;
      }

      if (elev !== null) {
        heightmap[row * HEIGHTMAP_RES + col] = elev;
        if (elev < minElev) minElev = elev;
        if (elev > maxElev) maxElev = elev;
      } else {
        heightmap[row * HEIGHTMAP_RES + col] = NaN;
        noDataCount++;
      }
    }
  }

  console.log(`  Elevation sources: ${dem5aHits} from DEM5A, ${z14Hits} from z14, ${noDataCount} no-data`);

  // Fill NaN values with average
  const avgElev = (minElev + maxElev) / 2;
  for (let i = 0; i < heightmap.length; i++) {
    if (isNaN(heightmap[i])) {
      heightmap[i] = avgElev;
    }
  }

  // Normalize to 16-bit and write raw file
  const elevRange = maxElev - minElev;
  const rawBuffer = Buffer.alloc(HEIGHTMAP_RES * HEIGHTMAP_RES * 2);
  for (let i = 0; i < heightmap.length; i++) {
    const normalized = elevRange > 0
      ? (heightmap[i] - minElev) / elevRange
      : 0;
    const uint16 = Math.round(normalized * 65535);
    rawBuffer.writeUInt16BE(uint16, i * 2);
  }

  const centerLat = (bounds.north + bounds.south) / 2;
  const widthM = (bounds.east - bounds.west) * 111320 * Math.cos(centerLat * Math.PI / 180);
  const lengthM = (bounds.north - bounds.south) * 111320;

  return {
    rawBuffer,
    minElevation: Math.round(minElev * 100) / 100,
    maxElevation: Math.round(maxElev * 100) / 100,
    widthM: Math.round(widthM * 10) / 10,
    lengthM: Math.round(lengthM * 10) / 10,
    noDataCount,
    dem5aHits,
    z14Hits
  };
}

// --- Step 3: Aerial tile manifest ---

function computeAerialTiles(bounds, manifest) {
  const photoDataset = manifest.datasets.find((ds) => ds.type === "imagery");
  if (!photoDataset) return { tiles: [] };

  const overlapping = photoDataset.tiles.filter((tile) => {
    return tile.bounds.east > bounds.west &&
           tile.bounds.west < bounds.east &&
           tile.bounds.north > bounds.south &&
           tile.bounds.south < bounds.north;
  });

  return {
    hole_bounds: bounds,
    tiles: overlapping.map((tile) => ({
      path: `../../${tile.local_path}`,
      z: tile.z,
      x: tile.x,
      y: tile.y,
      bounds: tile.bounds
    }))
  };
}

// --- Step 4: Anchors ---

function exportAnchors(alignment, bounds) {
  const anchors = alignment.anchors || [];
  const coefficients = alignment.transform?.coefficients;
  const originLat = (bounds.north + bounds.south) / 2;
  const originLon = (bounds.east + bounds.west) / 2;

  return anchors.map((anchor) => {
    let world = anchor.world;
    // If no world coords but we have pixel coords and a transform, compute them
    if (!world && anchor.official_px && coefficients) {
      world = pixelToWorld(anchor.official_px.x, anchor.official_px.y, coefficients);
    }

    const local = world
      ? latLonToMetersOffset(world.lat, world.lon, originLat, originLon)
      : null;

    return {
      type: anchor.type,
      label: anchor.label,
      official_px: anchor.official_px,
      world: world ? { lat: world.lat, lon: world.lon } : null,
      local: local ? { x: Math.round(local.x * 100) / 100, z: Math.round(local.z * 100) / 100 } : null
    };
  });
}

// --- Step 5: Generate hole-manifest.json ---

function buildManifest(holeData, alignment, bounds, terrain, aerialTiles, anchors) {
  const originLat = (bounds.north + bounds.south) / 2;
  const originLon = (bounds.east + bounds.west) / 2;
  const backTee = holeData.tee_yardages?.entries?.find((e) => e.tee_name === "Back");

  return {
    schema_version: "1.0.0",
    course_id: "lomond-country-club",
    hole_number: holeData.hole_number,
    par: holeData.par,
    stroke_index: holeData.stroke_index,
    championship_yards: backTee?.yards || null,
    bounds: {
      north: Math.round(bounds.north * 1e7) / 1e7,
      south: Math.round(bounds.south * 1e7) / 1e7,
      east: Math.round(bounds.east * 1e7) / 1e7,
      west: Math.round(bounds.west * 1e7) / 1e7
    },
    origin: {
      lat: Math.round(originLat * 1e7) / 1e7,
      lon: Math.round(originLon * 1e7) / 1e7,
      note: "Center of hole bounds, used as Unity scene origin"
    },
    terrain: {
      heightmap_file: "heightmap.raw",
      format: "uint16_bigendian",
      resolution: HEIGHTMAP_RES,
      min_elevation_m: terrain.minElevation,
      max_elevation_m: terrain.maxElevation,
      terrain_width_m: terrain.widthM,
      terrain_length_m: terrain.lengthM
    },
    aerial: {
      tiles_file: "aerial-tiles.json",
      tile_count: aerialTiles.tiles.length
    },
    anchors_file: "anchors.json",
    anchor_count: anchors.length,
    transform: {
      mean_residual_m: alignment.transform.mean_residual_m,
      max_residual_m: alignment.transform.max_residual_m
    },
    review_status: "prototype",
    exported_at: new Date().toISOString()
  };
}

// --- Main ---

async function exportHole(holeNumber) {
  const padded = String(holeNumber).padStart(2, "0");
  const holeDir = path.join(courseRoot, "holes", padded);
  const exportDir = path.join(courseRoot, "export", `hole-${padded}`);

  // Read hole data and alignment
  const holeData = JSON.parse(await readFile(path.join(holeDir, "hole.json"), "utf8"));
  const alignment = JSON.parse(await readFile(path.join(holeDir, "alignment.json"), "utf8"));

  if (alignment.status !== "transform_computed") {
    return { ok: false, message: `Hole ${holeNumber}: status is "${alignment.status}", need "transform_computed".` };
  }

  if (!alignment.transform?.pixel_to_world_ready || !alignment.transform?.coefficients) {
    return { ok: false, message: `Hole ${holeNumber}: no valid transform found.` };
  }

  const coefficients = alignment.transform.coefficients;

  // Step 1: Compute bounds from control points + anchors
  const bounds = computeHoleBounds(alignment, PADDING_M);
  console.log(`Bounds: N=${bounds.north.toFixed(5)}, S=${bounds.south.toFixed(5)}, E=${bounds.east.toFixed(5)}, W=${bounds.west.toFixed(5)}`);

  // Step 2: Extract heightmap
  const manifestPath = path.join(courseRoot, "basemap", "manifest.json");
  const terrain = await extractHeightmap(bounds, manifestPath);
  console.log(`Heightmap: ${terrain.minElevation}m – ${terrain.maxElevation}m, ${terrain.widthM}m × ${terrain.lengthM}m, ${terrain.noDataCount} no-data cells`);

  // Step 3: Aerial tile manifest
  const manifest = JSON.parse(await readFile(manifestPath, "utf8"));
  const aerialTiles = computeAerialTiles(bounds, manifest);
  console.log(`Aerial tiles: ${aerialTiles.tiles.length} photo tiles covering the hole`);

  // Step 4: Anchors
  const anchors = exportAnchors(alignment, bounds);
  console.log(`Anchors: ${anchors.length} exported`);

  // Step 5: Generate manifest
  const holeManifest = buildManifest(holeData, alignment, bounds, terrain, aerialTiles, anchors);

  // Write everything
  await mkdir(exportDir, { recursive: true });
  await writeFile(path.join(exportDir, "heightmap.raw"), terrain.rawBuffer);
  await writeFile(path.join(exportDir, "aerial-tiles.json"), JSON.stringify(aerialTiles, null, 2) + "\n", "utf8");
  await writeFile(path.join(exportDir, "anchors.json"), JSON.stringify(anchors, null, 2) + "\n", "utf8");
  await writeFile(path.join(exportDir, "hole-manifest.json"), JSON.stringify(holeManifest, null, 2) + "\n", "utf8");

  const summary = `Hole ${holeNumber}: exported to export/hole-${padded}/\n  Terrain: ${terrain.widthM}m × ${terrain.lengthM}m (${terrain.minElevation}–${terrain.maxElevation}m)\n  Aerial: ${aerialTiles.tiles.length} tiles\n  Anchors: ${anchors.length}`;
  console.log(`\n${summary}`);

  return {
    ok: true,
    message: summary,
    exportDir: `export/hole-${padded}`
  };
}

// Export for dev-server inline use
export { exportHole };

// CLI entry point
const isMain = process.argv[1] && path.resolve(process.argv[1]) === path.resolve(fileURLToPath(import.meta.url));
if (isMain) {
  const holeNumber = Number(process.argv[2]);
  if (!Number.isInteger(holeNumber) || holeNumber < 1 || holeNumber > 18) {
    console.error("Usage: node scripts/export-hole.mjs <holeNumber>");
    process.exit(1);
  }
  const result = await exportHole(holeNumber);
  if (!result.ok) {
    console.error(result.message);
    process.exit(1);
  }
}
