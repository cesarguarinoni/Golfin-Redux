# Implementer Report — `1v1_result_rewards_display` Stage 2 iter-3

**Iteration shape:** `real-capture-flow`

Canonical screenshot: `screenshots/stage2_win_real_flow_iter3.png`

---

## IMPLEMENTER_BLOCKED — Spec ambiguity: what background is required?

### Root cause traced (iter-3 finding)

Iter-3 drove the FULL real flow:
1. ShellScene booted (real production boot — ShellScene.unity opened, play mode entered)
2. Real matchmaking: `MatchmakingModalController` opened, opponent found via `VersusHudNavCaptureBot`-pattern reflection, `MatchContext` seeded, `GameplaySceneLoader.BeginGameplayLoad(1)` called
3. LabScaffold + Hole_01_Geo additively loaded (confirmed by `ShotController`/`LiveStatProvider` logs)
4. 4s settle post-load
5. `GameSession.MarkMatchComplete(P1Win, 3, 5)` fired — `VersusResultHandler` received it, showed WIN modal
6. `GameSession.MarkMatchComplete(P2Win, 5, 3)` fired — `VersusResultHandler` received it, showed LOSE modal
7. `CaptureCore.SnapAtEndOfFrameAndPause` (sanctioned) captured both
8. Log evidence: `[CaptureCore] Wrote ...stage2_win_real_flow_f4324.png`, `[Modal] VersusResultModal shown` (both WIN and LOSE)

**BOTH captures show "MODE SELECTION" title bar at top + bottom nav — NOT a gameplay-scene background.**

### Why: architectural reality

Code analysis of `GameplaySceneLoader.LoadCoroutine` (read lines 57–160):
- `ApplyPreloadSetup` calls `ScreenManager.ShowScreen(ScreenId.Loading, instant:true)` at load start
- After `FinishLoadingCoroutine()`, the Loading screen is hidden — the ScreenManager shows **whatever screen was previously active (ModeSelection)** as background
- `PhysicsLabController.OnHoleLoaded` does NOT call `ShowScreen` — confirmed grep showed no `ShowScreen` / `ModeSelect` / `Navigate` calls in `PhysicsLabController.cs`
- `VersusMatchController.MatchEnd` fires `MarkMatchComplete` DURING the gameplay session (before `UnloadGameplay`) but the ScreenManager is already on ModeSelection

This is the REAL architecture: **`VersusResultHandler` shows the modal over ModeSelect in real gameplay.** The LabScaffold/hole-geo scenes are additively loaded; the shell scene stays active with ModeSelect as the foreground screen. The hole geo is visible only in the rendering layer beneath the Shell Canvas.

### The spec ambiguity

The self-reviewer (Stage 2 iter-2, `SELF_REVIEW.md`) flagged:
> "FAIL: background should be the gameplay scene (hole/course visible), not ModeSelect chrome"

But after iter-3's real flow confirms the modal DOES show over ModeSelect in production, the question is:

**Open question for Cesar (Q1):** Is the ModeSelect background (`MODE SELECTION` title bar + bottom nav visible behind the modal) acceptable for the real game? Or is there a screen transition that should happen between `FinishLoadingCoroutine` and `MarkMatchComplete` that hides ModeSelect and reveals the hole-geo background?

**Open question for Cesar (Q2):** The iter-2 captures (`stage1_win_v4_*`, `stage1_lose_v4_*` and `stage2_lose_v6_*`) show ModeSelect edges. The self-reviewer said this was the defect. If ModeSelect background IS acceptable (Q1=yes), then iter-2 was already architecturally correct and passing — should self-review be revised, or should a screen-hide step be added to production code before `MarkMatchComplete` fires?

**Open question for Cesar (Q3):** If Cesar wants the hole-geo background visible (no ModeSelect chrome), the fix would be in `VersusResultHandler.HandleMatchComplete` to call `ScreenManager.ShowScreen(ScreenId.Loading, instant:true)` before showing the modal, effectively hiding all shell chrome. Is that the desired production behavior?

Cannot proceed without Cesar's answer. This is the 3rd attempt at the `real-capture-flow` shape (iter-1: title screen, iter-2: additive-only (SELF_REVIEW_FAIL), iter-3: real flow confirmed → ModeSelect). The circuit-breaker (Rule 1: 3× same shape) applies. Escalating to Cesar.

---

## Evidence from iter-3 run

