import "server-only";
import { isMockMode } from "./mode";
import { mockDb } from "./mockStore";
import { getSupabaseAdmin } from "./supabaseAdmin";

/**
 * Audit trail writer — inserts into public.admin_audit_log
 * (see migrations/2026_08_13_admin_audit_log.sql).
 *
 * Every mutation route calls writeAudit() as part of its success path.
 * In mock mode entries go to the in-memory mock audit log (visible in the
 * Audit Log panel) and the server console instead of Postgres.
 */
export async function writeAudit(
  adminEmail: string,
  action: string,
  targetUser: string | null,
  tableName: string | null,
  before: unknown,
  after: unknown
): Promise<void> {
  const entry = {
    admin_email: adminEmail,
    action,
    target_user: targetUser,
    table_name: tableName,
    before: before ?? null,
    after: after ?? null,
  };

  if (isMockMode()) {
    mockDb().audit.unshift({
      id: crypto.randomUUID(),
      at: new Date().toISOString(),
      adminEmail,
      action,
      targetUser,
      tableName,
      before: before ?? null,
      after: after ?? null,
    });
    console.info("[audit:mock]", JSON.stringify(entry));
    return;
  }

  const { error } = await getSupabaseAdmin()
    .from("admin_audit_log")
    .insert(entry);

  if (error) {
    throw new Error(`writeAudit failed: ${error.message}`);
  }
}
