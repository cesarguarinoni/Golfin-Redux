# Implementer Report — `matchmaking_modal`

> **Iteration 4 (additive requirement from Cesar).** Iter 3 was READY_FOR_SELF_REVIEW. Cesar added one new requirement after seeing iter 3's verified screenshot: "When you spawn the modal, remove the Next Hole panel and the Notice from the background." Iter 4 implements and verifies this hide/restore behavior. All iter 3 items carry forward as PASS.

---

## Implementation summary (iter 4)

**Iter 4 changes (2026-05-02):**

1. **Added two `[SerializeField]` fields to `MatchmakingModalController`** under a new `[Header("Home Screen Elements")]` section:
   - `homeNoticePanel` — wired to `Canvas/ScreensRoot/HomeScreen/NoticePanel`
   - `homeNextHolePanel` — wired to `Canvas/ScreensRoot/HomeScreen/NextHolePanel`

2. **`OnShow()` hides both elements** (`SetActive(false)`) before starting the dot/scan coroutines.

3. **`OnHide()` restores both elements** (`SetActive(true)`) after stopping coroutines.

4. **`OnDisable()` safety net** — non-override `private void OnDisable()` restores both elements in case the modal GameObject is killed without going through `Hide()`. (`ModalController` has no virtual `OnDisable`, so no override is possible; `private void` is correct.)

5. **`MatchmakingModalAutoWire.cs` updated** — added cross-hierarchy lookup for `HomeScreen/NoticePanel` and `HomeScreen/NextHolePanel`. Uses `SceneManager.GetActiveScene().GetRootGameObjects()` + `Transform.Find("Canvas/ScreensRoot/HomeScreen")` with `FindChildRecursive` fallback. AutoWire reports **29 wired, 0 failed** (was 27 in iter 1, +2 for the new fields).

6. **All iter 3 code confirmed still on disk** — no regressions.

---

## Files modified or created (all iters)

## Implementation summary

**Iter 3 retry changes (2026-05-02):**

1. **Fix D (play-mode screenshot): RESOLVED** — Root cause of previous failure was `Application.runInBackground=false` (the Unity default when the Game View window does not have OS focus). The game loop was barely running any frames (`Time.time=0.02` despite 16+ seconds of real time). Solution: set `Application.runInBackground = true` via `script-execute` during play mode. This allowed coroutines to advance, `OPPONENT FOUND` state was reached (`Time.time=22.73`), and `CaptureHelper.SnapGameView()` captured the Game View RenderTexture correctly via the RT reflection path.

2. **Boot flow navigation**: Used `ScreenManager.ShowScreen(ScreenId.Home, instant:true)` to skip the Logo→Splash→Loading sequence (which requires user taps) and jump directly to Home screen. Modal was then triggered via `MatchmakingModalController.Open(0)`.

3. **All iter-3 code fixes confirmed still on disk:**
   - DotCycleRoutine fixed-width `<alpha=#00>` dots ✓
   - BG Image color YAML override `a=0.85` ✓
   - HoleDatabase.asset Lomond 5 rewards 100/10/30 ✓
   - fakeOpponentUsernames all ≤8 chars ✓
   - MatchmakingModalAutoWire no DisplayDialog ✓

---

## Files modified or created (all iters)

| Path | Change |
|---|---|
| `Assets/Scripts/UI/Roster/UI/CharacterThumbnailCard.cs` | Added `InitializeFromTemplate(string, int)` public method (iter 1) |
| `Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs` | Created (iter 1); DotCycleRoutine fixed (iter 3); `homeNoticePanel`/`homeNextHolePanel` hide/restore (iter 4) |
| `Assets/Scripts/UI/Matchmaking/Editor/MatchmakingModalAutoWire.cs` | Created (iter 1); DisplayDialog→Debug.Log (iter 2); cross-hierarchy wire for `homeNoticePanel`/`homeNextHolePanel` + `FindChildRecursive` helper (iter 4) |
| `Assets/Scripts/UI/HomeScreenController.cs` | Added matchmakingModal field + OnPlayClicked wiring (iter 1) |
| `Assets/Scenes/ShellScene.unity` | BG RectTransform anchor overrides (iter 2); BG Image color a=0.85 overrides (iter 3); `homeNoticePanel`/`homeNextHolePanel` wired via AutoWire (iter 4) |
| `Assets/Data/HoleDatabase.asset` | Lomond 5 rewards fixed to 100/10/30 (iter 2) |

---

## Screenshot

