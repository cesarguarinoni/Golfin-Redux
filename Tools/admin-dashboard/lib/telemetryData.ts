import "server-only";
import { fetchUserDirectory, type UserIdentity } from "./data";
import { MOCK_NOW, MOCK_TELEMETRY_EVENTS, type MockEventRow } from "./mockTelemetry";
import { isMockMode } from "./mode";
import { getSupabaseAdmin } from "./supabaseAdmin";
import type {
  ClubStat,
  FunnelStage,
  FunnelStageId,
  HoleStat,
  ShotQuality,
  TelemetryEventRow,
  TelemetryEventsResponse,
  TelemetryKpis,
  TelemetryRange,
  TelemetrySummaryResponse,
  TelemetryTestersResponse,
  TesterRow,
} from "./types";

/**
 * Read side of the Telemetry panel. Branches mock ↔ live like lib/data.ts and
 * lib/tournamentData.ts, and mirrors tournamentData's naming (`fetchX`, local
 * `Row`, `num`/`str` coercers).
 *
 * WHY AGGREGATE IN TYPESCRIPT: 20 testers over one beta week is tens of
 * thousands of rows at worst. Fetching the window and reducing it here costs
 * one round trip and no schema surface — no views, no RPCs, no migration to
 * keep in step with the panel. If the tester count ever grows an order of
 * magnitude this is the thing to replace, and the 10k cap below is the tripwire
 * that says so out loud instead of quietly serving half an answer.
 *
 * EVERY payload key read here comes from beta_telemetry SPEC §1. Nothing is
 * invented; a field that is not in that table is not read.
 */

type Row = Record<string, unknown>;

/** Hard cap per aggregate read. Hitting it sets `truncated`, which the panel
 *  surfaces as a badge — a partial read must never look like a whole one. */
export const ROW_CAP = 10_000;
/** Event-explorer page size (SPEC §3.6). */
export const EVENT_PAGE_SIZE = 100;

const DAY_MS = 86_400_000;

function num(v: unknown): number | null {
  if (typeof v === "number" && Number.isFinite(v)) return v;
  return null;
}
function str(v: unknown): string | null {
  return typeof v === "string" && v.length > 0 ? v : null;
}
function payloadOf(r: Row): Row {
  const p = r.payload;
  return p && typeof p === "object" && !Array.isArray(p) ? (p as Row) : {};
}
/** payload.hole, tolerating the number arriving as a string. */
function holeOf(p: Row): number | null {
  const n = num(p.hole);
  if (n !== null) return Math.trunc(n);
  const s = str(p.hole);
  if (s === null) return null;
  const parsed = Number.parseInt(s, 10);
  return Number.isFinite(parsed) ? parsed : null;
}

function mean(xs: number[]): number | null {
  if (xs.length === 0) return null;
  return xs.reduce((a, b) => a + b, 0) / xs.length;
}
function median(xs: number[]): number | null {
  if (xs.length === 0) return null;
  const sorted = [...xs].sort((a, b) => a - b);
  const mid = sorted.length >> 1;
  const hi = sorted[mid] as number;
  if (sorted.length % 2 === 1) return hi;
  return ((sorted[mid - 1] as number) + hi) / 2;
}
/** Every rate in this file goes through here: a zero denominator is `null`
 *  ("nothing to divide"), never NaN and never a fabricated 0%. */
function rate(numerator: number, denominator: number): number | null {
  return denominator > 0 ? numerator / denominator : null;
}
function modeOf(xs: (string | null)[]): string | null {
  const counts = new Map<string, number>();
  for (const x of xs) {
    if (x === null) continue;
    counts.set(x, (counts.get(x) ?? 0) + 1);
  }
  let best: string | null = null;
  let bestN = 0;
  for (const [k, n] of counts) {
    if (n > bestN) {
      best = k;
      bestN = n;
    }
  }
  return best;
}

/**
 * Resolve the query window.
 *
 * Live default: the last 7 days. Mock default: the 7 days ending at the
 * fixture's frozen MOCK_NOW — otherwise the fixture would fall out of the
 * window the day after it was written and the panel would render empty, which
 * is exactly the failure mode a deterministic fixture exists to prevent.
 */
export function resolveRange(from?: string | null, to?: string | null): TelemetryRange {
  const end = parseBoundary(to, true) ?? (isMockMode() ? MOCK_NOW : new Date().toISOString());
  const start =
    parseBoundary(from, false) ?? new Date(Date.parse(end) - 7 * DAY_MS).toISOString();
  return { from: start, to: end };
}

