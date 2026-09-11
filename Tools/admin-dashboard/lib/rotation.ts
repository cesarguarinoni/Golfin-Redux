/**
 * Weekly rotation — the lineup generator, PURE (weekly_rotation_admin §4.1).
 *
 * No React, no Supabase, no `server-only`, no clock of its own, no
 * `Math.random`. A `rotations` row (a window, per-rarity quotas, a seed, the
 * pinned refs) plus the draft rows of the catalogs it draws from go in; the
 * DRAFT ROWS of the five existing catalogs come out — shop rows carrying the
 * week's window, one gacha banner, one pool, its rates, and the rotation row
 * itself stamped `materializedAt`. Nothing here writes: the panel hands the rows
 * to `upsertDraftRow` one at a time (§4.2), and publish is the ordinary drawer
 * five times in a fixed order (§4.3).
 *
 * WHY PURE, and why the seed is the row's. The Daily Missions panel is the
 * precedent: a generator that is deterministic in its inputs is one an operator
 * can PREVIEW, because the preview shows exactly what MATERIALIZE will write —
 * same rows, same ids, same odds. `mulberry32(seed)` (lib/gachaOdds.ts) is the
 * same PRNG the odds simulator uses, for the same reason: "run it again and it
 * says something else" makes a preview worthless as evidence.
 *
 * PINS WIN (§4.1 step 0). Every `pinned*` value is taken VERBATIM — the year
 * plan (§3.1a) is data in those four columns, not code here — and counts
 * against the quota; the RNG only fills what the pins left blank. A pin that
 * does not resolve, is deactivated, or is the default ball BLOCKS with the ref
 * named, because a lineup that silently dropped a planned club is a plan
 * nobody can trust.
 *
 * `is_active` is never a `data` field on either side (Tools/content): the rows
 * produced here carry it as `isActive`, the way `DraftRow` does.
 */

import { SHOP_CATEGORY_STRICT_BUILD } from "./buildGates";
import {
  effectiveOdds,
  mulberry32,
  RARITY_ORDER,
  rarityRank,
  type EffectiveOdd,
  type PoolEntry,
  type RateRow,
} from "./gachaOdds";

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

/** `wk_<ISO-year>_<ISO-week>` — validator rule R1 and the calendar agree on this. */
export const ROTATION_ID_RE = /^wk_\d{4}_\d{2}$/;

/** The pity group every weekly banner shares (§5): one counter across weeks. */
export const WEEKLY_PITY_GROUP = "weekly";

/** The bundled placeholder art every weekly banner starts with (§4.4). */
export const WEEKLY_BANNER_ART = "GachaBanner_Weekly";

/**
 * Publish order — dependencies FIRST, so a validator reading
 * `ctx.otherCatalogs` sees the rows its catalog references already in drafts
 * and the server never serves a banner whose pool is not published yet. The
 * chain stops at the first failure (§4.3); nothing after it is published.
 */
export const ROTATION_PUBLISH_ORDER = [
  "gacha_rates",
  "gacha_pools",
  "gacha_banners",
  "shop_catalog",
  "rotations",
] as const;
export type RotationCatalog = (typeof ROTATION_PUBLISH_ORDER)[number];

/**
 * The three price ladders (§3.3). Club and character ladders are
 * ECONOMY_MASTER.md §3 verbatim; the ball ladder is NEW there, added by this
 * task. No Supreme ball exists in the catalog; the Supreme entry only keeps
 * the record total so a hand-authored one prices rather than crashes. Every
 * generated price is a DEFAULT — the draft row is editable before publish.
 *
 * ⚠️ A BALL LISTING DELIVERS ONE BALL, NOT TEN. The spec's §3.3 wrote the ladder
 * as "quantity 10", but `golfin_shop_purchase` reads `quantity` for
 * `category = ticket` ONLY (2026_09_01_shop_purchase_tickets.sql — honouring
 * it for balls "would change what already-published listings deliver") and
 * validator rule G3-Q refuses any other value on a non-ticket row. A generated
 * `quantity: 10` would therefore be refused at publish, and if it were not, it
 * would charge 30 RP for a single ball. So the ball rows carry a BLANK
 * quantity (= 1) at the ladder price; the ten-ball listing is a server change
 * (shop purchase quantity for balls + a G3-Q relaxation), flagged in the
 * report, not something a content row can decide.
 */
export const CLUB_RP_LADDER: Record<string, number> = {
  Common: 100, Uncommon: 200, Rare: 400, Mythic: 800, Legendary: 1500, Supreme: 3000,
};
export const CHARACTER_RP_LADDER: Record<string, number> = {
  Common: 200, Uncommon: 400, Rare: 800, Mythic: 1600, Legendary: 3000, Supreme: 6000,
};
export const BALL_RP_LADDER: Record<string, number> = {
  Common: 30, Uncommon: 60, Rare: 120, Mythic: 250, Legendary: 500, Supreme: 1000,
};

/** Defaults for a rotation row the calendar creates (§4.4). */
export const ROTATION_DEFAULTS = {
  nameEn: "WEEKLY LINEUP",
  nameJa: "今週のラインナップ",
  clubQuota: "Common:3;Uncommon:2;Rare:2;Mythic:1;Legendary:1;Supreme:0",
  ballQuota: "Common:1;Uncommon:1;Rare:1;Mythic:0;Legendary:0",
  characterQuota: "Any:1",
  gachaFeaturedCount: "2",
  gachaBasePoolId: "pool_standard_club1",
  featuredWeightMul: "3",
  excludeWeeks: "4",
} as const;

/** Every column of a `rotations` row, in CSV order — the row editor and the
 *  calendar's "create week" both need the full shape so no key is missing. */
export const ROTATION_COLUMNS = [
  "rotationId", "startUtc", "endUtc", "nameEn", "nameJa", "clubQuota", "ballQuota",
  "characterQuota", "gachaFeaturedCount", "gachaBasePoolId", "featuredWeightMul",
  "excludeWeeks", "seed", "pinnedClubs", "pinnedBalls", "pinnedCharacter",
  "pinnedFeatured", "materializedAt",
] as const;

// ---------------------------------------------------------------------------
// Row shapes — structurally the validator's DraftRow and the panel's ContentStoredRow
// ---------------------------------------------------------------------------

