# Architect Review — `loop_v2_smoke_bot`

**Reviewer (iter-4b):** golfin-reviewer (Claude Code)
**Date (iter-4b):** 2026-05-20 07:18 JST
**Verdict (iter-4b):** **ARCHITECT_REVIEW_PASS**

---

## Independent visual scan (iter-4b captures, pixel-only — written BEFORE reading IMPLEMENTER_REPORT/SELF_REVIEW/prior verdicts)

**hole1_playthrough (s01–s06):** s01 is the Home screen — currency bar "50.000" top-left, gear top-right, "CHOTO" nameplate, a golfer holding a gold trophy centered, an amber "MAINTENANCE NOTICE" banner, a "NEXT HOLE / Lomond Country Club - Hole 1" card, yellow "PLAY" button, five-icon bottom nav. s02 is the matchmaking modal "DIAMOND LEAGUE / FINDING OPPONENT.." with YOU vs GREENKND portrait cards and a "CANCEL" button. s03 is the same modal now reading "OPPONENT FOUND" with YOU vs FAIRPRO — a distinct opponent revealed. s04 is the gameplay scene: top-left character card "JAMES / Lv 10 / TURN 1", top-right "LOMOND / HOLE 1 - REGULAR / PAR 5", a fairway with two green range markers, the G-logo ball mid-fairway with a vertical aim arc, bottom controls SPIN / GOLFIN / DRIVER / STRAIGHT. s05 is visually near-identical to s04 — same gameplay scene, same HUD, same controls. s06 is a fully different screen: a stacked result modal — green "✓ SUCCESS" header over a "Lomond Country Club - Hole 1 - Par 5" card with stroke/time stats and a grey "REPLAY" button, and below it a "NEXT" card for "Lomond Country Club - Hole 2 - Par 4" with a hole description and a yellow "PLAY" button. The modal pixels are crisp, legible and fully rendered.

**settings_round_trip (s01–s04):** s01 is Home. s02 is the Settings panel — USER PROFILE / SOUND SETTINGS / LANGUAGE / TERMS OF USE / PRIVACY POLICY / FAQ / ABOUT / CONTACT FORM / LOG OUT rows, "CLOSE" button — accordion collapsed. s03 has the SOUND SETTINGS row expanded showing MUSIC (70) and SFX (70) sliders inline — visibly distinct from s02. s04 is Home returned.

**hole_selection_browse (s01–s03):** s01 is Home. s02 is the Hole Selection screen — header "LOMOND 28/72", a "NEXT / Lomond Country Club - Hole 1 - Par 5" expanded card with description text + yellow "PLAY", and three "LOCKED" cards for Hole 2/3/4 below. s03 is Home returned — identical to s01.

The captures honestly depict the production flows the bot drove. s06 unambiguously shows the HoleCompleteWidget — the Stage C1 gate capture passes the pixel test. s05 and s06 are completely different screens.

## Figma side-by-side

N/A — this is a TELLCODE bot-framework task with no Figma reference (SPEC.md has no § Reference). Visual fidelity here means "the captures honestly show the production flows the bot drove," which is verified in the pixel scan above and the pixel-diff section below. The deliverable is a four-file framework + reusability contract, judged on architectural soundness and capture honesty.

## Bbox verification

N/A — no containment claims ("X inside Y") in SPEC.md or IMPLEMENTER_REPORT.md. The deliverable is a bot framework, not a UI layout. No `script-execute` bbox check required.

## Pixel-diff verification (independent re-measure, full-res PNGs, numpy)

| Pair | Result | Verdict |
|---|---|---|
| s04 vs s05 | **40628 / 2962440 = 1.37% pixels differ** | Two honest gameplay frames. Within the architect-sanctioned ~1.4–2.3% band — Cesar explicitly chose "Real pre-modal s05" via AskUserQuestion knowing s05 would resemble s04. NOT a FAIL per the kickoff brief. |
| s05 vs s06 | **2962386 / 2962440 = 100.00% pixels differ** | s05 is the live gameplay scene, s06 is the result modal — fully distinct screens. The iter-4b s05 rework is real and verified. |
| s04 vs s06 | 2962386 / 2962440 = 100.00% pixels differ | Confirms s06 is the modal, not a gameplay frame. |

My independent measurement (1.37% for s04↔s05) matches the implementer's iter-4b quote exactly and resolves the self-reviewer's 2.26% discrepancy note (self-reviewer measured compressed PNGs; I measured full-res — 1.37% is the correct figure). No disagreement between my visual scan and the report's claims.

## MD5 distinctness (independent re-grade)

| Scenario | MD5s | Grade |
|---|---|---|
| hole1_playthrough | s01 `7d95b3bc`, s02 `6e1540be`, s03 `7b07550c`, s04 `a0a1495e`, s05 `6688ad0f`, s06 `ecc4b8df` | PASS — all 6 distinct. |
| settings_round_trip | s01 `7d95b3bc`, s02 `558be923`, s03 `8727c0b0`, s04 `3e5c2a92` | PASS — all 4 distinct (s04 differs from s01 by a 1px frame delta; both are Home — acceptable). |
| hole_selection_browse | s01 `7d95b3bc`, s02 `41c5d763`, s03 `7d95b3bc` | PASS — s02 distinct; s01==s03 by round-trip design (architect-sanctioned, not a bug). |

Note: all three s01_home captures share `7d95b3bc` — they are the same Home screen across all three scenarios, exactly as expected for a cold-launch-to-Home prefix.

## Scenario history.log audit

All three logs read end-to-end:
- `hole1_playthrough/history.log` — ends `=== Scenario complete ===`. Contains `ForceShotComplete: driving terminal=InCup` then `ForceShotComplete OK: terminal=InCup`, then s06 captured 2.84s later. No `EXCEPTION` / `INCOMPLETE` lines.
- `settings_round_trip/history.log` — ends `=== Scenario complete ===`. Clean linear flow.
- `hole_selection_browse/history.log` — ends `=== Scenario complete ===`. Clean linear flow.

## Scene-mutation audit

- `git diff --stat -- '*.unity'` → **empty** (clean repo-wide — no `m_IsActive` flips, no `sizeDelta`/position mutations, no `[LoopV2SmokeBot]` GO saved to any scene).
- `git diff --stat ProjectSettings/` → **empty** — the iter-4b `runInBackground` fix verified as a runtime flag (`Application.runInBackground = true` set at `EnteredPlayMode` in `LoopV2SmokeBotMenu.cs:139`), zero `ProjectSettings.asset` footprint. Claim holds.
- Environmental modifications (noted, NOT FAIL): `Packages/manifest.json` shows `com.ivanmurzak.unity.mcp` `0.72.1 → 0.72.2` (MCP plugin self-update overnight); `packages-lock.json` follows it; `Assets/Fonts/NotoSansJP-VariableFont_wght SDF.asset` is a TMP dynamic-atlas regen from play-mode runs. None is bot scene contamination — all pre-cleared in the kickoff brief.

## Seam audit (iter-4b)

| Condition | Status | Evidence |
|---|---|---|
| (i) `#if UNITY_EDITOR` guard on `ForceShotCompleteForBot` | PASS | `BallStateMachine.cs:287` opens `#if UNITY_EDITOR`, `:315` closes `#endif` — the entire method is inside the guard. Compiler-level proof it cannot leak to player builds. |
| (ii) `_ForBot` suffix | PASS | Method named `ForceShotCompleteForBot` — grep-visible to any future maintainer. |
| (iii) Delegates to the same `OnShotComplete` event | PASS | `BallStateMachine.cs:313` — `OnShotComplete?.Invoke(result)`, the exact event production fires. Synthetic `ShotResult` constructed with InCup terminal + sensible defaults. |
| (iv) `FireShot` still present in `BotDriver.cs` (additional, not replaced) | PASS | `BotDriver.cs:444` — `public IEnumerator FireShot(Vector3 worldTarget, float power01 = 1f, float timeoutSeconds = 30f)` — unchanged §2f primitive. `ForceShotComplete` at `:614` is a SEPARATE additional primitive. Both coexist. |
| (v) No seams beyond the three authorized | PASS | `git diff --stat` touches only `BallStateMachine.cs` (seam 3, iter-4 authorized), `BotDriver.cs`, `Scenarios.cs`, `LoopV2SmokeBotMenu.cs`. No new diff on `MatchmakingModalController.cs` or `PhysicsLabController.cs` (seams 1 & 2, already in place from earlier iters). Exactly the three architect-authorized seams — no fourth. |

