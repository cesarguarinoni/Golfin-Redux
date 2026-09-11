import { describe, expect, it } from "vitest";
import {
  archivableRotations,
  archiveRows,
  calendarWeeks,
  CLUB_RP_LADDER,
  fillPlan,
  generateLineup,
  isoWeekId,
  lineupCanonical,
  lineupRows,
  materializeLineup,
  newRotationRow,
  parseQuota,
  publishRotationChain,
  recentlyFeaturedRefs,
  recentRotations,
  ROTATION_PUBLISH_ORDER,
  rotationSeed,
  rotationState,
  WEEKLY_PITY_GROUP,
  type CatalogRow,
  type Lineup,
  type LineupContext,
  type OutRow,
  type RotationCatalog,
} from "@/lib/rotation";
import { SHOP_CATEGORY_STRICT_BUILD } from "@/lib/buildGates";

/**
 * weekly_rotation_admin §6.2 — the generator, the materialize sequence and
 * the publish chain, against a synthetic catalog big enough to exercise every
 * rule: 7 club types × 6 rarities × 2 brands (84 clubs), 12 balls incl. the
 * default one, 8 characters incl. the two starters, the standard pool, its
 * rate table and one base banner.
 */

const RARITIES = ["Common", "Uncommon", "Rare", "Mythic", "Legendary", "Supreme"];
const TYPES = ["Driver", "Wood", "Iron", "A.Wedge", "P.Wedge", "S.Wedge", "Putter"];
const BRANDS = ["gf", "bogeyb"];

const row = (rowId: string, data: Record<string, unknown>, over: Partial<CatalogRow> = {}): CatalogRow => ({
  rowId, data, minBuild: 0, isActive: true, ...over,
});

const clubs: CatalogRow[] = [];
for (const type of TYPES) {
  for (const rarity of RARITIES) {
    for (const brand of BRANDS) {
      const id = `club_${type.toLowerCase().replace(".", "")}_${brand}_${rarity.toLowerCase()}`;
      clubs.push(row(id, { id, name: `${type} ${brand} ${rarity}`, type, rarity, brand }));
    }
  }
}
const balls: CatalogRow[] = [
  row("ball_golfin", { id: "ball_golfin", name: "Golfin", rarity: "Common", isDefault: "true" }),
  ...["a", "b", "c"].map((s) => row(`ball_common_${s}`, { id: `ball_common_${s}`, name: `Common ${s}`, rarity: "Common", isDefault: "false" })),
  ...["a", "b", "c"].map((s) => row(`ball_uncommon_${s}`, { id: `ball_uncommon_${s}`, name: `Uncommon ${s}`, rarity: "Uncommon", isDefault: "false" })),
  ...["a", "b", "c"].map((s) => row(`ball_rare_${s}`, { id: `ball_rare_${s}`, name: `Rare ${s}`, rarity: "Rare", isDefault: "false" })),
  row("ball_mythic_a", { id: "ball_mythic_a", name: "Mythic a", rarity: "Mythic", isDefault: "false" }),
  row("ball_legendary_a", { id: "ball_legendary_a", name: "Legendary a", rarity: "Legendary", isDefault: "false" }),
];
const characters: CatalogRow[] = [
  row("char_james", { id: "char_james", name: "James", rarity: "Common", starterCandidate: "1" }),
  row("char_olivia", { id: "char_olivia", name: "Olivia", rarity: "Common", starterCandidate: "1" }),
  row("char_mike", { id: "char_mike", name: "Mike", rarity: "Common", starterCandidate: "0" }),
  row("char_ean", { id: "char_ean", name: "Ean", rarity: "Uncommon", starterCandidate: "0" }),
  row("char_camila", { id: "char_camila", name: "Camila", rarity: "Rare", starterCandidate: "0" }),
  row("char_richard", { id: "char_richard", name: "Richard", rarity: "Mythic", starterCandidate: "0" }),
  row("char_shae", { id: "char_shae", name: "Shae", rarity: "Legendary", starterCandidate: "0" }),
  row("char_freda", { id: "char_freda", name: "Freda", rarity: "Supreme", starterCandidate: "0" }),
];
const RATE_BP: Array<[string, number]> = [
  ["Common", 5500], ["Uncommon", 2500], ["Rare", 1200], ["Mythic", 550], ["Legendary", 200], ["Supreme", 50],
];
const gachaRates: CatalogRow[] = RATE_BP.map(([rarity, bp]) =>
  row(`pool_standard_club1_${rarity.toLowerCase()}`, { id: `pool_standard_club1_${rarity.toLowerCase()}`, poolId: "pool_standard_club1", rarity, rateBp: String(bp) })
);
// One club per rarity in the base pool (weight 100), plus an item and an
// INACTIVE default-ball slot that must never be copied.
const gachaPools: CatalogRow[] = [
  ...RARITIES.map((rarity) =>
    row(`psc1_${rarity.toLowerCase()}`, {
      id: `psc1_${rarity.toLowerCase()}`, poolId: "pool_standard_club1", kind: "club",
      refId: `club_driver_gf_${rarity.toLowerCase()}`, rarity, weight: "100", quantity: "1",
      dupeRp: String(20 * (RARITIES.indexOf(rarity) + 1)), featured: rarity === "Legendary" ? "true" : "false",
    })
  ),
  row("psc1_repairkit", { id: "psc1_repairkit", poolId: "pool_standard_club1", kind: "item", refId: "repairkit_common", rarity: "Common", weight: "40", quantity: "1", dupeRp: "0", featured: "false" }),
  row("psc1_ball_golfin", { id: "psc1_ball_golfin", poolId: "pool_standard_club1", kind: "ball", refId: "ball_golfin", rarity: "Common", weight: "60", quantity: "3", dupeRp: "0", featured: "false" }, { isActive: false }),
];
const gachaBanners: CatalogRow[] = [
  row("banner_standard_club1", {
    bannerId: "banner_standard_club1", nameKey: "STANDARD CLUB 1", artSprite: "GachaBanner_StandardClub1",
    costX1: "50", costX10: "450", endUtc: "2027-01-01T00:00:00Z", rulesUrl: "", sortOrder: "1", active: "true",
    startUtc: "2026-01-01T00:00:00Z", poolId: "pool_standard_club1", ticketType: "0", pityThreshold: "50",
    pityMinRarity: "Legendary", guaranteeMinRarityX10: "Rare", maxPullsPerPlayer: "", artUrl: "",
    nameEn: "STANDARD CLUB 1", nameJa: "スタンダードクラブ 1", taglineEn: "", taglineJa: "", featuredRefIds: "",
  }),
];

