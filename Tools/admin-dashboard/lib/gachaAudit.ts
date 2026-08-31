/**
 * Gacha ops — the PURE half of the panel (gacha_server_pull §6).
 *
 * No React, no Supabase, no `server-only`, no clock. Everything here is a
 * function of rows in and rows out, which is what makes it the part the vitest
 * suite covers (ADMIN_DASHBOARD_OPS §2.x: the suite covers the pure modules
 * only, because a suite that needs a DOM or a database is a suite that rots).
 *
 * Two jobs live here, and both are the kind that must not be "checked by eye":
 *
 *   1. THE ODDS AUDIT. Did the server actually pay out what the rate table
 *      published? That is a comparison between two distributions, and the
 *      whole value of it is that nobody has to trust a screenshot of a pull log.
 *   2. THE CSV EXPORT. What an operator hands to someone who is asking whether
 *      the gacha is honest. Its SHAPE is a contract; a column silently added or
 *      reordered breaks whatever they built on top of it.
 *
 * ⚠️ FORCED SLOTS ARE EXCLUDED FROM THE COMPARISON, NOT COUNTED SEPARATELY OUT
 * OF TIDINESS. A pity slot and an x10-guarantee slot are drawn from a
 * RENORMALISED subset of the ladder, so they are SUPPOSED to over-produce high
 * rarities. Folding them into the observed distribution would make every
 * pity-carrying banner read as "paying out too many Legendaries" — the audit
 * would flag exactly the banners that are working. They are reported as their
 * own count so an operator can see how much of the payout came from the floor.
 */

import { RARITY_ORDER, type Rarity } from "./gachaOdds";

/** One prize row, narrowed to what the audit reads. */
export interface AuditPrize {
  pullId: string;
  slot: number;
  rarity: string;
  kind: string;
  refId: string;
  isDupe: boolean;
  dupeRp: number;
}

/** One pull row, narrowed to what the audit reads. */
export interface AuditPull {
  id: string;
  bannerId: string;
  poolId: string;
  pullCount: number;
  pityForced: boolean;
  guaranteeForced: boolean;
  createdAt: string;
}

export interface OddsAuditTier {
  rarity: Rarity;
  /** `rateBp / 10000` as a percentage, i.e. 12 for 1200 bp. */
  publishedPct: number;
  /** Count of UNFORCED slots that landed on this tier. */
  observed: number;
  /** `observed / comparableSlots` as a percentage. */
  observedPct: number;
  /** `observedPct - publishedPct`, in percentage POINTS. */
  deltaPt: number;
  /** True when |deltaPt| is beyond tolerance AND the sample is big enough. */
  amber: boolean;
}

export interface OddsAudit {
  /** Slots the comparison is over — unforced only. */
  comparableSlots: number;
  /** Slots excluded because pity or the x10 guarantee forced them. */
  forcedSlots: number;
  pityPulls: number;
  guaranteePulls: number;
  tiers: OddsAuditTier[];
  /** True once `comparableSlots` is large enough for `amber` to mean anything. */
  significant: boolean;
}

/** Delta beyond this many percentage POINTS is worth an operator's attention. */
export const ODDS_DELTA_TOLERANCE_PT = 2;

/**
 * Below this many comparable slots, a 2-point delta is noise.
 *
 * 1 000 is not a round number chosen for looking tidy: at a published 2 %
 * (the seeded Legendary rate) the standard deviation of the observed share over
 * 1 000 slots is ~0.44 pt, so a 2-point miss is more than four sigma. Under a
 * few hundred slots the same 2 points is inside one sigma and flagging it would
 * train the operator to ignore the colour.
 */
export const ODDS_SIGNIFICANCE_SLOTS = 1000;

/**
 * A PULL is forced or it is not — and a x10 has ten slots of which AT MOST ONE
 * was forced.
 *
 * `golfin_gacha_pulls` stores `pity_forced` / `guarantee_forced` per PULL, not
 * per slot, because a pull is what a player buys. So the audit cannot tell
 * WHICH slot of a forced x10 was the forced one, and excluding all ten would
 * throw away nine honest samples for every forced pull.
 *
 * The rule it uses instead: exclude the pull's HIGHEST-RARITY slot, once, per
 * forced flag. That is provably the forced one for the guarantee (it fires only
 * when nothing else reached the floor, so the re-rolled slot is the best in the
 * block) and is the right guess for pity (the forced slot is drawn from a
 * subset at or above the floor, so it is at or above every unforced slot in the
 * pull whenever pity fired at all). It is an approximation, and it is the
 * CONSERVATIVE one: it can only ever remove a high-rarity sample, never add one,
 * so the audit's failure mode is under-reporting a real over-payout rather than
 * inventing one.
 */
function excludeForcedSlots(pull: AuditPull, prizes: AuditPrize[]): AuditPrize[] {
  let toDrop = (pull.pityForced ? 1 : 0) + (pull.guaranteeForced ? 1 : 0);
  if (toDrop === 0) return prizes;

  // Sort a COPY: the caller's array is the reveal order and other readers depend
  // on it.
  const byRarityDesc = [...prizes].sort(
    (a, b) => RARITY_ORDER.indexOf(b.rarity as Rarity) - RARITY_ORDER.indexOf(a.rarity as Rarity)
  );
  const dropped = new Set<number>();
  for (const prize of byRarityDesc) {
    if (toDrop === 0) break;
    dropped.add(prize.slot);
    toDrop -= 1;
  }
  return prizes.filter((p) => !dropped.has(p.slot));
}