/** Accepts `YYYY-MM-DD` (widened to the whole UTC day) or a full ISO stamp. */
function parseBoundary(v: string | null | undefined, isEnd: boolean): string | null {
  if (!v) return null;
  const iso = /^\d{4}-\d{2}-\d{2}$/.test(v)
    ? `${v}T${isEnd ? "23:59:59.999" : "00:00:00.000"}Z`
    : v;
  const ms = Date.parse(iso);
  return Number.isFinite(ms) ? new Date(ms).toISOString() : null;
}

/** Start of the UTC day the range ends on — the denominator-free "today" the
 *  KPI cards show. Tied to the range end, not the wall clock, so mock mode's
 *  "today" is the fixture's last day rather than a permanent zero. */
function todayStart(range: TelemetryRange): number {
  return Date.parse(`${range.to.slice(0, 10)}T00:00:00.000Z`);
}

// ---------------------------------------------------------------------------
// Row fetch
// ---------------------------------------------------------------------------

interface Scan {
  rows: Row[];
  truncated: boolean;
  mock: boolean;
  tableMissing: boolean;
}

/**
 * The panel may deploy before the beta_telemetry §2.2 migration is applied.
 * "The table is not there yet" is a known, expected state on day one and reads
 * as a clean zero everywhere; anything else is a real failure and still throws.
 */
function isMissingTable(error: { code?: string; message?: string }): boolean {
  if (error.code === "42P01" || error.code === "PGRST205") return true;
  const message = error.message ?? "";
  return /relation .* does not exist|could not find the table/i.test(message);
}

/**
 * Every row in the window, capped. Filters on `received_at` (server clock) —
 * `ts` is the tester's device clock and a phone with a wrong date would
 * otherwise vanish from, or leak into, the range.
 */
async function scanEvents(range: TelemetryRange): Promise<Scan> {
  if (isMockMode()) {
    const rows = (MOCK_TELEMETRY_EVENTS as MockEventRow[])
      .filter((r) => r.received_at >= range.from && r.received_at <= range.to)
      .slice(0, ROW_CAP) as unknown as Row[];
    return { rows, truncated: rows.length >= ROW_CAP, mock: true, tableMissing: false };
  }

  const admin = getSupabaseAdmin();
  const { data, error } = await admin
    .from("telemetry_events")
    .select("*")
    .gte("received_at", range.from)
    .lte("received_at", range.to)
    .order("received_at", { ascending: false })
    .limit(ROW_CAP);
  if (error) {
    if (isMissingTable(error)) {
      console.warn("telemetry_events not found — migration not applied yet.");
      return { rows: [], truncated: false, mock: false, tableMissing: true };
    }
    throw new Error(`telemetry_events query failed: ${error.message}`);
  }

  const rows = (data ?? []) as Row[];
  return { rows, truncated: rows.length >= ROW_CAP, mock: false, tableMissing: false };
}

/** Rows of one event name. */
function byName(rows: Row[], name: string): Row[] {
  return rows.filter((r) => r.name === name);
}

// ---------------------------------------------------------------------------
// §3.1 KPIs + §3.2 funnel + §3.3 per-hole + §3.4 shot quality
// ---------------------------------------------------------------------------

function buildKpis(rows: Row[], range: TelemetryRange): TelemetryKpis {
  const cutoff = todayStart(range);
  const testers = new Set<string>();
  const testersToday = new Set<string>();
  const sessions = new Set<string>();
  const sessionsToday = new Set<string>();

  for (const r of rows) {
    const user = str(r.user_id);
    const session = str(r.session_id);
    const at = Date.parse(String(r.received_at ?? ""));
    const today = Number.isFinite(at) && at >= cutoff;
    if (user) {
      testers.add(user);
      if (today) testersToday.add(user);
    }
    if (session) {
      sessions.add(session);
      if (today) sessionsToday.add(session);
    }
  }

  const roundsStarted = byName(rows, "round_start").length;
  const abandons = byName(rows, "round_abandoned").length;

  return {
    activeTesters: testers.size,
    activeTestersToday: testersToday.size,
    sessions: sessions.size,
    sessionsToday: sessionsToday.size,
    roundsStarted,
    holesCompleted: byName(rows, "hole_complete").length,
    abandons,
    abandonRate: rate(abandons, roundsStarted),
    crashes: byName(rows, "client_error").length,
  };
}

const FUNNEL_ORDER: FunnelStageId[] = [
  "session_start",
  "home",
  "hole_select",
  "round_start",
  "hole_complete",
];

/** Screens that count as "reached hole selection" (either flavour, per SPEC §3.2). */
const HOLE_SELECT_SCREENS = new Set(["HoleSelection", "TournamentHoleSelection"]);

