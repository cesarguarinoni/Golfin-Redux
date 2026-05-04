# NOTES — `controls_c_fix` (queued, spec to be written 2026-05-05)

> Pre-spec working notes for the C.1 + C.2 fix. The full spec writes when Cesar kicks off the next session. **Do not hand this to the implementer** — these are architect-only working notes. A polished SPEC.md will replace this file.

## Inputs

- Diagnosis report: `Docs/Specs/Completed/controls_c_diagnosis/IMPLEMENTER_REPORT.md`
- Architect review: `Docs/Specs/Completed/controls_c_diagnosis/ARCHITECT_REVIEW.md`
- Code references walked 2026-05-04 (Architect):
  - `Assets/Scripts/Physics/Core/BallSimulation.cs:537-552` — Roll-phase stop check (two-clause)
  - `Assets/Scripts/Physics/Core/BallSimulation.cs:670-687` — Putt-phase stop check (identical structure to Roll)
  - `Assets/Scripts/Physics/Core/BallSimulation.cs:565-580` — `IsPutt` gate (speed/angle/surface)
  - `Assets/Scripts/Physics/Core/BallSimulation.cs:582-583` — `IsPuttSurface` helper (Green / GreenCollar only)
  - `Assets/Scripts/Physics/Core/BallSimulation.cs:619-621` — `RunPuttPhase` uses `puttCfg[surface]` only when `IsPuttSurface`; falls back to `surfaceCfg[surface]` for any non-putt-eligible surface (Fairway, Rough, Sand, Tee, etc.)
  - `Assets/Resources/Physics/surfaces.csv` — 11 surfaces, current k values: Fairway 0.18, Green 0.12, GreenCollar 0.15, Semirough 0.28, Rough 0.45, Tee 0.15, Sand 0.70, BunkerLip 0.55, CartPath 0.06, Water 1.00, OOB 0.50
  - `Assets/Resources/Physics/putt.csv` — Green k=0.10 stopSpeed=0.04, GreenCollar k=0.14 stopSpeed=0.05

## Diagnosis recap (from captures)

C.1 ("putter shoots ~100yd") **DOES NOT REPRODUCE as a velocity-resolution bug.** Putter pipeline is correct end-to-end: override 5 m/s, IsPutt=True, Putter.HasValue=True, Green origin classification, all gate clauses pass. Captured `velMagnitude=2.05 m/s` at 41% effort.

The "100 yd" symptom is rolling-resistance integration. Asymptotic distance for `dv/dt = -k·v` is `d_max = v₀/k`:

| Surface | k | v₀ at entry | d_max |
|---|---|---|---|
| Green | 0.10 | 2.0 m/s | 20 m |
| Fairway (after green→fairway transition) | 0.18 | ~1.7 m/s | 9.4 m |
| CartPath (driver shot iter 2) | 0.06 | residual | huge |

Shot 1 captured displacement: **17.30 m for a 41% putt** (sqrt(16.88² + 3.85²)). Architect re-derivation of analytic `d_max ≤ 12.4 m` under-predicts the observed 17.3 m by ~5 m due to fp-precision floor in the integrator (visible in log: `[PuttStep] t=0.500s |v|=2.0000m/s` — should have decayed to ~1.90 by then; fp rounding preserves it).

**→ The fix tunes `surfaces.csv` + `putt.csv` k values, validated by integrator-based unit tests, NOT by analytic `d_max=v/k`.** Architect note #2 from review.

C.2 ("rolls forever") root cause: **`stopConsecutive` clause 2 (`speedSq <= prevSpeedSq`) intermittently fails on real heightmap.** At very low speed, `|gTan|` rounds to 0.000 in F3-print but underlying fp value can be up to 0.0005 m/s² (sub-mm slope). The resistance term `−k·v·Dt` at v=0.0625 m/s and k=0.18 is `−0.0028 m/s/step`; slope-tangent re-acceleration of 0.0005 m/s² × Dt = 0.0021 m/s/step. Net: vel barely decreases each step, intermittently increases, and clause 2 fails ~98% of the time on Shot 1 (counter went 0→8 over 336 steps) and 100% of the time on Shot 2 (stuck at 0 for 75s).

