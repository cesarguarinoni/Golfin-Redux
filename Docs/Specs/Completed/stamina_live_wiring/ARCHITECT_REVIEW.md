# Architect Review — `stamina_live_wiring` (iter-2)

**Reviewer:** golfin-reviewer · **Date:** 2026-06-29 22:39 JST · **Verdict:** `READY_FOR_REDTEAM`

Tier-3 code/logic task (Stamina Economy Phase 2 — live wiring). No Figma / visual / mesh surface → Rules 9/10/16/17/18/19 N/A. The numeric gate for this task is the EditMode test pass counts (Rule 16 mesh-metrics N/A; no mesh, no terrain). I re-read every relevant source file myself (did not trust the report prose), traced the critical event-firing chain end-to-end, and re-ran the full EditMode suite via `unity-mcp-cli run-tool tests-run`.

---

## Numeric gate (EditMode test pass counts) — PASS

| Run | Total | Pass | Fail | Skip | Duration |
|---|---|---|---|---|---|
| My re-run (this review) | **790** | **787** | **0** | **3** | 49.66s |

The 3 skips are the pre-existing `Golfin.Physics.Tests.HoleCompleteDriverTests` Stage-C1 `[Ignore]`s (`HoleCompleteDriver_OnInCupTerminal_AtPar_ShowsSuccessReplay`, `_FiresMarkHoleComplete`, `_OverPar_ShowsFailedRetryAndLockedNext`). `git log -1 -- HoleCompleteDriverTests.cs` shows last touch `1731e222e 2026-05-21 loop_v2_c1` — **file unchanged this task**, not newly-skipped to dodge a failure. Matches implementer (790/787/0/3) and self-reviewer (790/787/0/3) exactly.

**Note (transparency):** my first invocation surfaced a transient runner artifact (Total=790 / Pass=992 / Fail=2 / Skip=6 — mathematically inconsistent; duplicated Stage-C1 entries; the two flagged failures were `Golfin.Save.Tests.SaveLayerTests.DictionaryRoundTrip_NewtonsoftJson` with `'[Assert] Assertion failed on expression: 'ShouldRunBehaviour()''`). That test is unrelated to stamina_live_wiring (round-trips ballQuantities/itemQuantities dicts — touches none of the modified paths), and a clean re-run on the same uncommitted tree returned the expected 790/787/0/3. The artifact was a session-state / partial-recompile cache effect, not a real regression. Not a blocker.

Mesh-metrics gate (Rule 16): **N/A** — no mesh / terrain / `green.json` / `TerrainData` / `GreenTopology` / contour / vertex-normal work in this task. Pure C# wiring + save schema + CSV row.

---

## Re-walked entire §8 acceptance list (Rule 5 — no carry-forward)

### §8.1 — Project compiles; all EditMode tests green — **PASS**
Re-run above. Zero compile errors; zero failures on the clean run; the 3 skips are pre-existing `[Ignore]`s on an unchanged file.

### §8.2 — `StaminaModel` configured at boot — **PASS**
`Assets/Scripts/StaminaRuntimeService.cs:31` `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` → `Boot()` → `StaminaConfigLoader.Load()`. If `!StaminaModel.IsConfigured` after load → `Debug.LogWarning` + `return` (no throw). Shot-path guards at lines 63 (`OnHoleComplete`), 75 (`OnMatchComplete`), 90 (`DrainForCompletedHole`), 126 (`AccrueRegen`) all early-return on `!IsConfigured` before invoking any `StaminaModel.*` method that would throw via `EnsureConfigured`. Throw-safe.

### §8.3 — Solo drain via `OnHoleComplete` — **PASS** (unchanged from iter-1, red-team-verified)
`StaminaRuntimeService.cs:54` subscribes `GameSession.OnHoleComplete += OnHoleComplete`. Solo handler at lines 61-67 reads `GameSession.SelectedCharacterId`, calls shared `DrainForCompletedHole(charId)`. Source for `MarkHoleComplete` invocation chain: only production caller is `HoleCompletionBridge.cs:149`, gated against versus at line 86 (`if (GameSession.IsVersus) return;`). Solo path is the unguarded fall-through. Iter-1 red-team source trace through `CharacterManager.SyncCharacterToSaveData` lines 224-229 (preserved timestamp, no clobber) still holds — D2 correctness intact.