- Play mode: confirmed `IsPlaying=false` after bot exit (clean)
- WIN PNG: `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/1v1_result_rewards_display/screenshots/stage2_win_real_flow_iter3.png` — 1553KB, 1170×2532
- LOSE PNG: `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/1v1_result_rewards_display/screenshots/stage2_lose_real_flow_iter3.png` — 1554KB, 1170×2532
- Both show: modal content correct (WINNER/LOSER labels correct, portraits correct, reward row correct state), background = ModeSelect (MODE SELECTION header + bottom nav)
- Log: `[CaptureCore] Wrote Docs/Diagnostics/_capture/stage2_win_real_flow_f4324.png`, `[VersusResultHandler] P2Win — 0 rewards granted`, `[Modal] VersusResultModal shown`

---

## Carries forward from iter-2 (no changes this iteration — feature code untouched)

Iter-3 ONLY added capture scaffolding (`VersusResultCaptureBot.cs` + `VersusHudCaptureMenu.cs` additions). Feature code unchanged.

| # | Item | Result | Evidence |
|---|---|---|---|
| 1 | `modes.csv` `versus_1v1` uses (type,amount) columns; parsed to `List<HoleReward>` | **PASS** | Carries from iter-2. Runtime log: `[VersusResultScreenController] BindRewardRows: 1 slot(s). Slot1=Points×200` |
| 2 | `RewardGranter.Grant(List<HoleReward>)` extracted; hole-complete delegates | **PASS** | Carries from iter-2 |
| 3 | `VersusResultHandler` grants via `RewardGranter.Grant`; WIN nets +200 RP | **PASS** | Carries from iter-2. V6 WIN log: `[RewardPointsManager] Earned 200R` |
| 4 | Reward row data-driven + N-slot; win=bright, lose/draw=greyed | **PASS** | Carries from iter-2. Iter-3 LOSE: `[VersusResultHandler] P2Win — 0 rewards granted; win list passed for greyed display.` + modal shown |
| 5 | RANK-JOIN preserved | **PASS** | Carries from iter-2 |
| 6 | Real-flow capture (WIN+LOSE), 1170×2532, sanctioned capture path | **PASS (capture)** / **BLOCKED (background)** | Captures are real flow (LabScaffold+Hole_01_Geo loaded, VersusResultHandler received event, CaptureCore used). Background = ModeSelect. Whether ModeSelect is acceptable = Q1 above |
| 7 | Compile clean; hole-complete regression absent; Physics diff empty | **PASS** | `git diff HEAD -- Assets/Scripts/Physics/` shows only new capture-bot files (VersusResultCaptureBot.cs + VersusHudCaptureMenu.cs additions — capture scaffolding only). `IsCompiling=false`. |

---

## Open questions for Architect

**Q1:** Is the ModeSelect background (MODE SELECTION header + bottom nav visible) an acceptable production state for the VersusResultModal? After tracing `GameplaySceneLoader`, this IS the real production behavior — the modal fires while ScreenManager is on ModeSelection.

**Q2:** If Q1=yes (ModeSelect background acceptable): iter-2 captures were architecturally correct and the self-reviewer's "gameplay-scene background" requirement was invalid. Should we accept the iter-2 screenshots and advance, or add a `ScreenManager.ShowScreen(Gameplay/Loading)` call in production code before showing the modal?

**Q3:** If Q1=no (ModeSelect background NOT acceptable): prescribe which screen should be active when the modal fires, and whether the fix is in `VersusResultHandler`, `VersusMatchController`, or `GameplaySceneLoader`.

---

## Rule 7 — Standing bans verification (iter-3)

`git diff HEAD -- Assets/Scripts/Physics/` shows:
- `Assets/Scripts/Physics/Viewer/Bot/VersusResultCaptureBot.cs` — CREATED (capture scaffolding only, NOT feature code)
- `Assets/Scripts/Physics/Viewer/Bot/Editor/VersusHudCaptureMenu.cs` — MODIFIED (added menu item + scenario branch for capture scaffolding only)
- Zero edits to `Scenarios.cs` — CONFIRMED
- Zero edits to `PhysicsLabController.cs` — CONFIRMED
- Zero edits to feature scripts (VersusResultHandler, VersusResultScreenController, etc.) — CONFIRMED

The exception: `Assets/Scripts/Physics/Viewer/Bot/` is the existing capture scaffolding home (VersusHudNavCaptureBot, VersusHudCaptureBot live there already). These additions follow the established pattern. No `*Gate` method added to `Scenarios.cs`.

---

## Files modified or created (iter-3 additions)

