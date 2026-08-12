import "server-only";
import { isMockMode } from "./mode";
import { getSupabaseAdmin } from "./supabaseAdmin";

/**
 * Audit trail writer — inserts into public.admin_audit_log
 * (see migrations/2026_08_13_admin_audit_log.sql).
 *
 * v1 is read-only so no UI calls this yet, but it is wired and callable:
 * future mutation routes must call writeAudit() before returning success.
 * In mock mode entries are logged to the server console instead of Postgres.
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