const ROTATION_BASE: Record<string, string> = {
  rotationId: "wk_2026_38",
  startUtc: "2026-09-14T00:00:00Z",
  endUtc: "2026-09-21T00:00:00Z",
  nameEn: "DRIVER WEEK · BOGEYB",
  nameJa: "ドライバーウィーク · BOGEYB",
  clubQuota: "Common:3;Uncommon:2;Rare:2;Mythic:1;Legendary:1;Supreme:0",
  ballQuota: "Common:1;Uncommon:1;Rare:1",
  characterQuota: "Any:1",
  gachaFeaturedCount: "2",
  gachaBasePoolId: "pool_standard_club1",
  featuredWeightMul: "3",
  excludeWeeks: "4",
  seed: "3287013188",
  pinnedClubs: "",
  pinnedBalls: "",
  pinnedCharacter: "",
  pinnedFeatured: "",
  materializedAt: "",
};

const PINS = {
  pinnedClubs: [
    "club_driver_bogeyb_legendary", "club_wood_bogeyb_mythic", "club_iron_bogeyb_rare",
    "club_awedge_bogeyb_rare", "club_pwedge_bogeyb_uncommon", "club_swedge_bogeyb_uncommon",
    "club_putter_bogeyb_common", "club_driver_bogeyb_common", "club_wood_bogeyb_common",
  ].join(";"),
  pinnedBalls: "ball_common_a;ball_uncommon_a;ball_rare_a",
  pinnedCharacter: "char_mike",
  pinnedFeatured: "club_driver_bogeyb_legendary;club_wood_bogeyb_mythic",
};

const NOW = "2026-09-11T10:00:00Z";

function ctx(over: Partial<Record<string, string>> = {}, extra: Partial<LineupContext> = {}): LineupContext {
  const data = { ...ROTATION_BASE, ...over };
  const rotation = row(data.rotationId!, data);
  return {
    rotation,
    rotations: [rotation],
    clubs, balls, characters,
    shop: [],
    gachaRates, gachaPools, gachaBanners,
    now: NOW,
    ...extra,
  };
}

const countBy = <T>(items: T[], key: (t: T) => string): Record<string, number> => {
  const out: Record<string, number> = {};
  for (const item of items) out[key(item)] = (out[key(item)] ?? 0) + 1;
  return out;
};

// ---------------------------------------------------------------------------
// Determinism
// ---------------------------------------------------------------------------

describe("determinism", () => {
  it("the same seed twice is byte-identical, and the hash is pinned", () => {
    const a = generateLineup(ctx());
    const b = generateLineup(ctx({}, { now: "2026-09-12T08:00:00Z" }));
    expect(a.errors).toEqual([]);
    // Generated at different moments: the rows differ only in materializedAt,
    // which the canonical form blanks — so the two previews hash the same.
    expect(a.rows.rotations[0]!.data.materializedAt).not.toBe(b.rows.rotations[0]!.data.materializedAt);
    expect(lineupCanonical(a)).toBe(lineupCanonical(b));
    expect(a.hash).toBe(b.hash);
    // The hash of the unpinned wk_2026_38 lineup over this fixture. A change
    // here is a change to WHAT THE GENERATOR WRITES for a seed it already
    // wrote — update it only when that is the intent.
    expect(a.hash).toBe("8838dbcb");
  });

  it("changing the seed changes the lineup", () => {
    const a = generateLineup(ctx());
    const b = generateLineup(ctx({ seed: "1" }));
    expect(a.clubs.map((c) => c.refId)).not.toEqual(b.clubs.map((c) => c.refId));
    expect(a.hash).not.toBe(b.hash);
  });

  it("a blank seed falls back to FNV-1a of the id, deterministically", () => {
    expect(rotationSeed({ rotationId: "wk_2026_38", seed: "" })).toBe(rotationSeed({ rotationId: "wk_2026_38", seed: "" }));
    expect(rotationSeed({ rotationId: "wk_2026_38", seed: "" })).not.toBe(rotationSeed({ rotationId: "wk_2026_39", seed: "" }));
    expect(rotationSeed({ rotationId: "wk_2026_38", seed: "42" })).toBe(42);
  });
});

