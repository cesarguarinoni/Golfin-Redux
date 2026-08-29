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
  /**
   * The game_banners row whose art is this tournament's sign-up-modal strip
   * (970x252). Must name a `tournament_modal` row. Null = no strip.
   * NOT `bannerUrl`, which is the 260x360 card art in a different bucket.
   */
  modalBannerId: string | null;
  /**
   * Sign-up modal blurb, English. NOT `tournaments.description` — that column is
   * GPS-owned and single-locale (see migrations/2026_08_17_tournament_description.sql).
   */
  descriptionEn: string | null;
  /** Sign-up modal blurb, Japanese. Shown only to players on Japanese. */
  descriptionJa: string | null;
  /** Build-time localization key; overrides both blurb columns when it resolves. */
  descriptionKey: string | null;
  botSeed: number | null;
  /** GPS-only; informational for golfin rows. */
  status: string | null;
  tier: string | null;
  createdAt: string | null;
  bands: PrizeBand[];
  /** Admin on/off switch. false = the game never receives it. Not the open/closed state. */
  isActive: boolean;
  // Entry restrictions (tournament_restrictions, 2026-08-19). Null = unrestricted.
  // Server enforces max_players + character bands at POST /golfin/{slug}/enter;
  // gear_rule / club_rarity_max are client-enforced by design (Q2/Q3 in
  // Docs/Specs/Active/tournament_restrictions/ARCHITECT_REVIEW.md).
  category: string | null;
  maxPlayers: number | null;
  playersPerDivision: number | null;
  divisionType: string | null;
  charRarityMin: string | null;
  charRarityMax: string | null;
  charLevelMin: number | null;
  charLevelMax: number | null;
  gearRule: string | null;
  clubRarityMax: string | null;
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
  descriptionEn: string | null;
  descriptionJa: string | null;
  descriptionKey: string | null;
  modalBannerId: string | null;
  isActive: boolean;
  // Entry restrictions — null = unrestricted (see TournamentRow).
  category: string | null;
  maxPlayers: number | null;
  playersPerDivision: number | null;
  divisionType: string | null;
  charRarityMin: string | null;
  charRarityMax: string | null;
  charLevelMin: number | null;
  charLevelMax: number | null;
  gearRule: string | null;
  clubRarityMax: string | null;
  bands: PrizeBand[];
  /** Required (typed slug) when editing a tournament that is Open or Ending. */
  confirmSlug?: string;
}

// ---------------------------------------------------------------------------
// Banners panel (SPEC game_banners §3)
// ---------------------------------------------------------------------------

/**
 * The four in-game slots, all hard-coded in the build; none can be added here.
 *
 * `home_promo`, `rankings` and `store` are AUTO-SERVED by GET /api/v1/banners.
 * `tournament_modal` is ASSIGNED per tournament instead
 * (tournaments.modal_banner_id) — with nothing assigned the sign-up modal
 * simply renders its no-banner state.
 *
 * NONE of them falls back to bundled art. A slot with nothing live is HIDDEN
 * in the client and the surrounding UI closes up (game_banners amendment A1);
 * the sprite in the prefab is an authoring placeholder a player never sees.
 * That is what makes "no row" a complete way to switch a banner off.
 */
export type BannerPlacement = "home_promo" | "rankings" | "store" | "tournament_modal";

/**
 * Derived from is_active + the schedule window — never stored. LIVE is the only
 * state a player can see; the other three all mean "the bundled sprite shows".
 */
export type BannerState = "LIVE" | "SCHEDULED" | "EXPIRED" | "OFF";

/** One row of public.game_banners as the panel renders it. */
export interface BannerRow {
  id: string;
  placement: BannerPlacement;
  /** ADMIN-ONLY name. Never sent to the client, never shown to a player. */
  label: string;
  imageUrlEn: string | null;
  imageUrlJa: string | null;
  linkUrl: string | null;
  startAt: string | null;
  endAt: string | null;
  sortOrder: number;
  isActive: boolean;
  createdAt: string | null;
  updatedAt: string | null;
}

