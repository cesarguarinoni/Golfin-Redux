# Cone Mesh Iteration Log — Phase 8.1 Visual Polish

**Session:** 2026-04-27  
**Files touched:** `ConeMeshGraphic.cs`, `ConeBandPalette.cs`, `ShotConeView.cs`, `LabScaffold.unity`  
**Commits:** phase-8.1 through phase-8.6b  

---

## Goal

Polish the aiming cone rendered by `ConeMeshGraphic` (a custom `MaskableGraphic` using `VertexHelper`) to match the Figma reference: semi-transparent grey fill, dark-to-light center gradient, three horizontal colored band lines, smooth silhouette edges.

---

## Context: Technical Setup

- **Class:** `ConeMeshGraphic : MaskableGraphic` in `Assets/Scripts/Gameplay/UI/ShotUI/`
- **Canvas reference resolution:** 1080×1920 (Unity default). Target device is iPhone 15 Pro Max (2778×1284 physical pixels, portrait). Scale factor ≈ 1.19×.
- **Cone geometry:** `_halfAngleDeg = 12.5°`, `_heightPx = 1009f`. Slope = 4.5:1 (every 1 canvas unit horizontal = 4.5 canvas units vertical step on silhouette).
- **Half-base:** `1009 × tan(12.5°) ≈ 223.7` canvas units.
- **Canvas pivot:** (0.5, 0) — apex at local Y = _heightPx, base at Y ≈ 0.
- **Scenes used for testing:** `Assets/Scenes/Physics/ShotConeTest.unity` (portrait, cone centered on ball) and `Assets/Scenes/Physics/LabScaffold.unity` (3D physics lab, portrait overlay).
- **ConeAlphaController:** A separate MonoBehaviour that drives `CanvasGroup.alpha` from shot state. In `Idle` state = 0.25 — nearly invisible on dark backgrounds. Disabled for visual testing.
- **ShotConeView:** Reads `_coneHeightPx` (serialized, was stuck at 600f) and pushes it to `ConeMeshGraphic.HeightPx` on every state change — overrode code defaults until patched.

---

## Iteration Log

### phase-8.1 (baseline)
**What it was:** Initial cone mesh implementation. Flat fill, no gradient, simple band lines, no anti-aliasing.  
**Problems reported by Cesar:**
- Cone sides appeared "shagged" (jagged/serrated silhouette edges)
- Band lines also jagged
- Cone tip not centered in LabScaffold
- Cone rendered at ~600px tall instead of 1009px reference

---

### phase-8.1b / 8.1c / 8.1d — gradient + curvature passes

**Changes made:**
- Added center-dark to edge-light gradient (`_centerColor` black 50% alpha → `_fillColor` grey 35% alpha)
- Added `_centerDarkFraction` slider (0 = point-dark, 0.5 = flat dark center band)
- Curved base: `yBot = -_curvaturePx * (1 - absN²)` (parabolic dip at center)
- Band lines curved with same radius: consistent arc across fill and bands
- Shared-vertex grid to eliminate seam artifacts between strips

**Root cause of 600px height:** `ShotConeView._coneHeightPx` was serialized as 600f in the LabScaffold scene, overriding the code default of 1009f. Fixed by patching the serialized value via MCP Roslyn + EditorSceneManager.SaveScene.

---

### phase-8.2 — strip count + edge feather (attempt 1)

**Change:** Increased `_strips` from 256 to 512. Added horizontal `_edgeFadePx`: edge columns faded alpha based on `absN` (x-axis distance from silhouette).

**Problem:** Horizontal fade doesn't align with the diagonal silhouette. The staircase runs diagonally; an x-axis fade softens the wrong direction. Silhouette still appeared jagged.

**Math:** At 512 strips, each strip = `(2 × 223.7) / 512 ≈ 0.874` canvas units wide. Each strip causes a vertical step of `0.874 × 4.5 ≈ 3.94` canvas units = ~4.7 physical pixels per stair step. Visible at full resolution.

---

### phase-8.3 — band lines smoothed, LabScaffold positioning

