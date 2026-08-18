import "server-only";
import { mockDb } from "./mockStore";
import { isMockMode } from "./mode";
import { getSupabaseAdmin } from "./supabaseAdmin";
import type { NoticeRow, NoticesResponse } from "./types";

/** Read side of the Notices panel. Branches mock ↔ live like lib/bannerData.ts. */

type Row = Record<string, unknown>;

function num(v: unknown, fallback = 0): number {
  return typeof v === "number" && Number.isFinite(v) ? v : fallback;
}
function str(v: unknown): string | null {
  return typeof v === "string" && v.length > 0 ? v : null;
}
/** Columns the DB declares NOT NULL still arrive as unknown over PostgREST. */
function text(v: unknown): string {
  return typeof v === "string" ? v : "";
}

function mapNotice(r: Row): NoticeRow {
  return {
    id: String(r.id ?? ""),
    label: String(r.label ?? "(unlabelled)"),
    titleEn: text(r.title_en),
    titleJa: str(r.title_ja),
    bodyEn: text(r.body_en),
    bodyJa: str(r.body_ja),
    startAt: str(r.start_at),
    endAt: str(r.end_at),
    sortOrder: num(r.sort_order),
    isActive: r.is_active === true,
    createdAt: str(r.created_at),
    updatedAt: str(r.updated_at),
  };
}

export async function fetchNotices(): Promise<NoticesResponse> {
  if (isMockMode()) {
    return { notices: sortForPanel(mockDb().notices), mock: true };
  }

  const res = await getSupabaseAdmin().from("home_notices").select("*");
  if (res.error) throw new Error(`home_notices query failed: ${res.error.message}`);

  return { notices: sortForPanel((res.data as Row[]).map(mapNotice)), mock: false };
}

/**
 * The order the endpoint serves in: highest sort_order first, newest first on a
 * tie. Reading the panel top-down is therefore reading the pages in the order a
 * player swipes them.
 */
export function sortForPanel(rows: NoticeRow[]): NoticeRow[] {
  return [...rows].sort(
    (a, b) => b.sortOrder - a.sortOrder || (b.createdAt ?? "").localeCompare(a.createdAt ?? "")
  );
}
