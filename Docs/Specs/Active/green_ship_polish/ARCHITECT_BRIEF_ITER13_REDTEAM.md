# Architect brief — iter-13 amendment FAILED red-team (H14 staircase)

**Date:** 2026-05-30 (CEST)
**STATUS:** IMPLEMENTER_BLOCKED — held for architect amendment before re-implementing.
**Decision (Cesar):** Architect amendment first — the fix touches a spec premise; do not re-dispatch the implementer until the approach is amended.

## What passed and what didn't

The drop-scaled-width amendment (iter-13 amendment) was implemented and PASSed the
`golfin-reviewer` gate, then **FAILED the adversarial red-team gate**. Five of six ridge
holes are genuinely clean (H03/H07/H11/H13/H18 — max adjacent Δh 1.74–3.42 cm/cell, ridge
staircase GONE at full res). **H14 is broken.**

## The H14 defect (independently reproduced by red-team)

- A side-agnostic, whole-green adjacent-cell Δh scan over the shipped `Hole_14/green.json`
  finds **22 adjacent cell-pairs with Δh > 5 cm, max 23.64 cm/cell**, forming one contiguous
  ascending diagonal across the green INTERIOR, cell (32,20)→(44,10). 10 of 11 sampled cliff
  cells are interior (not a contour-edge artifact). This is the original iter-13 staircase,
  alive on H14.
- **7 of the 22 failing pairs are INSIDE the smoothing band** (up to 14.93 cm), yet
  `verify-ridge.mjs` reports `continuity ✓` for H14.

## Root cause (this is the crux for the amendment)

`smoothRidgeBand()` and `verify-ridge.mjs` both measure distance from the **authored ridge
polyline** (the user-traced line the SPEC names as "the authority"). But the actual Poisson
height cliff sits on the **regionGrid boundary** (where `ridgeSeparated()` splits the two
tiers during relaxation). On H03/H07/H11/H13/H18 these two lines roughly coincide, so the
smoothing lands on the cliff and works. **On H14 they are disjoint by 5–15 m**
(authored ridge X∈[-123,-109]; actual cliff X∈[-107,-102]). So on H14:
1. `smoothRidgeBand` smooths a strip of empty grass next to the authored line and never
   touches the real cliff.
2. `verify-ridge.mjs` measures continuity against the same wrong line, so it misses the cliff.
3. The "same-side" filter (verify-ridge.mjs:252) additionally masks the in-band failures.

So the gate is **structurally blind** to the very defect class this task exists to kill,
and it only passed H14 because the check keys off the wrong line.

## Questions for the architect

1. **Which line is the authority for smoothing + verification — authored ridge polyline, or
   regionGrid boundary?** The SPEC says "the user-traced ridge polyline is the authority"
   and "no new arrow authoring." But the *real cliff* (the thing producing the staircase) is
   the regionGrid boundary. Red-team's fix: drive BOTH smoothing and the verify band off the
   regionGrid boundary, not the authored ridge. Does the architect endorse this? It contradicts
   the SPEC's stated premise and needs an explicit ruling.

2. **Is H14's authored ridge simply mis-placed?** A 5–15 m divergence between the authored
   line and the actual tier cliff suggests H14's ridge polyline may have been traced in the
   wrong spot. If so, the correct fix might be re-tracing H14's ridge (authoring fix), NOT
   changing the algorithm for all 18 holes. Which is it — fix the data (H14 ridge) or the
   algorithm (key off regionGrid boundary)? Or both?

3. **Adopt a side-agnostic whole-green Δh>5cm continuity gate?** The current gate is
   same-side and authored-ridge-relative, which is what let H14 through. Replace it with a
   gate that scans the entire green for ANY adjacent Δh>5cm (no side filter, no dependence on
   ridge line) so no failure can hide. Confirm this is the new acceptance gate.

4. **Regression risk on the 5 clean holes.** If smoothing keys off the regionGrid boundary
   instead of the authored ridge, do H03/H07/H11/H13/H18 (currently clean, where the lines
   coincide) stay clean? Likely yes, but the amendment should require re-verifying all six
   with the new side-agnostic gate.

## What is NOT in question

- The drop-scaled width mechanism itself is sound (the `1.5×` smoothstep peak-derivative
  factor was judged mathematically correct by the red-team — keep it).
- The 5 clean holes' results.
- bake-only / no schema / no importer change constraints.
- The /green-orbit video tool (now sanctioned) — but future H14 clips must be shot at a
  LOW grazing angle that can resolve a tier cliff, not near-top-down.

## Suggested shape of the amendment (for the architect to confirm or override)

- Smooth and verify off the **regionGrid boundary** (extract the set of cell-pairs where
  `ridgeSeparated()` is true; that polyline IS the cliff). Keep drop-scaled width + 1.5×.
- Add a **side-agnostic whole-green Δh>5cm gate** as the hard acceptance check.
- Decide H14 ridge re-authoring vs algorithm-only.
- Re-bake `--all`, re-verify all six ridge holes with the new gate, and shoot H14 + H07
  green-orbit clips at a LOW grazing angle.

Full red-team findings with cell coordinates and the raw height dump are in
`ARCHITECT_REVIEW.md` (RED-TEAM section).
