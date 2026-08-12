import type {
  ActivityRow,
  AdminUserRow,
  GamePointAction,
  PointsTransaction,
  UserDetailResponse,
} from "./types";

/**
 * Mock data layer — fixtures mirroring the live PLAYLIFE Supabase project
 * (5 real users observed 2026-08-12). Used whenever the app runs in mock mode.
 */

const U = {
  ken: "5f0b7c2e-1a44-4b3a-9c1d-0a6e3f8b2101",
  appleReview: "7d21a9c4-3e55-4f60-8b2a-c91d4e7f2202",
  wwtest: "1c83e5f6-9b77-42d1-a4e8-2f5a6d9c2303",
  apple: "9a45b1d8-6c22-4e93-b7f0-8d3c1e5a2404",
  cratilo: "3e67d2a0-4f88-41c5-96b3-7a1f8c4d2505",
} as const;

export const MOCK_USERS: AdminUserRow[] = [
  {
    id: U.ken,
    email: "greedisland.k.k@gmail.com",
    displayName: "ken",
    providers: ["google"],
    createdAt: "2026-04-09T02:14:33.000Z",
    lastSignInAt: "2026-08-10T11:42:07.000Z",
    emailConfirmedAt: "2026-04-09T02:14:35.000Z",
    bannedUntil: null,
    activityPts: 580,
    giftPts: 100,
    totalPoints: 680,
    avatarLevel: 4,
    avatarXp: 320,
    followersCount: 2,
    followingCount: 3,
    badgesCount: 5,
    trustLevel: 1,
  },
  {
    id: U.appleReview,
    email: "apple.review@wonderwall-g.com",
    displayName: "Apple Reviewer",
    providers: ["email"],
    createdAt: "2026-04-20T08:03:11.000Z",
    lastSignInAt: "2026-05-02T09:15:44.000Z",
    emailConfirmedAt: "2026-04-20T08:04:02.000Z",
    bannedUntil: null,
    activityPts: 425,
    giftPts: 50,
    totalPoints: 475,
    avatarLevel: 3,
    avatarXp: 180,
    followersCount: 0,
    followingCount: 0,
    badgesCount: 2,
    trustLevel: null,
  },
  {
    id: U.wwtest,
    email: "cesar.guarinoni@wonderwall-g.com",
    displayName: "WWtest",
    providers: ["google"],
    createdAt: "2026-08-11T05:22:48.000Z",
    lastSignInAt: "2026-08-12T01:37:19.000Z",
    emailConfirmedAt: "2026-08-11T05:22:50.000Z",
    bannedUntil: null,
    activityPts: 0,
    giftPts: 0,
    totalPoints: 0,
    avatarLevel: 1,
    avatarXp: 0,
    followersCount: 0,
    followingCount: 0,
    badgesCount: 0,
    trustLevel: null,
  },
  {
    id: U.apple,
    email: "cesar@clumsydwarf.com",
    displayName: "Apple",
    providers: ["apple"],
    createdAt: "2026-08-12T03:10:05.000Z",
    lastSignInAt: "2026-08-12T03:10:05.000Z",
    emailConfirmedAt: "2026-08-12T03:10:07.000Z",
    bannedUntil: null,
    activityPts: 0,
    giftPts: 0,
    totalPoints: 0,
    avatarLevel: 1,
    avatarXp: 0,
    followersCount: 0,
    followingCount: 0,
    badgesCount: 0,
    trustLevel: null,
  },
  {
    id: U.cratilo,
    email: "cesar.guarinoni@gmail.com",
    displayName: "Cratilo",
    providers: ["email"],
    createdAt: "2026-08-12T06:55:30.000Z",
    lastSignInAt: "2026-08-12T07:20:12.000Z",
    emailConfirmedAt: "2026-08-12T06:56:01.000Z",
    bannedUntil: null,
    activityPts: 123,
    giftPts: 0,
    totalPoints: 123,
    avatarLevel: 1,
    avatarXp: 40,
    followersCount: 0,
    followingCount: 0,
    badgesCount: 0,
    trustLevel: null,
  },
];

