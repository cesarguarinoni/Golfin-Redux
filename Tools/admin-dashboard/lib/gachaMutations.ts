import "server-only";
import { randomUUID } from "node:crypto";
import { writeAudit } from "./audit";
import { fetchGachaEnabled, GACHA_ENABLED_KEY } from "./gachaData";
import { isMockMode } from "./mode";
import {
  MOCK_GACHA_ENABLED,
  MOCK_PLAYER_PITY,
  MOCK_TICKET_BALANCES,
  MOCK_TICKET_TRANSACTIONS,
} from "./mockGacha";
import { getSupabaseAdmin } from "./supabaseAdmin";

/**
 * Gacha ops mutations (gacha_server_pull §5, §6).
 *
 * THREE WRITES, and every one of them is audited: pause/resume, a ticket
 * grant/adjust, and a pity reset.
 *
 * ⚠️ THE TICKET WRITE GOES THROUGH `golfin_ticket_credit`, NEVER THROUGH AN
 * INSERT. That function is the only writer of `golfin_tickets` and it is what
 * keeps the balance and the ledger in step — a direct `update` here would move
 * a balance with no transaction row behind it, and the whole reason the ledger
 * exists is to answer "where did these tickets come from". It also enforces the
 * floor: a negative adjustment that would take the balance below zero writes
 * nothing and comes back `insufficient`.
 *
 * ⚠️ AND IT IS NOT `golfin_pending_grants` ANY MORE. Before this task an admin
 * ticket grant queued a `kind = 'ticket'` grant row that the client applied into
 * its save blob — which made the DEVICE the authority on how many tickets a
 * player had. `app/api/users/[id]/inventory/route.ts` now routes the ticket kind
 * here instead. The grants queue keeps `'ticket'` in its CHECK for the rows that
 * already exist; nothing new writes it.
 */

export interface GachaOutcome {
  ok: boolean;
  status: number;
  message: string;
}

const ok = (message: string): GachaOutcome => ({ ok: true, status: 200, message });
const fail = (status: number, message: string): GachaOutcome => ({ ok: false, status, message });

/** An admin cannot move more than this in one go. Not a game rule — a typo rail. */
const MAX_TICKET_DELTA = 100000;

/**
 * §6 — the pause switch. `content_settings.gacha_enabled`.
 *
 * NARROWER THAN `content_enabled` ON PURPOSE, and the copy says so: pulling
 * remote content globally would also close the shop, the missions and the mode
 * fees. This closes the gacha and nothing else — every pull is refused with
 * `not_available / paused` and the banners stay visible, so a player sees the
 * screen they expect with a refusal instead of a hole in the UI.
 *
 * INSTANT, unlike the content kill switch: `golfin_gacha_pull` reads the row per
 * call, so there is no 60 s cache and no wait for the player's next launch.
 * That is exactly why pausing takes a typed confirmation in the panel.
 */
export async function setGachaEnabled(
  adminEmail: string,
  enabled: boolean
): Promise<GachaOutcome> {
  const before = await fetchGachaEnabled();

  if (isMockMode()) {
    MOCK_GACHA_ENABLED.value = enabled;
  } else {
    const res = await getSupabaseAdmin()
      .from("content_settings")
      .upsert(
        { key: GACHA_ENABLED_KEY, value: enabled, updated_at: new Date().toISOString() },
        { onConflict: "key" }
      );
    if (res.error) return fail(500, `content_settings update failed: ${res.error.message}`);
  }

  // Two distinct actions rather than one with a payload, so an audit reader can
  // tell "someone paused the gacha" from "someone resumed it" without opening
  // the row — the same reason `content.global_enabled` is not `content.enabled:*`.
  await writeAudit(
    adminEmail,
    enabled ? "gacha_resume" : "gacha_pause",
    null,
    "content_settings",
    { key: GACHA_ENABLED_KEY, value: before },
    { key: GACHA_ENABLED_KEY, value: enabled }
  );

  return ok(
    enabled
      ? "The gacha is LIVE. Pulls are accepted again, immediately."
      : "The gacha is PAUSED. Every pull is refused with not_available / paused, immediately. Banners stay visible."
  );
}

