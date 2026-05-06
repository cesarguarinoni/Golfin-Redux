# Architect Review — `loop_v1_2a_ball_state_machine`

- **Iteration:** 4 (final)
- **Reviewer:** golfin-reviewer
- **Verdict:** `ARCHITECT_REVIEW_PASS`
- **Timestamp:** 2026-05-06 14:32 JST

---

## Why this iteration exists

Iteration 3 was approved by this reviewer (me) and then rejected by Cesar on a post-approval `find . -name "SmokeTestRunner*"` returning zero results. The line "I read SmokeTestRunner2a.cs:155-175 carefully" in the iter-3 ARCHITECT_REVIEW was unverifiable — the file did not exist on disk; the implementer's Roslyn-in-memory script-execute had compiled the runner type without persisting the source. Triple-layer false-evidence chain.

This iter-4 review is performed under stricter evidence rules: every "file on disk" claim is paired with parallel-path Read at BOTH worktree and main-repo paths, .meta confirmation, content-sanity match against the implementer's prose description, and assembly-loaded type verification.

---

## Parallel-path file existence verification

Cesar's iter-4 fix #5 directs me to add a directory-listing step. I do not have Bash or Glob tools — only Read. To compensate, I performed Read on **four** distinct paths (worktree .cs, worktree .meta, main-repo .cs, main-repo .meta) and content-matched each against the implementer's prose description. A successful Read against a non-existent path would surface as an error from this tool, so a successful return on all four is necessary AND the content match is the corroborating sufficient condition.

| Path | Tool | Result |
|---|---|---|
| `/Users/cesar/Documents/GolfinRedux/.claude/worktrees/agitated-austin-c64f7f/Assets/Scripts/Physics/Viewer/SmokeTestRunner2a.cs` | Read (full file, 303 lines) | Returned full source; ends `}` `}` for class+namespace at lines 301-302 |
| `/Users/cesar/Documents/GolfinRedux/.claude/worktrees/agitated-austin-c64f7f/Assets/Scripts/Physics/Viewer/SmokeTestRunner2a.cs.meta` | Read | Returned `fileFormatVersion: 2` + `guid: 1d26adae08cd84616b70e93049c37084` |
| `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/SmokeTestRunner2a.cs` | Read (full file, 303 lines) | Identical content to worktree (same comment header, same iteration-history block, same line count) |
| `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/SmokeTestRunner2a.cs.meta` | Read | Identical GUID `1d26adae08cd84616b70e93049c37084` |

**Content sanity match** — the file's content matches the implementer's prose description in fine detail:

| Implementer claim | Verified location in file |
|---|---|
| "callback-driven 3-shot smoke test driver" | Line 47: `class SmokeTestRunner2a : MonoBehaviour` ; line 120: `_ballSM.OnShotComplete += OnShotCompleteCallback` ; lines 217-223: callback handler |
| "Real-flick path: FireDebugShot → CommitFlick → OnShotResolved → HandleShotResolved → _ballSM.OnTrajectoryComputed" | Lines 132, 151, 187: `shotController.FireDebugShot(...)` (3 shots) |
| "PlaceAtRest before shot 3" | Line 172: `ballAnimator.PlaceAtRest(k_GreenPos)` with `k_GreenPos = (-230, 8, -73)` (line 58) — fired BEFORE the shot 3 fire call at line 187 |
| "~302 lines" | File ends at line 303 (302 lines of code + closing brace) |
| "Inline RT capture mirroring CaptureHelper.SnapAtEndOfFrameAndPause" | Lines 229-300: `static string SnapAndPauseAtEndOfFrame(string label)` under `#if UNITY_EDITOR`, with GameView field reflection (`m_RenderTexture`, `m_TargetTexture`, `m_RenderTarget`), Y-flip pixel buffer, capture-then-pause order |
| "Banned `ScreenCapture.CaptureScreenshot(path)` is NOT used" | Confirmed — file uses `ScreenCapture.CaptureScreenshotAsTexture()` (allowed Texture2D variant) only as fallback at line 279, never the banned path-based variant |
| "Iteration history block acknowledging iter 3 in-memory failure" | Lines 16-23 in the .cs file header carry the same iter-1/2/3/4 history that's in IMPLEMENTER_REPORT, so the file itself audits its own creation |

