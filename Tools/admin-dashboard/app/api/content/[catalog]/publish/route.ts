import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { publishCatalog } from "@/lib/contentMutations";

export const dynamic = "force-dynamic";

/**
 * POST /api/content/:catalog/publish — validate (§D1), then run the atomic
 * `content_publish` RPC, then audit with the diff as before/after.
 *
 * A validation failure is a 400 carrying the FULL problem list, and NOTHING is
 * published — not the valid rows, not a subset. Warnings (an rpCost outside the
 * ECONOMY_MASTER band) ride along on the 200 instead.
 *
 * Publishing `characters` also mirrors into `golfin_characters`, the table
 * tournament rarity restrictions read — see lib/contentMutations.ts.
 */
export async function POST(request: Request, ctx: { params: Promise<{ catalog: string }> }) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }
  const { catalog } = await ctx.params;

  const body = (await request.json().catch(() => null)) as { note?: unknown } | null;
  const note = typeof body?.note === "string" ? body.note : undefined;

  try {
    const outcome = await publishCatalog(check.email, catalog, note);
    if (!outcome.ok) {
      return NextResponse.json(
        { error: outcome.message, problems: outcome.problems ?? [] },
        { status: outcome.status }
      );
    }
    return NextResponse.json({
      message: outcome.message,
      version: outcome.version,
      warnings: outcome.problems ?? [],
    });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`POST /api/content/${catalog}/publish failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
