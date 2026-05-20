# Self-Review — `loop_v2_smoke_bot` (iter-4b)

> Written by `golfin-self-reviewer`. Verifies iter-4b's claims against the actual captures, logs, code, and git state.

## Verdict

`PASS`

All checklist items genuinely pass. The iter-4b root-cause fix (`Application.runInBackground = true` at EnteredPlayMode) is verified to leave zero `ProjectSettings/` footprint, all three scenarios ran headless end-to-end, the C1 gate capture (s06 HoleCompleteWidget) shows real legible modal pixels, s05 ≠ s06 is confirmed at 100% pixel-diff, and the scene-mutation audit is clean.

## Visual diff notes (independent pixel scan — done BEFORE reading implementer narrative)

This task has no Figma reference (it is a bot framework, not a UI-layout task; SPEC.md has no § Reference). The "reference" for each capture is the production app itself. Pixel scan of all 13 captures:

**hole1_playthrough (6):**
- **s01_home** — Home screen. Currency bar top-left ("50.000"), gear top-right, "CHOTO" nameplate, golfer holding a gold trophy centered, a "MAINTENANCE NOTICE" banner, "NEXT HOLE / Lomond Country Club - Hole 1" card, yellow "PLAY" button, bottom nav bar. Normal Home.
- **s02_matchmaking_searching** — MatchMakingModal visible: "DIAMOND LEAGUE" header, "FINDING OPPONENT.." with YOU vs GREENKND portraits, "NEXT HOLE Lomond Country Club - Hole 1", "CANCEL" button. Correct searching state.
- **s03_opponent_found** — Same modal, now "OPPONENT FOUND" with "YOU vs FAIRPRO" portraits. Distinct from s02.
- **s04_gameplay_armed** — Gameplay scene: top-left card "JAMES / Lv 10 / TURN 1", top-right "LOMOND / HOLE 1 - REGULAR / PAR 5", fairway with two green range markers and the ball mid-fairway with an aim arc, bottom controls SPIN / GOLFIN / DRIVER / STRAIGHT. Scene rendered correctly, HUD fully populated.
- **s05_gameplay_pre_shot** — Visually near-identical to s04 (same gameplay scene/HUD). This is the intended honest pre-modal gameplay frame.
- **s06_result_modal** — HoleCompleteWidget: green "✓ SUCCESS" header over a "Lomond Country Club - Hole 1 - Par 5" card with a "REPLAY" button; below it a "NEXT" card for "Lomond Country Club - Hole 2 - Par 4" with description text and a yellow "PLAY" button. Modal pixels present, legible, fully rendered. **This is the Stage C1 gate capture and it passes.**

**settings_round_trip (4):**
- **s02_settings_open** — Settings panel: USER PROFILE / SOUND SETTINGS / LANGUAGE / TERMS OF USE / PRIVACY POLICY / FAQ / ABOUT / CONTACT FORM / LOG OUT rows, "CLOSE" button. Collapsed accordion.
- **s03_settings_sound_expanded** — SOUND SETTINGS row expanded showing MUSIC (70) and SFX (70) sliders. Distinct from s02 — accordion expansion is visible.

**hole_selection_browse (3):**
- **s02_hole_selection_grid** — HoleSelection screen: "NEXT / Lomond Country Club - Hole 1 - Par 5" expanded card with description + yellow "PLAY", then three "LOCKED" cards for Hole 2/3/4. Distinct from Home.
- s01 == s03 (Home) byte-identical — expected by round-trip design.

## Three-step protocol results

**Step 1 (describe pixels):** done above — anchored in actual pixels, not YAML.

**Step 2 (Figma compare):** N/A — no Figma reference exists for this task (bot framework). The Step 2 hard-rule about a missing `figma-reference.png` does not apply: SPEC.md has no § Reference and the deliverable is not a UI layout. Each capture was checked against expected production-app appearance instead, and all render correctly.

**Step 3 (spec checklist walk):** below.

