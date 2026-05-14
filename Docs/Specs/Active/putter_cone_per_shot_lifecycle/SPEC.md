# putter_cone_per_shot_lifecycle — Show shot cone between putts, hide on fire

> **STATUS:** Queued (drafted 2026-05-14 by architect chain, surfaced by Cesar Lesson O on `loop_v1_2f_putter_p2_in_context`). **Priority: HIGH — pick up immediately. §2f auto-toggle makes this bug front-and-center.**

## ⚠️ POST-REVERT ADDENDUM (2026-05-14 14:30 JST)

This spec was written before the `putter_aim_yaw_in_groundlevel` revert (committed 2026-05-14 14:00 JST, see Lesson Q). Two things to know:

1. **Stale line refs.** The revert deleted ~30 lines in `PhysicsLabController.cs`. Line numbers in the References section below are approximate — grep for `EnterPutterMode` / `ExitPutterMode` / `_centralBall.SetPuttMode` to find current locations.
2. **Putter no longer uses `Mode.GroundLevel`.** Putter mode now uses `ChaseCamera.Mode.Chase` for everything (Aiming, Flying, Rolling, AtRest). Visual smoke captures will frame the cone against Chase camera, not the previous low-angle GroundLevel view. The visibility lifecycle the spec tests is identical; only the rendered framing differs.

**HARD RULE (Lesson Q):** This fix MUST NOT re-introduce any putter-specific divergence. The bug fix is "putter follows the same per-shot visibility lifecycle as iron" — NOT "add a special putter visibility branch." If you find yourself writing `if (isPutt) hide_cone(); else default_lifecycle();`, STOP. The correct fix is to make the SetPuttMode(true) path NOT permanently disable `_coneGraphic` — instead, the cone subscribes to the same state transitions both modes use, and the puttMode-specific styling is applied on top (Approach A, locked).

---

## One-line

In putter mode, the shooting cone (or putter-equivalent aim visualization) should follow the same per-shot lifecycle as every other club: visible while aiming, hidden the moment the shot fires, visible again once the ball comes to rest and the player is aiming the next shot. Today the cone is permanently hidden the moment putter mode is entered and stays hidden for every subsequent putt.

## Cesar's observation (Lesson O, 2026-05-14)

> "Shooting cone with putter should disappear once the shot is made and reappear on next shot, like in any other shot."

The visual feedback loop the player has come to expect from non-putter shots (cone visible at aim, hidden during flight/roll, reappears at rest) is missing for putts. Players need that same "now you're aiming, now you've committed" UI rhythm in putter mode too.

## Root cause

[PhysicsLabController.cs:384](Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:384) — `EnterPutterMode` calls:

```csharp
if (_shotConeView != null) _shotConeView.SetPuttMode(true);
```

…which in [ShotConeView.cs:69-70](Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs:69):

```csharp
if (_coneGraphic  != null) _coneGraphic.enabled = !on;          // disabled while puttMode
if (_targetingLine != null) _targetingLine.gameObject.SetActive(!on);
```

…permanently disables `_coneGraphic` and `_targetingLine` for the duration of putter mode. The hide is gated on mode, not on shot state. There's no analog to the per-shot show/hide that non-putter mode gets via subscriptions to `ShotController.OnStateChanged`.

§2f doesn't touch `ShotConeView` (it's a Hard Rule 1 protected file). The fix lives in a follow-up spec.

## Scope

### Piece 1: per-shot lifecycle (original)

1. **Add a per-shot show/hide cycle inside puttMode.** Putt mode keeps its specialized visualization (puttTrack / putter-specific cone variant) but follows the same lifecycle: visible at Aiming, hidden on fire (`Aiming → Flying` or equivalent), visible again at `Resolving → Aiming`.
2. **Decide what "the cone" means in putter mode.** Options:
   - A: re-use the same `_coneGraphic` but parameterize it for short-distance putts (different size/colors).
   - B: introduce a putter-specific aim visualization (e.g., `PutterTrack` already mentioned in §2f SPEC) and apply the same show/hide cycle to it.
   - **ARCHITECT-LOCKED 2026-05-14 09:30 JST: Approach A.** Reuse `_coneGraphic` with putt-mode styling parameters (smaller scale, putt-distinct color). DO NOT introduce a separate visualization component — Approach B's "PutterTrack" risks coupling to `PuttPathPredictor` which is slated for deletion under Order 110 redesign. Approach A is the minimum-coupling fix.
3. **Wire `ShotController.OnStateChanged` (or `BallStateMachine.OnStateChanged`) subscription** in either `ShotConeView` or a new `PutterAimVisibility` controller so the show/hide tracks ball state. Idempotent — entering puttMode subscribes; exiting unsubscribes.
4. **Smoke evidence:** capture 4 frames in a single putt sequence:
   - Frame 1: aiming a putt — visualization visible.
   - Frame 2: just after fire — visualization hidden.
   - Frame 3: ball rolling — visualization still hidden.
   - Frame 4: ball at rest, next shot ready — visualization visible again.
5. **Tests:** 2 EditMode tests asserting visibility flips on `OnStateChanged` for both Aiming↔Flying transitions in putter mode.

### Piece 2: central ball sprite size parity (added 2026-05-14 10:00 JST, narrative corrected 10:30 JST)

