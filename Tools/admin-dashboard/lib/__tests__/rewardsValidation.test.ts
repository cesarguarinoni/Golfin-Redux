import { describe, expect, it } from "vitest";

/**
 * The Rewards panel's number rules — `game_point_actions`, which is LIVE ON SAVE
 * and sets what every player is paid (game_modes_admin §3).
 *
 * WHY THE LOGIC IS RESTATED HERE RATHER THAN IMPORTED. `checkNumber` is private
 * to `lib/rewardsMutations.ts` and that module is `server-only` — importing it
 * pulls in Supabase and the Next server runtime, which is exactly the kind of
 * dependency that makes a suite rot. Exporting it purely to test it would widen
 * a module's surface for the test's convenience.
 *
 * ⚠️ SO THIS FILE IS A CHARACTERISATION TEST, AND ITS HONEST LIMIT IS THAT IT
 * PINS THE RULES, NOT THE IMPLEMENTATION. It cannot catch `rewardsMutations.ts`
 * drifting away from what is written here. What DOES cover the real code is the
 * live probe recorded in IMPLEMENTER_REPORT: all six cases were fired at the
 * DEPLOYED PATCH route and refused, with `game_point_actions` read back
 * unchanged. Treat that as the integration half and this as the specification
 * half; if `checkNumber` ever changes, change both.
 */

/** Mirrors `checkNumber` in lib/rewardsMutations.ts. */
function checkNumber(label: string, value: number | null): string | null {
  if (value === null) return null;
  if (!Number.isFinite(value)) return `${label} must be a whole number or empty.`;
  if (!Number.isInteger(value)) return `${label} must be a whole number (no decimals).`;
  if (value < 0) return `${label} must be 0 or more.`;
  return null;
}

/** Mirrors the `field` coercion in app/api/rewards/[action]/route.ts. */
function field(v: unknown): number | null | "bad" {
  if (v === null || v === undefined || v === "") return null;
  if (typeof v !== "number" || !Number.isFinite(v)) return "bad";
  return v;
}

describe("checkNumber — the guard on what players are paid", () => {
  it("treats null as VALID, because null is a mode and not a missing value", () => {
    // pts NULL = the client supplies the amount, bounded by the caps. That is
    // how hole scores and tournament prizes work. Rejecting null here would
    // make every variable payout unsavable.
    expect(checkNumber("Points", null)).toBeNull();
  });

  it("accepts zero — an action that pays nothing is legal", () => {
    expect(checkNumber("Points", 0)).toBeNull();
  });

  it("refuses negatives", () => {
    expect(checkNumber("Points", -5)).toBe("Points must be 0 or more.");
  });

  it("refuses decimals", () => {
    expect(checkNumber("Points", 1.5)).toBe("Points must be a whole number (no decimals).");
  });

  it("refuses NaN and Infinity before the integer check", () => {
    expect(checkNumber("Daily cap", Number.NaN)).toBe("Daily cap must be a whole number or empty.");
    expect(checkNumber("Daily cap", Number.POSITIVE_INFINITY)).toBe("Daily cap must be a whole number or empty.");
  });
});

describe("the route's field() coercion", () => {
  it("maps absent / empty / null to null, so a blank input round-trips as 'no cap'", () => {
    expect(field(undefined)).toBeNull();
    expect(field(null)).toBeNull();
    expect(field("")).toBeNull();
  });

  it("refuses a NUMERIC STRING — the case a hand-rolled client is most likely to send", () => {
    // Proven against the deployed route: {"pts":"20"} -> 400.
    expect(field("20")).toBe("bad");
  });

  it("refuses booleans and objects", () => {
    expect(field(true)).toBe("bad");
    expect(field({})).toBe("bad");
  });

  it("passes real numbers through, zero included", () => {
    expect(field(0)).toBe(0);
    expect(field(20)).toBe(20);
  });

  it("refuses NaN rather than letting it reach checkNumber", () => {
    expect(field(Number.NaN)).toBe("bad");
  });
});