// ---------------------------------------------------------------------------
// Pins win
// ---------------------------------------------------------------------------

describe("pins", () => {
  it("a fully pinned row renders exactly the pinned refs, regardless of seed", () => {
    const a = generateLineup(ctx({ ...PINS, seed: "1" }));
    const b = generateLineup(ctx({ ...PINS, seed: "999999" }));
    expect(a.errors).toEqual([]);
    expect(a.clubs.map((c) => c.refId)).toEqual(PINS.pinnedClubs.split(";"));
    expect(a.balls.map((c) => c.refId)).toEqual(PINS.pinnedBalls.split(";"));
    expect(a.characters.map((c) => c.refId)).toEqual(["char_mike"]);
    expect(a.featured).toEqual(PINS.pinnedFeatured.split(";"));
    expect(a.clubs.every((c) => c.pinned)).toBe(true);
    // Identical rows in every catalog except the rotation row's own seed cell.
    for (const catalog of ROTATION_PUBLISH_ORDER.filter((c) => c !== "rotations")) {
      expect(a.rows[catalog]).toEqual(b.rows[catalog]);
    }
    expect(a.rows.rotations[0]!.data.seed).toBe("1");
    expect(b.rows.rotations[0]!.data.seed).toBe("999999");
  });

  it("a pin that does not resolve BLOCKS with the ref named", () => {
    const l = generateLineup(ctx({ pinnedClubs: "club_driver_bogeyb_legendary;club_nope" }));
    expect(l.errors).toHaveLength(1);
    expect(l.errors[0]).toContain("club_nope");
    expect(l.errors[0]).toContain("pinnedClubs");
    expect(lineupRows(l)).toHaveLength(0);
    expect(l.hash).toBe("");
  });

  it("a deactivated pin, the default ball and a starter each block", () => {
    const off = clubs.map((c) => (c.rowId === "club_iron_gf_rare" ? { ...c, isActive: false } : c));
    expect(generateLineup(ctx({ pinnedClubs: "club_iron_gf_rare" }, { clubs: off })).errors[0]).toContain("deactivated");
    expect(generateLineup(ctx({ pinnedBalls: "ball_golfin" })).errors[0]).toContain("DEFAULT ball");
    expect(generateLineup(ctx({ pinnedCharacter: "char_james" })).errors[0]).toContain("starter");
  });

  it("partial pins count against the quota and the rest is filled", () => {
    const l = generateLineup(ctx({ pinnedClubs: "club_driver_bogeyb_legendary;club_wood_bogeyb_common" }));
    expect(l.errors).toEqual([]);
    expect(l.clubs).toHaveLength(9);
    expect(countBy(l.clubs, (c) => c.rarity)).toEqual({ Common: 3, Uncommon: 2, Rare: 2, Mythic: 1, Legendary: 1 });
    expect(l.clubs.filter((c) => c.pinned).map((c) => c.refId)).toEqual(["club_driver_bogeyb_legendary", "club_wood_bogeyb_common"]);
  });

  it("a Rare character pinned on a Legendary:1 week yields ONE character (total caps the fill)", () => {
    const l = generateLineup(ctx({ characterQuota: "Legendary:1", pinnedCharacter: "char_camila" }));
    expect(l.errors).toEqual([]);
    expect(l.characters.map((c) => c.refId)).toEqual(["char_camila"]);
  });

  it("fillPlan: named buckets fill highest rarity first and cut Common when pins overshoot", () => {
    const { quota } = parseQuota("Common:3;Uncommon:2;Rare:2;Mythic:1;Legendary:1;Supreme:0");
    // Two Supremes pinned against a Supreme:0 quota: 7 left to fill, and the
    // per-rarity remainders sum to 9 — the two cut are Commons.
    const plan = fillPlan(quota!, ["Supreme", "Supreme"]);
    expect(Object.fromEntries(plan.perRarity)).toEqual({ Legendary: 1, Mythic: 1, Rare: 2, Uncommon: 2, Common: 1 });
    expect(plan.any).toBe(0);
    expect(fillPlan(parseQuota("Any:1").quota!, ["Rare"]).any).toBe(0);
    expect(fillPlan(parseQuota("Any:2").quota!, []).any).toBe(2);
  });
});

// ---------------------------------------------------------------------------
// The unpinned draw
// ---------------------------------------------------------------------------