export interface CatalogRow {
  rowId: string;
  /** `unknown` values so the validator's `DraftRow` and the panel's
   *  `ContentStoredRow` are both accepted; every read goes through `text()`. */
  data: Record<string, unknown>;
  minBuild: number;
  isActive: boolean;
}

/** A draft row the generator wants written — `upsertDraftRow`'s input, plus the catalog. */
export interface OutRow {
  catalog: RotationCatalog;
  rowId: string;
  data: Record<string, string>;
  minBuild: number;
  isActive: boolean;
}

// ---------------------------------------------------------------------------
// Small parsers, shared with the validator (contentValidate imports THIS module,
// never the reverse — that is what keeps the import graph acyclic)
// ---------------------------------------------------------------------------

const text = (v: unknown): string => (v === null || v === undefined ? "" : String(v));

/** A row's `data` with every value coerced to string — what `upsertDraftRow` takes. */
const stringData = (data: Record<string, unknown>): Record<string, string> =>
  Object.fromEntries(Object.entries(data).map(([k, v]) => [k, text(v)]));

/** `;`-separated refs, trimmed, blanks dropped. */
export function parseRefList(value: unknown): string[] {
  return text(value)
    .split(";")
    .map((s) => s.trim())
    .filter(Boolean);
}

export interface Quota {
  /** `Any` ⇒ one bucket over every rarity; otherwise one per named rarity. */
  any: number | null;
  perRarity: Map<string, number>;
  total: number;
}

/**
 * `Rarity:int;…` or `Any:int` (§3.1). Returns an error STRING rather than
 * throwing so the validator can hang it off the column.
 */
export function parseQuota(value: unknown): { quota: Quota | null; error: string | null } {
  const raw = text(value).trim();
  if (raw === "") return { quota: { any: null, perRarity: new Map(), total: 0 }, error: null };
  const perRarity = new Map<string, number>();
  let any: number | null = null;
  for (const part of raw.split(";").map((s) => s.trim()).filter(Boolean)) {
    const at = part.indexOf(":");
    if (at < 0) return { quota: null, error: `"${part}" is not Rarity:count.` };
    const key = part.slice(0, at).trim();
    const count = Number(part.slice(at + 1).trim());
    if (!Number.isInteger(count) || count < 0) {
      return { quota: null, error: `"${part}": the count must be a whole number ≥ 0.` };
    }
    if (key === "Any") {
      any = (any ?? 0) + count;
    } else if ((RARITY_ORDER as readonly string[]).includes(key)) {
      perRarity.set(key, (perRarity.get(key) ?? 0) + count);
    } else {
      return { quota: null, error: `"${key}" is neither Any nor one of ${RARITY_ORDER.join(", ")}.` };
    }
  }
  let total = any ?? 0;
  for (const n of perRarity.values()) total += n;
  return { quota: { any, perRarity, total }, error: null };
}

/** FNV-1a, 32-bit — the default seed of a rotation is the hash of its id. */
export function fnv1a32(input: string): number {
  let h = 0x811c9dc5;
  for (let i = 0; i < input.length; i += 1) {
    h ^= input.charCodeAt(i);
    h = Math.imul(h, 0x01000193) >>> 0;
  }
  return h >>> 0;
}

/** The row's `seed` when it is a whole number, else FNV-1a of the id (§3.1). */
export function rotationSeed(data: Record<string, unknown>): number {
  const raw = text(data.seed).trim();
  if (/^\d+$/.test(raw)) return Number(raw) >>> 0;
  return fnv1a32(text(data.rotationId).trim());
}

/** ISO instant → epoch ms, or null when absent, or NaN when unreadable. */
export function parseInstant(value: unknown): number | null {
  const raw = text(value).trim();
  if (raw === "") return null;
  return Date.parse(raw.replace(" ", "T"));
}

const isTrue = (v: unknown): boolean => {
  const s = text(v).trim().toLowerCase();
  return s === "true" || s === "1";
};

// ---------------------------------------------------------------------------
// ISO weeks — the calendar and the id format
// ---------------------------------------------------------------------------

/** The Monday 00:00 UTC on or before `ms`. */
export function mondayUtc(ms: number): number {
  const d = new Date(ms);
  const day = (d.getUTCDay() + 6) % 7; // Monday = 0
  return Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate()) - day * 86_400_000;
}

/** ISO-8601 year + week of the instant — `wk_2026_38` for 2026-09-14. */
export function isoWeekId(ms: number): string {
  const d = new Date(mondayUtc(ms));
  // ISO week 1 is the week containing the year's first Thursday.
  const thursday = new Date(d.getTime() + 3 * 86_400_000);
  const isoYear = thursday.getUTCFullYear();
  const jan4 = Date.UTC(isoYear, 0, 4);
  const week1Monday = mondayUtc(jan4);
  const week = Math.round((d.getTime() - week1Monday) / (7 * 86_400_000)) + 1;
  return `wk_${isoYear}_${String(week).padStart(2, "0")}`;
}

/** ISO-8601 UTC without milliseconds — the form every window column in the catalogs uses. */
export const isoInstant = (ms: number): string => new Date(ms).toISOString().replace(/\.\d{3}Z$/, "Z");

// ---------------------------------------------------------------------------
// The generator
// ---------------------------------------------------------------------------

export interface LineupContext {
  /** The rotation being generated — its `data` is the row, `minBuild` its own. */
  rotation: CatalogRow;
  /** EVERY rotation row (active or not) — the exclusion rule reads the recent ones. */
  rotations: CatalogRow[];
  clubs: CatalogRow[];
  balls: CatalogRow[];
  characters: CatalogRow[];
  /** Existing `shop_catalog` drafts — what the recent rotations listed. */
  shop: CatalogRow[];
  gachaRates: CatalogRow[];
  gachaPools: CatalogRow[];
  gachaBanners: CatalogRow[];
  /** ISO instant written to `materializedAt` — the panel's clock, passed in. */
  now: string;
}

export interface LineupPick {
  refId: string;
  name: string;
  rarity: string;
  /** Clubs only. */
  type: string;
  brand: string;
  pinned: boolean;
  rpCost: number;
}

export interface LineupWarning {
  bucket: "clubs" | "balls" | "characters" | "featured" | "banner" | "pool";
  message: string;
}

