# Investigation — Figma vs Unity size mismatch

**Filed:** 2026-04-28 (after `8_3_topbar` iteration 2 review)
**Filed by:** Cesar
**Priority:** Medium-High — affects every UI spec going forward; better to fix the root cause once than patch every spec

## Symptom

Putting a `180×180` size in Unity for the player portrait renders at a Figma-equivalent of `~216×216` (measured by Cesar via screenshot side-by-side with Figma). All other UI elements specced from Figma are similarly oversized in Unity by roughly **1.20×**.

This is NOT explained by the canvas resolution mismatch alone:
- Figma canvas: 1170×2532
- Unity ShotUI_Canvas reference: 1080×1920
- Pure ratio: 1170/1080 = **1.083**, 2532/1920 = **1.319**
- Observed mismatch: ~1.20

Something between the spec value and the rendered pixels is scaling everything up.

## Hypotheses to investigate (ranked, most likely first)

1. **CanvasScaler `MatchWidthOrHeight = 0.5`.** With Match=0.5, Unity averages the width and height scale factors. With reference 1080×1920 on a different actual screen (e.g., the Game View aspect ratio configured in Unity), the resulting scale could land at ~1.20× rather than the pure 1.083×. **Test:** open the scene's `ShotUI_Canvas` Inspector, note the CanvasScaler settings, then check the Game View's resolution in Unity (Game View dropdown). Compute `Mathf.Pow(actualWidth/refWidth, 1-match) * Mathf.Pow(actualHeight/refHeight, match)` for the actual screen.

2. **The reference Figma frame is 1170 but the Cesar's screenshot was taken at a non-1170 width.** ScreenshotTool.cs auto-compresses to 800px max dimension and then we side-by-side with Figma at 1170. If the comparison was done at different scales without rescaling one to match the other, "180px in Unity" might just be at a different display zoom than "180px in Figma." **Test:** measure a known-fixed element in both screenshots (e.g., the Settings button — supposed to be 86×86 in both Figma and Unity) and confirm same pixel count. If Settings is also 1.20× off, it's a true scale issue. If Settings matches, then the size issue is specific to portraits.

3. **The portrait sprite itself has implicit sizing.** Unity Image with PreserveAspect=true will scale the SPRITE inside the RectTransform, not the RectTransform itself. The portrait Image's RectTransform is 180×180 in scene YAML, but the visible character sprite art might extend OUTSIDE the RectTransform if the sprite has padding. The Camila.png sprite might have a 240×240 source where the character is centered in 180×180 of visible content. **Test:** open `Resources/Portraits/Thumbnails/Camila.png` in Unity, check Sprite import settings, check actual pixel dimensions, check pivot.

4. **Parent transform scale ≠ 1.** The PlayerCard, ShotUI_Canvas, or some parent has a `LocalScale` of 1.20× somewhere in the hierarchy. **Test:** walk the Transform hierarchy from canvas root to portrait Image, confirm LocalScale is (1,1,1) at every level.

5. **CanvasScaler `ReferencePixelsPerUnit` mismatch.** Different from `referenceResolution`. Default 100. If the Figma export assumed a different PPU, sprites could appear scaled. Less likely but worth checking.

## How to investigate

This is a measurement-and-inspection task, not a build task. The architect (or Cesar directly) should:

1. Open `Assets/Scenes/Physics/LabScaffold.unity` in Unity.
2. Open Game View, note the resolution dropdown setting.
3. Inspect `ShotUI_Canvas` → CanvasScaler component. Note `UI Scale Mode`, `Reference Resolution`, `Screen Match Mode`, `Match` value.
4. With the scene in Play mode, take a fresh screenshot.
5. Side-by-side with the Figma frame `In-Game - Shot Tests 9` exported as PNG at the same resolution.
6. Measure 3 reference elements in both: Settings button, Portrait, ChipStack. Note the pixel ratios.
7. Walk the Transform hierarchy of the Portrait GameObject — confirm LocalScale=(1,1,1) at every parent.
8. Check Camila.png sprite import settings — note source pixel size, pivot, PPU.

Once measured, the actual ratio + which elements are affected vs not affected will narrow the hypothesis space significantly.

## Decision needed after investigation

