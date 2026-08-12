import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { adjustRp } from "@/lib/mutations";

export const dynamic = "force-dynamic";

const UUID_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * POST /api/users/:id/rp — grant (+) or deduct (−) Reward Points.
 * Positive → earn_pts_v2 rpc, negative → spend_pts rpc (insufficient → 409).
 * Requires a bounded reason. Admin-only, audited.
 */
export async function POST(
  request: Request,
  ctx: { params: Promise<{ id: string }> }
) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const { id } = await ctx.params;
  if (!UUID_RE.test(id)) {
    return NextResponse.json({ error: "Invalid user id." }, { status: 400 });
  }

  const body = (await request.json().catch(() => null)) as {
    amount?: unknown;
    reason?: unknown;
  } | null;
  if (typeof body?.amount !== "number" || typeof body?.reason !== "string") {
    return NextResponse.json(
      { error: "amount (number) and reason (string) are required." },
      { status: 400 }
    );
  }

  try {
    const outcome = await adjustRp(check.email, id, body.amount, body.reason);
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`POST /api/users/${id}/rp failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
