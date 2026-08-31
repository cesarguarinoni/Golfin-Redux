/**
 * Gacha odds — PURE, and the REFERENCE the server roll is checked against.
 *
 * No React, no Supabase, no `server-only`, no clock, no `Math.random`. This
 * module is the executable statement of "what a pull does", written once, on the
 * side where an operator can see the answer BEFORE publishing:
 *
 *   1. Pity and the x10 guarantee decide a FORCED MINIMUM RARITY for a slot.
 *   2. Otherwise the rarity is drawn from `gacha_rates` by `rateBp` (basis
 *      points, 10 000 = 100 % per pool).
 *   3. The item inside that rarity is drawn from `gacha_pools` by `weight`.
 *
 * `golfin_gacha_pull()` (gacha_server_pull, plan §5 step 7) implements the same
 * three steps in plpgsql and is the ONLY authority at runtime — nothing here
 * ever runs on a player's device or decides a real prize. What this buys is a
 * differential test: the same seed, the same rows, the same distribution. A
 * server that disagrees with this function about a published pool is a server
 * paying out something other than what the admin displayed, and that is exactly
 * the bug an honest-odds screen exists to make impossible.
 *
 * ⚠️ IF YOU CHANGE THE ROLL ORDER HERE, THE SERVER FUNCTION CHANGES TOO.
 * Rarity-then-weight is not an implementation detail: it is what makes the
 * published rarity table true regardless of how many items sit in a rarity.
 * Drawing over a flat weight across all rarities would make the rate table a
 * decoration.
 */

/** CharacterRarity ladder order — same six as `contentValidate.RARITIES`. */
export const RARITY_ORDER = [
  "Common",
  "Uncommon",
  "Rare",
  "Mythic",
  "Legendary",
  "Supreme",
] as const;

export type Rarity = (typeof RARITY_ORDER)[number];

export const isRarity = (s: string): s is Rarity =>
  (RARITY_ORDER as readonly string[]).includes(s);

/** Ladder index, or -1. Used for the "at least this rarity" comparisons. */
export const rarityRank = (s: string): number =>
  (RARITY_ORDER as readonly string[]).indexOf(s);

/** One `gacha_rates` row, already narrowed to the fields the roll uses. */
export interface RateRow {
  poolId: string;
  rarity: string;
  /** Basis points. The active rows of a pool must sum to 10 000. */
  rateBp: number;
}

/** One `gacha_pools` row, already narrowed to the fields the roll uses. */
export interface PoolEntry {
  id: string;
  poolId: string;
  kind: string;
  refId: string;
  rarity: string;
  weight: number;
  quantity: number;
  dupeRp: number;
  featured: boolean;
}

/** The banner fields that steer a roll (pity + the x10 guarantee). */
export interface BannerRoll {
  poolId: string;
  /** Blank or 0 ⇒ NO pity (decision 2). */
  pityThreshold: number;
  pityMinRarity: string;
  guaranteeMinRarityX10: string;
}

export interface EffectiveOdd {
  entry: PoolEntry;
  /** Probability in [0, 1] of this exact entry on a single unforced pull. */
  p: number;
}

/**
 * Per-entry probability of a single UNFORCED pull, in pool order.
 *
 * `rate(rarity)/10000 × weight / Σ weight(pool, rarity)` — plan §3.3, computed
 * and displayed, never stored.
 *
 * Entries whose rarity has no rate row, or `rateBp = 0`, come back with `p = 0`
 * rather than being dropped: "this prize can never be rolled" is a fact the
 * panel has to be able to show, and dropping the row would hide it. Rarities
 * with a rate but no entry are the mirror-image problem and are caught by the
 * validator (rule 9), not here.
 */
export function effectiveOdds(rates: RateRow[], pool: PoolEntry[]): EffectiveOdd[] {
  const bp = new Map<string, number>();
  for (const rate of rates) bp.set(rate.rarity, (bp.get(rate.rarity) ?? 0) + rate.rateBp);

  const weightByRarity = new Map<string, number>();
  for (const entry of pool) {
    weightByRarity.set(entry.rarity, (weightByRarity.get(entry.rarity) ?? 0) + Math.max(0, entry.weight));
  }

  return pool.map((entry) => {
    const rarityP = (bp.get(entry.rarity) ?? 0) / 10000;
    const total = weightByRarity.get(entry.rarity) ?? 0;
    const share = total > 0 ? Math.max(0, entry.weight) / total : 0;
    return { entry, p: rarityP * share };
  });
}

