# Architect Review — `phase_b_surface_tuning` Stage 1 (Diagnostic Harness)

**Reviewer:** `golfin-reviewer` (Opus 4.7 1M)
**Iter-6 timestamp:** 2026-05-18 10:42 CEST
**Verdict:** `ARCHITECT_REVIEW_ESCALATE` — Fix #6 changes the diagnostic semantics in a way the architect must rule on before Stage 2 can be written. The lofted-launch geometry produces a usable Sub-mode 1b dataset (perfect) but a degraded Sub-mode 1a dataset (low-speed-only, large-surface-only). The implementer's "physical roll-off" explanation for Gate C mismatches is factually wrong — the mismatches are launch overshoots (ball never touched target surface), not boundary roll-offs. Three concrete options for Cesar in §Architect decision request.

---

## Independent data scan (iter-6, performed BEFORE reading IMPLEMENTER_REPORT)

Opened `sweep.csv` (182 data rows) and `real_shots.csv` (6 rows) cold. Recomputed every counter from raw CSV using `awk`. Findings, ranked by severity:

1. **Sample axis is no-op.** `sample_id=1` and `sample_id=2` are bit-identical for every roll row (same `actual_v`, `first_contact_pos`, `end_pos`, `roll_distance` — only `timestamp_iso` differs). Putt mode: `sample_id={1,2,3}` are bit-identical. The harness is fully deterministic. Effective sample count per (mode, surface, speed, spin) = 1, not 2-3.
2. **Spin axis is no-op.** `target_spin_rpm=500` and `target_spin_rpm=2700` produce bit-identical `actual_v`, `first_contact_pos`, `end_pos`, `roll_distance` for every (mode, surface, speed) combination. Either backspin is not being applied, or the lofted geometry makes Magnus force negligible at these velocities, or the integrator is gated on spin sign-bit elsewhere. Either way, the spin axis of the matrix produces zero diagnostic differentiation.
3. **Fix #6 launch overshoots target surfaces.** Geometry check: launched at +30° with horizontal velocity `vH`, the ball lands `vH²·sin(60°)/g ≈ vH² × 0.088 m` downrange from spawn. At vH=3 m/s that's 0.8m. At vH=15 m/s that's 19.8m. At vH=25 m/s that's 55m. The 2m clean-radius discovery filter is sufficient for vH=3 m/s but useless at vH=15+. Concrete evidence:
   - **Green** at vH=15: spawn (-238.4, 10.26, -75.77), first_contact (-214.24, 9.36, -75.86) → 24m horizontal flight, lands on Sand (the Green is 18m wide here). All 4 rows at vH=15 record `end_surface=Sand`.
   - **Green** at vH=20: lands 42m downrange on Fairway. **Ball never touched Green at all.** Same for vH=25.
   - **Tee** at vH=3 m/s already lands on Fairway (spawn (128, 10.16, 26.24), first_contact (130.6, 10.16, 26.24) → only 2.6m horizontal but Tee is < 2m wide on Hole 1). **Zero of 28 Tee roll rows record first contact on Tee.**
   - **CartPath** at vH=3: same problem; lands on Fairway. **Zero of 28 CartPath roll rows record first contact on CartPath.**
   - **Sand** at vH=12+: ball lands on Fairway. Sand strip is narrow.
4. **Sand "rolls" are first-bounce hops onto slopes.** Sand at vH=3: first_contact y=7.11, end y=7.27 (ball ended *higher* than first contact). vH=6: 7.48 → 8.08. vH=9: 7.79 → 8.63. `bounce_count=1`, `sim_duration=1-2.6s`. The ball bounces once off the bunker edge slope and stops mid-air or on the lip — not rolling on sand.
5. **Green "rolls" are non-monotonic.** vH=3 → roll=1.08m. vH=6 → roll=0.80m. vH=9 → roll=1.69m. vH=12 → roll=1.94m. **The vH=6 ball rolls *shorter* than the vH=3 ball on the same green.** Compare Sub-mode 1b on the same green (putt mode, ground-level launch): vH=0.5 → 0.89m, vH=1.0 → 1.88m, vH=1.83 → 3.53m, vH=2.5 → 4.87m, vH=3.5 → 6.86m, vH=5.0 → 9.85m, vH=7.0 → 13.84m. **Perfect monotonic linear scaling.** The contrast confirms the 1a roll data is corrupted by bounce energy loss, while the 1b putt data is clean.
6. **Real-shot draws are 2× the straight carry.** H1 straight=228.6m, draw3deg=460.5m. H9 straight=220m, draw3deg=430m. A 3° rotation cannot double carry; the draw spin axis or rotation is interacting with the integrator incorrectly. Implementer flagged "pre-existing direction issue" without diagnosis. The straight shots are usable (228 / 220 / 225m, all in 200-250m band); the draws are unusable.
7. **Sub-mode 1b (putts) is perfect.** 42/42 rows match target surface. Stimpmeter row at vH=1.83 reads 3.5333m vs Phase A math predicts 3.58m (1.4% deviation — well within any reasonable gate). Putt rollouts on Green and Fairway are monotonic linear in speed. **This is the actual smoking-gun dataset Cesar's "over-roll on green" diagnostic was asking for, and it lands clean.**
8. **Stimpmeter signal:** at 1.83 m/s release on Green, ball stops at 3.53m. Phase A math (k=0.50) predicts 3.66m. In-game observation (3.53m) is *shorter* than math prediction — not longer. This is the OPPOSITE of "over-roll on green." Either the diagnostic isolates a different bug than Cesar thought, or the over-roll bug surfaces only at higher putt speeds, or only with non-flat green slope (the discovery picks a flat patch — slope-driven over-roll is filtered out by design). Stage 2 needs to address this directly.

