# SELF_REVIEW — stamina_model (Phase 1)

**Reviewer:** golfin-self-reviewer
**Iteration:** N=1
**Timestamp:** 2026-06-29 17:26 CEST
**Verdict:** **FORWARD_TO_ARCHITECT**

---

## Task class

Pure C# logic + EditMode unit test task. NO UI, NO Figma reference, NO canonical screenshot. The visual-diff steps (Step 1 pixel description, Step 2 Figma A/B, Step 3 spec checklist against pixels, Figma fidelity table, bbox geometry, capture-helper compliance, scene-mutation audit for capture paths, production-flow capture) are **not applicable** — this task touches no scene, no GameObject, no UI. Verification is via:

1. Independent test re-run.
2. Source spot-check vs SPEC §6 formulas and §7 edge cases.
3. SPEC §2 scope (no OUT-of-scope file touched).
4. Assembly placement + CSV-load-convention check.
5. CSV authored↔runtime parity.

---

## Independent test re-run (gate)

Re-ran the EditMode suite via Unity MCP `tests-run` with `testClass: "StaminaModelTests"`:

```
Summary:
  Status:       Passed
  TotalTests:   770
  PassedTests:  28
  FailedTests:  0
  SkippedTests: 0
  Duration:     00:00:01.8853830  (~1.89 s)
```

**28/28 PASS, 0 FAIL, 0 SKIP, ~1.89 s.** Matches the implementer's reported count and duration exactly. Independent confirmation = PASS.

(Note: a first attempt with the fully-qualified class name `Golfin.Core.Stamina.Tests.StaminaModelTests` returned HTTP 500 "No tests found"; the short class name `StaminaModelTests` worked. MCP wire-format quirk, not a real test failure. The full-suite run with no filter also reports 770 total / 767 passed / 0 failed / 3 intentionally-skipped Stage-C1 tests — fully consistent with all 28 stamina tests passing.)

---

## SPEC §8 unit test list — walk

| # | Spec assertion | Test method | Verdict |
|---|---|---|---|
| 1 | `Parse` reads all 12 keys; `DegradedStats={Strength,ClubControl}` | `Parse_ReadsAll12Keys` | PASS |
| 2 | `MaxCondition(9)==114`, `(27)==222`, `(0)==60` | `MaxCondition_StaminaStat{9,27,0}_Returns…` (3) | PASS |
| 3 | `DrainForHole()==8` | `DrainForHole_Returns8` | PASS |
| 4 | `RegenPerHour(9)==30`, `(40)==92` | `RegenPerHour_Recovery{9,40}_Returns…` (2) | PASS |
| 5 | `RegenForElapsed(9,2h)==60`; zero→0; negative→0 | 3 tests | PASS |
| 6 | `ConditionPct(57,9)==0.5`; overflow→1; negative→0 | 3 tests | PASS |
| 7 | `PenaltyFor(0.80)==0` → `EffectiveStat(20,0.80)==20` | 2 tests | PASS |
| 8 | `PenaltyFor(0.0)==0.33` → `EffectiveStat(20,0.0)==13` | 2 tests | PASS |
| 9 | `PenaltyFor(0.20) < PenaltyFor(0.05)` and both `< 0.33` | `PenaltyFor_IsMonotonic_And_BelowFloor` | PASS |
| 10 | `MeterState(0.70)=High`, `(0.45)=Mid`, `(0.20)=Low` | 3 tests | PASS |
| 11 | `IsLowConditionFlag(0.20)==true`, `(0.30)==false` | 2 tests | PASS |
| 12 | `IsDegraded("strength")==true`, `("Recovery")==false` (+`ClubControl`, `Unknown`) | 4 tests | PASS |
| 13 | Calling `MaxCondition` before `Configure` throws | `MaxCondition_BeforeConfigure_Throws` | PASS |

All 13 spec assertions are covered by the 28 test methods; all 28 passed independently.

---

## Source spot-checks (SPEC §6 formulas + §7 edge cases)

