import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { setLoanOffers } from "@/lib/loanMutations";

export const dynamic = "force-dynamic";

const UUID_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * POST /api/users/:id/loan-offers `{ enabled, note }` — the recipient's
 * switch, `profiles.golfin_loan_offers` (loans_ops §3.2, §3.4).
 *
 * INSTANT: routers/loans.py reads the column per lend, so OFF refuses the very
 * next offer with `not_accepting`. Audited as `loan_offers_set`.
 */
export async function POST(request: Request, ctx: { params: Promise<{ id: string }> }) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const { id } = await ctx.params;
  if (!UUID_RE.test(id)) {
    return NextResponse.json({ error: "Invalid user id." }, { status: 400 });
  }

  const body = (await request.json().catch(() => null)) as { enabled?: unknown; note?: unknown } | null;
  if (typeof body?.enabled !== "boolean") {
    return NextResponse.json({ error: "enabled (boolean) is required." }, { status: 400 });
  }
  if (typeof body.note !== "string") {
    return NextResponse.json({ error: "note (string) is required." }, { status: 400 });
  }

  try {
    const outcome = await setLoanOffers(check.email, id, body.enabled, body.note);
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`POST /api/users/${id}/loan-offers failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
