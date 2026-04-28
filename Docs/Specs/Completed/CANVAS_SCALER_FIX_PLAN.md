# Canvas Scaler Fix — Plan & Rollout

**Status:** PLAN — ready to execute 2026-04-29 morning
**Filed:** 2026-04-28 evening
**Closes:** `Docs/Specs/Queued/FIGMA_UNITY_SIZE_MISMATCH.md` (investigation findings appended there)
**Decision locked:** Reference resolution **1170×2532**, Match **0** (anchor to width)

---

## TL;DR

Move all in-game CanvasScalers from `1080×1920 / Match=0.5` to `1170×2532 / Match=0`. After the change, 1 Figma px = 1 Unity unit at design resolution, removing the constant scale offset that has been distorting every UI spec since Phase 8 started.

Validate with an isolated test scene first. Roll out to the In-Game UI only. Plan the menu rollout for later (when we wire menus to gameplay), and don't touch the menu canvases yet.

---

## Step 1 — Game View custom resolutions (one-time setup, ~2 min)

In Unity, open Game View → resolution dropdown → "+" → add two custom resolutions:

| Label | Type | Width | Height |
|---|---|---|---|
| `iPhone 14 Pro 1170×2532` | Fixed Resolution | 1170 | 2532 |
| `iPhone 12 Pro Max 1284×2778` | Fixed Resolution | 1284 | 2778 |

Use the 1170 one as the **design-reference** view. Use 1284 to spot-check that things still look right on a different aspect.

---

## Step 2 — Isolated test scene (validate hypothesis before rollout)

### Goal

Prove that with `ref=1170×2532, Match=0` and the Game View at 1170×2532, an authored 180×180 RectTransform renders at exactly 180 screen pixels. If it does, hypothesis confirmed and rollout is safe. If not, return to investigation.

### Build (manual, ~5 min)

1. New scene: `Assets/Scenes/Tests/CanvasScalerTest.unity`
2. Create `Canvas` (GameObject → UI → Canvas)
3. CanvasScaler settings: variable per test (see matrix below)
   - UI Scale Mode: **Scale With Screen Size**
4. Add a child `Image` GameObject:
   - Name: `TestSquare180`
   - RectTransform: anchor `(0,1)` / `(0,1)`, pivot `(0,1)`, position `(48, -48)`, size `(180, 180)`
   - Image color: solid red `#FF0000` (high contrast for measurement)
   - Sprite: None (uses default UI sprite)
5. Add a child `TextMeshProUGUI`:
   - Name: `TestLabel`
   - RectTransform: anchor `(0,1)` / `(0,1)`, pivot `(0,1)`, position `(48, -240)`, size `(400, 60)`
   - Text: `180×180 box (Figma 1170 ref)`
   - Font: Rubik-VariableFont SDF, size 30, color black
6. Save scene.

### Test matrix

For each row, set the CanvasScaler values, set Game View to **1170×2532**, take a play-mode screenshot, measure the red square's pixel dimensions:

| # | Reference | Match | Expected red box size (1170 view) |
|---|---|---|---|
| 1 | 1080×1920 | 0.5 | 180 × 1.097 ≈ 197 px (current bad state, but at 1170 not 1284 — should be milder) |
| 2 | 1080×1920 | 0 | 180 × 1.083 = 195 px |
| 3 | 1170×2532 | 0.5 | exactly 180 px (both ratios are 1.000 since reference matches screen) |
| 4 | **1170×2532** | **0** | **exactly 180 px** ← the proposed configuration |

Pass criteria: row 4 measures **180 ± 1 px** square in the screenshot.

If row 4 passes → theory confirmed, proceed to Step 3.
If row 4 fails → architect investigates further before any production change.

### Comparison reference

Open `Figma.png` (the 1170-wide Figma export Cesar already produced) and overlay/diff the test scene screenshot. The 180×180 should land at the same pixel position as a 180×180 rect drawn in Figma at the same anchor.

---

## Step 3 — Roll out to In-Game UI (after Step 2 passes)

### Editor script approach

