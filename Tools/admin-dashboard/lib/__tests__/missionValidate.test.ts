import { describe, expect, it } from "vitest";
import { hasErrors, validateCatalog, type DraftRow, type ValidationContext } from "../contentValidate";

/**
 * The mission publish rules (missions_v1 §A6, contentValidate rules 11-17).
 *
 * WHAT THESE EXIST TO CATCH is one class of bug with two faces, and both faces
 * are the standing invariant: a client must never show a broken card, and never
 * wrongly spend or earn. A composed mission can be broken in ways a flat catalog
 * row cannot — a putter-only bag from the fairway is unplayable, a start area
 * that resolves to nothing has nowhere to put the ball, a supplied loadout
 * naming a club nobody makes hands the player an empty bag. Every one of those
 * is a card that reaches a player and dead-ends, so every one of them BLOCKS.
 *
 * The mission_clear CAP rule is the "wrongly earn" half: a firstClearRP above
 * `max_per_event` is a mission a player clears and is paid NOTHING for, because
 * the claim path refuses the amount. That is worse than a visual bug and it is
 * invisible until somebody actually clears the mission.
 */

const mission = (over: Record<string, unknown> = {}, rowId = "1"): DraftRow => ({
  rowId,
  minBuild: 0,
  isActive: true,
  data: {
    id: rowId,
    order: rowId,
    tier: "Beginner",
    key: "b_test",
    holeId: "4",
    par: "3",
    startAreaId: "GREEN",
    windPresetId: "CALM",
    loadoutId: "SUP_PUTTER",
    goal1Type: "SHOTS",
    goal1Param: "3",
    goal2Type: "",
    goal2Param: "",
    goal3Type: "",
    goal3Param: "",
    difficultyScore: "0",
    firstClearRP: "15",
    replayRP: "5",
    courseId: "lomond-country-club",
    pinIndex: "0",
    staminaDrain: "3",
    unlock: "start",
    ...over,
  },
});

const draft = (rowId: string, data: Record<string, unknown>, isActive = true): DraftRow => ({
  rowId,
  data,
  minBuild: 0,
  isActive,
});

function components(over: Partial<Record<string, Map<string, DraftRow>>> = {}) {
  const areas = new Map<string, DraftRow>([
    ["lomond_h04_green", draft("lomond_h04_green", {
      id: "lomond_h04_green", courseId: "lomond-country-club", holeId: "4",
      areaId: "GREEN", label: "Green", kind: "short", weight: "0",
      x: "1", y: "1", z: "1", pin_count: "3",
    })],
    ["lomond_h04_fairway", draft("lomond_h04_fairway", {
      id: "lomond_h04_fairway", courseId: "lomond-country-club", holeId: "4",
      areaId: "FAIRWAY", label: "Fairway", kind: "short", weight: "1",
      x: "1", y: "1", z: "1", pin_count: "3",
    })],
    ["lomond_h04_tee_back", draft("lomond_h04_tee_back", {
      id: "lomond_h04_tee_back", courseId: "lomond-country-club", holeId: "4",
      areaId: "TEE_BACK", label: "Back tee", kind: "tee", weight: "5",
      x: "", y: "", z: "", pin_count: "",
    })],
    ["lomond_h04_sand", draft("lomond_h04_sand", {
      id: "lomond_h04_sand", courseId: "lomond-country-club", holeId: "4",
      areaId: "SAND", label: "Bunker", kind: "short", weight: "2",
      x: "", y: "", z: "", pin_count: "",
    }, false)],
  ]);
  const winds = new Map<string, DraftRow>([
    ["CALM", draft("CALM", { id: "CALM", label: "Calm", relDirDeg: "0", speed: "0", weight: "0" })],
  ]);
  const loadouts = new Map<string, DraftRow>([
    ["SUP_PUTTER", draft("SUP_PUTTER", {
      id: "SUP_PUTTER", kind: "supplied", clubs: "Putter", rarity: "Common",
      weight: "0", allowedStartKinds: "green", label: "Putter only",
    })],
    ["SUP_FULL", draft("SUP_FULL", {
      id: "SUP_FULL", kind: "supplied", clubs: "Driver,Putter", rarity: "Common",
      weight: "0", allowedStartKinds: "any", label: "Full",
    })],
  ]);
  const weights = new Map<string, DraftRow>([
    ["shots_short_more", draft("shots_short_more", { id: "shots_short_more", goal: "SHOTS", match: "default", scope: "short", param: "", weight: "0" })],
    ["score_par", draft("score_par", { id: "score_par", goal: "SCORE", match: "exact", scope: "", param: "0", weight: "2" })],
    ["hole_par3", draft("hole_par3", { id: "hole_par3", goal: "HOLE_BASE", match: "exact", scope: "", param: "3", weight: "0" })],
    ["no_hazard", draft("no_hazard", { id: "no_hazard", goal: "NO_HAZARD", match: "any", scope: "", param: "", weight: "1" })],
  ]);
  const tiers = new Map<string, DraftRow>([
    ["Beginner", draft("Beginner", { tier: "Beginner", order: "1", scoreMin: "0", scoreMaxExcl: "6", firstClearRP: "15", replayRP: "5", tierClearBonusRP: "50", unlockClears: "0", missionsInTier: "10" })],
  ]);

  return new Map<string, Map<string, DraftRow>>([
    ["mission_start_areas", over.mission_start_areas ?? areas],
    ["mission_wind_presets", over.mission_wind_presets ?? winds],
    ["mission_loadouts", over.mission_loadouts ?? loadouts],
    ["mission_goal_weights", over.mission_goal_weights ?? weights],
    ["mission_tiers", over.mission_tiers ?? tiers],
  ]);
}

