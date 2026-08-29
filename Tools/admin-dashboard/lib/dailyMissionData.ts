import "server-only";
import { isMockMode } from "./mode";
import { getSupabaseAdmin } from "./supabaseAdmin";

/**
 * Reading the daily-mission calendar (missions_v1 §A6).
 *
 * `daily_missions` is a LIVE table, not a content catalog — the same shape as
 * `tournaments`. There is no draft and no publish: a row is the recipe players
 * are served on that UTC date, from the moment it exists. That is why the panel
 * over it is a calendar and not a `CatalogPanel`.
 *
 * THE CLEAR RATE IS COUNTED, NOT SAMPLED. `daily_mission_claims` has one row per
 * (player, date), so the count for a date IS the number of players who cleared
 * it. The denominator is the number of players who have EVER claimed a daily —
 * the only honest "eligible" population this dashboard can see, since it cannot
 * know who opened the game that day and did not play. It is deliberately over
 * ALL time and not over the window: a player who last claimed in July is still
 * a player who plays dailies, and a window-scoped denominator would make the
 * rate climb whenever the window shrank.
 */

export interface DailyRecipeGoal {
  type: string;
  param: string;
}

export interface DailyRecipe {
  holeId?: number;
  par?: number;
  startAreaId?: string;
  startKind?: string;
  windPresetId?: string;
  loadoutId?: string;
  goals?: DailyRecipeGoal[];
  modifier?: string;
  band?: string;
  difficultyScore?: number;
  [key: string]: unknown;
}

export interface DailyRow {
  date: string;
  recipe: DailyRecipe;
  recipeHash: string;
  pinned: boolean;
  pinnedBy: string | null;
  generatedAt: string | null;
  claims: number;
}

export interface DailyCalendarResponse {
  rows: DailyRow[];
  everClaimed: number;
  /** Today, UTC — the panel's "a past date cannot be pinned" boundary. */
  today: string;
  mock?: boolean;
  /**
   * Set when the tables are not there yet — the window between deploying this
   * panel and applying 2026_08_29_missions.sql. It is NOT an error: a panel
   * that renders a red 500 for a migration nobody has run yet tells the
   * operator nothing they can act on, and looks identical to a panel that is
   * genuinely broken. Naming the migration turns it into an instruction.
   */
  notMigrated?: string;
}

const MOCK: DailyCalendarResponse = {
  today: "2026-08-29",
  everClaimed: 6,
  rows: [
    {
      date: "2026-08-29",
      recipe: {
        holeId: 5, par: 4, startAreaId: "TEE_REGULAR", startKind: "tee",
        windPresetId: "CROSS_L", loadoutId: "SUP_FULL", modifier: "NONE",
        band: "AMATEUR", difficultyScore: 7,
        goals: [{ type: "SCORE", param: "0" }, { type: "NO_HAZARD", param: "" }],
      },
      recipeHash: "mock-hash-today",
      pinned: false,
      pinnedBy: null,
      generatedAt: "2026-08-29T00:00:04Z",
      claims: 4,
    },
    {
      date: "2026-08-28",
      recipe: {
        holeId: 12, par: 4, startAreaId: "ROUGH", startKind: "short",
        windPresetId: "HEAD_L", loadoutId: "SUP_IRONS", modifier: "DOUBLE_RP",
        band: "AMATEUR", difficultyScore: 8,
        goals: [{ type: "SHOTS", param: "3" }],
      },
      recipeHash: "mock-hash-yesterday",
      pinned: true,
      pinnedBy: "cesar@wonderwall-g.com",
      generatedAt: "2026-08-27T09:12:00Z",
      claims: 6,
    },
  ],
  mock: true,
};

function utcToday(): string {
  return new Date().toISOString().slice(0, 10);
}

/** PostgREST's undefined-table shapes, as lib/inventoryData.ts reads them. */
function isMissingRelation(message: string): boolean {
  const text = message.toLowerCase();
  return (
    text.includes("42p01") ||
    text.includes("does not exist") ||
    text.includes("could not find the table")
  );
}

