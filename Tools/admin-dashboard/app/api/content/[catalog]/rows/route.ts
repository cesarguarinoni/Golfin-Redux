import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchDraftRows, fetchFacetValues, filterableFields } from "@/lib/contentData";
import { upsertDraftRow } from "@/lib/contentMutations";
import type { ContentRowInput } from "@/lib/types";

export const dynamic = "force-dynamic";

/**
 * GET /api/content/:catalog/rows?page=&limit=&q=&<field>=&facets=1 — one page
 * of DRAFT rows.
 *
 * Pagination is server-side and not optional: clubs is 799 rows and growing, and
 * the point of `content_rows` being JSONB is that catalogs get wider too. `q`
 * matches row_id or the catalog's human-readable column (lib/contentData.ts).
 *
 * FACET FILTERS (content_panels_gaps §1). Any field on that catalog's allow-list
 * — `filterableFields()`, e.g. clubs: brand / type / rarity — may be passed as
 * its own query parameter for an EXACT match. They are AND-ed with each other
 * and with `q`, and `total` counts the filtered set, so paging is over the real
 * result. Unknown field names are ignored rather than 400ing: the allow-list is
 * a security boundary (the value reaches a PostgREST filter), and a client from
 * a newer build asking for a facet this server does not have must degrade.
 *
 * `facets=1` additionally returns the DISTINCT values of every filterable field,
 * read from the whole catalog rather than from the returned page — a brand that
 * only appears on page 9 has to be selectable from page 1.
 */
export async function GET(request: Request, ctx: { params: Promise<{ catalog: string }> }) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }
  const { catalog } = await ctx.params;
  const url = new URL(request.url);

  const filters: Record<string, string> = {};
  for (const field of filterableFields(catalog)) {
    const value = url.searchParams.get(field);
    if (value) filters[field] = value;
  }

  try {
    const rows = await fetchDraftRows(catalog, {
      page: Number(url.searchParams.get("page") ?? 1),
      limit: Number(url.searchParams.get("limit") ?? 50),
      q: url.searchParams.get("q") ?? "",
      filters,
    });
    // Only when asked: it reads the whole column, so a panel fetches it once on
    // mount rather than on every page change.
    const facetValues =
      url.searchParams.get("facets") === "1" ? await fetchFacetValues(catalog) : undefined;
    return NextResponse.json(facetValues ? { ...rows, facetValues } : rows);
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
