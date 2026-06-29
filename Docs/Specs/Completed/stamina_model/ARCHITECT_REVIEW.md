# ARCHITECT_REVIEW — stamina_model (Stamina Economy Phase 1)

**Reviewer:** golfin-reviewer
**Iteration:** N=1
**Timestamp:** 2026-06-29 23:48 JST
**Verdict:** **FORWARD_TO_REDTEAM**

---

## Task class

Pure C# logic + EditMode unit-test task. NO UI, NO Figma node, NO mesh, NO screenshot, NO scene mutation. Rules 14 (canonical screenshot ≥900px), 16 (mesh metrics), 17 (mesh-bake video), 18 (Figma fidelity), 19 (clone provenance), bbox containment, scene-mutation audit, capture-helper compliance, production-flow capture — all **N/A** for this task class. The objective gate is:

1. EditMode unit-test suite passing.
2. Source correctness against SPEC §6 / §7.
3. Purity (no `Resources`/IO in `StaminaModel` / `StaminaConfig.Parse`).
4. Assembly placement is a leaf with no cycle.
5. Runtime CSV byte-identical to authored CSV; loads via existing convention.
6. Scope: only new files; no OUT-of-scope modifications.

---

## 1. Independent EditMode test re-run

Ran via Unity MCP `tests-run` filtered to class `StaminaModelTests`:

```
Status:       Passed
TotalTests:   770
PassedTests:  28
FailedTests:  0
SkippedTests: 0
Duration:     00:00:01.6895080
```

**28/28 PASS, 0 FAIL, 0 SKIP, ~1.69s.** Independent confirmation; numbers match the implementer's report and the self-reviewer's re-run (within float jitter on duration). PASS.

---

## 2. SPEC §6 formula audit (source-read against `StaminaModel.cs`)

| Formula | SPEC §6 | Implementation (line) | Verdict |
|---|---|---|---|
| `MaxCondition(sta)` | `round(TankBase + sta * TankPerStaminaPoint)` | L45: `(int)Math.Round(_config.TankBase + staminaStat * _config.TankPerStaminaPoint)` | PASS |
| `DrainForHole()` | `DrainPerHole` | L52: `return _config.DrainPerHole` | PASS |
| `ConditionPct(c, sta)` | `clamp01(c / MaxCondition)`; guard `MaxCondition > 0` | L59–L61: `if (maxCond <= 0) return 0f;` THEN `Math.Max(0f, Math.Min(1f, condition / maxCond))` — guard precedes divide | PASS |
| `RegenPerHour(rec)` | `RegenBasePerHour + rec * RegenPerRecoveryPoint` | L68: identical | PASS |
| `RegenForElapsed(rec, dt)` | `max(0, RegenPerHour * dt.TotalHours)`; `dt <= 0 → 0` | L75–L76: `if (elapsed.TotalHours <= 0d) return 0f;` then `Math.Max(0f, …)` belt-and-braces | PASS |
| `PenaltyFor(pct)` | early-return 0 if `pct >= Comfort`; else `FloorPenalty * pow(clamp01(t), exp)` where `t=(Comfort-pct)/Comfort` | L86–L90: identical, including explicit clamp01 of t | PASS |
| `EffectiveStat(base, pct)` | `RoundToInt(base * (1 - PenaltyFor(pct)))` | L99–L101: `(int)Math.Round(baseStat * (1f - penalty))` | PASS |
| `MeterState(pct)` | piecewise `>= High → High`, `>= Mid → Mid`, else `Low` | L121–L123: identical chained `>=` | PASS |
| `IsLowConditionFlag(pct)` | `pct < LowConditionFlagPct` | L130: identical | PASS |

Manual numeric check at `pct = 0` with defaults (Comfort=0.70, Floor=0.33, Exp=1.6):
- `t = (0.70 - 0) / 0.70 = 1.0` → `pow(1, 1.6) = 1` → penalty = `0.33 * 1 = 0.33` ✓
- `EffectiveStat(20, 0.0) = round(20 * 0.67) = round(13.4) = 13` ✓

Monotonicity argument: with `t = (Comfort - pct) / Comfort` linearly decreasing in `pct`, and `pow(t, 1.6)` monotone-increasing in `t` on `[0,1]`, `PenaltyFor` is monotone non-increasing in `pct`. ✓

