import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchRewardActions } from "@/lib/rewardsData";

export const dynamic = "force-dynamic";

/** GET /api/rewards — the live `game_point_actions` catalog. Admin-only. */
export async function GET() {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }
  try {
    return NextResponse.json(await fetchRewardActions());
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("GET /api/rewards failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
