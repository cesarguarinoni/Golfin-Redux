/**
 * The odds audit and the pull-log CSV (gacha_server_pull §6, §7).
 *
 * WHY THESE TWO AND NOTHING ELSE FROM THE PANEL. Everything else in the Gacha
 * panel is either a Supabase read or React, and the suite covers the pure
 * modules only (ADMIN_DASHBOARD_OPS §2.x). These two are pure AND load-bearing:
 *
 *   * The odds audit is the panel's ANSWER to "is the gacha paying what it
 *     published". If it counted forced slots it would flag every working
 *     pity banner, an operator would learn to ignore the amber, and the audit
 *     would be worse than not having one.
 *   * The CSV is what leaves the building. Its column list is a contract with
 *     whatever spreadsheet someone builds on it.
 */

import { describe, expect, it } from "vitest";
import {
  auditOdds,
  ODDS_SIGNIFICANCE_SLOTS,
  PULL_CSV_COLUMNS,
  pullsToCsv,
  type AuditPrize,
  type AuditPull,
} from "../gachaAudit";

// The seeded pool_standard_club1 rate table (2026_08_31_content_gacha_seed).
const PUBLISHED = {
  Common: 5500,
  Uncommon: 2500,
  Rare: 1200,
  Mythic: 550,
  Legendary: 200,
  Supreme: 50,
};

function pull(id: string, over: Partial<AuditPull> = {}): AuditPull {
  return {
    id,
    bannerId: "banner_standard_club1",
    poolId: "pool_standard_club1",
    pullCount: 1,
    pityForced: false,
    guaranteeForced: false,
    createdAt: "2026-09-01T10:00:00Z",
    ...over,
  };
}

function prize(pullId: string, slot: number, rarity: string): AuditPrize {
  return {
    pullId,
    slot,
    rarity,
    kind: "club",
    refId: `club_${rarity.toLowerCase()}`,
    isDupe: false,
    dupeRp: 0,
  };
}

/** N single pulls, all of `rarity`, none forced. */
function unforced(n: number, rarity: string, offset = 0) {
  const pulls: AuditPull[] = [];
  const map = new Map<string, AuditPrize[]>();
  for (let i = 0; i < n; i += 1) {
    const id = `p${offset + i}`;
    pulls.push(pull(id));
    map.set(id, [prize(id, 0, rarity)]);
  }
  return { pulls, map };
}

describe("auditOdds — the comparison", () => {
  it("reports every ladder tier, including ones that never dropped", () => {
    const { pulls, map } = unforced(10, "Common");
    const audit = auditOdds(pulls, map, PUBLISHED);
    expect(audit.tiers.map((t) => t.rarity)).toEqual([
      "Common", "Uncommon", "Rare", "Mythic", "Legendary", "Supreme",
    ]);
    expect(audit.tiers.find((t) => t.rarity === "Supreme")?.observed).toBe(0);
  });

  it("computes the observed share over COMPARABLE slots, not over pulls", () => {
    const a = unforced(30, "Common", 0);
    const b = unforced(10, "Rare", 100);
    const audit = auditOdds([...a.pulls, ...b.pulls], new Map([...a.map, ...b.map]), PUBLISHED);

    expect(audit.comparableSlots).toBe(40);
    expect(audit.tiers.find((t) => t.rarity === "Common")?.observedPct).toBe(75);
    expect(audit.tiers.find((t) => t.rarity === "Rare")?.observedPct).toBe(25);
  });

  it("expresses the delta in percentage POINTS against the published bp", () => {
    const { pulls, map } = unforced(100, "Rare");
    const audit = auditOdds(pulls, map, PUBLISHED);
    const rare = audit.tiers.find((t) => t.rarity === "Rare")!;
    // published 1200 bp = 12 %, observed 100 %.
    expect(rare.publishedPct).toBe(12);
    expect(rare.deltaPt).toBe(88);
  });

  it("surfaces a tier that PAID OUT with no published rate rather than dropping it", () => {
    const { pulls, map } = unforced(1200, "Supreme");
    const audit = auditOdds(pulls, map, { Common: 10000 });
    const supreme = audit.tiers.find((t) => t.rarity === "Supreme")!;
    expect(supreme.publishedPct).toBe(0);
    expect(supreme.observedPct).toBe(100);
    expect(supreme.amber).toBe(true);
  });

  it("is empty-safe", () => {
    const audit = auditOdds([], new Map(), PUBLISHED);
    expect(audit.comparableSlots).toBe(0);
    expect(audit.significant).toBe(false);
    expect(audit.tiers.every((t) => t.observedPct === 0)).toBe(true);
    expect(audit.tiers.every((t) => t.amber === false)).toBe(true);
  });
});

