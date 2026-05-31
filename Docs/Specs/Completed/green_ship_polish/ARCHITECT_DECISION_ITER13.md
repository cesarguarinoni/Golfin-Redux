# Architect decision — iter-13 escalation (2026-05-30)

Self-reviewer verdict: **ESCALATE_TO_ARCHITECT**. The H07 ridge defect is fixed and
all five in-scope ridge holes (3, 7, 11, 13, 18) pass both gates at
`RIDGE_RAMP_WIDTH = 4.0 m`. Two items were escalated; Cesar ruled on both.

## Decision A — ramp width (4.0 m vs spec bound 2.5 m)

**PENDING Cesar's in-engine/video verdict.** Cesar chose "eyeball H07 first, but give
me the video." Canonical orbit `videos/h07_ridge_iter13_orbit.mp4` was sent to chat.
After he watches he will rule:
- **Accept 4.0 m** as-is, OR
- **Narrow to 2.5 m + relax the 5 cm continuity gate to ~6 cm** (the 5 cm gate was an
  arbitrary acceptance number; H11 misses it by only 0.7 cm at 2.5 m).

Context: every width ≤ 3.0 m passes the 12% slope cap with huge headroom (~3% actual),
so the *binding* constraint is the 5 cm continuity gate, not the slope cap. 4.0 m risks
over-smoothing the tier into a soft mound — the exact failure mode the spec warned about.

## Decision B — H14 (55 cm tier drop) — **BLOCK UNTIL SOLVED**

Cesar: **block the task until H14 is solved.** iter-13 cannot ship with a known-failing
2-tier hole in the `--all` batch. H14 has a ~55 cm tier drop (≈4× H07) and fails the
5 cm/cell continuity gate at any band width that preserves visible tiers (would need a
~22 m band). The flat slope cap (5.4% ≤ 12%) passes; only per-cell continuity fails.

**Required:** a per-hole large-drop solution before this task advances. Candidate
approaches for the implementer/architect to design (NOT yet specced):
- Adaptive band width scaled by tier drop (wider band for larger drops, capped so it
  doesn't consume the green), accepting the resulting gentler slope, OR
- A multi-step ramp (two or more sub-ridges / intermediate terraces) so each step carries
  a fraction of the 55 cm drop within the 5 cm/cell continuity budget, OR
- Per-hole continuity tolerance that scales with the hole's total tier drop.

This likely needs a spec amendment (architect) before the implementer re-bakes, because
it changes `smoothRidgeBand`'s single-width assumption.

## Next step

1. Cesar watches the orbit video and rules on Decision A.
2. Architect amends the spec for the H14 large-drop case (Decision B).
3. Re-dispatch golfin-implementer with: locked ramp width + H14 large-drop fix.

STATUS set to `IMPLEMENTER_BLOCKED` pending the above.
