# golfer_3d_test — sit rep for Architect

**From:** PC session (Claude Code), 2026-09-07
**Commit:** `8ec83e9ef` — *golfer_3d_test: fix Address — he never stood over the ball in a real round* (pushed to `main`)
**STATUS.md:** still `IMPLEMENTER_BLOCKED` — **awaiting Cesar's call**, see § Decision needed
**Verification:** `golfer_invariants.json` — **35 pass / 1 fail** (was 30 / 5), re-confirmed after rebasing onto `392539899` (control schemes)

---

## Headline

The blocker `KICKOFF_PC.md` was written around is **fixed and proven on real play-mode renders**,
not on the JSON. It was **two independent defects**, not one. Fixing only the first left the bug
fully intact from the second shot onward, which is why it survived a session of work.

The kickoff's prediction held exactly: **all four grip failures cleared with zero changes to the
grip solve.** Everything the contact solve does was being layered on a golfer standing idle.

| assertion | before | after |
|---|---|---|
| `grip.fingersClosed`  | FAIL 0.0873 m | **PASS 0.0276 m** |
| `grip.leadHandOnGrip` | FAIL −0.0036 m | **PASS 0.0531 m** |
| `grip.wrapped_r`      | FAIL 0.0573 m | **PASS 0.0324 m** |
| `grip.wrapped_l`      | FAIL 0.5724 m | **PASS 0.0309 m** |

### Evidence

- `screenshots/address_BEFORE.png` — Hole 06, real play mode. Upright, back to camera, arms at his
  sides, club dangling, head nowhere near the ball. The reported defect, reproduced first thing.
- `screenshots/address_AFTER.png` — same frame, same harness. Bent at the hips, both hands on the
  grip, shaft down to the ball.
- `screenshots/atrest_AFTER.png` — Turn 2, ball at rest, re-armed. **This is the frame that needed
  the second fix**; after cause 1 alone it still showed the upright pose.

---

## Cause 1 — address was keyed on the wrong signal

`GolferPresenter.HandleShotState` drove Address off `ShotInputState`: *"anything but
`ShotState.Idle` means address."*

`ShotController.State` is `Idle` **whenever the player is not physically touching the screen** —
`Aiming` does not begin until `justTouched` (`ShotController.Tick`, `case ShotState.Idle`, line 544).
So the entire window in which a golfer stands at address, lining the shot up, is a window in which
`ShotState` is `Idle` and the presenter was firing `Cancel`. The only moments he addressed were the
fraction of a second the finger was down, on the way into the swing.

**This is also the whole explanation for the lying assertion.** `shot.addressBeforeSwing` PASSed on
a render that plainly showed Idle because it samples animator states seen *while the harness drives
a synthetic drag* — the one time the old rule happened to be true. It was never a render check.

**Fix:** address now derives from `BallState.Aiming`, whose own definition in `BallState.cs` is
*"no shot in flight; player can input"* — the exact condition. One idempotent `RefreshStance()` that
every handler funnels into, applied once at bind because the state machine starts in `Aiming` and
fires no event for its initial state.

## Cause 2 — a stale `Reset` trigger, armed and waiting

`Reset` is an **AnyState → Idle** transition with `m_CanTransitionToSelf: 0`
(`AnimatorController_Golfer.controller`, transition `-4687611814050963985`).

`HandleShotComplete` sets it while the animator is **already in Idle**, so the any-state transition
is not taken (it would be Idle→Idle) and **the trigger is not consumed**. It sits armed. `Address`
then moves him to `Address_Drive` — and on the very next frame the still-pending `Reset` is finally
valid and drags him straight back to Idle.

Found by tracing animator state changes per frame: the log read `-> Address_Drive` immediately
followed by `-> Idle`, with nothing in the presenter having asked for it.

**Fix:** `RefreshStance` disarms **every** competing trigger before setting the one it wants, not
just the opposite one.

## Not a bug: "`ApplyGripPose()` does not take effect"

The kickoff's second unresolved item. It isn't real. The grip numbers were identical to four decimal
places across substantial solver rewrites **because the golfer was in Idle**, where the lead arm
hangs at his side and there is no grip to measure. The temporary probe that "fixed" it worked
because it forced a pose, not because it changed sampling phase. Nothing to chase.

---

## Harness hardened against the trap that hid all of this

The pipeline's own lesson here is that *no assertion was checking the thing the picture shows*.

- **NEW `shot.addressAtRest`** — asserts the **live** animator state on the frame the canonical
  address PNG is captured, with no shot in progress. This is the gate that matches the render.
- **`shot.backToIdle` → `shot.addressAfterShot`.** The old one accepted `"Idle" OR "Address"`, which
  is precisely the looseness that let the defect through: it reported PASS on a frame showing the
  reported bug one shot later. The replacement **requires** an Address state once the ball has
  re-armed, after a bounded 3 s wait.
- **`shot.addressBeforeSwing` kept**, but its detail string now states plainly that it samples
  across the drag and is **not** a render check, so it can't be misread again.

---

## Still open

| item | note |
|---|---|
| `budget.tris` **FAIL** | 15,632 vs 15,000. SuperHero_Male 12,566 + ClubHead 1,058 + Eyebrows 984 + Eyes 768 + Grip 192 + Shaft 64. The only remaining failure. |
| Putter worst fingertip | 0.0429 m against a 0.042 gate — marginal. Not re-measured since the fix; the Hole 06 run is driver. |
| `grip.wrapped_r` semantics | Kickoff's open question stands: in a real Vardon grip the trail pinky rides on the lead index and is legitimately off the club, so this may be asserting something anatomically wrong. Decide before tuning to satisfy it. Currently PASSes anyway. |
| `shot.cancel` | Wired, still never exercised. |
| Three iOS builds | §6 gate proof still not run; needs the Editor closed. |
| **Golfer has no clothes** | Skin and underwear in every frame. Outside this task's scope and unchecked by any invariant, but it is the first thing anyone sees. |

## Decision needed

`STATUS.md` is `IMPLEMENTER_BLOCKED` and per the pipeline only Cesar unblocks it. The stated
blocker is gone. Recommendation: **back to `golfin-implementer`** for the tri budget, the marginal
putter fingertip, and the three iOS builds.

Build profile is left on **`iOS-Full-Golfer`** (that is what supplies `GOLFIN_GOLFER_TEST`) because
work continues. It must go back to **`iOS-Full-GPS`** before anything ships.

## Files changed

| file | change |
|---|---|
| `Assets/Scripts/Gameplay/Golfer/GolferPresenter.cs` | Address derived from `BallState.Aiming` via idempotent `RefreshStance()`; disarms every competing trigger; subscribes to `BallSM.OnStateChanged`; `_lastShotState` removed |
| `Assets/Scripts/UI/Editor/GolferTestVerificationRecorder.cs` | Added `shot.addressAtRest`; `shot.backToIdle` → strict `shot.addressAfterShot`; `shot.addressBeforeSwing` detail clarified |
| `Docs/Specs/Active/golfer_3d_test/HEARTBEAT.log` | Full root-cause writeup |
| `Docs/Specs/Active/golfer_3d_test/golfer_invariants.json` | Refreshed, 35 / 1 |