Read `Assets/Scripts/Core/Stamina/StaminaConfig.cs` and `StaminaModel.cs`:

**§6 Formula fidelity:**
- `MaxCondition`: `(int)Math.Round(TankBase + staminaStat * TankPerStaminaPoint)` — matches spec. Round result for the three test inputs (9, 27, 0) is exact (114, 222, 60); no banker's-rounding ambiguity in spec test cases. PASS.
- `DrainForHole`: returns `_config.DrainPerHole`. PASS.
- `ConditionPct`: **divide-by-zero guard on line 60 (`if (maxCond <= 0) return 0f;`) precedes the divide** — safe. Result is `Math.Max(0f, Math.Min(1f, condition / maxCond))`, i.e. clamp01. PASS.
- `RegenPerHour`: linear, matches §6. PASS.
- `RegenForElapsed`: explicit `if (elapsed.TotalHours <= 0d) return 0f;` plus a `Math.Max(0f, …)` belt-and-braces — both negative and zero TimeSpan correctly yield 0. PASS.
- `PenaltyFor`: early-return 0 when `pct >= ComfortThresholdPct`; otherwise `t = (Comfort - pct) / Comfort`, clamp01 of t, then `FloorPenalty * Math.Pow(t, PenaltyCurveExp)`. Matches spec line-for-line, including the explicit clamp01.
  - Manual verification at pct=0: `t = (0.70-0)/0.70 = 1.0`; `pow(1, 1.6) = 1`; result = `0.33 * 1 = 0.33` ✓.
  - Monotonicity: `t` is linear-decreasing in `pct`; `pow(t, 1.6)` is monotone-increasing in `t` for `t ∈ [0,1]`; therefore `PenaltyFor` is monotone non-increasing in `pct`. PASS.
- `EffectiveStat`: `(int)Math.Round(baseStat * (1f - PenaltyFor(pct)))`. At pct=0 with base=20: `20 * 0.67 = 13.4` → `Math.Round(13.4) = 13` ✓. PASS.
- `MeterState`: chained `>=` thresholds — exactly the spec's piecewise definition. (Boundary `pct == MeterMidPct == 0.30` returns `Mid` because of `>=`, consistent with §10 note that meter_mid_pct (0.30) sits **slightly above** low_condition_flag_pct (0.25) — meter turns red at the same value the flag trips ON, by design.) PASS.
- `IsLowConditionFlag`: `pct < _config.LowConditionFlagPct`. PASS.

**§7 Edge cases:**
- `staminaStat=0` → `MaxCondition = round(60) = 60` ✓ (tested).
- `MaxCondition=0` guard → `ConditionPct=0` ✓ (guard on line 60). Default-CSV `TankBase=60` so MaxCondition is never 0 at runtime; guard is defensive.
- Overflow/negative `condition` → clamp via `Math.Max(0f, Math.Min(1f, …))` ✓ (tested).
- `pct >= Comfort` → penalty exactly 0 → `EffectiveStat == base` ✓ (tested).
- `pct = 0` → penalty `= FloorPenalty` exactly ✓ (tested).
- Monotonicity ✓ (tested).
- `IsDegraded` case-insensitive: uses `StringComparison.OrdinalIgnoreCase` ✓. Unknown stat → false ✓. Null guard present (line 108) — defensive extra not required by spec.
- Before-Configure throw: `EnsureConfigured()` throws `InvalidOperationException` with a clear message ✓ (tested). `EnsureConfigured()` is called by every public read method — full coverage.

**`Configure` idempotency:** sets `_config` and flips `_configured=true`. Repeated calls overwrite — acceptable for "call once at boot" semantics and useful for tests / hot-reload.

**`ResetForTests`:** declared `public static` inside `#if UNITY_EDITOR`. The implementer flagged this as a deviation (spec did not specify access modifier). `public+UNITY_EDITOR` is sound — `internal` would require `InternalsVisibleTo` across the asmdef boundary; the editor-only guard ensures it's stripped from player builds. Non-observable deviation, fine.

---