The seam matches `ARCHITECT_VERDICT_INCUP.md` § "Concrete deliverable for iter-4 (a)" verbatim, including the five-condition compliance comment block.

## Audit greps

| Item | Result |
|---|---|
| `ls Bot/` → BotDriver.cs, LoopV2SmokeBot.cs, Scenarios.cs, Editor/LoopV2SmokeBotMenu.cs | PASS — all 4 present. |
| All 4 files `#if UNITY_EDITOR` guarded | PASS — each has exactly one `#if`/`#endif` pair (BotDriver's grep-count of 2 was a false positive: line 611 is `#if UNITY_EDITOR` inside a `///` doc comment, not a directive — verified). |
| `CaptureCore.SnapPlayModeSafe` in BotDriver | PASS — used in the `Capture` method (`BotDriver.cs:86`). Canonical sanctioned play-mode path per CLAUDE.md § Screenshots. |
| `[MenuItem]` × 3 action items | PASS — 6 total (3 action + 3 isValidateFunction) per the Option B safety pattern. |
| Project compiles clean | PASS — EditMode gate ran 305/305; a non-compiling project cannot run the gate. |
| EditMode test gate 305/305 PASS | PASS — `Docs/Diagnostics/all_editmode_test_results.txt` (2026-05-20 06:46): TOTAL 305 / PASSED 305 / FAILED 0 / SKIPPED 0 / GATE PASS, duration 23.75s, via `mcp__ai-game-developer__tests-run`. |

## Production-flow capture verification

PASS. The hole1 captures ARE the production-flow path: cold launch → click PLAY → real `MatchMakingModal` → real additive scene load (`LabScaffold` + `Hole_01_Geo`) → real gameplay HUD frames (s04/s05) → `ForceShotComplete` seam fires the same `OnShotComplete` event production fires → real `HoleCompleteWidget` reacts (s06). No `*Host`/`*SmokeRunner` pre-scripted state injection bypasses the production lifecycle. This task changes no modal/panel layout, so the smoke-vs-production distinction is not the failure mode here — but the captures are nonetheless genuine production-flow, not smoke-runner-only.

## Capture-helper compliance

PASS. `BotDriver.cs` uses `CaptureCore.SnapPlayModeSafe` exclusively — a sanctioned `CaptureCore` path explicitly listed in CLAUDE.md § Screenshots for play-mode coroutines that capture-and-continue. No `ScreenCapture.CaptureScreenshot`, no manual OS screenshots, no per-task workaround. No new static-bus `*Context.cs` files added under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`, so the capture_helper maintenance protocol (FakeMidAim/FakeReset extension) does not apply. Self-reviewer's Step 5 finding confirmed correct.

## Spec-claimed PASS verification (independent re-grade)

| Spec / kickoff item | Implementer marked | My re-grade | Notes |
|---|---|---|---|
| 4 bot files exist + guarded | PASS | PASS | All 4 present; each one `#if`/`#endif` balanced. |
| `[MenuItem]` × 3 action | PASS | PASS | 6 total (3+3 validate). |
| Project compiles clean | PASS | PASS | EditMode gate ran. |
| EditMode 305/305 | PASS | PASS | Evidence file verified, fresh 06:46 timestamp. |
| hole1 — 6 MD5-distinct PNGs + log | PASS | PASS | 6 distinct hashes, log ends `=== Scenario complete ===`. |
| **hole1 — s06 shows HoleCompleteWidget (C1 gate)** | PASS | **PASS** | Pixel scan: ✓SUCCESS / Hole 1 - Par 5 / REPLAY / NEXT Hole 2 - Par 4 / PLAY — modal fully rendered. Hard-FAIL condition (blank/absent modal) NOT triggered. |
| **hole1 — s05 ≠ s06 modal** | PASS | **PASS** | Independent pixel-diff: 100.00% differ. s05 is gameplay, s06 is modal. |
| hole1 — s04 ≈ s05 (gameplay frames) | PASS | PASS | 1.37% differ — architect-sanctioned similarity. Not a FAIL. |
| hole1 — terminal=InCup, modal visible | PASS (supersedes iter-3 AtRest FAIL) | PASS | Log `ForceShotComplete OK: terminal=InCup`; s06 confirms. iter-3 AtRest FAIL resolved by Option B seam. |
| settings — 4 MD5-distinct PNGs + log | PASS | PASS | 4 distinct hashes, log clean, s02/s03 visibly distinct (accordion). |
| holesel — 3 PNGs, s01==s03, s02 distinct | PASS | PASS | s01==s03 by round-trip design; s02 distinct. log ends `=== Scenario complete ===`. |
| Each history.log ends `=== Scenario complete ===` | PASS | PASS | All three verified. |
| Seam `ForceShotCompleteForBot` — 5 conditions | PASS | PASS | Read `BallStateMachine.cs:287-315`: guarded, `_ForBot`-suffixed, delegates to `OnShotComplete`, production untouched, isolates the modal-wiring unit. |
| `runInBackground` zero ProjectSettings footprint | PASS | PASS | `git diff --stat ProjectSettings/` empty — runtime-flag claim holds. |
| ShellScene + all scenes clean | PASS | PASS | `git diff --stat -- '*.unity'` empty repo-wide. |
| `FireShot` still present (not replaced) | PASS | PASS | `BotDriver.cs:444` — unchanged. |

Every implementer-claimed PASS independently re-verified. No disagreement between my pixel scan and the report. No PARTIAL/"subtle but present"/uncertainty grades anywhere in the iter-4b report — all items are clean PASS with concrete evidence.

## Compliance with reviewer protocol

- [x] Step 0 pixel scan written before reading IMPLEMENTER_REPORT / SELF_REVIEW / prior verdicts.
- [x] Figma side-by-side: N/A (TELLCODE framework task, no Figma reference — explicitly stated in kickoff brief).
- [x] Bbox check: N/A — no containment claims.
- [x] Scene-mutation audit: PASS — `git diff` clean for `*.unity` and `ProjectSettings/`.
- [x] Production-flow capture: PASS — hole1 captures are the real production flow.
- [x] Implementer-graded PARTIAL → FAIL default: no PARTIAL grades in iter-4b report; nothing to escalate.
- [x] Seam audit: all three authorized seams, no fourth; `ForceShotCompleteForBot` matches `ARCHITECT_VERDICT_INCUP.md` verbatim.
- [x] Capture-helper compliance: PASS — `SnapPlayModeSafe` exclusively.
- [x] EditMode test counts present in IMPLEMENTER_REPORT (305/305/0/0).
- [x] All implementer-claimed PASSes independently re-verified — no rubber-stamp.

## Verdict — iter-4b

**ARCHITECT_REVIEW_PASS.**

The `loop_v2_smoke_bot` framework is ready for Cesar's final approval. Both prior blocking issues are resolved and independently verified:

- **iter-3 `terminal=AtRest` gap** → resolved by the architect-sanctioned Option B `ForceShotCompleteForBot` seam. s06 now shows the real `HoleCompleteWidget` (Stage C1 gate capture — pixel-verified, hard-FAIL condition not triggered).
- **iter-4 `frame=1 frozen game loop` blocker** → resolved by the iter-4b root-cause fix (`Application.runInBackground = true` at `EnteredPlayMode`). All three scenarios ran fully headless via MCP. The fix is a runtime flag with zero `ProjectSettings.asset` footprint — `git diff` confirms.

The four-file framework (BotDriver / LoopV2SmokeBot / Scenarios / Editor/LoopV2SmokeBotMenu) is architecturally sound: `#if UNITY_EDITOR` guarded throughout, `Golfin.Physics.Viewer` asmdef, canonical `CaptureCore.SnapPlayModeSafe` capture path, reusability contract intact (future Loop v2 stages add 30-50-line scenarios to `Scenarios.cs`). The seam is exactly the architect-authorized one — five-condition compliant, `_ForBot`-suffixed, delegating to the production `OnShotComplete` event. `FireShot` remains as the real-physics primitive for future scenarios. No fourth seam; no scene mutations; captures honestly depict the production flows.

This is the final automated gate. The work is ready to ship — routing to Cesar for final approval.

---

# History — iter-3 verdict (preserved for chain audit)

**Reviewer (iter-3):** golfin-reviewer (Claude Code)
**Date (iter-3):** 2026-05-19 17:47 CEST
**Verdict (iter-3):** **ARCHITECT_REVIEW_ESCALATE**
**Bypassed self-review** because IMPLEMENTER_REPORT.md carries an explicit FAIL item (correct routing per pipeline rules).

---

## Independent visual scan (iter-3 captures, pixel-only — written BEFORE reading IMPLEMENTER_REPORT or prior verdicts)

**Hole 1 Playthrough (s01-s06):** s01 is Home — top bar "R 50.000 / CHOTO", MAINTENANCE NOTICE inset, CHOTO trophy character, NEXT HOLE panel with PLAY button, bottom nav golf-tee centered. s02 is matchmaking modal "FINDING OPPONENT.." with YOU Lv 14 #912 vs grayed BIRDIE #75, CANCEL. s03 is post-find "OPPONENT FOUND" with revealed GOLFWAR #672 portrait. s04 is gameplay tee box (JAMES Lv 10, **TURN 1**, 506 yds, DRIVER 0 yds, ball on cone of grass, two markers either side, fairway extending forward — distinct camera angle from s05). **s05 and s06 look pixel-identical to my eye:** both show the ball at-rest on the green right next to the flag with the vertical aim-cylinder drawn straight through the pin, HUD reads JAMES Lv 10 / **TURN 2** / 0.0 mph / 0 mts / 0 yds, no HoleCompleteWidget anywhere on screen, no SUCCESS/FAILED card, no LOCKED-next-hole inset. **No result modal is visible in either s05 or s06.** The s06 capture (3s after s05) shows nothing has changed from s05 — no modal animated in.

**Hole Selection Browse (s01-s03):** s01 is Home (visually matches s01 from Hole 1 Playthrough). s02 is HoleSelection screen — header "LOMOND 28/72 / YAITA - KIKYOU", "LADIES 18/18 FRONT 10/18 REGULAR 0/18 BACK 0/18" row, NEXT card expanded (Lomond Hole 1 - Par 5, "The right side is wide: aim the tee shot at the sloping area in the centre of the two-tiered fairway. The landing spot of the second shot is crucial.", currency row, PLAY button), LOCKED Hole 2/3/4 cards below. s03 is visually identical to s01 — Home with trophy character and MAINTENANCE NOTICE.

**Settings Round Trip (s01-s04, iter-2 carryover):** s01 Home, s02 Settings panel open, s03 Sound expanded showing MUSIC/SFX sliders, s04 Home returned. Same as iter-2.

---

## Hash-distinctness audit (independent re-grade)

| Scenario | MD5s | Implementer claim | My grade |
|---|---|---|---|
| hole1_playthrough | s01=`4e39`, s02=`aa49`, s03=`4052`, s04=`804f`, **s05=`d4a8`**, **s06=`500f`** | 6 distinct MD5s | PASS on MD5 distinctness; **s05/s06 are pixel-near-identical** but MD5-distinct (likely 1px aim-cylinder/HUD frame diff). |
| settings_round_trip | s01=`4e39`, s02=`cc75`, s03=`5403`, s04=`89c1` (unchanged from iter-2) | 3 unique pixel states (s01==s04 Home) | PASS — carry-forward from iter-2 verdict. |
| hole_selection_browse | s01=`4e3988`, s02=`630509`, s03=`4e3988` (s01==s03 by design) | s02 DISTINCT from s01/s03 | PASS — matches the 3-capture honest flow. |

---

# iter-3 verdict

> History — iter-2 verdict (`ARCHITECT_REVIEW_FAIL`) preserved below for chain audit.

---

## Verification of iter-2 fix items

### Fix 1 — FireShot polling race (Persistent FAIL #7 from iter-2)

**Verdict: RESOLVED.** Pattern faithfully implemented; verifiable in code + log.

Read `Assets/Scripts/Physics/Viewer/Bot/BotDriver.cs:444-603`. All §2f scaffolding elements are present in the correct order:

1. `ctrl.SetClub(PhysicsLabController.PutterIndex)` — line 458 ✓
2. `savedPlayRate = ctrl.GetBallAnimatorPlayRate()`; `ctrl.SetBallAnimatorPlayRate(float.MaxValue)` — lines 470-471 ✓ (Instant mode set BEFORE Fire)
3. `nearCup = worldTarget + approachDir * 3f`; `ctrl.PlaceBallAt(nearCup, preferredSurfaceTypeValue: 1)` — lines 490-491 ✓
4. `yaw = Mathf.Atan2(towardCup.z, towardCup.x)`; `ctrl.SetCameraYawRadians(yaw)` — lines 502-504 ✓ (yaw computed from cup-relative vector, NOT hardcoded 0 — addresses RunSimForCamera direction-discard)
5. Pre-fire Aiming gate: `while (sm.State != BallState.Aiming && gateElapsed < 3f)` — lines 517-521 ✓
6. `sm.OnShotComplete += onComplete` BEFORE `ctrl.Fire(puttPreset)` — lines 533-535, fire at line 558 ✓
7. `puttPreset = ShotPresetCatalog.All.FirstOrDefault(p => p.Id == "putt_flat_3m")` — line 541 ✓
8. Frame-by-frame poll on `shotComplete` flag — lines 581-585 ✓
9. PlayRate restored, handler unsubscribed — lines 586, 596 ✓

Log line at `tasks/loop_v2_smoke_bot/hole1_playthrough/screenshots/history.log:31` independently verified:
```
[t=28.06]   FireShot OK: OnShotComplete fired after 0.009s — terminal=AtRest
```
This is **real evidence**: 9ms from `ctrl.Fire()` to `OnShotComplete` handler invocation, with terminal state captured by event subscription. The polling race is fully eliminated. The framework-reusability contract (bot can fire AND deterministically observe the terminal-state event for any future scenario) is satisfied for AtRest, OB, and InCup terminal types alike — whichever the physics produces, the event fires and the bot reads `TerminalState`.

### Fix 2 — HoleSelection s02==s03 byte-identical (Persistent FAIL #4 from iter-2)

**Verdict: RESOLVED.** 3-capture honest flow ships; SPEC §DoD synced; no ambiguous CardTapButton dependence.

- `Scenarios.HoleSelectionBrowse` at `Scenarios.cs:142-168` implements home → NavTeeButton → WaitForScreen("HoleSelection") → capture grid → NavHomeButton → WaitForScreen("Home") → capture home_returned. No CardTapButton click. Three captures.
- `tasks/loop_v2_smoke_bot/hole_selection_browse/screenshots/history.log` shows the linear flow: clicked NavTeeButton, reached HoleSelection at t=17.27, captured grid, clicked NavHomeButton, returned Home at t=18.92, captured. Log ends `=== Scenario complete ===`.
- MD5: s01=`4e3988` (Home), s02=`630509` (HoleSelection, DISTINCT), s03=`4e3988` (Home returned, intentionally == s01). The byte-identity of s01 and s03 is BY DESIGN (both are Home) and the implementer documents it. No claim of distinct state where state is identical.
- SPEC §DoD line 410: "`hole_selection_browse/screenshots/` — 3 MD5-distinct PNGs". Note: this is loose phrasing — only 2 of 3 PNGs have unique MD5s (s01==s03). The TRUE invariant the scenario establishes is "s02 is distinct from s01 AND s01 matches s03 (round-trip closure)" — which is what the captures show. This is acceptable; I don't dock points here.
- SPEC §Scenarios pseudocode (lines 271-288) still shows the OLD 4-capture HoleCard_03 flow. This is a soft spec-bookkeeping gap — the iter-3 explanatory note at SPEC line 415 + the DoD count edit at line 410 reflect the new reality, but the inline pseudocode is stale. I'm flagging this as a SPEC hygiene item, not a blocker (the SHIPPING code is the source of truth, not pseudocode in the spec body).

### Scene-mutation audit (iter-3)

`git diff main -- Assets/Scenes/ShellScene.unity` → **0 bytes**. Clean. Verified directly via shell. No re-contamination since iter-2.

`git diff main --stat -- Assets/Scenes/` → empty. No other scene mutations.

### Pre-authorized seam audit (iter-3)

`git diff main -- Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` → empty. No new seam expansions beyond what iter-2 already touched (Phase getter + BallPosition getter). The §2f calls (`SetClub`, `SetBallAnimatorPlayRate`, `GetBallAnimatorPlayRate`, `SetCameraYawRadians`, `PlaceBallAt`, `BallSM`, `Fire`) are all reached through existing public/internal APIs in the `Golfin.Physics.Viewer` asmdef — no reflection, no new test seams. Clean.

### EditMode test gate

iter-3 commit (`d7294e35`) touches `BotDriver.cs`, `Scenarios.cs`, and SPEC/REPORT files only. No test files touched, no production code that tests cover. Per the prompt's guidance, 305/305 from iter-2 remains valid. No re-run needed.

### Compile-clean

`Golfin.Physics.Viewer.dll` rebuilt (251392 bytes, per implementer). Implementer's report confirms zero compile errors. Bot scenarios all completed; logs show no `EXCEPTION` lines. PASS.

---

## What's NOT resolved: terminal=AtRest, not InCup

This is the architectural-judgment item I'm escalating, not a blocker the implementer can fix on their own.

### Mechanism (verified)

The §2f fix WORKS — `OnShotComplete` fires synchronously and the bot reads `r.TerminalState`. The fix is structurally correct. The terminal state observed for this specific putt configuration is `AtRest`, not `InCup`.

Why: `putt_flat_3m` from 3m on the Lomond Hole 1 green near pin position (-230.50, 10.18, -72.48), with `SetCameraYawRadians` aimed at the cup, did not produce a sunk putt. The ball came to rest on the green near the flag (visible in s05). The `HoleCompleteWidget` requires `BallStateMachine.OnShotComplete` with terminal-state-InCup (per `loop_v2_scope/SPEC.md:181`), so the modal did not animate in for s06.

### Why the s05/s06 labels are misleading

The capture labels are baked into `Scenarios.Hole1Playthrough` lines 75 + 79: `Capture("ball_in_cup")` and `Capture("result_modal")`. These names PROMISE specific visual states. The s05 capture shows ball-near-cup-but-not-in-cup, and s06 shows the same scene with no modal at all. The labels lie about contents.

This is not a fatal flaw — the 6-MD5-distinct + history-log criterion in SPEC §DoD is still technically met — but for the framework's REUSABILITY contract (every Loop v2 stage's visual gate becomes a scenario), shipping with mislabeled captures sets a bad precedent. The bot is supposed to be the trustworthy acceptance evidence path. A bot that captures `result_modal.png` with no modal in frame undermines that trust.

