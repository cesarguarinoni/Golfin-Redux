import { describe, expect, it } from "vitest";

import {
  buildLoanFunnel,
  buildLoanLifecycle,
  isLoanLive,
  isLoanPending,
  loanAnchorMs,
  type LoanEventRow,
  type LoanRow,
} from "../telemetryLoans";
import { loansToCsv } from "../loansCsv";

// ── event fixtures (the seven client events, payload keys as shipped) ─────────

const open = (kind = "character", user = "u1"): LoanEventRow => ({
  name: "loan_modal_open",
  user_id: user,
  payload: { kind, ref_id: "char_kai" },
});
const sent = (days = 3, via = "followed", kind = "character", user = "u1"): LoanEventRow => ({
  name: "loan_offer_sent",
  user_id: user,
  payload: { kind, ref_id: "char_kai", days, via },
});
const answered = (answer: "accept" | "decline", user = "u2"): LoanEventRow => ({
  name: "loan_offer_answered",
  user_id: user,
  payload: { loan_id: "L1", answer },
});
const rescinded = (user = "u1"): LoanEventRow => ({
  name: "loan_offer_rescinded",
  user_id: user,
  payload: { loan_id: "L1" },
});
const ret = (early = true, user = "u2"): LoanEventRow => ({
  name: "loan_return",
  user_id: user,
  payload: { loan_id: "L1", early },
});
const pill = (user = "u2"): LoanEventRow => ({
  name: "loan_pill_open",
  user_id: user,
  payload: { pending: 1, kind: "character" },
});
const setting = (on: boolean, user = "u2"): LoanEventRow => ({
  name: "loan_offers_setting",
  user_id: user,
  payload: { on },
});

// ── loan row fixtures ─────────────────────────────────────────────────────────

const NOW = Date.parse("2026-09-10T12:00:00Z");
const h = (hours: number) => new Date(NOW + hours * 3_600_000).toISOString();

function loan(over: Partial<LoanRow> = {}): LoanRow {
  return {
    id: "L",
    lender_id: "alice",
    borrower_id: "bob",
    kind: "character",
    days: 3,
    status: "active",
    offered_at: h(-30),
    offer_expires_at: h(18),
    answered_at: h(-28),
    starts_at: h(-28),
    ends_at: h(44),
    ended_at: null,
    created_at: h(-30),
    rp_to_lender: 0,
    rp_to_borrower: 0,
    ...over,
  };
}

