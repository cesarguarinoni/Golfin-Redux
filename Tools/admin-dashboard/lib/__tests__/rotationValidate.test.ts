import { describe, expect, it } from "vitest";
import {
  hasErrors,
  SHOP_CATEGORY_TO_CATALOG,
  SHOP_REFERENCED_CATALOGS,
  validateCatalog,
  type ContentProblem,
  type DraftRow,
  type ValidationContext,
} from "@/lib/contentValidate";

/**
 * weekly_rotation_admin §4.5 — R1 (rotations shape, overlap = error, gap =
 * warn), R2 (a tagged shop row / banner sits inside its rotation), R3 (a pity
 * group promises one threshold) and R4 (a hand-edited repeat inside
 * excludeWeeks warns). One positive and one negative fixture each, on the
 * SEED's own wk_2026_38 / wk_2026_39 rows so a rule that would refuse the
 * plan fails here rather than on prod.
 */

const draft = (rowId: string, data: Record<string, unknown>, over: Partial<DraftRow> = {}): DraftRow => ({
  rowId, data, minBuild: 0, isActive: true, ...over,
});

const rotation = (id: string, start: string, end: string, over: Record<string, unknown> = {}): DraftRow =>
  draft(id, {
    rotationId: id, startUtc: start, endUtc: end, nameEn: "WEEKLY LINEUP", nameJa: "今週のラインナップ",
    clubQuota: "Common:3;Uncommon:2;Rare:2;Mythic:1;Legendary:1;Supreme:0",
    ballQuota: "Common:1;Uncommon:1;Rare:1", characterQuota: "Any:1", gachaFeaturedCount: "2",
    gachaBasePoolId: "pool_standard_club1", featuredWeightMul: "3", excludeWeeks: "4", seed: "1",
    pinnedClubs: "", pinnedBalls: "", pinnedCharacter: "", pinnedFeatured: "", materializedAt: "",
    ...over,
  });

const WK38 = rotation("wk_2026_38", "2026-09-14T00:00:00Z", "2026-09-21T00:00:00Z");
const WK39 = rotation("wk_2026_39", "2026-09-21T00:00:00Z", "2026-09-28T00:00:00Z");
const WK40 = rotation("wk_2026_40", "2026-09-28T00:00:00Z", "2026-10-05T00:00:00Z");

const poolEntry = (id: string, refId: string, rarity: string, over: Record<string, unknown> = {}): DraftRow =>
  draft(id, { id, poolId: "pool_standard_club1", kind: "club", refId, rarity, weight: "100", quantity: "1", dupeRp: "20", featured: "false", ...over });

const club = (id: string, rarity = "Common"): DraftRow => draft(id, { id, name: id, type: "Driver", rarity, brand: "GF" });

const table = (rows: DraftRow[]): Map<string, DraftRow> => new Map(rows.map((r) => [r.rowId, r]));

const ctx = (other: Record<string, DraftRow[]> = {}, publishedMinBuild: Array<[string, number]> = []): ValidationContext => ({
  publishedMinBuild: new Map(publishedMinBuild),
  otherCatalogs: new Map(Object.entries(other).map(([k, v]) => [k, table(v)])),
});

const errorsOf = (p: ContentProblem[]) => p.filter((x) => x.severity === "error");
const warningsOf = (p: ContentProblem[]) => p.filter((x) => x.severity === "warning");
const messages = (p: ContentProblem[]) => p.map((x) => x.message);

// ---------------------------------------------------------------------------
// R1 — rotations
// ---------------------------------------------------------------------------