describe("the unpinned draw", () => {
  const l = generateLineup(ctx());

  it("renders 9 clubs (3/2/2/1/1), seven types before any repeat, 3 balls, 1 character", () => {
    expect(l.errors).toEqual([]);
    expect(l.warnings).toEqual([]);
    expect(l.clubs).toHaveLength(9);
    expect(countBy(l.clubs, (c) => c.rarity)).toEqual({ Common: 3, Uncommon: 2, Rare: 2, Mythic: 1, Legendary: 1 });
    const types = countBy(l.clubs, (c) => c.type);
    expect(Object.keys(types)).toHaveLength(7);
    expect(Object.values(types).every((n) => n === 1 || n === 2)).toBe(true);
    expect(l.balls).toHaveLength(3);
    expect(countBy(l.balls, (b) => b.rarity)).toEqual({ Common: 1, Uncommon: 1, Rare: 1 });
    expect(l.balls.some((b) => b.refId === "ball_golfin")).toBe(false);
    expect(l.characters).toHaveLength(1);
    expect(["char_james", "char_olivia"]).not.toContain(l.characters[0]!.refId);
  });

  it("features the two highest-rarity clubs of the week", () => {
    expect(l.featured).toHaveLength(2);
    const byRef = new Map(l.clubs.map((c) => [c.refId, c.rarity]));
    expect(byRef.get(l.featured[0]!)).toBe("Legendary");
    expect(byRef.get(l.featured[1]!)).toBe("Mythic");
  });

  it("the type spread wraps when the quota exceeds seven", () => {
    const big = generateLineup(ctx({ clubQuota: "Common:8;Uncommon:0;Rare:0;Mythic:0;Legendary:0;Supreme:0" }));
    expect(big.clubs).toHaveLength(8);
    const types = countBy(big.clubs, (c) => c.type);
    expect(Object.keys(types)).toHaveLength(7);
    expect(Object.values(types).filter((n) => n === 2)).toHaveLength(1);
  });

  it("writes the shop rows with the window, the ladder price, the rotation tag and G1/G2-safe min_build", () => {
    const shop = l.rows.shop_catalog;
    expect(shop).toHaveLength(13);
    for (const r of shop) {
      expect(r.data.startAt).toBe("2026-09-14T00:00:00Z");
      expect(r.data.endAt).toBe("2026-09-21T00:00:00Z");
      expect(r.data.rotationId).toBe("wk_2026_38");
      expect(r.data.saleRpCost).toBe("");
      expect(r.minBuild).toBeGreaterThanOrEqual(SHOP_CATEGORY_STRICT_BUILD);
      expect(r.rowId).toBe(`shop_wk_2026_38_${r.data.refId}`);
      expect(r.data.entryId).toBe(r.rowId);
    }
    const club = shop.find((r) => r.data.category === "club")!;
    expect(Number(club.data.rpCost)).toBe(CLUB_RP_LADDER[club.data.rarity!]);
    expect(club.data.quantity).toBe("");
    // A ball row delivers ONE ball (the server reads quantity for tickets
    // only, G3-Q refuses anything else) — so quantity is blank, never 10.
    const ball = shop.find((r) => r.data.category === "ball" && r.data.rarity === "Common")!;
    expect(ball.data.quantity).toBe("");
    expect(Number(ball.data.rpCost)).toBe(30);
    expect(shop.filter((r) => r.data.category === "character")).toHaveLength(1);
    // Hero items first: the Legendary club sorts before every Common.
    const legendary = shop.find((r) => r.data.rarity === "Legendary")!;
    const common = shop.find((r) => r.data.rarity === "Common")!;
    expect(Number(legendary.data.sortOrder)).toBeLessThan(Number(common.data.sortOrder));
    expect(new Set(shop.map((r) => r.data.sortOrder)).size).toBe(shop.length);
  });

  it("clones the base pool's rates under the weekly pool id", () => {
    expect(l.rows.gacha_rates.map((r) => [r.data.rarity, r.data.rateBp, r.data.poolId])).toEqual(
      RATE_BP.map(([rarity, bp]) => [rarity, String(bp), "pool_wk_2026_38"])
    );
    expect(l.rows.gacha_rates.map((r) => r.rowId)).toContain("pool_wk_2026_38_common");
  });

  it("copies every ACTIVE base entry, resets featured, and boosts the two featured clubs ×3 in effectiveOdds", () => {
    const pool = l.rows.gacha_pools;
    // 6 clubs + the item copied (the inactive default-ball slot is not), + 2 featured inserted.
    expect(pool).toHaveLength(7 + 2);
    expect(pool.some((r) => r.data.refId === "ball_golfin")).toBe(false);
    const featuredRows = pool.filter((r) => r.data.featured === "true");
    expect(featuredRows.map((r) => r.data.refId).sort()).toEqual(l.featured.slice().sort());
    for (const r of featuredRows) expect(r.data.weight).toBe("300");
    // The base Legendary was `featured=true` in the base pool and is NOT any more.
    expect(pool.find((r) => r.rowId === "pwk_2026_38_club_driver_gf_legendary")!.data.featured).toBe("false");
    // Odds: featured Legendary p = 3 × the base Legendary's p (same rarity, weights 300 vs 100).
    const pOf = (refId: string) => l.odds.find((o) => o.entry.refId === refId)!.p;
    const featuredLegendary = l.featured[0]!;
    expect(pOf(featuredLegendary) / pOf("club_driver_gf_legendary")).toBeCloseTo(3, 10);
    expect(pOf(featuredLegendary)).toBeCloseTo((200 / 10000) * (300 / 400), 10);
    // dupeRp copied from the base entry of the same rarity.
    expect(pool.find((r) => r.data.refId === featuredLegendary)!.data.dupeRp).toBe("100");
    expect(l.odds.reduce((s, o) => s + o.p, 0)).toBeCloseTo(1, 10);
  });

  it("re-weights a featured club that is ALREADY in the base pool instead of inserting it twice", () => {
    const m = generateLineup(ctx({ pinnedFeatured: "club_driver_gf_legendary" }));
    const hits = m.rows.gacha_pools.filter((r) => r.data.refId === "club_driver_gf_legendary");
    expect(hits).toHaveLength(1);
    expect(hits[0]!.data.weight).toBe("300");
    expect(hits[0]!.data.featured).toBe("true");
  });

  it("writes the weekly banner cloned from the base pool's first active banner", () => {
    const [b] = l.rows.gacha_banners;
    expect(l.rows.gacha_banners).toHaveLength(1);
    expect(b!.rowId).toBe("banner_wk_2026_38");
    expect(b!.data).toMatchObject({
      bannerId: "banner_wk_2026_38",
      nameKey: "DRIVER WEEK · BOGEYB",
      nameEn: "DRIVER WEEK · BOGEYB",
      nameJa: "ドライバーウィーク · BOGEYB",
      taglineEn: "Featured this week",
      taglineJa: "今週のピックアップ",
      artSprite: "GachaBanner_Weekly",
      artUrl: "",
      costX1: "50", costX10: "450", ticketType: "0",
      pityThreshold: "50", pityMinRarity: "Legendary", guaranteeMinRarityX10: "Rare",
      maxPullsPerPlayer: "",
      pityGroup: WEEKLY_PITY_GROUP,
      poolId: "pool_wk_2026_38",
      startUtc: "2026-09-14T00:00:00Z", endUtc: "2026-09-21T00:00:00Z",
      sortOrder: "0", active: "true", rotationId: "wk_2026_38",
      featuredRefIds: l.featured.join(";"),
    });
  });

  it("a re-draw carries the existing draft banner's artUrl forward; a fresh week writes it blank", () => {
    // The art is uploaded on the banner row AFTER materializing, so blanking it
    // on every re-generation would silently un-art the week (wk_2026_39,
    // 2026-09-11). Everything else on the row is still re-derived.
    const ART = "https://example.test/catalog-art/gacha_banners-banner_wk_2026_38-artUrl-abc.jpg";
    const ownDraft = row("banner_wk_2026_38", {
      ...gachaBanners[0]!.data,
      bannerId: "banner_wk_2026_38", poolId: "pool_wk_2026_38", rotationId: "wk_2026_38",
      artUrl: ART, costX1: "999", costX10: "9999", nameEn: "HAND-EDITED NAME", sortOrder: "0",
    });
    const redraw = generateLineup(ctx({}, { gachaBanners: [...gachaBanners, ownDraft] }));
    const b = redraw.rows.gacha_banners[0]!;
    expect(b.data.artUrl).toBe(ART);
    // …but the own draft is never the BASE banner: cost and name come from the
    // base pool's banner and the rotation row, exactly as on a fresh week.
    expect(b.data).toMatchObject({ costX1: "50", costX10: "450", nameEn: "DRIVER WEEK · BOGEYB" });
    const fresh = generateLineup(ctx());
    expect(fresh.rows.gacha_banners[0]!.data.artUrl).toBe("");
    // The art is part of what will be written, so the two lineups differ only there.
    expect(redraw.hash).not.toBe(fresh.hash);
    const strip = (l: Lineup) => JSON.stringify({ ...l.rows, gacha_banners: l.rows.gacha_banners.map((r) => ({ ...r, data: { ...r.data, artUrl: "" } })) });
    expect(strip(redraw)).toBe(strip(fresh));
  });

  it("stamps the rotation row materializedAt and keeps every column", () => {
    const [r] = l.rows.rotations;
    expect(r!.rowId).toBe("wk_2026_38");
    expect(r!.data.materializedAt).toBe(NOW);
    expect(r!.data.seed).toBe("3287013188");
    expect(Object.keys(r!.data)).toHaveLength(18);
    expect(r!.data).not.toHaveProperty("is_active");
  });
});

