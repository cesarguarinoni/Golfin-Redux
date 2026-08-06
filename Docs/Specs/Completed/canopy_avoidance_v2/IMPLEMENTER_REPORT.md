# Implementer Report — `canopy_avoidance_v2`

**Iteration shape:** `bot-canopy:first-pass`

## Implementation summary

Extended `BotTreeProbe.cs` (additive only) with three new public statics: `ApexForCarry` (parabolic height model per club, scaled to actual carry), `CountCanopyContacts` (full-line probe at modelled trajectory height counting canopy-only hits), and `TrySampleTreeAwareAimError` (scored sampler: trunk = hard reject unchanged, canopy = soft preference among survivors). Swapped the D2 call in `VersusBot.cs` from `TrySampleTrunkClearAimError` to `TrySampleTreeAwareAimError`, adding a `DebugDisableCanopyPreference` field (false = v2 scored; true = Order 352 exact). Extended the 2b log line with `canopyContacts=<n>`. Added 5 new EditMode tests (A–E) in `BotTreeProbeTests.cs` covering all four canopy-sampler gates. The measurement gate sweep over real Hole_08 data (3926 trees, N=1000 trials) produced `canopy_invariants.json` with all 3 assertions PASS.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Physics/Viewer/BotTreeProbe.cs` | Modified — added v2 constants (`ApexAtFullCarry`, `FullCarryForClub`) + three new public statics: `ApexForCarry`, `CountCanopyContacts`, `TrySampleTreeAwareAimError` |
| `Assets/Scripts/Physics/Viewer/VersusBot.cs` | Modified — D2 block: `TrySampleTrunkClearAimError` → `TrySampleTreeAwareAimError`; added `DebugDisableCanopyPreference` field; log extended with `canopyContacts=<n>` |
| `Assets/Scripts/Physics/Tests/BotTreeProbeTests.cs` | Modified — 5 new tests: Test A (NullTrees), Test B (AllCanopyFree), Test C (CanopyFreePrefersOverHeavy), Test D (AllTrunkBlocked), Test E (ApexDriftGuard_TableMatchesSim) |
| `Docs/Specs/Active/canopy_avoidance_v2/canopy_invariants.json` | Created — measurement gate output (N=1000, Hole 8, seed=42); all 3 assertions PASS |
| `Docs/Specs/Active/canopy_avoidance_v2/HEARTBEAT.log` | Created — activation + iter-1 baseline |
| `Docs/Specs/Active/canopy_avoidance_v2/STATUS.md` | Created (set to IMPLEMENTER_WORKING on activation) |

## Screenshot

- **Canonical screenshot:** N/A — this task is pure math + EditMode tests; SPEC §2 Out explicitly forbids play mode, scene edits, and prefab edits. No visual surface.
- **Play mode:** No (SPEC concurrency constraint: "DO NOT enter play mode")
- **Scene loaded:** None — EditMode test runner only.

## Acceptance checklist

### Gate 1 — EditMode tests

| Item | Result | Justification |
|---|---|---|
| `trees == null → first sample, single draw, canopyContacts == 0` | PASS | Test A (`TrySampleTreeAwareAimError_NullTrees_AcceptsFirstSampleCanopy0`) PASSED; confirmed via tool output: `BotTreeProbeTests.TrySampleTreeAwareAimError_NullTrees_AcceptsFirstSampleCanopy0 → Passed`. Code fast-path at BotTreeProbe.cs line 285: `if (trees == null) { deltaAimDeg = sampleRange(...); canopyContacts = 0; return true; }` |
| `all samples trunk-clear and canopy-free → returns smallest |delta|` | PASS | Test B (`TrySampleTreeAwareAimError_AllCanopyFree_ReturnsSmallestAbsDelta`) PASSED. Among all trunk-clear canopy-free samples, tie-break on `Mathf.Abs(delta)` picks smallest `|deltaAimDeg|`. |
| `one sample canopy-free, others canopy-heavy → returns the canopy-free one even if |delta| is larger` | PASS | Test C (`TrySampleTreeAwareAimError_CanopyFreePrefersOverHeavy_DespiteLargerDelta`) PASSED. Scored sampler picks `bestCanopy=0` candidate over a smaller-|delta| candidate with `bestCanopy=3`. |
| `all samples trunk-blocked → false, deltaAimDeg == 0` | PASS | Test D (`TrySampleTreeAwareAimError_AllTrunkBlocked_ReturnsFalseAndZero`) PASSED. Returns false, deltaAimDeg=0, canopyContacts=-1 — identical clamp to Order 352. |
| `apex drift guard: table matches BallSimulation within ±1.0 m` | PASS | Test E (`ApexDriftGuard_TableMatchesSim`) PASSED. Driver: spec 7.92 m / sim-derived within ±1.0 m; iron7: spec 5.29 m; wedge: spec 14.42 m; putter: 0.0 m (excluded upstream). |
| `Full suite green (1000 total / 997 passed / 0 failed / 3 skipped)` | PASS | Full EditMode suite run (no filter): `TotalTests=1000, PassedTests=997, FailedTests=0, SkippedTests=3`. The 3 skips are pre-existing Stage C1 skips in `HoleCompleteDriverTests` (`_OnInCupTerminal_AtPar_ShowsSuccessReplay`, `_FiresMarkHoleComplete`, `_OverPar_ShowsFailedRetryAndLockedNext`) — unchanged from baseline 995/992/0/3. Baseline +5 new tests = 1000/997/0/3. |
| `TrySampleTrunkClearAimError Order 352 tests remain green` | PASS | All 4 pre-existing `TrySampleTrunkClearAimError_*` tests PASSED: `_AllSamplesBlocked_ReturnsFalseAndZeroDelta`, `_AllSamplesClear_ReturnsTrue`, `_NullProvider_ReturnsFirstSample`, `_TrunkBlocksEarlySamples_ReturnsClearSample`. `TrySampleTrunkClearAimError` is untouched — additive change only. |

### Gate 2 — Measurement gate

| Item | Result | Justification |
|---|---|---|
| `clampRate_v2 <= clampRate_order352` (hard fail if it rises) | PASS | JSON A1: v2=0.0580 == order352=0.0580. Equal by construction: both methods use the same pre-generated sample pool (seed=42, N=1000, maxTries=5) so trunk-block rate is identical. Clamp count 58/1000. See `canopy_invariants.json`. |
| `meanCanopyContacts_v2 < meanCanopyContacts_order352` | PASS | JSON A2: v2=3.534 < order352=4.135 (14.5% reduction). Order 352 contacts post-hoc computed via `CountCanopyContacts` on the selected shot trajectory (Order 352's `TrySampleTrunkClearAimError` returns first trunk-clear sample regardless of canopy, which may have higher canopy exposure than the scored v2 pick). See `canopy_invariants.json`. |
| `trunkBlockRate_v2 == trunkBlockRate_order352` | PASS | JSON A3: both 0.5580 (shared value). Identical by construction: trunk check uses the same `LineHasTrunkInWindows` on the same sample pool for both methods. See `canopy_invariants.json`. |
| Invariant JSON dumped with per-assertion PASS/FAIL | PASS | `Docs/Specs/Active/canopy_avoidance_v2/canopy_invariants.json` exists and contains `"result": "PASS"` for all 3 assertions. Sweep params: N=1000, maxTries=5, aimErrorDegMax=6.0° (worst-case level-1 bot), carry=132.9m (Driver full), apex=7.92m, Hole 8 tee→pin, seed=42, 3926 tree instances. |
| §1.1 table reproduced | PASS | JSON `section1_1_table` field reproduces per-frac canopy hit rates: frac 0.50 trunk=39.75% canopy=96.38%, frac 0.62 trunk=41.13% canopy=97.38% — confirming that at the mid-flight band nearly all lines are canopy-blocked, validating the soft-preference design. |

### Gate 3 — No-op proofs

| Item | Result | Justification |
|---|---|---|
| Treeless hole identical to HEAD | PASS | `TrySampleTreeAwareAimError` fast-path (BotTreeProbe.cs line 285): `if (trees == null) { deltaAimDeg = sampleRange(...); canopyContacts = 0; return true; }` — single draw, first sample, canopyContacts=0. Bit-identical to `TrySampleTrunkClearAimError` null-path. Test A also covers this directly. |
| Putts unchanged | PASS | VersusBot.cs line 758: `if (!isPutt && trees != null && !DebugDisableTreeRecheck)` — the `!isPutt` guard is unchanged; putts skip the canopy sampler entirely and fall through to the unchecked `Random.Range(-bkt.aimErrorDegMax, bkt.aimErrorDegMax)` branch (line 779). No change to putter path. |
| `DebugDisableCanopyPreference = true` reproduces Order 352 exactly | PASS | `TrySampleTreeAwareAimError` at BotTreeProbe.cs lines 292-296: `if (disableCanopyPreference) { bool ok = TrySampleTrunkClearAimError(trees, ball, safeYaw, carry, aimErrorDegMax, maxTries, sampleRange, out deltaAimDeg); canopyContacts = ok ? 0 : -1; return ok; }` — delegates directly to `TrySampleTrunkClearAimError`, the unmodified Order 352 path. Execution is byte-identical. |

### Standing-ban compliance

| Item | Result | Justification |
|---|---|---|
| `git diff HEAD -- Assets/Scripts/Physics/` shows only task files | PASS | `git diff HEAD --name-only -- Assets/Scripts/Physics/` returns exactly: `Assets/Scripts/Physics/Tests/BotTreeProbeTests.cs`, `Assets/Scripts/Physics/Viewer/BotTreeProbe.cs`, `Assets/Scripts/Physics/Viewer/VersusBot.cs` — no other files. All 3 are explicitly in SPEC §7 Expected diff. |
| No `*Gate` method added to `Scenarios.cs` | PASS | `Scenarios.cs` not touched. `git diff HEAD -- Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` is empty. |
| `M_SplashDroplet.mat`, `M_SplashFoam.mat`, `M_SplashRing.mat` untouched | PASS | Pre-existing dirty in baseline (HEARTBEAT.log iter-1 baseline: `M Assets/Resources/FX/M_SplashDroplet.mat`, `M Assets/Resources/FX/M_SplashFoam.mat`, `M Assets/Resources/FX/M_SplashRing.mat`). Not modified by this task. |
| No `LabScaffold.unity` mutation | PASS | `LabScaffold.unity` pre-existing dirty in baseline. Not touched by this task. |
| No `#if UNITY_EDITOR` in modified files | PASS | BotTreeProbe.cs comment line 7: `// Production-safe: NO #if UNITY_EDITOR — VersusBot ships in player builds.` VersusBot.cs comment: `// No #if UNITY_EDITOR — field ships in player builds (production-safe).` Confirmed by inspection. |
| `PhysicsLabController.cs` untouched | PASS | Not in task scope; no diff. |
| `ShotUI/` files untouched (concurrent session) | PASS | Pre-existing dirty baseline shows `ShotUI/HoleCardWidget.cs`, `ShotUI/PowerGaugeWidget.cs`, `ShotUI/ShotInProgressUiGate.cs` as pre-existing. Not touched by this task. |
| No play mode entered | PASS | SPEC concurrency constraint observed. All verification done via EditMode test runner only. |
| No `git add -A` or staged files from pre-existing drift | PASS | No staging performed. Pre-existing dirty files remain in their pre-existing state per baseline. |

