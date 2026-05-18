# putter_cone_per_shot_lifecycle — Show shot cone between putts, hide on fire

> **STATUS:** Queued (drafted 2026-05-14 by architect chain, surfaced by Cesar Lesson O on `loop_v1_2f_putter_p2_in_context`). **Priority: HIGH — pick up immediately. §2f auto-toggle makes this bug front-and-center.**

## ⚠️ POST-REVERT ADDENDUM (2026-05-14 14:30 JST)

This spec was written before the `putter_aim_yaw_in_groundlevel` revert (committed 2026-05-14 14:00 JST, see Lesson Q). Two things to know:

1. **Stale line refs.** The revert deleted ~30 lines in `PhysicsLabController.cs`. Line numbers in the References section below are approximate — grep for `EnterPutterMode` / `ExitPutterMode` / `_centralBall.SetPuttMode` to find current locations.
2. **Putter no longer uses `Mode.GroundLevel`.** Putter mode now uses `ChaseCamera.Mode.Chase` for everything (Aiming, Flying, Rolling, AtRest). Visual smoke captures will frame the cone against Chase camera, not the previous low-angle GroundLevel view. The visibility lifecycle the spec tests is identical; only the rendered framing differs.

**HARD RULE (Lesson Q):** This fix MUST NOT re-introduce any putter-specific divergence. The bug fix is "putter follows the same per-shot visibility lifecycle as iron" — NOT "add a special putter visibility branch." If you find yourself writing `if (isPutt) hide_cone(); else default_lifecycle();`, STOP. The correct fix is to make the SetPuttMode(true) path NOT permanently disable `_coneGraphic` — instead, the cone subscribes to the same state transitions both modes use, and the puttMode-specific styling is applied on top (Approach A, locked).

---

## 🛑 SCENE-TRUTH AMENDMENT — APPROACH A REPLACED BY APPROACH C (2026-05-14, post-manual-smoke)

**Status of prior iteration:** Code per Approach A was implemented faithfully and EditMode tests pass. Manual smoke by Cesar in real Play mode (after MCP frozen-time blocker prevented automated capture) revealed Approach A was wrong on its premise. Iteration reverted.

### What Approach A got wrong

Approach A locked the decision "reuse `_coneGraphic` with putt-mode styling — `_coneGraphic` IS the putter aim viz, just restyled." That assumed `_coneGraphic` is the player-visible aim feedback in putter mode. **It is not.**

Scene truth, confirmed by Cesar 2026-05-14:

- **`ConeRoot`** (parent GameObject under the shot Canvas) contains two distinct visual elements:
  - **`_coneGraphic`** (`ConeMeshGraphic` component) — the long iron-style cone mesh. Visually correct *only* for non-putter clubs.
  - **`_clubHandle`** (the GameObject Cesar calls **"ClubHead"** in the Hierarchy) — the club-head proxy that scales/animates with power. Must remain visible during putter aim (the player still sees their putter head animate).
- **`PutterTrack`** (separate scene GameObject, wired as `PhysicsLabController._putterTrack`, has `PutterTrackGraphic` component) — **this** is the actual putter aim viz the player reads. The vertical track on the green.

Today, `PutterTrack` visibility is gated on MODE: `EnterPutterMode` does `_putterTrack.SetActive(true)`, `ExitPutterMode` does `_putterTrack.SetActive(false)`. It never followed per-shot state. That is the bug Lesson O actually surfaced — just on the wrong object name.

### What the player sees today (post-Approach-A iteration)

- **Putter aiming:** `_coneGraphic` (iron cone) visible AND `PutterTrack` visible — they overlap. Bug #1.
- **After fire (putter):** `_coneGraphic` correctly flips off via Approach A subscription. `PutterTrack` stays on because nothing wired it to state. Bug #2.

Both bugs share one root cause: Approach A flipped the wrong object's visibility.

### Approach C (locked 2026-05-14)

1. **Revert Piece 1 in `ShotConeView.cs` to the original visual behavior on `_coneGraphic`:**
   - `SetPuttMode(on)` restores `if (_coneGraphic != null) _coneGraphic.enabled = !on;` — `_coneGraphic` is permanently disabled while in putter mode (matches original code, visually correct).
   - Remove the `UpdateConeVisibility(state)` putter branch added in the prior iteration. (Keep the method only if needed for non-putter; non-putter cone visibility is already managed via `ApplyDebugFlags → SetOutlineVisible`, so `UpdateConeVisibility` can be deleted entirely.)
   - Remove the putter early-return guard in `SetOutlineVisible`.
   - Remove the `if (_coneGraphic != null) _coneGraphic.enabled = true;` line at the end of `SetPuttMode`.
   - `InjectForTests` can stay (harmless, useful for the rewritten tests).

