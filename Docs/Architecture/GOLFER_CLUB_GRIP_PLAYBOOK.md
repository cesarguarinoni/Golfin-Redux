# Golfer club-grip playbook — how a new club or character gets its grip without Cesar in the loop

Written 2026-09-16 after the driver (Remy → Olivia) and the putter (Olivia). Cesar's closing instruction: *"write all the
pertinent lessons so we don't have to go through the same micromanagement for each club/character."* This is the
runbook. `tasks/lessons.md` Lessons AX–BF carry the scars behind each rule; `Docs/Specs/Active/golfer_club_grip/`
carries the tooling, the evidence and the reports.

## 0. What Cesar judges, and what counts as evidence

1. **The Game view, nothing else.** Scene-camera frames shot from a coroutine are rendered before the rig evaluates:
   broken hands, club wrong. Cesar called them unusable. The stage-2 solver's own frames are fine because they are
   shot after end-of-frame with the rig settled; the harness's Game-view snaps (`golfer_h06_*`) are the truth.
2. **The hands.** Before any message, crop the hands at full resolution and put them beside the driver's approved
   stage-2 verify frames (`evidence/olivia/stage2/verify_*.png`, four cameras). Same way = stacked on the grip,
   palms the right way (lead away from the target, trail toward it), fingers wrapped, no grip poking past the hands.
3. **The club at the ball.** Face plane 15–60 mm behind the ball centre, face centre within 30 mm across, sole on
   the ground (0–15 mm), toe and heel level. Rows: `club.faceBehindBall.<tag>`, `stance.<tag>.headOnLie`.
4. **Motion.** A video is a real shot through `BotSwing.PlayPerfect` (the bot rule), never the animator's trigger
   alone — the trigger runs the swing clip into Idle and she "turns". 60 fps constant playback, uniform frames,
   no repeats (probe with ffprobe pts and a frame-diff before sending).
5. **Numbers never override a picture.** A row that passes on a stale or frozen pose is a bug in the row.

## 1. Pipeline for a NEW CLUB on an existing character (what the putter went through)

1. **Prefab facts first.** All 31 club prefabs share the pivot (butt at y −0.11, +Y down the shaft). Head axes
   differ per model: driver toe −Z / face −X (`DriverFaceLocal`), putter toe −X / face +Z. Render the head from ±X/±Z
   with markers (`Docs/Diagnostics/_capture/clubviews/*_marker_*`) to know which side is the face — do not infer it
   from vertex counts or comments.
2. **Mount for the solve.** `HandHingeStage2` per-club mode (`PuttKey` pattern): the club under `ClubSlot` (the
   anchors, `ClubStart/ClubEnd` and the WristTargets live there), child rotation that maps its head axes onto the
   driver's convention, `ClubEnd` at its own tip minus the driver's tip→ClubEnd offset (0.0234).