---

## Per-surface row breakdown (recomputed)

```
awk -F, 'NR>1 {print $3","$1}' sweep.csv | sort | uniq -c
  28 CartPath,roll    ← zero rows with first_contact ON CartPath
  28 Fairway,roll     ← all 28 land on Fairway (it's huge)
  21 Fairway,putt     ← all match
  21 Green,putt       ← all match (PERFECT data)
  28 Green,roll       ← 4 at vH=3 land on Green, 4 at vH=6 on Green, 4 at vH=9 on Green, 4 at vH=12 on Green; 12 overshoot to Sand/Fairway
  28 Sand,roll        ← 12 at vH=3,6,9 land on Sand (with hop-not-roll behavior); 16 overshoot
  28 Tee,roll         ← zero rows with first_contact ON Tee
```

**Usable rolling data for k-tuning:** Fairway 28/28 (with caveats), Green 12-16/28 (low-speed only, sample×spin axes deduplicate to 3-4 unique data points), Sand 0/28 (all are hops not rolls). Tee/CartPath 0/0.

**Usable putt data for k-tuning:** Green 21/21 (deduplicates to 7 unique speeds), Fairway 21/21 (deduplicates to 7).

**Usable real-shot data:** H1/H9 straight carry+roll, H9 straight carry only (H18 straight ends OOB, no roll). 2 of 6.

Compared to the spec's 315-row target with 9 surfaces × 7 speeds × 2 spins × 2 samples giving 252 unique data points in 1a + 63 in 1b = 315 unique observations, the actual unique-information yield is roughly: 7 unique Fairway rolls + 4 unique Green rolls (low-speed) + 14 unique putts + 3 real shots = **28 unique observations**, against an expected 315.

---

## Gate verification (recomputed independently)

### Gate A — git status (PASS, confirmed)

```
git status:
 D .claude/scheduled_tasks.lock         (pre-existing, not ours)
 M Docs/Specs/Active/phase_b_surface_tuning/STATUS.md
?? Assets/Scripts/Physics/Viewer/Editor/SurfaceRolloutMenu.cs
?? Assets/Scripts/Physics/Viewer/Editor/SurfaceRolloutMenu.cs.meta
?? Assets/Scripts/Physics/Viewer/SurfaceRolloutHarness.cs
?? Assets/Scripts/Physics/Viewer/SurfaceRolloutHarness.cs.meta
?? Docs/Specs/Active/phase_b_surface_tuning/{ARCHITECT_REVIEW,HEARTBEAT,IMPLEMENTER_REPORT,captures/}

git diff --stat HEAD:
 .claude/scheduled_tasks.lock                       |  1 -
 Docs/Specs/Active/phase_b_surface_tuning/STATUS.md | 22 +++++++++++++++++++++-
 2 files changed, 21 insertions(+), 2 deletions(-)
```

No `.unity` or `.asset` modifications. Scene-mutation audit PASS. Iter-4's `LabScaffold.unity` corruption is fully reverted.

### Gate B — Row counts (PARTIAL-PASS, confirmed)

```
awk -F',' 'NR>1{print $1}' sweep.csv | sort | uniq -c
  140 roll
   42 putt
```

```
awk -F',' 'NR>1{print $3}' sweep.csv | sort | uniq -c
  28 CartPath
  49 Fairway   (28 roll + 21 putt — wait, putt should only be Green+Fairway, so 28+21=49 ✓)
  49 Green     (28 roll + 21 putt ✓)
  28 Sand
  28 Tee
```

182/315 rows. Spec line 192 tolerance ("up to 9 missing if surfaces absent from Hole 1") allows up to 306 rows; actual gap is 133 rows (4 surfaces absent: Rough, GreenCollar, BunkerLip, Semirough). The spec wording is ambiguous between "9 missing rows" (literal) and "9 missing surfaces" (intent). Under "9 missing surfaces" reading, the gap is within tolerance because the gap is structural (Hole 1 lacks those surfaces), not lossy. README documents 4 surfaces absent. **Accept under intent reading**, but note that Stage 2 will need either a different hole or a re-run to cover Rough specifically (rough rollout is the most important non-Green/Sand surface for ball strike calibration).

### Gate C — End-surface mismatch (FAIL, with revised interpretation)

```
awk -F',' 'NR>1 && $1=="roll" && $3==$13 {match_count++} NR>1 && $1=="roll" {total++} END {printf "Roll match: %d/%d = %.1f%%\n", match_count, total, 100.0*match_count/total}'
Roll match: 56/140 = 40.0%
```

Putt mode: 42/42 match = 100%.

