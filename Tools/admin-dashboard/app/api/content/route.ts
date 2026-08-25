import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchCatalogs } from "@/lib/contentData";

export const dynamic = "force-dynamic";

/**
 * GET /api/content — every catalog with its published version, kill-switch
 * state and how many draft rows a publish would actually change.
 *
 * Admin-only, like every other data route. SPEC content_catalog §D.
 */
export async function GET() {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }
  try {
    return NextResponse.json(await fetchCatalogs());
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("GET /api/content failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