describe("buildLoanFunnel", () => {
  it("is all zeros and null rates on no rows", () => {
    const f = buildLoanFunnel([]);
    expect(f.modalOpens).toBe(0);
    expect(f.offersSent).toBe(0);
    expect(f.sentRate).toBeNull();
    expect(f.acceptRate).toBeNull();
    expect(f.earlyReturnRate).toBeNull();
    expect(f.viaSearchRate).toBeNull();
    expect(f.players).toBe(0);
    expect(f.stages.map((s) => s.count)).toEqual([0, 0, 0, 0, 0]);
    expect(f.stages.every((s) => s.pct === 0)).toBe(true);
  });

  it("counts one full lifecycle: open → sent → accepted → returned early", () => {
    const f = buildLoanFunnel([open(), sent(3, "search"), pill(), answered("accept"), ret(true)]);
    expect(f.modalOpens).toBe(1);
    expect(f.offersSent).toBe(1);
    expect(f.answered).toBe(1);
    expect(f.accepted).toBe(1);
    expect(f.declined).toBe(0);
    expect(f.returns).toBe(1);
    expect(f.earlyReturns).toBe(1);
    expect(f.pillOpens).toBe(1);
    expect(f.viaSearch).toBe(1);
    expect(f.viaFollowed).toBe(0);
    expect(f.sentRate).toBe(1);
    expect(f.acceptRate).toBe(1);
    expect(f.earlyReturnRate).toBe(1);
    expect(f.viaSearchRate).toBe(1);
    expect(f.daysMix).toEqual({ "3": 1 });
    expect(f.kindMix).toEqual({ character: 1 });
    expect(f.players).toBe(2);
    expect(f.stages.map((s) => [s.id, s.count, s.pct])).toEqual([
      ["modalOpens", 1, 1],
      ["offersSent", 1, 1],
      ["answered", 1, 1],
      ["accepted", 1, 1],
      ["returned", 1, 1],
    ]);
  });

  it("computes the SPEC's rates over a mixed set", () => {
    // 4 opens, 2 sent (one club for 7 days, followed), 1 accept, 1 decline,
    // 1 rescind, 1 late return, 1 pill, switch off then on.
    const rows = [
      open(),
      open(),
      open("club"),
      open(),
      sent(3, "search"),
      sent(7, "followed", "club"),
      answered("accept"),
      answered("decline", "u3"),
      rescinded(),
      ret(false),
      pill(),
      setting(false, "u3"),
      setting(true, "u3"),
      // Not a loan event — must be ignored.
      { name: "gacha_banner_view", payload: { banner_id: "b" } },
    ];
    const f = buildLoanFunnel(rows);
    expect(f.modalOpens).toBe(4);
    expect(f.offersSent).toBe(2);
    expect(f.sentRate).toBeCloseTo(0.5, 10);
    expect(f.answered).toBe(2);
    expect(f.accepted).toBe(1);
    expect(f.declined).toBe(1);
    // accept ÷ SENT, per SPEC §2 — not accept ÷ answered.
    expect(f.acceptRate).toBeCloseTo(0.5, 10);
    expect(f.rescinded).toBe(1);
    expect(f.returns).toBe(1);
    expect(f.earlyReturns).toBe(0);
    expect(f.earlyReturnRate).toBe(0);
    expect(f.viaSearchRate).toBeCloseTo(0.5, 10);
    expect(f.settingOn).toBe(1);
    expect(f.settingOff).toBe(1);
    expect(f.daysMix).toEqual({ "3": 1, "7": 1 });
    expect(f.kindMix).toEqual({ character: 1, club: 1 });
    expect(f.players).toBe(3);
    expect(f.stages[1]).toEqual({ id: "offersSent", count: 2, pct: 0.5 });
  });

  it("reads a stringified payload the way the gacha fold does", () => {
    const f = buildLoanFunnel([
      { name: "loan_offer_sent", user_id: "u1", payload: JSON.stringify({ days: 1, via: "search" }) },
    ]);
    expect(f.offersSent).toBe(1);
    expect(f.daysMix).toEqual({ "1": 1 });
    expect(f.viaSearch).toBe(1);
  });
});

