/**
 * The loans view for the Telemetry panel — PURE aggregation (loans_ops §2).
 *
 * No React, no Supabase, no clock unless one is handed in. Two folds:
 *
 *   buildLoanFunnel     over `telemetry_events` rows — the seven `loan_*`
 *                       events the client already ships. This is the BEHAVIOUR
 *                       view: who opened the modal and never sent, who sent and
 *                       was turned down. It follows `telemetryGacha.ts` exactly,
 *                       and for the same reason: the server's rows have one row
 *                       per loan and structurally cannot see a modal that was
 *                       closed without an offer.
 *
 *   buildLoanLifecycle  over `golfin_loans` rows — the SERVER truth. Terminal
 *                       counts, what is pending and active right now, how long
 *                       an answer takes, the 1/3/7 mix, the RP split totals,
 *                       and who lends / borrows most.
 *
 * Every rate is `null` rather than 0 when its denominator is empty: "no data"
 * and "nobody converted" are different findings, and a dashboard that renders
 * the first as 0 % invents the second.
 *
 * ⚠️ LAZY EXPIRY. Nothing sweeps `golfin_loans`; `GET /loans` flips a past-
 * `ends_at` row to `expired` when a client reads it. `activeNow` / `pendingNow`
 * therefore apply the router's own predicates (`_is_live`, `_is_locked`) to the
 * timestamps rather than trusting the status column — an `active` row whose
 * clock ran out yesterday is NOT active now, whatever the column says.
 */

/** One `telemetry_events` row, narrowed to what this module reads. */
export interface LoanEventRow {
  name?: unknown;
  payload?: unknown;
  user_id?: unknown;
}

/** One `golfin_loans` row, narrowed to what this module reads. Snake case:
 *  these are the table's own columns, handed over untouched. */
export interface LoanRow {
  id?: unknown;
  lender_id?: unknown;
  borrower_id?: unknown;
  kind?: unknown;
  days?: unknown;
  status?: unknown;
  offered_at?: unknown;
  offer_expires_at?: unknown;
  answered_at?: unknown;
  starts_at?: unknown;
  ends_at?: unknown;
  ended_at?: unknown;
  created_at?: unknown;
  rp_to_lender?: unknown;
  rp_to_borrower?: unknown;
}

export interface LoanFunnelStage {
  id: "modalOpens" | "offersSent" | "answered" | "accepted" | "returned";
  count: number;
  /** Share of the FIRST stage, 0..1. 0 when the first stage is empty. */
  pct: number;
}

export interface LoanFunnel {
  /** `loan_modal_open` — the lend modal opened on a character or a club. */
  modalOpens: number;
  /** `loan_offer_sent`. */
  offersSent: number;
  /** `loan_offer_answered`, either answer. */
  answered: number;
  accepted: number;
  declined: number;
  /** `loan_offer_rescinded` — the lender took an offer back. */
  rescinded: number;
  /** `loan_return` — the borrower gave it back before the clock. */
  returns: number;
  earlyReturns: number;
  /** `loan_pill_open` — the Home pill was opened. */
  pillOpens: number;
  /** `loan_offers_setting` — the recipient switch, split by direction. */
  settingOn: number;
  settingOff: number;
  /** `loan_offer_sent.via` — "search" vs "followed". Answers "how many offers
   *  went to somebody the lender does not follow" without a followers join. */
  viaSearch: number;
  viaFollowed: number;

  /** sent ÷ modal opens. */
  sentRateOfOpens: number | null;
  /** accepted ÷ SENT — the SPEC's definition, so a rescinded or still-pending
   *  offer counts against the rate; an offer nobody answered is not a success.
   *
   *  ⚠️ NOT the same number as `LoanLifecycle.acceptRate`, which is accepted ÷
   *  (accepted + declined) over the server's rows. Both are correct for their
   *  own question and both render on the Loans section, so the names must not
   *  collide — a reader who conflates them sees one figure disagreeing with
   *  itself. Hence the `OfSent` suffix. */
  acceptRateOfSent: number | null;
  /** early returns ÷ accepted, over the CLIENT's events. */
  earlyReturnRateOfAccepted: number | null;
  /** search ÷ sent. */
  viaSearchRate: number | null;

  /** Over `loan_offer_sent`. Keys are the day counts as strings ("1","3","7"). */
  daysMix: Record<string, number>;
  /** Over `loan_offer_sent`. */
  kindMix: Record<string, number>;
  /** Distinct users who fired any loan_ event in range. */
  players: number;

  stages: LoanFunnelStage[];
}

