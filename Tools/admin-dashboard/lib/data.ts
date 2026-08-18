import "server-only";
import type { User } from "@supabase/supabase-js";
import { MOCK_CATALOG } from "./mock";
import { mockDb } from "./mockStore";
import { isMockMode } from "./mode";
import { getSupabaseAdmin } from "./supabaseAdmin";
import type {
  ActivityRow,
  AdminUserRow,
  AuditEntry,
  AuditResponse,
  AuthProvider,
  GamePointAction,
  LedgerEntry,
  PointsCurrency,
  PointsResponse,
  PointsTransaction,
  UserDetailResponse,
  UsersResponse,
} from "./types";

/** Server-side data access — branches between mock fixtures and live Supabase. */

type Row = Record<string, unknown>;

function num(v: unknown, fallback = 0): number {
  return typeof v === "number" && Number.isFinite(v) ? v : fallback;
}

function numOrNull(v: unknown): number | null {
  return typeof v === "number" && Number.isFinite(v) ? v : null;
}

function str(v: unknown): string | null {
  return typeof v === "string" && v.length > 0 ? v : null;
}

function toProviders(user: User): AuthProvider[] {
  const known: AuthProvider[] = ["email", "google", "apple"];
  const fromIdentities = (user.identities ?? [])
    .map((i) => i.provider)
    .filter((p): p is AuthProvider => (known as string[]).includes(p));
  if (fromIdentities.length > 0) return [...new Set(fromIdentities)];
  const appProvider = user.app_metadata?.provider;
  return typeof appProvider === "string" && (known as string[]).includes(appProvider)
    ? [appProvider as AuthProvider]
    : ["email"];
}

async function listAllAuthUsers(): Promise<User[]> {
  const admin = getSupabaseAdmin();
  const perPage = 1000;
  const all: User[] = [];
  for (let page = 1; ; page++) {
    const { data, error } = await admin.auth.admin.listUsers({ page, perPage });
    if (error) throw new Error(`auth.admin.listUsers failed: ${error.message}`);
    all.push(...data.users);
    if (data.users.length < perPage) break;
  }
  return all;
}

function mergeUser(user: User, profile: Row | undefined): AdminUserRow {
  const p = profile ?? {};
  const activityPts = num(p.activity_pts);
  const giftPts = num(p.gift_pts);
  return {
    id: user.id,
    email: user.email ?? "(no email)",
    displayName: str(p.display_name),
    providers: toProviders(user),
    createdAt: user.created_at,
    lastSignInAt: user.last_sign_in_at ?? null,
    emailConfirmedAt: user.email_confirmed_at ?? null,
    bannedUntil: str((user as unknown as Row).banned_until),
    activityPts,
    giftPts,
    // RP rule: RP == total_points (= activity_pts + gift_pts).
    totalPoints: num(p.total_points, activityPts + giftPts),
    avatarLevel: num(p.avatar_level, 1),
    avatarXp: num(p.avatar_xp),
    followersCount: num(p.followers_count),
    followingCount: num(p.following_count),
    badgesCount: num(p.badges_count),
    trustLevel: numOrNull(p.trust_level), // tolerate missing column
  };
}

async function fetchCatalog(): Promise<GamePointAction[]> {
  const admin = getSupabaseAdmin();
  const { data, error } = await admin
    .from("game_point_actions")
    .select("*")
    .order("action");
  if (error) {
    console.warn("game_point_actions query failed:", error.message);
    return [];
  }
  return (data as Row[]).map((r) => ({
    action: String(r.action ?? ""),
    pts: numOrNull(r.pts),
    maxPerEvent: numOrNull(r.max_per_event),
    dailyCap: numOrNull(r.daily_cap),
    oncePerUser: r.once_per_user === true,
  }));
}

export async function fetchUsers(): Promise<UsersResponse> {
  if (isMockMode()) {
    return { users: mockDb().users, catalog: MOCK_CATALOG, mock: true };
  }

  const admin = getSupabaseAdmin();
  const [authUsers, profilesRes, catalog] = await Promise.all([
    listAllAuthUsers(),
    admin.from("profiles").select("*"),
    fetchCatalog(),
  ]);

  if (profilesRes.error) {
    throw new Error(`profiles query failed: ${profilesRes.error.message}`);
  }
  const profileById = new Map<string, Row>(
    (profilesRes.data as Row[]).map((p) => [String(p.id), p])
  );

  const users = authUsers
    .map((u) => mergeUser(u, profileById.get(u.id)))
    .sort((a, b) => b.createdAt.localeCompare(a.createdAt));

  return { users, catalog, mock: false };
}

function mapTransaction(r: Row): PointsTransaction {
  const currency: PointsCurrency = r.currency === "gift" ? "gift" : "activity";
  return {
    id: String(r.id ?? ""),
    userId: String(r.user_id ?? ""),
    type: String(r.type ?? "unknown"),
    amount: num(r.amount),
    currency,
    description: str(r.description),
    createdAt: String(r.created_at ?? ""),
    idempotencyKey: str(r.idempotency_key),
  };
}

