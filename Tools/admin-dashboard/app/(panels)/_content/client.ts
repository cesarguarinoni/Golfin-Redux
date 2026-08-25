"use client";

/**
 * Browser-side wrappers over the SIX content routes that already exist.
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
 *   POST /api/content/:catalog/enabled     the kill switch
 *
 * Plus `GET /api/audit`, which is how the version history is assembled — see
 * `fetchVersionHistory`.
 */

import type { ContentProblem } from "@/lib/contentValidate";
import type {
  AuditEntry,
  ContentCatalogsResponse,
  ContentDiffResponse,
  ContentRowInput,
  ContentRowsResponse,
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
  opts: { page?: number; limit?: number; q?: string } = {}
): Promise<ContentRowsResponse> {
  const params = new URLSearchParams();
  if (opts.page) params.set("page", String(opts.page));
  if (opts.limit) params.set("limit", String(opts.limit));
  if (opts.q) params.set("q", opts.q);
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

export function setCatalogEnabled(catalog: string, enabled: boolean): Promise<{ message: string }> {
  return call(`/api/content/${catalog}/enabled`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ enabled }),
  });
}

// ---------------------------------------------------------------------------
// Version history — assembled, not fetched
// ---------------------------------------------------------------------------

export interface VersionEntry {
  version: number;
  at: string | null;
  by: string | null;
  note: string | null;
  /** Set when this version was produced by a rollback of an earlier one. */
  restoredFrom: number | null;
  counts: { added: number; changed: number; deactivated: number; reactivated: number } | null;
  /** False ⇒ the number is known but nothing else is (outside the audit window). */
  detailed: boolean;
}

/**
 * ⚠️ THERE IS NO ENDPOINT THAT READS `content_versions`.
 *
 * The six deployed routes expose `publishedVersion` (a single number) and
 * accept a `toVersion` for rollback, but nothing lists the snapshots with their
 * timestamps, authors or notes. Adding one is server logic, which this task is
 * barred from, so the history is RECONSTRUCTED from `admin_audit_log` — every
 * publish and rollback writes `content.publish:<catalog>` /
 * `content.rollback:<catalog>` with the resulting version in `after`.
 *
 * What that costs, stated here rather than discovered later:
 *
 *   - `/api/audit` returns the 200 most recent admin actions ACROSS ALL PANELS,
 *     so a busy week of unrelated admin work pushes old publishes out of view.
 *   - Versions created outside the dashboard have no audit row at all. v1 of
 *     every catalog is exactly that: it was seeded by SQL.
 *
 * Versions with no audit row are still LISTED (from 1..publishedVersion) and
 * still restorable — they just carry no detail, and the UI says so. Silently
 * hiding a version you can roll back to would be the worse failure.
 */
export const HISTORY_CAP = 50;

export async function fetchVersionHistory(
  catalog: string,
  publishedVersion: number
): Promise<VersionEntry[]> {
  const known = new Map<number, VersionEntry>();

  try {
    const { entries } = await call<{ entries: AuditEntry[] }>("/api/audit");
    for (const entry of entries) {
      const isPublish = entry.action === `content.publish:${catalog}`;
      const isRollback = entry.action === `content.rollback:${catalog}`;
      if (!isPublish && !isRollback) continue;

      const after = (entry.after ?? {}) as {
        version?: number;
        note?: string | null;
        restoredFrom?: number;
      };
      const before = (entry.before ?? {}) as {
        counts?: { added: number; changed: number; deactivated: number; reactivated: number };
      };
      const version = Number(after.version);
      if (!Number.isFinite(version) || version < 1) continue;

      known.set(version, {
        version,
        at: entry.at || null,
        by: entry.adminEmail || null,
        note: typeof after.note === "string" ? after.note : null,
        restoredFrom: Number.isFinite(Number(after.restoredFrom)) ? Number(after.restoredFrom) : null,
        counts: before.counts ?? null,
        detailed: true,
      });
    }
  } catch {
    // The audit read is an enrichment, not a dependency: a failure here must
    // still leave a usable (if bare) list of restorable versions.
  }

  // CAPPED. The list is generated from a NUMBER, so without a bound a catalog
  // at v10000 renders ten thousand rows — which is exactly what the mock
  // fixture (publishedVersion 9999, deliberately absurd) produced the first
  // time this ran. Real catalogs sit in single digits today, so the cap is
  // never reached in practice; it is here so that a number can never turn into
  // an unbounded DOM. Newest first, so the cap drops the oldest — the versions
  // least likely to be a rollback target and, by definition, the ones with no
  // audit detail anyway.
  const floor = Math.max(1, publishedVersion - HISTORY_CAP + 1);
  const out: VersionEntry[] = [];
  for (let v = publishedVersion; v >= floor; v -= 1) {
    out.push(
      known.get(v) ?? {
        version: v,
        at: null,
        by: null,
        note: null,
        restoredFrom: null,
        counts: null,
        detailed: false,
      }
    );
  }
  return out;
}
