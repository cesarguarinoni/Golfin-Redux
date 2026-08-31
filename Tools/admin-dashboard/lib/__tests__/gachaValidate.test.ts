import { describe, expect, it } from "vitest";
import {
  hasErrors,
  validateCatalog,
  type ContentProblem,
  type DraftRow,
  type ValidationContext,
} from "@/lib/contentValidate";

/**
 * The twenty gacha publish rules (gacha_admin_catalogs §5.5), one positive and
 * at least one negative fixture each.
 *
 * WHY THIS SUITE IS NOT OPTIONAL. These four catalogs are the first content the
 * SERVER pays a player out of directly (`golfin_gacha_pull()`, spec B, reads the
 * published `content_rows` with no mirror in between). Every other catalog's bad
 * publish shows a wrong number on a card; a bad publish here is a rate table
 * that does not add up to 100 %, a roll that lands on nothing, or a prize the
 * game cannot render — with a ticket already spent.
 *
 * The fixtures are the SEED rows of SPEC §2, so a rule that would refuse the
 * catalogs this task ships fails here rather than on prod.
 */

const draft = (rowId: string, data: Record<string, unknown>, over: Partial<DraftRow> = {}): DraftRow => ({
  rowId,
  data,
  minBuild: 0,
  isActive: true,
  ...over,
});

// ---------------------------------------------------------------------------
// Fixtures — the seed pool, its rate table, two ticket types, one banner
// ---------------------------------------------------------------------------

const RATE_BP: Array<[string, number]> = [
  ["Common", 5500],
  ["Uncommon", 2500],
  ["Rare", 1200],
  ["Mythic", 550],
  ["Legendary", 200],
  ["Supreme", 50],
];

const rateRows = (over: Partial<Record<string, number>> = {}): DraftRow[] =>
  RATE_BP.map(([rarity, bp]) =>
    draft(`pool_standard_club1_${rarity.toLowerCase()}`, {
      id: `pool_standard_club1_${rarity.toLowerCase()}`,
      poolId: "pool_standard_club1",
      rarity,
      rateBp: String(over[rarity] ?? bp),
    })
  );

const poolEntry = (
  rowId: string,
  refId: string,
  rarity: string,
  over: Record<string, unknown> = {},
  rowOver: Partial<DraftRow> = {}
): DraftRow =>
  draft(
    rowId,
    {
      id: rowId,
      poolId: "pool_standard_club1",
      kind: "club",
      refId,
      rarity,
      weight: "100",
      quantity: "1",
      dupeRp: "0",
      featured: "false",
      ...over,
    },
    rowOver
  );

/** One entry per rated rarity — the minimum that satisfies rule 9. */
const poolRows = (): DraftRow[] => [
  poolEntry("psc1_driver_gf", "club_driver_gf", "Common"),
  poolEntry("psc1_iron9_klyro", "club_iron9_klyro", "Uncommon"),
  poolEntry("psc1_iron7_mireo", "club_iron7_mireo", "Rare"),
  poolEntry("psc1_awedge_fyloe", "club_awedge_fyloe", "Mythic"),
  poolEntry("psc1_pwedge_royal", "club_pwedge_royal", "Legendary", { featured: "true" }),
  poolEntry("psc1_putter_golfinx", "club_putter_golfinx", "Supreme", { featured: "true" }),
];

const ticketRows = (): DraftRow[] => [
  draft("0", { id: "0", key: "standard", nameEn: "Ticket", nameJa: "チケット" }),
  draft("1", { id: "1", key: "gold", nameEn: "Gold Ticket", nameJa: "ゴールドチケット" }),
];

const bannerRow = (over: Record<string, unknown> = {}, rowOver: Partial<DraftRow> = {}): DraftRow =>
  draft(
    "banner_standard_club1",
    {
      bannerId: "banner_standard_club1",
      nameKey: "STANDARD CLUB 1",
      artSprite: "GachaBanner_StandardClub1",
      costX1: "50",
      costX10: "450",
      endUtc: "2027-01-01T00:00:00Z",
      rulesUrl: "",
      sortOrder: "1",
      active: "true",
      startUtc: "2026-01-01T00:00:00Z",
      poolId: "pool_standard_club1",
      ticketType: "0",
      pityThreshold: "50",
      pityMinRarity: "Legendary",
      guaranteeMinRarityX10: "Rare",
      maxPullsPerPlayer: "",
      artUrl: "",
      nameEn: "STANDARD CLUB 1",
      nameJa: "スタンダードクラブ 1",
      taglineEn: "",
      taglineJa: "",
      featuredRefIds: "club_pwedge_royal",
      ...over,
    },
    rowOver
  );

