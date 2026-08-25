import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchVersions } from "@/lib/contentData";

export const dynamic = "force-dynamic";

/**
 * GET /api/content/:catalog/versions?page=&limit= — every published snapshot,
 * newest first (content_panels_gaps §2).
 *
 * This is the rollback target list, and it reads `content_versions` — the table
 * `content_publish` has written on every publish since Phase 0 and which nothing
 * had ever read. The panels previously reconstructed history from
 * `admin_audit_log`, which keeps the 200 most recent actions across ALL panels
 * and never saw the SQL-seeded v1 at all. Rollback is the plan's §7.3 answer to
 * "an update broke installed games"; a target list that loses its tail is a
 * safety rail that quietly stops reaching, so it now comes from the table that
 * actually holds every version.
 *
 * v1 is therefore always reachable. Paginated because a busy catalog will
 * accumulate versions indefinitely and the list must not become one giant read.
 *
 * Admin-only like every other content route. Read-only, so no audit write —
 * `lib/audit.ts` records mutations, and listing versions is not one.
 */
export async function GET(request: Request, ctx: { params: Promise<{ catalog: string }> }) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }
  const { catalog } = await ctx.params;
  const url = new URL(request.url);

  try {
    return NextResponse.json(
      await fetchVersions(catalog, {
        page: Number(url.searchParams.get("page") ?? 1),
        limit: Number(url.searchParams.get("limit") ?? 50),
      })
    );
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`GET /api/content/${catalog}/versions failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
