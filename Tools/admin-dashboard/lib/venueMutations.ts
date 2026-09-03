import "server-only";
import { writeAudit } from "./audit";
import { geohashEncode } from "./geohash";
import { isMockMode } from "./mode";
import { getSupabaseAdmin } from "./supabaseAdmin";
import { fetchVenue, isVenueCategory, mapVenue } from "./venueData";
import type { VenueInput, VenueRow } from "./types";

/**
 * Write side of the Partners panel (gps_checkin § B1).
 *
 * TWO RULES ARE ENFORCED HERE RATHER THAN IN THE UI, because the UI is not the
 * only caller and "the form doesn't have that button" is not a guarantee:
 *
 *  1. THE GEOHASH IS ALWAYS COMPUTED, NEVER ACCEPTED. `/venue/nearby` finds a
 *     venue by `geohash like 'prefix%'`, so a typed geohash that disagrees with
 *     the coordinates makes the row invisible to every player with no error
 *     anywhere. Whatever the request says, the value written is
 *     geohashEncode(lat, lon).
 *  2. THERE IS NO DELETE. `activities.venue_id` is a foreign key: deleting a
 *     venue somebody has checked into either fails or orphans their history.
 *     Deactivation (`is_active = false`) removes it from `/venue/nearby` on the
 *     client's next fetch, which is what "remove this partner" actually means.
 */

export type Outcome =
  | { ok: true; message: string; venue: VenueRow }
  | { ok: false; status: number; message: string };

const MAX_TEXT = 200;

function clean(v: string | null | undefined): string | null {
  if (v === null || v === undefined) return null;
  const t = String(v).trim();
  return t.length ? t.slice(0, MAX_TEXT) : null;
}

function validate(input: Partial<VenueInput>, requireName: boolean): string | null {
  if (requireName && !clean(input.name)) return "name is required.";
  if (input.category !== undefined && !isVenueCategory(input.category)) {
    return "category must be one of golf, range, food.";
  }
  if (input.latitude !== undefined || input.longitude !== undefined) {
    const lat = Number(input.latitude);
    const lon = Number(input.longitude);
    if (!Number.isFinite(lat) || lat < -90 || lat > 90) return "latitude must be between -90 and 90.";
    if (!Number.isFinite(lon) || lon < -180 || lon > 180) return "longitude must be between -180 and 180.";
  }
  if (input.gpsRadiusM !== undefined && input.gpsRadiusM !== null) {
    const r = Number(input.gpsRadiusM);
    // 50 m is tighter than consumer GPS is accurate; 5 km is bigger than any
    // course in the OSM seed (max 2,453 m). Outside that the number is a typo.
    if (!Number.isInteger(r) || r < 50 || r > 5000) {
      return "gps_radius_m must be a whole number between 50 and 5000.";
    }
  }
  return null;
}

/** The DB row for an input. `geohash` is derived, never read from the input. */
function toRow(input: Partial<VenueInput>): Record<string, unknown> {
  const row: Record<string, unknown> = {};
  if (input.name !== undefined) row.name = clean(input.name);
  if (input.category !== undefined) row.category = input.category;
  if (input.isPartner !== undefined) row.is_partner = input.isPartner === true;
  if (input.subtitle !== undefined) row.subtitle = clean(input.subtitle);
  if (input.priceLabel !== undefined) row.price_label = clean(input.priceLabel);
  if (input.chipExtra !== undefined) row.chip_extra = clean(input.chipExtra);
  if (input.partnerOffer !== undefined) row.partner_offer = clean(input.partnerOffer);
  if (input.address !== undefined) row.address = clean(input.address);
  if (input.imageUrl !== undefined) row.image_url = clean(input.imageUrl);
  if (input.gpsRadiusM !== undefined) row.gps_radius_m = input.gpsRadiusM;
  if (input.isActive !== undefined) row.is_active = input.isActive === true;

  if (input.latitude !== undefined && input.longitude !== undefined) {
    const lat = Number(input.latitude);
    const lon = Number(input.longitude);
    row.latitude = lat;
    row.longitude = lon;
    row.geohash = geohashEncode(lat, lon);   // rule 1 — always, never typed
  }
  row.updated_at = new Date().toISOString();
  return row;
}

export async function createVenue(
  adminEmail: string,
  input: VenueInput
): Promise<Outcome> {
  const bad = validate(input, true);
  if (bad) return { ok: false, status: 400, message: bad };
  if (input.latitude === undefined || input.longitude === undefined) {
    return { ok: false, status: 400, message: "latitude and longitude are required." };
  }

  const row = {
    ...toRow(input),
    // `sport_type` is the Flutter app's axis and is NOT NULL. A new admin row
    // is a golf-adjacent spot in all three categories, so it stays 'golf' and
    // `category` carries the real distinction.
    sport_type: "golf",
    source: "admin",
    is_active: input.isActive ?? true,
    category: input.category ?? "golf",
  };

  if (isMockMode()) {
    const venue = mapVenue({ ...row, id: Math.floor(Math.random() * 1e6) });
    await writeAudit(adminEmail, "venue.create", null, "venues", null, row);
    return { ok: true, message: `Created ${venue.name}.`, venue };
  }

  const res = await getSupabaseAdmin().from("venues").insert(row).select().single();
  if (res.error) return { ok: false, status: 500, message: res.error.message };

  const venue = mapVenue(res.data as Record<string, unknown>);
  await writeAudit(adminEmail, "venue.create", null, "venues", null, res.data);
  return { ok: true, message: `Created ${venue.name} (#${venue.id}).`, venue };
}

export async function updateVenue(
  adminEmail: string,
  id: number,
  input: Partial<VenueInput>
): Promise<Outcome> {
  const bad = validate(input, false);
  if (bad) return { ok: false, status: 400, message: bad };

  const before = await fetchVenue(id);
  if (!before) return { ok: false, status: 404, message: `Venue #${id} not found.` };

  // A partial edit that moves only ONE coordinate would leave the geohash
  // computed from a mixed pair. Fill the missing half from the stored row so
  // the derived value always matches the row that is actually saved.
  const merged: Partial<VenueInput> = { ...input };
  if (merged.latitude !== undefined && merged.longitude === undefined) {
    merged.longitude = before.longitude ?? undefined;
  }
  if (merged.longitude !== undefined && merged.latitude === undefined) {
    merged.latitude = before.latitude ?? undefined;
  }

  const row = toRow(merged);

  if (isMockMode()) {
    const venue = mapVenue({ ...before, ...row, id });
    await writeAudit(adminEmail, "venue.update", null, "venues", before, row);
    return { ok: true, message: `Saved ${venue.name}.`, venue };
  }

  const res = await getSupabaseAdmin()
    .from("venues")
    .update(row)
    .eq("id", id)
    .select()
    .single();
  if (res.error) return { ok: false, status: 500, message: res.error.message };

  const venue = mapVenue(res.data as Record<string, unknown>);
  await writeAudit(adminEmail, "venue.update", null, "venues", before, res.data);
  return { ok: true, message: `Saved ${venue.name} (#${venue.id}).`, venue };
}

/**
 * Deactivate — the panel's "remove". Rule 2 in the header: never a DELETE.
 * Reversible by construction, which is the other half of why it is the only
 * removal offered.
 */
export async function setVenueActive(
  adminEmail: string,
  id: number,
  active: boolean
): Promise<Outcome> {
  return updateVenue(adminEmail, id, { isActive: active });
}
