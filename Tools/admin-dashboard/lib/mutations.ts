import "server-only";
import { randomUUID } from "node:crypto";
import { writeAudit } from "./audit";
import { mockDb } from "./mockStore";
import { isMockMode } from "./mode";
import { getSupabaseAdmin } from "./supabaseAdmin";
import type { AdminUserRow, UserActionKind } from "./types";

/**
 * Phase-2 admin mutations. Every function:
 *   - is server-only and called from route handlers AFTER checkAdmin(),
 *   - writes an audit row via writeAudit() (before/after snapshots),
 *   - has a mock branch mutating lib/mockStore.ts so the UI updates visibly.
 */

export interface MutationOutcome {
  ok: boolean;
  status: number;
  message: string;
}

const ok = (message: string): MutationOutcome => ({ ok: true, status: 200, message });
const fail = (status: number, message: string): MutationOutcome => ({ ok: false, status, message });

/** Supabase ban duration for "permanent" (~100 years). 'none' lifts the ban. */
const BAN_DURATION = "876000h";

function findMockUser(userId: string): AdminUserRow | undefined {
  return mockDb().users.find((u) => u.id === userId);
}

function rpSnapshot(u: AdminUserRow): Record<string, number> {
  return {
    activity_pts: u.activityPts,
    gift_pts: u.giftPts,
    total_points: u.totalPoints,
  };
}

// ---------------------------------------------------------------------------
// Edit display name — writes profiles.display_name AND auth user_metadata
// (mirrors the prod profile-sync trigger's shape).
// ---------------------------------------------------------------------------
export async function updateDisplayName(
  adminEmail: string,
  userId: string,
  displayName: string
): Promise<MutationOutcome> {
  const name = displayName.trim();
  if (name.length < 1 || name.length > 40) {
    return fail(400, "Display name must be 1–40 characters.");
  }

  if (isMockMode()) {
    const u = findMockUser(userId);
    if (!u) return fail(404, "User not found.");
    const before = { display_name: u.displayName };
    u.displayName = name;
    await writeAudit(adminEmail, "edit_display_name", userId, "profiles", before, {
      display_name: name,
    });
    return ok(`Display name updated to "${name}".`);
  }

  const admin = getSupabaseAdmin();
  const beforeRes = await admin
    .from("profiles")
    .select("display_name")
    .eq("id", userId)
    .maybeSingle();
  if (beforeRes.error) return fail(500, beforeRes.error.message);

  const authRes = await admin.auth.admin.updateUserById(userId, {
    user_metadata: { display_name: name },
  });
  if (authRes.error) return fail(500, authRes.error.message);

  const profRes = await admin
    .from("profiles")
    .update({ display_name: name })
    .eq("id", userId);
  if (profRes.error) return fail(500, profRes.error.message);

  await writeAudit(
    adminEmail,
    "edit_display_name",
    userId,
    "profiles",
    { display_name: beforeRes.data?.display_name ?? null },
    { display_name: name }
  );
  return ok(`Display name updated to "${name}".`);
}

