import "server-only";
import { isMockMode } from "./mode";
import { MOCK_LOAN_RULES, mockLoansDb } from "./mockLoans";
import { getSupabaseAdmin } from "./supabaseAdmin";
import {
  buildLoanLifecycle,
  isLoanPending,
  loanAnchorMs,
  type LoanLifecycle,
  type LoanRow,
} from "./telemetryLoans";
import type {
  LoanAdminRow,
  LoanDetailResponse,
  LoanEventDto,
  LoanFilters,
  LoanRules,
  LoanRulesResponse,
  LoansResponse,
  TelemetryRange,
  UserLoansResponse,
} from "./types";

/**
 * Reading the LIVE loan tables (loans_ops §3.1).
 *
 * `golfin_loans` is written by routers/loans.py and by `golfin_loan_admin`;
 * `golfin_loan_events` by the status trigger and the same function. Nothing
 * here is published or versioned — this is the Gacha panel's kind of panel,
 * not a catalog. EVERYTHING HERE IS SERVER TRUTH: the absence of the
 * Inventory tab's red notice is deliberate.
 *
 * ⚠️ LAZY EXPIRY. Nothing sweeps `golfin_loans`; `GET /loans` on the API flips
 * a past-clock row when a CLIENT reads it. This module must not assume a cron
 * did — an `offered` row past `offer_expires_at` is shown as offered (the
 * panel's subtitle says so) and only `pendingNow` / `activeNow` on the
 * telemetry card apply the router's predicates to the timestamps.
 *
 * TOLERATES `golfin_loan_events` NOT EXISTING, on purpose: between deploying
 * this and applying `2026_09_10_golfin_loan_events.sql` every timeline read
 * 404s. The loan row still shows; the timeline names the migration.
 */

type Row = Record<string, unknown>;

export const LOAN_EVENTS_MIGRATION = "2026_09_10_golfin_loan_events.sql";

/** PostgREST's undefined-table shapes, as lib/gachaData.ts reads them. */
function isMissingRelation(message: string): boolean {
  const text = message.toLowerCase();
  return (
    text.includes("42p01") ||
    text.includes("does not exist") ||
    text.includes("could not find the table")
  );
}

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function num(v: unknown, fallback = 0): number {
  const n = typeof v === "string" ? Number(v) : v;
  return typeof n === "number" && Number.isFinite(n) ? n : fallback;
}

function numOrNull(v: unknown): number | null {
  const n = typeof v === "string" ? Number(v) : v;
  return typeof n === "number" && Number.isFinite(n) ? n : null;
}

function str(v: unknown): string | null {
  return typeof v === "string" && v.length > 0 ? v : null;
}

const PAGE = 50;

/** id → display_name for exactly the ids asked for. Never throws. */
async function profileNames(ids: Iterable<string>): Promise<Map<string, string | null>> {
  const out = new Map<string, string | null>();
  const wanted = [...new Set([...ids].filter(Boolean))];
  if (wanted.length === 0) return out;
  const res = await getSupabaseAdmin()
    .from("profiles")
    .select("id, display_name")
    .in("id", wanted);
  if (res.error) {
    console.warn("profiles(display_name) read failed:", res.error.message);
    return out;
  }
  for (const r of (res.data ?? []) as Row[]) out.set(String(r.id), str(r.display_name));
  return out;
}

function toLoan(r: Row, names: Map<string, string | null>): LoanAdminRow {
  const lenderId = String(r.lender_id ?? "");
  const borrowerId = String(r.borrower_id ?? "");
  return {
    id: String(r.id ?? ""),
    lenderId,
    lenderName: names.get(lenderId) ?? null,
    borrowerId,
    borrowerName: names.get(borrowerId) ?? null,
    kind: String(r.kind ?? ""),
    refId: String(r.ref_id ?? ""),
    days: num(r.days),
    status: String(r.status ?? ""),
    offeredAt: str(r.offered_at),
    offerExpiresAt: str(r.offer_expires_at),
    answeredAt: str(r.answered_at),
    startsAt: str(r.starts_at),
    endsAt: str(r.ends_at),
    endedAt: str(r.ended_at),
    levelAtStart: num(r.level_at_start),
    levelAtEnd: numOrNull(r.level_at_end),
    lenderShareBp: num(r.lender_share_bp, 2000),
    rpToLender: num(r.rp_to_lender),
    rpToBorrower: num(r.rp_to_borrower),
    createdAt: String(r.created_at ?? ""),
  };
}

