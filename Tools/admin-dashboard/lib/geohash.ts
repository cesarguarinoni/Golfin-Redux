/**
 * Geohash encoder — a byte-for-byte port of `backend/routers/venue.py::
 * _geohash_encode`, and the SAME precision (9).
 *
 * WHY THE DASHBOARD HAS ITS OWN COPY, and why it is not an input field.
 * `/venue/nearby` finds a venue by `geohash like 'prefix%'`. A row whose
 * geohash disagrees with its coordinates therefore EXISTS, shows on a map, and
 * is invisible to every player's nearby list — a failure with no error message
 * anywhere. gps_checkin § B1 makes that impossible by construction: the field
 * is not editable in the Partners panel, and every save recomputes it from the
 * coordinates being saved.
 *
 * The port must stay exact. `venues.geohash` already holds 1,981 rows written
 * by the Python encoder (2026_07_06_seed_osm_golf_japan.sql); a divergence here
 * would put admin-written rows in cells the OSM rows are not in.
 * `lib/__tests__/geohash.test.ts` pins three known points against it.
 */

const BASE32 = "0123456789bcdefghjkmnpqrstuvwxyz";

export function geohashEncode(lat: number, lon: number, precision = 9): string {
  let latLo = -90.0;
  let latHi = 90.0;
  let lonLo = -180.0;
  let lonHi = 180.0;
  const chars: string[] = [];
  let bit = 0;
  let ch = 0;
  let even = true; // true: longitude bit, false: latitude bit

  while (chars.length < precision) {
    if (even) {
      const mid = (lonLo + lonHi) / 2;
      if (lon >= mid) {
        ch = (ch << 1) | 1;
        lonLo = mid;
      } else {
        ch = ch << 1;
        lonHi = mid;
      }
    } else {
      const mid = (latLo + latHi) / 2;
      if (lat >= mid) {
        ch = (ch << 1) | 1;
        latLo = mid;
      } else {
        ch = ch << 1;
        latHi = mid;
      }
    }
    even = !even;
    bit += 1;
    if (bit === 5) {
      chars.push(BASE32.charAt(ch));
      bit = 0;
      ch = 0;
    }
  }
  return chars.join("");
}

/**
 * `lat,lon` out of what a human actually pastes: a Google Maps URL
 * (`.../@35.6541,139.7792,17z`) or a bare pair. Returns null for anything else,
 * which the caller turns into a Places text search.
 *
 * Mirrors `venue.py::_coords_from_text` — same two shapes, same order.
 */
export function coordsFromText(text: string): { lat: number; lon: number } | null {
  const at = /@(-?\d+\.\d+),\s*(-?\d+\.\d+)/.exec(text);
  const bare = at ? null : /^\s*(-?\d+\.\d+)\s*,\s*(-?\d+\.\d+)\s*$/.exec(text);
  const m = at ?? bare;
  if (!m) return null;
  const lat = Number(m[1]);
  const lon = Number(m[2]);
  if (lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180) return { lat, lon };
  return null;
}
