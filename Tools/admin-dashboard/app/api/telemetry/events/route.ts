import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchTelemetryEvents, resolveRange } from "@/lib/telemetryData";

export const dynamic = "force-dynamic";

/**
 * GET /api/telemetry/events?from=&to=&name=&user=&page= — raw event explorer.
 * Admin-only. Paginates SERVER-SIDE (100/page) rather than shipping the window
 * like the aggregate routes do.
 */
export async function GET(request: Request) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const params = new URL(request.url).searchParams;
  const pageParam = Number.parseInt(params.get("page") ?? "0", 10);
  try {
    return NextResponse.json(
      await fetchTelemetryEvents({
        range: resolveRange(params.get("from"), params.get("to")),
        name: params.get("name"),
        userId: params.get("user"),
        page: Number.isFinite(pageParam) ? pageParam : 0,
      })
    );
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("GET /api/telemetry/events failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