export interface LoanPartyRow {
  userId: string;
  /** Filled in by the caller from the profiles directory; the fold does not
   *  know names. */
  displayName: string | null;
  count: number;
}

export interface LoanLifecycle {
  /** Rows in range. */
  total: number;
  /** Count per status column value — the SEVEN, plus anything unexpected. */
  byStatus: Record<string, number>;
  /** `offered` and the offer clock has not run out, at `now`. */
  pendingNow: number;
  /** `active` and `now < ends_at`, at `now`. */
  activeNow: number;
  /** Offers that were accepted at some point: active, returned or expired. */
  accepted: number;
  /** Offers that got an answer: accepted + declined. */
  answered: number;
  /** accepted ÷ (accepted + declined). */
  acceptRate: number | null;
  /** `returned` rows whose `ended_at` is before `ends_at`. */
  earlyReturns: number;
  /** early returns ÷ loans that reached a terminal end (returned + expired). */
  earlyReturnRate: number | null;
  /** Hours from `offered_at` to `answered_at` over answered rows; null when
   *  nothing in range was answered (or nothing carried both timestamps). */
  medianHoursToAnswer: number | null;
  daysMix: Record<string, number>;
  kindMix: Record<string, number>;
  rpToLender: number;
  rpToBorrower: number;
  /** Top 5 by loans in range, most first. Ties broken by id so the list is
   *  stable between renders. */
  topLenders: LoanPartyRow[];
  topBorrowers: LoanPartyRow[];
}

export const TOP_PARTIES = 5;

/**
 * The statuses whose `answered_at` is the RECIPIENT's answer, and therefore the
 * only ones `medianHoursToAnswer` may sample.
 *
 * `rescinded` is deliberately absent: its `answered_at` is the lender's, not an
 * answer. `offered` has none yet, and `offer_expired`'s is a 48 h TTL running
 * out, which is nobody answering.
 */
export const ANSWERED_STATUSES: ReadonlySet<string> = new Set([
  "active",
  "returned",
  "expired",
  "declined",
]);

function payloadOf(row: LoanEventRow): Record<string, unknown> {
  const raw = row.payload;
  if (raw && typeof raw === "object" && !Array.isArray(raw)) return raw as Record<string, unknown>;
  if (typeof raw === "string") {
    try {
      const parsed: unknown = JSON.parse(raw);
      if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) {
        return parsed as Record<string, unknown>;
      }
    } catch {
      /* not JSON — treat as no payload */
    }
  }
  return {};
}

function num(v: unknown): number | null {
  const n = typeof v === "string" ? Number(v) : v;
  return typeof n === "number" && Number.isFinite(n) ? n : null;
}

function str(v: unknown): string | null {
  return typeof v === "string" && v.length > 0 ? v : null;
}

function ms(v: unknown): number | null {
  const s = str(v);
  if (!s) return null;
  const t = Date.parse(s);
  return Number.isFinite(t) ? t : null;
}

function rate(numerator: number, denominator: number): number | null {
  return denominator > 0 ? numerator / denominator : null;
}

function median(xs: number[]): number | null {
  if (xs.length === 0) return null;
  const sorted = [...xs].sort((a, b) => a - b);
  const mid = Math.floor(sorted.length / 2);
  return sorted.length % 2 === 1
    ? (sorted[mid] as number)
    : ((sorted[mid - 1] as number) + (sorted[mid] as number)) / 2;
}

function bump(map: Record<string, number>, key: string): void {
  map[key] = (map[key] ?? 0) + 1;
}

/**
 * Fold the loan events of one range into the funnel card.
 *
 * Rows of other event names are ignored, so the caller can hand this the same
 * unfiltered scan every other section reads.
 */