/** Editable fields — what create/update accept over the wire. */
export interface BannerInput {
  placement: BannerPlacement;
  label: string;
  imageUrlEn: string | null;
  imageUrlJa: string | null;
  linkUrl: string | null;
  startAt: string | null;
  endAt: string | null;
  sortOrder: number;
  isActive: boolean;
  /**
   * Required (typed label) when switching a banner that is currently LIVE off,
   * the same way editing an Open tournament requires confirmSlug. Deactivation
   * is player-facing and instant.
   */
  confirmLabel?: string;
}

export interface BannersResponse {
  banners: BannerRow[];
  mock: boolean;
  /**
   * Which tournaments each banner is assigned to, keyed by banner id — read off
   * `tournaments.modal_banner_id`. Only `tournament_modal` banners can appear.
   *
   * The panel shows this as "Assigned to N tournaments" so the blast radius of
   * switching one off is visible without opening the Tournaments panel, and the
   * delete confirmation can name them.
   */
  assignedTournaments: Record<string, string[]>;
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

// ---------------------------------------------------------------------------
// Player inventory (SPEC content_player_inventory §1, §4, §5)
// ---------------------------------------------------------------------------

/**
 * ⚠️ THIS IS NOT `user_inventory`. That table is the PARTNER APP's GIFT inventory
 * (backend routers/gifts.py) and this panel never touches it. The game's
 * inventory is `profiles.golfin_inventory` — a single JSONB blob, one per
 * player, next to golfin_character_id.
 */

/** One club or character row as stored in the blob. A row that is at its catalog
 *  default is stored as a BARE ID and arrives here with `atDefault: true` — the
 *  dashboard has no catalog, so "default" is genuinely all it can say, and
 *  saying it is more honest than inventing numbers. */
export interface InventoryEntityRow {
  id: string;
  /** True when the blob stored this as a bare id — the row is at catalog default. */
  atDefault: boolean;
  /** The fields that DIFFER from the default, verbatim from the blob. */
  deltas: Record<string, string | number | boolean>;
}

/** `profiles.golfin_inventory`, decoded far enough to render. */
export interface PlayerInventory {
  /** Wire-format version (`v`), from Golfin.InventorySync.InventoryCodec. */
  formatVersion: number | null;
  clubs: InventoryEntityRow[];
  characters: InventoryEntityRow[];
  items: Record<string, number>;
  balls: Record<string, number>;
  /** (int)TicketType → balance. */
  tickets: Record<string, number>;
  unlockedHoles: number[];
  starterCharacterId: string | null;
  selectedCharacterId: string | null;
  /** UTF-8 size of the stored blob — the number SPEC §1 budgets ~3 KB for. */
  bytes: number;
  /** The raw blob, for the "show me what is actually stored" disclosure. */
  raw: string;
}

/** The kinds a grant can be. Mirrors the `kind` CHECK constraint in
 *  migrations/2026_08_26_golfin_inventory.sql — adding one here without adding
 *  it there produces a row the database refuses. */
export const INVENTORY_GRANT_KINDS = [
  "club",
  "character",
  "item",
  "ball",
  "ticket",
  "hole",
] as const;
export type InventoryGrantKind = (typeof INVENTORY_GRANT_KINDS)[number];

/** One row of `golfin_pending_grants`. */
export interface InventoryGrantRow {
  id: string;
  kind: InventoryGrantKind | string;
  refId: string;
  amount: number;
  note: string | null;
  createdBy: string | null;
  createdAt: string;
  /** Null while the client has not drained it yet. */
  appliedAt: string | null;
}

export interface PlayerInventoryResponse {
  /** Null when the player has never synced — a normal state, not an error. */
  inventory: PlayerInventory | null;
  rev: number;
  updatedAt: string | null;
  grants: InventoryGrantRow[];
  mock: boolean;
}

// ---------------------------------------------------------------------------
// Notices panel (SPEC home_notices §3)
// ---------------------------------------------------------------------------

/**
 * Same four states as a banner, derived the same way from is_active + the
 * window. Aliased rather than redeclared so the two panels can never drift
 * into meaning different things by the same name.
 */
export type NoticeState = BannerState;

/** One row of public.home_notices as the panel renders it. */
export interface NoticeRow {
  id: string;
  /** ADMIN-ONLY name. Never sent to the client, never shown to a player. */
  label: string;
  titleEn: string;
  /** Null (not "") means "no Japanese written" — the client shows the EN one. */
  titleJa: string | null;
  bodyEn: string;
  bodyJa: string | null;
  startAt: string | null;
  endAt: string | null;
  sortOrder: number;
  isActive: boolean;
  createdAt: string | null;
  updatedAt: string | null;
}

/** Editable fields — what create/update accept over the wire. */
export interface NoticeInput {
  label: string;
  titleEn: string;
  titleJa: string | null;
  bodyEn: string;
  bodyJa: string | null;
  startAt: string | null;
  endAt: string | null;
  sortOrder: number;
  isActive: boolean;
  /**
   * Required (typed label) when switching a notice that is currently LIVE off,
   * the same way a LIVE banner does. Editing the text of a live notice is NOT
   * guarded — fixing a wrong date mid-maintenance is the thing this feature
   * exists for, and a confirmation there would be friction on the hot path.
   */
  confirmLabel?: string;
}

export interface NoticesResponse {
  notices: NoticeRow[];
  mock: boolean;
}

// ---------------------------------------------------------------------------
// Rewards panel — public.game_point_actions (game_modes_admin §3)
//
// ⚠️ NOT A CONTENT CATALOG. There is no draft, no publish and no version: the
// earn path reads this table per request, so a save here is live on the NEXT
// earn. It sits with the notices/banners shapes rather than the content ones
// precisely so nobody reaches for `publishCatalog` by muscle memory.
// ---------------------------------------------------------------------------

export interface RewardActionRow {
  /** Primary key AND the string a shipped client sends. Never editable. */
  action: string;
  /**
   * NULL is a MODE, not a missing value: the client supplies the amount and the
   * server validates it against `maxPerEvent` / `dailyCap`. That is how variable
   * payouts (hole scores, tournament prizes) work. A number here is FIXED and
   * any client-sent amount is ignored.
   */
  pts: number | null;
  /** Ceiling on one award. NULL = no ceiling (only meaningful when pts is NULL). */
  maxPerEvent: number | null;
  /** Ceiling on a UTC day's awards for this action. NULL = uncapped. */
  dailyCap: number | null;
  /** The router derives a deterministic idempotency key so "once" is atomic. */
  oncePerUser: boolean;
}

/** The four editable numbers. `action` and `oncePerUser` are not in scope
 *  (game_modes_admin §3: no new actions, no deletions, and once-per-user is a
 *  grant semantic rather than an economy dial). */
export interface RewardActionInput {
  pts: number | null;
  maxPerEvent: number | null;
  dailyCap: number | null;
}

export interface RewardActionsResponse {
  actions: RewardActionRow[];
  mock: boolean;
}

// ---------------------------------------------------------------------------
// Telemetry panel (SPEC telemetry_admin_panel §3)
//
// Read-only. Every shape here is derived in lib/telemetryData.ts from rows of
// public.telemetry_events; nothing in this panel writes. Field names in the
// `payload` JSON come from beta_telemetry SPEC §1 — never invent one.
// ---------------------------------------------------------------------------

/** Resolved query window. Filtering is on `received_at` (server clock), not the
 *  client-supplied `ts`, which can be skewed on a tester's device. */
export interface TelemetryRange {
  from: string;
  to: string;
}

/** Every aggregate response carries this so a capped read is never silent. */
export interface TelemetryReadMeta {
  mock: boolean;
  range: TelemetryRange;
  /** Rows actually scanned. */
  rowCount: number;
  /** True when the 10,000-row cap was hit — the numbers below are a partial read. */
  truncated: boolean;
  /** True when `telemetry_events` does not exist yet (the beta_telemetry §2.2
   *  migration has not been applied). Every number is a real zero, not a
   *  failure — the panel says so rather than showing a red 500. */
  tableMissing: boolean;
}

export interface TelemetryKpis {
  activeTesters: number;
  activeTestersToday: number;
  sessions: number;
  sessionsToday: number;
  roundsStarted: number;
  holesCompleted: number;
  abandons: number;
  /** abandons / roundsStarted, or null when no round started. */
  abandonRate: number | null;
  crashes: number;
}

export type FunnelStageId =
  | "session_start"
  | "home"
  | "hole_select"
  | "round_start"
  | "hole_complete";

export interface FunnelStage {
  id: FunnelStageId;
  /** Sessions that reached this stage OR any later one — so the funnel can
   *  never read as increasing when an event is lost in transit. */
  sessions: number;
  /** 0..1 of all sessions in range. */
  pct: number;
}

export interface HoleStat {
  hole: number;
  plays: number;
  completions: number;
  abandons: number;
  avgStrokes: number | null;
  avgPenaltyStrokes: number | null;
  shots: number;
  /** shot_taken with terminal "OB" ÷ shot_taken, on this hole. */
  obRate: number | null;
  avgDurationS: number | null;
  fpsLowMedian: number | null;
}

export interface ClubStat {
  club: string;
  shots: number;
  avgDistanceM: number | null;
}

export interface ShotQuality {
  shotsTaken: number;
  flickRejected: number;
  shotCancelled: number;
  /** rejected ÷ (rejected + taken). The headline number of the beta. */
  flickRejectRate: number | null;
  /** cancelled ÷ (cancelled + taken). */
  cancelRate: number | null;
  obShots: number;
  obRate: number | null;
  /** shot_taken rows that carried a timing_band — i.e. real touch flicks. Bots,
   *  capture drivers and debug shots send null and are excluded from the shares. */
  timingSampled: number;
  /** Each ÷ timingSampled; null when nothing was sampled in range. */
  timingGreenRate: number | null;
  timingGoldRate: number | null;
  timingRedRate: number | null;
  /** Mean timing_mul over the sampled rows. 1.0 = nobody is paying for their timing. */
  avgTimingMul: number | null;
  clubs: ClubStat[];
}

export interface TelemetrySummaryResponse extends TelemetryReadMeta {
  kpis: TelemetryKpis;
  funnel: FunnelStage[];
  holes: HoleStat[];
  shots: ShotQuality;
  /** Distinct event names seen in range — populates the explorer's filter. */
  eventNames: string[];
}

export interface TesterRow {
  userId: string;
  /** Resolved through the Users panel's lookup; null when no auth row exists. */
  email: string | null;
  displayName: string | null;
  platform: string | null;
  deviceModel: string | null;
  os: string | null;
  appVersion: string | null;
  buildNumber: number | null;
  sessions: number;
  /** Sessions with no session_end — app killed, or the batch never flushed. */
  uncleanExits: number;
  playTimeS: number;
  rounds: number;
  holesCompleted: number;
  /** last points_changed.balance − first, or null with fewer than two. */
  pointsDelta: number | null;
  crashes: number;
  lastSeen: string | null;
}

export interface TelemetryTestersResponse extends TelemetryReadMeta {
  testers: TesterRow[];
}

export interface TelemetryEventRow {
  eventId: string;
  userId: string;
  /** email → display name → truncated uuid. */
  tester: string;
  sessionId: string;
  name: string;
  ts: string;
  receivedAt: string;
  appVersion: string | null;
  buildNumber: number | null;
  platform: string | null;
  deviceModel: string | null;
  os: string | null;
  payload: unknown;
}

export interface TelemetryEventsResponse {
  mock: boolean;
  tableMissing: boolean;
  range: TelemetryRange;
  events: TelemetryEventRow[];
  page: number;
  pageSize: number;
  /** Exact match count for the filter, or null when the DB declined to count. */
  total: number | null;
  hasMore: boolean;
}

// ---------------------------------------------------------------------------
// Admin-managed content (SPEC content_catalog §D). Backend only in Phase 0 —
// route handlers, no panels. See lib/contentData.ts / lib/contentMutations.ts.
// ---------------------------------------------------------------------------

/** One row of `content_rows` or `content_drafts`. `data` is the CSV row. */
export interface ContentStoredRow {
  catalog: string;
  rowId: string;
  data: Record<string, string>;
  minBuild: number;
  isActive: boolean;
  /** Published rows only; drafts have no version until they are published. */
  version?: number;
  updatedAt?: string | null;
  updatedBy?: string | null;
}

export interface ContentCatalogSummary {
  name: string;
  publishedVersion: number;
  isEnabled: boolean;
  publishedCount: number;
  draftCount: number;
  /** Draft rows that differ from published — what a publish would actually change. */
  dirtyCount: number;
}

export interface ContentCatalogsResponse {
  catalogs: ContentCatalogSummary[];
  /**
   * The GLOBAL kill switch — `content_settings.content_enabled` (PLAN §7.4).
   *
   * A DIFFERENT SWITCH from every `catalogs[].isEnabled`, and the distinction is the one that
   * caused the bug `content_kill_switch_and_order` fixed: a per-catalog kill takes ONE catalog
   * back to its bundled CSV, this takes ALL of them. Reads as `true` when the table or the row
   * is missing, exactly as the endpoint's `_global_enabled()` fails open — a dashboard that
   * showed OFF on an unreadable flag would send an operator to flip a switch that is already on.
   */
  globalEnabled: boolean;
  mock: boolean;
}

export interface ContentRowsResponse {
  catalog: string;
  page: number;
  limit: number;
  total: number;
  /** Column order for the page, first-seen across its rows. */
  columns: string[];
  rows: ContentStoredRow[];
  /**
   * Distinct values per filterable field, present only when `?facets=1`.
   * Read from the WHOLE catalog, not from `rows` — a value that appears only on
   * a later page still has to be selectable (content_panels_gaps §1).
   */
  facetValues?: Record<string, string[]>;
  mock: boolean;
}

export type ContentDiffKind = "added" | "changed" | "deactivated" | "reactivated";

export interface ContentFieldDiff {
  column: string;
  before: string | null;
  after: string | null;
}

export interface ContentDiffEntry {
  rowId: string;
  kind: ContentDiffKind;
  fields: ContentFieldDiff[];
}

export interface ContentDiffResponse {
  catalog: string;
  publishedVersion: number;
  counts: Record<ContentDiffKind, number>;
  entries: ContentDiffEntry[];
  mock: boolean;
}

/** One published snapshot of a catalog (content_panels_gaps §2). */
export interface ContentVersionSummary {
  catalog: string;
  version: number;
  publishedBy: string | null;
  publishedAt: string | null;
  note: string | null;
  /** Rows in the snapshot — the catalog's size AT that version. */
  rowCount: number;
}

export interface ContentVersionsResponse {
  catalog: string;
  page: number;
  limit: number;
  total: number;
  /** Newest first. v1 is always reachable by paging to the end. */
  versions: ContentVersionSummary[];
  mock: boolean;
}

export interface ContentRowInput {
  rowId: string;
  data: Record<string, string>;
  minBuild?: number;
  isActive?: boolean;
  /**
   * The caller believes this row does NOT exist yet (the editor's `+ New row`
   * drawer). Without it the PUT is an upsert and "create a row whose id is
   * already taken" is indistinguishable from "edit that row" — the create wins
   * silently. With it, `upsertDraftRow` answers 409 (shop_stocking §2).
   */
  expectNew?: boolean;
}