**Step 5 (capture-helper compliance):** PASS. `BotDriver.cs` uses `CaptureCore.SnapPlayModeSafe` exclusively (5 grep hits) — a sanctioned `CaptureCore` path explicitly listed in CLAUDE.md § Screenshots for play-mode coroutines that must capture and continue. No `ScreenCapture.CaptureScreenshot`, no manual OS screenshots. No new `*Context.cs` files added under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`, so the capture_helper maintenance protocol does not apply.

**Step 6 (bbox geometry):** N/A — there are no containment claims in this task (no "text inside BG", "child inside parent", etc.). The deliverable is a bot framework, not a layout.

**Step 7 (scene-mutation audit):** PASS. `git diff --stat -- '*.unity'` is empty across the entire repo — zero scene mutations anywhere (no `m_IsActive` flips, no `sizeDelta`/position changes). `git diff --stat ProjectSettings/` is empty — the `runInBackground` runtime-flag claim holds (no `ProjectSettings.asset` footprint). The only non-script modified asset is `Assets/Fonts/NotoSansJP-VariableFont_wght SDF.asset` (TMP dynamic-atlas regen) and `Packages/manifest.json` + `packages-lock.json` (unity-mcp plugin self-update) — all environmental, none caused by the bot, all called out in the kickoff brief.

**Step 8 (production-flow capture):** PASS / satisfied. This task changes no modal/panel layout, so the smoke-vs-production distinction is not the failure mode here. The hole1 captures themselves ARE the production-flow path: cold launch → click PLAY → real MatchMakingModal → real additive scene load (LabScaffold + Hole_01_Geo) → real gameplay HUD frames (s04/s05). The `ForceShotComplete` seam fires the same `OnShotComplete` event production fires, and s06 captures the real `HoleCompleteWidget` reacting. No `*Host`/`*SmokeRunner` pre-scripted state injection bypasses the production lifecycle.

## Checklist verification

| Item | Implementer said | Self-reviewer says | Notes |
|---|---|---|---|
| 4 bot files exist (`ls Bot/`) | PASS | CONFIRMED | BotDriver.cs, LoopV2SmokeBot.cs, Scenarios.cs, Editor/LoopV2SmokeBotMenu.cs all present and modified in iter-4b. |
| All 4 files `#if UNITY_EDITOR` guarded | PASS | CONFIRMED | grep `#if UNITY_EDITOR` ≥1 in each (BotDriver has 2 — outer guard + the inner nested guard). |
| `CaptureCore.SnapPlayModeSafe` in BotDriver | PASS | CONFIRMED | 5 grep hits. Canonical sanctioned path. |
| 3 `[MenuItem]` action items | PASS | CONFIRMED | 6 `[MenuItem]` total (3 action + 3 validate) — matches the Option B safety pattern. |
| Project compiles clean | PASS | CONFIRMED | EditMode gate ran (305/305); a non-compiling project cannot run the test gate. |
| EditMode 305/305 PASS | PASS | CONFIRMED | `all_editmode_test_results.txt` (2026-05-20 06:46): TOTAL 305 / PASSED 305 / FAILED 0 / SKIPPED 0 / GATE PASS, duration 23.75s. |
| hole1 — 6 MD5-distinct PNGs + history.log | PASS | CONFIRMED | 6 PNGs, 6 distinct MD5s (`7d95b3bc`, `6e1540be`, `7b07550c`, `a0a1495e`, `6688ad0f`, `ecc4b8df`). history.log ends `=== Scenario complete ===`. |
| hole1 — s05 NOT a duplicate of s06 modal | PASS | CONFIRMED | Independent pixel-diff via Pillow: s05 vs s06 = **100.00% pixels differ** — s05 is gameplay scene, s06 is the modal. Fully distinct. |
| hole1 — s06 shows HoleCompleteWidget visible pixels (C1 gate) | PASS | CONFIRMED | Pixel scan: s06 shows ✓SUCCESS / Hole 1 - Par 5 / REPLAY / NEXT Hole 2 - Par 4 / PLAY — modal fully rendered and legible. **Hard-FAIL condition (blank/absent modal) NOT triggered.** |
| settings — 4 MD5-distinct PNGs + history.log | PASS | CONFIRMED | 4 distinct MD5s. history.log ends `=== Scenario complete ===`. s02 (collapsed) and s03 (Sound expanded with MUSIC/SFX sliders) visibly distinct. |
| holesel — 3 PNGs, s01==s03, s02 distinct | PASS | CONFIRMED | s01==s03 (`7d95b3bc`) byte-identical — expected round-trip design, NOT a bug per the kickoff brief. s02 (`41c5d763`) distinct, shows HoleSelection grid. history.log ends `=== Scenario complete ===`. |
| Each history.log ends `=== Scenario complete ===` | PASS | CONFIRMED | All three logs verified — none end `INCOMPLETE`. |
| Hole1 terminal=InCup, result modal visible | PASS (iter-4b, supersedes iter-3 AtRest FAIL) | CONFIRMED | hole1 history.log: `ForceShotComplete OK: terminal=InCup` then s06 captured the HoleCompleteWidget. The iter-3 AtRest FAIL is resolved by the Option B seam. |
| Seam `ForceShotCompleteForBot` — 5-condition compliance | PASS | CONFIRMED | Read BallStateMachine.cs:287-315: fully `#if UNITY_EDITOR` wrapped; `_ForBot` suffix; delegates to `OnShotComplete?.Invoke(result)` (same event production uses); production path untouched; isolates the modal-wiring unit. All five conditions hold. |
| `runInBackground` fix leaves zero ProjectSettings footprint | PASS | CONFIRMED | `LoopV2SmokeBotMenu.cs:139` sets `Application.runInBackground = true` (runtime flag) at EnteredPlayMode. `git diff --stat ProjectSettings/` empty — claim verified. |
| ShellScene clean (no `[LoopV2SmokeBot]` GO saved) | PASS | CONFIRMED | `git diff --stat Assets/Scenes/ShellScene.unity` empty; `git diff --stat -- '*.unity'` empty repo-wide. |