function run(rows: DraftRow[], extra: Partial<ValidationContext> = {}, catalog = "missions") {
  return validateCatalog(catalog, rows, {
    publishedMinBuild: new Map(),
    otherCatalogs: components(),
    ...extra,
  });
}

const errorsOn = (problems: ReturnType<typeof run>, column: string) =>
  problems.filter((p) => p.severity === "error" && p.column === column);

// ── missions ────────────────────────────────────────────────────────────────

describe("missions — the composed row", () => {
  it("accepts the shipped shape", () => {
    expect(hasErrors(run([mission()]))).toBe(false);
  });

  it("blocks a start area that does not exist on that hole", () => {
    const problems = run([mission({ holeId: "9" })]);
    expect(errorsOn(problems, "startAreaId")).toHaveLength(1);
  });

  it("blocks a DEACTIVATED start area — that is how 'no bunker here' is said", () => {
    const problems = run([mission({ startAreaId: "SAND", loadoutId: "SUP_FULL" })]);
    expect(errorsOn(problems, "startAreaId")[0]!.message).toContain("deactivated");
  });

  it("blocks a green-only loadout from a non-green start", () => {
    // SUP_PUTTER is allowedStartKinds=green; FAIRWAY is short but not GREEN.
    const problems = run([mission({ startAreaId: "FAIRWAY" })]);
    expect(errorsOn(problems, "loadoutId")).toHaveLength(1);
  });

  it("allows a green-only loadout from the GREEN itself", () => {
    expect(hasErrors(run([mission()]))).toBe(false);
  });

  it("blocks a DUPLICATE goal type — the mockup's repeated bullet is filler", () => {
    const problems = run([
      mission({ goal2Type: "SHOTS", goal2Param: "2" }),
    ]);
    expect(errorsOn(problems, "goal2Type")).toHaveLength(1);
    expect(errorsOn(problems, "goal2Type")[0]!.message).toContain("twice");
  });

  it("blocks a goal with no weight row — it would silently score 0", () => {
    const problems = run([mission({ goal2Type: "GIR", goal2Param: "" })]);
    expect(errorsOn(problems, "goal2Type")[0]!.message).toContain("mission_goal_weights");
  });

  it("blocks a non-numeric param on a numeric goal", () => {
    const problems = run([mission({ goal1Type: "SCORE", goal1Param: "fairway" })]);
    expect(errorsOn(problems, "goal1Param")).toHaveLength(1);
  });

  it("blocks a param with no type", () => {
    const problems = run([mission({ goal2Type: "", goal2Param: "3" })]);
    expect(errorsOn(problems, "goal2Param")).toHaveLength(1);
  });

  it("blocks a mission with no goals at all", () => {
    const problems = run([mission({ goal1Type: "", goal1Param: "" })]);
    expect(errorsOn(problems, "goal1Type")).toHaveLength(1);
  });

  it("blocks firstClearRP above mission_clear.max_per_event", () => {
    const problems = run([mission({ firstClearRP: "90" })], { missionClearMax: 60 });
    const err = errorsOn(problems, "firstClearRP")[0]!;
    expect(err.message).toContain("max_per_event");
    expect(err.message).toContain("paid nothing");
  });

  it("does not run the cap rule when the lookup did not load", () => {
    expect(hasErrors(run([mission({ firstClearRP: "90" })]))).toBe(false);
  });

  it("blocks an unlock that names a mission not in the catalog", () => {
    const problems = run([mission({ unlock: "clear:99" })]);
    expect(errorsOn(problems, "unlock")).toHaveLength(1);
  });

  it("accepts an unlock chain inside the catalog", () => {
    const rows = [mission({ unlock: "start" }, "1"), mission({ unlock: "clear:1", order: "2" }, "2")];
    expect(errorsOn(run(rows), "unlock")).toHaveLength(0);
  });

  it("blocks a duplicate campaign order", () => {
    const rows = [mission({}, "1"), mission({ order: "1" }, "2")];
    expect(errorsOn(run(rows), "order")).toHaveLength(1);
  });

  it("blocks a hole outside 1-18", () => {
    expect(errorsOn(run([mission({ holeId: "24" })]), "holeId")).toHaveLength(1);
  });

  it("WARNS, not blocks, when the stored difficultyScore has drifted", () => {
    const problems = run([mission({ difficultyScore: "99" })]);
    expect(hasErrors(problems)).toBe(false);
    expect(problems.some((p) => p.severity === "warning" && p.column === "difficultyScore")).toBe(true);
  });
});