/**
 * §3.2 — per-session progression.
 *
 * A session is counted at a stage when it reached that stage OR any LATER one.
 * That is what makes the bars monotonically non-increasing even when a single
 * screen_view is lost in a dropped batch: you cannot start a round without
 * having passed Home, so a round_start is evidence for the Home stage too. The
 * alternative — counting each stage independently — produces a funnel that goes
 * UP in the middle whenever the network hiccups, and an operator rightly stops
 * trusting the whole panel at that point.
 */
function buildFunnel(rows: Row[]): FunnelStage[] {
  const depthBySession = new Map<string, number>();

  const mark = (session: string | null, stage: FunnelStageId) => {
    if (!session) return;
    const depth = FUNNEL_ORDER.indexOf(stage);
    depthBySession.set(session, Math.max(depthBySession.get(session) ?? -1, depth));
  };

  for (const r of rows) {
    const session = str(r.session_id);
    if (!session) continue;
    // Any event at all establishes the session exists (depth -1 → 0 below).
    if (!depthBySession.has(session)) depthBySession.set(session, -1);

    const name = String(r.name ?? "");
    if (name === "session_start") mark(session, "session_start");
    else if (name === "round_start") mark(session, "round_start");
    else if (name === "hole_complete") mark(session, "hole_complete");
    else if (name === "screen_view") {
      const screen = str(payloadOf(r).screen);
      if (screen === "Home") mark(session, "home");
      else if (screen && HOLE_SELECT_SCREENS.has(screen)) mark(session, "hole_select");
    }
  }

  const total = depthBySession.size;
  return FUNNEL_ORDER.map((id, i) => {
    let reached = 0;
    for (const depth of depthBySession.values()) if (depth >= i) reached += 1;
    return { id, sessions: reached, pct: rate(reached, total) ?? 0 };
  });
}

/** §3.3 — one row per hole seen in round_start / hole_complete. */
function buildHoles(rows: Row[]): HoleStat[] {
  interface Acc {
    plays: number;
    completions: number;
    abandons: number;
    strokes: number[];
    penalties: number[];
    durations: number[];
    fpsLow: number[];
    shots: number;
    obShots: number;
  }
  const acc = new Map<number, Acc>();
  const get = (hole: number): Acc => {
    let a = acc.get(hole);
    if (!a) {
      a = { plays: 0, completions: 0, abandons: 0, strokes: [], penalties: [], durations: [], fpsLow: [], shots: 0, obShots: 0 };
      acc.set(hole, a);
    }
    return a;
  };

  for (const r of rows) {
    const p = payloadOf(r);
    const hole = holeOf(p);
    if (hole === null) continue;
    const name = String(r.name ?? "");

    if (name === "round_start") get(hole).plays += 1;
    else if (name === "round_abandoned") get(hole).abandons += 1;
    else if (name === "hole_complete") {
      const a = get(hole);
      a.completions += 1;
      const strokes = num(p.strokes);
      if (strokes !== null) a.strokes.push(strokes);
      const penalty = num(p.penalty_strokes);
      if (penalty !== null) a.penalties.push(penalty);
      const duration = num(p.duration_s);
      if (duration !== null) a.durations.push(duration);
      const fpsLow = num(p.fps_low);
      if (fpsLow !== null) a.fpsLow.push(fpsLow);
    } else if (name === "shot_taken") {
      const a = get(hole);
      a.shots += 1;
      if (str(p.terminal)?.toUpperCase() === "OB") a.obShots += 1;
    }
  }

  return [...acc.entries()]
    .map(([hole, a]) => ({
      hole,
      plays: a.plays,
      completions: a.completions,
      abandons: a.abandons,
      avgStrokes: mean(a.strokes),
      avgPenaltyStrokes: mean(a.penalties),
      shots: a.shots,
      obRate: rate(a.obShots, a.shots),
      avgDurationS: mean(a.durations),
      fpsLowMedian: median(a.fpsLow),
    }))
    .sort((a, b) => a.hole - b.hole);
}

