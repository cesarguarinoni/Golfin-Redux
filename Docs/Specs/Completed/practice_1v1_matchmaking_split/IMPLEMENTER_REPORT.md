# Implementer Report — `practice_1v1_matchmaking_split`

**Iteration:** 3

## Rejection follow-up

Addressing both defects from `CESAR_REJECTION.md`.

### Defect 1 — Cancel on matchmaking modal resurrects dead NextHolePanel (RESOLVED / GONE)

**Root cause (confirmed):** `MatchmakingModalController.OnHide()` and `OnDisable()` unconditionally called `SetActive(true)` on `homeNextHolePanel`. When the modal was opened from the Mode Select carousel (where `NextHolePanel` was already inactive / `m_IsActive: 0`), pressing Cancel triggered `SetActive(true)`, resurrecting the panel behind the carousel.

**Fix applied — `Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs`:**

Three changes in one file:

1. Added two bool fields (capture prior active-state before hiding):
```csharp
// Captured prior active-state of home panels so Cancel/OnDisable restores
// them to what they were before OnShow hid them — not unconditionally true.
private bool _noticeWasActive;
private bool _nextHoleWasActive;
```

2. `OnShow()` — capture BEFORE hiding:
```csharp
_noticeWasActive   = homeNoticePanel   != null && homeNoticePanel.activeSelf;
_nextHoleWasActive = homeNextHolePanel != null && homeNextHolePanel.activeSelf;
if (homeNoticePanel != null)   homeNoticePanel.SetActive(false);
if (homeNextHolePanel != null) homeNextHolePanel.SetActive(false);
```

3. `OnHide()` and `OnDisable()` — restore to captured value (not unconditionally `true`):
```csharp
if (homeNoticePanel != null)   homeNoticePanel.SetActive(_noticeWasActive);
if (homeNextHolePanel != null) homeNextHolePanel.SetActive(_nextHoleWasActive);
```

**Bot verification — `matchmaking_1v1_cancel_gate` scenario (iter-3 run):**
```
[t=28.54] Click: 'CancelButton'
[t=29.20]   WaitForModalHidden OK: 'MatchMakingModal' hidden after 0.0s
[t=30.96] [Matchmaking1v1CancelGate] NextHolePanel.activeInHierarchy=False (expected: false)
[t=30.96] === Matchmaking 1v1 Cancel Gate: PASS — Cancel returns to Mode Select carousel; NextHolePanel stays deactivated ===
```

Post-Cancel screenshot: `screenshots/cancel_gate_s03_post_cancel_home_2026-06-06.png` (1170x2532)

**Verdict: GONE** — `NextHolePanel.activeInHierarchy=False` confirmed by bot assertion at runtime.

---

### Defect 2 — Bot videos recorded at 250x540 instead of iPhone 14 full resolution (RESOLVED)

**Root cause:** `LoopV2SmokeBotMenu.RunPracticeFlowGate()` and `RunMatchmaking1v1Gate()` called `BotVideoRecorder.Arm(BotVideoRecorder.CaptureProfile.GameplayCapped)`. `GameplayCapped` resizes to 250x540 to avoid macOS GPU kernel panics on 3D rendering. Menu-only scenarios don't need the safety cap.

**Fix applied — `Assets/Scripts/Physics/Viewer/Bot/Editor/LoopV2SmokeBotMenu.cs`:**

Changed both launchers and added the new cancel gate launcher:
```csharp
// Practice Flow Gate — was GameplayCapped (250x540), now MenuNative (1170x2532)
BotVideoRecorder.Arm(BotVideoRecorder.CaptureProfile.MenuNative);

// Matchmaking 1v1 Gate — was GameplayCapped (250x540), now MenuNative (1170x2532)
BotVideoRecorder.Arm(BotVideoRecorder.CaptureProfile.MenuNative);

// Matchmaking 1v1 Cancel Gate (new in iter-3) — MenuNative (1170x2532)
BotVideoRecorder.Arm(BotVideoRecorder.CaptureProfile.MenuNative);
```

**Verification:**
- `practice_flow_gate.mp4`: 1170x2532, 13.1MB, 93.5s — confirmed by ffprobe
- `matchmaking_1v1_gate.mp4`: 1170x2532, 3.5MB, 21.3s — confirmed by ffprobe
- `matchmaking_1v1_cancel_gate.mp4`: 1170x2532, 2.6MB, 15.2s — confirmed by ffprobe

Videos: `screenshots/practice_flow_gate_s03_gameplay.png` (4.1MB = 1170×2532) and `screenshots/matchmaking_1v1_gate_s04_gameplay.png` (3.9MB = 1170×2532) confirm full-res stills also.

**Verdict: RESOLVED** — all three task videos now 1170x2532.

---

## Implementation summary (iter-3 changes on top of iter-1/2)

**New in iter-3:**

