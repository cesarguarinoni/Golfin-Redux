import "server-only";
import { randomUUID } from "node:crypto";
import { deriveNoticeState, validateNoticeInput } from "./notice";
import { fetchNotices } from "./noticeData";
import { writeAudit } from "./audit";
import { mockDb } from "./mockStore";
import { isMockMode } from "./mode";
import { getSupabaseAdmin } from "./supabaseAdmin";
import type { MutationOutcome } from "./mutations";
import type { NoticeInput, NoticeRow } from "./types";

/**
 * Write side of the Notices panel (SPEC home_notices §3).
 * Every path: server-only, called after checkAdmin(), audited with before/after,
 * with a mock branch so the UI is exercisable on fixtures.
 */

const ok = (message: string): MutationOutcome => ({ ok: true, status: 200, message });
const fail = (status: number, message: string): MutationOutcome => ({
  ok: false,
  status,
  message,
});

/**
 * Switching a LIVE notice off is player-facing and instant — the next client
 * fetch drops it and the panel closes. Re-type the label to mean it.
 *
 * Only *deactivation* is guarded. Editing the text of a live notice is the
 * feature's whole point (a wrong maintenance date must be fixable in one
 * click), and turning one ON is reversible in the same click.
 */
function liveOffGuard(
  existing: NoticeRow,
  nextActive: boolean,
  confirmLabel: string | undefined
): string | null {
  if (nextActive) return null;
  if (deriveNoticeState(existing, Date.now()) !== "LIVE") return null;
  if ((confirmLabel ?? "").trim() !== existing.label) {
    return `"${existing.label}" is LIVE — players are reading it right now. Re-type the label to confirm switching it off.`;
  }
  return null;
}

function snapshot(n: NoticeRow): Record<string, unknown> {
  return {
    label: n.label,
    title_en: n.titleEn,
    title_ja: n.titleJa,
    body_en: n.bodyEn,
    body_ja: n.bodyJa,
    start_at: n.startAt,
    end_at: n.endAt,
    sort_order: n.sortOrder,
    is_active: n.isActive,
  };
}

/** "" and null both mean "no Japanese written"; store the null. */
function orNull(v: string | null): string | null {
  const trimmed = (v ?? "").trim();
  return trimmed.length > 0 ? (v as string) : null;
}

function toDbRow(input: NoticeInput): Record<string, unknown> {
  return {
    label: input.label.trim(),
    title_en: input.titleEn.trim(),
    title_ja: orNull(input.titleJa),
    // Bodies are NOT trimmed of interior whitespace: newlines are content here.
    // Only the ends are, so a stray trailing return does not become a blank line
    // on a phone.
    body_en: input.bodyEn.trim(),
    body_ja: orNull(input.bodyJa)?.trim() ?? null,
    start_at: input.startAt ? new Date(input.startAt).toISOString() : null,
    end_at: input.endAt ? new Date(input.endAt).toISOString() : null,
    sort_order: input.sortOrder,
    is_active: input.isActive,
    updated_at: new Date().toISOString(),
  };
}

async function loadOne(id: string): Promise<NoticeRow | undefined> {
  const { notices } = await fetchNotices();
  return notices.find((n) => n.id === id);
}

// ---------------------------------------------------------------------------
// Create
// ---------------------------------------------------------------------------

export async function createNotice(
  adminEmail: string,
  input: NoticeInput
): Promise<MutationOutcome> {
  const err = validateNoticeInput(input);
  if (err) return fail(400, err);

  if (isMockMode()) {
    const now = new Date().toISOString();
    const row: NoticeRow = {
      id: randomUUID(),
      label: input.label.trim(),
      titleEn: input.titleEn.trim(),
      titleJa: orNull(input.titleJa),
      bodyEn: input.bodyEn.trim(),
      bodyJa: orNull(input.bodyJa)?.trim() ?? null,
      startAt: input.startAt ? new Date(input.startAt).toISOString() : null,
      endAt: input.endAt ? new Date(input.endAt).toISOString() : null,
      sortOrder: input.sortOrder,
      isActive: input.isActive,
      createdAt: now,
      updatedAt: now,
    };
    mockDb().notices.unshift(row);
    await writeAudit(adminEmail, "notice_create", null, "home_notices", null, snapshot(row));
    return ok(`Created "${row.label}".`);
  }

  const res = await getSupabaseAdmin()
    .from("home_notices")
    .insert(toDbRow(input))
    .select("id")
    .single();
  if (res.error) return fail(500, `Insert failed: ${res.error.message}`);

  const created = await loadOne(String((res.data as { id: string }).id));
  await writeAudit(
    adminEmail,
    "notice_create",
    null,
    "home_notices",
    null,
    created ? snapshot(created) : { label: input.label }
  );
  return ok(`Created "${input.label.trim()}".`);
}

