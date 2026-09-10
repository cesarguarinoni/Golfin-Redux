import { MOCK_USERS } from "./mock";
import type { LoanAdminRow, LoanEventDto, LoanRules } from "./types";

/**
 * Mock fixtures for the Loans panel, the drawer's Loans tab and the Telemetry
 * lifecycle card (loans_ops §3.1, §4.5 mock twin).
 *
 * ⚠️ DELIBERATELY ABSURD NUMBERS — 999 RP to a lender on a three-day loan —
 * for the reason `lib/mockGacha.ts` gives: ADMIN_DASHBOARD_OPS §3.5 records
 * mock fixtures being read as production facts. The parties are the Users
 * fixture's people so a name click lands on a drawer that exists.
 *
 * Timestamps are RELATIVE TO LOAD TIME rather than to the telemetry fixture's
 * frozen MOCK_NOW: the panel shows "ends in 2 d" against the real clock, and a
 * fixture pinned to 2026-08-18 would render every active loan as long expired.
 * The telemetry mock branch therefore returns every fixture regardless of the
 * range it is asked for — illustrative, and documented as such there.
 *
 * Mutated in place by `loanMutations.ts`'s mock branch — a forced return
 * SHOULD evaporate on dev-server restart — and held on `globalThis` for the
 * reason `lib/mockStore.ts` gives: in dev the panel's GET and its POST are
 * different route bundles with their own module instances, so a module-level
 * array mutated by `/api/loans/:id/actions` is NOT the array
 * `/api/users/:id/loans` reads. (Found on first use: the drawer still showed
 * the loan the panel had just force-returned as ACTIVE.)
 */

const NOW = Date.now();
const h = (hours: number) => new Date(NOW + hours * 3_600_000).toISOString();

function user(i: number): { id: string; name: string | null } {
  const u = MOCK_USERS[i];
  if (!u) throw new Error(`mockLoans: MOCK_USERS[${i}] missing`);
  return { id: u.id, name: u.displayName };
}

const KEN = user(0);
const APPLE = user(1);
const WWTEST = user(2);
const CRATILO = user(4);

function row(
  id: string,
  lender: { id: string; name: string | null },
  borrower: { id: string; name: string | null },
  over: Partial<LoanAdminRow>
): LoanAdminRow {
  return {
    id,
    lenderId: lender.id,
    lenderName: lender.name,
    borrowerId: borrower.id,
    borrowerName: borrower.name,
    kind: "character",
    refId: "char_kai",
    days: 3,
    status: "active",
    offeredAt: h(-30),
    offerExpiresAt: h(18),
    answeredAt: h(-28),
    startsAt: h(-28),
    endsAt: h(44),
    endedAt: null,
    levelAtStart: 42,
    levelAtEnd: null,
    lenderShareBp: 2000,
    rpToLender: 0,
    rpToBorrower: 0,
    createdAt: h(-30),
    ...over,
  };
}

const SEED_LOANS: LoanAdminRow[] = [
  // Live right now: KEN lent Kai to the reviewer a day ago, 2 days left.
  row("00000000-mock-loan-0001", KEN, APPLE, {
    rpToLender: 999,
    rpToBorrower: 3996,
  }),
  // Pending: WWtest offered a club to KEN, 40 h left on the offer clock.
  row("00000000-mock-loan-0002", WWTEST, KEN, {
    kind: "club",
    refId: "club_driver_klyro",
    days: 7,
    status: "offered",
    offeredAt: h(-8),
    offerExpiresAt: h(40),
    answeredAt: null,
    startsAt: null,
    endsAt: null,
    levelAtStart: 12,
  }),
  // Returned EARLY: a one-day loan given back after 5 hours, levelled once.
  row("00000000-mock-loan-0003", KEN, APPLE, {
    refId: "char_mia",
    days: 1,
    status: "returned",
    offeredAt: h(-60),
    offerExpiresAt: h(-12),
    answeredAt: h(-58),
    startsAt: h(-58),
    endsAt: h(-34),
    endedAt: h(-53),
    levelAtStart: 20,
    levelAtEnd: 21,
    rpToLender: 99,
    rpToBorrower: 396,
    createdAt: h(-60),
  }),
  // Declined after 3 h — the pair is in cooldown for 21 more hours.
  row("00000000-mock-loan-0004", APPLE, CRATILO, {
    kind: "club",
    refId: "club_putter_klyro",
    status: "declined",
    offeredAt: h(-6),
    offerExpiresAt: h(42),
    answeredAt: h(-3),
    startsAt: null,
    endsAt: null,
    levelAtStart: 5,
    createdAt: h(-6),
  }),
  // Ran to term: a seven-day loan that expired lazily two days ago.
  row("00000000-mock-loan-0005", CRATILO, KEN, {
    refId: "char_rin",
    days: 7,
    status: "expired",
    offeredAt: h(-220),
    offerExpiresAt: h(-172),
    answeredAt: h(-218),
    startsAt: h(-218),
    endsAt: h(-50),
    endedAt: h(-50),
    levelAtStart: 33,
    levelAtEnd: 36,
    rpToLender: 9999,
    rpToBorrower: 39996,
    createdAt: h(-220),
  }),
  // Rescinded by the lender 10 h after offering.
  row("00000000-mock-loan-0006", KEN, WWTEST, {
    refId: "char_kai",
    status: "rescinded",
    offeredAt: h(-14),
    offerExpiresAt: h(34),
    answeredAt: h(-4),
    startsAt: null,
    endsAt: null,
    createdAt: h(-14),
  }),
  // An offer nobody answered: lapsed at the 48 h mark.
  row("00000000-mock-loan-0007", WWTEST, APPLE, {
    kind: "club",
    refId: "club_wood3_klyro",
    days: 3,
    status: "offer_expired",
    offeredAt: h(-100),
    offerExpiresAt: h(-52),
    answeredAt: h(-52),
    startsAt: null,
    endsAt: null,
    levelAtStart: 9,
    createdAt: h(-100),
  }),
];

