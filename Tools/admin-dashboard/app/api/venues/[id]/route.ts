import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { updateVenue } from "@/lib/venueMutations";
import type { VenueInput } from "@/lib/types";

export const dynamic = "force-dynamic";

/**
 * PATCH /api/venues/:id — edit one venue. Admin-only, audited.
 *
 * ⚠️ NO PUBLISH STEP. `/venue/nearby` reads this table per request, so a 200
 * here means the player's NEXT fetch already sees the change. The panel says so
 * in a banner above the table.
 *
 * The request may not set `geohash`: it is recomputed from latitude/longitude
 * inside `updateVenue` on every save (§ B1). A geohash that disagrees with the
 * coordinates makes the row invisible to `/venue/nearby` with no error
 * anywhere, which is exactly the failure two hand-seeded rows are in today.
 */
export async function PATCH(
  request: Request,
  ctx: { params: Promise<{ id: string }> }
) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const { id } = await ctx.params;
  const venueId = Number(id);
  if (!Number.isInteger(venueId)) {
    return NextResponse.json({ error: "id must be an integer." }, { status: 400 });
  }

  const body = (await request.json().catch(() => null)) as (VenueInput & {
    geohash?: unknown;
  }) | null;
  if (!body || typeof body !== "object") {
    return NextResponse.json({ error: "Invalid body." }, { status: 400 });
  }
  if ("geohash" in body) {
    return NextResponse.json(
      { error: "geohash is derived from latitude/longitude and cannot be set." },
      { status: 400 }
    );
  }

  try {
    const outcome = await updateVenue(check.email, venueId, body);
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message, venue: outcome.venue });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`PATCH /api/venues/${id} failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