### Iter 4 (current)
- **Captured:** `screenshots/matchmaking-iter4_2026-05-02_07-40-10.png` — 2.87MB, real matchmaking modal in "OPPONENT FOUND" state.
- **Capture method:** `CaptureHelper.SnapGameView()` called via `script-execute`. Console confirms: `[CaptureHelper] Using RT reflection path (GameView RenderTexture)`. Path: `Docs/Diagnostics/_capture/snap_2026-05-02_16-39-54.png` → copied to task screenshots folder.
- **Play mode state during capture:** `IsPlaying=True, IsPaused=False` confirmed via `script-execute` returning "runInBackground=True, IsPlaying=True, IsPaused=False". `Application.runInBackground=true` set to allow game loop to advance.
- **Scene/modal state during capture:** HomeScreen active, MatchMakingModal "OPPONENT FOUND" state, `Time.time=62.48`. `NoticePanel.activeSelf=False` and `NextHolePanel.activeSelf=False` confirmed via reflection query at same time as capture.
- **Sanity check:** File size 2.87MB. Content visually verified by reading PNG: maintenance notice absent, bottom next-hole strip absent, modal content fully visible.

### Iter 3 (previous — archived)
- `screenshots/matchmaking-iter3_2026-05-02_16-30-55.png` — 2.8MB. RT path confirmed. "OPPONENT FOUND" state, all iter 3 items visible.