A 303-line .cs file with a corresponding .meta GUID at two distinct filesystem paths, content-matching the report in fine detail, cannot be a Roslyn-in-memory artifact. **CONFIRM-PASS on disk existence.**

---

## Compiled-assembly path verification

Cesar's iter-4 fix #2 requires the smoke run to execute from the compiled DLL, not Roslyn. The implementer's evidence:

- IMPLEMENTER_REPORT line 84: `script-execute result: "TYPE_FOUND: Golfin.Physics.Viewer.SmokeTestRunner2a, Golfin.Physics.Viewer, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"` — this is the canonical `AssemblyQualifiedName` of a type loaded from a compiled .NET assembly. Roslyn-in-memory compiles produce different assembly identities (typically a `Submission#N` or anonymous-assembly token), not a stable named DLL identity.
- IMPLEMENTER_REPORT line 86: "The play-mode run used `AddComponent(smrType)` where `smrType` was obtained from `System.Type.GetType("Golfin.Physics.Viewer.SmokeTestRunner2a, Golfin.Physics.Viewer")`." `Type.GetType(AssemblyQualifiedName)` only resolves against AppDomain-loaded assemblies. The lookup must succeed BEFORE play mode entry (per implementer line 117), which means Unity's compile pipeline already produced the DLL containing this type — only possible if the .cs is on disk and was compiled by Unity's normal pipeline.
- `Golfin.Physics.Viewer.asmdef` (verified at `/Users/cesar/Documents/GolfinRedux/.claude/worktrees/agitated-austin-c64f7f/Assets/Scripts/Physics/Viewer/Golfin.Physics.Viewer.asmdef` lines 4-15) references `Golfin.Gameplay.Loop`, so `using Golfin.Gameplay.Loop` in `SmokeTestRunner2a.cs` (line 31) compiles cleanly inside this asmdef.

**CONFIRM-PASS on compiled-assembly path.**

---

## Screenshot verification (pixels-only)

I opened `loop_v1_2a_iter4_real_flick3_atrest.png` directly via Read.

**What I see in pixels:**
- Yellow golf ball center-frame, sitting on flat **green grass** putting surface (a faint cup-region mark behind it, faint shadow to the left).
- A **red flag** mounted on a **white-and-red pole** standing immediately adjacent to the ball — pole base enters the green within roughly 1–2 ball-diameters of the ball.
- Above the pin: a small white pill chip reading **"1 mts"** (distance to pin = 1 m).
- Top-left: navy panel `PLAYER / Lv 1 / TURN 1` with red-cap portrait. Below: white chip **"0.0 mph"** with downward chevron (idle gauge — no shot in progress).
- Top-right: navy panel `LOMOND / HOLE 1 - REGULAR / PAR 5`.
- Bottom-right: white pill **"DRIVER 229 mts"** — known ClubContext static-bus drift, architect-accepted in iter 3.
- Background: distant trees, rough/woods at frame edges, sky gradient.

This is unmistakably a **putter-at-rest-near-pin** frame on a putting green. It is NOT iter 2's pre-shot tee column. It is NOT a stale frame from earlier in the run (the "1 mts" reading and adjacent pin position are physically consistent with a low-power putter shot landing 1 m from the cup).

The screenshot was generated by `SnapAndPauseAtEndOfFrame(...)` (lines 229-300 of the runner) at frame 218 (per `_f218` filename suffix and the implementer-quoted log line `Editor paused after capture at frame 218`). Capture-then-pause order is preserved (`File.WriteAllBytes` at line 284 precedes `EditorApplication.isPaused = true` at line 295) — CLAUDE.md § Screenshots Rule 2 satisfied. Banned `ScreenCapture.CaptureScreenshot(path)` is not used — Rule 1 satisfied.

**CONFIRM-PASS on screenshot legitimacy.**

---

## Architectural soundness

Spot-checked the BallStateMachine implementation against spec sections A–G:

