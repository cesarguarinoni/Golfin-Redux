import { describe, expect, it } from "vitest";

/**
 * The `golfin_mode_fees` row mapping — the shape `mirrorModeFees` writes, and
 * the shape `fetchVersionSnapshot` reads back off a version snapshot.
 *
 * This is the code the red-team's iter-1 blocker was about: the mirror is what
 * `/points/spend` prices a mode entry against, so a mapping that writes the
 * wrong number charges players the wrong number. Same characterisation-test
 * caveat as rewardsValidation.test.ts — both functions live in `server-only`
 * modules, so the RULES are pinned here and the live behaviour is evidenced by
 * the prod rollback reproduction in IMPLEMENTER_REPORT (publish 12 -> rollback
 * -> mirror 10, audit `{"mirrored": true}`).
 */

type Row = { rowId: string; data: Record<string, unknown>; isActive: boolean };

/** Mirrors the row mapping in `mirrorModeFees` (lib/contentMutations.ts). */
function toMirrorRows(rows: Row[]) {
  return rows
    .filter((r) => r.isActive)
    .map((r) => ({
      mode_id: r.rowId,
      entry_fee: Math.max(0, Math.trunc(Number(r.data.entryFee) || 0)),
      is_locked:
        String(r.data.locked ?? "").trim().toLowerCase() === "true" ||
        String(r.data.locked ?? "").trim() === "1",
    }));
}

const row = (rowId: string, data: Record<string, unknown>, isActive = true): Row => ({ rowId, data, isActive });

describe("what a modes publish writes into golfin_mode_fees", () => {
  it("carries the fee across as an integer", () => {
    expect(toMirrorRows([row("practice", { entryFee: "10", locked: "false" })])).toEqual([
      { mode_id: "practice", entry_fee: 10, is_locked: false },
    ]);
  });

  it("reads `locked` the way the CLIENT does — true/1, case-insensitive", () => {
    // ContentFields.GetBool accepts "true" (any case) and "1". If the mirror and
    // the client disagreed here, a Coming Soon card would be enterable, or a
    // live one refused as mode_locked.
    for (const locked of ["true", "TRUE", "True", "1"]) {
      expect(toMirrorRows([row("m", { entryFee: "0", locked })])[0]!.is_locked).toBe(true);
    }
    for (const locked of ["false", "FALSE", "0", "", "nonsense"]) {
      expect(toMirrorRows([row("m", { entryFee: "0", locked })])[0]!.is_locked).toBe(false);
    }
  });

  it("never writes a negative or fractional fee", () => {
    // The validator already refuses these and the table has its own check
    // constraint; this is the third layer, and the one that decides what a
    // blank cell means.
    expect(toMirrorRows([row("m", { entryFee: "-5" })])[0]!.entry_fee).toBe(0);
    expect(toMirrorRows([row("m", { entryFee: "7.9" })])[0]!.entry_fee).toBe(7);
    expect(toMirrorRows([row("m", { entryFee: "" })])[0]!.entry_fee).toBe(0);
    expect(toMirrorRows([row("m", {})])[0]!.entry_fee).toBe(0);
  });

  it("EXCLUDES deactivated rows rather than mirroring them as free", () => {
    // Deactivation withdraws a mode (I6). Mirroring it at 0 would make a
    // withdrawn mode free to enter; leaving its old row means the server keeps
    // refusing at the last known price.
    const out = toMirrorRows([
      row("live", { entryFee: "10" }),
      row("withdrawn", { entryFee: "10" }, false),
    ]);
    expect(out.map((r) => r.mode_id)).toEqual(["live"]);
  });

  it("maps every shipped mode exactly as prod holds them", () => {
    // Read off golfin_mode_fees on 2026-08-28.
    const shipped = [
      row("practice", { entryFee: "10", locked: "false" }),
      row("versus_1v1", { entryFee: "0", locked: "false" }),
      row("tournaments", { entryFee: "0", locked: "false" }),
      row("driving_range", { entryFee: "0", locked: "true" }),
      row("missions", { entryFee: "0", locked: "true" }),
    ];
    expect(toMirrorRows(shipped)).toEqual([
      { mode_id: "practice", entry_fee: 10, is_locked: false },
      { mode_id: "versus_1v1", entry_fee: 0, is_locked: false },
      { mode_id: "tournaments", entry_fee: 0, is_locked: false },
      { mode_id: "driving_range", entry_fee: 0, is_locked: true },
      { mode_id: "missions", entry_fee: 0, is_locked: true },
    ]);
  });
});