3. **Solve** with `roll mode pitchscan` at that club's own address clip, with the golfer placed at the ball for that
   club (the harness hook at the right block — the putter's is the green putt address in `PuttGripOnGreen`).
   Menu pattern: `GOLFIN/Golfer Test/Stage 2 <CLUB> solve (pitch scan)`. Session keys are
   `GolferTestVerification.Stage2`, `...Stage2Verify`, `...Stage2Roll` — use the constants, not guessed names.
4. **Bake** into per-club presenter fields (slot pose for both slots, anchors, wrist offsets, ClubEnd, club child
   rotation), applied in that club's mode with a `RigBuilder.Build()`; drive values restored otherwise. Fold the
   recorder's solved face roll in the way `ApplyFaceRollFix` does (slot rolled, anchors counter-rotated). The
   placement point is the FACE behind the ball (`addressFaceOffsetLocal*`), never the shaft tip; its y stays 0.
5. **Verify** (`Stage 2 <CLUB> verify`), then the side-by-side with the driver's verify frames. Gate rows:
   face square ±5°, head at ball < 50 mm, palms > 0.5, hands on shaft < 3 mm, fingers on the contact circle, wrists
   ≤ 40° from the clip. Rows open on the driver by the letter (heel pad, trail palm on thumb) stay open.
6. **Height and distance** without touching the grip: hips drop via the stance rig (`Stance_Hips`), placement offset.
   The lie lever (rolling both slots about the aim line) swings the head sideways 15 cm per 12° and does not level
   the blade — measure before using it. Address transitions must exist on the animator for the club's state
   (`Address_Drive ⇄ Address_Putt` on `IsPutt` were missing on Olivia).
7. **Game view + video** through the harness (`Record <CLUB> motion video`), real shot via `BotSwing`, then the
   frame-diff probe, then the send with the side-by-side. One message, everything in it, paths included.

## 2. Pipeline for a NEW CHARACTER (what Olivia went through)

1. Build from the reference prefab with a transform-by-transform diff (instance yaw 180 on Remy's, root scale,
   every authored child) — `GolferTestCharacterBuilder`. A "mirrored / backwards" symptom is a facing or scale
   error until proven otherwise; check the face-on frame first.
2. Stage 0 capture on the new hands (finger half-thickness measured on the palmar side, L_prox), stage 1 wrap,
   stage 2 per club as in §1 — every club, not only the driver.
3. `FINDINGS_FOR_NEXT_CHARACTER.md` and `OLIVIA_RIG_HANDOFF.md` are the model-side requirements for the Architect.

## 3. Traps that cost hours (do not rediscover)

- **Animator culling freezes the pose.** `GolferPresenter.ApplyCulling` leaves `CullUpdateTransforms` off the swing:
  a club switch at address kept the drive pose on screen while `Address_Putt` played into nothing, and every row and
  scan read that frozen pose (36 identical samples). Measure under `AlwaysAnimate` + end-of-frame, restore after.
- **Reflection on Animation Rigging:** `RigConstraint.data` is a by-ref property (NotSupportedException); use the
  serialized `m_Data` field. Fence presenter hooks with try/catch — an exception in an event handler kills the
  harness coroutine and the editor sits in play mode.
- **The harness must fail loud:** `Docs/Scripts/watch_golfer_run.sh` (exception / silence / timeout) plus the
  in-harness stall watchdog; video windows mark progress every second of golfer time so the watchdog stays quiet.
- **Video:** constant playback in `BotVideoRecorder` (the one authorized Physics-tree edit) at 60 fps; the harness
  window is GOLFER time (`HoldSim`), never wall time; the encoder renders ~1.5–2.5 fps at 1170×2532 so the wall
  watchdog override must cover it (420 s); the one-clip-per-session guard is reset per run.
- **What the player sees:** the club at the ball in the HUD is the on-course handle SPRITE (`S_Controls_*`); only
  the driver template has the golfer's-eye view; the sprites are Gemini art and are not to be touched without
  Cesar. There is no character in the putting camera.
- **Placement:** ground the root at the BALL's lie (top surface at its xz, ball collider excluded), not under the
  feet — an 11 cm sink on a 12° rough. The address point is the face centre behind the ball, not the shaft tip.
- **Face-centre rows:** the "face" vertex set can include the hosel neck and lift the centre; the sole (lowest
  vertex over the ROOT ground) is the honest height.
- **Never edit .cs in play mode; refresh after every edit; never save the gated prefab with the define off; run the
  pipeline's menus, not hand-rolled fits.** A wrist fit left the hands 93/67 mm off the club and was called solved.

## 4. Where things are

| What | Where |
|---|---|
| Solver, per-club mode, bakes | `Assets/Scripts/UI/Editor/HandHingeStage2.cs` (`PuttKey`, `PutterClubEndY`, `PutterChildRotation`, `ApplyPuttBakeToPrefab`) |
| Harness: hooks, rows, scans, video windows, stall watchdog | `Assets/Scripts/UI/Editor/GolferTestVerificationRecorder.cs` |
| Presenter: per-mode slot/anchor/stance/placement application | `Assets/Scripts/Gameplay/Golfer/GolferPresenter.cs` |
| Run watcher | `Docs/Scripts/watch_golfer_run.sh` |
| Evidence, bakes, comparisons | `Docs/Specs/Active/golfer_club_grip/evidence/olivia/{stage2,stage2_putt,putt,putt60,swing60}` |
| Reports | `Docs/Specs/Active/golfer_club_grip/IMPLEMENTER_REPORT.md`, `STATUS.md` |
| Rules | `tasks/lessons.md` AX–BF; memories `feedback_new_club_stage2_pipeline`, `feedback_watch_runs_fail_loud` |
