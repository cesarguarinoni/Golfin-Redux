import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchLoans } from "@/lib/loansData";
import { LOAN_STATUSES } from "@/lib/types";

export const dynamic = "force-dynamic";

/**
 * GET /api/loans — the loan log (loans_ops §3.2).
 *
 * Filters: `status` (one of the seven), `kind` (character | club), `q`
 * (display name partial, a loan / user uuid, or a ref_id partial — resolved
 * server-side because `golfin_loans` carries ids, not names), `from` / `to`
 * (ISO), `page` (0-based, 50 per page).
 *
 * Read-only, so no `writeAudit` — the panel's writes each have their own route.
 */
export async function GET(request: Request) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const params = new URL(request.url).searchParams;
  const status = params.get("status") ?? undefined;
  if (status && !(LOAN_STATUSES as readonly string[]).includes(status)) {
    return NextResponse.json({ error: `status must be one of ${LOAN_STATUSES.join(", ")}.` }, { status: 400 });
  }
  const kind = params.get("kind") ?? undefined;
  if (kind && kind !== "character" && kind !== "club") {
    return NextResponse.json({ error: "kind must be character or club." }, { status: 400 });
  }
  const pageRaw = params.get("page");
  const page = pageRaw ? Number(pageRaw) : 0;
  if (!Number.isInteger(page) || page < 0) {
    return NextResponse.json({ error: "page must be a non-negative integer." }, { status: 400 });
  }

  try {
    return NextResponse.json(
      await fetchLoans({
        status,
        kind,
        q: params.get("q") ?? undefined,
        from: params.get("from") ?? undefined,
        to: params.get("to") ?? undefined,
        page,
      })
    );
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("GET /api/loans failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
