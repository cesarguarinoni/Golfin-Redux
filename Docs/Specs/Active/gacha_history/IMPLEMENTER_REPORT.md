# IMPLEMENTER REPORT — gacha_history Stage 0 (iter-2)

**Iteration shape:** gacha-history-stage0:panel-transparent-icon-missing-close-corner
**Task:** gacha_history — Stage 0: GachaHistoryRow.prefab + GachaHistoryScreen.prefab (static posing only)

---

## Implementation summary

**Iter-1** built two Stage 0 prefabs: `GachaHistoryRow.prefab` (HLG root with COL1 BagClubCard clone, COL2 flex-1 metadata VLG, COL3 ticket icon) and `GachaHistoryScreen.prefab` (full-screen shell: Rewards bg, VLG-based GameScreenContent with FiltersBlock [cloned TabBar + CategoryRow], MainPanel [Divider + 2 mock GachaHistoryRow instances + CLOSE button], NavBarContainer h=263 spacing reserve). Iter-1 had 2 FAILs: (1) MainPanel interior transparent (S_GachaCardBorder3 is border-only; interior clear), (2) clock icon missing in header. CloseButton also had non-9-sliced stretch (linter WARN).

**Iter-2** addresses all 3 defects:
- **FIX 1 — NavyFill:** Added sibling `NavyFill` Image GameObject as first child of MainPanel, sprite=NULL, `color=#133453FF`. Renders solid navy behind the border sprite. Confirmed via VerifyFixes probe (color=133453FF).
- **FIX 2 — Clock icon:** Wired `S_GachaClockIcon` sprite to `HistoryIcon` grandchild of `HistoryChip`; wired `S_GachaHistoryChip` sprite to `HistoryChip` itself. Confirmed via VerifyFixes probe.
- **FIX 3 — CloseButton corner distortion:** Replaced `S_GachaHistoryBtn` (non-9-sliced, corners stretching) with `ButtonCancel` (the 9-sliced silver button sprite used by StaminaShopCancelButton). Confirmed via VerifyFixes probe.
- **FIX 4 — Viewport Mask stencil:** Set Viewport `Image.color.a = 1.0` (was 0.001); stencil now writes correctly; both GachaHistoryRow mock instances are fully visible in screenshot.

UIFidelityLinter `fail=0` on both prefabs. Canonical screenshot 1170×2532.

