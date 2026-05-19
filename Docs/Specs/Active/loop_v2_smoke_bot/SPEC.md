# loop_v2_smoke_bot — Production-flow playthrough bot

**Parent scope:** `Docs/Specs/Active/loop_v2_scope/SPEC.md` (inserted between Stage C0 and C1)
**Task type:** TELLCODE — pattern matches existing `SmokeRunner2fHost` template exactly
**Notion:** GOLFIN_Roadmap — new entry (Order 335, between C0=330 and C1=340)
**Status:** SPEC_READY

---

## Goal

A bot that drives the **production flow** (ShellScene → PLAY → matchmaking → gameplay → fire-to-cup) and captures MD5-distinct screenshots at every state transition. Eliminates the need for Cesar to play through the loop manually during every remaining Stage C1 / D / E / F visual gate.

After this spec ships:
- Single menu item `GOLFIN/Smoke/Loop v2 Production Playthrough (Hole_01)` arms the bot, opens ShellScene, enters play mode.
- Bot waits for Home → clicks PLAY → matchmaking → OPPONENT FOUND → loading → gameplay → ball at tee → fires putt → ball reaches `InCup`.
- Captures screenshot at each transition (8 frames + 1 log).
- Self-destructs cleanly after run. `#if UNITY_EDITOR` guarded throughout.

---

## Pre-flight (implementer logs in IMPLEMENTER_REPORT.md)

1. **Confirm CaptureCore canonical path is current.** Recent C0 work surfaced that EditMode capture has issues, but the bot runs in **PlayMode** — `CaptureCore.SnapPlayModeSafe` is the sanctioned path. Verify the signature hasn't changed:
   ```
   grep -n 'public static.*SnapPlayModeSafe' Assets/Scripts/Capture/CaptureCore.cs
   ```

2. **Confirm production button names.** The bot drives the production UI by finding buttons. Verify text or names:
   ```
   grep -n '"PLAY"\|playButton' Assets/Scripts/UI/HomeScreenController.cs | head -10
   grep -n 'matchmaking\|cancelButton' Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs | head -10
   ```

3. **Confirm fire-to-cup harness availability.** `SmokeRunner2fHost` uses `PhysicsLabController.FireShotForSmokeRunner(...)` or similar. The bot needs a way to fire a putt with a known direction/power. Either reuse the existing test seam or add a minimal new one. Check what's available:
   ```
   grep -n 'public.*Fire\|public.*ForSmokeRunner\|public.*SetAim' Assets/Scripts/Physics/Viewer/PhysicsLabController.cs | head -20
   ```

---

## Architecture

**Lives in:** `Assets/Scripts/Physics/Viewer/` (same asmdef as the §2c-§2f smoke runners — `Golfin.Physics.Viewer`). All files `#if UNITY_EDITOR` guarded.

**Two files, mirroring the §2f pattern:**

### File 1: `LoopV2SmokeBot.cs` (host MonoBehaviour)

Lives on a temp GameObject in ShellScene at play-mode entry. SessionState-armed flag prevents accidental fires. `Awake` checks the armed flag, sets it false, starts the coroutine. `Start` is empty (matches §2f host).

Coroutine sequence:

| # | Action | Capture | MD5-distinct? |
|---|---|---|---|
| 0 | Wait `StartupWait` (5s realtime) for ShellScene boot + persistent UI settle | — | — |
| 1 | Verify Home screen active. Find `playButton`. | `s01_home.png` | ✓ frame at rest |
| 2 | Click `playButton` via `Button.onClick.Invoke()` | — | — |
| 3 | Wait for matchmaking modal visible (poll up to 10s realtime) | `s02_matchmaking_searching.png` | ✓ animated overlay frame |
| 4 | Wait for "OPPONENT FOUND" state (poll for `MatchmakingModalController._state == OpponentFound` or text contains "FOUND") | `s03_opponent_found.png` | ✓ different frame |
| 5 | Wait for `GameplaySceneLoader` BeginGameplayLoad to fire (poll `SceneManager.GetSceneByName("LabScaffold").isLoaded`) | `s04_loading.png` | ✓ loading screen visible |
| 6 | Wait for hole scene loaded (poll `SceneManager.GetSceneByName("Hole_01_Geo").isLoaded`) + 2s realtime settle | `s05_gameplay_armed.png` | ✓ ball at tee |
| 7 | Fire a putt via the controller test seam (direction: toward pin, power: high enough to reach cup). Use `BallAnimator.PlayRate=Instant` per §2f lesson | — | — |
| 8 | Wait for ball state = `InCup` (poll up to 25s realtime) | `s06_ball_in_cup.png` | ✓ post-cup frame |
| 9 | Wait 2s for result modal animation (lab `HoleCompleteWidget` or eventual ShellScene Result modal from C1) | `s07_result_modal.png` | ✓ |
| 10 | Write `s08_history.log` — turn count + state transitions + `GameSession` final values + scene names loaded | log | — |
| 11 | Self-destruct (`Destroy(this)`) | — | — |

