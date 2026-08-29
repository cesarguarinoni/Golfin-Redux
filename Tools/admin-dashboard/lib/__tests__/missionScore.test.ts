import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";
import { holeBase, scoreGoal, scoreMission, tierForScore, type WeightRow } from "../missionScore";

/**
 * The mission difficulty scorer (missions_v1 §A6).
 *
 * THE CENTRAL TEST HERE IS THE FIXED POINT, and it is the only honest way to
 * check this code. `mission_goal_weights.csv` is an EXPANSION of the design
 * workbook's prose — "≤150→0, ≤200→1, else 2" became three rows with a `match`
 * column — so the expansion is either faithful or it is a quiet re-tiering of
 * the whole campaign. The proof is that scoring the 40 SHIPPED missions with it
 * reproduces the 40 `difficultyScore` values the designer wrote by hand.
 *
 * It also pins this implementation against the OTHER one. `scoreGoal` here and
 * `goal_weight` in services/daily_mission.py are the same function in two
 * languages, because the dashboard cannot import Python and the daily generator
 * cannot import TypeScript. Both run this same fixed point
 * (`test_the_scorer_reproduces_all_forty_shipped_difficulty_scores` on the
 * backend), so a divergence fails one of them.
 *
 * The CSVs are read from the game repo rather than fixtured: a fixture would
 * drift from the file that actually ships, which is exactly the failure this
 * test exists to catch.
 */

const DATA = join(__dirname, "../../../../Assets/Resources/Data");

function load(name: string): Array<Record<string, string>> {
  const lines = readFileSync(join(DATA, name), "utf-8")
    .split("\n")
    .filter((line) => line.length > 0 && !line.startsWith("#"));
  const header = parse(lines[0]!);
  return lines.slice(1).map((line) => {
    const values = parse(line);
    return Object.fromEntries(header.map((h, i) => [h, values[i] ?? ""]));
  });
}

/** RFC4180-ish single line: the mission CSVs quote comma-bearing club masks. */
function parse(line: string): string[] {
  const out: string[] = [];
  let field = "";
  let quoted = false;
  for (let i = 0; i < line.length; i++) {
    const ch = line[i];
    if (quoted) {
      if (ch === '"' && line[i + 1] === '"') { field += '"'; i++; }
      else if (ch === '"') quoted = false;
      else field += ch;
    } else if (ch === '"') quoted = true;
    else if (ch === ",") { out.push(field); field = ""; }
    else field += ch;
  }
  out.push(field);
  return out;
}

const weightRows: WeightRow[] = load("mission_goal_weights.csv").map((r) => ({
  goal: r.goal!, match: r.match!, scope: r.scope!, param: r.param!, weight: r.weight!,
}));

describe("difficulty scoring against the 40 shipped missions", () => {
  const missions = load("missions.csv");
  const areas = new Map(load("mission_start_areas.csv").map((r) => [r.areaId!, r]));
  const winds = new Map(load("mission_wind_presets.csv").map((r) => [r.id!, r]));
  const loadouts = new Map(load("mission_loadouts.csv").map((r) => [r.id!, r]));

  it("has all 40 campaign rows", () => {
    expect(missions).toHaveLength(40);
  });

  it.each(missions.map((m) => [m.id!, m.key!, m] as const))(
    "mission %s (%s) scores exactly what the workbook says",
    (_id, _key, m) => {
      const area = areas.get(m.startAreaId!)!;
      const score = scoreMission(weightRows, {
        par: Number(m.par),
        startAreaWeight: Number(area.weight),
        startKind: area.kind!,
        windWeight: Number(winds.get(m.windPresetId!)!.weight),
        loadoutWeight: Number(loadouts.get(m.loadoutId!)!.weight),
        goals: [1, 2, 3]
          .map((slot) => ({ type: m[`goal${slot}Type`]!, param: m[`goal${slot}Param`]! }))
          .filter((g) => g.type !== ""),
      });
      expect(score).toBe(Number(m.difficultyScore));
    }
  );

  it("puts every mission in the tier the catalog assigns it", () => {
    const tiers = load("mission_tiers.csv").map((r) => ({
      tier: r.tier!,
      scoreMin: Number(r.scoreMin),
      scoreMaxExcl: Number(r.scoreMaxExcl),
    }));
    for (const m of missions) {
      expect(tierForScore(tiers, Number(m.difficultyScore))).toBe(m.tier);
    }
  });
});

describe("the match kinds", () => {
  it("exact wins over lte and default", () => {
    expect(scoreGoal(weightRows, "SCORE", "0", "tee", 4)).toBe(2);
    expect(scoreGoal(weightRows, "PUTTS", "1", "short", 4)).toBe(3);
  });

  it("lte takes the FIRST bound the value fits, in file order", () => {
    expect(scoreGoal(weightRows, "DIST", "150", "tee", 4)).toBe(0);
    expect(scoreGoal(weightRows, "DIST", "175", "tee", 4)).toBe(1);
    expect(scoreGoal(weightRows, "DIST", "300", "tee", 4)).toBe(2); // default
  });

  it("as_score scores SHOTS from a TEE start through the SCORE ladder", () => {
    // 3 strokes on a par 4 is a birdie, which the SCORE ladder weighs 4.
    expect(scoreGoal(weightRows, "SHOTS", "3", "tee", 4)).toBe(4);
    // The same 3 from a SHORT start is the absolute ladder instead: 3+ = 0.
    expect(scoreGoal(weightRows, "SHOTS", "3", "short", 4)).toBe(0);
  });

  it("scope narrows a row to one start kind", () => {
    expect(scoreGoal(weightRows, "SHOTS", "1", "short", 3)).toBe(3);
    expect(scoreGoal(weightRows, "SHOTS", "2", "short", 3)).toBe(1);
  });

  it("an unknown goal scores 0 rather than throwing", () => {
    expect(scoreGoal(weightRows, "NOT_A_GOAL", "7", "tee", 4)).toBe(0);
  });

  it("hole base is the par term", () => {
    expect(holeBase(weightRows, 3)).toBe(0);
    expect(holeBase(weightRows, 4)).toBe(1);
    expect(holeBase(weightRows, 5)).toBe(2);
  });
});
