import "server-only";
import { MOCK_USERS } from "./mock";

/**
 * Mock-mode telemetry fixture — the panel's development and verification data.
 *
 * DETERMINISTIC BY CONSTRUCTION. Every timestamp is an offset from the frozen
 * `MOCK_NOW` below; there is no `Date.now()`, no `Math.random()`, no
 * `crypto.randomUUID()` anywhere in this file. Two renders a week apart produce
 * byte-identical numbers, which is the only reason a screenshot of this panel
 * is worth anything as evidence.
 *
 * It is also how the panel was built and reviewed BEFORE the telemetry_events
 * migration landed (telemetry_admin_panel SPEC §4).
 *
 * Shape: 5 testers, 10 sessions, 2 holes, one abandon, one crash, three flick
 * rejects, one unclean exit — enough to light up all six sections.
 *
 * Payload keys are exactly beta_telemetry SPEC §1. Nothing here is invented.
 */

/** Frozen "now". The default range ends here, so §3.1's "today" counters have
 *  something to count and stay stable forever. */
export const MOCK_NOW = "2026-08-18T18:00:00.000Z";
const MOCK_NOW_MS = Date.parse(MOCK_NOW);

export interface MockEventRow {
  event_id: string;
  user_id: string;
  session_id: string;
  name: string;
  ts: string;
  received_at: string;
  app_version: string | null;
  build_number: number | null;
  platform: string | null;
  device_model: string | null;
  os: string | null;
  payload: Record<string, unknown>;
}

interface Device {
  platform: string;
  deviceModel: string;
  os: string;
  appVersion: string;
  buildNumber: number;
}

const DEVICES: Device[] = [
  { platform: "iOS", deviceModel: "iPhone15,3", os: "iOS 18.5", appVersion: "1.5.7", buildNumber: 2192 },
  { platform: "iOS", deviceModel: "iPhone14,5", os: "iOS 17.6.1", appVersion: "1.5.7", buildNumber: 2192 },
  { platform: "iOS", deviceModel: "iPhone12,1", os: "iOS 16.7.8", appVersion: "1.5.6", buildNumber: 2181 },
  { platform: "Android", deviceModel: "Pixel 8", os: "Android OS 15", appVersion: "1.5.7", buildNumber: 2192 },
  { platform: "iOS", deviceModel: "iPad13,4", os: "iPadOS 18.5", appVersion: "1.5.7", buildNumber: 2192 },
];

/** The five fixture testers, borrowed from the Users fixture so the id → email
 *  lookup resolves to the same people the rest of the dashboard shows. */
const TESTERS = MOCK_USERS.slice(0, 5).map((u, i) => ({
  id: u.id,
  device: must(DEVICES[i], `device ${i}`),
}));

/** The fixture is authored data, not input: an out-of-range index is a typo in
 *  this file, and failing loudly beats emitting a row with `undefined` in it. */
function must<T>(v: T | undefined, what: string): T {
  if (v === undefined) throw new Error(`mockTelemetry: missing ${what}`);
  return v;
}

/** Hours before MOCK_NOW → ISO. Negative offsets only; everything is history. */
function at(hoursAgo: number, minutes = 0): string {
  return new Date(MOCK_NOW_MS - hoursAgo * 3_600_000 + minutes * 60_000).toISOString();
}

/** Deterministic pseudo-uuid: readable in the explorer, stable across renders. */
function id(prefix: string, n: number): string {
  const hex = n.toString(16).padStart(12, "0");
  return `${prefix.padEnd(8, "0").slice(0, 8)}-0000-4000-8000-${hex}`;
}

interface SessionPlan {
  /** Index into TESTERS. */
  tester: number;
  /** Hours before MOCK_NOW the session starts. */
  startsHoursAgo: number;
  /** How far the session gets. */
  depth: "boot" | "home" | "holeSelect" | "round" | "complete";
  /** Holes played, in order, when depth is "round"/"complete". */
  holes?: number[];
  /** Session ends cleanly (a session_end event exists). */
  cleanExit?: boolean;
  abandon?: boolean;
  crash?: boolean;
  flickRejects?: number;
  cancels?: number;
  /** Balances seen by points_changed, in order. */
  balances?: number[];
  /** Tournament menu screens instead of the practice ones. */
  tournament?: boolean;
}

/**
 * 10 sessions. Deliberately uneven: two never leave the boot screens, one
 * bounces off hole selection, one abandons mid-round, one crashes. A funnel
 * where every stage is 100% would prove nothing about the funnel.
 */
