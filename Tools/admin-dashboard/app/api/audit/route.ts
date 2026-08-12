import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchAuditLog } from "@/lib/data";

export const dynamic = "force-dynamic";

/** GET /api/audit — admin_audit_log viewer. Admin-only, read-only. */
export async function GET() {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  try {
    const data = await fetchAuditLog();
    return NextResponse.json(data);
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("GET /api/audit failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