/** §3.4 — do the controls work. */
function buildShotQuality(rows: Row[]): ShotQuality {
  const taken = byName(rows, "shot_taken");
  const rejected = byName(rows, "flick_rejected").length;
  const cancelled = byName(rows, "shot_cancelled").length;

  let obShots = 0;
  const clubs = new Map<string, { shots: number; distances: number[] }>();
  for (const r of taken) {
    const p = payloadOf(r);
    if (str(p.terminal)?.toUpperCase() === "OB") obShots += 1;
    const club = str(p.club) ?? "(unknown)";
    let c = clubs.get(club);
    if (!c) {
      c = { shots: 0, distances: [] };
      clubs.set(club, c);
    }
    c.shots += 1;
    const d = num(p.distance_m);
    if (d !== null) c.distances.push(d);
  }

  const clubStats: ClubStat[] = [...clubs.entries()]
    .map(([club, c]) => ({ club, shots: c.shots, avgDistanceM: mean(c.distances) }))
    .sort((a, b) => b.shots - a.shots || a.club.localeCompare(b.club));

  return {
    shotsTaken: taken.length,
    flickRejected: rejected,
    shotCancelled: cancelled,
    flickRejectRate: rate(rejected, rejected + taken.length),
    cancelRate: rate(cancelled, cancelled + taken.length),
    obShots,
    obRate: rate(obShots, taken.length),
    clubs: clubStats,
  };
}

export async function fetchTelemetrySummary(
  range: TelemetryRange
): Promise<TelemetrySummaryResponse> {
  const { rows, truncated, mock, tableMissing } = await scanEvents(range);
  return {
    mock,
    range,
    rowCount: rows.length,
    truncated,
    tableMissing,
    kpis: buildKpis(rows, range),
    funnel: buildFunnel(rows),
    holes: buildHoles(rows),
    shots: buildShotQuality(rows),
    eventNames: [...new Set(rows.map((r) => String(r.name ?? "")))].filter(Boolean).sort(),
  };
}

// ---------------------------------------------------------------------------
// §3.5 Testers
// ---------------------------------------------------------------------------

export async function fetchTelemetryTesters(
  range: TelemetryRange
): Promise<TelemetryTestersResponse> {
  const [{ rows, truncated, mock, tableMissing }, directory] = await Promise.all([
    scanEvents(range),
    fetchUserDirectory(),
  ]);

  interface Acc {
    sessions: Set<string>;
    sessionsWithEnd: Set<string>;
    /** session_id → largest duration_s seen. session_end fires on every pause,
     *  and duration_s is realtimeSinceStartup, so the LAST one is the session
     *  total — summing them all would count the same minutes many times over. */
    sessionDuration: Map<string, number>;
    rounds: number;
    holes: number;
    crashes: number;
    platforms: (string | null)[];
    devices: (string | null)[];
    oses: (string | null)[];
    lastSeen: string | null;
    latestAt: number;
    appVersion: string | null;
    buildNumber: number | null;
    /** [receivedAtMs, balance] — points delta is last minus first. */
    balances: [number, number][];
  }

  const acc = new Map<string, Acc>();

  for (const r of rows) {
    const userId = str(r.user_id);
    if (!userId) continue;
    let a = acc.get(userId);
    if (!a) {
      a = {
        sessions: new Set(),
        sessionsWithEnd: new Set(),
        sessionDuration: new Map(),
        rounds: 0,
        holes: 0,
        crashes: 0,
        platforms: [],
        devices: [],
        oses: [],
        lastSeen: null,
        latestAt: -Infinity,
        appVersion: null,
        buildNumber: null,
        balances: [],
      };
      acc.set(userId, a);
    }

    const session = str(r.session_id);
    if (session) a.sessions.add(session);
    a.platforms.push(str(r.platform));
    a.devices.push(str(r.device_model));
    a.oses.push(str(r.os));

    const receivedAt = str(r.received_at);
    const at = receivedAt ? Date.parse(receivedAt) : NaN;
    if (Number.isFinite(at) && at > a.latestAt) {
      a.latestAt = at;
      a.lastSeen = receivedAt;
      a.appVersion = str(r.app_version);
      a.buildNumber = num(r.build_number);
    }

    const name = String(r.name ?? "");
    const p = payloadOf(r);
    if (name === "round_start") a.rounds += 1;
    else if (name === "hole_complete") a.holes += 1;
    else if (name === "client_error") a.crashes += 1;
    else if (name === "session_end") {
      if (session) {
        a.sessionsWithEnd.add(session);
        const d = num(p.duration_s);
        if (d !== null) {
          a.sessionDuration.set(session, Math.max(a.sessionDuration.get(session) ?? 0, d));
        }
      }
    } else if (name === "points_changed") {
      const balance = num(p.balance);
      if (balance !== null && Number.isFinite(at)) a.balances.push([at, balance]);
    }
  }

  const testers: TesterRow[] = [...acc.entries()].map(([userId, a]) => {
    const identity: UserIdentity | undefined = directory.get(userId);
    const sorted = [...a.balances].sort((x, y) => x[0] - y[0]);
    const firstBalance = sorted[0]?.[1] ?? null;
    const lastBalance = sorted[sorted.length - 1]?.[1] ?? null;
    let playTimeS = 0;
    for (const d of a.sessionDuration.values()) playTimeS += d;

    return {
      userId,
      email: identity?.email ?? null,
      displayName: identity?.displayName ?? null,
      platform: modeOf(a.platforms),
      deviceModel: modeOf(a.devices),
      os: modeOf(a.oses),
      appVersion: a.appVersion,
      buildNumber: a.buildNumber,
      sessions: a.sessions.size,
      uncleanExits: a.sessions.size - a.sessionsWithEnd.size,
      playTimeS: Math.round(playTimeS),
      rounds: a.rounds,
      holesCompleted: a.holes,
      pointsDelta:
        sorted.length >= 2 && firstBalance !== null && lastBalance !== null
          ? lastBalance - firstBalance
          : null,
      crashes: a.crashes,
      lastSeen: a.lastSeen,
    };
  });

  testers.sort((x, y) => (y.lastSeen ?? "").localeCompare(x.lastSeen ?? ""));
  return { mock, range, rowCount: rows.length, truncated, tableMissing, testers };
}

