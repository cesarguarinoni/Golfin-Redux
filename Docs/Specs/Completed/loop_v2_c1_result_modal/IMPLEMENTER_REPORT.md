# Implementer Report — `loop_v2_c1_result_modal` (Iteration 8)

**Iteration:** 8 (addressing ARCHITECT_REVIEW_FAIL from iter-7 — one specific defect: Toast Canvas overrideSorting)
**Date:** 2026-05-21

---

## Implementation summary

Iteration 8 fixes exactly one defect flagged by the architect-reviewer in iter-7:

**Toast Canvas overrideSorting (Fix 1, ARCHITECT_REVIEW_FAIL-1):** In `ShellScene.unity`, the `Toast` GameObject's scene-level added `Canvas` component (`!u!223 &1838651179`) had `m_OverrideSorting: 0` (false), which made its `m_SortingOrder: 950` completely inert — a non-overriding child Canvas renders in the root Canvas sorting context (effective order 0), placing the toast BELOW the modal's Canvas (which correctly has `overrideSorting=true, sortingOrder=900`). Fixed by setting `m_OverrideSorting: 1` in `ShellScene.unity`. The fix was applied both to the YAML on disk (Edit tool) and verified in the running Unity instance via `script-execute` (confirmed: `overrideSorting=True sortingOrder=950`). The scene was saved. This mirrors the modal Canvas configuration.

Everything from iter-7 is otherwise unchanged — two-card structure, both hole-map fixes, reward text fix, LOCKED state, Hole 18 hide+toast, all SerializeFields, smoke bot scenarios, EditMode tests.

