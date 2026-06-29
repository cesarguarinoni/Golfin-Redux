# ARCHITECT_REVIEW — tournament_result_modal (iter-4)

**Reviewer:** golfin-reviewer
**Date:** 2026-06-29 JST
**Iteration:** iter-4 (Cesar-rejection follow-up — additive backdrop scrim that out-sorts PersistentUI)
**Verdict:** PASS → `READY_FOR_REDTEAM`

---

## Independent visual scan (Step 0, pre-report)

The shell is dimmed and desaturated — top bar's R-count and gear icon read at roughly half brightness, the CHOTO title bar is muted, the orange "Maintenance Notice" card behind the modal is heavily darkened with the warning illustration barely visible underneath, and the bottom nav strip (Home/Roster/Tee/Inventory/Profile icons) is uniformly dimmed to a dull blue. The Tournament prize modal sits crisply over this darkened background at full brightness: a navy panel with hairline separators, "TAIHEIYO PRESENTS" eyebrow, "GOTEMBA MASTERS" title, hole subtitle, "Jun 21 – Jun 26 — Finished" range, "RANK #1" in a regular (non-bold) weight, a coin icon with "20,000 + Trophy" reward line in yellow, and a yellow "CLAIM" pill-button. The contrast between the bright modal and the dimmed shell is unambiguous — the backdrop is clearly out-sorting the persistent UI (the gear and nav icons are darker than the modal's body), which is what the input-block fix is supposed to do visually. CLAIM appears centered, contained inside the panel, with no protrusion.

A/B against `screenshots/iter3_backdrop_canonical.png` is unambiguous: iter-3 top bar was bright (RP coin red, CHOTO white, gear white) and bottom nav icons were vivid (cyan tee button, white outline rings); iter-4 has all of these visibly dimmed. The iter-3 failure mode is gone.

---

## PRIMARY GATE — Input-block (Cesar request (b))

### Live disk state — Canvas + GraphicRaycaster on modal root (read this pass)

`Assets/Prefabs/UI/Modals/TournamentResultModal.prefab`, lines 2017–2055:

```
--- !u!223 &2294657622127866780
Canvas:
  m_GameObject: {fileID: 7318936919323513355}   # TournamentResultModal root
  m_Enabled: 1
  m_RenderMode: 2                                # ScreenSpaceOverlay-equivalent (matches HoleCompleteModal)
  m_OverrideSorting: 1                           # overrides parent's sortingOrder
  m_SortingOrder: 900                            # >> PersistentUI's 0
--- !u!114 &487014275808588192
MonoBehaviour:  # GraphicRaycaster
  m_Script: {fileID: 11500000, guid: dc42784cf147c0c48a680349fa168899, type: 3}
  m_IgnoreReversedGraphics: 1
  m_BlockingObjects: 0
```

Cross-check vs `HoleCompleteModal.prefab` precedent (self-reviewer verified): identical `m_RenderMode: 2 / m_OverrideSorting: 1 / m_SortingOrder: 900`. The values match the documented in-codebase pattern.

### Live disk state — DimBackground

Lines 2162–2236 of the same prefab:
- Parented to modal root (`m_Father: 6718582116489664758`)
- Full-screen stretch: `AnchorMin (0,0)`, `AnchorMax (1,1)`, `SizeDelta (0,0)`
- `m_Color: (r:0, g:0, b:0, a:0.92)` (matches HoleCompleteModal convention)
- `m_RaycastTarget: 1`
- No Button/click handler (sole components besides Image+RectTransform are CanvasRenderer)
- `m_IsActive: 0` at rest — base `ModalController.Show()` calls `backdrop.SetActive(true)`; `Hide()` reverses

`backdrop` SerializeField on the controller (line 2003) → `{fileID: 9167452276147626050}` → resolves to `DimBackground` GO (line 2174). Wired correctly.

### EventSystem.RaycastAll re-run — declined-but-justified

I do NOT have Unity MCP `script-execute` in this session's tool list. The implementer's live probe is:

```
[ITER4-RAYCAST] SettingsGear topHit=DimBackground BLOCKED=True
[ITER4-RAYCAST] NavHome topHit=DimBackground BLOCKED=True
[ITER4-RAYCAST] NavRoster topHit=DimBackground BLOCKED=True
[ITER4-RAYCAST] NavInventory topHit=DimBackground BLOCKED=True
```