2. **Wire `PutterTrack` to the per-shot lifecycle.** Recommended placement: `ShotConeView`, since it already owns shot-state subscriptions and the `_puttMode` flag.
   - Add `[SerializeField] private GameObject _putterTrack;` to `ShotConeView`. Wire the same scene GameObject `PhysicsLabController._putterTrack` already references — both serialized refs point at the same GO. (Or: refactor to have `PhysicsLabController` hand the reference to `ShotConeView` via a setter on enter/exit. Inspector wire is simpler.)
   - In `HandleStateChanged`, when `_puttMode == true`, set `_putterTrack.SetActive(aiming)` where `aiming = state.State is Idle or Aiming or Pulling or Timing or Flicking`. (Same bool the prior iteration used for cone.) When `_puttMode == false`, do nothing — `PhysicsLabController.ExitPutterMode` already handles the deactivation.
   - `SetPuttMode(false)`: belt-and-suspenders, set `_putterTrack.SetActive(false)` if non-null. Prevents stale-on if mode is exited mid-Resolving.

3. **`PhysicsLabController` is mostly unchanged.** Keep `EnterPutterMode._putterTrack.SetActive(true)` and `ExitPutterMode._putterTrack.SetActive(false)` — they bracket the mode lifetime. The per-shot toggle from `ShotConeView` rides on top and is idempotent.

4. **EditMode tests rewrite (`PutterConeLifecycleTests.cs`):**
   - G1 renamed/replaced: `G1_PutterMode_PutterTrackHiddenOnResolving` — assert `_putterTrack.activeSelf == false` after driving state to `Resolving` in putter mode.
   - G2 renamed/replaced: `G2_PutterMode_PutterTrackVisibleAgainAtNextAiming` — assert `_putterTrack.activeSelf == true` after returning to `Aiming`.
   - **Add G3:** `G3_PutterMode_ConeGraphicStaysDisabledAcrossAllStates` — assert `_coneGraphic.enabled == false` at Aiming, Timing, Resolving, and back to Aiming. The iron cone never re-enables in putter mode.
   - **Add G4:** `G4_NonPutterMode_PutterTrackUntouched` — sanity check that non-putter state changes don't toggle `_putterTrack`.

5. **Smoke evidence requirement updated** (replaces Piece 1 § Smoke evidence point 4):
   - Frame 1 (putter, Aiming): **PutterTrack visible**, iron cone hidden, ClubHead/`_clubHandle` visible.
   - Frame 2 (putter, just fired / Resolving): **PutterTrack hidden**, iron cone hidden, ClubHead visible.
   - Frame 3 (putter, ball rolling / Resolving): same as Frame 2.
   - Frame 4 (putter, next Aiming): same as Frame 1.
   - HUD `BALL:` state label must be readable and match the claimed state in each frame.
   - If MCP frozen-time still blocks, capture via the `GOLFIN/Smoke/Capture PutterCone Lifecycle` menu item in a normal (non-MCP) Play session — Cesar will run it.

### Hard rules added by this amendment

- **NEVER `SetActive(false)` on `ConeRoot` or any parent of `_clubHandle`.** `_clubHandle` (the ClubHead GameObject) must remain visible during putter aim. The cone-hide must be component-level only (`_coneGraphic.enabled = false`). Disabling the parent GameObject would hide the ClubHead and is wrong.
- **Lesson Q's HARD RULE is superseded by this amendment for the putter case.** Approach C *is* a putter-specific visibility branch — `if (_puttMode) PutterTrack.SetActive(aiming); else /* non-putter path unchanged */;`. That's correct, because putter and non-putter mode use different aim viz GameObjects in the scene; making them share one code path was the spec error. The Lesson Q ban on "putter-specific divergence" still applies to *physics/camera* behavior, not to visibility wiring of different scene objects.
- Piece 2 (central ball sprite size parity) is unaffected by this amendment. That fix is correct, keep it as-is.

### References added

- [PutterTrackGraphic.cs](Assets/Scripts/Gameplay/UI/ShotUI/PutterTrackGraphic.cs) — the actual putter aim viz component.
- [PhysicsLabController.cs:354](Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:354) — `_putterTrack` serialized GameObject ref.
- [PhysicsLabController.cs:384, 406](Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:384) — `_putterTrack.SetActive(true/false)` on mode enter/exit. Keep these; per-shot toggle from `ShotConeView` rides on top.

### Iteration trail

- **Iter 1 (Approach A):** 8 code PASS / 4 smoke FAIL → SELF_REVIEW_FAIL (smoke proved cone never hid) → IMPLEMENTER_BLOCKED (MCP frozen-time).
- **Cesar manual smoke 2026-05-14:** revealed Approach A premise wrong. Two visual bugs surfaced. Approach C replaces Approach A.
- **Iter 2 (Approach C):** to be done. STATUS → SPEC_READY.

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
