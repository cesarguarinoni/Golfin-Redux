import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchUserLoans } from "@/lib/loansData";

export const dynamic = "force-dynamic";

const UUID_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * GET /api/users/:id/loans — the drawer's Loans tab (loans_ops §3.4): loans
 * OUT (this user lends), IN (this user borrowed and the offer was accepted),
 * pending OFFERS awaiting their answer, and the `golfin_loan_offers` switch.
 */
export async function GET(_request: Request, ctx: { params: Promise<{ id: string }> }) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const { id } = await ctx.params;
  if (!UUID_RE.test(id)) {
    return NextResponse.json({ error: "Invalid user id." }, { status: 400 });
  }

  try {
    return NextResponse.json(await fetchUserLoans(id));
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`GET /api/users/${id}/loans failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