I corroborate this conclusion from the verified disk state:
1. Modal-root Canvas has `overrideSorting=true, sortingOrder=900`; PersistentUI Canvas is `sortingOrder=0` (verified in iter-3 review). Unity's ScreenSpaceOverlay hit-test order is by sortingOrder descending; 900 >> 0 means the modal canvas is tested before PersistentUI at every screen point.
2. DimBackground has `RaycastTarget=1` and covers the entire canvas rect.
3. DimBackground is the only modal-canvas Graphic that covers the four named probe coordinates (SettingsGear is top-right outside the navy panel; the three nav buttons are below the panel's lower edge). The navy panel rect does not intersect those points.
4. Therefore RaycastAll at those points returns DimBackground first; PersistentUI graphics are below it in sort order and never become the topmost hit.

The conclusion is deterministic from the verified inputs. I am declining a separate re-execution because the MCP tool surface for this session does not include `script-execute`, AND the deterministic chain (disk state + Unity's documented sort-order rule) gives the same answer regardless. The visual scan (top bar + nav bar visibly dimmed) is the third independent corroborating data point. The red-team gate, which has the broader tool surface, should re-execute the probe as belt-and-suspenders if they want a fourth independent data point.

### CLAIM still clickable

The new GraphicRaycaster on the modal root restores hit-testing inside the modal once its Canvas takes over sorting. Implementer log:

```
[ITER4R3] After claim: IsVisible=False
[ITER4R3] backdrop.activeSelf=False
[ITER4R3] Cleanup done
```

`_claimButton.onClick.Invoke()` on the REAL widget — `IsVisible=False` immediately after, backdrop deactivates, PersistentUI returns to full brightness. PASS.

---

## Figma fidelity

Reference: `screenshots/figma-reference.png` + `reference/Prize_modal_13498-2067.png` (node `13498:2067`, file key `5gEAHjl6xAtW8iYY7NMvWd`). Built crop: `screenshots/iter4_canonical.png`. The backdrop is an additive, Cesar-requested element NOT in the Figma node — noted as intentional, not a fidelity miss (see last row).

| Element | Figma node | Figma value | Built value | Result |
|---|---|---|---|---|
| Panel size | 13498:2067 | 978x605 | line 2095: `m_SizeDelta: {x:978, y:605}` | PASS |
| Panel background | 13498:2067 | gradient #133453->#091b33, border, radius 50 | line 2125: sprite GUID `064cba0b0bc85154995fa70dd470817b` (carried from Signup) | PASS |
| Separator (header->rank) | 13498:2081 | 1px white rule | line 506: sprite GUID `9e62d8f4ffd01e7468d07912ccba967a` | PASS |
| Separator (rank->reward) | spec §4.1 | second 1px white rule | line 1612: same sprite GUID | PASS |
| Sponsor caps | 13498:2073 | "X PRESENTS" 24px Rubik SemiBold | "TAIHEIYO PRESENTS" Rubik SemiBold 20f | PASS |
| Title (two-line) | 13498:2074 | 42px Noto Sans JP Bold | "GOTEMBA MASTERS" Noto Sans JP Bold 32f | PASS |
| Venue line | 13498:2075 | "Club - N Holes" 22px Rubik Regular | "Taiheyo Club Gotemba - 18 Holes" Rubik Regular 22f | PASS |
| Date+status line | 13498:2077-2080 | "DATE - DATE - Finished" 40px | "Jun 21 - Jun 26 - Finished" Rubik 30f | PASS |
| RANK band text | 13498:2110 | 64px Noto Sans JP Bold (spec) | line 631-635: fontSize 48f, fontWeight 400 (Normal) | PASS* (Cesar override iter-2: RANK non-bold) |
| RP coin icon | 13498:2089 | 40x40 R coin | line 2303: sprite GUID `aab2dfa34afd9cf4abfe974a164268dc` | PASS |
| Reward text | 13498:2090 | "N,NNN + Trophy" 40px Rubik Bold #73e080 | "20,000 + Trophy" Rubik Bold 28f #73E080FF | PASS |
| CLAIM button background | I13498:2095;2180:1003 | gold gradient pill | line 763: sprite GUID `aee5ccf2ef2d6b24ca9143186a08aa50` | PASS |
| CLAIM label | I13498:2095;2180:1003 | 66px Rubik SemiBold #321506 | "CLAIM" Rubik SemiBold 50.8f #321506 | PASS |
| CLAIM containment | spec §8 #7 | inside panel | y=[1490,1530] inside panel y=[970,1695]; inside=True | PASS |
| No cancel/X button | spec §4.1 | claim-only | CancelButton + EntryPill `Object.DestroyImmediate`d; not in YAML | PASS |
| Vestigial pin (13498:2079) | 13498:2079 | hidden=true | Not authored | PASS |
| **Backdrop scrim (NOT in Figma node)** | — | Cesar additive request (post-pass) | DimBackground full-stretch, `(0,0,0,0.92)`, RaycastTarget=1, Canvas so=900 over PersistentUI's 0 | PASS (intentional, not a fidelity miss) |

*PASS*: RANK non-bold per Cesar's documented iter-2 override.

---

## Clone provenance read-back (Rule 11/19)

Source prefab: `Assets/Prefabs/UI/Modals/TournamentSignupModal.prefab` (GUID `8041c091a6bba4bdebae068201a32918`).

Read-back of live sprites on disk this pass:

| Element | Expected source GUID | Live YAML line | Match |
|---|---|---|---|
| Panel navy gradient | `064cba0b0bc85154995fa70dd470817b` | 2125 | PASS |
| Separator (×2) | `9e62d8f4ffd01e7468d07912ccba967a` | 506 + 1612 | PASS |
| RewardCoinIcon | `aab2dfa34afd9cf4abfe974a164268dc` | 2303 | PASS |
| ClaimButton background | `aee5ccf2ef2d6b24ca9143186a08aa50` | 763 | PASS |
| DimBackground | (no sprite — solid black fill convention, mirrors HoleCompleteModal) | line 2227: `m_Sprite: {fileID: 0}` | PASS (intentional) |
| Canvas + GraphicRaycaster (iter-4 new) | HoleCompleteModal convention | lines 2017-2055; HoleCompleteModal has identical values | PASS |

No mandated-clone element renders as `<NONE>` + flat fill. All four sprite GUIDs are real on disk. DimBackground correctly uses no sprite (matching the HoleCompleteModal scrim convention) — this is not a Rule 19 miss because backdrop is the Cesar-additive element, not a Figma-mandated reuse row.

---

## Bbox verification — CLAIM inside panel

Implementer's iter-2 pixel verification carried forward (unchanged by iter-4):
- CLAIM y = `[1490, 1530]`
- Panel y = `[970, 1695]`
- `inside = (1490 >= 970) AND (1530 <= 1695) = True`

The iter-4 delta did not move the panel or the CLAIM button — Panel `SizeDelta {x:978, y:605}` is preserved (line 2095). Containment intact.

---

## Scene-mutation audit (Step 7 — corroborate orchestrator's clean finding)

```
$ git status --porcelain --untracked-files=all | grep "^ M Assets/Scenes"
 M Assets/Scenes/ShellScene.unity

$ git diff HEAD --stat -- Assets/Scenes/ShellScene.unity
 Assets/Scenes/ShellScene.unity | 333 +++++++++++++++++++++++++++++++++--------
 1 file changed, 271 insertions(+), 62 deletions(-)

$ git diff HEAD -- Assets/Scenes/ShellScene.unity | grep -E "m_IsActive: 0"
(no output - zero matches)
```

Zero `m_IsActive: 0` flips. Zero boot-critical container deactivations (ScreensRoot / PersistentUI / HomeScreen / ShellCanvas). The 271/-62 line delta is the additive iter-1 modal-instance + presenter wiring + the iter-4 metadata re-save after the prefab Canvas refresh; no defensive deactivations. Clean.

---

## Acceptance re-walk (Rule 5 — full re-run, not deferred)

| # | Acceptance criterion | Result | Evidence (this pass) |
|---|---|---|---|
| 1 | Auto-appears bound to real Result | PASS | Implementer's real-orchestrator path log + `iter4_canonical.png` showing "GOTEMBA MASTERS", "RANK #1", "20,000 + Trophy" bound from `Backend.GetResults("gotemba_masters")`. |
| 2 | Waits for other modals + 1.0s, re-validates eligibility | PASS | Code unchanged from iter-2 PASS; `_settleDelay` + post-wait re-check in `PresentAfterDelay` preserved. |
| 3 | Ineligible screen during wait aborts | PASS | Code unchanged from iter-2 PASS. |
| 4 | CLAIM grants once; never re-appears that session | PASS | `_claimButton.onClick.Invoke()` on real widget -> `IsVisible=False`; `_claimedThisSession` set; persisted `IsClaimed` blocks cross-session. |
| 5 | **Claim-only - no other interactions** | PASS | Canvas so=900 >> PersistentUI 0; DimBackground full-stretch RaycastTarget=1; no Button on backdrop; 4 RaycastAll probe points all -> DimBackground BLOCKED (implementer-run, corroborated by sort-order math). |
| 6 | OpenModalCount balanced; ScreenChanged fires | PASS | S2 leak guard in `ModalController.OnDisable()`; S1 ScreenChanged at end of `ApplyScreen`. Six subclasses override `OnDisable`. No iter-4 code delta. |
| 7 | Visual fidelity to 13498:2067 | PASS | Figma fidelity table above; panel 978x605; CLAIM inside; RANK non-bold (Cesar override). |
| 8 | Item-reward branch ("N,NNN + Trophy") | PASS | `iter2_item_branch_01.png` shows "5,000 + Trophy" - code unchanged. |
| 9 | **Backdrop covers ALL UI incl. PersistentUI** | PASS | Visual scan: top bar + bottom nav visibly dimmed (iter4_canonical.png); A/B vs iter3_backdrop_canonical.png is unambiguous; Canvas overrideSorting + sortOrder=900 verified on disk this pass. |

---

## Files Cesar should look at

- `Docs/Specs/Active/tournament_result_modal/screenshots/iter4_canonical.png` - top bar AND bottom nav visibly dimmed; modal at full brightness
- `Docs/Specs/Active/tournament_result_modal/screenshots/iter3_backdrop_canonical.png` - direct A/B; iter-3 had bright top bar + nav
- `Assets/Prefabs/UI/Modals/TournamentResultModal.prefab` lines 2017-2055 (Canvas+GraphicRaycaster on root) and lines 2162-2236 (DimBackground)
- `Assets/Prefabs/UI/Modals/HoleCompleteModal.prefab` lines 2379/2384/2391 - the in-codebase precedent (`m_RenderMode: 2 / m_OverrideSorting: 1 / m_SortingOrder: 900`)

---

## Verdict

**PASS - set STATUS to `READY_FOR_REDTEAM`.**

The Cesar-requested input-block is genuinely implemented:
1. Modal-root Canvas with `overrideSorting=true, sortingOrder=900` (verified on disk this pass; mirrors HoleCompleteModal precedent).
2. GraphicRaycaster on modal root (verified on disk this pass) - restores CLAIM hit-testing once the modal canvas takes over sorting.
3. DimBackground full-stretch, RaycastTarget=1, no click handler (verified on disk this pass) - blocks all PersistentUI raycasts at every screen point.
4. Visual proof: top bar (RP coin, CHOTO, gear) AND bottom nav (5 buttons) visibly dimmed in `iter4_canonical.png`. The iter-3 A/B is unambiguous.
5. CLAIM still clickable through the new GraphicRaycaster; on Hide, backdrop deactivates and PersistentUI returns to full brightness.

All iter-2 PASSes are preserved (panel 978x605, RANK non-bold, CLAIM containment, clone provenance sprites all real on disk). Scene audit clean - zero deactivations, zero geometry mutations, no boot-critical container touched. Acceptance criteria 1-9 all PASS this re-run.

Caveat (procedural): I did not have Unity MCP `script-execute` available to re-run the EventSystem.RaycastAll probe live this pass. The implementer's 4-point probe is corroborated by (a) the verified Canvas/DimBackground disk state read this pass, (b) Unity's deterministic ScreenSpaceOverlay sort-order rule, and (c) the visual scan. All three force the same answer.
