import "server-only";
import { mockDb } from "./mockStore";
import { isMockMode } from "./mode";
import { getSupabaseAdmin } from "./supabaseAdmin";
import { ID_COLUMN } from "./contentValidate";
import type {
  ContentCatalogSummary,
  ContentCatalogsResponse,
  ContentVersionSummary,
  ContentVersionsResponse,
  ContentDiffEntry,
  ContentDiffResponse,
  ContentFieldDiff,
  ContentRowsResponse,
  ContentStoredRow,
} from "./types";

/**
 * Read side of the content catalogs (SPEC content_catalog §D).
 * Branches mock ↔ live exactly like lib/noticeData.ts.
 *
 * EVERY read is bounded. Clubs is 799 rows today and the catalog is the thing
 * that grows, so `fetchDraftRows` pages server-side (§D "server-side pagination
 * is not optional") and the diff reads only the columns it compares.
 */

type Row = Record<string, unknown>;

/** PostgREST's default max-rows. Whole-catalog reads page rather than assume. */
const PAGE = 1000;

export const DEFAULT_LIMIT = 50;
export const MAX_LIMIT = 200;

/** Column `q` searches besides row_id — the human-readable one, per catalog. */
const SEARCH_COLUMN: Record<string, string> = {
  clubs: "name",
  characters: "name",
  items: "name",
  bags: "name",
  balls: "name",
  texts: "English",
  shop_catalog: "refId",
};

function mapRow(catalog: string, r: Row): ContentStoredRow {
  const data = (r.data ?? {}) as Record<string, unknown>;
  const asStrings: Record<string, string> = {};
  for (const [k, v] of Object.entries(data)) {
    asStrings[k] = v === null || v === undefined ? "" : String(v);
  }
  return {
    catalog,
    rowId: String(r.row_id ?? ""),
    data: asStrings,
    minBuild: Number(r.min_build ?? 0),
    isActive: r.is_active !== false,
    version: r.version === undefined ? undefined : Number(r.version),
    updatedAt: (r.updated_at as string) ?? null,
    updatedBy: (r.updated_by as string) ?? null,
  };
}

/** Whole-table read of one catalog, paged. Used by the diff and by publish. */
export async function fetchAllRows(
  table: "content_rows" | "content_drafts",
  catalog: string
): Promise<ContentStoredRow[]> {
  if (isMockMode()) {
    const store = table === "content_rows" ? mockDb().contentPublished : mockDb().contentDrafts;
    return store.filter((r) => r.catalog === catalog);
  }

  const supabase = getSupabaseAdmin();
  const out: ContentStoredRow[] = [];
  for (let offset = 0; ; offset += PAGE) {
    // The select list differs per table, which defeats supabase-js's literal-type
    // inference; the shape is normalised by mapRow() immediately below.
    const columns =
      table === "content_rows"
        ? "row_id, data, min_build, is_active, version"
        : "row_id, data, min_build, is_active, updated_by";
    const res = await supabase
      .from(table)
      .select(columns)
      .eq("catalog", catalog)
      .order("row_id")
      .range(offset, offset + PAGE - 1);
    if (res.error) throw new Error(`${table} query failed: ${res.error.message}`);
    const batch = (res.data as unknown as Row[]) ?? [];
    out.push(...batch.map((r) => mapRow(catalog, r)));
    if (batch.length < PAGE) return out;
  }
}

/** Number of dirty draft rows — what a publish would actually change. */
function dirtyCount(published: ContentStoredRow[], drafts: ContentStoredRow[]): number {
  const byId = new Map(published.map((r) => [r.rowId, r]));
  return drafts.filter((d) => {
    const p = byId.get(d.rowId);
    if (!p) return true;
    return (
      JSON.stringify(p.data) !== JSON.stringify(d.data) ||
      p.minBuild !== d.minBuild ||
      p.isActive !== d.isActive
    );
  }).length;
}

