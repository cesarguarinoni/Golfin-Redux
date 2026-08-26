import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { setCatalogEnabled } from "@/lib/contentMutations";

export const dynamic = "force-dynamic";

/**
 * POST /api/content/:catalog/enabled `{ enabled }` — the PER-CATALOG §7.4 kill switch.
 *
 * Disabling makes THIS catalog vanish from /api/v1/content and names it in the response's
 * top-level `disabled` list. That one catalog falls back to its bundled CSV (§2 I1); no other
 * catalog is touched.
 *
 * ⚠️ IT DOES NOT TOUCH THE TOP-LEVEL `enabled` FLAG. It used to — the endpoint ANDed this column
 * across the REQUESTED catalogs, and the client drops EVERY cache on `enabled:false`, so killing
 * one catalog reverted all seven on every client (content_kill_switch_and_order). The global
 * switch is `POST /api/content/enabled`, with no catalog segment.
 */
export async function POST(request: Request, ctx: { params: Promise<{ catalog: string }> }) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }
  const { catalog } = await ctx.params;

  const body = (await request.json().catch(() => null)) as { enabled?: unknown } | null;
  if (typeof body?.enabled !== "boolean") {
    return NextResponse.json({ error: "enabled (boolean) is required." }, { status: 400 });
  }

  try {
    const outcome = await setCatalogEnabled(check.email, catalog, body.enabled);
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`POST /api/content/${catalog}/enabled failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