/** Σ of the effective odds — 1 for a pool whose rates sum to 10 000 and whose
 *  every rated rarity has at least one entry. Anything else is the shortfall. */
export const totalOdds = (odds: EffectiveOdd[]): number =>
  odds.reduce((sum, o) => sum + o.p, 0);

// ---------------------------------------------------------------------------
// Seeded PRNG
// ---------------------------------------------------------------------------

/**
 * mulberry32 — 32-bit, seeded, and deliberately NOT `Math.random`.
 *
 * A simulation an operator runs before publishing has to be reproducible: "run
 * it again and it says something else" makes the number unusable as evidence,
 * and makes the differential test against the server impossible to write.
 */
export function mulberry32(seed: number): () => number {
  let a = seed >>> 0;
  return function next(): number {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

// ---------------------------------------------------------------------------
// The roll
// ---------------------------------------------------------------------------

/** Rarities the pool can actually pay out, in ladder order, with their weight. */
function rollableRarities(rates: RateRow[], pool: PoolEntry[]): Array<[string, number]> {
  const hasEntry = new Set<string>();
  for (const entry of pool) if (entry.weight > 0) hasEntry.add(entry.rarity);
  return rates
    .filter((r) => r.rateBp > 0 && hasEntry.has(r.rarity))
    .map((r) => [r.rarity, r.rateBp] as [string, number])
    .sort((a, b) => rarityRank(a[0]) - rarityRank(b[0]));
}

/** Draw a rarity by `rateBp`, restricted to rarities at or above `minRarity`. */
function drawRarity(
  candidates: Array<[string, number]>,
  rnd: () => number,
  minRarity?: string
): string | null {
  const floor = minRarity ? rarityRank(minRarity) : -1;
  const eligible = floor < 0 ? candidates : candidates.filter(([r]) => rarityRank(r) >= floor);
  const total = eligible.reduce((sum, [, bp]) => sum + bp, 0);
  if (total <= 0) return null;

  // `rnd() * total` is in [0, total); the last bucket therefore always wins the
  // residue, so floating-point drift can never fall through to null.
  let ticket = rnd() * total;
  for (const [rarity, bp] of eligible) {
    ticket -= bp;
    if (ticket < 0) return rarity;
  }
  return eligible[eligible.length - 1]?.[0] ?? null;
}

/** Draw an entry inside one rarity, by `weight`. */
function drawEntry(pool: PoolEntry[], rarity: string, rnd: () => number): PoolEntry | null {
  const inRarity = pool.filter((e) => e.rarity === rarity && e.weight > 0);
  const total = inRarity.reduce((sum, e) => sum + e.weight, 0);
  if (total <= 0) return null;
  let ticket = rnd() * total;
  for (const entry of inRarity) {
    ticket -= entry.weight;
    if (ticket < 0) return entry;
  }
  return inRarity[inRarity.length - 1] ?? null;
}

export interface SimulateResult {
  pulls: number;
  /** Observed count per rarity, keyed by rarity name. */
  observed: Record<string, number>;
  /** Published probability per rarity (rateBp / 10000), for the same keys. */
  published: Record<string, number>;
  /** Observed count per pool entry id. */
  byEntry: Record<string, number>;
  pityHits: number;
  guaranteeHits: number;
  /** Pulls that could not be resolved to an entry — always 0 on a valid pool. */
  empty: number;
}

/**
 * Roll `n` pulls against a pool, exactly the way `golfin_gacha_pull()` will.
 *
 * `n` is a count of SINGLE pulls; the x10 guarantee is applied to each block of
 * ten, which is what a x10 is. The pity counter carries across blocks, as it
 * does per user × banner on the server.
 *
 * PITY, PRECISELY (decision 2): the `pityThreshold`-th pull is forced to at
 * least `pityMinRarity`, i.e. at most `threshold - 1` sub-minimum prizes can
 * occur in a row, and the counter resets on any pull that reaches the rarity —
 * whether it was forced there or got there by luck. A threshold of 0 or a blank
 * `pityMinRarity` means no pity; the two are treated identically, so a
 * half-filled banner never silently acquires one.
 */
export function simulate(
  rates: RateRow[],
  pool: PoolEntry[],
  banner: BannerRoll,
  n: number,
  seed: number
): SimulateResult {
  const rnd = mulberry32(seed);
  const candidates = rollableRarities(rates, pool);

  const observed: Record<string, number> = {};
  const byEntry: Record<string, number> = {};
  const published: Record<string, number> = {};
  for (const rate of rates) published[rate.rarity] = rate.rateBp / 10000;

  const pityOn =
    banner.pityThreshold > 0 && !!banner.pityMinRarity && rarityRank(banner.pityMinRarity) >= 0;
  const guaranteeOn =
    !!banner.guaranteeMinRarityX10 && rarityRank(banner.guaranteeMinRarityX10) >= 0;

  let counter = 0;
  /** Best rarity rank seen so far in the current block of ten (x10 guarantee).
   *  LOCAL to this call: a module-level counter would carry one operator's
   *  simulation into the next, which is the "two pities coincide" bug the old
   *  game shipped (plan §3.1) reintroduced in a different place. */
  let blockBest = -1;
  let pityHits = 0;
  let guaranteeHits = 0;
  let empty = 0;

  for (let i = 0; i < n; i += 1) {
    const slotInBlock = i % 10;
    if (slotInBlock === 0) blockBest = -1;

    // `counter + 1 >= threshold`, i.e. the THRESHOLD-th pull is the forced one:
    // a threshold of 3 allows at most two sub-minimum prizes in a row.
    //
    // ⚠️ CORRECTED 2026-08-31 (gacha_server_pull §3 step 1). This shipped as
    // `counter >= threshold`, which fires one pull LATE — threshold 3 forced the
    // fourth. `golfin_gacha_pull()` implements the spec's rule, and the two must
    // agree or the SPEC §7 parity harness is comparing two different algorithms.
    // The difference is invisible in a rate table (pity slots are excluded from
    // the published comparison) and visible in `pityHits`, which is exactly the
    // number the parity check reads.
    const pityForces = pityOn && counter + 1 >= banner.pityThreshold;
    // The guarantee lands on the LAST slot of a block of ten, and only when the
    // nine before it did not already produce the rarity. Firing it on slot 0
    // would make every x10 open on its best prize, which is the opposite of how
    // a guarantee reads — and would double-count against the published rates.
    const guaranteeForces =
      guaranteeOn &&
      slotInBlock === 9 &&
      blockBest < rarityRank(banner.guaranteeMinRarityX10);

    let minRarity: string | undefined;
    if (pityForces) minRarity = banner.pityMinRarity;
    else if (guaranteeForces) minRarity = banner.guaranteeMinRarityX10;

    // A forced minimum no rollable rarity can satisfy falls back to an unforced
    // draw rather than paying nothing. The validator refuses that banner
    // (rule 13), so this is the belt to that braces — never the normal path.
    const rarity = drawRarity(candidates, rnd, minRarity) ?? drawRarity(candidates, rnd);
    if (!rarity) {
      empty += 1;
      continue;
    }
    const entry = drawEntry(pool, rarity, rnd);
    if (!entry) {
      empty += 1;
      continue;
    }

    observed[rarity] = (observed[rarity] ?? 0) + 1;
    byEntry[entry.id] = (byEntry[entry.id] ?? 0) + 1;
    blockBest = Math.max(blockBest, rarityRank(rarity));

    if (pityForces) pityHits += 1;
    else if (guaranteeForces) guaranteeHits += 1;

    // The counter resets on a pull that REACHED the rarity, however it got
    // there — a pity that fires resets itself, and so does a lucky Legendary.
    if (pityOn) {
      counter = rarityRank(rarity) >= rarityRank(banner.pityMinRarity) ? 0 : counter + 1;
    }
  }

  return { pulls: n, observed, published, byEntry, pityHits, guaranteeHits, empty };
}
