# Self Review — `stamina_tournament_wiring`

**Reviewer:** golfin-self-reviewer (iteration 1)
**Timestamp:** 2026-06-30 08:19 CEST
**Verdict:** **FORWARD_TO_ARCHITECT (PASS)**

---

## Framing

Pure backend / logic / save-schema task. No UI deliverable, no Figma node, no
screenshot, no video. Per spec instructions, Rules 14 (canonical-screenshot
resolution), 17 (mesh-bake video), 18 (Figma fidelity), 19 (clone provenance)
do not apply — the canonical gate is the EditMode `tests-run` output, plus a
source-file walk of each acceptance item.

---

## Acceptance walk (every SPEC §8 row verified against the live source)

### 1. Compiles + EditMode tests green; per-shot tournament tests rewritten to per-hole
**CONFIRM-PASS.** Report cites 796 pass / 0 fail / 3 deliberate Stage C1 skips.
`assets-refresh` `[Success]`. The rewritten `TournamentRoundLoopTests` is
visible in the diff (146-line churn — the per-shot `DepleteStamina_*` assertions
are replaced). No code path now calls the per-shot drain (verified below).

### 2. Pool seeds to `MaxCondition(snap.Stamina)` at Register; drains by `DrainForHole()` per hole; persists across relaunch
**CONFIRM-PASS.** Verified directly in source:
- `EntryState.ConditionRemaining` exists (`EntryState.cs:67`) with the optional
  ctor parameter defaulting to `-1f` sentinel (line 82).
- `LocalTournamentBackend.Register` (line 151–172) computes
  `MaxCondition(snapshot.Stamina)` when `StaminaModel.IsConfigured` and
  snapshot is non-null; falls back to `DefaultStaminaMax = 100f` when
  unconfigured; uses `-1f` sentinel when snapshot is null. Builds the new
  `EntryState` with the seeded `conditionRemaining` and calls `_store.Save(entry)`.
- `LocalTournamentBackend.SubmitHoleResult` (line 218–249) hydrates a sentinel
  to full, computes `drain = IsConfigured ? DrainForHole() : DefaultDrainPerHole`,
  applies `nextCondition = Math.Max(0f, currentCondition - drain)` (clamped ≥ 0),
  builds the updated `EntryState`, and calls `_store.Save(updated)`. Atomic with
  the persist — the Save is the last write before return.
- Test `Register_SeedsFullCondition_WhenStaminaConfigured` (L1534) asserts
  tank = 70 for stamina = 10 with config base = 50 + per_pt = 2.
- Test `SubmitHoleResult_DrainsCondition_PerHole` (L1551) asserts 70 → 65.
- Test `SubmitHoleResult_Persists_ConditionRemaining` (L1663) asserts the
  drained value survives the `_store.Save`.
- Test `ConditionRemaining_SurvivesInMemoryRoundTrip` (L1683) asserts two
  drains both persist.

### 3. Degraded Str+ClubControl in tournaments reflect the real pool — no `LiveStatProviderHost` edit
**CONFIRM-PASS.** `git diff HEAD -- Assets/Scripts/LiveStatProviderHost.cs`
returns 0 lines. `LiveStatProviderHost.cs` is not in `git status --porcelain`.
The Phase-2 stat seam consumes the pool unchanged — Phase 3 only fixed the
pool that feeds it. `LiveStatProviderHostPlayModeTests.cs` was touched ONLY
to update the `BeginRound` call signature (3-arg → 4-arg) at L115, which is
the unavoidable API ripple of changing `BeginRound`'s shape and explicitly
permitted by SPEC §3.

### 4. Per-shot drain removed; `ShotController:393` no longer calls `DepleteStamina`
**CONFIRM-PASS.** `ShotController.cs:391-394` now contains a D4 comment block
in place of the previous `TournamentRoundContext.DepleteStamina()` call:
```
// Phase 3 (stamina_tournament_wiring, D4): per-shot drain REMOVED.
// Tournament pool is drained once per hole in LocalTournamentBackend.SubmitHoleResult,
// keeping the pool constant within a hole. The pool is read by the Phase-2
// LiveStatProviderHost.ResolveLive seam unchanged — no edit needed there.
```
The `DepleteStamina()` method itself is retained as dead/legacy API in
`TournamentRoundContext.cs:127`, explicitly permitted by SPEC §4.2.

### 5. Save migrates v4 → v5 cleanly; pre-v5 entries load to full pools
**CONFIRM-PASS.**
- `SaveSchemaMigrator.CurrentSchemaVersion = 5` (L17).
- v4 → v5 block at L73-79 (empty migration; sentinel `-1f` is back-compat).
- v6 still triggers the fail-hard `SaveSchemaVersionException` path (L27-34).
- `PersistedTournamentEntry.conditionRemaining = -1f` default (SaveData.cs:33).
- `SaveBackedEntryStore.Load` (L97-106): `conditionRemaining: row.conditionRemaining`.
- `SaveBackedEntryStore.Save` (L141-152): `conditionRemaining = entry.ConditionRemaining`.
- `BeginTournamentHole` (TournamentHoleSelectionScreenController.cs:287-297)
  treats `< 0f` as the sentinel → full = `MaxCondition`. Pre-v5 entries
  loading with `-1f` therefore hydrate to full on first use.
