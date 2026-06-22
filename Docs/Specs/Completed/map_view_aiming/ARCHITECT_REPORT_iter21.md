# ARCHITECT REPORT — `map_view_aiming` (Order 352), iter-21 status

**Date:** 2026-06-20
**From:** Claude Code (orchestrator), for the human Architect (Cesar's claude.ai chat)
**Trigger:** Cesar reviewed the iter-21 canonical. Six issues remain after 21 iterations. He asked for a report.

## 0. Where we actually are
The **v2 architecture is right and should be kept**: overlay camera (no RenderTexture/uvRect/flip), real `HoleCardWidget` entry, **club carry (124 yd, not driver 154)**, concentric rings accepted, tight framing (no off-field grey), shot-UI ball culled, untampered §11 validator exits 0, Physics diff empty. That's real progress vs the iter-15 mess.

**But the §11 numeric gate is GREEN while six visual things are wrong.** That is the core problem, repeated: the invariant assertions verify *weaker* properties (markers in-viewport, screenY ordering, loose collinearity) than "looks like the reference." So the pipeline keeps passing visually-wrong work. And the six issues are not one bug — they are six **independent ad-hoc formulas** that never add up to the single coherent aim model SPEC §6 demands ("guide line, landing zone, power rings share ONE aim direction and origin").

## 1. Cesar's six issues, each root-caused in `MapViewController.cs`

| # | Cesar's issue | Root cause (code) |
|---|---|---|
| 1 | Labels not stacked 120/100/80 — "100 on top, 120 & 80 to the sides" | `UpdateRingLabels` (L951-961) places labels at **clock positions** (80=3 o'clock, 100=12, 120=9 o'clock) on purpose, to avoid overlap. Cesar wants them **stacked vertically along the aim line**, outer→inner = 120 (far/top) → 100 → 80 (near/bottom), each sitting on its own ring. The clock layout is the wrong model. |
| 2 | Aiming at OB instead of the natural starting aim | `Open()` (L366-373) **overrides** the game's natural aim: `_aimYawRadians = flagAim` (a heading recomputed toward `_flagWorldPos`). Because the pin is mis-resolved (#5), "toward flag" points into OB/trees. It should keep the **natural `ShotController.CameraHeadingRadians`** the shot starts with (saved at L341 but then discarded). |
| 3 | Blue line is "straight with 2 bumps", not a natural flight path | `UpdateGuideLine` (L889-896) sets each of 24 vertices' Y to `SampleTerrainHeight(bent)` — the line **hugs the terrain**, so it bumps over every mound (the "2 bumps"). It needs to read as a **trajectory** (smooth arc / smooth ground line), not a terrain-conforming polyline. |
| 4 | Rings and line don't align — rings should center on the line's endpoint | Rings center at `landCtr = ball + aimDir·carry` (straight, L844/874). With Fade/Draw armed the guide line **bends** to an endpoint offset sideways by `LateralAtT(1)·len` (L894), so line-end ≠ ring-center. Also the ring **radii are arbitrary** (`0.12/0.18/0.24·carry` clamped 3-20 m, L870-872) — they do **not** represent the 80/100/120%-power landing distances; they're just three nested circles. The model isn't "where the ball lands at each power", it's decorative. |
| 5 | Flag indicator sits in the fairway, not on the pin | `_flagWorldPos = HoleContext.PinWorld`, set by `PhysicsLabController` from a name-matched "Flag" GO or a `GreenCentroid` fallback. It resolves to a **fairway position**, not the real pin/green. The hole indicator (L687 `BuildHoleIndicator`) faithfully draws to a wrong point. Needs the authored pin (`GreenTopology.GetDefaultPin`) or the correct Flag GO. |
| 6 | Color-coded landing zone is missing | `BuildLandingZoneDecal` (L518) builds a **white/yellow→transparent** disc (NOT the reference's red→green heat gradient), centered at `landCtr` — i.e. **underneath the rings**, which occlude it, and it's the wrong colors. Effectively invisible. |

## 2. The meta-problem (why 21 iterations didn't converge)
- **No single source-of-truth aim model.** Guide line, rings, labels, landing zone, flag, and the open-aim are each computed separately with hand-tuned constants. Fixing one (e.g. concentric rings) leaves the others (line endpoint, labels, landing zone) referencing a different origin/convention. This is the literal "patch over a patch."
- **The gate doesn't encode the visual requirements.** §11 asserts in-viewport + ordering + loose collinearity. It does NOT assert: rings centered at the *bent* line endpoint, labels stacked outer→inner along aim, landing-zone visible & red→green, aim == natural heading, line smooth-not-terrain-bumped, pin on the green. So every one of Cesar's six can be true while the gate is green.
- **The reference image was never matched element-by-element.** `reference_old_ui.jpg` shows the exact target (ball at bottom; ONE curved aim line to a single landing; a red→green target blob AT that landing; thin rings around it labeled 80/120 along the line; flag on the green). No iteration sat down and reproduced that layout; each chased the last-named symptom.

## 3. Recommendation
1. **Keep v2 architecture.** Don't reset again.
2. **Author ONE precise visual model** (Architect, against `reference_old_ui.jpg`), defined relative to a single computed **landing endpoint `L = ball + aimDir·carry (+ Fade/Draw lateral)`**:
   - Guide line: smooth ball→L (gentle arc or flat), not terrain-conforming.
   - Landing endpoint L is the shared origin for: the red→green gradient blob, the concentric rings, and the labels.
   - Rings = the 80/100/120%-power **landing distances** (or a defined dispersion), centered on L, drawn on top of the blob, thin.
   - Labels stacked along the aim at each ring, 120 far → 80 near, white.
   - Flag indicator at the **real pin** (`GetDefaultPin`), on the green.
   - Open aim = the **natural** `CameraHeadingRadians`; framing must keep L on-screen without re-aiming.
3. **Make Cesar's eye (or encoded visual asserts) the acceptance**, not the current §11 set — or extend §11 to assert the six things above (ring-center==line-end, label order, landing-zone-visible, aim==natural, pin-in-green). A gate that can't see these will keep passing them.
4. Given the convergence failure, the fastest path is likely **the Architect authoring the exact model in §6 (numbers/colors/positions relative to L)** and one implementer pass against it — or an interactive fix with Cesar rather than the blind review loop.

## 4. Accounting
21 iterations. iter-1..15 = the withdrawn RT/flip/entry-point mess (see `ARCHITECT_ESCALATION.md`). iter-16..21 = v2: fixed architecture/carry/framing/rings-concept/ball-cull, but the **per-element visual coherence** (this report) is still unmet, and the numeric gate didn't catch it. The honest read: the incremental subagent loop is good at the structural/mechanical fixes and bad at "match this picture" — that needs an explicit visual model and a human (or visual-encoded) gate.
