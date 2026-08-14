import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { uploadTournamentArt } from "@/lib/tournamentMutations";

export const dynamic = "force-dynamic";

/**
 * POST /api/tournaments/art — multipart upload of per-tournament card art
 * into the public `tournament-art` bucket (SPEC §5c.2/§5c.4).
 * Returns { url } for the editor to store in banner_url. Admin-only, audited.
 */
export async function POST(request: Request) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  let form: FormData;
  try {
    form = await request.formData();
  } catch {
    return NextResponse.json({ error: "Expected multipart/form-data." }, { status: 400 });
  }

  const file = form.get("file");
  const slug = form.get("slug");
  if (!(file instanceof File)) {
    return NextResponse.json({ error: "file is required." }, { status: 400 });
  }
  if (typeof slug !== "string") {
    return NextResponse.json({ error: "slug is required." }, { status: 400 });
  }

  try {
    const outcome = await uploadTournamentArt(check.email, slug, file);
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message, url: outcome.url });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("POST /api/tournaments/art failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
