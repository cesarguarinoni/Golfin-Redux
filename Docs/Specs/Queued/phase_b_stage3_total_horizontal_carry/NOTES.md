# Phase B Stage 3 — Total horizontal carry tuning (NOTES, not SPEC)

**Status:** QUEUED, no SPEC yet. Picks up after Loop v2 ships.

## Why this exists

Phase B Stage 2 (closed 2026-05-19) revealed that `rolling_resistance` in `surfaces.csv` only governs the pure-roll tail of a shot. The harness measures `first_contact → end_pos`, which is dominated by the bounce/skid phase. Changing `rolling_resistance` by +183% (Green 0.12 → 0.34) moved total roll distance by 1.9% at v=25 and 0.0% at v=12. That's not a tune — that's a wrong-knob.

The real knobs for total horizontal carry after first contact:

- **`tangent_friction`** — horizontal energy lost per ground contact. Likely the dominant knob for carry-after-landing.
- **`restitution`** — bounce energy preservation. Controls bounce count.
- `rolling_resistance` — only matters once the ball is in pure roll (low speed, end of trajectory).

## Open questions for SPEC kickoff

1. **Back-solve model:** d_total = f(restitution, tangent_friction, rolling_resistance, v_contact). Closed-form (analytic bounce sum) or numerical (sample existing harness data, fit empirically)?
2. **Harness extension:** Stage 1 harness already captures `terrainHits` per bounce — currently aggregated into `bounce_count` in CSV. Worth exposing per-bounce velocities directly in CSV for Stage 3 fitting? Probably yes.
3. **Scope:** Just Fairway/Green/Sand again, or sweep all 9 surfaces this round?
4. **Test gate fallout:** Phase A tests use `rolling_resistance` indirectly through `BallSimulation`. Aggressive `tangent_friction` changes may shift those test results. Pre-budget which tests are likely to need band updates.
5. **Cesar's actual feel target:** he should play a few rounds with current values and articulate the specific over-roll experience (approach to green, putt off green, driver fairway, etc.) before we re-engineer.

## Related findings to fold in

- **GreenCollar putt-k inversion** (Stage 2 §Caveats #5): `putt.csv` has Green=0.50, GreenCollar=0.40. Comment says "Slightly slower than green" but lower k → longer roll → faster. Either fix the value or fix the comment.
- **Roll-path spin response is ~null** (Stage 2 §Caveats #4): spin=500 vs spin=2700 produces identical roll. Backspin doesn't bite at landing. Separate but related.
- **Sand H1 vs H9 zone divergence** (Stage 2 §Caveats #2): Sand H1 over-damped, H9 canonical. Likely zone-discovery picked a near-lip pixel. Mitigation in harness, not in `surfaces.csv`.

## Pre-flight when picking up

- Re-read `Docs/Specs/Completed/phase_b_surface_tuning_stage2/STATUS.md` and the SPEC
- Pre/post comparison script: `_compare_pre_post.py` in same folder
- Stage 1 canonical CSV: `Docs/Specs/Completed/phase_b_surface_tuning/captures/20260518_122845/sweep.csv`
- Confirm with Cesar: is current `surfaces.csv` post-Stage-2-revert (i.e. pre-Stage-2 values) the right baseline, or has live-play forced earlier tweaks?