## Purity check — no Resources/IO in StaminaModel + StaminaConfig.Parse

| File | `using UnityEngine` | `Resources.Load` | Verdict |
|---|---|---|---|
| `StaminaConfig.cs` | NO (only `System`, `System.Collections.Generic`, `System.Globalization`, `System.Linq`) | NO | PURE ✓ |
| `StaminaModel.cs` | NO (only `System`, `System.Collections.Generic`, `System.Linq`) | NO | PURE ✓ |
| `StaminaConfigLoader.cs` | YES | YES (`Resources.Load<TextAsset>("Gameplay/stamina_economy")`) | bootstrap-only ✓ |

PASS — purity preserved exactly as SPEC §3 requires.

---

## CSV-load convention match

Read `Assets/Scripts/Gameplay/Config/ControlsConfigLoader.cs` to verify the claimed pattern match:

| Aspect | `ControlsConfigLoader` | `StaminaConfigLoader` | Match? |
|---|---|---|---|
| Load call | `Resources.Load<TextAsset>("Gameplay/controls")` | `Resources.Load<TextAsset>("Gameplay/stamina_economy")` | YES |
| Resources path | `Assets/Resources/Gameplay/controls.csv` | `Assets/Resources/Gameplay/stamina_economy.csv` | YES |
| Null guard | warns + returns defaults | logs error + leaves model unconfigured | Equivalent |
| Line parsing | `text.Split('\n')` → `Trim()`, skip blank/`#`, skip header | Same (`StaminaConfig.Parse`) | YES |
| Number parse | `float.TryParse` with `NumberStyles.Float` + `InvariantCulture` | Same | YES |

PASS — convention matched exactly. `controls.csv` already lives at `Assets/Resources/Gameplay/`; `stamina_economy.csv` is now its sibling.

`diff Docs/Design/stamina_economy.csv Assets/Resources/Gameplay/stamina_economy.csv` → **byte-identical**. Authored and runtime copies match.

---

## Scope check (SPEC §2 OUT) — git status walk

`git status --porcelain --untracked-files=all` returns ONLY new files (no `M `, no `D ` rows):

- `Assets/Resources/Gameplay/stamina_economy.csv` (+ `.meta`) — IN scope (runtime CSV).
- `Assets/Scripts/Core.meta`, `…/Stamina.meta`, `…/Stamina/Tests.meta` — IN scope (Unity-generated folder metas).
- `Assets/Scripts/Core/Stamina/Golfin.Core.Stamina.asmdef` (+ `.meta`) — IN scope.
- `Assets/Scripts/Core/Stamina/StaminaConfig.cs` (+ `.meta`) — IN scope.
- `Assets/Scripts/Core/Stamina/StaminaConfigLoader.cs` (+ `.meta`) — IN scope.
- `Assets/Scripts/Core/Stamina/StaminaModel.cs` (+ `.meta`) — IN scope.
- `Assets/Scripts/Core/Stamina/Tests/Golfin.Core.Stamina.Tests.asmdef` (+ `.meta`) — IN scope.
- `Assets/Scripts/Core/Stamina/Tests/StaminaModelTests.cs` (+ `.meta`) — IN scope.
- `Docs/Specs/Active/stamina_model/{ARCHITECT_REVIEW.md, HEARTBEAT.log, IMPLEMENTER_REPORT.md, SELF_REVIEW.md, STATUS.md}` — task folder.

**Zero modified files.** No `LiveStatProviderHost.cs`, no `CharacterDetailPanel.cs`, no `TournamentRoundContext.cs`, no save schema, no `StaminaCostPerShot` drain. SPEC §2 OUT respected. PASS.

---

## Assembly placement check

`Golfin.Core.Stamina.asmdef`:
- `name`: `Golfin.Core.Stamina`
- `references`: `[]` (zero — true leaf)
- `autoReferenced`: `true` (Assembly-CSharp picks it up automatically)
- `noEngineReferences`: `false` (correct — `StaminaConfigLoader` needs `UnityEngine` for `Resources` + `Debug.Log`)
- `includePlatforms` / `excludePlatforms`: both empty (all platforms)

