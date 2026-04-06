/**
 * colors.mjs — HSL conversion and color matching utilities
 */

export function rgbToHsl(r, g, b) {
  r /= 255; g /= 255; b /= 255;
  const max = Math.max(r, g, b), min = Math.min(r, g, b);
  let h, s, l = (max + min) / 2;
  if (max === min) {
    h = s = 0;
  } else {
    const d = max - min;
    s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
    switch (max) {
      case r: h = ((g - b) / d + (g < b ? 6 : 0)) / 6; break;
      case g: h = ((b - r) / d + 2) / 6; break;
      case b: h = ((r - g) / d + 4) / 6; break;
    }
  }
  return { h: h * 360, s, l };
}

/**
 * Check if a hue falls within a range, handling wrap-around (e.g., 340-20 for red).
 */
export function hueInRange(hue, range) {
  if (!range) return true;
  const [lo, hi] = range;
  if (lo <= hi) return hue >= lo && hue <= hi;
  // Wrap-around: e.g., [340, 20] means 340-360 OR 0-20
  return hue >= lo || hue <= hi;
}
