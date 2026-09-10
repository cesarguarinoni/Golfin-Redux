import "server-only";
import { writeAudit } from "./audit";
import { fetchLoanRaw } from "./loansData";
import { isMockMode } from "./mode";
import { mockLoansDb } from "./mockLoans";
import { getSupabaseAdmin } from "./supabaseAdmin";
import { LOAN_ADMIN_ACTIONS, type LoanAdminAction } from "./types";

/**
 * Loan ops mutations (loans_ops §3.2, §3.6).
 *
 * TWO WRITES, and every one of them is audited: the three per-loan support
 * actions, and the per-player offers switch.
 *
 * ⚠️ THE LOAN WRITE GOES THROUGH `golfin_loan_admin`, NEVER THROUGH AN UPDATE.
 * That function is what keeps the row, the timeline (`golfin_loan_events`) and
 * the note in ONE transaction, and it is what stamps the timeline row `admin`
 * instead of letting the trigger attribute a forced return to the borrower. A
 * direct `update` here would move a status with no timeline row behind it,
 * which is exactly the "who did this to my loan" question the timeline exists
 * to answer. Every refusal it returns (`note_required`, `not_found`,
 * `not_active`, `not_offered`, `bad_action`) is mapped to a 409 carrying the
 * status string, so the panel's toast names the reason.
 *
 * THE OFFERS SWITCH IS A PLAIN `profiles` UPDATE, like every other profiles
 * mutation in `lib/mutations.ts`: the API reads `profiles.golfin_loan_offers`
 * per lend and there is no function in between.
 */

export interface LoanOutcome {
  ok: boolean;
  status: number;
  message: string;
}

const ok = (message: string): LoanOutcome => ({ ok: true, status: 200, message });
const fail = (status: number, message: string): LoanOutcome => ({ ok: false, status, message });

/** Every action needs a note ≥ 3 chars (SPEC § Decisions of record). */
export const MIN_NOTE_LENGTH = 3;
export const MAX_NOTE_LENGTH = 500;

/** What each refusal of `golfin_loan_admin` means, for the 409 body. */
const REFUSALS: Record<string, string> = {
  note_required: "A note of at least 3 characters is required.",
  admin_required: "The acting admin's email is missing.",
  not_found: "No loan with that id.",
  not_active: "not_active — only an ACTIVE loan can be force-returned.",
  not_offered: "not_offered — only an OFFERED loan can be cancelled.",
  bad_action: "bad_action — unknown admin action.",
};

type Row = Record<string, unknown>;

/** The columns the audit snapshots, before and after. The whole row would
 *  also be defensible, but these are the ones an action can move. */
function snapshot(row: Row | null): Row | null {
  if (!row) return null;
  return {
    id: row.id ?? null,
    status: row.status ?? null,
    answered_at: row.answered_at ?? row.answeredAt ?? null,
    ended_at: row.ended_at ?? row.endedAt ?? null,
    level_at_end: row.level_at_end ?? row.levelAtEnd ?? null,
    lender_id: row.lender_id ?? row.lenderId ?? null,
    borrower_id: row.borrower_id ?? row.borrowerId ?? null,
  };
}

function validNote(note: unknown): string | null {
  if (typeof note !== "string") return null;
  const trimmed = note.trim();
  if (trimmed.length < MIN_NOTE_LENGTH || trimmed.length > MAX_NOTE_LENGTH) return null;
  return trimmed;
}

/**
 * §3.2 — `POST /api/loans/:id/actions`.
 *
 * The mock branch mirrors the SQL function's behaviour step for step —
 * including its refusals and the timeline row — so the panel can be exercised
 * end to end without a database, per ADMIN_DASHBOARD_OPS §4.5.
 */
