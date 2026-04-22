const fs = require('fs');
const path = require('path');
const base = 'C:/Users/cesar/GolfinRedux/Tools/UHoleGeo/output/lomond-country-club/export';
fs.readdirSync(base)
  .filter(d => fs.statSync(path.join(base, d)).isDirectory())
  .sort()
  .forEach(h => {
    const p = path.join(base, h, 'zone-contours.json');
    if (!fs.existsSync(p)) return;
    const j = JSON.parse(fs.readFileSync(p, 'utf8'));
    const t = (j.zones && j.zones.tee) ? j.zones.tee : [];
    const px = t.map(r => r.pixel_count).join(',');
    const sizes = t.map(r => {
      const s = r.size_m || {};
      return (s.x || 0).toFixed(0) + 'x' + (s.z || 0).toFixed(0);
    }).join(' ');
    console.log(h.padEnd(20) + ' tees=' + t.length + '  sizes=[' + sizes + ']  px=[' + px + ']');
  });