/**
 * §5.1 — grant or adjust a player's tickets, through the ledger.
 *
 * ONE FUNCTION FOR BOTH, with `adjust` choosing the reason and unlocking a
 * negative delta. They are the same write; splitting them into two exported
 * functions would duplicate the validation and let the two drift, and the
 * distinction that matters (was this a gift or a correction?) is exactly one
 * string on the ledger row.
 *
 * A grant is additive-only, matching `issueInventoryGrant`. An adjust may be
 * negative and is refused by the SQL function when it would go below zero —
 * refused, not clamped: an operator who typed -500 against a balance of 3 has
 * made a mistake, and silently taking 3 hides it.
 */
export async function creditTickets(
  adminEmail: string,
  userId: string,
  ticketType: number,
  delta: number,
  adjust: boolean
): Promise<GachaOutcome> {
  if (!Number.isInteger(ticketType) || ticketType < 0) {
    return fail(400, "ticketType must be a non-negative integer.");
  }
  if (!Number.isInteger(delta) || delta === 0) {
    return fail(400, "Amount must be a non-zero whole number.");
  }
  if (Math.abs(delta) > MAX_TICKET_DELTA) {
    return fail(400, `Amount must be between -${MAX_TICKET_DELTA} and ${MAX_TICKET_DELTA}.`);
  }
  if (!adjust && delta < 0) {
    return fail(400, "A grant cannot subtract. Use Adjust for a negative amount.");
  }

  const reason = adjust ? "admin_adjust" : "admin_grant";
  const action = adjust ? "ticket_adjust" : "ticket_grant";
  const key = randomUUID();

  if (isMockMode()) {
    const row = MOCK_TICKET_BALANCES.find((b) => b.ticketType === ticketType);
    const before = row?.balance ?? 0;
    if (before + delta < 0) {
      return fail(409, `That would take the balance below zero (${before} + ${delta}). Nothing changed.`);
    }
    const after = before + delta;
    if (row) {
      row.balance = after;
      row.updatedAt = new Date().toISOString();
    } else {
      MOCK_TICKET_BALANCES.push({
        ticketType, label: `Type ${ticketType}`, balance: after,
        updatedAt: new Date().toISOString(),
      });
    }
    MOCK_TICKET_TRANSACTIONS.unshift({
      id: randomUUID(), ticketType, delta, balanceAfter: after,
      reason, createdBy: adminEmail, createdAt: new Date().toISOString(),
    });
    await writeAudit(adminEmail, action, userId, "golfin_tickets",
      { ticket_type: ticketType, balance: before },
      { ticket_type: ticketType, balance: after, delta, reason });
    return ok(`${delta > 0 ? "+" : ""}${delta} ticket(s) of type ${ticketType}. Balance is now ${after}.`);
  }

  const res = await getSupabaseAdmin().rpc("golfin_ticket_credit", {
    p_user_id: userId,
    p_ticket_type: ticketType,
    p_delta: delta,
    p_reason: reason,
    p_key: key,
    p_created_by: adminEmail,
  });

  if (res.error) {
    // A missing FUNCTION is a deploy-order problem, not a fault, and it has a
    // one-line fix an operator can act on. ADMIN_DASHBOARD_OPS §3.2 says
    // migration first / deploy second precisely because this window exists;
    // naming the file turns a 500 into an instruction.
    const message = res.error.message.toLowerCase();
    if (
      message.includes("golfin_ticket_credit") &&
      (message.includes("does not exist") || message.includes("schema cache"))
    ) {
      return fail(
        503,
        "The ticket ledger does not exist on this project yet. Apply " +
          "playlife/backend/migrations/2026_09_01_golfin_gacha.sql in the Supabase SQL editor, " +
          "then try again. (Nothing was written, and the old grants-queue path is gone on purpose.)"
      );
    }
    return fail(500, `Could not write the ticket ledger: ${res.error.message}`);
  }

  const data = (res.data ?? {}) as { status?: string; balance?: number };

  if (data.status === "insufficient") {
    return fail(
      409,
      `That would take the balance below zero (it is ${data.balance ?? 0}). Nothing was written.`
    );
  }
  if (data.status === "unknown_ticket_type") {
    return fail(
      400,
      `Ticket type ${ticketType} is not a published, active ticket_types row. ` +
        "Publish it in the Ticket Types panel first — a balance in a type no screen can name is a support ticket."
    );
  }
  if (data.status !== "ok") {
    return fail(500, `golfin_ticket_credit returned an unexpected status: ${data.status}`);
  }

  const after = data.balance ?? 0;

  // The BEFORE is derived rather than read again: the function is the only
  // writer and it just told us the after, so `after - delta` is the balance it
  // saw under its own row lock. A second read here could race another credit
  // and record a `before` that was never true.
  await writeAudit(
    adminEmail,
    action,
    userId,
    "golfin_tickets",
    { ticket_type: ticketType, balance: after - delta },
    { ticket_type: ticketType, balance: after, delta, reason, idempotency_key: key }
  );

  return ok(
    `${delta > 0 ? "+" : ""}${delta} ticket(s) of type ${ticketType}. Balance is now ${after}.`
  );
}

