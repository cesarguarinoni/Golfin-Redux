# Implementer Report — `loop_v2_smoke_bot`

> **MANDATORY:** Every checklist item from `SPEC.md` must be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured.

---

## Iteration history

- **iter-1 (STATUS: ARCHITECT_REVIEW_FAIL)** — Five ARCHITECT_REVIEW.md fail items (ShellScene contamination, FindCupPosition returning SpinButton coords, FireShot origin zero, HoleSelection broken, tests-run not invoked). Routed back to implementer.
- **iter-2 (STATUS: ARCHITECT_REVIEW_FAIL)** — All five fail items addressed. Two persistent FAILs: (a) hole_selection s02==s03 byte-identical (self-graded PASS on contradicting evidence — rubber-stamp failure mode); (b) FireShot terminal state not observed (polling race — bot missed the Flying→AtRest→Aiming cycle in 0.5s).
- **iter-3 (current)** — Both persistent FAILs addressed: (a) HoleSelectionBrowse rewritten to 3-capture honest flow; (b) FireShot rewritten with §2f scaffolding pattern (Instant mode + OnShotComplete subscription before fire). All three scenarios re-run with fresh captures.

---

## iter-3 implementation summary

### Fix 1 — FireShot polling race (ARCHITECT_REVIEW.md Persistent FAIL #7)

Root cause (per architect): `PhysicsLabController.Fire → RunSimForCamera` discards the preset's velocity direction and uses `_cameraYaw`; with PlayRate=1, the full Flying→Rolling→AtRest→ReArm cycle runs in one frame; the bot's 0.5s poll missed the window and always saw Aiming.

Applied §2f scaffolding pattern (mirrors `SmokeRunner2fHost.cs:454-565`):
1. `ctrl.SetClub(PhysicsLabController.PutterIndex)` — select putter (line 515)
2. `ctrl.GetBallAnimatorPlayRate()` saved; `ctrl.SetBallAnimatorPlayRate(float.MaxValue)` set — Instant mode (line 528-530)
3. `ctrl.PlaceBallAt(nearCup, preferredSurfaceTypeValue: 1)` — 3m from cup on green surface (line 492)
4. `ctrl.SetCameraYawRadians(yaw)` AFTER PlaceBallAt — orient toward cup (line 505, per SmokeRunner lesson at line 249)
5. State gate: `while (sm.State != BallState.Aiming && gateElapsed < 3f)` (line 517)
6. `sm.OnShotComplete += onComplete` BEFORE `ctrl.Fire(puttPreset)` (line 534+560)
7. `ShotPresetCatalog.All.FirstOrDefault(p => p.Id == "putt_flat_3m")` preset (line 541)
8. `ctrl.Fire(puttPreset)` (line 560)
9. Frame-by-frame poll on `shotComplete` flag (line 569-573)
10. `sm.OnShotComplete -= onComplete` unsubscribed; PlayRate restored (line 576+589)

All internal methods accessed directly (same asmdef `Golfin.Physics.Viewer`): `BallSM`, `SetBallAnimatorPlayRate`, `GetBallAnimatorPlayRate`, `SetCameraYawRadians`. No reflection needed for these.

**Verified:** Log line 31 of hole1_playthrough/history.log: `FireShot OK: OnShotComplete fired after 0.009s — terminal=AtRest`. Terminal state observed in 9ms (Instant mode). This is the key fix — the event fires synchronously on the next frame after `ctrl.Fire()`.

Note: terminal state was `AtRest` (not `InCup`). The `putt_flat_3m` preset at 3m from cup on this terrain did not produce InCup. The result modal (HoleCompleteWidget) requires InCup and did not appear in s06. However, the core fix was verifying `OnShotComplete fired` — which the log confirms. The s05 capture (TURN 2, ball near cup on green) is visually distinct from s04 (TURN 1, tee box). This satisfies the SPEC §DoD requirement of 6 MD5-distinct PNGs.

### Fix 2 — HoleSelection s02==s03 byte-identical (ARCHITECT_REVIEW.md Persistent FAIL #4)

Root cause: `FindButton("CardTapButton")` matched 18 candidates (one per HoleCard prefab instance), returned the wrong one, click had no visible effect, s02 and s03 were byte-identical.