Write a one-shot editor menu item: `GOLFIN/Tools/Migrate CanvasScalers to 1170×2532`.

Logic:
1. For each scene in the Build Settings list (or hard-code the 5 physics-lab scene paths):
   - Load via `EditorSceneManager.OpenScene(..., OpenSceneMode.Additive)`
   - For each CanvasScaler in the scene where `uiScaleMode == ScaleWithScreenSize` AND `referenceResolution == (1080, 1920)`:
     - Set `referenceResolution = (1170, 2532)`
     - Set `screenMatchMode = MatchWidthOrHeight`
     - Set `matchWidthOrHeight = 0`
     - Mark scene dirty
   - Save scene
2. Walk prefabs the same way (only `GameplayMonitorCanvas.prefab` is in scope, and it's already correct — script should detect "no change needed" and report).
3. Print a summary: scenes touched, scalers updated, scalers skipped (already correct or wrong mode).

### Target files (verified inventory from investigation)

```
Scenes/Physics/LabScaffold.unity        — 2 scalers (LabCanvas + ShotUI_Canvas)
Scenes/Physics/ShotConeTest.unity       — 1 scaler
Scenes/Physics/PhysicsLab_Range.unity   — 1 scaler
Scenes/Physics/PhysicsLab_Hole1.unity   — 2 scalers
Scenes/Physics/PhysicsLab_Dashboard.unity — 1 scaler

Total: 7 scalers across 5 files, all physics-lab scenes.
```

### Already-correct (script confirms and skips with log message)

- `Scenes/ShellScene.unity` line 86681 (1170×2532, Match=1) — leave alone (Cesar authored)
- `Prefabs/Original/Gameplay/Hud/GameplayMonitorCanvas.prefab` (1170×2532, Match=1) — leave alone

### Untouched regardless (mode 0 ignores reference resolution)

- `Scenes/ShellScene.unity` lines 35504, 105186 (800×600, mode 0)
- `Prefabs/UI/PersistentUI.prefab` (800×600, mode 0)

### Procedure

1. **Backup branch.** `git checkout -b canvas-scaler-migration` and tag `pre-canvas-scaler-fix`.
2. Run the migration script. Inspect the git diff — should be 7 small CanvasScaler edits, no other changes.
3. **Re-tune `LabScaffold.unity` widget sizes.** The cone, power gauge, and club handle were authored against 1080-ref. After the change, screen-px scale at 1170-ref is 0.762× of what it was. Two paths:

   **Path 3a (recommended): bump procedural sizes to restore current visual.**
   - `ShotConeView._coneHeightPx`: 1009 → ~1325 (1009 × 1080/1170 inverse → ~1093, but ×1.31 to fully restore previous on-screen size → ~1325)
   - `ShotConeView._handleWidth`: 178 → ~234
   - `ShotConeView._handleHeight`: 100 → ~131
   - `ConeMeshGraphic._heightPx`: 1009 → ~1325 (must match `_coneHeightPx`)
   - `TimingSlabGraphic._coneHeightPx`: 1009 → ~1325
   - `PowerGaugeGraphic._innerRadius`: 80 → ~105, `_outerRadius`: 100 → ~131
   - `PowerGaugeWidget` RectTransform: 200×200 → 263×263
   - `PowerGaugeWidget` anchored position: `(-180, -460)` → `(-235, -603)` (proportional shift to keep visual location)

   **Path 3b: leave widgets at current numbers and accept the size shrink as a minor visual tweak.** They were eyeballed anyway.

   **Cesar's call** based on play-mode comparison. Likely Path 3a for the cone (it's center-anchored and prominent) but Path 3b is fine for the gauge.

4. **Verify the 8.3 topbar.** Player card / hole card / settings should now render at exact Figma sizes. Take a fresh `topbar-diff-v3.png` at 1170×2532 game view and compare to Figma.
5. **Fix the lingering 8.3 authoring bugs** flagged in the investigation:
   - ChipStack RectTransform width: `248 → 298` on both PlayerCard and HoleCard
