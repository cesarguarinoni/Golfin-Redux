/**
 * Mission difficulty scoring — PURE and CLIENT-SAFE.
 *
 * `difficultyScore = HOLE_BASE(par) + startArea.weight + wind.weight
 *                    + loadout.weight + Σ goal weights`
 *
 * THE STORED VALUE IS DISPLAY; THIS IS THE TRUTH (missions_v1 §A6). A
 * `missions` row carries a `difficultyScore` because the designer wrote one in
 * the workbook, but the publish RECOMPUTES it from `mission_goal_weights` and
 * the component catalogs — so tuning a weight re-tiers the campaign, which is
 * the whole reason the weights are a catalog and not constants in the client.
 *
 * ⚠️ THIS IS A SECOND IMPLEMENTATION OF `services/daily_mission.py`'s
 * `goal_weight` / `hole_base`, AND THAT IS DELIBERATE — the dashboard cannot
 * import Python and the generator cannot import TypeScript. What keeps them
 * honest is that BOTH are checked against the same fixed point: scoring the 40
 * shipped missions must reproduce the 40 `difficultyScore` values in
 * missions.csv. `test_the_scorer_reproduces_all_forty_shipped_difficulty_scores`
 * (backend) and `missionScore.test.ts` (here) are the same assertion in two
 * languages. If you change one, the other's test tells you.
 */

export interface WeightRow {
  /** goal type, or the pseudo-goals HOLE_BASE. */
  goal: string;
  /** exact | lte | default | any | as_score */
  match: string;
  /** "" (any start kind) | tee | short */
  scope: string;
  param: string;
  weight: string | number;
}

const num = (v: unknown): number | null => {
  const s = v === null || v === undefined ? "" : String(v).trim();
  if (s === "") return null;
  const n = Number(s);
  return Number.isFinite(n) ? n : null;
};

const int = (v: unknown): number => Math.trunc(num(v) ?? 0);

const text = (v: unknown): string => (v === null || v === undefined ? "" : String(v).trim());

/** The SCORE ladder, used directly and by `as_score`. */
function scoreLadder(rows: WeightRow[], relative: number): number {
  const clamped = Math.max(-2, Math.min(2, relative));
  const row = rows.find((r) => r.goal === "SCORE" && text(r.param) === String(clamped));
  return row ? int(row.weight) : 0;
}

/** The difficulty weight of ONE goal. Mirrors `goal_weight` in daily_mission.py. */
export function scoreGoal(
  rows: WeightRow[],
  goal: string,
  param: unknown,
  startKind: string,
  par: number
): number {
  if (!goal) return 0;
  const candidates = rows.filter(
    (r) => r.goal === goal && (text(r.scope) === "" || text(r.scope) === startKind)
  );
  const p = text(param);

  for (const r of candidates) {
    if (r.match === "exact" && text(r.param) === p) return int(r.weight);
  }
  for (const r of candidates) {
    if (r.match === "as_score") {
      const n = num(p);
      return n === null ? 0 : scoreLadder(rows, Math.trunc(n) - par);
    }
  }
  const value = num(p);
  if (value !== null) {
    for (const r of candidates) {
      const bound = num(r.param);
      if (r.match === "lte" && bound !== null && value <= bound) return int(r.weight);
    }
  }
  for (const r of candidates) {
    if (r.match === "default" || r.match === "any") return int(r.weight);
  }
  return 0;
}

export function holeBase(rows: WeightRow[], par: number): number {
  const row = rows.find((r) => r.goal === "HOLE_BASE" && text(r.param) === String(par));
  return row ? int(row.weight) : 0;
}

export interface MissionComponents {
  par: number;
  /** The start-area row for this mission's (hole, areaId). */
  startAreaWeight: number;
  startKind: string;
  windWeight: number;
  loadoutWeight: number;
  goals: Array<{ type: string; param: unknown }>;
}

export function scoreMission(rows: WeightRow[], m: MissionComponents): number {
  let total = holeBase(rows, m.par) + m.startAreaWeight + m.windWeight + m.loadoutWeight;
  for (const goal of m.goals) {
    total += scoreGoal(rows, goal.type, goal.param, m.startKind, m.par);
  }
  return total;
}

/** The tier a score falls in: `scoreMin <= score < scoreMaxExcl`. */
export function tierForScore(
  tiers: Array<{ tier: string; scoreMin: number; scoreMaxExcl: number }>,
  score: number
): string | null {
  const hit = tiers.find((t) => score >= t.scoreMin && score < t.scoreMaxExcl);
  return hit ? hit.tier : null;
}