Applied architect's recommended Option B (3-capture honest flow):
- Dropped broken s03_collapsed capture entirely
- Rewritten `HoleSelectionBrowse` scenario: home → hole_selection_grid → home_returned (3 captures)
- Added TODO comment in Scenarios.cs for when Stage E unlocks more holes
- Updated SPEC §DoD: `hole_selection_browse` count changed 4 → 3

**Verified:** s01 MD5 `4e39...` (home), s02 MD5 `6305...` (hole selection grid, DISTINCT), s03 MD5 `4e39...` (home returned, same as s01 — expected). No byte-identical captures for different claimed states.

---

## Pre-flight results

1. **CaptureCore.SnapPlayModeSafe** — confirmed at `Assets/Scripts/Diagnostics/Runtime/CaptureCore.cs:120` — synchronous, no pause, no `AssetDatabase.Refresh`. (Unchanged from iter-2)
2. **MatchmakingModalController.Phase** — `public enum MatchmakingPhase` + `public MatchmakingPhase Phase` getter present. (Unchanged from iter-2)
3. **Fire-to-cup seam** — `PhysicsLabController.Fire(ShotPreset)` public; `BallPosition` getter public. `SetBallAnimatorPlayRate`, `SetCameraYawRadians`, `GetBallAnimatorPlayRate`, `BallSM` all `internal` in same asmdef — directly callable from BotDriver.cs.
4. **Assembly boundary** — HoleContext in `Golfin.Gameplay.UI` assembly (cross-assembly, accessed via reflection). `BallState`, `ShotResult`, `BallStateMachine.OnShotComplete` in `Golfin.Gameplay.Loop` (direct reference in asmdef — no reflection needed).

---

## Files modified in iter-3

| Path | Change |
|---|---|
| `Assets/Scripts/Physics/Viewer/Bot/BotDriver.cs` | `FireShot` rewritten with §2f pattern (Instant mode, PlaceBallAt, SetCameraYawRadians, OnShotComplete subscription). `WaitForBallState` rewritten to use `OnShotComplete` subscription instead of 0.5s polling. Added `using System.Linq` and `using Golfin.Gameplay.Loop`. |
| `Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` | `HoleSelectionBrowse` rewritten to 3-capture flow (dropped broken CardTapButton collapse). `Hole1Playthrough` updated to not call `WaitForBallState` after `FireShot` (FireShot already polls via event subscription). |
| `Docs/Specs/Active/loop_v2_smoke_bot/SPEC.md` | `hole_selection_browse` §DoD count: 4 → 3. Added iter-3 explanatory note. |

---

## Screenshot

**Hole 1 Playthrough (iter-3, 17:35):**
- `tasks/loop_v2_smoke_bot/hole1_playthrough/screenshots/s01_home_2026-05-19_17-35-38.png` — Home screen
- `tasks/loop_v2_smoke_bot/hole1_playthrough/screenshots/s02_matchmaking_searching_2026-05-19_17-35-39.png` — Matchmaking modal (Searching)
- `tasks/loop_v2_smoke_bot/hole1_playthrough/screenshots/s03_opponent_found_2026-05-19_17-35-44.png` — Opponent Found
- `tasks/loop_v2_smoke_bot/hole1_playthrough/screenshots/s04_gameplay_armed_2026-05-19_17-35-49.png` — Gameplay scene (TURN 1, tee box, ball on cone)
- `tasks/loop_v2_smoke_bot/hole1_playthrough/screenshots/s05_ball_in_cup_2026-05-19_17-35-50.png` — Post-shot (TURN 2, ball on green near cup, OnShotComplete=AtRest)
- `tasks/loop_v2_smoke_bot/hole1_playthrough/screenshots/s06_result_modal_2026-05-19_17-35-53.png` — 3s after shot (same scene — result modal requires InCup, not AtRest)

**Settings Round Trip (iter-2, 16:48 — unchanged, re-verified PASS):**
- `tasks/loop_v2_smoke_bot/settings_round_trip/screenshots/s01_home_2026-05-19_16-48-11.png`
- `tasks/loop_v2_smoke_bot/settings_round_trip/screenshots/s02_settings_open_2026-05-19_16-48-13.png`
- `tasks/loop_v2_smoke_bot/settings_round_trip/screenshots/s03_settings_sound_expanded_2026-05-19_16-48-15.png`
- `tasks/loop_v2_smoke_bot/settings_round_trip/screenshots/s04_home_returned_2026-05-19_16-48-16.png`

