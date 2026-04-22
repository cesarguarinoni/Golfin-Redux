// Simulate per-cell adaptive skirt radius for Tee 1's worst direction (225°)
// maxH = 20.71m, baseline at 2m SW = 12.78m (drop 7.93m)
const MaxRampSlope = 0.35; // ~19 degrees
const maxH = 20.71;

// Natural baseline profile walking SW from tee edge (I sampled this earlier):
const profile = [
  { r: 0.0, bH: 19.81 }, // center, roughly
  { r: 2.0, bH: 19.26 },
  { r: 5.0, bH: 18.29 },
  { r: 8.0, bH: 17.15 },
  { r: 12.0, bH: 15.40 },
  { r: 16.0, bH: 13.60 },
  { r: 20.0, bH: 11.95 },
  { r: 25.0, bH: 10.24 },
];

console.log('Adaptive skirt simulation, Tee 1 SW direction (worst case)');
console.log('');
console.log('r(m) | baseline | drop=maxH-bH | desiredR=drop/0.35 | actualSkirtEnds(=desiredR) | t=r/dR | smoothstep | rampedH | cellSlopeOnRamp');
for (const p of profile) {
  const drop = Math.max(0, maxH - p.bH);
  const dR = drop / MaxRampSlope; // required skirt radius to keep ramp slope ≤ 0.35
  const t = Math.min(1, p.r / dR);
  const s = t * t * (3 - 2 * t);
  const rampedH = maxH * (1 - s) + p.bH * s;
  // Slope of ramp at this point = |d(rampedH)/d(r)| = |(platformY - bH) * smoothstep'(t) / dR|
  const ds = 6 * t * (1 - t);
  const rampSlope = Math.abs((maxH - p.bH) * ds / dR);
  console.log(`${p.r.toFixed(1).padStart(5)} | ${p.bH.toFixed(2).padStart(7)} | ${drop.toFixed(2).padStart(11)} | ${dR.toFixed(2).padStart(17)} | ${(p.r < dR ? 'inside':'outside').padStart(26)} | ${t.toFixed(3).padStart(6)} | ${s.toFixed(3).padStart(10)} | ${rampedH.toFixed(2).padStart(7)} | ${rampSlope.toFixed(3)}`);
}
console.log('');
console.log('Max ramp slope should never exceed MaxRampSlope (0.35)');
console.log('Final skirt radius for this direction:', (Math.max(0, maxH - profile[profile.length-1].bH) / MaxRampSlope).toFixed(1), 'm');
