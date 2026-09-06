# ARCHITECT_REVIEW — `control_scheme_seam`

**Verdict:** PASS — recommend Cesar approves and the folder moves to `Docs/Specs/Completed/`.
**Reviewed:** 2026-09-05 (Architect, Cowork) against commit `8913901a7` and IMPLEMENTER_REPORT.md.

## Verified in the codebase (not taken from the report)
- `ShotController.cs`: `ResolveAndPublish` (l.696) is the single tail; `CommitFlick` (l.634) and `CommitExternal` (l.650) both call it; `BeginExternalDrag()` / `BeginExternalDrag(bool)` overload pair (l.359/366); `_ownsTiming` gates `TickArrow` only on the external-drag path (l.466) and resets in `TransitionToIdle` (l.552).
- `GameSession.AppendShotTimingKeys(payload, shot, schemeId = 0)` writes `payload["scheme"]` (l.276–284).
- `ShotControllerSeamParityTests`: 11 `[Test]`s, raw `fp` equality.
- Commit is scoped to the task; the `map_view_v2` drift listed in report §7 is correctly excluded.

## The four deviations — all accepted
1. `fadeDrawMaxTiltRad` as a 7th tail parameter — correct; the spec's shorter signature would have changed the mode-gated tilt. Spec text was wrong.
2. `PublishShotSfx()` in the callers — correct; matches the spec's own `CommitExternal` pseudocode and preserves side-effect order.
3. Overload pair instead of a default argument — correct and non-obvious (reflective `Type.EmptyTypes` lookups in four bots). Keep this as a standing note for any future public `ShotController` method.
4. `ControlSchemeService` in `Golfin.Gameplay.UI`, `scheme` as an `AppendShotTimingKeys` parameter — both forced by asmdef visibility and the `Assets/Scripts/Physics/` zero-edit ban. The spec flagged the first; the second is the right call.

## Things the review chain would have flagged, so noting them here
- **Design-of-record change (Cesar, 2026-09-05):** Settings › Controls selects with the blue row fill like Language/Graphics, NOT the radio button drawn in Figma `14089:101926`. The Figma frame is now stale; updated by the Architect in the same session (see plan §8). Rule for every future Settings-screen frame: match the live submenu component, then the node.
- Linear-space blending: the pre-composited segment sprites are correct but are pinned to the card's `(11,32,58)` — if the modal card sprite is ever re-skinned, `make_controls_segment.py` must be re-run. Recorded in `UI_ELEMENT_PALETTE.md`? → NOT yet; add a row when the next palette pass happens.
- `telemetryData` dashboard test not written (`server-only` import) — acceptable; extract to a pure module if the scheme filter ever gets logic beyond bucketing.

## Outstanding before Completed
- On-device pass (1170×2532 + 16:9) of the two Settings surfaces — Cesar.
- Nothing else. The three scheme specs (`scheme_pendulum` → `scheme_needle` → `scheme_freeswing`) build on this seam unchanged.