async function withNames(rows: Row[]): Promise<LoanAdminRow[]> {
  const ids = new Set<string>();
  for (const r of rows) {
    ids.add(String(r.lender_id ?? ""));
    ids.add(String(r.borrower_id ?? ""));
  }
  const names = await profileNames(ids);
  return rows.map((r) => toLoan(r, names));
}

/** The moment the table sorts and ranges on: `offered_at`, falling back for
 *  pre-offers rows the way `loanAnchorMs` does. */
function anchorOf(loan: LoanAdminRow): string {
  return loan.offeredAt ?? loan.startsAt ?? loan.createdAt;
}

function newestFirst(a: LoanAdminRow, b: LoanAdminRow): number {
  return anchorOf(b).localeCompare(anchorOf(a)) || b.id.localeCompare(a.id);
}

// ---------------------------------------------------------------------------
// §3.1 fetchLoans — the panel's table
// ---------------------------------------------------------------------------

export async function fetchLoans(filters: LoanFilters = {}): Promise<LoansResponse> {
  const page = Math.max(0, Math.floor(filters.page ?? 0));
  const limit = Math.min(Math.max(filters.limit ?? PAGE, 1), 500);
  const q = filters.q?.trim().toLowerCase() ?? "";

  if (isMockMode()) {
    const rows = mockLoansDb().loans.filter((l) => {
      if (filters.status && l.status !== filters.status) return false;
      if (filters.kind && l.kind !== filters.kind) return false;
      if (filters.from && anchorOf(l) < filters.from) return false;
      if (filters.to && anchorOf(l) > filters.to) return false;
      if (q) {
        const hay = [l.id, l.refId, l.lenderName ?? "", l.borrowerName ?? ""].join(" ").toLowerCase();
        if (!hay.includes(q)) return false;
      }
      return true;
    }).sort(newestFirst);
    const slice = rows.slice(page * limit, page * limit + limit);
    return { loans: slice, page, hasMore: page * limit + limit < rows.length, mock: true };
  }

  const admin = getSupabaseAdmin();

  // The free-text filter is resolved to user ids FIRST, because `golfin_loans`
  // has no name — it has two user ids — and a partial display-name match is
  // what an operator actually types. A uuid matches the loan id or either
  // party; anything else also matches `ref_id`.
  const orClauses: string[] = [];
  if (q) {
    if (UUID_RE.test(q)) {
      orClauses.push(`id.eq.${q}`, `lender_id.eq.${q}`, `borrower_id.eq.${q}`);
    } else {
      const safe = q.replace(/[%_,()]/g, "");
      if (safe) orClauses.push(`ref_id.ilike.*${safe}*`);
      const people = await admin
        .from("profiles")
        .select("id")
        .ilike("display_name", `%${safe}%`)
        .limit(200);
      if (people.error) {
        console.warn("profiles(display_name ilike) read failed:", people.error.message);
      } else {
        const ids = ((people.data ?? []) as Row[]).map((r) => String(r.id));
        if (ids.length > 0) {
          orClauses.push(`lender_id.in.(${ids.join(",")})`, `borrower_id.in.(${ids.join(",")})`);
        }
      }
      // No clause at all means the text matched nobody and no ref — an empty
      // table, not the unfiltered one.
      if (orClauses.length === 0) return { loans: [], page, hasMore: false, mock: false };
    }
  }

  let query = admin
    .from("golfin_loans")
    .select("*")
    .order("created_at", { ascending: false })
    .range(page * limit, page * limit + limit - 1);

  if (filters.status) query = query.eq("status", filters.status);
  if (filters.kind) query = query.eq("kind", filters.kind);
  // Ranged on `created_at`: for an offers-era row it equals `offered_at` to
  // the millisecond (both default now() in the same insert), and it is the one
  // stamp a pre-offers row is guaranteed to carry. Ordering on it keeps the
  // page cursor and the filter on one column.
  if (filters.from) query = query.gte("created_at", filters.from);
  if (filters.to) query = query.lte("created_at", filters.to);
  if (orClauses.length > 0) query = query.or(orClauses.join(","));

  const res = await query;
  if (res.error) throw new Error(`golfin_loans read failed: ${res.error.message}`);

  const rows = (res.data ?? []) as Row[];
  const loans = await withNames(rows);
  return { loans, page, hasMore: rows.length === limit, mock: false };
}

