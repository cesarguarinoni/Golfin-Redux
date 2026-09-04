import "server-only";
import { MOCK_BANNERS } from "./mockBanners";
import {
  MOCK_CONTENT_CATALOGS,
  MOCK_CONTENT_DRAFTS,
  MOCK_CONTENT_PUBLISHED,
  MOCK_CONTENT_VERSIONS,
} from "./mockContent";
import { MOCK_NOTICES } from "./mockNotices";
import { MOCK_VENUES } from "./mockVenues";
import { MOCK_REWARD_ACTIONS } from "./mockRewards";
import { MOCK_ACTIVITIES, MOCK_TRANSACTIONS, MOCK_USERS } from "./mock";
import { MOCK_TOURNAMENTS, MOCK_TOURNAMENT_ENTRIES } from "./mockTournaments";
import type {
  ActivityRow,
  AdminUserRow,
  AuditEntry,
  BannerRow,
  ContentCatalogSummary,
  ContentStoredRow,
  ContentVersionSummary,
  NoticeRow,
  PointsTransaction,
  RewardActionRow,
  VenueRow,
  TournamentEntryRow,
  TournamentRow,
} from "./types";

/**
 * Mutable in-memory database for mock mode. Seeded from lib/mock.ts fixtures
 * on first access; phase-2 mutations edit these arrays so the UI visibly
 * updates. Stored on globalThis so the state survives Next.js dev-mode HMR
 * and is shared across route bundles within one server process.
 */

export interface MockDb {
  users: AdminUserRow[];
  transactions: PointsTransaction[];
  activities: ActivityRow[];
  audit: AuditEntry[];
  tournaments: TournamentRow[];
  /** tournament id → entries. */
  tournamentEntries: Record<string, TournamentEntryRow[]>;
  banners: BannerRow[];
  notices: NoticeRow[];
  /** `game_point_actions` — the LIVE earn catalog (game_modes_admin §3). No
   *  draft/publish pair here, deliberately: there is none in prod either. */
  rewardActions: RewardActionRow[];
  /** `venues` — the Partners panel's spots (gps_checkin §B1). Here rather than in
   *  a module-level array because the panel's GET and its PATCH are different
   *  route bundles in dev, and only globalThis is shared between them. */
  venues: VenueRow[];
  /** Admin-managed content (SPEC content_catalog §D2). Fixtures are DELIBERATELY
   *  absurd — every price is 9999 — because §3.5 records mock fixtures being
   *  read as production facts. */
  contentCatalogs: ContentCatalogSummary[];
  contentPublished: ContentStoredRow[];
  contentDrafts: ContentStoredRow[];
  /** Published snapshots — the rollback target list (content_panels_gaps §2). */
  contentVersions: ContentVersionSummary[];
  /** `content_settings.content_enabled` — the GLOBAL kill switch (PLAN §7.4). Seeded ON, so mock
   *  mode starts in the state prod is in and the OFF banner means something when it appears. */
  contentGlobalEnabled: boolean;
}

const g = globalThis as unknown as { __golfinMockDb?: MockDb };

export function mockDb(): MockDb {
  if (!g.__golfinMockDb) {
    g.__golfinMockDb = {
      users: structuredClone(MOCK_USERS),
      transactions: structuredClone(MOCK_TRANSACTIONS),
      activities: structuredClone(MOCK_ACTIVITIES),
      audit: [],
      tournaments: structuredClone(MOCK_TOURNAMENTS),
      tournamentEntries: structuredClone(MOCK_TOURNAMENT_ENTRIES),
      banners: structuredClone(MOCK_BANNERS),
      notices: structuredClone(MOCK_NOTICES),
      rewardActions: structuredClone(MOCK_REWARD_ACTIONS),
      venues: structuredClone(MOCK_VENUES),
      contentCatalogs: structuredClone(MOCK_CONTENT_CATALOGS),
      contentPublished: structuredClone(MOCK_CONTENT_PUBLISHED),
      contentDrafts: structuredClone(MOCK_CONTENT_DRAFTS),
      contentVersions: structuredClone(MOCK_CONTENT_VERSIONS),
      contentGlobalEnabled: true,
    };
  }
  return g.__golfinMockDb;
}

/** Restore pristine fixtures (handy for tests; unused by the UI). */
export function resetMockDb(): void {
  g.__golfinMockDb = undefined;
}
