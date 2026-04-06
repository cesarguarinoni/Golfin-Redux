# TASK.md — UHole Instructions for Claude Code

> Read this file at the start of each task. After completing, add a status line at the bottom.

## Context

- Working directory: `Tools/UHole/`
- Vanilla HTML/CSS/JS app — zero npm dependencies
- ESM scripts (`.mjs`, `"type": "module"`)
- Dev server: `http://127.0.0.1:4173` or `4174`
- App: `app/index.html`, `app/styles.css`, `app/app.js`
- Data: `output/lomond-country-club/`

---

## Current Task — Add DEM5A (5m lidar) Support to Basemap Fetch

**Goal:** Fetch higher-resolution DEM tiles from GSI's DEM5A dataset alongside the
existing z14 DEM tiles. DEM5A provides ~5m resolution lidar-derived elevation data
(vs ~8m from the current z14 CSV tiles). This will significantly improve terrain accuracy.

**Important:** DEM5A doesn't cover all of Japan — availability depends on whether lidar
surveys have been conducted in the area. The fetch should handle missing tiles gracefully.

### Step 1: Add DEM5A PNG tile fetching to `scripts/fetch-gsi-basemap.mjs`

Add a new dataset to the fetch alongside the existing `gsi-photo-z17` and `gsi-dem-z14`:

**New dataset:**
- ID: `gsi-dem5a-z15`
- URL template: `https://cyberjapandata.gsi.go.jp/xyz/dem5a_png/{z}/{x}/{y}.png`
- Zoom level: 15
- Tile size: 256×256 PNG
- Format: RGB-encoded elevation (see decoding below)

Add the tile fetching logic similar to the existing photo/DEM fetch, but at zoom 15.
Store tiles in: `output/lomond-country-club/basemap/gsi-dem5a-z15/`

Use the same coverage area as the photo tiles (same `coverageMeters` config).

**Handle missing tiles:** DEM5A may return 404 for areas without lidar coverage.
Log a warning but don't fail — just skip that tile. Track how many tiles were
successfully fetched vs skipped in the manifest.

### Step 2: PNG Elevation Decoding

GSI PNG elevation tiles encode elevation in RGB channels. The decoding formula:

```javascript
function decodePngElevation(r, g, b) {
  // GSI PNG elevation tile format:
  // If R=128, G=0, B=0 → nodata
  if (r === 128 && g === 0 && b === 0) return null;
  
  // Elevation in centimeters as a 24-bit value
  const raw = r * 65536 + g * 256 + b;
  
  // If MSB is set (r >= 128), it's negative
  if (r >= 128) {
    return (raw - 16777216) * 0.01; // negative elevation in meters
  }
  return raw * 0.01; // positive elevation in meters
}
```

**Note:** This is the GSI-specific format, NOT the Mapbox terrain-rgb format.
The GSI format stores elevation in centimeters as a signed 24-bit integer
packed into RGB.

### Step 3: Create a DEM5A tile parser

Create `scripts/lib/dem5a.mjs` with functions to:

1. Read a DEM5A PNG tile and decode all 256×256 pixels to elevation values
2. Sample elevation at a given lat/lon from a set of loaded DEM5A tiles
3. Handle nodata pixels (interpolate from neighbors or return null)

```javascript
// Read PNG tile using Node.js built-in (no dependencies)
// PNG is a zlib-compressed format. Use node:zlib to decompress.
// Or simpler: just fetch the raw bytes via the tile URL and decode
// the PNG manually using a minimal PNG reader.

// Actually, the simplest approach for Node.js without dependencies:
// Use the PNG format's structure to extract RGBA data.
// PNG files: signature (8 bytes) → IHDR chunk → IDAT chunk(s) → IEND
// The IDAT contains zlib-compressed filtered scanlines.
// Node's built-in zlib can decompress.

import { readFile } from "node:fs/promises";
import { inflateSync } from "node:zlib";

function parsePng(buffer) {
  // Verify PNG signature
  const signature = [137, 80, 78, 71, 13, 10, 26, 10];
  for (let i = 0; i < 8; i++) {
    if (buffer[i] !== signature[i]) throw new Error("Not a PNG file");
  }
  
  let offset = 8;
  let width = 0, height = 0, bitDepth = 0, colorType = 0;
  const idatChunks = [];
  
  while (offset < buffer.length) {
    const length = buffer.readUInt32BE(offset);
    const type = buffer.toString("ascii", offset + 4, offset + 8);
    const data = buffer.subarray(offset + 8, offset + 8 + length);
    
    if (type === "IHDR") {
      width = data.readUInt32BE(0);
      height = data.readUInt32BE(4);
      bitDepth = data[8];
      colorType = data[9];
    } else if (type === "IDAT") {
      idatChunks.push(data);
    } else if (type === "IEND") {
      break;
    }
    
    offset += 12 + length; // 4 (length) + 4 (type) + length + 4 (CRC)
  }
  
  // Concatenate and decompress IDAT
  const compressed = Buffer.concat(idatChunks);
  const decompressed = inflateSync(compressed);
  
  // Unfilter scanlines (each row has a 1-byte filter prefix)
  const channels = colorType === 2 ? 3 : colorType === 6 ? 4 : 1;
  const bpp = channels * (bitDepth / 8);
  const rowBytes = width * bpp;
  const pixels = new Uint8Array(width * height * channels);
  
  let prevRow = new Uint8Array(rowBytes);
  for (let y = 0; y < height; y++) {
    const filterType = decompressed[y * (rowBytes + 1)];
    const rowStart = y * (rowBytes + 1) + 1;
    const row = new Uint8Array(rowBytes);
    
    for (let i = 0; i < rowBytes; i++) {
      const raw = decompressed[rowStart + i];
      const a = i >= bpp ? row[i - bpp] : 0;
      const b = prevRow[i];
      const c = i >= bpp ? prevRow[i - bpp] : 0;
      
      switch (filterType) {
        case 0: row[i] = raw; break;
        case 1: row[i] = (raw + a) & 0xFF; break;
        case 2: row[i] = (raw + b) & 0xFF; break;
        case 3: row[i] = (raw + Math.floor((a + b) / 2)) & 0xFF; break;
        case 4: row[i] = (raw + paethPredictor(a, b, c)) & 0xFF; break;
      }
    }
    
    pixels.set(row, y * rowBytes);
    prevRow = row;
  }
  
  return { width, height, channels, pixels };
}

function paethPredictor(a, b, c) {
  const p = a + b - c;
  const pa = Math.abs(p - a);
  const pb = Math.abs(p - b);
  const pc = Math.abs(p - c);
  if (pa <= pb && pa <= pc) return a;
  if (pb <= pc) return b;
  return c;
}
```