- `MatchmakingModalController.cs`: `_noticeWasActive`/`_nextHoleWasActive` capture pattern — fixes Cancel resurrection of `NextHolePanel`
- `BotDriver.cs`: added `IsNextHolePanelActive()` method for cancel gate assertion
- `Scenarios.cs`: added `Matchmaking1v1CancelGate` coroutine
- `LoopV2SmokeBot.cs`: added `"matchmaking_1v1_cancel_gate"` switch case
- `LoopV2SmokeBotMenu.cs`: changed Practice/1v1 launchers to `MenuNative`; added Cancel Gate menu item + validator
- Three videos re-recorded at 1170×2532

**Prior iters (unchanged in iter-3):** `HoleSelectionScreenController.cs`, `HoleSelectionAutoWire.cs`, `ModeCarouselController.cs`, `ModeSelectScreenController.cs`, `ShellScene.unity` — core routing logic from iter-1.

---

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs` | Modified (iter-3) — `_noticeWasActive`/`_nextHoleWasActive` capture pattern in `OnShow`/`OnHide`/`OnDisable` |
| `Assets/Scripts/Physics/Viewer/Bot/BotDriver.cs` | Modified (iter-3) — added `IsNextHolePanelActive()` |
| `Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` | Modified (iter-3) — added `Matchmaking1v1CancelGate` coroutine |
| `Assets/Scripts/Physics/Viewer/Bot/LoopV2SmokeBot.cs` | Modified (iter-3) — added `matchmaking_1v1_cancel_gate` switch case |
| `Assets/Scripts/Physics/Viewer/Bot/Editor/LoopV2SmokeBotMenu.cs` | Modified (iter-3) — Practice/1v1 launchers now `MenuNative`; added Cancel Gate launcher + validator |
| `Assets/Scripts/UI/HoleSelection/HoleSelectionScreenController.cs` | Modified (iter-1) — `HandleActionClicked` routes Practice directly, no matchmaking modal |
| `Assets/Scripts/UI/HoleSelection/Editor/HoleSelectionAutoWire.cs` | Modified (iter-1) — removed matchmaking wiring from Practice side |
| `Assets/Scripts/UI/ModeSelect/ModeCarouselController.cs` | Modified (iter-1) — added `matchmakingModal1v1` SerializeField + `matchmaking_1v1` target case |
| `Assets/Scripts/UI/ModeSelect/ModeSelectScreenController.cs` | Modified (iter-1) — same additions |
| `Assets/Scenes/ShellScene.unity` | Modified (iter-1) — wired `matchmakingModal1v1` field on both controllers |
| `Docs/Specs/Active/practice_1v1_matchmaking_split/videos/practice_flow_gate.mp4` | Updated (iter-3) — replaced 250x540 iter-2 clip with 1170x2532 full-res (13.1MB) |
| `Docs/Specs/Active/practice_1v1_matchmaking_split/videos/matchmaking_1v1_gate.mp4` | Updated (iter-3) — replaced 250x540 iter-2 clip with 1170x2532 full-res (3.5MB) |
| `Docs/Specs/Active/practice_1v1_matchmaking_split/videos/matchmaking_1v1_cancel_gate.mp4` | Created (iter-3) — 1170x2532 cancel gate video (2.6MB) |

**Pre-existing dirty paths** (present in iter-3 kickoff baseline in HEARTBEAT.log, not introduced by this task):
- `Assets/Golf/Courses/lomond-country-club/Data/hole-0{3,4,5,7,8,9,11,12,13,14,15,16}-geo/TerrainData_Hole*Geo.asset` — pre-existing
- `Assets/Plugins/NuGet/*.dll`, `.nuget-installed.json` — pre-existing
- `Docs/Diag/baked-pivot/M0-regression-*.md` — pre-existing
- `Packages/manifest.json`, `Packages/packages-lock.json` — pre-existing
- `Docs/Specs/Active/mode_select_system/BRIEF_*.md`, `SPEC.md` (deleted) — pre-existing deletions

---

## Screenshots

New full-resolution captures (1170×2532) from iter-3 bot runs:

- `screenshots/cancel_gate_s01_home_pre_play_2026-06-06.png` — Mode Select carousel before 1v1 PLAY (3.4MB)
- `screenshots/cancel_gate_s02_matchmaking_modal_open_2026-06-06.png` — Matchmaking modal open (3.0MB)
- `screenshots/cancel_gate_s03_post_cancel_home_2026-06-06.png` — After Cancel: carousel visible, NextHolePanel gone (3.4MB) ← **key defect-1 evidence**
- `screenshots/practice_flow_gate_s01_home.png` — Practice flow: Mode Select home (3.4MB)
- `screenshots/practice_flow_gate_s03_gameplay.png` — Practice flow: gameplay reached (4.3MB)
- `screenshots/matchmaking_1v1_gate_s02_modal.png` — 1v1 gate: matchmaking modal (3.0MB)
- `screenshots/matchmaking_1v1_gate_s04_gameplay.png` — 1v1 gate: gameplay reached (4.1MB)

Canonical screenshot: `screenshots/cancel_gate_s03_post_cancel_home_2026-06-06.png`

*(Long edge = 2532px ≥ 900px. This is the post-Cancel frame that was the Cesar-rejected defect-1 evidence. The `NextHolePanel` must NOT be visible — the only UI visible should be the Mode Select carousel. Bot asserts `NextHolePanel.activeInHierarchy=False`.)*

---

## Videos

Canonical video: `videos/practice_flow_gate.mp4`

*(Practice path: Home → Practice PLAY click → Hole Select → hole card PLAY click → gameplay at hole 3 → ForceShotComplete → result modal → PLAY NEXT → hole 4 loaded. 13.1MB, 1170x2532, 93.5s, captioned.)*

Supporting video: `videos/matchmaking_1v1_gate.mp4`

*(1v1 path: Home → 1v1 PLAY click → matchmaking modal visible → opponent found → gameplay at random hole. 3.5MB, 1170x2532, 21.3s, captioned.)*

Supporting video: `videos/matchmaking_1v1_cancel_gate.mp4`

*(Cancel gate: Mode Select → 1v1 PLAY → matchmaking modal → CANCEL → carousel restored, NextHolePanel absent. 2.6MB, 1170x2532, 15.2s, captioned.)*

---

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| Change 0 — PLAY buttons on both mode card surfaces routed data-driven off `target` column in modes.csv | PASS | `ModeCarouselController.HandlePlayClicked` and `ModeSelectScreenController.HandlePlayClicked` dispatch via `mode.target` switch: `hole_select` → ShowScreen(HoleSelection), `matchmaking_1v1` → matchmakingModal1v1.Open(randomHoleIndex), `none` → log warning. Verified by iter-1 code review + iter-2/3 bot execution. |
| Change 1 — Practice path: no matchmaking modal; `HoleCardController` tap seeds + launches directly | PASS | `HandleActionClicked` calls `GameSession.SeedSession` + `GameplaySceneLoader.BeginGameplayLoad` directly. Iter-3 bot log: `MatchMakingModal visible after ActionButton click: False (expected: false)`. LabScaffold loaded. GameSession.CurrentHoleNumber=3. |
| Change 2 — 1v1 path: Mode Select 1v1 PLAY → random hole → `MatchmakingModalController.Open(randomHoleIndex)` | PASS | Iter-3 bot: clicked modeId='versus_1v1' via real onClick. `WaitForModalVisible OK: 'MatchMakingModal' visible after 0.0s`. Gameplay loaded at random hole. |
| Change 3 — Exactly one seed point per path; matchmaking seeds for 1v1, Practice seeds on launch | PASS | `grep "SeedSession"` returns exactly two production-code sites: `HoleSelectionScreenController.cs:296` (Practice) and `MatchmakingModalController.cs:414` (1v1). |
| **Cancel gate (iter-3)** — `MatchmakingModalController.Cancel` restores home panel states; `NextHolePanel` stays inactive after Cancel from carousel | PASS | Bot asserts `NextHolePanel.activeInHierarchy=False (expected: false)`. Bot PASS log line present. Full screenshot: `cancel_gate_s03_post_cancel_home_2026-06-06.png` (1170x2532). |
| Gate 1 — Practice: Hole Select → tap hole → no matchmaking modal → gameplay at that hole; hole-out → result modal → PLAY NEXT works | PASS | Iter-3 bot: ActionButton clicked. No matchmaking modal. LabScaffold loaded. ForceShotComplete(InCup) → result modal. PLAY NEXT → next hole loaded. Full video: `practice_flow_gate.mp4` (1170x2532). |
| Gate 2 — 1v1: Mode Select 1v1 PLAY → matchmaking modal shows random opponent → gameplay at random hole (1-18) | PASS | Iter-3 bot: MatchmakingModal visible. Opponent found. Gameplay loaded at random hole. Full video: `matchmaking_1v1_gate.mp4` (1170x2532). |
| Gate 3 — No regression: 360+ EditMode tests pass | PASS | Iter-3 EditMode run: 360 passed / 0 failed / 3 skipped (pre-existing). Run timestamp: 2026-06-06 ~10:10. |
| Video resolution — all task videos at 1170x2532 iPhone 14 portrait | PASS | ffprobe confirms: `practice_flow_gate.mp4` 1170x2532, `matchmaking_1v1_gate.mp4` 1170x2532, `matchmaking_1v1_cancel_gate.mp4` 1170x2532. |

## Known FAIL items

None.

## Spec deviations

- `matchmakingModal1v1` wired to the existing `MatchMakingModal` in ShellScene. No new components created.
- `Random.Range(0, 18)` (0-based) maps to holes 1-18 via `MatchmakingModalController`'s internal `holeNumber = _resolvedIndex + 1` conversion. Verified clean.
- **Dead code advisory (HomeScreenController):** `HomeScreenController.cs:408` `OnPlayClicked` still calls `matchmakingModal.Open(currentHoleIndex)`. This surface is in the deactivated `NextHolePanel` and is unreachable in current navigation. Out of scope per architect's direction. Queued for removal when `NextHolePanel` is redesigned.

## Open questions for Architect

None.
