"use client";

/**
 * Browser-side wrappers over the content routes.
 *
 * This module is deliberately thin and deliberately closed: every panel goes
 * through it, and it can only reach endpoints that were already deployed by
 * `content_catalog` Stage D. If a panel wants something that is not in here,
 * that is a finding for the report — not a new route (SPEC §Out of scope).
 *
 *   GET  /api/content                      catalogs + versions + dirty counts
 *   GET  /api/content/:catalog/rows        one server page of DRAFT rows
 *   PUT  /api/content/:catalog/rows        upsert one draft row
 *   GET  /api/content/:catalog/diff        drafts vs published, field level
 *   POST /api/content/:catalog/publish     validate → publish → audit
 *   POST /api/content/:catalog/rollback    republish a snapshot, FORWARD
 *   POST /api/content/:catalog/enabled     the PER-CATALOG kill switch
 *   POST /api/content/enabled              the GLOBAL kill switch (content_cleanup_quick item 2)
 *   GET  /api/content/:catalog/versions    every snapshot — the rollback targets
 */

import type { ContentProblem } from "@/lib/contentValidate";
import type {
  ContentCatalogsResponse,
  ContentDiffResponse,
  ContentRowInput,
  ContentRowsResponse,
  ContentVersionsResponse,
} from "@/lib/types";

/** Every failure reaches the UI as a thrown Error carrying the server's text. */
async function call<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, init);
  const body = (await res.json().catch(() => null)) as (T & { error?: string }) | null;
  if (!res.ok) {
    const err = new Error(body?.error ?? `Request failed (${res.status})`) as Error & {
      problems?: ContentProblem[];
      status?: number;
    };
    err.problems = (body as { problems?: ContentProblem[] } | null)?.problems ?? [];
    err.status = res.status;
    throw err;
  }
  return body as T;
}

export function fetchCatalogs(): Promise<ContentCatalogsResponse> {
  return call<ContentCatalogsResponse>("/api/content");
}

export function fetchRows(
  catalog: string,
  opts: {
    page?: number;
    limit?: number;
    q?: string;
    /** Exact-match facet filters, AND-ed server-side (§1). */
    filters?: Record<string, string>;
    /** Ask for the catalog-wide distinct facet values alongside the page. */
    withFacets?: boolean;
  } = {}
): Promise<ContentRowsResponse> {
  const params = new URLSearchParams();
  if (opts.page) params.set("page", String(opts.page));
  if (opts.limit) params.set("limit", String(opts.limit));
  if (opts.q) params.set("q", opts.q);
  for (const [field, value] of Object.entries(opts.filters ?? {})) {
    if (value) params.set(field, value);
  }
  if (opts.withFacets) params.set("facets", "1");
  const qs = params.toString();
  return call<ContentRowsResponse>(`/api/content/${catalog}/rows${qs ? `?${qs}` : ""}`);
}

export function saveRow(catalog: string, row: ContentRowInput): Promise<{ message: string }> {
  return call<{ message: string }>(`/api/content/${catalog}/rows`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(row),
  });
}

export function fetchDiff(catalog: string): Promise<ContentDiffResponse> {
  return call<ContentDiffResponse>(`/api/content/${catalog}/diff`);
}

export function publishCatalog(
  catalog: string,
  note?: string
): Promise<{ message: string; version: number; warnings: ContentProblem[] }> {
  return call(`/api/content/${catalog}/publish`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ note: note || undefined }),
  });
}

export function rollbackCatalog(
  catalog: string,
  toVersion: number
): Promise<{ message: string; version: number }> {
  return call(`/api/content/${catalog}/rollback`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ toVersion }),
  });
}

/** ONE catalog back to (or off) its bundled CSV. See `setGlobalContentEnabled` for the other one. */
export function setCatalogEnabled(catalog: string, enabled: boolean): Promise<{ message: string }> {
  return call(`/api/content/${catalog}/enabled`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ enabled }),
  });
}

/**
 * EVERY catalog, for every player — `content_settings.content_enabled` (PLAN §7.4).
 *
 * Note the URL has no catalog segment, which is the point: the global flag is not a property of
 * any catalog, and the bug this pipeline already shipped once was exactly a per-catalog column
 * doing a global job.
 */
export function setGlobalContentEnabled(enabled: boolean): Promise<{ message: string }> {
  return call("/api/content/enabled", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ enabled }),
  });
}

// ---------------------------------------------------------------------------
// Version history — READ from content_versions (content_panels_gaps §2)
// ---------------------------------------------------------------------------

/**
 * Every published snapshot, newest first, straight from `content_versions`.
 *
 * This replaces the reconstruction-from-`admin_audit_log` that shipped with
 * content_admin_panels. That approach could only ever see versions the DASHBOARD
 * had published, within the 200 most recent admin actions across all panels —
 * so it lost its tail as unrelated admin work accumulated, and it never saw v1
 * at all, because v1 of every catalog was seeded by SQL before the dashboard
 * existed. Rollback is the plan's §7.3 safety rail; a target list that quietly
 * stops reaching is worse than one that is obviously empty.
 *
 * `admin_audit_log` keeps its actual job — who did what — and is no longer
 * asked a question it could not answer.
 */
export function fetchVersions(
  catalog: string,
  opts: { page?: number; limit?: number } = {}
): Promise<ContentVersionsResponse> {
  const params = new URLSearchParams();
  if (opts.page) params.set("page", String(opts.page));
  if (opts.limit) params.set("limit", String(opts.limit));
  const qs = params.toString();
  return call<ContentVersionsResponse>(`/api/content/${catalog}/versions${qs ? `?${qs}` : ""}`);
}
