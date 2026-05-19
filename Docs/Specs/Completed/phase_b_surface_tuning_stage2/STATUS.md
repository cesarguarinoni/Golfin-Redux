# STATUS — `phase_b_surface_tuning` Stage 2

**Closed:** 2026-05-19 (REVERTED + filed Stage 3 stub)
**Outcome:** `CLOSED — architectural finding delivered, k edits reverted`

## What shipped

- Architectural finding: `rolling_resistance` only affects pure-roll tail. Total horizontal carry from first contact is dominated by bounce/skid phase, controlled by `tangent_friction` + `restitution`. The Stage 2 SPEC's back-solve `k_new = k_cur × (d_obs/d_target)` assumed `d ∝ 1/k` (pure-roll model) — doesn't match what the harness measures.
- Confirmed via pre/post comparison: k bumps of +28% / +183% / +46% produced roll changes of −0.2% / −1.9% / −6.6% at v=25. At v=12–15 (real approach landing speed) effect was 0.0%–0.6%. Cesar would not feel it.
- Same shape as Stage 2 SPEC §Caveats #5 GreenCollar putt-k inversion — variable name suggests one thing, model dominated by something else. Pattern in the codebase.

## What was reverted

`surfaces.csv`:
- Fairway 0.23 → **0.18** (back to pre-Stage-2)
- Green 0.34 → **0.12** (back)
- Sand 1.02 → **0.70** (back)

Stimpmeter putt unchanged through entire experiment (3.5333 m, deterministic). Test gate stayed 294/294.

## What's filed for later (NOT in flight)

Stub: `Docs/Specs/Queued/phase_b_stage3_total_horizontal_carry/NOTES.md`. Primary knobs `tangent_friction` + `restitution`, redesigned back-solve model that accounts for bounce sequence. Tier 3. Picks up after Loop v2 ships.

## Cesar's call (2026-05-19)

"B, but we're running in circles and overcomplicating the math. Come back to polish later instead of being stuck in the Loop forever."

Loop v2 is the priority. Phase B fully closed.
