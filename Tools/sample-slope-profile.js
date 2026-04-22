const fs = require('fs');

const RES = 2049;
const terrainWidthM = 151.6;
const terrainLengthM = 127.2;
const elevRangeM = 34.9;

const raw = fs.readFileSync('C:/Users/cesar/GolfinRedux/Tools/UHoleGeo/output/lomond-country-club/holes/04/heightmap.raw');
function h(row, col) {
  const idx = (row * RES + col) * 2;
  const v = raw.readUInt16BE(idx);
  return (v / 65535) * elevRangeM;
}

const cellW = terrainWidthM / (RES - 1);
const cellH = terrainLengthM / (RES - 1);

function toCell(wx, wz) {
  const col = Math.round((wx + terrainWidthM / 2) / cellW);
  const row = Math.round((wz + terrainLengthM / 2) / cellH);
  return { row, col };
}

// Sample a profile walking OUT from Tee 1's SW edge in the SW direction
// The worst drop was at angle 225° (SW) — ~8m drop at 2m out.
// Let's walk from tee edge outward to see the natural slope profile.

// Start point: approximate SW corner of Tee 1
// Tee 1 bbox: x=[35.51, 61.86], z=[-46.68, -16.71]
// SW corner direction from center (48.69, -31.86) toward (35.51, -46.68)
// Unit vector from center to SW corner:
const cx = 48.69, cz = -31.86;
const targetX = 35.51, targetZ = -46.68;
const vx = targetX - cx, vz = targetZ - cz;
const vLen = Math.hypot(vx, vz);
const dx = vx / vLen, dz = vz / vLen;

console.log('Walking from tee center outward in SW direction (worst-case direction)');
console.log('Step (m) | World (x,z)       | Baseline H (m) | Δ from tee center');
console.log('---------|-------------------|----------------|-----------------');

const centerCell = toCell(cx, cz);
const centerH = h(centerCell.row, centerCell.col);

// Walk 0 to 25m outward in 0.5m steps
for (let r = 0; r <= 25; r += 0.5) {
  const wx = cx + dx * r;
  const wz = cz + dz * r;
  const { row, col } = toCell(wx, wz);
  const hh = h(row, col);
  console.log(`  ${r.toFixed(1).padStart(5)} | (${wx.toFixed(2).padStart(6)},${wz.toFixed(2).padStart(6)}) | ${hh.toFixed(2).padStart(14)} | ${(hh-centerH).toFixed(2).padStart(6)}`);
}

console.log('\n--- Same for NE direction (opposite side, uphill) ---');
for (let r = 0; r <= 25; r += 0.5) {
  const wx = cx - dx * r;  // opposite direction
  const wz = cz - dz * r;
  const { row, col } = toCell(wx, wz);
  const hh = h(row, col);
  console.log(`  ${r.toFixed(1).padStart(5)} | (${wx.toFixed(2).padStart(6)},${wz.toFixed(2).padStart(6)}) | ${hh.toFixed(2).padStart(14)} | ${(hh-centerH).toFixed(2).padStart(6)}`);
}
