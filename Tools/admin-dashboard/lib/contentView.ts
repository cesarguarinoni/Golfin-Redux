/**
 * Presentation model for the content panels — PURE and CLIENT-SAFE.
 *
 * No `server-only`, no Supabase, no fetch. This is the table that says "the
 * clubs panel shows these columns, offers these facets, and searches like
 * this", so the five panels are one component with five descriptors rather
 * than five near-copies that drift.
 *
 * The catalog SHAPES (required columns, id column, category→catalog) live in
 * `lib/contentValidate.ts` and are imported, not restated: that module is what
 * blocks a bad publish, and a second copy of the truth here would be a second
 * thing to forget to update.
 */

import {
  ID_COLUMN,
  isValidNewRowId,
  ROW_ID_MAX,
  SHOP_CATEGORY_TO_CATALOG,
} from "./contentValidate";
import type { ContentStoredRow } from "./types";

export { ID_COLUMN, SHOP_CATEGORY_TO_CATALOG, isValidNewRowId, ROW_ID_MAX };

/** Catalogs these panels edit. `bags`/`balls` ride inside the Items panel. */
export const CONTENT_CATALOGS = [
  "clubs",
  "characters",
  "items",
  "bags",
  "balls",
  "texts",
  "shop_catalog",
] as const;

export type ContentCatalog = (typeof CONTENT_CATALOGS)[number];

// ---------------------------------------------------------------------------
// Facets — and the honest limit on them
// ---------------------------------------------------------------------------

/**
 * A facet dropdown over one `data` field.
 *
 * Every facet is now a real server query: `/api/content/:catalog/rows` takes the
 * field as its own parameter and matches `data->>'<field>'` EXACTLY, with all
 * active facets AND-ed together and with `q`, and `total` counting the filtered
 * set (content_panels_gaps §1).
 *
 * There is deliberately no `coverage` field any more. The previous version
 * carried one because the facets were being squeezed through the single
 * free-text `q`, which forced rarity to match the ROW ID — and the generated
 * ids encode rarity (`club_awedge_bogeyb_common`) while the 7 hand-authored
 * ones do not. That made rarity look like a partial facet. It never was: every
 * one of the 799 club rows carries `data.rarity`, the 7 hand-authored ones
 * included, and `data->>rarity=eq.Common` returns 133 on prod. The facet was
 * reading the wrong place. Now that all three are complete queries, showing a
 * coverage caveat would state something untrue.
 *
 * `values` are NOT listed here: they come from the server
 * (`fetchFacetValues`), so a brand added in drafts appears without a deploy.
 */
export interface Facet {
  /** `data` field this facet filters on. Must be on the route's allow-list. */
  column: string;
  /** i18n key for the label. */
  labelKey: "c.facet.brand" | "c.facet.type" | "c.facet.rarity" | "c.facet.category";
}

const BRAND_FACET: Facet = { column: "brand", labelKey: "c.facet.brand" };
const TYPE_FACET: Facet = { column: "type", labelKey: "c.facet.type" };
const RARITY_FACET: Facet = { column: "rarity", labelKey: "c.facet.rarity" };
const CATEGORY_FACET: Facet = { column: "category", labelKey: "c.facet.category" };

// ---------------------------------------------------------------------------
// One descriptor per panel
// ---------------------------------------------------------------------------

export interface CatalogView {
  catalog: ContentCatalog;
  /** Columns shown in the table, in order. `row_id` is always rendered first. */
  columns: string[];
  /** Columns the row editor exposes. Empty ⇒ every column in `data`. */
  editable?: string[];
  /** Facets offered above the table. At most one can be active at a time. */
  facets: Facet[];
  /** Rows per server page. */
  limit: number;
}

/**
 * Prefer `catalogView()` over indexing this directly: with
 * `noUncheckedIndexedAccess` on, a raw index is `CatalogView | undefined` at
 * every use site, and the honest single check belongs here rather than as
 * eight `!` assertions in the panel.
 */