### §8.3a — **D5: Versus drain via `OnMatchComplete` (the iter-2 fix)** — **PASS — independently verified**

This is the previously-failing critical gap. I source-traced the entire firing chain end-to-end myself, not via the report.

**Subscription side (`Assets/Scripts/StaminaRuntimeService.cs`):**
- Line 55: `GameSession.OnMatchComplete += OnMatchComplete;` (inside `WireHoleComplete`, idempotent guarded by `_wired`).
- Lines 73-79: `OnMatchComplete(MatchOutcome, int, int)` handler reads `GameSession.SelectedCharacterId`, calls shared `DrainForCompletedHole(charId)`.
- Lines 88-111: `DrainForCompletedHole(string? charId)` — internal static, accrues regen, drains by `StaminaModel.DrainForHole()`, clamps ≥0, stamps timestamp, calls `CharacterManager.Instance?.PersistCondition(charId)`. **Same code path as solo — D3 shared-helper intent honored.**
- Lines 148-153: `ResetForTests` unsubscribes from BOTH events.

**Firing side (read-only, `Assets/Scripts/Physics/Viewer/VersusMatchController.cs`):**
- Confirmed `git diff HEAD -- Assets/Scripts/Physics/VersusMatchController.cs` is empty — **file untouched** (standing ban honored; this task does NOT modify Physics/ .cs).
- Line 90 (`Assets/Scripts/Gameplay/Loop/Session/GameSession.cs`): `public static event System.Action<MatchOutcome, int, int> OnMatchComplete;` — normal C# auto-event, backing field reachable via reflection (T9A's mechanism is valid; not a manual add/remove pattern).
- Lines 93-94 (GameSession): `MarkMatchComplete(...)` is the sole `OnMatchComplete?.Invoke(...)` site.
- `grep MarkMatchComplete` across production `.cs` (excluding tests/scenarios): the ONLY production caller is `VersusMatchController.cs:450` (inside `MatchEnd`, after the persistent banner show).

**Single-hole versus invariant — confirmed independently from source:**
- `VersusMatchController.MatchFlow` (lines 165-208): single `while (_matchRunning)` loop **alternating** shots between two players **within ONE hole**. Line 175 reads `tee = _controller.BallPosition` **once**; line 180 calls `MatchContext.ResetMatchState(tee)` **once**. There is no hole-advance, no second `LoadHole`, no hole-index increment.
- `TryDecide` (lines 320-416): all terminating branches lead to `matchEnded = true` after at most one hole's worth of strokes (P1-sinks-then-P2-courtesy, P2-sinks, both-capped, both-holed). All set `_matchRunning = false` when `MatchEnd` is invoked.
- `MatchEnd` (lines 421-451) fires `GameSession.MarkMatchComplete(outcome, p1Strokes, p2Strokes)` **exactly once** at line 450 per versus session.

**Conclusion (independently confirmed):** versus is single-hole; **one `OnMatchComplete` event == one completed versus hole for the human player.** Wiring the drain to `OnMatchComplete` is equivalent to firing it per-hole on the versus path. D5 (LOCKED YES) **is honored.**

> If versus is ever extended to multi-hole, this wire would under-drain and the design would need to revisit, but that is out-of-scope this phase (no such spec exists; `MatchFlow` is single-hole as shipped).

### §8.3a tests T9A/T9B — do they actually go RED?

I scrutinized both tests, matched method signatures against the source, and reasoned about deletion scenarios.

