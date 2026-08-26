import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { setGlobalContentEnabled } from "@/lib/contentMutations";

export const dynamic = "force-dynamic";

/**
 * POST /api/content/enabled `{ enabled }` — the GLOBAL §7.4 kill switch.
 *
 * ⚠️ NOT `/api/content/:catalog/enabled`, which is the PER-CATALOG switch and takes exactly one
 * catalog back to its bundled CSV. This one writes `content_settings.content_enabled`: `false`
 * makes every client ignore the whole content response and drop EVERY catalog's cache, for every
 * player, until it is flipped back.
 *
 * The route sits at `/api/content/enabled` — with no catalog segment — because there is no catalog
 * involved. Next's router cannot confuse the two: `[catalog]` is a dynamic segment and `enabled`
 * here is a literal one at a shallower depth.
 *
 * The current state is NOT read here; it comes back with everything else on `GET /api/content` as
 * `globalEnabled`, so the panel has one source for it.
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
    const outcome = await setGlobalContentEnabled(check.email, body.enabled);
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("POST /api/content/enabled failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
