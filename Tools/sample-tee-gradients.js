const fs = require('fs');
const RES = 2049, W=151.6, L=127.2, E=34.9;
const raw = fs.readFileSync('C:/Users/cesar/GolfinRedux/Tools/UHoleGeo/output/lomond-country-club/holes/04/heightmap.raw');
const cellW = W/(RES-1), cellH = L/(RES-1);
function h(r,c){ if(r<0||r>=RES||c<0||c>=RES)return 0; return raw.readUInt16BE((r*RES+c)*2)/65535*E; }
function toCell(wx,wz){ return {row: Math.round((wz+L/2)/cellH), col: Math.round((wx+W/2)/cellW)}; }
const t1 = JSON.parse(fs.readFileSync('C:/Users/cesar/GolfinRedux/Tools/UHoleGeo/output/lomond-country-club/export/hole-04/zone-contours.json','utf8')).zones.tee[0];

// Tee 1 center
const cx=48.69, cz=-31.86;
// For each angle, sample baseline at skirt outer edge (roughly 2m past polygon edge in that direction)
// and compute the local gradient magnitude there (|∇baseline|).
const tee1Radius = Math.max(t1.size_m.x, t1.size_m.z) / 2;
console.log('Tee 1 nominal half-extent:', tee1Radius.toFixed(2),'m');
console.log('');
console.log('angle° | baseline@ring (m) | drop from maxH=20.71 | local |grad|');
console.log('-------|-------------------|----------------------|-------------');
for(let a=0; a<360; a+=15){
  const dx=Math.cos(a*Math.PI/180), dz=Math.sin(a*Math.PI/180);
  const r = tee1Radius + 2.0;  // 2m outside polygon boundary
  const wx = cx+dx*r, wz = cz+dz*r;
  const {row,col} = toCell(wx,wz);
  const v = h(row,col);
  // Gradient: central differences
  const dHdx = (h(row, col+1) - h(row, col-1)) / (2*cellW);
  const dHdz = (h(row+1, col) - h(row-1, col)) / (2*cellH);
  const gmag = Math.hypot(dHdx, dHdz);
  console.log(`  ${a.toString().padStart(3)}  |      ${v.toFixed(2).padStart(5)}        |       ${(20.71-v).toFixed(2).padStart(5)}         |   ${gmag.toFixed(3)}`);
}