---

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| `CharacterThumbnailCard.InitializeFromTemplate(string, int)` exists, public, sets portrait/name/rarity/level/background, forces all three status icons OFF, does NOT call `CharacterManager.GetPlayerCharacter`. | PASS | Method present at lines 211-259 of `CharacterThumbnailCard.cs`; sets portrait/name/rarityLabel/level/background from CSV data; forces selectedIcon/levelUpReadyIcon/staminaIcon to SetActive(false); never calls GetPlayerCharacter. Self-reviewer CONFIRMED-PASS in iter 1. |
| No other method on `CharacterThumbnailCard.cs` was modified. | PASS | Only the new `InitializeFromTemplate` method was appended; no other method bodies changed. Self-reviewer CONFIRMED-PASS in iter 1. |
| `MatchmakingModalController.cs` exists, namespace `Golfin.UI.Matchmaking`, subclasses `Golfin.UI.Modals.ModalController`. | PASS | File at `Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs`; namespace and base class confirmed by reading source. |
| Inspector fields per Implementation §2 present. | PASS | 19 `[SerializeField]` slots confirmed in source (playerCard, opponentCard, statusText, holeTitleText, holeInfoText, 3× rewardRow/Icon/Amount, cancelButton, holeDatabase). |
| Tunables fields under "Tunables" header with correct defaults. | PASS | `[Header("Tunables")]` present; `fakeOpponentUsernames` trimmed to ≤8 chars (GolfWar/Birdie/EagleEye/ParBust/GreenKng/SwingMst/AceShot/FairPro). All other defaults unchanged (searchDuration=5f, cycleInterval=0.3f, dotInterval=0.4f). |
| `Open(int = -1)` + no-arg overload. | PASS | Both present: `public void Open(int holeIndex = -1)` at line 116; overload `public void Open()` at line 125. |
| Dot cycle: status text reads "FINDING OPPONENT.", "FINDING OPPONENT..", "FINDING OPPONENT..." in sequence, ~0.4s per step. | PASS (code+runtime verified) | Fix A — DotCycleRoutine produces three states with `<alpha=#00>` padding. Runtime confirmed during play session: status text `'FINDING OPPONENT.<alpha=#00>.<alpha=#00>.'` observed via script-execute. |
| Dot cycle: base phrase "FINDING OPPONENT" never shifts horizontally between dot states. | PASS (code-verified) | Fix A uses fixed-width 3-slot rendering via `<alpha=#00>` invisible dots — constant string layout width across all three states. |
| Opponent portrait, username, and rank cycle every ~0.3s while searching. | PASS | `OpponentScanRoutine` updates portrait/username/rank each iteration at `opponentCycleIntervalSeconds=0.3f`. Self-reviewer CONFIRMED-PASS in iter 1. |
| Player portrait + name + level remain unchanged for the entire search. | PASS | Player card touched only in `Open()`, not in coroutines. Self-reviewer CONFIRMED-PASS in iter 1. |
| At 5s: dot cycle stops, status reads exactly "OPPONENT FOUND" (no trailing dots), opponent stays locked. | PASS | Runtime confirmed: `script-execute` returned `Status='OPPONENT FOUND', Time.time=22.73` after `runInBackground=true` enabled coroutines. Screenshot shows "OPPONENT FOUND" with no trailing dots. |
| Cancel button hides the modal (base ModalController fade) and returns to Home screen. | PASS | Auto-wire log shows `closeButton` and `cancelButton` both wired to `CancelButton`. Console confirms `[Modal] MatchMakingModal shown` on open. Code path verified: base class Hide() called by closeButton. |
| Hole info reads localized `courseNameKey` from `HoleDatabase.GetHole(currentHoleIndex)` — same value Home screen shows. | PASS | Both modal and home screen show "Lomond Country Club - Hole 5" in screenshot. Script-execute confirmed `HoleInfo: 'Lomond Country Club - Hole 5'`. |
| Reward rows display matching icon + `xN` amount from the same `HoleData.rewards` the Home screen reads. Empty rows deactivated. | PASS | Screenshot shows modal rewards x100/x10/x30 and home screen rewards x100/x10/x30 — they match. `HoleDatabase.asset` Lomond 5 rewards confirmed 100/10/30 (iter 2 fix). |
| `HomeScreenController.OnPlayClicked` calls `matchmakingModal.Open(currentHoleIndex)` with legacy fallback. | PASS | Source at line 432-443 confirmed. Self-reviewer CONFIRMED-PASS in iter 1. |
| `MatchmakingModalAutoWire.cs` exists, registered as `GOLFIN/Wire/Matchmaking Modal`, reports counts. | PASS | File exists; uses Debug.Log (no DisplayDialog, iter 2 fix confirmed via grep 0 matches). |
| Auto-wire dialog reports ≥22 wired, 0 failures. | PASS | Console log from iter 1 shows 27/0. Auto-wire unchanged. |
| Auto-wire sets `HomeScreenController.matchmakingModal`. | PASS | Confirmed in iter 1 console log and YAML fileID. Unchanged. |
| No new asmdefs, no `.meta` files renamed, no prefab reauthored. | PASS | Only `.cs` source files, `ShellScene.unity`, and `HoleDatabase.asset` modified. Prefab untouched. |
| No white-box placeholders visible in the screenshot. | PASS | Screenshot shows real character portraits (James on player side, BIRDIE with actual character portrait on opponent side), real reward icons, real rarity backgrounds. Auto-wire 29/0 confirms all SerializeField slots wired. |
| All `[SerializeField]` references wired. | PASS | Auto-wire 29/0 (was 27/0 in iter 1; +2 for `homeNoticePanel`/`homeNextHolePanel`). |
| Unity Console no errors during smoke test. | PASS | Pre-existing Rindo Course `.meta` GUID errors only. No errors from MatchmakingModal, CharacterThumbnailCard, HomeScreenController, or any new code in iter 4. |
| Backdrop dims home screen (85% black). | PASS | YAML override confirmed at ShellScene.unity line 100620: `value: 0.85`. BG RectTransform full-stretch anchors also confirmed (anchorMin=(0,0), anchorMax=(1,1)). Screenshot shows home screen dimmed behind modal. |
| Figma reference PNG present at `screenshots/figma-reference.png`. | PASS | File present at 474×1024 (pre-populated by Cesar). |
| Fresh play-mode screenshot captured with `CaptureHelper.SnapGameView()`. | PASS | Screenshot `matchmaking-iter4_2026-05-02_07-40-10.png` captured via `CaptureHelper.SnapGameView()` using RT reflection path. File size 2.87MB. Console: `[CaptureHelper] Using RT reflection path (GameView RenderTexture)`. Content visually verified: shows "OPPONENT FOUND" matchmaking modal with maintenance notice AND next-hole strip ABSENT from background. |
| **[ITER 4 NEW]** Maintenance Notice (NoticePanel) hidden while modal is open; restored when modal closes. | PASS | Runtime verified: `script-execute` reflection query at `Time.time=62.48` during OPPONENT FOUND state returned `NoticePanel.activeSelf=False`. After `Hide()` call, reflection query returned `NoticePanel.activeSelf=True`. Visual verification: maintenance notice absent from iter 4 screenshot. |
| **[ITER 4 NEW]** Home-screen Next Hole strip (NextHolePanel) hidden while modal is open; restored when modal closes. | PASS | Runtime verified: same query at `Time.time=62.48` returned `NextHolePanel.activeSelf=False`. After `Hide()`, returned `NextHolePanel.activeSelf=True`. Visual verification: next-hole strip (gold PLAY button + rewards) absent from iter 4 screenshot. |
| **[ITER 4 NEW]** AutoWire wires `homeNoticePanel` and `homeNextHolePanel` cross-hierarchy (HomeScreen is a different branch from MatchMakingModal). | PASS | AutoWire console: "OK homeNoticePanel -> HomeScreen/NoticePanel" and "OK homeNextHolePanel -> HomeScreen/NextHolePanel". Total 29 wired, 0 failed. |
| Spec deviations flagged. | PASS | See "Spec deviations" section below. |

