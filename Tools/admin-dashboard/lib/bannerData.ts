import "server-only";
import { isBannerPlacement } from "./banner";
import { mockDb } from "./mockStore";
import { isMockMode } from "./mode";
import { getSupabaseAdmin } from "./supabaseAdmin";
import type { BannerPlacement, BannerRow, BannersResponse } from "./types";

/** Read side of the Banners panel. Branches mock ↔ live like lib/tournamentData.ts. */

type Row = Record<string, unknown>;

function num(v: unknown, fallback = 0): number {
  return typeof v === "number" && Number.isFinite(v) ? v : fallback;
}
function str(v: unknown): string | null {
  return typeof v === "string" && v.length > 0 ? v : null;
}

function mapBanner(r: Row): BannerRow {
  // A row whose placement this build does not know about would be unrenderable
  // — but the DB CHECK constraint makes that impossible, so home_promo is a
  // defensive default rather than a case that fires.
  const placement: BannerPlacement = isBannerPlacement(r.placement)
    ? r.placement
    : "home_promo";
  return {
    id: String(r.id ?? ""),
    placement,
    label: String(r.label ?? "(unlabelled)"),
    imageUrlEn: str(r.image_url_en),
    imageUrlJa: str(r.image_url_ja),
    linkUrl: str(r.link_url),
    startAt: str(r.start_at),
    endAt: str(r.end_at),
    sortOrder: num(r.sort_order),
    isActive: r.is_active === true,
    createdAt: str(r.created_at),
    updatedAt: str(r.updated_at),
  };
}

export async function fetchBanners(): Promise<BannersResponse> {
  if (isMockMode()) {
    const db = mockDb();
    const assigned: Record<string, string[]> = {};
    for (const t of db.tournaments) {
      const id = t.modalBannerId;
      if (id) (assigned[id] ??= []).push(t.slug ?? t.title);
    }
    return { banners: sortForPanel(db.banners), mock: true, assignedTournaments: assigned };
  }

  const admin = getSupabaseAdmin();
  const [bRes, tRes] = await Promise.all([
    admin.from("game_banners").select("*"),
    // Which tournaments point at which banner. Tolerated failure: on a DB that
    // predates the migration this errors, and an absent count must not take the
    // whole Banners panel down — it just means no assignment is shown.
    admin.from("tournaments").select("slug, title, modal_banner_id"),
  ]);
  if (bRes.error) throw new Error(`game_banners query failed: ${bRes.error.message}`);

  const assignedTournaments: Record<string, string[]> = {};
  if (tRes.error) {
    console.warn("modal_banner_id assignment count failed:", tRes.error.message);
  } else {
    for (const r of (tRes.data ?? []) as Row[]) {
      const id = str(r.modal_banner_id);
      if (!id) continue;
      (assignedTournaments[id] ??= []).push(str(r.slug) ?? str(r.title) ?? "(untitled)");
    }
  }

  return {
    banners: sortForPanel((bRes.data as Row[]).map(mapBanner)),
    mock: false,
    assignedTournaments,
  };
}

/**
 * The order the endpoint would pick in: highest sort_order first, newest first
 * on a tie. Reading the panel top-down within a placement therefore reads the
 * same way the server resolves "which one is live".
 */
export function sortForPanel(rows: BannerRow[]): BannerRow[] {
  return [...rows].sort(
    (a, b) => b.sortOrder - a.sortOrder || (b.createdAt ?? "").localeCompare(a.createdAt ?? "")
  );
}
