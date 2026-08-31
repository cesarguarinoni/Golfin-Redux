import { NextResponse } from "next/server";
import { checkAdmin } from "@/lib/auth";
import { fetchPlayerInventory } from "@/lib/inventoryData";
import { creditTickets } from "@/lib/gachaMutations";
import { issueInventoryGrant, revokeInventoryGrant } from "@/lib/inventoryMutations";

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

  // A ticket addresses its target by NUMBER (the `ticket_types` row id, which is
  // the client's TicketType int). Checked before the Number() cast below so a
  // typo is a message naming the field rather than a NaN reaching the ledger.
  if (body.kind === "ticket" && !/^\d+$/.test(body.refId.trim())) {
    return NextResponse.json(
      { error: "A ticket grant addresses its type by number, so refId must be an integer." },
      { status: 400 }
    );
  }

  try {
    // ⚠️ A TICKET IS NOT A GRANT ANY MORE (gacha_server_pull §5.1). It is routed
    // to `golfin_ticket_credit()` — the only writer of the `golfin_tickets`
    // ledger — instead of being queued for the client to apply into its save
    // blob. The old path made the DEVICE the authority on a currency the server
    // now sells and spends.
    //
    // Routed HERE rather than by asking the caller to use a different endpoint,
    // because the grant modal is where an operator already goes and a modal that
    // says "wrong door" is a modal people work around. The response message says
    // where the tickets actually went. `note` is dropped on this path: the ledger
    // row carries `reason` and `created_by`, and a free-text note with nowhere to
    // live would silently vanish.
    const outcome =
      body.kind === "ticket"
        ? await creditTickets(check.email, id, Number(body.refId), body.amount, false)
        : await issueInventoryGrant(
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

/**
 * DELETE /api/users/:id/inventory `{ grantId }` — revoke a grant that has NOT drained yet
 * (CONTENT_PIPELINE_PLAN §6.5 decision 3).
 *
 * This is the ONLY subtraction anywhere in the inventory feature, and it is deliberately confined
 * to the queue: it removes a PENDING row, never anything a player already holds. Once a grant is
 * applied it is in the blob, and the blob has no subtraction at all — the merge only ever takes
 * the max. An applied grant therefore comes back 409, not a silent no-op, because the operator is
 * usually mid-mistake and needs to know they are now too late.
 *
 * Admin-only and audited, like every other mutation here. The revoked row is kept in the audit's
 * `before` payload, so deleting it loses no history.
 */
export async function DELETE(
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

  const body = (await request.json().catch(() => null)) as { grantId?: unknown } | null;
  if (typeof body?.grantId !== "string" || !UUID_RE.test(body.grantId.trim())) {
    return NextResponse.json({ error: "grantId (uuid) is required." }, { status: 400 });
  }

  try {
    const outcome = await revokeInventoryGrant(check.email, id, body.grantId);
    if (!outcome.ok) {
      return NextResponse.json({ error: outcome.message }, { status: outcome.status });
    }
    return NextResponse.json({ message: outcome.message });
  } catch (err) {
    const message = err instanceof Error ? err.message : "Unknown error";
    console.error(`DELETE /api/users/${id}/inventory failed:`, message);
    return NextResponse.json({ error: message }, { status: 500 });
  }
}
