# Bot Framework — `loop_v2_smoke_bot`

A reusable in-editor bot that drives the GOLFIN app **like a real player** — clicking real
buttons, waiting on real screens/modals, and playing real physics shots through the
production shot interface. Built as the Loop v2 smoke bot; **intended as the base for
multiplayer bots.**

This document is the handover guide: architecture, how to run it, how to extend it, the
hard-won gotchas, and where to take it for multiplayer.

---

## 1. What it is / why it exists

Every Loop v2 stage carries a visual gate that otherwise costs ~30 min of manual play per
iteration. The bot replaces that: a scenario drives the production UI end-to-end and
produces screenshot + video evidence unattended. The framework is deliberately generic —
it drives *any* UI, not just golf — so new scenarios are 30–50 lines each.

**Status:** shipped. Three scenarios pass; demo videos approved (`Docs/Videos/`).

---

## 2. Architecture — four files

All under `Assets/Scripts/Physics/Viewer/Bot/`, asmdef `Golfin.Physics.Viewer`, every file
`#if UNITY_EDITOR`-guarded (the bot is an editor-only tool, never ships in a player build).

| File | Role |
|---|---|
| `BotDriver.cs` | **The framework.** Reusable primitives — click, wait, capture, navigate, shoot. No scenario logic. |
| `Scenarios.cs` | **The scenario library.** Thin static coroutines composing primitives. One per flow. |
| `LoopV2SmokeBot.cs` | **The host MonoBehaviour.** Lives only in play mode; runs the selected scenario coroutine; self-destructs. |
| `Editor/LoopV2SmokeBotMenu.cs` | **The launcher.** `GOLFIN/Smoke/Loop v2/*` menu items; arms + enters play mode; injects the host. |

Driver/Scenario split is the reusability contract: a new gate = a new coroutine in
`Scenarios.cs`, never a new bot file.

---

## 3. Lifecycle — how a run works

```
GOLFIN/Smoke/Loop v2/<scenario>  (menu click, or script-execute the menu method)
  └─ LoopV2SmokeBotMenu.Launch(scenarioKey)
       1. EditorSceneManager.OpenScene(ShellScene, Single)   — never saved to disk
       2. SessionState: Armed=true, Scenario=<key>
       3. Temporarily clear EnterPlayModeOptions.DisableSceneReload (restored on exit)
       4. EditorApplication.EnterPlaymode()
  └─ OnPlayModeStateChanged(EnteredPlayMode)   [re-registered every domain reload]
       5. Application.runInBackground = true    — CRITICAL, see §6
       6. new GameObject("[LoopV2SmokeBot]") + AddComponent<LoopV2SmokeBot>()
          (created in the play-mode scene, never saved → zero scene contamination)
  └─ LoopV2SmokeBot.Start()
       7. verify Armed, clear it (prevents re-entry on domain reload), timeScale guard
       8. StartCoroutine(SafeRun())
  └─ SafeRun()
       9. WaitForSecondsRealtime(5) startup settle
      10. dispatch on Scenario key → Scenarios.<X>(driver)
      11. RunWithCatch — drives the scenario enumerator with try/catch around MoveNext
          (C# can't try/catch across yield; this pattern can)
      12. FlushLog → tasks/loop_v2_smoke_bot/<scenario>/screenshots/history.log
      13. Destroy(gameObject); EditorApplication.ExitPlaymode()
```