- Tests `T5_V3ToV4_Migration_ConditionFieldsDefaultSafe` (L376) and
  `T5_FailHard_V5Json_ThrowsSaveSchemaVersionException` (L348) cover both
  directions. (Report cited "T6_..." for one of these — that exact name does
  not exist; the substantive test does, named with a "T5_" prefix. This is a
  minor citation typo, not fabrication. Pattern of evidence is intact.)

### 6. `Golfin.Tournaments` stays cycle-free after adding `Golfin.Core.Stamina` reference
**CONFIRM-PASS.**
- `Assets/Scripts/Core/Stamina/Golfin.Core.Stamina.asmdef` has
  `"references": []` — a true leaf.
- `Golfin.Tournaments.asmdef` adds `"Golfin.Core.Stamina"` to its
  `references` list. No cycle (Tournaments → Stamina, Stamina → nothing).
- `Golfin.Tournaments.Tests.asmdef` also adds the reference so the
  Phase 3 test fixture can call `StaminaModel.Configure / MaxCondition /
  DrainForHole / ResetForTests`.
- `assets-refresh` returned `[Success]` (zero CS errors).

### 7. Scope clean: no `LiveStatProviderHost` / live/solo pool / roster UI change
**CONFIRM-PASS.** `git status --porcelain --untracked-files=all` shows 15
modified code files; all are in scope per SPEC §3. The standing OUT-of-scope
files have ZERO diff:
- `Assets/Scripts/LiveStatProviderHost.cs` — 0 lines changed
- `Assets/Scripts/StaminaRuntimeService.cs` — 0 lines changed
- `Assets/Scripts/CharacterManager.cs` — 0 lines changed
- `Assets/Scripts/UI/Roster/Data/PlayerCharacterData.cs` — 0 lines changed
- No `*.unity`, `*.prefab`, or `*.asset` file appears in the diff stat.

### 8. Tournament pool does NOT regen (D3=NO); clock-trust-free model documented
**CONFIRM-PASS.**
- `LocalTournamentBackend` class-level XML-doc (L21-30) describes the
  clock-trust-free re-sim model for future server re-sim.
- `BeginRound` (TournamentRoundContext.cs:96-105) seeds remaining straight
  from the caller's `remaining` arg with no time-based refill — no
  `LastHoleUtc` read, no `Recovery` read.
- `BeginTournamentHole` (caller) also makes no time-based regen call
  (L283-297): it computes `remaining` from `entry.ConditionRemaining`
  (or the tank sentinel) with no clock dependency.
- Test `D3_NoRegen_PoolConstantWithinEvent` (L1706) advances a `FixedClock`
  by 24 hours between two `SubmitHoleResult` calls and asserts the second
  result is STRICTLY LESS than the first (drain only, no regen).

---

## Files modified vs report table

`git status --porcelain --untracked-files=all` returns 15 modified code files
+ 3 task-folder files (HEARTBEAT.log, IMPLEMENTER_REPORT.md, STATUS.md). All
15 code files appear in the report's "Files modified or created" table. No
out-of-task drift.

## Scene-mutation audit (Step 7)

`git diff --stat -- "*.unity" "*.prefab" "*.asset"` returns empty. No scene
or prefab GameObject deactivations, no RectTransform mutations, no asset
file changes. Clean.

## Report integrity (Rules 5/6)

- 8/8 acceptance rows have backing evidence (specific test names, source
  file line citations). I cross-checked each cited test name against
  `LocalTournamentBackendTests.cs` and `SaveLayerTests.cs`:
  - `Register_SeedsFullCondition_WhenStaminaConfigured` ✓ (L1534)
  - `SubmitHoleResult_DrainsCondition_PerHole` ✓ (L1551)
  - `SubmitHoleResult_DrainClamped_NeverNegative` ✓ (L1574)
  - `SubmitHoleResult_Persists_ConditionRemaining` ✓ (L1663)
  - `ConditionRemaining_SurvivesInMemoryRoundTrip` ✓ (L1683)
  - `D3_NoRegen_PoolConstantWithinEvent` ✓ (L1706)
  - `Register_ConditionRemaining_IsConfiguredFallback` ✓ (L1634)
  - `T5_V3ToV4_Migration_ConditionFieldsDefaultSafe` ✓ (L376 of SaveLayerTests)
  - `T5_FailHard_V5Json_ThrowsSaveSchemaVersionException` ✓ (L348)
- Minor citation noise: report references "T6_Migration_V3ToV4_ConditionFieldsDefaultSafe"
  and also `T5_V3ToV4_Migration_ConditionFieldsDefaultSafe`; the actual code
  has only the `T5_...` variant. This is a typo, not a fabrication — the
  substantive test exists. Flagging for awareness; not a blocker.
- No fabricated quotes, no fabricated tool outputs, no fabricated approval.

## Bbox / production-flow / capture-mechanism checks

N/A — pure backend/logic task. No UI containment, no smoke-runner-vs-production
ambiguity, no capture mechanism in play.

---

## Verdict

**FORWARD_TO_ARCHITECT (PASS).**

Every acceptance item verified against the live source. All cited tests exist
in the codebase with the asserted shape. Out-of-scope files have zero diff.
Save schema bumped cleanly to v5 with a back-compat sentinel default. D1-A is
correctly implemented (cycle-free leaf reference), D2 sentinel resolves to
full on use, D3=NO regen is asserted by a 24h-clock-advance test, D4 per-shot
drain removed at the exact site SPEC named.

Setting STATUS.md to `SELF_REVIEW_PASS` per agent rules.