**Failure modes** (log and continue if possible):
- Home not active → log error, capture `s01_FAIL_home_not_active.png`, abort.
- PlayButton not found → log error, abort.
- Matchmaking modal never appears (10s timeout) → log + capture current screen, abort.
- OPPONENT FOUND never fires (15s timeout) → log + capture, abort.
- Hole_01_Geo never loads (15s timeout from BeginGameplayLoad) → log + capture, abort.
- Ball never reaches `InCup` (25s timeout from fire) → log + capture, continue to result-modal step anyway (still useful evidence for "putt didn't go in" diagnostics).
- Any caught exception → log full stack, capture screen, abort.

All aborts still write the log file with what was captured, and still self-destruct.

### File 2: `Editor/LoopV2SmokeBotMenu.cs` (launcher)

Mirror of `SmokeRunner2fMenu`:
- Refuses to run if `EditorApplication.isPlaying` already
- Opens `ShellScene.unity` via `EditorSceneManager.OpenScene`
- Attaches `LoopV2SmokeBot` to a temp GameObject named `[SmokeBot]`
- Arms the bot via `SessionState`
- `EditorApplication.delayCall` saves scene + enters play mode (matches §2f exception: if `delayCall` is swallowed by Unity-MCP, fall back to `editor-application-set-state isPlaying:true` per Lesson Q architect-driven pattern — note in IMPLEMENTER_REPORT)

Menu path: `GOLFIN/Smoke/Loop v2 Production Playthrough (Hole_01)`

---

## Why this stays TELLCODE and not full pipeline

- **No new architecture** — it's a smoke runner, the pattern has shipped 5 times before (`2c`, `2d`, `2e`, `2f`, `PutterCone`).
- **No production code edits** — the bot reads existing public APIs (Button.onClick, GameSession, SceneManager).
- **Already-canonical capture path** — `CaptureCore.SnapPlayModeSafe`.
- **No tests required** — the bot IS the test. Its capture outputs ARE the evidence.

If the implementer hits a missing test seam in `MatchmakingModalController` (e.g. `_state` is private and no public observable equivalent exists), THAT minor seam-add can be in scope here, but flag in IMPLEMENTER_REPORT as a scope-extension. Anything bigger → BLOCKED, escalate.

---

## Scope

### Files CREATED

- `Assets/Scripts/Physics/Viewer/LoopV2SmokeBot.cs` (~250 lines, mirrors `SmokeRunner2fHost.cs`)
- `Assets/Scripts/Physics/Viewer/Editor/LoopV2SmokeBotMenu.cs` (~80 lines, mirrors `SmokeRunner2fMenu.cs`)

### Files POTENTIALLY EDITED (only if test seam missing)

- `Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs` — public read-only property `MatchmakingState State { get; }` exposing internal `_state`. ONLY if no equivalent observable exists. Flag in report.
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — if no public putt-fire test seam exists; reuse §2f's `BallAnimator.PlayRate` lesson.

### Files DELETED

None.

---

## Implementation steps

1. **Pre-flight checks** (above), log results.
2. **Create `LoopV2SmokeBot.cs`** with the full coroutine sequence + abort paths.
3. **Create `LoopV2SmokeBotMenu.cs`** with the open-scene + arm + enter-play sequence.
4. **Test seam audit** — only if pre-flight #2/#3 found a missing API, add the minimal seam needed.
5. **Compile clean.**
6. **Run the bot once locally** (implementer-driven, via the new menu). All 8 captures should be MD5-distinct. Log should show clean turn count + `InCup` terminal state.
7. **Captures live at** `tasks/loop_v2_smoke_bot/screenshots/`. Commit them with the spec.
8. **Commit + push.** Message: `loop_v2_smoke_bot: production-flow playthrough bot for visual gates`

