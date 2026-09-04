import { describe, expect, it } from "vitest";
import { coordsFromText, geohashEncode } from "../geohash";

/**
 * The three fixtures are NOT invented: they are rows that already exist in
 * `public.venues`, written by the PYTHON encoder. If this port ever diverges,
 * an admin-written row lands in a different geohash cell from the OSM rows
 * around it and disappears from `/venue/nearby` — with no error anywhere.
 *
 * That failure is not hypothetical: an audit of all 1,988 rows on 2026-09-03
 * found TWO hand-seeded ones (#1 東京ゴルフ倶楽部, #7 Lomond Country Club, both
 * `source='manual'`) whose stored geohash does not match their coordinates.
 * Both are unreachable from `/venue/nearby` today. `venueData.geohashDrift()`
 * surfaces them in the panel; re-saving either row fixes it.
 */
describe("geohashEncode", () => {
  it("matches the Python encoder on rows already in venues", () => {
    // 2026_07_06_seed_osm_golf_japan.sql:162 — 霞ヶ関カンツリー倶楽部
    expect(geohashEncode(35.8989308, 139.4048214)).toBe("xn7hhppqr");
    // 2026_09_03_seed_demo_spots.sql — 焼肉 GREEN
    expect(geohashEncode(35.69, 139.625)).toBe("xn7711pv6");
    // venues #1993 TEST Office (WeWork Harumi), GPS_DEVICE_PASS row 0.2
    expect(geohashEncode(35.654103, 139.779219)).toBe("xn76uf8h5");
  });

  it("honours the precision argument", () => {
    expect(geohashEncode(35.654103, 139.779219, 5)).toHaveLength(5);
    expect(geohashEncode(35.654103, 139.779219)).toHaveLength(9);
  });

  it("handles the equator/prime-meridian origin without NaN", () => {
    expect(geohashEncode(0, 0)).toMatch(/^[0-9b-hjkmnp-z]{9}$/);
  });
});

describe("coordsFromText", () => {
  it("reads a pasted Google Maps URL", () => {
    expect(coordsFromText("https://www.google.com/maps/@35.654103,139.779219,17z"))
      .toEqual({ lat: 35.654103, lon: 139.779219 });
  });

  it("reads a bare lat,lon pair", () => {
    expect(coordsFromText(" 35.654103, 139.779219 "))
      .toEqual({ lat: 35.654103, lon: 139.779219 });
  });

  it("returns null for a place name, so the caller falls through to search", () => {
    expect(coordsFromText("霞ヶ関カンツリー倶楽部")).toBeNull();
    expect(coordsFromText("WeWork Harumi")).toBeNull();
  });

  it("rejects an out-of-range pair rather than writing a bad row", () => {
    expect(coordsFromText("935.6, 139.7")).toBeNull();
  });
});
