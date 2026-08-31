import { describe, expect, it } from "vitest";

import { buildGachaFunnel, type GachaEventRow } from "../telemetryGacha";

const view = (bannerId: string, user = "u1"): GachaEventRow => ({
  name: "gacha_banner_view",
  user_id: user,
  payload: { banner_id: bannerId, position: 0, live_count: 3 },
});

const tap = (bannerId: string, count = 1, user = "u1"): GachaEventRow => ({
  name: "gacha_pull_tap",
  user_id: user,
  payload: { banner_id: bannerId, count, cost: 50, ticket_type: 0, balance_before: 100 },
});

const result = (
  bannerId: string,
  status: string,
  extra: Record<string, unknown> = {},
  user = "u1"
): GachaEventRow => ({
  name: "gacha_pull_result",
  user_id: user,
  payload: { banner_id: bannerId, count: 1, status, latency_ms: 200, ...extra },
});

const skip = (bannerId: string, cardsShown = 3, user = "u1"): GachaEventRow => ({
  name: "gacha_reveal_skip",
  user_id: user,
  payload: { banner_id: bannerId, count: 10, cards_shown: cardsShown },
});

const rules = (bannerId: string, user = "u1"): GachaEventRow => ({
  name: "gacha_rules_open",
  user_id: user,
  payload: { banner_id: bannerId },
});

