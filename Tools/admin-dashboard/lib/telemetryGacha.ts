/**
 * The gacha funnel — PURE aggregation over `telemetry_events` rows.
 *
 * No React, no Supabase, no clock. It takes the rows `telemetryData.scanEvents`
 * already fetched for the range the panel is showing and folds the five
 * `gacha_*` events (gacha_ops_polish §3) into one card's worth of numbers.
 *
 * ⚠️ THIS IS THE BEHAVIOUR VIEW, NOT THE LEDGER. `golfin_gacha_pulls` is the
 * authority on what was won and what was charged; it has one row per PULL, so it
 * structurally cannot answer the question this card exists for — what happened
 * to the players who looked and did NOT pull. Views, taps, refusals and skips
 * live only here, and prize detail deliberately stops at the six-int rarity
 * histogram so the two never become two copies of one truth.
 *
 * Every rate is `null` rather than 0 when its denominator is empty: "no data" and
 * "nobody converted" are different findings, and a dashboard that renders the
 * first as 0 % invents the second.
 */

/** One `telemetry_events` row, narrowed to what this module reads. */
export interface GachaEventRow {
  name?: unknown;
  payload?: unknown;
  user_id?: unknown;
}

export interface GachaBannerRow {
  bannerId: string;
  views: number;
  taps: number;
  pulls: number;
}

export interface GachaFunnel {
  /** `gacha_banner_view` — one per banner per Rewards Center open. */
  views: number;
  /** `gacha_pull_tap` — PULL pressed, before the server was asked. */
  taps: number;
  /** `gacha_pull_result` rows, any status. */
  results: number;
  /** `gacha_pull_result` with `status = "ok"`. */
  pulls: number;
  /** Single pulls versus x10s, over the `ok` results. */
  pullsX1: number;
  pullsX10: number;
  /** `gacha_rules_open` — the RATES modal. */
  rulesOpens: number;
  /** `gacha_reveal_skip`. */
  skips: number;

  /** taps ÷ views. The first drop-off, and the one a banner's art can move. */
  tapRate: number | null;
  /** ok ÷ taps. Everything between a tap and a pull is a refusal. */
  pullRate: number | null;
  /** skips ÷ ok pulls. A statement about the ANIMATION, not about the pull. */
  skipRate: number | null;
  /** insufficient ÷ results. The one refusal that is an economy signal. */
  insufficientRate: number | null;
  /** rules opens ÷ views. Whether the disclosure surface is read at all. */
  rulesRate: number | null;

  /** Mean `latency_ms` over every answered pull, `ok` or not. */
  meanLatencyMs: number | null;
  /** Count per `status`, so a refusal spike is nameable and not just "not ok". */
  byStatus: Record<string, number>;
  /** Six ints, ladder order (Common first), summed over every `ok` result. */
  rarities: number[];
  /** Prizes that were duplicates and paid RP instead. */
  dupes: number;
  /** `ok` results where the server forced a floor. */
  pityForced: number;
  guaranteeForced: number;
  /** Distinct users who fired any gacha event in range. */
  players: number;

  perBanner: GachaBannerRow[];
}

const RARITY_SLOTS = 6;

function payloadOf(row: GachaEventRow): Record<string, unknown> {
  const raw = row.payload;
  if (raw && typeof raw === "object" && !Array.isArray(raw)) return raw as Record<string, unknown>;
  if (typeof raw === "string") {
    // The column is jsonb, but a client that posted a stringified payload — or a
    // fixture written by hand — would otherwise contribute silently-zero rows.
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

function rate(numerator: number, denominator: number): number | null {
  return denominator > 0 ? numerator / denominator : null;
}

function mean(xs: number[]): number | null {
  return xs.length > 0 ? xs.reduce((sum, x) => sum + x, 0) / xs.length : null;
}

/**
 * Fold the gacha events of one range into the funnel card.
 *
 * Rows of other event names are ignored, so the caller can hand this the same
 * unfiltered scan every other section reads.
 */
export function buildGachaFunnel(rows: GachaEventRow[]): GachaFunnel {
  let views = 0;
  let taps = 0;
  let results = 0;
  let pulls = 0;
  let pullsX1 = 0;
  let pullsX10 = 0;
  let rulesOpens = 0;
  let skips = 0;
  let dupes = 0;
  let pityForced = 0;
  let guaranteeForced = 0;

  const byStatus: Record<string, number> = {};
  const latencies: number[] = [];
  const rarities = new Array<number>(RARITY_SLOTS).fill(0);
  const players = new Set<string>();

  interface Acc {
    views: number;
    taps: number;
    pulls: number;
  }
  const perBanner = new Map<string, Acc>();
  const bannerAcc = (id: string): Acc => {
    let a = perBanner.get(id);
    if (!a) {
      a = { views: 0, taps: 0, pulls: 0 };
      perBanner.set(id, a);
    }
    return a;
  };

  for (const row of rows) {
    const name = String(row.name ?? "");
    if (!name.startsWith("gacha_")) continue;

    const p = payloadOf(row);
    const bannerId = str(p.banner_id) ?? "(unknown)";
    const user = str(row.user_id);
    if (user) players.add(user);

    switch (name) {
      case "gacha_banner_view":
        views += 1;
        bannerAcc(bannerId).views += 1;
        break;

      case "gacha_pull_tap":
        taps += 1;
        bannerAcc(bannerId).taps += 1;
        break;

      case "gacha_rules_open":
        rulesOpens += 1;
        break;

      case "gacha_reveal_skip":
        skips += 1;
        break;

      case "gacha_pull_result": {
        results += 1;

        // An unnamed status is counted under "unknown" rather than dropped: a
        // result the dashboard cannot name is exactly the thing worth seeing.
        const status = str(p.status) ?? "unknown";
        byStatus[status] = (byStatus[status] ?? 0) + 1;

        const latency = num(p.latency_ms);
        if (latency !== null) latencies.push(latency);

        if (status !== "ok") break;

        pulls += 1;
        bannerAcc(bannerId).pulls += 1;

        const count = num(p.count);
        if (count === 10) pullsX10 += 1;
        else if (count !== null) pullsX1 += 1;

        const hist = p.rarities;
        if (Array.isArray(hist)) {
          for (let i = 0; i < RARITY_SLOTS && i < hist.length; i += 1) {
            rarities[i] = (rarities[i] ?? 0) + (num(hist[i]) ?? 0);
          }
        }

        dupes += num(p.dupes) ?? 0;
        if (p.pity_forced === true) pityForced += 1;
        if (p.guarantee_forced === true) guaranteeForced += 1;
        break;
      }

      default:
        break;
    }
  }

  return {
    views,
    taps,
    results,
    pulls,
    pullsX1,
    pullsX10,
    rulesOpens,
    skips,
    tapRate: rate(taps, views),
    pullRate: rate(pulls, taps),
    skipRate: rate(skips, pulls),
    insufficientRate: rate(byStatus.insufficient ?? 0, results),
    rulesRate: rate(rulesOpens, views),
    meanLatencyMs: mean(latencies),
    byStatus,
    rarities,
    dupes,
    pityForced,
    guaranteeForced,
    players: players.size,
    perBanner: [...perBanner.entries()]
      .map(([bannerId, a]) => ({ bannerId, ...a }))
      .sort((a, b) => b.views - a.views || b.taps - a.taps || a.bannerId.localeCompare(b.bannerId)),
  };
}