PASS.

---

## 3. SPEC §7 edge-case audit

| Edge case | SPEC §7 | Implementation | Verdict |
|---|---|---|---|
| `staminaStat = 0` → `MaxCondition = round(TankBase) = 60` | required | covered by `MaxCondition_StaminaStat0_Returns60` ✓ | PASS |
| `MaxCondition = 0` divide-by-zero guard | `ConditionPct → 0` | L60: explicit `if (maxCond <= 0) return 0f;` (precedes divide) | PASS |
| Condition overflow/negative → clamp 0..1 | required | `Math.Max(0f, Math.Min(1f, …))` + 2 tests pass | PASS |
| `pct >= Comfort` → penalty exactly 0 | required | L86 early-return | PASS |
| `pct = 0` → penalty = `FloorPenalty` exact | required | math above; test `PenaltyFor_AtZero_EqualsFloorPenalty` passes | PASS |
| `PenaltyFor` monotonic non-increasing | required | proven by math + test `PenaltyFor_IsMonotonic_And_BelowFloor` | PASS |
| `IsDegraded` case-insensitive | required | L111 `StringComparison.OrdinalIgnoreCase`; tests `IsDegraded_LowercaseStrength_ReturnsTrue` + `IsDegraded_ClubControl_ReturnsTrue` pass | PASS |
| Unknown stat → false | required | foreach with no match → false; `IsDegraded_Unknown_ReturnsFalse` passes | PASS |
| Null statName | safety extra | L108 explicit null guard → false; defensive (no test, but harmless extra) | PASS |
| Pre-Configure call throws clear `InvalidOperationException` | required | `EnsureConfigured()` called by EVERY public read method; throws `InvalidOperationException` with explicit message; `MaxCondition_BeforeConfigure_Throws` passes | PASS |

Spot-check: all public methods (`MaxCondition`, `DrainForHole`, `ConditionPct`, `RegenPerHour`, `RegenForElapsed`, `PenaltyFor`, `EffectiveStat`, `IsDegraded`, `MeterState`, `IsLowConditionFlag`) call `EnsureConfigured()` at L44, L51, L58, L67, L74, L85, L99, L107, L120, L129 respectively. Full coverage — no method can silently use a zero config. PASS.

---

## 4. Purity audit (no `UnityEngine.Resources` / IO in StaminaModel + StaminaConfig.Parse)

| File | `using UnityEngine` | `Resources.Load` | Verdict |
|---|---|---|---|
| `StaminaConfig.cs` | NO (only `System`, `System.Collections.Generic`, `System.Globalization`, `System.Linq`) | NO | PURE ✓ |
| `StaminaModel.cs` | NO (only `System`, `System.Collections.Generic`, `System.Linq`) | NO | PURE ✓ |
| `StaminaConfigLoader.cs` | YES | YES (`Resources.Load<TextAsset>("Gameplay/stamina_economy")`) | bootstrap-only ✓ |

SPEC §3 satisfied exactly: `StaminaModel` + `StaminaConfig.Parse` are EditMode-testable without a scene or `Resources` round-trip; only the thin loader touches `Resources`. PASS.

---

## 5. Assembly placement audit

`Golfin.Core.Stamina.asmdef`:
```json
"name": "Golfin.Core.Stamina",
"references": [],
"autoReferenced": true,
"noEngineReferences": false,
"includePlatforms": [], "excludePlatforms": []
```

Test asmdef `Golfin.Core.Stamina.Tests.asmdef`:
```json
"references": ["Golfin.Core.Stamina"],
"includePlatforms": ["Editor"],
"precompiledReferences": ["nunit.framework.dll"],
"optionalUnityReferences": ["TestAssemblies"]
```

**Cycle check:** new asmdef has zero `references` entries → cannot participate in any cycle by definition. PASS.

**Reachability rationale (SPEC §3 — must be callable from `LiveStatProviderHost` AND `Golfin.Roster`):**
- `LiveStatProviderHost` lives in Assembly-CSharp (no explicit asmdef in `Assets/Scripts/Gameplay/Stats/`).
- `CharacterDetailPanel` / `CharacterDatabaseCSV` live in Assembly-CSharp (no explicit asmdef in `Assets/Scripts/UI/Roster/Managers/`).
- `autoReferenced: true` means Assembly-CSharp automatically picks up `Golfin.Core.Stamina` at compile time, so both consumers can call `StaminaModel.*` without further asmdef wiring.
- A consumer that lives in an explicit asmdef later (e.g. `Golfin.Tournaments` for the round context) would explicitly add `"Golfin.Core.Stamina"` to its `references` array — same as the test asmdef already does. Leaf placement supports this without introducing any cycle.

