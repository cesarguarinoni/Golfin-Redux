/**
 * dem5a.mjs — GSI DEM5A PNG tile parser and elevation sampler
 *
 * Reads GSI PNG elevation tiles (no npm dependencies).
 * PNG decoding uses Node.js built-in zlib.
 */

import { readFile } from "node:fs/promises";
import { inflateSync } from "node:zlib";

const TILE_SIZE = 256;

// --- PNG parser ---

function paethPredictor(a, b, c) {
  const p = a + b - c;
  const pa = Math.abs(p - a);
  const pb = Math.abs(p - b);
  const pc = Math.abs(p - c);
  if (pa <= pb && pa <= pc) return a;
  if (pb <= pc) return b;
  return c;
}

export function parsePng(buffer) {
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

  // Determine channels from colorType
  const channels = colorType === 2 ? 3 : colorType === 6 ? 4 : 1;
  const bpp = channels * (bitDepth / 8);
  const rowBytes = width * bpp;
  const pixels = new Uint8Array(width * height * channels);

  // Unfilter scanlines (each row has a 1-byte filter prefix)
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

// --- GSI PNG elevation decoding ---

export function decodePngElevation(r, g, b) {
  // GSI PNG elevation tile format:
  // If R=128, G=0, B=0 -> nodata
  if (r === 128 && g === 0 && b === 0) return null;

  // Elevation in centimeters as a 24-bit value
  const raw = r * 65536 + g * 256 + b;

  // If MSB is set (r >= 128), it's negative
  if (r >= 128) {
    return (raw - 16777216) * 0.01; // negative elevation in meters
  }
  return raw * 0.01; // positive elevation in meters
}

// --- Tile reading and sampling ---

export async function readDem5aTile(filePath) {
  const buffer = await readFile(filePath);
  const { width, height, channels, pixels } = parsePng(buffer);

  if (width !== TILE_SIZE || height !== TILE_SIZE) {
    throw new Error(`Expected ${TILE_SIZE}x${TILE_SIZE} PNG, got ${width}x${height}`);
  }

  const grid = new Float64Array(TILE_SIZE * TILE_SIZE);
  for (let i = 0; i < TILE_SIZE * TILE_SIZE; i++) {
    const r = pixels[i * channels];
    const g = pixels[i * channels + 1];
    const b = pixels[i * channels + 2];
    const elev = decodePngElevation(r, g, b);
    grid[i] = elev !== null ? elev : NaN;
  }

  return grid;
}

export function sampleDem5a(dem5aTiles, lat, lon) {
  for (const tile of dem5aTiles) {
    const { bounds, grid } = tile;
    if (lon < bounds.west || lon > bounds.east || lat > bounds.north || lat < bounds.south) {
      continue;
    }
    // Fractional position within tile
    const fx = (lon - bounds.west) / (bounds.east - bounds.west);
    const fy = (bounds.north - lat) / (bounds.north - bounds.south);
    const col = Math.min(Math.floor(fx * TILE_SIZE), TILE_SIZE - 1);
    const row = Math.min(Math.floor(fy * TILE_SIZE), TILE_SIZE - 1);
    const val = grid[row * TILE_SIZE + col];
    if (!isNaN(val)) {
      return val;
    }
  }
  return null;
}
