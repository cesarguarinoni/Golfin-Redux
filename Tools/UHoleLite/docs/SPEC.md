# UHole Lite — Map-Based Hole Pipeline

> A simpler, faster alternative to UHole that uses official course hole diagrams
> as both the visual texture and the zone-classification source.

---

## Overview

UHole Lite takes the official hole illustration GIFs from a golf course website,
extracts the playable area, detects surface zones and tee markers via color
segmentation, generates procedural elevation, and exports a package that the
existing Unity `HoleImporter.cs` can consume.

**Key difference from UHole:** No satellite imagery, no DEM tiles, no alignment UI.
The official map IS the texture. Setup per hole ≈ 2 minutes (vs ~30 min for UHole).

---

## Data Sources

### 1. Official Lomond CC Hole Maps
- URL pattern: `https://www.lomond-cc.com/wp-content/themes/templateB/images/course_e{NN}.gif`
- NN = 01–18 (zero-padded)
- All images: 850×638 pixels, GIF format, 256-color palette
- Layout (consistent across all 18):
  - **Left panel** (~320px): black background with green zoom inset (top) + hole description (bottom)
  - **Right panel** (~530px): 3D-rendered hole illustration with tee markers, OB lines, bunkers, etc.

### 2. Official Lomond CC Course Page
- URL: `https://www.lomond-cc.com/course/`
- Data: yardage table (all 18 holes × 4 tee sets), par, HDCP, hole descriptions in Japanese
- Course map: `course_d01.gif` (full 18-hole overview)
- Green zoom insets show green dimensions in yards (width × depth)

### 3. Rakuten GORA Course Info
- URL: `https://booking.gora.golf.rakuten.co.jp/guide/course_info/disp/c_id/240078`
- Confirms all yardage/par/HDCP data
- Additional metadata:
  - Course type: 丘陵 (hilly)
  - Elevation profile: 適度なアップダウン (moderate ups and downs)
  - Total area: 1,370,000 m²
  - Designer: 川田太三 (Kawata Taizo)
  - Greens: Bent grass, single green per hole
  - Course rating: 75.3 (back tees), 72.3 (regular)

### 4. Hole Descriptions (from official site, Japanese)
These provide elevation hints for procedural terrain generation:
- Hole 1: "ティーショットは右側が広く、二段のフェアウェイセンターの傾斜部が狙い目" → two-tiered fairway with slope
- Hole 4: "グリーン右から傾斜の為、右サイドから攻めるのがベスト。グリーン左斜面下に落ちると苦戦" → green slopes right to left
- etc. (to be scraped per-hole)

---

## Image Layout Analysis

```
┌──────────────────────────────────────────────────┐
│                  850 × 638 px                     │
│ ┌──────────┐ ┌──────────────────────────────────┐ │
│ │  GREEN   │ │                                  │ │
│ │  ZOOM    │ │                                  │ │
│ │  INSET   │ │     HOLE ILLUSTRATION            │ │
│ │ (top-L)  │ │     (3D-rendered view)           │ │
│ │          │ │                                  │ │
│ ├──────────┤ │     • Tee dots (colored)         │ │
│ │ No.01    │ │     • Fairway (bright green)     │ │
│ │ par.5    │ │     • Trees (dark masses)        │ │
│ │ HDCP 9   │ │     • Bunkers (tan/pink)         │ │
│ │          │ │     • OB lines (white)           │ │
│ │ Japanese │ │     • Cart paths (gray)          │ │
│ │ hole     │ │     • Water (blue)               │ │
│ │ desc.    │ │     • Tee yardage legend (bot-R) │ │
│ └──────────┘ └──────────────────────────────────┘ │
└──────────────────────────────────────────────────┘
```

### Extraction Plan
1. Crop the right panel (illustration only), removing the black info panel
2. Remove the tee yardage legend box (bottom-right corner)
3. The remaining image = the hole texture
4. Upscale from ~530×600 to 1024×1024 using Sharp with Lanczos sharpening