describe("buildLoanLifecycle", () => {
  it("is empty and null on no rows", () => {
    const l = buildLoanLifecycle([], NOW);
    expect(l.total).toBe(0);
    expect(l.byStatus).toEqual({});
    expect(l.pendingNow).toBe(0);
    expect(l.activeNow).toBe(0);
    expect(l.acceptRate).toBeNull();
    expect(l.earlyReturnRate).toBeNull();
    expect(l.medianHoursToAnswer).toBeNull();
    expect(l.rpToLender).toBe(0);
    expect(l.topLenders).toEqual([]);
    expect(l.topBorrowers).toEqual([]);
  });

  it("folds one full lifecycle row (offered → accepted → returned early)", () => {
    const l = buildLoanLifecycle(
      [
        loan({
          status: "returned",
          ended_at: h(-2),
          rp_to_lender: 40,
          rp_to_borrower: 160,
        }),
      ],
      NOW
    );
    expect(l.total).toBe(1);
    expect(l.byStatus).toEqual({ returned: 1 });
    expect(l.accepted).toBe(1);
    expect(l.answered).toBe(1);
    expect(l.acceptRate).toBe(1);
    expect(l.earlyReturns).toBe(1);
    expect(l.earlyReturnRate).toBe(1);
    // answered_at − offered_at = 2 h.
    expect(l.medianHoursToAnswer).toBeCloseTo(2, 10);
    expect(l.rpToLender).toBe(40);
    expect(l.rpToBorrower).toBe(160);
    expect(l.daysMix).toEqual({ "3": 1 });
    expect(l.kindMix).toEqual({ character: 1 });
    expect(l.topLenders).toEqual([{ userId: "alice", displayName: null, count: 1 }]);
    expect(l.topBorrowers).toEqual([{ userId: "bob", displayName: null, count: 1 }]);
    expect(l.activeNow).toBe(0);
    expect(l.pendingNow).toBe(0);
  });

  it("applies the router's predicates at NOW rather than trusting the column", () => {
    const rows = [
      // Live: active and the clock has not run out.
      loan({ id: "live", status: "active", ends_at: h(10) }),
      // Column says active but ends_at passed — lazy expiry has not read it yet.
      loan({ id: "stale", status: "active", ends_at: h(-1) }),
      // Pending: offered, clock still running.
      loan({ id: "pending", status: "offered", answered_at: null, starts_at: null, ends_at: null, offer_expires_at: h(5) }),
      // Offered but past its TTL — not pending, whatever the column says.
      loan({ id: "lapsed", status: "offered", answered_at: null, starts_at: null, ends_at: null, offer_expires_at: h(-5) }),
    ];
    const l = buildLoanLifecycle(rows, NOW);
    expect(l.activeNow).toBe(1);
    expect(l.pendingNow).toBe(1);
    expect(l.byStatus).toEqual({ active: 2, offered: 2 });
    expect(isLoanLive(rows[1] as LoanRow, NOW)).toBe(false);
    expect(isLoanPending(rows[3] as LoanRow, NOW)).toBe(false);
  });

  it("mixes statuses: rates, median, days mix, top parties", () => {
    const rows: LoanRow[] = [
      loan({ id: "a", status: "returned", ended_at: h(-1), lender_id: "alice", borrower_id: "bob", answered_at: h(-29) }), // 1 h to answer, early
      loan({ id: "b", status: "expired", ended_at: h(-1), ends_at: h(-1), lender_id: "alice", borrower_id: "carol", answered_at: h(-27) }), // 3 h
      loan({ id: "c", status: "declined", starts_at: null, ends_at: null, lender_id: "alice", borrower_id: "dave", answered_at: h(-25), days: 7 }), // 5 h
      loan({ id: "d", status: "rescinded", starts_at: null, ends_at: null, lender_id: "erin", borrower_id: "bob", answered_at: h(-20), days: 1, kind: "club" }),
      loan({ id: "e", status: "offer_expired", starts_at: null, ends_at: null, lender_id: "erin", borrower_id: "bob", answered_at: h(-10), offer_expires_at: h(-10) }),
      loan({ id: "f", status: "active", lender_id: "frank", borrower_id: "bob", answered_at: h(-26), rp_to_lender: 10, rp_to_borrower: 40 }), // 4 h
    ];
    const l = buildLoanLifecycle(rows, NOW);
    expect(l.total).toBe(6);
    expect(l.byStatus).toEqual({
      returned: 1,
      expired: 1,
      declined: 1,
      rescinded: 1,
      offer_expired: 1,
      active: 1,
    });
    expect(l.accepted).toBe(3); // returned + expired + active
    expect(l.answered).toBe(4); // + declined
    expect(l.acceptRate).toBeCloseTo(0.75, 10);
    expect(l.earlyReturns).toBe(1);
    expect(l.earlyReturnRate).toBeCloseTo(0.5, 10); // 1 early of 2 ended
    // Answered rows carrying both stamps: a(1h) b(3h) c(5h) d(rescinded, 10h) f(4h)
    // → sorted 1,3,4,5,10 → median 4. offer_expired is excluded by name.
    expect(l.medianHoursToAnswer).toBeCloseTo(4, 10);
    expect(l.daysMix).toEqual({ "3": 4, "7": 1, "1": 1 });
    expect(l.kindMix).toEqual({ character: 5, club: 1 });
    expect(l.rpToLender).toBe(10);
    expect(l.rpToBorrower).toBe(40);
    expect(l.topLenders.map((p) => [p.userId, p.count])).toEqual([
      ["alice", 3],
      ["erin", 2],
      ["frank", 1],
    ]);
    expect(l.topBorrowers[0]).toEqual({ userId: "bob", displayName: null, count: 4 });
    expect(l.activeNow).toBe(1);
  });

  it("tolerates pre-offers rows with a null offered_at", () => {
    // A v1 loan: started active, no offer stamps at all.
    const legacy = loan({
      id: "legacy",
      status: "returned",
      offered_at: null,
      offer_expires_at: null,
      answered_at: null,
      starts_at: h(-50),
      ends_at: h(22),
      ended_at: h(-3),
    });
    const l = buildLoanLifecycle([legacy], NOW);
    expect(l.accepted).toBe(1);
    expect(l.earlyReturns).toBe(1);
    // No offered_at/answered_at pair → no time-to-answer sample, not a 0.
    expect(l.medianHoursToAnswer).toBeNull();
    expect(loanAnchorMs(legacy)).toBe(NOW - 50 * 3_600_000);
    expect(loanAnchorMs({ created_at: h(-1) })).toBe(NOW - 3_600_000);
    expect(loanAnchorMs({})).toBeNull();
  });

  it("caps the top lists at five, most-loans first", () => {
    const rows: LoanRow[] = [];
    for (let i = 0; i < 7; i += 1) {
      for (let n = 0; n <= i; n += 1) rows.push(loan({ id: `${i}-${n}`, lender_id: `lender${i}` }));
    }
    const l = buildLoanLifecycle(rows, NOW);
    expect(l.topLenders).toHaveLength(5);
    expect(l.topLenders[0]).toEqual({ userId: "lender6", displayName: null, count: 7 });
    expect(l.topLenders[4]?.userId).toBe("lender2");
  });
});