### Step 4: Update the basemap manifest

Add the DEM5A dataset to `manifest.json` alongside the existing datasets:

```json
{
  "id": "gsi-dem5a-z15",
  "label": "GSI DEM5A (5m lidar)",
  "type": "elevation_hires",
  "zoom": 15,
  "tile_span_m": ...,
  "local_root": "basemap/gsi-dem5a-z15",
  "tile_count": ...,
  "tiles_available": ...,
  "tiles_missing": ...,
  "tiles": [...]
}
```

Include `tiles_available` and `tiles_missing` counts so we know coverage.

### Step 5: Update `export-hole.mjs` to prefer DEM5A

In `extractHeightmap()`, check for `gsi-dem5a-z15` dataset first. If available,
sample from DEM5A tiles. Fall back to `gsi-dem-z14` for any pixels where DEM5A
has no data (missing tile or nodata pixel).

This gives us the best of both: lidar precision where available, z14 coverage
everywhere else.

### Step 6: Test

Run:
```powershell
cd Tools/UHole
node scripts/fetch-gsi-basemap.mjs --force
```

Check:
- How many DEM5A tiles were fetched vs 404'd for the Lomond area
- Whether the PNG files are valid and decodable
- Log the elevation range from a sample tile to verify decoding

Then re-export Hole 1:
```powershell
node scripts/export-hole.mjs 1
```

Compare the heightmap stats (min/max elevation, terrain dimensions) with the
previous z14-only export.

### Verification

- [ ] DEM5A tiles fetched to `basemap/gsi-dem5a-z15/`
- [ ] Manifest includes `gsi-dem5a-z15` dataset with tile counts
- [ ] PNG decoder correctly extracts elevation values
- [ ] Sample elevation values are reasonable (100-300m range for Lomond area)
- [ ] `export-hole.mjs` prefers DEM5A over z14 where available
- [ ] Falls back to z14 gracefully where DEM5A has no coverage
- [ ] Re-exported heightmap has more terrain detail than before (if DEM5A covers the area)

### Do NOT change

- Photo tile fetching (z17)
- The alignment tool
- The Unity importer
- Existing z14 DEM fetching (keep it as fallback)

---

## Current Task — Diagnose Texture-Terrain Offset

**Problem:** The aerial texture and terrain heightmap are misaligned despite both
using the same geographic bounds and lat/lon sampling. This has persisted through
multiple fix attempts. We need a diagnostic approach.

### Diagnostic: Bake a test pattern into both heightmap and texture

Modify `export-hole.mjs` TEMPORARILY to add a diagnostic marker:

**In `extractHeightmap()`**, after the normal elevation sampling, set the
NW corner 10x10 pixels to maximum elevation (a visible bump):

```javascript
// Diagnostic: mark NW corner of heightmap
for (let row = 0; row < 10; row++) {
  for (let col = 0; col < 10; col++) {
    heightmap[row * HEIGHTMAP_RES + col] = maxElev;
  }
}
console.log("DIAGNOSTIC: NW corner of heightmap set to max elevation");
```

Then in the Unity importer's `ApplyAerialTexture()`, mark the NW corner of
the texture with a bright red square:

```csharp
// Diagnostic: mark NW corner of texture
// NW = py=0 (V=0=minZ=north), px=0 (U=0=minX=west)
for (int py = 0; py < 20; py++) {
    for (int px = 0; px < 20; px++) {
        outputPixels[py * texRes + px] = Color.red;
    }
}
UnityEngine.Debug.Log("DIAGNOSTIC: NW corner of texture marked red");
```

Re-export and re-import. In Unity Scene view from above:
- The heightmap bump (elevated square) should be in the NW corner of the terrain
- The red texture square should be in the SAME NW corner
- If they're in different corners, we know exactly which axis is flipped

### What to look for

| Bump position | Red square position | Diagnosis |
|---|---|---|
| Same corner | Same corner | Alignment is correct, offset is from data |
| NW | SW | Texture V is inverted |
| NW | NE | Texture U is inverted |
| NW | SE | Both U and V inverted |

### After diagnosis

Report which corners the bump and red square appear in. That will tell us
exactly what needs to flip. Then remove the diagnostic code and apply the fix.

### Do NOT

- Change the alignment tool
- Change anchor data
- Leave the diagnostic code in after the fix is identified
