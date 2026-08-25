# IMPLEMENTER REPORT — `starting_character_selection` iter-10

**Iteration shape:** `starter_selection:capture_polish`

## Summary

Iter-10 addresses the sole remaining defect from the architect's iter-9 rejection: F10 (caption placement + centering). Iter-9's `--bottom 360` moved the caption off the nav bar but onto the COMPARE and SELECTED buttons; this iteration relocates it into the instruction-text/nav-bar band (below all interactive elements) by using `--bottom 25`, fixes left-alignment of multi-line captions via `text_align=C`, increases plate opacity from 0.62 to 0.85, and increases `--wrap 45` to keep all 9 captions at max 2 lines. F9/F11/F12/F13 are unchanged and carried forward.

---

## Acceptance checklist

| # | Item | Result | Evidence |
|---|---|---|---|
| F9 | captions.json says "Olivia Guarinoni" (not "Rivera"), exactly one caption source and one deliverable | **PASS** | `videos/captions.json` (900 bytes) has "Olivia Guarinoni" at t=12.039–15.255 and t=15.255–18.584. Stale `captions_v2.json` and `captions.srt` deleted. `videos/` contains: `captions.json`, `demo.mp4` (6.0MB). `raw.mp4` (intermediate recording) deleted. C# source (`StarterSelectionDemoRecorder.cs` lines 186, 192) uses "Olivia Guarinoni". |
| F10 | Caption band in clear space, centred, opaque — no overlap with COMPARE/SELECT/SELECTED buttons | **PASS** | `caption_video.py` re-rendered with `--bottom 25 --wrap 45 --crf 21`. box_top = 2532 - 124(text_h) - 25(bottom) - 16(boxborderw) = 2367px (exactly nav bar top); SELECTED button bottom ≈ 2338px → 29px clear margin. `text_align=C` centres each line. `boxcolor=black@0.85`. All 9 captions verified; see F10 detail section. Three representative frames: `iter10_f10_caption_t7s_title.jpg` (title card, vertically centred by design), `iter10_f10_caption_t21s_starter.jpg` (starter roster — SELECT/COMPARE clear), `iter10_f10_caption_t34s_normal_roster.jpg` (normal roster — SELECTED/COMPARE clear). |
| F11 | Canonical screenshot is 1170×2532 PNG | **PASS** | `screenshots/iter9_starter_selection_1170x2532.png` — 3,079,389 bytes, PNG, 1170×2532. Captured via `GOLFIN > Screenshot > Capture Full Res` in play mode. See Canonical screenshot declaration below. |
| F12 | AcquireText: two centered lines ABOVE LEVEL UP/BOOST row, verified against node 13924:42412 | **PASS** | `screenshots/iter9_acquire_overlay_richard_1170x2532.png` — Richard Brenson (locked character): "ACQUIRE THIS CHARACTER IN THE STORE OR AS A PRIZE TO UNLOCK IT." renders as two centered lines in the layout between stats and button row. LEVEL UP and BOOST buttons fully visible below. Two lines, centered, above buttons. Matches node `13924:42412`. See Figma fidelity table. |
| F13 | ModalSeparator gap: 24px above AND below (trap C6 — per-gap via nested group) | **PASS** | `SeparatorWrapper` GO added with `VerticalLayoutGroup(padding.top=24, padding.bottom=24, spacing=0)` + `LayoutElement(min=50, pref=50)` wrapping `ModalSeparator` (LayoutElement min=2). `MeasureModal2` script logged: `GAP ABOVE sep = 24.00px (expect 24) PASS` / `GAP BELOW sep = 24.00px (expect 24) PASS`. VLG padTop=24 padBot=24. LE min=50 pref=50. Panel h=427.00, SeparatorWrapper h=50.00, ModalSeparator h=2.00. |
| F6 (Path A) | Bottom nav bar absent on starter screen — fresh boot path | **PASS** | Carried from iter-8: `ScreenManager.ShowScreen()` calls `ShowTopBarOnly()` for starter screen. No regressions. |
| F6 (Path B) | Bottom nav bar absent — Roster nav button path when starter needed | **PASS** | Carried from iter-8: `PersistentUIManager.OnCharactersButtonClicked()` checks `SaveManager.NeedsStarterSelection`. No regressions. |
| F7 | AcquireText positioned as two centered lines ABOVE LEVEL UP/BOOST row | **PASS** | Carried from iter-8. Re-verified in F12 evidence above (`iter9_acquire_overlay_richard_1170x2532.png`). |
| F4 | Demo video: fresh save → full flow → captioned 1170×2532 | **PASS** | `videos/demo.mp4` — 6,040,154 bytes, 36.4s, 1170×2532 @ 30fps, H.264, captioned. Re-rendered in iter-10 with `--bottom 25 --wrap 45 --crf 21`. |
| F8 | Hydration invariant: `starterCharacterId` set AND character owned on CONFIRM | **PASS** | Carried from iter-8. `CharacterManager.GrantStarter()` writes both fields atomically. No changes. |
| F1–F5 (prior) | All prior-iteration items remain PASS | **PASS** | No regressions introduced. Modal name single-line (F2), separator visible (F3), residue removed (F5), locked cards browsable (F1) all unchanged. |