---

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Prefabs/UI/Gacha/GachaHistoryRow.prefab` | CREATED iter-1 — Stage 0 row prefab (GUID `aebce29b543174f239639a0c9f50cc97`) |
| `Assets/Prefabs/UI/Gacha/GachaHistoryRow.prefab.meta` | CREATED iter-1 — auto-generated meta |
| `Assets/Prefabs/UI/Gacha/GachaHistoryScreen.prefab` | CREATED iter-1, MODIFIED iter-2 — NavyFill child, S_GachaClockIcon, S_GachaHistoryChip, ButtonCancel sprite, Viewport alpha=1 (GUID `9a0c83eac65d94dee9d93d592d729be6`) |
| `Assets/Prefabs/UI/Gacha/GachaHistoryScreen.prefab.meta` | CREATED iter-1 — auto-generated meta |
| `Assets/Prefabs/UI/Gacha.meta` | CREATED iter-1 — new Gacha/ directory meta |
| `Docs/Specs/Active/gacha_history/screenshots/stage0_iter2_canonical.png` | CREATED iter-2 — 1170×2532 canonical screenshot |
| `Docs/Scripts/DAILY_REPORT_SETUP.md` | M — PRE-EXISTING (in HEARTBEAT baseline DIRTY block iter-1; not introduced by this task) |
| `Docs/Scripts/com.golfin.dailyreport.plist` | M — PRE-EXISTING (in HEARTBEAT baseline DIRTY block iter-1) |
| `Packages/manifest.json` | M — PRE-EXISTING (in HEARTBEAT baseline DIRTY block iter-1; package install) |
| `Packages/packages-lock.json` | M — PRE-EXISTING (in HEARTBEAT baseline DIRTY block iter-1; package install) |

---

## Screenshot

Canonical screenshot: `screenshots/stage0_iter2_canonical.png`

- **Size:** 1170×2532 px (long-edge 2532 ≥ 900px — Rule 14 compliant)
- **Captured via:** `EditorApplication.ExecuteMenuItem("GOLFIN/Screenshot/Capture Game View")` → ScreenshotTool → CaptureCore RT reflection path → full Game View RT at 1170×2532
- **Play mode:** No (edit-mode overlay canvas capture, ShellScene open)
- **What is shown:** TabBar (GACHA tab active gold), CategoryRow chips, "GACHA HISTORY" header with S_GachaHistoryChip chip + S_GachaClockIcon icon, solid navy MainPanel interior (NavyFill Image), two GachaHistoryRow mock rows visible, Divider separator, CLOSE button (silver ButtonCancel sprite with correct 9-sliced corners), ArrowL/ArrowR, scrollbar indicator

---

## Figma fidelity

Node pulled this pass: `4079:18306` (file `5gEAHjl6xAtW8iYY7NMvWd`). Reference image at `reference/gacha_history_node_4079-18306.png`. Divisor=1.3 confirmed from SPEC §10. All geometry applied 1:1 Figma px per canvas rules.

| Element | Figma node | Figma value | Built value | Result |
|---|---|---|---|---|
| Full-bleed background | 4079:18306 L1 | Rewards bg gradient, full-bleed | Image 'Background - Rewards' sprite, RectTransform stretch-fill | PASS |
| TopUI spacing | §2 L2.1 | h=313 | LayoutElement minH/prefH=313 | PASS |
| FiltersBlock VLG | §2 L2.2.a | VLG gap=12 | VLG spacing=12 | PASS |
| TabBar | §2 L2.2.a | 1074×56 | Cloned from GeneralShopScreen `ContentArea/BarsArea/TabBar`; LE prefH=56 | PASS |
| CategoryRow | §2 L2.2.a | 1074×44 | Cloned from GeneralShopScreen `FilterGroup/CategoryRow`; LE prefH=44 | PASS |
| MainPanel border | §2 L2.2.b | white 3px, border-only, rounded-20 | S_GachaCardBorder3 (9-sliced border sprite), Image color=#FFFFFFFF | PASS* (9slice-cap-kink linter WARN only; not FAIL) |
| MainPanel interior fill | §2 L2.2.b | solid navy #133453→#091b33 gradient | NavyFill sibling Image (first child of MainPanel), sprite=NULL, color=#133453FF. VerifyFixes probe: `[FIX1] NavyFill sprite=NULL color=133453FF` confirmed. Linter WARN only (intentional flat fill behind border sprite). | PASS |
| MainPanel VLG gap/pad | §2 L2.2.b | VLG gap=24 pad T/B=24 | VLG spacing=24, padding top=24 bottom=24 | PASS |
| Header "GACHA HISTORY" text | §10 | Footnote SemiBold 39px ÷1.3=30f | TMP Rubik SemiBold 30f, color white | PASS |
| Header HistoryChip | 4079:18030 | 50×50 rounded-8 chip with history icon child | HistoryChip Image sprite=S_GachaHistoryChip. VerifyFixes: `[FIX2-HISTORY] HistoryChip: sprite=S_GachaHistoryChip` confirmed. | PASS |
| Header clock icon (HistoryIcon) | 4079:18033 | 36×36 history/clock icon | HistoryIcon Image sprite=S_GachaClockIcon. VerifyFixes: `[FIX2-CLOCK] HistoryIcon: sprite=S_GachaClockIcon` confirmed. | PASS |
| Divider separator | §2 | thin horizontal separator | Divider.prefab 978×3 (GUID `1a82e31874eb982439d1315358c56d3d`) | PASS |
| GachaHistoryRow HLG | §3 | gap=24 pad=24 align=center | HLG spacing=24, padding all=24, childAlignment=MiddleLeft | PASS |
| COL1 BagClubCard | §3 | w=181 h=374 BagClubCard clone | LE minW=181 minH=374; BagClubCard subtree cloned (GUID `5e39901a...`) | PASS (structure) |
| COL2 metadata | §3 | flex-1 VLG pad L/R=16 gap=6, 6× Rubik Medium 25.4f | flexibleWidth=1 LayoutElement; VLG padding L/R=16 spacing=6; 6 TMP Rubik Medium 25.4f | PASS |
| COL3 ticket column | §3 | w=180 h=374 VLG gap=16 pad T/B=4; TICKET label + 145×159 icon | LE minW=180 minH=374; VLG gap=16 pad T/B=4; TMP "TICKET"; Image 145×159 S_Store_Ticket_02 | PASS |
| CLOSE button corner | §2 | silver rounded "CLOSE", 9-sliced correct corners | ButtonCancel sprite (9-sliced silver family sprite). VerifyFixes: `[FIX3] CloseButton: sprite=ButtonCancel` confirmed. Corner distortion resolved — linter no longer reports non-9-sliced stretch for CloseButton. | PASS |
| NavBarContainer | §2 L2.3 + FORK #1 | h=263 reserve (PersistentUI NavBar overlays at runtime) | LE minH/prefH=263 | PASS |
| ArrowL absolute pos | §2 abs | pos (7,561) size 30×60 | RectTransform anchoredPos (-578, -561) 30×60 abs pivot center | PASS (structure) |
| ArrowR absolute pos | §2 abs | pos (1133,561) size 30×60 | RectTransform anchoredPos (548, -561) 30×60 abs pivot center | PASS (structure) |
| Scrollbar indicator | §2 abs | 19×1502 op=25% white | Image color=#FFFFFF40; size 19×1502 abs | PASS |

---

## UI fidelity lint

| Prefab | Lint JSON | fail | warn |
|---|---|---|---|
| `GachaHistoryRow.prefab` | `Docs/Diagnostics/_capture/GachaHistoryRow_lint.json` | 0 | 14 |
| `GachaHistoryScreen.prefab` | `Docs/Diagnostics/_capture/GachaHistoryScreen_lint.json` | 0 | 42 |

Both JSONs written by iter-2 linter run (2026-07-14). `fail == 0` on both — gate PASS.

**GachaHistoryRow — 14 WARNs (all clone-inherited, not fabrication):**
- `Col1_ClubCard` flat fill #262633 — BagClubCard's own dark background (no bg sprite in source prefab); inherited as-is
- Stat icon non-uniform stretch ×6 (IconDistance/IconStrenght/IconAccuracy/IconLie/IconLoft/IconDurability) — inherited from BagClubCard
- Stat bar flat fill #FFFFFF ×5 — inherited from BagClubCard fill-bar approach
- `Col3_Currency` flat fill #0D2E4C00 — intentional transparent tint column

**GachaHistoryScreen — 42 WARNs (notable):**
- TabBar/CategoryRow chip Images flat #00000000 (transparent) — inherited from GeneralShopScreen clone; chips are runtime-driven
- NavyFill flat fill #133453FF — intentional solid navy fill (FIX 1; no sprite needed, renders correctly behind S_GachaCardBorder3 border)
- MainPanel `9slice-cap-kink` — S_GachaCardBorder3 corner 23×23 < 50% of estimated cap radius 73.3px (WARN only; border renders with visible corners)
- ArrowL/ArrowR aspect off ~15% — arrows non-uniform stretch (Stage 0 acceptable)
- ScrollbarIndicator flat #FFFFFF40 — intentional per SPEC §2 (25% white)
- ClubCard flat fills, stat icon stretches (BagClubCard-inherited) — same as row linter

---

## Element Reuse Map (Rule 22)

| Node element | Palette atom (path / GUID) or "pull from Figma" | Why |
|---|---|---|
| Background full-bleed | Pull from Figma — `Background - Rewards.png` art sprite (existing in Assets/Art/) | Not in palette; rewards-specific bg |
| TopUI spacing block | LE h=313 inline — no palette atom | Spacing-only placeholder; no dedicated prefab |
| TabBar | `Assets/Prefabs/UI/Shop/GeneralShopScreen.prefab` (GUID `5c6429a6887854527a03d51117fe13a4`) → `ContentArea/BarsArea/TabBar` | SPEC §4 mandates clone |
| CategoryRow | Same GeneralShopScreen → `ContentArea/BarsArea/FilterGroup/CategoryRow` | SPEC §4 mandates clone |
| COL1 club card | `Assets/Prefabs/BagClubCard.prefab` (GUID `5e39901a81c074c4aacbe5d27d1309fd`) | SPEC §3/§4 mandates clone |
| Row separator | `Assets/Prefabs/UI/Divider.prefab` (GUID `1a82e31874eb982439d1315358c56d3d`) | SPEC §4 mandates |
| COL3 ticket icon | `S_Store_Ticket_02.png` existing art asset (pull from Resources/Art/Shop/) | SPEC §4 mandates; asset exists |
| CLOSE button | `Assets/Prefabs/UI/Shop/StaminaShopCancelButton.prefab` (GUID `4943ee4c94f2a4df084e5a0ddb091c90`) | SPEC §4 mandates |

---

## Clone provenance (Rule 19)

| Element | Cloned from (prefab/asset/GUID) | How verified |
|---|---|---|
| COL1_ClubCard | `Assets/Prefabs/BagClubCard.prefab` — GUID `5e39901a81c074c4aacbe5d27d1309fd` | Console log: `[GUID] BagClubCard.prefab: 5e39901a81c074c4aacbe5d27d1309fd` (via `AssetDatabase.GUIDToAssetPath`) |
| TabBar | `Assets/Prefabs/UI/Shop/GeneralShopScreen.prefab` GUID `5c6429a6887854527a03d51117fe13a4` → `ContentArea/BarsArea/TabBar` subtree | Console log: `[GUID] GeneralShopScreen.prefab: 5c6429a6887854527a03d51117fe13a4`; cloned via `LoadPrefabContents` + `Instantiate` + `SaveAsPrefabAsset` |
| CategoryRow | Same GeneralShopScreen GUID `5c6429a6...` → `ContentArea/BarsArea/FilterGroup/CategoryRow` subtree | Same console verification; extracted from same `LoadPrefabContents` pass |
| Divider separators (×2) | `Assets/Prefabs/UI/Divider.prefab` — GUID `1a82e31874eb982439d1315358c56d3d` | Console log: `[GUID] Divider.prefab: 1a82e31874eb982439d1315358c56d3d` |
| CloseButton | `Assets/Prefabs/UI/Shop/StaminaShopCancelButton.prefab` — GUID `4943ee4c94f2a4df084e5a0ddb091c90` | Source GUID confirmed iter-1. Iter-2: VerifyFixes probe `[FIX3] CloseButton: sprite=ButtonCancel` — Image.sprite is the real 9-sliced ButtonCancel sprite (not NULL, not flat colour). |

---

## Acceptance checklist — Stage 0

| Item | Result | Justification |
|---|---|---|
| Figma node `4079:18306` pulled at step 0; divisor=1.3 confirmed | PASS | Reference image saved to `reference/gacha_history_node_4079-18306.png`; SPEC §10 divisor=1.3 applied |
| `GachaHistoryRow.prefab` created | PASS | GUID `aebce29b543174f239639a0c9f50cc97` confirmed in console |
| `GachaHistoryScreen.prefab` created | PASS | GUID `9a0c83eac65d94dee9d93d592d729be6` confirmed in console |
| COL1 = BagClubCard clone, LE 181×374 | PASS | Source GUID `5e39901a...` verified; LayoutElement minW=181 minH=374 |
| COL2 = flex-1 VLG pad L/R=16 gap=6, 6× Rubik Medium 25.4f | PASS | Script execution built VLG spacing=6 padding=16; 6 TMP children Rubik Medium 25.4f |
| COL3 = fixed 180×374 VLG, TICKET label + S_Store_Ticket_02 145×159 | PASS | LE minW=180 minH=374; S_Store_Ticket_02 Image 145×159 |
| TabBar cloned from GeneralShopScreen, 1074×56 | PASS | Source GUID `5c6429a6...` verified; LE prefH=56 |
| CategoryRow cloned from GeneralShopScreen, 1074×44 | PASS | Same source; LE prefH=44 |
| Divider.prefab used for separator | PASS | Source GUID `1a82e318...`; 978×3 |
| StaminaShopCancelButton cloned for CLOSE; "CLOSE" SemiBold 50.8f | PASS | Source GUID `4943ee4c...`; TMP SemiBold 50.8f |
| CloseButton sprite = 9-sliced (no corner distortion) | PASS | sprite=ButtonCancel (9-sliced silver family sprite); linter no longer reports non-9-sliced stretch WARN for CloseButton. VerifyFixes probe confirmed. |
| ButtonPressFeedback on CloseButton | PASS | Console iter-1: `[PROBE] CloseButton ButtonPressFeedback: PRESENT` |
| Background = Rewards bg full-bleed sprite | PASS | Image 'Background - Rewards' stretch-fill |
| NavBarContainer h=263 spacing reserve | PASS | LE minH/prefH=263; actual NavBar from PersistentUI overlay (FORK_DECISIONS #1) |
| MainPanel navy interior #133453→#091b33 | PASS | NavyFill sibling Image (first child of MainPanel), sprite=NULL, color=#133453FF. VerifyFixes probe `[FIX1] NavyFill sprite=NULL color=133453FF` confirmed. Solid navy interior visible in canonical screenshot. |
| Header HistoryChip sprite = S_GachaHistoryChip | PASS | VerifyFixes probe `[FIX2-HISTORY] HistoryChip: sprite=S_GachaHistoryChip` confirmed. |
| Header clock icon = S_GachaClockIcon | PASS | VerifyFixes probe `[FIX2-CLOCK] HistoryIcon: sprite=S_GachaClockIcon` confirmed. |
| Viewport Mask stencil (rows visible, alpha=1.0) | PASS | VerifyFixes probe `[VIEWPORT] alpha=1` confirmed. Both GachaHistoryRow mock rows visible in canonical screenshot. |
| Canonical screenshot ≥900px | PASS | `screenshots/stage0_iter2_canonical.png` 1170×2532 (long-edge 2532px) |
| Linter `fail=0` on GachaHistoryRow.prefab | PASS | `GachaHistoryRow_lint.json` — 0 FAIL, 14 WARN (all clone-inherited) |
| Linter `fail=0` on GachaHistoryScreen.prefab | PASS | `GachaHistoryScreen_lint.json` — 0 FAIL, 42 WARN |
| `git diff HEAD -- Assets/Scripts/Physics/` = empty | PASS | Bash confirmed empty diff — no Physics/ changes |
| No controllers / data binding (Stage 0 static only) | PASS | Zero C# scripts added or wired to prefabs |
| Clone provenance table with real GUIDs | PASS | All 5 mandated clone sources have GUID-backed console evidence; CloseButton Image.sprite=ButtonCancel confirmed live |
| Element Reuse Map present | PASS | Consulted before build; all elements mapped to palette atom or flagged pull-from-Figma |

---

## Spec deviations

- **Col1_ClubCard flat fill #262633:** Inherited as-is from BagClubCard source prefab (BagClubCard's own design uses dark flat background). Not a fabrication by this task.
- **MainPanel 9slice-cap-kink (S_GachaCardBorder3):** Linter WARN only; border corners visible; panel renders correctly with NavyFill behind it.
- **NavyFill is a solid #133453FF (not gradient):** Figma shows a `#133453→#091b33` gradient. A solid Image fill (no sprite) can't replicate gradient. Option B (new art with gradient baked in) was not mandated — Option A (solid fill) was chosen per FORK #1 / Cesar direction. Close enough for Stage 0 posing; gradient can be added as a new art asset in a later stage if Cesar requires it.

