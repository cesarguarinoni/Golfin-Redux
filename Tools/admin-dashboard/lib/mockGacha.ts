import type {
  GachaPullRow,
  PlayerPityRow,
  TicketBalanceRow,
  TicketTransactionRow,
} from "./types";

/**
 * Mock fixtures for the Gacha panel (gacha_server_pull §6).
 *
 * ⚠️ DELIBERATELY ABSURD NUMBERS, the same convention `lib/mockContent.ts`
 * follows for the same reason: ADMIN_DASHBOARD_OPS §3.5 records mock fixtures
 * being read as production facts about a real user. A cost of 450 and a
 * plausible email are the shape a mistake takes; `mock-alice@example.invalid`
 * and a 999-ticket balance are not.
 *
 * These are exported as CONSTANTS and mutated in place by the mock branch of
 * `gachaMutations.ts` — the same posture `MOCK_INVENTORY_GRANTS` takes. A
 * granted ticket SHOULD evaporate when the dev server restarts: it is a
 * fixture, not state worth surviving a reload.
 */

export const MOCK_GACHA_ENABLED = { value: true };

export const MOCK_GACHA_PULLS: GachaPullRow[] = [
  {
    id: "00000000-mock-pull-0001",
    userId: "11111111-1111-1111-1111-111111111111",
    userEmail: "mock-alice@example.invalid",
    bannerId: "banner_standard_club1",
    poolId: "pool_standard_club1",
    pullCount: 10,
    ticketType: 0,
    cost: 450,
    pityBefore: 47,
    pityAfter: 0,
    pityForced: true,
    guaranteeForced: false,
    build: 9999,
    createdAt: "2026-09-01T10:12:00.000Z",
    prizes: [
      { slot: 0, kind: "club", refId: "club_driver_gf", quantity: 1, rarity: "Common", isDupe: true, dupeRp: 20, grantId: null, refName: "Driver G&F" },
      { slot: 1, kind: "ball", refId: "ball_golfin", quantity: 3, rarity: "Common", isDupe: false, dupeRp: 0, grantId: "g-mock-1", refName: "Golfin Ball" },
      { slot: 2, kind: "item", refId: "repairkit_common", quantity: 1, rarity: "Common", isDupe: false, dupeRp: 0, grantId: "g-mock-2", refName: "Repair Kit" },
      { slot: 3, kind: "club", refId: "club_wood_gf", quantity: 1, rarity: "Common", isDupe: false, dupeRp: 0, grantId: "g-mock-3", refName: "Wood G&F" },
      { slot: 4, kind: "club", refId: "club_iron9_klyro", quantity: 1, rarity: "Uncommon", isDupe: false, dupeRp: 0, grantId: "g-mock-4", refName: "Iron 9 Klyro" },
      { slot: 5, kind: "club", refId: "club_driver_gf", quantity: 1, rarity: "Common", isDupe: true, dupeRp: 20, grantId: null, refName: "Driver G&F" },
      { slot: 6, kind: "item", refId: "repairkit_rare", quantity: 1, rarity: "Rare", isDupe: false, dupeRp: 0, grantId: "g-mock-5", refName: "Repair Kit (Rare)" },
      { slot: 7, kind: "ball", refId: "ball_golfin", quantity: 3, rarity: "Common", isDupe: false, dupeRp: 0, grantId: "g-mock-6", refName: "Golfin Ball" },
      { slot: 8, kind: "club", refId: "club_pwedge_royal", quantity: 1, rarity: "Legendary", isDupe: false, dupeRp: 0, grantId: "g-mock-7", refName: "P.Wedge Royal" },
      { slot: 9, kind: "club", refId: "club_wood_gf", quantity: 1, rarity: "Common", isDupe: true, dupeRp: 20, grantId: null, refName: "Wood G&F" },
    ],
  },
  {
    id: "00000000-mock-pull-0002",
    userId: "22222222-2222-2222-2222-222222222222",
    userEmail: "mock-ken@example.invalid",
    bannerId: "banner_test_b",
    poolId: "pool_standard_club1",
    pullCount: 1,
    ticketType: 0,
    cost: 75,
    pityBefore: 3,
    pityAfter: 4,
    pityForced: false,
    guaranteeForced: false,
    build: 9999,
    createdAt: "2026-09-01T09:40:00.000Z",
    prizes: [
      { slot: 0, kind: "ticket", refId: "1", quantity: 2, rarity: "Rare", isDupe: false, dupeRp: 0, grantId: null, refName: "Gold Ticket" },
    ],
  },
];

export const MOCK_TICKET_BALANCES: TicketBalanceRow[] = [
  { ticketType: 0, label: "Ticket", balance: 999, updatedAt: "2026-09-01T10:12:00.000Z" },
  { ticketType: 1, label: "Gold Ticket", balance: 2, updatedAt: "2026-09-01T09:40:00.000Z" },
];

export const MOCK_TICKET_TRANSACTIONS: TicketTransactionRow[] = [
  {
    id: "00000000-mock-tx-0001",
    ticketType: 0,
    delta: -450,
    balanceAfter: 999,
    reason: "gacha:banner_standard_club1:x10",
    createdBy: null,
    createdAt: "2026-09-01T10:12:00.000Z",
  },
  {
    id: "00000000-mock-tx-0002",
    ticketType: 0,
    delta: 1000,
    balanceAfter: 1449,
    reason: "admin_grant",
    createdBy: "mock-admin@example.invalid",
    createdAt: "2026-09-01T10:00:00.000Z",
  },
];

export const MOCK_PLAYER_PITY: PlayerPityRow[] = [
  {
    bannerId: "banner_standard_club1",
    counter: 0,
    totalPulls: 40,
    threshold: 50,
    minRarity: "Legendary",
    pullLimit: null,
    updatedAt: "2026-09-01T10:12:00.000Z",
  },
  {
    bannerId: "banner_test_a",
    counter: 0,
    totalPulls: 12,
    threshold: null,
    minRarity: null,
    pullLimit: null,
    updatedAt: "2026-08-31T18:00:00.000Z",
  },
];