export function buildLoanFunnel(rows: LoanEventRow[]): LoanFunnel {
  let modalOpens = 0;
  let offersSent = 0;
  let accepted = 0;
  let declined = 0;
  let rescinded = 0;
  let returns = 0;
  let earlyReturns = 0;
  let pillOpens = 0;
  let settingOn = 0;
  let settingOff = 0;
  let viaSearch = 0;
  let viaFollowed = 0;
  const daysMix: Record<string, number> = {};
  const kindMix: Record<string, number> = {};
  const players = new Set<string>();

  for (const row of rows) {
    const name = String(row.name ?? "");
    if (!name.startsWith("loan_")) continue;

    const p = payloadOf(row);
    const user = str(row.user_id);
    if (user) players.add(user);

    switch (name) {
      case "loan_modal_open":
        modalOpens += 1;
        break;

      case "loan_offer_sent": {
        offersSent += 1;
        const days = num(p.days);
        if (days !== null) bump(daysMix, String(days));
        const kind = str(p.kind);
        if (kind) bump(kindMix, kind);
        // Anything that is not "search" is the followed list — the client only
        // has the two, and an unnamed via must not vanish from the sum.
        if (str(p.via) === "search") viaSearch += 1;
        else viaFollowed += 1;
        break;
      }

      case "loan_offer_answered":
        if (str(p.answer) === "accept") accepted += 1;
        else declined += 1;
        break;

      case "loan_offer_rescinded":
        rescinded += 1;
        break;

      case "loan_return":
        returns += 1;
        if (p.early === true) earlyReturns += 1;
        break;

      case "loan_pill_open":
        pillOpens += 1;
        break;

      case "loan_offers_setting":
        if (p.on === true) settingOn += 1;
        else settingOff += 1;
        break;

      default:
        break;
    }
  }

  const answered = accepted + declined;
  const stageOf = (id: LoanFunnelStage["id"], count: number): LoanFunnelStage => ({
    id,
    count,
    pct: modalOpens > 0 ? count / modalOpens : 0,
  });

  return {
    modalOpens,
    offersSent,
    answered,
    accepted,
    declined,
    rescinded,
    returns,
    earlyReturns,
    pillOpens,
    settingOn,
    settingOff,
    viaSearch,
    viaFollowed,
    sentRateOfOpens: rate(offersSent, modalOpens),
    acceptRateOfSent: rate(accepted, offersSent),
    earlyReturnRateOfAccepted: rate(earlyReturns, accepted),
    viaSearchRate: rate(viaSearch, offersSent),
    daysMix,
    kindMix,
    players: players.size,
    stages: [
      stageOf("modalOpens", modalOpens),
      stageOf("offersSent", offersSent),
      stageOf("answered", answered),
      stageOf("accepted", accepted),
      stageOf("returned", returns),
    ],
  };
}

/** Router predicate `_is_live`: `status = 'active' and now() < ends_at`. */
export function isLoanLive(row: LoanRow, nowMs: number): boolean {
  if (String(row.status ?? "") !== "active") return false;
  const ends = ms(row.ends_at);
  return ends !== null && nowMs < ends;
}

/** Router predicate `_is_pending_offer`: `offered` and the offer clock has not
 *  run out. A missing `offer_expires_at` (a pre-offers row somehow still
 *  offered) is treated as pending — the router locks it, so should this. */
export function isLoanPending(row: LoanRow, nowMs: number): boolean {
  if (String(row.status ?? "") !== "offered") return false;
  const expires = ms(row.offer_expires_at);
  return expires === null || nowMs < expires;
}

/**
 * Which section of the Users drawer's Loans tab a row belongs in, from the
 * BORROWER's side. `null` means the player is not the borrower on it.
 *
 * ⚠️ TOTAL BY CONSTRUCTION, and it has to be. The first version listed the
 * three "went nowhere" statuses explicitly, which left a hole: an `offered`
 * row whose 48 h TTL has run out is not pending (so not in `offers`), not
 * accepted (so not in `in`), and its status is still literally `offered` (so
 * not in the went-nowhere list either). It appeared in NO section — and
 * because expiry is LAZY, that is not a rare state: the row keeps saying
 * `offered` until some client's `GET /loans` gets around to flipping it. It is
 * also the exact case the went-nowhere section was added for, so the hole was
 * in the one place it could do the most damage.
 *
 * A lapsed offer is therefore classified as went-nowhere on the strength of its
 * CLOCK rather than its column — which is what `_expire` will stamp it as
 * anyway, and the same "trust the timestamps, not the status" rule the
 * lifecycle card already follows.
 */
export function borrowerSection(
  row: LoanRow,
  borrowerId: string,
  nowMs: number
): "offers" | "in" | "wentNowhere" | null {
  if (str(row.borrower_id) !== borrowerId) return null;
  if (isLoanPending(row, nowMs)) return "offers";
  const status = String(row.status ?? "");
  if (status === "active" || status === "returned" || status === "expired") return "in";
  // Everything else this player was offered: declined, rescinded, lapsed — and
  // an `offered` row that is no longer pending, which is a lapse the server has
  // not swept yet.
  return "wentNowhere";
}

/**
 * The moment a loan belongs to, for ranging. `offered_at` is the natural
 * anchor; rows that predate the offers migration have none and fall back to
 * `starts_at`, then `created_at`. Null when the row carries none of the three,
 * which is a row the caller should not range at all.
 */
