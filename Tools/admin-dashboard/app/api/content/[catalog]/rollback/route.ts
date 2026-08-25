import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { rollbackCatalog } from "@/lib/contentMutations";

export const dynamic = "force-dynamic";

/**
 * POST /api/content/:catalog/rollback `{ toVersion }` — restore a snapshot.
 *
 * It comes back as a NEW, HIGHER version. Rollback never decrements
 * `published_version`; see lib/contentMutations.ts for why that matters to a
 * client that already fetched the bad version.
 */
export async function POST(request: Request, ctx: { params: Promise<{ catalog: string }> }) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }
  const { catalog } = await ctx.params;

  const body = (await request.json().catch(() => null)) as { toVersion?: unknown } | null;
  const toVersion = Number(body?.toVersion);
  if (!Number.isFinite(toVersion)) {
    return NextResponse.json({ error: "toVersion (number) is required." }, { status: 400 });
  }

  try {
    const outcome = await rollbackCatalog(check.email, catalog, toVersion);
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message, version: outcome.version });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`POST /api/content/${catalog}/rollback failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
