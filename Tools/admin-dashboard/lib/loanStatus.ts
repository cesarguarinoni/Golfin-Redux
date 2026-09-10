import type { DictKey } from "./i18n";

/**
 * EVERY judgement this dashboard makes about a loan's status or its clocks,
 * in one pure module (loans_ops).
 *
 * ⚠️ THIS FILE EXISTS BECAUSE THE SAME BUG HAPPENED FOUR TIMES.
 *
 *   1. `medianHoursToAnswer` guarded by exclusion and so counted `rescinded`,
 *      whose `answered_at` is the LENDER withdrawing. A card labelled "median
 *      time to answer" averaged in a change of mind.
 *   2. A lapsed-but-unswept `offered` row matched none of the drawer's three
 *      borrower lists and rendered in no section at all.
 *   3. Both shipped under comments asserting the opposite of what the code did.
 *   4. `clockLine`'s `switch` had a `default` arm reading "answered {rel}",
 *      which labelled a lender's rescind and a 48 h lapse as the recipient
 *      answering — on the panel whose whole job is explaining what happened.
 *
 * One shape every time: **a status classified by an incomplete list, and a row
 * falling through or landing in the wrong bucket.** Scattering that judgement
 * across a fold, a data loader and a React component gave it three places to
 * hide. It now has one, and `ALL_LOAN_STATUSES` plus the exhaustive tests over
 * it mean a status added later fails loudly rather than landing in a `default`.
 *
 * THE RULE THIS MODULE ENCODES: read the CLOCKS, not the status column. Expiry
 * is lazy — nothing sweeps `golfin_loans`, and `GET /loans` flips a past-clock
 * row only when some client happens to read it — so the column routinely says
 * `offered` for an offer that lapsed yesterday. Every predicate below therefore
 * takes `nowMs` and applies the router's own timestamp tests.
 */

/** One `golfin_loans` row, snake_case: the table's own columns, untouched. */
export interface LoanRow {
  id?: unknown;
  lender_id?: unknown;
  borrower_id?: unknown;
  kind?: unknown;
  days?: unknown;
  status?: unknown;
  offered_at?: unknown;
  offer_expires_at?: unknown;
  answered_at?: unknown;
  starts_at?: unknown;
  ends_at?: unknown;
  ended_at?: unknown;
  created_at?: unknown;
  rp_to_lender?: unknown;
  rp_to_borrower?: unknown;
}

/**
 * The seven, in lifecycle order. The tests iterate THIS, so adding a status
 * without teaching the functions below about it fails the suite.
 */
export const ALL_LOAN_STATUSES = [
  "offered",
  "active",
  "returned",
  "expired",
  "declined",
  "rescinded",
  "offer_expired",
] as const;

export type LoanStatusName = (typeof ALL_LOAN_STATUSES)[number];

/**
 * The statuses whose `answered_at` is the RECIPIENT's answer.
 *
 * `rescinded` is deliberately absent — its `answered_at` is the lender's.
 * `offered` has none yet, and `offer_expired`'s is a TTL running out, which is
 * nobody answering. `routers/loans.py::_terminal_answer` stamps the same column
 * for a decline AND a rescind, which is what made this easy to get wrong.
 */
export const ANSWERED_STATUSES: ReadonlySet<string> = new Set<string>([
  "active",
  "returned",
  "expired",
  "declined",
]);

function str(v: unknown): string | null {
  return typeof v === "string" && v.length > 0 ? v : null;
}

function ms(v: unknown): number | null {
  const s = str(v);
  if (!s) return null;
  const t = Date.parse(s);
  return Number.isFinite(t) ? t : null;
}

/** Router predicate `_is_live`: `status = 'active' and now() < ends_at`. */
export function isLoanLive(row: LoanRow, nowMs: number): boolean {
  if (String(row.status ?? "") !== "active") return false;
  const ends = ms(row.ends_at);
  return ends !== null && nowMs < ends;
}

/** Router predicate `_is_pending_offer`: `offered` and the offer clock has not
 *  run out. A missing `offer_expires_at` (a pre-offers row somehow still
 *  offered) counts as pending — the router locks it, so should this. */
export function isLoanPending(row: LoanRow, nowMs: number): boolean {
  if (String(row.status ?? "") !== "offered") return false;
  const expires = ms(row.offer_expires_at);
  return expires === null || nowMs < expires;
}

/**
 * The moment a loan belongs to, for ranging. `offered_at` is the natural
 * anchor; rows predating the offers migration have none and fall back to
 * `starts_at`, then `created_at`. Null when the row carries none of the three.
 */