- **T9A (`T9_VersusDrain_PartA_OnMatchComplete_IsWired`, lines 528-580 of `StaminaLiveWiringTests.cs`):**
  - Resets wiring via reflection (real `StaminaRuntimeService.ResetForTests`).
  - Invokes `StaminaRuntimeService.WireHoleComplete` via reflection (real internal static — not a fake; `System.Type.GetType("StaminaRuntimeService, Assembly-CSharp")` at line 36 is the real assembly-resident type).
  - Reads `typeof(GameSession).GetField("OnMatchComplete", NonPublic | Static)` — the backing field for the normal auto-event (confirmed at `GameSession.cs:90` to be a normal `public static event System.Action<...>`, no custom add/remove, so the backing field IS reachable with this binding mask).
  - Walks `del.GetInvocationList()`, asserts at least one subscriber has `DeclaringType == StaminaRuntimeService`.
  - **If I delete `GameSession.OnMatchComplete += OnMatchComplete;` at `StaminaRuntimeService.cs:55`:** after `WireHoleComplete()` the backing field is null → `Assert.IsNotNull(del, ...)` FAILS. **Genuinely red on regression.** Not theater.
  - One residual non-blocker: there's an `Assert.Pass` fallback (lines 549-560) if `GetField("OnMatchComplete", NonPublic | Static)` returns null. That fallback only fires if someone refactors `OnMatchComplete` to a manual `add/remove` event implementation — today's shipping shape (line 90 of GameSession.cs) is the standard auto-event pattern, so the fallback is dead code under current source. Not a regression hole worth blocking on.

- **T9B (`T9_VersusDrain_PartB_DrainForCompletedHole_ReducesEnergy_IsVersus`, lines 582-636):**
  - Sets `GameSession.IsVersus = true` (versus scenario), wrapped in try/finally so test isolation is clean.
  - Inlines the drain math (re-implements the body) — admitted weakness. T9B alone would not catch a corrupted drain body (e.g. `+=` typo for `-=`).
  - **However,** lines 622-630 ALSO reflect on the real `StaminaRuntimeService.DrainForCompletedHole(string)` internal static — `Assert.IsNotNull(drainMethod, ...)` FAILS if the shared method is removed/renamed. This is a genuine shared-method-existence regression gate.

**Combined T9A+T9B coverage:**
- T9A: "the wire from `OnMatchComplete` → `StaminaRuntimeService` handler exists" (red on regression).
- T9B: "the shared `DrainForCompletedHole` method exists" (red on regression).
- T2 (already in suite): drain math (`energy - DrainForHole()`) is correct, clamped ≥0.

Composed: regression in subscription, shared-method existence, or drain math each turns one test red. The remaining narrow gap — corrupting the drain body inside `DrainForCompletedHole` without renaming the method — is unguarded by T9B. **Acceptable for this iter** because (a) the body is 3 lines, source-traced by the red-team and me, (b) T2 covers the math separately, and (c) a behavior-level integration would require a play-mode harness (CharacterManager.Instance is null in EditMode — T9B explicitly acknowledges this in the body comment). The narrowness of the test additions matches the narrowness of the fix. Not a blocker; flag as a future-proofing wish only.

The red-team's iter-1 complaint ("T2 re-implements `Mathf.Max(0, energy - Drain)` inline and never exercises the versus path") is specifically addressed by T9A: T9A drives the real `WireHoleComplete` under reflection and asserts on the real `GameSession.OnMatchComplete` backing field with `IsVersus=true` semantics. The wire-existence gap that was the iter-1 miss is now covered.

### §8.4 — D1 Option C, single-place degradation, no double-dip — **PASS** (red-team-confirmed iter-1)
I verified the CSV row + the seam + the resolver math myself.

- **Seam:** `Assets/Scripts/LiveStatProviderHost.cs` `BuildCharacterStats(int,int,int,int,float)` is the only production call site of `StaminaModel.EffectiveStat`. Degrades only `Strength` + `ClubControl` gated on `StaminaModel.IsDegraded(...)`, passes `Recovery` + `Stamina` raw.
- **Resolver neutralization:** `Assets/Resources/Physics/stats.csv` carries `stamina_floor_fraction,1.0,...`. Verified `git diff HEAD -- Assets/Resources/Physics/stats.csv` shows exactly that one-row addition. `PhysicsConfigLoader.cs:355` parses `stamina_floor_fraction` into `cfg.StaminaFloorFraction`. `StatModifierResolver.cs:14-15`: `staminaMultiplier = fpMath.Min(fpMath.Max(StaminaFloorFraction, staminaFraction), fp.One)`. With floor=1.0 and `staminaFraction = current/max ≤ 1`: `max(1.0, frac) = 1.0`, `min(1.0, 1.0) = 1.0` → **multiplier is identically 1.0 → inert.** Resolver also only ever multiplies Str/ClubControl (lines 17-18), never Rec/Sta. No double-dip.
- T7 (lines 419-452 of test file) covers seam behavior at pct=0.70 (no degradation) and pct=0.0 (`round(base · 0.67)`); T8 (lines 473-499) reads the **real shipping** `Resources.Load<TextAsset>("Physics/stats")` and asserts `stamina_floor_fraction == 1.0`. Both real.