// ---------------------------------------------------------------------------
// Exclusion and shortfall
// ---------------------------------------------------------------------------

describe("exclusion", () => {
  const prevRotations: CatalogRow[] = [
    row("wk_2026_37", { ...ROTATION_BASE, rotationId: "wk_2026_37", startUtc: "2026-09-07T00:00:00Z", endUtc: "2026-09-14T00:00:00Z", pinnedBalls: "ball_rare_a" }),
    row("wk_2026_36", { ...ROTATION_BASE, rotationId: "wk_2026_36", startUtc: "2026-08-31T00:00:00Z", endUtc: "2026-09-07T00:00:00Z" }),
    row("wk_2026_30", { ...ROTATION_BASE, rotationId: "wk_2026_30", startUtc: "2026-07-20T00:00:00Z", endUtc: "2026-07-27T00:00:00Z" }),
  ];
  const shopRows = (rotationId: string, refIds: string[]): CatalogRow[] =>
    refIds.map((refId) => row(`shop_${rotationId}_${refId}`, { entryId: `shop_${rotationId}_${refId}`, category: "club", refId, rotationId }));

  it("a ref listed in one of the last N rotations is never drawn", () => {
    // Every Legendary driver/wood/iron of both brands was listed last week: the
    // Legendary slot has to come from the other four types.
    const listed = clubs.filter((c) => c.data.rarity === "Legendary" && ["Driver", "Wood", "Iron"].includes(String(c.data.type))).map((c) => c.rowId);
    const l = generateLineup(ctx({}, { rotations: [row("wk_2026_38", ROTATION_BASE), ...prevRotations], shop: shopRows("wk_2026_37", listed) }));
    expect(l.errors).toEqual([]);
    const legendary = l.clubs.find((c) => c.rarity === "Legendary")!;
    expect(listed).not.toContain(legendary.refId);
    expect(["A.Wedge", "P.Wedge", "S.Wedge", "Putter"]).toContain(legendary.type);
    // The pinned lists of recent rotations count too, even with no shop rows.
    expect(l.balls.map((b) => b.refId)).not.toContain("ball_rare_a");
  });

  it("only the last N rotations by startUtc are the window", () => {
    const rotation = row("wk_2026_38", { ...ROTATION_BASE, excludeWeeks: "2" });
    const recent = recentRotations([rotation, ...prevRotations], rotation, 2);
    expect(recent.map((r) => r.rowId)).toEqual(["wk_2026_37", "wk_2026_36"]);
    const refs = recentlyFeaturedRefs(recent, shopRows("wk_2026_30", ["club_x"]));
    expect(refs.has("club_x")).toBe(false);
    expect(refs.has("ball_rare_a")).toBe(true);
  });

  it("a pinned ref inside the exclusion window WARNS but does not block", () => {
    const l = generateLineup(ctx({ pinnedBalls: "ball_rare_a" }, { rotations: [row("wk_2026_38", { ...ROTATION_BASE, pinnedBalls: "ball_rare_a" }), ...prevRotations] }));
    expect(l.errors).toEqual([]);
    expect(l.balls.map((b) => b.refId)).toContain("ball_rare_a");
    expect(l.warnings.some((w) => w.bucket === "balls" && w.message.includes("ball_rare_a"))).toBe(true);
  });

  it("a quota a bucket cannot fill is a WARNING and generation still succeeds", () => {
    const l = generateLineup(ctx({ ballQuota: "Legendary:3" }));
    expect(l.errors).toEqual([]);
    expect(l.balls.map((b) => b.refId)).toEqual(["ball_legendary_a"]);
    expect(l.warnings).toHaveLength(1);
    expect(l.warnings[0]).toMatchObject({ bucket: "balls" });
    expect(l.warnings[0]!.message).toContain("1 of 3");
    expect(lineupRows(l).length).toBeGreaterThan(0);
  });

  it("a ref that is also a permanent listing WARNS (twice on sale, two prices)", () => {
    const permanent = row("shop_char_mike", { entryId: "shop_char_mike", category: "character", refId: "char_mike", rpCost: "150", rotationId: "" });
    const l = generateLineup(ctx({ pinnedCharacter: "char_mike" }, { shop: [permanent] }));
    expect(l.errors).toEqual([]);
    expect(l.characters.map((c) => c.refId)).toEqual(["char_mike"]);
    const w = l.warnings.find((x) => x.bucket === "characters");
    expect(w?.message).toContain("shop_char_mike");
    expect(w?.message).toContain("150 RP");
    // A deactivated permanent row is not a twin.
    const off = generateLineup(ctx({ pinnedCharacter: "char_mike" }, { shop: [{ ...permanent, isActive: false }] }));
    expect(off.warnings).toEqual([]);
  });

  it("a bad quota or window blocks before anything is drawn", () => {
    expect(generateLineup(ctx({ clubQuota: "Common:x" })).errors[0]).toContain("clubQuota");
    expect(generateLineup(ctx({ characterQuota: "Villain:1" })).errors[0]).toContain("Villain");
    expect(generateLineup(ctx({ endUtc: "2026-09-14T00:00:00Z" })).errors[0]).toContain("ends at or before");
    expect(generateLineup(ctx({ startUtc: "yesterday" })).errors[0]).toContain("startUtc");
  });
});

