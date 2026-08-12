import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchLedger } from "@/lib/data";

export const dynamic = "force-dynamic";

/** GET /api/points — global points_transactions ledger. Admin-only, read-only. */
export async function GET() {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  try {
    const data = await fetchLedger();
    return NextResponse.json(data);
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("GET /api/points failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