// ---------------------------------------------------------------------------
// One-shot auth actions: resend confirmation, password reset, confirm email,
// ban, unban.
// ---------------------------------------------------------------------------
export async function performUserAction(
  adminEmail: string,
  userId: string,
  action: UserActionKind
): Promise<MutationOutcome> {
  if (isMockMode()) {
    const u = findMockUser(userId);
    if (!u) return fail(404, "User not found.");

    switch (action) {
      case "resend_confirmation": {
        if (u.emailConfirmedAt) {
          return fail(400, "Email is already confirmed.");
        }
        await writeAudit(adminEmail, "resend_confirmation_email", userId, "auth.users", null, null);
        return ok(`Confirmation email resent to ${u.email} (mock — nothing actually sent).`);
      }
      case "send_password_reset": {
        await writeAudit(adminEmail, "send_password_reset", userId, "auth.users", null, null);
        return ok(`Password-reset email sent to ${u.email} (mock — nothing actually sent).`);
      }
      case "confirm_email": {
        if (u.emailConfirmedAt) return fail(400, "Email is already confirmed.");
        const before = { email_confirmed_at: u.emailConfirmedAt };
        u.emailConfirmedAt = new Date().toISOString();
        await writeAudit(adminEmail, "confirm_email", userId, "auth.users", before, {
          email_confirmed_at: u.emailConfirmedAt,
        });
        return ok(`${u.email} marked as confirmed.`);
      }
      case "ban": {
        const before = { banned_until: u.bannedUntil };
        const until = new Date(Date.now() + 876000 * 3600 * 1000).toISOString();
        u.bannedUntil = until;
        await writeAudit(adminEmail, "ban_user", userId, "auth.users", before, {
          banned_until: until,
        });
        return ok(`${u.email} banned (banned_until ≈ +100 years).`);
      }
      case "unban": {
        const before = { banned_until: u.bannedUntil };
        u.bannedUntil = null;
        await writeAudit(adminEmail, "unban_user", userId, "auth.users", before, {
          banned_until: null,
        });
        return ok(`${u.email} unbanned.`);
      }
    }
  }

  const admin = getSupabaseAdmin();
  const userRes = await admin.auth.admin.getUserById(userId);
  if (userRes.error || !userRes.data.user) {
    return fail(404, userRes.error?.message ?? "User not found.");
  }
  const user = userRes.data.user;
  const email = user.email;
  if (!email) return fail(400, "User has no email address.");
  const bannedBefore =
    (user as unknown as Record<string, unknown>).banned_until ?? null;

  switch (action) {
    case "resend_confirmation": {
      if (user.email_confirmed_at) return fail(400, "Email is already confirmed.");
      // NOTE: auth.resend({type:'signup'}) re-sends the confirmation email for an
      // existing unconfirmed user. auth.admin.generateLink({type:'signup'}) would
      // require the user's password, which we do not have. Verify template config
      // in the Supabase dashboard before relying on this in prod.
      const { error } = await admin.auth.resend({ type: "signup", email });
      if (error) return fail(500, error.message);
      await writeAudit(adminEmail, "resend_confirmation_email", userId, "auth.users", null, null);
      return ok(`Confirmation email resent to ${email}.`);
    }
    case "send_password_reset": {
      const { error } = await admin.auth.resetPasswordForEmail(email);
      if (error) return fail(500, error.message);
      await writeAudit(adminEmail, "send_password_reset", userId, "auth.users", null, null);
      return ok(`Password-reset email sent to ${email}.`);
    }
    case "confirm_email": {
      if (user.email_confirmed_at) return fail(400, "Email is already confirmed.");
      const { data, error } = await admin.auth.admin.updateUserById(userId, {
        email_confirm: true,
      });
      if (error) return fail(500, error.message);
      await writeAudit(
        adminEmail,
        "confirm_email",
        userId,
        "auth.users",
        { email_confirmed_at: null },
        { email_confirmed_at: data.user.email_confirmed_at ?? null }
      );
      return ok(`${email} marked as confirmed.`);
    }
    case "ban": {
      const { error } = await admin.auth.admin.updateUserById(userId, {
        ban_duration: BAN_DURATION,
      });
      if (error) return fail(500, error.message);
      await writeAudit(
        adminEmail,
        "ban_user",
        userId,
        "auth.users",
        { banned_until: bannedBefore },
        { ban_duration: BAN_DURATION }
      );
      return ok(`${email} banned (ban_duration ${BAN_DURATION}).`);
    }
    case "unban": {
      const { error } = await admin.auth.admin.updateUserById(userId, {
        ban_duration: "none",
      });
      if (error) return fail(500, error.message);
      await writeAudit(
        adminEmail,
        "unban_user",
        userId,
        "auth.users",
        { banned_until: bannedBefore },
        { banned_until: null }
      );
      return ok(`${email} unbanned.`);
    }
  }
}

// ---------------------------------------------------------------------------
// Delete user — requires the caller to re-type the email (server-enforced).
// FK cascade removes profiles, points_transactions, activities.
// ---------------------------------------------------------------------------
export async function deleteUser(
  adminEmail: string,
  userId: string,
  confirmEmail: string
): Promise<MutationOutcome> {
  if (isMockMode()) {
    const db = mockDb();
    const u = findMockUser(userId);
    if (!u) return fail(404, "User not found.");
    if (confirmEmail.trim().toLowerCase() !== u.email.toLowerCase()) {
      return fail(400, "Confirmation email does not match this user's email.");
    }
    const before = { ...u };
    db.users = db.users.filter((x) => x.id !== userId);
    db.transactions = db.transactions.filter((t) => t.userId !== userId);
    db.activities = db.activities.filter((a) => a.userId !== userId);
    await writeAudit(adminEmail, "delete_user", userId, "auth.users", before, null);
    return ok(`Deleted ${u.email} and all dependent rows (mock).`);
  }

  const admin = getSupabaseAdmin();
  const userRes = await admin.auth.admin.getUserById(userId);
  if (userRes.error || !userRes.data.user) {
    return fail(404, userRes.error?.message ?? "User not found.");
  }
  const user = userRes.data.user;
  if (confirmEmail.trim().toLowerCase() !== (user.email ?? "").toLowerCase()) {
    return fail(400, "Confirmation email does not match this user's email.");
  }

  const profileRes = await admin
    .from("profiles")
    .select("*")
    .eq("id", userId)
    .maybeSingle();
  const before = {
    auth_user: {
      id: user.id,
      email: user.email,
      created_at: user.created_at,
      last_sign_in_at: user.last_sign_in_at,
    },
    profile: profileRes.data ?? null,
  };

  const { error } = await admin.auth.admin.deleteUser(userId);
  if (error) return fail(500, error.message);

  await writeAudit(adminEmail, "delete_user", userId, "auth.users", before, null);
  return ok(`Deleted ${user.email} and all dependent rows.`);
}