6. Run play-mode smoke test on each affected scene (LabScaffold + the four `PhysicsLab_*` scenes). Cone fires correctly, gauge animates, no UI cut off at canvas edges, no console errors.
7. Commit `canvas-scaler-migration: in-game scenes to 1170×2532 / Match=0` and merge.

---

## Step 4 — Plan for menu screens (defer execution)

**Don't migrate menu screens yet.** Reasoning:
- Menu canvases (Roster, Inventory, Bags, Items) are built on top of `PersistentUI.prefab` which uses Constant Pixel Size mode and is unaffected.
- The ShellScene has 3 CanvasScalers; one is already 1170×2532 and the other two are Constant Pixel Size mode. So no menu screen is currently authored against the bad 1080×1920 / Match=0.5 combo.
- The "menu → gameplay integration" task is on the roadmap (item C in TellCode roadmap). When we get there, we'll create new canvases for the Hole Picker and any new shell screens, and they should be authored at 1170×2532 / Match=0 from the start.

**Action item for the menu rollout:** when wiring menus to gameplay (roadmap item C), do an audit pass across all menu-screen prefabs to confirm they're all on Constant Pixel Size mode OR already at 1170×2532. If any are found at 1080×1920 / Match=0.5, run the same migration script on those files only. Add this audit as a pre-condition for closing roadmap item C.

---

## Step 5 — Update Architect agent prompt and blueprint

Add a new section to `Docs/Architecture/RUNTIME_BLUEPRINT.md`:

```
## UI Coordinate System

Canonical reference: **1170×2532** (iPhone 14 Pro / 13 Pro point grid × 3, matches Figma source).
Canonical scaler config: `Scale With Screen Size, Reference 1170×2532, Match Width Or Height, Match 0`.

At 1170-wide screens, 1 Figma px = 1 Unity unit (scale factor 1.000).
At 1284-wide screens, scale factor = 1.097 (uniform on both axes).
At 1080-wide screens, scale factor = 0.923.

When writing a UI spec, extract Figma values directly and use them 1:1 in Unity.
No conversion factor needed.

Exceptions:
- `PersistentUI.prefab` and ShellScene's secondary canvases use Constant Pixel Size mode (800×600 reference is ignored). These were authored before the design system standardized on 1170.
- If a new canvas needs a different mode, document the rationale here.
```

Update the Architect agent prompt to reference this section instead of any hard-coded "Figma is 1170 but Unity is 1080, divide by..." instructions (if any such instruction exists).

---

## Acceptance criteria for closing this task

- [ ] Game View has the two custom resolutions added (Step 1)
- [ ] Test scene built and the matrix run; row 4 measures 180×180 ± 1 px (Step 2)
- [ ] Editor migration script runs cleanly on all 7 in-scope scalers (Step 3.2)
- [ ] LabScaffold cone/gauge/handle visually unchanged after rescaler change (Step 3.3)
- [ ] 8.3 topbar measures 1:1 with Figma at 1170 view, ChipStack width fixed (Step 3.4–3.5)
- [ ] No play-mode regressions in physics-lab scenes (Step 3.6)
- [ ] Migration committed and merged (Step 3.7)
- [ ] Blueprint updated with UI coordinate system section (Step 5)
- [ ] Audit task added to roadmap item C for menu rollout (Step 4)

## Out of scope

- Menu / inventory / roster screens (defer to roadmap item C)
- Constant-Pixel-Size canvases (mode 0) — they don't have the bug
- Re-authoring the original 8.3 spec numbers — those numbers were correct, they just weren't being rendered at the spec'd size due to the scaler issue
- iPhone 15 Pro Max or other newer devices — 1170 ref + Match=0 handles them via uniform scale factor, no per-device tuning needed

---

## Tomorrow's session order (2026-04-29)

After this plan is executed:

1. **Test hypothesis** (Steps 1–2 above)
2. **Fix In-Game UI** if confirmed (Step 3)
3. **Make plan for menu screens** (Step 4 — already drafted, just confirm with Cesar)
4. **Resume Phase 8 implementation** with the new scaler in place — next part is **8.4 (Wind + Hole indicators)** per `PHASE_8_SHOT_UI_POLISH.md`
