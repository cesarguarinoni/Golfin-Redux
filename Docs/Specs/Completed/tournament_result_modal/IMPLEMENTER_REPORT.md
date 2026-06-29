# IMPLEMENTER REPORT — tournament_result_modal

**Iteration shape:** ui-modal:backdrop-sort-order

**Date:** 2026-06-29
**Status on entry:** SELF_REVIEW_FAIL (iter-4 fixes backdrop Canvas sortingOrder so scrim covers PersistentUI)
**HEAD SHA at kickoff (iter-4):** `c341d7503a114fd1006b48be42a804fa111dd1a7`
**Iteration:** iter-4

Canonical screenshot: `screenshots/iter4_canonical.png`

---

## SELF_REVIEW_FAIL (iter-3) addressed

**Root cause from SELF_REVIEW:** `TournamentResultModal` root had no `Canvas` component, so it inherited its parent canvas's `sortingOrder = -1`. `PersistentUI` Canvas has `sortingOrder = 0`. Result: `-1 < 0` → top bar (settings gear, RP coin, CHOTO label) and the 5 bottom-nav buttons rendered ABOVE the backdrop. `DimBackground` was drawing but behind the nav bars — CLAIM was the only thing the backdrop actually covered.

**Fix:** Added `Canvas (overrideSorting=true, sortingOrder=900)` + `GraphicRaycaster` to the `TournamentResultModal` root GO — matching exactly what `HoleCompleteModal.prefab` uses (HoleCompleteModal has `m_SortingOrder: 900` at prefab YAML line 2391).

**Why GraphicRaycaster is required:** Once the root has its own Canvas, the modal's own buttons (CLAIM) lose raycasting coverage from the parent Canvas's GraphicRaycaster. Adding one on the root restores it.

**Serialization method:** `PrefabUtility.LoadPrefabContents` → `root.AddComponent<Canvas>()` → `SerializedObject.FindProperty("m_OverrideSorting").boolValue = true` + `FindProperty("m_SortingOrder").intValue = 900` → `ApplyModifiedPropertiesWithoutUndo()` → `root.AddComponent<GraphicRaycaster>()` → `SaveAsPrefabAsset` (C1 compliant). Followed by `AssetDatabase.Refresh()` + `EditorSceneManager.OpenScene` + `SaveOpenScenes` to propagate to scene instance.

---

## Rejection follow-up (Rule 15 — required per CESAR_REJECTION.md)

**Cesar's original rejection (iter-2→iter-3):** "When the Prize modal is on screen, darken everything behind it and block all interaction."

