import type { ContentCatalogSummary, ContentStoredRow } from "./types";

/**
 * Mock-mode fixtures for the content catalogs.
 *
 * ⚠️ DELIBERATELY, VISIBLY FAKE. Every price is 9999 and every id carries a
 * `mock_` prefix. ADMIN_DASHBOARD_OPS.md §3.5 records a real incident where mock
 * fixtures were read as production facts; a fixture that looks plausible is the
 * bug. If you find yourself wanting realistic numbers here, you want a real
 * service key instead (see lib/mode.ts).
 */

export const MOCK_CONTENT_CATALOGS: ContentCatalogSummary[] = [
  { name: "clubs", publishedVersion: 9999, isEnabled: true, publishedCount: 2, draftCount: 2, dirtyCount: 1 },
  { name: "characters", publishedVersion: 9999, isEnabled: true, publishedCount: 1, draftCount: 1, dirtyCount: 0 },
  { name: "items", publishedVersion: 9999, isEnabled: true, publishedCount: 0, draftCount: 0, dirtyCount: 0 },
  { name: "bags", publishedVersion: 9999, isEnabled: true, publishedCount: 0, draftCount: 0, dirtyCount: 0 },
  { name: "balls", publishedVersion: 9999, isEnabled: true, publishedCount: 0, draftCount: 0, dirtyCount: 0 },
  { name: "texts", publishedVersion: 9999, isEnabled: true, publishedCount: 1, draftCount: 1, dirtyCount: 0 },
  { name: "shop_catalog", publishedVersion: 9999, isEnabled: true, publishedCount: 1, draftCount: 1, dirtyCount: 0 },
];

const row = (
  catalog: string,
  rowId: string,
  data: Record<string, string>
): ContentStoredRow => ({ catalog, rowId, data, minBuild: 0, isActive: true, version: 9999 });

export const MOCK_CONTENT_PUBLISHED: ContentStoredRow[] = [
  row("clubs", "mock_club_driver", {
    id: "mock_club_driver", name: "MOCK Driver", type: "Driver", rarity: "Common", brand: "MOCK",
    basePower: "9999", baseAccuracy: "9999", maxDurability: "9999", startLevel: "1", maxLevel: "9999",
  }),
  row("clubs", "mock_club_putter", {
    id: "mock_club_putter", name: "MOCK Putter", type: "Putter", rarity: "Common", brand: "MOCK",
    basePower: "9999", baseAccuracy: "9999", maxDurability: "9999", startLevel: "1", maxLevel: "9999",
  }),
  row("characters", "mock_char", {
    id: "mock_char", name: "MOCK", lastName: "FIXTURE", rarity: "Common",
    baseStrength: "1", baseClubControl: "1", baseRecovery: "1", baseStamina: "1",
    startLevel: "1", maxLevel: "9999",
  }),
  row("texts", "MOCK_KEY", { key: "MOCK_KEY", English: "MOCK FIXTURE", Japanese: "モックデータ" }),
  row("shop_catalog", "mock_shop_entry", {
    entryId: "mock_shop_entry", category: "club", refId: "mock_club_driver",
    rpCost: "9999", saleRpCost: "9998", sortOrder: "9999", popular: "false", offer: "false", rarity: "",
  }),
];

/** Drafts start identical to published except one obviously-dirty club row. */
export const MOCK_CONTENT_DRAFTS: ContentStoredRow[] = MOCK_CONTENT_PUBLISHED.map((r) =>
  r.rowId === "mock_club_driver"
    ? { ...r, data: { ...r.data, name: "MOCK Driver (EDITED DRAFT)" } }
    : { ...r }
);