export interface Lineup {
  rotationId: string;
  seed: number;
  startUtc: string;
  endUtc: string;
  clubs: LineupPick[];
  balls: LineupPick[];
  characters: LineupPick[];
  /** Featured club refIds, highest rarity first. */
  featured: string[];
  rows: Record<RotationCatalog, OutRow[]>;
  /** The weekly banner's effective odds over the generated pool. */
  odds: EffectiveOdd[];
  warnings: LineupWarning[];
  /** Non-empty ⇒ BLOCKED: no rows, no materialize. */
  errors: string[];
  /** FNV-1a over the canonical JSON of every row — what the determinism test quotes. */
  hash: string;
}

const EMPTY_ROWS = (): Record<RotationCatalog, OutRow[]> => ({
  gacha_rates: [],
  gacha_pools: [],
  gacha_banners: [],
  shop_catalog: [],
  rotations: [],
});

/** Deterministic Fisher–Yates over a copy, driven by the seeded PRNG. */
function shuffle<T>(items: readonly T[], rnd: () => number): T[] {
  const out = items.slice();
  for (let i = out.length - 1; i > 0; i -= 1) {
    const j = Math.floor(rnd() * (i + 1));
    const tmp = out[i]!;
    out[i] = out[j]!;
    out[j] = tmp;
  }
  return out;
}

const byRowId = (rows: CatalogRow[]): Map<string, CatalogRow> =>
  new Map(rows.map((r) => [r.rowId, r]));

/** Rarity rank DESCENDING for sort orders: Supreme 0 … Common 5. */
const rankDesc = (rarity: string): number => {
  const r = rarityRank(rarity);
  return r < 0 ? RARITY_ORDER.length : RARITY_ORDER.length - 1 - r;
};

/** Higher rarity first; ties keep input order (stable). */
const sortRarityDesc = <T extends { rarity: string }>(items: T[]): T[] =>
  items
    .map((item, i) => ({ item, i }))
    .sort((a, b) => rankDesc(a.item.rarity) - rankDesc(b.item.rarity) || a.i - b.i)
    .map((x) => x.item);

/**
 * The rotations that came BEFORE this one, most recent first, `n` of them —
 * the exclusion window (§4.1 step 1). Ordered by `startUtc`, not by id, so a
 * hand-made rotation with an odd id still counts as recent.
 */
export function recentRotations(rotations: CatalogRow[], rotation: CatalogRow, n: number): CatalogRow[] {
  const start = parseInstant(rotation.data.startUtc);
  if (start === null || Number.isNaN(start) || n <= 0) return [];
  return rotations
    .filter((r) => r.rowId !== rotation.rowId)
    .map((r) => ({ r, s: parseInstant(r.data.startUtc) }))
    .filter((x): x is { r: CatalogRow; s: number } => x.s !== null && !Number.isNaN(x.s) && x.s < start)
    .sort((a, b) => b.s - a.s)
    .slice(0, n)
    .map((x) => x.r);
}

/**
 * Every ref the recent rotations listed: their `shop_catalog` rows (active or
 * not — an archived row was still on sale that week) AND their own pinned
 * lists, so an unmaterialized planned week already counts. Superset of the
 * spec's "shop rows tagged with those rotationIds", deliberately.
 */
export function recentlyFeaturedRefs(recent: CatalogRow[], shop: CatalogRow[]): Set<string> {
  const ids = new Set(recent.map((r) => r.rowId));
  const out = new Set<string>();
  for (const row of shop) {
    if (ids.has(text(row.data.rotationId).trim())) {
      const ref = text(row.data.refId).trim();
      if (ref) out.add(ref);
    }
  }
  for (const r of recent) {
    for (const col of ["pinnedClubs", "pinnedBalls", "pinnedCharacter"]) {
      for (const ref of parseRefList(r.data[col])) out.add(ref);
    }
  }
  return out;
}

/**
 * How many picks each rarity bucket still needs once the pins are counted.
 *
 * Pins count against the quota by TOTAL first: a bucket whose pinned count
 * already reaches the quota's total needs nothing more, whatever rarity the
 * pins are. That is what lets the plan pin a Rare character on a
 * `Legendary:1` marquee week (the exclusion window had no Legendary left) and
 * still get ONE character, not two. What remains is filled per rarity, highest
 * first, so when the pins overshoot one rarity the cut lands on Common.
 */
export function fillPlan(quota: Quota, pinnedRarities: string[]): { any: number; perRarity: Map<string, number> } {
  const budget = Math.max(0, quota.total - pinnedRarities.length);
  if (quota.any !== null && quota.perRarity.size === 0) {
    return { any: budget, perRarity: new Map() };
  }
  const pinnedCount = new Map<string, number>();
  for (const r of pinnedRarities) pinnedCount.set(r, (pinnedCount.get(r) ?? 0) + 1);
  const perRarity = new Map<string, number>();
  let left = budget;
  const rarities = Array.from(quota.perRarity.keys()).sort((a, b) => rankDesc(a) - rankDesc(b));
  for (const rarity of rarities) {
    const need = Math.max(0, (quota.perRarity.get(rarity) ?? 0) - (pinnedCount.get(rarity) ?? 0));
    const take = Math.min(need, left);
    if (take > 0) perRarity.set(rarity, take);
    left -= take;
  }
  // `Any:N` alongside rarities: whatever budget the named buckets did not use.
  return { any: quota.any !== null ? Math.max(0, left) : 0, perRarity };
}

const pick = (row: CatalogRow, pinned: boolean, ladder: Record<string, number>): LineupPick => ({
  refId: row.rowId,
  name: text(row.data.name).trim() || row.rowId,
  rarity: text(row.data.rarity).trim(),
  type: text(row.data.type).trim(),
  brand: text(row.data.brand).trim(),
  pinned,
  rpCost: ladder[text(row.data.rarity).trim()] ?? 0,
});