// ---------------------------------------------------------------------------
// RP grant / adjust. Positive → earn_pts_v2 rpc, negative → spend_pts rpc.
// Mock simulates both, including the insufficient branch and the
// activity-first-then-gift debit order.
// ---------------------------------------------------------------------------
export async function adjustRp(
  adminEmail: string,
  userId: string,
  amount: number,
  reason: string
): Promise<MutationOutcome> {
  if (!Number.isInteger(amount) || amount === 0) {
    return fail(400, "Amount must be a non-zero integer.");
  }
  if (Math.abs(amount) > 1_000_000) {
    return fail(400, "Amount out of range (max ±1,000,000).");
  }
  const trimmedReason = reason.trim();
  if (trimmedReason.length < 1 || trimmedReason.length > 200) {
    return fail(400, "Reason is required (1–200 characters).");
  }
  const description = `admin: ${trimmedReason}`;

  if (isMockMode()) {
    const db = mockDb();
    const u = findMockUser(userId);
    if (!u) return fail(404, "User not found.");
    const before = rpSnapshot(u);
    const now = new Date().toISOString();

    if (amount > 0) {
      u.activityPts += amount;
      u.totalPoints = u.activityPts + u.giftPts;
      db.transactions.unshift({
        id: randomUUID(),
        userId,
        type: "manual_admin_grant",
        amount,
        currency: "activity",
        description,
        createdAt: now,
        idempotencyKey: randomUUID(),
      });
      await writeAudit(adminEmail, "rp_adjust", userId, "profiles", before, rpSnapshot(u));
      return ok(`Granted +${amount} RP to ${u.email}.`);
    }

    const abs = -amount;
    if (abs > u.totalPoints) {
      // Mirrors the live spend_pts {status:'insufficient'} payload.
      return fail(
        409,
        `Insufficient RP: ${u.email} has ${u.totalPoints} RP, tried to deduct ${abs}.`
      );
    }
    // Debit order: activity first, then gift.
    const fromActivity = Math.min(u.activityPts, abs);
    const fromGift = abs - fromActivity;
    u.activityPts -= fromActivity;
    u.giftPts -= fromGift;
    u.totalPoints = u.activityPts + u.giftPts;
    if (fromActivity > 0) {
      db.transactions.unshift({
        id: randomUUID(),
        userId,
        type: "spend",
        amount: -fromActivity,
        currency: "activity",
        description,
        createdAt: now,
        idempotencyKey: randomUUID(),
      });
    }
    if (fromGift > 0) {
      db.transactions.unshift({
        id: randomUUID(),
        userId,
        type: "spend",
        amount: -fromGift,
        currency: "gift",
        description,
        createdAt: now,
        idempotencyKey: randomUUID(),
      });
    }
    await writeAudit(adminEmail, "rp_adjust", userId, "profiles", before, rpSnapshot(u));
    return ok(`Deducted ${abs} RP from ${u.email}.`);
  }

  const admin = getSupabaseAdmin();
  const beforeRes = await admin
    .from("profiles")
    .select("activity_pts, gift_pts, total_points")
    .eq("id", userId)
    .maybeSingle();
  if (beforeRes.error) return fail(500, beforeRes.error.message);
  if (!beforeRes.data) return fail(404, "Profile not found.");

  if (amount > 0) {
    // Deployed signature (migrations/2026_08_12_points_spend_idempotency.sql,
    // applied to prod 2026-08-12): earn_pts_v2(p_user_id uuid, p_action text,
    // p_pts int, p_description text, p_key uuid).
    const { error } = await admin.rpc("earn_pts_v2", {
      p_user_id: userId,
      p_action: "manual_admin_grant",
      p_pts: amount,
      p_description: description,
      p_key: randomUUID(),
    });
    if (error) return fail(500, `earn_pts_v2 failed: ${error.message}`);
  } else {
    // Deployed signature (same migration): spend_pts(p_user_id uuid,
    // p_amount int, p_reason text, p_key uuid).
    const { data, error } = await admin.rpc("spend_pts", {
      p_user_id: userId,
      p_amount: -amount,
      p_reason: description,
      p_key: randomUUID(),
    });
    if (error) return fail(500, `spend_pts failed: ${error.message}`);
    const status =
      data && typeof data === "object"
        ? (data as { status?: string }).status
        : undefined;
    if (status === "insufficient") {
      return fail(
        409,
        `Insufficient RP: the user has ${beforeRes.data.total_points ?? 0} RP, tried to deduct ${-amount}.`
      );
    }
  }

  const afterRes = await admin
    .from("profiles")
    .select("activity_pts, gift_pts, total_points")
    .eq("id", userId)
    .maybeSingle();
  await writeAudit(
    adminEmail,
    "rp_adjust",
    userId,
    "profiles",
    beforeRes.data,
    afterRes.data ?? null
  );
  return ok(
    amount > 0 ? `Granted +${amount} RP.` : `Deducted ${-amount} RP.`
  );
}
