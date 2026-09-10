import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchLoanDetail } from "@/lib/loansData";

export const dynamic = "force-dynamic";

const UUID_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * GET /api/loans/:id — one loan and its timeline (loans_ops §3.2).
 *
 * The timeline is `golfin_loan_events`: every status change, whoever caused it
 * (router, lazy expiry, admin), with the admin's email and note on the rows an
 * admin wrote. `notMigrated` names the migration while the table is absent.
 *
 * The RP split's ledger rows are NOT attached: `golfin_loan_split` writes them
 * with the borrower's NAME in the description and a per-(round, loan) derived
 * idempotency key, neither of which carries the loan id back — so the row
 * shows the running `rp_to_lender` / `rp_to_borrower` totals instead, and says
 * so in the panel.
 */
export async function GET(_request: Request, ctx: { params: Promise<{ id: string }> }) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const { id } = await ctx.params;
  // Mock ids are not uuids ("00000000-mock-loan-0001"); the live table's are.
  if (!UUID_RE.test(id) && !id.startsWith("00000000-mock-")) {
    return NextResponse.json({ error: "Invalid loan id." }, { status: 400 });
  }

  try {
    const detail = await fetchLoanDetail(id);
    if (!detail) return NextResponse.json({ error: "No loan with that id." }, { status: 404 });
    return NextResponse.json(detail);
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`GET /api/loans/${id} failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