// ---------------------------------------------------------------------------
// Update
// ---------------------------------------------------------------------------

export async function updateNotice(
  adminEmail: string,
  id: string,
  input: NoticeInput
): Promise<MutationOutcome> {
  const err = validateNoticeInput(input);
  if (err) return fail(400, err);

  const existing = await loadOne(id);
  if (!existing) return fail(404, "Notice not found.");

  const guard = liveOffGuard(existing, input.isActive, input.confirmLabel);
  if (guard) return fail(409, guard);

  const before = snapshot(existing);

  if (isMockMode()) {
    const row = mockDb().notices.find((n) => n.id === id);
    if (!row) return fail(404, "Notice not found.");
    Object.assign(row, {
      label: input.label.trim(),
      titleEn: input.titleEn.trim(),
      titleJa: orNull(input.titleJa),
      bodyEn: input.bodyEn.trim(),
      bodyJa: orNull(input.bodyJa)?.trim() ?? null,
      startAt: input.startAt ? new Date(input.startAt).toISOString() : null,
      endAt: input.endAt ? new Date(input.endAt).toISOString() : null,
      sortOrder: input.sortOrder,
      isActive: input.isActive,
      updatedAt: new Date().toISOString(),
    });
    await writeAudit(adminEmail, "notice_update", null, "home_notices", before, snapshot(row));
    return ok(`Saved "${row.label}".`);
  }

  const upd = await getSupabaseAdmin().from("home_notices").update(toDbRow(input)).eq("id", id);
  if (upd.error) return fail(500, `Update failed: ${upd.error.message}`);

  const after = await loadOne(id);
  await writeAudit(
    adminEmail,
    "notice_update",
    null,
    "home_notices",
    before,
    after ? snapshot(after) : null
  );
  return ok(`Saved "${input.label.trim()}".`);
}

// ---------------------------------------------------------------------------
// Activate / deactivate — the one-click switch on the list row
// ---------------------------------------------------------------------------

export async function setNoticeActive(
  adminEmail: string,
  id: string,
  active: boolean,
  confirmLabel?: string
): Promise<MutationOutcome> {
  const existing = await loadOne(id);
  if (!existing) return fail(404, "Notice not found.");

  const guard = liveOffGuard(existing, active, confirmLabel);
  if (guard) return fail(409, guard);

  // Publishing an empty notice would open the panel on a blank card. The same
  // rule validateNoticeInput applies on save, enforced on this path too.
  if (active && !existing.titleEn.trim() && !existing.bodyEn.trim()) {
    return fail(
      400,
      `"${existing.label}" has no English text — write a title or body before activating.`
    );
  }

  const before = snapshot(existing);
  const action = active ? "notice_activate" : "notice_deactivate";

  if (isMockMode()) {
    const row = mockDb().notices.find((n) => n.id === id);
    if (!row) return fail(404, "Notice not found.");
    row.isActive = active;
    row.updatedAt = new Date().toISOString();
    await writeAudit(adminEmail, action, null, "home_notices", before, snapshot(row));
  } else {
    const upd = await getSupabaseAdmin()
      .from("home_notices")
      .update({ is_active: active, updated_at: new Date().toISOString() })
      .eq("id", id);
    if (upd.error) return fail(500, `Update failed: ${upd.error.message}`);

    const after = await loadOne(id);
    await writeAudit(
      adminEmail,
      action,
      null,
      "home_notices",
      before,
      after ? snapshot(after) : null
    );
  }

  return ok(
    active
      ? `"${existing.label}" is on — players see it on their next launch, or when they next open Home.`
      : `"${existing.label}" is off — it disappears from the panel on the next fetch.`
  );
}

// ---------------------------------------------------------------------------
// Delete
// ---------------------------------------------------------------------------

export async function deleteNotice(
  adminEmail: string,
  id: string,
  confirmLabel: string
): Promise<MutationOutcome> {
  const existing = await loadOne(id);
  if (!existing) return fail(404, "Notice not found.");
  if (confirmLabel.trim() !== existing.label) {
    return fail(400, "Confirmation label does not match.");
  }

  const before = snapshot(existing);

  if (isMockMode()) {
    const db = mockDb();
    db.notices = db.notices.filter((n) => n.id !== id);
    await writeAudit(adminEmail, "notice_delete", null, "home_notices", before, null);
    return ok(`Deleted "${existing.label}".`);
  }

  const { error } = await getSupabaseAdmin().from("home_notices").delete().eq("id", id);
  if (error) return fail(500, `Delete failed: ${error.message}`);

  await writeAudit(adminEmail, "notice_delete", null, "home_notices", before, null);
  return ok(`Deleted "${existing.label}".`);
}