export async function runLoanAdminAction(
  adminEmail: string,
  loanId: string,
  action: string,
  note: unknown
): Promise<LoanOutcome> {
  if (!(LOAN_ADMIN_ACTIONS as readonly string[]).includes(action)) {
    return fail(400, `action must be one of ${LOAN_ADMIN_ACTIONS.join(", ")}.`);
  }
  const cleanNote = validNote(note);
  if (!cleanNote) {
    return fail(400, `note (string, ${MIN_NOTE_LENGTH}–${MAX_NOTE_LENGTH} chars) is required.`);
  }
  const kind = action as LoanAdminAction;

  const before = await fetchLoanRaw(loanId);
  if (!before) return fail(404, REFUSALS.not_found ?? "No loan with that id.");

  if (isMockMode()) {
    const loan = mockLoansDb().loans.find((l) => l.id === loanId);
    if (!loan) return fail(404, REFUSALS.not_found ?? "No loan with that id.");
    const from = loan.status;
    const now = new Date().toISOString();
    if (kind === "force_return") {
      if (loan.status !== "active") return fail(409, REFUSALS.not_active ?? "not_active");
      loan.status = "returned";
      loan.endedAt = now;
      loan.levelAtEnd = loan.levelAtEnd ?? loan.levelAtStart;
    } else if (kind === "cancel_offer") {
      if (loan.status !== "offered") return fail(409, REFUSALS.not_offered ?? "not_offered");
      loan.status = "rescinded";
      loan.answeredAt = now;
    } else {
      const thirtyDays = 30 * 24 * 3_600_000;
      for (const l of mockLoansDb().loans) {
        if (
          l.lenderId === loan.lenderId &&
          l.borrowerId === loan.borrowerId &&
          (l.status === "rescinded" || l.status === "declined") &&
          l.answeredAt
        ) {
          l.answeredAt = new Date(Date.parse(l.answeredAt) - thirtyDays).toISOString();
        }
      }
    }
    const events = (mockLoansDb().events[loanId] ??= []);
    events.push({
      id: 1000 + events.length,
      at: now,
      fromStatus: from,
      toStatus: loan.status,
      actor: "admin",
      adminEmail,
      note: cleanNote,
    });
    await writeAudit(
      adminEmail,
      `loan_${kind}`,
      loan.lenderId,
      "golfin_loans",
      snapshot(before),
      { ...snapshot({ ...loan } as unknown as Row), note: cleanNote }
    );
    return ok(messageFor(kind));
  }

  const rpc = await getSupabaseAdmin().rpc("golfin_loan_admin", {
    p_loan_id: loanId,
    p_action: kind,
    p_admin: adminEmail,
    p_note: cleanNote,
  });
  if (rpc.error) {
    // The function does not exist until 2026_09_10_golfin_loan_events.sql is
    // applied; say which file rather than echoing PostgREST's "not found".
    const m = rpc.error.message.toLowerCase();
    if (m.includes("could not find the function") || m.includes("does not exist")) {
      return fail(
        503,
        "golfin_loan_admin is not on this project yet — apply 2026_09_10_golfin_loan_events.sql."
      );
    }
    return fail(500, `golfin_loan_admin failed: ${rpc.error.message}`);
  }

  const result = (rpc.data ?? {}) as { status?: unknown };
  const status = typeof result.status === "string" ? result.status : "unknown";
  if (status !== "ok") {
    return fail(status === "not_found" ? 404 : 409, REFUSALS[status] ?? `Refused: ${status}`);
  }

  const after = await fetchLoanRaw(loanId);
  await writeAudit(
    adminEmail,
    `loan_${kind}`,
    String(before.lender_id ?? ""),
    "golfin_loans",
    snapshot(before),
    { ...snapshot(after), note: cleanNote }
  );
  return ok(messageFor(kind));
}

function messageFor(kind: LoanAdminAction): string {
  switch (kind) {
    case "force_return":
      return "Loan force-returned. The asset is back with its owner; the borrower's client reconciles on its next refresh.";
    case "cancel_offer":
      return "Offer cancelled (rescinded). The asset is unlocked on the lender's side. NOTE: this starts the pair's 24 h re-offer cooldown — clear it if the case calls for it.";
    case "clear_cooldown":
      return "Cooldown cleared for this lender → borrower pair. The lender may offer again now.";
  }
}

/**
 * §3.2 — `POST /api/users/:id/loan-offers` — the recipient's switch,
 * `profiles.golfin_loan_offers`. Audited as `loan_offers_set` with the
 * before/after value and the operator's note.
 */
export async function setLoanOffers(
  adminEmail: string,
  userId: string,
  enabled: boolean,
  note: unknown
): Promise<LoanOutcome> {
  const cleanNote = validNote(note);
  if (!cleanNote) {
    return fail(400, `note (string, ${MIN_NOTE_LENGTH}–${MAX_NOTE_LENGTH} chars) is required.`);
  }

  let before: boolean;
  if (isMockMode()) {
    before = mockLoansDb().offersEnabled[userId] ?? true;
    mockLoansDb().offersEnabled[userId] = enabled;
  } else {
    const admin = getSupabaseAdmin();
    const cur = await admin.from("profiles").select("golfin_loan_offers").eq("id", userId).maybeSingle();
    if (cur.error) return fail(500, `profiles read failed: ${cur.error.message}`);
    if (!cur.data) return fail(404, "User not found.");
    before = (cur.data as { golfin_loan_offers?: unknown }).golfin_loan_offers !== false;

    const upd = await admin.from("profiles").update({ golfin_loan_offers: enabled }).eq("id", userId);
    if (upd.error) return fail(500, `profiles update failed: ${upd.error.message}`);
  }

  await writeAudit(
    adminEmail,
    "loan_offers_set",
    userId,
    "profiles",
    { enabled: before },
    { enabled, note: cleanNote }
  );
  return ok(
    enabled
      ? "Loan offers ON — this player can be offered loans again."
      : "Loan offers OFF — every offer to this player is refused with not_accepting, immediately."
  );
}
