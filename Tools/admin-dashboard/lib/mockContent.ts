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
  { name: "items", publishedVersion: 9999, isEnabled: true, publishedCount: 2, draftCount: 2, dirtyCount: 0 },
  { name: "bags", publishedVersion: 9999, isEnabled: true, publishedCount: 2, draftCount: 2, dirtyCount: 0 },
  // DISABLED on purpose: the kill-switch badge and the "the game is not being
  // served this catalog" copy need a catalog in that state to render against.
  { name: "balls", publishedVersion: 9999, isEnabled: false, publishedCount: 2, draftCount: 2, dirtyCount: 0 },
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
  // A key with NO Japanese, so the Texts panel's "No Japanese" badge has
  // something to render in mock mode.
  row("texts", "MOCK_KEY_NO_JA", { key: "MOCK_KEY_NO_JA", English: "MOCK FIXTURE (no JA)", Japanese: "" }),
  row("items", "mock_item_repairkit", {
    id: "mock_item_repairkit", name: "MOCK Repair Kit", category: "repair", rarity: "Common",
    restorePercent: "9999", thumbnailSprite: "MOCK-Thumb", fullSprite: "MOCK-Full",
    proTip: "MOCK FIXTURE", info: "MOCK FIXTURE",
  }),
  row("items", "mock_item_potion", {
    id: "mock_item_potion", name: "MOCK Stamina Potion", category: "stamina", rarity: "Rare",
    restorePercent: "9999", thumbnailSprite: "MOCK-Thumb", fullSprite: "MOCK-Full",
    proTip: "MOCK FIXTURE", info: "MOCK FIXTURE",
  }),
  row("bags", "mock_bag_starter", {
    id: "mock_bag_starter", name: "MOCK Starter Bag", rarity: "Common",
    thumbnail: "MOCK-Thumb", fullImage: "MOCK-Full", description: "MOCK FIXTURE", unlocked: "true",
  }),
  row("bags", "mock_bag_locked", {
    id: "mock_bag_locked", name: "MOCK Locked Bag", rarity: "Legendary",
    thumbnail: "MOCK-Thumb", fullImage: "MOCK-Full", description: "MOCK FIXTURE", unlocked: "false",
  }),
  row("balls", "mock_ball_default", {
    id: "mock_ball_default", name: "MOCK Ball", brand: "MOCK", power: "9999", rebound: "9999",
    windResistance: "9999", roll: "9999", spin: "9999", thumbnailSprite: "MOCK-Thumb",
    fullSprite: "MOCK-Full", info: "MOCK FIXTURE",
  }),
  // Deactivated on purpose: the shop typeahead must be seen NOT offering it,
  // and the diff's `deactivated` category needs a row to be about.
  { ...row("balls", "mock_ball_retired", {
      id: "mock_ball_retired", name: "MOCK Retired Ball", brand: "MOCK", power: "9999",
      rebound: "9999", windResistance: "9999", roll: "9999", spin: "9999",
      thumbnailSprite: "MOCK-Thumb", fullSprite: "MOCK-Full", info: "MOCK FIXTURE",
    }), isActive: false },
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
