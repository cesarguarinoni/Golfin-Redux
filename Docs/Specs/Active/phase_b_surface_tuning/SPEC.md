# SPEC — `phase_b_surface_tuning` (Stage 2: Apply k-value updates)

> Stage 2 of Phase B. Stage 1 (diagnostic harness) shipped, harness ran, CSV at `Docs/Specs/Completed/phase_b_surface_tuning/captures/20260518_122845/sweep.csv` (546 data rows). This Stage applies architect-derived k-value updates to `surfaces.csv` and re-validates via the harness.

## Status

See `STATUS.md`. Starting at `SPEC_READY`.

## Pipeline tier

**Tier 2 TELLCODE.** Single CSV row edits + re-validation gate. No asmdef, no scene, no runtime spatial math, no new tests. Multi-file in the sense of "CSV + verification harness re-run" but established pattern.

---

## Headline findings from Stage 1 CSV analysis

Cesar's filter (Sub-mode 1a roll rows, `surface_target ∈ {Fairway, Green, Sand}`, `end_surface == surface_target` clean-capture sanity, `source_hole ∈ {1, 9}`) plus all 126 putt rows. Filtered dataset: 168 roll rows + 126 putt rows.

### Reframe: "Over-roll on green" is a ROLL-PATH bug, not PUTT-PATH

Cesar reported visible over-rolling on green during live play (2026-05-13, drove the P0 escalation). The diagnostic shows two **different** code paths into Green behavior, each with its own k-value:

| Path | Code | Configured in | Current k | Observed |
|---|---|---|---|---|
| **Putt path** (`IsPutt=true`, `RunPuttPhase`) | `BallSimulation.RunPuttPhase` | `putt.csv` | Green = **0.50** | Stimpmeter row (v=1.80 m/s observed, comment says v=1.83 design): **3.5333 m roll vs 3.66 m predicted (linear v/k) = −3.5% drift** |
| **Roll path** (approach shot landing) | `BallSimulation.RunRollPhase` | `surfaces.csv` | Green = **0.12** | At v_target=25 (contact ≈29.5 m/s, real driver landing speed): **8.32 m roll** — vs PGA target of 2–4 m for well-struck approach checks |

**Stimpmeter sign-flip Cesar flagged** is real: putt path is ~3.5% UNDER-rolling, not over-rolling. The "over-roll on green" complaint is therefore almost certainly about **approach shots landing on green**, not putts — and the roll-path k=0.12 produces 8 m of roll-out at driver landing speeds where real golf gets 2–4 m. That's the bug.

This SPEC fixes the ROLL path. The PUTT path is approximately correct (−3.5% Stimpmeter drift is within typical game-physics noise; tightening it further is deferred to Phase B follow-up or Stage 3).

### Observed roll-path data (median across filtered samples, 4 per bucket)

| Surface | v_tgt | v_contact | roll | d/v_contact |
|---|---|---|---|---|
| Fairway | 20 | 24.34 | 18.48 | 0.759 |
| Fairway | 25 | 29.50 | 25.23 | 0.855 |
| Green   | 20 | 23.68 | 5.69  | 0.240 |
| Green   | 25 | 29.15 | 8.32  | 0.285 |
| Sand (H9) | 20 | 23.65 | 1.30 | 0.055 |
| Sand (H9) | 25 | 29.13 | 1.95 | 0.067 |

Real-world targets used to back-solve new k values (`k_new = k_cur × (d_obs / d_target)`):

| Surface | v_tgt | d_target | Source |
|---|---|---|---|
| Fairway | 20 | 14.0 m | Trackman 2024 PGA driver carry+roll mid-band (15–30 yd → 13.7–27.4 m) |
| Fairway | 25 | 20.0 m | Trackman, upper mid |
| Green | 20 | 2.0 m | PGA mid-iron approach checks within 2–4 m |
| Green | 25 | 3.0 m | PGA, slightly longer at high-speed landing |
| Sand | 20 | 1.0 m | Penner 2002 + Cochran 1968 — ball plugs/decelerates rapidly in sand |
| Sand | 25 | 1.2 m | Same; slightly more at higher landing speeds |

### Proposed `surfaces.csv` changes

| Surface | Current k | Proposed k | Δ | Rationale |
|---|---|---|---|---|
| Fairway | 0.18 | **0.23** | +28% | Modest tighten. Brings v=25 roll from 25 m → ~20 m (top of Trackman tour band) |
| Green | 0.12 | **0.34** | +183% | The big one. Brings v=25 roll from 8 m → ~3 m (PGA approach-check band) |
| Sand | 0.70 | **1.02** | +46% | Using H9-only data (Cesar caveat: Sand H1 is over-damped, H9 canonical) |

