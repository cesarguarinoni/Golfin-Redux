/**
 * terrain.mjs — 2D value noise and terrain generation utilities
 */

/**
 * Simple integer hash for reproducible pseudo-random values.
 * Returns a float in [-1, 1].
 */
function hash(x, y) {
  let n = (x | 0) * 374761393 + (y | 0) * 668265263;
  n = ((n ^ (n >> 13)) * 1274126177) | 0;
  return ((n ^ (n >> 16)) & 0x7fffffff) / 1073741824 - 1;
}

/**
 * Smooth 2D value noise with cosine interpolation.
 */
function smoothNoise(x, y) {
  const ix = Math.floor(x), iy = Math.floor(y);
  const fx = x - ix, fy = y - iy;
  const sx = (1 - Math.cos(fx * Math.PI)) * 0.5;
  const sy = (1 - Math.cos(fy * Math.PI)) * 0.5;
  const v00 = hash(ix, iy), v10 = hash(ix + 1, iy);
  const v01 = hash(ix, iy + 1), v11 = hash(ix + 1, iy + 1);
  const top = v00 + sx * (v10 - v00);
  const bot = v01 + sx * (v11 - v01);
  return top + sy * (bot - top);
}

/**
 * Multi-octave 2D value noise (Perlin-like).
 * @param {number} x - X coordinate
 * @param {number} y - Y coordinate
 * @param {number} octaves - Number of octaves (default 4)
 * @returns {number} Value in [-1, 1]
 */
export function perlin2D(x, y, octaves = 4) {
  let value = 0, amplitude = 1, frequency = 1, maxAmp = 0;
  for (let i = 0; i < octaves; i++) {
    value += smoothNoise(x * frequency, y * frequency) * amplitude;
    maxAmp += amplitude;
    amplitude *= 0.5;
    frequency *= 2;
  }
  return value / maxAmp;
}

/**
 * Simple 3×3 Gaussian-like blur on a 2D float array.
 * @param {Float64Array} data - width×height array
 * @param {number} width
 * @param {number} height
 * @param {number} passes - number of blur passes
 * @returns {Float64Array}
 */
export function blur2D(data, width, height, passes = 1) {
  let src = new Float64Array(data);
  let dst = new Float64Array(width * height);

  for (let p = 0; p < passes; p++) {
    for (let y = 0; y < height; y++) {
      for (let x = 0; x < width; x++) {
        let sum = 0, weight = 0;
        for (let dy = -1; dy <= 1; dy++) {
          for (let dx = -1; dx <= 1; dx++) {
            const nx = x + dx, ny = y + dy;
            if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
            const w = (dx === 0 && dy === 0) ? 4 : (dx === 0 || dy === 0) ? 2 : 1;
            sum += src[ny * width + nx] * w;
            weight += w;
          }
        }
        dst[y * width + x] = sum / weight;
      }
    }
    [src, dst] = [dst, src];
  }
  return src;
}