describe("loansToCsv", () => {
  it("writes a header and one quoted line per loan, escaping commas and quotes", () => {
    const csv = loansToCsv([
      {
        id: "L1",
        offeredAt: "2026-09-10T10:00:00Z",
        status: "active",
        kind: "club",
        refId: "club_driver_x",
        lenderId: "a",
        lenderName: 'Al "The Wall", Sr.',
        borrowerId: "b",
        borrowerName: null,
        days: 3,
        levelAtStart: 42,
        levelAtEnd: null,
        rpToLender: 10,
        rpToBorrower: 40,
        startsAt: "2026-09-10T11:00:00Z",
        endsAt: "2026-09-13T11:00:00Z",
        endedAt: null,
        offerExpiresAt: null,
        answeredAt: "2026-09-10T11:00:00Z",
        lenderShareBp: 2000,
        createdAt: "2026-09-10T10:00:00Z",
      },
    ]);
    const lines = csv.split("\n");
    expect(lines).toHaveLength(2);
    expect(lines[0]).toBe(
      "id,offered_at,status,kind,ref_id,lender_id,lender,borrower_id,borrower,days,level_at_start,level_at_end,rp_to_lender,rp_to_borrower,starts_at,ends_at,ended_at,offer_expires_at,answered_at,lender_share_bp"
    );
    expect(lines[1]).toContain('"Al ""The Wall"", Sr."');
    expect(lines[1]).toContain(",,");
    expect(lines[1]?.endsWith(",2000")).toBe(true);
  });

  it("is header-only on no rows", () => {
    expect(loansToCsv([]).split("\n")).toHaveLength(1);
  });
});