let seq = 0;
function ev(
  loanId: string,
  at: string,
  from: string | null,
  to: string,
  actor: string,
  extra: Partial<LoanEventDto> = {}
): LoanEventDto {
  seq += 1;
  return { id: seq, at, fromStatus: from, toStatus: to, actor, adminEmail: null, note: null, ...extra };
}

/** loan id → its timeline, as the trigger would have written it. */
const SEED_EVENTS: Record<string, LoanEventDto[]> = {
  "00000000-mock-loan-0001": [
    ev("00000000-mock-loan-0001", h(-30), null, "offered", "lender"),
    ev("00000000-mock-loan-0001", h(-28), "offered", "active", "borrower"),
  ],
  "00000000-mock-loan-0002": [ev("00000000-mock-loan-0002", h(-8), null, "offered", "lender")],
  "00000000-mock-loan-0003": [
    ev("00000000-mock-loan-0003", h(-60), null, "offered", "lender"),
    ev("00000000-mock-loan-0003", h(-58), "offered", "active", "borrower"),
    ev("00000000-mock-loan-0003", h(-53), "active", "returned", "borrower"),
  ],
  "00000000-mock-loan-0004": [
    ev("00000000-mock-loan-0004", h(-6), null, "offered", "lender"),
    ev("00000000-mock-loan-0004", h(-3), "offered", "declined", "borrower"),
  ],
  "00000000-mock-loan-0005": [
    ev("00000000-mock-loan-0005", h(-220), null, "offered", "lender"),
    ev("00000000-mock-loan-0005", h(-218), "offered", "active", "borrower"),
    ev("00000000-mock-loan-0005", h(-48), "active", "expired", "system"),
  ],
  "00000000-mock-loan-0006": [
    ev("00000000-mock-loan-0006", h(-14), null, "offered", "lender"),
    ev("00000000-mock-loan-0006", h(-4), "offered", "rescinded", "lender"),
  ],
  "00000000-mock-loan-0007": [
    ev("00000000-mock-loan-0007", h(-100), null, "offered", "lender"),
    ev("00000000-mock-loan-0007", h(-51), "offered", "offer_expired", "system"),
  ],
};

/** `profiles.golfin_loan_offers` per fixture user. Absent = true (the column default). */
const SEED_OFFERS_ENABLED: Record<string, boolean> = {
  [CRATILO.id]: false,
};

/** What `GET /api/v1/loans/rules` answers today — the router's constants. */
export const MOCK_LOAN_RULES: LoanRules = {
  lenderShareBp: 2000,
  allowedDays: [1, 3, 7],
  maxLoansOut: 3,
  maxLoansIn: 3,
  maxPendingIn: 3,
  offerTtlHours: 48,
  reofferCooldownHours: 24,
  endedWindowDays: 14,
};

export interface MockLoansDb {
  loans: LoanAdminRow[];
  /** loan id → timeline. */
  events: Record<string, LoanEventDto[]>;
  /** user id → `profiles.golfin_loan_offers`. Absent = true. */
  offersEnabled: Record<string, boolean>;
}

const g = globalThis as unknown as { __golfinMockLoans?: MockLoansDb };

/** The one mutable copy every route bundle shares. */
export function mockLoansDb(): MockLoansDb {
  if (!g.__golfinMockLoans) {
    g.__golfinMockLoans = {
      loans: structuredClone(SEED_LOANS),
      events: structuredClone(SEED_EVENTS),
      offersEnabled: structuredClone(SEED_OFFERS_ENABLED),
    };
  }
  return g.__golfinMockLoans;
}
