import "server-only";
import { writeAudit } from "./audit";
import { fetchRewardAction } from "./rewardsData";
import { mockDb } from "./mockStore";
import { isMockMode } from "./mode";
import { getSupabaseAdmin } from "./supabaseAdmin";
import type { RewardActionInput, RewardActionRow } from "./types";

/**
 * Write side of the Rewards panel — `public.game_point_actions`
 * (game_modes_admin SPEC §3).
 *
 * THE SHAPE OF THIS MODULE IS THE POINT: it has exactly ONE exported mutation,
 * and it is an UPDATE. There is no create and no delete, deliberately.
 *
 *   * NO CREATE. Inserting an action no client ever sends is harmless and
 *     pointless; inserting one a client DOES send requires shipping that client
 *     anyway, at which point the action arrives with it. A button that can only
 *     produce dead rows is a button that will one day produce a live one by
 *     accident.
 *   * NO DELETE. Actions are referenced BY NAME from clients already installed.
 *     Delete `hole_replay` and every replay earn from every phone comes back
 *     `{awarded: 0, reason: "Unknown game action"}` — silently, because the
 *     queued op is consumed either way. There is no `is_active` column to
 *     deactivate instead, and inventing deactivation semantics for a table the
 *     earn path reads with a bare lookup is not a thing to do in passing.
 *
 * NO DRAFT / PUBLISH CYCLE EITHER, and the panel says so in a banner. Every
 * other economy surface in this dashboard stages an edit and publishes it; this
 * one is live on the next earn request. That asymmetry is not an oversight — it
 * mirrors the server: `_get_game_action` looks the row up per request, so there
 * is no version for a draft to become.
 *
 * Same posture as `adjustRp`: server-only, called AFTER checkAdmin(), audited
 * with a full before/after.
 */

export interface RewardOutcome {
  ok: boolean;
  status: number;
  message: string;
}

const ok = (message: string): RewardOutcome => ({ ok: true, status: 200, message });
const fail = (status: number, message: string): RewardOutcome => ({ ok: false, status, message });

/**
 * Validate one nullable, non-negative integer field.
 *
 * `null` is ALWAYS legal and always means something: for `pts` it is "the client
 * supplies the amount, bounded by the caps"; for the two caps it is "no cap".
 * Returning it unchanged is what makes the panel's blank cell a first-class
 * value rather than a way to accidentally write 0.
 */
function checkNumber(label: string, value: number | null): string | null {
  if (value === null) return null;
  if (!Number.isFinite(value)) return `${label} must be a whole number or empty.`;
  if (!Number.isInteger(value)) return `${label} must be a whole number (no decimals).`;
  if (value < 0) return `${label} must be 0 or more.`;
  return null;
}

export async function updateRewardAction(
  adminEmail: string,
  action: string,
  input: RewardActionInput
): Promise<RewardOutcome> {
  const name = (action ?? "").trim();
  if (!name) return fail(400, "action is required.");

  // The row must ALREADY EXIST. This is what makes "no create" a property of the
  // module and not merely of the UI: the route is reachable without the panel,
  // and an upsert here would quietly mint an action from a typo'd URL.
  const before: RewardActionRow | null = await fetchRewardAction(name);
  if (!before) {
    return fail(
      404,
      `No earn action "${name}". Actions cannot be created here — a client that ` +
        "sends one has to ship with it (game_modes_admin §3)."
    );
  }

  for (const [label, value] of [
    ["Points", input.pts],
    ["Max / event", input.maxPerEvent],
    ["Daily cap", input.dailyCap],
  ] as Array<[string, number | null]>) {
    const problem = checkNumber(label, value);
    if (problem) return fail(400, problem);
  }

  const after: RewardActionRow = {
    ...before,
    pts: input.pts,
    maxPerEvent: input.maxPerEvent,
    dailyCap: input.dailyCap,
  };

  if (isMockMode()) {
    const store = mockDb().rewardActions;
    const at = store.findIndex((a) => a.action === name);
    if (at >= 0) store[at] = after;
  } else {
    const res = await getSupabaseAdmin()
      .from("game_point_actions")
      .update({
        pts: input.pts,
        max_per_event: input.maxPerEvent,
        daily_cap: input.dailyCap,
      })
      .eq("action", name);
    if (res.error) return fail(500, `game_point_actions update failed: ${res.error.message}`);
  }

  // targetUser stays null — the target is the economy, not a player. The action
  // string names WHAT changed so an audit reader can tell a payout change from a
  // content publish without opening the payload, exactly like `content.publish:`.
  await writeAudit(adminEmail, "points_action_update", null, "game_point_actions", before, after);

  return ok(
    `${name}: pts ${describe(before.pts)} → ${describe(after.pts)}, ` +
      `max/event ${describe(before.maxPerEvent)} → ${describe(after.maxPerEvent)}, ` +
      `daily cap ${describe(before.dailyCap)} → ${describe(after.dailyCap)}. ` +
      "Live from the next earn request."
  );
}

const describe = (v: number | null): string => (v === null ? "(client amount / no cap)" : String(v));
