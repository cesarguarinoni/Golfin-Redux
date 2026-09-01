import { describe, expect, it } from "vitest";
import {
  hasErrors,
  isValidNewRowId,
  validateCatalog,
  type DraftRow,
  type ValidationContext,
} from "@/lib/contentValidate";

/**
 * `validateCatalog` is THE place a bad publish is stopped, and its own docstring
 * says it was kept pure precisely so it could be tested without a database. It
 * then went 681 lines without a test until the red-team gate escalated
 * `game_modes_admin` over exactly this (REDTEAM_REVIEW iter-3).
 *
 * SCOPE: the `modes` rules this task added, plus the two behaviours every
 * catalog depends on (errors block / warnings do not, and nothing publishes on a
 * failure). NOT an attempt to retro-cover clubs, characters or shop_catalog —
 * those rules predate this task and testing them here would be scope this task
 * did not take. That is a deliberate line, not an oversight.
 */

const row = (rowId: string, data: Record<string, unknown>, isActive = true): DraftRow => ({
  rowId,
  data,
  minBuild: 0,
  isActive,
});

/** A `modes` row that passes everything, so each test can break exactly one thing. */
const mode = (rowId: string, over: Record<string, unknown> = {}): DraftRow =>
  row(rowId, {
    id: rowId,
    title: rowId.toUpperCase(),
    entryFee: "0",
    rewards: "0",
    locked: "false",
    target: "none",
    order: "1",
    ...over,
  });

const ctx = (over: Partial<ValidationContext> = {}): ValidationContext => ({
  publishedMinBuild: new Map(),
  otherCatalogs: new Map(),
  ...over,
});

const errorsFor = (rows: DraftRow[], c = ctx()) =>
  validateCatalog("modes", rows, c).filter((p) => p.severity === "error");
const warningsFor = (rows: DraftRow[], c = ctx()) =>
  validateCatalog("modes", rows, c).filter((p) => p.severity === "warning");

describe("modes — the rules that gate a fee publish", () => {
  it("accepts the catalog as it actually ships", () => {
    // The five real modes, with the values in Assets/Resources/Data/modes.csv.
    // If this ever fails, the validator has become stricter than the data the
    // game runs on — which is the one way a validator makes itself useless.
    const shipped = [
      mode("practice", { entryFee: "10", rewards: "5", target: "hole_select", order: "2" }),
      mode("versus_1v1", { entryFee: "0", rewards: "20", target: "matchmaking_1v1", order: "1" }),
      mode("tournaments", { entryFee: "0", rewards: "0", target: "tournaments", order: "3" }),
      mode("driving_range", { entryFee: "0", rewards: "0", locked: "true", target: "none", order: "4" }),
      mode("missions", { entryFee: "0", rewards: "20", locked: "true", target: "none", order: "5" }),
    ];
    expect(validateCatalog("modes", shipped, ctx())).toEqual([]);
  });

  it("refuses a negative entryFee", () => {
    // A mode that PAYS you to enter. golfin_mode_fees has its own check
    // constraint, but failing here names the row instead of 500ing the publish.
    const problems = errorsFor([mode("practice", { entryFee: "-1" })]);
    expect(problems).toHaveLength(1);
    expect(problems[0]!.column).toBe("entryFee");
  });

  it("refuses an empty target — a PLAY button that routes nowhere", () => {
    const problems = errorsFor([mode("practice", { target: "" })]);
    expect(problems.map((p) => p.column)).toContain("target");
  });

  it("accepts an UNRECOGNISED target, because the client is what withholds it", () => {
    // Deliberate division of labour: the dashboard cannot know what the builds
    // in the wild dispatch, so ModesDatabaseCSV withholds an unroutable mode at
    // load time. Erroring here would make publishing a mode for a FUTURE build
    // impossible, which is the whole point of appending one.
    expect(errorsFor([mode("weekly", { target: "battle_royale" })])).toEqual([]);
  });

  it("refuses a `locked` value the client would silently read as false", () => {
    // GetBool treats anything it does not recognise as false, so "yes" would
    // publish a Coming Soon mode as LIVE.
    const problems = errorsFor([mode("missions", { locked: "yes" })]);
    expect(problems.map((p) => p.column)).toContain("locked");
  });

  it.each(["true", "false", "1", "0", ""])("accepts locked=%o", (locked) => {
    expect(errorsFor([mode("missions", { locked })])).toEqual([]);
  });

  it("refuses a duplicate order — the carousel sort key", () => {
    const problems = errorsFor([mode("a", { order: "2" }), mode("b", { order: "2" })]);
    expect(problems.map((p) => p.column)).toContain("order");
  });

  it("refuses a duplicate order in a FIVE-row catalog, not just a pair", () => {
    // Red-team iter-4 found this hole by exploiting it: with the clash only ever
    // tested at exactly two rows, scoping the real rule to `rows.length < 3`
    // broke it for the shipped 5-row catalog and kept all 36 tests green. A rule
    // exercised at one cardinality is a rule tested by coincidence.
    const five = [
      mode("versus_1v1", { order: "1" }),
      mode("practice", { order: "2" }),
      mode("tournaments", { order: "3" }),
      mode("driving_range", { order: "4" }),
      mode("missions", { order: "4" }), // clashes with driving_range
    ];
    const problems = errorsFor(five);
    expect(problems.map((p) => p.column)).toContain("order");
    // and it must name the SECOND of the pair, so an operator knows which to move
    expect(problems.some((p) => p.rowId === "missions")).toBe(true);
  });

  it("accepts five DISTINCT orders — the rule must not fire on the shipped set", () => {
    // The accepting direction. Without it, a rule that errors on any 5-row
    // catalog would pass the test above and make the real catalog unpublishable.
    const five = ["1", "2", "3", "4", "5"].map((order, i) => mode(`m${i}`, { order }));
    expect(errorsFor(five)).toEqual([]);
  });

  it("refuses a missing required column", () => {
    const bare = row("practice", { id: "practice", title: "PRACTICE" });
    expect(errorsFor([bare]).map((p) => p.column)).toContain("entryFee");
  });

  it("refuses publishing an empty catalog", () => {
    expect(hasErrors(validateCatalog("modes", [], ctx()))).toBe(true);
  });
});