export function generateLineup(ctx: LineupContext): Lineup {
  const data = ctx.rotation.data;
  const rotationId = text(data.rotationId).trim() || ctx.rotation.rowId;
  const seed = rotationSeed(data);
  const errors: string[] = [];
  const warnings: LineupWarning[] = [];
  const startUtc = text(data.startUtc).trim();
  const endUtc = text(data.endUtc).trim();

  const blocked = (): Lineup => ({
    rotationId, seed, startUtc, endUtc, clubs: [], balls: [], characters: [], featured: [],
    rows: EMPTY_ROWS(), odds: [], warnings, errors, hash: "",
  });

  // ---- the row itself has to parse before anything is drawn -------------
  const startMs = parseInstant(startUtc);
  const endMs = parseInstant(endUtc);
  if (startMs === null || Number.isNaN(startMs)) errors.push(`startUtc "${startUtc}" is not a readable instant.`);
  if (endMs === null || Number.isNaN(endMs)) errors.push(`endUtc "${endUtc}" is not a readable instant.`);
  if (startMs !== null && endMs !== null && !Number.isNaN(startMs) && !Number.isNaN(endMs) && endMs <= startMs) {
    errors.push(`The window ends at or before it starts (${startUtc} → ${endUtc}).`);
  }
  const quotas = {
    clubs: parseQuota(data.clubQuota),
    balls: parseQuota(data.ballQuota),
    characters: parseQuota(data.characterQuota),
  };
  for (const [name, q] of Object.entries(quotas)) {
    if (q.error) errors.push(`${name === "clubs" ? "clubQuota" : name === "balls" ? "ballQuota" : "characterQuota"}: ${q.error}`);
  }
  const featuredCount = Math.max(0, Math.trunc(Number(text(data.gachaFeaturedCount).trim() || "0")) || 0);
  const featuredMul = Number(text(data.featuredWeightMul).trim() || "1");
  if (!Number.isFinite(featuredMul) || featuredMul <= 0) errors.push(`featuredWeightMul "${text(data.featuredWeightMul)}" must be a positive number.`);
  const excludeWeeks = Math.max(0, Math.trunc(Number(text(data.excludeWeeks).trim() || "0")) || 0);
  const basePoolId = text(data.gachaBasePoolId).trim();
  if (!basePoolId) errors.push("gachaBasePoolId is empty — the weekly pool is cloned from it.");
  if (errors.length > 0) return blocked();

  // ---- eligibility ------------------------------------------------------
  const clubsById = byRowId(ctx.clubs);
  const ballsById = byRowId(ctx.balls);
  const charsById = byRowId(ctx.characters);
  const recent = recentRotations(ctx.rotations, ctx.rotation, excludeWeeks);
  const excluded = recentlyFeaturedRefs(recent, ctx.shop);
  const recentIds = recent.map((r) => r.rowId);

  const eligibleClubs = ctx.clubs.filter((c) => c.isActive && !excluded.has(c.rowId));
  const eligibleBalls = ctx.balls.filter((b) => b.isActive && !isTrue(b.data.isDefault) && !excluded.has(b.rowId));
  const eligibleChars = ctx.characters.filter(
    (c) => c.isActive && !isTrue(c.data.starterCandidate) && !excluded.has(c.rowId)
  );

  // ---- step 0: pins, verbatim -------------------------------------------
  const resolvePins = (
    column: string,
    index: Map<string, CatalogRow>,
    label: string,
    ladder: Record<string, number>,
    extraCheck?: (row: CatalogRow) => string | null
  ): LineupPick[] => {
    const out: LineupPick[] = [];
    for (const ref of parseRefList(data[column])) {
      const row = index.get(ref);
      if (!row) {
        errors.push(`${column}: "${ref}" is not a ${label} — it does not resolve.`);
        continue;
      }
      if (!row.isActive) {
        errors.push(`${column}: "${ref}" is deactivated in ${label}s.`);
        continue;
      }
      const extra = extraCheck?.(row);
      if (extra) {
        errors.push(`${column}: ${extra}`);
        continue;
      }
      if (excluded.has(ref)) {
        warnings.push({
          bucket: column === "pinnedClubs" ? "clubs" : column === "pinnedBalls" ? "balls" : "characters",
          message: `"${ref}" is pinned but was listed within the last ${excludeWeeks} rotation(s) (${recentIds.join(", ")}).`,
        });
      }
      out.push(pick(row, true, ladder));
    }
    return out;
  };

  const clubs = resolvePins("pinnedClubs", clubsById, "club", CLUB_RP_LADDER);
  const balls = resolvePins("pinnedBalls", ballsById, "ball", BALL_RP_LADDER, (row) =>
    isTrue(row.data.isDefault) ? `"${row.rowId}" is the DEFAULT ball — every player already owns it.` : null
  );
  const characters = resolvePins("pinnedCharacter", charsById, "character", CHARACTER_RP_LADDER, (row) =>
    isTrue(row.data.starterCandidate) ? `"${row.rowId}" is an FTUE starter — starters are never listed.` : null
  );
  const pinnedFeatured: string[] = [];
  for (const ref of parseRefList(data.pinnedFeatured)) {
    const row = clubsById.get(ref);
    if (!row) errors.push(`pinnedFeatured: "${ref}" is not a club — it does not resolve.`);
    else if (!row.isActive) errors.push(`pinnedFeatured: "${ref}" is deactivated in clubs.`);
    else if (!pinnedFeatured.includes(ref)) pinnedFeatured.push(ref);
  }
  if (errors.length > 0) return blocked();

  // ---- steps 1–3: fill the remainder, seeded -----------------------------
  const rnd = mulberry32(seed);
  const taken = new Set<string>([...clubs, ...balls, ...characters].map((p) => p.refId));

  // Clubs, with the type spread: no second club of a type until every type
  // has one; a quota larger than the number of types wraps.
  const clubTypes = Array.from(new Set(ctx.clubs.map((c) => text(c.data.type).trim()).filter(Boolean))).sort();
  const typeCount = new Map<string, number>(clubTypes.map((t) => [t, 0]));
  for (const c of clubs) typeCount.set(c.type, (typeCount.get(c.type) ?? 0) + 1);
  const clubPlan = fillPlan(quotas.clubs.quota!, clubs.map((c) => c.rarity));
  const clubBuckets = Array.from(clubPlan.perRarity.entries());
  if (clubPlan.any > 0) clubBuckets.push(["Any", clubPlan.any]);
  for (const [rarity, count] of clubBuckets) {
    const candidates = shuffle(
      eligibleClubs.filter((c) => !taken.has(c.rowId) && (rarity === "Any" || text(c.data.rarity).trim() === rarity)),
      rnd
    );
    let got = 0;
    for (let i = 0; i < count; i += 1) {
      const floor = Math.min(...Array.from(typeCount.values()));
      // The least-used types first; fall through to the next count only when
      // no candidate of a least-used type is left in this bucket.
      let chosen: CatalogRow | undefined;
      for (let level = floor; chosen === undefined && level <= floor + clubTypes.length; level += 1) {
        chosen = candidates.find((c) => !taken.has(c.rowId) && (typeCount.get(text(c.data.type).trim()) ?? 0) === level);
      }
      if (!chosen) break;
      taken.add(chosen.rowId);
      const t = text(chosen.data.type).trim();
      typeCount.set(t, (typeCount.get(t) ?? 0) + 1);
      clubs.push(pick(chosen, false, CLUB_RP_LADDER));
      got += 1;
    }
    if (got < count) {
      warnings.push({ bucket: "clubs", message: `Only ${got} of ${count} ${rarity} club(s) could be filled — ${eligibleClubs.length} eligible after the ${excludeWeeks}-week exclusion.` });
    }
  }

  const fillSimple = (
    bucket: "balls" | "characters",
    quota: Quota,
    picked: LineupPick[],
    eligible: CatalogRow[],
    ladder: Record<string, number>
  ) => {
    const plan = fillPlan(quota, picked.map((p) => p.rarity));
    const buckets = Array.from(plan.perRarity.entries());
    if (plan.any > 0) buckets.push(["Any", plan.any]);
    for (const [rarity, count] of buckets) {
      const candidates = shuffle(
        eligible.filter((c) => !taken.has(c.rowId) && (rarity === "Any" || text(c.data.rarity).trim() === rarity)),
        rnd
      );
      const chosen = candidates.slice(0, count);
      for (const row of chosen) {
        taken.add(row.rowId);
        picked.push(pick(row, false, ladder));
      }
      if (chosen.length < count) {
        warnings.push({ bucket, message: `Only ${chosen.length} of ${count} ${rarity} ${bucket === "balls" ? "ball(s)" : "character(s)"} could be filled — ${eligible.length} eligible after the ${excludeWeeks}-week exclusion.` });
      }
    }
  };
  fillSimple("balls", quotas.balls.quota!, balls, eligibleBalls, BALL_RP_LADDER);
  fillSimple("characters", quotas.characters.quota!, characters, eligibleChars, CHARACTER_RP_LADDER);

  // A ref that ALSO has an active permanent listing (an untagged shop row)
  // would be on sale twice that week, at two prices. The plan pinned
  // char_mike for wk_2026_38 while shop_stocking's placeholder
  // `shop_char_mike` (150 RP) was still live — warn, do not decide.
  const permanent = new Map<string, CatalogRow>();
  for (const row of ctx.shop) {
    if (row.isActive && !text(row.data.rotationId).trim()) permanent.set(text(row.data.refId).trim(), row);
  }
  for (const [bucket, picks] of [["clubs", clubs], ["balls", balls], ["characters", characters]] as const) {
    for (const p of picks) {
      const twin = permanent.get(p.refId);
      if (twin) {
        warnings.push({
          bucket,
          message: `"${p.refId}" is also a permanent listing (${twin.rowId}, ${text(twin.data.rpCost).trim() || "?"} RP) — the week would list it twice, at two prices.`,
        });
      }
    }
  }

  // ---- featured: pins, then the week's clubs highest rarity first ----------
  const featured = pinnedFeatured.slice(0, Math.max(featuredCount, pinnedFeatured.length));
  for (const c of sortRarityDesc(clubs)) {
    if (featured.length >= featuredCount) break;
    if (!featured.includes(c.refId)) featured.push(c.refId);
  }
  if (featured.length < featuredCount) {
    warnings.push({ bucket: "featured", message: `Only ${featured.length} of ${featuredCount} featured club(s) — the lineup has no more clubs to feature.` });
  }

  // ---- step 4: the rows ---------------------------------------------------
  const rows = EMPTY_ROWS();
  const poolId = `pool_${rotationId}`;
  const bannerId = `banner_${rotationId}`;

  // shop_catalog — clubs, balls, then the character, each bucket highest
  // rarity first; sortOrder = 100 × rarity rank (Supreme 0 … Common 500) +
  // running index, so the hero items lead the lineup and every row is unique.
  const shopMinBuild = (ref: CatalogRow): number => Math.max(SHOP_CATEGORY_STRICT_BUILD, ref.minBuild);
  const indexByRarity = new Map<string, number>();
  const shopRow = (category: "club" | "ball" | "character", p: LineupPick, ref: CatalogRow): OutRow => {
    const idx = indexByRarity.get(p.rarity) ?? 0;
    indexByRarity.set(p.rarity, idx + 1);
    return {
      catalog: "shop_catalog",
      rowId: `shop_${rotationId}_${p.refId}`,
      minBuild: shopMinBuild(ref),
      isActive: true,
      data: {
        entryId: `shop_${rotationId}_${p.refId}`,
        category,
        refId: p.refId,
        rpCost: String(p.rpCost),
        saleRpCost: "",
        sortOrder: String(100 * rankDesc(p.rarity) + idx),
        popular: "false",
        offer: "false",
        rarity: p.rarity,
        startAt: startUtc,
        endAt: endUtc,
        saleStartAt: "",
        saleEndAt: "",
        // Blank on every category — see the ladder note: the server delivers 1.
        quantity: "",
        rotationId,
      },
    };
  };
  for (const p of sortRarityDesc(clubs)) rows.shop_catalog.push(shopRow("club", p, clubsById.get(p.refId)!));
  for (const p of sortRarityDesc(balls)) rows.shop_catalog.push(shopRow("ball", p, ballsById.get(p.refId)!));
  for (const p of sortRarityDesc(characters)) rows.shop_catalog.push(shopRow("character", p, charsById.get(p.refId)!));

  // gacha_rates — the base pool's table, re-keyed.
  const baseRates = ctx.gachaRates.filter((r) => r.isActive && text(r.data.poolId).trim() === basePoolId);
  if (baseRates.length === 0) {
    warnings.push({ bucket: "pool", message: `Base pool "${basePoolId}" has no active rate rows — the weekly banner would have no odds. Publish would refuse it.` });
  }
  for (const r of baseRates) {
    const rarity = text(r.data.rarity).trim();
    const id = `${poolId}_${rarity.toLowerCase()}`;
    rows.gacha_rates.push({
      catalog: "gacha_rates", rowId: id, minBuild: r.minBuild, isActive: true,
      data: { id, poolId, rarity, rateBp: text(r.data.rateBp).trim() },
    });
  }

  // gacha_pools — every ACTIVE base entry copied (featured reset: the weekly
  // banner's featured rows are the week's, not the base pool's), then the
  // featured clubs inserted or re-weighted ×featuredWeightMul.
  const baseEntries = ctx.gachaPools.filter((r) => r.isActive && text(r.data.poolId).trim() === basePoolId);
  if (baseEntries.length === 0) {
    warnings.push({ bucket: "pool", message: `Base pool "${basePoolId}" has no active entries — a pull would pay nothing. Publish would refuse it.` });
  }
  const usedPoolIds = new Set<string>();
  const poolRowId = (refId: string): string => {
    let id = `p${rotationId}_${refId}`;
    for (let n = 2; usedPoolIds.has(id); n += 1) id = `p${rotationId}_${refId}_${n}`;
    usedPoolIds.add(id);
    return id;
  };
  const dupeRpByRarity = new Map<string, string>();
  const clubWeightByRarity = new Map<string, number>();
  const allDupe: number[] = [];
  for (const e of baseEntries) {
    const rarity = text(e.data.rarity).trim();
    const dupe = text(e.data.dupeRp).trim();
    if (dupe !== "" && !dupeRpByRarity.has(rarity)) dupeRpByRarity.set(rarity, dupe);
    if (dupe !== "" && Number.isFinite(Number(dupe))) allDupe.push(Number(dupe));
    if (text(e.data.kind).trim() === "club") {
      const w = Number(text(e.data.weight).trim());
      if (Number.isFinite(w)) clubWeightByRarity.set(rarity, Math.max(clubWeightByRarity.get(rarity) ?? 0, w));
    }
  }
  const medianDupe = (): string => {
    if (allDupe.length === 0) return "0";
    const s = allDupe.slice().sort((a, b) => a - b);
    return String(s[Math.floor((s.length - 1) / 2)]);
  };
  const featuredSet = new Set(featured);
  const featuredDone = new Set<string>();
  for (const e of baseEntries) {
    const refId = text(e.data.refId).trim();
    const kind = text(e.data.kind).trim();
    const isFeatured = kind === "club" && featuredSet.has(refId) && !featuredDone.has(refId);
    const baseWeight = Number(text(e.data.weight).trim());
    const weight = isFeatured ? Math.max(1, Math.round((Number.isFinite(baseWeight) ? baseWeight : 100) * featuredMul)) : text(e.data.weight).trim();
    if (isFeatured) featuredDone.add(refId);
    const id = poolRowId(refId);
    rows.gacha_pools.push({
      catalog: "gacha_pools", rowId: id, minBuild: e.minBuild, isActive: true,
      data: {
        id, poolId, kind, refId,
        rarity: text(e.data.rarity).trim(),
        weight: String(weight),
        quantity: text(e.data.quantity).trim() || "1",
        dupeRp: text(e.data.dupeRp).trim(),
        featured: isFeatured ? "true" : "false",
      },
    });
  }
  for (const refId of featured) {
    if (featuredDone.has(refId)) continue;
    const club = clubsById.get(refId)!;
    const rarity = text(club.data.rarity).trim();
    const base = clubWeightByRarity.get(rarity) ?? Math.max(0, ...Array.from(clubWeightByRarity.values())) ?? 100;
    const id = poolRowId(refId);
    rows.gacha_pools.push({
      catalog: "gacha_pools", rowId: id, minBuild: Math.max(0, club.minBuild), isActive: true,
      data: {
        id, poolId, kind: "club", refId, rarity,
        weight: String(Math.max(1, Math.round((base > 0 ? base : 100) * featuredMul))),
        quantity: "1",
        dupeRp: dupeRpByRarity.get(rarity) ?? medianDupe(),
        featured: "true",
      },
    });
    featuredDone.add(refId);
  }

  // gacha_banners — one, cloned from the base pool's first active banner.
  const baseBanner = ctx.gachaBanners
    .filter((b) => b.isActive && isTrue(b.data.active) && text(b.data.poolId).trim() === basePoolId)
    .map((b) => ({ b, sort: Number(text(b.data.sortOrder).trim()) }))
    .sort((a, b) => (Number.isFinite(a.sort) ? a.sort : 1e9) - (Number.isFinite(b.sort) ? b.sort : 1e9) || a.b.rowId.localeCompare(b.b.rowId))
    .map((x) => x.b)[0];
  if (!baseBanner) {
    warnings.push({ bucket: "banner", message: `No active banner rolls "${basePoolId}" — cost, ticket and pity were left at the standard defaults (50 / 450 / ticket 0 / no pity).` });
  }
  const bd = baseBanner?.data ?? {};
  // The art is the one thing on this row the generator does not own — an
  // operator uploads it on the banner row AFTER materializing (installed
  // builds withhold a weekly banner that has no artUrl). So a re-draw carries
  // the existing draft's artUrl forward instead of blanking it; everything
  // else is re-derived from the rotation row and the base banner as before.
  const ownBanner = ctx.gachaBanners.find((b) => b.rowId === bannerId);
  const artUrl = text(ownBanner?.data.artUrl).trim();
  const nameEn = text(data.nameEn).trim() || ROTATION_DEFAULTS.nameEn;
  const nameJa = text(data.nameJa).trim() || ROTATION_DEFAULTS.nameJa;
  rows.gacha_banners.push({
    catalog: "gacha_banners", rowId: bannerId, minBuild: baseBanner?.minBuild ?? 0, isActive: true,
    data: {
      bannerId,
      nameKey: nameEn,
      artSprite: WEEKLY_BANNER_ART,
      costX1: text(bd.costX1).trim() || "50",
      costX10: text(bd.costX10).trim() || "450",
      endUtc,
      rulesUrl: "",
      sortOrder: "0",
      active: "true",
      startUtc,
      poolId,
      ticketType: text(bd.ticketType).trim() || "0",
      pityThreshold: text(bd.pityThreshold).trim(),
      pityMinRarity: text(bd.pityMinRarity).trim(),
      guaranteeMinRarityX10: text(bd.guaranteeMinRarityX10).trim(),
      maxPullsPerPlayer: "",
      artUrl,
      nameEn,
      nameJa,
      taglineEn: "Featured this week",
      taglineJa: "今週のピックアップ",
      featuredRefIds: featured.join(";"),
      rotationId,
      pityGroup: WEEKLY_PITY_GROUP,
    },
  });

  // rotations — the row itself, materialized now.
  const rotationData: Record<string, string> = {};
  for (const col of ROTATION_COLUMNS) rotationData[col] = text(data[col]);
  rotationData.rotationId = rotationId;
  rotationData.seed = String(seed);
  rotationData.materializedAt = ctx.now;
  rows.rotations.push({
    catalog: "rotations", rowId: rotationId, minBuild: ctx.rotation.minBuild, isActive: ctx.rotation.isActive,
    data: rotationData,
  });

  // ---- odds: what the RATES & RULES modal will show for this banner --------
  const rateRows: RateRow[] = rows.gacha_rates.map((r) => ({
    poolId, rarity: r.data.rarity!, rateBp: Number(r.data.rateBp) || 0,
  }));
  const poolRows: PoolEntry[] = rows.gacha_pools.map((r) => ({
    id: r.rowId, poolId, kind: r.data.kind!, refId: r.data.refId!, rarity: r.data.rarity!,
    weight: Number(r.data.weight) || 0, quantity: Number(r.data.quantity) || 1,
    dupeRp: Number(r.data.dupeRp) || 0, featured: r.data.featured === "true",
  }));
  const odds = effectiveOdds(rateRows, poolRows);

  const lineup: Lineup = {
    rotationId, seed, startUtc, endUtc,
    clubs, balls, characters, featured, rows, odds, warnings, errors, hash: "",
  };
  lineup.hash = lineupHash(lineup);
  return lineup;
}

