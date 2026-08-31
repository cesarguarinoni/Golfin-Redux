import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchGachaOdds, type OddsSample } from "@/lib/gachaData";

export const dynamic = "force-dynamic";

/**
 * GET /api/gacha/odds?banner=<id>&sample=100|1000|all — the odds audit (§6).
 *
 * "Did the server pay out what the rate table published?" — over UNFORCED slots
 * only. Pity and x10-guarantee slots are drawn from a renormalised subset of the
 * ladder and are supposed to skew, so counting them would flag exactly the
 * banners that are working. `lib/gachaAudit.ts` does the arithmetic and is what
 * the vitest suite covers.
 *
 * Scoped to a BANNER, not a pool: two banners can share a pool while promising
 * different pity, so auditing "the pool" would mix two populations with
 * different forced-slot rates.
 */
export async function GET(request: Request) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const params = new URL(request.url).searchParams;
  const banner = (params.get("banner") ?? "").trim();
  if (!banner) {
    return NextResponse.json({ error: "banner is required." }, { status: 400 });
  }

  const raw = params.get("sample") ?? "1000";
  let sample: OddsSample;
  if (raw === "all") sample = null;
  else if (raw === "100") sample = 100;
  else if (raw === "1000") sample = 1000;
  else {
    return NextResponse.json(
      { error: "sample must be 100, 1000 or all." },
      { status: 400 }
    );
  }

  try {
    return NextResponse.json(await fetchGachaOdds(banner, sample));
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("GET /api/gacha/odds failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
