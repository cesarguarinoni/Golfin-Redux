DONE

map_view_v2 — approved by Cesar 2026-09-07 after five review rounds.

The B1 overhead map ships: flat dotted terrain-hugging aim line with 50-yd ticks, lime range fan
whose outer arc IS max reach (1.2 x club carry, ShotController's overpower ceiling), lime landing
glow + white ring + crosshair, HUD-style target readout, the REAL in-game hole indicator standing
over the pin, and SHOT VIEW in the bottom-left. The over-range state is DRAWN, never applied —
MapTargetCarryM was measured written back unclamped.

Code landed in 547b48a9c (build sweep). Evidence: IMPLEMENTER_REPORT.md rounds 1-5;
12 invariant dumps; 24 captures across Holes 01/04/08 (screenshots/ is gitignored, on disk only).
