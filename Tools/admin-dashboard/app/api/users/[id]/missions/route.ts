import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchPlayerMissions } from "@/lib/dailyMissionData";
import { resetMissionProgress } from "@/lib/missionMutations";

export const dynamic = "force-dynamic";

const UUID_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** GET /api/users/:id/missions — one player's campaign progress + daily claims. */
export async function GET(_request: Request, ctx: { params: Promise<{ id: string }> }) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }
  const { id } = await ctx.params;
  if (!UUID_RE.test(id)) {
    return NextResponse.json({ error: "Invalid user id." }, { status: 400 });
  }
  try {
    return NextResponse.json(await fetchPlayerMissions(id));
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`GET /api/users/${id}/missions failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}

/**
 * DELETE /api/users/:id/missions `{ missionId }` — reset one mission.
 *
 * Erases clears, attempts and the best score, so the player's next clear pays
 * the FIRST-CLEAR amount again. It does not claw back points already credited
 * and it does not touch the idempotency ledger. Admin-only, audited.
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

  const body = (await request.json().catch(() => null)) as { missionId?: unknown } | null;
  if (typeof body?.missionId !== "string" || !body.missionId.trim()) {
    return NextResponse.json({ error: "missionId (string) is required." }, { status: 400 });
  }

  try {
    const outcome = await resetMissionProgress(check.email, id, body.missionId);
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`DELETE /api/users/${id}/missions failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