---

## Caveats from Stage 1 data (carried into Stage 2 as documentation)

1. **Putt determinism**: Green putt samples have stdev = **0.0000** across all 9 samples per (surface, v) bucket — bit-exact deterministic across Holes 1/9/18, confirming Cesar's "putt samples are n=1 (no jitter)" caveat for Green. **Caveat is NOT true for Fairway putts** — stdev 1.5–20m because Fairway terrain varies per hole (slopes/contour pick up the difference). Worth filing as future-Stage observation; not actioned in this SPEC.
2. **Sand H1 vs H9**: Sand H1 zone is consistently more damped than H9 (e.g. v=25: H1=0.68 m, H9=1.95 m). Likely H1's discovery picked a sand center near a cup/lip area. Cesar's "median-filter Sand H1" caveat = use H9 numbers when proposing Sand k. **This SPEC's Sand k is derived from H9 only.**
3. **Stimpmeter putt result (per Cesar's flag)**: Observed 3.5333 m vs predicted 3.66 m (using v=1.83 design) = **−3.5% drift**. Cesar's 1.4% figure matches the predict using v=1.80 (the CSV's discretized target speed). Direction of drift (UNDER, not OVER) is opposite of what the original "over-roll on green" complaint suggested — confirming Cesar's hypothesis that two different code paths are at play. **Putt.csv not edited in this SPEC.**
4. **Spin response anomaly**: Roll-path samples at spin=500 vs spin=2700 produce ~identical roll distances (Green v=15: 3.50 m vs 3.47 m, 0.8% delta). The model does not visibly respond to backspin during roll phase. **Out of scope for Stage 2** — flag as Stage 3 candidate (spin-decay-on-bounce investigation).
5. **GreenCollar putt-k inversion**: `putt.csv` has `Green=0.50, GreenCollar=0.40` with comment "Slightly slower than green". But `d = v/k` means lower k → longer roll → faster green. So GreenCollar at 0.40 is actually FASTER than Green at 0.50, opposite the comment's intent. **Architect-observed anomaly; out of scope for Stage 2. File as Phase B follow-up.**

---

## Implementation

### File 1 (only): `Assets/Resources/Physics/surfaces.csv`

Three single-row edits. Use `Filesystem:edit_file` (PowerShell pivot on EPERM per memory rule #10 if needed). Do not change column order, column count, or any other rows.

**Current rows:**
```
Fairway,0.50,0.55,0.18,0.10,closely-mown grass baseline
Green,0.40,0.75,0.12,0.05,checks quickly; low roll
Sand,0.15,0.85,0.70,0.25,bunker; heavy damping
```

**New rows:**
```
Fairway,0.50,0.55,0.23,0.10,closely-mown grass baseline; k bumped 0.18→0.23 (Phase B Stage 2, 2026-05-18) — Trackman 2024 PGA driver carry+roll mid-band 15-30yd
Green,0.40,0.75,0.34,0.05,checks quickly; k bumped 0.12→0.34 (Phase B Stage 2, 2026-05-18) — addresses approach-shot over-roll; PGA mid-iron approach checks 2-4m at driver landing speed
Sand,0.15,0.85,1.02,0.25,bunker; heavy damping; k bumped 0.70→1.02 (Phase B Stage 2, 2026-05-18) — H9-anchored (Cesar caveat: Sand H1 over-damped); ball plugs rapidly per Penner 2002 + Cochran 1968
```

All other rows (GreenCollar, Semirough, Rough, Tee, BunkerLip, CartPath, Water, OOB) are **unchanged**.

### Re-validation gate

After applying the CSV edits, implementer must:

1. **Run existing test gate** — `RollAndPuttTuningTests.cs` and full physics test suite. **Must remain 294/294 PASS.**
   - If any test fails: stop, do NOT escalate auto-bump test bands. Report which tests failed with their expected/actual numbers. Architect will decide whether to tighten test bands as part of this SPEC or kick to Phase B follow-up.
2. **Re-run harness** via `GOLFIN/Physics/Run Surface Rollout Sweep` MenuItem, full sweep. Place output at `Docs/Specs/Active/phase_b_surface_tuning/captures/<new_timestamp>/`.
3. **Verify acceptance bands** (medians across filtered rows, same filter as Stage 1):
   - Fairway v=25 roll: **target 17–23 m** (current 25.2, post-tune predict ~20)
   - Green v=25 roll: **target 2.5–4.0 m** (current 8.3, post-tune predict ~3)
   - Sand H9 v=25 roll: **target 1.0–1.5 m** (current 1.95, post-tune predict ~1.2)
   - Green putt Stimpmeter (v_target=1.80) **unchanged**: should still produce 3.5333 m ± 0.001 (deterministic). If this changes, surfaces.csv edit is bleeding into putt path — escalate immediately.
4. **Visual smoke check** — Cesar plays one or two holes, confirms approach shots check on green per real-golf intuition.

### Acceptance checklist

- [ ] `surfaces.csv` three rows edited per spec (Fairway, Green, Sand); all other rows byte-identical
- [ ] Full test suite re-run: **294/294 PASS**, no new failures (test-band tightening explicitly NOT in scope)
- [ ] Harness re-run produces new CSV at `Docs/Specs/Active/phase_b_surface_tuning/captures/<new_timestamp>/sweep.csv`
- [ ] Median roll values fall within acceptance bands (Fairway 17–23, Green 2.5–4.0, Sand H9 1.0–1.5 at v=25; Stimpmeter putt unchanged)
- [ ] No production code (`.cs`) touched
- [ ] No `putt.csv` touched
- [ ] No test-band edits (separate task — see Out of scope)
- [ ] `IMPLEMENTER_REPORT.md` documents: pre-vs-post numbers at v=20 and v=25 for all three surfaces, plus Stimpmeter unchanged confirmation

---

## Out of scope (Phase B follow-ups)

- **Rough k tuning** (current 0.45 → original Phase B target ~0.65): Cesar's filter excluded Rough. No clean CSV data this round. File as Phase B sub-task — harness already supports Rough, just re-run with `surface_target ∈ {Rough}` filter and follow same back-solve method.
- **Tee k audit** (current 0.15): Same — no clean data. File as Phase B sub-task.
- **Semirough, GreenCollar, BunkerLip, CartPath**: same.
- **Phase A test-band tightening** (`RollAndPuttTuningTests.cs` loose bands `[8, 45]` and `[100, 400]`): existing Notion task **Order 111 "lab-only verification gap"**. Best handled after Stage 2 ships and we have multi-surface tuned numbers to back-fit test bands to.
- **GreenCollar putt-k inversion** (putt.csv Green=0.50, GreenCollar=0.40 inverts the "Slightly slower" comment): file as Phase B follow-up.
- **Spin-decay-on-bounce model** (spin=500 vs spin=2700 produce ~identical roll): Stage 3 candidate. Separate ticket.

---

## Files / hierarchy this task touches

- **EDIT** `Assets/Resources/Physics/surfaces.csv` (3 rows: Fairway, Green, Sand)
- **NEW** `Docs/Specs/Active/phase_b_surface_tuning/captures/<post_tune_timestamp>/` (harness re-run output)
- **NEW** `Docs/Specs/Active/phase_b_surface_tuning/IMPLEMENTER_REPORT.md`

No new code files. No asmdef, no scene, no production `.cs` changes. No `putt.csv` changes.

---

## Smoke evidence

`IMPLEMENTER_REPORT.md` shows the pre-vs-post comparison table at v_target=20 and v_target=25 across all three edited surfaces, plus Stimpmeter putt unchanged confirmation. Cesar visually plays a few approach shots to a green and confirms balls check rather than over-rolling. No further visual fidelity capture required — numerical CSV correctness IS the evidence (per Stage 1 SPEC convention).

---

## References

- Stage 1 sweep CSV: `Docs/Specs/Completed/phase_b_surface_tuning/captures/20260518_122845/sweep.csv` (canonical)
- Stage 1 analysis scripts: `Docs/Specs/Active/phase_b_surface_tuning/_analysis.py`, `_analysis2.py`, `_analysis3.py`
- Real-world target sources: Trackman Annual Golf Report 2024 (driver carry+roll); PGA Tour ShotLink approach-shot data (mid-iron check distances); Penner, A.R. (2002) "The physics of putting." *Canadian Journal of Physics* 80(2):83–96; Cochran & Stobbs, *The Search for the Perfect Swing* (1968)
- Predecessor Phase A: `Docs/Specs/Completed/controls_c_fix/` — locked Green k_putt=0.50, CartPath k_roll=0.30; observation-only on Fairway/Rough at the time
- Loop v1 §2e ships AtRest across all 18 holes → enabled clean roll-distance capture (the diagnostic gate)
- Lesson K — real-world targets must be cited, never fabricated. All targets above have an inline source.