---

## Definition of Done

**Audit grep:**
- [ ] `ls Assets/Scripts/Physics/Viewer/LoopV2SmokeBot.cs` → exists
- [ ] `ls Assets/Scripts/Physics/Viewer/Editor/LoopV2SmokeBotMenu.cs` → exists
- [ ] `grep -c 'CaptureCore.SnapPlayModeSafe\|SnapAtEndOfFrameAndPause' Assets/Scripts/Physics/Viewer/LoopV2SmokeBot.cs` → at least 8 hits (one per capture step)
- [ ] `grep -c '#if UNITY_EDITOR' Assets/Scripts/Physics/Viewer/LoopV2SmokeBot.cs Assets/Scripts/Physics/Viewer/Editor/LoopV2SmokeBotMenu.cs` → both guarded
- [ ] Project compiles clean
- [ ] EditMode test gate **305/305 PASS** unchanged (no new tests, no regressions)

**Self-evidence:**
- [ ] 8 PNGs in `tasks/loop_v2_smoke_bot/screenshots/`, all MD5-distinct (`md5sum` on each)
- [ ] `s08_history.log` shows: bot started, each transition logged with timestamp, ball reached `InCup`, turn count > 0, `GameSession.CurrentHoleNumber == 1`
- [ ] Visual spot-check on `s07_result_modal.png` shows the result modal (the lab one for now; Stage C1 will swap to ShellScene Result modal and re-run the bot to verify)

**Cesar visual gate:** light. Cesar reviews the 8 captures + log; if the playthrough flow looks right, approve. No manual play required.

---

## Handoff

**Implementer:** Claude Code (TELLCODE).
**Spec:** `Docs/Specs/Active/loop_v2_smoke_bot/SPEC.md`
**Architect-side close:** STATUS.md → DONE, move folder to `Docs/Specs/Completed/`, flip Notion entry to Done, set Closed date. Memory note: this bot becomes the **default visual gate** for Stages C1/D/E/F — implementer should re-run the bot for each subsequent stage's gate and Cesar reviews captures rather than playing manually.

---

## Out of scope (deferred to other stages)

- **Multi-hole runs** — bot ships with Hole 1 only. Stage E SPEC may extend to a multi-hole bot variant if needed (parameterize `holeNumber`).
- **PLAY NEXT / MENU button presses** — Stage D adds those buttons; bot extension to drive them lives in Stage D's SPEC if needed.
- **FAILED-state playthrough** — bot only tests SUCCESS path. A FAILED-path bot (hit ball OB or run out of strokes) is its own future spec if needed.
- **Production builds** — `#if UNITY_EDITOR` guarded; never ships in player builds.
- **Bot as automated CI test** — current scope is editor-menu-driven. A CI-runnable variant (via `-batchmode -executeMethod`) is a future spec if/when CI is set up.

---

## Risk register

| # | Risk | Mitigation |
|---|---|---|
| 1 | Hole_01_Geo content lacks a clean putt-to-cup line from tee | Use existing §2f putt fire parameters (those work on Hole_01_Geo); if not, file a content task and bot ships with whatever putt fires |
| 2 | `MatchmakingModalController` internal state has no observable | Add minimal public `State` getter; flag in report |
| 3 | Bot self-destruct races with capture completion | Mirror §2f's `CaptureWait` constant (1.5s) before destroying; pattern is proven |
| 4 | Bot runs in a Unity-MCP-frozen-time session | `WaitForSecondsRealtime` (not `WaitForSeconds`) per §2f Lesson; bot inherits the timeScale=1 guard from §2f host |
| 5 | Captures land in stale-RT trap (Lesson K) | Use canonical `CaptureCore.SnapPlayModeSafe` only; no `screenshot-game-view`, no `AssetDatabase.Refresh()` |
| 6 | Test seam additions creep beyond minimal | Implementer reports seam additions in IMPLEMENTER_REPORT §2 explicitly; if >2 production files touched, escalate to architect |
