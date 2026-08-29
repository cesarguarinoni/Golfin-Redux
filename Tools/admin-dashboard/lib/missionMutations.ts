import "server-only";
import { writeAudit } from "./audit";
import { validateCatalog, hasErrors, type ContentProblem, type DraftRow } from "./contentValidate";
import { fetchAllRows } from "./contentData";
import { isMockMode } from "./mode";
import { getSupabaseAdmin } from "./supabaseAdmin";
import type { DailyRecipe } from "./dailyMissionData";

/**
 * Write side of the missions feature (missions_v1 §A6).
 * Every function: server-only, called AFTER checkAdmin(), audited with
 * before/after, with a mock branch. Same shape as lib/inventoryMutations.ts.
 *
 * TWO WRITES, AND NEITHER OF THEM PAYS ANYTHING. Pinning a recipe decides what
 * players are asked to do; resetting a mission decides what a player's next
 * clear is worth. Neither touches the ledger — the RP a reset "gives back" is
 * paid by the player earning it again, through the same server-priced claim
 * path as the first time.
 */

export interface MissionOutcome {
  ok: boolean;
  status: number;
  message: string;
  problems?: ContentProblem[];
}

const ok = (message: string): MissionOutcome => ({ ok: true, status: 200, message });
const fail = (status: number, message: string, problems?: ContentProblem[]): MissionOutcome => ({
  ok: false,
  status,
  message,
  problems,
});

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

function utcToday(): string {
  return new Date().toISOString().slice(0, 10);
}

/**
 * A pinned recipe is VALIDATED LIKE A MISSION ROW (§A6), because it is one — a
 * mission composed by hand instead of by the generator, and every way a
 * hand-composed mission can be broken is a way this one can be.
 *
 * It is checked by running the REAL `missions` validator over a synthetic
 * single-row draft rather than by a second set of rules here. A second set is a
 * second thing to keep in step, and the failure it would eventually let through
 * — a putter-only loadout from the fairway, say — is a daily nobody can clear,
 * for everybody, for a whole day.
 */
async function validatePinnedRecipe(recipe: DailyRecipe): Promise<ContentProblem[]> {
  if (isMockMode()) return [];

  const goals = Array.isArray(recipe.goals) ? recipe.goals : [];
  const row: DraftRow = {
    rowId: "0",
    minBuild: 0,
    isActive: true,
    data: {
      id: "0",
      order: "0",
      tier: "Beginner",
      key: "pinned_daily",
      holeId: String(recipe.holeId ?? ""),
      par: String(recipe.par ?? ""),
      startAreaId: String(recipe.startAreaId ?? ""),
      windPresetId: String(recipe.windPresetId ?? ""),
      loadoutId: String(recipe.loadoutId ?? ""),
      goal1Type: String(goals[0]?.type ?? ""),
      goal1Param: String(goals[0]?.param ?? ""),
      goal2Type: String(goals[1]?.type ?? ""),
      goal2Param: String(goals[1]?.param ?? ""),
      goal3Type: String(goals[2]?.type ?? ""),
      goal3Param: String(goals[2]?.param ?? ""),
      firstClearRP: "0",
      replayRP: "0",
      courseId: "lomond-country-club",
      pinIndex: String(recipe.pinIndex ?? 0),
      staminaDrain: String(recipe.staminaDrain ?? 8),
      unlock: "start",
    },
  };

  const otherCatalogs = new Map<string, Map<string, DraftRow>>();
  for (const name of [
    "mission_start_areas", "mission_wind_presets", "mission_loadouts",
    "mission_goal_weights", "mission_tiers",
  ]) {
    const rows = await fetchAllRows("content_drafts", name);
    otherCatalogs.set(
      name,
      new Map(rows.map((r) => [r.rowId, { rowId: r.rowId, data: r.data, minBuild: r.minBuild, isActive: r.isActive }]))
    );
  }

  return validateCatalog("missions", [row], {
    publishedMinBuild: new Map(),
    otherCatalogs,
  }).filter(
    // The synthetic row carries a placeholder tier and zero RP so the campaign
    // rules (band drift, ladder order, the RP ceiling) can say nothing useful
    // about it. Everything that DOES apply to a daily — the components resolve,
    // the loadout admits the start, the goals are typed and not duplicated — is
    // what survives this filter.
    (p) => !["tier", "order", "firstClearRP", "replayRP", "difficultyScore"].includes(p.column ?? "")
  );
}