**Implementer's explanation ("physical roll-off from narrow surfaces") is factually wrong.** A roll-off happens when first_contact IS on the target surface and the ball then rolls off the edge. The actual mismatches in this dataset are launch overshoots — first_contact is never on the target surface. Concrete evidence from Tee rows: all 28 Tee rows have first_contact x∈[130.6, 167.1], z≈26.24, end x∈[132.6, 198.6]. The Tee patch in Hole 1 is < 2m wide; the ball's *first contact* is already 2-40m downrange in the Fairway. Same for CartPath. Same for Green at vH≥15 (lands on Sand/Fairway, not Green).

**The 5% gate was written assuming the original drop-from-3m-altitude spec.** In a 3m vertical drop with 30°-below velocity, horizontal travel before contact is `vH × (3/g·tan60°)^0.5 ≈ vH × 0.59` (e.g., vH=3 → 1.8m; vH=15 → 8.8m; vH=25 → 14.7m). Still problematic for narrow surfaces, but much better than the +30° lofted launch (which gives vH²×0.088 → vH=25 → 55m). Whether the original spec's gate is even achievable on Hole 1 with the original geometry is unclear without re-running.

**Verdict on Gate C: FAIL under the spec's gate. The implementer's framing of "physical roll-off" is incorrect; the mismatches are launch overshoots, a Fix #6 side-effect.**

### Gate D — Zero non-warning zero-contact rows (PASS, confirmed)

```
awk -F',' 'NR>1 && $6=="TIMEOUT"' sweep.csv | wc -l → 0
awk -F',' 'NR>1 && $6=="0.000"' sweep.csv | wc -l → 0
```

No TIMEOUT rows, no zero-velocity-at-contact rows. This is the iter-4 ball-fell-through-world bug — fully fixed.

### Gate E — Eyeball signals

| Signal | Result | Status |
|---|---|---|
| Stimpmeter (putt Green vH=1.83) | 3.5333m vs predict 3.58m (1.4% short) | PASS |
| Green roll distance > 0 | Yes at vH=3,6,9,12 | PASS at low speeds only |
| Sand roll distance > 0 | Yes at vH=3,6,9 but data is hops not rolls (end_y > contact_y) | FAIL on physical-realism grounds |
| Driver straight carry 200-250m | H1=228, H9=220, H18=225 (OOB) | PASS for H1/H9 |
| Driver draw carry | H1=460, H9=430 (2× straight) | FAIL (geometry anomaly) |

---

## Fix #6 geometric soundness analysis

**Implementer's claim:** "The lofted-launch approach preserves the 30°-below geometry AT FIRST CONTACT while allowing the ball to start from surface level. The roll behavior after first contact is identical to the spec's intent."

**Analysis:**

- *At-contact geometry preserved (TRUE):* by energy conservation, a ball launched from y₀ with `(vX, +vY, 0)` returns to y₀ with `(vX, -vY, 0)` (ignoring drag). Spin axis unchanged. At the moment of first contact, the ball's velocity vector matches what a 3m drop would have produced if vY₀ were chosen correctly. ✓
- *First contact position NOT preserved (FALSE).* A 3m drop with vH horizontal velocity puts first contact at `horizontal = vH × sqrt(2×3/g) ≈ vH × 0.78` meters downrange of spawn. A +30° lofted launch puts first contact at `vH²×sin(60°)/g ≈ vH² × 0.088` meters downrange. At vH=3 these are similar (2.3m vs 0.8m). At vH=15 they diverge wildly (11.7m drop vs 19.8m loft). At vH=25 they diverge catastrophically (19.5m drop vs 55m loft).
- *Roll behavior after first contact (PARTIAL):* same impact velocity → same bounce-and-roll physics, true. But "first contact position" determines which surface the ball is rolling ON. If the loft lands the ball on Sand instead of Green, the post-contact roll is governed by k-Sand, not k-Green. The diagnostic intent ("measure k-Green at vH=15") is impossible if the ball never touches Green.

**Conclusion:** Fix #6 is geometrically correct only for the low-velocity / large-surface subset of the matrix. For high velocities or narrow surfaces it converts the diagnostic from "roll on surface S" to "roll on whatever surface lies vH²×0.088 meters from S's center." That's not what the spec asked for.

**Alternative fixes the implementer could have tried (for future iter context, NOT a fix list now):**
- Modify `PlacementSnapHelper.Snap` to accept an "above-surface offset" parameter (production change, out of spec scope).
- Add a `PhysicsLabController.PlaceBallAtAirborne(worldPos)` method that bypasses the surface snap (production change, but minimal).
- Use the original -30° downward velocity but spawn directly via the static-bus `BallSimulation.Simulate` math path with the real providers (architectural deviation: this would test the math path, not the game path — but it would isolate the Stage 2 k-tuning question cleanly).
- Pre-fire a tiny "lift" shot that gets the ball above surface, then fire the diagnostic shot (gross, but in-scope).

The implementer chose the simplest in-scope fix, but didn't measure whether it preserves the diagnostic intent across the speed × surface matrix. It does not.

---

## Bbox verification (containment check)

N/A — this task has no "X inside Y" containment claim. Scene mutation audit + git diff handled above.

---

## Architect decision request — three options

The harness is now structurally sound (no scene corruption, no ball-fall-through-world, MenuItem reachable, CSVs produced). The data, however, is not what the spec asked for. Three options:

