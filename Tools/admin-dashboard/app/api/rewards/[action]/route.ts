import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { updateRewardAction } from "@/lib/rewardsMutations";
import type { RewardActionInput } from "@/lib/types";

export const dynamic = "force-dynamic";

/**
 * PATCH /api/rewards/:action — edit ONE earn action's numbers. Admin-only, audited.
 *
 * PATCH, and no POST or DELETE sibling, because those are the operations this
 * table does not get (game_modes_admin §3): a shipped client refers to actions by
 * name, so creating one is pointless and deleting one silently drops every earn
 * that used it. `updateRewardAction` enforces the same rule server-side rather
 * than trusting the absence of a handler.
 *
 * ⚠️ THERE IS NO PUBLISH STEP AFTER THIS. The earn path reads the row per
 * request, so a 200 here means the next earn already pays the new amount.
 */
export async function PATCH(
  request: Request,
  ctx: { params: Promise<{ action: string }> }
) {
  const check = await checkAdmin();
  if (!check.ok) {
    return NextResponse.json({ error: check.message }, { status: check.status });
  }

  const { action } = await ctx.params;

  const body = (await request.json().catch(() => null)) as Partial<RewardActionInput> | null;
  if (!body || typeof body !== "object") {
    return NextResponse.json({ error: "Invalid body." }, { status: 400 });
  }

  // `null` is a VALUE here, not "unset" — it is what "the client supplies the
  // amount" and "no cap" are written as. So each field is read explicitly and a
  // missing key is normalised to null rather than left undefined, which would
  // otherwise reach the update as a no-op and look like a silently ignored edit.
  const field = (v: unknown): number | null | "bad" => {
    if (v === null || v === undefined || v === "") return null;
    if (typeof v !== "number" || !Number.isFinite(v)) return "bad";
    return v;
  };

  const pts = field(body.pts);
  const maxPerEvent = field(body.maxPerEvent);
  const dailyCap = field(body.dailyCap);
  if (pts === "bad" || maxPerEvent === "bad" || dailyCap === "bad") {
    return NextResponse.json(
      { error: "pts, maxPerEvent and dailyCap must each be a number or null." },
      { status: 400 }
    );
  }

  try {
    const outcome = await updateRewardAction(check.email, action, { pts, maxPerEvent, dailyCap });
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`PATCH /api/rewards/${action} failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
