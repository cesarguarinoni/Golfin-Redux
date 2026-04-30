# Implementer Report — `8_3_topbar` (Iteration 4)

## Implementation summary

Iteration 4 addresses the single FAIL from architect review: the broken custom PNG `RoundedRect_R8.png` with a fabricated GUID. Steps taken: (1) deleted `Assets/Art/UI/RoundedRect_R8.png` and its `.meta` file; (2) replaced both `m_Sprite` references in `LabScaffold.unity` (lines 4648 and 4814, fileIDs 7000012 and 7000015) from the fabricated GUID to Unity's built-in UISprite `{fileID: 10913, guid: 0000000000000000f000000000000000, type: 0}`; (3) confirmed `m_Type: 1` (Sliced) and `m_ShowMaskGraphic: 1` are unchanged on both containers. Fix 6 (ChipStack widths/positions) was not touched — those YAML values are unchanged from iteration 3.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scenes/Physics/LabScaffold.unity` | Modified — two `m_Sprite` lines (fileID 7000012 PortraitContainer Image and fileID 7000015 HoleMapContainer Image) changed from fabricated GUID `a1b2c3d4e5f6789012345678abcdef01` to built-in UISprite `{fileID: 10913, guid: 0000000000000000f000000000000000, type: 0}`. All other YAML unchanged. |
| `Assets/Art/UI/RoundedRect_R8.png` | Deleted — broken PNG with unreliable alpha channel |
| `Assets/Art/UI/RoundedRect_R8.png.meta` | Deleted — .meta with fabricated GUID `a1b2c3d4e5f6789012345678abcdef01` |

## Screenshot

- **Path:** `Docs/Specs/Active/8_3_topbar/screenshots/2026-04-28_iter4.png`
- **Captured at:** 2026-04-28 ~18:27 local time via `ScreenCapture.CaptureScreenshot()` in play mode, compressed to 800px via compress_screenshots.py
- **Scene loaded:** LabScaffold (Hole loaded per PhysicsLabAutoRestore)
- **Play mode:** Active (IsPlaying=true confirmed via editor-application-get-state before capture)
- **Hole loaded:** Hole scene auto-restored by PhysicsLabAutoRestore on play mode entry

## Acceptance checklist

### Carried forward from iteration 2 (unchanged — YAML re-verified against iteration 4 scene)

| Item | Result | Justification |
|---|---|---|
| Settings is on its OWN row at top (Y=24, Y-bottom=110), NOT on the cards row | PASS | YAML unchanged from iteration 2: anchoredPosition {x:-58, y:-24}, size 86x86. Not touched in iterations 3 or 4. |
| Settings is a single 86×86 white circle with navy gear centered inside it | PASS | YAML unchanged. Not touched in iterations 3 or 4. |
| Settings position: anchored top-right with anchoredPosition (-58, -24) | PASS | YAML: m_AnchoredPosition {x:-58, y:-24}. Unchanged. |
| Player card RectTransform is 478×180 at anchoredPosition (48, -158) with anchor=(0,1) pivot=(0,1) | PASS | YAML unchanged: m_SizeDelta {x:478, y:180}, pos {x:48, y:-158}. |
| Hole card RectTransform is 478×180 at anchoredPosition (-48, -158) with anchor=(1,1) pivot=(1,1) | PASS | YAML unchanged: m_SizeDelta {x:478, y:180}, pos {x:-48, y:-158}. |
| Both cards are 48px from their respective screen edges | PASS | PlayerCard pos.x=48, HoleCard pos.x=-48. Unchanged. |
| Cards row top edge starts at Y=158 (BELOW the settings row, with ~24px gap) | PASS | Cards anchoredPosition.y=-158. Unchanged. |
| Chip stack offset 10px from card top | PASS | Both ChipStacks: anchoredPosition.y=-10. Unchanged. |
| Chips are flat navy #001E39 rectangles, no sprite | PASS | YAML unchanged: m_Color {r:0, g:0.118, b:0.224}, m_Sprite {fileID:0}. |
| Player chip text is Middle Left aligned | PASS | YAML unchanged: all 3 player chip TMPs m_HorizontalAlignment=1. |
| Hole chip text is Middle Right aligned | PASS | YAML unchanged: all 3 hole chip TMPs m_HorizontalAlignment=4. |
| Chip text font is Rubik-VariableFont_wght SDF, size 23, color white | PASS | YAML unchanged. |
| Portrait visible (real sprite — Camila or selected character — NOT a white box) | PASS | Screenshot confirms Camila portrait visible in player card left side. |
| Hole map visible (real sprite — NOT a white box) | PASS | Screenshot confirms Lomond hole map (green course aerial) visible in hole card right side. |
| All SerializeField references wired in the Inspector | PASS | YAML unchanged: PlayerCardWidget and HoleCardWidget slots non-zero as in iteration 2. |
| Unity Console has no errors related to this task | PASS | Log checked via editor log; no compile errors, no missing reference warnings for HUD components. |
| _defaultPortrait wired to Camila.png in Inspector | PASS | YAML unchanged; screenshot shows real portrait rendering. |
| _defaultHoleMap wired to Lomond - Hole 1.png in Inspector | PASS | YAML unchanged; screenshot shows real holemap rendering. |
| Icon - Settings.png asset inspection documented (case (a) or (b)) | PASS | Case (b): documented in iteration 2 report, unchanged. |
| Settings button visually 58px from right screen edge | PASS | YAML: anchoredPosition.x=-58. Screenshot shows gear icon in top-right corner. |
| PortraitContainer hierarchy includes RarityBackground under Portrait sprite | PASS | YAML unchanged: PortraitContainer RT (7000001) children=[{7000003} RarityBackground RT, {2072076076} Portrait RT]. |
| HoleMapContainer hierarchy includes HoleMapBackground under HoleMap sprite | PASS | YAML unchanged: HoleMapContainer RT (7000006) children=[{7000009} HoleMapBackground RT, {459716537} HoleMap RT]. |
| PlayerCardWidget._portrait points to inner Portrait Image | PASS | YAML unchanged: fileID 2072076077. |
| HoleCardWidget._holeMap points to inner HoleMap Image | PASS | YAML unchanged: fileID 459716538. |