Rationale in `IMPLEMENTER_REPORT.md` § Assembly placement is sound. PASS.

---

## 6. Runtime CSV — byte-identical + loads via existing convention

`diff Docs/Design/stamina_economy.csv Assets/Resources/Gameplay/stamina_economy.csv` → **no diff** (both 1045 bytes). Authored ↔ runtime parity. PASS.

Loader pattern match (read `Assets/Scripts/Gameplay/Config/ControlsConfigLoader.cs`):

| Aspect | `ControlsConfigLoader` (existing) | `StaminaConfigLoader` (new) | Match |
|---|---|---|---|
| Load call | `Resources.Load<TextAsset>("Gameplay/controls")` | `Resources.Load<TextAsset>("Gameplay/stamina_economy")` | YES |
| Resources path | `Assets/Resources/Gameplay/controls.csv` | `Assets/Resources/Gameplay/stamina_economy.csv` (sibling) | YES |
| Null guard | `Debug.LogWarning + return defaults` | `Debug.LogError + leave unconfigured` | Equivalent (correct here because no sensible defaults exist) |
| Line parsing | `Split('\n')` → `Trim()`, skip blank/`#`, skip header | Same (inside `StaminaConfig.Parse`) | YES |
| Number parsing | `float.TryParse + NumberStyles.Float + InvariantCulture` | Same | YES |

PASS.

---

## 7. Scope audit (SPEC §2 — no OUT-of-scope modifications)

`git status --porcelain --untracked-files=all` (run this pass):

All entries are `??` (untracked, new) — zero `M` (modified), zero `D` (deleted), zero `R` (renamed).

In-scope new files:
- `Assets/Resources/Gameplay/stamina_economy.csv` (+ `.meta`) — runtime CSV (SPEC §4)
- `Assets/Scripts/Core.meta`, `Assets/Scripts/Core/Stamina.meta`, `Assets/Scripts/Core/Stamina/Tests.meta` — Unity-generated folder metas
- `Assets/Scripts/Core/Stamina/Golfin.Core.Stamina.asmdef` (+ `.meta`)
- `Assets/Scripts/Core/Stamina/StaminaConfig.cs` (+ `.meta`)
- `Assets/Scripts/Core/Stamina/StaminaModel.cs` (+ `.meta`)
- `Assets/Scripts/Core/Stamina/StaminaConfigLoader.cs` (+ `.meta`)
- `Assets/Scripts/Core/Stamina/Tests/Golfin.Core.Stamina.Tests.asmdef` (+ `.meta`)
- `Assets/Scripts/Core/Stamina/Tests/StaminaModelTests.cs` (+ `.meta`)
- `Docs/Specs/Active/stamina_model/*` — task folder

**Zero modifications** to:
- `LiveStatProviderHost.cs` ✓
- `CharacterDetailPanel.cs` ✓
- `TournamentRoundContext.cs` ✓
- save schema ✓
- `StaminaCostPerShot` drain call ✓
- any scene, prefab, or `Assets/Scripts/Physics/` file ✓

SPEC §2 OUT respected exactly. PASS.

**Hard-rule 13 (preflight baseline + path attribution):** every uncommitted path lives inside either the task spec folder or the IN-scope new-files set; nothing to attribute. PASS.

---

## 8. SPEC §9 acceptance criteria — final walk

| # | Criterion | Verdict | Evidence (this pass) |
|---|---|---|---|
| 1 | New files compile; project builds; no OUT-of-scope changes | PASS | Tests ran cleanly (full compile of 770-test suite); `git status` shows zero `M ` rows |
| 2 | All unit tests pass in EditMode | PASS | Independent re-run: 28/28 PASS, 0 FAIL, 0 SKIP |
| 3 | Chosen assembly + rationale recorded; no new asmdef cycle | PASS | `Golfin.Core.Stamina` leaf, zero deps, `autoReferenced:true`; rationale in §5 above and in `IMPLEMENTER_REPORT.md` |
| 4 | Runtime CSV loads via existing CSV convention (cite matched pattern) | PASS | `StaminaConfigLoader` mirrors `ControlsConfigLoader` 1:1; CSV at sibling Resources path |
| 5 | `StaminaModel` / `StaminaConfig.Parse` carry no Resources/IO dependency | PASS | Source confirmed: neither file imports `UnityEngine` or calls `Resources.Load` |

