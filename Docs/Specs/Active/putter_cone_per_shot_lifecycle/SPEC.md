# putter_cone_per_shot_lifecycle — Show shot cone between putts, hide on fire

> **STATUS:** Queued (drafted 2026-05-14 by architect chain, surfaced by Cesar Lesson O on `loop_v1_2f_putter_p2_in_context`). **Priority: HIGH — pick up immediately. §2f auto-toggle makes this bug front-and-center.**

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

## Out of scope

- Non-putter cone behavior. It already works correctly per CLAUDE.md's `_coneGraphic` lifecycle; don't touch what isn't broken.
- Putter-specific cone styling/coloring redesign. Reuse existing visuals; this is a visibility fix, not a re-skin.
- Auto-toggle logic itself (§2f, shipped).

## Hard rules

1. Do NOT modify `BallStateMachine.cs`, `BallSimulation.cs`, `ClubButtonWidget.cs` (Hard Rule 1 list). Subscribe to their public events, don't change them.
2. Do NOT break the non-putter cone lifecycle. If you touch `ShotConeView`, gate puttMode-specific logic behind the existing `_puttMode` flag — don't change branches that only apply when `_puttMode == false`.
3. Subscribe/unsubscribe symmetry: every `OnEnable`/`Enter*` subscription has a matching `OnDisable`/`Exit*` unsubscription.
4. Test gate must remain bit-exact pre-existing + N new tests.

## Definition of done

- In putter mode (auto-entered or manually selected), the aim visualization (cone or putter-specific equivalent) is visible at Aiming, hidden during Flying/Rolling, visible again at the next Aiming.
- 4-frame smoke capture sequence shows the cycle clearly.
- 2 new EditMode tests PASS; baseline+2 target met.
- Cesar Lesson O verification: cone behaves like other clubs across multiple putts.

## Estimate

Half-day. Small surface area; the work is mostly subscribing to existing events and gating visibility correctly.

## References

- [PhysicsLabController.cs:384, 417](Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:384) — `EnterPutterMode` / `ExitPutterMode` call `SetPuttMode(true/false)`.
- [ShotConeView.cs:69-70](Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs:69) — the permanent-hide on `_puttMode=true`.
- `Docs/Specs/Completed/loop_v1_2f_putter_p2_in_context/` — Cesar's Lesson O surfaced this.
