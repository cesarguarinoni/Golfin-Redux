# Cesar Rejection — `fade_draw_aim_line_bend` (after ARCHITECT_REVIEW_PASS, iter-3)

Cesar rejected the iter-3 `ARCHITECT_REVIEW_PASS` on sight. The full pipeline (implementer → self-review → reviewer → red-team) green-lit it; Cesar caught the defects in one frame. This is logged to `.claude/review_misses.log`.

## What Cesar saw (the defects)
1. **The aim line is a stack of horizontal rungs, not a curved line.** The segmented-`Image` poly-line stretched the vertical `imgLine1` sprite into short tilted rects with transparent gaps between them, so it read as ladder rungs / slashes — never a continuous curve. The segment *centroids* sat at the right lateral offset, which is why every reviewer's "+59px" measurement passed while the thing never looked like a line. (Numbers measured; picture not seen.)
2. **Broken game UI in the video** — the bottom buttons (SPIN / GOLFIN / FADE-DRAW / DRIVER) render with no white icon parts.
3. **A flipped (upside-down) frame** still present in the video.

## Root cause of #1 — and Cesar's architectural call
Cesar challenged the whole approach: *"is a segmented line with images the best way to draw a curved line in Unity? I suspect it is not."* He is right. Confirmed against current practice (OnPopulateMesh/`VertexHelper`, UI Extensions `UILineRenderer`): the segmented-`Image` poly-line is an anti-pattern (N GameObjects, N draw calls, gap/rotation artifacts). **Decision (Cesar-approved): rewrite as a single `MaskableGraphic` that emits ONE textured triangle-strip mesh via `OnPopulateMesh`.** This is also what SPEC Phase A originally asked for ("a sprite-textured `UILineRenderer`-style mesh") — the implementer built the segmented hack instead.

## Fix done by the architect (this session) — renderer rewrite
- `AimLineBendRenderer` rewritten: `MonoBehaviour` (N child Images) → `MaskableGraphic` with `OnPopulateMesh`. One continuous textured ribbon following the quadratic curve. Same `imgLine1` sprite, UV-stretched once along length (D6 preserved). Curve math unchanged (sign + clamp identical; EditMode tests preserved via a pure `LateralAtT`/`TipLateralX` API).
- `ShotConeView.SetupBendRenderer` now hosts the Graphic on a dedicated child GO `AimLineMesh` (one Graphic per GameObject; the parent keeps the original Image, disabled).
- **Second bug found + fixed during verification:** `[RequireComponent(typeof(CanvasRenderer))]` on the `Graphic` base class is NOT honoured by runtime `AddComponent` on a subclass → the GO had no `CanvasRenderer`, so the mesh was built but never drawn (blank line). Fixed by declaring `[RequireComponent(typeof(CanvasRenderer))]` on `AimLineBendRenderer` AND constructing the GO with `typeof(CanvasRenderer)` explicitly.

### Verification done
- Compiles clean (no errors).
- EditMode tests: **487/487 pass** (3 unrelated skips). Curve is a clean quadratic (lateral ratio 1:4:9:16 in t²), DRAW = negative local X.
- Mesh geometry dump: single connected strip, 50 verts / 48 tris, **no gaps**.
- **Rendered pixel proof (play mode):** three lines — straight (vertical), draw, fade — render as **smooth continuous curves**, draw/fade bending opposite directions. The rungs are gone. (Shown inline to Cesar.)

## What still remains (NOT done — the deliverable)
- Defects #2 (broken UI buttons) and #3 (flipped frame) are **video-capture (BotVideoRecorder) artifacts**, not fixed yet. Still pixels via `CaptureCore` were clean; the BotVideoRecorder path is the unreliable one.
- A production capture over a real loaded hole (1170×2532) of the NEW renderer, plus a clean normal-play video (no y-flip, intact UI, ball fires and curves matching the line), per SPEC § Capture gate, still needs to be produced and re-reviewed.

STATUS → `CESAR_REJECTED`.