// ── mission_loadouts ────────────────────────────────────────────────────────

describe("mission_loadouts — a supplied bag must exist", () => {
  const clubs = new Map<string, DraftRow>([
    ["club_putter_common", draft("club_putter_common", { id: "club_putter_common", type: "Putter", rarity: "Common" })],
  ]);

  const validate = (row: DraftRow) =>
    validateCatalog("mission_loadouts", [row], {
      publishedMinBuild: new Map(),
      otherCatalogs: new Map([["clubs", clubs]]),
    });

  it("accepts a supplied bag whose every club type resolves", () => {
    expect(hasErrors(validate(draft("SUP_PUTTER", {
      id: "SUP_PUTTER", kind: "supplied", clubs: "Putter", rarity: "Common",
      weight: "0", allowedStartKinds: "green",
    })))).toBe(false);
  });

  it("BLOCKS a supplied bag naming a club nobody makes at that rarity", () => {
    const problems = validate(draft("SUP_BAD", {
      id: "SUP_BAD", kind: "supplied", clubs: "Putter,Driver", rarity: "Common",
      weight: "0", allowedStartKinds: "any",
    }));
    expect(errorsOn(problems, "clubs")[0]!.message).toContain("unplayable");
  });

  it("blocks an own loadout whose mask is neither * nor ban:", () => {
    const problems = validate(draft("OWN_BAD", {
      id: "OWN_BAD", kind: "own", clubs: "Driver", rarity: "",
      weight: "0", allowedStartKinds: "any",
    }));
    expect(errorsOn(problems, "clubs")).toHaveLength(1);
  });

  it("blocks an unknown allowedStartKinds", () => {
    const problems = validate(draft("OWN", {
      id: "OWN", kind: "own", clubs: "*", rarity: "", weight: "0", allowedStartKinds: "beach",
    }));
    expect(errorsOn(problems, "allowedStartKinds")).toHaveLength(1);
  });
});

// ── mission_start_areas ─────────────────────────────────────────────────────

describe("mission_start_areas — the baked table", () => {
  const validate = (rows: DraftRow[]) =>
    validateCatalog("mission_start_areas", rows, {
      publishedMinBuild: new Map(),
      otherCatalogs: new Map(),
    });

  const area = (rowId: string, over: Record<string, unknown> = {}) =>
    draft(rowId, {
      id: rowId, courseId: "lomond-country-club", holeId: "4", areaId: "GREEN",
      label: "Green", kind: "short", weight: "0",
      x: "1", y: "2", z: "3", pin_count: "3", ...over,
    });

  it("blocks a TEE area that carries coordinates — it must resolve to the scene marker", () => {
    const problems = validate([area("t", { areaId: "TEE_BACK", kind: "tee" })]);
    expect(errorsOn(problems, "x")).toHaveLength(1);
  });

  it("WARNS, not blocks, on an unbaked short area — that is the Phase A state", () => {
    const problems = validate([area("g", { x: "", y: "", z: "" })]);
    expect(hasErrors(problems)).toBe(false);
    expect(problems[0]!.message).toContain("Bake Start Areas");
  });

  it("blocks a hole outside 1-18", () => {
    expect(errorsOn(validate([area("g", { holeId: "0" })]), "holeId")).toHaveLength(1);
  });

  it("blocks two rows disagreeing on an area's KIND", () => {
    const problems = validate([area("a"), area("b", { kind: "tee", x: "", y: "", z: "" })]);
    expect(errorsOn(problems, "kind").length).toBeGreaterThan(0);
  });

  it("only WARNS when two rows differ on an area's weight — per-hole tuning is allowed", () => {
    const problems = validate([area("a"), area("b", { weight: "4" })]);
    expect(hasErrors(problems)).toBe(false);
    expect(problems.some((p) => p.column === "weight")).toBe(true);
  });
});