export function loanAnchorMs(row: LoanRow): number | null {
  return ms(row.offered_at) ?? ms(row.starts_at) ?? ms(row.created_at);
}

/**
 * Which section of the Users drawer's Loans tab a row belongs in, from the
 * BORROWER's side. `null` means this player is not the borrower on it.
 *
 * TOTAL BY CONSTRUCTION — defect 2 above. A lapsed offer is filed as
 * went-nowhere on the strength of its CLOCK, which is what `_expire` will stamp
 * it as anyway.
 */
export function borrowerSection(
  row: LoanRow,
  borrowerId: string,
  nowMs: number
): "offers" | "in" | "wentNowhere" | null {
  if (str(row.borrower_id) !== borrowerId) return null;
  if (isLoanPending(row, nowMs)) return "offers";
  const status = String(row.status ?? "");
  if (status === "active" || status === "returned" || status === "expired") return "in";
  return "wentNowhere";
}

/** What an admin may do to a loan in this state (SPEC § 3.3). */
export type LoanAdminActionName = "force_return" | "cancel_offer" | "clear_cooldown";

/**
 * The action bar for a row.
 *
 * Takes the whole row and `nowMs` rather than a bare status, because the
 * SERVER's guards are on the status column while the operator is looking at a
 * clock: `golfin_loan_admin` refuses `cancel_offer` on anything but `offered`,
 * so a lapsed-but-unswept offer still accepts it — and doing so starts a
 * 24 h cooldown on a pair whose offer had already died of its own accord.
 * Offering the button on a row whose clock has run out invites that, so it is
 * withheld. The same for `force_return` on an `active` row past `ends_at`.
 */
export function actionsFor(
  row: { status: string; offerExpiresAt: string | null; endsAt: string | null },
  nowMs: number
): LoanAdminActionName[] {
  const asRow: LoanRow = {
    status: row.status,
    offer_expires_at: row.offerExpiresAt,
    ends_at: row.endsAt,
  };
  if (row.status === "active") return isLoanLive(asRow, nowMs) ? ["force_return"] : [];
  if (row.status === "offered") return isLoanPending(asRow, nowMs) ? ["cancel_offer"] : [];
  if (row.status === "rescinded" || row.status === "declined") return ["clear_cooldown"];
  return [];
}

/** The camelCase shape the panel and the drawer hand in. `LoanAdminRow`
 *  satisfies it structurally. */
export interface LoanClockRow {
  status: string;
  offerExpiresAt: string | null;
  endsAt: string | null;
  endedAt: string | null;
  answeredAt: string | null;
}

/**
 * The one clock line a row shows: WHICH string, and WHICH timestamp to render
 * relative to. Returns null when the row carries no relevant stamp.
 *
 * ⚠️ EVERY STATUS IS NAMED — defect 4 above. The previous version had a
 * `default` arm reading "answered {rel}", which swept up `rescinded` (the
 * lender pulling the offer back) and `offer_expired` (a TTL lapsing unseen) and
 * told the operator the recipient had answered. On the panel that exists to
 * explain why an offer disappeared, that is the worst possible lie.
 *
 * The fallback that remains is for a status this build has never heard of, and
 * it says only that something changed — never who did it.
 */
export function clockLabel(
  row: LoanClockRow,
  nowMs: number
): { key: DictKey; iso: string } | null {
  const at = (iso: string | null, key: DictKey) => (iso ? { key, iso } : null);

  switch (row.status) {
    case "offered": {
      if (!row.offerExpiresAt) return null;
      const past = Date.parse(row.offerExpiresAt) < nowMs;
      return { key: past ? "loans.offerExpiresPast" : "loans.offerExpires", iso: row.offerExpiresAt };
    }
    case "active": {
      if (!row.endsAt) return null;
      const past = Date.parse(row.endsAt) < nowMs;
      return { key: past ? "loans.endsPast" : "loans.ends", iso: row.endsAt };
    }
    case "returned":
    case "expired":
      return at(row.endedAt, "loans.ended");
    // The ONE status where `answered_at` really is the recipient answering.
    case "declined":
      return at(row.answeredAt, "loans.answered");
    // The lender's own action, not an answer.
    case "rescinded":
      return at(row.answeredAt, "loans.rescindedAt");
    // Nobody answered; the 48 h clock simply ran out.
    case "offer_expired":
      return at(row.answeredAt, "loans.lapsedAt");
    default:
      // An unknown status. Say only that it changed.
      return at(row.answeredAt ?? row.endedAt, "loans.changedAt");
  }
}
