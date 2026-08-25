import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchDraftRows } from "@/lib/contentData";
import { upsertDraftRow } from "@/lib/contentMutations";
import type { ContentRowInput } from "@/lib/types";

export const dynamic = "force-dynamic";

/**
 * GET /api/content/:catalog/rows?page=&limit=&q= — one page of DRAFT rows.
 *
 * Pagination is server-side and not optional: clubs is 799 rows and growing, and
 * the point of `content_rows` being JSONB is that catalogs get wider too. `q`
 * matches row_id or the catalog's human-readable column (lib/contentData.ts).
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
      await fetchDraftRows(catalog, {
        page: Number(url.searchParams.get("page") ?? 1),
        limit: Number(url.searchParams.get("limit") ?? 50),
        q: url.searchParams.get("q") ?? "",
      })
    );
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`GET /api/content/${catalog}/rows failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}

/**
 * PUT /api/content/:catalog/rows — upsert ONE draft row. Audited.
 *
 * Drafts are never served to the game, so this deliberately does not validate:
 * publish is the gate (§D1), and rejecting a half-typed row would make an
 * editor unusable.
 */
export async function PUT(request: Request, ctx: { params: Promise<{ catalog: string }> }) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }
  const { catalog } = await ctx.params;

  const body = (await request.json().catch(() => null)) as ContentRowInput | null;
  if (!body || typeof body !== "object") {
    return NextResponse.json({ error: "Invalid body." }, { status: 400 });
  }

  try {
    const outcome = await upsertDraftRow(check.email, catalog, body);
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`PUT /api/content/${catalog}/rows failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