export async function fetchCatalogs(): Promise<ContentCatalogsResponse> {
  if (isMockMode()) {
    // DERIVE the counts from the mock rows rather than trusting the numbers
    // stored on the fixture (content_admin_panels, 2026-08-25).
    //
    // `upsertDraftRow` mutates `contentDrafts` but never refreshed the summary,
    // so after editing a draft in mock mode `dirtyCount` stayed at whatever the
    // fixture said — 0 — while the diff correctly showed a changed row. That is
    // the one number an operator reads to decide whether a publish is needed,
    // and a stale one is exactly the "mock fixtures read as fact" failure
    // ADMIN_DASHBOARD_OPS.md §3.5 is about.
    //
    // Deriving is also what the live branch below does, so the two modes now
    // agree by construction instead of by every mutation remembering to keep a
    // cached count in step. `publishedVersion` / `isEnabled` still come from the
    // fixture — publish, rollback and the kill switch own those and do update
    // them. LIVE BEHAVIOUR IS UNCHANGED: this whole block is mock-only.
    const store = mockDb();
    return {
      catalogs: store.contentCatalogs.map((meta) => {
        const published = store.contentPublished.filter((r) => r.catalog === meta.name);
        const drafts = store.contentDrafts.filter((r) => r.catalog === meta.name);
        return {
          ...meta,
          publishedCount: published.length,
          draftCount: drafts.length,
          dirtyCount: dirtyCount(published, drafts),
        };
      }),
      mock: true,
    };
  }

  const supabase = getSupabaseAdmin();
  const res = await supabase
    .from("content_catalogs")
    .select("name, published_version, is_enabled")
    .order("name");
  if (res.error) throw new Error(`content_catalogs query failed: ${res.error.message}`);

  const catalogs: ContentCatalogSummary[] = [];
  for (const r of (res.data as Row[]) ?? []) {
    const name = String(r.name);
    // Counts come from head-only count queries; the dirty count needs the rows
    // themselves, and the catalogs are small enough (max 799) that one paged
    // read per catalog on an admin list page is the right trade for accuracy.
    const [published, drafts] = await Promise.all([
      fetchAllRows("content_rows", name),
      fetchAllRows("content_drafts", name),
    ]);
    catalogs.push({
      name,
      publishedVersion: Number(r.published_version ?? 0),
      isEnabled: r.is_enabled !== false,
      publishedCount: published.length,
      draftCount: drafts.length,
      dirtyCount: dirtyCount(published, drafts),
    });
  }
  return { catalogs, mock: false };
}

/**
 * `data` fields a catalog may be filtered on (content_panels_gaps §1).
 *
 * An ALLOW-LIST, not free-form: the value is interpolated into a PostgREST
 * filter, so accepting an arbitrary field name from the query string would let
 * a caller aim the filter anywhere in the JSONB document. These are the columns
 * the panels actually facet on, and every one of them is a plain scalar.
 */
const FILTERABLE: Record<string, string[]> = {
  clubs: ["brand", "type", "rarity"],
  characters: ["rarity"],
  items: ["category", "rarity"],
  bags: ["rarity"],
  balls: ["brand"],
  texts: [],
  shop_catalog: ["category"],
};

export function filterableFields(catalog: string): string[] {
  return FILTERABLE[catalog] ?? [];
}

/** Drop anything not on the allow-list, and any empty value. */
function sanitizeFilters(catalog: string, filters: Record<string, string>): Array<[string, string]> {
  const allowed = new Set(filterableFields(catalog));
  return Object.entries(filters)
    .map(([field, value]) => [field, (value ?? "").trim()] as [string, string])
    .filter(([field, value]) => value !== "" && allowed.has(field));
}

