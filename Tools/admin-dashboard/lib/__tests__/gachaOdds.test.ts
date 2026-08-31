import { describe, expect, it } from "vitest";
import {
  effectiveOdds,
  mulberry32,
  rarityRank,
  simulate,
  totalOdds,
  type BannerRoll,
  type PoolEntry,
  type RateRow,
} from "@/lib/gachaOdds";

/**
 * `lib/gachaOdds.ts` is the REFERENCE the server roll is checked against
 * (gacha_admin_catalogs §5.3, plan §5 step 7). It decides what an operator is
 * told a pull will do, and in the next spec it decides whether the plpgsql
 * function agrees — so it is the one module here whose numbers have to be
 * pinned rather than eyeballed.
 *
 * The fixture is the SEED POOL from SPEC §2.2 / §2.3, unmodified, because the
 * acceptance criteria are stated about that pool.
 */

const RATES: RateRow[] = [
  { poolId: "pool_standard_club1", rarity: "Common", rateBp: 5500 },
  { poolId: "pool_standard_club1", rarity: "Uncommon", rateBp: 2500 },
  { poolId: "pool_standard_club1", rarity: "Rare", rateBp: 1200 },
  { poolId: "pool_standard_club1", rarity: "Mythic", rateBp: 550 },
  { poolId: "pool_standard_club1", rarity: "Legendary", rateBp: 200 },
  { poolId: "pool_standard_club1", rarity: "Supreme", rateBp: 50 },
];

const entry = (
  id: string,
  rarity: string,
  weight: number,
  over: Partial<PoolEntry> = {}
): PoolEntry => ({
  id,
  poolId: "pool_standard_club1",
  kind: "club",
  refId: id,
  rarity,
  weight,
  quantity: 1,
  dupeRp: 0,
  featured: false,
  ...over,
});

const POOL: PoolEntry[] = [
  entry("psc1_driver_gf", "Common", 100),
  entry("psc1_wood_gf", "Common", 100),
  entry("psc1_ball_golfin", "Common", 60, { kind: "ball", quantity: 3 }),
  entry("psc1_iron9_klyro", "Uncommon", 100),
  entry("psc1_repairkit_common", "Common", 40, { kind: "item" }),
  entry("psc1_iron7_mireo", "Rare", 100),
  entry("psc1_repairkit_rare", "Rare", 40, { kind: "item" }),
  entry("psc1_awedge_fyloe", "Mythic", 100),
  entry("psc1_repairkit_mythic", "Mythic", 30, { kind: "item" }),
  entry("psc1_pwedge_royal", "Legendary", 100, { featured: true }),
  entry("psc1_putter_golfinx", "Supreme", 100, { featured: true }),
];

/** banner_standard_club1 — pity Legendary at 50, x10 guarantees Rare. */
const WITH_PITY: BannerRoll = {
  poolId: "pool_standard_club1",
  pityThreshold: 50,
  pityMinRarity: "Legendary",
  guaranteeMinRarityX10: "Rare",
};

/** banner_test_a — the NO-PITY acceptance case for decision 2. */
const NO_PITY: BannerRoll = {
  poolId: "pool_standard_club1",
  pityThreshold: 0,
  pityMinRarity: "",
  guaranteeMinRarityX10: "",
};

const SEED = 20260831;

describe("effectiveOdds", () => {
  it("sums to 1 for a pool whose rates sum to 10000 and whose rarities all have entries", () => {
    expect(totalOdds(effectiveOdds(RATES, POOL))).toBeCloseTo(1, 12);
  });

  it("splits a rarity's rate across its entries by weight", () => {
    const odds = effectiveOdds(RATES, POOL);
    const byId = new Map(odds.map((o) => [o.entry.id, o.p]));
    // Common is 5500 bp over weights 100 + 100 + 60 + 40 = 300.
    expect(byId.get("psc1_driver_gf")).toBeCloseTo(0.55 * (100 / 300), 12);
    expect(byId.get("psc1_ball_golfin")).toBeCloseTo(0.55 * (60 / 300), 12);
    expect(byId.get("psc1_repairkit_common")).toBeCloseTo(0.55 * (40 / 300), 12);
    // A rarity with ONE entry gets the whole rate.
    expect(byId.get("psc1_putter_golfinx")).toBeCloseTo(0.005, 12);
  });

  it("gives an entry in a rarity with rateBp 0 a probability of 0 rather than dropping it", () => {
    const shelved = RATES.map((r) =>
      r.rarity === "Supreme" ? { ...r, rateBp: 0 } : r.rarity === "Common" ? { ...r, rateBp: 5550 } : r
    );
    const odds = effectiveOdds(shelved, POOL);
    const supreme = odds.find((o) => o.entry.id === "psc1_putter_golfinx");
    expect(supreme).toBeDefined();
    expect(supreme?.p).toBe(0);
    // The pool as a whole still adds up — the shelved rate went to Common.
    expect(totalOdds(odds)).toBeCloseTo(1, 12);
  });

  it("does not divide by zero on a rarity with no rate row", () => {
    const odds = effectiveOdds([], POOL);
    expect(totalOdds(odds)).toBe(0);
    expect(odds.every((o) => o.p === 0)).toBe(true);
  });
});