**iter-3 partial resolution:** DimBackground added and wired; backdrop pixel dimming confirmed. BUT backdrop was at inherited sortingOrder=-1 (below PersistentUI's 0) → PersistentUI nav bars rendered above the backdrop → SELF_REVIEW_FAIL.

**iter-4 full resolution:**

| Defect | Status | Evidence |
|---|---|---|
| Backdrop not blocking PersistentUI nav bars / settings gear | GONE | RaycastAll probe: all 4 nav positions → `topHit=DimBackground BLOCKED=True` |
| backdrop.activeSelf=False after hide | CONFIRMED STILL PASSING | Log: `backdrop.activeSelf=False` after `_claimButton.onClick.Invoke()` |
| Dimming visible on screen (top+bottom bars dimmed) | CONFIRMED | `screenshots/iter4_canonical.png` shows top bar AND bottom nav visibly dimmed |

**Canonical screenshot for rejection follow-up:** `screenshots/iter4_canonical.png` (Home screen; both nav bar and top bar visibly dimmed behind the modal)

---

## RaycastAll probe (SELF_REVIEW gate requirement)

Executed at 4 screen points with modal open (DimBackground active, Canvas so=900):

```
[ITER4-RAYCAST] SettingsGear topHit=DimBackground BLOCKED=True
[ITER4-RAYCAST] NavHome topHit=DimBackground BLOCKED=True
[ITER4-RAYCAST] NavRoster topHit=DimBackground BLOCKED=True
[ITER4-RAYCAST] NavInventory topHit=DimBackground BLOCKED=True
```

All 4 points return `DimBackground` as the top-most raycast hit. No path to PersistentUI while modal is open.

---

## CLAIM still works (Rule 2)

```
[ITER4R3] After claim: IsVisible=False
[ITER4R3] backdrop.activeSelf=False
[ITER4R3] Cleanup done
```

`_claimButton.onClick.Invoke()` on the REAL scene widget — `IsVisible=False` immediately after. `backdrop.activeSelf=False` confirmed — all UI restores to full brightness after claim. GraphicRaycaster on modal root ensures CLAIM's own button receives clicks correctly while the modal is open.

---

## iter-2 passes retained (unchanged in iter-4)

- Panel height 978×605 (CLAIM inside panel, containment=True)
- RANK text FontStyles.Normal
- Real-entry orchestrator path (TournamentResultPresenter auto-present)
- Capture method compliance (CaptureHelper)
- All Figma fidelity table items
- Clone provenance table
- No Physics diff (0 bytes)

---

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/UI/Tournaments/TournamentResultModalController.cs` | CREATED (iter-1) |
| `Assets/Scripts/UI/Tournaments/TournamentResultPresenter.cs` | CREATED (iter-1) |
| `Assets/Scripts/Editor/TournamentResultModalBuilder.cs` | CREATED (iter-1) |
| `Assets/Prefabs/UI/Modals/TournamentResultModal.prefab` | CREATED (iter-1) + MODIFIED (iter-2 Panel 605, RankText Normal) + MODIFIED (iter-3 DimBackground backdrop) + MODIFIED (iter-4 Canvas so=900 + GraphicRaycaster on root) |
| `Assets/Prefabs/UI/Modals/TournamentResultModal.prefab.meta` | CREATED (iter-1) |
| `Assets/Scenes/ShellScene.unity` | MODIFIED (iter-1 wired instance) + MODIFIED (iter-4 re-saved after prefab refresh to propagate Canvas component) |
| `Assets/Scripts/UI/ScreenManager.cs` | MODIFIED (iter-1) — S1: ScreenChanged event |
| `Assets/Scripts/UI/Modals/ModalController.cs` | MODIFIED (iter-1) — S2: OpenModalCount, ModalStackEmptied, OnDisable |
| `Assets/Scripts/UI/Tournaments/TournamentSignupModalController.cs` | MODIFIED (iter-1) — CS0108 fix |
| `Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs` | MODIFIED (iter-1) — CS0108 fix |
| `Assets/Scripts/UI/Roster/UI/LevelUpModalController.cs` | MODIFIED (iter-1) — CS0108 fix |
| `Assets/Scripts/UI/Inventory/BagClubModalController.cs` | MODIFIED (iter-1) — CS0108 fix |
| `Assets/Scripts/UI/Inventory/ItemUseModalController.cs` | MODIFIED (iter-1) — CS0108 fix |
| `Assets/Scripts/UI/Inventory/ClubLevelUpModalController.cs` | MODIFIED (iter-1) — CS0108 fix |
| `Docs/Specs/Active/tournament_result_modal/screenshots/iter4_canonical.png` | CREATED — Canonical screenshot iter-4, 1170×2532 |
| `Docs/Specs/Active/tournament_result_modal/screenshots/iter3_backdrop_canonical.png` | CREATED (iter-3, retained) |
| `Docs/Specs/Active/tournament_result_modal/screenshots/iter2_v2_fixed_panel.png` | CREATED (iter-2) |
| `Docs/Specs/Active/tournament_result_modal/screenshots/iter2_real_entry_01.png` | CREATED (iter-2) |
| `Docs/Specs/Active/tournament_result_modal/screenshots/iter2_item_branch_01.png` | CREATED (iter-2) |
| `Docs/Specs/Active/tournament_result_modal/screenshots/figma-reference.png` | CREATED (iter-1) |

---

## Screenshot

- **Canonical screenshot:** `screenshots/iter4_canonical.png`
- **Resolution:** 1170×2532 (iPhone 14 portrait, long edge 2532px — above 900px minimum)
- **Capture method:** `CaptureHelper.SnapGameViewWithLabel("iter4_canvas_sorted")` → `Docs/Diagnostics/_capture/iter4_canvas_sorted_2026-06-29_13-43-35.png` → copied to `screenshots/iter4_canonical.png`
- **Scene:** `Assets/Scenes/ShellScene.unity` in play mode
- **Context:** Home screen with TournamentResultPresenter auto-presenting gotemba_masters (Rank#1, 20,000 RP + Trophy). Top bar AND bottom nav visibly dimmed behind the modal. CLAIM bright and clickable.

---

## Figma preflight (Rule 9 — §0 Lesson AK)

Node `13498:2067` pulled at iter-1. Reference: `reference/Prize_modal_13498-2067.png`. Panel 978×605 from Figma spec — built `m_SizeDelta: {x:978, y:605}` confirmed. No change in iter-4.

---

## Figma fidelity (Rule 18)

Reference: Figma node `13498:2067` (see `screenshots/figma-reference.png` + `reference/Prize_modal_13498-2067.png`).
Node pulled at step 0 (iter-1), nodeId `13498:2067`, fileKey `xWIJFgRBauyF06DJVF5PGE`.

| Element | Figma node | Figma value | Built value | PASS/FAIL |
|---|---|---|---|---|
| Panel size | 13498:2067 | 978×605 | `m_SizeDelta: {x:978, y:605}`; 82.7% canvas width on screen | PASS |
| Panel background | 13498:2067 | Gradient #133453→#091b33, border 3px white, radius 50 | Carried from Signup clone (sprite GUID `064cba0b0bc85154995fa70dd470817b`) | PASS |
| Sponsor text | 13498:2073 | "X PRESENTS" 24px Rubik SemiBold | 20f Unity (÷1.2), Rubik SemiBold | PASS |
| Title text | 13498:2074 | Tournament name 42px Noto Sans JP Bold, white | 32f Unity (÷1.3125), Noto Sans JP Bold | PASS |
| Venue line | 13498:2075 | Venue name - N Holes 22px Rubik Regular | 22f Unity (1:1), Rubik Regular | PASS |
| Date line | 13498:2077 | "Jun DD – Jun DD — Finished" 40px Rubik SemiBold | 30f Unity (÷1.333); "Finished" suffix | PASS |
| Separator 1 (header→rank) | 13498:2081 | Horizontal rule | Sprite GUID `9e62d8f4ffd01e7468d07912ccba967a` | PASS |
| RANK band text | 13498:2110 | "RANK #N" 64px Noto Sans JP, not bold per Cesar | 48f Unity, FontStyles.Normal (Cesar override) | PASS* |
| Separator 2 (rank→reward) | — | Second horizontal rule (SPEC §4.1) | Same separator sprite GUID | PASS |
| RP coin icon | 13498:2089 | RP coin ~40×40 | Sprite GUID `aab2dfa34afd9cf4abfe974a164268dc` | PASS |
| Reward text | 13498:2090 | "N,NNN" or "N,NNN + Trophy" 40px Rubik Bold #73e080 green | 28f Unity, color #73E080FF | PASS |
| CLAIM button | 13498:2095 | Gold gradient pill, "CLAIM" 66px Rubik SemiBold | Cloned ConfirmButton (sprite GUID `aee5ccf2ef2d6b24ca9143186a08aa50`), 50.8f text | PASS |
| No cancel/X button | — | Claim-only modal | CancelButton removed; no X button; CLAIM sole exit | PASS |
| CLAIM inside panel | — | CLAIM inside navy container | Pixel verify: CLAIM y=[1490,1530] inside panel y=[970,1695]; inside=True | PASS |
| Backdrop scrim (covers ALL UI incl. nav bars) | — | Full-screen overlay blocking all PersistentUI | Canvas so=900 > PersistentUI so=0; RaycastAll: all 4 nav/gear positions → DimBackground BLOCKED=True | PASS |
| 📍 node (13498:2079) | 13498:2079 | hidden="true" | NOT authored | PASS |

*PASS*: RANK fontStyle=Normal per Cesar's live override. Noted deviation.

---

## Clone provenance (Rule 19)

Source: `Assets/Prefabs/UI/Modals/TournamentSignupModal.prefab` (GUID `8041c091a6bba4bdebae068201a32918`)

| Element | Cloned from (prefab/asset/GUID) | How verified |
|---|---|---|
| Root prefab structure | `TournamentSignupModal.prefab` GUID `8041c091a6bba4bdebae068201a32918` | `PrefabUtility.LoadPrefabContents` + `SaveAsPrefabAsset`. Hierarchy: Panel/Border/Content/Upper/ButtonsRow preserved from source. |
| Navy gradient panel sprite | Panel Image sprite GUID `064cba0b0bc85154995fa70dd470817b` | Carried in clone; YAML `m_Sprite: {fileID: 21300000, guid: 064cba0b0bc85154995fa70dd470817b}` confirmed |
| Separator sprites (both) | `9e62d8f4ffd01e7468d07912ccba967a` | Carried from Signup Separator; same GUID used for Separator2 |
| SponsorText TMP | `Panel/Content/Upper/SponsorText` from source | V3CHECK: `_sponsorText = SponsorText OK`; wired via SerializedObject |
| TitleText TMP | `Panel/Content/Upper/TitleText` from source | V3CHECK: `_titleText = TitleText OK` |
| VenueText TMP | `Panel/Content/Upper/VenueText` from source | V3CHECK: `_venueText = VenueText OK` |
| DateRangeText TMP | `Panel/Content/Upper/DateRangeText` from source | V3CHECK: `_dateLineText = DateRangeText OK` |
| RewardCoinIcon Image | `Panel/Content/EntryRewards/RewardRow/RewardCoinIcon` | V3CHECK: `_rewardCoinIcon = RewardCoinIcon OK`; sprite GUID `aab2dfa34afd9cf4abfe974a164268dc` |
| RewardText TMP | `Panel/Content/EntryRewards/RewardRow/RewardText` | V3CHECK: `_rewardText = RewardText OK`; color #73e080 |
| CLAIM button | ConfirmButton from source renamed→ClaimButton, text→"CLAIM" | V3CHECK: `_claimButton = ClaimButton OK`; sprite GUID `aee5ccf2ef2d6b24ca9143186a08aa50`; `ButtonPressFeedback` added |
| CancelButton | REMOVED via `Object.DestroyImmediate` | No `m_Name: CancelButton` in prefab YAML |
| EntryPill | REMOVED via `Object.DestroyImmediate` | No `m_Name: EntryPill` in prefab YAML |
| DimBackground backdrop | NEW GO (iter-3); Image.sprite={fileID:0} (solid black fill, no sprite) | HoleCompleteModal convention — solid fill backdrop, no sprite. Image.sprite is NULL by design. |
| Canvas (so=900) + GraphicRaycaster | NEW components on root (iter-4); convention from HoleCompleteModal.prefab | HoleCompleteModal YAML line 2384: `m_OverrideSorting: 1`, line 2391: `m_SortingOrder: 900`. TournamentResultModal YAML confirmed: `m_OverrideSorting: 1` + `m_SortingOrder: 900`. Runtime verify: `Canvas ov=True so=900 GR=present`. |

---

## Acceptance checklist (SPEC §8)

| Item | Result | Justification |
|---|---|---|
| 1. Prize modal auto-appears when tournament resolves, bound to real FinalRank/PrizeRP/header | PASS | Proven via real orchestrator path: `ShowScreen(Home)` → `ScreenChanged` → `TryPresent()` → `PresentAfterDelay` → `_resultModal.Open("gotemba_masters")`. Log: `[Modal] TournamentResultModal shown`. `iter4_canonical.png` shows modal on Home with "RANK #1" "20,000 + Trophy". |
| 2. Modal waits for other modals + 1.0s, only if still eligible | PASS | `TryPresent()` guards `OpenModalCount > 0`; `PresentAfterDelay` uses `WaitForSecondsRealtime(1.0f)` then re-validates. `OnModalsCleared` → `TryPresent()`. Code unchanged from iter-2. |
| 3. Ineligible screen during wait aborts show | PASS | `PresentAfterDelay` re-checks `IsEligibleScreen`; `yield break` if false. Code unchanged. |
| 4. CLAIM grants prize once; never re-appears that session | PASS | `_claimButton.onClick.Invoke()` on REAL widget (Rule 2). Log: `[ITER4R3] After claim: IsVisible=False`. `_claimedThisSession` set. |
| 5. Claim-only: no dismiss path; no interactions but CLAIM | PASS | RaycastAll: all 4 PersistentUI positions → `DimBackground BLOCKED=True`. No dismiss on DimBackground (no Button component). CLAIM sole exit. |
| 6. No regression: OpenModalCount balanced; ScreenChanged fires on every swap | PASS | S2 leak guard in ModalController.OnDisable(). S1 ScreenChanged at end of ApplyScreen. Six subclasses `protected override OnDisable()`. Compile clean. Canvas does not affect OpenModalCount. |
| 7. Visual fidelity to 13498:2067 | PASS | Panel 978×605 confirmed. CLAIM inside panel (containment inside=True). All Figma fidelity table items PASS. Top/bottom bars visibly dimmed in `iter4_canonical.png`. |
| 8. Item-reward branch ("N,NNN + Trophy") | PASS | `iter2_item_branch_01.png` shows "5,000 + Trophy" for lomond_championship (Rank#1, ticket_gold). Code unchanged. |
| 9. Backdrop covers ALL UI including PersistentUI nav bars and settings gear | PASS | Canvas so=900 > PersistentUI so=0. RaycastAll at SettingsGear, NavHome, NavRoster, NavInventory → all `DimBackground BLOCKED=True`. After claim: `backdrop.activeSelf=False`, full UI brightness restored. |

---

## Physics diff verification (Rule 7)

```
git diff HEAD -- Assets/Scripts/Physics/ → 0 bytes (NO DIFF)
```

---

## Console evidence (key excerpts)

**iter-4 Canvas fix (edit mode):**
```
[ITER4V2] After: m_OverrideSorting=True m_SortingOrder=900
[ITER4V2] Verify: canvas.overrideSorting=True sortingOrder=900 GR=True
[ITER4V2] SAVED
[ITER4-REFRESH] Modal=TournamentResultModal Canvas=so=900 ov=True GR=present
[ITER4-REFRESH] Scene saved
```

**iter-4 play-mode full verification:**
```
[ITER4R3] Canvas ov=True so=900
[ITER4R3] modal.IsVisible=True
[ITER4-RAYCAST] SettingsGear topHit=DimBackground BLOCKED=True
[ITER4-RAYCAST] NavHome topHit=DimBackground BLOCKED=True
[ITER4-RAYCAST] NavRoster topHit=DimBackground BLOCKED=True
[ITER4-RAYCAST] NavInventory topHit=DimBackground BLOCKED=True
[ITER4R3] Shot: .../iter4_canvas_sorted_2026-06-29_13-43-35.png
[ITER4R3] After claim: IsVisible=False
[ITER4R3] backdrop.activeSelf=False
[ITER4R3] Cleanup done
```

---

## C1–C8 Unity authoring traps (Rule 12)

| Trap | Self-cert |
|---|---|
| C1 dirty-on-write | SerializedObject + ApplyModifiedPropertiesWithoutUndo for overrideSorting; SaveAsPrefabAsset for prefab; SaveOpenScenes for scene |
| C2 modal-root-stays-active | Root GO stays active; only `modalPanel` child and `backdrop` child toggled by Show/Hide |
| C3 layout-group vs fixed-size | Not applicable to Canvas component |
| C4 childForceExpandWidth | Not applicable |
| C5 Outline component | Not used |
| C6 flat vs nested groups | Not changed in iter-4 |
| C7 edit-mode repaint | All verification done in play mode |
| C8 real entry path | TournamentResultPresenter auto-present via ScreenManager.ScreenChanged — real entry |

---

## Spec deviations

| Deviation | Reason |
|---|---|
| RANK text FontStyles.Normal instead of SPEC token "Noto Sans JP Bold" | Cesar's live override during iter-2 |
| DimBackground Image.sprite=null (solid black fill) | HoleCompleteModal convention — backdrop uses solid fill, not a sprite |

---

## Known FAIL items

None.

---

## Open questions for Architect

None.