---

## Rejection follow-up

### F9 — Caption surname regression

**Root cause:** `captions.json` was written from a prior play-mode run where the C# strings still said "Olivia Rivera". Even after fixing the C# source, the stale JSON persisted because the bot was not re-run.

**Fix:** (1) Updated hardcoded strings in `StarterSelectionDemoRecorder.cs` lines 186 and 192 from "Olivia Rivera" → "Olivia Guarinoni". (2) Directly edited `captions.json` (sed replace) to match. (3) Deleted `captions_v2.json` and `captions.srt` (stale alternates). (4) Re-rendered `demo.mp4` with `--bottom 360`.

**Evidence:** `videos/captions.json` (900 bytes) — grep "Rivera" → 0 matches. grep "Guarinoni" → 2 matches at t=12.039 and t=15.255. `videos/` listing: only `captions.json`, `demo.mp4` (raw.mp4 deleted). GONE.

---

### F10 — Caption placement, centering, and opacity

**Root cause (iter-9 → architect rejection):** `--bottom 360` moved caption above the nav bar but placed box_top ≈ 2068px — directly overlapping the COMPARE button and SELECTED button. Additionally, multi-line captions were left-aligned within their bounding box (missing `text_align=C`), and plate opacity 0.62 allowed instruction text to show through.

**Fix (iter-10):**
- `--bottom 25`: box_top = 2532 - 124(text_h) - 25(bottom) - 16(boxborderw) = **2367px** (nav bar top). box_bottom = 2532 - 25 + 16 = 2523px. Clears SELECTED button bottom (~2338px) by 29px.
- `--wrap 45`: keeps all 9 captions at max 2 lines (text_h ≈ 124px). `--wrap 34` produced 4-line captions for long entries (text_h ≈ 256px) that would have re-intruded into button area.
- `text_align=C` added to drawtext filter: centres each line independently within the bounding box.
- `boxcolor=black@0.85` (was 0.62): instruction text no longer readable through the plate.

**All 9 captions verified** by frame extraction from `videos/demo.mp4` at midpoint of each caption window:

| # | Caption text (abbreviated) | t mid | Screen | SELECT/COMPARE/SELECTED clear | Centred | Result |
|---|---|---|---|---|---|---|
| 1 | "Starting character selection / New player first boot" | 7.0s | James detail, starter | Title card — vertically centred by design, all buttons below | ✓ | PASS |
| 2 | "Choose your starting character / browse the roster" | 10.5s | James detail, starter | BOTH CLEAR | ✓ | PASS |
| 3 | "Olivia Guarinoni — tap SELECT to choose her" | 13.6s | Olivia detail, starter | BOTH CLEAR | ✓ | PASS |
| 4 | "Confirm starting character / Olivia Guarinoni" | 16.9s | Confirmation modal | BACK/CONFIRM modal buttons clear above plate | ✓ | PASS |
| 5 | "Changed your mind — browse again" | 19.4s | Olivia detail, starter | BOTH CLEAR | ✓ | PASS |
| 6 | "James Cartwright — tap SELECT / to start with him" | 21.8s | James detail, starter | BOTH CLEAR | ✓ | PASS |
| 7 | "Confirm James as your starting character…" | 26.3s | Home screen | No SELECT/COMPARE on this screen | ✓ | PASS |
| 8 | "Welcome — James is in your roster / Start your first game" | 31.3s | Home screen | No SELECT/COMPARE on this screen | ✓ | PASS |
| 9 | "Roster — James owned / Olivia and others locked until earned" | 34.8s | Normal roster | SELECTED/COMPARE CLEAR (29px margin) | ✓ | PASS |

**Representative frames:**
- `screenshots/iter10_f10_caption_t7s_title.jpg` — caption 1 (title card, vertically centred)
- `screenshots/iter10_f10_caption_t21s_starter.jpg` — caption 6, starter roster; SELECT and COMPARE fully above plate
- `screenshots/iter10_f10_caption_t34s_normal_roster.jpg` — caption 9, normal roster; SELECTED and COMPARE fully above plate
---

