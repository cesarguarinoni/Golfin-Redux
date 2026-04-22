const MaxRampSlope = 0.35;
const maxH = 20.71;
const BaseSkirtM = 2.0;
const MaxSkirtM = 10.0;

const profile = [
  { r: 0.0, bH: 19.81 },
  { r: 2.0, bH: 19.26 },
  { r: 5.0, bH: 18.29 },
  { r: 8.0, bH: 17.15 },
  { r: 12.0, bH: 15.40 },
  { r: 16.0, bH: 13.60 },
  { r: 20.0, bH: 11.95 },
];

console.log('Per-cell adaptive dR = clamp(1.5 * drop / MaxRampSlope, BaseSkirtM, MaxSkirtM=10)');
console.log('');
console.log('r(m) | baseline | drop | dR(m) | t=r/dR | smooth | rampedH | cellSlope | effectiveRampSlope@cell');
let prev = null;
for (const p of profile) {
  const drop = Math.max(0, maxH - p.bH);
  const dRraw = 1.5 * drop / MaxRampSlope;
  const dR = Math.max(BaseSkirtM, Math.min(MaxSkirtM, dRraw));
  const t = Math.min(1, p.r / dR);
  const s = t * t * (3 - 2 * t);
  const rampedH = maxH * (1 - s) + p.bH * s;
  const ds = 6 * t * (1 - t);
  const rampSlopeAtCell = Math.abs((maxH - p.bH) * ds / dR);
  let cellSlope = '';
  if (prev) cellSlope = (Math.abs(rampedH - prev.rampedH) / (p.r - prev.r)).toFixed(3);
  const culled = (p.r > dR ? ' OUTSIDE' : '');
  console.log(`${p.r.toFixed(1).padStart(5)} | ${p.bH.toFixed(2).padStart(7)} | ${drop.toFixed(2).padStart(5)} | ${dR.toFixed(2).padStart(5)} | ${t.toFixed(3).padStart(6)} | ${s.toFixed(3).padStart(6)} | ${rampedH.toFixed(2).padStart(7)} | ${cellSlope.padStart(7)} | ${rampSlopeAtCell.toFixed(3)}${culled}`);
  prev = { r: p.r, rampedH };
}

console.log('');
console.log('With MaxSkirtM=10m cap, at r>10m the skirt does nothing (MAX merge preserves baseline).');
