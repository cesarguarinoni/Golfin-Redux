# Red-Team Review — `stamina_live_wiring` (iter-2)

**Reviewer:** golfin-redteam-reviewer · **Date:** 2026-06-30 JST · **Verdict:** `ARCHITECT_REVIEW_PASS`

Tier-3 code/logic task (Stamina Economy Phase 2). No Figma/mesh/visual/clone surface → Rules 16/17/18/19 N/A (correctly). Gate = code correctness + behavioral correctness of the stamina economy. I re-read the actual production source (not the reports), re-ran the suite twice, and drove a live editor reflection probe to confirm the D5 wire.

This is iter-2 of iteration-shape `stamina-wiring:versus-drain-unwired`. iter-1 was my own FAIL: versus never drained because `StaminaRuntimeService` subscribed only to `GameSession.OnHoleComplete`, which `HoleCompletionBridge:86` suppresses on the versus path. I reproduced that exact rejection below and confirmed it is GONE.

---

## Prior-rejection reproduction (Rule 15) — D5 versus drain: GONE

iter-1 blocker: in a real 1v1, `IsVersus=true` → `HoleCompletionBridge.HandleShot()` returns at line 86 before `MarkHoleComplete` → `OnHoleComplete` never fires → versus never drained.

**iter-2 fix verified from source + live editor:**
- `StaminaRuntimeService.WireHoleComplete()` now subscribes to **both** `GameSession.OnHoleComplete` (solo) AND `GameSession.OnMatchComplete` (versus). Both route through the shared `internal static DrainForCompletedHole(string?)`.
- **Live editor probe (script-execute):** after `WireHoleComplete()`, `OnMatchComplete` invocation list = `total=1 stamina_found=True`. The Stamina handler IS subscribed to the real event in the running editor. Backing field is accessible (`BACKING_FIELD_FOUND=True`), so the T9A assertion path is the REAL one, not the `Assert.Pass` escape branch.

### Single-hole confirmation (the "shifted blocker" check)
Read `VersusMatchController.MatchFlow()` end-to-end (lines 165-208): reads ONE tee at hole load (`_controller.BallPosition`), runs alternating turns on that single hole, and ends via `MatchEnd` → `MarkMatchComplete` exactly once when someone holes out or hits the stroke cap. **There is genuinely NO multi-hole loop / hole-advance.** A versus match = one hole → `OnMatchComplete` fires once → drains once. No under-drain. The blocker did not shift, it is gone.

### Correct character, once, clamped, AccrueRegen+Persist
`OnMatchComplete` → `DrainForCompletedHole(GameSession.SelectedCharacterId)` → `AccrueRegen` → `Mathf.Max(0, energy - DrainForHole())` → stamp ts → `PersistCondition`. Identical correctness to the solo path (shared body). On the 1v1 entry path `SelectedCharacterId` = the **human's** live char: `MatchmakingModalController:477 SeedSession(hole, CharacterManager.GetSelectedCharacterId(), bag)`. The opponent is populated separately into `MatchContext.Players[1]` (line 486+), never into `SelectedCharacterId`. So versus drains the live human char, not the opponent, not empty.

---

## Mirror-risk: double-fire / double-drain — NONE
- **Versus drains exactly once:** `HoleCompletionBridge.cs` git diff vs HEAD = EMPTY → the `if (GameSession.IsVersus) return;` suppression at line 86 is intact. Versus fires ONLY `OnMatchComplete`; `OnHoleComplete` never fires for versus. No double-drain.
- **Solo never fires `OnMatchComplete`:** the ONLY production caller of `MarkMatchComplete` is `VersusMatchController:450`, and that controller hard-no-ops in `Start()` when `!GameSession.IsVersus`. So solo fires only `OnHoleComplete`. No solo double-drain. (The `Scenarios.cs:2954` `MarkMatchComplete` call is a pre-existing editor capture-bot fallback, not in this diff, not a production play path.)

---

## Everything else I attacked from source — held

**D1 / Option C double-dip — degradation applied EXACTLY once.**
`LiveStatProviderHost.BuildCharacterStats(int,int,int,int,float)` is the single degradation site: degrades only Strength+ClubControl via the `IsDegraded(...)` gate, passes Recovery+Stamina raw, guards `!IsConfigured` → raw stats + log-once (no throw). Resolver neutralized: `Assets/Resources/Physics/stats.csv` line 15 `stamina_floor_fraction,1.0,...`; parse chain `PhysicsConfigLoader:355 case "stamina_floor_fraction" → cfg.StaminaFloorFraction = fp.FromFloat(1.0)`; resolver `StatModifierResolver:13-15` `staminaMultiplier = min(max(StaminaFloorFraction, frac≤1), 1) = min(max(1.0,frac),1.0) = 1.0` identically → inert. Header line correctly skipped; the row's comment uses semicolons so it splits clean (and only parts[0]/parts[1] are used regardless). No double-dip.

**Boot race — no throw on shot path.** `Boot()` ([RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]) loads config; `!IsConfigured` → log+return. All five `StaminaModel.*` call sites (`BuildCharacterStats`, `OnHoleComplete`, `OnMatchComplete`, `DrainForCompletedHole`, `AccrueRegen`) guard `!IsConfigured` and return raw/no-op. `CFG=True` confirmed in editor.

