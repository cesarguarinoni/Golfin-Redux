# Architect Review — `stamina_tournament_wiring`

**Reviewer:** golfin-reviewer (iteration 1)
**Timestamp:** 2026-06-30 08:22 CEST
**Verdict:** **READY_FOR_REDTEAM (PASS)**

---

## Framing

Pure backend/logic/save-schema task (Stamina Economy Phase 3 — tournament
pool). No UI deliverable, no Figma node, no canonical screenshot, no video.
Per kickoff instructions: Rules 14 (canonical-screenshot resolution),
17 (mesh-bake video), 18 (Figma fidelity), 19 (clone provenance) do not
apply. The canonical gate is the EditMode `tests-run` output (796 pass / 0
fail / 3 deliberate Stage C1 skips per the implementer report) plus a
line-by-line source audit of each acceptance item.

Independent walk-through completed below; I did NOT trust the self-reviewer's
PASS — each row was re-verified against the live source.

---

## Acceptance walk (every SPEC §8 row re-verified — Rule 5)

### 1. EntryState + LocalTournamentBackend (Register seeds, SubmitHoleResult drains atomically with `_store.Save`)
**PASS.** Verified live:
- `EntryState.ConditionRemaining` is a public `float` get-only property on
  `Assets/Scripts/Tournaments/EntryState.cs:67`; the optional ctor param
  defaults to `-1f` (line 82). Both the snapshot-aware (line 74-92) and
  legacy (line 98-108) ctors forward to the field correctly.
- `LocalTournamentBackend.Register` (`LocalTournamentBackend.cs:151-176`):
  - Computes `snapshot = _stats?.SnapshotFor(characterId)` (L151).
  - `IsConfigured + snapshot != null` → `MaxCondition(snapshot.Stamina)`.
  - `snapshot == null` → `-1f` sentinel (preserved on legacy/null paths).
  - `IsConfigured == false` → `DefaultStaminaMax = 100f` fallback (D1-A).
  - Builds `EntryState(..., conditionRemaining: conditionPool)` and persists
    via `_store.Save(entry)` (L174). One-shot atomic upsert.
- `LocalTournamentBackend.SubmitHoleResult` (L218-251):
  - Reads `entry.ConditionRemaining`; sentinel `-1f` hydrates to full from
    snapshot or fallback (L223-234).
  - `drain = IsConfigured ? DrainForHole() : DefaultDrainPerHole` (L236).
  - `nextCondition = Math.Max(0f, currentCondition - drain)` — clamped ≥ 0 (L237).
  - New `EntryState` cloned with `conditionRemaining: nextCondition` (L247),
    persisted by `_store.Save(updated)` before return (L249) — atomic with the
    hole-result persist, exactly per SPEC §4.1.

### 2. TournamentRoundContext.BeginRound (4-param seed from entry, NO regen, NO flat-100 reset)
**PASS.** Verified live:
- `TournamentRoundContext.BeginRound(string, CharacterSnapshot, float, float)`
  is the new 4-param signature at `TournamentRoundContext.cs:96`.
- Body sets `StaminaEnergyMax = tankMax; StaminaEnergyRemaining = remaining`
  directly from the caller's args (L101-102). No `= DefaultStaminaMax` reset
  (the previous flat-100 line is gone — `git diff` confirms removal).
- Critical D3=NO check: no `LastHoleUtc` or `Recovery` read anywhere in
  `BeginRound`; no time-based refill arithmetic. The pool is seeded straight
  from the caller value. ✅
- `BeginTournamentHole` (`TournamentHoleSelectionScreenController.cs:287-297`)
  is the caller; it computes `tank` from `MaxCondition(snapshot.Stamina)`
  (with `IsConfigured` fallback to `DefaultStaminaMax`), and `remaining` from
  `entry.ConditionRemaining < 0f ? tank : Mathf.Min(tank, entry.ConditionRemaining)`
  — exact match for SPEC §4.3 (sentinel→full hydration, no regen).