describe("R1 rotations", () => {
  const pools = [poolEntry("psc1_driver_gf", "club_driver_gf", "Common")];

  it("the seed's first three weeks are clean", () => {
    const p = validateCatalog("rotations", [WK38, WK39, WK40], ctx({ gacha_pools: pools }));
    expect(p).toEqual([]);
  });

  it("overlapping windows between two ACTIVE rotations are an ERROR", () => {
    const late39 = rotation("wk_2026_39", "2026-09-19T00:00:00Z", "2026-09-28T00:00:00Z");
    const p = validateCatalog("rotations", [WK38, late39], ctx({ gacha_pools: pools }));
    expect(hasErrors(p)).toBe(true);
    expect(errorsOf(p)).toHaveLength(1);
    expect(errorsOf(p)[0]).toMatchObject({ rowId: "wk_2026_39", column: "startUtc" });
    expect(errorsOf(p)[0]!.message).toContain("overlaps wk_2026_38");
    // Deactivating one of the two is the way out.
    expect(validateCatalog("rotations", [WK38, { ...late39, isActive: false }], ctx({ gacha_pools: pools }))).toEqual([]);
  });

  it("a gap between consecutive active rotations is a WARNING only", () => {
    const p = validateCatalog("rotations", [WK38, WK40], ctx({ gacha_pools: pools }));
    expect(hasErrors(p)).toBe(false);
    expect(warningsOf(p)).toHaveLength(1);
    expect(warningsOf(p)[0]!.message).toContain("gap between wk_2026_38");
    expect(warningsOf(p)[0]!.message).toContain("wk_2026_40");
  });

  it("the id shape, the window order and the quota grammar", () => {
    const p = validateCatalog("rotations", [
      rotation("week38", "2026-09-14T00:00:00Z", "2026-09-21T00:00:00Z"),
      rotation("wk_2026_39", "2026-09-28T00:00:00Z", "2026-09-21T00:00:00Z"),
      rotation("wk_2026_40", "2026-09-28T00:00:00Z", "2026-10-05T00:00:00Z", { clubQuota: "Common:3,Rare:2" }),
      rotation("wk_2026_41", "2026-10-05T00:00:00Z", "2026-10-12T00:00:00Z", { characterQuota: "Villain:1" }),
      rotation("wk_2026_42", "2026-10-12T00:00:00Z", "2026-10-19T00:00:00Z", { startUtc: "" }),
    ], ctx({ gacha_pools: pools }));
    const m = messages(errorsOf(p));
    expect(m.some((x) => x.includes('"week38" is not wk_<ISO-year>_<ISO-week>'))).toBe(true);
    expect(m.some((x) => x.includes("ends at or before it starts") && x.includes("2026-09-28"))).toBe(true);
    expect(m.some((x) => x.includes("clubQuota") && x.includes("Common:3,Rare:2"))).toBe(true);
    expect(m.some((x) => x.includes("Villain"))).toBe(true);
    expect(m.some((x) => x.includes("startUtc is empty"))).toBe(true);
  });

  it("gachaBasePoolId must resolve to a pool with active entries", () => {
    const p = validateCatalog("rotations", [rotation("wk_2026_38", "2026-09-14T00:00:00Z", "2026-09-21T00:00:00Z", { gachaBasePoolId: "pool_nope" })], ctx({ gacha_pools: pools }));
    expect(errorsOf(p)).toHaveLength(1);
    expect(errorsOf(p)[0]).toMatchObject({ column: "gachaBasePoolId" });
    expect(errorsOf(p)[0]!.message).toContain("pool_nope");
    const off = [poolEntry("psc1_driver_gf", "club_driver_gf", "Common", {}), ];
    off[0] = { ...off[0]!, isActive: false };
    expect(hasErrors(validateCatalog("rotations", [WK38], ctx({ gacha_pools: off })))).toBe(true);
  });

  it("a comma in a pin list and a duplicated pin are refused", () => {
    const p = validateCatalog("rotations", [
      rotation("wk_2026_38", "2026-09-14T00:00:00Z", "2026-09-21T00:00:00Z", { pinnedClubs: "club_a,club_b" }),
      rotation("wk_2026_39", "2026-09-21T00:00:00Z", "2026-09-28T00:00:00Z", { pinnedBalls: "ball_a;ball_a" }),
    ], ctx({ gacha_pools: pools }));
    const m = messages(errorsOf(p));
    expect(m.some((x) => x.includes("pinnedClubs contains a comma"))).toBe(true);
    expect(m.some((x) => x.includes("pinnedBalls lists ball_a twice"))).toBe(true);
  });

  it("numeric knobs: featuredWeightMul must be positive, excludeWeeks a whole number", () => {
    const p = validateCatalog("rotations", [
      rotation("wk_2026_38", "2026-09-14T00:00:00Z", "2026-09-21T00:00:00Z", { featuredWeightMul: "0", excludeWeeks: "1.5", gachaFeaturedCount: "-1" }),
    ], ctx({ gacha_pools: pools }));
    const cols = errorsOf(p).map((x) => x.column);
    expect(cols).toContain("featuredWeightMul");
    expect(cols).toContain("excludeWeeks");
    expect(cols).toContain("gachaFeaturedCount");
  });
});