/**
 * Canonical JSON of every generated row (keys sorted), for hashing and
 * diffing. `materializedAt` is BLANKED first: it is the clock, not the
 * lineup, and two previews of one seed a minute apart must hash the same.
 */
export function lineupCanonical(lineup: Lineup): string {
  const rows: OutRow[] = [];
  for (const catalog of ROTATION_PUBLISH_ORDER) rows.push(...lineup.rows[catalog]);
  return JSON.stringify(
    rows.map((r) => ({
      catalog: r.catalog,
      rowId: r.rowId,
      minBuild: r.minBuild,
      isActive: r.isActive,
      data: Object.fromEntries(
        Object.keys(r.data).sort().map((k) => [k, k === "materializedAt" ? "" : r.data[k]])
      ),
    }))
  );
}

/** FNV-1a (hex) over `lineupCanonical` — same seed ⇒ same hash, by construction. */
export const lineupHash = (lineup: Lineup): string =>
  fnv1a32(lineupCanonical(lineup)).toString(16).padStart(8, "0");

/** Every row, in publish order — what MATERIALIZE writes. */
export function lineupRows(lineup: Lineup): OutRow[] {
  const out: OutRow[] = [];
  for (const catalog of ROTATION_PUBLISH_ORDER) out.push(...lineup.rows[catalog]);
  return out;
}