/**
 * §6 — reset one banner's pity counter for one player.
 *
 * `counter = 0` ONLY. `total_pulls` is deliberately untouched: it is what
 * `maxPullsPerPlayer` is measured against, and zeroing it would hand the player
 * a fresh allowance of a capped banner, which is a different (and much larger)
 * decision than "give them their pity back". If that is ever wanted it should be
 * its own button with its own confirmation.
 *
 * A player with no row on that banner is a no-op, not an error: their counter is
 * already 0 and creating a zero row to "reset" it would be theatre.
 */
export async function resetPity(
  adminEmail: string,
  userId: string,
  bannerId: string
): Promise<GachaOutcome> {
  const banner = bannerId.trim();
  if (!banner) return fail(400, "bannerId is required.");

  if (isMockMode()) {
    const row = MOCK_PLAYER_PITY.find((p) => p.bannerId === banner);
    if (!row) return ok(`No pity counter on ${banner} — nothing to reset.`);
    const before = row.counter;
    row.counter = 0;
    row.updatedAt = new Date().toISOString();
    await writeAudit(adminEmail, "gacha_pity_reset", userId, "golfin_gacha_pity",
      { banner_id: banner, counter: before }, { banner_id: banner, counter: 0 });
    return ok(`Pity on ${banner} reset from ${before} to 0. Total pulls unchanged.`);
  }

  const admin = getSupabaseAdmin();

  // Read first so the audit's `before` is the real counter and the message can
  // name it. An operator resetting a counter wants to know what they took away.
  const existing = await admin
    .from("golfin_gacha_pity")
    .select("counter, total_pulls")
    .eq("user_id", userId)
    .eq("banner_id", banner)
    .maybeSingle();

  if (existing.error) return fail(500, `Could not read the pity row: ${existing.error.message}`);
  if (!existing.data) return ok(`No pity counter on ${banner} — nothing to reset.`);

  const before = (existing.data as { counter: number }).counter;
  if (before === 0) return ok(`Pity on ${banner} is already 0 — nothing changed.`);

  const res = await admin
    .from("golfin_gacha_pity")
    .update({ counter: 0, updated_at: new Date().toISOString() })
    .eq("user_id", userId)
    .eq("banner_id", banner);

  if (res.error) return fail(500, `Could not reset the pity counter: ${res.error.message}`);

  await writeAudit(
    adminEmail,
    "gacha_pity_reset",
    userId,
    "golfin_gacha_pity",
    { banner_id: banner, counter: before },
    { banner_id: banner, counter: 0 }
  );

  return ok(`Pity on ${banner} reset from ${before} to 0. Total pulls unchanged.`);
}