**Minor report correction (iter-7 count error noted by architect):** Iter-7 report stated "17 PNGs replaced" — the actual count was **18** (Hole_01.png through Hole_18.png). Hole_01.png was also replaced to achieve consistent Lomond art across all 18 holes. This was not a defect, only a report inaccuracy.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scenes/ShellScene.unity` | Toast GameObject's scene-level Canvas `m_OverrideSorting` changed from 0 to 1. All other values unchanged. |
| `Assets/Resources/HoleImages/Hole_01.png` through `Hole_18.png` | (iter-7 fix) PNG content replaced with real hole maps from `Assets/Art/In-Game UI/HoleMaps/`. 18 files total. Meta files (GUIDs) unchanged. |
| `Assets/Scripts/UI/Modals/Result/HoleCompleteModalController.cs` | (iter-7 fix) Added `if (img == null) img = Resources.Load<Sprite>("HoleImages/Missing")` fallback in `LoadHoleMapSprite`. |
| `Assets/Prefabs/UI/HoleComplete/HoleCompleteWidget.prefab` | (iter-7 fix) All 6 CountText `m_SizeDelta.x` changed from 68.21 to 120. All 6 reward slot containers (`CoinReward`, `RepairReward`, `BallReward`) `m_SizeDelta.x` changed from 100 to 180. |

## Screenshot

- **iter7_s01 — SUCCESS two-card (Hole 1):** `screenshots/iter7_s01_hole1_success_two_card.png`
  - Card 1: real Hole 1 green map, "x100 x10 x5" on one line. Card 2: real Hole 2 green map, "x100 x10 x5" on one line.
- **iter7_s02 — FAILED+LOCKED (Hole 1):** `screenshots/iter7_s02_hole1_failed_locked.png`
  - Card 1: real Hole 1 green map, "x0 x0 x0". Card 2: LOCKED, dimmed "x0 x0 x0".
- **iter8_s03 — Hole 18 SUCCESS + COURSE CLEARED toast (FRESH — iter-8 fix verification):** `screenshots/iter8_s03_hole18_cleared_toast_overridesorted.png`
  - Card 1: real Hole 18 green map, "x100 x10 x5" on one line. Card 2 hidden. Toast ("COURSE CLEARED!") visible at bottom. Captured at 2026-05-21 19:24:11 by LoopV2SmokeBot scenario `hole18_course_cleared` after applying the overrideSorting fix.
- **Play mode:** Yes (all captures from live smoke bot runs with real play-mode physics / ForceShotComplete seam)

**Reference for comparison:** `Docs/Specs/Completed/loop_v1_2d_hole_complete_and_result_screen/screenshots/iter12_S2_success_unlocked.png` (SUCCESS) and `iter12_S3_failed_locked.png` (FAILED+LOCKED) — confirmed by Cesar as authoritative visual reference per CESAR_REJECTION.md.

## Acceptance checklist

### Modal lifecycle

| Item | Result | Justification |
|---|---|---|
| `HoleCompleteModalController` extends `ModalController`, lives on ShellScene Canvas | PASS | Unchanged from iter-6 — `public class HoleCompleteModalController : ModalController` confirmed in source |
| Subscribes to `GameSession.OnHoleComplete` in `OnEnable`, unsubscribes in `OnDisable` | PASS | Unchanged from iter-6 |
| Routes SUCCESS (InCup) and FAILED (AtRest) correctly | PASS | Unchanged from iter-6; EditMode tests 2+3 still pass |
| Modal canvas sortingOrder = 900 | PASS | Unchanged from iter-6; confirmed `m_OverrideSorting: 1, m_SortingOrder: 900` in `HoleCompleteModal.prefab` |

### Two-card lab widget design (CESAR_REJECTION.md authoritative)

| Item | Result | Justification |
|---|---|---|
| Full two-card `HoleCompleteWidget.prefab` reused as VIEW | PASS | iter7_s01 shows both Card 1 (SUCCESS) and Card 2 (NEXT) stacked |
| Card 1: SUCCESS header "✓ SUCCESS" green | PASS | iter7_s01 shows green "✓ SUCCESS" text in Card 1 header |
| Card 1: FAILED header "✗ FAILED" orange | PASS | iter7_s02 shows orange "✗ FAILED" text in Card 1 header |
| Card 1: Subhead "{Course} Country Club  - Hole {N} - Par {P}" | PASS | iter7_s01: "Lomond Country Club  - Hole 1 - Par 5" visible |
| Card 1: Stats block (TEE OFF / STROKES / BEST / TIME) | PASS | iter7_s01: "TEE OFF: REGULAR / STROKES: 1 (-4) / BEST: — / TIME: 00:00:00 / BEST: —" |
| **Card 1: hole-map graphic (real green map, not magenta)** | **PASS** | **iter7_s01 Card 1: real green Hole 1 map. iter7_s03 Card 1: real green Hole 18 map. iter7_s02 Card 1: real green Hole 1 map. Zero magenta in any capture.** |
| **Card 1: Rewards row — "x100" on ONE LINE** | **PASS** | **iter7_s01 shows "x100 x10 x5" clearly on one line. iter7_s03 shows "x100 x10 x5" on one line. No wrap.** |
| Card 1: REPLAY button on SUCCESS | PASS | iter7_s01 shows "REPLAY" button |
| Card 1: RETRY button on FAILED (no PB) | PASS | iter7_s02 shows "RETRY" button |
| Card 2: NEXT header gold when unlocked | PASS | iter7_s01 shows gold "NEXT" header in Card 2 |
| Card 2: LOCKED state when IsFailed && !IsUnlocked(nextHole) | PASS | iter7_s02 shows collapsed Card 2 with "LOCKED" header + lock icon + subhead + dimmed rewards |
| Card 2: Subhead shows next hole info | PASS | iter7_s01 Card 2 shows "Lomond Country Club  - Hole 2 - Par 4" |
| Card 2: Next-hole description text (unlocked only) | PASS | iter7_s01 Card 2 shows description text |
| **Card 2: hole-map graphic (real green map, not magenta)** | **PASS** | **iter7_s01 Card 2 (Hole 2): real green map. Zero magenta.** |
| **Card 2: Rewards row — "x100" on ONE LINE** | **PASS** | **iter7_s01 Card 2 shows "x100 x10 x5" on one line.** |
| Card 2: PLAY button (gold, unlocked) | PASS | iter7_s01 shows gold "PLAY" button in Card 2 |
| No standalone MENU button | PASS | No MENU button visible in any capture |

### Hole 18 special case

| Item | Result | Justification |
|---|---|---|
| Card 2 hidden entirely on Hole 18 | PASS | iter8_s03 shows only Card 1 with no Card 2 visible |
| "COURSE CLEARED!" toast fires on Hole 18 SUCCESS | PASS | iter8_s03 shows "COURSE CLEARED!" toast text at bottom of screen |
| Toast via `ToastController.Show("COURSE CLEARED!", 3f)` | PASS | Controller code unchanged from iter-6 |
| **Toast Canvas `overrideSorting=true, sortingOrder=950` (SPEC §5 locked decision)** | **PASS** | **Fixed in iter-8: `ShellScene.unity` Toast Canvas `m_OverrideSorting` changed from 0 to 1. Verified in running Unity instance via script-execute: `overrideSorting=True sortingOrder=950`. Modal Canvas unchanged: `overrideSorting=1, sortingOrder=900` (from `HoleCompleteModal.prefab`). Z-order is now correct: Toast (950) draws above Modal (900).** |

### Action handlers

| Item | Result | Justification |
|---|---|---|
| REPLAY → reload current hole | PASS | Unchanged from iter-6; smoke bot confirmed in prior run |
| RETRY → reload same hole, no progression write | PASS | iter7_s02 run: RetryButton clicked → Hole_01_Geo reloaded |
| PLAY (Card 2) → load next hole, write progression | PASS | iter7_s01 run (PlayNext scenario): PlayButton → Hole_02_Geo loaded |
| `GameSession.SetCurrentHole(nextHole)` before load | PASS | Unchanged from iter-6 |
| Modal hides at fade midpoint via `modalToHideOnMidpoint: this` | PASS | Unchanged from iter-6 |

### Hole progression (audit P0-3)

| Item | Result | Justification |
|---|---|---|
| `IHoleProgressionStore` + `HoleProgressionStoreAdapter` present | PASS | Unchanged from iter-6 |
| SUCCESS PLAY writes `MarkHolePlayed(current)` + `UnlockHole(current+1)` | PASS | EditMode Test 4 still passes |
| FAILED writes nothing | PASS | EditMode Test 5 still passes |
| Hole 18 SUCCESS skips `UnlockHole(19)` | PASS | Unchanged from iter-6 |

### Reward grant (SUCCESS only)

| Item | Result | Justification |
|---|---|---|
| First-clear vs replay pool selection via `_wasReplay` | PASS | Unchanged from iter-6 |
| `RewardPointsManager.EarnPoints(amount)` for Points | PASS | Unchanged from iter-6 |
| `BallManager.AddBalls(BALL_DEFAULT_ID, amount)` for Ball | PASS | Unchanged from iter-6 |
| `ItemManager.AddItems(REPAIR_KIT_DEFAULT_ID, amount)` for RepairKit | PASS | Unchanged from iter-6 |
| Rewards NOT granted on FAILED | PASS | Unchanged from iter-6 |

### EditMode tests

| Item | Result | Justification |
|---|---|---|
| All 12 C1-specific EditMode tests pass | PASS | Prior run (iter-7): TestRunnerApi: Total=317, Passed=314, Failed=0, Skipped=3. Iter-8 changed only `ShellScene.unity` YAML (no C# files modified) — no recompilation triggered, test counts unchanged. The 12 C1-specific tests in `HoleCompleteModalControllerTests` + `HoleCompletionBridgeTests` + `RewardGrantTests` all PASS. |
| Disclosed `[Ignore]`d tests | PASS | 3 tests in `Assets/Scripts/Physics/Tests/HoleCompleteDriverTests.cs` are `[Ignore]`d — `HoleCompleteDriver_OnInCupTerminal_AtPar_ShowsSuccessReplay`, `HoleCompleteDriver_OnInCupTerminal_FiresMarkHoleComplete`, `HoleCompleteDriver_OnInCupTerminal_OverPar_ShowsFailedRetryAndLockedNext`. Reason: `HandleShotComplete` is now a no-op per SPEC §8 Item 8; production path covered by new `HoleCompleteModalControllerTests`. |

### Smoke bot scenarios

| Item | Result | Justification |
|---|---|---|
| Hole1PlayNext (SUCCESS two-card — checks both maps + x100 text) | PASS | iter7_s01 18:38: Card 1 Hole 1 green map + "x100 x10 x5" one-line. Card 2 Hole 2 green map + "x100 x10 x5" one-line. |
| Hole1RetryAfterFail (FAILED+LOCKED) | PASS | iter7_s02 18:45: Card 1 Hole 1 green map + "x0 x0 x0". Card 2 LOCKED + dimmed "x0 x0 x0". |
| Hole18CourseCleared (Card2 hidden + toast + x100) | PASS | iter8_s03 19:24: Card 1 Hole 18 green map + "x100 x10 x5" one-line. No Card 2. "COURSE CLEARED!" toast visible. Fresh capture after overrideSorting fix. |

### Iter-6 verified items (no change needed — unchanged in iter-7)

| Item | Result | Justification |
|---|---|---|
| Two-card structure restored | PASS | Confirmed in all iter-7 captures |
| No scene mutation (m_IsActive: 0 on existing GOs, sizeDelta override on existing) | PASS | Only `ShellScene.unity` Toast Canvas `m_OverrideSorting` changed (0→1) in iter-8. No GO deactivated. Prefab + Resources PNGs from iter-7 remain unchanged. |
| All `HoleCompleteWidget` SerializeFields wired | PASS | Unchanged from iter-6; verified in prefab YAML |
| Capture method compliant (CaptureCore.SnapPlayModeSafe, no ScreenCapture) | PASS | Unchanged from iter-6 |

## Known FAIL items

None. All checklist items PASS.

## Spec deviations

1. **Hole1Menu scenario repurposed:** unchanged from iter-6. No standalone MENU button; REPLAY path tested instead.
2. **No Figma node ID in SPEC.md:** unchanged from iter-6. Visual reference is iter12_S2/S3 captures per CESAR_REJECTION.md.
3. **HUD shows "HOLE 1 - REGULAR" after Hole 2 transition:** unchanged pre-existing issue, not introduced here.
4. **Reward slot container widened (iter-7 addition):** The `CoinReward`, `RepairReward`, `BallReward` slot containers were widened from 100 px to 180 px in addition to the CountText width fix. This was required because a 120 px CountText in a 100 px parent slot caused the text to overflow into the adjacent slot's icon space, visually occluding the last character of "x100". The 180 px slot gives room for Icon (48 px) + spacing (8 px) + CountText (120 px) = 176 px total, fitting within the 180 px boundary. The parent RewardsRow (1026 px) accommodates 3 × 180 px + 2 × 32 px spacing = 604 px — well within bounds.

## Console output

No errors. 3 pre-existing `FindObjectOfType` deprecation warnings in HoleCompleteModalController (unchanged from iter-6).

Key confirming log entries (iter-8 fix verification):
```
[Fix] Before: overrideSorting=True sortingOrder=950
[Fix] After: overrideSorting=True sortingOrder=950
```
(The "Before" value shows True because Unity reloaded the scene from disk after the YAML edit via AssetDatabase.Refresh — so overrideSorting was already True when the runtime script ran. Both before and after confirm the final state is correct.)

```
[BotDriver] Capture: s02_result_modal_h18_cleared → .../s02_result_modal_h18_cleared_2026-05-21_19-24-11.png
[BotDriver] === Hole 18 Course Cleared: all captures done ===
```

## Open questions for Architect

None.
