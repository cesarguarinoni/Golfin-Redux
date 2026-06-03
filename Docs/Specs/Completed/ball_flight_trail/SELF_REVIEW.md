# Self-Review — `ball_flight_trail`

> Written by `golfin-self-reviewer` subagent. Iteration **2** of self-review.

**Reviewed:** 2026-06-03 (JST)

## Verdict

`PASS` → `FORWARD_TO_ARCHITECT` (`STATUS.md` → `READY_FOR_ARCHITECT_REVIEW`)

The four hard FAILs and four soft FAILs from iter-1 are resolved. The scene is restored to HEAD parity (285 GameObjects, 21-line diff containing only the BallTrailController component-add + `_trailMaterial` + `PhysicsLabController._ballTrail` ref). New canonical screenshots show actual colored ribbons in distinct frames. The motion video is genuine motion (frame-extracts are byte-distinct across the timeline) and shows ribbon formation, gold-perfect ribbon, and the OB recolor flip. The `ForceOBRecolorForCapture()` seam is a line-for-line copy of the production OB code path, so the in-motion red-line frame IS a valid proof that `MaterialPropertyBlock._BaseColor` propagates to already-laid TrailRenderer segments.

**Forwarded with a blocking housekeeping note** (see § Housekeeping required before DONE). The architect-reviewer should not write `READY_FOR_REDTEAM` until the stale iter-1 artifacts are deleted, because they remain in the folder and could mislead the next reviewer or Cesar.

---

## Visual diff notes — independent pixel scan (BEFORE reading SPEC/report)

I opened the three new canonical PNGs and selected video frames with no spec context first.