**Clamp/edge math — correct.** Drain `Mathf.Max(0, …)` (≥0); regen `Mathf.Min(max, …)` (≤max); `AccrueRegen`: default ts → stamp+return; `elapsed.TotalHours <= 0` → no-op. (Minor: `DrainForCompletedHole` stamps ts then `PersistCondition` calls `AccrueRegen` again a few ms later → adds a sub-microscopic regen; clamped, negligible, pre-existing iter-1, covered within epsilon by T5. Not a blocker.)

**Save migration v3→v4 — clean, fail-hard preserved.** `CurrentSchemaVersion=4`; no-transform v3→v4 block; `PersistedCharacter` adds `conditionEnergy=0f`/`conditionUpdatedUtc=""` (field initializers → default-safe → full pool on hydrate). Fail-hard-on-newer guard intact (`schemaVersion > Current` throws). `SaveLayerTests` updates honest: old `T5_FailHard_V4` retargeted to v5, `Is3→Is4`, new `T5_V3ToV4` against the real `SaveSchemaMigrator.Migrate` + real `SaveData`.

**Config vs SPEC/test numbers — match shipping CSV.** drain 8, MaxCondition(9)=114 / (0)=60, regen Rec9=30/hr, comfort 0.70, floor 0.33, exp 1.6, degraded `Strength;ClubControl`.

**Scope — clean.** `git diff HEAD` = exactly the 8 SPEC §3 tracked files + 2 new untracked (`StaminaRuntimeService.cs`, `StaminaLiveWiringTests.cs` + metas). `git diff HEAD -- Assets/Scripts/Physics/` = EMPTY (zero .cs; only the data CSV). No `CharacterDetailPanel`/`StatBar` (Phase 4), no `TournamentRoundContext`/`ShotController` (Phase 3 — per-shot `DepleteStamina` untouched), no `Scenarios.cs` `*Gate`, no `LabScaffold`, no `M_Splash`.

---

## Test honesty

- **T9A (`PartA_OnMatchComplete_IsWired`) — GENUINE primary-defect guard.** Drives the real `WireHoleComplete()`, reads the real `GameSession.OnMatchComplete` backing field (confirmed accessible → real assert branch, not Pass escape), asserts a `StaminaRuntimeService`-declared delegate is in the invocation list. Goes RED if the `OnMatchComplete +=` line is deleted. This guards the *exact* iter-1 defect (missing wire). Empirically `stamina_found=True`.
- **T9B (`PartB`) — adequate-but-weaker (secondary).** Re-implements the drain arithmetic inline rather than invoking the production `DrainForCompletedHole`, plus a reflection method-existence check. It does NOT end-to-end exercise the drain body. RATIONALE I accept: (1) the primary D5 regression risk was the wire, which T9A covers directly; (2) the drain arithmetic is independently covered by T2/T3/T5 and re-derived correct from source; (3) full end-to-end drain is infeasible in EditMode — I confirmed `CharacterManager.Instance` NREs/null without a running scene, and the iter-1 fix instruction explicitly allowed "drive the real versus path OR the shared DrainForCompletedHole." Tightening T9B to actually invoke the wrapper in a PlayMode harness is a worthwhile follow-up, NOT a production-correctness blocker.
- T7 (real private static seam), T8 (real shipping `Physics/stats` asset), T3 (real `AccrueRegen` static) — all real, as in iter-1.

## Independent test re-run (I ran it twice)
`unity-mcp-cli run-tool tests-run` (EditMode, full suite), two runs:
**TotalTests 790 · Passed 787 · Failed 0 · Skipped 3 · Status Passed** (both runs identical).
The 3 skips are the pre-existing `Golfin.Physics.Tests.HoleCompleteDriverTests` Stage-C1 `[Ignore]`s (`HoleCompleteDriverTests.cs` unchanged this task). **The reviewer's flagged transient anomaly (Pass=992 > Total=790, phantom `SaveLayerTests.DictionaryRoundTrip_NewtonsoftJson`) did NOT reproduce in either of my runs** — SaveLayer is clean, the v3→v4 migration did not break Newtonsoft dictionary round-trip.

## Three break-attempts
1. **Versus multi-hole under-drain** — FAILED to break: `MatchFlow` is single-hole, `OnMatchComplete` fires once.
2. **Double-fire double-drain (the mirror of the original bug)** — FAILED to break: bridge suppression intact; versus fires only OnMatchComplete, solo only OnHoleComplete; `MarkMatchComplete` is versus-only.
3. **Wrong-char / empty-char drain on versus entry** — FAILED to break: `SelectedCharacterId` = live human char, seeded at matchmaking; opponent is `Players[1]`, never SelectedCharacterId.

## Routing
`ARCHITECT_REVIEW_PASS` → advances to Cesar. The iter-1 D5 versus-drain blocker is genuinely fixed (verified from source AND a live editor wire probe), no double-fire mirror bug introduced, no double-dip, scope clean, 790/787/0/3 twice. The only residual (T9B not invoking the production drain wrapper) is a test-tightening follow-up, not a correctness defect — flagging it for the implementer's backlog, not blocking on it.