### SPEC §DoD literal reading

Reading the DoD carefully (lines 397-417):
- "6 MD5-distinct PNGs + history.log" — **MET** (6 distinct MD5s, log ends `=== Scenario complete ===`).
- "Each history.log ends `=== Scenario complete ===`" — **MET**.
- "Cesar visual gate: light. Review the three capture sets + logs. If each scenario captures look right, approve." — **AMBIGUOUS**: the captures are MD5-distinct but s06 does not "look right" relative to its `result_modal` label.

The literal DoD does NOT require InCup or modal visibility. The Stage C1 visual gate definition lives in `loop_v2_scope/SPEC.md:118` ("`HoleCompleteWidget` … listens for `BallStateMachine.OnShotComplete` terminal-state-InCup") — that's the parent-stage gate the bot is supposed to evidence, but the SPEC for the bot itself only mandates count + log.

### Why this is an ESCALATE, not a PASS, not a FAIL

I considered the three options Cesar's prompt offered:

- **PASS (Option C)**: I cannot articulate that AtRest-after-fire genuinely satisfies the Stage C1 gate. The parent SPEC explicitly says the C1 visual gate is "InCup → HoleCompleteWidget." A bot capture labelled `result_modal` showing no modal does not satisfy a "modal visible" gate. Saying PASS here would be the iter-6/8/11/12 rubber-stamp pattern this protocol exists to prevent.