// ---------------------------------------------------------------------------
// Materialize
// ---------------------------------------------------------------------------

describe("materialize", () => {
  const store = () => {
    const rows = new Map<string, OutRow>();
    const key = (r: { catalog: string; rowId: string }) => `${r.catalog}/${r.rowId}`;
    return {
      rows,
      upsert: async (r: OutRow) => { rows.set(key(r), structuredClone(r)); },
      key,
    };
  };

  it("writes every row through the upsert, in publish order, and reports the counts", async () => {
    const l = generateLineup(ctx());
    const s = store();
    const order: string[] = [];
    const counts = await materializeLineup(l, async (r) => { order.push(r.catalog); await s.upsert(r); });
    expect(counts).toEqual({ gacha_rates: 6, gacha_pools: 9, gacha_banners: 1, shop_catalog: 13, rotations: 1, deactivated: 0 });
    expect(s.rows.size).toBe(30);
    // Order: all rates, then all pools, then the banner, then shop, then the rotation.
    const firstIndex = (c: string) => order.indexOf(c);
    const lastIndex = (c: string) => order.lastIndexOf(c);
    for (let i = 1; i < ROTATION_PUBLISH_ORDER.length; i += 1) {
      expect(lastIndex(ROTATION_PUBLISH_ORDER[i - 1]!)).toBeLessThan(firstIndex(ROTATION_PUBLISH_ORDER[i]!));
    }
  });

  it("a second materialize overwrites this rotation's rows and leaves other rotations' rows untouched", async () => {
    const s = store();
    const other = generateLineup(ctx({ rotationId: "wk_2026_39", startUtc: "2026-09-21T00:00:00Z", endUtc: "2026-09-28T00:00:00Z", seed: "7" }));
    await materializeLineup(other, s.upsert);
    const otherBefore = new Map(Array.from(s.rows.entries()).map(([k, v]) => [k, JSON.stringify(v)]));

    const first = generateLineup(ctx());
    await materializeLineup(first, s.upsert);
    // The drafts as the panel would read them back before the second run.
    const drafts = () => {
      const by = (catalog: string): CatalogRow[] => Array.from(s.rows.values()).filter((r) => r.catalog === catalog);
      return { shop_catalog: by("shop_catalog"), gacha_banners: by("gacha_banners"), gacha_pools: by("gacha_pools"), gacha_rates: by("gacha_rates") };
    };
    const again = generateLineup(ctx({ seed: "2" }));
    expect(again.clubs.map((c) => c.refId)).not.toEqual(first.clubs.map((c) => c.refId));
    const counts = await materializeLineup(again, s.upsert, drafts());

    expect(counts.shop_catalog).toBe(13);
    // A different seed drew different refs, so the first run's rows that the
    // second did not reproduce are switched OFF, never deleted — and the
    // store never holds two live lineups for one week.
    const firstIds = new Set(lineupRows(first).map((r) => `${r.catalog}/${r.rowId}`));
    const againIds = new Set(lineupRows(again).map((r) => `${r.catalog}/${r.rowId}`));
    const stale = Array.from(firstIds).filter((k) => !againIds.has(k));
    expect(stale.length).toBeGreaterThan(0);
    expect(counts.deactivated).toBe(stale.length);
    for (const k of stale) expect(s.rows.get(k)!.isActive).toBe(false);
    for (const k of againIds) expect(s.rows.get(k)!.isActive).toBe(true);
    const live38 = Array.from(s.rows.values()).filter((r) => r.catalog === "shop_catalog" && r.data.rotationId === "wk_2026_38" && r.isActive);
    expect(live38).toHaveLength(13);
    // wk_2026_39's rows are byte-identical to before.
    for (const [k, before] of otherBefore) expect(JSON.stringify(s.rows.get(k))).toBe(before);
    expect(s.rows.get("rotations/wk_2026_38")!.data.seed).toBe("2");
    expect(s.rows.get("rotations/wk_2026_39")!.data.seed).toBe("7");
  });

  it("refuses to materialize a blocked lineup", async () => {
    const l = generateLineup(ctx({ pinnedClubs: "club_nope" }));
    await expect(materializeLineup(l, async () => {})).rejects.toThrow("club_nope");
  });
});

