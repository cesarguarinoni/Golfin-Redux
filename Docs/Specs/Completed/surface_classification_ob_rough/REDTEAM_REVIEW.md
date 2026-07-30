# RED-TEAM REVIEW — `surface_classification_ob_rough` (REDO / iter-2 camera fix)

**Verdict: ARCHITECT_REVIEW_PASS** · 2026-07-29 19:05 JST

Scope of this pass (per orchestrator): attack the iter-2 OB-camera fix ONLY. The
classifier + Stage-2 clips already survived the prior 5-attack red-team and were
re-derived from source; not re-litigated here. Miss #3 (2026-07-29) is the
scar this gate exists to prevent from recurring: the whole chain judged a settle
STILL, not the camera MOTION, and Cesar caught an under-terrain clamp on sight.

## Evidence I generated myself

- Dense frame walk of `videos/stage1_ob_after_iter2_fixed.mp4` (12.0s, 320 frames,
  1170×2532): 4fps montage + full-res extracts at t=0.5/1.5/2.5/3.0/3.75/4.5/6/8/10/11.5.
- Full-res montage of the OLD rejected clip `videos/stage1_ob_after.mp4` for A/B.
- Full diff of `LoopCameraDirector.cs` + `LoopCameraDirectorTests.cs`; caller grep
  of `ComputeOBFreezePivot` / `ResetToOrigin`; Physics/ diff namelist.

## Attack 1 — Motion, not a still (the miss-#3 shape)

**Rejected defect: GONE.** The NEW settle (t=4.5 through t=11.5) is a clean,
STABLE aerial overhead of Hole 6 — fairway greens, cart path, water hazard,
bunker, tree framing all visible; camera clearly above ground at ~27° pitch. t=4.5
and t=11.5 are compositionally identical → the camera is settled, not
bouncing/recoiling. Contrast the OLD clip's settle (old_montage rows 3–6, and
`CESAR_REJECT_..._final.jpg`): flat featureless skirt filling the lower ~40% with
the real course jammed at the mid-frame horizon and floating tree-shadows. That
signature is absent from every new settle frame.

**The one thing that nearly failed it — and why it didn't.** New-clip transition
frames t≈2.5–3.75 (~1.5s) show a near-ground grazing view: ~90% of the frame is a
flat, featureless green plane with the horizon crammed into the top ~5% and a
giant flagstick spanning the frame. On first read this looked WORSE than the
rejected defect. I A/B'd the exact same timestamps in the OLD rejected clip
(`old_montage` row 2) — they are **identical**. These grazing frames are a
pre-existing flight/transition artifact (ball skimming low over the green/skirt on
the way to the boundary with the chase cam), present in the clip Cesar rejected,
NOT introduced by this fix, and NOT the defect he flagged (he cited the settle at
t≈10/12). Camera is ABOVE the plane looking across it (top surface recedes to a
horizon; no backface/void/z-fight) → not an under-terrain sink. Not a regression,
not a settle sink → out of scope for this iteration. Surfaced below for Cesar.

## Attack 2 — Does the fix generalize or is it fit to one shot?

- **≥40m branch** (this shot: hit at X≈182, origin X≈80 → ~102m): fires → pivot at
  midpoint XZ, `Terrain.SampleHeight(mid)+25m`. Because Y is sampled at the
  midpoint +25m, the pivot is always 25m above local terrain — it cannot clip the
  terrain it sits over. Confirmed by the clean stable settle.
- **~40m boundary:** at 40m, midpoint is 20m from origin +25m up → ~51° pitch;
  just-below → old `hit+5m` path. Discontinuity exists but both sides are
  individually sane; no shot in this scenario sits on the seam.
- **<40m near-tee OOB (UNCHANGED path):** still returns `hitPos + 5m`, which can be
  shallow. BUT (a) it is the ORIGINAL pre-existing behavior, (b) it now ALSO
  receives the new `ResetToOrigin` velocity-zero (kills the bounce component for
  ALL OB entries), (c) it was never the rejected defect — Cesar rejected the long
  out-of-grid overshoot, which is fixed, and (d) the report documents it explicitly
  as unchanged/acceptable. Not a blocker; noted.

## Attack 3 — Regression from touching the viewer

- `ComputeOBFreezePivot` has exactly ONE real call site (`LoopCameraDirector.cs:217`),
  updated to pass `shotOrigin`. Other grep hits are the definition + comments. No
  un-updated caller → no compile/logic break.
- New `ResetToOrigin(LastShotOrigin, LastShotLaunchDir)` is gated inside
  `if (change.Next == BallState.OB)` — it cannot touch normal (non-OB) Flying/
  Rolling/AtRest/InCup chase transitions. The pre-existing Aiming→Flying
  `ArmChaseForShot` reset (`:245`) is unchanged.
- Camera is presentation-only: OOB penalty/provenance is resolved by
  `BallStateMachine` before `HandleStateChange` runs. Physics/ diff = exactly the 5
  authorized files (`ObGroundSkirt.cs` untouched; fix done wholly in the pivot).
  Orchestrator-derived: BakedZoneClassifierTests 12/12, RealHoleTerrainTests 60/60 →
  no classifier regression from the viewer edit.

## Attack 4 — Test honesty (Rule 6)

`Director_OnOB_NoWaterHit_FallsBackToChangePosition` → renamed
`…_LongShot_UsesMidpointPivot`. OLD assertions: `pivot.x==500`, `pivot.y==2+5==7`.
NEW assertions: `pivot.x==250` (midpoint of origin 0 and hit 500), `pivot.y==2+25==27`.
finalPos is 500m from origin → distance ≫ 40m → exercises the midpoint branch. The
new values are the INTENDED fixed behavior and would FAIL on the old code (500≠250,
7≠27) — this is a tightened assertion of new behavior, NOT a loosened test. Honest.

## Prior-rejection defect ledger

| Cesar's flagged defect | Same-angle re-shoot | Verdict |
|---|---|---|
| Camera sinks to/under terrain on OOB clamp; flat skirt fills bottom ~40% of settle | new settle t=4.5–11.5 (clean stable aerial) vs `CESAR_REJECT_..._final.jpg` | GONE |
| Camera bounce-back when clamp arms | t=4.5 ≡ t=11.5 (settled, not oscillating); `ResetToOrigin` zeroes carry-over velocity | GONE |

## Surfaced for Cesar (non-blocking)

Transition frames t≈2.5–3.75 of the Stage-1 clip render an ugly near-ground
grazing view of a flat green plane (~1.5s). It is a PRE-EXISTING flight artifact —
byte-for-byte present in the clip you rejected (`stage1_ob_after.mp4`), not
introduced by this fix, and not the settle sink you flagged. The settle you
objected to is now a clean aerial. Flagging in case you want a separate polish
task for the low-flight framing; it is not a defect in this scoped iteration.

Strongest attack: the t≈2.5–3.75 flat-plane grazing frames that looked like a
larger version of the rejected defect — defeated by A/B against the old clip
proving they pre-exist the fix and are flight-phase, not the settle.