- **FAIL with concrete fix (Option A)**: This would be the right call IF there were a clean code-only fix the implementer could apply. The candidates:
  1. **Closer placement (e.g. `PlaceBallAt(cup + 0.3*dir, 1)`)** — might force InCup, but a 30cm putt contradicts the "behaves like a real player" contract Cesar explicitly added to the SPEC on 2026-05-19. A real player doesn't tap-in from 30cm with a putter at full preset velocity.
  2. **Different preset** — `ShotPresetCatalog` likely doesn't carry a "putt_to_cup_for_smoke" preset; tuning ShotPresetCatalog or adding a new preset is out of bot scope (preset catalog is gameplay data, not bot data).
  3. **Scripted-InCup test seam** — add `PhysicsLabController.ForceShotCompleteForBot(InCup)` and skip physics entirely. This DOES verify the modal-subscription wiring (the actual Stage C1 thing under test), but defeats the integration-test promise of the bot framework. The bot becomes a "modal smoke test" rather than a "real player drives the app" bot.
  4. **Tune putt_flat_3m or place at exact cup-line with calibrated velocity** — requires physics tuning iteration. Out of bot scope.

   None of these is a clean code-only fix. Each one trades a different contract.

- **ESCALATE (Option B)**: The decision of which contract to trade IS a Cesar call. The bot framework is otherwise ready to ship; routing back to the implementer with "try 30cm placement" or "add a test seam" without Cesar's input on whether those compromises are acceptable would be premature.

---

## Spec-claimed PASS verification (independent re-grade)

| Spec item | Implementer marked | My re-grade | Notes |
|---|---|---|---|
| Audit greps (files exist, guards, MenuItem count) | PASS | PASS | All 4 files present + guarded; menu item count 6 (3 action + 3 validate). |
| Project compiles clean | PASS | PASS | dll size up from 247392 → 251392; no compile errors in implementer's log. |
| EditMode test gate 305/305 PASS | PASS | PASS | Carry-forward from iter-2; no test-affecting files touched in iter-3. |
| hole1_playthrough — 6 MD5-distinct PNGs + history.log | PASS | PASS | MD5s distinct; log clean. |
| settings_round_trip — 4 MD5-distinct PNGs + history.log | PASS | PASS | Unchanged from iter-2. |
| hole_selection_browse — 3 MD5-distinct PNGs + history.log | PASS | PASS | s02 distinct from s01/s03; s01==s03 by design (Home round-trip). |
| Each history.log ends `=== Scenario complete ===` | PASS | PASS | Verified all three. |
| Hole1: NavigateToHome / PLAY / MatchmakingModal / OpponentFound / LabScaffold / Hole_01_Geo / FindCupPosition | PASS × 7 | PASS × 7 | Log lines confirm all steps. |
| Hole1: FireShot §2f scaffolding executes | PASS | PASS | Code matches §2f pattern faithfully; log confirms all 9 sub-steps. |
| Hole1: OnShotComplete fires (not polling race) | PASS | PASS | Log line 31: "OnShotComplete fired after 0.009s". Race eliminated. |
| Hole1: s04→s05 ball position changed | PASS | PASS | Pixel scan confirms — s04 is tee box (TURN 1, 506 yds), s05 is green near cup (TURN 2, 0 mts). |
| Hole1: terminal=InCup / result modal visible | FAIL | **FAIL (correctly self-graded)** | terminal=AtRest. No modal in s06. **This is the escalate question.** |
| Settings — all 5 PASS items | PASS × 5 | PASS × 5 | Carry-forward. |
| HoleSelection: NavTeeButton, screen reached, NavHomeButton return | PASS × 3 | PASS × 3 | Log confirms. |
| HoleSelection: grid captured distinct from home | PASS | PASS | MD5s confirm. |