### Option A — Accept iter-6 as Stage 1 with documented limitations, write Stage 2 SPEC against the partial dataset

Stage 2 calibrates from:
- **Putt-Green** (k-putt-green, 7 unique data points, vH 0.5-7.0 m/s, monotonic linear) — pristine
- **Putt-Fairway** (k-putt-fairway, 7 unique data points) — pristine
- **Roll-Fairway** (k-roll-fairway, 7 unique data points, vH 3-25 m/s) — clean but high actual_v compared to target (30% boost at low vH dropping to -7% at high vH due to lofted-launch gravity asymmetry)
- **Roll-Green** (k-roll-green, 3-4 unique data points at vH 3-12 only) — limited but usable
- Real-shot H1/H9 straight (sanity check) — 2 data points

Out of scope for Stage 2 from this dataset: k-roll-Sand (data is hops not rolls), k-roll-Tee, k-roll-CartPath, k-roll-Rough (surface absent), k-roll-GreenCollar (absent), Bunker/Semirough. Spin sensitivity tuning (axis is no-op).

**Pros:** unblocks Stage 2 immediately. Cesar's primary diagnostic question ("does in-game green over-roll match my eyeball?") IS answered: putt-Green at Stimpmeter speed reads 3.53m vs math 3.58m — the game does NOT over-roll on green at Stimpmeter speed. (Cesar should sanity-check this against his in-game observation: was the eyeball over-roll on PUTT shots or on LANDED shots? If putts, this dataset contradicts the bug report; if landed shots, the dataset is silent.)
**Cons:** Sand/Tee/CartPath/Rough k-values cannot be calibrated from this dataset. Spin sensitivity stays unmeasured.

### Option B — Re-run Stage 1 with original drop geometry, modifying PhysicsLabController to bypass `PlacementSnapHelper.Snap`

Architect approves a one-method addition to `PhysicsLabController`: `PlaceBallAtAirborne(Vector3 worldPos)` that sets ball position without surface snap. Harness uses it for the 1a aerial spawn. Re-run sweep. Expected outcome: lower-loft horizontal travel before contact, more rows hit target surface, narrower-surface mismatches reduced (but not eliminated for sub-2m surfaces).

**Pros:** matches spec intent. Recovers some Sand/narrow-surface diagnostic signal.
**Cons:** production code change (one method, minimal). Re-runs the 35-45 min sweep. Tee/CartPath narrow surfaces still problematic.

### Option C — Accept iter-6 as Stage 1, defer Sand/Tee/CartPath calibration to a Stage 1-bis sweep on a different hole

Architect writes Stage 1-bis to run the sweep on a hole with wider Sand bunkers and a defined Rough zone. Hole 9 or 18 may have larger Sand. Stage 2 starts on what we have (putt-Green/Fairway + roll-Green/Fairway), and the bunker-shot calibration becomes a Phase B.1 follow-up.

**Pros:** ships Stage 2 sooner with the partial dataset. No production code change.
**Cons:** doubled work for Sand calibration. Hole 9/18 may have the same launch-overshoot problem if Sand bunkers are narrow.

---

## Recommendation to Cesar

I recommend **Option A** — accept iter-6 with documented limitations and write Stage 2 against this dataset. Reasoning:

1. The 1b putt data IS the smoking-gun dataset Cesar's bug report was about. Stage 2 can calibrate k-putt-Green/Fairway today and ship the most impactful tuning fix.
2. The 1a roll data is degraded but not garbage — Fairway has 7 clean data points covering vH 3-25, Green has 3-4 data points at low speed. Enough to set rough k-roll bounds for those two surfaces.
3. Sand/Tee/CartPath calibration was always going to be hole-specific; deferring it to a Stage 2-bis matches normal staging.
4. Re-running the sweep (Option B) costs 35-45 min of Cesar's time and requires a production code change for a surface (Sand) where the underlying physics issue (ball-hops-up-slope-at-bunker-edge instead of rolling) is a real game-physics question that needs separate investigation anyway — not just a harness-geometry problem.

If Cesar instead disagrees and says "I want clean roll data for all 5 surfaces before Stage 2 starts," **Option B**. The production-code change is one method, contained.

---

## Spec deviations from implementer report (assessment)

1. **Fix #6 (lofted launch):** assessed in detail above. Implementer's claim of "geometry preserved" is true at impact velocity but false at impact position; the framing of "physical roll-off" for Gate C mismatches is factually wrong. Implementer should have flagged the launch-overshoot side-effect explicitly. The fix is not "wrong" per se — it makes the harness produce data — but its scope is narrower than implementer claims.
2. **Sample axis no-op:** not mentioned in IMPLEMENTER_REPORT. Worth a one-line note for Stage 2 — Cesar shouldn't read the CSV expecting 2-3× sample variance.
3. **Spin axis no-op:** not mentioned. Worth investigating before Stage 2 (could be a real bug in how the harness passes `SpinState` for the lofted shot, or a real bug in `BallSimulation` for low-altitude high-spin rolls).
4. **Draw shot 2× carry:** acknowledged as "pre-existing direction issue" without diagnosis. For Stage 2 purposes, draw shots aren't strictly required (the SPEC said Sub-mode 2 validates "Mode 1 numbers translate to real terrain," which straight-shot pairs satisfy). But this is a real bug that should land in `Docs/Tasks.md` or `Docs/Specs/Queued/` as a follow-up — the 3° draw rotation should not 2× the carry distance, regardless of whether the harness ever uses it again.

