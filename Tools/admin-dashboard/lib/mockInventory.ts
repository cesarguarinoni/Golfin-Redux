import type { InventoryGrantRow, PlayerInventory } from "./types";

/**
 * Mock inventory fixtures — DELIBERATELY ABSURD, the same rule the content
 * fixtures follow (`lib/mockContent.ts`: every price is 9999).
 *
 * The scar this exists for is recorded in content_catalog SPEC §3.5: mock
 * fixtures being read as production facts. An inventory panel is the very worst
 * place for that to happen, because a plausible-looking blob is exactly what a
 * support question looks like. So the club is `club_MOCK_NOT_REAL`, every count
 * is 9999, and the hole list runs to 999.
 */

const MOCK_BLOB = {
  v: 1,
  clubs: [
    "club_MOCK_NOT_REAL_default",
    { id: "club_MOCK_NOT_REAL_levelled", lv: 9999, sPow: 99 },
  ],
  characters: ["char_MOCK_NOT_REAL", { id: "char_MOCK_LOCKED", own: false }],
  items: { item_MOCK_NOT_REAL: 9999 },
  balls: { ball_MOCK_NOT_REAL: -1 },
  tickets: { "0": 9999 },
  holes: [1, 2, 999],
  starter: "char_MOCK_NOT_REAL",
  selected: "char_MOCK_NOT_REAL",
};

const RAW = JSON.stringify(MOCK_BLOB);

export const MOCK_PLAYER_INVENTORY: PlayerInventory = {
  formatVersion: 1,
  clubs: [
    { id: "club_MOCK_NOT_REAL_default", atDefault: true, deltas: {} },
    {
      id: "club_MOCK_NOT_REAL_levelled",
      atDefault: false,
      deltas: { lv: 9999, sPow: 99 },
    },
  ],
  characters: [
    { id: "char_MOCK_NOT_REAL", atDefault: true, deltas: {} },
    { id: "char_MOCK_LOCKED", atDefault: false, deltas: { own: false } },
  ],
  items: { item_MOCK_NOT_REAL: 9999 },
  balls: { ball_MOCK_NOT_REAL: -1 },
  tickets: { "0": 9999 },
  unlockedHoles: [1, 2, 999],
  starterCharacterId: "char_MOCK_NOT_REAL",
  selectedCharacterId: "char_MOCK_NOT_REAL",
  bytes: new TextEncoder().encode(RAW).length,
  raw: RAW,
};

export const MOCK_INVENTORY_REV = 9999;

export const MOCK_INVENTORY_GRANTS: InventoryGrantRow[] = [
  {
    id: "00000000-0000-4000-8000-00000000mock",
    kind: "item",
    refId: "item_MOCK_NOT_REAL",
    amount: 9999,
    note: "MOCK FIXTURE — not a real grant",
    createdBy: "mock@example.invalid",
    createdAt: "2026-08-26T00:00:00.000Z",
    appliedAt: null,
  },
];