// ---------------------------------------------------------------------------
// Materialize (§4.2) and publish (§4.3) — sequenced here, performed by the caller
// ---------------------------------------------------------------------------

export type UpsertFn = (row: OutRow) => Promise<void>;

export interface RotationDrafts {
  shop_catalog: CatalogRow[];
  gacha_banners: CatalogRow[];
  gacha_pools: CatalogRow[];
  gacha_rates: CatalogRow[];
}

export interface MaterializeCounts extends Record<RotationCatalog, number> {
  /** Rows of THIS rotation from an earlier materialize that the new lineup no
   *  longer contains — deactivated, never deleted (I6). */
  deactivated: number;
}

/**
 * The existing draft rows THIS rotation owns — shop rows and the banner by
 * `rotationId`, pool entries and rates by the generated `pool_<rotationId>`.
 * Never any other rotation's; never a permanent listing.
 */
export function rowsOfRotation(rotationId: string, drafts: RotationDrafts): OutRow[] {
  return archiveRows([{ rowId: rotationId, data: {}, minBuild: 0, isActive: true }], drafts).map((r) => ({ ...r, isActive: true }));
}

/**
 * Write every generated row, one `upsertDraftRow` at a time, in publish order.
 * Regenerating is an EDIT (`expectNew` false at the call site): the ids are
 * deterministic in the refs, so a second MATERIALIZE overwrites the rows it
 * produces again — and a row the previous materialize wrote for THIS rotation
 * that the new lineup no longer contains (a different seed drew a different
 * club, so `shop_<rotation>_<ref>` is a different id) is DEACTIVATED, so a
 * regenerated week never lists both lineups. Rows of other rotations are never
 * touched: they are not in `rowsOfRotation`'s selection. Returns the count per
 * catalog plus how many stale rows were switched off.
 */