// ---------------------------------------------------------------------------
// §3.1 fetchLoanDetail — one row + its timeline
// ---------------------------------------------------------------------------

function toEvent(r: Row): LoanEventDto {
  return {
    id: num(r.id),
    at: String(r.at ?? ""),
    fromStatus: str(r.from_status),
    toStatus: String(r.to_status ?? ""),
    actor: String(r.actor ?? ""),
    adminEmail: str(r.admin_email),
    note: str(r.note),
  };
}

/** The raw `golfin_loans` row, or null. Shared with the mutation module so the
 *  audit's `before` snapshot is read the same way the panel reads it. */
export async function fetchLoanRaw(loanId: string): Promise<Row | null> {
  if (isMockMode()) {
    const l = mockLoansDb().loans.find((x) => x.id === loanId);
    return l ? ({ ...l } as unknown as Row) : null;
  }
  const res = await getSupabaseAdmin().from("golfin_loans").select("*").eq("id", loanId).maybeSingle();
  if (res.error) throw new Error(`golfin_loans read failed: ${res.error.message}`);
  return (res.data as Row | null) ?? null;
}

export async function fetchLoanDetail(loanId: string): Promise<LoanDetailResponse | null> {
  if (isMockMode()) {
    const loan = mockLoansDb().loans.find((x) => x.id === loanId);
    if (!loan) return null;
    return { loan, events: [...(mockLoansDb().events[loanId] ?? [])], mock: true };
  }

  const raw = await fetchLoanRaw(loanId);
  if (!raw) return null;
  const [loan] = await withNames([raw]);
  if (!loan) return null;

  const ev = await getSupabaseAdmin()
    .from("golfin_loan_events")
    .select("*")
    .eq("loan_id", loanId)
    .order("at", { ascending: true })
    .order("id", { ascending: true });

  if (ev.error) {
    if (isMissingRelation(ev.error.message)) {
      return { loan, events: [], mock: false, notMigrated: LOAN_EVENTS_MIGRATION };
    }
    throw new Error(`golfin_loan_events read failed: ${ev.error.message}`);
  }
  return { loan, events: ((ev.data ?? []) as Row[]).map(toEvent), mock: false };
}

// ---------------------------------------------------------------------------
// §3.4 fetchUserLoans — the drawer tab's three lists + the switch
// ---------------------------------------------------------------------------

const ACCEPTED_STATUSES = ["active", "returned", "expired"];

/** Offered to them, never held by them. The complement of ACCEPTED_STATUSES and
 *  a live `offered`, so between the three lists no row a borrower is party to is
 *  invisible in the drawer. */
const WENT_NOWHERE_STATUSES = ["declined", "rescinded", "offer_expired"];

