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

// ---------------------------------------------------------------------------
// Tournaments panel (SPEC tournaments_server_side §5)
// ---------------------------------------------------------------------------

/** Derived from start_at/end_at — never read from tournaments.status for golfin rows. */
export type TournamentState = "Upcoming" | "Open" | "Ending" | "Ended" | "Unknown";

export type TournamentKind = "golfin" | "gps";

/** One row of public.tournament_prize_bands. */
export interface PrizeBand {
  /** Empty string for a band added in the editor and not yet saved. */
  id: string;
  rankFrom: number;
  rankTo: number;
  rpReward: number;
  itemRewardId: string | null;
}

/** public.tournaments, golfin-flavoured. GPS rows come through with nulls. */
export interface TournamentRow {
  id: string;
  kind: TournamentKind;
  /** Game-facing stable key (the old tournaments.csv id). Null on gps rows. */
  slug: string | null;
  title: string;
  /** Japanese display name; used for JP players when no name_key resolves. */
  titleJa: string | null;
  nameKey: string | null;
  courseId: string | null;
  holeSet: string | null;
  startAt: string | null;
  endAt: string | null;
  resolveDelayMinutes: number | null;
  entryFeePts: number;
  botFieldId: string | null;
  sponsorName: string | null;
  leagueKey: string | null;
  bannerUrl: string | null;
  botSeed: number | null;
  /** GPS-only; informational for golfin rows. */
  status: string | null;
  tier: string | null;
  createdAt: string | null;
  bands: PrizeBand[];
  /** Admin on/off switch. false = the game never receives it. Not the open/closed state. */
  isActive: boolean;
  /** Rows in tournament_entries for this tournament (all, including bots). */
  entryCount: number;
  humanEntryCount: number;
}

/** public.tournament_entries — read-only in the panel. */
export interface TournamentEntryRow {
  id: string;
  userId: string | null;
  userEmail: string | null;
  displayName: string | null;
  characterId: string | null;
  isBot: boolean;
  bestScore: number | null;
  holesCompleted: number;
  status: string;
  finalRank: number | null;
  prizePtsAwarded: number | null;
  prizeClaimed: boolean;
  enteredAt: string | null;
  submittedAt: string | null;
}

export interface TournamentsResponse {
  tournaments: TournamentRow[];
  mock: boolean;
}

export interface TournamentEntriesResponse {
  entries: TournamentEntryRow[];
  mock: boolean;
}

/** Editable fields — what create/update accept over the wire. */
export interface TournamentInput {
  slug: string;
  title: string;
  titleJa: string | null;
  nameKey: string | null;
  courseId: string;
  holeSet: string;
  startAt: string;
  endAt: string;
  resolveDelayMinutes: number;
  entryFeePts: number;
  botFieldId: string;
  sponsorName: string | null;
  leagueKey: string | null;
  bannerUrl: string | null;
  isActive: boolean;
  bands: PrizeBand[];
  /** Required (typed slug) when editing a tournament that is Open or Ending. */
  confirmSlug?: string;
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
