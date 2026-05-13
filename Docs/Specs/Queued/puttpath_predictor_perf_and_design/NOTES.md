# PuttPathPredictor — Full Redesign — Architect NOTES

**Status:** DESIGN_LOCKED — Cesar answered all 3 open questions 2026-05-13.
**Architect (claude.ai), 2026-05-07 initial, 2026-05-13 design-locked**

Spun out of §2b camera transitions because Cesar flagged: "PathPredictor needs work. We shipped but it eats a lot of processor and might be too much. We need to check what other games do."

Perf-only throttle path is dropped. Going straight to redesign — redesign makes the perf issue obsolete.

---

## LOCKED DECISIONS (2026-05-13)

| # | Question | Decision |
|---|---|---|
| L1 | Sim vs arcade positioning | **Sim** — GOLFIN sits closer to PGA 2K than Everybody's Golf. Player reads the green; game does not pre-compute the full putt path for them. |
| L2 | Perf throttle first vs redesign | **Redesign only.** Skip throttle-only path. Replacing the current live full-trajectory recomputation with a baked + lightweight system makes the per-frame sim cost moot. |
| L3 | Slope-arrow source | **Baked per-green-region on hole-load.** One-time bake when hole loads. Slope vectors stored per-cell, sampled at draw time. Deterministic, low runtime cost, no per-aim recompute. |

### Interpretation of L1 + L3 on the 5-option matrix

The 5 options originally framed:
- (a) Status quo + throttle — DROPPED per L2
- (b) Grid + slope arrows — **MATCHES L1/L3 cleanly**
- (c) Target marker at apex — too predictive for Sim positioning
- (d) Hybrid: short live segment + arrows — the short live segment is mild assistance, weaker fit for Sim
- (e) Aim-line + power gauge only — closer to fit, but arrows give the player real green-reading feedback that L1 wants

**Architect read: ship (b) — pure grid + slope arrows.** No live predicted curve segment at all. Player aim-line + power gauge + baked slope arrows on the green is the full feedback set. Putter behavior becomes a green-reading skill check, matching Sim positioning.

If Cesar wants a sanity-check "where will my ball go" hint despite Sim positioning, that's a single subtle dot at the predicted stop position (option (c) flavour) overlaid on the arrow grid — but that's a polish add-on, not the baseline.

---

## What gets built

### Bake step (one-time per hole load)
- Subscribe to `HoleContext.OnHoleLoaded` (or equivalent — verify the exact event name in `Golfin.Course` assembly during SPEC).
- For each Green / GreenCollar surface region detected via `BakedZoneClassifier` or surface scan: sample heightmap on a regular grid (cell size TBD — start with 0.5m, tune if visual density wrong).
- Per cell, compute slope vector (gradient of height field) + magnitude. Store as `Vector2 slope2D, float magnitude` in a per-region array.
- Persist for the lifetime of the loaded hole; rebuild on hole-unload + reload.

### Render step (per frame while Aiming with Putter)
- Subscribe to `ShotModeContext` / `ClubContext` to know when putter aim is active.
- For each baked cell visible to camera + within ~10m aim radius of ball: draw an arrow (direction = slope2D normalized, length/color = magnitude).
- Implementation: single instanced quad/mesh draw per visible cell with arrow texture. NO per-cell GameObjects.
- Color ramp: green (gentle, <2% grade) → yellow (moderate, 2–5%) → red (severe, >5%). Numbers configurable in CSV.

### Removal
- Delete the live trajectory-recomputation predictor logic from `Assets/Scripts/UI/HUD/PuttPathPredictor.cs`.
- Replace with new MonoBehaviour `PutterGreenReader.cs` (or similar — name TBD).
- Update `PhysicsLabController.cs:118` SerializeField reference.
- Putt path predictor is deleted, NOT hidden. Sim positioning means the player chooses; the predicted curve is a crutch we're cutting.

---

## What's still owed before SPEC

Architecture-level questions for Cesar OR Architect-decidable during SPEC:

1. **Cell size for the bake grid.** 0.5m is a starting guess. Architect can decide during SPEC after looking at a sample hole's green area + Figma if green-reading mockups exist.
2. **Arrow asset.** Reuse an existing sprite, ship a placeholder colorblock, or wait for Figma art? Lean: placeholder colorblock for v1, polish ticket later.
3. **Visible-cell culling.** Frustum + distance, or just distance (ball + 10m)? Lean: distance-only — simpler, mobile-perf-friendly.
4. **Heatmap mode (P1 waiver #4).** Cesar's original Putter P1 spec listed a heatmap mode that was never built. Does it survive the redesign as "color the cells by magnitude in addition to drawing arrows"? Lean: yes, free with arrow magnitude already computed.
5. **Bake on main thread (blocking) vs Job System (async).** For a single green region ~30m × 30m at 0.5m cells = ~3600 cells, main-thread should be <50ms. Async is nice-to-have. Lean: main-thread for v1, async if profiling demands.

All five are SPEC-level decisions — not blockers for SPEC kickoff, just to be locked then.

---

## Pointers

- `Assets/Scripts/UI/HUD/PuttPathPredictor.cs` — current MonoBehaviour (to be deleted)
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:118` — `_puttPathPredictor` SerializeField (to be renamed/repointed)
- `BakedZoneClassifier` (Golfin.Course) — surface region source
- `HoleContext` (Golfin.Gameplay.UI.HUD) — hole load/unload events
- Putter P1 spec: `Docs/Specs/Completed/putter_p1_ui/`
- B-followups list: `Docs/TellCode.md` § "B-followups"

---

## Sequencing

Not on Loop v1 critical path. Order 110 in Notion (Phase 01 Putter P1, P1 priority). Earlier suggestion was "after §2f closes" — that's still right, since the redesign touches `PhysicsLabController` and §2f also touches it. Avoid merge conflicts by sequencing.

Estimate: 1–2 days for full redesign + bake step.