// ── mission_tiers / goal weights / daily weights ────────────────────────────

describe("mission_tiers — bands must be contiguous", () => {
  const tier = (name: string, order: string, min: string, max: string) =>
    draft(name, {
      tier: name, order, scoreMin: min, scoreMaxExcl: max, firstClearRP: "15",
      replayRP: "5", tierClearBonusRP: "50", unlockClears: "8", missionsInTier: "10",
    });

  const validate = (rows: DraftRow[]) =>
    validateCatalog("mission_tiers", rows, { publishedMinBuild: new Map(), otherCatalogs: new Map() });

  it("accepts a contiguous ladder", () => {
    expect(hasErrors(validate([tier("Beginner", "1", "0", "6"), tier("Amateur", "2", "6", "10")]))).toBe(false);
  });

  it("blocks a GAP — a score belonging to no tier", () => {
    const problems = validate([tier("Beginner", "1", "0", "6"), tier("Amateur", "2", "8", "10")]);
    expect(errorsOn(problems, "scoreMin")).toHaveLength(1);
  });

  it("blocks unlockClears above the missions in the tier", () => {
    const rows = [draft("Amateur", {
      tier: "Amateur", order: "1", scoreMin: "0", scoreMaxExcl: "6", firstClearRP: "15",
      replayRP: "5", tierClearBonusRP: "50", unlockClears: "12", missionsInTier: "10",
    })];
    expect(errorsOn(validate(rows), "unlockClears")).toHaveLength(1);
  });
});

describe("mission_goal_weights — every goal needs a row", () => {
  it("blocks a curve missing a goal type", () => {
    const rows = [draft("score_par", { id: "score_par", goal: "SCORE", match: "exact", scope: "", param: "0", weight: "2" })];
    const problems = validateCatalog("mission_goal_weights", rows, {
      publishedMinBuild: new Map(), otherCatalogs: new Map(),
    });
    // Every goal but SCORE is missing, plus the three HOLE_BASE rows.
    expect(problems.filter((p) => p.severity === "error").length).toBeGreaterThan(10);
  });

  it("blocks an unknown match kind", () => {
    const rows = [draft("x", { id: "x", goal: "SCORE", match: "roughly", scope: "", param: "0", weight: "2" })];
    const problems = validateCatalog("mission_goal_weights", rows, {
      publishedMinBuild: new Map(), otherCatalogs: new Map(),
    });
    expect(errorsOn(problems, "match")).toHaveLength(1);
  });
});

describe("daily_mission_weights — every draw group must be able to draw", () => {
  const row = (id: string, component: string, optionId: string, weight: string) =>
    draft(id, { id, component, optionId, pickWeight: weight, note: "" });

  it("blocks a component group that is entirely zero-weighted", () => {
    const rows = [
      row("b", "band", "AMATEUR", "0"),
      row("s", "startKind", "tee", "1"),
      row("l", "loadout", "SUP_FULL", "1"),
      row("w", "wind", "CALM", "1"),
      row("p", "primaryGoal", "SCORE", "1"),
      row("q", "secondaryGoal", "NONE", "1"),
      row("m", "modifier", "NONE", "1"),
    ];
    const problems = validateCatalog("daily_mission_weights", rows, {
      publishedMinBuild: new Map(), otherCatalogs: new Map(),
    });
    expect(problems.some((p) => p.severity === "error" && p.message.includes("cannot draw"))).toBe(true);
  });

  it("blocks a MISSING component group", () => {
    const rows = [row("b", "band", "AMATEUR", "60")];
    const problems = validateCatalog("daily_mission_weights", rows, {
      publishedMinBuild: new Map(), otherCatalogs: new Map(),
    });
    expect(problems.filter((p) => p.severity === "error").length).toBeGreaterThanOrEqual(6);
  });
});