// ---------------------------------------------------------------------------
// §3.6 Event explorer — the one endpoint that paginates in the DB
// ---------------------------------------------------------------------------

export interface EventQuery {
  range: TelemetryRange;
  name?: string | null;
  userId?: string | null;
  page: number;
}

function labelFor(userId: string, directory: Map<string, UserIdentity>): string {
  const identity = directory.get(userId);
  return identity?.email ?? identity?.displayName ?? `${userId.slice(0, 8)}…`;
}

function mapEvent(r: Row, directory: Map<string, UserIdentity>): TelemetryEventRow {
  const userId = String(r.user_id ?? "");
  return {
    eventId: String(r.event_id ?? ""),
    userId,
    tester: labelFor(userId, directory),
    sessionId: String(r.session_id ?? ""),
    name: String(r.name ?? ""),
    ts: String(r.ts ?? ""),
    receivedAt: String(r.received_at ?? ""),
    appVersion: str(r.app_version),
    buildNumber: num(r.build_number),
    platform: str(r.platform),
    deviceModel: str(r.device_model),
    os: str(r.os),
    payload: r.payload ?? {},
  };
}

/**
 * Unlike the aggregates, this NEVER loads the window — it pages in the database
 * with `.range()`. The explorer is the one place a beta week could genuinely
 * out-grow a single fetch.
 */
export async function fetchTelemetryEvents(
  query: EventQuery
): Promise<TelemetryEventsResponse> {
  const page = Math.max(0, Math.trunc(query.page));
  const offset = page * EVENT_PAGE_SIZE;
  const { range, name, userId } = query;
  const directory = await fetchUserDirectory();

  if (isMockMode()) {
    const all = (MOCK_TELEMETRY_EVENTS as MockEventRow[])
      .filter((r) => r.received_at >= range.from && r.received_at <= range.to)
      .filter((r) => (name ? r.name === name : true))
      .filter((r) => (userId ? r.user_id === userId : true))
      .sort((a, b) => b.received_at.localeCompare(a.received_at));
    const slice = all.slice(offset, offset + EVENT_PAGE_SIZE) as unknown as Row[];
    return {
      mock: true,
      tableMissing: false,
      range,
      events: slice.map((r) => mapEvent(r, directory)),
      page,
      pageSize: EVENT_PAGE_SIZE,
      total: all.length,
      hasMore: offset + slice.length < all.length,
    };
  }

  const admin = getSupabaseAdmin();
  let q = admin
    .from("telemetry_events")
    .select("*", { count: "exact" })
    .gte("received_at", range.from)
    .lte("received_at", range.to);
  if (name) q = q.eq("name", name);
  if (userId) q = q.eq("user_id", userId);

  const { data, error, count } = await q
    .order("received_at", { ascending: false })
    .range(offset, offset + EVENT_PAGE_SIZE - 1);
  if (error) {
    if (isMissingTable(error)) {
      return {
        mock: false,
        tableMissing: true,
        range,
        events: [],
        page,
        pageSize: EVENT_PAGE_SIZE,
        total: 0,
        hasMore: false,
      };
    }
    throw new Error(`telemetry_events query failed: ${error.message}`);
  }

  const rows = (data ?? []) as Row[];
  const total = typeof count === "number" ? count : null;
  return {
    mock: false,
    tableMissing: false,
    range,
    events: rows.map((r) => mapEvent(r, directory)),
    page,
    pageSize: EVENT_PAGE_SIZE,
    total,
    hasMore: total === null ? rows.length === EVENT_PAGE_SIZE : offset + rows.length < total,
  };
}