// ---------------------------------------------------------------------------
// R2 / R4 — shop_catalog
// ---------------------------------------------------------------------------

describe("R2 + R4 shop_catalog", () => {
  const refs = { clubs: [club("club_a", "Common"), club("club_b", "Common"), club("club_c", "Rare")] };
  const shopRow = (id: string, refId: string, rotationId: string, start: string, end: string, over: Record<string, unknown> = {}): DraftRow =>
    draft(id, {
      entryId: id, category: "club", refId, rpCost: "100", saleRpCost: "", sortOrder: "500", popular: "false", offer: "false",
      rarity: "Common", startAt: start, endAt: end, saleStartAt: "", saleEndAt: "", quantity: "", rotationId, ...over,
    }, { minBuild: 2350 });

  it("a generated row — window equal to the rotation's — is clean", () => {
    const p = validateCatalog("shop_catalog", [shopRow("shop_wk_2026_38_club_a", "club_a", "wk_2026_38", "2026-09-14T00:00:00Z", "2026-09-21T00:00:00Z")],
      ctx({ ...refs, rotations: [WK38, WK39] }));
    expect(p).toEqual([]);
  });

  it("R2: an unknown rotationId is an error, and so is a window outside the rotation's", () => {
    const p = validateCatalog("shop_catalog", [
      shopRow("shop_x", "club_a", "wk_2099_01", "2026-09-14T00:00:00Z", "2026-09-21T00:00:00Z"),
      shopRow("shop_y", "club_b", "wk_2026_38", "2026-09-13T00:00:00Z", "2026-09-22T00:00:00Z"),
      shopRow("shop_z", "club_c", "wk_2026_38", "", "", { rarity: "Rare", rpCost: "400" }),
    ], ctx({ ...refs, rotations: [WK38, WK39] }));
    const e = errorsOf(p);
    expect(e.some((x) => x.rowId === "shop_x" && x.column === "rotationId" && x.message.includes("wk_2099_01"))).toBe(true);
    expect(e.some((x) => x.rowId === "shop_y" && x.column === "startAt" && x.message.includes("before rotation wk_2026_38 starts"))).toBe(true);
    expect(e.some((x) => x.rowId === "shop_y" && x.column === "endAt" && x.message.includes("after rotation wk_2026_38 ends"))).toBe(true);
    // A tagged row with NO window is a listing that outlives its week: both bounds fail.
    expect(e.filter((x) => x.rowId === "shop_z").map((x) => x.column).sort()).toEqual(["endAt", "startAt"]);
  });

  it("R2: a blank rotationId is not checked at all (the five permanent listings)", () => {
    const p = validateCatalog("shop_catalog", [shopRow("shop_club_a", "club_a", "", "", "")], ctx({ ...refs, rotations: [WK38] }));
    expect(p).toEqual([]);
  });

  it("R2: the rotations catalog not being loaded is an error, not silence", () => {
    const p = validateCatalog("shop_catalog", [shopRow("shop_wk_2026_38_club_a", "club_a", "wk_2026_38", "2026-09-14T00:00:00Z", "2026-09-21T00:00:00Z")], ctx(refs));
    expect(errorsOf(p)).toHaveLength(1);
    expect(errorsOf(p)[0]!.message).toContain("not loaded");
  });

  it("R4: a ref the previous rotation listed WARNS on a hand edit, and never blocks", () => {
    const p = validateCatalog("shop_catalog", [
      shopRow("shop_wk_2026_38_club_a", "club_a", "wk_2026_38", "2026-09-14T00:00:00Z", "2026-09-21T00:00:00Z"),
      shopRow("shop_wk_2026_39_club_a", "club_a", "wk_2026_39", "2026-09-21T00:00:00Z", "2026-09-28T00:00:00Z"),
      shopRow("shop_wk_2026_39_club_b", "club_b", "wk_2026_39", "2026-09-21T00:00:00Z", "2026-09-28T00:00:00Z"),
    ], ctx({ ...refs, rotations: [WK38, WK39] }));
    expect(hasErrors(p)).toBe(false);
    expect(warningsOf(p)).toHaveLength(1);
    expect(warningsOf(p)[0]).toMatchObject({ rowId: "shop_wk_2026_39_club_a", column: "refId" });
    expect(warningsOf(p)[0]!.message).toContain("R4");
    expect(warningsOf(p)[0]!.message).toContain("wk_2026_38");
  });

  it("R4: the window is the last N ROTATIONS by startUtc, and a rotation's OWN pins count as listed", () => {
    // Six rotations before wk_2026_40; excludeWeeks 4 reaches back to wk_2026_36,
    // so a listing in wk_2026_35 is outside the window and wk_2026_39's pin is inside it.
    const earlier = [
      rotation("wk_2026_35", "2026-08-24T00:00:00Z", "2026-08-31T00:00:00Z"),
      rotation("wk_2026_36", "2026-08-31T00:00:00Z", "2026-09-07T00:00:00Z"),
      rotation("wk_2026_37", "2026-09-07T00:00:00Z", "2026-09-14T00:00:00Z"),
    ];
    const wk39pinned = rotation("wk_2026_39", "2026-09-21T00:00:00Z", "2026-09-28T00:00:00Z", { pinnedClubs: "club_c" });
    const p = validateCatalog("shop_catalog", [
      shopRow("shop_wk_2026_35_club_a", "club_a", "wk_2026_35", "2026-08-24T00:00:00Z", "2026-08-31T00:00:00Z"),
      shopRow("shop_wk_2026_40_club_a", "club_a", "wk_2026_40", "2026-09-28T00:00:00Z", "2026-10-05T00:00:00Z"),
      shopRow("shop_wk_2026_40_club_c", "club_c", "wk_2026_40", "2026-09-28T00:00:00Z", "2026-10-05T00:00:00Z", { rarity: "Rare", rpCost: "400" }),
    ], ctx({ ...refs, rotations: [...earlier, WK38, wk39pinned, WK40] }));
    expect(hasErrors(p)).toBe(false);
    const w = warningsOf(p);
    expect(w.map((x) => x.rowId)).toEqual(["shop_wk_2026_40_club_c"]);
    expect(w[0]!.message).toContain("wk_2026_39");
    expect(w[0]!.message).not.toContain("wk_2026_35");
  });

  it("the ball band follows the ball ladder, so the generator's 30 RP Common ball does not warn", () => {
    const balls = [draft("ball_x", { id: "ball_x", name: "X", brand: "B", rarity: "Common", isDefault: "false" })];
    const ok = validateCatalog("shop_catalog", [
      draft("shop_wk_2026_38_ball_x", { entryId: "shop_wk_2026_38_ball_x", category: "ball", refId: "ball_x", rpCost: "30", saleRpCost: "", sortOrder: "500", popular: "false", offer: "false", rarity: "Common", startAt: "2026-09-14T00:00:00Z", endAt: "2026-09-21T00:00:00Z", saleStartAt: "", saleEndAt: "", quantity: "", rotationId: "wk_2026_38" }, { minBuild: 2350 }),
    ], ctx({ balls, rotations: [WK38] }));
    expect(ok).toEqual([]);
    const dear = validateCatalog("shop_catalog", [
      draft("shop_ball_x", { entryId: "shop_ball_x", category: "ball", refId: "ball_x", rpCost: "500", sortOrder: "1", rarity: "Common" }),
    ], ctx({ balls, rotations: [] }));
    expect(warningsOf(dear)).toHaveLength(1);
    expect(warningsOf(dear)[0]!.message).toContain("Common ball band 15–60");
  });
});

