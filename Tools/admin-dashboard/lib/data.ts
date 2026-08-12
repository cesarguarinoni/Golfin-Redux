import "server-only";
import type { User } from "@supabase/supabase-js";
import { getMockUserDetail, getMockUsers, MOCK_CATALOG } from "./mock";
import { isMockMode } from "./mode";
import { getSupabaseAdmin } from "./supabaseAdmin";
import type {
  ActivityRow,
  AdminUserRow,
  AuthProvider,
  GamePointAction,
  PointsCurrency,
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
    return { users: getMockUsers(), catalog: MOCK_CATALOG };
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

  return { users, catalog };
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
    return getMockUserDetail(userId);
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
