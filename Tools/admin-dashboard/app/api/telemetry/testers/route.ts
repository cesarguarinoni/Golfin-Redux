import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchTelemetryTesters, resolveRange } from "@/lib/telemetryData";

export const dynamic = "force-dynamic";

/** GET /api/telemetry/testers?from=&to= — per-tester rollup. Admin-only. */
export async function GET(request: Request) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const params = new URL(request.url).searchParams;
  try {
    const range = resolveRange(params.get("from"), params.get("to"));
    return NextResponse.json(await fetchTelemetryTesters(range));
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("GET /api/telemetry/testers failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
