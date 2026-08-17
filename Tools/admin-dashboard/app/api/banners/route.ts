import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchBanners } from "@/lib/bannerData";
import { createBanner } from "@/lib/bannerMutations";
import type { BannerInput } from "@/lib/types";

export const dynamic = "force-dynamic";

/** GET /api/banners — every row, both placements, active and draft. Admin-only. */
export async function GET() {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }
  try {
    return NextResponse.json(await fetchBanners());
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("GET /api/banners failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}

/** POST /api/banners — create a banner. Admin-only, audited. */
export async function POST(request: Request) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const body = (await request.json().catch(() => null)) as BannerInput | null;
  if (!body || typeof body !== "object") {
    return NextResponse.json({ error: "Invalid body." }, { status: 400 });
  }

  try {
    const outcome = await createBanner(check.email, body);
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("POST /api/banners failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
