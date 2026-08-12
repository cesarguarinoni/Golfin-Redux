import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchUsers } from "@/lib/data";

export const dynamic = "force-dynamic";

/** GET /api/users — full user list + economy catalog. Admin-only, read-only. */
export async function GET() {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  try {
    const data = await fetchUsers();
    return NextResponse.json(data);
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("GET /api/users failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
