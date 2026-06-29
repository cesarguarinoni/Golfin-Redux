# Architect Review — `tournament_round_loop` (T6)

**Verdict:** ARCHITECT_REVIEW_FAIL — single open item: **§12.1 acceptance video**.
Everything else PASSES (loop built, 12/12 tests, locked labels fixed, CLOSE→bottom fixed by architect, 100 RP debit verified). The video is the only remaining gate, and Cesar has directed that **the pipeline must produce it — NOT Cesar manually**.

---

## The blocker was a misdiagnosis, not an impossibility

iter-1/iter-2 claimed the video "requires adding a scenario to `Scenarios.cs`, banned by Rule 7." Correct that you cannot touch `Scenarios.cs` — it lives under `Assets/Scripts/Physics/` (ZERO-edit ban). But that does **not** make the video impossible. `BotDriver` (`Assets/Scripts/Physics/Viewer/Bot/BotDriver.cs`) already exposes **public** primitives that do everything the video needs, and they can be invoked from a NEW host authored **outside** `Physics/`:

| Primitive (public on `BotDriver`) | Use |
|---|---|
| `IEnumerator Click(string nameOrText, float settle=0.8f)` | tap a real button by name/label |
| `IEnumerator WaitForScreen(string idOrName, float timeout=15)` | wait for a `ScreenId` |
| `IEnumerator WaitForModalVisible(string modalName, ...)` / `WaitForModalHidden(...)` | Signup modal in/out |
| `IEnumerator WaitForAnyHoleGeoScene(float timeout=40)` | gameplay scene loaded |
| **`IEnumerator PlayHoleToCup(int par, float firstStrokePowerOverride=0)`** | **plays a full hole to the cup — this is the golf-playing part you thought was impossible** |
| `IEnumerator NavigateToHome(...)`, `string GetCurrentScreenName()`, `Vector3 FindCupPosition()` | nav + assertions |

## Authorized capture path (architect-approved non-Physics route)

Author a **NEW editor-only capture harness OUTSIDE `Assets/Scripts/Physics/`** — e.g.
`Assets/Scripts/Editor/Tournaments/TournamentLoopCaptureHarness.cs` (+ menu item) — that:

1. References the `Golfin.Physics.Viewer` asmdef so it can `new BotDriver(captureDir)` and call the primitives above. (Asmdef reference, NOT a source edit to Physics — fully allowed.)
2. Is armed + launched the same way `LoopV2SmokeBotMenu` arms `LoopV2SmokeBot`: a menu item sets a `SessionState` flag + enters play; a `playModeStateChanged` hook on EnteredPlayMode creates a host GO that runs the coroutine below; arm `BotVideoRecorder.Begin()`/`End()` around it for the 1170×2532 capture (reuse `BotVideoRecorder` verbatim — it already forces the iPhone-14 preset and the one-record-per-session GPU guard).
3. Coroutine drives the **REAL ShellScene tournament flow** (no synthetic buttons):
   ```
   yield NavigateToHome()
   yield Click("<Tournaments nav/home entry>")            // reach Tournament Selection
   yield WaitForScreen("TournamentSelection")
   yield Click("<kasumigaseki_open card CTA — 'SIGN UP'>") // real card onClick → opens Signup modal
   yield WaitForModalVisible("TournamentSignupModal")
   yield Click("CONFIRM")                                  // 100 RP debit + register + → HoleSelection
   yield WaitForModalHidden("TournamentSignupModal")
   yield WaitForScreen("TournamentHoleSelection")          // Hole 1 = NEXT
   yield Click("PLAY")                                     // NEXT card → BeginTournamentHole(hole1)
   yield WaitForAnyHoleGeoScene()
   yield PlayHoleToCup(par: 5)                             // hole 1 (Lomond Hole 1 = Par 5)
   yield WaitForScreen("TournamentHoleSelection")          // back; Hole 1 Finished, Hole 2 = NEXT
   yield Click("PLAY")                                     // Hole 2
   yield WaitForAnyHoleGeoScene()
   yield PlayHoleToCup(par: 4)                             // hole 2 (Lomond Hole 2 = Par 4)
   yield WaitForScreen("TournamentLeaderboard")            // real strokes ranked
   ```
   (Confirm the exact button names/labels against the live scene — use `BotDriver.FindButton` semantics; the kasumigaseki CTA text is "SIGN UP", the NEXT-card action button label is "PLAY", modal confirm label is "CONFIRM".)

## Pre-set demo state (already done by architect — do NOT re-do)
- `tournaments.csv`: kasumigaseki `entryFeeRP = 100`.
- Save state RESET on disk: kasumigaseki has **no entry** → its card boots in **"SIGN UP"** state. (If you register during a dry run, clear it again before the recorded run: remove the row from `SaveDataHost.Instance.Data.tournamentEntries` + `FlushNow()`.)

## Deliverable
- `Docs/Specs/Active/tournament_round_loop/videos/tournament_round_loop.mp4` (1170×2532), captioned per the standing rule via `Docs/Scripts/build_bot_video.py`. Bot raw lands in `tasks/loop_v2_smoke_bot/<scenario>/video/raw.mp4` → copy to the task `videos/` folder.
- Verify Y-orientation by decoding CONSECUTIVE frames (not `-ss` keyframe sampling — see `reference_video_flip_verification`).
- Flip §12.1 to PASS in `IMPLEMENTER_REPORT.md` with the absolute video path, then set STATUS to `READY_FOR_SELF_REVIEW`.

## Guardrails
- BotVideoRecorder allows **one record per Editor session** (GPU-wedge guard). Do your dry-run WITHOUT recording first; arm the recorder only for the final clean pass. If you need a second recorded take, relaunch Unity.
- If `PlayHoleToCup` can't sink within `par+N` on a tournament-launched hole, surface it — do NOT fabricate. It works for practice/1v1 holes; the tournament boot uses the same `BeginGameplayLoad` path so it should behave identically.
- Do NOT add a `Scenarios.cs` coroutine. Do NOT edit anything under `Assets/Scripts/Physics/`. The harness is a brand-new file outside that tree.