describe("the drift warning covers versus_1v1 and NOTHING else", () => {
  // The decision of record (Cesar, 2026-08-28): card reward numbers are
  // DECOUPLED from what is paid — every mode except multiplayer shows an average
  // over a selection the player has not made yet. versus_1v1 is the one card
  // claiming an exact payout, so it is the one pair checked. These tests exist
  // so a future reader cannot "helpfully" generalise it into a mapping table
  // without a test going red.

  it("warns when the 1v1 card disagrees with versus_win.pts", () => {
    const warnings = warningsFor([mode("versus_1v1", { rewards: "20" })], ctx({ versusWinPts: 25 }));
    expect(warnings).toHaveLength(1);
    expect(warnings[0]!.rowId).toBe("versus_1v1");
    expect(warnings[0]!.message).toContain("25");
  });

  it("is a WARNING, not an error — a two-step change must stay publishable", () => {
    const problems = validateCatalog("modes", [mode("versus_1v1", { rewards: "20" })], ctx({ versusWinPts: 25 }));
    expect(hasErrors(problems)).toBe(false);
  });

  it("prefers reward1Amount over the legacy `rewards` int", () => {
    const warnings = warningsFor(
      [mode("versus_1v1", { rewards: "999", reward1Amount: "25" })],
      ctx({ versusWinPts: 25 })
    );
    expect(warnings).toEqual([]);
  });

  it("says nothing when they agree", () => {
    expect(warningsFor([mode("versus_1v1", { rewards: "25" })], ctx({ versusWinPts: 25 }))).toEqual([]);
  });

  it("NEVER warns about any other mode, whatever its reward says", () => {
    // The regression this file exists for. practice/tournaments/missions reward
    // numbers are card copy; comparing them to an action would warn forever.
    const others = [
      mode("practice", { rewards: "5" }),
      mode("tournaments", { rewards: "0", order: "3" }),
      mode("missions", { rewards: "20", order: "5" }),
    ];
    expect(warningsFor(others, ctx({ versusWinPts: 25 }))).toEqual([]);
  });

  it("stays silent when versus_win.pts was not loaded, or is NULL", () => {
    // undefined = the publish path did not load it (a non-modes publish, or the
    // advisory lookup blipped). null = the action exists with a client-supplied
    // amount. Neither is something to warn about.
    expect(warningsFor([mode("versus_1v1", { rewards: "20" })], ctx())).toEqual([]);
    expect(warningsFor([mode("versus_1v1", { rewards: "20" })], ctx({ versusWinPts: null }))).toEqual([]);
  });
});