---

## 9. Spec deviations

One, acknowledged by implementer and harmless:

- `StaminaModel.ResetForTests()` is declared `public` (rather than `internal`) inside an `#if UNITY_EDITOR` guard. SPEC did not specify the access modifier. `internal` would require `InternalsVisibleTo` across the asmdef boundary; the editor-only guard ensures it is stripped from player builds. **Non-observable deviation.** Accept.

---

## 10. PIPELINE_HARDENING rule walk (for completeness)

- Rule 1 (real-entry): N/A — no player entry point exists yet (Phase 1 is foundation only; SPEC §2 explicitly forbids wiring).
- Rule 2 (synthetic entry = FAIL): N/A — no entry point at all.
- Rule 3 (invariant JSON): N/A — no world→screen geometry.
- Rule 4 (TaggedCamera flip-free capture): N/A — no capture.
- Rule 5 (re-run full acceptance list every pass): **DONE** — re-walked §9 (1–5) and §8 (13 rows) independently.
- Rule 6 (report integrity): every PASS in IMPLEMENTER_REPORT is backed by a real tool result. Independently verified test counts match. No fabrication detected. PASS.
- Rule 7 (standing bans): zero edits to `Assets/Scripts/Physics/`, no `*Gate` scenarios, no scene additions, no `M_Splash*.mat`. PASS.
- Rule 8 (clone-provenance): N/A — no reuse mandate.
- Rule 9 (Figma node re-pull): N/A — no Figma node.
- Rule 10 (reference-image diff): N/A — no UI.
- Rule 11 (clone-provenance read-back): N/A.
- Rules 12/13 (Unity authoring traps / single-modal render harness): N/A — no Unity authoring touched.
- Rule 14 (scene-mutation guardrail): N/A — no scene touched.
- Rules 14–19 (review-hardening, mesh, video, Figma fidelity, clone provenance): N/A — pure logic + EditMode tests. The hook lets `ARCHITECT_REVIEW.md` write through for this task class without these tables.

---

## 11. Final verdict

All 5 SPEC §9 acceptance criteria pass independently this pass. The 28 unit tests cover every SPEC §8 assertion and every SPEC §7 edge case. Source matches SPEC §6 formulas line-for-line, including the divide-by-zero guard, the `pct >= Comfort` early-return, the `t <= 0` early-return for `RegenForElapsed`, and the `EnsureConfigured` throw at every entry point. Purity is preserved exactly (only `StaminaConfigLoader` imports `UnityEngine`). The new asmdef is a true leaf — zero references — so a cycle is impossible by construction. Scope is clean: zero `M `/`D` rows, only new IN-scope files.

The one deviation (`ResetForTests` access modifier) is non-observable and stripped from player builds.

**Verdict:** `FORWARD_TO_REDTEAM`. Setting `STATUS.md` → `READY_FOR_REDTEAM` so the adversarial red-team gate runs next. (I do not write `ARCHITECT_REVIEW_PASS` — that gate is owned by `golfin-redteam-reviewer`.)

---

## 12. Iteration count

Iteration **1** of architect review for this task. N < 3; verdict-on-merits applies.

---

# RED-TEAM REVIEW (golfin-redteam-reviewer)

**Reviewer:** golfin-redteam-reviewer (adversarial gate)
**Timestamp:** 2026-06-29 17:41 CEST
**Verdict:** **ARCHITECT_REVIEW_PASS**

I did not trust the reviewer's PASS. I re-generated every piece of evidence myself: re-ran
the tests against the live assembly, re-derived all formula values by hand in Python, and
probed the live production `StaminaModel` with inputs NO test covers. I then ran three
deliberate break-attempts. All failed. This is a genuine PASS, not a carry-forward.

## R1. Independent test re-run (not the reviewer's numbers)