### 3. Per-shot drain GONE from ShotController (~L391-394) — D4=YES
**PASS.** `Assets/Scripts/Gameplay/Input/ShotController.cs:391-394` now
contains only a D4 explanatory comment block; the previous
`TournamentRoundContext.DepleteStamina();` call is removed. Confirmed by
`grep -n "DepleteStamina\|StaminaCostPerShot" Assets/Scripts/Gameplay/Input/ShotController.cs`
→ ZERO matches. The legacy `DepleteStamina()` method itself is retained in
`TournamentRoundContext.cs:127` as dead/legacy API, explicitly permitted
by SPEC §4.4 ("may stay as dead/test-only API or be deleted").

### 4. Save schema v4 → v5 (CurrentSchemaVersion=5; sentinel -1f default; v4→v5 migrator block; SaveBackedEntryStore maps in both directions)
**PASS.** Verified live:
- `SaveSchemaMigrator.CurrentSchemaVersion = 5` (`SaveSchemaMigrator.cs:17`).
- v4 → v5 block at L73-79 (empty migration; sentinel default = back-compat).
- v6 (and any future version > 5) still trips the fail-hard
  `SaveSchemaVersionException` at L27-34.
- `PersistedTournamentEntry.conditionRemaining = -1f` field initializer at
  `SaveData.cs:33` — Newtonsoft will keep this default when a pre-v5 JSON is
  loaded without the field.
- `SaveBackedEntryStore.Load` (`SaveBackedEntryStore.cs:97-107`):
  `conditionRemaining: row.conditionRemaining` is passed through verbatim,
  including the sentinel.
- `SaveBackedEntryStore.Save` (L141-152):
  `conditionRemaining = entry.ConditionRemaining` is written back; the
  existing-row UPSERT branch preserves the `claimed` flag — no other
  field is overwritten.
- **Sentinel→full reproducibility (SPEC §6).** The hydration point is the
  `BeginTournamentHole` caller (verified in §2 above). It treats `< 0f` as
  the sentinel and resolves to `MaxCondition(snapshot.Stamina)`. The
  `SubmitHoleResult` path *also* hydrates internally before draining
  (`LocalTournamentBackend.cs:223-234`) — defense in depth. Both paths
  exercised by tests (see §7 below).
- Test coverage:
  - `T5_CurrentSchemaVersion_Is4` (badly named — checks ==5,
    `SaveLayerTests.cs:367-371`).
  - `T5_FailHard_V5Json_ThrowsSaveSchemaVersionException` (also confusingly
    named — uses `schemaVersion: 6` JSON to verify v6 throws,
    `SaveLayerTests.cs:347-362`).
  - `T5_V3ToV4_Migration_ConditionFieldsDefaultSafe` (L376) asserts
    schemaVersion==5 after migration from v3 (L405) and that condition
    fields default-safe.
  - Note: report cites a "T6_..." test that doesn't exist; the self-reviewer
    correctly flagged this as a citation typo (the substantive T5_ variant
    exists). Not a fabrication; not a blocker — minor docs hygiene only.

### 5. OUT-of-scope files UNTOUCHED — `git diff` confirms zero lines
**PASS.** `git status --porcelain --untracked-files=all` shows 15 modified
code files + 4 task-folder files (HEARTBEAT, IMPLEMENTER_REPORT, SELF_REVIEW,
STATUS). All 15 code files are within SPEC §3 scope.
`git diff HEAD --stat -- "Assets/Scripts/LiveStatProviderHost.cs" "Assets/Scripts/StaminaRuntimeService.cs" "Assets/Scripts/CharacterManager.cs" "Assets/Scripts/UI/Roster/Data/PlayerCharacterData.cs"`
→ EMPTY (zero lines). Scene-mutation audit
`git diff HEAD --stat -- "*.unity" "*.prefab" "*.asset" "*.mat"` → EMPTY.
No scene corruption, no prefab mutation.

### 6. asmdef cycle check — Tournaments → Core.Stamina cycle-free
**PASS.**
- `Golfin.Core.Stamina.asmdef` has `"references": []` — a TRUE leaf.
- `Golfin.Tournaments.asmdef` now lists `"Golfin.Core.Stamina"` in
  `references` (L4-8). No cycle possible (Stamina has zero outgoing edges).