describe("row ids a `+ New row` may mint", () => {
  it("accepts lower snake and refuses what the exporter could not resolve", () => {
    expect(isValidNewRowId("modes", "weekly_challenge")).toBe(true);
    expect(isValidNewRowId("modes", "Weekly-Challenge")).toBe(false);
    expect(isValidNewRowId("modes", "")).toBe(false);
  });

  it("caps length at 80 — the bound /points/spend was raised to match", () => {
    // MAX_MODE_ID_LEN in routers/points.py is 80 BECAUSE of this. An id longer
    // here than there is a mode that publishes and can never be paid for.
    expect(isValidNewRowId("modes", "m".repeat(80))).toBe(true);
    expect(isValidNewRowId("modes", "m".repeat(81))).toBe(false);
  });
});

/**
 * shop_catalog — the TWO ticket rules (gacha_server_pull §5.2).
 *
 * SCOPE, deliberately narrow for the same reason the header states: this covers
 * the two rules THIS task added (G1-T and G3-Q) and nothing else about
 * shop_catalog. Retro-covering G1, G2 and the sale-price rules would be scope
 * this task did not take.
 *
 * WHY THESE TWO ARE WORTH PINNING. G1-T is the ONLY thing standing between an
 * operator and a live ticket listing that the shipped client cannot apply — the
 * server would charge the RP, credit the ledger correctly, and the player would
 * be shown a failure. And `min_build` is immutable once published, so there is
 * no fixing it afterwards. G3-Q closes the trap that `quantity` opens: the
 * server reads it for `ticket` only, so a `quantity: 5` on a ball row is a
 * number an operator wrote that means nothing.
 */

const shopRow = (rowId: string, data: Record<string, unknown>, over: Partial<DraftRow> = {}): DraftRow => ({
  rowId,
  data: { entryId: rowId, sortOrder: "1", ...data },
  minBuild: 0,
  isActive: true,
  ...over,
});

/**
 * `otherCatalogs` entries so the referential-integrity rule is satisfied.
 *
 * The referenced rows are full `DraftRow`s, `data` included: rule 8's RP-band
 * WARNING reads `…?.get(refId)?.data.rarity` with no guard on `.data`, so a
 * stub without one throws inside the validator rather than failing the
 * assertion. (Worth knowing: a caller that builds this map by hand and omits
 * `data` gets a TypeError from `validateCatalog`, not a validation problem.)
 */
const refCtx = () =>
  ctx({
    otherCatalogs: new Map([
      ["ticket_types", new Map([["0", row("0", { id: "0", key: "standard", nameEn: "Ticket" })]])],
      ["balls", new Map([["ball_golfin", row("ball_golfin", { id: "ball_golfin", name: "Golfin Ball" })]])],
      ["clubs", new Map([["club_driver_gf", row("club_driver_gf", { id: "club_driver_gf", name: "Driver", rarity: "Common" })]])],
    ]),
  });

const shopErrors = (rows: DraftRow[]) =>
  validateCatalog("shop_catalog", rows, refCtx()).filter((p) => p.severity === "error");

