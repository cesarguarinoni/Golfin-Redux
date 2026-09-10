import type { LoanAdminRow } from "./types";

/**
 * The Loans panel's CSV — PURE, so it is testable (loans_ops §3.3).
 *
 * One line per loan, every column the table shows plus the raw ids the table
 * hides behind names. Same posture as `pullsToCsv`: the export is an evidence
 * file, so the ids are in it even though the screen resolves them.
 */

export const LOAN_CSV_COLUMNS = [
  "id",
  "offered_at",
  "status",
  "kind",
  "ref_id",
  "lender_id",
  "lender",
  "borrower_id",
  "borrower",
  "days",
  "level_at_start",
  "level_at_end",
  "rp_to_lender",
  "rp_to_borrower",
  "starts_at",
  "ends_at",
  "ended_at",
  "offer_expires_at",
  "answered_at",
  "lender_share_bp",
] as const;

function cell(v: unknown): string {
  if (v === null || v === undefined) return "";
  const s = String(v);
  return /[",\n\r]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
}

export function loansToCsv(rows: LoanAdminRow[]): string {
  const lines: string[] = [LOAN_CSV_COLUMNS.map(cell).join(",")];
  for (const r of rows) {
    lines.push(
      [
        r.id,
        r.offeredAt,
        r.status,
        r.kind,
        r.refId,
        r.lenderId,
        r.lenderName,
        r.borrowerId,
        r.borrowerName,
        r.days,
        r.levelAtStart,
        r.levelAtEnd,
        r.rpToLender,
        r.rpToBorrower,
        r.startsAt,
        r.endsAt,
        r.endedAt,
        r.offerExpiresAt,
        r.answeredAt,
        r.lenderShareBp,
      ]
        .map(cell)
        .join(",")
    );
  }
  return lines.join("\n");
}
