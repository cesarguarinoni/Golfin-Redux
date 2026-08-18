import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchTelemetrySummary, resolveRange } from "@/lib/telemetryData";

export const dynamic = "force-dynamic";

/**
 * GET /api/telemetry/summary?from=&to= — KPIs, funnel, per-hole, shot quality.
 * Admin-only, read-only: this panel has no mutation and writes no audit row.
 */
export async function GET(request: Request) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const params = new URL(request.url).searchParams;
  try {
    const range = resolveRange(params.get("from"), params.get("to"));
    return NextResponse.json(await fetchTelemetrySummary(range));
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("GET /api/telemetry/summary failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