export async function pinDailyRecipe(
  adminEmail: string,
  date: string,
  recipe: DailyRecipe,
  recipeHash: string
): Promise<MissionOutcome> {
  const day = (date ?? "").trim();
  if (!ISO_DATE.test(day)) return fail(400, "date must be YYYY-MM-DD (UTC).");
  // A PAST DATE CANNOT BE PINNED (§A6). Today cannot either: players may already
  // be mid-round on today's recipe, and swapping it under them makes their claim
  // fail the recipe_hash check and pay nothing.
  if (day <= utcToday()) {
    return fail(400, "Only a FUTURE date can be pinned — today's recipe may already be in play.");
  }
  if (!recipe || typeof recipe !== "object") return fail(400, "recipe must be an object.");
  if (!recipeHash || typeof recipeHash !== "string") {
    return fail(400, "recipeHash is required — it is what the claim path matches against.");
  }

  const problems = await validatePinnedRecipe(recipe);
  if (hasErrors(problems)) {
    return fail(
      400,
      `${problems.filter((p) => p.severity === "error").length} problem(s) with that recipe; nothing was pinned.`,
      problems
    );
  }

  let before: unknown = null;
  if (!isMockMode()) {
    const db = getSupabaseAdmin();
    const existing = await db
      .from("daily_missions")
      .select("date, recipe, recipe_hash, pinned, pinned_by")
      .eq("date", day)
      .maybeSingle();
    before = existing.error ? null : existing.data;

    const res = await db.from("daily_missions").upsert(
      {
        date: day,
        recipe,
        recipe_hash: recipeHash,
        pinned: true,
        pinned_by: adminEmail,
        generated_at: new Date().toISOString(),
      },
      { onConflict: "date" }
    );
    if (res.error) return fail(500, `Could not pin the recipe: ${res.error.message}`);
  }

  await writeAudit(adminEmail, "missions.daily.pin", null, "daily_missions", before, {
    date: day,
    recipe,
    recipeHash,
  });
  return ok(`Recipe pinned for ${day}.`);
}

/**
 * Reset one player's progress on one mission.
 *
 * WHAT THIS DOES AND DOES NOT DO, because the difference matters to whoever is
 * answering the support ticket: it erases `clears`, `attempts` and
 * `best_strokes`, so the player's NEXT clear pays the FIRST-CLEAR amount again.
 * It does NOT claw back points already credited, and it does not touch
 * `mission_claims` — the idempotency ledger stays intact, so a client replaying
 * an old key still gets its original answer rather than being paid twice.
 */
export async function resetMissionProgress(
  adminEmail: string,
  userId: string,
  missionId: string
): Promise<MissionOutcome> {
  const mission = (missionId ?? "").trim();
  if (!mission) return fail(400, "missionId is required.");

  let before: unknown = null;
  if (!isMockMode()) {
    const db = getSupabaseAdmin();
    const existing = await db
      .from("mission_progress")
      .select("mission_id, clears, attempts, best_strokes, first_cleared_at")
      .eq("user_id", userId)
      .eq("mission_id", mission)
      .maybeSingle();
    if (existing.error) return fail(500, `Could not read mission progress: ${existing.error.message}`);
    if (!existing.data) {
      return fail(404, `This player has no progress on mission ${mission}.`);
    }
    before = existing.data;

    const res = await db
      .from("mission_progress")
      .delete()
      .eq("user_id", userId)
      .eq("mission_id", mission);
    if (res.error) return fail(500, `Could not reset the mission: ${res.error.message}`);
  }

  await writeAudit(adminEmail, "missions.progress.reset", userId, "mission_progress", before, {
    userId,
    missionId: mission,
  });
  return ok(`Mission ${mission} reset.`);
}