export async function fetchUserLoans(userId: string): Promise<UserLoansResponse> {
  const nowMs = Date.now();

  if (isMockMode()) {
    const mine = mockLoansDb().loans.filter((l) => l.lenderId === userId || l.borrowerId === userId).sort(
      newestFirst
    );
    return {
      out: mine.filter((l) => l.lenderId === userId),
      in: mine.filter((l) => l.borrowerId === userId && ACCEPTED_STATUSES.includes(l.status)),
      offers: mine.filter(
        (l) =>
          l.borrowerId === userId &&
          isLoanPending({ status: l.status, offer_expires_at: l.offerExpiresAt }, nowMs)
      ),
      wentNowhere: mine.filter(
        (l) => l.borrowerId === userId && WENT_NOWHERE_STATUSES.includes(l.status)
      ),
      offersEnabled: mockLoansDb().offersEnabled[userId] ?? true,
      mock: true,
    };
  }

  const admin = getSupabaseAdmin();
  const [rows, profile] = await Promise.all([
    admin
      .from("golfin_loans")
      .select("*")
      .or(`lender_id.eq.${userId},borrower_id.eq.${userId}`)
      .order("created_at", { ascending: false })
      .limit(500),
    admin.from("profiles").select("golfin_loan_offers").eq("id", userId).maybeSingle(),
  ]);
  if (rows.error) throw new Error(`golfin_loans read failed: ${rows.error.message}`);
  if (profile.error) {
    // A missing column must not take the tab down — the switch reads as ON,
    // which is the column's own default.
    console.warn("profiles(golfin_loan_offers) read failed:", profile.error.message);
  }

  const raw = (rows.data ?? []) as Row[];
  const loans = (await withNames(raw)).sort(newestFirst);
  const byId = new Map(raw.map((r) => [String(r.id), r]));

  return {
    out: loans.filter((l) => l.lenderId === userId),
    in: loans.filter((l) => l.borrowerId === userId && ACCEPTED_STATUSES.includes(l.status)),
    offers: loans.filter(
      (l) => l.borrowerId === userId && isLoanPending((byId.get(l.id) ?? {}) as LoanRow, nowMs)
    ),
    wentNowhere: loans.filter(
      (l) => l.borrowerId === userId && WENT_NOWHERE_STATUSES.includes(l.status)
    ),
    offersEnabled: (profile.data as { golfin_loan_offers?: unknown } | null)?.golfin_loan_offers !== false,
    mock: false,
  };
}

// ---------------------------------------------------------------------------
// §2 fetchLoanLifecycle — the telemetry card, over the rows in range
// ---------------------------------------------------------------------------

/** Fetch cap for the lifecycle fold. `golfin_loans` grows per OFFER, not per
 *  shot; a beta that produces more loans than this in one range has a bigger
 *  problem than a partial card. */
const LIFECYCLE_CAP = 5000;