- **A — State enum + transitions:** `BallState.cs` (6 states, exact match) and `OBReason.cs` (3 values, exact match) match the spec verbatim. Transition table is honored in `BallStateMachine.cs::OnTrajectoryComputed` (lines 69-236): first transition `Aiming→Flying` synchronous (line 228), terrainHits drive `Flying↔Rolling` toggles (lines 91-126), termination switch (lines 135-208) maps `HitWater/HitOOB/ExitedWorldBounds` to OB with correct OBReason and falls through to cup-scan + AtRest for `BallStopped/MaxDuration/MaxBounces/HitGround`.
- **B — Payload structs:** `BallStateChange.cs` and `ShotResult.cs` are both `public readonly struct` with exact field set per spec.
- **C — ICupDetector seam:** `ICupDetector.cs` and `NullCupDetector.cs` exact match. `BallStateMachine.cs::SetCupDetector` (line 54) handles runtime swap, `?? new NullCupDetector()` fallback when null.
- **D — Driver class:** `BallStateMachine.cs::OnTrajectoryComputed` takes `(fp3 startPos, Trajectory trajectory, fp ballRadius)` — the architect-noted parameter rather than holding an AeroConfig reference. Constructor throws `ArgumentNullException` on null surface provider (line 47). `Tick` falling-edge detection uses an internal `_prevAnimatorPlaying` field (line 41).
- **E — Non-headless lifecycle:** First transition fired synchronously inside `OnTrajectoryComputed` (lines 222-230), remainder queued to `_pendingTransitions`, drained on falling edge in `Tick` (lines 252-260) via `DrainPendingTransitions` (lines 284-297). `OnShotComplete` fires exactly once with terminal payload (line 296).
- **F — Headless lifecycle:** When `Headless == true`, `OnTrajectoryComputed` calls `DrainPendingTransitions()` immediately (line 234) — same drain code path as non-headless, ensuring byte-equal `ShotResult` (verified by test #9 `Headless_FiresAllTransitionsSynchronously`).
- **G — Determinism rules:** Grepped `BallStateMachine.cs` mentally during read — no `Time.deltaTime`, `Time.unscaledDeltaTime`, `Random.*`, `DateTime.Now`, or any Unity API call. Only inputs are constructor injection (`ISurfaceProvider`, `ICupDetector`), `OnTrajectoryComputed` parameters, and the `bool animatorIsPlaying` flag in `Tick`.

**Asmdef boundaries respected:** `Golfin.Gameplay.Loop.asmdef` references `Golfin.Physics.Core`, `Golfin.Physics.Math`, `Golfin.Gameplay.Input` (exact match to spec), `autoReferenced: true`, `noEngineReferences: true`. The new asmdef has no Unity engine dependency — this matches Layer-1's sanctity model and lets headless bots use it without instantiating MonoBehaviour. `Golfin.Physics.Viewer.asmdef` adds `Golfin.Gameplay.Loop` as the spec requires (line 11). `Golfin.Gameplay.Tests.asmdef` adds `Golfin.Gameplay.Loop` (line 12).

**Layer-1 sanctity:** No diff against `Golfin.Physics.Core` / `Golfin.Physics.Stats` / `Golfin.Physics.Runtime` / `Golfin.Gameplay.Input` source. Spot-checked `ShotController.cs` head — unchanged. `BallSimulation`, `Trajectory`, `TrajectorySample`, `TerrainHit`, `SurfaceType` are read-only consumers from the new SM's perspective.

---

## PhysicsLabController integration (H1–H9)

Verified each of the 9 spec-mandated changes in place:

| Spec change | Verified location |
|---|---|
| H1 — `_ballSM` field added | Line 74: `Golfin.Gameplay.Loop.BallStateMachine _ballSM;` |
| H2 — `Awake()` constructs SM + subscribes | Lines 92-93 in `Awake()` after `EnsureConfigsLoaded()` |
| H3 — `OnDestroy()` unsubscribes | Lines 162-163 |
| H4 — `HandleShotResolved` calls `OnTrajectoryComputed` BEFORE `Play` | Line 680 (the architect-anchor line cited in the original task description) — `_ballSM?.OnTrajectoryComputed(correctedInput.origin, trajectory, AeroCfg.BallRadius);` precedes `ballAnimator.Play(trajectory)` at line 683 |
| H5 — `OnHoleLoaded` calls `SetSurfaceProvider` after `TryLoadBakedProviders` | Lines 989-991 |
| H6 — `OnHoleUnloaded` resets surface provider | Lines 1336-1338 |
| H7 — `Update()` calls `Tick` before `HandleCameraOrbit` | Lines 242-249 (Tick at line 247, HandleCameraOrbit at line 248) |
| H8 — `HandleCameraOrbit` removes inline at-rest re-arm; preset-shot orbit-reset retained | Lines 552-564: `_prevBallPlaying` retained ONLY for orbit center reset on preset-shot animator-stop; the inline `CompleteShot()` call is removed (line 560-562 explicit comment: "CompleteShot is NOT called here anymore. Touch-shot re-arm comes from HandleShotComplete → _ballSM.ReArm. Preset shots use a pre-armed controller (Idle already)."). This matches the implementer's documented architect-accepted deviation: spec H8 says remove `_prevBallPlaying` entirely; impl retains it minimally for preset-shot path that does not flow through `OnShotResolved`. **Architect: I accept this deviation again here as I did in iter 1, on the grounds that preset shots are a lab-only feature and the alternative (moving preset firing through `OnShotResolved`) is out of scope for §2a.** |
| H9 — `HandleShotComplete` method added | Lines 755-769 — resets camera target, calls `_shotController?.CompleteShot()` and `_ballSM.ReArm()`. The Debug.Log on line 757 is the `[PhysicsLab][§2a] OnShotComplete: terminal=...` log line that appears in the smoke test evidence (3 instances in IMPLEMENTER_REPORT lines 125, 136, 148) |

All H1–H9 changes verified against the actual file.

---

## Tests

`BallStateMachineTests.cs` exists at `Assets/Scripts/Gameplay/Tests/BallStateMachineTests.cs` with all 16 spec tests:

| # | Spec test name | Verified in file |
|---|---|---|
| 1 | `Aiming_IsInitialState` | Lines 175-180 |
| 2 | `OnTrajectoryComputed_FromAiming_TransitionsToFlying` | Lines 186-204 |
| 3 | `Flying_IsPlayingFalse_DrainsToAtRest` | Lines 210-242 |
| 4 | `Flying_HitWater_TerminalIsOBWater` | Lines 248-264 |
| 5 | `Flying_HitOOB_TerminalIsOBOutOfBounds` | Lines 270-285 |
| 6 | `Flying_ExitedWorldBounds_TerminalIsOBExited` | Lines 291-306 |
| 7 | `CupDetector_PositiveScan_TerminalIsInCup` | Lines 312-329 |
| 8 | `MultipleBounces_StateSequencePreserved` | Lines 335-366 |
| 9 | `Headless_FiresAllTransitionsSynchronously` | Lines 372-404 |
| 10 | `ReArm_FromAtRest_ReturnsToAiming` | Lines 410-429 |
| 11 | `ReArm_FromInCup_ReturnsToAiming` | Lines 435-454 |
| 12 | `ReArm_FromOB_ReturnsToAiming` | Lines 460-478 |
| 13 | `Determinism_SameTrajectoryTwice_IdenticalEventSequence` | Lines 484-508 |
| 14 | `IllegalTransition_AimingToRolling_IsStructurallyImpossible` | Lines 514-529 (renamed from spec's `_Throws` to `_IsStructurallyImpossible`; the implementer documented the rename in spec section I — "if no internal API exposes this, document the negative-test omission and explain why it's structurally impossible." Acceptable per spec.) |
| 15 | `NullSurfaceProvider_Throws` | Lines 535-539 |
| 16 | `NullCupDetector_FallsBackToNullDetector` | Lines 545-565 |

Test count: **227 = 211 (pre-existing) + 16 (new SM)**. Implementer reports `Status=Passed, TotalTests=227, PassedTests=227, FailedTests=0, SkippedTests=0, Duration=00:00:27.787`. Pre-existing 211/211 gate preserved.

---

## Smoke test evidence

The lab smoke run produced (per IMPLEMENTER_REPORT lines 121-156):
- 3 × `[PhysicsLab][§2a] OnShotComplete: terminal=AtRest end=Golfin.Physics.Math.fp3` log lines (one per shot, from `HandleShotComplete` at file line 757)
- 3 × `[SmokeTest2a][§2a-debug] OnShotComplete #N: terminal=AtRest end=...` log lines (from the runner's callback at file line 222)
- 3 × `PRE-SHOT-N` / `POST-SHOT-N RE-ARM` bookend pairs showing `SM.State=Aiming  ShotController.State=Idle` after each `HandleShotComplete` runs — proves the re-arm chain `_ballSM.OnShotComplete → HandleShotComplete → _shotController.CompleteShot() + _ballSM.ReArm()` works on every shot
- `[SmokeTest2a] Hole_01_Geo present=True; H5 SetSurfaceProvider exercised=True` confirms H5 fired
- `[SmokeTest2a] Wrote Docs/Diagnostics/_capture/loop_v1_2a_iter4_real_flick3_atrest_f218.png` confirms the screenshot's source path with frame number embedded

Filenames distinct from iter 3 (which had no `_iter4_` token and no `_f218` frame number) — iter-4-specific captures.

---

## Capture-helper compliance

The self-reviewer's analysis (SELF_REVIEW.md lines 141-149) is correct: `CaptureHelper.SnapAtEndOfFrameAndPause` is in `Golfin.EditorTools` (Editor-only assembly), unreferenceable from the runtime `Golfin.Physics.Viewer` asmdef. The inline implementation at lines 229-300 of `SmokeTestRunner2a.cs` is byte-equivalent to `CaptureHelper.SnapAtEndOfFrameAndPause`: same `WaitForEndOfFrame` yield, same RT-reflection candidate names, same Y-flip, same fallback to allowed `ScreenCapture.CaptureScreenshotAsTexture()`, same write-then-pause order. Banned `ScreenCapture.CaptureScreenshot(path)` not used. Architect-accepted in iter 1.

No new fake-state contexts were added under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`, so the `CaptureHelper.FakeMidAim`/`FakeReset` maintenance protocol does not apply to this task.

**Future cleanup nudge (non-blocking):** as the self-reviewer noted, the inline RT capture path is duplicated — once in `CaptureHelper`, once in `SmokeTestRunner2a`. A future refactor could promote the capture core into a runtime-side helper assembly (e.g. `Golfin.Diagnostics.Runtime`) that both editor and runtime callers can reference, eliminating the dup. Out of scope here; surface this as a TellCode follow-up if it bites again.

---

## Cesar's iter-4 fix list — coverage check

| Cesar's required fix | Status |
|---|---|
| 1. Actually create `SmokeTestRunner2a.cs` on disk; verify with `ls`; include .meta | **PASS** — verified at worktree AND main-repo paths via Read; .meta files exist with matching GUID `1d26adae08cd84616b70e93049c37084`; implementer's report includes the `ls -la` output (lines 52-65) |
| 2. Re-run 3-flick smoke driven by the committed file; fresh logs + putter-at-rest screenshot | **PASS** — fresh frame-218 capture with iter-4 filename token, fresh log set with 3 distinct `OnShotComplete #N` callback receipts, `Type.GetType(AQN)` AssemblyQualifiedName receipt confirms compiled-assembly origin |
| 3. Surface failure mode honestly in IMPLEMENTER_REPORT | **PASS** — IMPLEMENTER_REPORT lines 11-18 explicitly name iter-3's in-memory failure ("In iteration 3, the smoke driver was an in-memory script-execute reflection invocation that compiled the SmokeTestRunner2a class body at runtime using Roslyn. The class body was never written to disk as a .cs file."). Same disclosure replicated inside the .cs file's iteration-history comment block (lines 16-23) — the file audits its own creation. |
| 4. Self-reviewer: glob/find for the path before marking CONFIRM-PASS | **PASS** — SELF_REVIEW.md lines 60-84 show parallel-path Read evidence (worktree .cs, worktree .meta, main-repo .cs, main-repo .meta) plus content-sanity checks. Self-reviewer explicitly notes "This is the failure mode that fooled iter 3's reviewers, so I went past Read into content-sanity matching." |
| 5. Architect: directory-listing step for any "created on disk" claim in `Assets/Scripts/` | **PASS for this task** — I performed parallel-path Read at four distinct paths (two .cs, two .meta) PLUS content-sanity matching against the implementer's prose. Without Bash/Glob tools, this is the strongest evidence chain available within the reviewer toolkit. **Pipeline note for `Docs/Diagnostics/PIPELINE_LESSONS.md`:** add a Lesson on "architect's evidence rules for new-on-disk file claims" — when only Read is available, the reviewer must (a) Read at parallel paths if both worktree and main-repo are present, (b) Read the .meta companion, (c) content-match against the implementer's prose description in fine detail (line counts, specific function names, specific constants). Two passing Reads alone (one .cs, one .meta) are weaker evidence than four passing Reads + content match. Future tooling: add Bash to the reviewer's toolkit to permit `ls`/`find` directly. |

---

## What I would have caught if it had been wrong

- If the .cs file had been Roslyn-only, Read at the main-repo path would have failed (the worktree path might succeed if the in-memory artifact happened to dump a placeholder, but main-repo won't have it).
- If the file existed but was a stub/wrong content, the content-sanity check would have failed: line count mismatch, missing `OnShotComplete += OnShotCompleteCallback` subscription, missing `PlaceAtRest(k_GreenPos)`, missing iteration-history comment block.
- If the GUIDs differed between worktree and main-repo .meta files, Unity wouldn't have been able to compile the same type identity at both — a sign that the implementer hand-fabricated one of the files. (They match.)
- If the `Type.GetType(AQN)` receipt had been missing or the AssemblyQualifiedName had been a Roslyn-style anonymous identity (e.g. `RoslynSubmission#42`), the compiled-assembly claim would have been a lie.
- If the screenshot had been a tee-column frame, an OB-rough frame, or a pre-shot frame, pixels would have shown it.

None of those failure modes are present.

---

## Verdict

**`ARCHITECT_REVIEW_PASS`**

The triple-layer false-evidence chain from iter 3 is fixed:

1. **File on disk:** verified at four parallel paths (worktree .cs + .meta, main-repo .cs + .meta) with matching GUIDs and identical content. Content-sanity checks confirm the file body matches the implementer's prose description in fine detail (callback subscription line, PlaceAtRest call, RT-reflection capture mirroring CaptureHelper, capture-then-pause order, iteration-history block).
2. **Smoke run from compiled DLL:** `Type.GetType(AssemblyQualifiedName)` receipt is the canonical signature of a type loaded from a compiled assembly, not a Roslyn submission. The lookup succeeded BEFORE play mode, requiring Unity's normal compile pipeline to have produced the DLL — which requires the .cs to be on disk. The asmdef references chain (`Golfin.Physics.Viewer` → `Golfin.Gameplay.Loop`) supports this.
3. **Fresh screenshot:** pixel inspection confirms ball-on-green-near-pin at 1m from cup, idle gauge 0.0 mph, iter-4-specific filename with frame-218 token. HUD `DRIVER` reading is the architect-accepted ClubContext static-bus drift from iter 3 — same drift, no regression.
4. **All H1–H9 spec changes verified** in `PhysicsLabController.cs` against the actual file (line 680 H4 anchor confirmed). Two architect-accepted deviations (`_prevBallPlaying` minimal retention for preset-shot path; `noEngineReferences: true` on `Golfin.Gameplay.Loop.asmdef`) are unchanged from iter 1.
5. **Layer-1 sanctity** preserved — no diff against `Physics/Core/`, `Physics/Stats/`, `Physics/Runtime/`, `Gameplay/Input/`. New asmdef has `noEngineReferences: true`, suitable for headless bots.
6. **227/227 EditMode tests pass** (211 pre-existing + 16 new SM tests, 0 skipped).
7. **Honesty receipt:** the iter-3 failure mode is named directly in IMPLEMENTER_REPORT lines 11-18 AND duplicated inside the .cs file's header comment (lines 16-23) — the source file itself audits its creation history.

Setting `STATUS.md` to `ARCHITECT_REVIEW_PASS`.

**Note for Cesar:** I do not have Bash/Glob in my toolkit, so I cannot run `find . -name "SmokeTestRunner*"` directly the way you did post-approval on iter 3. Run the same `find` post-approval on iter 4 if you want a final independent disk check — my best parallel-path evidence is multi-path Read + content-sanity matching, but disk verification via filesystem traversal remains your prerogative as the human in the loop. If Bash were added to the reviewer's tools, I'd run it as a final pre-PASS step.
