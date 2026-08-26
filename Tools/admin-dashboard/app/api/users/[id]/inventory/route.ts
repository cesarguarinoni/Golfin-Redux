import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchPlayerInventory } from "@/lib/inventoryData";
import { issueInventoryGrant } from "@/lib/inventoryMutations";

export const dynamic = "force-dynamic";

const UUID_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * GET /api/users/:id/inventory — the player's game inventory blob + grant queue.
 * Admin-only, read-only.
 *
 * ⚠️ `profiles.golfin_inventory`, NOT `user_inventory` (the PARTNER APP's gift
 * inventory). See lib/inventoryData.ts.
 */
export async function GET(
  _request: Request,
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

  try {
    return NextResponse.json(await fetchPlayerInventory(id));
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`GET /api/users/${id}/inventory failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}

/**
 * POST /api/users/:id/inventory — queue an additive grant.
 *
 * Writes `golfin_pending_grants`, NOT the blob: the client is the writer of
 * record for the blob and an admin write would race its 30 s write-behind. The
 * player picks the grant up on their next launch and acks it.
 *
 * Additive-only — `amount` must be ≥ 1, enforced here, by the CHECK constraint,
 * and again on the client. Admin-only, audited.
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
    kind?: unknown;
    refId?: unknown;
    amount?: unknown;
    note?: unknown;
  } | null;

  if (
    typeof body?.kind !== "string" ||
    typeof body?.refId !== "string" ||
    typeof body?.amount !== "number"
  ) {
    return NextResponse.json(
      { error: "kind (string), refId (string) and amount (number) are required." },
      { status: 400 }
    );
  }

  try {
    const outcome = await issueInventoryGrant(
      check.email,
      id,
      body.kind,
      body.refId,
      body.amount,
      typeof body.note === "string" ? body.note : ""
    );
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`POST /api/users/${id}/inventory failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
