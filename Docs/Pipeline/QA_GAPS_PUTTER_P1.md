# QA Gap Analysis — putter_p1_ui

Four issues caught by Cesar's naked-eye review that the automated checklist missed.
Documented here so the architect can tighten the spec template and implementer checklist.

---

## Gap 1 — Cone root still visible in putter mode

**What was missed:** `ShotConeView.SetPuttMode(true)` disables `_coneGraphic.enabled`,
but `ApplyDebugFlags()` runs on every `OnStateChanged` tick and calls
`SetOutlineVisible(_shotController.DebugFlags.ShowConeOutline)`. `SetOutlineVisible`
re-enables the graphic unconditionally, overriding the putter-mode setting.
The checklist item ("cone graphic hidden") was verified by **code inspection only**
(static idle-state screenshot, no active aiming). The cone is invisible at rest but
reappears the moment the player starts a pull gesture.

**Checklist fix:** Any "X hidden in putter mode" item must require an **active-aiming
screenshot** (touch held, power gauge > 0%). Idle-state screenshots are insufficient
for visibility checks on conditionally-shown UI.

---

## Gap 2 — PutterTrack top not aligned with ball center

**What was missed:** The spec gave a hardcoded `anchoredPosition.y = -1453`. The
implementer verified "YAML matches spec value" — it did. The spec value was derived
without checking the live canvas coordinate of the ball widget. On the actual device
resolution the track starts ~100–200px below the ball center.

**Checklist fix:** Any task that positions a widget relative to another widget must
include a checklist item: *"Widget origin visually aligns with the reference element
at runtime (screenshot zoomed in)."* Hardcoded pixel offsets from spec should be
treated as estimates, not ground truth. Preferred implementation is always dynamic
(anchor relative to the reference widget's RectTransform), with a hardcoded fallback
only as a last resort.

---

## Gap 3 — Club selector card still shows "yrds" in putter mode

**What was missed:** The spec listed `HoleIndicatorWidget` and `PowerGaugeWidget` as
the distance-unit targets. `ClubButtonWidget` (the bottom-right club card) also
displays a distance string but was not in the spec's change list. The implementer
only touched widgets explicitly named in the spec.

**Checklist fix:** Any task that changes displayed units must include a mandatory
**"distance unit audit"** item: *"Every widget on screen that displays a distance
value has been identified and updated."* The architect's spec must enumerate ALL such
widgets, not just the ones driving the change.

---

## Gap 4 — PuttPathRoot resets between shots and path direction looks wrong

**What was missed:** Two sub-issues, both marked FAIL by the implementer as
"requires active aiming at runtime" and never verified:

- **Between-shot reset:** `_hasCache` is not cleared when state transitions to
  `Idle/Resolving`. On the second aim, the cached aim/power values are compared and
  the delta may be below threshold, so `Predict()` is never called again.

- **Path direction:** Verified only by code review; the world-to-canvas projection
  was never run against actual trajectory data. A bug in how `AimYawRadians` maps to
  the camera view direction would produce a visually wrong path that only shows up at
  runtime during an active putt.

**Checklist fix:** Predicted-path items must not be accepted on code review alone.
The spec should require a specific capture step: *enter putter mode, initiate a pull
gesture to ~50% power, hold, capture screenshot*. If an active-aiming screenshot
cannot be taken, the item must be escalated to READY_FOR_ARCHITECT_REVIEW — never
silently marked FAIL and let through to self-review or architect-review as acceptable.

---

## Gap 5 — PuttPathRoot originates from 3D ball, not 2D ball sprite center

**Status: PENDING**

**What was observed:** The putt path line starts from the 3D ball's world position projected onto the canvas, not from the center of the 2D ball sprite (`CentralBallWidget`) shown on screen. This causes a visible offset between where the path visually appears to begin and the ball graphic.

**Root cause:** `PuttPathPredictor.AlignPuttPathRoot()` uses `WorldToScreenPoint` on the 3D ball transform. The 3D ball is not always centered in the Game View camera frame; the 2D sprite is the visual representation but the 3D ball can be off-center depending on camera angle.

**Expected fix:** When the camera is corrected so the 3D ball is always centered, this misalignment will resolve automatically. No immediate code change required.

**Checklist fix:** Any path/trail that originates from a "ball" position must include a checklist item: *"Path/trail origin aligns visually with the 2D ball sprite center (zoomed screenshot at runtime)."*

---

## Summary table

| Gap | Root cause | Checklist fix |
|---|---|---|
| Cone reappears | `SetOutlineVisible` ignores `_puttMode` | Require active-aiming screenshot for all "hidden" checks |
| Track offset | Hardcoded pixel value from spec not verified at runtime | Require zoomed alignment screenshot; prefer dynamic anchoring |
| Club card unit | Spec missed one distance-displaying widget | Mandatory distance-unit audit enumerating all visible widgets |
| Path predictor | Two runtime-only bugs accepted on code review alone | Active-aiming screenshot required; no FAIL passthrough on path items |
| PuttPathRoot origin offset | 3D ball world position ≠ 2D sprite center | Require zoomed origin-alignment screenshot; deferred until camera fix |