export async function fetchDraftRows(
  catalog: string,
  opts: { page?: number; limit?: number; q?: string; filters?: Record<string, string> } = {}
): Promise<ContentRowsResponse> {
  const page = Math.max(1, Math.floor(opts.page ?? 1));
  const limit = Math.min(MAX_LIMIT, Math.max(1, Math.floor(opts.limit ?? DEFAULT_LIMIT)));
  const q = (opts.q ?? "").trim();
  const filters = sanitizeFilters(catalog, opts.filters ?? {});
  const from = (page - 1) * limit;

  if (isMockMode()) {
    const all = mockDb()
      .contentDrafts.filter((r) => r.catalog === catalog)
      .filter((r) => !q || JSON.stringify(r).toLowerCase().includes(q.toLowerCase()))
      // Same semantics as the live branch below: exact match, AND-ed.
      .filter((r) => filters.every(([field, value]) => (r.data[field] ?? "") === value))
      .sort((a, b) => a.rowId.localeCompare(b.rowId));
    const rows = all.slice(from, from + limit);
    return { catalog, page, limit, total: all.length, columns: columnsOf(rows), rows, mock: true };
  }

  let query = getSupabaseAdmin()
    .from("content_drafts")
    .select("row_id, data, min_build, is_active, updated_by", { count: "exact" })
    .eq("catalog", catalog);

  if (q) {
    // row_id OR the catalog's human-readable column. `data->>col` is indexable
    // and, unlike a `data::text` scan, does not match on column NAMES.
    const escaped = q.replace(/[%,()]/g, "");
    const column = SEARCH_COLUMN[catalog];
    const clauses = [`row_id.ilike.*${escaped}*`];
    if (column) clauses.push(`data->>${column}.ilike.*${escaped}*`);
    query = query.or(clauses.join(","));
  }

  // Facet filters are EXACT and AND-ed with each other and with `q`, which is
  // what made the previous scalar-`q` workaround unnecessary: brand=BogeyB AND
  // rarity=Common is now one query, and `total` is the count of the FILTERED
  // set, so pagination is over the real result rather than over the page.
  for (const [field, value] of filters) {
    query = query.eq(`data->>${field}`, value);
  }

  const res = await query.order("row_id").range(from, from + limit - 1);
  if (res.error) throw new Error(`content_drafts query failed: ${res.error.message}`);

  const rows = ((res.data as Row[]) ?? []).map((r) => mapRow(catalog, r));
  return { catalog, page, limit, total: res.count ?? rows.length, columns: columnsOf(rows), rows, mock: false };
}

/**
 * Distinct values of each filterable field, for the facet dropdowns (§1).
 *
 * SERVER-DERIVED, deliberately: the old panel built its options from whichever
 * 50 rows happened to be on screen, so a brand that appeared only on page 9 was
 * unselectable. Reading the whole column is what makes "adding a brand in
 * drafts makes it appear" true without a deploy.
 *
 * One column of one catalog — 799 strings at the current worst case — and it is
 * fetched once per panel load, not per keystroke.
 */
export async function fetchFacetValues(
  catalog: string,
  fields?: string[]
): Promise<Record<string, string[]>> {
  const wanted = (fields?.length ? fields : filterableFields(catalog)).filter((f) =>
    filterableFields(catalog).includes(f)
  );
  const out: Record<string, string[]> = {};
  if (wanted.length === 0) return out;

  const rows = isMockMode()
    ? mockDb().contentDrafts.filter((r) => r.catalog === catalog)
    : await fetchAllRows("content_drafts", catalog);

  for (const field of wanted) {
    const seen = new Set<string>();
    for (const row of rows) {
      const value = (row.data[field] ?? "").trim();
      if (value) seen.add(value);
    }
    out[field] = [...seen].sort((a, b) => a.localeCompare(b));
  }
  return out;
}

function columnsOf(rows: ContentStoredRow[]): string[] {
  const seen: string[] = [];
  for (const row of rows) {
    for (const key of Object.keys(row.data)) {
      if (!seen.includes(key)) seen.push(key);
    }
  }
  return seen;
}

/**
 * Drafts vs published, field-level (§D). `deactivated` is called out separately
 * from `changed` because it is the one edit that removes content from a shop or
 * a gacha pool while every player who owns one keeps it (§2 I6) — an admin has
 * to see it as its own category, not as one field among twenty.
 */