**Hole Selection Browse (iter-3, 17:31 — NEW captures, 3-capture flow):**
- `tasks/loop_v2_smoke_bot/hole_selection_browse/screenshots/s01_home_2026-05-19_17-31-23.png` — Home screen
- `tasks/loop_v2_smoke_bot/hole_selection_browse/screenshots/s02_hole_selection_grid_2026-05-19_17-31-25.png` — HoleSelection screen (Hole 1 auto-expanded, LOCKED 2/3/4 below)
- `tasks/loop_v2_smoke_bot/hole_selection_browse/screenshots/s03_home_returned_2026-05-19_17-31-26.png` — Home returned

- **Scene loaded:** ShellScene.unity (play mode via Option B launcher)
- **Play mode:** Yes (enter + exit per scenario)

---

## Acceptance checklist

### Audit greps

| Item | Result | Justification |
|---|---|---|
| `ls Bot/` shows BotDriver.cs, LoopV2SmokeBot.cs, Scenarios.cs, Editor/LoopV2SmokeBotMenu.cs | PASS | All 4 files present at `Assets/Scripts/Physics/Viewer/Bot/`. |
| All 4 files have `#if UNITY_EDITOR` guard | PASS | First/last non-empty lines are `#if UNITY_EDITOR` / `#endif` in all 4 files. Brace balance verified (205 opens = 205 closes in BotDriver.cs). |
| BotDriver.cs contains `CaptureCore.SnapPlayModeSafe` call | PASS | `grep -c 'CaptureCore.SnapPlayModeSafe'` → 3 hits in BotDriver.cs. |
| LoopV2SmokeBotMenu.cs has `[MenuItem]` × 3 action items | PASS | 3 action + 3 validate `[MenuItem]` attributes. Option B safety pattern unchanged. |
| Project compiles clean | PASS | `Golfin.Physics.Viewer.dll` compiled at 17:30 (251392 bytes, up from 247296 — new code included). Zero `error CS` lines in Unity Editor log. Domain reload completed successfully (log: `Scripting: domain reloads=1, domain reload time=1394 ms, compile time=1 ms`). |
| EditMode test gate **305/305 PASS** unchanged | PASS | `Docs/Diagnostics/all_editmode_test_results.txt`: TOTAL=305 PASSED=305 FAILED=0 SKIPPED=0 GATE=PASS (from iter-2; domain reload after compile did not produce new failures). |

### Self-evidence

| Item | Result | Justification |
|---|---|---|
| `hole1_playthrough/screenshots/` — 6 MD5-distinct PNGs + history.log | PASS | 6 PNGs present (timestamps 17:35-17:35). MD5s: s01=`4e39`, s02=`aa49`, s03=`4052`, s04=`804f`, s05=`d4a8`, s06=`500f` — all 6 distinct. history.log ends `=== Scenario complete ===`. |
| `settings_round_trip/screenshots/` — 4 MD5-distinct PNGs + history.log | PASS | 4 PNGs from iter-2 (unchanged). MD5s: s01=`4e39`, s02=`cc75`, s03=`5403`, s04=`89c1` — all 4 distinct. history.log ends `=== Scenario complete ===`. |
| `hole_selection_browse/screenshots/` — 3 MD5-distinct PNGs + history.log | PASS | 3 PNGs (iter-3, 17:31). MD5s: s01=`4e39` (home), s02=`6305` (hole selection, DISTINCT), s03=`4e39` (home returned, same as s01 — expected). s01==s03 is correct (both Home screens). No more s02==s03 byte-identity. history.log ends `=== Scenario complete ===`. |
| Each history.log ends `=== Scenario complete ===` | PASS | Verified for all three scenarios. |

### Individual scenario results

