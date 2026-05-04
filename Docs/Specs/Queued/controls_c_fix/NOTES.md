# NOTES — `controls_c_fix` (queued, spec to be written 2026-05-05)

> Pre-spec working notes for the C.1 + C.2 fix. The full spec writes when Cesar kicks off the next session. **Do not hand this to the implementer** — these are architect-only working notes. A polished SPEC.md will replace this file.
>
> **Updated 2026-05-04 18:40 JST** with realism check after Cesar's question about whether 17.3m for a 41% putt is realistic. Short answer: yes — full-power putter ≈ 40m per real golf, so 41% ≈ 16m is in the right ballpark. The "rolls forever" symptom is purely the stop-check bug, not bad distances. CSV tuning recommendation downscoped accordingly.

## Inputs

- Diagnosis report: `Docs/Specs/Completed/controls_c_diagnosis/IMPLEMENTER_REPORT.md`
- Architect review: `Docs/Specs/Completed/controls_c_diagnosis/ARCHITECT_REVIEW.md`
- Code references walked 2026-05-04 (Architect):
  - `Assets/Scripts/Physics/Core/BallSimulation.cs:537-552` — Roll-phase stop check (two-clause)
  - `Assets/Scripts/Physics/Core/BallSimulation.cs:670-687` — Putt-phase stop check (identical structure to Roll)
  - `Assets/Scripts/Physics/Core/BallSimulation.cs:565-580` — `IsPutt` gate (speed/angle/surface)
  - `Assets/Scripts/Physics/Core/BallSimulation.cs:582-583` — `IsPuttSurface` helper (Green / GreenCollar only)
  - `Assets/Scripts/Physics/Core/BallSimulation.cs:619-621` — `RunPuttPhase` uses `puttCfg[surface]` only when `IsPuttSurface`; falls back to `surfaceCfg[surface]` for any non-putt-eligible surface
  - `Assets/Resources/Physics/surfaces.csv` — 11 surfaces, current k values: Fairway 0.18, Green 0.12, GreenCollar 0.15, Semirough 0.28, Rough 0.45, Tee 0.15, Sand 0.70, BunkerLip 0.55, CartPath 0.06, Water 1.00, OOB 0.50
  - `Assets/Resources/Physics/putt.csv` — Green k=0.10 stopSpeed=0.04, GreenCollar k=0.14 stopSpeed=0.05

## Realism check (2026-05-04 18:40 JST update)

**Cesar's question:** Is 17.3m for a 41% putt realistic? Does a full-power putter do ~40m?

**Real-world numbers (from web search):**
- Full-power putter shot maxes at **30-40 yards (27-37m)** per The Club Washer
- PGA Tour green Stimpmeter readings: **10.5-12** (Stimp 12 = ball released at 1.83 m/s rolls 3.66m on a flat green)
- Major championship greens: 13-15

**Captured data vs real golf:**

| Metric | Real golf | Our captured data | Verdict |
|---|---|---|---|
| Full-power putter max distance | ~40m | extrapolated: 5 m/s ÷ 0.41 × 17.3m ≈ **42m** | ✓ realistic |
| 41% power putter | linear scale: ~16m | **17.30m** | ✓ realistic |
| Stop time | ~5-10s | **21+s and still rolling** | ✗ broken (stop check) |
| Stimpmeter equivalence (1.83 m/s on Green) | 3.66m on Stimp 12 | analytic d=v/k=18.3m on our Green k=0.10 | ✗ Green too slow |

**Conclusion:** Distances at the upper end (full-power putter, full driver) are realistic. **The "rolls forever" problem is purely the stop-check bug** — proportional resistance `dv/dt = -k·v` asymptotes to zero, never reaches zero, so without a working stop check the ball rolls indefinitely.

The Stimpmeter mismatch is real (Green k=0.10 is way too low for PGA Tour feel) but it's a "putts feel mushy" issue, not a "ball goes too far" issue. A 2 m/s putt on Stimp 12 should travel ~4m — ours travels 18m+. **Worth tuning Green/GreenCollar k upward**, but not by enough to break the realistic full-power total-travel of 40m (because most of those 40m happen off the green on Fairway anyway).

CartPath k=0.06 is a clear outlier — real cart paths are bouncy concrete that balls mostly bounce off; our k=0.06 lets the ball roll forever on them. Driver Shot 2 captured 296m total with the last ~100m being slow CartPath roll. **Bump CartPath k significantly.**

Other surfaces (Fairway/Rough/Sand/Tee/Semirough) — leave alone. They produce realistic total-travel distances when paired with corrected Green and CartPath values. Verify via test before touching anything else.

## Diagnosis recap (from captures, unchanged)

C.1 ("putter shoots ~100yd") **DOES NOT REPRODUCE as a velocity-resolution bug.** Putter pipeline is correct end-to-end: override 5 m/s, IsPutt=True, Putter.HasValue=True, Green origin classification, all gate clauses pass. Captured `velMagnitude=2.05 m/s` at 41% effort.

The "100 yd" symptom was actually Cesar seeing the ball still rolling after several seconds and assuming something was very wrong with launch velocity. In reality, the launch was correct and the ball was on a slow-decay trajectory toward an asymptotic 17m total.