/** The five ref catalogs a pool resolves against, with the seed rows' rarities. */
const clubRows = (): Map<string, DraftRow> =>
  new Map(
    (
      [
        ["club_driver_gf", "Common"],
        ["club_wood_gf", "Common"],
        ["club_iron9_klyro", "Uncommon"],
        ["club_iron7_mireo", "Rare"],
        ["club_awedge_fyloe", "Mythic"],
        ["club_pwedge_royal", "Legendary"],
        ["club_putter_golfinx", "Supreme"],
      ] as const
    ).map(([id, rarity]) => [id, draft(id, { id, name: id, rarity })])
  );

const ballRows = (): Map<string, DraftRow> =>
  new Map([["ball_golfin", draft("ball_golfin", { id: "ball_golfin", name: "Golfin" })]]);

const itemRows = (): Map<string, DraftRow> =>
  new Map([
    ["repairkit_common", draft("repairkit_common", { id: "repairkit_common", rarity: "Common" })],
    ["repairkit_rare", draft("repairkit_rare", { id: "repairkit_rare", rarity: "Rare" })],
  ]);

interface Others {
  rates?: DraftRow[];
  pools?: DraftRow[];
  tickets?: DraftRow[];
  banners?: DraftRow[];
  clubs?: Map<string, DraftRow>;
}

/** The context `publishCatalog` builds, with the same catalogs it loads. */
const ctx = (others: Others = {}): ValidationContext => {
  const map = new Map<string, Map<string, DraftRow>>();
  const put = (name: string, rows: DraftRow[]) =>
    map.set(name, new Map(rows.map((r) => [r.rowId, r])));
  put("gacha_rates", others.rates ?? rateRows());
  put("gacha_pools", others.pools ?? poolRows());
  put("ticket_types", others.tickets ?? ticketRows());
  put("gacha_banners", others.banners ?? [bannerRow()]);
  map.set("clubs", others.clubs ?? clubRows());
  map.set("balls", ballRows());
  map.set("items", itemRows());
  map.set("characters", new Map());
  return { publishedMinBuild: new Map(), otherCatalogs: map };
};

const errorsOf = (problems: ContentProblem[]): string[] =>
  problems.filter((p) => p.severity === "error").map((p) => p.message);
const warningsOf = (problems: ContentProblem[]): string[] =>
  problems.filter((p) => p.severity === "warning").map((p) => p.message);
const joined = (problems: ContentProblem[]): string => errorsOf(problems).join(" | ");

// ---------------------------------------------------------------------------

describe("the seed catalogs publish cleanly", () => {
  it("gacha_rates", () => {
    expect(errorsOf(validateCatalog("gacha_rates", rateRows(), ctx()))).toEqual([]);
  });
  it("gacha_pools", () => {
    expect(errorsOf(validateCatalog("gacha_pools", poolRows(), ctx()))).toEqual([]);
  });
  it("gacha_banners", () => {
    expect(errorsOf(validateCatalog("gacha_banners", [bannerRow()], ctx()))).toEqual([]);
  });
  it("ticket_types", () => {
    expect(errorsOf(validateCatalog("ticket_types", ticketRows(), ctx()))).toEqual([]);
  });
});