`tests-run --testClass StaminaModelTests` via Unity MCP, this pass:
```
Summary.Status:  Passed
TotalTests:      770   (registered EditMode count)
PassedTests:     28
FailedTests:     0
SkippedTests:    0
```
28 unique `Golfin.Core.Stamina.Tests.StaminaModelTests.*` names, every one `"Status": "Passed"`,
zero Failed rows. Matches the implementer's and reviewer's claim. **Not fabricated** (Rule 6 PASS).

## R2. Is the suite a real gate (non-circular)? — re-derived, then probed the LIVE assembly

- No shadow `StaminaModel`/`StaminaConfig` exists anywhere outside `Core/Stamina` (grep clean) —
  the tests bind the production types, and the test asmdef `references: ["Golfin.Core.Stamina"]`.
- Every assertion is a hard literal (114, 222, 60, 0.33, 13, 30, 92, 60, …). I re-derived each in
  Python: all match. **No `.5` boundary** exists in `MaxCondition` over sta∈[0,50], and
  `EffectiveStat` lands on 13.4 — so banker's rounding (`Math.Round` ToEven) never diverges from
  round-half-up on any tested OR reachable default-CSV value. A wrong formula WOULD go red.
- Live-assembly probe with values no test touches (proves it's not a stale DLL):
  `MaxCondition(13)=138`, `ConditionPct(30,9)=0.2631579`, `PenaltyFor(0.35)=0.1088594`,
  `EffectiveStat(30,0.10)=22`, `MeterState(0.299)=Low`, `IsLowConditionFlag(0.25)=False` (strict `<`).
  All match my hand-derivation exactly.
- **Pre-Configure guard on EVERY method, not just the tested one:** after `ResetForTests()`,
  `RegenPerHour(5)` AND `PenaltyFor(0.5)` both throw `InvalidOperationException` (SPEC §7 honored
  beyond the single `MaxCondition` test).

## R3. Three break-attempts (all failed)

1. **Divide-by-zero (visual/geometric):** forced a config with `tank_base=0, per_point=0` →
   `MaxCondition(0)=0`, `ConditionPct(50,0)=0` — no NaN/Inf; guard precedes the divide. Survived.
2. **Silent-zero parse path:** a CSV missing `drain_per_hole` → `DrainPerHole=0` (intended fallback,
   no crash); `degraded_stats=A;B;C ; ` → 3 entries, trailing empty/whitespace token dropped via
   `Where(s.Length>0)`. No silent-zero corruption of present keys. Survived.
3. **Spec-intent boundaries:** `PenaltyFor(0.70)=0` (≥comfort early-return), `PenaltyFor(0.6999)=2.3e-7`
   (continuous), `PenaltyFor(1.5)=0`, `PenaltyFor(-0.2)=0.33` (t-clamped to floor, not extrapolated).
   Monotonic non-increasing verified across the FULL [0,0.70] range (701-point scan), not just the two
   tested points. Survived.

## R4. Purity / scope / CSV

- `StaminaModel.cs` + `StaminaConfig.cs`: zero `using UnityEngine`, zero `Resources`, zero `Mathf`,
  zero `System.IO` (only matches are comment text). Only `StaminaConfigLoader.cs` touches `Resources`.
- `git status --porcelain --untracked-files=all`: every row is `??` (new). Zero `M`/`D`/`R`. All new
  files under `Assets/Scripts/Core/Stamina/` + `Assets/Resources/Gameplay/stamina_economy.csv` + the
  task folder. `LiveStatProviderHost`, `CharacterDetailPanel`, `TournamentRoundContext`, save schema,
  `StaminaCostPerShot` — all untouched (SPEC §2 OUT respected).
- `diff Docs/Design/stamina_economy.csv Assets/Resources/Gameplay/stamina_economy.csv` → **IDENTICAL**
  (byte-for-byte). All 12 keys parse; `degraded_stats` semicolon-split = {Strength, ClubControl}.
- New leaf asmdef has `references: []` → cycle impossible by construction.

## R5. Report integrity

Every PASS in IMPLEMENTER_REPORT.md is backed by a real, independently-reproduced tool result.
No fabricated test output, no invented quote. Nothing to log to `review_misses.log`.

**Verdict:** `ARCHITECT_REVIEW_PASS` — tried hard to break it, could not. Advances to Cesar.