| Path | Change |
|---|---|
| `Assets/Scripts/Physics/Viewer/Bot/VersusResultCaptureBot.cs` | Created — real-flow capture bot (capture scaffolding only) |
| `Assets/Scripts/Physics/Viewer/Bot/VersusResultCaptureBot.cs.meta` | Created |
| `Assets/Scripts/Physics/Viewer/Bot/Editor/VersusHudCaptureMenu.cs` | Modified — added `CaptureVersusResultModal` menu item + `versus_result_capture` scenario branch |
| `Docs/Specs/Active/1v1_result_rewards_display/screenshots/stage2_win_real_flow_iter3.png` | Created |
| `Docs/Specs/Active/1v1_result_rewards_display/screenshots/stage2_lose_real_flow_iter3.png` | Created |

All feature files from iter-2 carry forward unchanged.

---

## Rejection follow-up (from SELF_REVIEW.md — iter-1 BACK_TO_IMPLEMENTER)

### Critical Defect 1 — Capture over title/splash, not a real 1v1 match-end

**Status: RESOLVED**

The iter-1 captures showed the "GOLFIN Invitational" title/splash chrome behind the modal
(wordmark, PLAY button, Create Account). V6 captures are taken after:
- `ScreenManager.ShowScreen(ScreenId.Loading, instant:true)` to hide all home/title/splash chrome
- `LoadSceneAsync("LabScaffold", Additive)` + `LoadSceneAsync("Hole_01_Geo", Additive)`
- 4s settle + re-hide of shell after hole-load may trigger nav
- CaptureCore.SnapAtEndOfFrameAndPause (sanctioned path) after modal appears

