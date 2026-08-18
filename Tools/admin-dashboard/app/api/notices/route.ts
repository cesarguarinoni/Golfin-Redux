import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchNotices } from "@/lib/noticeData";
import { createNotice } from "@/lib/noticeMutations";
import type { NoticeInput } from "@/lib/types";

export const dynamic = "force-dynamic";

/** GET /api/notices — every row, live and draft. Admin-only. */
export async function GET() {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }
  try {
    return NextResponse.json(await fetchNotices());
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("GET /api/notices failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}

/** POST /api/notices — create a notice. Admin-only, audited. */
export async function POST(request: Request) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const body = (await request.json().catch(() => null)) as NoticeInput | null;
  if (!body || typeof body !== "object") {
    return NextResponse.json({ error: "Invalid body." }, { status: 400 });
  }

  try {
    const outcome = await createNotice(check.email, body);
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error("POST /api/notices failed:", message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