export async function materializeLineup(
  lineup: Lineup,
  upsert: UpsertFn,
  existing?: RotationDrafts
): Promise<MaterializeCounts> {
  if (lineup.errors.length > 0) throw new Error(`Lineup is blocked: ${lineup.errors.join(" ")}`);
  const counts: MaterializeCounts = { gacha_rates: 0, gacha_pools: 0, gacha_banners: 0, shop_catalog: 0, rotations: 0, deactivated: 0 };
  const fresh = new Set(lineupRows(lineup).map((r) => `${r.catalog}/${r.rowId}`));
  if (existing) {
    for (const stale of rowsOfRotation(lineup.rotationId, existing)) {
      if (fresh.has(`${stale.catalog}/${stale.rowId}`)) continue;
      await upsert({ ...stale, isActive: false });
      counts.deactivated += 1;
    }
  }
  for (const row of lineupRows(lineup)) {
    await upsert(row);
    counts[row.catalog] += 1;
  }
  return counts;
}

export interface PublishStep {
  catalog: RotationCatalog;
  ok: boolean;
  message: string;
  version?: number;
}
export interface PublishChainResult {
  ok: boolean;
  steps: PublishStep[];
  /** The catalog the chain stopped at, when it did. */
  stoppedAt: RotationCatalog | null;
}
export type PublishFn = (catalog: RotationCatalog) => Promise<{ ok: boolean; message: string; version?: number }>;

/**
 * The five catalogs, dependencies first, STOP ON FIRST FAILURE. What is
 * already published stays published — the drawer never half-publishes a
 * catalog, and this never continues past one that refused.
 */
export async function publishRotationChain(publish: PublishFn): Promise<PublishChainResult> {
  const steps: PublishStep[] = [];
  for (const catalog of ROTATION_PUBLISH_ORDER) {
    const res = await publish(catalog);
    steps.push({ catalog, ok: res.ok, message: res.message, version: res.version });
    if (!res.ok) return { ok: false, steps, stoppedAt: catalog };
  }
  return { ok: true, steps, stoppedAt: null };
}

