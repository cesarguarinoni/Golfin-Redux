import "server-only";
import type { VenueRow } from "./types";
import { mapVenue } from "./venueData";

/**
 * Mock-mode fixtures for the Partners panel. Three rows, one per category, so
 * every branch of the table (partner badge, food/range colouring, a
 * test_fixture row with no partner fields) has something to render.
 *
 * The MUTABLE copy lives in `mockStore` — see venueData.mockUpsertVenue.
 */
export const MOCK_VENUES: VenueRow[] = [
  mapVenue({
    id: 1993, name: "TEST Office (WeWork Harumi)", category: "golf",
    latitude: 35.654103, longitude: 139.779219, geohash: "xn76uf8h5",
    gps_radius_m: 500, source: "test_fixture", is_active: true,
  }),
  mapVenue({
    id: 9001, name: "焼肉 GREEN", category: "food", is_partner: true,
    subtitle: "焼肉 · ゴルファー10%OFF", price_label: "¥3,000〜",
    chip_extra: "10%OFF", partner_offer: "ゴルファー10%OFF",
    latitude: 35.69, longitude: 139.625, geohash: "xn7711pv6",
    gps_radius_m: 300, rating: 4.6, source: "demo", is_active: true,
  }),
  mapVenue({
    id: 9002, name: "GOLF LAB", category: "range", is_partner: true,
    subtitle: "東京都港区 · 高精度シミュ", price_label: "¥2,000/60分",
    chip_extra: "プロ指導", latitude: 35.6585, longitude: 139.745,
    geohash: "xn76ggrjy", gps_radius_m: 300, rating: 4.8,
    source: "demo", is_active: true,
  }),
];