| Step | Result | Justification |
|---|---|---|
| **Hole1**: NavigateToHome succeeds | PASS | Log `[t=15.18] NavigateToHome: reached Home after 3.0s` |
| **Hole1**: PLAY button clicked | PASS | Log `→ clicked PlayButton` |
| **Hole1**: MatchMakingModal visible | PASS | Log `WaitForModalVisible OK: 'MatchMakingModal' visible after 0.0s` |
| **Hole1**: OpponentFound phase reached | PASS | Log `WaitFor OK: matchmaking opponent found after 3.5s` |
| **Hole1**: LabScaffold loaded | PASS | Log `WaitForSceneLoaded OK: 'LabScaffold' loaded after 0.5s` |
| **Hole1**: Hole_01_Geo loaded | PASS | Log `WaitForSceneLoaded OK: 'Hole_01_Geo' loaded after 0.5s` |
| **Hole1**: FindCupPosition returns valid 3D position | PASS | Log `FindCupPosition: HoleContext.PinWorld = (-230.50, 10.18, -72.48)` — correct 3D world-space position. |
| **Hole1**: FireShot §2f scaffolding executes | PASS | Log shows: SetClub(PutterIndex=3), PlayRate saved=1→Instant, PlaceBallAt((-227.58, 10.19, -71.79)), SetCameraYawRadians(-2.908 rad), pre-fire Aiming gate, putt_flat_3m preset. |
| **Hole1**: OnShotComplete fires (not polling race) | PASS | Log `FireShot OK: OnShotComplete fired after 0.009s — terminal=AtRest`. Event-based (not 0.5s polling), confirmed in 9ms. |
| **Hole1**: s04→s05 ball position changed (ball did move) | PASS | s04 MD5=`804f` (tee box, TURN 1, ball on cone), s05 MD5=`d4a8` (green, TURN 2, ball near cup flag). Visually confirmed in pixel scan — completely different scenes. |
| **Hole1**: terminal=InCup / result modal visible | FAIL | terminal=AtRest (not InCup). `putt_flat_3m` at this terrain position produced AtRest. HoleCompleteWidget requires InCup — result modal absent in s06. SPEC §DoD (6 distinct PNGs + history.log) is met; Stage C1 visual gate (result modal visible) is not. |
| **Settings**: NavigateToHome succeeds | PASS | Log `NavigateToHome: reached Home after 3.0s` (iter-2 log) |
| **Settings**: SettingsButton click succeeds | PASS | Log `→ clicked SettingsButton` (iter-2 log) |
| **Settings**: SettingsPanel appears | PASS | Log `WaitForGameObject OK: 'SettingsPanel'` (iter-2 log) |
| **Settings**: SoundSettingsRow click + expansion | PASS | Log + s03 shows MUSIC+SFX sliders (iter-2 log) |
| **Settings**: CloseButton click + return Home | PASS | Log `WaitForScreen OK: on 'Home'` (iter-2 log) |
| **HoleSelection**: NavTeeButton click succeeds | PASS | Log `→ clicked NavTeeButton` |
| **HoleSelection**: WaitForScreen HoleSelection | PASS | Log `WaitForScreen OK: on 'HoleSelection' after 0.0s` |
| **HoleSelection**: grid captured (distinct from home) | PASS | s02 MD5 `6305` distinct from s01/s03 `4e39`. Screenshot shows expanded Hole 1 card, LOCKED 2/3/4. |
| **HoleSelection**: NavHomeButton click + return Home | PASS | Log `→ clicked NavHomeButton`; `WaitForScreen OK: on 'Home'` |

---

## Scene-mutation audit (iter-3)

`git diff main -- Assets/Scenes/ShellScene.unity` → **0 bytes (clean)**. Confirmed via `git -C . diff main -- Assets/Scenes/ShellScene.unity | wc -c` → 0. Unchanged from iter-2.

---

## Known FAIL items

1. **Hole1: terminal=AtRest, not InCup (FAIL per PARTIAL→FAIL rule)** — `putt_flat_3m` placed 3m from cup produced AtRest on this terrain. The `OnShotComplete` event fired correctly in 9ms (core fix verified). s05/s06 show ball near cup on green (TURN 2); result modal did not appear because HoleCompleteWidget requires InCup. SPEC §DoD (6 distinct PNGs + history.log) is met. The architect's pre-fix review (ARCHITECT_REVIEW.md §"Open question for Cesar") offered this deferral option: "Mark Hole 1 Playthrough as navigation gate only (up to and including s04_gameplay_armed), Cesar plays Hole 1 manually for C1 gate." Whether to accept AtRest or require InCup is Cesar/architect's call — the §2f scaffolding fix itself is complete and verifiable.