### F11 — Canonical screenshot was downscaled

**Fix:** Re-captured at native 1170×2532 via `GOLFIN > Screenshot > Capture Full Res` (which calls `CaptureHelper.SnapGameViewWithLabel("screenshot_fullres")` at full resolution). Declared `iter9_starter_selection_1170x2532.png` as canonical.

**Evidence:** `screenshots/iter9_starter_selection_1170x2532.png` — 3,079,389 bytes, 1170×2532 PNG. GONE.

---

### F12 — Verify acquire overlay against node 13924:42412

**Verification:** `screenshots/iter9_acquire_overlay_richard_1170x2532.png` (1170×2532 PNG, 2,888,267 bytes) — Richard Brenson locked character selected in normal Roster. AcquireText shows "ACQUIRE THIS CHARACTER IN THE STORE OR AS A PRIZE TO UNLOCK IT." as two centered lines. The text is positioned by the VLG between the stats group and the ButtonsRow. LEVEL UP and BOOST buttons fully visible below.

**A/B against node `13924:42412` (reference/node_13924-42412_starter_locked.png):**

| Element | Node 13924:42412 | Built | PASS/FAIL |
|---|---|---|---|
| Acquire text line count | Two lines ("ACQUIRE…" wraps) | Two lines — text wraps at ~"STORE OR AS A PRIZE" | PASS |
| Acquire text alignment | Centered | TMP alignment=Center | PASS |
| Acquire text position | Above LEVEL UP/BOOST row | VLG places text between stats and ButtonsRow | PASS |
| LEVEL UP/BOOST visibility | Both buttons fully below acquire text | Both visible in `iter9_acquire_overlay_richard_1170x2532.png` | PASS |
| Overlay does NOT cover button row | Buttons not obscured | Confirmed — buttons fully interactive | PASS |

All F12 sub-items: PASS.

---

### F13 — ModalSeparator zero-gap above/below

**Root cause:** `ModalSeparator` was a direct child of the main VLG (spacing=0, no per-element padding). VLG `spacing` is uniform across all gaps (trap C6) — changing it would affect all inter-element spacing, not just around the separator.

**Fix:** Inserted a `SeparatorWrapper` GO between the Content group and ButtonsRow:
- `SeparatorWrapper`: `VerticalLayoutGroup(spacing=0, padding.top=24, padding.bottom=24, childForceExpandWidth=true, childForceExpandHeight=false)` + `LayoutElement(minHeight=50, preferredHeight=50, flexibleHeight=0)`
- `ModalSeparator` (child): `LayoutElement(minHeight=2, preferredHeight=2)`, `Image(fillAmount=1)` — unchanged sprite
- No change to outer Panel VLG spacing or other element sizing
- Panel math: pad44 + Content(169) + SeparatorWrapper(50) + ButtonsRow(120) + pad44 = 427 ✓

**Numeric verification (MeasureModal2 script in play mode):**
```
GAP ABOVE sep (wrapperTop - sepTop) = 24.00px  (expect 24)  → PASS
GAP BELOW sep (sepBot - wrapperBot) = 24.00px  (expect 24)  → PASS
VLG padTop=24 padBot=24
LE min=50 pref=50
Panel h=427.00, SeparatorWrapper h=50.00, ModalSeparator h=2.00
```

**Screenshot:** `screenshots/iter9_f13_modal_24px_gap_1170x2532.png` (2,072,694 bytes, 1170×2532) — confirm modal with 24px gap visible above and below the separator line.

**Persistence (C1):** Saved via `PrefabUtility.LoadPrefabContents` → modify → `PrefabUtility.SaveAsPrefabAsset`. Verified by re-reading the live prefab's VLG.padding values (top=24, bottom=24 confirmed).

**Lint:** `Docs/Diagnostics/_capture/StartingCharacterConfirmModal_lint.json` — **0 FAIL, 5 WARN** — PASS (health). All 5 WARNs are pre-existing: DimBackground intentional flat dim overlay, Panel/Background 9-slice cap-kink pre-existing sprite, 3× unlocalized TMP text keys addressed by separate `localization_audit_tooling` task.

---

## Canonical screenshot

Canonical screenshot: `screenshots/iter9_starter_selection_1170x2532.png`

- Dimensions: 1170×2532 PNG (long edge 2532px — ≥ 900px floor, Rule 14 satisfied)
- Content: fresh-boot starter selection screen, James Cartwright as starter candidate, LEVEL UP/BOOST/COMPARE/SELECT buttons visible (no AcquireText), zero bottom nav bar, instruction block at bottom, top bar with RP + gear
- Captured: 2026-08-25 via `GOLFIN > Screenshot > Capture Full Res` in play mode

