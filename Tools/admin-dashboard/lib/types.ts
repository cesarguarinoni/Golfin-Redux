/** Shared domain types for the GOLFIN admin dashboard. */

export type AuthProvider = "email" | "google" | "apple";

/** One row in the Users panel list — auth.users joined with public.profiles. */
export interface AdminUserRow {
  id: string;
  email: string;
  displayName: string | null;
  providers: AuthProvider[];
  createdAt: string;
  lastSignInAt: string | null;
  emailConfirmedAt: string | null;
  bannedUntil: string | null;
  /** public.profiles.activity_pts */
  activityPts: number;
  /** public.profiles.gift_pts */
  giftPts: number;
  /** RP rule: Reward Points == total_points (= activity_pts + gift_pts). */
  totalPoints: number;
  avatarLevel: number;
  avatarXp: number;
  followersCount: number;
  followingCount: number;
  badgesCount: number;
  /** Tolerate missing column — null when absent. */
  trustLevel: number | null;
}

export type PointsCurrency = "activity" | "gift";

export interface PointsTransaction {
  id: string;
  userId: string;
  type: string;
  /** Negative = spend. */
  amount: number;
  currency: PointsCurrency;
  /** Often Japanese. */
  description: string | null;
  createdAt: string;
  idempotencyKey: string | null;
}

/** GPS check-in etc. Schema is loose — tolerate anything, may be empty. */
export interface ActivityRow {
  id: string;
  userId: string;
  /** Best-effort human label derived from whatever columns exist. */
  label: string;
  createdAt: string | null;
}

/** public.game_point_actions — read-only economy catalog. */
export interface GamePointAction {
  action: string;
  pts: number | null;
  maxPerEvent: number | null;
  dailyCap: number | null;
  oncePerUser: boolean;
}

export interface UsersResponse {
  users: AdminUserRow[];
  catalog: GamePointAction[];
  /** True when the server is running on fixtures (mock mode). */
  mock: boolean;
}

export interface UserDetailResponse {
  transactions: PointsTransaction[];
  activities: ActivityRow[];
}

/** points_transactions row joined with the owner's email (Points panel). */
export interface LedgerEntry extends PointsTransaction {
  userEmail: string;
}

export interface PointsResponse {
  entries: LedgerEntry[];
  mock: boolean;
}

/** public.admin_audit_log row (Audit Log panel). */
export interface AuditEntry {
  id: string;
  at: string;
  adminEmail: string;
  action: string;
  targetUser: string | null;
  tableName: string | null;
  before: unknown;
  after: unknown;
}

export interface AuditResponse {
  entries: AuditEntry[];
  mock: boolean;
}

/** Users-panel admin actions (POST /api/users/:id/actions). */
export const USER_ACTION_KINDS = [
  "resend_confirmation",
  "send_password_reset",
  "confirm_email",
  "ban",
  "unban",
] as const;
export type UserActionKind = (typeof USER_ACTION_KINDS)[number];

export interface MutationResponse {
  message: string;
}
