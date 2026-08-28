import "server-only";
import { mockDb } from "./mockStore";
import { isMockMode } from "./mode";
import { getSupabaseAdmin } from "./supabaseAdmin";
import type { RewardActionRow, RewardActionsResponse } from "./types";

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

  return { actions: sortActions((res.data as Row[]).map(mapAction)), mock: false };
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