## Specific failures (if any)

None. No OVERRIDE-FAIL, no CONFIRMED-FAIL.

The iter-3 `terminal=AtRest` FAIL is fully resolved by the iter-4 `ForceShotCompleteForBot` seam (architect-sanctioned Option B, verdict `ARCHITECT_VERDICT_INCUP.md`). The iter-4 `frame=1 freeze` blocker is fully resolved by the iter-4b `runInBackground` root-cause fix. Both prior blocking issues are independently verified above.

## Notes / minor observations (not failures)

- **s04 vs s05 pixel-diff measured at 2.26%**, slightly above the 1.37% the implementer quoted (likely a measurement-method difference — compressed PNGs here vs full-res, or a different nonzero-pixel definition). This is still well within the "two honest gameplay frames look similar" range Cesar explicitly accepted when choosing the "Real pre-modal s05" option. Not a FAIL — flagged only for transparency. The architect-relevant fact is s05≠s06 (100%), which holds firmly.
- Environmental modifications outside the bot's scope: `NotoSansJP-...SDF.asset` (TMP atlas regen), `Packages/manifest.json` + `packages-lock.json` (unity-mcp self-update). The kickoff brief pre-cleared all three as not bot contamination. Noting them per protocol; they do not affect the verdict.
- Stale May-19 captures were correctly deleted from all three `screenshots/` folders (visible as `D` entries in `git status`) and replaced with the iter-4b 06:33–06:46 set. Good hygiene — no mixed-iteration capture sets.

## Routing

`FORWARD_TO_ARCHITECT` — routes to `golfin-reviewer` for final review.

## Iteration count

This is iteration **1** of self-review for this task (no prior `SELF_REVIEW.md` existed). N < 3 — no forced escalation. Verdict stands as PASS on its own merits.