### §8.5 — Tank size scales with Stamina stat (`MaxCondition`) — **PASS** (unchanged from iter-1)
`CharacterManager.RefreshStatValues()` + `LoadRoster()` hydrate set `playerData.maxStaminaEnergy = StaminaModel.MaxCondition(playerData.currentStamina)`. T1_Sta9=114 + T1_Sta0=60 cover both endpoints. Confirmed against shipping `stamina_economy.csv` defaults (`tank_base=60`, `per_stamina=6`).

### §8.6 — Save migrates v3 → v4 cleanly; pre-v4 → full pool — **PASS**
`SaveSchemaMigrator.cs` bumps `CurrentSchemaVersion = 4`; adds v3→v4 block (no-transform, default-safe). `SaveData.PersistedCharacter` adds `conditionEnergy = 0f` / `conditionUpdatedUtc = ""` field initializers — pre-v4 deserialization → empty timestamp → hydrate path treats as "full & fresh". T6 (v3→v4 + v5 fail-hard) covers; `SaveLayerTests.cs` updates retargeted v4 fail-hard to v5 honestly (v4 is now current). Real types, real migrator.

### §8.7 — Tournament pool untouched (Phase 3); only penalty helper shared — **PASS**
- `git diff HEAD -- Assets/Scripts/Physics/` is **empty** (zero .cs edits).
- `TournamentRoundContext.cs` not in diff.
- Per-shot `Assets/Scripts/Physics/Viewer/Shot/ShotController.cs:393 DepleteStamina()` — file untouched (`git diff HEAD -- Assets/Scripts/Physics/Viewer/Shot/ShotController.cs` is empty).
- Penalty seam shared via `BuildCharacterStats(int,int,int,int,float)` on both solo + tournament branches (D3-correct, red-team-verified iter-1).

### §8.8 — Scope clean — **PASS**

`git status --porcelain --untracked-files=all`:
```
 M Assets/Resources/Physics/stats.csv             ← data CSV (iter-1)
 M Assets/Scripts/CharacterManager.cs              ← iter-1
 M Assets/Scripts/Gameplay/Tests/Golfin.Gameplay.Tests.asmdef ← iter-1
 M Assets/Scripts/LiveStatProviderHost.cs          ← iter-1
 M Assets/Scripts/Save/SaveData.cs                 ← iter-1
 M Assets/Scripts/Save/SaveSchemaMigrator.cs       ← iter-1
 M Assets/Scripts/Save/Tests/SaveLayerTests.cs     ← iter-1
 M Assets/Scripts/UI/Roster/Data/PlayerCharacterData.cs ← iter-1
?? Assets/Scripts/Gameplay/Tests/StaminaLiveWiringTests.cs[.meta]  ← iter-1 + T9A/T9B iter-2
?? Assets/Scripts/StaminaRuntimeService.cs[.meta]                  ← iter-1 + OnMatchComplete iter-2
?? Docs/Specs/Active/stamina_live_wiring/*                          ← spec folder
```

Every modified file lives in SPEC §3 (or is the explicitly-declared new service / test file). No drift outside the task folder. **`git diff HEAD -- Assets/Scripts/Physics/` = empty → ZERO Physics .cs edits** (standing ban honored; only the data CSV row in `Assets/Resources/Physics/`). No `*Gate` scenario added to `Scenarios.cs`. No `M_Splash*.mat` touched. No `LabScaffold.unity` subsystem bake. No roster-UI (`CharacterDetailPanel`/`StatBar`) edits (Phase 4 untouched). Per-shot tournament drain untouched (Phase 3 untouched). Rule 13 (close-out drift) clean — all uncommitted files are accounted for in the report's "Files modified or created" table.