describe("auditOdds — forced slots are EXCLUDED, and that is the whole point", () => {
  it("drops the pity slot of a x1 pity pull", () => {
    const pulls = [pull("p1", { pityForced: true })];
    const map = new Map([["p1", [prize("p1", 0, "Legendary")]]]);
    const audit = auditOdds(pulls, map, PUBLISHED);

    expect(audit.comparableSlots).toBe(0);
    expect(audit.forcedSlots).toBe(1);
    expect(audit.pityPulls).toBe(1);
    expect(audit.tiers.find((t) => t.rarity === "Legendary")?.observed).toBe(0);
  });

  it("drops ONE slot of a forced x10 and keeps the other nine", () => {
    const prizes = [
      ...Array.from({ length: 9 }, (_, i) => prize("p1", i, "Common")),
      prize("p1", 9, "Rare"),
    ];
    const pulls = [pull("p1", { pullCount: 10, guaranteeForced: true })];
    const audit = auditOdds(pulls, new Map([["p1", prizes]]), PUBLISHED);

    expect(audit.comparableSlots).toBe(9);
    expect(audit.forcedSlots).toBe(1);
    expect(audit.guaranteePulls).toBe(1);
    // The RARE slot is the one dropped — the guarantee only fires when nothing
    // else reached the floor, so the re-rolled slot is the best in the block.
    expect(audit.tiers.find((t) => t.rarity === "Rare")?.observed).toBe(0);
    expect(audit.tiers.find((t) => t.rarity === "Common")?.observed).toBe(9);
  });

  it("drops TWO slots when a x10 was both pity-forced and guarantee-forced", () => {
    const prizes = [
      ...Array.from({ length: 8 }, (_, i) => prize("p1", i, "Common")),
      prize("p1", 8, "Legendary"),
      prize("p1", 9, "Mythic"),
    ];
    const pulls = [pull("p1", { pullCount: 10, pityForced: true, guaranteeForced: true })];
    const audit = auditOdds(pulls, new Map([["p1", prizes]]), PUBLISHED);

    expect(audit.comparableSlots).toBe(8);
    expect(audit.forcedSlots).toBe(2);
    // The two HIGHEST are dropped, not the two last.
    expect(audit.tiers.find((t) => t.rarity === "Legendary")?.observed).toBe(0);
    expect(audit.tiers.find((t) => t.rarity === "Mythic")?.observed).toBe(0);
    expect(audit.tiers.find((t) => t.rarity === "Common")?.observed).toBe(8);
  });

  it("does not mutate the caller's prize array (it is the reveal order)", () => {
    const prizes = [prize("p1", 0, "Common"), prize("p1", 1, "Supreme")];
    const before = prizes.map((p) => p.slot);
    auditOdds([pull("p1", { pullCount: 10, pityForced: true })], new Map([["p1", prizes]]), PUBLISHED);
    expect(prizes.map((p) => p.slot)).toEqual(before);
  });

  it("a banner whose Legendaries come ENTIRELY from pity does not read as over-paying", () => {
    // 1 000 honest Commons + 40 pity-forced Legendaries. Folding the forced
    // slots in would put Legendary at ~3.8 % against a published 2 % and paint
    // the row amber — flagging a banner that is working exactly as designed.
    const honest = unforced(1000, "Common", 0);
    const forced: AuditPull[] = [];
    const forcedMap = new Map<string, AuditPrize[]>();
    for (let i = 0; i < 40; i += 1) {
      const id = `f${i}`;
      forced.push(pull(id, { pityForced: true }));
      forcedMap.set(id, [prize(id, 0, "Legendary")]);
    }

    const audit = auditOdds(
      [...honest.pulls, ...forced],
      new Map([...honest.map, ...forcedMap]),
      PUBLISHED
    );
    const legendary = audit.tiers.find((t) => t.rarity === "Legendary")!;
    expect(legendary.observed).toBe(0);
    expect(legendary.amber).toBe(false);
    expect(audit.forcedSlots).toBe(40);
  });
});