describe("shop_catalog — G1-T, the ticket build gate", () => {
  it("refuses an ACTIVE ticket row while TICKET_SHOP_BUILD is 0", () => {
    // ⚠️ This test asserts the CURRENT value of the constant on purpose. When
    // the spec-C build is archived and TICKET_SHOP_BUILD is set, this test
    // SHOULD fail — and the person setting it is exactly the person who should
    // be told to revisit the ticket rules. A test written to pass in both
    // states would gate nothing.
    const problems = shopErrors([
      shopRow("shop_ticket_5", { category: "ticket", refId: "0", rpCost: "100", quantity: "5" }),
    ]);
    expect(problems.map((p) => p.column)).toContain("min_build");
    expect(problems.find((p) => p.column === "min_build")!.message).toMatch(/ticket purchase/i);
  });

  it("leaves a DEACTIVATED ticket row alone", () => {
    // Same carve-out G1 and G2 make, for the same reason: no client renders a
    // deactivated row, and min_build is immutable once published — so gating one
    // would make a catalog permanently unpublishable with deactivation as the
    // only way out.
    const problems = shopErrors([
      shopRow(
        "shop_ticket_5",
        { category: "ticket", refId: "0", rpCost: "100" },
        { isActive: false }
      ),
    ]);
    expect(problems.map((p) => p.column)).not.toContain("min_build");
  });

  it("does not double-report a ticket row under G1 as well", () => {
    // `ticket` is neither club nor ball, so it would trip G1 too. Two errors on
    // one row for one cause — naming two different constants and two different
    // builds — is how an operator learns to skim them.
    const minBuildProblems = shopErrors([
      shopRow("shop_ticket_5", { category: "ticket", refId: "0", rpCost: "100" }),
    ]).filter((p) => p.column === "min_build");
    expect(minBuildProblems).toHaveLength(1);
  });

  it("still refuses a ticket row that points at nothing", () => {
    const problems = shopErrors([
      shopRow("shop_ticket_x", { category: "ticket", refId: "nope", rpCost: "100" }),
    ]);
    expect(problems.map((p) => p.column)).toContain("refId");
  });
});

describe("shop_catalog — G3-Q, quantity means something only for a ticket", () => {
  it("refuses quantity > 1 on a ball row", () => {
    const problems = shopErrors([
      shopRow("shop_ball_x", { category: "ball", refId: "ball_golfin", rpCost: "50", quantity: "3" }),
    ]);
    expect(problems.map((p) => p.column)).toContain("quantity");
  });

  it("accepts an explicit quantity of 1 on a non-ticket row", () => {
    // 1 is what a blank means, so writing it changes nothing and refusing it
    // would be pedantry.
    const problems = shopErrors([
      shopRow("shop_ball_x", { category: "ball", refId: "ball_golfin", rpCost: "50", quantity: "1" }),
    ]);
    expect(problems.map((p) => p.column)).not.toContain("quantity");
  });

  it("accepts a blank quantity on every category", () => {
    const problems = shopErrors([
      shopRow("shop_ball_x", { category: "ball", refId: "ball_golfin", rpCost: "50", quantity: "" }),
    ]);
    expect(problems.map((p) => p.column)).not.toContain("quantity");
  });

  it("refuses quantity 0 on a ticket row — a listing that sells nothing", () => {
    const problems = shopErrors([
      shopRow("shop_ticket_0", { category: "ticket", refId: "0", rpCost: "100", quantity: "0" }),
    ]);
    expect(problems.map((p) => p.column)).toContain("quantity");
  });

  it("refuses a non-numeric quantity (the NUMERIC column rule)", () => {
    const problems = shopErrors([
      shopRow("shop_ticket_x", { category: "ticket", refId: "0", rpCost: "100", quantity: "five" }),
    ]);
    expect(problems.map((p) => p.column)).toContain("quantity");
  });
});

/**
 * ball_data_wiring §5 — `rarity` became a REQUIRED column on `balls` when the
 * catalog went from 2 rows to 20 and every new ball was assigned a tier.
 *
 * Two rules meet on this column and neither one alone is enough: REQUIRED says
 * the KEY must be present, and the generic rarity rule says the VALUE must be
 * one of the six. A row can satisfy either while failing the other, so both are
 * asserted here.
 */
const ball = (rowId: string, over: Record<string, unknown> = {}): DraftRow =>
  row(rowId, {
    id: rowId,
    name: "Test Ball",
    brand: "TEST",
    rarity: "Common",
    power: "0",
    rebound: "0",
    windResistance: "0",
    roll: "0",
    spin: "0",
    ...over,
  });

const ballErrors = (rows: DraftRow[]) =>
  validateCatalog("balls", rows, ctx()).filter((p) => p.severity === "error");