C.2 ("rolls forever") root cause: **`stopConsecutive` clause 2 (`speedSq <= prevSpeedSq`) intermittently fails on real heightmap.** At very low speed, `|gTan|` rounds to 0.000 in F3-print but underlying fp value can be up to 0.0005 m/s² (sub-mm slope). The resistance term `−k·v·Dt` at v=0.0625 m/s and k=0.18 is `−0.0028 m/s/step`; slope-tangent re-acceleration of 0.0005 m/s² × Dt = 0.0021 m/s/step. Net: vel barely decreases each step, intermittently increases, and clause 2 fails ~98% of the time on Shot 1 (counter went 0→8 over 336 steps) and 100% of the time on Shot 2 (stuck at 0 for 75s).

Don't blindly delete clause 2. It guards against "ball rolling uphill should not count as stopping" — strictly necessary on real terrain. Three repair candidates considered:

1. **Drop clause 2 entirely.** Count by clause 1 only (`speedSq < stopThresh`). Risk: ball rolling uphill at sub-stopSpeed gets credited as stopped even though it's about to roll back down. Probably wrong.
2. **Tolerance window.** `speedSq <= prevSpeedSq + epsilon` for some `epsilon = stopSpeed²·0.01` — counts as "stopped" if speed isn't *meaningfully* increasing. **CHOSEN** — closest to original intent without dropping safety. Architect-picked option 2 per Cesar's "you pick the best" 2026-05-04.
3. **Two-stage stop.** Clause 1 alone for a longer required-step count; add slope-aware override. Overkill for the symptom.

## Three concerns the SPEC.md must address (revised)

1. **Stop-check repair.** Apply option 2 (tolerance window) to BOTH `RunRollPhase` (537-552) and `RunPuttPhase` (670-687) — identical fixes; they're literal copies of each other. Epsilon: `coeff.StopSpeed * coeff.StopSpeed * fp.FromFloat(0.01f)` (1% of stopSpeed²). Conservative; only triggers on truly-stopping balls, not actively-accelerating ones.

2. **CSV tuning — minimal, only where realism breaks:**
   - **`putt.csv` Green k: 0.10 → 0.50** (matches Stimp 12 PGA Tour standard: 1.83 m/s release rolls 3.66m).
   - **`putt.csv` GreenCollar k: 0.14 → 0.40** (slightly slower than green; matches Stimp ~10).
   - **`surfaces.csv` CartPath k: 0.06 → 0.30** (real cart paths are slick concrete but stop balls within 30s of contact; current 0.06 is essentially frictionless).
   - **Leave alone:** Fairway 0.18, Rough 0.45, Semirough 0.28, Tee 0.15, Sand 0.70, BunkerLip 0.55. These produce realistic total-travel distances when paired with the Green/CartPath fixes. **Verify via test before any further change.**

3. **Integrator-based validation tests.** New EditMode tests asserting realistic targets:
   - **Stimpmeter test:** 1.83 m/s ball on flat Green at origin rolls 3.0–4.5m (Stimp 10–12 band).
   - **Long-putt test:** 5 m/s putter shot on Green→Fairway transition, total roll ≤ 45m.
   - **Driver fairway roll-out:** Driver landing on Fairway from height, rolls 15–30m after first ground contact.
   - **CartPath stop:** Driver landing on CartPath from height, terminates in ≤ 5m of roll-out.
   - **Stop-check correctness:** All test sims terminate within 0.5s of `|v| < stopSpeed` (asserts the stop-check actually fires).

5 new tests; bit-exact gate goes from 198 → 203.

## Out of scope (carry forward to next specs)

- **64 m/s velocity cap** — Notion entry C.5 (`35631e0e-9a36-8133-9734-d5b4418db9f6`). Separate diagnostic micro-spec.
- **C.3 / C.4 picker rules** — Notion entries `35531e0e-9a36-811b-b5a6-c93e62e3ef25` (force putter on green) and `35531e0e-9a36-81a4-9060-d1602ee11b5d` (block putter off green). Wait until C.1+C.2 fix lands so the surface read used by both rules is settled.

## Pipeline tier guess

**Tier 3 (full pipeline).** Touches `BallSimulation.cs` (the bit-exact-gate file) and adds new EditMode tests. Visual fidelity not at stake but spatial-math + bit-exact gate are. More eyes, no fan-out (single file does most of the work).

## Three open questions for Cesar before writing SPEC.md

(Updated 2026-05-04 18:40 JST after the realism check.)

1. **Green k: 0.10 → 0.50** to match Stimpmeter standard (PGA Tour Stimp 12 = ball at 1.83 m/s rolls 3.66m). Yes / no / different target Stimp? Default if no answer: yes (Stimp 12 PGA Tour feel).
2. **CartPath k: 0.06 → 0.30** so the ball actually stops on cart paths instead of skating forever. Yes / no? Default if no answer: yes.
3. **Leave Fairway/Rough/Sand/Tee/Semirough alone** — they produce realistic total-travel distances when combined with the Green/CartPath tuning above. Verify via test, no preemptive change. Yes / no? Default if no answer: yes (validate first, tune later if tests show drift from real-golf bands).

Stop-check repair choice (option 2, tolerance window) **already chosen** per Cesar's 2026-05-04 "you pick the best" instruction.