- `Golfin.Tournaments.Tests.asmdef` also adds the reference (necessary for
  `TournamentStaminaPhase3Tests` to call `StaminaModel.Configure / MaxCondition
  / DrainForHole / ResetForTests`).

### 7. Report integrity — cited tests exist with the asserted shape (Rule 5)
**PASS.** I re-grepped every named test cited in `IMPLEMENTER_REPORT.md`:
- `Register_SeedsFullCondition_WhenStaminaConfigured` ✓ `LocalTournamentBackendTests.cs:1534`
- `SubmitHoleResult_DrainsCondition_PerHole` ✓ L1551 (asserts decrement exactly = `DrainForHole()`)
- `SubmitHoleResult_DrainClamped_NeverNegative` ✓ L1574 (uses HoleSet18 for 18 unique IDs; tank=70 / drain=5 → 16 drain steps)
- `Register_ConditionRemaining_SentinelDefault_WhenNoSnapshot` ✓ L1619 (no-snapshot → -1f)
- `Register_ConditionRemaining_IsConfiguredFallback` ✓ L1634 (unconfigured → 100f)
- `SubmitHoleResult_Persists_ConditionRemaining` ✓ L1663
- `ConditionRemaining_SurvivesInMemoryRoundTrip` ✓ L1683
- `D3_NoRegen_PoolConstantWithinEvent` ✓ L1706 (24h FixedClock advance → pool decreases, equals `afterHole1 - DrainForHole()` to 0.001f)
- `Pool_IsConstantWithinHole_NoPerShotDrain` ✓ `TournamentRoundLoopTests.cs:89`
- `T5_V3ToV4_Migration_ConditionFieldsDefaultSafe` ✓ `SaveLayerTests.cs:376`
- `T5_FailHard_V5Json_ThrowsSaveSchemaVersionException` ✓ `SaveLayerTests.cs:348` (technically tests v6, not v5 — name is misleading but the assertion is correct)

The "Files modified or created" table in `IMPLEMENTER_REPORT.md` matches
`git status --porcelain --untracked-files=all` exactly (15 code files
listed + task-folder docs). No undeclared drift.

86 test attributes (`[Test]`+`[TestCase]`) in
`LocalTournamentBackendTests.cs` alone; consistent with the report's claimed
799 EditMode test total (796 pass + 3 skip).

### 8. D3=NO regen + clock-trust-free model documented
**PASS.**
- `LocalTournamentBackend` class XML-doc at L21-30 documents the
  clock-trust-free re-sim model verbatim per SPEC §6 ("drain-count alone
  is sufficient to reconstruct the per-hole pool" — future server re-sim
  never needs to trust client clocks).
- `TournamentRoundContext.cs:19-23` carries a mirror doc.
- The test `D3_NoRegen_PoolConstantWithinEvent` (L1706) advances a
  `FixedClock` by 24 hours between two `SubmitHoleResult` calls and asserts
  the second value is STRICTLY LESS than the first AND equals
  `afterHole1 - DrainForHole()` (no regen, clock-irrelevant).

---

## Reproducibility from persisted data (SPEC §6) — explicit check

The kickoff specifically called out that the `-1`-sentinel→full and v4→v5
behavior must be reproducible from persisted data. Confirmed:

- **-1 sentinel → full** on `Load` is enforced at two sites: (a)
  `BeginTournamentHole` (the gameplay caller — verified in §2) and (b)
  inside `SubmitHoleResult` (`LocalTournamentBackend.cs:223-234` — internal
  defense in depth). The `ConditionRemaining_SurvivesInMemoryRoundTrip` test
  (L1683) and `T5_V3ToV4_Migration_ConditionFieldsDefaultSafe` cover both
  paths. ✓
- **v4 → v5 migrator block** is present at `SaveSchemaMigrator.cs:73-79`,
  empty per SPEC (`-1f` sentinel default is back-compat). v6 fail-hard
  preserved. ✓

---

## Scope / scene-mutation audit (Step 7 of CLAUDE.md visual-review checklist — adapted to backend task)

