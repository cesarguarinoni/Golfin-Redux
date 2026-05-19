# Implementer Report — `phase_b_surface_tuning` Stage 2: Apply k-value updates

## Implementation summary

Three rows in `Assets/Resources/Physics/surfaces.csv` were edited per spec: Fairway rolling_resistance bumped 0.18→0.23, Green rolling_resistance bumped 0.12→0.34, Sand rolling_resistance bumped 0.70→1.02. All other 8 rows are byte-identical. The full physics test suite (294/294) remained passing. The harness was re-run via `GOLFIN/Physics/Run Surface Rollout Sweep` MenuItem, producing 546 data rows at `Docs/Specs/Active/phase_b_surface_tuning/captures/20260519_061402/`. The Stimpmeter putt is unchanged (3.5333m). However, all three harness acceptance bands (Fairway/Green/Sand v=25 roll targets) are FAIL because the harness "roll distance" measurement is dominated by bounce-phase horizontal displacement, which k does not control — see Architectural finding below.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Resources/Physics/surfaces.csv` | MODIFIED — Fairway 0.18→0.23, Green 0.12→0.34, Sand 0.70→1.02 (3 rows only) |
| `Docs/Specs/Active/phase_b_surface_tuning/captures/20260519_061402/sweep.csv` | NEW — 546 post-tune data rows |
| `Docs/Specs/Active/phase_b_surface_tuning/captures/20260519_061402/real_shots.csv` | NEW — 6 sub-mode 2 real-shot rows |
| `Docs/Specs/Active/phase_b_surface_tuning/captures/20260519_061402/progress.log` | NEW — 552 done entries |
| `Docs/Specs/Active/phase_b_surface_tuning/IMPLEMENTER_REPORT.md` | NEW — this file |
| `Docs/Specs/Active/phase_b_surface_tuning/HEARTBEAT.log` | NEW — run log |

No `.cs` files modified. No `putt.csv` modified. No `.unity` or `.asset` files modified.

## Screenshot

This task is numerical/CSV verification, not visual UI. Per SPEC.md § Smoke evidence: "Numerical CSV correctness IS the evidence." No game-view screenshot is required or applicable for Stage 2.

- **Captured at:** N/A — data task; evidence is the CSV files at `captures/20260519_061402/`
- **Scene loaded:** N/A
- **Play mode:** Run for harness sweep (exited clean after sweep completion)

## Pre vs Post comparison table

Medians using Cesar's filter (mode=roll, surface_target ∈ {Fairway, Green, Sand}, end_surface=surface_target, source_hole ∈ {1,9}). Sand uses H9-only for per-spec derivation.

| Surface | v_tgt | pre-k | post-k | pre roll | post roll | delta | Acceptance band |
|---|---|---|---|---|---|---|---|
| Fairway | 20.0 | 0.18 | 0.23 | 18.4749m | 18.4455m | −0.029m | (v=25 band) |
| Fairway | 25.0 | 0.18 | 0.23 | 25.2302m | 25.1748m | −0.055m | [17–23] **FAIL** |
| Green | 20.0 | 0.12 | 0.34 | 5.6867m | 5.5981m | −0.089m | (v=25 band) |
| Green | 25.0 | 0.12 | 0.34 | 8.3199m | 8.1612m | −0.159m | [2.5–4.0] **FAIL** |
| Sand H9 | 20.0 | 0.70 | 1.02 | 1.3030m | 1.2220m | −0.081m | (v=25 band) |
| Sand H9 | 25.0 | 0.70 | 1.02 | 1.9482m | 1.8192m | −0.129m | [1.0–1.5] **FAIL** |
| Stimpmeter (Green putt v=1.80) | — | putt.csv k=0.50 (unchanged) | same | 3.5333m | 3.5333m | 0.000m | 3.5333 ±0.001 **PASS** |

The k changes produce directionally correct reductions (all deltas negative, as expected), but the magnitudes are ~2% vs the expected 63–71% reduction.

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| `surfaces.csv` three rows edited per spec (Fairway 0.18→0.23, Green 0.12→0.34, Sand 0.70→1.02); all other rows byte-identical | PASS | File verified via Read after edit: Fairway=0.23, Green=0.34, Sand=1.02; GreenCollar/Semirough/Rough/Tee/BunkerLip/CartPath/Water/OOB all unchanged |
| Full test suite re-run: **294/294 PASS**, no new failures | PASS | `tests-run` via MCP HTTP session returned `{"Status":"Passed","TotalTests":294,"PassedTests":294,"FailedTests":0}` (Duration: 00:00:18). All 5 `RollAndPuttTuningTests` including `Stimpmeter_Green_RollsTo3to4Meters` passed. |
| Harness re-run produces new CSV at `Docs/Specs/Active/phase_b_surface_tuning/captures/<new_timestamp>/sweep.csv` | PASS | `captures/20260519_061402/sweep.csv` — 547 lines (header + 546 rows). `progress.log` shows 552 done entries (matches Stage 1 count). Console logged `[SurfaceRolloutHarness] All sweeps complete. 552 captures.` |
| Fairway v=25 median roll: target **17–23 m** | FAIL | Post-tune median = 25.1748m (pre-tune: 25.2302m, delta −0.055m). Target not met. See Architectural finding below. |
| Green v=25 median roll: target **2.5–4.0 m** | FAIL | Post-tune median = 8.1612m (pre-tune: 8.3199m, delta −0.159m). Target not met. See Architectural finding below. |
| Sand H9 v=25 median roll: target **1.0–1.5 m** | FAIL | Post-tune median = 1.8192m (pre-tune: 1.9482m, delta −0.129m). Target not met. See Architectural finding below. |
| Green putt Stimpmeter (v_target=1.80) **unchanged**: 3.5333 m ± 0.001 | PASS | Post-tune Stimpmeter = 3.5333m (identical to pre-tune bit-exact). Delta = 0.0000m. Confirms `surfaces.csv` edit does not bleed into putt path. |
| No production code (`.cs`) touched | PASS | `git diff HEAD --name-only` shows only `Assets/Resources/Physics/surfaces.csv` and the new `Docs/Specs/Active/phase_b_surface_tuning/` files. No `.cs` changes. |
| No `putt.csv` touched | PASS | `Assets/Resources/Physics/putt.csv` not modified; Stimpmeter value unchanged confirms this. |
| No test-band edits | PASS | All test files are byte-identical to pre-task; `tests-run` count unchanged at 294. |
| `IMPLEMENTER_REPORT.md` documents pre-vs-post numbers at v=20 and v=25 for all three surfaces, plus Stimpmeter unchanged confirmation | PASS | See Pre vs Post comparison table above. v=20 and v=25 for Fairway, Green, Sand H9 all documented. Stimpmeter PASS confirmed. |

## Known FAIL items

### FAIL 1 — Fairway v=25 median roll 25.17m vs target 17–23m

### FAIL 2 — Green v=25 median roll 8.16m vs target 2.5–4.0m

### FAIL 3 — Sand H9 v=25 median roll 1.82m vs target 1.0–1.5m

**Common root cause — bounce-dominated measurement:**

The harness measures `roll_distance_m = XZDist(first_contact_pos, end_pos)`. This metric includes horizontal displacement accumulated across ALL bounces after the first contact, PLUS the final roll phase. The rolling_resistance k parameter only controls the final roll phase (after the last bounce when `vnOut < RollTransitionThreshold`).

Evidence from runtime `[RollStep]` logs confirms the new k values ARE loaded and applied correctly: Green shows `k=0.340` (new value), Fairway shows `k=0.230` (new value). The physics simulation is correct.

The disconnect: for a 29.15 m/s approach shot on Green (4 bounces observed in data), the horizontal displacement during 4 bounces is ~8m. The final roll phase entered at very low horizontal speed (post-4th-bounce with tangent_friction=0.75 applied each time: 0.25^4 × 25m ≈ 0.098m/s entering roll) contributes only ~0.28m with k=0.12 and ~0.10m with k=0.34. The observable difference is ~0.18m — matching the measured delta of −0.159m.

**Implication:** The k parameter has correct leverage in real gameplay for low-speed shots that land and roll without multiple high-speed bounces (short irons, putts, chip-and-run). For high-speed approach shots (v_contact ~29m/s, multiple bounces), reducing total measured roll distance requires reducing the bounce-phase horizontal carry — which is controlled by `tangent_friction` and `restitution`, NOT `rolling_resistance`.

**To achieve the SPEC's acceptance bands (Fairway 17–23m, Green 2.5–4.0m, Sand 1.0–1.5m at v=25), the Architect would need to either:**
1. Raise `tangent_friction` for Fairway/Green (more horizontal energy absorbed per bounce), OR
2. Lower `restitution` for Fairway/Green (less bounce height = fewer subsequent bounces), OR
3. Redesign the acceptance criteria to target the roll phase specifically (measure from LAST bounce to end), OR
4. Accept that these k-bumps are correct for the roll phase and the harness acceptance bands were back-solved from the wrong formula.

The SPEC's back-solve formula `k_new = k_cur × (d_obs/d_target)` assumes `d ∝ 1/k`, which requires pure roll (no multi-bounce). The harness measurement includes multi-bounce horizontal carry which is k-independent.

## Spec deviations

None. All edits are exactly per SPEC.md §Implementation. The FAILs are not deviations — they are findings from the verification gate that the SPEC explicitly mandated.

## Console output

Key console entries from harness run (from Unity Editor.log, lines ~3260000–3307503):

```
[SurfaceRolloutHarness] Waiting 8.0s for startup...
[RollStep] ... surface=Green k=0.340 rollMul=1.000 stopSpeed=0.050 ...   (confirming new Green k)
[RollStep] ... surface=Fairway k=0.230 rollMul=1.000 stopSpeed=0.100 ... (confirming new Fairway k)
[SurfaceRolloutHarness] Hole 18 done. Total so far: 552
[SurfaceRolloutHarness] Sub-mode 2 done. Total: 552
[SurfaceRolloutHarness] All sweeps complete. 552 captures. Output: .../captures/20260519_061402
```

Test suite run: `{"Status":"Passed","TotalTests":294,"PassedTests":294,"FailedTests":0,"Duration":"00:00:18.4889520"}`

## Open questions for Architect

1. **Acceptance band strategy:** The harness acceptance bands (Fairway 17–23m, Green 2.5–4.0m, Sand 1.0–1.5m) cannot be achieved by adjusting `rolling_resistance` alone. The multi-bounce horizontal carry (controlled by `tangent_friction` and `restitution`) dominates the measured distance by ~97%. Should this SPEC be closed as PASS on the k-edit step (correct direction, new k values applied, Stimpmeter unchanged) with a separate Phase B sub-task to address tangent_friction/restitution tuning? Or should the k values be reverted and the approach changed?

2. **k-value utility scope:** The new k values ARE applied in-game and WILL reduce roll for scenarios with few or no bounces (chip shots, putts, short-iron approach shots that check quickly). Is this the correct scope for Stage 2 (i.e., putt and short-game feel), with the over-roll on green from full approach shots addressed separately via tangent_friction?

3. **Per SPEC §Re-validation gate:** "If any test fails: stop, do NOT escalate auto-bump test bands." Tests are all PASS (294/294). The failing items are acceptance BAND checks, not unit tests. Per pipeline rules, acceptance band FAILs route to `READY_FOR_ARCHITECT_REVIEW`. This report is correctly routing there.