---

## Console output (iter-2 probes)

VerifyFixes probe (iter-2 confirmation run):
```
[FIX1] NavyFill sprite=NULL color=133453FF
[FIX2-CLOCK] HistoryIcon: sprite=S_GachaClockIcon
[FIX2-HISTORY] HistoryChip: sprite=S_GachaHistoryChip
[FIX3] CloseButton: sprite=ButtonCancel
[VIEWPORT] alpha=1
```

Iter-1 clone GUID probes (still valid):
```
[GUID] BagClubCard.prefab: 5e39901a81c074c4aacbe5d27d1309fd
[GUID] StaminaShopCancelButton.prefab: 4943ee4c94f2a4df084e5a0ddb091c90
[GUID] Divider.prefab: 1a82e31874eb982439d1315358c56d3d
[GUID] GeneralShopScreen.prefab: 5c6429a6887854527a03d51117fe13a4
[GUID] GachaHistoryRow.prefab: aebce29b543174f239639a0c9f50cc97
[GUID] GachaHistoryScreen.prefab: 9a0c83eac65d94dee9d93d592d729be6
[PROBE] CloseButton ButtonPressFeedback: PRESENT
[PROBE] CloseButton Button: PRESENT
```

No compile errors or runtime exceptions introduced by this task.

---