// ---------------------------------------------------------------------------
// Calendar (§4.4)
// ---------------------------------------------------------------------------

export type CalendarState = "LIVE" | "SCHEDULED" | "GENERATED" | "NOT_GENERATED" | "ENDED" | "MISSING";

export interface CalendarCell {
  /** `wk_YYYY_WW` of this Monday. */
  weekId: string;
  mondayUtc: string;
  /** The rotation row occupying the week (by id, else by window), if any. */
  rotation: CatalogRow | null;
  state: CalendarState;
}

/**
 * The state of one rotation row on the clock:
 *   ENDED          endUtc ≤ now
 *   NOT_GENERATED  never materialized (amber — needs the workbench)
 *   GENERATED      materialized, but the rotation row is not published yet
 *   LIVE           materialized + published, window open
 *   SCHEDULED      materialized + published, window in the future
 */
export function rotationState(row: CatalogRow | null, now: number, unpublished: boolean): CalendarState {
  if (!row) return "MISSING";
  const end = parseInstant(row.data.endUtc);
  const start = parseInstant(row.data.startUtc);
  if (end !== null && !Number.isNaN(end) && end <= now) return "ENDED";
  if (!text(row.data.materializedAt).trim()) return "NOT_GENERATED";
  if (unpublished) return "GENERATED";
  if (start !== null && !Number.isNaN(start) && start <= now) return "LIVE";
  return "SCHEDULED";
}

/** The next `count` Mondays from the one containing `now`, each with its rotation. */
export function calendarWeeks(
  rotations: CatalogRow[],
  now: number,
  unpublishedIds: Set<string>,
  count = 8
): CalendarCell[] {
  const byId = byRowId(rotations);
  const first = mondayUtc(now);
  const cells: CalendarCell[] = [];
  for (let i = 0; i < count; i += 1) {
    const monday = first + i * 7 * 86_400_000;
    const weekId = isoWeekId(monday);
    let row = byId.get(weekId) ?? null;
    if (!row) {
      row =
        rotations.find((r) => {
          const s = parseInstant(r.data.startUtc);
          const e = parseInstant(r.data.endUtc);
          return s !== null && e !== null && !Number.isNaN(s) && !Number.isNaN(e) && s <= monday && monday < e;
        }) ?? null;
    }
    // The clock for a cell is the cell's own Monday, not `now`: a future week
    // whose row is published reads SCHEDULED, and the current week reads LIVE.
    const state = rotationState(row, now, row ? unpublishedIds.has(row.rowId) : false);
    cells.push({ weekId, mondayUtc: isoInstant(monday), rotation: row, state });
  }
  return cells;
}

/**
 * The draft `rotations` row the calendar creates for a missing week (§4.4):
 * that Monday → next Monday, defaults copied from the LATEST rotation (by
 * startUtc), pins blank, seed recomputed from the new id.
 */
export function newRotationRow(weekId: string, mondayMs: number, rotations: CatalogRow[]): Record<string, string> {
  const latest = rotations
    .map((r) => ({ r, s: parseInstant(r.data.startUtc) }))
    .filter((x): x is { r: CatalogRow; s: number } => x.s !== null && !Number.isNaN(x.s))
    .sort((a, b) => b.s - a.s)[0]?.r;
  const copy = (col: keyof typeof ROTATION_DEFAULTS): string => text(latest?.data[col]).trim() || ROTATION_DEFAULTS[col];
  return {
    rotationId: weekId,
    startUtc: isoInstant(mondayMs),
    endUtc: isoInstant(mondayMs + 7 * 86_400_000),
    nameEn: ROTATION_DEFAULTS.nameEn,
    nameJa: ROTATION_DEFAULTS.nameJa,
    clubQuota: copy("clubQuota"),
    ballQuota: copy("ballQuota"),
    characterQuota: copy("characterQuota"),
    gachaFeaturedCount: copy("gachaFeaturedCount"),
    gachaBasePoolId: copy("gachaBasePoolId"),
    featuredWeightMul: copy("featuredWeightMul"),
    excludeWeeks: copy("excludeWeeks"),
    seed: String(fnv1a32(weekId)),
    pinnedClubs: "",
    pinnedBalls: "",
    pinnedCharacter: "",
    pinnedFeatured: "",
    materializedAt: "",
  };
}

// ---------------------------------------------------------------------------
// Archive (§4.4) — deactivate, never delete (I6)
// ---------------------------------------------------------------------------

export const ARCHIVE_GRACE_MS = 7 * 86_400_000;

/** Rotations whose `endUtc` is more than 7 days ago. */
export function archivableRotations(rotations: CatalogRow[], now: number): CatalogRow[] {
  return rotations.filter((r) => {
    const end = parseInstant(r.data.endUtc);
    return end !== null && !Number.isNaN(end) && end < now - ARCHIVE_GRACE_MS;
  });
}

/**
 * The ACTIVE draft rows the ended rotations own — shop rows and banners by
 * `rotationId`, pools and rates by the generated `pool_<rotationId>` id (those
 * two catalogs carry no rotationId column). Each comes back as the same row
 * with `isActive: false`, ready for `upsertDraftRow`.
 */
export function archiveRows(
  ended: CatalogRow[],
  drafts: { shop_catalog: CatalogRow[]; gacha_banners: CatalogRow[]; gacha_pools: CatalogRow[]; gacha_rates: CatalogRow[] }
): OutRow[] {
  const ids = new Set(ended.map((r) => r.rowId));
  const poolIds = new Set(ended.map((r) => `pool_${r.rowId}`));
  const out: OutRow[] = [];
  const push = (catalog: RotationCatalog, rows: CatalogRow[], owned: (r: CatalogRow) => boolean) => {
    for (const r of rows) {
      if (r.isActive && owned(r)) out.push({ catalog, rowId: r.rowId, data: stringData(r.data), minBuild: r.minBuild, isActive: false });
    }
  };
  push("gacha_rates", drafts.gacha_rates, (r) => poolIds.has(text(r.data.poolId).trim()));
  push("gacha_pools", drafts.gacha_pools, (r) => poolIds.has(text(r.data.poolId).trim()));
  push("gacha_banners", drafts.gacha_banners, (r) => ids.has(text(r.data.rotationId).trim()));
  push("shop_catalog", drafts.shop_catalog, (r) => ids.has(text(r.data.rotationId).trim()));
  return out;
}