### Green Zoom Inset
- Located in the top-left panel on black background
- Shows green shape with yardage dimensions (e.g., "30" depth × "31" width)
- **Extract these dimensions** for green metadata
- Do NOT include in the terrain texture (it's in the info panel that gets cropped)

---

## Pipeline Steps

### Step 1: Scrape & Catalog (`scripts/scrape-course.mjs`)

Downloads all 18 hole GIFs + the course overview map + scrapes yardage data.

**Input:** Course URL  
**Output:**
```
output/lomond-country-club/
  source/
    course_e01.gif ... course_e18.gif   (original hole maps)
    course_d01.gif                       (course overview map)
  course.json                            (yardage, par, hdcp, descriptions)
```

`course.json` schema:
```json
{
  "course_id": "lomond-country-club",
  "display_name": "Lomond Country Club",
  "native_name": "ローモンドカントリー倶楽部",
  "source_urls": {
    "official": "https://www.lomond-cc.com/course/",
    "gora": "https://booking.gora.golf.rakuten.co.jp/guide/course_info/disp/c_id/240078"
  },
  "course_type": "hilly",
  "elevation_profile": "moderate",
  "area_m2": 1370000,
  "greens": "bent",
  "holes": [
    {
      "number": 1,
      "par": 5,
      "hdcp": 9,
      "tees": {
        "back":    { "yards": 531, "color": "blue" },
        "regular": { "yards": 509, "color": "green" },
        "front":   { "yards": 488, "color": "white" },
        "ladies":  { "yards": 458, "color": "red" }
      },
      "description_jp": "ティーショットは右側が広く...",
      "green_dimensions": { "width_yd": 31, "depth_yd": 30 },
      "source_gif": "source/course_e01.gif"
    }
    // ... 18 holes
  ]
}
```

### Step 2: Extract & Upscale Illustration (`scripts/extract-hole.mjs`)

For each hole, crops the illustration area from the GIF and upscales it.

**Per-hole processing:**

1. **Crop the illustration** — Remove the left info panel (black background).
   Find the right edge of the black panel by scanning from left for the first
   column where non-black content begins. Approximate split: x ≈ 310-320.
   Use a smarter approach: find the widest continuous black column.

2. **Remove the yardage legend** — The bottom-right corner has a legend box
   showing colored dots + yardages (e.g., "● 458 ○ 488 ● 509 ● 531").
   Detect it by finding the cluster of tee-color dots in the bottom-right
   ~150×80px region. Fill with surrounding background color (grass/tree).

3. **Trim transparent/black edges** — Remove any remaining black borders
   from the crop.

4. **Upscale to 1024×1024** — Use Sharp with:
   - `resize(1024, 1024, { fit: 'contain', background: dark green })`
   - `sharpen()` for crisp edges
   - Output as PNG (better quality than GIF for textures)

**Output:**
```
output/lomond-country-club/
  holes/01/
    illustration.png       (1024×1024, cropped+upscaled hole image)
    illustration_raw.png   (original crop before upscale, for reference)
```

### Step 3: Color Zone Classification (`scripts/classify-zones.mjs`)

Analyzes the illustration to identify surface zones.

**Color Ranges** (calibrated from hole 1 analysis):

| Zone       | RGB Range (approximate)                          | Priority |
|------------|--------------------------------------------------|----------|
| Fairway    | H:80-150, S:40-100%, L:30-60% (bright green)    | 1        |
| Green      | H:80-150, brighter/lighter than fairway          | 2        |
| Semi-rough | H:80-150, darker than fairway, S:30-60%          | 3        |
| Rough      | H:60-160, dark, low saturation greens            | 4        |
| Trees      | Very dark, H:60-160, L<25%                       | 5        |
| Bunker     | H:20-50 (tan/beige/pink), S:20-60%, L:60-80%    | 6        |
| Water      | H:180-250 (blue), S>30%                          | 7        |
| Cart path  | Gray, low saturation, L:40-60%                   | 8        |
| OB         | Near-white with line detection                   | 9        |

**NOTE:** These ranges will need per-course tuning. The GIF palette is only 256
colors, so we can also work directly with the palette indices after identifying
which palette entries map to which zone.

**Approach:** Work in HSL color space for more intuitive thresholds. Use
connected-component analysis to group same-zone pixels into contiguous regions.
Small isolated regions get absorbed into their surrounding zone.

**Output:**
```
output/lomond-country-club/
  holes/01/
    zones.png              (indexed color image: each zone = distinct color)
    zones.json             (zone polygons or grid, for Unity consumption)
```

`zones.json` schema:
```json
{
  "resolution": 1024,
  "zones": {
    "fairway":   { "color": [0, 200, 0],   "pixel_count": 120000 },
    "green":     { "color": [0, 255, 100],  "pixel_count": 8000 },
    "semi_rough":{ "color": [80, 150, 40],  "pixel_count": 45000 },
    "rough":     { "color": [40, 80, 20],   "pixel_count": 95000 },
    "trees":     { "color": [10, 30, 5],    "pixel_count": 200000 },
    "bunker":    { "color": [220, 200, 150], "pixel_count": 3000 },
    "water":     { "color": [30, 80, 200],  "pixel_count": 5000 },
    "cart_path": { "color": [150, 150, 150], "pixel_count": 2000 },
    "ob":        { "color": [255, 0, 0],    "pixel_count": 1000 }
  },
  "grid": "base64-encoded uint8 grid where each byte = zone index"
}
```

### Step 4: Detect Tee Markers (`scripts/detect-tees.mjs`)

Finds the colored tee dots in the illustration.

**Tee marker colors** (from yardage legend):
- Blue dot = Back tee (531y for hole 1)
- Green dot = Regular tee (509y)
- White dot = Front tee (488y)
- Red dot = Ladies tee (458y)

**Detection approach:**
1. In the illustration image, scan for small circular clusters (radius ~4-8px)
   of the tee marker colors
2. Filter by size (must be dot-shaped, not large areas)
3. Filter by location (tees cluster near the bottom/tee-end of the hole)
4. Output normalized positions (0-1 range within the illustration)

**Validation:** The tee positions should be roughly collinear and ordered by
distance from the green (back furthest, ladies closest).

**Output:** Added to `hole-manifest.json` as `anchors`:
```json
{
  "anchors": [
    { "type": "tee_back",    "color": "blue",  "pos": { "x": 0.72, "y": 0.88 } },
    { "type": "tee_regular", "color": "green", "pos": { "x": 0.68, "y": 0.86 } },
    { "type": "tee_front",   "color": "white", "pos": { "x": 0.64, "y": 0.85 } },
    { "type": "tee_ladies",  "color": "red",   "pos": { "x": 0.60, "y": 0.84 } }
  ]
}
```

### Step 5: Generate Procedural Heightmap (`scripts/generate-terrain.mjs`)

Creates a plausible heightmap from the zone map + hole descriptions.

**Elevation Rules:**
- Water = lowest point (0m relative)
- Bunkers = slight depressions (-1 to -2m from surrounding terrain)
- Green = relatively flat plateau, slight slope for drainage (per description)
- Fairway = gentle slope from tee toward green (overall drop 5-15m typical for hilly course)
- Rough = follows fairway elevation but may have undulation
- Trees/OB = slight ridges at edges (+2-5m from fairway)

**Terrain Generation Algorithm:**
1. Start with a base slope from tee to green (back tee is usually higher)
2. Apply Perlin noise for natural undulation (amplitude 3-8m, frequency ~0.01)
3. Flatten areas classified as "green" (reduce noise amplitude to ±0.5m)
4. Add depressions at bunker locations
5. Set water zones to the minimum elevation
6. Add slight ridges along tree lines
7. Parse Japanese hole description for specific slope hints:
   - "二段のフェアウェイ" → two-level fairway with a step
   - "打ち上げ" → uphill hole
   - "打ち下ろし" → downhill hole
   - "傾斜" → slope present
   - "左ドッグレッグ" / "右ドッグレッグ" → dogleg

**Course-level parameters** (from GORA):
- Type: 丘陵 (hilly) → base undulation amplitude = moderate (5-10m)
- Profile: 適度なアップダウン → mix of uphill and downhill holes
- Total elevation range estimate: ~30-50m across the course

**Output:**
```
output/lomond-country-club/
  holes/01/
    heightmap.raw          (129×129 uint16 big-endian, same format as UHole)
```

**Heightmap parameters:** Stored in `hole-manifest.json`:
```json
{
  "terrain": {
    "heightmap_file": "heightmap.raw",
    "format": "uint16be",
    "resolution": 129,
    "min_elevation_m": 0,
    "max_elevation_m": 25,
    "terrain_width_m": 500,
    "terrain_length_m": 500
  }
}
```

**Terrain sizing:** Derived from the back tee yardage. For a 531yd par 5,
the tee-to-green distance is ~485m. The terrain needs to be larger than this
to include rough and OB areas. Rule of thumb: `terrain_size ≈ back_yards * 1.1`
in the long dimension, with the short dimension ~60-70% of that.

### Step 6: Export Hole Package (`scripts/export-hole.mjs`)

Assembles the final package in UHole-compatible format.

**Output:**
```
output/lomond-country-club/
  export/
    hole-01/
      hole-manifest.json     (metadata, terrain specs, anchors)
      heightmap.raw           (129×129 uint16be)
      texture.png             (1024×1024 upscaled illustration)
      zones.json              (zone classification data)
      anchors.json            (tee positions in local coords)
```

`hole-manifest.json` is compatible with the existing `HoleImporter.cs` format:
```json
{
  "schema_version": "1.0.0",
  "pipeline": "uhole-lite",
  "course_id": "lomond-country-club",
  "hole_number": 1,
  "par": 5,
  "stroke_index": 9,
  "championship_yards": 531,
  "bounds": null,
  "origin": null,
  "terrain": {
    "heightmap_file": "heightmap.raw",
    "format": "uint16be",
    "resolution": 129,
    "min_elevation_m": 0.0,
    "max_elevation_m": 25.0,
    "terrain_width_m": 530.0,
    "terrain_length_m": 580.0
  },
  "texture": {
    "file": "texture.png",
    "resolution": 1024
  },
  "zones_file": "zones.json",
  "anchors_file": "anchors.json",
  "green_dimensions": { "width_yd": 31, "depth_yd": 30 },
  "review_status": "auto-generated"
}
```

**Anchor positions** in `anchors.json` use local coordinates (meters from
terrain center), same as UHole format:
```json
[
  {
    "type": "tee_back",
    "label": "Back Tee (531y)",
    "local": { "x": 120.5, "z": -230.0 }
  }
]
```

Conversion from normalized image positions to local meters:
```
local.x = (pos.x - 0.5) * terrain_width_m
local.z = (pos.y - 0.5) * terrain_length_m
```

(Note: z axis may need flipping depending on image orientation vs Unity convention.
The terrain texture and heightmap must use the same orientation as the anchors.)

---

## Unity Integration

### Option A: Modify existing HoleImporter.cs
Add a code path that detects `"pipeline": "uhole-lite"` in the manifest and:
- Uses `texture.png` directly instead of stitching aerial tiles
- Skips the tile-bounds-based UV mapping (texture = terrain 1:1)
- Uses the same heightmap, anchor, and scene creation code

### Option B: Create UHoleLiteImporter.cs
A simplified importer that only handles the lite format. Less code, easier to
maintain, but duplicates some logic.

**Recommendation:** Option A. The HoleImporter already works; adding a lite
code path is ~30 lines of changes.

---

## File Structure

```
Tools/UHoleLite/
  package.json
  README.md
  .gitignore
  scripts/
    scrape-course.mjs        (Step 1: download GIFs + yardage data)
    extract-hole.mjs          (Step 2: crop, clean, upscale)
    classify-zones.mjs        (Step 3: color segmentation)
    detect-tees.mjs           (Step 4: find tee marker dots)
    generate-terrain.mjs      (Step 5: procedural heightmap)
    export-hole.mjs           (Step 6: assemble export package)
    run-all.mjs               (orchestrator: runs steps 1-6 for a hole or all 18)
    lib/
      colors.mjs              (HSL conversion, zone color ranges)
      terrain.mjs             (Perlin noise, slope generation)
      image-utils.mjs         (crop, upscale, pixel sampling)
  config/
    lomond-country-club.json  (per-course color calibration + elevation hints)
  output/
    lomond-country-club/
      source/                 (downloaded GIFs)
      holes/01..18/           (per-hole working data)
      export/hole-01..18/     (final packages for Unity)
      course.json             (master course data)
  docs/
    SPEC.md                   (this file)
    TASK.md                   (Claude Code handoff)
```

---

## Dependencies

```json
{
  "dependencies": {
    "sharp": "^0.33.0",
    "node-fetch": "^3.3.0"
  }
}
```

Sharp handles: GIF reading, cropping, resizing, sharpening, PNG output.
No other heavy dependencies. Perlin noise is implemented inline (simple 2D noise).

---

## CLI Usage

```bash
# Download all hole maps + course data
node scripts/scrape-course.mjs lomond-country-club

# Process a single hole (steps 2-6)
node scripts/run-all.mjs lomond-country-club 1

# Process all 18 holes
node scripts/run-all.mjs lomond-country-club --all

# Run individual steps (for debugging/tuning)
node scripts/extract-hole.mjs lomond-country-club 1
node scripts/classify-zones.mjs lomond-country-club 1
node scripts/detect-tees.mjs lomond-country-club 1
node scripts/generate-terrain.mjs lomond-country-club 1
node scripts/export-hole.mjs lomond-country-club 1
```

---

## Implementation Order

1. **Step 1 (Scrape)** — Straightforward HTTP + HTML parsing
2. **Step 2 (Extract)** — Image cropping with Sharp
3. **Step 4 (Tee Detection)** — Color blob detection (simpler than zone classification)
4. **Step 3 (Zone Classification)** — Color segmentation (most tuning needed)
5. **Step 5 (Terrain)** — Procedural heightmap generation
6. **Step 6 (Export)** — Assembly + format conversion
7. **Unity integration** — Modify HoleImporter.cs to handle lite pipeline

Steps 1-2 first because they produce visible output immediately (the cropped/
upscaled images). Step 4 before 3 because tee detection is simpler and gives
quick validation. Step 3 is the most iterative (tuning color thresholds).

---

## Open Questions

1. **Terrain aspect ratio:** The illustrations are roughly 4:3 (530×638 after crop).
   Should the terrain be rectangular to match, or square? Rectangular is more
   accurate but the heightmap would need non-square resolution.
   → **Decision: Use rectangular terrain.** Match the illustration aspect ratio.

2. **Green detection:** The green is a distinct brighter green with a visible
   outline/shadow. Can we detect the green boundary reliably enough to mark it
   as a separate zone? If not, manual placement may be needed.
   → **Try auto-detect first.** Fall back to manual annotation if it fails.

3. **OB boundary detection:** OB is marked with white lines in the illustration.
   Line detection is harder than area detection. May need Canny edge detection
   or just skip OB zones for now.
   → **Defer OB to v2.** Focus on surface zones first.

4. **Multiple courses:** The tool is built for Lomond CC but the color calibration
   will differ per course. The `config/` folder holds per-course settings.
   → **Build for extensibility** but only calibrate Lomond for now.
