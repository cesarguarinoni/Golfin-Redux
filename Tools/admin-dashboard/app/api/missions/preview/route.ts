import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";

export const dynamic = "force-dynamic";

/**
 * GET /api/missions/preview?days=14 — what the generator WOULD produce.
 *
 * A THIN PROXY, ON PURPOSE. The generator is `services/daily_mission.py` on the
 * API, and there must be exactly ONE of it: a TypeScript port here would be a
 * second implementation of the draw, and the entire value of a deterministic
 * recipe is that the preview, the server and the offline client agree about what
 * a date produces. So this forwards to `GET /api/v1/missions/admin/daily-preview`
 * with the server-to-server admin key (the same `admin_preload_key` gate
 * routers/signups.py and routers/tournaments.py already use) and returns what it
 * says.
 *
 * WHEN IT IS NOT CONFIGURED IT SAYS SO, and the panel keeps working. Preview is
 * one control on a panel whose other three — the calendar, the clear rate,
 * pinning — need only Supabase. A missing env var must degrade that one control,
 * not the page: `{ unavailable: true, reason }` is a 200 the panel renders as an
 * explanation rather than an error nobody can act on.
 *
 * Required env (Cloudflare): PLAYLIFE_API_URL, PLAYLIFE_ADMIN_KEY.
 */
export async function GET(request: Request) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const base = process.env.PLAYLIFE_API_URL;
  const adminKey = process.env.PLAYLIFE_ADMIN_KEY;
  if (!base || !adminKey) {
    return NextResponse.json({
      unavailable: true,
      reason:
        "Preview needs PLAYLIFE_API_URL and PLAYLIFE_ADMIN_KEY set on this deployment. " +
        "The calendar and clear rates work without them. PINNING DOES NOT: the Pin button " +
        "is rendered per preview row, so with no preview there is no future date to pin.",
      data: [],
    });
  }

  const days = Number(new URL(request.url).searchParams.get("days") ?? 14);
  const url =
    `${base.replace(/\/$/, "")}/api/v1/missions/admin/daily-preview` +
    `?days=${Number.isFinite(days) ? Math.max(1, Math.min(60, days)) : 14}` +
    `&admin_key=${encodeURIComponent(adminKey)}`;

  try {
    const res = await fetch(url, { cache: "no-store" });
    const body = (await res.json().catch(() => null)) as { data?: unknown; detail?: string } | null;
    if (!res.ok) {
      return NextResponse.json(
        { error: body?.detail ?? `Preview failed (${res.status})` },
        { status: res.status === 403 ? 502 : 500 }
      );
    }
    return NextResponse.json({ data: body?.data ?? [] });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("GET /api/missions/preview failed:", message);
    return NextResponse.json({ error: message }, { status: 502 });
  }
}
