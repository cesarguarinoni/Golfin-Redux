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
    return { banners: sortForPanel(mockDb().banners), mock: true };
  }

  const { data, error } = await getSupabaseAdmin().from("game_banners").select("*");
  if (error) throw new Error(`game_banners query failed: ${error.message}`);

  return { banners: sortForPanel((data as Row[]).map(mapBanner)), mock: false };
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