export async function fetchDiff(catalog: string): Promise<ContentDiffResponse> {
  const [published, drafts] = await Promise.all([
    fetchAllRows("content_rows", catalog),
    fetchAllRows("content_drafts", catalog),
  ]);

  let publishedVersion = 0;
  if (isMockMode()) {
    publishedVersion = mockDb().contentCatalogs.find((c) => c.name === catalog)?.publishedVersion ?? 0;
  } else {
    const res = await getSupabaseAdmin()
      .from("content_catalogs")
      .select("published_version")
      .eq("name", catalog)
      .maybeSingle();
    if (res.error) throw new Error(`content_catalogs query failed: ${res.error.message}`);
    publishedVersion = Number((res.data as Row | null)?.published_version ?? 0);
  }

  const byId = new Map(published.map((r) => [r.rowId, r]));
  const entries: ContentDiffEntry[] = [];
  const counts = { added: 0, changed: 0, deactivated: 0, reactivated: 0 };

  for (const draft of drafts.sort((a, b) => a.rowId.localeCompare(b.rowId))) {
    const prev = byId.get(draft.rowId);
    if (!prev) {
      counts.added += 1;
      entries.push({
        rowId: draft.rowId,
        kind: "added",
        fields: Object.entries(draft.data).map(([column, after]) => ({ column, before: null, after })),
      });
      continue;
    }

    const fields: ContentFieldDiff[] = [];
    for (const column of new Set([...Object.keys(prev.data), ...Object.keys(draft.data)])) {
      const before = prev.data[column] ?? null;
      const after = draft.data[column] ?? null;
      if (before !== after) fields.push({ column, before, after });
    }
    if (prev.minBuild !== draft.minBuild) {
      fields.push({ column: "min_build", before: String(prev.minBuild), after: String(draft.minBuild) });
    }

    if (prev.isActive !== draft.isActive) {
      const kind = draft.isActive ? "reactivated" : "deactivated";
      counts[kind] += 1;
      fields.push({ column: "is_active", before: String(prev.isActive), after: String(draft.isActive) });
      entries.push({ rowId: draft.rowId, kind, fields });
      continue;
    }
    if (fields.length > 0) {
      counts.changed += 1;
      entries.push({ rowId: draft.rowId, kind: "changed", fields });
    }
  }

  return { catalog, publishedVersion, counts, entries, mock: isMockMode() };
}

// ---------------------------------------------------------------------------
// Version history (content_panels_gaps §2)
// ---------------------------------------------------------------------------

/**
 * Every snapshot of one catalog, newest first.
 *
 * WHY THIS EXISTS. The panels reconstructed history from `admin_audit_log`,
 * which keeps the 200 most recent admin actions ACROSS ALL PANELS and never saw
 * the v1 seed at all — that was applied by SQL, before the dashboard existed.
 * Rollback is the plan's §7.3 answer to "an update broke installed games", and
 * a rollback target list that silently loses its tail is a safety rail that
 * quietly stops reaching. `content_versions` has held every snapshot since
 * Phase 0 (written inside `content_publish`); nothing read it until now.
 *
 * The audit log keeps its job — WHO did WHAT — and stops being asked to answer
 * "what versions exist", which it was never able to.
 *
 * `row_count` comes from the snapshot's own length rather than a second query:
 * the snapshot IS the version, so counting it is free and cannot disagree.
 */
export async function fetchVersions(
  catalog: string,
  opts: { page?: number; limit?: number } = {}
): Promise<ContentVersionsResponse> {
  const page = Math.max(1, Math.floor(opts.page ?? 1));
  const limit = Math.min(MAX_LIMIT, Math.max(1, Math.floor(opts.limit ?? DEFAULT_LIMIT)));
  const from = (page - 1) * limit;

  if (isMockMode()) {
    const all = mockDb().contentVersions.filter((v) => v.catalog === catalog);
    all.sort((a, b) => b.version - a.version);
    return {
      catalog,
      page,
      limit,
      total: all.length,
      versions: all.slice(from, from + limit),
      mock: true,
    };
  }

  const res = await getSupabaseAdmin()
    .from("content_versions")
    // `snapshot` is the whole catalog at that version and can be ~1 MB for
    // clubs, so it is NOT selected — only its length is wanted, and PostgREST
    // can compute that server-side with a computed column alias.
    .select("version, published_by, published_at, note, snapshot", { count: "exact" })
    .eq("catalog", catalog)
    .order("version", { ascending: false })
    .range(from, from + limit - 1);
  if (res.error) throw new Error(`content_versions query failed: ${res.error.message}`);

  const versions: ContentVersionSummary[] = ((res.data as Row[]) ?? []).map((r) => ({
    catalog,
    version: Number(r.version ?? 0),
    publishedBy: (r.published_by as string) ?? null,
    publishedAt: (r.published_at as string) ?? null,
    note: (r.note as string) ?? null,
    rowCount: Array.isArray(r.snapshot) ? (r.snapshot as unknown[]).length : 0,
  }));

  return { catalog, page, limit, total: res.count ?? versions.length, versions, mock: false };
}

/** The catalogs a `shop_catalog` publish has to resolve refIds against. */
export const REFERENCED_CATALOGS = ["clubs", "balls", "items", "bags", "characters"];

export { ID_COLUMN };