/** activities schema is loose — derive a best-effort label from whatever exists. */
function mapActivity(r: Row): ActivityRow {
  const labelSource =
    str(r.label) ??
    str(r.name) ??
    str(r.title) ??
    str(r.description) ??
    str(r.type) ??
    str(r.kind) ??
    str(r.action);
  return {
    id: String(r.id ?? crypto.randomUUID()),
    userId: String(r.user_id ?? ""),
    label: labelSource ?? "activity",
    createdAt: str(r.created_at),
  };
}

export async function fetchUserDetail(userId: string): Promise<UserDetailResponse> {
  if (isMockMode()) {
    const db = mockDb();
    return {
      transactions: db.transactions
        .filter((t) => t.userId === userId)
        .sort((a, b) => b.createdAt.localeCompare(a.createdAt)),
      activities: db.activities
        .filter((a) => a.userId === userId)
        .sort((a, b) => (b.createdAt ?? "").localeCompare(a.createdAt ?? "")),
    };
  }

  const admin = getSupabaseAdmin();

  const txRes = await admin
    .from("points_transactions")
    .select("*")
    .eq("user_id", userId)
    .order("created_at", { ascending: false })
    .limit(50);
  if (txRes.error) {
    throw new Error(`points_transactions query failed: ${txRes.error.message}`);
  }

  // activities may be empty or shaped differently — tolerate failure.
  let activities: ActivityRow[] = [];
  const actRes = await admin
    .from("activities")
    .select("*")
    .eq("user_id", userId)
    .order("created_at", { ascending: false })
    .limit(50);
  if (!actRes.error && actRes.data) {
    activities = (actRes.data as Row[]).map(mapActivity);
  }

  return {
    transactions: (txRes.data as Row[]).map(mapTransaction),
    activities,
  };
}

/** Global points ledger (Points panel) — reverse-chron, joined with emails. */
export async function fetchLedger(): Promise<PointsResponse> {
  if (isMockMode()) {
    const db = mockDb();
    const emailById = new Map(db.users.map((u) => [u.id, u.email]));
    const entries: LedgerEntry[] = db.transactions
      .map((t) => ({ ...t, userEmail: emailById.get(t.userId) ?? "(deleted user)" }))
      .sort((a, b) => b.createdAt.localeCompare(a.createdAt));
    return { entries, mock: true };
  }

  const admin = getSupabaseAdmin();
  const [txRes, authUsers] = await Promise.all([
    admin
      .from("points_transactions")
      .select("*")
      .order("created_at", { ascending: false })
      .limit(500),
    listAllAuthUsers(),
  ]);
  if (txRes.error) {
    throw new Error(`points_transactions query failed: ${txRes.error.message}`);
  }
  const emailById = new Map(authUsers.map((u) => [u.id, u.email ?? "(no email)"]));
  const entries: LedgerEntry[] = (txRes.data as Row[]).map((r) => {
    const t = mapTransaction(r);
    return { ...t, userEmail: emailById.get(t.userId) ?? "(deleted user)" };
  });
  return { entries, mock: false };
}

/** Audit log viewer (Audit Log panel) — read-only. */
export async function fetchAuditLog(): Promise<AuditResponse> {
  if (isMockMode()) {
    return { entries: mockDb().audit, mock: true };
  }

  const admin = getSupabaseAdmin();
  const { data, error } = await admin
    .from("admin_audit_log")
    .select("*")
    .order("at", { ascending: false })
    .limit(200);
  if (error) {
    throw new Error(`admin_audit_log query failed: ${error.message}`);
  }
  const entries: AuditEntry[] = (data as Row[]).map((r) => ({
    id: String(r.id ?? ""),
    at: String(r.at ?? ""),
    adminEmail: String(r.admin_email ?? ""),
    action: String(r.action ?? ""),
    targetUser: str(r.target_user),
    tableName: str(r.table_name),
    before: r.before ?? null,
    after: r.after ?? null,
  }));
  return { entries, mock: false };
}

/**
 * user_id → display identity, for panels that show a name next to a foreign
 * key (Telemetry). Deliberately lives here and reuses `listAllAuthUsers()` +
 * the one `profiles` select above rather than growing a second lookup pattern
 * in another module.
 */
export interface UserIdentity {
  email: string | null;
  displayName: string | null;
}

export async function fetchUserDirectory(): Promise<Map<string, UserIdentity>> {
  if (isMockMode()) {
    return new Map(
      mockDb().users.map((u) => [
        u.id,
        { email: u.email, displayName: u.displayName },
      ])
    );
  }

  const admin = getSupabaseAdmin();
  const [authUsers, profilesRes] = await Promise.all([
    listAllAuthUsers(),
    admin.from("profiles").select("id, display_name"),
  ]);

  const nameById = new Map<string, string | null>();
  if (profilesRes.error) {
    // A missing/renamed profiles column must not take a read-only panel down —
    // emails alone still name every tester.
    console.warn("profiles lookup failed:", profilesRes.error.message);
  } else {
    for (const p of (profilesRes.data ?? []) as Row[]) {
      nameById.set(String(p.id), str(p.display_name));
    }
  }

  return new Map(
    authUsers.map((u) => [
      u.id,
      { email: u.email ?? null, displayName: nameById.get(u.id) ?? null },
    ])
  );
}