const PLAN: SessionPlan[] = [
  { tester: 0, startsHoursAgo: 74, depth: "complete", holes: [1, 2], cleanExit: true, flickRejects: 2, cancels: 1, balances: [0, 20, 45] },
  { tester: 0, startsHoursAgo: 5, depth: "complete", holes: [3], cleanExit: true, balances: [45, 65] },
  { tester: 1, startsHoursAgo: 50, depth: "complete", holes: [1], cleanExit: true, flickRejects: 1, balances: [0, 20] },
  { tester: 1, startsHoursAgo: 27, depth: "round", holes: [2], abandon: true, cleanExit: true },
  { tester: 2, startsHoursAgo: 47, depth: "holeSelect", cleanExit: true },
  { tester: 2, startsHoursAgo: 22, depth: "complete", holes: [1], crash: true, cleanExit: false },
  { tester: 3, startsHoursAgo: 30, depth: "home", cleanExit: true },
  { tester: 3, startsHoursAgo: 3, depth: "complete", holes: [2], cleanExit: true, tournament: true, balances: [100, 120] },
  { tester: 4, startsHoursAgo: 26, depth: "boot", cleanExit: true },
  { tester: 4, startsHoursAgo: 2, depth: "round", holes: [1], cleanExit: true, cancels: 1 },
];

/** Per-hole shot script — fixed, so avg strokes / OB rate never move. */
/**
 * shot_timing_telemetry: `timing` is the slab progress the flick was judged on, exactly as
 * the client ships it — null for the shots a bot/debug driver took (no touch sample). The
 * band and the multiplier are derived from it with the shipped ControlsConfig edges below,
 * so the fixture cannot drift from the client's own verdict by hand-editing one of the three.
 */
const SHOTS: Record<
  number,
  { club: string; distance: number; terminal: string; surface: string; penalty: number; timing: number | null }[]
> = {
  1: [
    { club: "Driver", distance: 214.4, terminal: "AtRest", surface: "Fairway", penalty: 0, timing: 0.91 },
    { club: "7 Iron", distance: 132.1, terminal: "AtRest", surface: "Green", penalty: 0, timing: 0.62 },
    { club: "Putter", distance: 6.3, terminal: "InCup", surface: "Green", penalty: 0, timing: 0.88 },
  ],
  2: [
    { club: "Driver", distance: 231.8, terminal: "OB", surface: "OOB", penalty: 1, timing: 0.18 },
    { club: "Driver", distance: 198.2, terminal: "AtRest", surface: "Rough", penalty: 0, timing: 0.47 },
    { club: "9 Iron", distance: 96.7, terminal: "AtRest", surface: "Green", penalty: 0, timing: null },
    { club: "Putter", distance: 4.1, terminal: "InCup", surface: "Green", penalty: 0, timing: 0.93 },
  ],
  3: [
    { club: "Driver", distance: 205.9, terminal: "AtRest", surface: "Bunker", penalty: 0, timing: 0.31 },
    { club: "S.Wedge", distance: 61.4, terminal: "AtRest", surface: "Green", penalty: 0, timing: 0.72 },
    { club: "Putter", distance: 9.8, terminal: "InCup", surface: "Green", penalty: 0, timing: null },
  ],
};

/** The shipped ControlsConfig.Default band edges + multipliers (F15). Kept here only so the
 *  fixture's three keys stay mutually consistent — the real client is the source of truth. */
const TIMING_GOLD_Y01 = 0.45;
const TIMING_GREEN_Y01 = 0.85;
const TIMING_MUL_RED = 0.7;
const TIMING_MUL_GOLD = 0.9;

function timingBand(t: number | null): string | null {
  if (t === null) return null;
  if (t >= TIMING_GREEN_Y01) return "green";
  if (t >= TIMING_GOLD_Y01) return "gold";
  return "red";
}

function timingMul(t: number | null): number {
  if (t === null) return 1;
  if (t >= TIMING_GREEN_Y01) return 1;
  const mul =
    t >= TIMING_GOLD_Y01
      ? TIMING_MUL_GOLD +
        ((1 - TIMING_MUL_GOLD) * (t - TIMING_GOLD_Y01)) / (TIMING_GREEN_Y01 - TIMING_GOLD_Y01)
      : TIMING_MUL_RED + ((TIMING_MUL_GOLD - TIMING_MUL_RED) * t) / TIMING_GOLD_Y01;
  return Math.round(mul * 100) / 100;
}

const PAR: Record<number, number> = { 1: 4, 2: 5, 3: 4 };
/**
 * fps_low base per hole. Hole 2 sits deliberately under 20 — it is the fixture's
 * problem hole (it also carries the only OB) so the panel's two red-tint rules
 * are both exercised by data rather than only by code.
 */
const FPS_BY_HOLE: Record<number, number> = { 1: 41.2, 2: 18.6, 3: 36.5 };