Key design points:
- **Armed flag in `SessionState`** survives domain reloads (a compile between the menu
  click and play-mode entry won't lose the scenario). The handler is a no-op unless armed.
- **The host is injected at `EnteredPlayMode`, never saved.** Earlier iterations baked
  `[LoopV2SmokeBot]` GameObjects into `ShellScene.unity` — a hard scene-contamination bug.
  The fix: create it in-memory at play-mode entry only. `git diff` on `ShellScene.unity`
  must always be empty.
- **`Destroy(gameObject)`, not `Destroy(this)`** — destroying only the component leaks the
  GameObject.

---

## 4. BotDriver primitives

Construct with a capture directory: `new BotDriver("tasks/loop_v2_smoke_bot/<scenario>/screenshots")`.
All waits use `WaitForSecondsRealtime` / `Time.unscaledDeltaTime` so they survive a frozen
`timeScale`.

**UI interaction**
- `Button FindButton(nameOrText)` — match by GO name (exact/ci) or child `TMP_Text` substring. Warns on ambiguity.
- `IEnumerator Click(nameOrText, settleSeconds)` — invoke a button's `onClick`.
- `IEnumerator TypeInto(inputFieldName, text)`, `string ReadText(name)`
- `IEnumerator SetSliderValue(name, value)`, `IEnumerator SetToggle(name, on)`

**Waiting**
- `WaitFor(predicate, desc, timeout)` — generic predicate poll.
- `WaitForScreen(screenNameOrId, timeout)` — ScreenManager current screen.
- `WaitForModalVisible / WaitForModalHidden(modalName, timeout)`
- `WaitForGameObject(goName, timeout)`, `WaitForSceneLoaded(sceneName, timeout)`

**Navigation / capture**
- `NavigateToHome(totalTimeout)` — Logo → Splash (clicks StartButton) → Loading → Home.
- `Capture(label)` — screenshot via `CaptureCore.SnapPlayModeSafe` (the only sanctioned
  capture path); auto-prefixes `s01_`, `s02_`, … and copies into the scenario folder.
- `LogStep(msg)` / `FlushLog()`

**Gameplay**
- `PlayHoleToCup(int par)` — play a hole with real shots (see §5).
- `FireShot(worldTarget, power, timeout)` — single §2f-pattern lab shot (places ball, fires preset).
- `ForceShotComplete(stateName)` — the test seam (see §5).
- `WaitForBallState(stateName, timeout)`, `FindCupPosition()`

**Cross-assembly bridge** — `BotDriver`'s asmdef (`Golfin.Physics.Viewer`) cannot
statically reference Assembly-CSharp types (`ScreenManager`, `MatchmakingModalController`).
Those are reached via `Type.GetType("...,Assembly-CSharp")` + reflection
(`GetCurrentScreenName()`, `GetMatchmakingPhase()`). Same-asmdef and
`Golfin.Gameplay.*` types are referenced directly.

---

## 5. Playing a hole — `PlayHoleToCup`

`PlayHoleToCup(par)` plays the current hole with **real physics shots through the
production shot interface**, so it looks exactly like a player and the shot UI behaves
correctly.

Per stroke:
1. Distance = horizontal `ballPos → cupPos` (`FindCupPosition()` reads
   `HoleContext.PinWorld`).
2. `SelectShot(dist, isFirstStroke, …)` → club + power + label. **Driver only on stroke 1**;
   later strokes pick Wedge / Putter by remaining distance.
3. `PhysicsLabController.SetClub(index)` — `0 Driver, 1 Iron 7, 2 Wedge, 3 Putter`
   (`PutterIndex = LabClubs.Length-1`). `SetClub` raises `ClubSelectionBroadcast` so the
   shot UI / cone update.
4. `SetCameraYawRadians(atan2(dz,dx))` — aim. `RunSimForCamera` fires in the camera-yaw
   direction from the ball's current rest position.
5. **Fire through the production drag path** (mirrors `ClubHandleDragger` — a real player):
   ```
   shotController.BeginExternalDrag();                 // Idle → Aiming
   for ~0.85s: SetExternalPower(Lerp(0→power), 0);     // club handle visibly pulls DOWN
   SetExternalPower(power, 0); brief hold;
   shotController.EndExternalDrag();                    // Timing → CommitFlick → Resolving → fires
   ```
6. Subscribe `BallStateMachine.OnShotComplete`, wait for terminal state, capture a still.
7. Loop until `InCup`, or until **par + 3 strokes** — then the `ForceShotComplete("InCup")`
   seam finishes the hole as a safety net.

**Why the drag path matters.** Firing via `PhysicsLabController.Fire(preset)` or
`ShotController.FireDebugShot()` works physically but is *instant* — it bypasses the
`ShotController` state ramp, so the cone / ball / club-handle never hide and the handle
never animates. Driving `BeginExternalDrag → SetExternalPower → EndExternalDrag` runs the
real `ShotController` state machine: `ConeAlphaController` fades the cone canvas (cone +
handle) to alpha 0 on `Resolving`, `CentralBallWidget` deactivates, and `ShotConeView`
moves the club handle by `PowerNormalized` every frame. **Always drive a player-facing UI
through its real input path, not a debug seam, when the visuals must look real.**

### The shot-complete seam

`BallStateMachine.ForceShotCompleteForBot(BallState terminal)` — an editor-only seam that
fires the **same** `OnShotComplete` event production fires. Five-condition seam principle
(from `ARCHITECT_VERDICT_INCUP.md`): (i) isolates one unit of behavior; (ii) the real path
stays default; (iii) `#if UNITY_EDITOR`-guarded; (iv) `_ForBot`-suffixed (grep-visible);
(v) delegates to the same production entry point. Use it only for gates *downstream* of
terminal-state observation (modal wiring, progression) — never as the default shot path.

---

## 6. Hard-won gotchas — read before extending

- **`Application.runInBackground = true` is mandatory.** When the Unity Editor is not the
  foreground OS app — i.e. every automated/MCP-driven run — Unity throttles the play-mode
  loop to a halt: the game freezes at frame 1, `Time.time` stuck near 0, while
  `EditorApplication.update` keeps ticking (so MCP still responds — misleading). The
  launcher sets it at `EnteredPlayMode`; it is a runtime flag, reverts on play-exit, and
  leaves zero `ProjectSettings.asset` footprint. Without it the bot does nothing.
- **Capture only via `CaptureCore.SnapPlayModeSafe`.** Never `ScreenCapture.CaptureScreenshot(path)`
  (async, fails when paused). Never invent a per-task capture path.
- **Never bake bot objects into a scene.** Inject at play-mode entry, never save. Audit
  with `git diff Assets/Scenes/ShellScene.unity` — must be empty.
- **MCP server can die.** The `unity-mcp-server` (port 21573) is a child process; if MCP
  calls fail with "Unable to connect", relaunch:
  `cd Library/mcp-server/<platform> && nohup ./unity-mcp-server port=21573 plugin-timeout=10000 client-transport=streamableHttp authorization=none > /tmp/unity-mcp-server.log 2>&1 &`
- **Control play mode via `editor-application-set-state`**, never simulated keystrokes
  (a fullscreen Game View eats them).
- **A long synchronous `script-execute` blocks the main thread** → the game loop can't
  advance. Enter play mode, let the bot run autonomously, poll with separate short calls.

---

## 7. Adding a scenario

1. Add a static `IEnumerator <Name>(BotDriver d)` coroutine to `Scenarios.cs` (compose
   primitives; 30–50 lines).
2. Add a `case "<key>":` to the dispatch switch in `LoopV2SmokeBot.SafeRun()`.
3. Add a `[MenuItem("GOLFIN/Smoke/Loop v2/<Name>")]` + its validate function in
   `LoopV2SmokeBotMenu.cs`.

That's it — the launcher, host lifecycle, capture, and logging are all shared.

---

## 8. Demo video pipeline (`BotFrameRecorder` + `build_bot_video.py`)

Bot runs can be assembled into captioned demo videos. The pipeline is split so that ALL
encoding and captioning is data-driven (ffmpeg), not baked into the engine. This replaces
the original temporary `BotVideoRecorder.cs` (in-engine `MediaEncoder` + a live caption
canvas — removed after the first demos; that approach was custom and cumbersome).

**Capture (Unity) — `Assets/Scripts/Physics/Viewer/Bot/BotFrameRecorder.cs`**
- A companion MonoBehaviour injected by `LoopV2SmokeBotMenu` at `EnteredPlayMode`, but
  ONLY when `BotFrameRecorder.RecordVideo` is armed (SessionState; clears itself on
  `Start`, like `LoopV2SmokeBot.Armed`). In-memory, never saved to a scene.
- Each frame: `yield return new WaitForEndOfFrame()` **(mandatory** —
  `CaptureScreenshotAsTexture` returns null otherwise), then `CaptureCore.SnapPlayModeSafe`
  dumps a PNG to `Docs/Diagnostics/_capture/botframe_NNNNN_*.png`. Only frames that
  produced a real file are counted, so the manifest stays 1:1 with the PNGs.
- On play-mode exit writes `tasks/loop_v2_smoke_bot/<scenario>/video/frames_manifest.csv`
  (per-frame `Time.realtimeSinceStartup` — the SAME clock `BotDriver.LogStep` uses, so
  captions sync exactly).
- It ONLY dumps frames — no MediaEncoder, no in-game caption canvas.

**Assemble (ffmpeg) — `Docs/Scripts/build_bot_video.py`**
- `python3 Docs/Scripts/build_bot_video.py --scenario <key> [--title "..."] [--keep-frames]`
- Reads the manifest + the bot's `history.log`, builds an ffmpeg concat list with
  per-frame real-time durations, derives `drawtext` captions from the log's
  `Click: '<name>'` lines, encodes `Docs/Videos/<scenario>_stageF_buttons.mp4`, and
  deletes the PNG frames (unless `--keep-frames`).
- Captions are data — edit `parse_captions` in the script to recaption; no Unity rebuild.

**Arming a recorded run** (e.g. via MCP `script-execute`):
`BotFrameRecorder.RecordVideo = true; LoopV2SmokeBotMenu.RunSettingsRoundTrip();`

**Requires** `ffmpeg` + `ffprobe` on PATH or in `~/.local/bin` (no Homebrew needed —
static builds from evermeet.cx work).

**Known limitation:** capture runs ~8–12 fps (PNG-encode bound), so the 0.12 s
`ButtonPressFeedback` pulse is only marginally sampled. For a pulse-focused showcase,
drop the Game View resolution before the run to lift the capture frame rate.

---

## 9. Taking it to multiplayer bots

The framework already drives "any UI like a real player" — that is exactly the multiplayer
bot contract. To extend:

- **New scenarios, same primitives.** A multiplayer flow (matchmaking against a real
  opponent, lobby, turn handoff, rematch) is just another `Scenarios.cs` coroutine. The
  matchmaking primitives (`WaitForModalVisible`, `GetMatchmakingPhase`) already exist.
- **Multiple bot instances.** Today one `[LoopV2SmokeBot]` host runs one scenario. For
  bot-vs-bot, generalise the host to support N concurrently, or run a bot as a headless
  second client. The driver primitives are instance-safe (no static run state).
- **Turn-based play.** `PlayHoleToCup` is single-actor; a multiplayer hole alternates
  strokes. Factor the per-stroke block (`SelectShot` → aim → drag-fire → wait
  `OnShotComplete`) into a `PlayOneStroke()` primitive the turn loop can call per player.
- **Production path, always.** Keep firing through `ShotController`'s drag path so an
  observing client sees correct UI. The `_ForBot` seam stays a downstream-gate-only escape
  hatch.
- **Determinism.** The physics is deterministic — a fixed aim+power sequence reproduces
  exactly. Tune a bot "skill" by perturbing aim/power, not by re-rolling physics.

---

## 10. File reference

| Path | Role |
|---|---|
| `Assets/Scripts/Physics/Viewer/Bot/BotDriver.cs` | Primitives (framework) |
| `Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` | Scenario library |
| `Assets/Scripts/Physics/Viewer/Bot/LoopV2SmokeBot.cs` | Play-mode host |
| `Assets/Scripts/Physics/Viewer/Bot/Editor/LoopV2SmokeBotMenu.cs` | Launcher + menu items |
| `Assets/Scripts/Gameplay/Loop/BallStateMachine.cs` | `ForceShotCompleteForBot` seam |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | `Fire`, `FireViaShotController`, `SetClub`, `SetCameraYawRadians`, `BallPosition` |
| `Assets/Scripts/Gameplay/Input/ShotController.cs` | Production shot state machine + `BeginExternalDrag/SetExternalPower/EndExternalDrag` |
| `Assets/Scripts/Gameplay/UI/ShotUI/` | Cone, club handle, `ConeAlphaController`, `ClubHandleDragger` |
| `Docs/Specs/Completed/loop_v2_smoke_bot/` | Original spec + review history |
| `Docs/Videos/` | Approved demo videos |