Test asmdef `Golfin.Core.Stamina.Tests.asmdef`:
- `references`: `["Golfin.Core.Stamina"]`
- `includePlatforms`: `["Editor"]` (Editor-only)
- `precompiledReferences`: `["nunit.framework.dll"]`
- `optionalUnityReferences`: `["TestAssemblies"]` (needed for the Test Runner to pick it up)

**Cycle check:** new asmdef has zero references → cannot participate in any cycle. PASS.

**Reachability rationale:** Both consumers cited in SPEC §3 (`LiveStatProviderHost` and `CharacterDetailPanel`) live in Assembly-CSharp (no explicit asmdef in `Assets/Scripts/Gameplay/Stats/` or `Assets/Scripts/UI/Roster/Managers/`); `autoReferenced:true` means Assembly-CSharp picks up `Golfin.Core.Stamina` automatically. Confirmed by inspecting that `CharacterDatabaseCSV.cs` and `ControlsConfigLoader.cs` are both reachable from these locations without further wiring. Rationale in `IMPLEMENTER_REPORT.md` § Assembly placement is sound. PASS.

---

## Acceptance criteria walk (SPEC §9)

| # | Criterion | Verdict | Evidence |
|---|---|---|---|
| 1 | New files compile; project builds; no OUT-of-scope changes | PASS | `git status` shows only IN-scope new files; the 770-test run executed cleanly (full compile) |
| 2 | All unit tests pass in EditMode | PASS | 28/28 independent re-run, 0 failures, ~1.89 s |
| 3 | Chosen assembly + rationale recorded; no new asmdef cycle | PASS | `Golfin.Core.Stamina` leaf, zero deps, `autoReferenced:true`; rationale in report |
| 4 | Runtime CSV loads via existing CSV convention (cite matched pattern) | PASS | `StaminaConfigLoader` mirrors `ControlsConfigLoader` exactly; CSV at sibling location |
| 5 | `StaminaModel`/`StaminaConfig.Parse` carry no Resources/IO dependency | PASS | Confirmed by source read — neither file uses `UnityEngine` |

---

## Procedural gates (PIPELINE_HARDENING)

- **Rule 5 (re-walk entire acceptance list):** done — all 5 §9 criteria + all 13 §8 test rows walked.
- **Rule 6 (report integrity):** every PASS in the report is backed by a visible tool result (independent test re-run + source reads). No fabrication detected.
- **Rule 9 (Figma node re-pull):** N/A — no Figma node in SPEC.
- **Rule 10 (reference-image diff):** N/A — no UI.
- **Rule 11 (clone-provenance read-back):** N/A — no clone mandate.
- **Rule 14 (canonical-screenshot ≥900px):** N/A — pure-logic task, no screenshot.
- **Rule 16 (mesh metrics):** N/A — not a mesh task.
- **Rule 17 (mesh-bake video):** N/A.
- **Rule 18 (Figma fidelity table):** N/A — SPEC references no Figma node.
- **Rule 19 (clone provenance):** N/A — SPEC has no REUSE/clone mandate.
- **Capture-helper compliance:** N/A — no new `*Context.cs` added; no screenshot taken.
- **Scene-mutation audit:** zero modified files anywhere; zero scene paths touched. PASS.

---

## Visual diff notes

N/A — pure C# logic + EditMode test task. No screenshot, no UI, no Figma reference.

---

## Figma fidelity

N/A — SPEC references no Figma node.

---

## Specific failures

None.

---

## Routing

`FORWARD_TO_ARCHITECT` — setting `STATUS.md` → `SELF_REVIEW_PASS` so the architect-reviewer (`golfin-reviewer`) runs next.

The implementer's report is concrete, internally consistent, and independently verifiable on every claim that matters. The only spec deviation (`ResetForTests` access modifier) is acknowledged, harmless, and stripped from player builds.

---

## Iteration count

This is iteration **1** of self-review for this task. N < 3 — verdict-on-merits applies.
