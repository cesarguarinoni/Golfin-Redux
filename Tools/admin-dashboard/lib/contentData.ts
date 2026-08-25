import "server-only";
import { mockDb } from "./mockStore";
import { isMockMode } from "./mode";
import { getSupabaseAdmin } from "./supabaseAdmin";
import { ID_COLUMN } from "./contentValidate";
import type {
  ContentCatalogSummary,
  ContentCatalogsResponse,
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

export async function fetchDraftRows(
  catalog: string,
  opts: { page?: number; limit?: number; q?: string } = {}
): Promise<ContentRowsResponse> {
  const page = Math.max(1, Math.floor(opts.page ?? 1));
  const limit = Math.min(MAX_LIMIT, Math.max(1, Math.floor(opts.limit ?? DEFAULT_LIMIT)));
  const q = (opts.q ?? "").trim();
  const from = (page - 1) * limit;

  if (isMockMode()) {
    const all = mockDb()
      .contentDrafts.filter((r) => r.catalog === catalog)
      .filter((r) => !q || JSON.stringify(r).toLowerCase().includes(q.toLowerCase()))
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

  const res = await query.order("row_id").range(from, from + limit - 1);
  if (res.error) throw new Error(`content_drafts query failed: ${res.error.message}`);

  const rows = ((res.data as Row[]) ?? []).map((r) => mapRow(catalog, r));
  return { catalog, page, limit, total: res.count ?? rows.length, columns: columnsOf(rows), rows, mock: false };
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

/** The catalogs a `shop_catalog` publish has to resolve refIds against. */
export const REFERENCED_CATALOGS = ["clubs", "balls", "items", "bags", "characters"];

export { ID_COLUMN };
