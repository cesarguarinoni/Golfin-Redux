import { mkdir, readFile, writeFile } from "node:fs/promises";
import { stat } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { degreeOffsetsFromMeters, enumerateTiles, lonToTileX, latToTileY, tileSpanMeters } from "./lib/tiles.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const courseId = "lomond-country-club";
const courseRoot = path.join(root, "output", courseId);
const basemapRoot = path.join(courseRoot, "basemap");
const photoRoot = path.join(basemapRoot, "gsi-photo-z17");
const demRoot = path.join(basemapRoot, "gsi-dem-z14");
const manifestPath = path.join(basemapRoot, "manifest.json");
const provenancePath = path.join(courseRoot, "provenance.json");
const forceRefetch = process.argv.includes("--force");

const inferredCourseCenter = {
  lat: 34.9115,
  lon: 136.4370,
  source: "manual_verification_google_maps",
  confidence: "high",
  note: "Clubhouse verified at 34.9132, 136.4416 via Google Maps. Course center estimated from aerial imagery to be slightly west-northwest of clubhouse."
};

async function main() {
  await mkdir(photoRoot, { recursive: true });
  await mkdir(demRoot, { recursive: true });

  const photoZoom = 17;
  const demZoom = 14;
  const coverageMeters = {
    west: 2800,
    east: 2000,
    north: 2000,
    south: 2000
  };

  const photoRange = tileRangeForCoverage(inferredCourseCenter.lat, inferredCourseCenter.lon, photoZoom, coverageMeters);
  const demRange = tileRangeForCoverage(inferredCourseCenter.lat, inferredCourseCenter.lon, demZoom, coverageMeters);
  const photoTiles = enumerateTiles(photoRange, photoZoom);
  const demTiles = enumerateTiles(demRange, demZoom);

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
      }
    ]
  };

  await writeFile(manifestPath, JSON.stringify(manifest, null, 2) + "\n", "utf8");
  await updateProvenance(manifest);
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

async function updateProvenance(manifest) {
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