describe("balls — rarity is required and must be one of the six", () => {
  it("accepts a row carrying a real tier", () => {
    expect(ballErrors([ball("ball_golfin")])).toEqual([]);
    expect(ballErrors([ball("ball_shimmer_g", { rarity: "Legendary" })])).toEqual([]);
  });

  it("refuses a row with NO rarity key — the pre-column shape", () => {
    // What a `content_rows` row published before 2026-08-31 looks like. It parses
    // client-side (ClubCsvParser.ParseRarity defaults to Common) but it must not
    // PUBLISH: an unstated tier is an operator omission, not a Common ball.
    const { rarity: _omitted, ...noRarity } = ball("ball_legacy").data;
    const problems = ballErrors([row("ball_legacy", noRarity)]);
    expect(problems.map((p) => p.column)).toContain("rarity");
    expect(problems.find((p) => p.column === "rarity")!.message).toMatch(/required/i);
  });

  it("refuses a BLANK rarity", () => {
    const problems = ballErrors([ball("ball_blank", { rarity: "" })]);
    expect(problems.map((p) => p.column)).toContain("rarity");
  });

  it("still ALLOWS a blank rarity where the column is optional", () => {
    // The blank rule keys off REQUIRED, not off a hardcoded catalog list. Two
    // shipped catalogs legitimately carry blank rarities — shop_catalog (7 of 8
    // rows; it is a display override) and mission_loadouts (4 of 13; it is an
    // optional filter on a club loadout). Naming shop_catalog directly, as the
    // first cut of this rule did, broke mission_loadouts' publish.
    const shop = validateCatalog("shop_catalog", [
      row("shop_x", { entryId: "shop_x", category: "club", refId: "club_driver_gf",
                      rpCost: "100", sortOrder: "1", rarity: "" }),
    ], refCtx()).filter((p) => p.severity === "error");
    expect(shop.map((p) => p.column)).not.toContain("rarity");

    const loadout = validateCatalog("mission_loadouts", [
      row("OWN", { id: "OWN", kind: "own", clubs: "driver", weight: "1",
                   allowedStartKinds: "tee", rarity: "" }),
    ], ctx()).filter((p) => p.severity === "error");
    expect(loadout.map((p) => p.column)).not.toContain("rarity");
  });

  it("refuses a tier that is not one of the six", () => {
    const problems = ballErrors([ball("ball_bogus", { rarity: "Platinum" })]);
    const rarityProblem = problems.find((p) => p.column === "rarity");
    expect(rarityProblem).toBeDefined();
    expect(rarityProblem!.message).toMatch(/Platinum/);
  });
});

// ── mission_loadouts — the mask VOCABULARY (publish_blocked_catalogs) ────────
//
// The grammar itself, and its parity with the C# resolver, live in
// loadoutTokens.test.ts. What is here is the two rules the validator gained: a
// token it does not know, and a ban that bans nothing. Both are errors, because
// a mission whose card promises a restriction and then does not apply it is
// broken content — `ban:Iron7,Iron9` named the two iron models the design
// workbook knew and let Iron 4/5/6/8, 96 of the 114 shipped irons, straight through.

const loadoutClubs = () =>
  new Map([
    ["club_iron_gf", row("club_iron_gf", { id: "club_iron_gf", name: "Iron 5 G&F", type: "Iron", rarity: "Common" })],
    ["club_putter_gf", row("club_putter_gf", { id: "club_putter_gf", name: "Putter G&F", type: "Putter", rarity: "Common" })],
    ["club_wood_old", row("club_wood_old", { id: "club_wood_old", name: "Wood G&F", type: "Wood", rarity: "Common" }, false)],
  ]);

const loadoutErrors = (rows: DraftRow[]) =>
  validateCatalog("mission_loadouts", rows, ctx({ otherCatalogs: new Map([["clubs", loadoutClubs()]]) })).filter(
    (p) => p.severity === "error"
  );

const ownLoadout = (rowId: string, clubs: string): DraftRow =>
  row(rowId, { id: rowId, kind: "own", clubs, rarity: "", weight: "0", allowedStartKinds: "any" });