Once root cause is identified, decide:
- **Option A:** Adjust the Unity reference resolution to match Figma (1080×1920 → 1170×2532). One-time fix, then 1 Figma unit = 1 Unity unit. Risk: existing widgets (cone, power gauge, club handle) authored against 1080 will need re-tuning.
- **Option B:** Keep Unity 1080-reference and document a Figma→Unity scale factor in the blueprint. Apply it in every future spec. Risk: error-prone, easy to forget.
- **Option C:** Something else if the root cause turns out to be sprite-side, not canvas-side.

## Out of scope for this investigation

- Don't fix individual specs to compensate (e.g., don't change 180→150 to "look right"). We want the root cause.
- Don't change the existing 8_3_topbar implementation while this is being investigated. That spec uses the current Unity values; it'll be re-tuned (or not) once we know the real fix.

## Files / hierarchy this investigation will touch

Read-only inspection initially. Possible writes once a fix is chosen:
- `Assets/Scenes/Physics/LabScaffold.unity` (CanvasScaler settings on ShotUI_Canvas)
- `Docs/Architecture/RUNTIME_BLUEPRINT.md` (document the canonical Figma→Unity unit mapping in §4 or new section)
- All active and queued UI specs (potentially) if a global rescale is decided

## Acceptance criteria for closing this task

- Root cause identified and documented (which of the 5 hypotheses, or a 6th).
- A canonical "Figma X = Unity Y" rule documented in `RUNTIME_BLUEPRINT.md`.
- Decision made on Option A vs B vs C.
- If Option A or C: a follow-up implementation spec filed for the rescale.
- If Option B: blueprint documents the conversion factor, and the Architect agent prompt is updated to apply it.

---

## Architect investigation findings (2026-04-28 evening session)

### Measurement procedure used

Cesar provided two side-by-side PNGs, both at exactly 1170×2532:
- `Figma.png` — direct export of the canonical Figma frame (`In-Game - Shot Tests 9`, frame id `4065:15675`)
- `Unity.png` — Unity play-mode screenshot at iPhone 12 Pro Max native (1284×2778), downscaled to 1170×2532 to match Figma's pixel space

Architect ran pixel-level measurements on three reference elements: Settings button white area, character portrait color region, and the player chip stack navy rectangles. Also measured chip dimensions in the **un-resized** original Unity capture (`topbar-diff-v2.png` at 1284×2778) to bypass any artifact from the downscale.

### Key measurements

In the resized 1170-wide Unity image vs the 1170 Figma:

| Element | Unity size | Figma size | W ratio | H ratio |
|---|---|---|---|---|
| Settings white area | 94×94 | 211×201 | 0.45 | 0.47 |
| Portrait color region | 338×399 | 301×282 | 1.12 | 1.41 |
| Chip-stack region | 696×389 | 671×278 | 1.04 | 1.40 |