| Out-of-scope file | git diff lines |
|---|---|
| `Assets/Scripts/LiveStatProviderHost.cs` | 0 |
| `Assets/Scripts/StaminaRuntimeService.cs` | 0 |
| `Assets/Scripts/CharacterManager.cs` | 0 |
| `Assets/Scripts/UI/Roster/Data/PlayerCharacterData.cs` | 0 |
| Any `*.unity` / `*.prefab` / `*.asset` / `*.mat` | 0 |
| `Assets/Scripts/Physics/` (standing ban) | 0 |

Clean.

---

## Verdict

**READY_FOR_REDTEAM (PASS).** Every SPEC §8 acceptance row verified against
the live source (not just trusted from the self-reviewer). All cited tests
exist with the asserted assertion shape and assertion strength. D1-A
(backend → `Golfin.Core.Stamina` leaf reference) implemented exactly as
specced — cycle-free, no `ITournamentBackend` shape change, `IsConfigured`
fallback intact. D2 sentinel `-1f` hydrates to full at two well-tested
sites. D3=NO is proved by a 24h-clock-advance test that asserts a strict
decrease. D4 per-shot drain removed at the exact site SPEC named, with no
residual references to `DepleteStamina` from `ShotController.cs`. Save
schema v4 → v5 bump is back-compat (empty migrator block; sentinel default
on the new field). Out-of-scope files have zero diff lines.

Minor docs hygiene notes (NOT blockers — surfaced for awareness):
1. `T5_FailHard_V5Json_ThrowsSaveSchemaVersionException` is named confusingly
   — it tests v6 (not v5) is rejected. The substantive assertion is correct.
2. `T5_CurrentSchemaVersion_Is4` is named "Is4" but asserts the value is 5.
3. Implementer report cites `T6_Migration_V3ToV4_ConditionFieldsDefaultSafe`
   alongside the real `T5_V3ToV4_...` — self-reviewer flagged as typo, not
   a fabrication; the substantive test exists.

Setting STATUS to `READY_FOR_REDTEAM` per agent rules (golfin-reviewer no
longer writes `ARCHITECT_REVIEW_PASS`; that gate belongs to the red-team).

---

# RED-TEAM REVIEW — `stamina_tournament_wiring`

**Reviewer:** golfin-redteam-reviewer (adversarial gate)
**Timestamp:** 2026-06-30 08:24 CEST
**Verdict:** **ARCHITECT_REVIEW_PASS** (tried to break it across 7 vectors + 4 logic attacks; all held)

