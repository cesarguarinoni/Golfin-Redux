import { copyFile, mkdir, readFile, stat, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { buildAlignmentRecord, buildHoleRecord, lomondHoles, lomondMetadata } from "./lib/lomond-data.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const outputRoot = path.join(root, "output", lomondMetadata.courseId);
const sourceRoot = path.join(outputRoot, "source");
const holesRoot = path.join(outputRoot, "holes");
const humanRoot = path.join(outputRoot, "human");
const cacheManifestPath = path.join(sourceRoot, "cache.json");
const forceRefetch = process.argv.includes("--force");

async function main() {
  console.log("Fetching official Lomond course page...");

  let html = "";
  await mkdir(sourceRoot, { recursive: true });
  await mkdir(holesRoot, { recursive: true });
  await mkdir(humanRoot, { recursive: true });

  const cache = {
    schema_version: "1.0.0",
    course_id: lomondMetadata.courseId,
    force_refetch: forceRefetch,
    last_run_at: new Date().toISOString(),
    course_page: {
      cached: false,
      path: "source/course-page.html"
    },
    holes: []
  };

  const coursePagePath = path.join(sourceRoot, "course-page.html");

  if (!forceRefetch) {
    try {
      html = await readFile(coursePagePath, "utf8");
      cache.course_page.cached = true;
      console.log("Using cached official Lomond course page.");
    } catch {
      html = "";
    }
  }

  if (!html) {
    try {
      const response = await fetch(lomondMetadata.website, {
        headers: {
          "User-Agent": "CourseIntakeBot/0.1 (+https://local.course-intake)"
        }
      });
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }
      html = await response.text();
    } catch (error) {
      console.warn(`Fetch failed: ${error.message}`);
      console.warn("Continuing with baked official metadata and empty raw source capture.");
    }
  }

  if (html && !cache.course_page.cached) {
    await writeFile(coursePagePath, html, "utf8");
    cache.course_page.cached = false;
  } else if (!html) {
    try {
      await stat(coursePagePath);
      cache.course_page.cached = true;
    } catch {
      cache.course_page.cached = false;
    }
  }

  const holeLinks = extractHoleLinks(html, lomondMetadata.website);
  const cachedManifest = await readCacheManifest();
  if (!holeLinks.size && cachedManifest?.holes?.length) {
    for (const entry of cachedManifest.holes) {
      if (entry.hole_number && entry.source_url) {
        holeLinks.set(entry.hole_number, entry.source_url);
      }
    }
  }

  for (const [holeNumber, holeLink] of holeLinks) {
    const padded = String(holeNumber).padStart(2, "0");
    const holeDir = path.join(holesRoot, padded);
    const holeSourceDir = path.join(holeDir, "source");
    await mkdir(holeSourceDir, { recursive: true });
    const filename = `official-map${extensionFromUrl(holeLink)}`;
    const localPath = path.join(holeSourceDir, filename);
    let usedCache = false;

    if (!forceRefetch) {
      try {
        await stat(localPath);
        usedCache = true;
        console.log(`Using cached official reference for hole ${holeNumber}: ${filename}`);
      } catch {
        usedCache = false;
      }
    }

    try {
      if (!usedCache) {
        const response = await fetch(holeLink, {
          headers: {
            "User-Agent": "CourseIntakeBot/0.1 (+https://local.course-intake)"
          }
        });
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`);
        }
        const bytes = new Uint8Array(await response.arrayBuffer());
        await writeFile(localPath, bytes);
        console.log(`Saved official reference for hole ${holeNumber}: ${filename}`);
      }
      cache.holes.push({
        hole_number: holeNumber,
        cached: usedCache,
        path: `holes/${padded}/source/${filename}`,
        source_url: holeLink
      });
    } catch (error) {
      console.warn(`Could not download hole ${holeNumber} reference: ${error.message}`);
    }
  }

  const course = {
    schema_version: "1.0.0",
    course_id: lomondMetadata.courseId,
    display_name: lomondMetadata.displayName,
    native_name: lomondMetadata.nativeName,
    country_code: lomondMetadata.countryCode,
    address: lomondMetadata.address,
    website: lomondMetadata.website,
    hole_count: 18,
    par_total: lomondMetadata.parTotal,
    yardage_claims: [
      {
        value: lomondMetadata.overviewYardage,
        unit: "yards",
        source_id: "official-course-page-overview",
        status: "conflict",
        note: "Overview section on the official course page reports 7,028 yards."
      },
      {
        value: lomondMetadata.backTeeTotal,
        unit: "yards",
        source_id: "official-course-page-hole-table",
        status: "derived",
        note: "Back-tee total from the official hole-by-hole table sums to 7,024 yards."
      }
    ],
    review_status: "ingested",
    source_summary: {
      official_sources: html ? 2 : 1,
      authoritative_public_sources: 0,
      reference_only_sources: 0
    },
    holes: lomondHoles.map((hole) => ({
      hole_number: hole.holeNumber,
      path: `holes/${String(hole.holeNumber).padStart(2, "0")}/hole.json`
    })),
    attribution: [
      "Course metadata and yardages derived from the official Lomond Country Club course page.",
      "Additional aerial, DEM, and reference-only sources must be added before production use."
    ]
  };

  const provenance = {
    course_id: lomondMetadata.courseId,
    captured_at: new Date().toISOString(),
    sources: [
      {
        source_id: "official-course-page-overview",
        category: "official",
        url: lomondMetadata.website,
        license_status: "unknown_site_content_review_required",
        trust_level: "official",
        use_mode: "metadata_and_reference_images"
      },
      {
        source_id: "official-course-page-hole-table",
        category: "official",
        url: lomondMetadata.website,
        license_status: "unknown_site_content_review_required",
        trust_level: "official",
        use_mode: "metadata"
      }
    ],
    extraction: {
      hole_detail_links_found: Array.from(holeLinks.entries()).map(([holeNumber, url]) => ({
        hole_number: holeNumber,
        url
      }))
    },
    required_before_production: [
      "Add licensable aerial imagery source and attribution.",
      "Add DEM source and attribution.",
      "Georeference official hole detail images against a licensable base map.",
      "Populate reviewed geometry artifacts for every hole."
    ]
  };

  await writeJson(path.join(outputRoot, "course.json"), course);
  await writeJson(path.join(outputRoot, "provenance.json"), provenance);
  await writeJson(cacheManifestPath, cache);

  const humanHoleSummaries = [];

  for (const hole of lomondHoles) {
    const padded = String(hole.holeNumber).padStart(2, "0");
    const holeDir = path.join(holesRoot, padded);
    await mkdir(holeDir, { recursive: true });
    const holeRecord = buildHoleRecord(hole.holeNumber, holeLinks.get(hole.holeNumber) || null);
    await writeJson(path.join(holeDir, "hole.json"), holeRecord);
    const nextAlignment = buildAlignmentRecord(hole.holeNumber, holeLinks.get(hole.holeNumber) || null);
    const alignmentPath = path.join(holeDir, "alignment.json");
    const existingAlignment = await readJsonIfExists(alignmentPath);
    const mergedAlignment = mergeAlignment(existingAlignment, nextAlignment);
    await writeJson(alignmentPath, mergedAlignment);
    const humanSummary = await writeHumanHoleFolder(holeRecord, mergedAlignment);
    humanHoleSummaries.push(humanSummary);
  }

  await writeHumanCourseIndex(course, humanHoleSummaries);

  console.log(`Wrote normalized package to ${outputRoot}`);
}

function extractHoleLinks(html, baseUrl) {
  const results = new Map();
  if (!html) {
    return results;
  }

  const anchorPattern = /<a\b[^>]*href="([^"]+)"[^>]*>([\s\S]*?)<\/a>/gi;
  let match;
  while ((match = anchorPattern.exec(html))) {
    const href = match[1];
    const innerText = stripTags(match[2]).trim();
    const holeNumber = Number(innerText);
    if (Number.isInteger(holeNumber) && holeNumber >= 1 && holeNumber <= 18 && !results.has(holeNumber)) {
      try {
        results.set(holeNumber, new URL(href, baseUrl).href);
      } catch {
        continue;
      }
    }
  }
  return results;
}

function stripTags(input) {
  return input.replace(/<[^>]+>/g, " ").replace(/\s+/g, " ");
}

function extensionFromUrl(url) {
  try {
    const pathname = new URL(url).pathname;
    const ext = path.extname(pathname);
    return ext && ext.length <= 5 ? ext : ".jpg";
  } catch {
    return ".jpg";
  }
}

async function writeJson(filePath, payload) {
  await writeFile(filePath, JSON.stringify(payload, null, 2) + "\n", "utf8");
}

async function readCacheManifest() {
  try {
    return JSON.parse(await readFile(cacheManifestPath, "utf8"));
  } catch {
    return null;
  }
}

async function readJsonIfExists(filePath) {
  try {
    return JSON.parse(await readFile(filePath, "utf8"));
  } catch {
    return null;
  }
}

function mergeAlignment(existingAlignment, nextAlignment) {
  if (!existingAlignment) {
    return nextAlignment;
  }

  return {
    ...nextAlignment,
    ...existingAlignment,
    official_map: {
      ...existingAlignment.official_map,
      ...nextAlignment.official_map
    },
    target_base_map: existingAlignment.target_base_map || nextAlignment.target_base_map,
    control_points: existingAlignment.control_points || nextAlignment.control_points,
    transform: existingAlignment.transform || nextAlignment.transform,
    notes: Array.from(new Set([...(existingAlignment.notes || []), ...(nextAlignment.notes || [])]))
  };
}

async function writeHumanHoleFolder(holeRecord, alignmentRecord) {
  const padded = String(holeRecord.hole_number).padStart(2, "0");
  const humanHoleDir = path.join(humanRoot, "holes", padded);
  await mkdir(humanHoleDir, { recursive: true });

  const officialReference = holeRecord.assets?.official_references?.[0] || null;
  let copiedOfficialMap = null;
  if (officialReference) {
    const sourcePath = path.join(outputRoot, officialReference);
    const targetName = path.basename(officialReference);
    const targetPath = path.join(humanHoleDir, targetName);
    try {
      await copyFile(sourcePath, targetPath);
      copiedOfficialMap = targetName;
    } catch {
      copiedOfficialMap = null;
    }
  }

  const teeRows = holeRecord.tee_yardages.entries
    .map((entry) => `| ${entry.tee_name} | ${entry.yards} | ${entry.meters} |`)
    .join("\n");
  const controlPointCount = alignmentRecord?.control_points?.length || 0;

  const markdown = `# Hole ${holeRecord.hole_number}

- Par: ${holeRecord.par}
- Stroke index: ${holeRecord.stroke_index}
- Review status: ${holeRecord.review_status}
- Geometry status: ${holeRecord.geometry_status}
- Alignment status: ${alignmentRecord?.status || "unknown"}
- Saved control points: ${controlPointCount}

## Tee Yardages

Source: \`${holeRecord.tee_yardages.source_id}\`

| Tee | Yards | Meters |
| --- | ---: | ---: |
${teeRows}

## Assets

- Official map: ${copiedOfficialMap ? `./${copiedOfficialMap}` : holeRecord.assets.official_references[0]}
- Hole data JSON: ../../holes/${padded}/hole.json
- Alignment JSON: ../../holes/${padded}/alignment.json

## Notes

${(holeRecord.notes || []).map((note) => `- ${note}`).join("\n") || "- None"}
`;

  await writeFile(path.join(humanHoleDir, "README.md"), markdown + "\n", "utf8");

  return {
    hole_number: holeRecord.hole_number,
    par: holeRecord.par,
    stroke_index: holeRecord.stroke_index,
    alignment_status: alignmentRecord?.status || "unknown",
    control_points: controlPointCount,
    relative_readme: `holes/${padded}/README.md`
  };
}

async function writeHumanCourseIndex(courseRecord, holeSummaries) {
  const rows = holeSummaries
    .sort((a, b) => a.hole_number - b.hole_number)
    .map((hole) => `| ${hole.hole_number} | ${hole.par} | ${hole.stroke_index} | ${hole.alignment_status} | ${hole.control_points} | [Open](./${hole.relative_readme}) |`)
    .join("\n");

  const markdown = `# ${courseRecord.display_name}

- Native name: ${courseRecord.native_name || "n/a"}
- Website: ${courseRecord.website}
- Address: ${courseRecord.address}
- Holes: ${courseRecord.hole_count}
- Total par: ${courseRecord.par_total}
- Review status: ${courseRecord.review_status}

## Hole Index

| Hole | Par | Stroke Index | Alignment | Control Points | Summary |
| ---: | ---: | ---: | --- | ---: | --- |
${rows}

## Course Notes

${(courseRecord.attribution || []).map((line) => `- ${line}`).join("\n")}
`;

  await writeFile(path.join(humanRoot, "README.md"), markdown + "\n", "utf8");
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
