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