/**
 * Compare what the server paid against what the pool published.
 *
 * `publishedBp` is the banner's pool's `gacha_rates`, keyed by rarity. A tier
 * with no rate row is reported at 0 % published rather than dropped: "this tier
 * paid out and has no published rate" is the single most important thing this
 * audit can find, and dropping the row would hide it.
 */
export function auditOdds(
  pulls: AuditPull[],
  prizesByPull: Map<string, AuditPrize[]>,
  publishedBp: Record<string, number>
): OddsAudit {
  const observed = new Map<string, number>();
  let comparableSlots = 0;
  let forcedSlots = 0;
  let pityPulls = 0;
  let guaranteePulls = 0;

  for (const pull of pulls) {
    const prizes = prizesByPull.get(pull.id) ?? [];
    if (pull.pityForced) pityPulls += 1;
    if (pull.guaranteeForced) guaranteePulls += 1;

    const comparable = excludeForcedSlots(pull, prizes);
    forcedSlots += prizes.length - comparable.length;

    for (const prize of comparable) {
      observed.set(prize.rarity, (observed.get(prize.rarity) ?? 0) + 1);
      comparableSlots += 1;
    }
  }

  const significant = comparableSlots >= ODDS_SIGNIFICANCE_SLOTS;

  const tiers: OddsAuditTier[] = RARITY_ORDER.map((rarity) => {
    const publishedPct = (publishedBp[rarity] ?? 0) / 100;
    const count = observed.get(rarity) ?? 0;
    const observedPct = comparableSlots > 0 ? (count / comparableSlots) * 100 : 0;
    const deltaPt = observedPct - publishedPct;
    return {
      rarity,
      publishedPct,
      observed: count,
      observedPct,
      deltaPt,
      amber: significant && Math.abs(deltaPt) > ODDS_DELTA_TOLERANCE_PT,
    };
  });

  return { comparableSlots, forcedSlots, pityPulls, guaranteePulls, tiers, significant };
}

// ---------------------------------------------------------------------------
// CSV export
// ---------------------------------------------------------------------------

/**
 * The pull-log CSV, ONE ROW PER PRIZE.
 *
 * Per prize and not per pull, because every question this file gets exported to
 * answer — "what did this player actually receive", "how often did this club
 * drop", "what did we pay out in dupe RP" — is a question about prizes. A
 * pull-shaped file would need the reader to explode a nested column first.
 *
 * The pull-level fields repeat on each of its rows. That is the deliberate
 * trade: the file is bigger and every row stands alone in a spreadsheet filter,
 * which is what it will actually be opened in.
 */
export const PULL_CSV_COLUMNS = [
  "pull_id",
  "created_at",
  "user_email",
  "user_id",
  "banner_id",
  "pool_id",
  "pull_count",
  "ticket_type",
  "cost",
  "pity_forced",
  "guarantee_forced",
  "slot",
  "kind",
  "ref_id",
  "quantity",
  "rarity",
  "is_dupe",
  "dupe_rp",
] as const;

export interface PullCsvRow {
  id: string;
  createdAt: string;
  userEmail: string | null;
  userId: string;
  bannerId: string;
  poolId: string;
  pullCount: number;
  ticketType: number;
  cost: number;
  pityForced: boolean;
  guaranteeForced: boolean;
  prizes: Array<{
    slot: number;
    kind: string;
    refId: string;
    quantity: number;
    rarity: string;
    isDupe: boolean;
    dupeRp: number;
  }>;
}

/**
 * RFC 4180 quoting, applied to every cell rather than only the ones that look
 * like they need it.
 *
 * A ref id or an email cannot contain a comma today. "Cannot today" is how a
 * CSV writer acquires a corruption bug two catalogs later, and quoting
 * unconditionally costs two bytes a cell.
 */
function cell(value: unknown): string {
  const s = value === null || value === undefined ? "" : String(value);
  return `"${s.replace(/"/g, '""')}"`;
}

export function pullsToCsv(rows: PullCsvRow[]): string {
  const lines: string[] = [PULL_CSV_COLUMNS.map(cell).join(",")];

  for (const pull of rows) {
    // A pull with no prize rows still gets ONE line. It should be impossible —
    // the function writes the prizes in the same transaction — so if it ever
    // happens the export is where it becomes visible, and a silently skipped
    // pull would hide exactly that.
    const prizes = pull.prizes.length > 0 ? pull.prizes : [null];

    for (const prize of prizes) {
      lines.push(
        [
          pull.id,
          pull.createdAt,
          pull.userEmail ?? "",
          pull.userId,
          pull.bannerId,
          pull.poolId,
          pull.pullCount,
          pull.ticketType,
          pull.cost,
          pull.pityForced,
          pull.guaranteeForced,
          prize?.slot ?? "",
          prize?.kind ?? "",
          prize?.refId ?? "",
          prize?.quantity ?? "",
          prize?.rarity ?? "",
          prize?.isDupe ?? "",
          prize?.dupeRp ?? "",
        ].map(cell).join(",")
      );
    }
  }

  // Trailing newline: without one, `cat a.csv b.csv` silently joins the last
  // row of the first file to the header of the second.
  return lines.join("\n") + "\n";
}
