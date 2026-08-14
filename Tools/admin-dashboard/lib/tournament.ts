/**
 * Tournament rules shared by the server routes and the client panel.
 * Client-safe — do not import server-only modules here.
 */

import { findCourse } from "./courses";
import type { PrizeBand, TournamentRow, TournamentState } from "./types";

/**
 * Derived state — the SAME rules as LocalTournamentBackend.DeriveState.
 * `tournaments.status` is NOT maintained for golfin rows on purpose (SPEC §4.1):
 * two sources of truth for "is it open" is how schedules rot.
 */
export const ENDING_WINDOW_MS = 60 * 60 * 1000; // final hour

export function deriveState(
  startAt: string | null,
  endAt: string | null,
  nowMs: number
): TournamentState {
  const start = startAt ? Date.parse(startAt) : NaN;
  const end = endAt ? Date.parse(endAt) : NaN;
  if (Number.isNaN(start) || Number.isNaN(end)) return "Unknown";
  if (nowMs < start) return "Upcoming";
  if (nowMs >= end) return "Ended";
  if (end - nowMs <= ENDING_WINDOW_MS) return "Ending";
  return "Open";
}

/** Open and Ending are the states where players may be mid-entry. */
export function isLive(state: TournamentState): boolean {
  return state === "Open" || state === "Ending";
}

/**
 * Which art layer this tournament will resolve to on the client (SPEC §5c.1).
 * Shown in the row so a missing asset is visible before publish, not after.
 */
export type ArtLayer = "remote" | "bundled" | "placeholder";

export function artLayer(t: Pick<TournamentRow, "bannerUrl" | "courseId">): ArtLayer {
  if (t.bannerUrl) return "remote";
  return findCourse(t.courseId) ? "bundled" : "placeholder";
}

/** `1-18`, `1-9`, `1,3,5`, `1-3,7` → expanded hole numbers. Mirrors ExpandHoleSet. */
export function expandHoleSet(holeSet: string | null): number[] {
  if (!holeSet) return [];
  const out: number[] = [];
  for (const part of holeSet.split(",")) {
    const token = part.trim();
    if (!token) continue;
    const range = token.match(/^(\d+)\s*-\s*(\d+)$/);
    if (range) {
      const from = Number(range[1]);
      const to = Number(range[2]);
      if (!Number.isFinite(from) || !Number.isFinite(to) || from > to) return [];
      for (let h = from; h <= to; h++) out.push(h);
      continue;
    }
    const single = Number(token);
    if (!Number.isInteger(single)) return [];
    out.push(single);
  }
  return out;
}

export function validateHoleSet(holeSet: string): string | null {
  const holes = expandHoleSet(holeSet);
  if (holes.length === 0) return "Hole set is empty or malformed (expected e.g. 1-18).";
  if (holes.some((h) => h < 1 || h > 18)) return "Holes must be between 1 and 18.";
  if (new Set(holes).size !== holes.length) return "Hole set repeats a hole.";
  return null;
}

export const SLUG_RE = /^[a-z][a-z0-9_]{2,47}$/;

export function validateSlug(slug: string): string | null {
  if (!SLUG_RE.test(slug)) {
    return "Slug must be lowercase a–z, digits and underscores, 3–48 chars, starting with a letter.";
  }
  return null;
}

/**
 * Rank-band validation (SPEC §5): no gaps, no overlaps, band 1 present.
 * Returns [] when the ladder is sound. Bands need not arrive sorted.
 */
export function validatePrizeBands(bands: PrizeBand[]): string[] {
  const errors: string[] = [];
  if (bands.length === 0) return ["No prize bands — nobody gets paid."];

  for (const b of bands) {
    if (!Number.isInteger(b.rankFrom) || !Number.isInteger(b.rankTo)) {
      errors.push("Ranks must be whole numbers.");
    } else if (b.rankFrom < 1) {
      errors.push(`Band starting at rank ${b.rankFrom}: ranks start at 1.`);
    } else if (b.rankFrom > b.rankTo) {
      errors.push(`Band ${b.rankFrom}–${b.rankTo}: "from" is above "to".`);
    }
    if (!Number.isInteger(b.rpReward) || b.rpReward < 0) {
      errors.push(`Band ${b.rankFrom}–${b.rankTo}: RP must be a whole number ≥ 0.`);
    }
    // tournament_prize amounts are paid through earn_pts_v2 under the
    // 'tournament_prize' catalog action, capped at max_per_event = 2000.
    if (b.rpReward > 2000) {
      errors.push(
        `Band ${b.rankFrom}–${b.rankTo}: ${b.rpReward} RP exceeds the tournament_prize cap of 2000 — the payout would be rejected server-side.`
      );
    }
  }
  if (errors.length > 0) return errors;

  const sorted = [...bands].sort((a, b) => a.rankFrom - b.rankFrom);
  const first = sorted[0];
  if (first && first.rankFrom !== 1) {
    errors.push(`No band covers rank 1 — the winner gets nothing (first band starts at ${first.rankFrom}).`);
  }
  for (let i = 1; i < sorted.length; i++) {
    const prev = sorted[i - 1];
    const cur = sorted[i];
    if (!prev || !cur) continue;
    if (cur.rankFrom <= prev.rankTo) {
      errors.push(`Bands ${prev.rankFrom}–${prev.rankTo} and ${cur.rankFrom}–${cur.rankTo} overlap.`);
    } else if (cur.rankFrom !== prev.rankTo + 1) {
      errors.push(`Gap between rank ${prev.rankTo} and ${cur.rankFrom} — those places pay nothing.`);
    }
  }
  return errors;
}

export function prizePoolSummary(bands: PrizeBand[]): { top: number; total: number; places: number } {
  if (bands.length === 0) return { top: 0, total: 0, places: 0 };
  const sorted = [...bands].sort((a, b) => a.rankFrom - b.rankFrom);
  let total = 0;
  let places = 0;
  for (const b of sorted) {
    const count = Math.max(0, b.rankTo - b.rankFrom + 1);
    total += count * b.rpReward;
    places += count;
  }
  return { top: sorted[0]?.rpReward ?? 0, total, places };
}
