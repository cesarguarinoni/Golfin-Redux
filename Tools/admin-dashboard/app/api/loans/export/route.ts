import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { loansToCsv } from "@/lib/loansCsv";
import { fetchLoans } from "@/lib/loansData";

export const dynamic = "force-dynamic";

/**
 * GET /api/loans/export — the FILTERED loan log as CSV (loans_ops §3.3).
 *
 * Same query parameters as `/api/loans`, deliberately, for the reason
 * `gacha/export` gives: the export must be WHAT IS ON SCREEN, and the only way
 * to guarantee that is one query behind both. One difference: it is not
 * capped at the page — `EXPORT_LIMIT` rows from page 0, an evidence file
 * rather than a reading unit.
 */
const EXPORT_LIMIT = 500;

export async function GET(request: Request) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const params = new URL(request.url).searchParams;
  try {
    const data = await fetchLoans({
      status: params.get("status") ?? undefined,
      kind: params.get("kind") ?? undefined,
      q: params.get("q") ?? undefined,
      from: params.get("from") ?? undefined,
      to: params.get("to") ?? undefined,
      page: 0,
      limit: EXPORT_LIMIT,
    });
    return new NextResponse(loansToCsv(data.loans), {
      headers: {
        "Content-Type": "text/csv; charset=utf-8",
        "Content-Disposition": 'attachment; filename="golfin_loans.csv"',
      },
    });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("GET /api/loans/export failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