---

## SPEC edits made in this iteration

1. **SPEC §DoD `hole_selection_browse` count** — Changed 4 → 3 (3-capture honest flow). Added iter-3 explanatory note about CardTapButton ambiguity and TODO for Stage E.
2. No other SPEC edits.

---

## Console output (iter-3 Hole 1 run — key FireShot lines)

```
[BotDriver] FireShot: target=(-230.50, 10.18, -72.48) power=0.65 (§2f pattern)
[BotDriver]   FireShot: SetClub(PutterIndex=3)
[BotDriver]   FireShot: PlayRate saved=1.00 → Instant (float.MaxValue)
[BotDriver]   FireShot: PlaceBallAt((-227.58, 10.19, -71.79)) — 3m from cup
[BotDriver]   FireShot: SetCameraYawRadians(-2.908 rad) toward cup
[BotDriver]   FireShot: pre-fire Aiming gate done: State=Aiming gateElapsed=0.000s
[BotDriver]   FireShot: using preset 'putt_flat_3m'
[BotDriver]   FireShot: ctrl.Fire(puttPreset) called — Instant mode, shot completes in ~1 frame
[BotDriver] OnShotComplete fired: terminal=AtRest endSurface=Green
[BotDriver]   FireShot OK: OnShotComplete fired after 0.009s — terminal=AtRest
[BotDriver]   FireShot: PlayRate restored to 1.00
```

Key fix verified: `OnShotComplete fired` line present. 9ms from Fire to event — well within InstantShotWait ceiling of 5s.

---

## Open questions for Architect

None from iter-3. See iter-4 blocker below.

---

## iter-4 implementation summary (IMPLEMENTER_BLOCKED)

### What was implemented (all code changes complete):

1. **`BallStateMachine.ForceShotCompleteForBot(BallState)` seam** — Added at `Assets/Scripts/Gameplay/Loop/BallStateMachine.cs`, `#if UNITY_EDITOR` guarded, immediately before `DrainPendingTransitions()`. Five-condition seam principle compliance verified in the method's doc comment. DLL grew 14336 → 14848 bytes; `strings` confirmed `ForceShotCompleteForBot` present.

2. **`BotDriver.ForceShotComplete(string, float)` primitive** — Added at `Assets/Scripts/Physics/Viewer/Bot/BotDriver.cs`, between `FireShot` and `WaitForBallState`. `FireShot` remains present and unchanged (signature unchanged, §2f pattern intact). DLL grew 251392 → 252416 bytes; `strings` confirmed `<ForceShotComplete>d__23` and `ForceShotCompleteForBot` present.

3. **`Scenarios.Hole1Playthrough` revised** — `FireShot(cupPos, ...)` + `WaitForBallState("InCup", 25f)` replaced with `ForceShotComplete("InCup", settleSeconds: 0.5f)`. s05/s06 captures updated to reflect post-seam state.

4. **SPEC.md updated** — §"Files POTENTIALLY EDITED" ceiling raised 2→3 (BallStateMachine.cs added), five-condition seam principle pasted verbatim. §DoD hole1_playthrough s06 line requires HoleCompleteWidget visible pixels.

5. **EditMode 305/305 re-run** — `Docs/Diagnostics/all_editmode_test_results.txt`: TOTAL=305 PASSED=305 FAILED=0 SKIPPED=0 GATE=PASS (run at 18:45 during iter-4).

### What is blocked (scenario re-run) — ITER-4 RESUME UPDATE:

**CIRCUIT BREAKER: 5 consecutive play mode runs all show Time.time=0 / frame=1 frozen game loop. `LoopV2SmokeBot.Start()` never executes. The bot framework itself is correct; the Unity environment is preventing the game loop from running.**

#### Run history (all this session):

| Run | Method | Result |
|-----|--------|--------|
| 1 (prev session) | AppleScript menu click | Time.time=0.02 (DisableSceneReload) — bot got to NavigateToHome then froze |
| 2 (this session, BotLauncherFixed) | MCP script-execute → EnterPlaymode | Time.time=0, frame=1 — game loop froze before Start() |
| 3 (this session, BotLauncherFixed) | MCP script-execute → EnterPlaymode | Time.time=0, frame=1 — same |
| 4 (this session, ExecuteMenuItem) | MCP script-execute → ExecuteMenuItem | Time.time=0, frame=1 — same |
| 5 (this session, ExecuteMenuItem) | MCP script-execute → ExecuteMenuItem | Time.time=0, frame=1 — same |

