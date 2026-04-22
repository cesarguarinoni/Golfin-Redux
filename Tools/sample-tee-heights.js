const fs = require('fs');

const RES = 2049;
const terrainWidthM = 151.6;
const terrainLengthM = 127.2;
const elevRangeM = 34.9;

const raw = fs.readFileSync('C:/Users/cesar/GolfinRedux/Tools/UHoleGeo/output/lomond-country-club/holes/04/heightmap.raw');
// read uint16be, row 0 = south
function h(row, col) {
  const idx = (row * RES + col) * 2;
  const v = raw.readUInt16BE(idx);
  return (v / 65535) * elevRangeM;
}

// world (wx, wz) -> cell (col, row)
// The Unity terrain is centered: terrainGO.position = (-terrainX/2, ..., -terrainZ/2)
// So world x=0 is at col=RES/2, world z=0 is at row=RES/2 (since row 0 = south = negative z)
// terrainPos.x = -terrainX/2, cellW = terrainX/(RES-1)
// wx = terrainPos.x + col * cellW  =>  col = (wx - terrainPos.x) / cellW = (wx + terrainX/2) / cellW
const cellW = terrainWidthM / (RES - 1);
const cellH = terrainLengthM / (RES - 1);

function toCell(wx, wz) {
  const col = Math.round((wx + terrainWidthM / 2) / cellW);
  const row = Math.round((wz + terrainLengthM / 2) / cellH);
  return { row, col };
}

const zones = JSON.parse(fs.readFileSync('C:/Users/cesar/GolfinRedux/Tools/UHoleGeo/output/lomond-country-club/export/hole-04/zone-contours.json','utf8'));

for (const tee of zones.zones.tee) {
  console.log(`\n=== Tee ${tee.id} (center ${JSON.stringify(tee.center_local)}, size ${JSON.stringify(tee.size_m)}) ===`);
  
  // sample heights at each contour vertex
  const contourHeights = [];
  for (const p of tee.contour) {
    const { row, col } = toCell(p.x, p.z);
    if (row < 0 || row >= RES || col < 0 || col >= RES) continue;
    contourHeights.push(h(row, col));
  }
  contourHeights.sort((a, b) => a - b);
  const minH = contourHeights[0];
  const maxH = contourHeights[contourHeights.length - 1];
  const medianH = contourHeights[Math.floor(contourHeights.length / 2)];
  console.log(`contour heights: min=${minH.toFixed(2)}m, median=${medianH.toFixed(2)}m, max=${maxH.toFixed(2)}m, spread=${(maxH-minH).toFixed(2)}m`);
  
  // sample interior (center)
  const centerCell = toCell(tee.center_local.x, tee.center_local.z);
  const centerH = h(centerCell.row, centerCell.col);
  console.log(`center height: ${centerH.toFixed(2)}m`);
  
  // Now we compute the tee's maxH the same way FlattenTerrainUnderTees does:
  // iterate ALL cells inside the contour polygon.
  // For speed, just iterate a bbox and do a point-in-polygon.
  let bboxMinX = Infinity, bboxMaxX = -Infinity, bboxMinZ = Infinity, bboxMaxZ = -Infinity;
  for (const p of tee.contour) {
    if (p.x < bboxMinX) bboxMinX = p.x;
    if (p.x > bboxMaxX) bboxMaxX = p.x;
    if (p.z < bboxMinZ) bboxMinZ = p.z;
    if (p.z > bboxMaxZ) bboxMaxZ = p.z;
  }
  function inPoly(px, pz, poly) {
    let inside = false;
    for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
      if (((poly[i].z > pz) !== (poly[j].z > pz)) &&
          (px < (poly[j].x - poly[i].x) * (pz - poly[i].z) / (poly[j].z - poly[i].z) + poly[i].x))
        inside = !inside;
    }
    return inside;
  }
  let teeMaxH = -Infinity, teeMinH = Infinity;
  const rowLo = Math.max(0, Math.floor((bboxMinZ + terrainLengthM/2) / cellH));
  const rowHi = Math.min(RES-1, Math.ceil((bboxMaxZ + terrainLengthM/2) / cellH));
  const colLo = Math.max(0, Math.floor((bboxMinX + terrainWidthM/2) / cellW));
  const colHi = Math.min(RES-1, Math.ceil((bboxMaxX + terrainWidthM/2) / cellW));
  for (let row = rowLo; row <= rowHi; row++) {
    const wz = -terrainLengthM/2 + row * cellH;
    for (let col = colLo; col <= colHi; col++) {
      const wx = -terrainWidthM/2 + col * cellW;
      if (inPoly(wx, wz, tee.contour)) {
        const v = h(row, col);
        if (v > teeMaxH) teeMaxH = v;
        if (v < teeMinH) teeMinH = v;
      }
    }
  }
  console.log(`INTERIOR maxH=${teeMaxH.toFixed(2)}m, minH=${teeMinH.toFixed(2)}m, spread=${(teeMaxH-teeMinH).toFixed(2)}m`);
  
  // Now sample terrain at 2m (skirt radius) OUTSIDE the contour in 16 directions
  const cx = tee.center_local.x, cz = tee.center_local.z;
  console.log(`\nBaseline heights at skirt outer edge (2m outside contour):`);
  const drops = [];
  for (let ang = 0; ang < 360; ang += 22.5) {
    const dx = Math.cos(ang * Math.PI / 180);
    const dz = Math.sin(ang * Math.PI / 180);
    // walk outward from center until we're 2m outside the contour
    // just pick a point at tee_size/2 + 2m from center in this direction
    const r = Math.max(tee.size_m.x, tee.size_m.z) / 2 + 2.0;
    const wx = cx + dx * r;
    const wz = cz + dz * r;
    const { row, col } = toCell(wx, wz);
    if (row < 0 || row >= RES || col < 0 || col >= RES) continue;
    const v = h(row, col);
    const drop = teeMaxH - v;
    drops.push({ ang, wx: wx.toFixed(1), wz: wz.toFixed(1), v: v.toFixed(2), drop: drop.toFixed(2) });
  }
  drops.sort((a, b) => parseFloat(b.drop) - parseFloat(a.drop));
  for (const d of drops.slice(0, 6)) {
    console.log(`  ang=${d.ang}° at (${d.wx},${d.wz}) baseline=${d.v}m  DROP FROM maxH = ${d.drop}m`);
  }
}
