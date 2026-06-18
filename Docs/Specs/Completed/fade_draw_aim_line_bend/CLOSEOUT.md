# CLOSEOUT — `fade_draw_aim_line_bend` (Order 355) — DONE (2026-06-18, Cesar-approved)

The earlier pipeline files (`IMPLEMENTER_REPORT.md`, `SELF_REVIEW.md`, `ARCHITECT_REVIEW.md`,
`CESAR_REJECTION.md`) describe the messy automated run that Cesar rejected on sight (segmented-`Image`
poly-line rendering as horizontal rungs, plus a broken-capture video). They are kept as the historical
record. This note records the ACTUAL shipped state, which was hand-driven by the architect after the
rejection.

## What shipped
- **`AimLineBendRenderer` rewritten** from N segmented `Image` GameObjects → a **single `MaskableGraphic`
  that emits one textured triangle-strip via `OnPopulateMesh`** (the `UILineRenderer` pattern the SPEC's
  Phase A actually asked for). The aim line now renders as a **smooth continuous curve** (no rungs/gaps),
  reusing the `Indicator - Direction` sprite (D6 look preserved). Cesar-chosen approach.
- Fixed a `[RequireComponent(CanvasRenderer)]`-not-inherited bug (mesh built but drew blank) by requiring
  it on the subclass + constructing the host GO with `CanvasRenderer`.
- Bend strength `AimLineCurveScale` 0.35 → **0.55** (Cesar: more pronounced/readable bend).
- Sign-faithful to Order 356 (DRAW vs FADE opposite; line direction == ball curve direction).
- Capture: a **single continuous normal-play shot** (boot ShellScene → Hole 6 → arm Fade/Draw via the
  on-screen button → bent aim line → charge → fire → ball launches+curves), recorded full 1170×2532 via
  `BotVideoRecorder`. No bespoke capture choreography, no camera tricks, no obtrusive captions.
- Fire path: `FireDebugShot` / `FireViaShotController` gained an optional `coneFinetune` (applied only when
  `FadeDrawActive`; default 0 keeps all callers straight) so the demo shot launches AND curves.

## Verification
- 488/488 EditMode tests pass (incl. the `AimLineBendTests` curve-math suite).
- Renderer mesh proven a single connected strip (50 verts / 48 tris, clean quadratic) + rendered smooth in
  real play. Buttons fixed in the same recording (see the separate action-button fix).

## Deliverable
- Video (gitignored): `videos/fade_draw_aim_line_bend_gate.mp4` (45.98s, 1170×2532). Copy archived at
  `Docs/Reports/Media/fade_draw_aim_line_bend_2026-06-18.mp4`.
- Stills: `screenshots/s01_straight_line.png`, `s02_fadedraw_armed.png`, `s03_draw_bent.png`,
  `s04_draw_ball_flight.png` (refreshed to the shipped-state 09:11 run).

## Known limitation (accepted)
- The ball's in-flight draw curve reads **subtly from the normal chase camera** (it re-centers behind the
  ball, flattening lateral curve on screen). The aim-LINE bend is clear; the ball's physical fade/draw was
  already proven in Order 356. A top-down trajectory overlay is the rigorous curve proof if ever needed.

Lesson AG (segmented-line anti-pattern + CanvasRenderer trap + capture-mechanism gate) recorded in
`tasks/lessons.md`.