describe("gacha_rates — rules 1-4", () => {
  it("rule 1: refuses a rarity that is not one of the six", () => {
    const rows = rateRows();
    rows[0] = draft("bogus", { id: "bogus", poolId: "pool_standard_club1", rarity: "Ultra", rateBp: "5500" });
    expect(joined(validateCatalog("gacha_rates", rows, ctx()))).toContain('"Ultra" is not one of');
  });

  it("rule 1: refuses a rateBp above 10000 and a fractional one", () => {
    expect(joined(validateCatalog("gacha_rates", rateRows({ Common: 10001 }), ctx()))).toContain(
      "between 0 and 10000"
    );
    const fractional = rateRows();
    fractional[0]!.data.rateBp = "55.5";
    expect(joined(validateCatalog("gacha_rates", fractional, ctx()))).toContain("whole number");
  });

  it("rule 2: refuses a pool that is missing a rarity", () => {
    const rows = rateRows().filter((r) => r.data.rarity !== "Supreme");
    rows[0]!.data.rateBp = "5550"; // keep the sum at 10000 so only rule 2 fires
    expect(joined(validateCatalog("gacha_rates", rows, ctx()))).toContain("has no Supreme rate row");
  });

  it("rule 2: refuses two active rows for the same rarity", () => {
    const rows = [
      ...rateRows({ Common: 3000 }),
      draft("dup_common", {
        id: "dup_common",
        poolId: "pool_standard_club1",
        rarity: "Common",
        rateBp: "2500",
      }),
    ];
    expect(joined(validateCatalog("gacha_rates", rows, ctx()))).toContain("active Common rate rows");
  });

  it("rule 3: refuses a pool whose rates sum to 9850", () => {
    // The acceptance's own example.
    const problems = validateCatalog("gacha_rates", rateRows({ Rare: 1050 }), ctx());
    expect(joined(problems)).toContain("sum to 9850 basis points, not 10000");
    expect(hasErrors(problems)).toBe(true);
  });

  it("rule 3: a DEACTIVATED rate row does not count toward the sum", () => {
    const rows = rateRows();
    // Deactivating Supreme leaves 9950 — the rule must see that, not 10000.
    rows[5] = { ...rows[5]!, isActive: false };
    expect(joined(validateCatalog("gacha_rates", rows, ctx()))).toContain("sum to 9950");
  });

  it("rule 4: refuses a rate table for a pool with no entries", () => {
    expect(joined(validateCatalog("gacha_rates", rateRows(), ctx({ pools: [] })))).toContain(
      "no active gacha_pools entries"
    );
  });
});

describe("gacha_pools — rules 5-9", () => {
  it("rule 5: refuses an unknown kind", () => {
    const rows = poolRows();
    rows[0]!.data.kind = "trophy";
    expect(joined(validateCatalog("gacha_pools", rows, ctx()))).toContain('Unknown kind "trophy"');
  });

  it("rule 5: refuses a refId that does not exist", () => {
    const rows = poolRows();
    rows[0]!.data.refId = "club_that_never_was";
    expect(joined(validateCatalog("gacha_pools", rows, ctx()))).toContain(
      'refId "club_that_never_was" does not exist in the clubs catalog'
    );
  });

  it("rule 5: refuses a refId whose row is DEACTIVATED", () => {
    const clubs = clubRows();
    clubs.set("club_driver_gf", { ...clubs.get("club_driver_gf")!, isActive: false });
    expect(joined(validateCatalog("gacha_pools", poolRows(), ctx({ clubs })))).toContain(
      "is deactivated in clubs"
    );
  });

  it("rule 5: a ticket prize resolves in ticket_types, not in a ref catalog", () => {
    const rows = [
      ...poolRows(),
      poolEntry("psc1_gold_ticket", "1", "Rare", { kind: "ticket", quantity: "2" }),
    ];
    expect(errorsOf(validateCatalog("gacha_pools", rows, ctx()))).toEqual([]);

    const bad = [...poolRows(), poolEntry("psc1_bad_ticket", "7", "Rare", { kind: "ticket" })];
    expect(joined(validateCatalog("gacha_pools", bad, ctx()))).toContain(
      'refId "7" does not exist in the ticket_types catalog'
    );
  });

  it("rule 6: refuses Common on a club the catalog calls Rare", () => {
    // The acceptance's own example.
    const rows = poolRows();
    rows[2] = poolEntry("psc1_iron7_mireo", "club_iron7_mireo", "Common");
    const message = joined(validateCatalog("gacha_pools", rows, ctx()));
    expect(message).toContain('rarity is "Common" but "club_iron7_mireo" is Rare in clubs');
  });

  it("rule 6: a BALL keeps the operator's rarity — it has none of its own", () => {
    const rows = [
      ...poolRows(),
      poolEntry("psc1_ball_golfin", "ball_golfin", "Mythic", { kind: "ball", quantity: "3" }),
    ];
    expect(errorsOf(validateCatalog("gacha_pools", rows, ctx()))).toEqual([]);
  });

  it("rule 7: refuses weight 0, quantity 0, a negative dupeRp and a non-boolean featured", () => {
    const zeroWeight = poolRows();
    zeroWeight[0]!.data.weight = "0";
    expect(joined(validateCatalog("gacha_pools", zeroWeight, ctx()))).toContain("weight 0 is below 1");

    const zeroQty = poolRows();
    zeroQty[0]!.data.quantity = "0";
    expect(joined(validateCatalog("gacha_pools", zeroQty, ctx()))).toContain("quantity 0 is below 1");

    const negativeDupe = poolRows();
    negativeDupe[0]!.data.dupeRp = "-20";
    expect(joined(validateCatalog("gacha_pools", negativeDupe, ctx()))).toContain(
      "would CHARGE the player"
    );

    const badBool = poolRows();
    badBool[0]!.data.featured = "yes";
    expect(joined(validateCatalog("gacha_pools", badBool, ctx()))).toContain('"yes" is not true or false');
  });

  it("rule 8: refuses an entry visible to a build that cannot see its prize", () => {
    const clubs = clubRows();
    clubs.set("club_driver_gf", { ...clubs.get("club_driver_gf")!, minBuild: 2500 });
    expect(joined(validateCatalog("gacha_pools", poolRows(), ctx({ clubs })))).toContain(
      "min_build 0 is below the min_build of \"club_driver_gf\""
    );
  });

  it("rule 8: a DEACTIVATED entry is not gated — min_build is immutable once published", () => {
    const clubs = clubRows();
    clubs.set("club_driver_gf", { ...clubs.get("club_driver_gf")!, minBuild: 2500 });
    const rows = poolRows();
    rows[0] = { ...rows[0]!, isActive: false };
    // Deactivating removes the Common entry, so rule 9 fires instead — but NOT
    // rule 8, which is the point.
    expect(joined(validateCatalog("gacha_pools", rows, ctx({ clubs })))).not.toContain("min_build 0 is below");
  });

  it("rule 9: refuses a Legendary rate with no Legendary entry", () => {
    // The acceptance's own example, from BOTH publishes.
    const withoutLegendary = poolRows().filter((r) => r.data.rarity !== "Legendary");
    expect(joined(validateCatalog("gacha_pools", withoutLegendary, ctx()))).toContain(
      "Legendary has a rate of 200 bp but no active entry"
    );
    expect(
      joined(validateCatalog("gacha_rates", rateRows(), ctx({ pools: withoutLegendary })))
    ).toContain("Legendary has a rate of 200 bp but no active entry");
  });

  it("rule 9: an entry in a rarity with rate 0 is a WARNING, not a block", () => {
    const rates = rateRows({ Supreme: 0, Common: 5550 });
    const problems = validateCatalog("gacha_pools", poolRows(), ctx({ rates }));
    expect(hasErrors(problems)).toBe(false);
    expect(warningsOf(problems).join(" | ")).toContain("Unreachable: Supreme has a rate of 0");
  });
});

