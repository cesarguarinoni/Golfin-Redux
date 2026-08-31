import "server-only";
import { randomUUID } from "node:crypto";
import { writeAudit } from "./audit";
import { isMockMode } from "./mode";
import { MOCK_INVENTORY_GRANTS } from "./mockInventory";
import { getSupabaseAdmin } from "./supabaseAdmin";
import { INVENTORY_GRANT_KINDS, type InventoryGrantKind } from "./types";

/**
 * Issuing an inventory grant (SPEC content_player_inventory §4, §5).
 *
 * WHY A QUEUE AND NOT AN EDIT TO THE BLOB. The client is the writer of record
 * for `profiles.golfin_inventory` — it write-behinds every 30 s — so an admin
 * write to the blob would race the player's own next push and lose to it,
 * silently and non-deterministically. A grants queue the client drains at boot
 * cannot race anything, and it is the only shape that stays correct while the
 * client remains the writer.
 *
 * ADDITIVE-ONLY, IN THREE PLACES. `amount > 0` is a CHECK constraint in the
 * schema, this function rejects a non-positive amount before it gets there, and
 * the client ignores one if it somehow arrives. A grant cannot take anything
 * away — that is what makes it safe to hand to support.
 *
 * AUDITED like every other mutation: checkAdmin() in the route, writeAudit()
 * here on the success path.
 *
 * REVOKING — see `revokeInventoryGrant` below (PLAN §6.5 decision 3). Grants are additive-only
 * with no subtraction anywhere in the system, so a fat-fingered grant is PERMANENT once it drains:
 * the only fix is SQL against a player's blob. Revoking one that has not drained yet is the cheap
 * half of that problem and closes most of it.
 */

export interface MutationOutcome {
  ok: boolean;
  status: number;
  message: string;
}

const ok = (message: string): MutationOutcome => ({ ok: true, status: 200, message });
const fail = (status: number, message: string): MutationOutcome => ({ ok: false, status, message });

const MAX_AMOUNT = 9999;
const MAX_REF_ID = 64;
const MAX_NOTE = 200;

export function isInventoryGrantKind(v: unknown): v is InventoryGrantKind {
  return typeof v === "string" && (INVENTORY_GRANT_KINDS as readonly string[]).includes(v);
}

export async function issueInventoryGrant(
  adminEmail: string,
  userId: string,
  kind: string,
  refId: string,
  amount: number,
  note: string
): Promise<MutationOutcome> {
  if (!isInventoryGrantKind(kind)) {
    return fail(400, `Unknown grant kind '${kind}'.`);
  }

  const ref = refId.trim();
  if (ref.length < 1 || ref.length > MAX_REF_ID) {
    return fail(400, `refId is required (1–${MAX_REF_ID} characters).`);
  }

  // ⚠️ A TICKET IS NO LONGER A GRANT (gacha_server_pull §5.1).
  //
  // Until 2026-09-01 an admin ticket grant queued a `kind = 'ticket'` row that
  // the client applied into `SaveData.ticketBalances` — which made the DEVICE
  // the authority on how many tickets a player held, and left the server unable
  // to answer "how many do they have" at all. `golfin_tickets` is that authority
  // now, and `golfin_ticket_credit()` is its only writer.
  //
  // Refused HERE and not only in the route, so a second caller cannot
  // reintroduce the old path by calling this function directly. The queue's
  // CHECK constraint keeps `'ticket'` for the rows that already exist and are
  // still draining; nothing new writes one.
  if (kind === "ticket") {
    return fail(
      400,
      "Tickets are no longer delivered through the grants queue. Use the Gacha tab " +
        "(or POST /api/gacha/users/:id/tickets), which writes the golfin_tickets ledger."
    );
  }

  // `hole` addresses its target by NUMBER (a hole number). A non-numeric refId
  // there is a grant the client would silently drop as unapplicable — better
  // refused here, where someone is watching.
  if (kind === "hole" && !/^\d+$/.test(ref)) {
    return fail(400, `A '${kind}' grant addresses its target by number, so refId must be an integer.`);
  }

  if (!Number.isInteger(amount) || amount < 1 || amount > MAX_AMOUNT) {
    return fail(400, `Amount must be a whole number between 1 and ${MAX_AMOUNT}. Grants cannot subtract.`);
  }

  // A club or a character is a thing you either own or do not — there is no
  // stacking (ClubOwnershipService: "clubs are unique"). Accepting amount 5
  // would imply five drivers and deliver one.
  if ((kind === "club" || kind === "character") && amount !== 1) {
    return fail(400, `A '${kind}' is owned or not owned — amount must be 1.`);
  }

  const trimmedNote = note.trim();
  if (trimmedNote.length > MAX_NOTE) {
    return fail(400, `Note must be at most ${MAX_NOTE} characters.`);
  }

  const row = {
    id: randomUUID(),
    user_id: userId,
    kind,
    ref_id: ref,
    amount,
    note: trimmedNote.length > 0 ? trimmedNote : null,
    created_by: adminEmail,
    created_at: new Date().toISOString(),
    applied_at: null as string | null,
  };

  if (isMockMode()) {
    // Mutates the fixture array directly rather than going through mockStore's
    // globalThis-backed db. Deliberate: a queued grant SHOULD evaporate when the
    // dev server restarts — it is a fixture, not state worth surviving a reload,
    // and mockStore exists for the arrays the panels read as a database.
    MOCK_INVENTORY_GRANTS.unshift({
      id: row.id,
      kind,
      refId: ref,
      amount,
      note: row.note,
      createdBy: adminEmail,
      createdAt: row.created_at,
      appliedAt: null,
    });
    await writeAudit(adminEmail, "inventory_grant", userId, "golfin_pending_grants", null, row);
    return ok(`Queued ${amount}× ${ref} (${kind}). The player receives it on their next launch.`);
  }

  const { error } = await getSupabaseAdmin().from("golfin_pending_grants").insert(row);
  if (error) {
    return fail(500, `Could not queue the grant: ${error.message}`);
  }

  await writeAudit(adminEmail, "inventory_grant", userId, "golfin_pending_grants", null, row);
  return ok(`Queued ${amount}× ${ref} (${kind}). The player receives it on their next launch.`);
}