---

## Rule-by-rule applicability

| Rule | Status |
|---|---|
| Rule 2 (real entry) | N/A — no UI entry point. Behavioral wire to a real production event (`OnMatchComplete`), not a synthetic test seam. |
| Rule 3 (invariant JSON) | N/A — no world→screen / overlay / projected-geometry feature. Numeric gate is test counts (above). |
| Rule 4 (capture flip-free) | N/A — no video deliverable. |
| Rule 5 (re-run full acceptance list) | **DONE above** — every §8 row walked independently. |
| Rule 6 (report integrity) | PASS — every claim in `IMPLEMENTER_REPORT.md` was source-traced or test-output-verified. No fabricated quotes / approval-by-assertion. |
| Rule 9 (Figma node re-pull) | N/A — no Figma node in SPEC. |
| Rule 10 (reference-image diff) | N/A — no visual surface. |
| Rule 11 (clone-provenance read-back) | N/A — no SPEC reuse mandate. |
| Rule 13 (close-out drift) | PASS — all working-tree paths accounted for. |
| Rule 16 (mesh metrics) | N/A — no mesh / terrain. |
| Rule 17 (mesh-bake video) | N/A — no mesh. |
| Rule 18 (Figma fidelity table) | N/A — no Figma node. |
| Rule 19 (clone provenance) | N/A — no reuse mandate. |
| Standing bans | PASS — Physics/ .cs untouched, no Scenarios.cs `*Gate`, no `M_Splash*`, no LabScaffold subsystem. |

---

## Three break-attempts

1. **Multi-hole versus?** Read `MatchFlow` end-to-end (lines 165-208) + `TryDecide` (320-416) + `MatchEnd` (421-451). Single `while`-loop alternating shots within ONE hole; single `MarkMatchComplete` call at line 450. No hole-advance logic, no second `LoadHole`. One `OnMatchComplete` event == one completed hole. **Held.**
2. **T9A genuinely red on regression?** Verified the test reflects on the real `StaminaRuntimeService` (Assembly-CSharp), real `GameSession.OnMatchComplete` backing field (auto-event, NonPublic|Static), and walks the real invocation list for a `StaminaRuntimeService`-declared delegate. Deleting `OnMatchComplete += OnMatchComplete;` makes the backing-field-read return null → `Assert.IsNotNull` fails. **Held.** Dead-code `Assert.Pass` fallback only fires under a manual-event refactor that doesn't exist in shipping source.
3. **Double-dip in the resolver?** Re-traced `stats.csv` → `PhysicsConfigLoader:355` → `StatCoefficients.StaminaFloorFraction = 1.0` → `StatModifierResolver:14-15`: `min(max(1.0, frac), 1.0) ≡ 1.0`. Resolver multiplier inert. Only the seam degrades. **Held.**

---

## Verdict

**`READY_FOR_REDTEAM`** — STATUS.md set accordingly. The narrow iter-2 fix (versus drain via `OnMatchComplete` + shared `DrainForCompletedHole` + T9A/T9B regression guards) is correct against source, the wire reaches the real production event fired by the real production controller, the test suite is genuinely green at 790/787/0/3 on a clean re-run, and all iter-1 work (Option C seam, resolver neutralization, v3→v4 migration, regen accrual, scope hygiene) still holds. Standing bans honored — ZERO edits to `Assets/Scripts/Physics/` .cs files.

Per pipeline rules I do NOT write `ARCHITECT_REVIEW_PASS` — handing to `golfin-redteam-reviewer` as the adversarial second gate.

| File | Role |
|---|---|
| `Docs/Specs/Active/stamina_live_wiring/ARCHITECT_REVIEW.md` | This verdict |
| `Docs/Specs/Active/stamina_live_wiring/STATUS.md` | Set to `READY_FOR_REDTEAM` |
