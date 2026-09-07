# golfer_3d_test — kickoff for the PC session

Written 2026-09-07 from the Mac session. The Mac has released Unity; the active build profile is
back on `iOS-Full-GPS`, no stash, no dirty scenes.

## Read this first: the headline finding invalidates most of the last two days of grip work

**In real gameplay the golfer never enters Address.** He stands bolt upright, arms at his sides,
back to camera, with the club dangling from his right hand and the club head nowhere near the ball.

Proof is a real play-mode frame on Hole 06, captured by the §6 harness at the moment it calls
"address". `Docs/Specs/**/screenshots/` is gitignored, so the frames from the Mac session did not
travel — **regenerate them yourself**, which takes about a minute and is better evidence anyway:
run `GOLFIN > Golfer Test > Verify on Hole 06` and open the newest
`Docs/Diagnostics/_capture/golfer_h06_address_*.png`. You should see the golfer standing upright,
back to camera, arms at his sides, club dangling. That is the bug.

Everything the grip solve does is layered on top of a golfer standing idle, so no amount of grip
work can show up until this is fixed. **Fix Address first. Do not touch the grip until it is.**

### The trap that hid it

`shot.addressBeforeSwing` **PASSES** in `golfer_invariants.json` while the render clearly shows
Idle. That assertion is lying — it records animator states it *saw* during setup rather than the
state that is live when the frame is drawn. Treat it as broken; fix it to assert the live state,
and distrust any other assertion that has never been checked against a render.

### Where to start

`GolferPresenter.HandleShotState` is edge-triggered off `ShotInputState`:

```
if (s.State == _lastShotState) return;
if (s.State != ShotState.Idle) { anim.ResetTrigger(PCancel);  anim.SetTrigger(PAddress); }
else                           { anim.ResetTrigger(PAddress); anim.SetTrigger(PCancel);  }
```

Open questions, in order:
1. Does `OnStateChanged` ever publish a non-Idle state in a real round? Log `s.State` transitions.
2. If it does, is the Address trigger consumed by a transition that immediately exits again?
3. Is `_lastShotState` initialised such that the first real transition is swallowed?

Note the `Address_Drive` / `Address_Putt` states were just given a `cycleOffset` (0.0409 / 0.0891)
so that holding them at speed 0 shows the ADDRESS frame rather than frame 0 — frame 0 of the swing
clip is the actor standing upright before he sets up, which would have produced this same symptom
independently. That change is correct and should be kept; it simply is not sufficient on its own.

## Second unresolved bug

`ApplyGripPose()` is called first thing in `LateUpdate`, but **it does not take effect at the point
the harness samples**. Evidence: three consecutive harness runs reported grip numbers identical to
four decimal places across substantial solver rewrites. Adding a temporary probe that invoked
`ApplyGripPose()` explicitly immediately before measuring moved `grip.fingersClosed` from 0.0873
(straight, FAIL) to 0.0533 (PASS). The probe has been removed so the harness measures reality.

Unknown whether this is (a) the solve genuinely not running in play, or (b) the harness sampling in
the Update phase and seeing a stale pose. Settle it before drawing conclusions from the JSON.

## What DOES work, verified

In edit mode, posed at the address frame (t=0.24s of `ANIM_Golf_Drive`), with `ApplyGripPose()`
called explicitly, the contact solve produces a real grip — verified numerically AND on the render:

| measure | before solve | after solve |
|---|---|---|
| trail-hand worst fingertip from shaft axis | 0.0597 m | **0.0323 m** |
| lead-hand worst fingertip from shaft axis  | 0.1277 m | **0.0300 m** |
| lead fist off the shaft axis               | 0.0580 m | **0.0062 m** |
| thumbs vs shaft axis                       | 62 / 68 deg | **0 / 0 deg** |

## Dead ends — do NOT redo these

- **CMU mocap** (`_Test/CMU/`, 31 clips). All retarget to a golfer standing upright with the club
  dangling; none is a golf address. Kept only as evidence.
- **Mixamo prop animations** (`_Test/Grip/`, 3 clips: baseball at-bat, baseball variation,
  greatsword). All the same loosely-closed fist as the golf clips. Fingertip-to-palm 0.136 / 0.148 /
  0.117 m against golf's 0.142, where a closed fist is ~0.04. Sourcing a grip does not work.
- **Palm-normal curl direction.** The normal is built from an index-to-pinky vector so it flips with
  handedness; using it wrecked the working case (worst fingertip 0.0323 -> 0.0843 m).
- **"Always make a fist" when contact is unreachable.** A finger can be a centimetre short of a
  shaft it is properly wrapped around; this took the right pinky 0.0323 -> 0.1073 m.
- **Overlapping the hands** (`LeadHandOverlap` 0.82 to close the seam between fists). Lead fist went
  0.0062 -> 0.0257 m off the shaft and the mesh sheared. Structural: `JoinLeadHandToShaft` swings one
  bone, so it matches a target's DIRECTION but not its distance. Closing that seam needs two-bone IK.

## The rendering trap that cost this session most

**Edit-mode renders do not re-skin.** The bones animate, the mesh draws its rest pose, and every
close-up lies. Any harness that renders a posed character in edit mode MUST set, on every
SkinnedMeshRenderer:

```csharp
sk.forceMatrixRecalculationPerRender = true;
sk.updateWhenOffscreen = true;
```

Several conclusions earlier in this task were drawn from unskinned frames and were wrong.

## Still open, lower priority

- `budget.tris` FAILS: 15,632 vs 15,000 limit (SuperHero_Male 12,566 + ClubHead 1,058 + Eyebrows 984
  + Eyes 768 + Grip 192 + Shaft 64).
- Putter worst fingertip 0.0429 m against a 0.042 gate — marginal fail.
- The worst finger is the pinky in every measurement. In a real Vardon/overlap grip the TRAIL pinky
  rides on the lead index and is legitimately off the club, so `grip.wrapped_r` may be asserting
  something anatomically wrong. Decide before tuning to satisfy it.
- `shot.cancel` (cancel-to-idle) is wired but never exercised.
- Three real iOS builds for the §6 gate proof still not run (`./Tools/unity-build-ios.sh golfer`,
  no-arg, `gps`) — needs the Editor closed.

## Environment

- Working on this feature requires the `iOS-Full-Golfer` build profile (that is what supplies
  `GOLFIN_GOLFER_TEST`). **Switch back to `iOS-Full-GPS` when finished.**
- Compile-check without touching the Editor: Unity's own Roslyn against the generated `.csproj`.
  See the `reference_compile_check_without_unity` memory. Check BOTH define states — the whole
  point of this feature is that it vanishes when the define is off.
- The harness is `GOLFIN > Golfer Test > Verify on Hole 06`; it writes
  `Docs/Diagnostics/_capture/golfer_invariants.json` and a set of `golfer_h06_*.png` frames.
  **Look at the PNGs. The JSON has already been caught passing on a frame that was visibly wrong.**