function buildRows(): MockEventRow[] {
  const rows: MockEventRow[] = [];
  let seq = 0;

  PLAN.forEach((plan, s) => {
    const tester = must(TESTERS[plan.tester], `tester ${plan.tester}`);
    const sessionId = id("5e551000", s + 1);
    const base = plan.startsHoursAgo;
    let minute = 0;
    let balanceIdx = 0;

    const push = (name: string, minutesIn: number, payload: Record<string, unknown>) => {
      const when = at(base, minutesIn);
      rows.push({
        event_id: id("e7e70000", ++seq),
        user_id: tester.id,
        session_id: sessionId,
        name,
        ts: when,
        // received_at trails the client clock by a few seconds — the batch flush.
        received_at: new Date(Date.parse(when) + 4000).toISOString(),
        app_version: tester.device.appVersion,
        build_number: tester.device.buildNumber,
        platform: tester.device.platform,
        device_model: tester.device.deviceModel,
        os: tester.device.os,
        payload,
      });
    };

    const points = () => {
      if (!plan.balances || balanceIdx >= plan.balances.length) return;
      const balance = must(plan.balances[balanceIdx], `balance ${balanceIdx}`);
      const previous =
        balanceIdx === 0 ? null : must(plan.balances[balanceIdx - 1], "previous balance");
      balanceIdx += 1;
      push("points_changed", (minute += 1), {
        balance,
        delta: previous === null ? null : balance - previous,
      });
    };

    push("session_start", minute, {
      device_model: tester.device.deviceModel,
      os: tester.device.os,
      memory_mb: 6144,
      screen: "1170x2532",
    });
    push("screen_view", (minute += 1), { screen: "Logo", since_boot_s: 0.4 });

    if (plan.depth === "boot") {
      if (plan.cleanExit) push("session_end", (minute += 2), { duration_s: 190 });
      return;
    }

    push("screen_view", (minute += 1), { screen: "Home", since_boot_s: 6.8 });
    points();

    if (plan.depth === "home") {
      if (plan.cleanExit) push("session_end", (minute += 4), { duration_s: 380 });
      return;
    }

    const menuScreen = plan.tournament ? "TournamentHoleSelection" : "HoleSelection";
    push("screen_view", (minute += 1), { screen: menuScreen, since_boot_s: 32.5 });

    if (plan.depth === "holeSelect") {
      if (plan.cleanExit) push("session_end", (minute += 3), { duration_s: 520 });
      return;
    }

    for (const hole of plan.holes ?? []) {
      const shots = must(SHOTS[hole] ?? SHOTS[1], `shot script for hole ${hole}`);
      const roundStartMinute = (minute += 1);
      push("round_start", roundStartMinute, {
        hole,
        character_id: "char_kaito",
        bag_slot: 0,
        is_tournament: plan.tournament === true,
        tournament_id: plan.tournament ? "golfin_weekly_open" : null,
      });

      for (let r = 0; r < (plan.flickRejects ?? 0); r++) {
        push("flick_rejected", (minute += 1), {
          speed: 0.42 + r * 0.05,
          hole,
          shot_number: r + 1,
        });
      }
      for (let c = 0; c < (plan.cancels ?? 0); c++) {
        push("shot_cancelled", (minute += 1), { hole, shot_number: 1 });
      }

      // An abandoned round stops after the first shot and bounces to the menu.
      const played = plan.abandon ? shots.slice(0, 1) : shots;
      played.forEach((shot, i) => {
        push("shot_taken", (minute += 1), {
          shot_number: i + 1,
          club: shot.club,
          distance_m: shot.distance,
          terminal: shot.terminal,
          ob_reason: shot.terminal === "OB" ? "CrossedBoundary" : null,
          surface: shot.surface,
          penalty: shot.penalty,
          hole,
          timing01: shot.timing,
          timing_mul: timingMul(shot.timing),
          timing_band: timingBand(shot.timing),
        });
      });

      if (plan.abandon) {
        push("screen_view", (minute += 2), { screen: menuScreen, since_boot_s: 410.2 });
        push("round_abandoned", minute, {
          hole,
          shots_taken: played.length,
          last_screen: menuScreen,
        });
        continue;
      }

      // depth "round" without an abandon = the app died mid-round: shots, then
      // silence. It reaches the round_start funnel stage but never hole_complete.
      if (plan.depth !== "complete") continue;

      if (plan.crash) {
        push("client_error", (minute += 1), {
          message: "NullReferenceException: Object reference not set to an instance of an object",
          stack: "at Golfin.Gameplay.UI.HUD.ShotMeter.OnDisable () [0x00012]",
          screen: "Gameplay",
        });
      }

      const strokes = played.length + played.reduce((n, s2) => n + s2.penalty, 0);
      // Deterministic per-session jitter so a median has something to average.
      const fpsLow = must(FPS_BY_HOLE[hole] ?? FPS_BY_HOLE[1], "fps base") + ((s % 3) - 1) * 0.7;
      push("hole_complete", (minute += 1), {
        hole,
        strokes,
        penalty_strokes: played.reduce((n, s2) => n + s2.penalty, 0),
        result: "InCup",
        duration_s: (minute - roundStartMinute) * 60,
        fps_avg: fpsLow + 14.6,
        fps_low: fpsLow,
        par: PAR[hole] ?? 4,
      });
      points();
    }

    if (plan.cleanExit) {
      push("session_end", (minute += 2), { duration_s: minute * 60 });
    }
  });

  return rows;
}

/** Frozen at module scope — one build per process, identical every time. */
export const MOCK_TELEMETRY_EVENTS: MockEventRow[] = buildRows();
