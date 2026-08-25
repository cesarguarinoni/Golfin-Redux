import "server-only";
import { MOCK_BANNERS } from "./mockBanners";
import { MOCK_CONTENT_CATALOGS, MOCK_CONTENT_DRAFTS, MOCK_CONTENT_PUBLISHED } from "./mockContent";
import { MOCK_NOTICES } from "./mockNotices";
import { MOCK_ACTIVITIES, MOCK_TRANSACTIONS, MOCK_USERS } from "./mock";
import { MOCK_TOURNAMENTS, MOCK_TOURNAMENT_ENTRIES } from "./mockTournaments";
import type {
  ActivityRow,
  AdminUserRow,
  AuditEntry,
  BannerRow,
  ContentCatalogSummary,
  ContentStoredRow,
  NoticeRow,
  PointsTransaction,
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
  /** Admin-managed content (SPEC content_catalog §D2). Fixtures are DELIBERATELY
   *  absurd — every price is 9999 — because §3.5 records mock fixtures being
   *  read as production facts. */
  contentCatalogs: ContentCatalogSummary[];
  contentPublished: ContentStoredRow[];
  contentDrafts: ContentStoredRow[];
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
      contentCatalogs: structuredClone(MOCK_CONTENT_CATALOGS),
      contentPublished: structuredClone(MOCK_CONTENT_PUBLISHED),
      contentDrafts: structuredClone(MOCK_CONTENT_DRAFTS),
    };
  }
  return g.__golfinMockDb;
}

/** Restore pristine fixtures (handy for tests; unused by the UI). */
export function resetMockDb(): void {
  g.__golfinMockDb = undefined;
}