describe("buildGachaFunnel", () => {
  it("counts the five events and the conversions between them", () => {
    const rows = [
      view("b1"),
      view("b1"),
      tap("b1"),
      result("b1", "ok"),
      skip("b1"),
      rules("b1"),
    ];

    const f = buildGachaFunnel(rows);

    expect(f.views).toBe(2);
    expect(f.taps).toBe(1);
    expect(f.results).toBe(1);
    expect(f.pulls).toBe(1);
    expect(f.skips).toBe(1);
    expect(f.rulesOpens).toBe(1);

    expect(f.tapRate).toBeCloseTo(0.5, 10);
    expect(f.pullRate).toBeCloseTo(1, 10);
    expect(f.skipRate).toBeCloseTo(1, 10);
    expect(f.rulesRate).toBeCloseTo(0.5, 10);
  });

  it("ignores every event that is not a gacha_ one", () => {
    const rows: GachaEventRow[] = [
      { name: "shot_taken", payload: { club: "driver" } },
      { name: "round_start", payload: {} },
      view("b1"),
    ];
    const f = buildGachaFunnel(rows);
    expect(f.views).toBe(1);
    expect(f.taps).toBe(0);
    expect(f.results).toBe(0);
  });

  it("returns null rates rather than zero when a denominator is empty", () => {
    // "No data" and "nobody converted" are different findings; rendering the
    // first as 0 % invents the second.
    const f = buildGachaFunnel([]);
    expect(f.tapRate).toBeNull();
    expect(f.pullRate).toBeNull();
    expect(f.skipRate).toBeNull();
    expect(f.insufficientRate).toBeNull();
    expect(f.rulesRate).toBeNull();
    expect(f.meanLatencyMs).toBeNull();
    expect(f.views).toBe(0);
  });

  it("names every refusal instead of lumping them into not-ok", () => {
    const rows = [
      tap("b1"),
      tap("b1"),
      tap("b1"),
      tap("b1"),
      result("b1", "ok"),
      result("b1", "insufficient"),
      result("b1", "cost_changed"),
      result("b1", "paused"),
    ];
    const f = buildGachaFunnel(rows);

    expect(f.results).toBe(4);
    expect(f.pulls).toBe(1);
    expect(f.byStatus).toEqual({ ok: 1, insufficient: 1, cost_changed: 1, paused: 1 });
    expect(f.insufficientRate).toBeCloseTo(0.25, 10);
    expect(f.pullRate).toBeCloseTo(0.25, 10);
  });

  it("counts a status this build has never heard of under unknown", () => {
    const f = buildGachaFunnel([result("b1", "some_future_status")]);
    expect(f.byStatus).toEqual({ some_future_status: 1 });
    expect(f.pulls).toBe(0);

    const missing = buildGachaFunnel([
      { name: "gacha_pull_result", payload: { banner_id: "b1", latency_ms: 5 } },
    ]);
    expect(missing.byStatus).toEqual({ unknown: 1 });
  });

  it("sums the rarity histogram, dupes and the two forced flags over ok results only", () => {
    const rows = [
      result("b1", "ok", {
        count: 10,
        rarities: [7, 2, 0, 1, 0, 0],
        dupes: 3,
        pity_forced: true,
        guarantee_forced: false,
      }),
      result("b1", "ok", {
        count: 10,
        rarities: [8, 1, 1, 0, 0, 0],
        dupes: 1,
        pity_forced: false,
        guarantee_forced: true,
      }),
      // A refusal carries none of these and must not contribute.
      result("b1", "insufficient", { rarities: [99, 99, 99, 99, 99, 99], dupes: 99 }),
    ];
    const f = buildGachaFunnel(rows);

    expect(f.rarities).toEqual([15, 3, 1, 1, 0, 0]);
    expect(f.dupes).toBe(4);
    expect(f.pityForced).toBe(1);
    expect(f.guaranteeForced).toBe(1);
    expect(f.pullsX10).toBe(2);
    expect(f.pullsX1).toBe(0);
  });

  it("means latency over EVERY answered pull, refusals included", () => {
    // A refusal that takes four seconds is still four seconds of the player's
    // life; excluding it would make the number describe the happy path only.
    const rows = [
      result("b1", "ok", { latency_ms: 100 }),
      result("b1", "insufficient", { latency_ms: 300 }),
    ];
    expect(buildGachaFunnel(rows).meanLatencyMs).toBeCloseTo(200, 10);
  });

  it("splits x1 from x10 and leaves a countless row out of both", () => {
    const rows = [
      result("b1", "ok", { count: 1 }),
      result("b1", "ok", { count: 10 }),
      result("b1", "ok", { count: 10 }),
      { name: "gacha_pull_result", payload: { banner_id: "b1", status: "ok" } },
    ];
    const f = buildGachaFunnel(rows);
    expect(f.pulls).toBe(4);
    expect(f.pullsX1).toBe(1);
    expect(f.pullsX10).toBe(2);
  });

  it("breaks the funnel down per banner, busiest first", () => {
    const rows = [
      view("quiet"),
      view("busy"),
      view("busy"),
      view("busy"),
      tap("busy"),
      result("busy", "ok"),
      tap("quiet"),
      result("quiet", "insufficient"),
    ];
    const f = buildGachaFunnel(rows);

    expect(f.perBanner).toEqual([
      { bannerId: "busy", views: 3, taps: 1, pulls: 1 },
      { bannerId: "quiet", views: 1, taps: 1, pulls: 0 },
    ]);
  });

  it("files a row with no banner_id under (unknown) instead of dropping it", () => {
    const f = buildGachaFunnel([{ name: "gacha_banner_view", payload: {} }]);
    expect(f.views).toBe(1);
    expect(f.perBanner).toEqual([{ bannerId: "(unknown)", views: 1, taps: 0, pulls: 0 }]);
  });

  it("counts distinct players, not rows", () => {
    const rows = [view("b1", "u1"), view("b1", "u1"), tap("b1", 1, "u2"), rules("b1", "u3")];
    expect(buildGachaFunnel(rows).players).toBe(3);
  });

  it("reads a payload that arrived as a JSON string", () => {
    const rows: GachaEventRow[] = [
      { name: "gacha_pull_result", payload: JSON.stringify({ banner_id: "b1", status: "ok", count: 10 }) },
    ];
    const f = buildGachaFunnel(rows);
    expect(f.pulls).toBe(1);
    expect(f.pullsX10).toBe(1);
  });

  it("survives a payload that is neither an object nor JSON", () => {
    const rows: GachaEventRow[] = [
      { name: "gacha_banner_view", payload: "not json at all" },
      { name: "gacha_banner_view", payload: null },
      { name: "gacha_banner_view" },
    ];
    const f = buildGachaFunnel(rows);
    expect(f.views).toBe(3);
    expect(f.perBanner[0]?.bannerId).toBe("(unknown)");
  });
});
