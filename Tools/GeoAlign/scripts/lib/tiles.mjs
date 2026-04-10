export function lonToTileX(lon, zoom) {
  return Math.floor(((lon + 180) / 360) * (2 ** zoom));
}

export function latToTileY(lat, zoom) {
  const radians = (lat * Math.PI) / 180;
  return Math.floor((1 - Math.log(Math.tan(radians) + 1 / Math.cos(radians)) / Math.PI) / 2 * (2 ** zoom));
}

export function tileXToLon(tileX, zoom) {
  return (tileX / (2 ** zoom)) * 360 - 180;
}

export function tileYToLat(tileY, zoom) {
  const n = Math.PI - (2 * Math.PI * tileY) / (2 ** zoom);
  return (180 / Math.PI) * Math.atan(0.5 * (Math.exp(n) - Math.exp(-n)));
}

export function tileBounds(tileX, tileY, zoom) {
  return {
    west: tileXToLon(tileX, zoom),
    north: tileYToLat(tileY, zoom),
    east: tileXToLon(tileX + 1, zoom),
    south: tileYToLat(tileY + 1, zoom)
  };
}

export function metersPerPixel(lat, zoom) {
  return (156543.03392 * Math.cos((lat * Math.PI) / 180)) / (2 ** zoom);
}

export function tileSpanMeters(lat, zoom) {
  return metersPerPixel(lat, zoom) * 256;
}

export function degreeOffsetsFromMeters(lat, eastMeters, northMeters) {
  const dLat = northMeters / 111320;
  const dLon = eastMeters / (111320 * Math.cos((lat * Math.PI) / 180));
  return { dLat, dLon };
}

export function tileRangeForRadius(lat, lon, zoom, radiusMeters) {
  const { dLat, dLon } = degreeOffsetsFromMeters(lat, radiusMeters, radiusMeters);
  const north = lat + dLat;
  const south = lat - dLat;
  const east = lon + dLon;
  const west = lon - dLon;

  const minX = lonToTileX(west, zoom);
  const maxX = lonToTileX(east, zoom);
  const minY = latToTileY(north, zoom);
  const maxY = latToTileY(south, zoom);

  return { minX, maxX, minY, maxY };
}

export function enumerateTiles(range, zoom) {
  const tiles = [];
  for (let y = range.minY; y <= range.maxY; y += 1) {
    for (let x = range.minX; x <= range.maxX; x += 1) {
      tiles.push({
        x,
        y,
        z: zoom,
        bounds: tileBounds(x, y, zoom)
      });
    }
  }
  return tiles;
}