export const CATALOG_VIEWS: Record<string, CatalogView> = {
  clubs: {
    catalog: "clubs",
    columns: ["name", "type", "rarity", "brand", "basePower", "baseAccuracy", "maxDurability", "startLevel", "maxLevel"],
    facets: [BRAND_FACET, TYPE_FACET, RARITY_FACET],
    limit: 50,
  },
  characters: {
    catalog: "characters",
    columns: ["name", "lastName", "rarity", "baseStrength", "baseClubControl", "baseRecovery", "baseStamina", "startLevel", "maxLevel"],
    facets: [RARITY_FACET],
    limit: 50,
  },
  items: {
    catalog: "items",
    columns: ["name", "category", "rarity", "restorePercent"],
    facets: [CATEGORY_FACET, RARITY_FACET],
    limit: 50,
  },
  bags: {
    catalog: "bags",
    columns: ["name", "rarity", "unlocked"],
    facets: [RARITY_FACET],
    limit: 50,
  },
  balls: {
    catalog: "balls",
    columns: ["name", "brand", "power", "rebound", "windResistance", "roll", "spin"],
    facets: [BRAND_FACET],
    limit: 50,
  },
  texts: {
    catalog: "texts",
    columns: ["English", "Japanese"],
    facets: [],
    limit: 50,
  },
  shop_catalog: {
    catalog: "shop_catalog",
    columns: ["category", "refId", "rpCost", "saleRpCost", "sortOrder", "popular", "offer"],
    facets: [CATEGORY_FACET],
    limit: 50,
  },
};

/** The view for a catalog. Throws on an unregistered name — a panel pointed at
 *  a catalog that does not exist is a build-time mistake, not a runtime state. */
export function catalogView(catalog: string): CatalogView {
  const view = CATALOG_VIEWS[catalog];
  if (!view) throw new Error(`No CatalogView registered for "${catalog}".`);
  return view;
}

// ---------------------------------------------------------------------------
// Sprite fields — which columns name ART, and where the build looks for it
// ---------------------------------------------------------------------------

/**
 * `catalog → { column: Resources folder }` (content_two_way §6).
 *
 * A sprite column holds a FILE NAME, not a URL and not an upload: the build
 * resolves it with `Resources.Load<Sprite>("<folder>/<name>")`, so a name that
 * names nothing in that build renders nothing. Since content_two_way §4 the
 * client WITHHOLDS such a row (clubs excepted — they fall back to a shared
 * Placeholder sprite), which is safe but silent, and the only place an operator
 * can find out before publishing is here.
 *
 * The folder strings are copies of the loader constants, cited so a rename in
 * Unity is findable from the dashboard:
 *
 *   CharacterDatabaseCSV.cs:36-37   Portraits/Thumbnails, Portraits/FullBody
 *   ItemDatabaseCSV.cs:25-26        Items/Thumbnails,     Items/Full
 *   BallDatabaseCSV.cs:25-26        Balls/Thumbnails,     Balls/Full
 *   ClubDatabaseCSV.cs:39-41        Clubs/Portraits, Clubs/Full, Clubs/Controls
 *
 * Art by URL — which would let an admin-created row render on an INSTALLED
 * build — is the next spec (`content_art_urls`), not this one.
 */
export const SPRITE_FIELD_FOLDER: Record<string, Record<string, string>> = {
  characters: {
    portraitSprite: "Portraits/Thumbnails",
    portraitFull: "Portraits/FullBody",
  },
  items: {
    thumbnailSprite: "Items/Thumbnails",
    fullSprite: "Items/Full",
  },
  balls: {
    thumbnailSprite: "Balls/Thumbnails",
    fullSprite: "Balls/Full",
  },
  clubs: {
    portraitSprite: "Clubs/Portraits",
    portraitFull: "Clubs/Full",
    controlSprite: "Clubs/Controls",
  },
};