describe("auditOdds — amber", () => {
  it("stays off below the significance floor however big the delta", () => {
    const { pulls, map } = unforced(ODDS_SIGNIFICANCE_SLOTS - 1, "Supreme");
    const audit = auditOdds(pulls, map, PUBLISHED);
    expect(audit.significant).toBe(false);
    expect(audit.tiers.every((t) => t.amber === false)).toBe(true);
  });

  it("fires above the floor when the delta exceeds tolerance", () => {
    const { pulls, map } = unforced(ODDS_SIGNIFICANCE_SLOTS, "Supreme");
    const audit = auditOdds(pulls, map, PUBLISHED);
    expect(audit.significant).toBe(true);
    // Supreme published at 0.5 %, observed 100 %.
    expect(audit.tiers.find((t) => t.rarity === "Supreme")?.amber).toBe(true);
    // Common published at 55 %, observed 0 % — also beyond tolerance.
    expect(audit.tiers.find((t) => t.rarity === "Common")?.amber).toBe(true);
  });

  it("does not fire on a delta inside tolerance", () => {
    // 1 000 slots: 550 Common, 250 Uncommon, 120 Rare, 55 Mythic, 20 Legendary,
    // 5 Supreme — exactly the published table.
    const parts = [
      ["Common", 550], ["Uncommon", 250], ["Rare", 120],
      ["Mythic", 55], ["Legendary", 20], ["Supreme", 5],
    ] as const;
    const pulls: AuditPull[] = [];
    const map = new Map<string, AuditPrize[]>();
    let n = 0;
    for (const [rarity, count] of parts) {
      for (let i = 0; i < count; i += 1) {
        const id = `x${n++}`;
        pulls.push(pull(id));
        map.set(id, [prize(id, 0, rarity)]);
      }
    }
    const audit = auditOdds(pulls, map, PUBLISHED);
    expect(audit.comparableSlots).toBe(1000);
    expect(audit.tiers.every((t) => t.amber === false)).toBe(true);
    expect(audit.tiers.every((t) => Math.abs(t.deltaPt) < 0.001)).toBe(true);
  });
});

describe("pullsToCsv", () => {
  const row = {
    id: "pull-1",
    createdAt: "2026-09-01T10:00:00Z",
    userEmail: "alice@example.com",
    userId: "u1",
    bannerId: "banner_standard_club1",
    poolId: "pool_standard_club1",
    pullCount: 10,
    ticketType: 0,
    cost: 450,
    pityForced: false,
    guaranteeForced: true,
    prizes: [
      { slot: 0, kind: "club", refId: "club_driver_gf", quantity: 1, rarity: "Common", isDupe: true, dupeRp: 20 },
      { slot: 1, kind: "ball", refId: "ball_golfin", quantity: 3, rarity: "Common", isDupe: false, dupeRp: 0 },
    ],
  };

  it("writes the header in the declared column order", () => {
    const [header] = pullsToCsv([row]).split("\n");
    expect(header).toBe(PULL_CSV_COLUMNS.map((c) => `"${c}"`).join(","));
  });

  it("writes ONE ROW PER PRIZE, repeating the pull-level fields", () => {
    const lines = pullsToCsv([row]).trim().split("\n");
    expect(lines).toHaveLength(3); // header + 2 prizes
    expect(lines[1]).toContain('"pull-1"');
    expect(lines[2]).toContain('"pull-1"');
    expect(lines[1]).toContain('"club_driver_gf"');
    expect(lines[2]).toContain('"ball_golfin"');
  });

  it("still emits a line for a pull with no prizes, so it cannot be lost silently", () => {
    const lines = pullsToCsv([{ ...row, prizes: [] }]).trim().split("\n");
    expect(lines).toHaveLength(2);
    expect(lines[1]).toContain('"pull-1"');
  });

  const onePrize = row.prizes.slice(0, 1);

  it("quotes every cell and doubles embedded quotes", () => {
    const csv = pullsToCsv([
      { ...row, userEmail: 'we"ird, name@example.com', prizes: onePrize },
    ]);
    expect(csv).toContain('"we""ird, name@example.com"');
  });

  it("renders a null email as an empty cell, never the string null", () => {
    const csv = pullsToCsv([{ ...row, userEmail: null, prizes: onePrize }]);
    expect(csv).not.toContain("null");
    expect(csv.split("\n")[1]).toContain(',"",');
  });

  it("ends with a newline so two exports can be concatenated", () => {
    expect(pullsToCsv([row]).endsWith("\n")).toBe(true);
  });

  it("is header-only for an empty export", () => {
    expect(pullsToCsv([])).toBe(PULL_CSV_COLUMNS.map((c) => `"${c}"`).join(",") + "\n");
  });
});
