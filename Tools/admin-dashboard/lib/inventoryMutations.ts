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

  // `ticket` and `hole` address their target by NUMBER (a TicketType int, a hole
  // number). A non-numeric refId there is a grant the client would silently drop
  // as unapplicable — better refused here, where someone is watching.
  if ((kind === "ticket" || kind === "hole") && !/^\d+$/.test(ref)) {
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