describe("mulberry32", () => {
  it("is deterministic for a seed and returns values in [0, 1)", () => {
    const a = mulberry32(SEED);
    const b = mulberry32(SEED);
    for (let i = 0; i < 100; i += 1) {
      const value = a();
      expect(value).toBe(b());
      expect(value).toBeGreaterThanOrEqual(0);
      expect(value).toBeLessThan(1);
    }
  });

  it("gives different streams for different seeds", () => {
    expect(mulberry32(1)()).not.toBe(mulberry32(2)());
  });
});

describe("simulate", () => {
  it("is deterministic for a fixed seed", () => {
    const a = simulate(RATES, POOL, WITH_PITY, 10000, SEED);
    const b = simulate(RATES, POOL, WITH_PITY, 10000, SEED);
    expect(a).toEqual(b);
  });

  it("resolves every pull on a valid pool", () => {
    const result = simulate(RATES, POOL, WITH_PITY, 10000, SEED);
    expect(result.empty).toBe(0);
    const total = Object.values(result.observed).reduce((sum, n) => sum + n, 0);
    expect(total).toBe(10000);
  });

  it("lands within 1.5 points of the published rates at 10 000 pulls", () => {
    // ⚠️ THE DELTA IS NOT NOISE. Pity and the x10 guarantee move the
    // distribution ON PURPOSE — every pity hit converts a would-be Common into
    // a Legendary or better. 1.5 points is the acceptance's threshold for
    // "the published table still describes what players get".
    //
    // AVERAGED OVER FIVE SEEDS, not measured on one (gacha_ops_polish §4b). At
    // 10 000 pulls a single seed's Common share swings between −0.7 and −1.7
    // points, so a one-seed assertion at 1.5 was a coin toss dressed as a gate:
    // §4b changed how many random draws a guaranteed block consumes — which
    // changes NO distribution — and that alone was enough to move the fixture's
    // seed from −1.50 to −1.69 and fail it. The mean is the number the threshold
    // was always describing.
    const seeds = [SEED, 1234, 7, 99, 555];
    for (const rate of RATES) {
      const deltas = seeds.map((seed) => {
        const result = simulate(RATES, POOL, WITH_PITY, 10000, seed);
        return (result.observed[rate.rarity] ?? 0) / result.pulls - rate.rateBp / 10000;
      });
      const mean = deltas.reduce((sum, d) => sum + d, 0) / deltas.length;
      expect(Math.abs(mean)).toBeLessThanOrEqual(0.015);
    }
  });

  it("hits pity on a banner that has one and never on a banner that does not", () => {
    expect(simulate(RATES, POOL, WITH_PITY, 10000, SEED).pityHits).toBeGreaterThan(0);
    expect(simulate(RATES, POOL, NO_PITY, 10000, SEED).pityHits).toBe(0);
  });

  it("treats a blank pityMinRarity as no pity even when a threshold is set", () => {
    // Decision 2 is that a half-filled banner never silently acquires a pity.
    const halfFilled: BannerRoll = { ...NO_PITY, pityThreshold: 10, pityMinRarity: "" };
    expect(simulate(RATES, POOL, halfFilled, 1000, SEED).pityHits).toBe(0);
  });

  it("forces the rarity at the threshold", () => {
    // A pity of 1 on a Supreme floor: after ANY pull below Supreme the next one
    // is forced, so more than half the pulls must be Supreme — a number no
    // 0.5 % rate could produce by luck.
    const brutal: BannerRoll = {
      poolId: "pool_standard_club1",
      pityThreshold: 1,
      pityMinRarity: "Supreme",
      guaranteeMinRarityX10: "",
    };
    const result = simulate(RATES, POOL, brutal, 2000, SEED);
    expect(result.observed.Supreme ?? 0).toBeGreaterThan(900);
    expect(result.pityHits).toBeGreaterThan(900);
  });

  it("forces the THRESHOLD-th pull, not the one after it", () => {
    // The off-by-one this pins is the one gacha_server_pull §3 had to correct:
    // `counter >= threshold` fires one pull LATE. With a threshold of 1 and a
    // Supreme floor, EVERY pull must be forced — the counter starts at 0 and
    // 0 + 1 >= 1 — so a run of N produces N pity hits and N Supremes. Under the
    // old rule the first pull of every reset would slip through unforced, and
    // `pityHits` would come back materially below N.
    //
    // This is the number the SPEC §7 parity harness compares against
    // `golfin_gacha_pull()`, so a drift here is a drift between the two
    // implementations of the roll.
    const everyPull: BannerRoll = {
      poolId: "pool_standard_club1",
      pityThreshold: 1,
      pityMinRarity: "Supreme",
      guaranteeMinRarityX10: "",
    };
    const result = simulate(RATES, POOL, everyPull, 500, SEED);
    expect(result.pityHits).toBe(500);
    expect(result.observed.Supreme ?? 0).toBe(500);
  });

  it("allows exactly threshold - 1 sub-minimum prizes before forcing", () => {
    // Threshold 2 on a Supreme floor. Slot 0 is unforced (0 + 1 >= 2 is false);
    // if it misses, slot 1 is forced (1 + 1 >= 2). So no more than one
    // consecutive non-Supreme can ever appear, and at least half the pulls are
    // Supreme however unlucky the seed.
    const two: BannerRoll = {
      poolId: "pool_standard_club1",
      pityThreshold: 2,
      pityMinRarity: "Supreme",
      guaranteeMinRarityX10: "",
    };
    const result = simulate(RATES, POOL, two, 1000, SEED);
    expect(result.observed.Supreme ?? 0).toBeGreaterThanOrEqual(500);
    expect(result.pityHits).toBeGreaterThan(450);
  });

  it("fires the x10 guarantee only on blocks where all TEN slots missed the rarity", () => {
    // gacha_ops_polish §4b. This asserted ~0.8^9 (the first NINE slots) because
    // that is what the old implementation did; `golfin_gacha_pull()` (B §3)
    // rolls slot 9 normally and re-rolls it only when all TEN missed, which is
    // 0.8^10 ≈ 0.107. The prizes are the same either way — the FLAG is not, and
    // the flag is what the admin shows and what the pull log records.
    const guaranteeOnly: BannerRoll = {
      poolId: "pool_standard_club1",
      pityThreshold: 0,
      pityMinRarity: "",
      guaranteeMinRarityX10: "Rare",
    };
    const result = simulate(RATES, POOL, guaranteeOnly, 10000, SEED);
    // 1000 blocks of ten. P(no Rare+ in all TEN) = 0.8^10 ≈ 0.107, so roughly
    // 107 blocks — and the window excludes the 0.8^9 ≈ 134 the old rule gave.
    expect(result.guaranteeHits).toBeGreaterThan(70);
    expect(result.guaranteeHits).toBeLessThan(145);
    // And it must lift the Rare-or-better share above the published 20 %.
    const rarePlus = Object.entries(result.observed)
      .filter(([rarity]) => rarityRank(rarity) >= rarityRank("Rare"))
      .reduce((sum, [, n]) => sum + n, 0);
    expect(rarePlus / result.pulls).toBeGreaterThan(0.2);
  });

  it("never fires the guarantee on a block whose slot 9 reached the rarity by luck", () => {
    // The sharp edge of §4b, stated as an invariant rather than as a rate: a
    // block that ends on a Rare+ is a block the guarantee did NOT rescue, so the
    // number of flagged blocks can never exceed the number of blocks whose every
    // slot would otherwise have missed. Re-deriving that from `observed` is not
    // possible (the forced slot IS a Rare), so the bound used here is the one
    // fact the old rule violated: guaranteeHits < blocks × 0.8^9.
    const guaranteeOnly: BannerRoll = {
      poolId: "pool_standard_club1",
      pityThreshold: 0,
      pityMinRarity: "",
      guaranteeMinRarityX10: "Rare",
    };
    const blocks = 2000;
    const result = simulate(RATES, POOL, guaranteeOnly, blocks * 10, SEED);
    const nineSlotRate = Math.pow(0.8, 9);   // 0.1342 — what the old code produced
    const tenSlotRate  = Math.pow(0.8, 10);  // 0.1074 — what the server produces
    const observedRate = result.guaranteeHits / blocks;
    expect(observedRate).toBeLessThan((nineSlotRate + tenSlotRate) / 2);
    expect(Math.abs(observedRate - tenSlotRate)).toBeLessThan(0.02);
  });

  it("does not carry guarantee state from one call into the next", () => {
    // The old game's "two pities coincide" bug was shared state. Running a
    // 5-pull simulation (half a block) must not change what the next call does.
    const first = simulate(RATES, POOL, WITH_PITY, 5, SEED);
    const second = simulate(RATES, POOL, WITH_PITY, 10000, SEED);
    expect(first.pulls).toBe(5);
    expect(second).toEqual(simulate(RATES, POOL, WITH_PITY, 10000, SEED));
  });

  it("reports empty pulls instead of throwing when nothing is rollable", () => {
    const result = simulate(RATES, [], NO_PITY, 10, SEED);
    expect(result.empty).toBe(10);
    expect(Object.keys(result.observed)).toHaveLength(0);
  });
});