- `trail_blue_inflight.png` — PhysicsLab view, ball-in-flight moment. A clean **light-blue vertical ribbon** (looks like ~#5BB0FF rendered, consistent with `#2E9BFF` × alpha gradient) runs from low in the frame up toward the flag-stick at the centre, with "462 yds" label on the flag. The ribbon is clearly NOT a fresh tip — it's a deposited segment of several metres of laid trail. HUD chips (GOLFIN, DRIVER 250yds, TURN 1) frame the scene as in prior shots.
- `trail_gold_inflight.png` — Same camera framing, "TURN 3". A **gold/yellow vertical ribbon** of similar length runs through the middle of the frame, slightly off-axis vs the flag (consistent with a Green-accuracy clean shot). Distinctly NOT blue, NOT red.
- `trail_ob_recolor.png` — Same camera framing, "TURN 5". A **bright red vertical ribbon** runs through the middle, slightly leaning left, extending the full visible height. The green pad has shifted slightly (the ball is at a different position than the blue/gold shots) confirming a separate, distinct shot was fired before the recolor.
- `trail_vid_extract_blue.png` — Frame extract from the motion video showing the same scene with a thick blue ribbon descending from the centre — also captioned "BLUE RIBBON: Normal/degraded shot (Yellow accuracy) / Trail forms and follows ball through flight and roll".

Net: three distinct color states, three distinct PNGs, MD5 verified non-identical. The "ball flight trail" feature is visibly demonstrated in each still.

### Video frame extracts (1 fps + 4 fps via ffmpeg)

`videos/ball_trail_motion.mp4` — 1.56 MB, 19.125 s, **1080×1920 portrait** (note: report claims 1920×1080 landscape — actually portrait, minor mistake), H.264, 8 fps captured, 153 source frames in `screenshots/vidframes/`.

Frame-extracts at 1 fps and 4 fps, MD5 verified across timeline:
- f_001 / f_002 / f_005 → **clear blue ribbon visible, ball mid-flight** (different yaw positions visible across frames — genuine motion, not slideshow).
- g_010 → ball partway through flight; short blue trail visible behind.
- g_020 → blue ribbon at maximum extent, ball about to come to rest.
- g_036 / g_040 / g_060 → **clear gold/yellow ribbon visible across the gold-shot segment** (smooth narrow yellow line down the middle of a near-empty light-cyan/grey backdrop, which is the camera switching to a top-down/aerial perspective for the gold and OB shots).
- g_068 / g_069 → near-blank frames (the OB camera angle shows only the deposited ribbon tail; the ball/ribbon is mostly off-camera in this top-down view).
- g_070 → **thin red vertical line in the centre — the recolor moment**, captioned "OB COLOR: Already-laid blue ribbon flips ENTIRELY to red / `ForceOBRecolorForCapture()` calls `SetRibbonColor(_obColor)` via MPB".
- g_073, g_077 → post-recolor; ribbon has faded (the `time = 8s` fade) so the visible red is short, but the moment of recolor is captured cleanly at g_070.

The red sliver at g_070 is thinner than I would ideally want for a maximally-vivid demonstration, because the OB shot's camera angle ends up viewing the laid ribbon nearly end-on. **However**, the OB-recolor still PNG (`trail_ob_recolor.png`) shows the red ribbon clearly from a viewing angle that makes the full width visible. Combined with the video, the two artifacts together demonstrate the recolor.

---

## Checklist verification (Gate-walk against the 4 acceptance gates + the 4 prior FAILs)

| Item | Implementer said | Self-reviewer says | Notes |
|---|---|---|---|
| **PRIOR FAIL 1 (scene corruption)** | FIXED | **CONFIRM-FIXED** | `git diff --numstat Assets/Scenes/Physics/LabScaffold.unity` = **21 lines** (was 18,586 in iter-1). `grep -c '^GameObject:' Assets/Scenes/Physics/LabScaffold.unity` = **285**, identical to `git show HEAD:…` which also = **285**. Net ΔGO = 0. Diff body contains ONLY: (a) component fileID 1075126837 added to BallAnimator's component list, (b) the BallTrailController MonoBehaviour block with serialized `_flightColor`/`_obColor`/`_perfectColor`/`_trailMaterial`/`_time`/`_minVertexDistance`/`_startWidth`, (c) `_ballTrail: {fileID: 1075126837}` ref added to PhysicsLabController. No `m_IsActive: 0`, no removed GOs, no `sizeDelta` changes. Surgical and correct. |
| **PRIOR FAIL 2 (duplicate "blue flying" screenshot)** | FIXED via new file | **CONFIRM-FIXED** | `trail_blue_inflight.png` MD5 = `e3b6d277b53cebd36f4792b7c9bf00c0`, NOT identical to any other screenshot. Shows a real blue ribbon mid-flight. (Note: the stale iter-1 duplicate `trail_blue_flying.png` still sits in the folder — flagged under § Housekeeping.) |
| **PRIOR FAIL 3 (no screenshot shows a ribbon)** | FIXED with 3 new PNGs | **CONFIRM-FIXED** | All three new `_inflight` / `_recolor` PNGs show a clearly visible colored ribbon in the claimed state. Per-frame visual scan above. |
| **PRIOR FAIL 4 (slideshow video)** | FIXED with `ball_trail_motion.mp4` | **CONFIRM-FIXED** | Frame MD5s across the timeline (f_001, f_005, f_010, f_015, f_018, f_019) are ALL distinct — this is real motion, not a slideshow. Ball is visibly moving across frames f_001 → f_005 → g_010 → g_020. Ribbon visibly forms behind it. Camera shifts angle between shots A/B/C. |
| **PRIOR FAIL 5 (whole-ribbon recolor unverified in motion)** | FIXED via `ForceOBRecolorForCapture()` seam | **CONFIRM-FIXED** | The seam (BallTrailController.cs L202-208) executes **line-for-line identical code** to the production OB branch (L91-99): `SetRibbonColor(_obColor); _tr.emitting = false;`. So the in-motion video moment at g_070 IS a proof that the MPB `_BaseColor` write recolors already-laid TrailRenderer geometry on the URP Particles/Unlit shader at draw time — the only difference vs the real OB path is what triggered it (a seam call vs the BallStateMachine event), and both end up at the same two lines. This is a legitimate substitute for a flat-PhysicsLab-with-no-OB-zone limitation. SPEC sanctions `#if UNITY_EDITOR` bot seams (Change B, "Add an internal test/bot seam"). |
| **Gate 1**: On `Aiming→Flying`, `emitting==true` & `RibbonColorForBot==_flightColor` for degraded shot | PASS | **CONFIRM-PASS** | `trail_blue_inflight.png` shows the blue ribbon on a Yellow-accuracy shot. EditMode G1 readback `rgb=(0.180,0.608,1.000)` = #2E9BFF. Motion video segment A shows ribbon forming and following the ball through flight + roll. |
| **Gate 1b**: `RibbonColorForBot==_perfectColor` for a clean full-swing flick (Green) | PASS | **CONFIRM-PASS** | `trail_gold_inflight.png` shows the gold ribbon clearly distinct from blue. Video g_036/g_040/g_060 confirm the gold color is visible in motion across multiple frames. EditMode G2 `lastClean=True rgb=(1.000,0.824,0.290)` = #FFD24A. |
| **Gate 2**: On OB shot, `RibbonColorForBot==_obColor` after OB (WHOLE ribbon flips) | PASS | **CONFIRM-PASS** | `trail_ob_recolor.png` shows the entire deposited ribbon recolored red. Video g_070 shows the recolor moment at ~17.5 s timestamp. Verified the seam runs the identical code path as the production OB branch — see PRIOR FAIL 5 row above. |
| **Gate 3**: Putt shot → `_flightColor` (never perfect) | PASS | **CONFIRM-PASS** (latch-logic) | `!IsPutt` guard in `ShotController.CommitFlick()` was already confirmed by iter-1 self-reviewer; code unchanged in iter-2. No visual to verify since flat lab has no putt-only scenario, but the latch logic is correct. |
| **Gate 4**: BallAnimator untouched; unrelated systems untouched | PASS | **CONFIRM-PASS** | `git diff Assets/Scripts/Physics/Viewer/BallAnimator.cs` = empty. Scene at HEAD GO parity (285). The `BallTrailCaptureRunner.cs` is untracked iter-2 scaffolding — not wired in any scene (its GUID `addaceca5a83f4b3fbc5b0d54123193e` does not appear in `Assets/Scenes/` or `Assets/Prefabs/`), so it cannot auto-run in player or editor — but it is leftover dev scaffolding that must be deleted before DONE (see § Housekeeping). |

---

## Bbox verification (Step 6)

**Not applicable.** No "X inside Y" containment claims in this SPEC — it is a runtime VFX feature (TrailRenderer ribbon), not a UI layout. Bbox check skipped per Step 6.

---

## Scene-mutation audit (Step 7)

`git diff --numstat Assets/Scenes/Physics/LabScaffold.unity` = **21 lines** (vs 18,586 in iter-1). Full diff body:

```
@@ -12916,6 +12916,7 @@ GameObject (BallAnimator)
  m_Component:
+  - component: {fileID: 1075126837}        ← BallTrailController component added

@@ -12960,6 +12961,25 @@
+--- !u!114 &1075126837                      ← BallTrailController MonoBehaviour block
+  ...
+  _flightColor / _obColor / _perfectColor / _trailMaterial / _time / _minVertexDistance / _startWidth

@@ -18951,6 +18971,7 @@ MonoBehaviour (PhysicsLabController)
+  _ballTrail: {fileID: 1075126837}           ← ref wiring on PhysicsLabController
```

GameObject count: 285 (HEAD) → 285 (working). Zero unintended mutations. The exact "BallTrailController component-add + `_trailMaterial` assignment + `PhysicsLabController._ballTrail` ref" that the architect's context line predicted, nothing more.

`git status --porcelain --untracked-files=all` cross-referenced against the iter-2 baseline DIRTY block in `HEARTBEAT.log` (HEAD SHA `6ddceec4`):
- All pre-existing `M`/`??` paths (NuGet, Taiheyo metas, h07 captures, regression .md files, Packages, `capture-all-holes.mjs`) ARE in the baseline block — confirmed pre-existing.
- Iter-2-introduced paths: `M Assets/Scenes/Physics/LabScaffold.unity`, `M Assets/Scripts/Gameplay/Input/ShotController.cs`, `M Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`, `?? Assets/Scripts/Physics/Viewer/BallTrailController.cs(.meta)`, `?? Assets/Scripts/Physics/Viewer/BallTrailCaptureRunner.cs(.meta)`, `?? Assets/Art/3D/Balls/BallTrail.mat(.meta)`, and the spec-folder artifacts. All match `IMPLEMENTER_REPORT.md` § "Files modified or created" (Rule 13 compliant).

---

## Production-flow capture check (Step 8)

This is a runtime VFX feature; the analogous gate is "captured via real play-mode + production code path." Verified:
- All three canonical stills + the motion video were captured via `CaptureCore.SnapPlayModeSafe("label")` called from a play-mode coroutine (`BallTrailCaptureRunner.FullCaptureSequence`).
- Shots are fired via `_plc.FireViaShotController(0.85f, DebugShotAccuracy.{Yellow,Green,Red})` — the actual `ShotController.CommitFlick` path, with real `_ballSM` state-machine transitions firing `OnStateChanged` → BallTrailController.HandleStateChanged → SetRibbonColor.
- The ONLY non-production element is the OB seam, which is justified because flat PhysicsLab has no OB zone, and the seam executes the identical code path. SPEC-sanctioned `#if UNITY_EDITOR` bot seam.

No smoke-runner / static state injection that bypasses production lifecycle. Production-flow capture is satisfied.

---

## Capture-helper compliance check (Step 5)

- **Screenshot provenance:** Report explicitly cites `CaptureCore.SnapPlayModeSafe("label")` for every still + every video frame, called from a play-mode coroutine. Compliant with CLAUDE.md § Screenshots rule 1 (no `ScreenCapture.CaptureScreenshot`), rule 2 (capture-then-pause, not pause-then-capture — `SnapPlayModeSafe` does not pause, which is correct for a continuous capture coroutine), rule 6 (`CaptureCore` is the sole sanctioned path).
- **Maintenance protocol for new contexts:** No new `*Context.cs` file under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/` introduced. Rule N/A.

---

## Iteration count

This is iteration **2** of self-review for `ball_flight_trail`. N < 3; PASS routing is permitted.

---

## Housekeeping required before DONE (BLOCKING — flag for architect-reviewer)

These are NOT FAILs (they do not compromise the evidence — the report cites only the new files and the live `ForceOBRecolorForCapture` seam is sanctioned by the SPEC's bot-seam allowance) — but they MUST be cleaned up before Cesar's final approval / before STATUS hits `DONE`. The architect-reviewer must enforce these.

1. **Delete stale iter-1 screenshots** (still in `screenshots/`, NOT cited by IMPLEMENTER_REPORT.md as evidence):
   - `trail_blue_flying.png` (MD5 `fe6ea0563f619ac38c29f71d63619ba0` — duplicate of `trail_initial_state.png`, the flagged iter-1 duplicate)
   - `trail_initial_state.png` (same MD5 — the iter-1 pre-shot aiming frame)
   - `trail_gold_perfect.png` (iter-1 mislabelled aiming frame)
   - `trail_ob_red.png` (iter-1 mislabelled aiming frame)
   Leaving them in place can mislead the next reviewer or Cesar into mistaking pre-shot aiming frames for ribbon evidence.
2. **Delete stale slideshow video:** `videos/ball_trail_states.mp4` (the iter-1 still-image-slideshow that was the soft FAIL).
3. **Delete raw uncaptioned video:** `videos/ball_trail_motion_raw.mp4` is the pre-caption stitch; only `ball_trail_motion.mp4` (captioned) is the canonical artifact. Remove the raw to keep the deliverable folder clean.
4. **Delete temp capture scaffolding:** `Assets/Scripts/Physics/Viewer/BallTrailCaptureRunner.cs` + `.meta`. Per `feedback_restore_playable_state`, no leftover auto-running test scripts in the shipped state. Verified clean: the runner's GUID `addaceca5a83f4b3fbc5b0d54123193e` is NOT referenced in any scene/prefab so it cannot auto-run — but it is `#if UNITY_EDITOR` dev scaffolding that the report itself flags ("They will be removed at task close-out after this review pass") and should be deleted now that capture is done. The implementer's note confirms this is the plan.
5. **Optional:** Consider also deleting the 153-frame `screenshots/vidframes/` PNG dump now that the motion video is stitched — these are intermediate artifacts not cited as evidence. Not strictly required.

The `ForceOBRecolorForCapture()` seam ON `BallTrailController` itself is fine to ship as-is (it's `#if UNITY_EDITOR`, the SPEC sanctions bot seams, and removing it without the standalone runner would require another iter anyway). The architect can decide whether to require its removal — I would argue keep it as a permanent reviewer/QA seam.

---

## Minor inaccuracies in IMPLEMENTER_REPORT (non-blocking)

- Video dimensions: report says `1920×1080` landscape; ffprobe shows `1080×1920` portrait. Doesn't affect the verdict — the video is real motion and shows the feature — but the report should be corrected for accuracy.
- Report says "video captured at 8fps" — ffprobe confirms `r_frame_rate=8/1`. Correct.

---

## Routing

`FORWARD_TO_ARCHITECT` with the housekeeping note above. Setting `STATUS.md` → `READY_FOR_ARCHITECT_REVIEW`.

The core feature evidence is solid:
1. ✅ Scene restored to HEAD parity (285 GOs, 21-line surgical diff).
2. ✅ Three distinct PNGs showing real ribbons in three claimed colors.
3. ✅ Genuine motion video (frame-distinct across timeline) showing ribbon formation, gold-perfect, and the OB recolor moment.
4. ✅ OB whole-ribbon-recolor proven via a sanctioned bot seam that runs the identical production code path.
5. ✅ Code changes match SPEC structure (already confirmed by iter-1 self-reviewer on the unchanged code).
