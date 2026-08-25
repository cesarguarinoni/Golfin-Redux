import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { setCatalogEnabled } from "@/lib/contentMutations";

export const dynamic = "force-dynamic";

/**
 * POST /api/content/:catalog/enabled `{ enabled }` — the §7.4 kill switch.
 *
 * Disabling makes the catalog vanish from /api/v1/content and drops the
 * top-level `enabled` flag. The game then runs on its bundled CSVs (§2 I1),
 * which is the whole point: one flip, and remote content stops mattering.
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
