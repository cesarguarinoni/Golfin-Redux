import "server-only";
import { MOCK_ACTIVITIES, MOCK_TRANSACTIONS, MOCK_USERS } from "./mock";
import type {
  ActivityRow,
  AdminUserRow,
  AuditEntry,
  PointsTransaction,
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
}

const g = globalThis as unknown as { __golfinMockDb?: MockDb };

export function mockDb(): MockDb {
  if (!g.__golfinMockDb) {
    g.__golfinMockDb = {
      users: structuredClone(MOCK_USERS),
      transactions: structuredClone(MOCK_TRANSACTIONS),
      activities: structuredClone(MOCK_ACTIVITIES),
      audit: [],
    };
  }
  return g.__golfinMockDb;
}

/** Restore pristine fixtures (handy for tests; unused by the UI). */
export function resetMockDb(): void {
  g.__golfinMockDb = undefined;
}
