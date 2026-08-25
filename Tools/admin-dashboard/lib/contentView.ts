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

import { ID_COLUMN, SHOP_CATEGORY_TO_CATALOG } from "./contentValidate";
import type { ContentStoredRow } from "./types";

export { ID_COLUMN, SHOP_CATEGORY_TO_CATALOG };

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
 * How a facet value reaches the SERVER.
 *
 * `/api/content/:catalog/rows` takes `page`, `limit` and `q`, and `q` matches
 * `row_id ILIKE *q*` OR `data->>{searchColumn} ILIKE *q*`. There is no
 * `?brand=` / `?type=` / `?rarity=` — adding one is server logic, which this
 * task is explicitly barred from (SPEC §Out of scope). So a facet is only
 * offered when its value provably lands in one of those two columns, and each
 * facet declares which and how completely.
 *
 * Measured against the shipped `Assets/Resources/Data/Clubs.csv` (799 rows) on
 * 2026-08-25 — these numbers are not estimates:
 *
 *   brand  → `name`   799/799  every club name contains its brand
 *   type   → `name`   798/799  only `club_awedge_fyloe` misses ("A. Wedge Fyloe"
 *                              has a space, type is "A.Wedge")
 *   rarity → `row_id` 792/799  the 7 originally-shipped rows predate the
 *                              `club_<type>_<brand>_<rarity>` id convention
 *
 * `coverage` is rendered in the UI next to the facet. A filter that quietly
 * drops rows is worse than no filter, so the ones that cannot be complete say
 * so instead of pretending.
 */
export interface Facet {
  /** `data` column the values are read from, and grouped by. */
  column: string;
  /** i18n key for the label. */
  labelKey: "c.facet.brand" | "c.facet.type" | "c.facet.rarity";
  /**
   * Which server-side column the chosen value is matched against via `q`.
   * `name` → the catalog's search column; `row_id` → the id.
   */
  matches: "name" | "row_id";
  /** Rows out of the catalog this facet can actually reach, or null if exact. */
  coverage: { hit: number; total: number } | null;
  /**
   * Turn a chosen facet value into the `q` string sent to the server.
   * Rarity matches the id SUFFIX convention, so it is lowercased.
   */
  toQuery: (value: string) => string;
}

const BRAND_FACET: Facet = {
  column: "brand",
  labelKey: "c.facet.brand",
  matches: "name",
  coverage: null,
  toQuery: (v) => v,
};

const TYPE_FACET: Facet = {
  column: "type",
  labelKey: "c.facet.type",
  matches: "name",
  coverage: { hit: 798, total: 799 },
  toQuery: (v) => v,
};

const RARITY_FACET: Facet = {
  column: "rarity",
  labelKey: "c.facet.rarity",
  matches: "row_id",
  coverage: { hit: 792, total: 799 },
  toQuery: (v) => v.toLowerCase(),
};

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
    facets: [],
    limit: 50,
  },
  items: {
    catalog: "items",
    columns: ["name", "category", "rarity", "restorePercent"],
    facets: [],
    limit: 50,
  },
  bags: {
    catalog: "bags",
    columns: ["name", "rarity", "unlocked"],
    facets: [],
    limit: 50,
  },
  balls: {
    catalog: "balls",
    columns: ["name", "brand", "power", "rebound", "windResistance", "roll", "spin"],
    facets: [],
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
    facets: [],
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
// Shop row state
// ---------------------------------------------------------------------------

export type ShopState = "LIVE" | "SCHEDULED" | "ENDED" | "OFF";

/**
 * The untranslated state badge (§11.3), derived from the listing window.
 *
 * ⚠️ `startAt` / `endAt` DO NOT EXIST on `shop_catalog` today. They are §11.2
 * *proposed* columns and no migration has applied them — the shipped header is
 * `entryId,category,refId,rpCost,saleRpCost,sortOrder,popular,offer,rarity`.
 * Schema changes are out of scope for this task, so this reads the windows
 * WHEN PRESENT (the row is JSONB and I4 is additive-only, so they can appear
 * without touching this function) and otherwise falls back to the one piece of
 * lifecycle state that does exist: `is_active`.
 *
 * That fallback is why an inactive row reads OFF rather than ENDED. Inventing a
 * schedule out of a column that is not there would be worse than saying "this
 * row is switched off", which is exactly what `is_active` means.
 */
export function shopState(row: ContentStoredRow, now: number = Date.now()): ShopState {
  if (!row.isActive) return "OFF";

  const start = parseInstant(row.data.startAt);
  const end = parseInstant(row.data.endAt);
  if (start !== null && now < start) return "SCHEDULED";
  if (end !== null && now >= end) return "ENDED";
  return "LIVE";
}

/** True when this catalog carries the §11.2 window columns yet. */
export function hasListingWindows(rows: ContentStoredRow[]): boolean {
  return rows.some((r) => "startAt" in r.data || "endAt" in r.data);
}

/** A sale is on when saleRpCost undercuts rpCost AND any sale window is open. */
export function shopOnSale(row: ContentStoredRow, now: number = Date.now()): boolean {
  const rp = Number(row.data.rpCost);
  const sale = row.data.saleRpCost?.trim();
  if (!sale) return false;
  const saleN = Number(sale);
  if (!Number.isFinite(rp) || !Number.isFinite(saleN) || saleN <= 0 || saleN >= rp) return false;

  const start = parseInstant(row.data.saleStartAt);
  const end = parseInstant(row.data.saleEndAt);
  if (start !== null && now < start) return false;
  if (end !== null && now >= end) return false;
  return true;
}

function parseInstant(v: string | undefined): number | null {
  const s = (v ?? "").trim();
  if (!s) return null;
  const ms = Date.parse(s);
  return Number.isNaN(ms) ? null : ms;
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