/**
 * Delete a grant that has NOT been applied yet (PLAN §6.5 decision 3).
 *
 * WHY THIS AND NOT A GRANTS PANEL. A separate panel is not warranted at dozens of grants per
 * tester; the real gap is narrower. Grants are additive-only and there is no subtraction anywhere
 * — not in the queue, not in the merge, not on the client — so once a grant DRAINS, undoing it
 * means hand-editing a player's inventory blob in SQL. Catching it while it is still pending is
 * the cheap half of that, and it is the half that covers a wrong id typed thirty seconds ago.
 *
 * ⚠️ IT IS DELIBERATELY A DELETE, NOT A `revoked_at` COLUMN. The queue's whole contract, on both
 * the client and the API, is `applied_at is null` ⇒ pending; a third state would have to be
 * special-cased in `routers/golfin_inventory.py`'s drain AND its ack, for a row nobody will ever
 * read again. The history is not lost — `admin_audit_log` keeps the full row in the audit's
 * `before` payload, which is exactly the job it already has.
 *
 * TWO FILTERS, BOTH LOAD-BEARING. `user_id` scopes the delete to the drawer that is open, and
 * `applied_at is null` is what makes revoking an ALREADY-DRAINED grant impossible rather than
 * merely discouraged: the player has it, deleting the row would not take it back, and a UI that
 * said "revoked" would be lying. That case returns 409 and says what actually happened.
 */
export async function revokeInventoryGrant(
  adminEmail: string,
  userId: string,
  grantId: string
): Promise<MutationOutcome> {
  const id = grantId.trim();
  if (!id) return fail(400, "grantId is required.");

  if (isMockMode()) {
    const found = MOCK_INVENTORY_GRANTS.find((g) => g.id === id);
    if (!found) return fail(404, "That grant no longer exists.");
    if (found.appliedAt) {
      return fail(409, "That grant has already been applied — the player has it, so it cannot be revoked.");
    }
    MOCK_INVENTORY_GRANTS.splice(MOCK_INVENTORY_GRANTS.indexOf(found), 1);
    await writeAudit(adminEmail, "inventory_grant_revoke", userId, "golfin_pending_grants", found, null);
    return ok(`Revoked ${found.amount}× ${found.refId} (${found.kind}) before it was applied.`);
  }

  const admin = getSupabaseAdmin();

  // Read first, so the failure can SAY WHICH failure it is. A delete that matches nothing is
  // ambiguous between "already applied", "already revoked" and "wrong user", and those need three
  // different sentences in front of an operator who is mid-mistake.
  const existing = await admin
    .from("golfin_pending_grants")
    .select("id, user_id, kind, ref_id, amount, note, created_by, created_at, applied_at")
    .eq("id", id)
    .eq("user_id", userId)
    .maybeSingle();

  if (existing.error) return fail(500, `Could not read the grant: ${existing.error.message}`);
  if (!existing.data) return fail(404, "That grant no longer exists for this player.");

  const row = existing.data as { applied_at: string | null; kind: string; ref_id: string; amount: number };
  if (row.applied_at) {
    return fail(
      409,
      "That grant has already been applied — the player has it. Revoking the queue row would not " +
        "take it back, so nothing was changed."
    );
  }

  // The `applied_at is null` filter is REPEATED on the delete and is not redundant: the read above
  // and the delete are two statements, and a boot in between is exactly the window where a tester
  // drains the grant. Without it, the delete would silently win that race and the operator would
  // be told the grant was revoked while the player was already holding it.
  const res = await admin
    .from("golfin_pending_grants")
    .delete()
    .eq("id", id)
    .eq("user_id", userId)
    .is("applied_at", null)
    .select("id");

  if (res.error) return fail(500, `Could not revoke the grant: ${res.error.message}`);
  if (!res.data || res.data.length === 0) {
    return fail(409, "The player applied that grant while this page was open — nothing was revoked.");
  }

  await writeAudit(adminEmail, "inventory_grant_revoke", userId, "golfin_pending_grants", existing.data, null);
  return ok(`Revoked ${row.amount}× ${row.ref_id} (${row.kind}) before it was applied.`);
}