describe("gacha_banners — rules 10-18", () => {
  it("rule 10: refuses a pool with no rate table and one whose table does not sum", () => {
    expect(joined(validateCatalog("gacha_banners", [bannerRow()], ctx({ rates: [] })))).toContain(
      "has no active rate table"
    );
    expect(
      joined(validateCatalog("gacha_banners", [bannerRow()], ctx({ rates: rateRows({ Rare: 1050 }) })))
    ).toContain("rates sum to 9850 basis points");
  });

  it("rule 10: refuses a ticketType that does not resolve, and one that is deactivated", () => {
    expect(joined(validateCatalog("gacha_banners", [bannerRow({ ticketType: "9" })], ctx()))).toContain(
      'ticketType "9" is not a ticket_types id'
    );
    const tickets = ticketRows();
    tickets[0] = { ...tickets[0]!, isActive: false };
    expect(joined(validateCatalog("gacha_banners", [bannerRow()], ctx({ tickets })))).toContain(
      'ticketType "0" is deactivated'
    );
  });

  it("rule 11: refuses a negative cost and WARNS on a x10 dearer than ten x1s", () => {
    expect(joined(validateCatalog("gacha_banners", [bannerRow({ costX1: "-1" })], ctx()))).toContain(
      "costX1 -1 is negative"
    );
    const problems = validateCatalog("gacha_banners", [bannerRow({ costX10: "600" })], ctx());
    expect(hasErrors(problems)).toBe(false);
    expect(warningsOf(problems).join(" | ")).toContain("more than ten x1s (500)");
  });

  it("rule 12: refuses an unreadable timestamp and an inverted window", () => {
    expect(joined(validateCatalog("gacha_banners", [bannerRow({ startUtc: "soon" })], ctx()))).toContain(
      "is not a readable timestamp"
    );
    expect(
      joined(
        validateCatalog(
          "gacha_banners",
          [bannerRow({ startUtc: "2027-01-01T00:00:00Z", endUtc: "2026-01-01T00:00:00Z" })],
          ctx()
        )
      )
    ).toContain("ends at or before it starts");
  });

  it("rule 13: pityThreshold 0 with a pityMinRarity is a WARNING only", () => {
    // The acceptance's own example, and decision 2's blank-or-zero equivalence.
    const zero = validateCatalog(
      "gacha_banners",
      [bannerRow({ pityThreshold: "0", pityMinRarity: "Legendary" })],
      ctx()
    );
    expect(hasErrors(zero)).toBe(false);
    expect(warningsOf(zero).join(" | ")).toContain("is ignored");

    const blank = validateCatalog(
      "gacha_banners",
      [bannerRow({ pityThreshold: "", pityMinRarity: "Legendary" })],
      ctx()
    );
    expect(hasErrors(blank)).toBe(false);
    expect(warningsOf(blank).join(" | ")).toContain("is ignored");
  });

  it("rule 13: a threshold with NO rarity is refused", () => {
    expect(
      joined(validateCatalog("gacha_banners", [bannerRow({ pityMinRarity: "" })], ctx()))
    ).toContain("pityMinRarity is required");
  });

  it("rule 13: a pity or guarantee rarity the pool never rolls is refused", () => {
    const rates = rateRows({ Legendary: 0, Common: 5700 });
    expect(joined(validateCatalog("gacha_banners", [bannerRow()], ctx({ rates })))).toContain(
      'Legendary has a rate of 0 in pool "pool_standard_club1", so the pity could never be paid'
    );
    const noRare = rateRows({ Rare: 0, Common: 6700 });
    expect(joined(validateCatalog("gacha_banners", [bannerRow()], ctx({ rates: noRare })))).toContain(
      "the x10 guarantee could never be paid"
    );
  });

  it("rule 13: a banner with NO pity at all passes — banner_test_a", () => {
    const problems = validateCatalog(
      "gacha_banners",
      [
        bannerRow(
          {
            bannerId: "banner_test_a",
            pityThreshold: "",
            pityMinRarity: "",
            guaranteeMinRarityX10: "",
            nameEn: "TEST BANNER A",
            nameJa: "テストバナー A",
            featuredRefIds: "",
            sortOrder: "2",
          },
          { rowId: "banner_test_a" }
        ),
      ],
      ctx()
    );
    expect(errorsOf(problems)).toEqual([]);
    expect(warningsOf(problems)).toEqual([]);
  });

  it("rule 14: refuses a cap of 0 and accepts a blank one", () => {
    expect(
      joined(validateCatalog("gacha_banners", [bannerRow({ maxPullsPerPlayer: "0" })], ctx()))
    ).toContain("would let nobody pull");
    expect(
      errorsOf(validateCatalog("gacha_banners", [bannerRow({ maxPullsPerPlayer: "" })], ctx()))
    ).toEqual([]);
  });

  it("rule 15: an active banner needs both locales and some artwork", () => {
    expect(joined(validateCatalog("gacha_banners", [bannerRow({ nameJa: "" })], ctx()))).toContain(
      '"nameJa" is empty on an active banner'
    );
    expect(
      joined(validateCatalog("gacha_banners", [bannerRow({ artSprite: "", artUrl: "" })], ctx()))
    ).toContain("needs artSprite (bundled) or artUrl (uploaded)");
    // artUrl alone is enough — an installed build fetches it.
    expect(
      errorsOf(
        validateCatalog(
          "gacha_banners",
          [
            bannerRow({
              artSprite: "",
              artUrl:
                "https://wmszyghwwkaptgqdunel.supabase.co/storage/v1/object/public/catalog-art/" +
                "gacha_banners-banner_standard_club1-artUrl-abcdef123456.png",
            }),
          ],
          ctx()
        )
      )
    ).toEqual([]);
  });

  it("rule 15: a DEACTIVATED banner is not held to it — deactivate is the delete", () => {
    expect(
      errorsOf(
        validateCatalog("gacha_banners", [bannerRow({ nameJa: "" }, { isActive: false })], ctx())
      )
    ).toEqual([]);
  });

  it("rule 16: refuses an artUrl outside the catalog-art bucket", () => {
    expect(
      joined(
        validateCatalog(
          "gacha_banners",
          [bannerRow({ artUrl: "https://evil.example.com/banner.png" })],
          ctx()
        )
      )
    ).toContain("catalog-art");
    expect(
      joined(
        validateCatalog(
          "gacha_banners",
          [
            bannerRow({
              artUrl:
                "https://wmszyghwwkaptgqdunel.supabase.co/storage/v1/object/public/game-banners/x.png",
            }),
          ],
          ctx()
        )
      )
    ).toContain('must be inside the "catalog-art" bucket');
  });

  it("rule 17: a shared sortOrder among active rows is a WARNING", () => {
    const twin = bannerRow(
      { bannerId: "banner_test_b", nameEn: "B", nameJa: "B", featuredRefIds: "" },
      { rowId: "banner_test_b" }
    );
    const problems = validateCatalog("gacha_banners", [bannerRow(), twin], ctx());
    expect(hasErrors(problems)).toBe(false);
    expect(warningsOf(problems).join(" | ")).toContain("sortOrder 1 is shared by");
  });

  it("rule 18: a featured ref that is not in the pool is a WARNING", () => {
    const problems = validateCatalog(
      "gacha_banners",
      [bannerRow({ featuredRefIds: "club_pwedge_royal;club_wood_gf" })],
      ctx()
    );
    expect(hasErrors(problems)).toBe(false);
    expect(warningsOf(problems).join(" | ")).toContain(
      '"club_wood_gf" is featured but is not an active entry'
    );
  });
});