## Spec deviations

None. The implementation matches SPEC exactly:
- `TrySampleTrunkClearAimError` left in place, untouched (Order 352 tests remain green).
- `TryFindTrunkClearAim` windows untouched.
- No hard canopy rejection.
- No BallSimulation, CSV, prefab, scene edits.

## Console output

Not applicable — no play mode entered. EditMode test runner output: 1000 tests, 997 passed, 0 failed, 3 skipped. No errors or warnings from the modified files.

## Open questions for Architect

None.

---

## Evidence citations

- `Docs/Specs/Active/canopy_avoidance_v2/canopy_invariants.json` — all 3 assertions PASS (A1 clampRate 0.0580==0.0580, A2 meanCanopy 3.534<4.135, A3 trunkBlockRate 0.5580==0.5580)
- EditMode full suite run: Summary `{"TotalTests":1000,"PassedTests":997,"FailedTests":0,"SkippedTests":3}` — tool result from `tests-run` (no filter)
- `git diff HEAD --name-only -- Assets/Scripts/Physics/` → 3 files only (BotTreeProbeTests.cs, BotTreeProbe.cs, VersusBot.cs)
- BotTreeProbe.cs line 285 (treeless fast-path), 292-296 (DebugDisableCanopyPreference delegating to TrySampleTrunkClearAimError), 758 VersusBot.cs (!isPutt guard unchanged)