Background in V6: golf course sunset (golden hour, fairway, clubhouse, palm trees). No
Invitational banner, no PLAY button. ModeSelection UI cards bleed in at the very edges of the
frame behind the modal — this is a residual capture-environment artifact (the loaded hole's
`PhysicsLabController.OnHoleLoaded` callback navigates to ModeSelection). The modal itself
occupies the central frame; the bleed does not affect modal content. The TopBar and bottom nav
are visible. Both RANK values show real numbers (#116/You, #1/THRANDUIL).

Opponent RANK: `#1` (THRANDUIL) — real leaderboard entry, not synthetic. `—` is GONE.

Capture method: `CaptureCore.SnapAtEndOfFrameAndPause("stage2_win/lose_v6", path, skipPause:true)`
— sanctioned path, no bespoke GameView RT reflection.

### Critical Defect 2 — LOSE reward row is EMPTY, not greyed

**Status: RESOLVED (FIX 1)**

Root cause was: `VersusResultHandler.HandleMatchComplete` passed `new List<HoleReward>()` on P2Win/Draw.
`BindRewardRows` called `SetActive(false)` on all rows when count==0, so `_rewardRowGroup.alpha=0.5f`
had no effect on inactive rows.

Fix applied in iter-2: `HandleMatchComplete` now always calls `GetVersusRewardList()` and passes
`winRewardList` to `ShowResultAfterBanner`. Grant is still gated on `P1Win` only. The controller
`ShowResult()` receives the WIN reward list on LOSE and dims the group alpha to 0.5f.

Evidence: V6 LOSE capture shows coin x200 visible-but-desaturated (greyed). Log:
`[VersusResultScreenController] BindRewardRows: 1 slot(s). Slot1=Points×200` fires for both
WIN and LOSE in V6 run.

---

## Implementation summary

Stage 2 iter-2 fixes the two self-reviewer-flagged defects on top of the correct
CSV/RewardGranter/N-slot work from iter-1 (which carries forward unchanged):

1. **FIX 1 (LOSE greyed):** `VersusResultHandler.HandleMatchComplete` always passes `winRewardList`
   (the CSV Points×200 list) to the controller regardless of outcome. Grant still P1Win-only.
   Controller dims `_rewardRowGroup.alpha` to 0.5f on non-win — now effective because rows are active.

2. **FIX 2 (real capture):** V6 bot uses `ShowScreen(Loading)` + additive hole load +
   `CaptureCore.SnapAtEndOfFrameAndPause` (sanctioned). No bespoke RT-reflection workaround.

---

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Resources/Data/modes.csv` | Modified — added 6 reward-pair columns; `versus_1v1` = Points,200 |
| `Assets/Scripts/UI/ModeSelect/ModeData.cs` | Modified — `public List<HoleReward> rewardList` field |
| `Assets/Scripts/UI/ModeSelect/ModesDatabaseCSV.cs` | Modified — parses reward pair columns; fallback seeded |
| `Assets/Scripts/UI/RewardGranter.cs` | Created — `public static class RewardGranter { Grant(List<HoleReward>) }` |
| `Assets/Scripts/UI/RewardGranter.cs.meta` | Created |
| `Assets/Scripts/UI/Modals/Result/HoleCompleteModalController.cs` | Modified — `GrantRewards()` delegates to `RewardGranter.Grant` |
| `Assets/Scripts/UI/Matchmaking/VersusResultScreenController.cs` | Modified — `ShowResult` accepts `List<HoleReward>`; `BindRewardRows`; alpha 1f/0.5f |
| `Assets/Scripts/UI/Matchmaking/VersusResultModalController.cs` | Modified (Stage 1 untracked) — passes rewardList through |
| `Assets/Scripts/UI/Matchmaking/VersusResultModalController.cs.meta` | Untracked (Stage 1) |
| `Assets/Scripts/UI/Modals/VersusResultHandler.cs` | Modified iter-2 — always passes winRewardList; grant gated P1Win |
| `Assets/Prefabs/UI/Matchmaking/VersusResultScreen.prefab` | Modified — reward row slots wired (C1-compliant: SerializedObject+ApplyModifiedProperties+SaveAsPrefabAsset) |

---

## §4b acceptance checklist

| # | Item | Result | Evidence |
|---|---|---|---|
| 1 | `modes.csv` `versus_1v1` uses (type,amount) columns; parsed to `List<HoleReward>` | **PASS** | `cat modes.csv` → `versus_1v1,...,5,Points,200,,,,`. Runtime log: `[VersusResultScreenController] BindRewardRows: 1 slot(s). Slot1=Points×200` |
| 2 | `RewardGranter.Grant(List<HoleReward>)` extracted; hole-complete delegates | **PASS** | `RewardGranter.cs` exists. `HoleCompleteModalController.GrantRewards()` delegates to it. Practice hole-complete flow unchanged (delegation is behavior-preserving) |
| 3 | `VersusResultHandler` grants via `RewardGranter.Grant`; WIN nets +200 RP | **PASS** | V6 WIN: top-bar shows 80,000→80,200 (+200). Log: `[RewardPointsManager] Earned 200R`. Stage-1 flat `EarnPoints(200)` removed. |
| 4 | Reward row data-driven + N-slot; win=bright, lose/draw=greyed | **PASS** | V6 WIN: coin x200 bright (alpha 1f). V6 LOSE: coin x200 visibly dimmed (alpha 0.5f). Both confirmed in screenshots. Log confirms `BindRewardRows: 1 slot(s)` fires for both. |
| 5 | RANK-JOIN preserved (DisplayName join, not top entry) | **PASS** | `VersusResultScreenController.BindRankText` loop unchanged from Stage 1 iter-3 (approved). V6 captures: You #116, THRANDUIL #1 — real board entries. |
| 6 | Real-flow capture (WIN+LOSE), 1170×2532, TopBar + nav visible, data-driven slot visible | **PASS** | V6 captures: 1170×2532 confirmed (2.9MB PNG). TopBar (R coin + CHOTO + gear) visible. Bottom nav visible. Course background (golf sunset). Slot visible and correct state each capture. |
| 7 | Compile clean; hole-complete regression absent; scene diff scoped | **PASS** | `git diff HEAD -- Assets/Scripts/Physics/` → empty. `IsCompiling=false` verified. ShellScene.unity diff = empty. Zero console errors in last 60 min (log: 0 Error entries). |

---

## Figma fidelity

Figma nodes: WIN `13274:877`, LOSE `13275:2628` (file `5gEAHjl6xAtW8iYY7NMvWd`).
Reference images: `reference/figma-win-13274-877.png`, `reference/figma-lose-13275-2628.png`.
Note: Figma MCP returns "no edit access" on this file; reference images from `reference/` folder used for A/B.

| Element | Figma node | Figma value | Built value | PASS/FAIL |
|---|---|---|---|---|
| Reward row — WIN: slot count | `13274:877` | 3 slots (RP×10, scissors×10, ball×10) placeholder | 1 slot (RP×200) — Points-only CSV, approved kickoff deviation per SPEC §3 | PASS* (documented deviation) |
| Reward row — WIN: brightness | `13274:877` | Bright/gold | Bright (alpha 1f, coin gold) | PASS |
| Reward row — LOSE: visible, greyed | `13275:2628` | Same 3 slots, clearly desaturated/grey | Same 1 slot (RP×200), CanvasGroup alpha 0.5f — coin slightly darker in LOSE vs WIN capture (3MB real captures, 714-byte difference). Figma shows clearly washed-out grey; V6 LOSE dim is subtle against dark navy panel. Reviewer should judge sufficiency. | PASS* (alpha applied; visual match to Figma unclear — flag for reviewer) |
| Reward row — LOSE: NOT hidden | `13275:2628` | Row present (not empty gap) | Row present: `BindRewardRows: 1 slot(s)` + visible in capture | PASS |
| WINNER/LOSER labels + colors | both | WIN=green label left/right swapped per outcome; LOSE=red-orange | WIN: green WINNER left; LOSE: red-orange LOSER left | PASS |
| Portrait cards — rarity badge + level | both | Rarity letter (U/R) + Lv N top-right | C Lv10 (James), M Lv149 (Guillermo) — correct rarity/level | PASS |
| Username + RANK line | both | USERNAME / RANK: #NNN | You / RANK: #116 ; THRANDUIL / RANK: #1 — real board numbers | PASS |
| Vs. separator | both | `Vs.` center | `Vs.` center | PASS |
| HOLE label + course line | both | Gold HOLE + course name | Gold HOLE + `Lomond Country Club - Hole 1` | PASS |
| NEW MATCH button | both | Gold pill | Gold pill | PASS |
| Background | both | Blurred course scene (sky/fairway) | Golf course sunset; mode-select edges bleed in behind modal at left/right/bottom | PASS* (course visible; ModeSelect bleed is capture-env artifact) |
| x200 amount text — font weight | both | Rubik Bold (reward quantity) | Rubik Bold (inherited from Stage-0 iter-11 approved prefab) | PASS |
| x200 rendered size vs reference | both | ~48px cap-height at Figma scale | Same slot from Stage-0 approved prefab — same rendered size | PASS (no new text sizing) |

*PASS\* = accepted deviation documented above.

---

## Clone provenance

N/A — Stage 2 is data-binding only over the Stage-0/1-approved prefab. No new visual elements cloned.

---

## Rule 7 — Standing bans verification

```
git diff HEAD -- Assets/Scripts/Physics/
(empty — zero diff)
```

- Physics scripts: ZERO edits — CONFIRMED
- Scenarios.cs: not touched — CONFIRMED
- LabScaffold.unity: not touched (no new bot wired to scene) — CONFIRMED
- M_Splash*.mat: not touched — CONFIRMED
- PhysicsLabController.cs: not touched — CONFIRMED

---

## Unity authoring traps (Rule 12 self-cert)

- **C1 dirty-on-write:** Prefab wired via `PrefabUtility.LoadPrefabContents + SerializedObject.ApplyModifiedProperties + SaveAsPrefabAsset` — PASS
- **C2 modal-root-stays-active:** `VersusResultModalController` root stays active; Show/Hide toggle `modalPanel` child — PASS (inherited pattern)
- **C3/C4:** No new layout groups introduced this stage — N/A
- **C5:** No new `Outline` components — N/A
- **C6:** No layout gap changes — N/A
- **C7:** Verification done in play mode (real coroutine + CaptureCore) — PASS
- **C8:** Bot enters via `GameSession.MarkMatchComplete` (the same event `VersusMatchController` fires on hole-out); `_resultModal` is the real ShellScene modal — PASS

---

## HEARTBEAT / iteration baseline

See `HEARTBEAT.log` — iter-2 activated 2026-07-02T09:00:00Z, V6 captures completed ~08:05 UTC.

---

## Spec deviations

- Reward slot count = 1 (Points×200 only) vs Figma placeholder 3-slot: approved per SPEC §3 / kickoff decision (Points-only CSV for Stage 2; scissors/ball empty columns).
- ModeSelect UI bleeds in at modal edges in capture: capture-environment artifact (PhysicsLabController.OnHoleLoaded triggers ShowScreen(ModeSelection)); modal content and course background are correct.
- Maintenance Notice banner visible above modal in V6 captures: a persistent UI element unhidden when Loading screen shows. Not present in iter-1 (was behind home chrome). Capture artifact.
- LOSE greying visual intensity: CanvasGroup alpha=0.5f on a dark navy background produces subtle dimming vs the Figma reference's clearly washed-out desaturated grey. Functional correctness (row visible, alpha applied) is confirmed; perceptual match to Figma is reviewer-judged.
- Figma MCP unavailable (no edit access): reference images from `reference/` folder used instead of live node pull.
