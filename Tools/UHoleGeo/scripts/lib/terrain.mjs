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

/**
 * Separable Gaussian blur on a 2D float array (no mask — blurs everything).
 * @param {Float64Array} data - width×height array
 * @param {number} width
 * @param {number} height
 * @param {number} sigma - Gaussian standard deviation in cells
 * @returns {Float64Array} blurred copy
 */
export function gaussianBlur(data, width, height, sigma) {
  const radius = Math.ceil(sigma * 3);
  const kernelSize = radius * 2 + 1;
  const kernel = new Float64Array(kernelSize);
  let kernelSum = 0;
  for (let i = 0; i < kernelSize; i++) {
    const d = i - radius;
    kernel[i] = Math.exp(-(d * d) / (2 * sigma * sigma));
    kernelSum += kernel[i];
  }
  for (let i = 0; i < kernelSize; i++) kernel[i] /= kernelSum;

  // Horizontal pass
  const temp = new Float64Array(width * height);
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      let sum = 0, wSum = 0;
      for (let k = 0; k < kernelSize; k++) {
        const sx = x + k - radius;
        if (sx < 0 || sx >= width) continue;
        sum += data[y * width + sx] * kernel[k];
        wSum += kernel[k];
      }
      temp[y * width + x] = sum / wSum;
    }
  }

  // Vertical pass
  const result = new Float64Array(width * height);
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      let sum = 0, wSum = 0;
      for (let k = 0; k < kernelSize; k++) {
        const sy = y + k - radius;
        if (sy < 0 || sy >= height) continue;
        sum += temp[sy * width + x] * kernel[k];
        wSum += kernel[k];
      }
      result[y * width + x] = sum / wSum;
    }
  }

  return result;
}

/**
 * Separable Gaussian blur on a 2D float array with zone mask.
 * Only blurs cells where mask[i] is truthy. Cells outside the
 * mask are treated as zero during convolution (boundary handling).
 * Uses two-pass separable approach: horizontal then vertical.
 *
 * @param {Float64Array} data - width×height array
 * @param {Uint8Array} mask - width×height mask (1 = blur, 0 = skip)
 * @param {number} width
 * @param {number} height
 * @param {number} sigma - Gaussian standard deviation in cells
 * @returns {Float64Array} blurred copy
 */
export function gaussianBlurMasked(data, mask, width, height, sigma) {
  const radius = Math.ceil(sigma * 3);
  const kernelSize = radius * 2 + 1;
  const kernel = new Float64Array(kernelSize);
  let kernelSum = 0;
  for (let i = 0; i < kernelSize; i++) {
    const d = i - radius;
    kernel[i] = Math.exp(-(d * d) / (2 * sigma * sigma));
    kernelSum += kernel[i];
  }
  for (let i = 0; i < kernelSize; i++) kernel[i] /= kernelSum;

  // Horizontal pass
  const temp = new Float64Array(width * height);
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const idx = y * width + x;
      if (!mask[idx]) { temp[idx] = data[idx]; continue; }
      let sum = 0, wSum = 0;
      for (let k = 0; k < kernelSize; k++) {
        const sx = x + k - radius;
        if (sx < 0 || sx >= width) continue;
        const sIdx = y * width + sx;
        if (!mask[sIdx]) continue;
        sum += data[sIdx] * kernel[k];
        wSum += kernel[k];
      }
      temp[idx] = wSum > 0 ? sum / wSum : data[idx];
    }
  }

  // Vertical pass
  const result = new Float64Array(width * height);
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const idx = y * width + x;
      if (!mask[idx]) { result[idx] = temp[idx]; continue; }
      let sum = 0, wSum = 0;
      for (let k = 0; k < kernelSize; k++) {
        const sy = y + k - radius;
        if (sy < 0 || sy >= height) continue;
        const sIdx = sy * width + x;
        if (!mask[sIdx]) continue;
        sum += temp[sIdx] * kernel[k];
        wSum += kernel[k];
      }
      result[idx] = wSum > 0 ? sum / wSum : temp[idx];
    }
  }

  return result;
}

/**
 * Fritsch-Carlson monotone cubic interpolation.
 *
 * Unlike natural cubic splines, this preserves the monotonicity of
 * each interval: flat data stays flat, drops stay monotone. This
 * prevents overshoot and preserves terrace-like elevation profiles.
 *
 * @param {number[]} xs - sorted x coordinates (strictly increasing)
 * @param {number[]} ys - y values at each x
 * @returns {function(number): number} interpolation function
 */
export function monotoneCubicSpline(xs, ys) {
  const n = xs.length;
  if (n < 2) return () => ys[0] || 0;
  if (n === 2) {
    const slope = (ys[1] - ys[0]) / (xs[1] - xs[0]);
    return (x) => {
      if (x <= xs[0]) return ys[0];
      if (x >= xs[1]) return ys[1];
      return ys[0] + slope * (x - xs[0]);
    };
  }

  // Step 1: Compute secants (slopes between consecutive points)
  const delta = new Float64Array(n - 1);
  for (let i = 0; i < n - 1; i++) {
    delta[i] = (ys[i + 1] - ys[i]) / (xs[i + 1] - xs[i]);
  }

  // Step 2: Initialize tangents using average of neighbouring secants
  const m = new Float64Array(n);
  m[0] = delta[0];
  m[n - 1] = delta[n - 2];
  for (let i = 1; i < n - 1; i++) {
    if (delta[i - 1] * delta[i] <= 0) {
      // Sign change or zero — set tangent to zero (local extremum)
      m[i] = 0;
    } else {
      m[i] = (delta[i - 1] + delta[i]) / 2;
    }
  }

  // Step 3: Fritsch-Carlson monotonicity correction
  for (let i = 0; i < n - 1; i++) {
    if (Math.abs(delta[i]) < 1e-30) {
      // Flat segment — force both tangents to zero
      m[i] = 0;
      m[i + 1] = 0;
    } else {
      const alpha = m[i] / delta[i];
      const beta = m[i + 1] / delta[i];

      // Check if (alpha, beta) falls outside the monotonicity region
      // The region is: alpha² + beta² <= 9
      const h = Math.sqrt(alpha * alpha + beta * beta);
      if (h > 3) {
        const tau = 3 / h;
        m[i] = tau * alpha * delta[i];
        m[i + 1] = tau * beta * delta[i];
      }
    }
  }

  // Step 4: Build Hermite basis evaluation function
  return function (x) {
    if (x <= xs[0]) return ys[0];
    if (x >= xs[n - 1]) return ys[n - 1];

    // Binary search for interval
    let lo = 0, hi = n - 2;
    while (lo < hi) {
      const mid = (lo + hi) >> 1;
      if (xs[mid + 1] < x) lo = mid + 1;
      else hi = mid;
    }
    const i = lo;

    // Hermite interpolation on [xs[i], xs[i+1]]
    const h = xs[i + 1] - xs[i];
    const t = (x - xs[i]) / h;
    const t2 = t * t;
    const t3 = t2 * t;

    const h00 = 2 * t3 - 3 * t2 + 1;
    const h10 = t3 - 2 * t2 + t;
    const h01 = -2 * t3 + 3 * t2;
    const h11 = t3 - t2;

    return h00 * ys[i] + h10 * h * m[i] +
           h01 * ys[i + 1] + h11 * h * m[i + 1];
  };
}
