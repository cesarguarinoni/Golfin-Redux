import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { resetPity } from "@/lib/gachaMutations";

export const dynamic = "force-dynamic";

const UUID_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * DELETE /api/gacha/users/:id/pity `{ bannerId }` — reset one pity counter (§6).
 *
 * `counter = 0` ONLY. `total_pulls` is untouched, because it is what
 * `maxPullsPerPlayer` is measured against: zeroing it would hand the player a
 * fresh allowance of a capped banner, which is a much larger decision than
 * "give them their pity back" and deserves its own button if it is ever wanted.
 *
 * A DELETE rather than a POST because the operator's intent is "remove this
 * counter", and it is the same shape as the missions tab's reset — an operator
 * who has used one has used both.
 */
export async function DELETE(request: Request, ctx: { params: Promise<{ id: string }> }) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const { id } = await ctx.params;
  if (!UUID_RE.test(id)) {
    return NextResponse.json({ error: "Invalid user id." }, { status: 400 });
  }

  const body = (await request.json().catch(() => null)) as { bannerId?: unknown } | null;
  if (typeof body?.bannerId !== "string" || !body.bannerId.trim()) {
    return NextResponse.json({ error: "bannerId (string) is required." }, { status: 400 });
  }

  try {
    const outcome = await resetPity(check.email, id, body.bannerId);
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`DELETE /api/gacha/users/${id}/pity failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