// ---------------------------------------------------------------------------
// The catalogs a shop publish loads — found by the live chain (2026-09-11)
// ---------------------------------------------------------------------------

describe("shop publish loads every catalog a category resolves in", () => {
  it("SHOP_REFERENCED_CATALOGS covers SHOP_CATEGORY_TO_CATALOG — ticket_types included", () => {
    // `ticket` joined the category map on 2026-08-31 while this list stayed at
    // five, so a Shop-drawer publish with the ticket row present failed with
    // `refId "0" does not exist in the ticket_types catalog`. The rotation
    // chain was the first drawer publish to hit it.
    for (const target of Object.values(SHOP_CATEGORY_TO_CATALOG)) {
      expect(SHOP_REFERENCED_CATALOGS).toContain(target);
    }
    expect(SHOP_REFERENCED_CATALOGS).toContain("ticket_types");
  });

  it("with ticket_types loaded, the shipped ticket row resolves", () => {
    const p = validateCatalog("shop_catalog", [
      draft("shop_ticket_standard_50", { entryId: "shop_ticket_standard_50", category: "ticket", refId: "0", rpCost: "100", sortOrder: "80", quantity: "50" }, { minBuild: 2536, isActive: false }),
    ], ctx({ ticket_types: [draft("0", { id: "0", key: "standard", nameEn: "Ticket", nameJa: "チケット" })], rotations: [] }));
    expect(hasErrors(p)).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// The archive trap — an archived week must never block a later publish
// ---------------------------------------------------------------------------

describe("archived rows never block a publish", () => {
  const rates = ["Common", "Uncommon", "Rare", "Mythic", "Legendary", "Supreme"].map((rarity, i) =>
    draft(`pool_standard_club1_${rarity.toLowerCase()}`, { id: `pool_standard_club1_${rarity.toLowerCase()}`, poolId: "pool_standard_club1", rarity, rateBp: String([5500, 2500, 1200, 550, 200, 50][i]) })
  );
  const pools = ["Common", "Uncommon", "Rare", "Mythic", "Legendary", "Supreme"].map((rarity) =>
    poolEntry(`psc1_${rarity.toLowerCase()}`, `club_${rarity.toLowerCase()}`, rarity)
  );
  // The archived week: its pool and rates deactivated by ARCHIVE ENDED.
  const archivedRates = rates.map((r) => ({ ...r, rowId: r.rowId.replace("pool_standard_club1", "pool_wk_2026_36"), data: { ...r.data, id: r.rowId.replace("pool_standard_club1", "pool_wk_2026_36"), poolId: "pool_wk_2026_36" }, isActive: false }));
  const archivedPool = pools.map((p) => ({ ...p, rowId: p.rowId.replace("psc1_", "pwk_2026_36_"), data: { ...p.data, id: p.rowId.replace("psc1_", "pwk_2026_36_"), poolId: "pool_wk_2026_36" }, isActive: false }));
  const tickets = [draft("0", { id: "0", key: "standard", nameEn: "Ticket", nameJa: "チケット" })];
  const wk36 = rotation("wk_2026_36", "2026-09-11T03:16:35Z", "2026-09-11T03:36:35Z", {}) ;
  const other = { gacha_rates: [...rates, ...archivedRates], gacha_pools: [...pools, ...archivedPool], ticket_types: tickets, rotations: [{ ...wk36, isActive: false }, WK38] };
  const banner = (id: string, over: Record<string, unknown> = {}, isActive = true): DraftRow =>
    draft(id, {
      bannerId: id, nameKey: id, artSprite: "GachaBanner_Weekly", costX1: "0", costX10: "0",
      endUtc: "2026-09-11T03:36:35Z", rulesUrl: "", sortOrder: "0", active: "true", startUtc: "2026-09-11T03:16:35Z",
      poolId: "pool_wk_2026_36", ticketType: "0", pityThreshold: "50", pityMinRarity: "Legendary",
      guaranteeMinRarityX10: "Rare", maxPullsPerPlayer: "", artUrl: "", nameEn: id, nameJa: id,
      taglineEn: "", taglineJa: "", featuredRefIds: "club_common", rotationId: "wk_2026_36", pityGroup: "weekly", ...over,
    }, { isActive });

  it("an ARCHIVED banner whose pool and rates are inactive is not an error (the live trap of 2026-09-11)", () => {
    const live = banner("banner_standard_club1", { poolId: "pool_standard_club1", rotationId: "", pityGroup: "", featuredRefIds: "", endUtc: "2027-01-01T00:00:00Z", startUtc: "2026-01-01T00:00:00Z", sortOrder: "1" });
    const p = validateCatalog("gacha_banners", [banner("banner_wk_2026_36", {}, false), live], ctx(other));
    expect(p).toEqual([]);
    // The same banner ACTIVE is refused — rule 10 still bites where it should.
    const q = validateCatalog("gacha_banners", [banner("banner_wk_2026_36", {}, true), live], ctx(other));
    expect(hasErrors(q)).toBe(true);
    expect(errorsOf(q).some((x) => x.rowId === "banner_wk_2026_36" && x.message.includes("no active rate table"))).toBe(true);
  });

  it("an archived banner still has to be a SANE row (costs, window, pity shape)", () => {
    const p = validateCatalog("gacha_banners", [banner("banner_wk_2026_36", { costX1: "-5", endUtc: "2026-09-11T03:00:00Z", pityThreshold: "50", pityMinRarity: "" }, false)], ctx(other));
    const cols = errorsOf(p).map((x) => x.column);
    expect(cols).toContain("costX1");
    expect(cols).toContain("endUtc");
    expect(cols).toContain("pityMinRarity");
    expect(cols).not.toContain("poolId");
  });

  it("an INACTIVE shop row whose ref was retired later does not block the shop", () => {
    const retired = draft("club_gone", { id: "club_gone", name: "Gone", type: "Driver", rarity: "Common", brand: "GF" }, { isActive: false });
    const row = (isActive: boolean): DraftRow => draft("shop_wk_2026_36_club_gone", {
      entryId: "shop_wk_2026_36_club_gone", category: "club", refId: "club_gone", rpCost: "100", saleRpCost: "", sortOrder: "500",
      popular: "false", offer: "false", rarity: "Common", startAt: "2026-09-11T03:16:35Z", endAt: "2026-09-11T03:36:35Z",
      saleStartAt: "", saleEndAt: "", quantity: "", rotationId: "wk_2026_36",
    }, { minBuild: 2350, isActive });
    expect(validateCatalog("shop_catalog", [row(false)], ctx({ clubs: [retired], rotations: [wk36] }))).toEqual([]);
    const active = validateCatalog("shop_catalog", [row(true)], ctx({ clubs: [retired], rotations: [wk36] }));
    expect(errorsOf(active).some((x) => x.message.includes("deactivated in clubs"))).toBe(true);
  });

  it("an INACTIVE rotation whose base pool was retired does not block the rotations catalog", () => {
    const gone = rotation("wk_2026_36", "2026-09-11T03:16:35Z", "2026-09-11T03:36:35Z", { gachaBasePoolId: "pool_retired" });
    expect(validateCatalog("rotations", [{ ...gone, isActive: false }, WK38], ctx({ gacha_pools: pools }))).toEqual([]);
    expect(hasErrors(validateCatalog("rotations", [gone, WK38], ctx({ gacha_pools: pools })))).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// R2 / R3 — gacha_banners
// ---------------------------------------------------------------------------

describe("R2 + R3 gacha_banners", () => {
  const rates = ["Common", "Uncommon", "Rare", "Mythic", "Legendary", "Supreme"].map((rarity, i) =>
    draft(`pool_standard_club1_${rarity.toLowerCase()}`, { id: `pool_standard_club1_${rarity.toLowerCase()}`, poolId: "pool_standard_club1", rarity, rateBp: String([5500, 2500, 1200, 550, 200, 50][i]) })
  );
  const pools = ["Common", "Uncommon", "Rare", "Mythic", "Legendary", "Supreme"].map((rarity) =>
    poolEntry(`psc1_${rarity.toLowerCase()}`, `club_${rarity.toLowerCase()}`, rarity)
  );
  const tickets = [draft("0", { id: "0", key: "standard", nameEn: "Ticket", nameJa: "チケット" })];
  const banner = (id: string, over: Record<string, unknown> = {}): DraftRow =>
    draft(id, {
      bannerId: id, nameKey: id, artSprite: "GachaBanner_Weekly", costX1: "50", costX10: "450",
      endUtc: "2026-09-21T00:00:00Z", rulesUrl: "", sortOrder: "0", active: "true", startUtc: "2026-09-14T00:00:00Z",
      poolId: "pool_standard_club1", ticketType: "0", pityThreshold: "50", pityMinRarity: "Legendary",
      guaranteeMinRarityX10: "Rare", maxPullsPerPlayer: "", artUrl: "", nameEn: id, nameJa: id,
      taglineEn: "", taglineJa: "", featuredRefIds: "", rotationId: "", pityGroup: "", ...over,
    });
  const other = { gacha_rates: rates, gacha_pools: pools, ticket_types: tickets, rotations: [WK38, WK39] };

  it("two weekly banners sharing the group with identical pity are clean, and R2 accepts an equal window", () => {
    const p = validateCatalog("gacha_banners", [
      banner("banner_wk_2026_38", { rotationId: "wk_2026_38", pityGroup: "weekly", sortOrder: "0" }),
      banner("banner_wk_2026_39", { rotationId: "wk_2026_39", pityGroup: "weekly", startUtc: "2026-09-21T00:00:00Z", endUtc: "2026-09-28T00:00:00Z", sortOrder: "1" }),
    ], ctx(other));
    expect(p).toEqual([]);
  });

  it("R3: a shared pityGroup with a different threshold is an ERROR naming the rule", () => {
    const p = validateCatalog("gacha_banners", [
      banner("banner_wk_2026_38", { rotationId: "wk_2026_38", pityGroup: "weekly", sortOrder: "0" }),
      banner("banner_wk_2026_39", { rotationId: "wk_2026_39", pityGroup: "weekly", startUtc: "2026-09-21T00:00:00Z", endUtc: "2026-09-28T00:00:00Z", sortOrder: "1", pityThreshold: "30" }),
    ], ctx(other));
    expect(hasErrors(p)).toBe(true);
    expect(errorsOf(p)).toHaveLength(1);
    expect(errorsOf(p)[0]).toMatchObject({ rowId: "banner_wk_2026_39", column: "pityGroup" });
    expect(errorsOf(p)[0]!.message).toContain("R3");
    expect(errorsOf(p)[0]!.message).toContain("50/Legendary vs 30/Legendary");
  });

  it("R3: a different pityMinRarity is an error too; blank and 0 thresholds compare equal; inactive banners are ignored", () => {
    const rarity = validateCatalog("gacha_banners", [
      banner("banner_a", { pityGroup: "weekly", sortOrder: "0" }),
      banner("banner_b", { pityGroup: "weekly", sortOrder: "1", pityMinRarity: "Mythic" }),
    ], ctx(other));
    expect(errorsOf(rarity).some((x) => x.message.includes("R3"))).toBe(true);

    const noPity = validateCatalog("gacha_banners", [
      banner("banner_a", { pityGroup: "g", sortOrder: "0", pityThreshold: "", pityMinRarity: "" }),
      banner("banner_b", { pityGroup: "g", sortOrder: "1", pityThreshold: "0", pityMinRarity: "" }),
    ], ctx(other));
    expect(errorsOf(noPity)).toEqual([]);

    const off = validateCatalog("gacha_banners", [
      banner("banner_a", { pityGroup: "weekly", sortOrder: "0" }),
      banner("banner_b", { pityGroup: "weekly", sortOrder: "1", pityThreshold: "30", active: "false" }),
    ], ctx(other));
    expect(errorsOf(off).some((x) => x.message.includes("R3"))).toBe(false);
  });

  it("R2: a banner tagged with a rotation must sit inside its window", () => {
    const p = validateCatalog("gacha_banners", [
      banner("banner_wk_2026_38", { rotationId: "wk_2026_38", startUtc: "2026-09-10T00:00:00Z" }),
      banner("banner_x", { rotationId: "wk_2099_01", sortOrder: "1" }),
    ], ctx(other));
    const e = errorsOf(p);
    expect(e.some((x) => x.rowId === "banner_wk_2026_38" && x.column === "startUtc" && x.message.includes("R2"))).toBe(true);
    expect(e.some((x) => x.rowId === "banner_x" && x.column === "rotationId" && x.message.includes("wk_2099_01"))).toBe(true);
  });
});