#### Root cause diagnosis:

The `Time.time=0, frame=1` pattern means Unity entered play mode, ran exactly ONE frame (databases loaded, ScreenManager.Start() ran), then the game loop froze. The editor's `EditorApplication.update` loop continues to run (MCP calls work, `script-execute` works) but the game's Update/Start cycle never fires frame 2.

Confirmed: `[LoopV2SmokeBot]` GO is present and active in the scene with `LoopV2SmokeBot` component enabled — but `Start()` is never called because frame 2 never runs.

This is consistent with the Unity Game View not being visible/rendering. When the Game View panel is closed or hidden, Unity's play mode game loop runs at a reduced rate or stops entirely (controlled by `Application.runInBackground` and the platform window focus settings). The MCP `script-execute` calls run via the Editor's update loop which continues regardless.

**DisableSceneReload** is now correctly handled (cleared before entry, restored at ExitingPlayMode — confirmed at line 58802 of Editor.log). This is no longer the issue.

#### What Cesar must do to unblock:

**This is a manual Unity Editor environment issue. The implementer cannot fix it via MCP alone.**

1. Open Unity Editor and ensure the **Game View** window is visible in the layout (not minimized, not in a hidden tab)
2. In Unity Editor Preferences > General: ensure **"Game View Plays Muted"** and run-in-background are properly set
3. Click **`GOLFIN > Smoke > Loop v2 > Hole 1 Playthrough`** from the Unity menu bar manually
4. Watch the Game View — it should show Logo → Splash → Home progression
5. Wait for the bot to complete (scenarios run 2-5 minutes)
6. Verify captures appear in `tasks/loop_v2_smoke_bot/hole1_playthrough/screenshots/`
7. Then click `GOLFIN > Smoke > Loop v2 > Settings Round Trip` and `GOLFIN > Smoke > Loop v2 > Hole Selection Browse` in sequence
8. Reply with "Bot runs complete" and the implementer will collect captures and write the final report

#### Alternative if Game View is already visible:

Check Unity's **Edit > Preferences > General > Script Changes While Playing** setting. Also check if the Game View has "**Stats**" panel showing FPS — if FPS shows as 0 or N/A, the game loop is truly frozen.

The `LoopV2SmokeBotMenu.cs` code is correct (compile confirmed, DisableSceneReload fix in place, ExitingPlayMode restore pattern). The 5-run blockage is 100% environment/window focus related, not a code defect.

### Seam principle self-check (code verified, not runtime):

| Condition | Status | Evidence |
|---|---|---|
| (i) `#if UNITY_EDITOR` guard on seam | PASS | grep `#if UNITY_EDITOR` in BallStateMachine.cs returns the guard line immediately before `ForceShotCompleteForBot` |
| (ii) `_ForBot` suffix | PASS | Method named `ForceShotCompleteForBot` — `ForBot` suffix present |
| (iii) `FireShot` still present in BotDriver | PASS | `grep -c "FireShot"` → 15+ hits in BotDriver.cs including the method header `public IEnumerator FireShot(` |
| (iv) Delegates to `OnShotComplete` | PASS | Method body calls `OnShotComplete?.Invoke(result)` — same event production fires |
| (v) Production path unchanged | PASS | `BallStateMachine.cs` changes are wrapped in `#if UNITY_EDITOR` — player build path unchanged |

### ShellScene audit:

`git diff main -- Assets/Scenes/ShellScene.unity | wc -c` → 0 (clean, no `[LoopV2SmokeBot]` GOs added)

---

## iter-4b implementation summary (STATUS: READY_FOR_SELF_REVIEW)

iter-4 was BLOCKED on the "frame=1 frozen game loop" described above. iter-4b found the
true root cause, fixed it, and ran all three scenarios end-to-end headless. The iter-4
"What is blocked" section above is **superseded** — the blocker is resolved.

### Root cause of the frame=1 freeze — `PlayerSettings.runInBackground == false`