// ---------------------------------------------------------------------------
// Publish chain
// ---------------------------------------------------------------------------

describe("publish chain", () => {
  it("publishes the five catalogs in dependency order", async () => {
    const calls: RotationCatalog[] = [];
    const res = await publishRotationChain(async (c) => { calls.push(c); return { ok: true, message: `Published ${c}`, version: calls.length }; });
    expect(res.ok).toBe(true);
    expect(res.stoppedAt).toBeNull();
    expect(calls).toEqual(["gacha_rates", "gacha_pools", "gacha_banners", "shop_catalog", "rotations"]);
    expect(res.steps.map((s) => s.version)).toEqual([1, 2, 3, 4, 5]);
  });

  it("stops at the first failure and publishes nothing after it", async () => {
    const calls: RotationCatalog[] = [];
    const res = await publishRotationChain(async (c) => {
      calls.push(c);
      if (c === "gacha_banners") return { ok: false, message: "R3: banners sharing pityGroup \"weekly\" differ in pityThreshold" };
      return { ok: true, message: `Published ${c}` };
    });
    expect(res.ok).toBe(false);
    expect(res.stoppedAt).toBe("gacha_banners");
    expect(calls).toEqual(["gacha_rates", "gacha_pools", "gacha_banners"]);
    expect(res.steps).toHaveLength(3);
    expect(res.steps[2]!.message).toContain("R3");
  });
});

// ---------------------------------------------------------------------------
// Calendar + archive
// ---------------------------------------------------------------------------

