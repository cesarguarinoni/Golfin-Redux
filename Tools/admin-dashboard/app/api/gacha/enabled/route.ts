import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { setGachaEnabled } from "@/lib/gachaMutations";

export const dynamic = "force-dynamic";

/**
 * POST /api/gacha/enabled `{ enabled }` — the GACHA pause switch
 * (gacha_server_pull §6).
 *
 * ⚠️ NOT `/api/content/enabled`, which is the GLOBAL content kill switch. That
 * one makes every client drop EVERY catalog's cache and run bundled; this one
 * writes `content_settings.gacha_enabled` and closes the gacha ALONE — the shop,
 * the missions and the mode fees keep working. Two rows in the same table, two
 * routes, deliberately: an operator reaching for "stop the gacha" must not be
 * one click from stopping remote content for the whole game.
 *
 * INSTANT, unlike the content switch: `golfin_gacha_pull` reads the flag per
 * call, so there is no 60 s response cache and no apply-at-next-launch. The
 * panel requires a typed confirmation to pause for exactly that reason.
 *
 * The current state is NOT read here; it comes back with the pull log on
 * `GET /api/gacha/pulls` as `gachaEnabled`, so the panel has one source for it.
 */
export async function POST(request: Request) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const body = (await request.json().catch(() => null)) as { enabled?: unknown } | null;
  if (typeof body?.enabled !== "boolean") {
    return NextResponse.json({ error: "enabled (boolean) is required." }, { status: 400 });
  }

  try {
    const outcome = await setGachaEnabled(check.email, body.enabled);
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("POST /api/gacha/enabled failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