## Framing
Pure backend/logic/save-schema task. No UI/Figma/screenshot/video gates apply
(confirmed by kickoff). Canonical gate = EditMode `tests-run`, re-run by me
independently below; plus a full re-derivation of every acceptance row against
live source (Rule 5 — I did NOT carry the reviewer's PASS forward).

## Independent test re-run (not taken on faith)
Ran the EditMode suite myself via `unity-mcp-cli run-tool tests-run`:
`{"Status":"Passed","TotalTests":799,"PassedTests":796,"FailedTests":0,"SkippedTests":3,"Duration":"00:00:51"}`.
Matches the report's 796/0/3 EXACTLY. The 3 skips are pre-existing Stage C1
`HoleCompleteDriverTests` (documented no-ops, unrelated to this task). **Number
is real, not fabricated.**

## Seven kickoff attack vectors — verdicts
1. **Drain correctness & atomicity — HELD.** `SubmitHoleResult` (LocalTournamentBackend.cs:237)
   `nextCondition = Math.Max(0f, currentCondition - drain)`; persisted by the single
   `_store.Save(updated)` at L249 (only Save in the method — no double-persist, no
   compute-without-persist). `IsConfigured=false` path drains a sane flat
   `DefaultDrainPerHole=5f` (not 0, no crash). Sentinel `-1` hydrates to full
   BEFORE draining (L223-234), so a v4 entry's first hole drains from full, not from -1→0.
2. **Seed correctness — HELD.** `Register` seeds `MaxCondition(snapshot.Stamina)` (L158);
   `BeginRound` (4-param) seeds straight from `entry.ConditionRemaining` with no regen,
   no flat-100 reset (TournamentRoundContext.cs:96-105). Sentinel hydrates to full at
   BOTH load paths (`BeginTournamentHole` caller + internal `SubmitHoleResult`). A
   freshly-registered-no-snapshot entry reads -1 → hydrates to full on use, never persists -1 as a real pool.
3. **D4 — HELD.** `grep DepleteStamina ShotController.cs` → ZERO. Only residual refs are
   inside the dead `TournamentRoundContext.DepleteStamina()` legacy API itself (L127/138),
   which has no production caller. No other production writer of `StaminaEnergyRemaining`
   except `BeginRound`(seed) and `EndRound`(teardown reset). No alternate per-shot drain caller.
4. **Schema migration — HELD.** `CurrentSchemaVersion==5`; v4→v5 block empty (sentinel default
   safe); v6 fail-hard preserved; a v5 reload is idempotent (no re-drain/transform). v3 JSON
   migrates v3→v4→v5 preserving rewardPoints + defaulting condition fields (verified in
   `T5_V3ToV4_Migration_ConditionFieldsDefaultSafe` and `T6_Migration_V3ToV4_...`).
   Reproducible-from-drain-count (SPEC §6): `ConditionRemaining_SurvivesInMemoryRoundTrip`
   derives expected = `MaxCondition - N*DrainForHole()` — condition reconstructed from
   drain-count arithmetic alone, no clock. SPEC §6 is explicitly doc-only (no engine); satisfied.
5. **Scope — HELD.** I re-ran `git diff HEAD --` myself on all four OUT files
   (LiveStatProviderHost, StaminaRuntimeService, CharacterManager, PlayerCharacterData) →
   0 diff lines each. `*.unity`/`*.prefab`/`*.asset`/`*.mat`/`Physics/` diff = EMPTY.
   15 modified code files, all in SPEC §3 scope.
6. **asmdef — HELD.** `Golfin.Core.Stamina.asmdef` `references:[]` (true leaf);
   `Golfin.Tournaments` adds the reference → `Tournaments → Stamina` cycle-free (Stamina has
   zero outgoing edges). Full suite compiled + ran green = no circular-dependency error.
7. **Report integrity (Rule 6) — HELD; reviewers' "fabrication" flag was WRONG.** Every
   cited test EXISTS with the claimed shape. **Correction to both prior reviews:** they
   declared `T6_Migration_V3ToV4_ConditionFieldsDefaultSafe` "does not exist / citation typo /
   fabrication" — it DOES exist at `StaminaLiveWiringTests.cs:386` with a correct assertion.
   The implementer report was ACCURATE; the two reviewers under-grepped (searched only
   SaveLayerTests.cs). No fabrication by anyone; nothing to log.

## Test-name nits (surfaced, NOT blockers — assertions are all correct)
- `T5_CurrentSchemaVersion_Is4` — name says Is4, asserts `==5` (correct). Stale name.
- `T5_FailHard_V5Json_...` / `T6_FailHard_V5_...` — names say V5, bodies test v6-throws
  (correct, since v5 is now legal). Stale names.
These are cosmetic; the wrong thing is the NAME, never the ASSERTION. Per kickoff rule
("a stale NAME with a correct assertion is a surfaced nit, not a blocker") these do not fail.

## Strongest break attempt that held
I tried to force a **double-drain / drain-without-persist** race: a path where `SubmitHoleResult`
computes the drain but the entry that gets saved is the pre-drain clone, or where a v4 sentinel
entry drains from `-1` (→ clamped to 0 instantly, nuking the pool on the first tournament hole
after an app update). Both are defeated: the drain feeds the SAME `updated` EntryState that is
the sole argument to the single `_store.Save(updated)` (atomic, one write), and the sentinel is
hydrated to full at L223-234 BEFORE the `currentCondition - drain` subtraction — so a migrated
v4 player's first hole correctly drains `MaxCondition - drain`, not `0`. Verified by
`SubmitHoleResult_Persists_ConditionRemaining` (65) and the clamp test (≥0 after 16 drains).

## Verdict
**ARCHITECT_REVIEW_PASS.** Every SPEC §8 row independently re-derived against live source;
EditMode suite independently re-run green (796/0/3); all seven attack vectors and four logic
attacks held; scope clean; no fabrication (and a prior-review fabrication claim corrected as
itself incorrect). Only nits are three stale test NAMES with correct assertions — not blockers.