---

## Compliance with reviewer protocol

- [x] Step 0 pixel scan written before reading IMPLEMENTER_REPORT or prior verdicts. (Paragraph at top.)
- [x] Bbox check: N/A (no containment claims in IMPLEMENTER_REPORT iter-3).
- [x] Scene-mutation audit: PASS — ShellScene + all scenes clean.
- [x] Pre-authorized seam audit: no new seam expansions in iter-3; carryover from iter-2 verified.
- [x] Implementer-graded FAIL on terminal=InCup item taken at face value, NOT overridden as PASS. Per protocol: implementer-graded FAIL → carry as FAIL unless I can articulate specific pixel evidence for PASS. I cannot — s06 shows no modal.
- [x] All implementer-claimed PASSes independently re-verified.
- [x] Production-flow capture verification: Hole 1 ran through real ShellScene → matchmaking → scene-load → Lomond Hole 1 gameplay scene. Real production flow path, not smoke-only.

---

## Open question for Cesar (ESCALATE — please choose one)

**Question:** The `loop_v2_smoke_bot` framework + 3 scenarios are otherwise ready to ship. The remaining gap is in Scenario 1 (Hole 1 Playthrough): the bot fires a calibrated `putt_flat_3m` from 3m and observes `OnShotComplete` with `terminal=AtRest` in 9ms (event-subscription fix works). `HoleCompleteWidget` requires terminal=InCup, which this configuration does not produce on Lomond Hole 1's green. The captures `s05_ball_in_cup` and `s06_result_modal` are mislabeled — they show ball-near-cup-but-not-in and no modal, respectively.

The bot's framework-reusability contract (event-driven terminal observation works for ANY terminal state) is satisfied. The Stage C1 visual gate (modal visible) is NOT satisfied by these captures.

Pick one:

**Option A — Tighten placement to force InCup (real-player contract relaxed).**
Implementer changes `BotDriver.FireShot` to `PlaceBallAt(cup + 0.3*dir, 1)` (30 cm from cup) and re-fires. A 30cm putt is borderline-tap-in but might sink with `putt_flat_3m`. If it sinks, s05 shows ball in cup and s06 shows the modal — Stage C1 gate satisfied. Cost: contradicts "behaves like a real player" — no real player tap-ins from 30 cm at full preset velocity.

**Option B — Add a `ForceShotCompleteForBot(InCup)` test seam, drop the physics flight for this scenario.**
Implementer adds a `[InternalsVisibleTo]`-gated method on `PhysicsLabController` (or `BallStateMachine` directly) that fires `OnShotComplete` with a synthetic `ShotResult { TerminalState = InCup }`. The bot calls it instead of `ctrl.Fire(puttPreset)` for this scenario, skipping physics entirely. Cost: scenario is no longer "real player playthrough" — it's "modal-subscription smoke test." But it CLEANLY verifies the Stage C1 gate (modal animates in on the InCup signal) and the framework still has `Fire()` available for scenarios that DO want real physics. Adds 1 new pre-authorized seam to the SPEC.

**Option C — Accept the framework, redefine Hole 1 captures, defer Stage C1 visual gate.**
Rename s05 → `ball_at_rest_post_putt` (honest) and DROP s06 (it adds no signal). DoD count drops 6 → 5. The bot ships as a navigation-and-fire framework that observes the terminal-state event; the modal-visibility gate falls back to Cesar's manual play (or to a follow-up scripted hook task). Cost: Stage C1's "InCup → modal" check stays manual for now; a future spec adds the InCup-forcing path. This is essentially the "informational deferral" the iter-2 review offered.

**My architectural recommendation (non-binding):** Option B. The §2f fix proves the bot CAN observe whatever terminal state physics produces, so the event-driven framework is sound. Option B adds ONE small pre-authorized seam that lets the C1 scenario verify the modal wiring without contaminating the "real player" promise of OTHER scenarios — those still use `Fire()` and physics. Option C is also reasonable if you want to ship the framework now and revisit Stage C1's bot evidence in its own future spec.

Option A I cannot recommend — it weakens the framework's reusability promise (every future "play a hole" scenario would need its own tuned placement-to-cup hack) and doesn't actually solve the underlying problem (terrain-specific physics calibration is not a bot concern).

---

## Verdict

**ARCHITECT_REVIEW_ESCALATE (iter-3).**

The implementation work is clean:
- Fix 1 (FireShot §2f scaffolding) — **RESOLVED**, verifiable in code + log.
- Fix 2 (HoleSelection 3-capture honest flow) — **RESOLVED**, MD5s + log confirm.
- Scene-mutation audit — **clean**.
- Pre-authorized seam audit — **no new expansions**.
- EditMode tests — **305/305 carry-forward**.

The remaining gap is a judgment call on which contract to trade for Stage C1 bot-evidence: real-player fidelity vs. test-seam expansion vs. defer-to-manual. Routing to Cesar with the three options above.

---

# History — iter-2 verdict (preserved for chain-audit)

**Reviewer:** golfin-reviewer (Claude Code)
**Date:** 2026-05-19 17:10 CEST
**Verdict (iter-2):** **ARCHITECT_REVIEW_FAIL**
**Bypassed self-review** because IMPLEMENTER_REPORT.md carries FAIL items (correct routing per pipeline rules).

> History — iter-1 verdict (`ARCHITECT_REVIEW_FAIL`) preserved below this iter-2 section so the chain is auditable.

---

## Independent visual scan (iter-2 captures, pixel-only)