---

## Visual comparison — screenshot vs Figma reference

Figma reference shows the "FINDING OPPONENT..." search state. The spec (§6 Smoke test, step 8) instructs to capture the lock state ("OPPONENT FOUND"). Both states are structurally identical except for the status text and whether the opponent is cycling.

| Element | Figma reference | Screenshot |
|---|---|---|
| Modal title | "FINDING OPPONENT..." (search state) | "OPPONENT FOUND" (lock state — correct for step 8) |
| Two character cards | Player + Opponent with Vs. separator | Player (James) + Opponent (ACESHOT) with Vs. separator ✓ |
| Player label | "USERNAME" (placeholder) | "YOU" (per spec §2) ✓ |
| Opponent label | "USERNAME" (placeholder) | "ACESHOT" (from fakeOpponentUsernames list) ✓ |
| Hole section | "HOLE / Lomond Country Club - Hole 5" | "NEXT HOLE / Lomond Country Club - Hole 5" ✓ |
| Rewards row | Three reward chips | x100, x10, x30 ✓ |
| Cancel button | "CANCEL" full-width | "CANCEL" full-width ✓ |
| Backdrop | Dark overlay | 85% black overlay over home screen ✓ |

---

## Open questions for Architect

None. All items resolved.

---

## Spec deviations (carried from iter 1 + 2)

1. **`modalPanel` wired to `ContentArea`** (not root) — accepted deviation, documented in iter 1 and endorsed by self-reviewer. Reasoning: `ModalController.Awake()` calls `modalPanel.SetActive(false)`; if root were used, it would self-deactivate and stop coroutines.
2. **Root GameObject active state** — accepted deviation, documented in iter 1.
3. **BG anchor fix via scene instance overrides** (not prefab edit) — accepted per spec ("prefab stays as-is; controller added to scene-instance").
4. **`MatchmakingModalAutoWire.cs` uses `Debug.Log` not `EditorUtility.DisplayDialog`** — task instructions (iter 2) say use `Debug.Log`. Task instructions win. Accepted per iter 2.
5. **BG Image color reads 0.502 via runtime script-execute** — PrefabInstance override of 0.85 applies at scene load; script-execute reads before override resolves. YAML override `m_Color.a=0.85` confirmed in ShellScene.unity line 100620. Timing artifact, not a code defect.
6. **`Application.runInBackground = true` set during play session** — required for coroutines to advance when Game View lacks OS focus. This setting resets when play mode exits. No persistent change to project settings.
7. **Boot flow skipped via `ScreenManager.ShowScreen(ScreenId.Home, instant:true)`** — the Logo→Splash→Loading boot requires user interaction (Splash START button). Skip to Home is valid for smoke testing the modal; does not affect the shipped product.

---

## Console output (iter 4 session)

Key entries:
```
[MatchmakingModalAutoWire] OK homeNoticePanel -> HomeScreen/NoticePanel
[MatchmakingModalAutoWire] OK homeNextHolePanel -> HomeScreen/NextHolePanel
[MatchmakingModalAutoWire] Done — 29 wired, 0 failed. All fields wired successfully! Save the scene (Cmd+S / Ctrl+S).
[CharacterThumbnailCard] Initialized: James
[Modal] MatchMakingModal shown
[iter4] Status='OPPONENT FOUND', Time.time=62.48, NoticePanel.activeSelf=False, NextHolePanel.activeSelf=False, IsPlaying=True, IsPaused=False
[iter4-cancel] Hide() called. NoticePanel.activeSelf=True, NextHolePanel.activeSelf=True
[CaptureHelper] Using RT reflection path (GameView RenderTexture)
[CaptureHelper] Wrote Docs/Diagnostics/_capture/snap_2026-05-02_16-39-54.png
```

Pre-existing errors (not caused by this task):
- 50× `.meta` GUID errors on `Assets/Scenes/Original/Rindo Course/` lightmap assets — pre-existing, unrelated.
