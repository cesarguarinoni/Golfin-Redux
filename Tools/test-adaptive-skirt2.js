const MaxRampSlope = 0.35;
const maxH = 20.71;
const BaseSkirtM = 2.0;

const profile = [
  { r: 0.0, bH: 19.81 },
  { r: 2.0, bH: 19.26 },
  { r: 5.0, bH: 18.29 },
  { r: 8.0, bH: 17.15 },
  { r: 12.0, bH: 15.40 },
  { r: 16.0, bH: 13.60 },
  { r: 20.0, bH: 11.95 },
  { r: 25.0, bH: 10.24 },
];

console.log('Per-cell adaptive dR = max(BaseSkirtM, 1.5 * |drop| / MaxRampSlope)');
console.log('');
console.log('r(m) | baseline | drop | dR(m) | t=r/dR | smooth | rampedH | cellToCellSlope');
let prev = null;
for (const p of profile) {
  const drop = Math.max(0, maxH - p.bH);
  const dR = Math.max(BaseSkirtM, 1.5 * drop / MaxRampSlope);
  const t = Math.min(1, p.r / dR);
  const s = t * t * (3 - 2 * t);
  const rampedH = maxH * (1 - s) + p.bH * s;
  let cellSlope = '';
  if (prev) {
    cellSlope = (Math.abs(rampedH - prev.rampedH) / (p.r - prev.r)).toFixed(3);
  }
  console.log(`${p.r.toFixed(1).padStart(5)} | ${p.bH.toFixed(2).padStart(7)} | ${drop.toFixed(2).padStart(5)} | ${dR.toFixed(2).padStart(5)} | ${t.toFixed(3).padStart(6)} | ${s.toFixed(3).padStart(6)} | ${rampedH.toFixed(2).padStart(7)} | ${cellSlope.padStart(7)}`);
  prev = { r: p.r, rampedH };
}