const MOCK_TRANSACTIONS: PointsTransaction[] = [
  // ken — plausible ledger
  {
    id: "b1a2f3e4-0001-4a01-9001-aaaaaaaa0001",
    userId: U.ken,
    type: "hole_complete",
    amount: 20,
    currency: "activity",
    description: "ホール1 クリア",
    createdAt: "2026-08-08T10:02:11.000Z",
    idempotencyKey: "0f1e2d3c-0001-4b01-8001-bbbbbbbb0001",
  },
  {
    id: "b1a2f3e4-0002-4a02-9002-aaaaaaaa0002",
    userId: U.ken,
    type: "screenshot",
    amount: 50,
    currency: "activity",
    description: "スクリーンショット投稿",
    createdAt: "2026-08-09T04:47:52.000Z",
    idempotencyKey: null,
  },
  {
    id: "b1a2f3e4-0003-4a03-9003-aaaaaaaa0003",
    userId: U.ken,
    type: "versus_win",
    amount: 20,
    currency: "activity",
    description: "対戦勝利ボーナス",
    createdAt: "2026-08-09T12:30:00.000Z",
    idempotencyKey: null,
  },
  {
    id: "b1a2f3e4-0004-4a04-9004-aaaaaaaa0004",
    userId: U.ken,
    type: "gift",
    amount: 100,
    currency: "gift",
    description: "スコア投稿ギフト",
    createdAt: "2026-08-10T02:18:36.000Z",
    idempotencyKey: "0f1e2d3c-0004-4b04-8004-bbbbbbbb0004",
  },
  {
    id: "b1a2f3e4-0005-4a05-9005-aaaaaaaa0005",
    userId: U.ken,
    type: "spend",
    amount: -30,
    currency: "activity",
    description: "アイテム交換",
    createdAt: "2026-08-10T09:05:14.000Z",
    idempotencyKey: null,
  },
  // Apple Reviewer
  {
    id: "b1a2f3e4-0006-4a06-9006-aaaaaaaa0006",
    userId: U.appleReview,
    type: "screenshot",
    amount: 50,
    currency: "activity",
    description: "スクリーンショット投稿",
    createdAt: "2026-04-21T07:12:30.000Z",
    idempotencyKey: null,
  },
  {
    id: "b1a2f3e4-0007-4a07-9007-aaaaaaaa0007",
    userId: U.appleReview,
    type: "hole_complete",
    amount: 20,
    currency: "activity",
    description: "ホール3 クリア",
    createdAt: "2026-04-22T03:40:19.000Z",
    idempotencyKey: null,
  },
  {
    id: "b1a2f3e4-0008-4a08-9008-aaaaaaaa0008",
    userId: U.appleReview,
    type: "gift",
    amount: 50,
    currency: "gift",
    description: "レビュー協力ギフト",
    createdAt: "2026-04-25T10:00:00.000Z",
    idempotencyKey: null,
  },
  // Cratilo — Slice 1 acceptance grant
  {
    id: "b1a2f3e4-0009-4a09-9009-aaaaaaaa0009",
    userId: U.cratilo,
    type: "manual_admin_grant",
    amount: 123,
    currency: "activity",
    description: "admin test grant (Slice 1 acceptance)",
    createdAt: "2026-08-12T07:05:45.000Z",
    idempotencyKey: "0f1e2d3c-0009-4b09-8009-bbbbbbbb0009",
  },
];

const MOCK_ACTIVITIES: ActivityRow[] = [
  {
    id: "c2b3a4d5-0001-4c01-a001-cccccccc0001",
    userId: U.ken,
    label: "GPS check-in — 東京ゴルフ倶楽部 (course geofence)",
    createdAt: "2026-08-09T04:31:08.000Z",
  },
  {
    id: "c2b3a4d5-0002-4c02-a002-cccccccc0002",
    userId: U.ken,
    label: "GPS check-in — 若洲ゴルフリンクス",
    createdAt: "2026-08-10T01:55:40.000Z",
  },
];

export const MOCK_CATALOG: GamePointAction[] = [
  { action: "hole_complete", pts: null, maxPerEvent: 20, dailyCap: 400, oncePerUser: false },
  { action: "hole_replay", pts: null, maxPerEvent: 5, dailyCap: 100, oncePerUser: false },
  { action: "versus_win", pts: 20, maxPerEvent: 20, dailyCap: 200, oncePerUser: false },
  { action: "tournament_prize", pts: null, maxPerEvent: 2000, dailyCap: null, oncePerUser: false },
];

export function getMockUsers(): AdminUserRow[] {
  return MOCK_USERS;
}

export function getMockUserDetail(userId: string): UserDetailResponse {
  return {
    transactions: MOCK_TRANSACTIONS.filter((t) => t.userId === userId).sort(
      (a, b) => b.createdAt.localeCompare(a.createdAt)
    ),
    activities: MOCK_ACTIVITIES.filter((a) => a.userId === userId).sort(
      (a, b) => (b.createdAt ?? "").localeCompare(a.createdAt ?? "")
    ),
  };
}
