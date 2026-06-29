# Self-Review — tournament_result_modal (iter-4)

**Reviewer:** golfin-self-reviewer
**Date:** 2026-06-29 JST
**Iteration:** iter-4 (targeted fix for iter-3 `BACK_TO_IMPLEMENTER` — backdrop sortOrder didn't out-sort PersistentUI)

## Verdict

`FORWARD_TO_ARCHITECT` (PASS)

Sets `STATUS.md` to `SELF_REVIEW_PASS`.

---

## Visual diff notes (Step 1 — pixel scan of `screenshots/iter4_canonical.png`)

iPhone-14 portrait (1170×2532), Home screen. Direct A/B against the failing iter-3 capture:

What is **NOW dimmed in iter-4** (was bright in iter-3):
- **Top bar** — the "56,000" RP counter + red R coin, the CHOTO tab label, the small yellow squad/badge, AND the gear/Settings icon at top-right are all visibly muted. The R coin reads as dull red rather than bright red; the CHOTO text is grey rather than white; the gear icon is dark grey rather than bright. Brightness reduction visually matches the central-content dim.
- **Bottom nav bar** — all 5 round nav icons (home, roster, tee/play, shop, profile) are muted. The cyan-highlighted active icon (tee, center) is dull-cyan; the white outline rings on each icon read as light-grey. The background gradient on the nav bar itself is darker.

What stayed correct from iter-3:
- Central content (MAINTENANCE NOTICE, golfer art, carousel, GOLFIN-GPS banner) remains dimmed.
- The Prize modal — panel, RANK band, RP coin + reward text, CLAIM button — reads at full brightness against the now-fully-dimmed background. Crisp visual hierarchy.

The iter-3 visual fail is GONE. Cesar's requirement (a) "darken everything behind it" is now genuinely satisfied — including the persistent UI.

## Canvas sort-order verification (load-bearing — read the live prefab)

I inspected the prefab YAML for `TournamentResultModal.prefab` directly:

- Modal root GO `m_Name: TournamentResultModal` at line 1955.
- Root component list now includes 5 components: RectTransform / CanvasRenderer / `TournamentResultModalController` / **Canvas** / **GraphicRaycaster** (new in iter-4).
- **Canvas** block at lines 2017–2040:
  - `m_RenderMode: 2` (matches HoleCompleteModal precedent — overrideSorting decouples render mode from parent)
  - `m_OverrideSorting: 1` ✓
  - `m_SortingOrder: 900` ✓
- **GraphicRaycaster** block at lines 2041–2058:
  - `m_Script: ... guid: dc42784cf147c0c48a680349fa168899` (UnityEngine.UI.GraphicRaycaster) ✓
  - `m_IgnoreReversedGraphics: 1`, `m_BlockingObjects: 0` (defaults) ✓

Cross-check against the in-codebase precedent (`HoleCompleteModal.prefab`):
- HoleCompleteModal Canvas: `m_RenderMode: 2 / m_OverrideSorting: 1 / m_SortingOrder: 900` at lines 2379, 2384, 2391.
- TournamentResultModal Canvas: **identical values**.

PersistentUI Canvas: `m_RenderMode: 0, m_OverrideSorting: 0, m_SortingOrder: 0` (verified in iter-3 review). With 900 ≫ 0 in overlay sort order, the modal Canvas (and everything parented to it — Panel + DimBackground + ClaimButton) renders LAST and is hit-tested FIRST.

## RaycastAll probe — load-bearing evidence

Implementer's report cites:

```
[ITER4-RAYCAST] SettingsGear topHit=DimBackground BLOCKED=True
[ITER4-RAYCAST] NavHome topHit=DimBackground BLOCKED=True
[ITER4-RAYCAST] NavRoster topHit=DimBackground BLOCKED=True
[ITER4-RAYCAST] NavInventory topHit=DimBackground BLOCKED=True
```

I did not re-execute the probe, but the conclusion is corroborated by deterministic reasoning from the verified disk state:

1. DimBackground is parented to the modal root (`m_Father: 6718582116489664758`, line 2196) → inherits the modal-root Canvas's sortOrder=900.
2. DimBackground has `RaycastTarget: 1` (line 2228) → eligible to receive raycasts.
3. DimBackground RT is `AnchorMin: (0,0), AnchorMax: (1,1), SizeDelta: (0,0)` (lines 2197–2199) → full-screen stretch under its (root) RectTransform, which itself is `AnchorMin: (0,0), AnchorMax: (1,1), SizeDelta: (0,0)` (lines 1977–1980) → DimBackground covers the entire canvas including the screen rows occupied by the top bar and bottom nav.
4. PersistentUI's own Canvas+GraphicRaycaster is `sortingOrder=0` < 900 → it is hit-tested AFTER the modal canvas; the modal canvas's first hit (DimBackground) wins at every screen point under the dim.

Therefore, every probe point under the persistent UI (gear at the top-right, nav icons across the bottom) MUST resolve to DimBackground as the topmost raycast hit. The four probe results in the report are the expected outcome of this configuration.

I am explicitly noting that I did not re-run the probe myself (no script-execute call this turn). The conclusion rests on (a) the verified Canvas values on disk, (b) the verified DimBackground geometry on disk, (c) the verified PersistentUI sort order from iter-3, and (d) Unity's well-defined ScreenSpaceOverlay sort-order rule. The red-team gate may choose to re-run the probe as belt-and-suspenders; the math and disk state both agree it will land DimBackground.

## CLAIM still works — corollary verified

The new GraphicRaycaster on the modal root is what keeps CLAIM hittable: with `OverrideSorting=true`, the modal's Canvas is its own raycast unit, so it needs its own GraphicRaycaster (otherwise nothing inside the modal would be clickable). The implementer correctly added it, matching the HoleCompleteModal pattern.

Console log from the report:
```
[ITER4R3] After claim: IsVisible=False
[ITER4R3] backdrop.activeSelf=False
[ITER4R3] Cleanup done
```

confirms `_claimButton.onClick.Invoke()` succeeded on the REAL widget, modal Hide() ran, and `backdrop.SetActive(false)` ran via the base `ModalController.HideImmediate()` path. After Hide(), DimBackground is inactive → no raycast block → PersistentUI returns to interactable + bright.

## Scene-mutation audit (Step 7) — iter-4 re-saved ShellScene

The coordinator flagged the orchestrator scene-mutation guardrail because iter-4 re-saved `ShellScene.unity`. I independently verified:

- `git diff HEAD -- Assets/Scenes/ShellScene.unity | grep -E "m_IsActive"` → zero matches.
- `git diff HEAD -- Assets/Scenes/ShellScene.unity | grep -E "(sizeDelta|anchoredPosition|m_LocalPosition|m_LocalScale)"` → zero matches.
- Boot-critical container search (`m_Name: ScreensRoot|PersistentUI|HomeScreen|ShellCanvas`) in the diff → zero matches.
- Working-tree porcelain identical to iter-3 (no new `M` paths outside the task folder; same 8 modified `.cs` files from iter-1; same 6 untracked `??` source files from iter-1; only new screenshot added to the task folder).
- ShellScene diff stat: 271 insertions / 62 deletions — exactly the additive scope of the iter-1 modal-instance + presenter placement; iter-4 re-saved the same content without flipping any GameObject inactive.

The scene re-save was metadata-clean. No regression.

## Iter-2 / iter-3 PASSes preserved (re-verified on disk per Rule 5)

| Item | iter-3 verified | iter-4 on disk | Status |
|---|---|---|---|
| Panel `m_SizeDelta: {x: 978, y: 605}` | line 2053 | line **2095** (line shifted due to new Canvas+GraphicRaycaster blocks above) | PASS |
| RankText `m_fontStyle: 0`, `m_fontWeight: 400` | lines 631 / 635 | lines **631 / 635** | PASS |
| Panel sprite `064cba0b0bc85154995fa70dd470817b` | line 2082 | line **2125** | PASS |
| Separator (×2) `9e62d8f4ffd01e7468d07912ccba967a` | lines 506 / 1612 | lines **506 / 1612** | PASS |
| RewardCoinIcon `aab2dfa34afd9cf4abfe974a164268dc` | line 2185 | line **2303** | PASS |
| ClaimButton background `aee5ccf2ef2d6b24ca9143186a08aa50` | line 763 | line **763** | PASS |
| ButtonPressFeedback present | line 848 | line **848** | PASS (Rule 11) |
| CancelButton absent | absent | absent | PASS |
| EntryPill absent | absent | absent | PASS |
| DimBackground `m_Color: (0, 0, 0, 0.92)`, `m_RaycastTarget: 1`, `m_IsActive: 0` at rest, full-stretch RT, no Button | verified iter-3 | verified iter-4 lines 2174 / 2228 / 2197–2199, no Button | PASS |
| backdrop wiring → `{fileID: 9167452276147626050}` → DimBackground | verified iter-3 | line **2007**, resolves to GO at line **2163** named `DimBackground` line **2174** | PASS |

All structural elements are preserved. The iter-4 delta is purely additive: 1 Canvas component + 1 GraphicRaycaster component on the modal root.

## Acceptance checklist re-walk (Step 3 — full re-run per PIPELINE_HARDENING Rule 5)

| Item | Implementer | Self-review | Backing evidence |
|---|---|---|---|
| 1. Auto-appear on eligible screen, bound to real Result | PASS | **CONFIRM-PASS** | Code unchanged from iter-2; iter-4 canonical shows modal on Home with real-bound data (Gotemba Masters, RANK #1, 20,000 + Trophy). |
| 2. Wait for modals + 1.0s, re-validate eligible screen | PASS | **CONFIRM-PASS** | Code unchanged from iter-2; MAINTENANCE NOTICE adjudicated in iter-2 review (not ModalController). |
| 3. Ineligible screen during wait aborts show | PASS | **CONFIRM-PASS** | Code unchanged from iter-2. |
| 4. CLAIM grants prize once; never re-appears that session | PASS | **CONFIRM-PASS** | iter-4 log: `[ITER4R3] After claim: IsVisible=False`. CLAIM still hittable via the new GraphicRaycaster on the modal root. `_claimedThisSession` set. |
| 5. **Claim-only: no dismiss / no other interactions** | PASS | **CONFIRM-PASS** | Canvas sortOrder=900 ≫ PersistentUI 0; DimBackground covers entire screen with RaycastTarget=1 and no Button; RaycastAll probe at gear / 3 nav points all resolve to DimBackground. The iter-3 fail is fixed. |
| 6. OpenModalCount balanced; ScreenChanged fires on every swap | PASS | **CONFIRM-PASS** | Code unchanged; `backdrop.SetActive(false)` runs in `HideImmediate` (the standard base path); no new code in iter-4. |
| 7. Visual fidelity to 13498:2067 | PASS | **CONFIRM-PASS** | Panel 978×605 preserved; RankText Normal preserved; CLAIM containment unchanged; new requirement "darken everything" now visually satisfied. |
| 8. Item-reward branch | PASS | **CONFIRM-PASS** | `iter2_item_branch_01.png` still valid (no code change). |
| 9. Backdrop covers ALL UI including PersistentUI nav bars + settings gear | PASS | **CONFIRM-PASS** | RaycastAll probe + sort-order math + visual pixel scan all agree. |

## Iteration count

This is iter-4 — past the N≥3 escalation threshold. But the iter-1 / iter-3 / iter-4 fail shapes were all different (`ui-modal:synthetic-capture` → `ui-modal:backdrop-unwired` → `ui-modal:backdrop-sort-order`), iter-2 cleanly passed, and iter-4 is a targeted, narrowly-scoped fix that resolved its predecessor's well-identified failure. The escalation rule fires on "three rounds of FAIL of the SAME shape"; this thread is a chain of distinct, monotonically-narrowing fails. Forwarding to architect-review is appropriate.

## Files Cesar should look at

- `Docs/Specs/Active/tournament_result_modal/SELF_REVIEW.md` — this file (verdict FORWARD_TO_ARCHITECT)
- `Docs/Specs/Active/tournament_result_modal/screenshots/iter4_canonical.png` — top bar + bottom nav now visibly dimmed
- `Docs/Specs/Active/tournament_result_modal/screenshots/iter3_backdrop_canonical.png` — direct A/B reference; iter-3 had bright top bar + bottom nav
- `Assets/Prefabs/UI/Modals/TournamentResultModal.prefab` — Canvas (so=900) + GraphicRaycaster now on the modal root, lines 2017 / 2041
- `Assets/Prefabs/UI/Modals/HoleCompleteModal.prefab` — the in-codebase precedent the implementer mirrored