describe("ticket_types — rules 19-20", () => {
  it("rule 19: refuses a non-integer id, a bad key, a duplicate key and a missing locale", () => {
    const rows = ticketRows();
    rows[1]!.data.id = "one";
    expect(joined(validateCatalog("ticket_types", rows, ctx()))).toContain("must be a whole number ≥ 0");

    const badKey = ticketRows();
    badKey[1]!.data.key = "Gold Ticket";
    expect(joined(validateCatalog("ticket_types", badKey, ctx()))).toContain("must be lower-case snake");

    const dupKey = ticketRows();
    dupKey[1]!.data.key = "standard";
    expect(joined(validateCatalog("ticket_types", dupKey, ctx()))).toContain(
      'key "standard" is already used'
    );

    const noJa = ticketRows();
    noJa[0]!.data.nameJa = "";
    expect(joined(validateCatalog("ticket_types", noJa, ctx()))).toContain('"nameJa" is empty');
  });

  it("rule 20: refuses deactivating a type an active banner charges", () => {
    const rows = ticketRows();
    rows[0] = { ...rows[0]!, isActive: false };
    expect(joined(validateCatalog("ticket_types", rows, ctx()))).toContain(
      "is charged by active banner(s) banner_standard_club1"
    );
  });

  it("rule 20: a type NO active banner charges may be deactivated", () => {
    const rows = ticketRows();
    rows[1] = { ...rows[1]!, isActive: false }; // the gold ticket, which nothing charges
    expect(errorsOf(validateCatalog("ticket_types", rows, ctx()))).toEqual([]);
  });

  it("rule 20: a DEACTIVATED banner does not pin its ticket type", () => {
    const rows = ticketRows();
    rows[0] = { ...rows[0]!, isActive: false };
    const banners = [bannerRow({}, { isActive: false })];
    expect(errorsOf(validateCatalog("ticket_types", rows, ctx({ banners })))).toEqual([]);
  });
});

describe("the shared rules still apply to the gacha catalogs", () => {
  it("a missing required column blocks", () => {
    const rows = poolRows();
    delete rows[0]!.data.quantity;
    expect(joined(validateCatalog("gacha_pools", rows, ctx()))).toContain(
      'Missing required column "quantity"'
    );
  });

  it("a non-numeric numeric column blocks", () => {
    expect(
      joined(validateCatalog("gacha_banners", [bannerRow({ costX1: "fifty" })], ctx()))
    ).toContain('"fifty" is not a number');
  });

  it("data.bannerId must agree with the row id", () => {
    const row = bannerRow({ bannerId: "banner_something_else" });
    expect(joined(validateCatalog("gacha_banners", [row], ctx()))).toContain(
      'data.bannerId is "banner_something_else" but the row id is "banner_standard_club1"'
    );
  });

  it("publishing an empty gacha catalog is refused", () => {
    expect(hasErrors(validateCatalog("gacha_pools", [], ctx()))).toBe(true);
  });
});