---

## Files reviewed

| Path | Purpose | Verdict |
|---|---|---|
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/phase_b_surface_tuning/SPEC.md` | Stage 1 contract | Spec gate-C interpretation in question |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/phase_b_surface_tuning/IMPLEMENTER_REPORT.md` | Iter-6 evidence | Gate C explanation factually incorrect; rest verified accurate |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/phase_b_surface_tuning/captures/20260518_103259/sweep.csv` | 182 data rows | Counts confirmed; per-row physics analyzed above |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/phase_b_surface_tuning/captures/20260518_103259/real_shots.csv` | 6 rows | 2/6 usable (H1 straight, H9 straight) |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/phase_b_surface_tuning/captures/20260518_103259/progress.log` | 188 done lines | Confirmed: matches sweep+real_shots row counts |
| `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/SurfaceRolloutHarness.cs` | Harness source | Fix #6 confirmed at lines 588-596; geometry analyzed |
| `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/Editor/SurfaceRolloutMenu.cs` | Menu source | Non-destructive (Fix #2 verified — no SaveScene, no RemoveIfPresent) |

---

## Final verdict (iter-6): ARCHITECT_REVIEW_ESCALATE

Routing to Cesar with three concrete options. Architect-reviewer recommendation is **Option A (accept iter-6 + write Stage 2 against partial dataset)** but the call is Cesar's. No fix list for implementer — the implementation is structurally sound; the question is whether the data shape matches what Stage 2 needs, which only Cesar can rule on.

---

# Iter-7 Review — PASS for accepted scope

**Reviewer:** `golfin-reviewer` (Opus 4.7 1M)
**Iter-7 timestamp:** 2026-05-18 12:55 CEST
**Verdict:** `ARCHITECT_REVIEW_PASS` for the **accepted scope Cesar selected (Option 1)**: Fairway H1/H9/H18, Green H1/H9, Sand H1/H9, plus all putt rows. Stage 2 SPEC is unblocked. CartPath, Tee (all holes) + H18 Green/Sand are explicitly out of scope and deferred to Stage 2-bis per Cesar's call.

## Cesar's Option 1 decision recap (Stage 2 scope)

- **In scope for Stage 2 k-tuning:** Fairway (all 3 holes), Green (H1+H9), Sand (H1+H9), all putt rows (Green+Fairway across all holes).
- **Deferred to Stage 2-bis:** CartPath/Tee restitution (will use a pure vertical-drop capture path, not the -30° horizontal drop). H18 Sand (43%) and H18 Green (71%) excluded due to surface-edge geometry on that specific hole.
- **Rationale:** Cesar explicitly accepted the iter-7 dataset as the basis for Stage 2 once Gate C is reinterpreted against accepted scope. Under accepted scope the Gate-C match rates are uniformly 100% (Fairway H1/H9/H18, Green H1/H9, Sand H1/H9 all 28/28). Implementer's overall "56.2%" was diluted by the now-excluded CartPath/Tee/H18-edge surfaces.

## Iter-7 independent verification

Read CSVs raw with Python `csv.DictReader` (utf-8-sig). All counters recomputed from scratch; implementer claims cross-checked against raw rows.

### Production code containment (PASS)

`git diff Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` shows a single, contained change:
- One new field: `fp3? _airborneOriginOverride` (line 113).
- One new method: `internal void PlaceBallAtAirborne(Vector3 worldPos)` (lines 598-619).
- One six-line early-return added at the top of `GetCurrentOrigin` (lines 1153-1160) that consumes the override and falls through to the existing surface-snap path on null.

`PlacementSnapHelper.Snap` is untouched. No other production class modified. No `EditorSceneManager.SaveScene` / `MarkSceneDirty` / `RemoveIfPresent` calls anywhere in `SurfaceRolloutMenu.cs` (verified by grep — `OpenScene`/`CloseScene` only). `GetCurrentOrigin` continues to fall through to the existing `ballAnimator.CurrentBall.position` branch when `_airborneOriginOverride` is null, so production gameplay is unaffected by design and by inspection.

### Scene state clean (PASS)

```
git status --short
 D .claude/scheduled_tasks.lock                                            (pre-existing)
 M Assets/Scripts/Physics/Viewer/PhysicsLabController.cs                   (intended)
 M Docs/Specs/Active/phase_b_surface_tuning/STATUS.md                      (workflow)