describe("mission_loadouts — club tokens", () => {
  it("accepts ban:Iron, the family token", () => {
    expect(loadoutErrors([ownLoadout("OWN_NO_IRONS", "ban:Iron")])).toEqual([]);
  });

  it("reports an unknown club token", () => {
    // "Hybrid" is a club a designer might reasonably type. Nothing in the grammar
    // answers to it, so the ban would be silently inert — the exact failure mode
    // this rule exists to make loud.
    const problems = loadoutErrors([ownLoadout("OWN_NO_HYBRIDS", "ban:Hybrid")]);
    expect(problems).toHaveLength(1);
    expect(problems[0]!.column).toBe("clubs");
    expect(problems[0]!.message).toMatch(/Unknown club token "Hybrid"/);
  });

  it("reports a ban that bans nothing", () => {
    // `Iron9` is a KNOWN token — it just matches no active row in this catalog
    // (the only iron here is a 5). A ban nobody feels is a mission lying on its card.
    const problems = loadoutErrors([ownLoadout("OWN_NO_NINES", "ban:Iron9")]);
    expect(problems).toHaveLength(1);
    expect(problems[0]!.message).toMatch(/bans nothing/);
  });

  it("counts only ACTIVE clubs when deciding a ban bans nothing", () => {
    // The only Wood in the catalog is deactivated, so `ban:Wood` reaches nothing
    // a player can hold. Same rule as the supplied side, which has always been
    // active-only.
    expect(loadoutErrors([ownLoadout("OWN_NO_WOODS", "ban:Wood")])[0]!.message).toMatch(/bans nothing/);
  });

  it("leaves `*` alone", () => {
    expect(loadoutErrors([ownLoadout("OWN", "*")])).toEqual([]);
  });

  it("resolves a supplied bag through the same grammar", () => {
    // `Iron5` against a row whose `type` column is the bare "Iron" — the compare
    // that used to be `type === token`, and the reason all 17 errors were false.
    const supplied = row("SUP", {
      id: "SUP", kind: "supplied", clubs: "Iron5,Putter", rarity: "Common",
      weight: "0", allowedStartKinds: "any",
    });
    expect(loadoutErrors([supplied])).toEqual([]);
  });
});

// ── gacha_pools — the deactivated-row carve-out ─────────────────────────────

const poolCtx = () =>
  ctx({
    otherCatalogs: new Map([
      ["balls", new Map([["ball_golfin", row("ball_golfin", { id: "ball_golfin", name: "Golfin", rarity: "Common", isDefault: "true" })]])],
    ]),
  });

const poolRow = (over: Record<string, unknown> = {}, isActive = true): DraftRow =>
  row(
    "psc1_ball_golfin",
    {
      id: "psc1_ball_golfin", poolId: "pool_standard_club1", kind: "ball",
      refId: "ball_golfin", rarity: "Common", weight: "60", quantity: "3",
      dupeRp: "0", featured: "false", ...over,
    },
    isActive
  );

const poolErrors = (rows: DraftRow[]) =>
  validateCatalog("gacha_pools", rows, poolCtx()).filter((p) => p.severity === "error");

describe("gacha_pools — leaves a DEACTIVATED pool row alone", () => {
  it("still refuses the default ball while the row is ACTIVE", () => {
    // The control. Without it the carve-out below could pass by the rule simply
    // having been deleted.
    expect(poolErrors([poolRow()])[0]!.message).toMatch(/DEFAULT ball/);
  });

  it("says nothing about refId once the row is deactivated", () => {
    // `psc1_ball_golfin` is the row an operator switched off BY HAND when they
    // noticed 11 % of every Common pull was a no-op. Rule 21 then fired on the
    // switched-off row: one error, and gacha_pools could not be published at all,
    // with no remedy the rule itself would accept. Same carve-out, same reason, as
    // "leaves a DEACTIVATED ticket row alone" above.
    expect(poolErrors([poolRow({}, false)]).map((p) => p.column)).not.toContain("refId");
  });

  it("but a deactivated row must still be a SANE row", () => {
    // Rules 6 and 7 stay outside the guard on purpose: reactivating a row is one
    // click, and no publish gate runs in between.
    const problems = poolErrors([poolRow({ rarity: "Platinum" }, false)]);
    expect(problems.map((p) => p.column)).toContain("rarity");
    expect(poolErrors([poolRow({ weight: "0" }, false)]).map((p) => p.column)).toContain("weight");
  });
});
