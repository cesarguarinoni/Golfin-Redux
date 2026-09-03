import "server-only";
import { geohashEncode } from "./geohash";
import { isMockMode } from "./mode";
import { getSupabaseAdmin } from "./supabaseAdmin";
import type {
  VenueDrift,
  VenueFilters,
  VenueRow,
  VenuesResponse,
} from "./types";

/**
 * Read side of the Partners panel — `public.venues` (gps_checkin § B1).
 *
 * NOT a content catalog, and shaped so nobody mistakes it for one: no draft, no
 * publish, no version badge. The game reads `venues` per request through
 * `/venue/nearby`, so a save here is live on the player's NEXT fetch. Same
 * posture as the Rewards panel, and the panel says so at the top.
 *
 * Branches mock ↔ live like lib/rewardsData.ts.
 */

type Row = Record<string, unknown>;

export const VENUE_CATEGORIES = ["golf", "range", "food"] as const;
export type VenueCategory = (typeof VENUE_CATEGORIES)[number];

export function isVenueCategory(v: unknown): v is VenueCategory {
  return typeof v === "string" && (VENUE_CATEGORIES as readonly string[]).includes(v);
}

/** The table is 1,9xx rows and the panel is a browse-and-edit surface, not a
 *  report. A page cap keeps a category-wide filter from shipping the whole
 *  thing into the browser. */
export const VENUE_PAGE_SIZE = 200;

function num(v: unknown): number | null {
  if (v === null || v === undefined || v === "") return null;
  const n = Number(v);
  return Number.isFinite(n) ? n : null;
}

function str(v: unknown): string | null {
  if (v === null || v === undefined) return null;
  const s = String(v);
  return s.length ? s : null;
}

export function mapVenue(r: Row): VenueRow {
  const latitude = num(r.latitude);
  const longitude = num(r.longitude);
  const geohash = str(r.geohash);
  return {
    id: Number(r.id),
    name: String(r.name ?? ""),
    category: isVenueCategory(r.category) ? r.category : "golf",
    isPartner: r.is_partner === true,
    subtitle: str(r.subtitle),
    priceLabel: str(r.price_label),
    chipExtra: str(r.chip_extra),
    partnerOffer: str(r.partner_offer),
    latitude,
    longitude,
    geohash,
    address: str(r.address),
    imageUrl: str(r.image_url),
    gpsRadiusM: num(r.gps_radius_m) ?? 500,
    rating: num(r.rating),
    isActive: r.is_active !== false,
    source: str(r.source),
    updatedAt: str(r.updated_at),
    // Computed, never stored: whether the row's geohash agrees with its own
    // coordinates. A row where it does not is invisible to /venue/nearby.
    geohashOk:
      latitude === null || longitude === null || !geohash
        ? true
        : geohashEncode(latitude, longitude).startsWith(geohash.slice(0, 5)),
  };
}

const MOCK_VENUES: VenueRow[] = [
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

/**
 * A page of venues under the panel's filters.
 *
 * `search` is an `ilike` on name — the same operator `/venue/search` uses, so a
 * name an operator can find here is a name the app can find too.
 */
export async function fetchVenues(filters: VenueFilters = {}): Promise<VenuesResponse> {
  if (isMockMode()) {
    return { venues: applyFilters(MOCK_VENUES, filters), mock: true, drift: [] };
  }

  let q = getSupabaseAdmin().from("venues").select("*");

  if (filters.category) q = q.eq("category", filters.category);
  if (filters.partner !== undefined) q = q.eq("is_partner", filters.partner);
  if (filters.active !== undefined) q = q.eq("is_active", filters.active);
  if (filters.source) q = q.eq("source", filters.source);
  if (filters.search) q = q.ilike("name", `%${filters.search}%`);

  const res = await q
    .order("is_partner", { ascending: false })
    .order("name", { ascending: true })
    .limit(VENUE_PAGE_SIZE);
  if (res.error) throw new Error(`venues query failed: ${res.error.message}`);

  const venues = (res.data as Row[]).map(mapVenue);
  return { venues, mock: false, drift: driftFrom(venues) };
}

export async function fetchVenue(id: number): Promise<VenueRow | null> {
  if (isMockMode()) return MOCK_VENUES.find((v) => v.id === id) ?? null;

  const res = await getSupabaseAdmin()
    .from("venues")
    .select("*")
    .eq("id", id)
    .maybeSingle();
  if (res.error) throw new Error(`venues query failed: ${res.error.message}`);
  return res.data ? mapVenue(res.data as Row) : null;
}

/** The distinct `source` values actually present, for the filter chips. Read
 *  from the data rather than hardcoded, because auto-registration invents new
 *  ones (`places_auto_register`) without anyone editing this file. */
export async function fetchVenueSources(): Promise<string[]> {
  if (isMockMode()) {
    return [...new Set(MOCK_VENUES.map((v) => v.source).filter(Boolean))] as string[];
  }
  const res = await getSupabaseAdmin().from("venues").select("source");
  if (res.error) return [];
  const set = new Set<string>();
  for (const r of (res.data ?? []) as Row[]) {
    const s = str(r.source);
    if (s) set.add(s);
  }
  return [...set].sort();
}

/**
 * The one cross-surface warning this panel can raise.
 *
 * A row whose stored geohash disagrees with its own coordinates is INVISIBLE to
 * `/venue/nearby` — the row exists, the map shows it, and no player's list ever
 * contains it. Nothing errors, so it can only be found by comparing the two,
 * which is what this does. Re-saving the row fixes it (§ B1: the geohash is
 * always recomputed on save, never typed).
 *
 * Same posture as `rewardsData.missionDrift`: advisory, best-effort, and shown
 * at the top of the panel rather than buried in a tooltip.
 */
function driftFrom(venues: VenueRow[]): VenueDrift[] {
  return venues
    .filter((v) => !v.geohashOk)
    .slice(0, 20)
    .map((v) => ({
      id: v.id,
      name: v.name,
      stored: v.geohash ?? "",
      computed:
        v.latitude !== null && v.longitude !== null
          ? geohashEncode(v.latitude, v.longitude)
          : "",
    }));
}

function applyFilters(rows: VenueRow[], f: VenueFilters): VenueRow[] {
  return rows.filter((v) => {
    if (f.category && v.category !== f.category) return false;
    if (f.partner !== undefined && v.isPartner !== f.partner) return false;
    if (f.active !== undefined && v.isActive !== f.active) return false;
    if (f.source && v.source !== f.source) return false;
    if (f.search && !v.name.toLowerCase().includes(f.search.toLowerCase())) return false;
    return true;
  });
}
