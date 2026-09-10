import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchLoanRules } from "@/lib/loansData";

export const dynamic = "force-dynamic";

/**
 * GET /api/loans/rules — the loan constants, read from the API
 * (`GET /api/v1/loans/rules` on playlife) so the panel's Rules card can never
 * drift from routers/loans.py. A 200 with `unavailable` when the API cannot be
 * reached: the card explains, the rest of the panel keeps working.
 */
export async function GET() {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }
  try {
    return NextResponse.json(await fetchLoanRules());
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("GET /api/loans/rules failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