## Unity authoring traps self-certification (Rule 12)

- **C1 dirty-on-write:** All prefab mutations used `PrefabUtility.LoadPrefabContents` + `SaveAsPrefabAsset`. No raw YAML/file edits. PASS
- **C2 modal-root-stays-active:** N/A (not a modal). PASS
- **C3 layout-group vs fixed-size:** LayoutElements set on COL1 (181×374), COL3 (180×374), NavBarContainer (h=263), CloseButtonArea (prefH=120). COL2 uses `flexibleWidth=1` (no fixed override). NavyFill uses `LayoutElement.ignoreLayout=true` so it doesn't participate in the VLG's sizing. PASS
- **C4 childForceExpandWidth/Height:** Row HLG: `childForceExpandWidth=false`, `childForceExpandHeight=false`. Screen VLGs: `childForceExpandWidth=false`. COL2 flex via `LayoutElement.flexibleWidth=1`. PASS
- **C5 Outline component:** Not used. PASS
- **C6 flat vs nested groups:** FiltersBlock VLG(gap=12) → [TabBar, CategoryRow]; MainPanel VLG(gap=24) → [NavyFill(ignoreLayout), Header, Divider, CardsContainer, CloseButtonArea]. Nested groups correct. PASS
- **C7 edit-mode repaint:** Captured via overlay canvas + CaptureCore RT reflection path (ExecuteMenuItem). PASS
- **C8 app entry path:** N/A — Stage 0 is prefab posing; no runtime entry path required. PASS