Don't blindly delete clause 2. It guards against "ball rolling uphill should not count as stopping" — strictly necessary on real terrain. Three repair candidates (architect picks the right one when writing SPEC.md):

1. **Drop clause 2 entirely.** Count by clause 1 only (`speedSq < stopThresh`). Risk: ball rolling uphill at sub-stopSpeed gets credited as stopped even though it's about to roll back down. Probably wrong.
2. **Tolerance window.** `speedSq <= prevSpeedSq + epsilon` for some `epsilon = stopSpeed²·0.01` — counts as "stopped" if speed isn't *meaningfully* increasing. Likely correct. Need to reason about the epsilon value.
3. **Two-stage stop.** Clause 1 alone for a longer required-step count (e.g. 30 steps = 0.125s at sub-stopSpeed regardless of clause 2). Add a separate slope-aware override: if `|gTan| > stopSpeed·k` (i.e. terminal velocity > stopSpeed), the ball is on a slope steep enough to never terminate at stopSpeed — treat differently or raise stopSpeed for that step.

**Lean toward option 2** unless steep-slope analysis says otherwise. Spec writing tomorrow.

## Three concerns the SPEC.md must address

1. **CSV tuning.** New k values for `surfaces.csv` + `putt.csv` validated by an integrator-based test. Initial proposed bands (subject to revision after running the test):
   - Green k 0.10 → 0.45 (target: 2 m/s putt rolls 2.5–3.5m)
   - GreenCollar k 0.14 → 0.55
   - Fairway k 0.18 → 0.55 (target: residual 1 m/s rolls 0.8–1.5m)
   - Rough k 0.45 → 1.20
   - Semirough k 0.28 → 0.85
   - Tee k 0.15 → 0.50
   - Sand: leave at 0.70 (already plays sticky)
   - CartPath k 0.06 → ??? Keep low (cart paths are bouncy/slick) but raise enough to stop within 30s.
   - Water/OOB: terminal surfaces, k irrelevant
2. **Stop-check repair.** Pick option 1 / 2 / 3 above. Apply to BOTH `RunRollPhase` (537-552) and `RunPuttPhase` (670-687) — identical fixes; they're literal copies of each other.
3. **Integrator-based validation test.** New EditMode test: load `surfaces.csv` + `putt.csv` → fire 2 m/s putt on Green flat ground at origin → assert final-roll-distance in target band. One per playable surface (Green/GreenCollar/Fairway/Rough/Semirough). 5 new tests; bit-exact gate goes from 198 → 203.

## Out of scope (carry forward to next specs)

- **64 m/s velocity cap** — Notion entry C.5 (`35631e0e-9a36-8133-9734-d5b4418db9f6`). Separate diagnostic micro-spec.
- **C.3 / C.4 picker rules** — Notion entries `35531e0e-9a36-811b-b5a6-c93e62e3ef25` (force putter on green) and `35531e0e-9a36-81a4-9060-d1602ee11b5d` (block putter off green). Wait until C.1+C.2 fix lands so the surface read used by both rules is settled.

## Pipeline tier guess

**Tier 3 (full pipeline).** Touches `BallSimulation.cs` (the bit-exact-gate file) and adds new EditMode tests. Visual fidelity not at stake but spatial-math + bit-exact gate are. More eyes, no fan-out (single file does most of the work).

## Open questions for Cesar before writing SPEC.md

1. **Stop-check repair option 1 / 2 / 3?** I'll pick option 2 unless Cesar wants to weigh in — it's the closest to "what the original code intended" without dropping safety.
2. **CSV tuning source of truth.** Cesar specced these original k values — are the targets above (2.5–3.5m for a 2 m/s putt) the right band, or does Cesar want a different feel (e.g. Stimp 10 vs 12)?
3. **Should the fix include both CSV files (surfaces.csv + putt.csv)?** Putt.csv is only used for Green/GreenCollar in `RunPuttPhase`; everything else falls back to surfaces.csv. Both need new values.