export async function fetchDailyCalendar(days = 30): Promise<DailyCalendarResponse> {
  if (isMockMode()) return MOCK;

  const today = utcToday();
  const from = new Date(Date.now() - days * 86_400_000).toISOString().slice(0, 10);
  const db = getSupabaseAdmin();

  const daily = await db
    .from("daily_missions")
    .select("date, recipe, recipe_hash, pinned, pinned_by, generated_at")
    .gte("date", from)
    .order("date", { ascending: false });
  if (daily.error) {
    if (isMissingRelation(daily.error.message)) {
      return {
        today,
        everClaimed: 0,
        rows: [],
        notMigrated:
          "`daily_missions` does not exist yet — apply backend/migrations/2026_08_29_missions.sql, " +
          "then reload. Nothing else on this page will work until it does.",
      };
    }
    throw new Error(daily.error.message);
  }

  const claims = await db
    .from("daily_mission_claims")
    .select("date, user_id")
    .gte("date", from);
  if (claims.error && !isMissingRelation(claims.error.message)) {
    throw new Error(claims.error.message);
  }

  const perDate = new Map<string, number>();
  for (const row of claims.error ? [] : claims.data ?? []) {
    const key = String((row as { date: string }).date).slice(0, 10);
    perDate.set(key, (perDate.get(key) ?? 0) + 1);
  }

  const everRows = await db.from("daily_mission_claims").select("user_id");
  const everClaimed = everRows.error
    ? 0
    : new Set((everRows.data ?? []).map((r) => String((r as { user_id: string }).user_id))).size;

  return {
    today,
    everClaimed,
    rows: (daily.data ?? []).map((r) => {
      const row = r as {
        date: string; recipe: DailyRecipe; recipe_hash: string;
        pinned: boolean; pinned_by: string | null; generated_at: string | null;
      };
      const date = String(row.date).slice(0, 10);
      return {
        date,
        recipe: row.recipe ?? {},
        recipeHash: row.recipe_hash ?? "",
        pinned: Boolean(row.pinned),
        pinnedBy: row.pinned_by ?? null,
        generatedAt: row.generated_at ?? null,
        claims: perDate.get(date) ?? 0,
      };
    }),
  };
}

// ---------------------------------------------------------------------------
// One player's mission state — the Users drawer tab (§A6)
// ---------------------------------------------------------------------------

export interface PlayerMissionRow {
  missionId: string;
  clears: number;
  attempts: number;
  bestStrokes: number | null;
  firstClearedAt: string | null;
}

export interface PlayerDailyClaim {
  date: string;
  streak: number;
  rp: number;
  strokes: number | null;
}

export interface PlayerMissionsResponse {
  missions: PlayerMissionRow[];
  dailyClaims: PlayerDailyClaim[];
  mock?: boolean;
}

const MOCK_PLAYER: PlayerMissionsResponse = {
  missions: [
    { missionId: "1", clears: 2, attempts: 3, bestStrokes: 2, firstClearedAt: "2026-08-20T10:00:00Z" },
    { missionId: "2", clears: 0, attempts: 4, bestStrokes: null, firstClearedAt: null },
  ],
  dailyClaims: [{ date: "2026-08-28", streak: 3, rp: 45, strokes: 4 }],
  mock: true,
};

export async function fetchPlayerMissions(userId: string): Promise<PlayerMissionsResponse> {
  if (isMockMode()) return MOCK_PLAYER;

  const db = getSupabaseAdmin();
  const progress = await db
    .from("mission_progress")
    .select("mission_id, clears, attempts, best_strokes, first_cleared_at")
    .eq("user_id", userId);
  // A missing table is "this player has done nothing", which is the true state
  // of every player before the migration lands — the same degrade
  // lib/inventoryData.ts and golfin_inventory.py both make, for the same reason.
  const progressRows = progress.error ? [] : progress.data ?? [];

  const claims = await db
    .from("daily_mission_claims")
    .select("date, streak, rp, strokes")
    .eq("user_id", userId)
    .order("date", { ascending: false })
    .limit(30);
  const claimRows = claims.error ? [] : claims.data ?? [];

  return {
    missions: progressRows
      .map((r) => {
        const row = r as {
          mission_id: string; clears: number; attempts: number;
          best_strokes: number | null; first_cleared_at: string | null;
        };
        return {
          missionId: String(row.mission_id),
          clears: Number(row.clears ?? 0),
          attempts: Number(row.attempts ?? 0),
          bestStrokes: row.best_strokes === null ? null : Number(row.best_strokes),
          firstClearedAt: row.first_cleared_at,
        };
      })
      .sort((a, b) => Number(a.missionId) - Number(b.missionId)),
    dailyClaims: claimRows.map((r) => {
      const row = r as { date: string; streak: number; rp: number; strokes: number | null };
      return {
        date: String(row.date).slice(0, 10),
        streak: Number(row.streak ?? 0),
        rp: Number(row.rp ?? 0),
        strokes: row.strokes === null ? null : Number(row.strokes),
      };
    }),
  };
}