export function loanAnchorMs(row: LoanRow): number | null {
  return ms(row.offered_at) ?? ms(row.starts_at) ?? ms(row.created_at);
}

/**
 * Fold the loans of one range into the lifecycle card.
 *
 * `nowMs` is a parameter rather than `Date.now()` so the fold is deterministic
 * under test and so the mock fixture can be evaluated at its own frozen clock.
 */
export function buildLoanLifecycle(rows: LoanRow[], nowMs: number): LoanLifecycle {
  const byStatus: Record<string, number> = {};
  const daysMix: Record<string, number> = {};
  const kindMix: Record<string, number> = {};
  const lenders = new Map<string, number>();
  const borrowers = new Map<string, number>();
  const hoursToAnswer: number[] = [];

  let pendingNow = 0;
  let activeNow = 0;
  let accepted = 0;
  let declined = 0;
  let ended = 0;
  let earlyReturns = 0;
  let rpToLender = 0;
  let rpToBorrower = 0;

  for (const row of rows) {
    const status = str(row.status) ?? "unknown";
    bump(byStatus, status);

    const days = num(row.days);
    if (days !== null) bump(daysMix, String(days));
    const kind = str(row.kind);
    if (kind) bump(kindMix, kind);

    const lender = str(row.lender_id);
    if (lender) lenders.set(lender, (lenders.get(lender) ?? 0) + 1);
    const borrower = str(row.borrower_id);
    if (borrower) borrowers.set(borrower, (borrowers.get(borrower) ?? 0) + 1);

    if (isLoanPending(row, nowMs)) pendingNow += 1;
    if (isLoanLive(row, nowMs)) activeNow += 1;

    // An offer that was accepted at some point is every status a loan can
    // only reach THROUGH accept. A pre-offers row (no offered_at) was never an
    // offer, but it was certainly accepted — it started active.
    if (status === "active" || status === "returned" || status === "expired") accepted += 1;
    if (status === "declined") declined += 1;

    if (status === "returned" || status === "expired") {
      ended += 1;
      if (status === "returned") {
        const endedAt = ms(row.ended_at);
        const endsAt = ms(row.ends_at);
        if (endedAt !== null && endsAt !== null && endedAt < endsAt) earlyReturns += 1;
      }
    }

    // Time-to-answer, over the statuses the RECIPIENT actually answered.
    //
    // ⚠️ AN INCLUSION LIST, NOT AN EXCLUSION ONE, and that is the whole fix.
    // The first version excluded `offered` and `offer_expired` by name and let
    // everything else through — which quietly counted `rescinded`. But
    // `answered_at` on a rescinded row is when the LENDER pulled the offer
    // back (`routers/loans.py::_terminal_answer` stamps it for decline AND
    // rescind; so does `golfin_loan_admin`'s `cancel_offer`), and the
    // recipient never answered at all. A card labelled "median time to answer"
    // was averaging in the lender's change of mind.
    //
    // The four that ARE answers: `declined` is the recipient saying no, and
    // `active` / `returned` / `expired` are the three states a loan can only
    // reach THROUGH their accept.
    if (ANSWERED_STATUSES.has(status)) {
      const offered = ms(row.offered_at);
      const answeredAt = ms(row.answered_at);
      // `answered_at < offered_at` is not a clock bug — it is what
      // `clear_cooldown` deliberately leaves behind, having pushed the pair's
      // declined rows 30 days into the past. Dropping the sample is right:
      // the row's answer moment is gone, and a negative would poison a median.
      if (offered !== null && answeredAt !== null && answeredAt >= offered) {
        hoursToAnswer.push((answeredAt - offered) / 3_600_000);
      }
    }

    rpToLender += num(row.rp_to_lender) ?? 0;
    rpToBorrower += num(row.rp_to_borrower) ?? 0;
  }

  const top = (map: Map<string, number>): LoanPartyRow[] =>
    [...map.entries()]
      .sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]))
      .slice(0, TOP_PARTIES)
      .map(([userId, count]) => ({ userId, displayName: null, count }));

  return {
    total: rows.length,
    byStatus,
    pendingNow,
    activeNow,
    accepted,
    answered: accepted + declined,
    acceptRate: rate(accepted, accepted + declined),
    earlyReturns,
    earlyReturnRate: rate(earlyReturns, ended),
    medianHoursToAnswer: median(hoursToAnswer),
    daysMix,
    kindMix,
    rpToLender,
    rpToBorrower,
    topLenders: top(lenders),
    topBorrowers: top(borrowers),
  };
}
