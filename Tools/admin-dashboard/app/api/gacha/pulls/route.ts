import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchGachaPulls } from "@/lib/gachaData";

export const dynamic = "force-dynamic";

/**
 * GET /api/gacha/pulls — the pull log (gacha_server_pull §6).
 *
 * Filters: `email` (partial, resolved to user ids server-side because
 * `golfin_gacha_pulls` has no email column and auth.users is not joinable over
 * PostgREST), `banner`, `from` / `to` (ISO), `before` (keyset cursor),
 * `limit` (≤ 200, default 50).
 *
 * Read-only, so there is no `writeAudit` here — there is nothing to audit. The
 * panel's three WRITES (pause, tickets, pity reset) each have their own route.
 */
export async function GET(request: Request) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const params = new URL(request.url).searchParams;
  const limitRaw = params.get("limit");
  const limit = limitRaw ? Number(limitRaw) : undefined;
  if (limit !== undefined && (!Number.isInteger(limit) || limit < 1 || limit > 200)) {
    return NextResponse.json({ error: "limit must be an integer 1–200." }, { status: 400 });
  }

  try {
    return NextResponse.json(
      await fetchGachaPulls({
        email: params.get("email") ?? undefined,
        bannerId: params.get("banner") ?? undefined,
        from: params.get("from") ?? undefined,
        to: params.get("to") ?? undefined,
        before: params.get("before") ?? undefined,
        limit,
      })
    );
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("GET /api/gacha/pulls failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
