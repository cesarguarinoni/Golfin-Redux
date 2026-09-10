import { describe, expect, it } from "vitest";

import {
  ALL_LOAN_STATUSES,
  ANSWERED_STATUSES,
  actionsFor,
  borrowerSection,
  clockLabel,
  isLoanLive,
  isLoanPending,
  loanAnchorMs,
  type LoanClockRow,
  type LoanRow,
} from "../loanStatus";

/**
 * The point of this file is EXHAUSTIVENESS. This task shipped four defects of
 * one shape — a status classified by an incomplete list — so every test that
 * can iterate `ALL_LOAN_STATUSES` does, rather than sampling the interesting
 * ones. A status added later fails here instead of quietly landing in a
 * `default` arm.
 */

const NOW = Date.parse("2026-09-10T12:00:00Z");
const h = (hours: number) => new Date(NOW + hours * 3_600_000).toISOString();

function row(over: Partial<LoanRow> = {}): LoanRow {
  return {
    borrower_id: "bob",
    lender_id: "alice",
    status: "active",
    offered_at: h(-30),
    offer_expires_at: h(18),
    answered_at: h(-28),
    starts_at: h(-28),
    ends_at: h(44),
    ended_at: null,
    created_at: h(-30),
    ...over,
  };
}

function clockRow(over: Partial<LoanClockRow> = {}): LoanClockRow {
  return {
    status: "active",
    offerExpiresAt: h(18),
    endsAt: h(44),
    endedAt: null,
    answeredAt: h(-28),
    ...over,
  };
}

describe("clockLabel", () => {
  it("gives every one of the seven statuses its own truthful label", () => {
    const got = Object.fromEntries(
      ALL_LOAN_STATUSES.map((status) => [
        status,
        clockLabel(clockRow({ status, endedAt: h(-2) }), NOW)?.key,
      ])
    );
    expect(got).toEqual({
      offered: "loans.offerExpires",
      active: "loans.ends",
      returned: "loans.ended",
      expired: "loans.ended",
      // The ONE status where answered_at really is the recipient answering.
      declined: "loans.answered",
      // The two the old `default` arm mislabelled as "answered".
      rescinded: "loans.rescindedAt",
      offer_expired: "loans.lapsedAt",
    });
  });

  it("never calls a rescind or a lapse an answer", () => {
    // The exact defect: `clockLine`'s default arm rendered "answered {rel}" for
    // both, on the panel whose job is explaining why an offer disappeared. A
    // rescind is the LENDER withdrawing; a lapse is a 48 h clock running out
    // unseen. `_terminal_answer` and `_expire` both stamp `answered_at`, which
    // is what made this easy to get wrong.
    for (const status of ["rescinded", "offer_expired"] as const) {
      expect(clockLabel(clockRow({ status }), NOW)?.key).not.toBe("loans.answered");
    }
    expect(clockLabel(clockRow({ status: "declined" }), NOW)?.key).toBe("loans.answered");
  });

  it("flips the offer and loan clocks on whether they have run out", () => {
    expect(clockLabel(clockRow({ status: "offered", offerExpiresAt: h(4) }), NOW)?.key)
      .toBe("loans.offerExpires");
    expect(clockLabel(clockRow({ status: "offered", offerExpiresAt: h(-4) }), NOW)?.key)
      .toBe("loans.offerExpiresPast");
    expect(clockLabel(clockRow({ status: "active", endsAt: h(4) }), NOW)?.key).toBe("loans.ends");
    expect(clockLabel(clockRow({ status: "active", endsAt: h(-4) }), NOW)?.key).toBe("loans.endsPast");
  });

  it("says only that something changed for a status it has never heard of", () => {
    // Never a specific claim about who acted. A future status must not inherit
    // "answered" the way rescinded and offer_expired did.
    expect(clockLabel(clockRow({ status: "repossessed" }), NOW)?.key).toBe("loans.changedAt");
  });

  it("returns null rather than a label with no timestamp behind it", () => {
    expect(clockLabel(clockRow({ status: "offered", offerExpiresAt: null }), NOW)).toBeNull();
    expect(clockLabel(clockRow({ status: "active", endsAt: null }), NOW)).toBeNull();
    expect(clockLabel(clockRow({ status: "returned", endedAt: null }), NOW)).toBeNull();
    expect(clockLabel(clockRow({ status: "declined", answeredAt: null }), NOW)).toBeNull();
  });

  it("points every label at the timestamp that label is about", () => {
    const r = clockRow({ status: "returned", endedAt: h(-2), answeredAt: h(-28) });
    expect(clockLabel(r, NOW)?.iso).toBe(h(-2)); // ended_at, not answered_at
    const d = clockRow({ status: "declined", endedAt: h(-2), answeredAt: h(-28) });
    expect(clockLabel(d, NOW)?.iso).toBe(h(-28)); // answered_at, not ended_at
  });
});