/** The `Resources/` folder a column's sprite must live in, or null when the
 *  column does not name art. */
export function spriteFolder(catalog: string, column: string): string | null {
  return SPRITE_FIELD_FOLDER[catalog]?.[column] ?? null;
}

// ---------------------------------------------------------------------------
// Art URL columns (content_art_urls)
// ---------------------------------------------------------------------------

/**
 * `catalog → [urlColumn, …]` — columns that hold a public Supabase Storage URL
 * instead of a bundled sprite name (SPEC content_art_urls §3, I4).
 *
 * These columns are additive: a row can carry BOTH a sprite name (for builds
 * that bundled the art) AND a URL (for installed builds that have not). The
 * resolution ladder in each DatabaseCSV prefers the URL when a cached copy is
 * present.
 *
 * The row editor shows an upload button beside each of these fields; the API
 * route is POST /api/content/art.
 */
export const ART_URL_COLUMNS: Record<string, readonly string[]> = {
  characters: ["portraitUrl", "fullUrl"],
  clubs:      ["portraitUrl", "fullUrl", "controlUrl"],
  items:      ["thumbnailUrl", "fullUrl"],
  balls:      ["thumbnailUrl", "fullUrl"],
} as const;

/**
 * Returns true when the given column in the given catalog is a URL column
 * rather than a bundled sprite-name column.
 */
export function isArtUrlColumn(catalog: string, column: string): boolean {
  return (ART_URL_COLUMNS[catalog] ?? []).includes(column);
}

// ---------------------------------------------------------------------------
// Shop row state
// ---------------------------------------------------------------------------

export type ShopState = "LIVE" | "SCHEDULED" | "ENDED" | "OFF" | "BROKEN";

/**
 * A window bound → epoch ms, or null when genuinely absent.
 *
 * FAILS CLOSED, exactly like `routers/notices.py` `_parse`: a bound that is
 * PRESENT but unreadable THROWS, and the caller treats the row as broken rather
 * than as unscheduled. "We could not read the schedule window" must never
 * collapse into "so show it forever" — that is how a typo in an end date turns
 * a one-week sale into a permanent one.
 *
 * An absent bound (empty string) is a real, meaningful value: no bound.
 */
export function parseWindowBound(value: string | undefined): number | null {
  const raw = (value ?? "").trim();
  if (raw === "") return null;
  const ms = Date.parse(raw.replace(" ", "T"));
  if (Number.isNaN(ms)) {
    throw new RangeError(`Unparseable window bound: ${raw}`);
  }
  return ms;
}

/**
 * The untranslated state badge (§11.3), derived from the listing window.
 *
 * `startAt` / `endAt` / `saleStartAt` / `saleEndAt` were specified in
 * CONTENT_PIPELINE_PLAN.md §11.2 and added to the catalog by
 * content_panels_gaps §3 — empty on every shipped row, so nothing changed
 * behaviour on the way in. `endAt` is EXCLUSIVE, matching `home_notices`.
 *
 * A row with no window falls back to `is_active`, which is why an inactive row
 * reads OFF rather than ENDED: `is_active` is a switch, not a schedule.
 * A row whose window cannot be PARSED reads BROKEN — never LIVE.
 */
export function shopState(row: ContentStoredRow, now: number = Date.now()): ShopState {
  if (!row.isActive) return "OFF";

  let start: number | null;
  let end: number | null;
  try {
    start = parseWindowBound(row.data.startAt);
    end = parseWindowBound(row.data.endAt);
  } catch {
    return "BROKEN";
  }

  if (start !== null && now < start) return "SCHEDULED";
  if (end !== null && now >= end) return "ENDED";
  return "LIVE";
}

/** True when this catalog carries the §11.2 window columns yet. */
export function hasListingWindows(rows: ContentStoredRow[]): boolean {
  return rows.some((r) => "startAt" in r.data || "endAt" in r.data);
}