?? Assets/Scripts/Physics/Viewer/Editor/SurfaceRolloutMenu.cs              (new harness)
?? Assets/Scripts/Physics/Viewer/Editor/SurfaceRolloutMenu.cs.meta         (new harness)
?? Assets/Scripts/Physics/Viewer/SurfaceRolloutHarness.cs                  (new harness)
?? Assets/Scripts/Physics/Viewer/SurfaceRolloutHarness.cs.meta             (new harness)
?? Docs/Specs/Active/phase_b_surface_tuning/{ARCHITECT_REVIEW,HEARTBEAT,IMPLEMENTER_REPORT,captures/}.md
```

Zero `.unity` or `.asset` modifications. Iter-4's scene corruption stays reverted.

### Harness iter-7 fixes (PASS — all 6 verified in source)

| Fix | File / line | Verification |
|---|---|---|
| Item 1 — `PlaceBallAtAirborne` use | `SurfaceRolloutHarness.cs:615` in `CaptureRollPath` | Called immediately before `HandleShotResolvedForTests`; matches the controller's single-shot override semantics |
| Item 2 — Drop geometry restored | `SurfaceRolloutHarness.cs:619-621` | `vY = -vHorizontal * tan(30°)` (downward). No residual `+30°` lofted-launch code anywhere in the file |
| Item 3 — Sample jitter | `SurfaceRolloutHarness.cs:607-608` | `(sampleId - 1) * 0.10m` in +X. Confirmed working (see CSV check below) |
| Item 4 — Spin axis | `SurfaceRolloutHarness.cs:86` | `BackspinAxis1a = (0,0,1)` — `Cross((0,0,1),(1,0,0)) = (0,1,0)` upward Magnus. Math verified by hand |
| Item 5 — Draw fix | `SurfaceRolloutHarness.cs:778` | `PlaceBallAtAirborne(teePos)` called before each `CaptureRealShot` shot. Real-shot CSV confirms draw carry now within 1% of straight (was 2×) |
| Item 6 — Holes 9+18 added | `SurfaceRolloutHarness.cs:42` + loop at line 231 | `_holesForSubMode1 = {1,9,18}`; per-hole `OnHoleLoaded/Unloaded`; progress key includes `hole{N}`; `source_hole` is col 2 of sweep.csv |

### Row counters (independent recompute — PASS)

```
Total sweep.csv rows: 546 (header excluded)
By mode: {roll: 420, putt: 126}
By hole: {1: 182, 9: 182, 18: 182}
real_shots.csv: 6 rows
progress.log: 552 done + 552 pending = 1104 lines
```

Implementer's reported 1038 progress.log lines was wrong — actual is 1104 (552 of each). Capture count of 552 (=546+6) is consistent with progress.log and matches the README. Minor reporting error, immaterial to data integrity.

### Gate C recompute (PASS for accepted scope)

```
hole=  1 surface=Fairway      : 28/28 = 100.0%   ACCEPTED
hole=  1 surface=Green        : 28/28 = 100.0%   ACCEPTED
hole=  1 surface=Sand         : 28/28 = 100.0%   ACCEPTED
hole=  9 surface=Fairway      : 28/28 = 100.0%   ACCEPTED
hole=  9 surface=Green        : 28/28 = 100.0%   ACCEPTED
hole=  9 surface=Sand         : 28/28 = 100.0%   ACCEPTED
hole= 18 surface=Fairway      : 28/28 = 100.0%   ACCEPTED
hole= 18 surface=Green        : 20/28 =  71.4%   DEFERRED (Stage 2-bis)
hole= 18 surface=Sand         : 12/28 =  42.9%   DEFERRED (Stage 2-bis)
hole=  1 surface=Tee          :  0/28 =   0.0%   DEFERRED (Stage 2-bis)
hole=  9 surface=Tee          :  4/28 =  14.3%   DEFERRED (Stage 2-bis)
hole= 18 surface=Tee          :  4/28 =  14.3%   DEFERRED (Stage 2-bis)
hole=  1 surface=CartPath     :  0/28 =   0.0%   DEFERRED (Stage 2-bis)
hole=  9 surface=CartPath     :  0/28 =   0.0%   DEFERRED (Stage 2-bis)
hole= 18 surface=CartPath     :  0/28 =   0.0%   DEFERRED (Stage 2-bis)