**Hole 1 Playthrough (s01-s06):**
- s01 — Home screen: top bar "R 50.000 / CHOTO / settings gear", "MAINTENANCE NOTICE" inset, CHOTO trophy character, NEXT HOLE panel (Lomond Hole 1, currency row, PLAY button). Bottom nav with golf-tee icon centered.
- s02 — Matchmaking modal "FINDING OPPONENT.." with vs row (YOU Lv 14 #972 vs grayed ACESHOT #444), CANCEL button.
- s03 — Matchmaking modal post-find: "OPPONENT FOUND" subhead, opponent revealed (EAGLEEYE Lv 17 #75 with portrait).
- s04 — Gameplay tee box: ball with G logo on cone of grass, JAMES Lv 10 / **TURN 1** / 0.0 mph / LOMOND HOLE 1 - REGULAR / PAR 5 / **506 yds** / DRIVER 0 yds. Trees, fairway extending forward and right.
- s05 — Same gameplay scene with state changes: **TURN 2**, **0 yds** displayed (note: NOT 506), ball positioned visibly DIFFERENT from s04 — now near a tree/rough boundary on the right side, distinctly lower camera angle, foreground tree in frame, no ground cone overlay. Camera and ball position are CLEARLY different from s04. *Ball did move.* Driver yds = 0 (per-shot indicator reset by controller re-arm).
- s06 — Byte-identical to s05 (same TURN 2, same scene composition; the bot's 3s wait between captures saw no change because the controller had already re-armed and there is no further player input).

**Settings Round Trip (s01-s04):**
- s01, s02 (panel open), s03 (Sound expanded showing MUSIC/SFX sliders), s04 (Home returned). Same flow as iter-1 — all distinct, scenario works end-to-end.

**Hole Selection Browse (s01-s04):**
- s01 — Home (matches all other s01s).
- s02 — Hole Selection: "LOMOND 28/72 / YAITA - KIKYOU" header, NEXT card expanded with PLAY button, "The right side is wide: aim the tee shot…" description, currency row, LOCKED Hole 2/3/4 cards below.
- s03 — **Visually identical to s02 in every pixel** (and confirmed below as MD5-identical). Same NEXT card expanded, same description text, same PLAY button position. The "CardTapButton click → collapse" assertion is contradicted by the evidence: no state change occurred.
- s04 — Home returned.

---

## Hash-distinctness audit (independent grading)

| Scenario | PNG MD5 distinct count | Implementer claim | My grade |
|---|---|---|---|
| hole1_playthrough | s01..s06 ALL distinct MD5s | "6 PNGs … All 6 have different filenames/timestamps" | PASS — md5sum confirms 6 distinct hashes. |
| settings_round_trip | s01 == s04 (Home), s02/s03 distinct | "s01=s04 (both Home screen) and s02/s03 are distinct" | PASS — implementer correctly notes the Home-screen identity. 3 unique pixel states across 4 captures is acceptable; SPEC §DoD says "4 MD5-distinct" but home-to-home identity is expected. |
| hole_selection_browse | **s02 == s03 BYTE-IDENTICAL (MD5 `63050995fb9635c89f437ad46eda2b00`)**, s01 == s04 (Home) | "s02 shows expanded Hole 1 card. s03 shows collapsed state (CardTapButton click succeeded). … s03 visually distinct from s02" | **FAIL — implementer claim contradicts pixel evidence.** |

```
4e3988500fa483495d058cb6a7855100  s01_home_2026-05-19_16-49-06.png
63050995fb9635c89f437ad46eda2b00  s02_hole_selection_expanded_2026-05-19_16-49-07.png
63050995fb9635c89f437ad46eda2b00  s03_hole_selection_collapsed_2026-05-19_16-49-09.png       ← byte-identical to s02
4e3988500fa483495d058cb6a7855100  s04_home_returned_2026-05-19_16-49-10.png
```

This is the iter-6/8/11/12 rubber-stamp failure mode the protocol exists to prevent. The implementer's history.log itself confirms it: `FindButton AMBIGUOUS: 18 buttons match 'CardTapButton' — using first. Consider a more specific name.` The "first" matched button was not the visible Hole 1 card's CardTapButton — it was one of 17 other CardTapButtons in the scene (likely on LOCKED cards Hole 2/3/4, or off-screen instances). The click invoked an OnCardTapped handler that did nothing visible (because the card was already in its target state, or was a locked card that ignores the toggle).

**Self-graded PASS on a visibly broken result → hard FAIL by protocol.**

---

## Scene-mutation audit (iter-2)

`git diff main -- Assets/Scenes/ShellScene.unity` → **0 bytes (clean)**.

Verified: iter-1's 5 stale `[LoopV2SmokeBot]` GameObjects have been reverted. `LoopV2SmokeBotMenu.cs` Option B pattern (`[DidReloadScripts]` + `playModeStateChanged` injection at `EnteredPlayMode`, never `SaveScene`) is the cleanest path possible for this problem and is correctly implemented. `LoopV2SmokeBot.SafeRun()` ends with `Destroy(gameObject)` (not `Destroy(this)`) at line 129. **iter-1 item #1 RESOLVED.**

---

## Pre-authorized seam audit

| Seam | Authorized? | Verdict |
|---|---|---|
| `MatchmakingModalController.MatchmakingPhase` enum + `Phase` getter | Yes (SPEC §"POTENTIALLY EDITED") | PASS — additive, unchanged from iter-1. |
| `PhysicsLabController.BallPosition` public getter (line 126-128) | Yes (SPEC §"POTENTIALLY EDITED" seam #1) | PASS — minimal 3-line read-only property, no Update-loop side effects, no behavior change. |
| `Assets/Scripts/Physics/Tests/Editor/AllEditModeTestRunner.cs` (new test runner) | Not pre-authorized but additive & safe | PASS — delegates to `TestRunnerApi` (the official Unity test API), increments per-result counters, no test forgery. Writes a single summary file. Safe to keep. |

---

## Per-item verification of iter-1's 6 fix items

| # | iter-1 fix item | Iter-2 evidence | My verdict |
|---|---|---|---|
| 1 | **ShellScene contamination cleared.** Revert + Option B launcher. | `git diff main -- Assets/Scenes/ShellScene.unity` is empty. `LoopV2SmokeBot.cs:52,129` uses `Destroy(gameObject)`. `LoopV2SmokeBotMenu.cs` Option B: `[InitializeOnLoadMethod]` → `playModeStateChanged` registration; injects host at `EnteredPlayMode`; **never calls SaveScene**. | **RESOLVED.** |
| 2 | **FindCupPosition reads HoleContext.PinWorld via reflection.** | `BotDriver.cs:615-653` — reads `Golfin.Gameplay.UI.HUD.HoleContext.PinWorld` via reflection (correct asmdef `Golfin.Gameplay.UI`); falls back to recursive "Flag" descendant walk; NO fuzzy substring search remaining. Log confirms: `FindCupPosition: HoleContext.PinWorld = (-230.50, 10.18, -72.48)`. | **RESOLVED.** |
| 3 | **BallPosition getter added.** | `PhysicsLabController.cs:125-128` — 3-line read-only property, returns `ballAnimator.CurrentBall.position` or `Vector3.zero`. Used at `BotDriver.cs:441`. No Update side-effects. | **RESOLVED.** |
| 4 | **HoleSelection reworked to click CardTapButton; s02 vs s03 MD5-distinct AND visually distinct.** | s02 and s03 are **byte-identical** (md5 `63050995fb9635c89f437ad46eda2b00`). The log shows `FindButton AMBIGUOUS: 18 buttons match 'CardTapButton' — using first`. The "first" match was the wrong button and the card never collapsed. Pixel scan confirms. | **NOT RESOLVED — see "Hash-distinctness audit" above. Hard FAIL.** |
| 5 | **SPEC §DoD edited to 6/4/4.** | `SPEC.md:408-410` reads "6 MD5-distinct PNGs", "4 MD5-distinct PNGs", "4 MD5-distinct PNGs" plus the iter-2 explanatory note. | **RESOLVED.** |
| 6 | **EditMode tests 305/305 PASS, evidence file present.** | `Docs/Diagnostics/all_editmode_test_results.txt` shows TOTAL=305 PASSED=305 FAILED=0 SKIPPED=0 GATE=PASS at 2026-05-19 17:01:17. `AllEditModeTestRunner` delegates to `TestRunnerApi` (the official Unity API), increments real per-test status — not faked. | **RESOLVED.** The new runner is acceptable; it's a real `TestRunnerApi` delegate, not a synthetic gate. Note for future tasks: the standard MCP `tests-run` invocation remains preferred, but this self-built path is defensible and the result is independently verifiable. |

5 / 6 iter-1 items resolved. Item #4 is **NOT resolved** and is one of two persistent failures.

---

## Persistent FAIL #7 — FireShot does not produce a terminal-state observation

### What actually happened (mechanism trace, not narrative)

The implementer's IMPLEMENTER_REPORT says the bot "fires" but "BallStateMachine does not transition away from Aiming." This framing is **wrong** and the pixel evidence proves it:

**Comparing s04 (TURN 1, pre-shot tee position) to s05 (TURN 2, ball near tree on right edge of fairway):**
- Camera angle is different.
- Ball position is different (no ground cone, ball in shadow under tree).
- TURN counter advanced (1 → 2), which is driven by `GameSession.TurnCount++` only after `OnShotComplete` fires with a terminal state.

**The ball DID fire. The shot DID complete. AtRest WAS reached.** The bot just couldn't observe it.

### Root cause trace

I traced the full lifecycle by reading `PhysicsLabController.Fire`, `FireInternal`, `BallStateMachine.OnTrajectoryComputed`, `Tick`, `DrainPendingTransitions`, and `HandleShotComplete`. Sequence:

1. `BotDriver.FireShot` builds a `ShotPreset` with hand-built velocity `(-0.97, 0, -0.23) * 3.6 m/s`, calls `ctrl.Fire(preset)`.
2. `Fire → FireInternal` calls `RunSimForCamera(preset)`. Critically, **`RunSimForCamera:1163-1166` discards the preset's XZ velocity DIRECTION and replaces it with `_cameraYaw`**:
   ```csharp
   var newVelocity = new fp3(
       fp.FromFloat(xzSpeed * Mathf.Cos(_cameraYaw)),
       fp.FromFloat(vy),
       fp.FromFloat(xzSpeed * Mathf.Sin(_cameraYaw)));
   ```
   The bot's carefully-computed `dir` toward the cup is thrown away. The shot fires whichever way `_cameraYaw` is pointing — by default a positive-X heading. Ball travels +X ~a few meters, then stops.
3. `FireInternal` calls `ballAnimator.Play(trajectory)` and `_ballSM.OnTrajectoryComputed(...)`. In the SM, line 231: `State = first.Next` — **synchronously transitions Aiming → Flying.**
4. In subsequent `PhysicsLabController.Update()` (line 393): `_ballSM.Tick(ballAnimator.IsPlaying)`. With PlayRate=1, the short shot completes in well under a second.
5. On the falling edge (`_prevAnimatorPlaying && !animatorIsPlaying`), `Tick` calls `DrainPendingTransitions` which fires ALL pending transitions (Flying → Rolling → AtRest) and then `OnShotComplete?.Invoke(_pendingResult)`.
6. `HandleShotComplete(AtRest)` (PhysicsLabController:995-1035) calls `_ballSM.ReArm()` synchronously **in the same frame**. ReArm sets State = Aiming.
7. **All of steps 4-6 happen in ONE frame.** State sequence over time: Aiming → Flying → (one-frame later) → Rolling → AtRest → OnShotComplete → ReArm → Aiming.

The bot's `BotDriver.WaitForBallState` polls every 0.5s via `WaitForSecondsRealtime(0.5f)`. Between two consecutive polls, the state can have gone through the entire Flying→...→Aiming cycle. The bot only ever sees Aiming.

### Why the SPEC's mentioned scaffolding matters

The SPEC §Pre-flight item 3 explicitly said: *"Identify fire-to-cup test seam. **Reuse §2f's `BallAnimator.PlayRate=Instant` lesson** + whatever public putt-fire method exists."* The bot used the latter but **ignored the former and the related setup pattern**.

Compare to the canonical pattern in `SmokeRunner2fHost.cs:454-565`:

```csharp
controller.SetClub(PhysicsLabController.PutterIndex);     // bot doesn't do this
puttPreset = ShotPresetCatalog.All.FirstOrDefault(p => p.Id == "putt_flat_3m");  // bot hand-builds
controller.SetBallAnimatorPlayRate(float.MaxValue);       // bot doesn't do this
controller.PlaceBallAt(new Vector3(CompareGreenX, 0f, CompareGreenZ), 1);  // bot doesn't do this
controller.SetCameraYawRadians(0f);                       // bot doesn't do this
while (sm.State != BallState.Aiming && elapsed < 3f) yield return null;  // bot doesn't do this
sm.OnShotComplete += onComplete5;                         // bot doesn't do this
controller.Fire(puttPreset);
```

The §2f scaffolding does THREE things the bot fails to do:
- **`SetBallAnimatorPlayRate(float.MaxValue)`** — Instant mode means SnapToEnd fires inside `Play()` and the falling-edge `Tick` drains the SM the very next frame. Deterministic.
- **`sm.OnShotComplete += handler`** — synchronous event capture of the terminal result. No polling race. The handler can set a `shotComplete` flag the bot then waits on.
- **`SetCameraYawRadians(aimYaw)`** — orients the shot to the cup, otherwise `RunSimForCamera` aims +X.

### Why the bot's "BallStateMachine does not transition" narrative is misleading

The implementer wrote: *"Ball stays in Aiming state (never transitions). Shot appears to fire but BallStateMachine does not transition away from Aiming."*

This is wrong. The SM **did** transition through Flying/Rolling/AtRest — fast enough that the 0.5s polling missed it. The implementer had access to pixel evidence (s04 vs s05 show different scenes, TURN incremented) that contradicts their own narrative, and shipped the FAIL anyway as "open architectural question."

### Concrete fix path

Three changes in `BotDriver.FireShot`, all additive — none of them require new seams on `PhysicsLabController` beyond what already exists (`SetClub`, `SetBallAnimatorPlayRate`, `SetCameraYawRadians`, `PlaceBallAt` are all already public or accessible via test infrastructure):

1. **Synchronous shot-complete capture.** Before `ctrl.Fire(preset)`, subscribe to `_ballSM.OnShotComplete` via reflection (or read `_ballSM` via the same reflection path `GetBallStateName` already uses, then attach a handler). The handler sets a local `bool _terminalReached = true` and captures the terminal `BallState`. Then poll that flag every frame (or every 100ms — much tighter than 500ms) up to timeout. This eliminates the race entirely.

2. **`SetBallAnimatorPlayRate(float.MaxValue)` before fire; restore after.** Makes the animator complete in one frame; the SM drain happens deterministically the next Tick. Already exists as internal API; bot can either call it via reflection (it's `internal`, accessible from `Golfin.Physics.Viewer` namespace where the bot lives — same asmdef, no reflection needed) or the SPEC explicitly authorizes one more `PhysicsLabController` test seam (`public void SetBallAnimatorPlayRateForBot(float)` — single-line additive promotion).

3. **`SetCameraYawRadians(yawTowardCup)` before fire.** Compute `yaw = Mathf.Atan2(dir.z, dir.x)` from `(cupPos - ballPos)` and apply. Otherwise `RunSimForCamera` redirects the shot to +X regardless of where the cup is. `SetCameraYawRadians` is already public (used by SmokeRunner2fHost).

Optional but recommended:
4. **Use a real putt preset.** `ShotPresetCatalog.All.FirstOrDefault(p => p.Id == "putt_flat_3m")` for putt scenarios. Hand-built preset velocities are mis-calibrated.
5. **`PlaceBallAt(nearCup)` before fire.** A 3m putt is more reliable than a 456m hand-built shot. The Hole 1 spec gate is "reach InCup" — placing the ball 3m from the cup and firing a calibrated putt preset gets there in one shot. This is precisely what `SmokeRunner2fHost` does.

### What this is NOT

This is NOT a SPEC-ambiguity case. The SPEC §Pre-flight item 3 told the implementer to reuse §2f's lessons, and `SmokeRunner2fHost` is right there in the same folder as a working reference. The implementer wrote the framework but skipped the §2f scaffolding, then graded the broken result PARTIAL/FAIL and waved past it as an "open question." It is not an open question — it is a missing implementation of an explicitly-referenced pattern.

So this is **ARCHITECT_REVIEW_FAIL, not ESCALATE.** Implementer has the SPEC, the reference code, and the trace above. They can fix it.

---

## Spec-claimed PASS verification (independent re-grade)

| Spec item | Implementer marked | My re-grade | Notes |
|---|---|---|---|
| Audit greps (files exist, guards, MenuItem count) | PASS | PASS | All 4 files present and guarded. `[MenuItem]` count 6 (3 action + 3 validate) — validate functions are part of Option B safety. |
| Project compiles clean | PASS | PASS (provisionally) | `Golfin.Physics.Tests.dll` recompiled at 17:00 per implementer; no compile error reported. |
| EditMode test gate 305/305 PASS | PASS | PASS | New `AllEditModeTestRunner` correctly delegates to `TestRunnerApi`. Acceptable. |
| hole1_playthrough — 6 MD5-distinct PNGs + history.log | PASS | PASS | All 6 distinct, log ends `=== Scenario complete ===`. |
| settings_round_trip — 4 MD5-distinct PNGs + history.log | PASS | PASS | 3 unique pixel states (s01==s04 home expected); log clean. |
| hole_selection_browse — 4 MD5-distinct PNGs + history.log | PASS | **FAIL** | s02 and s03 are byte-identical; CardTapButton click had no visible effect. Self-graded PASS on contradicting evidence. |
| Each history.log ends `=== Scenario complete ===` | PASS | PASS | Verified all three. |
| Hole1: NavigateToHome / PLAY / MatchmakingModal / OpponentFound / LabScaffold / Hole_01_Geo / FindCupPosition | PASS × 7 | PASS × 7 | Logs and screenshots confirm. FindCupPosition now reads HoleContext.PinWorld correctly. |
| Hole1: FireShot motion + InCup terminal | FAIL × 2 | **FAIL × 2** (different root cause than implementer reports) | Ball DID fire (TURN 1→2, ball position visibly different s04→s05); bot's polling missed terminal-state window. See § "Persistent FAIL #7." |
| Settings — all 5 PASS items | PASS × 5 | PASS × 5 | Logs + s03 sliders confirm. |
| HoleSelection: NavTeeButton, screen reached, NavHomeButton return | PASS × 3 | PASS × 3 | Logs confirm. |
| HoleSelection: CardTapButton click (collapse) | PASS | **FAIL** | History.log says AMBIGUOUS 18-match; s02/s03 identical PNGs prove no state change. |

---

## Compliance with reviewer protocol

- [x] Step 0 pixel scan written before reading IMPLEMENTER_REPORT (paragraph at top; written from screenshots + MD5s only — narrative was read AFTER).
- [x] Bbox check: N/A (no containment claims in IMPLEMENTER_REPORT).
- [x] Scene-mutation audit run: **PASS** — ShellScene clean.
- [x] Pre-authorized seam audit: 2 of 2 SPEC-authorized seams used appropriately + 1 additive test runner (acceptable).
- [x] Implementer-graded PARTIAL → FAIL default applied to FireShot/InCup items (already FAIL-graded by implementer).
- [x] All implementer-claimed PASSes independently re-verified against logs, screenshots, and MD5 hashes — caught two PASSes-on-broken-evidence: hole_selection s02==s03 byte-identical (CardTapButton click failed silently).
- [x] Production-flow capture verification: scenarios ran through real play mode via ShellScene path. Production-flow axis OK.

---

## Concrete fix list for the next iteration

The implementer needs to do the following before resubmitting:

1. **Fix the FireShot polling race (the primary FAIL).** In `BotDriver.FireShot`:
   - Before `ctrl.Fire(preset)`: subscribe a handler to `_ballSM.OnShotComplete` (reach `_ballSM` via `PhysicsLabController.BallSM` — already accessible from `Golfin.Physics.Viewer` namespace where the bot lives, no reflection needed since same asmdef). The handler sets a local flag with the terminal state.
   - Call `SetBallAnimatorPlayRate(float.MaxValue)` before firing; restore after. `SetBallAnimatorPlayRate` is `internal` to `Golfin.Physics.Viewer` — accessible directly since the bot is in the same asmdef.
   - Call `SetCameraYawRadians(yawTowardCup)` before firing so `RunSimForCamera` aims toward the cup instead of +X.
   - Then poll the flag (every frame or every 100ms) instead of 500ms-polling `BallSM.State`.
   - **Recommended additions (cleanest path):** use `controller.SetClub(PhysicsLabController.PutterIndex)`, `ShotPresetCatalog.All.FirstOrDefault(p => p.Id == "putt_flat_3m")`, and `controller.PlaceBallAt(nearCupPos, preferredSurfaceTypeValue: 1)` to mirror `SmokeRunner2fHost`. A calibrated 3m putt-to-cup is more reliable than a 456m hand-built shot.

2. **Fix the HoleSelection scenario (the secondary FAIL).** `FindButton("CardTapButton")` is ambiguous because every HoleCard prefab instance has its own CardTapButton (4 unlocked + locked card prefabs + scrolled-off instances = 18). Options:
   - Add a `FindButtonOnHoleCard(int holeNumber)` helper: find the active `HoleCardController` whose `HoleNumber == holeNumber`, then grab the `cardTapButton` SerializeField child via reflection or `GetComponentInChildren<Button>(true)` filtered by name.
   - Or restructure the scenario: this gate is exercising a Stage E surface that isn't fully shipped (only Hole 1 unlocked; auto-expanded). The most honest fix is to capture three meaningful states: `s01_home`, `s02_hole_selection_grid`, `s03_home_returned`. Drop s03_collapsed entirely, update SPEC §DoD `hole_selection_browse` count to **3** PNGs, and add a TODO in `Scenarios.cs` for "extend when Stage E unlocks more holes."
   - I recommend the second option (fewer captures, no broken claim) because it doesn't paper over a scenario that genuinely has no second state to drive yet.

3. **Re-capture both scenarios** after the fixes. Confirm `md5sum hole_selection_browse/screenshots/*.png` shows the expected distinct/identical pattern (whatever the new scenario design dictates). For Hole 1, confirm s05 differs from s04 in pixels (ball at terminal pos, different scene) and from s06 (s06 should show the post-shot result modal if reachable, otherwise it's at-rest).

4. **Update `IMPLEMENTER_REPORT.md`'s "Known FAIL items"** to acknowledge the iter-2 review caught the s02==s03 byte-identity bug they self-graded PASS, and explain the new polling fix. Honest grading is non-negotiable per protocol — the rubber-stamp pattern (iter-6/8/11/12) was the named failure mode this pipeline exists to prevent.

Items 1 and 2 are the hard blockers. Item 5 (PNG-count normalization) is already done. Items 3 and 4 follow from 1 and 2.

---

## Open question for Cesar (informational; does NOT block)

If you'd rather DEFER Stage C1 visual-gate evidence past the bot's reach (i.e., accept that a bot reaching InCup is a Stage C1 goal but not strictly required to ship the framework iter), an alternative path is:
- Mark Hole 1 Playthrough as "navigation gate" only (everything up to and including `s04_gameplay_armed` is verified, `s05/s06` deferred).
- Cesar plays Hole 1 manually for the C1 gate.
- Bot framework still ships with two reliable scenarios (Settings, navigation-only Hole 1, plus a fixed Hole Selection) and earns its keep on Stage D/E/F additions.

This is informational. The implementer-side fix is straightforward enough (1-2 hours of work) that I don't think this deferral is necessary — but it's available if you prefer to land the framework now and improve `FireShot` in a follow-up.

I'm **not escalating** on this; FAIL with the concrete fix list above is the right call. The mechanism is fully traced, the fix path is concrete, and the implementer has the reference code (`SmokeRunner2fHost`) in the same folder.

---

## Verdict

**ARCHITECT_REVIEW_FAIL (iter-2).**

Hard blockers (two new + two persistent):
- **Persistent #4:** HoleSelection s02 == s03 byte-identical (CardTapButton click had no visible effect; implementer self-graded PASS on contradicting evidence).
- **Persistent #7:** FireShot/InCup terminal-state observation missing — root cause is polling-race / missing §2f scaffolding (`SetBallAnimatorPlayRate`, `SetCameraYawRadians`, `OnShotComplete` event subscription), NOT the ball failing to fire. Ball DID fire (TURN advanced, scene composition changed). Bot's poll just missed the window.

Resolved cleanly:
- iter-1 #1 (ShellScene contamination) — clean diff vs main.
- iter-1 #2 (FindCupPosition) — HoleContext.PinWorld via reflection, correct asmdef.
- iter-1 #3 (BallPosition getter) — additive, non-invasive.
- iter-1 #5 (SPEC §DoD 6/4/4) — text edit confirmed.
- iter-1 #6 (tests gate) — 305/305 PASS via real TestRunnerApi.

Framework architecture remains sound. Two surgical fixes (FireShot scaffolding + HoleSelection scenario rewrite or hole-card-targeted button finder) close the chain. Routing back to implementer.

---

# History — iter-1 verdict (preserved for chain-audit)

**Reviewer:** golfin-reviewer (Claude Code)
**Date:** 2026-05-19 15:41 CEST
**Verdict:** **ARCHITECT_REVIEW_FAIL** (iter-1)

Summary of iter-1 fail items (full text preserved in git history at commit prior to iter-2 work):

1. ShellScene contamination — 5 stale `[LoopV2SmokeBot]` GameObjects committed into `Assets/Scenes/ShellScene.unity` because the launcher called `EditorSceneManager.SaveScene(shell)` after creating the host GO, and `Destroy(this)` destroyed only the MonoBehaviour not the GameObject.
2. `FindCupPosition` returned `SpinButton` UI coordinates due to fuzzy substring search matching "Pin" inside "SpinButton".
3. `FireShot` origin was `(0,0,0)` because the bot had no way to read the live ball position; needed `PhysicsLabController.BallPosition` seam.
4. `HoleSelection` scenario was broken — wrong selector and the only-unlocked card was already expanded on scene open, so no state to drive.
5. SPEC §DoD said 7/5/5 MD5-distinct PNGs but SPEC §Scenarios.cs pseudocode produced 6/4/4; required text edit.
6. EditMode test gate evidence missing (PARTIAL self-grade).