/**
 * A sale is on when `saleRpCost` undercuts `rpCost` AND the sale window is open.
 * Fails closed the same way: an unreadable sale bound is NOT a sale.
 */
export function shopOnSale(row: ContentStoredRow, now: number = Date.now()): boolean {
  const rp = Number(row.data.rpCost);
  const sale = row.data.saleRpCost?.trim();
  if (!sale) return false;
  const saleN = Number(sale);
  if (!Number.isFinite(rp) || !Number.isFinite(saleN) || saleN <= 0 || saleN >= rp) return false;

  let start: number | null;
  let end: number | null;
  try {
    start = parseWindowBound(row.data.saleStartAt);
    end = parseWindowBound(row.data.saleEndAt);
  } catch {
    return false;
  }
  if (start !== null && now < start) return false;
  if (end !== null && now >= end) return false;
  return true;
}

// ---------------------------------------------------------------------------
// Resolved reference preview
// ---------------------------------------------------------------------------

/** The `data` column holding a human name, per catalog. */
const NAME_COLUMN: Record<string, string> = {
  clubs: "name",
  characters: "name",
  items: "name",
  bags: "name",
  balls: "name",
};

/**
 * The `data` column holding the ART REFERENCE — a Unity sprite NAME, not a URL.
 *
 * This is why the §11.3 "art thumbnail" is a monogram tile and not an `<img>`:
 * the catalogs carry `portraitSprite: "Driver-G&F"` / `thumbnailSprite`, which
 * `Resources.Load` resolves INSIDE THE GAME. There is no URL and no bucket —
 * `game-banners` holds banner art only, and art-URL columns are out of scope
 * (SPEC §Out of scope). So the panel shows the exact sprite name the game will
 * load, which is the fact an operator can actually act on, plus a stable
 * colour/monogram tile so the row reads as an entity rather than an id.
 */
const ART_COLUMN: Record<string, string> = {
  clubs: "portraitSprite",
  characters: "portraitSprite",
  items: "thumbnailSprite",
  bags: "thumbnail",
  balls: "thumbnailSprite",
};

export interface ResolvedRef {
  rowId: string;
  name: string;
  rarity: string;
  /** Unity sprite name — NOT a URL. See ART_COLUMN. */
  artRef: string;
  isActive: boolean;
}

export function resolveRef(catalog: string, row: ContentStoredRow): ResolvedRef {
  return {
    rowId: row.rowId,
    name: row.data[NAME_COLUMN[catalog] ?? "name"] ?? row.rowId,
    rarity: row.data.rarity ?? "",
    artRef: row.data[ART_COLUMN[catalog] ?? ""] ?? "",
    isActive: row.isActive,
  };
}

/** Deterministic tile colour from an id, so the same entity always looks the same. */
export function monogramHue(seed: string): number {
  let h = 0;
  for (let i = 0; i < seed.length; i += 1) h = (h * 31 + seed.charCodeAt(i)) % 360;
  return h;
}

export function monogram(name: string): string {
  const words = name.trim().split(/[\s._-]+/).filter(Boolean);
  const first = words[0];
  if (!first) return "?";
  const second = words[1];
  if (!second) return first.slice(0, 2).toUpperCase();
  return `${first.charAt(0)}${second.charAt(0)}`.toUpperCase();
}

// ---------------------------------------------------------------------------
// Rarity styling — mirrors RarityHelper's ladder, for badges only
// ---------------------------------------------------------------------------

export const RARITY_STYLE: Record<string, string> = {
  Common: "border-zinc-600 bg-surface-800 text-zinc-300",
  Uncommon: "border-emerald-600/50 bg-emerald-500/10 text-emerald-300",
  Rare: "border-sky-500/50 bg-sky-500/10 text-sky-300",
  Mythic: "border-violet-500/50 bg-violet-500/10 text-violet-300",
  Legendary: "border-amber-500/50 bg-amber-500/10 text-amber-300",
  Supreme: "border-rose-500/50 bg-rose-500/10 text-rose-300",
};