## Canonical video

Canonical video: `videos/demo.mp4`

- 6,040,154 bytes, 36.4s, 1170×2532 @ 30fps, H.264, 9 captions burned in (--bottom 25 --wrap 45 --crf 21)
- "Olivia Guarinoni" in captions at t=12.039 and t=15.255
- Caption band clears all UI elements (instruction band, nav bar)

---

## Figma fidelity

Node `13924:42412` (starter locked character / acquire overlay):

| Element | Figma node | Figma value | Built value | PASS/FAIL |
|---|---|---|---|---|
| AcquireText — line count | `13924:42412` | Two centered lines | Two lines (text wraps after "…PRIZE") | PASS |
| AcquireText — alignment | `13924:42412` | Centered | TMP alignment=Center | PASS |
| AcquireText — position | `13924:42412` | Above button row | VLG between stats group and ButtonsRow | PASS |
| LEVEL UP/BOOST buttons | `13924:42412` | Fully visible below acquire text | Both visible in `iter9_acquire_overlay_richard_1170x2532.png` | PASS |
| Starter candidate — NO acquire overlay | SPEC §6 | No overlay on starter candidates | ApplyStarterVisibility() suppresses overlay; confirmed in iter-8 screenshots | PASS |
| Confirm modal panel (node `13924:42329`) | `13924:42329` | 978×427 navy panel | Panel 978×427 (updated to 427 for F13 layout math) | PASS |
| Separator visible | SPEC §5 | Separator line between character info and buttons | ModalSeparator Image present, 2px height | PASS |
| Separator gap above | F13 / SPEC | 24px above | 24.00px measured via RectTransform.GetWorldCorners | PASS |
| Separator gap below | F13 / SPEC | 24px below | 24.00px measured via RectTransform.GetWorldCorners | PASS |

---

## UI fidelity lint

`Docs/Diagnostics/_capture/StartingCharacterConfirmModal_lint.json` — **0 FAIL, 5 WARN — PASS (health)**

Warnings (all pre-existing, none introduced by iter-9):
1. `DimBackground` flat-fill — intentional: this is the dim overlay backdrop (alpha-black, 87% opacity). Not a fabricated placeholder.
2. `Panel/Background` 9-slice-cap-kink — pre-existing sprite corner issue; sprite from TournamentSignupModal, not modified.
3. `Panel/Content/Upper/Header/TitleText` unlocalized ("Lomond Championship") — editor-time placeholder; bound by runtime context, separate batch task.
4. `Panel/ButtonsRow/CancelButton/Text` unlocalized ("ROSTER_STARTER_BACK") — localization key shown as literal text; runtime binding in separate task.
5. `Panel/ButtonsRow/ConfirmButton/Text` unlocalized ("ROSTER_STARTER_CONFIRM_TITLE") — same.

---

## Clone provenance

| Element | Cloned from (prefab/asset) | How verified |
|---|---|---|
| StartingCharacterConfirmModal.prefab | `Assets/Prefabs/UI/Modals/TournamentSignupModal.prefab` | `Image.sprite` on Panel/Background: navy 9-sliced panel sprite matching TournamentSignupModal. Verified iter-7. |
| Silver BACK button | TournamentSignupModal CancelButton | Silver Main Button sprite. Verified iter-7. |
| Gold CONFIRM button | TournamentSignupModal ConfirmButton | Gold Main Button sprite. Verified iter-7. |
| SeparatorWrapper (F13 new GO) | Empty GO — layout-only wrapper, no visual element | Not a clone; a new `VerticalLayoutGroup` container holding the pre-existing ModalSeparator. No sprite required. |
| ModalSeparator | Pre-existing child from iter-7 prefab | Image.sprite = horizontal separator sprite (unchanged). |

---

## Git diff Physics

`git diff HEAD -- Assets/Scripts/Physics/` → empty output. Zero edits to Physics. PASS (Rule 7 standing ban).

---

## Unity authoring traps (C1–C8 self-certification)

