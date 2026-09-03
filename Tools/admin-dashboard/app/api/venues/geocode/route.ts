import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { coordsFromText, geohashEncode } from "@/lib/geohash";
import type { GeocodeResult } from "@/lib/types";

export const dynamic = "force-dynamic";

/**
 * POST /api/venues/geocode — "Find on map" for the editor drawer.
 *
 * DEVIATION FROM THE SPEC, and why. § B1 routes this through the playlife API's
 * `/venue/geocode`. That endpoint exists and is what the Unity client would
 * use, but this dashboard has NO channel to it: it talks only to Supabase with
 * the service key and holds no PLAYLIFE bearer token, and `/venue/geocode` is
 * `Depends(get_current_user)`. Minting a token here would mean giving the
 * dashboard a player identity, which is a worse thing to own than a regex.
 *
 * So the two shapes an operator actually pastes — a Google Maps URL and a bare
 * `lat,lon` — are resolved LOCALLY by `coordsFromText`, the same two shapes and
 * the same order as `venue.py::_coords_from_text`. A free-text place NAME needs
 * a Places call and therefore a key: when `GOOGLE_PLACES_API_KEY` is set in the
 * dashboard's env this route makes it directly; when it is not, it says so
 * plainly rather than failing silently, and the operator pastes a link instead.
 *
 * The geohash comes back with the coordinates so the drawer can SHOW what will
 * be written — but it is recomputed again in `venueMutations.toRow` on save,
 * because a value that travelled through a form is a value that could have been
 * edited.
 */
export async function POST(request: Request) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const body = (await request.json().catch(() => null)) as {
    query?: string;
    latitude?: number;
    longitude?: number;
  } | null;
  if (!body) {
    return NextResponse.json({ error: "Invalid body." }, { status: 400 });
  }

  let lat = typeof body.latitude === "number" ? body.latitude : null;
  let lon = typeof body.longitude === "number" ? body.longitude : null;
  let name: string | null = null;
  let address: string | null = null;

  const query = (body.query ?? "").trim();

  if ((lat === null || lon === null) && query) {
    const parsed = coordsFromText(query);
    if (parsed) {
      lat = parsed.lat;
      lon = parsed.lon;
    }
  }

  if ((lat === null || lon === null) && query) {
    const key = process.env.GOOGLE_PLACES_API_KEY;
    if (!key) {
      return NextResponse.json(
        {
          error:
            "Paste a Google Maps link or a `lat, lon` pair. " +
            "Searching by place name needs GOOGLE_PLACES_API_KEY in the dashboard env.",
        },
        { status: 400 }
      );
    }
    try {
      const res = await fetch("https://places.googleapis.com/v1/places:searchText", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-Goog-Api-Key": key,
          "X-Goog-FieldMask":
            "places.id,places.displayName,places.location,places.formattedAddress",
        },
        body: JSON.stringify({ textQuery: query, maxResultCount: 1, languageCode: "ja" }),
      });
      if (!res.ok) {
        return NextResponse.json(
          { error: `Places API error ${res.status}: ${(await res.text()).slice(0, 200)}` },
          { status: 502 }
        );
      }
      const json = (await res.json()) as {
        places?: Array<{
          displayName?: { text?: string };
          formattedAddress?: string;
          location?: { latitude?: number; longitude?: number };
        }>;
      };
      const place = json.places?.[0];
      if (!place) {
        return NextResponse.json({ data: null, message: "No place matched that query." });
      }
      lat = place.location?.latitude ?? null;
      lon = place.location?.longitude ?? null;
      name = place.displayName?.text ?? null;
      address = place.formattedAddress ?? null;
    } catch (err) {
      const message = err instanceof Error ? err.message : "Unknown error";
      return NextResponse.json({ error: message }, { status: 502 });
    }
  }

  if (lat === null || lon === null) {
    return NextResponse.json(
      { error: "Provide a Google Maps link, a `lat, lon` pair, or a place name." },
      { status: 400 }
    );
  }

  const data: GeocodeResult = {
    latitude: lat,
    longitude: lon,
    geohash: geohashEncode(lat, lon),
    name,
    address,
  };
  return NextResponse.json({ data });
}
