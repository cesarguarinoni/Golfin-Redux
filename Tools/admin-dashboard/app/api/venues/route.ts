import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchVenueSources, fetchVenues, isVenueCategory } from "@/lib/venueData";
import { createVenue } from "@/lib/venueMutations";
import type { VenueFilters, VenueInput } from "@/lib/types";

export const dynamic = "force-dynamic";

/** GET /api/venues — a filtered page of `venues`. Admin-only. */
export async function GET(request: Request) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const url = new URL(request.url);
  const tri = (key: string): boolean | undefined => {
    const v = url.searchParams.get(key);
    return v === "true" ? true : v === "false" ? false : undefined;
  };
  const category = url.searchParams.get("category");

  const filters: VenueFilters = {
    ...(isVenueCategory(category) ? { category } : {}),
    ...(tri("partner") !== undefined ? { partner: tri("partner") } : {}),
    ...(tri("active") !== undefined ? { active: tri("active") } : {}),
    ...(url.searchParams.get("source") ? { source: url.searchParams.get("source")! } : {}),
    ...(url.searchParams.get("search") ? { search: url.searchParams.get("search")! } : {}),
  };

  try {
    const body = await fetchVenues(filters);
    return NextResponse.json({ ...body, sources: await fetchVenueSources() });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("GET /api/venues failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}

/**
 * POST /api/venues — create a partner row. Admin-only, audited.
 *
 * There is no DELETE sibling, and that is the design (gps_checkin § B1):
 * `activities.venue_id` is a foreign key, so removing a venue somebody checked
 * into either fails or orphans their history. PATCH `{isActive:false}` is the
 * removal, and it is reversible.
 */
export async function POST(request: Request) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const body = (await request.json().catch(() => null)) as VenueInput | null;
  if (!body || typeof body !== "object") {
    return NextResponse.json({ error: "Invalid body." }, { status: 400 });
  }

  try {
    const outcome = await createVenue(check.email, body);
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message, venue: outcome.venue });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("POST /api/venues failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