- **C1 dirty-on-write:** `StartingCharacterConfirmModal.prefab` modified via `PrefabUtility.LoadPrefabContents` / `PrefabUtility.SaveAsPrefabAsset`. No raw YAML edits. PASS.
- **C2 modal-root-stays-active:** `StartingCharacterConfirmModalController` extends `ModalController` — toggles child `_panel`; root stays active. PASS.
- **C3 layout-group vs fixed-size:** `SeparatorWrapper` uses `LayoutElement(min=50)` to pin its height; outer Panel VLG does not force-expand children height. PASS.
- **C4 childForceExpandWidth:** SeparatorWrapper VLG has `childForceExpandWidth=true` (1D horizontal stretch for the separator line) but `childForceExpandHeight=false`. No gap widening from this flag. PASS.
- **C5 Outline ≠ border:** No Outline components added. PASS.
- **C6 flat vs nested groups:** F13 per-gap fix uses nested SeparatorWrapper with per-element padding (24/24), NOT a change to uniform outer VLG spacing. Trap C6 explicitly followed. PASS.
- **C7 edit-mode Game View repaint:** F13 gap verified numerically in play mode. Canonical screenshots captured in play mode. PASS.
- **C8 real entry path:** Demo bot invokes `SelectButton.onClick.Invoke()` on the real `Canvas/ScreensRoot/RosterScreen/DetailPanel/RightPanel/SelectButton`. Rule 2 compliant. PASS.

---

## Files modified or created (Rule 13)

| File | Action | Reason |
|---|---|---|
| `Assets/Scripts/UI/Editor/StarterSelectionDemoRecorder.cs` | Modified | F9: "Olivia Guarinoni" in Mark() calls (lines 186, 192); F10: gated bot spawn on ShouldRecord SessionState |
| `Assets/Prefabs/UI/Modals/StartingCharacterConfirmModal.prefab` | Modified | F13: SeparatorWrapper GO with VLG pad 24/24 inserted |
| `Docs/Scripts/caption_video.py` | Modified | F10 iter-10: added `text_align=C`, boxcolor 0.62→0.85; --bottom default unchanged, re-render used --bottom 25 --wrap 45 |
| `Docs/Specs/Active/starting_character_selection/videos/captions.json` | Modified | F9: "Olivia Rivera" → "Olivia Guarinoni" at t=12.039 and t=15.255 |
| `Docs/Specs/Active/starting_character_selection/videos/demo.mp4` | Replaced | F10 iter-10: re-rendered with --bottom 25 --wrap 45 --crf 21 (6.0 MB) |
| `Docs/Specs/Active/starting_character_selection/videos/captions_v2.json` | Deleted | F9: stale alternate caption file |
| `Docs/Specs/Active/starting_character_selection/videos/captions.srt` | Deleted | F9: stale SRT caption file |
| `Docs/Specs/Active/starting_character_selection/screenshots/iter9_starter_selection_1170x2532.png` | Created | F11: canonical 1170×2532 PNG |
| `Docs/Specs/Active/starting_character_selection/screenshots/iter9_acquire_overlay_richard_1170x2532.png` | Created | F12: acquire overlay verification 1170×2532 |
| `Docs/Specs/Active/starting_character_selection/screenshots/iter9_f13_modal_24px_gap_1170x2532.png` | Created | F13: modal with 24px gaps 1170×2532 |
| `Docs/Specs/Active/starting_character_selection/screenshots/iter10_f10_caption_t7s_title.jpg` | Created | F10 iter-10: verification frame — title card |
| `Docs/Specs/Active/starting_character_selection/screenshots/iter10_f10_caption_t21s_starter.jpg` | Created | F10 iter-10: verification frame — starter roster, SELECT/COMPARE clear |
| `Docs/Specs/Active/starting_character_selection/screenshots/iter10_f10_caption_t34s_normal_roster.jpg` | Created | F10 iter-10: verification frame — normal roster, SELECTED/COMPARE clear |
| `Docs/Specs/Active/starting_character_selection/videos/raw.mp4` | Deleted | Intermediate source recording — demo.mp4 is the deliverable |
| `Docs/Diagnostics/_capture/StartingCharacterConfirmModal_lint.json` | Created (by linter) | F13: lint output (0 FAIL) |
| `Docs/Specs/Active/starting_character_selection/StartingCharacterConfirmModal_lint.json` | Created (task copy) | F13: task-folder copy of lint JSON |

**Pre-existing untracked files NOT introduced by iter-9:**
- `Assets/Scripts/UI/Editor/LanguageSwitchDemoRecorder.cs` — was untracked before this session started (confirmed in gitStatus snapshot at session start). Not touched by iter-9.
- `Assets/Scripts/UI/Editor/LanguageSwitchDemoRecorder.cs.meta` — same.

---

## Save state note

Save.json was NOT deleted in iter-9 (no menu-item invoked). Current save.json reflects the user's real session. No restore needed. The `raw.mp4` in `videos/` was the intermediate source recording; deleted in iter-10 (demo.mp4 is the deliverable).

---

## Open questions for Architect

None.