**Bug:** `CentralBallWidget._normalSize = 80f` (code default) and the matching scene Inspector value of `80` are wrong. Cesar's manually-authored RectTransform `m_SizeDelta = 150` on the CentralBallWidget GO in `LabScaffold.unity` IS the correct size for both regular and putter mode shots. The bug surfaces whenever `SetPuttMode(false)` fires — it writes `sizeDelta = _normalSize = 80`, overriding Cesar's authored 150. Cesar observed this happening after teleport-then-shot scenarios via LabCanvas, but the trigger is just "any code path that calls `SetPuttMode(false)`" — i.e. `ExitPutterMode` (whether triggered by manual club switch or §2f auto-exit from putter). Verified via scene YAML: `m_SizeDelta: {x: 150, y: 150}` is authored, `_normalSize: 80` is what overrides it at runtime.

**Architect-locked fix:**
6. **Set `_normalSize = 150f` and `_puttModeSize = 150f`** in both the `CentralBallWidget.cs` field defaults AND the `LabScaffold.unity` Inspector values for the `CentralBallWidget` MonoBehaviour instance. Both fields stay as separate `[SerializeField]`. Do NOT collapse to a single field — Cesar wants the option to diverge later. The mode-based resize becomes a no-op for current values; that's intentional.
7. **Verify scene change via Unity Editor MCP** (`gameobject-component-modify`), NOT raw YAML (controls_g lesson). The RectTransform `m_SizeDelta` value must remain `150,150` (do NOT touch it). Only the two `[SerializeField]` floats on the MonoBehaviour are changing.
8. **Smoke capture for parity:** capture three frames in the per-shot lifecycle: (a) regular-shot ball at Aiming, (b) putter-mode ball at Aiming, (c) regular-mode ball at Aiming AGAIN after a putter→regular auto-switch (triggers `SetPuttMode(false)`). All three frames must show the central ball at identical visible size (150×150). Frame (c) is the regression-proof capture.
9. **No new tests required for Piece 2** — it's a data-only change. The existing visibility tests from Piece 1 will exercise both code paths.

## Out of scope

- Non-putter cone behavior. It already works correctly per CLAUDE.md's `_coneGraphic` lifecycle; don't touch what isn't broken.
- Putter-specific cone styling/coloring redesign. Reuse existing visuals; this is a visibility fix, not a re-skin.
- Auto-toggle logic itself (§2f, shipped).
- **Refactoring `_normalSize` + `_puttModeSize` into a single field.** Keep them separate per Cesar's directive 2026-05-14.
- **Renaming `_normalSize` to clarify it's the non-putter-mode size.** Naming clarity is a future polish; this spec is the size-parity fix.

## Hard rules

1. Do NOT modify `BallStateMachine.cs`, `BallSimulation.cs`, `ClubButtonWidget.cs` (Hard Rule 1 list). Subscribe to their public events, don't change them.
2. Do NOT break the non-putter cone lifecycle. If you touch `ShotConeView`, gate puttMode-specific logic behind the existing `_puttMode` flag — don't change branches that only apply when `_puttMode == false`.
3. Subscribe/unsubscribe symmetry: every `OnEnable`/`Enter*` subscription has a matching `OnDisable`/`Exit*` unsubscription.
4. Test gate must remain bit-exact pre-existing + N new tests.

## Definition of done

- In putter mode (auto-entered or manually selected), the aim visualization (cone or putter-specific equivalent) is visible at Aiming, hidden during Flying/Rolling, visible again at the next Aiming.
- 4-frame smoke capture sequence shows the cycle clearly.
- 2 new EditMode tests PASS; baseline+2 target met.
- **Central ball sprite renders at identical visible size (150×150) in both regular and putter modes** — 5th smoke frame proves parity.
- **`CentralBallWidget.cs` field defaults updated to `_normalSize = 150f, _puttModeSize = 150f`** AND `LabScaffold.unity` Inspector values match (verified via Unity Editor MCP).
- Cesar Lesson O verification: cone behaves like other clubs across multiple putts AND ball stays the same size when crossing the auto-switch boundary.

## Estimate

Half-day to 1 day (was half-day pre-amendment). Piece 1 mostly subscribing to existing events + gating visibility. Piece 2 is a 2-field Inspector tweak + smoke capture parity proof.

## References

- [PhysicsLabController.cs:384, 417](Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:384) — `EnterPutterMode` / `ExitPutterMode` call `SetPuttMode(true/false)`.
- [PhysicsLabController.cs:405, 433](Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:405) — `_centralBall.SetPuttMode(true/false)` (Piece 2 call sites).
- [ShotConeView.cs:69-70](Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs:69) — the permanent-hide on `_puttMode=true` (Piece 1 bug location).
- [CentralBallWidget.cs:27-29, 41-46](Assets/Scripts/Gameplay/UI/ShotUI/CentralBallWidget.cs:27) — `_normalSize` / `_puttModeSize` fields + `SetPuttMode` (Piece 2 fix location).
- [LabScaffold.unity:26429-26435](Assets/Scenes/Physics/LabScaffold.unity:26429) — scene Inspector values for the `CentralBallWidget` MonoBehaviour instance.
- `Docs/Specs/Completed/loop_v1_2f_putter_p2_in_context/` — Cesar's Lesson O surfaced both pieces.