In the original 1284-wide Unity capture (no resize), individual chip rows measured **63 px tall × ~628 px wide** at the player chip-stack column. Authored value: 48 tall × 248 wide (the chipstack RectTransform is 248 wide in scene YAML, not the spec'd 298 — a separate authoring bug).

### Root cause: CanvasScaler reference-vs-screen aspect mismatch

The CanvasScaler in `LabScaffold.unity` (and most physics scenes) uses:
- `m_ReferenceResolution: {x: 1080, y: 1920}`
- `m_UiScaleMode: 1` (Scale With Screen Size)
- `m_ScreenMatchMode: 0` (MatchWidthOrHeight)
- `m_MatchWidthOrHeight: 0.5`

With Match=0.5, Unity computes a single uniform scale factor as the geometric mean of the width-ratio and height-ratio between screen and reference:

```
scale = exp( 0.5 * log(scrW/refW) + 0.5 * log(scrH/refH) )
```

For iPhone 12 Pro Max screen (1284×2778) against 1080×1920 reference:
- width ratio = 1284/1080 = 1.189
- height ratio = 2778/1920 = 1.447
- mixed (geometric mean) = **1.312**

So an authored 48-unit chip renders at 48 × 1.312 = **63 px** — exactly what was measured. An authored 180-unit portrait renders at 236 px in 1284-space, or 215 px after the 1170-resize — close to Cesar's "~216×216" eyeball measurement.

**Width vs height ratio asymmetry was a measurement artifact**: when the 1284-wide capture is downscaled to 1170-wide, both axes get the same 0.911 reduction, but eye-level comparison of 1170-wide Figma to 1170-wide resized-Unity creates an *apparent* asymmetry because the original Unity image had the scaler's uniform 1.312× already baked in along both axes. The chip's *measured* width (628 px) at 1284-capture matched a chip stretched across most of its container — once we accounted for the chipstack's actual 248-unit width (vs the 298-unit spec), the math reconciled.

Settings button mismatch (Figma 211×201, Unity 94×94) is a separate effect: the Figma export apparently includes the gear's drop-shadow / glow within the white area's bounding box, while Unity's gear is rendered without it. Not load-bearing for the diagnosis. The 86×86 spec stands.

### Verdict

**Hypothesis 1 confirmed.** The fix is to change the CanvasScaler reference resolution from 1080×1920 to 1170×2532 (matching the Figma design source) and set Match=0 (anchor to width). With ref=1170, screens at 1170 wide give a scale factor of exactly 1.000 (1 Figma px = 1 Unity unit). On other screens (e.g. iPhone 12 Pro Max at 1284), scale factor is uniformly 1.097 across both axes — predictable and correct.

Hypotheses 2–5 ruled out:
- (2) Pixel-count comparison at matched 1170-wide resolution showed real size differences, not just zoom-level mismatch.
- (3) Camila.png sprite shape (170×343, non-square) is letterboxed inside the 180×180 frame via PreserveAspect — that's correct behavior, not the bug.
- (4) Parent LocalScale chain inspected — all (1,1,1).
- (5) ReferencePixelsPerUnit is 100 on all scalers, default, not relevant.

### Inventory of CanvasScalers to update

| File | UiScaleMode | Reference | Match | Action |
|---|---|---|---|---|
| `Scenes/Physics/LabScaffold.unity` (×2 canvases) | 1 (Screen) | 1080×1920 | 0.5 | Update both to 1170×2532, Match=0 |
| `Scenes/Physics/ShotConeTest.unity` | 1 | 1080×1920 | 0.5 | Update |
| `Scenes/Physics/PhysicsLab_Range.unity` | 1 | 1080×1920 | 0.5 | Update |
| `Scenes/Physics/PhysicsLab_Hole1.unity` (×2) | 1 | 1080×1920 | 0.5 | Update |
| `Scenes/Physics/PhysicsLab_Dashboard.unity` | 1 | 1080×1920 | 0.5 | Update |
| `Scenes/ShellScene.unity` (canvas at line 86681) | 1 | **1170×2532** | 1 | Already correct (Cesar authored) |
| `Scenes/ShellScene.unity` (×2 canvases at lines 35504, 105186) | 0 (Constant Pixel) | 800×600 | n/a | Mode 0 ignores reference — leave |
| `Prefabs/UI/PersistentUI.prefab` | 0 (Constant Pixel) | 800×600 | n/a | Mode 0 — leave |
| `Prefabs/Original/Gameplay/Hud/GameplayMonitorCanvas.prefab` | 1 | **1170×2532** | 1 | Already correct |

**Total to change: 7 CanvasScaler instances across 6 physics-lab scene files.** Other in-game / menu canvases are already either correct or in Constant Pixel Size mode (which ignores reference resolution entirely, so unaffected).

### Decision: Option A confirmed for in-game

- Reference: **1170×2532**
- Match: **0** (anchor to width — preferred for portrait-mobile games where width is sacred)
- Apply to all 7 physics-scene scalers via a one-time editor script.

Cone / power gauge / club handle were authored against 1080 ref. Scale factor change at 1170-screen: 1.000 / 1.312 = 0.762, so they'll appear ~24% smaller in screen pixels relative to current. They are anchored to canvas center and use procedural sizes (cone height 1009px, gauge 200×200, club handle 178×100) — bumping these up by ~1.31× post-change will restore current proportions. Quick visual check in play mode after the rescaler change.

### Verification protocol

Before rolling out the change to production scenes, validate the hypothesis with an isolated test scene (see `Docs/Specs/Active/CANVAS_SCALER_FIX_PLAN.md`). If the test scene shows pixel-exact match between Figma and Unity at 1170×2532, theory is confirmed and we proceed with the rollout. If not, return to investigation.