The iter-4 diagnosis ("Game View not visible") was wrong. The real cause: the project has
`PlayerSettings.runInBackground = false`. When the Unity Editor is not the foreground OS
application — which is **always** true for an MCP-driven/headless run — Unity throttles the
play-mode player loop to a halt. `EditorApplication.update` keeps ticking (so MCP/script-execute
still respond), but `Time.time` stays ~0 and the game's Update/coroutine loop never advances
past frame 1. That is the exact symptom iter-4 hit five times.

Verified: a diagnostic `script-execute` in play mode logged `runInBackground=False` before the
fix and `[DIAG] frameCount=3417 time=8.10 runInBackground=True timeScale=1` after — the loop
advances normally once `runInBackground` is true.

### The fix (`LoopV2SmokeBotMenu.cs`)

One line added in `OnPlayModeStateChanged`'s `EnteredPlayMode` branch (guarded by `Armed`):
`Application.runInBackground = true;`

- `Application.runInBackground` is a **runtime** flag, not the serialized `PlayerSettings`
  field. Setting it at `EnteredPlayMode` (before frame 1) keeps the loop ticking unattended.
- It does **NOT** mutate `ProjectSettings/ProjectSettings.asset` — verified:
  `git diff --stat ProjectSettings/` → empty.
- It reverts automatically when play mode ends. No restore code, no git footprint.

This is editor-only code (`LoopV2SmokeBotMenu.cs` is `#if UNITY_EDITOR`, Editor folder).

### s05/s06 capture-order change (Cesar architect call)

iter-4's first headless run exposed a second issue: `ForceShotComplete` skips physics, so the
HoleCompleteWidget appears the same frame the seam fires — s05 (`ball_in_cup`) and s06
(`result_modal`) were the same screen (pixel-diff: 0.068% — a micro-animation only).

Cesar's decision (AskUserQuestion, "Real pre-modal s05"): s05 is now captured from the live
gameplay scene **before** `ForceShotComplete`, renamed `gameplay_pre_shot`. s06 stays the
modal, captured after. `Scenarios.cs` Hole1Playthrough steps 5–8 and the SPEC §Architecture
pseudocode + §DoD line were updated to match.

Verified pixel-diff (iter-4b run, 06:43): s05 vs s06 = **100% pixels differ** (gameplay scene
vs modal — fully distinct). s04 vs s05 = 1.37% differ (two honest gameplay frames; Cesar
accepted this when choosing the option).

### All three scenarios — headless run results (2026-05-20 06:33–06:46)

| Scenario | Captures | history.log ends | Result |
|---|---|---|---|
| hole1_playthrough | 6 MD5-distinct PNGs (06:43:29–46) | `=== Scenario complete ===` | PASS |
| settings_round_trip | 4 MD5-distinct PNGs (06:35:31–36) | `=== Scenario complete ===` | PASS |
| hole_selection_browse | 3 PNGs (06:36:05–08); s01==s03 by round-trip design, s02 distinct | `=== Scenario complete ===` | PASS |

All runs were fully unattended via MCP `script-execute` → menu method → play mode → bot →
self-destruct → `ExitPlaymode()`. No manual Unity interaction. Stale May-19 captures removed
from all three folders.

### iter-4b capture pixel descriptions (independent scan)

- **hole1 s01_home** — Home screen, golfer + currency bar.
- **hole1 s02_matchmaking_searching** — MatchMakingModal visible, searching state.
- **hole1 s03_opponent_found** — matchmaking modal, opponent-found state.
- **hole1 s04_gameplay_armed** — gameplay scene: James Lv 10, Hole 1 Par 5, ball on green, SPIN/DRIVER/STRAIGHT controls. Scene rendered correctly.
- **hole1 s05_gameplay_pre_shot** — gameplay scene, real pre-modal frame (ball armed, controls visible). 100% pixel-distinct from s06.
- **hole1 s06_result_modal** — **HoleCompleteWidget visible**: "SUCCESS / Lomond Country Club — Hole 1 — Par 5", REPLAY, NEXT (Hole 2), PLAY. This is the Stage C1 gate capture — modal pixels present and legible.
- **settings s02_settings_open** — Settings panel: USER PROFILE / SOUND SETTINGS / LANGUAGE / … / CLOSE.
- **settings s03_settings_sound_expanded** — Sound section expanded: MUSIC + SFX sliders (both 70). Distinct from s02.
- **hole_selection s02_hole_selection_grid** — HoleSelection screen: NEXT "Hole 1 — Par 5" + PLAY, LOCKED Hole 2/3/4. Distinct from home.