describe("actionsFor", () => {
  it("offers exactly the action each status can accept", () => {
    const got = Object.fromEntries(
      ALL_LOAN_STATUSES.map((status) => [
        status,
        actionsFor({ status, offerExpiresAt: h(18), endsAt: h(44) }, NOW),
      ])
    );
    expect(got).toEqual({
      offered: ["cancel_offer"],
      active: ["force_return"],
      returned: [],
      expired: [],
      declined: ["clear_cooldown"],
      rescinded: ["clear_cooldown"],
      offer_expired: [],
    });
  });

  it("withholds the action on a row whose clock has already run out", () => {
    // The server guards on the STATUS COLUMN, so `golfin_loan_admin` would
    // happily cancel a lapsed offer — and that starts a 24 h cooldown on a pair
    // whose offer had already died on its own. The operator is looking at a
    // clock, so the button is withheld rather than inviting that.
    expect(actionsFor({ status: "offered", offerExpiresAt: h(-1), endsAt: null }, NOW)).toEqual([]);
    expect(actionsFor({ status: "active", offerExpiresAt: null, endsAt: h(-1) }, NOW)).toEqual([]);
    // Still live an hour the other way — same row, same column, different clock.
    expect(actionsFor({ status: "offered", offerExpiresAt: h(1), endsAt: null }, NOW))
      .toEqual(["cancel_offer"]);
    expect(actionsFor({ status: "active", offerExpiresAt: null, endsAt: h(1) }, NOW))
      .toEqual(["force_return"]);
  });

  it("offers nothing for a status it has never heard of", () => {
    expect(actionsFor({ status: "repossessed", offerExpiresAt: null, endsAt: null }, NOW)).toEqual([]);
  });
});

describe("borrowerSection", () => {
  it("places every one of the seven statuses in exactly one section", () => {
    const placed = ALL_LOAN_STATUSES.map(
      (status) => [status, borrowerSection(row({ status }), "bob", NOW)] as const
    );
    expect(placed.every(([, s]) => s !== null)).toBe(true);
    expect(Object.fromEntries(placed)).toEqual({
      offered: "offers",
      active: "in",
      returned: "in",
      expired: "in",
      declined: "wentNowhere",
      rescinded: "wentNowhere",
      offer_expired: "wentNowhere",
    });
  });

  it("files a LAPSED but unswept offer under wentNowhere, not nowhere at all", () => {
    const lapsed = row({ status: "offered", offer_expires_at: h(-1) });
    expect(isLoanPending(lapsed, NOW)).toBe(false);
    expect(borrowerSection(lapsed, "bob", NOW)).toBe("wentNowhere");
    expect(borrowerSection(row({ status: "offered", offer_expires_at: h(1) }), "bob", NOW))
      .toBe("offers");
  });

  it("classifies an ACTIVE row past its clock as held, not as an offer", () => {
    const stale = row({ status: "active", ends_at: h(-1) });
    expect(isLoanLive(stale, NOW)).toBe(false);
    expect(borrowerSection(stale, "bob", NOW)).toBe("in");
  });

  it("returns null when the player is not the borrower", () => {
    expect(borrowerSection(row(), "alice", NOW)).toBeNull();
    expect(borrowerSection(row(), "bob", NOW)).toBe("in");
  });
});

describe("ANSWERED_STATUSES", () => {
  it("holds exactly the statuses whose answered_at is the recipient's", () => {
    expect([...ANSWERED_STATUSES].sort()).toEqual(["active", "declined", "expired", "returned"]);
    expect(ANSWERED_STATUSES.has("rescinded")).toBe(false);
    expect(ANSWERED_STATUSES.has("offer_expired")).toBe(false);
    expect(ANSWERED_STATUSES.has("offered")).toBe(false);
  });

  it("agrees with clockLabel about what an answer is", () => {
    // The two must not drift: a status the median samples is a status whose
    // clock line may say "answered", and vice versa — except for the three
    // accepted ones, whose line is about their own end instead.
    for (const status of ALL_LOAN_STATUSES) {
      const saysAnswered = clockLabel(clockRow({ status }), NOW)?.key === "loans.answered";
      if (saysAnswered) expect(ANSWERED_STATUSES.has(status)).toBe(true);
    }
  });
});

describe("isLoanLive / isLoanPending / loanAnchorMs", () => {
  it("reads the clocks rather than the status column", () => {
    expect(isLoanLive(row({ status: "active", ends_at: h(1) }), NOW)).toBe(true);
    expect(isLoanLive(row({ status: "active", ends_at: h(-1) }), NOW)).toBe(false);
    expect(isLoanLive(row({ status: "returned", ends_at: h(1) }), NOW)).toBe(false);
    expect(isLoanPending(row({ status: "offered", offer_expires_at: h(1) }), NOW)).toBe(true);
    expect(isLoanPending(row({ status: "offered", offer_expires_at: h(-1) }), NOW)).toBe(false);
    // A pre-offers row with no offer clock is treated as pending: the router
    // locks it, so the dashboard must not show it as free.
    expect(isLoanPending(row({ status: "offered", offer_expires_at: null }), NOW)).toBe(true);
  });

  it("falls back through offered_at → starts_at → created_at", () => {
    expect(loanAnchorMs(row({ offered_at: h(-5) }))).toBe(NOW - 5 * 3_600_000);
    expect(loanAnchorMs(row({ offered_at: null, starts_at: h(-6) }))).toBe(NOW - 6 * 3_600_000);
    expect(loanAnchorMs(row({ offered_at: null, starts_at: null, created_at: h(-7) })))
      .toBe(NOW - 7 * 3_600_000);
    expect(loanAnchorMs({})).toBeNull();
  });
});