export async function fetchLoanLifecycle(range: TelemetryRange): Promise<LoanLifecycle> {
  const nowMs = Date.now();

  if (isMockMode()) {
    // Every fixture, whatever the range: the fixtures are anchored to load
    // time (see mockLoans.ts) and the telemetry fixture to a frozen 2026-08-18,
    // and a card that was always empty in mock mode would prove nothing.
    const rows: LoanRow[] = mockLoansDb().loans.map((l) => ({
      id: l.id,
      lender_id: l.lenderId,
      borrower_id: l.borrowerId,
      kind: l.kind,
      days: l.days,
      status: l.status,
      offered_at: l.offeredAt,
      offer_expires_at: l.offerExpiresAt,
      answered_at: l.answeredAt,
      starts_at: l.startsAt,
      ends_at: l.endsAt,
      ended_at: l.endedAt,
      created_at: l.createdAt,
      rp_to_lender: l.rpToLender,
      rp_to_borrower: l.rpToBorrower,
    }));
    const life = buildLoanLifecycle(rows, nowMs);
    const names = new Map(mockLoansDb().loans.flatMap((l) => [
      [l.lenderId, l.lenderName] as const,
      [l.borrowerId, l.borrowerName] as const,
    ]));
    life.topLenders = life.topLenders.map((p) => ({ ...p, displayName: names.get(p.userId) ?? null }));
    life.topBorrowers = life.topBorrowers.map((p) => ({ ...p, displayName: names.get(p.userId) ?? null }));
    return life;
  }

  // Pulled on `created_at <= to` and ranged in TypeScript on the anchor
  // (`offered_at` → `starts_at` → `created_at`), because PostgREST cannot
  // express a three-way coalesce in a filter and the table is small.
  const res = await getSupabaseAdmin()
    .from("golfin_loans")
    .select(
      "id, lender_id, borrower_id, kind, days, status, offered_at, offer_expires_at, answered_at, starts_at, ends_at, ended_at, created_at, rp_to_lender, rp_to_borrower"
    )
    .lte("created_at", range.to)
    .order("created_at", { ascending: false })
    .limit(LIFECYCLE_CAP);

  if (res.error) {
    if (isMissingRelation(res.error.message)) return buildLoanLifecycle([], nowMs);
    throw new Error(`golfin_loans read failed: ${res.error.message}`);
  }

  const fromMs = Date.parse(range.from);
  const toMs = Date.parse(range.to);
  const rows = ((res.data ?? []) as LoanRow[]).filter((r) => {
    const at = loanAnchorMs(r);
    return at !== null && at >= fromMs && at <= toMs;
  });

  const life = buildLoanLifecycle(rows, nowMs);
  const names = await profileNames([
    ...life.topLenders.map((p) => p.userId),
    ...life.topBorrowers.map((p) => p.userId),
  ]);
  life.topLenders = life.topLenders.map((p) => ({ ...p, displayName: names.get(p.userId) ?? null }));
  life.topBorrowers = life.topBorrowers.map((p) => ({ ...p, displayName: names.get(p.userId) ?? null }));
  return life;
}

// ---------------------------------------------------------------------------
// §3.3 fetchLoanRules — the read-only Rules card, straight from the API
// ---------------------------------------------------------------------------

/**
 * A THIN PROXY to `GET /api/v1/loans/rules`, for the reason the missions
 * preview gives: there must be exactly one copy of these numbers, and it lives
 * in routers/loans.py. A TypeScript copy here would be the drift the card
 * exists to rule out. The endpoint is public (the same numbers are in every
 * player's lend modal), so no admin key is involved.
 *
 * `PLAYLIFE_API_URL` is a plain `vars` entry in wrangler.jsonc — a public URL,
 * not a secret — with the Fly hostname as the fallback so a fresh checkout
 * still reaches the API.
 */
const DEFAULT_API_URL = "https://playlife-api.fly.dev";

export async function fetchLoanRules(): Promise<LoanRulesResponse> {
  if (isMockMode()) return { rules: MOCK_LOAN_RULES, source: "mock" };

  const base = (process.env.PLAYLIFE_API_URL || DEFAULT_API_URL).replace(/\/$/, "");
  try {
    const res = await fetch(`${base}/api/v1/loans/rules`, { cache: "no-store" });
    const body = (await res.json().catch(() => null)) as { data?: Row; detail?: string } | null;
    if (!res.ok || !body?.data) {
      return {
        rules: null,
        source: base,
        unavailable: body?.detail ?? `HTTP ${res.status}`,
      };
    }
    const d = body.data;
    const rules: LoanRules = {
      lenderShareBp: num(d.lender_share_bp),
      allowedDays: Array.isArray(d.allowed_days) ? d.allowed_days.map((x) => num(x)) : [],
      maxLoansOut: num(d.max_loans_out),
      maxLoansIn: num(d.max_loans_in),
      maxPendingIn: num(d.max_pending_in),
      offerTtlHours: num(d.offer_ttl_hours),
      reofferCooldownHours: num(d.reoffer_cooldown_hours),
      endedWindowDays: num(d.ended_window_days),
    };
    return { rules, source: base };
  } catch (err) {
    return {
      rules: null,
      source: base,
      unavailable: err instanceof Error ? err.message : String(err),
    };
  }
}
