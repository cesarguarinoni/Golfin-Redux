import "server-only";
import { mockDb } from "./mockStore";
import { isMockMode } from "./mode";
import { getSupabaseAdmin } from "./supabaseAdmin";
import { DAILY_BASE_RP } from "./contentValidate";
import type { RewardActionRow, RewardActionsResponse, RewardDrift } from "./types";

/**
 * Read side of the Rewards panel — `public.game_point_actions`.
 *
 * NOT the content pipeline. There is no `content_drafts` half to diff against
 * and no published version to show, because the earn path
 * (`POST /points/earn-game`) reads this table on every request. What you see
 * here is what the next earn pays.
 *
 * Branches mock ↔ live like lib/noticeData.ts.
 */

type Row = Record<string, unknown>;

/**
 * A nullable integer column. NULL and "absent" are the SAME answer here and
 * both mean something specific — "no fixed amount" / "no cap" — so this must
 * never coerce to 0. A `pts` of 0 is an action that pays nothing; a `pts` of
 * NULL is an action whose amount the client supplies. Collapsing the two would
 * silently turn every variable payout into a free one.
 */
function nullableInt(v: unknown): number | null {
  if (v === null || v === undefined) return null;
  const n = Number(v);
  return Number.isFinite(n) ? Math.trunc(n) : null;
}

function mapAction(r: Row): RewardActionRow {
  return {
    action: String(r.action ?? ""),
    pts: nullableInt(r.pts),
    maxPerEvent: nullableInt(r.max_per_event),
    dailyCap: nullableInt(r.daily_cap),
    oncePerUser: r.once_per_user === true,
  };
}

/** Every action, alphabetically — the order `select * ... order by action` gives
 *  in the migration's own verification query, so the panel and a psql session
 *  read the same. */
export async function fetchRewardActions(): Promise<RewardActionsResponse> {
  if (isMockMode()) {
    return { actions: sortActions(mockDb().rewardActions), mock: true };
  }

  const res = await getSupabaseAdmin()
    .from("game_point_actions")
    .select("action, pts, max_per_event, daily_cap, once_per_user");
  if (res.error) throw new Error(`game_point_actions query failed: ${res.error.message}`);

  const actions = sortActions((res.data as Row[]).map(mapAction));
  return { actions, mock: false, missionDrift: await missionDrift(actions) };
}

/**
 * The two cross-surface checks the missions economy needs (missions_v1 §A6).
 *
 * They live HERE rather than in the publish validator because they are about
 * what is ALREADY published: the validator can stop a mission that would exceed
 * today's cap, but it cannot stop somebody LOWERING the cap afterwards, and
 * that is the change that silently breaks payouts. A mission whose
 * `firstClearRP` is above `mission_clear.max_per_event` is a mission a player
 * clears and is paid NOTHING for — the "wrongly earn nothing" half of the
 * standing invariant, arrived at from the other direction.
 *
 * BEST-EFFORT, ALWAYS. A read failure returns no warnings rather than throwing:
 * this decorates a panel whose actual job is editing the table, and taking that
 * panel down because an advisory lookup blipped would be the tail wagging the
 * dog.
 */
async function missionDrift(actions: RewardActionRow[]): Promise<RewardDrift[]> {
  const out: RewardDrift[] = [];

  const daily = actions.find((a) => a.action === "daily_mission");
  if (daily && daily.pts !== null && daily.pts !== DAILY_BASE_RP) {
    out.push({
      action: "daily_mission",
      message:
        `daily_mission pays ${daily.pts} RP but the design's base is ${DAILY_BASE_RP} ` +
        "(DailyRewards.baseRP). One of the two has moved without the other.",
    });
  }

  const clear = actions.find((a) => a.action === "mission_clear");
  if (!clear || clear.maxPerEvent === null) return out;

  try {
    const res = await getSupabaseAdmin()
      .from("golfin_mission_rewards")
      .select("mission_id, first_clear_rp")
      .gt("first_clear_rp", clear.maxPerEvent)
      .eq("is_active", true);
    if (res.error) return out;
    const over = (res.data ?? []) as Array<{ mission_id: string; first_clear_rp: number }>;
    if (over.length > 0) {
      const shown = over.slice(0, 6).map((r) => `#${r.mission_id} (${r.first_clear_rp})`).join(", ");
      out.push({
        action: "mission_clear",
        message:
          `${over.length} published mission(s) pay more than max_per_event ${clear.maxPerEvent} — ` +
          `${shown}${over.length > 6 ? `, +${over.length - 6} more` : ""}. A player who clears one ` +
          "is refused and paid NOTHING. Raise the cap, or lower those missions.",
      });
    }
  } catch {
    // See the doc comment: advisory only.
  }
  return out;
}

/** One action, or null. The mutation path's before-image. */
export async function fetchRewardAction(action: string): Promise<RewardActionRow | null> {
  if (isMockMode()) {
    return mockDb().rewardActions.find((a) => a.action === action) ?? null;
  }

  const res = await getSupabaseAdmin()
    .from("game_point_actions")
    .select("action, pts, max_per_event, daily_cap, once_per_user")
    .eq("action", action)
    .maybeSingle();
  if (res.error) throw new Error(`game_point_actions query failed: ${res.error.message}`);
  return res.data ? mapAction(res.data as Row) : null;
}

export function sortActions(rows: RewardActionRow[]): RewardActionRow[] {
  return [...rows].sort((a, b) => a.action.localeCompare(b.action));
}