**Changes:**
- Band lines: switched to `_strips / 2 = 256` strip count (512 made bands too fuzzy/blurry — Cesar's feedback)
- `BandFeatherPx = 2f`: feathered top/bottom edges on band lines (alpha fade above/below band)
- LabScaffold: patched `ConeMesh.sizeDelta = (0,0)`, `anchoredPos = (0, 120)` to center cone tip on ball

**Feedback from Cesar:** Bands back to 256 was correct. Cone center now aligned. Silhouette still jagged.

---

### phase-8.4 — cone size fix

**Change:** Patched LabScaffold `_heightPx` serialized value from 600 to 1009 via YAML edit. Also patched `ShotConeView._coneHeightPx`.

---

### phase-8.5 — perpendicular silhouette feather (attempt 2)

**Approach:** Replaced horizontal edge fade with dedicated feather geometry: two thin triangle strips (left and right) whose inner edge sits exactly on the cone silhouette (fully opaque) and outer edge is offset by `_edgeFadePx` in the **perpendicular-to-silhouette** direction (fully transparent).

**Normal calculation:**
- Silhouette direction: `(-hb, heightPx)` (base to apex)
- Outward normal (rotated 90° clockwise): `(heightPx, hb)`, normalized
- With `_edgeFadePx = 8f`: `ox = 8 × (1009 / 1033.4) ≈ 7.81` canvas units horizontal, `oy = 8 × (223.7 / 1033.4) ≈ 1.73` canvas units vertical

**Problems Cesar reported after this commit:**
1. "Cone is too fuzzy" — 7.81cu horizontal extension = visibly soft/blurry edges
2. "Anti-aliasing making cone wider than colored band lines" — feather outer vertices extend ~8cu beyond where band lines terminate (bands end at cone silhouette; feather pushes past it)
3. "Colored bands should not be semi-transparent, only the cone" — `AddBandLine` was using `c0 = Color32(c.r, c.g, c.b, 0)` feather vertices above/below each band row, creating a transparent-to-opaque fade. Bands should be hard-edged and fully opaque.

---

### phase-8.6 — current state ✅

**Two targeted fixes:**

**Fix 1 — Reduce feather width:**  
`_edgeFadePx`: 8f → 2f  
- New ox = `2 × (1009 / 1033.4) ≈ 1.95` canvas units — stays within band line boundary, minimal visual bleed
- Still provides perpendicular anti-aliasing on the diagonal silhouette

**Fix 2 — Remove band line transparency:**  
`AddBandLine` rewritten: was 4-vert columns (outer-bot transparent → inner-bot opaque → inner-top opaque → outer-top transparent) producing 3 quad strips. Now: 2-vert columns (bot opaque → top opaque), 1 quad strip. Zero alpha fade.  
`ConeBandPalette.BandFeatherPx` constant removed (no longer used).

**Also patched:** LabScaffold.unity YAML `_edgeFadePx: 8` → `_edgeFadePx: 2` (serialized fields keep old values after code default changes).

---

## Current State (after phase-8.6)

| Parameter | Value |
|---|---|
| `_halfAngleDeg` | 12.5° |
| `_heightPx` | 1009f |
| `_strips` (fill + feather) | 512 |
| `_strips` (band lines) | 256 (`_strips / 2`) |
| `_curvaturePx` | 15f |
| `_centerDarkFraction` | 0f (inspector-tunable) |
| `_edgeFadePx` | 2f |
| `BandHalfHeightPx` | 2f (4px total band height) |
| Band y-positions | Red: 0.00, Gold: 0.45, Green: 0.85 |
| Fill alpha | Center: 50% black, Edge: 35% grey |

**Mesh structure (per OnPopulateMesh call):**
1. Fill grid: `(512+1) × 2` verts = 1026 verts, 1024 quads
2. Right feather strip: `(512+1) × 2` verts = 1026 verts, 512 quads
3. Left feather strip: same
4. Band line (red): `(256+1) × 2` verts = 514 verts, 256 quads
5. Band line (gold): same
6. Band line (green): same

Total: ~5130 verts, ~4096 quads — well within canvas budget.

---

## Remaining Visual Issues (not yet addressed)

1. **Silhouette still jagged at pixel level.** The perpendicular feather at 2px provides some softening but the fundamental staircase (4.7 physical pixels per strip step) is still visible under close inspection. Potential approaches the architect may want to consider:
   - Accept it: the staircase is sub-5px and may be acceptable in motion
   - Canvas `additionalShaderChannels` + custom material with MSAA or SDF-based alpha
   - Reduce `_halfAngleDeg` so the slope is steeper and steps are smaller vertically
   - Use a procedural texture/sprite approach instead of VertexHelper geometry

2. **Ball camera not centered in LabScaffold.** The Chase camera doesn't place the ball at screen center in portrait view. Deferred — acknowledged, not scheduled.

3. **Phase 8.1 remaining work** (from `PHASE_8_SHOT_UI_POLISH.md` spec): Power gauge, HUD elements (player card, hole card, wind/hole indicators, action buttons, ball/club selectors, centerpiece ball, trail). Cone mesh visual polish is unblocked; waiting for architect ack before proceeding to Phase 8.2.

---

## Key Lessons

- **Serialized Unity fields don't update when code defaults change.** Any `[SerializeField]` field set in a scene or prefab retains its serialized value even if the code default changes. Must patch scene YAML or use a Roslyn script to update in-editor.
- **Horizontal edge fade doesn't work on diagonal silhouettes.** Fading by x-distance is only correct for vertical edges. The cone silhouette is diagonal (4.5:1 slope) — the fade must be in the perpendicular direction.
- **Feather width directly controls apparent cone width.** At `_edgeFadePx = 8`, the feather extends ~7.8cu past the silhouette, making the cone visually wider than its own band markers. Keep `_edgeFadePx ≤ 3` to stay within band endpoints.
- **Band line feathering vs. cone feathering are separate concerns.** The user wants bands fully opaque (hard-edged arcs). Only the cone silhouette should have any alpha fade.
- **ConeAlphaController hides cone in Idle state.** Alpha = 0.25 at idle means the cone is nearly invisible without disabling this component for visual testing.

---

## Screenshot for Architect Reference

`Assets/Screenshots/_compressed/screenshot_2026-04-27_13-30-25.png` — ShotConeTest scene, edit mode, showing current cone state after phase-8.6.