Accepted-scope match rate: 196/196 = 100.0%
```

Recomputed independently and matches implementer's per-cell numbers exactly. Under accepted scope, Gate C is unambiguous PASS.

### Physical-plausibility spot-checks on accepted-scope rows

**1. `actual_v_at_contact_mps` plausibility (Fairway H1, spin=500, s=1).** Predicted impact speed under -30° drop from 3m: `|v|_contact = sqrt(1.333·vH² + 58.86)` (energy conservation). Observed vs predicted (vH/predicted/actual): 3/8.42/8.56, 6/10.34/10.44, 9/12.92/12.84, 12/15.84/15.54, 15/18.94/18.48, 20/24.33/23.86, 25/29.87/29.25. All within ±2% — matches gravity + minor drag damping. PASS.

**2. Roll monotonic in v_horiz (accepted scope, spin=500, s=1).**
- Fairway H1: 1.47 / 3.26 / 5.65 / 7.98 / 11.04 / 17.31 / 24.17 — strict monotonic ✓
- Fairway H9: 1.95 / 4.20 / 6.72 / 9.59 / 12.93 / 19.64 / 26.44 — strict monotonic ✓
- Fairway H18: 1.41 / 2.77 / 4.71 / 6.97 / 9.66 / 15.16 / 21.44 — strict monotonic ✓
- Green H1: 0.52 / 1.09 / 1.78 / 2.62 / 3.67 / 6.00 / 8.77 — strict monotonic ✓
- Green H9: 0.49 / 1.28 / 2.20 / 2.37 / 3.33 / 5.43 / 7.95 — strict monotonic ✓ (vH=9→12 jump small but increasing)
- Sand H1: 0.067 / 0.104 / 0.124 / 0.183 / **0.345 / 0.314** / 0.606 — NON-MONOTONIC at vH=15→20 dip
- Sand H9: 0.122 / 0.247 / 0.390 / 0.526 / 0.774 / 1.303 / 1.948 — strict monotonic ✓

**Sand H1 non-monotonic finding:** vH=15 (0.345m) > vH=20 (0.314m) at s=1/spin=500. The s=2 sample at vH=20 is 0.524m so the sample-axis spread is large at high vH on Sand H1, but the s=1-only sequence dips. Inspecting all 4 (vH=20, s∈{1,2}, spin∈{500,2700}) Sand H1 rows: rolls = 0.314 / 0.524 / 0.577 / 0.507. At vH=25 the same group is 0.606 / 0.771 / 0.573 / 0.752. So at vH=20 the s=1/spin=500 row is the outlier; the other three at vH=20 are all > the largest vH=15 value (0.345). This looks like a bunker-edge hop where the s=1/spin=500 specific spawn happened to land near the lip and lose extra energy — a real physics phenomenon on Sand, not a data-collection bug. Stage 2 SPEC should be aware that Sand k-tuning will need either median-of-samples or per-row inspection for outlier rejection; a single (vH, spin, sample) point on Sand can legitimately under-report. Sand H9 (different bunker, different geometry) is clean monotonic which corroborates the "edge geometry, not harness bug" reading.

This is a known limitation, NOT a Stage-1 FAIL — Cesar's Option 1 acceptance covered "Sand H1+H9 in-scope" and the implementer's spec deviation #2 already flagged Sand as bounce-on-slope. Documenting in Stage 2 SPEC as required.

**3. Bounce counts (PASS).**
- Roll mode (accepted): Fairway H1/H9/H18 avg 5.57/5.86/4.86 (all >1). Green H1/H9 avg 4.00/3.57 (all >1). Sand H1/H9 avg 1.57/1.71 (28/28 and 20/28 with bounces>1 — edge cases for slow shots on Sand).
- Putt mode: Green H1/H9/H18 all 0 bounces (pure roll). Putt Fairway is heterogeneous (H1: 0-4 bounces, H9: 5 bounces always, H18: 0). The 5-bounce Fairway H9 putts indicate the ball is hitting micro-terrain undulations — physically plausible on a sloped fairway with a real baked height provider, not a harness bug. Accepted as data-as-is; Stage 2 putt-k tuning should be on Green (which is monotonic and clean) and only use Fairway putts as a secondary sanity check.

**4. Sample-axis non-zero delta (PASS with caveat).** Across accepted-scope (vH, spin) pairs (98 total), 84 have differing s=1 vs s=2 roll values. Identical pairs are concentrated on Fairway H18 (10/14) and Sand H9 (4/14). The harness DOES apply the +0.10m X jitter; the convergence on a flat slope-uniform region is physically expected (10cm offset on uniform Fairway → same physics output). Sample-jitter is a working mechanism, not a no-op as iter-6 showed.

**Caveat:** `CapturePuttPath` does NOT apply jitter (no `SampleJitterM` reference in the putt path). All 3 putt samples per (surface, vH) are bit-identical. This is consistent with implementer's iter-7 fix-list which only mentions sample jitter for sub-mode 1a. Not a regression vs spec — spec did not require putt jitter. But Stage 2 must treat 1b putt samples as 1 effective observation per (surface, vH), not 3. Documenting this as a known limitation for Stage 2 SPEC.

**5. Spin-axis non-zero delta (PASS).** 96/98 accepted-scope pairs differ between spin=500 and spin=2700. 1 identical pair on Green H1. Implementer's "209/210 across full dataset" claim is consistent within the accepted scope. Magnus axis fix is working.

**6. Stimpmeter signal (PASS).** Putt Green vH=1.8 reads 3.5333m on H1, H9, and H18 (all bit-identical because putt has no jitter and three holes all use canonical k-Green). Phase-A math predicts 3.58m. 1.4% short. This is the canonical reference row for Stage 2 putt-Green k tuning.

**7. Real shots (PASS with two OOB legs).** Per `real_shots.csv`:
- H1 straight 228.6m, draw3deg roll=0 (ends OOB at the contact pos) — draw-shot direction landed off course but the carry numbers still 229.4m, so the iter-7 spin-axis fix worked at the launch level; the OOB is a hole-1-aim issue not a harness bug.
- H9 both shots clean: straight 220m carry + 21.3m roll, draw 219m carry + 21.3m roll.
- H18 straight OOB at contact; draw3deg clean 223m carry + 22m roll. Same pattern.
- Draw vs straight carry deltas: H1 +0.4%, H9 −0.6%, H18 −1.0%. Iter-6's 2× draw bug is confirmed fixed.

The 2/6 OOB legs are not in accepted Stage 2 scope (real-shot data is a sanity check, not a calibration source) and the 4/6 clean shots all land in the 220-229m / 21-23m roll band — consistent with Phase A's loose `[200, 250]` carry expectation.

## Bbox / containment

N/A — diagnostic harness output, no UI containment claims.

## Accepted scope (final, for Stage 2 SPEC author)

**Included:**
- Fairway: H1, H9, H18 (28 roll rows × 3 holes = 84 rows; all `end_surface == Fairway`).
- Green: H1, H9 (28 roll rows × 2 holes = 56 rows; all `end_surface == Green`).
- Sand: H1, H9 (28 roll rows × 2 holes = 56 rows; all `end_surface == Sand`; H1 has 1 vH-monotonic outlier, documented).
- Putt-Green: H1, H9, H18 (21 putt rows × 3 holes = 63 rows; all clean monotonic; **Stimpmeter row 3.5333m vs 3.58m predict = canonical reference for putt-Green k**).
- Putt-Fairway: H1 (clean) + H9 (heterogeneous bounces) + H18 (clean) = 63 rows. Use H1+H18 as primary; H9 as sanity check only.

**Excluded (deferred to Stage 2-bis):**
- CartPath all holes (0% Gate C — drop geometry overshoots narrow CartPath at any vH).
- Tee all holes (0-14% Gate C — same root cause).
- Green H18 (71% Gate C — surface-edge geometry on H18 specifically).
- Sand H18 (43% Gate C — bunker geometry on H18 specifically).

**Stage 2-bis SPEC will need:** pure-vertical-drop capture method (zero horizontal velocity, lands on exactly the discovery cell) for CartPath/Tee/narrow-surface restitution measurement. Architect to write that SPEC after Stage 2 ships.

## Known limitations to document in Stage 2 SPEC

1. **Sample-jitter strategy:** sub-mode 1a applies +0.10m X jitter to sample_id=2. Most accepted-scope pairs (84/98) produce non-zero deltas. ~14% of (vH, spin) pairs converge to identical roll on uniform Fairway/Sand terrain — this is physically expected, not a bug, but Stage 2 should treat samples as a tie-breaking / outlier-detection input rather than as independent observations.
2. **Putt-sample no-op:** sub-mode 1b does NOT jitter putts (3 samples per (surface, vH) are bit-identical). Effective putt observations = 1 per (surface, vH). Stimpmeter signal is still canonical because the single observation matches Phase-A math.
3. **Spin-axis evidence:** 96/98 accepted-scope (vH, sample) pairs differ between spin=500 and spin=2700. The Magnus effect IS being applied. 1 identical pair on Green H1 noted; minor.
4. **Draw shot:** the iter-6 2× carry bug is FIXED via `PlaceBallAtAirborne(teePos)` before each real shot. Draws now match straights within 1%.
5. **Ball spawn airborne:** uses new `PhysicsLabController.PlaceBallAtAirborne` (single-shot override, consumed on the next `HandleShotResolvedForTests` call). This is the production code change Cesar approved as Option B in iter-6 review.
6. **Sand H1 non-monotonic outlier** at (vH=20, s=1, spin=500): roll=0.314m < roll(vH=15)=0.345m. The other three samples at vH=20 are all higher (0.524, 0.577, 0.507). Bunker-edge physics. Stage 2 should use median-across-samples or filter outliers when fitting k-Sand from H1.

## Cesar handoff for Stage 2 SPEC

**Cesar writes Stage 2 SPEC** for k-tuning of Fairway / Green / Sand using:
- **Source CSV:** `Docs/Specs/Active/phase_b_surface_tuning/captures/20260518_122845/sweep.csv`
- **Row filter:** `surface_target ∈ {Fairway, Green, Sand}` AND `first_contact_surface == surface_target`. Practically this is `end_surface == surface_target` for the accepted scope (Gate C 100% for every (hole, surface) pair listed above).
  - Additionally exclude `source_hole == 18` for `surface_target ∈ {Green, Sand}`.
- **Canonical reference rows:**
  - Putt-Green k: Stimpmeter row `(mode=putt, surface_target=Green, target_v_horizontal_mps=1.8, source_hole=1)` → `roll_distance_m=3.5333m` vs Phase-A predict 3.58m.
  - Roll-Fairway k: H1+H9+H18 vH-monotonic series (spin=500, s=1) are the primary calibration anchors.
  - Roll-Green k: H1+H9 vH-monotonic series.
  - Roll-Sand k: H9 vH-monotonic series (clean); H1 series with the documented outlier filter.
- **Out of scope (Stage 2-bis):** CartPath, Tee, H18 Green, H18 Sand — pure-vertical-drop capture path required, architect SPEC to follow.

## Final iter-7 verdict: ARCHITECT_REVIEW_PASS

Independent recompute of every accepted-scope counter confirms the implementer's numbers. Production code change (`PhysicsLabController.PlaceBallAtAirborne`) is minimal and contained. Scene state is clean (zero `.unity`/`.asset` mutations). All 6 iter-7 work items are present in the harness source with no residual Fix #6 lofted-launch code. The accepted-scope dataset (196 roll rows + 126 putt rows, all 100% Gate C under accepted scope) is sound basis for Stage 2 k-tuning. The Sand H1 non-monotonic outlier and putt-sample no-op are documented as known limitations for Stage 2 SPEC, not Stage 1 failures.

STATUS → `ARCHITECT_REVIEW_PASS` (not DONE — Cesar moves to DONE after writing the Stage 2 SPEC).