### Iteration 3 items — Fix 6 (ChipStack sizing — confirmed PASS, unchanged)

| Item | Result | Justification |
|---|---|---|
| Player ChipStack is 248 wide (not 298), positioned at (180, -10) | PASS | YAML verified unchanged from iter 3: RectTransform &1851587491 m_SizeDelta {x:248, y:160}, m_AnchoredPosition {x:180, y:-10}. |
| Hole ChipStack is 248 wide (not 298), positioned at (50, -10) | PASS | YAML verified unchanged from iter 3: RectTransform &1567968518 m_SizeDelta {x:248, y:160}, m_AnchoredPosition {x:50, y:-10}. |
| Visible center gap between player chip stack right edge and hole chip stack left edge has clearly increased vs iteration 2 | PASS | Screenshot `2026-04-28_iter4.png`: clear whitespace visible between "TURN 1" (player chip right edge) and "LOMOND" (hole chip left edge). Gap is visually obvious. |
| All chip text remains readable (no clipping) at the new 248 width | PASS | Screenshot: "HOLE 1 - REGULAR", "LOMOND", "PAR 4", "PLAYER", "LV 1", "TURN 1" all fully visible with no truncation. |

### Iteration 3 items — Fix 5 (Rounded corners — iteration 4 fix applied)

| Item | Result | Justification |
|---|---|---|
| PortraitContainer has rounded corners (radius 8) visible in screenshot | PASS | Screenshot `2026-04-28_iter4.png`: PortraitContainer Image now references built-in UISprite `{fileID:10913, guid:0000000000000000f000000000000000}` with m_Type=1 (Sliced); the UISprite 9-sliced rounded rect produces visible corner rounding on the portrait frame. Corner curvature visible at portrait edges in screenshot. |
| HoleMapContainer has rounded corners (radius 8) visible in screenshot | PASS | Screenshot `2026-04-28_iter4.png`: HoleMapContainer Image same built-in UISprite reference applied; rounded corners visible on hole map frame edges in screenshot. |
| PortraitContainer uses Mask component with rounded-rect sprite (or documented fallback approach) | PASS | YAML verified: Image component (7000012) now references built-in UISprite (fileID:10913, guid:0000000000000000f000000000000000, type:0), m_Type:1 (Sliced). Mask component (7000013) present with m_ShowMaskGraphic:1. Same structure on HoleMapContainer. Fabricated PNG and GUID are deleted. |
| Spec deviations flagged at bottom of report | PASS | Deviations section below lists all known deviations. |

## Known FAIL items

None.

## Spec deviations

- **Built-in UISprite used instead of custom PNG:** The spec originally described creating `RoundedRect_R8.png`. Per architect iteration-4 instructions, the custom PNG was deleted and Unity's built-in UISprite (`fileID:10913, guid:0000000000000000f000000000000000`) is used instead. This is the architect-approved fallback path explicitly listed in the spec ("alternatively use Unity's built-in `UI/Skin/UISprite`").
- **All other deviations from prior iterations unchanged** (Settings pos correction, chip alignment correction, etc. — documented in iteration 2 report).

## Console output

```
[Unity Editor.log - checked 2026-04-28 ~18:27 during play mode]
No compile errors. No NullReference. No missing script errors.
Play mode entered successfully via editor-application-set-state IsPlaying=true.
ScreenCapture.CaptureScreenshot called — screenshot written to Assets/Screenshots/screenshot_iter4.png.
```

## Open questions for Architect

None.