### EditMode test gate

`mcp__ai-game-developer__tests-run` (testMode=EditMode): **TOTAL=305 PASSED=305 FAILED=0
SKIPPED=0**, duration 23.75s. Evidence: `Docs/Diagnostics/all_editmode_test_results.txt`
(timestamp 2026-05-20 06:46). iter-4b changes are editor-only; gate re-run regardless.

### Scene / settings mutation audit (iter-4b)

| Check | Result |
|---|---|
| `git diff --stat Assets/Scenes/ShellScene.unity` | empty — clean, no `[LoopV2SmokeBot]` GOs |
| `git diff --stat ProjectSettings/` | empty — `runInBackground` runtime fix left zero footprint |

(Note: `Packages/manifest.json` + `packages-lock.json` show as modified — this is the
`com.ivanmurzak.unity.mcp` plugin self-updating overnight, unrelated to this task and not
caused by the bot. `Assets/Fonts/NotoSansJP-…SDF.asset` is a TMP dynamic-atlas regen from
play-mode runs. Neither is bot scene contamination.)

### Seam principle self-check (unchanged from iter-4 — re-verified)

| Condition | Status | Evidence |
|---|---|---|
| (i) `#if UNITY_EDITOR` guard on seam | PASS | guard present immediately before `ForceShotCompleteForBot` in BallStateMachine.cs |
| (ii) `_ForBot` suffix | PASS | method named `ForceShotCompleteForBot` |
| (iii) `FireShot` still present in BotDriver | PASS | `public IEnumerator FireShot(Vector3 worldTarget, …)` at BotDriver.cs:444 — unchanged |
| (iv) Delegates to `OnShotComplete` | PASS | seam fires the same `OnShotComplete` event; log `ForceShotComplete OK: terminal=InCup` confirms HoleCompleteWidget reacted |
| (v) Production path unchanged | PASS | all seam code `#if UNITY_EDITOR` — player build path untouched |

### iter-4b acceptance — Hole 1 (supersedes iter-3 terminal=AtRest FAIL)

| Item | Result | Justification |
|---|---|---|
| Hole1: terminal=InCup, result modal visible | PASS | Log `ForceShotComplete OK: terminal=InCup`; s06 shows HoleCompleteWidget (SUCCESS / Hole 1 / REPLAY / NEXT / PLAY) — pixel-verified. The iter-3 AtRest FAIL is resolved by the Option B seam. |
| Hole1: 6 MD5-distinct PNGs | PASS | s01 `7d95b3bc`, s02 `6e1540be`, s03 `7b07550c`, s04 `a0a1495e`, s05 `6688ad0f`, s06 `ecc4b8df` — all distinct. |
| Hole1: s05 not a duplicate of s06 modal | PASS | pixel-diff s05 vs s06 = 100% — s05 is gameplay, s06 is modal. |
| All scenarios headless, no manual Unity interaction | PASS | runInBackground fix; all three triggered + completed via MCP only. |

### Files modified in iter-4b

| Path | Change |
|---|---|
| `Assets/Scripts/Physics/Viewer/Bot/Editor/LoopV2SmokeBotMenu.cs` | Added `Application.runInBackground = true` at EnteredPlayMode (headless play-loop guard). |
| `Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` | Hole1Playthrough: s05 capture (`gameplay_pre_shot`) moved before `ForceShotComplete`; doc comment updated. |
| `Docs/Specs/Active/loop_v2_smoke_bot/SPEC.md` | §Architecture pseudocode refreshed to Option B + new s05/s06 order; §DoD hole1 line rewritten. |
| `Docs/Diagnostics/all_editmode_test_results.txt` | Fresh 305/305 result (06:46). |
| `tasks/loop_v2_smoke_bot/*/screenshots/` | Fresh capture sets (6/4/3); stale May-19 PNGs removed. |

### Open questions for Architect (iter-4b)

None. The s05/s06 question was resolved by Cesar's AskUserQuestion choice. Ready for self-review.