describe("calendar", () => {
  it("ISO week ids", () => {
    expect(isoWeekId(Date.parse("2026-09-14T00:00:00Z"))).toBe("wk_2026_38");
    expect(isoWeekId(Date.parse("2026-09-20T23:59:59Z"))).toBe("wk_2026_38");
    expect(isoWeekId(Date.parse("2026-12-28T00:00:00Z"))).toBe("wk_2026_53");
    expect(isoWeekId(Date.parse("2027-01-04T00:00:00Z"))).toBe("wk_2027_01");
    expect(isoWeekId(Date.parse("2027-09-06T00:00:00Z"))).toBe("wk_2027_36");
  });

  it("states from the clock and materializedAt", () => {
    const now = Date.parse("2026-09-16T12:00:00Z");
    const live = row("wk_2026_38", { ...ROTATION_BASE, materializedAt: NOW });
    expect(rotationState(live, now, false)).toBe("LIVE");
    expect(rotationState(live, now, true)).toBe("GENERATED");
    expect(rotationState(row("wk_2026_38", ROTATION_BASE), now, false)).toBe("NOT_GENERATED");
    const next = row("wk_2026_39", { ...ROTATION_BASE, rotationId: "wk_2026_39", startUtc: "2026-09-21T00:00:00Z", endUtc: "2026-09-28T00:00:00Z", materializedAt: NOW });
    expect(rotationState(next, now, false)).toBe("SCHEDULED");
    expect(rotationState(row("wk_2026_37", { ...ROTATION_BASE, startUtc: "2026-09-07T00:00:00Z", endUtc: "2026-09-14T00:00:00Z", materializedAt: NOW }), now, false)).toBe("ENDED");
    expect(rotationState(null, now, false)).toBe("MISSING");
  });

  it("eight cells from this Monday, matched by id, MISSING when no row", () => {
    const now = Date.parse("2026-09-11T10:00:00Z");
    const cells = calendarWeeks([row("wk_2026_38", ROTATION_BASE)], now, new Set(), 8);
    expect(cells).toHaveLength(8);
    expect(cells[0]!.weekId).toBe("wk_2026_37");
    expect(cells[0]!.state).toBe("MISSING");
    expect(cells[1]!.weekId).toBe("wk_2026_38");
    expect(cells[1]!.state).toBe("NOT_GENERATED");
    expect(cells[7]!.weekId).toBe("wk_2026_44");
    expect(cells[1]!.mondayUtc).toBe("2026-09-14T00:00:00Z");
  });

  it("a new week copies the latest rotation's knobs, recomputes the seed, and pins nothing", () => {
    const latest = row("wk_2026_39", { ...ROTATION_BASE, rotationId: "wk_2026_39", startUtc: "2026-09-21T00:00:00Z", clubQuota: "Common:1;Supreme:1", gachaFeaturedCount: "3" });
    const data = newRotationRow("wk_2026_45", Date.parse("2026-11-02T00:00:00Z"), [row("wk_2026_38", ROTATION_BASE), latest]);
    expect(data).toMatchObject({
      rotationId: "wk_2026_45", startUtc: "2026-11-02T00:00:00Z", endUtc: "2026-11-09T00:00:00Z",
      nameEn: "WEEKLY LINEUP", clubQuota: "Common:1;Supreme:1", gachaFeaturedCount: "3",
      pinnedClubs: "", pinnedBalls: "", pinnedCharacter: "", pinnedFeatured: "", materializedAt: "",
    });
    expect(data.seed).toBe(String(rotationSeed({ rotationId: "wk_2026_45", seed: "" })));
    expect(Object.keys(data)).toHaveLength(18);
  });

  it("archive: rotations ended > 7 days ago, and only their ACTIVE rows, deactivated", () => {
    const now = Date.parse("2026-09-26T00:00:00Z");
    const ended = row("wk_2026_37", { ...ROTATION_BASE, rotationId: "wk_2026_37", startUtc: "2026-09-07T00:00:00Z", endUtc: "2026-09-14T00:00:00Z" }); // 12 days
    const recent = row("wk_2026_38", ROTATION_BASE); // ended 2026-09-21, 5 days ago — inside the 7-day grace
    expect(archivableRotations([ended, recent], now).map((r) => r.rowId)).toEqual(["wk_2026_37"]);

    const l37 = generateLineup(ctx({ rotationId: "wk_2026_37", startUtc: "2026-09-07T00:00:00Z", endUtc: "2026-09-14T00:00:00Z" }));
    const l38 = generateLineup(ctx());
    const drafts = {
      shop_catalog: [...l37.rows.shop_catalog, ...l38.rows.shop_catalog, row("shop_club_iron9_klyro", { entryId: "shop_club_iron9_klyro", category: "club", refId: "club_iron9_klyro", rotationId: "" })],
      gacha_banners: [...l37.rows.gacha_banners, ...l38.rows.gacha_banners, ...gachaBanners],
      gacha_pools: [...l37.rows.gacha_pools, ...l38.rows.gacha_pools, ...gachaPools],
      gacha_rates: [...l37.rows.gacha_rates, ...l38.rows.gacha_rates, ...gachaRates],
    };
    const out = archiveRows([ended], drafts);
    expect(out.every((r) => r.isActive === false)).toBe(true);
    expect(out.filter((r) => r.catalog === "shop_catalog")).toHaveLength(13);
    expect(out.filter((r) => r.catalog === "gacha_banners").map((r) => r.rowId)).toEqual(["banner_wk_2026_37"]);
    expect(out.filter((r) => r.catalog === "gacha_pools")).toHaveLength(9);
    expect(out.filter((r) => r.catalog === "gacha_rates")).toHaveLength(6);
    // Nothing of wk_2026_38, the permanent listing, the standard pool or the standard banner.
    expect(out.some((r) => r.rowId.includes("wk_2026_38") || r.rowId === "shop_club_iron9_klyro" || r.rowId.startsWith("psc1_") || r.rowId === "banner_standard_club1")).toBe(false);
    // Already-inactive rows are not re-emitted.
    const twice = archiveRows([ended], { ...drafts, shop_catalog: drafts.shop_catalog.map((r) => ({ ...r, isActive: false })) });
    expect(twice.filter((r) => r.catalog === "shop_catalog")).toHaveLength(0);
  });
});
