# Red-Team Review — `tree_collisions` (iter-8 + iter-8c, post-Architect-adjudication)

**Reviewer:** golfin-redteam-reviewer (adversarial gate)
**Timestamp:** 2026-06-12 10:38 CEST
**Verdict:** **ARCHITECT_REVIEW_PASS**

I drove the live ai-game-developer MCP myself (Unity running, `unity-mcp` server listening on
:21573 — reached via a direct streamable-HTTP MCP session since the tools were not surfaced in my
function schema this run). Every number and probe below is from MY OWN `tests-run` / `script-execute`
runs, not the cited report.

---

## TL;DR

The two ship-blockers I personally caught in prior rounds are GONE, confirmed by my own live probes:
- **Canopy slow-mo (Cesar's original rejection):** exactly ONE 0.401× cut at canopy entry (y=8.951),
  then natural free-fall. No slow-mo.
- **Airborne stuck-ball (my iter-6 blocker):** the exact PROBE7-A/B configs that froze at y≈2.0/1.4m
  with 14401+ samples now land at finalY=0.0213m with 664–1007 samples.

Sim is provably byte-frozen to the verified iter-7 checkpoint `2fb4c2b7`. The test tightening is
non-vacuous (I traced it). The §9 trunk clip's at-rest payoff (last ~3.5s) reads cleanly as
"ball at rest on the ground at the base of a bare trunk, normal chase camera." Scope is clean bar one
cosmetic Files-table prose nit. I tried three ways to break it and could not.

---

## Step 1 — Live full physics EditMode suite (run by me)

`tests-run testMode=EditMode` (full suite), live:
```
Summary: TotalTests=379  PassedTests=376  FailedTests=0  SkippedTests=3  (Duration 00:00:25.1)
```
- 0 failures. 376/379 — matches the cited numbers EXACTLY.
- 3 skips are the pre-existing `HoleCompleteDriverTests.HoleCompleteDriver_OnInCup…` Stage C1
  `[Ignore]` tests (first skipped name in the Results array), nothing tree-related.

`tests-run testClass=TreeCollisionTests` (live, includePassingTests=true):
```
TotalTests reported context 379 / class 9 PASS / 0 FAIL / 0 SKIP
  TreeCollision_CanopyEntryImpulse_NoSlowMoDescent        PASS  ← tightened test
  TreeCollision_AirborneTrunkDescending_BallReachesGround PASS  ← PROBE7 (my old blocker)
  TreeCollision_CanopyDamp_LandsCloserThanNoTrees         PASS
  TreeCollision_TrunkDeflect_BallDoesNotPassThrough       PASS
  TreeCollision_RollPhase_TrunkDeflectsRollingBall        PASS
  TreeCollision_PuttPhase_TrunkDeflectsRollingBall        PASS
  TreeCollision_Determinism_SameInputSameTree_…           PASS
  TreeCollision_NullProvider_BitExactWithPhase6           PASS
  TreeCollision_AbsentCsv_NoExceptionNullProvider         PASS
```
Both critical tests PASS live. No disagreement with the cited counts.

---

## Step 2 — Live sim re-probe (confirming the Architect's adjudication; NOT demanding changes)

All via `script-execute`, real provider built through `TreeObstacleLoader.LoadInstancesFromText` +
`TreeObstacleProvider.Create`, default profile (trunkR 0.25, trunkH 3, canopyR 3, canopyTop 9,
restit 0.15, damp 0.40), one tree at origin.

### (a) Canopy entry-impulse — Cesar's original rejection: GONE
Config `origin=(0,15,-0.5) vel=(0,-8,0.5)`, vacuum. Per-step velocity-ratio trace:
```
samples=1024  finalY=0.0213
  DROP i=135 y=8.951 ratio=0.401 vy=-5.41   [CANOPY-BAND]   ← the ONE 0.40 entry impulse
  DROP i=355 y=0.030 ...                     [GROUND]        ← ground bounce (post first contact)
  DROP i=527/528/529 y=2.609 ...             [OTHER, post-bounce, low |v|]
  ... (all remaining drops are GROUND or post-bounce apex)
COUNT: canopy-band drops=1   pre-first-ground drops=1
finalT(with)=4.248s   finalT(no)=4.895s
```
EXACTLY ONE damping step in the canopy band (y∈(3,9]), ratio 0.401 ≈ `canopyHitDamping=0.40`. After
the cut the ball free-falls — finalT WITH trees (4.248s) is actually *less* than WITHOUT (4.895s) and
trivially within the 1.5× bound. No per-step re-damp, no slow-mo. Cesar's rejection is decisively
fixed.

### (b) Airborne stuck-ball — MY iter-6 blocker: GONE
The exact PROBE7 configs that I previously broke (and others), Default aero, live:
```
PROBE7-A  (0,6,-6) v(0,-3,12):  finalY=0.0213  samples=723   finalZ=-0.428
PROBE7-B  (0,8,-8) v(0,-5,8):   finalY=0.0213  samples=821   finalZ=-1.238
PROBE-DN  (0,15,-0.5) v(0,-8,0.5): finalY=0.0213 samples=1007 finalZ=-0.372
PROBE-X1  (0,5,-4)  v(0,-4,10):  finalY=0.0213  samples=664   finalZ=-0.652
PROBE-X2  (0,10,-10) v(0,-6,11): finalY=0.0213  samples=763   finalZ=-0.671
```
In iter-6 PROBE7-A/B were STUCK floating at y≈2.03 / 1.38 burning 14401+ samples. Now every config
reaches the ground (finalY=0.0213m ≈ ballRadius) in 664–1007 samples (far below the 14400 cap). The
iter-7 `frac=0` push-out + advance-to-tNext fix works.

### (c) Roll + putt trunk deflect + determinism: hold
```
ROLL:  withZ=-0.248  noZ=1.979   DEFLECTED=True
PUTT:  withZ=-7.335  noZ=43.688  DEFLECTED=True
DETERMINISM: samples 291==291  bitExact=True
```

### (d) Is the tightened test non-vacuous? — yes
`TreeCollisionTests.cs` assertion (b) breaks the scan at the first sample with `y < 0.2`. In my live
trace that first sample is i=355 (y=0.030). The canopy cut is at i=135 — BEFORE i=355 — so it is
inside the scanned window, and it is the ONLY ratio<0.7 drop in that window. The `[OTHER]` drops at
y=2.609 / 0.652 are all at i≥527, i.e. AFTER the first ground contact (post-bounce), so they are never
scanned. The test asserts `dampStepCount==1`, `3.0 < dampY ≤ 9.0` (my trace: 8.951 ✓), and
`0.25 < dampRatio < 0.55` (my trace: 0.401 ✓). It genuinely isolates the single canopy impulse — not
a vacuous pass.

---

## Step 3 — Sim-frozen proof (hard gate)

`git diff 2fb4c2b7 --` (committed checkpoint, real blobs) is **EMPTY** for every forbidden file:
`BallSimulation.cs`, `TreeObstacleData.cs`, `TreeObstacleProvider.cs`, `TreeObstacleLoader.cs`,
`TreeObstacleBaker.cs`, `tree_collision_profiles.csv`, all `Hole_*/tree_obstacles.csv`, and
`PhysicsLab_Hole1.unity`. Also empty for `BotVideoRecorder.cs`, `PhysicsLabController.cs`,
`build_bot_video.py` (committed at the checkpoint, untouched since). Sim is byte-frozen. The
implementer touched ONLY: `TreeCollisionTests.cs` (assertion-b tightening), `Scenarios.cs` (new
`TreeTrunkNormalPlay` scenario), `LoopV2SmokeBotMenu.cs` (+17), `LoopV2SmokeBot.cs` (+4), task docs.

---

## Step 4 — §9 trunk video (Cesar rejected 3×) — my own frame walk

`ffprobe`: 1170×2532, 16.25s, 11.6 MB, h264, 436 frames. I extracted MY OWN frames at
0/40/60/70/80/90/97% (`/tmp/rt_frames/`) and read each — I did not reuse the reviewer's frames.

| t | What I see |
|---|---|
| 40% (6.5s) | Pre-shot establishing chase-cam: ball on the tee on the fairway, looking straight down the hole to the green flag (250 yds), trees lining the left. Completely normal in-game view, ball on ground. |
| 60% (9.75s) | TURN 1, 18% gauge, low chase cam across the ground at a bare trunk lying across the lower frame (the target). Aim/charge moment. |
| 70% (11.37s) | TURN 1, 18% gauge, chase cam tracking low; some foliage in frame as the cam moves toward the trunk. Aiming phase, not the payoff. |
| 80% (13.0s) | **TURN 2** — ball already settled at the base of the bare trunk. |
| 90% (14.62s) | **TURN 2, 0.0 mph** — ball at rest on grass at the base of a large bare-bark trunk; trunk dominates center, no foliage between camera and ball at ball height. |
| 97% (15.76s) | Identical sustained at-rest hold. |

**Camera audit:** the new `TreeTrunkNormalPlay`/`TreeTrunkNormalPlayBody` (Scenarios.cs from ~:1873)
contains the explicit comment "No camera tricks, no Downrange mode, no per-frame camera override" and
grep confirms ZERO `SetMode`/`Downrange`/`SetDownrangeFraming` in its body — the only camera call is
`ctrl.SetCameraYawRadians(yawToTree)` (a normal-play aim parameter). The Downrange code that survives
in the file is in the OLD `TrunkStrikeBody` (the rejected `TreeCollisionGate` path), which iter-8c
does not use. The new scenario has the iter-5 `try { … } finally { restoreCanvases(); d.FlushLog(); }`
canvas restore.

**Geometry corroboration:** target trunk is real — `Hole_01/tree_obstacles.csv` data line 250 is
`MESH_JapaneseBlack_01` at (-132.8786, -53.2395) baseY 6.5204 scale 1.0627, matching the implementer's
idx=247 claim. The tree's base sits at ~6.5m terrain height on the fairway hillside, so the ball's
at-rest y=6.84 is terrain height — NOT a foliage lodge.

**My honest call on the trunk clip: PASS.** The at-rest payoff (the last ~3.5s) unmistakably shows the
ball on the ground at the base of a BARE TRUNK under the NORMAL chase camera — the exact thing Cesar's
"video only shows canopy, no trunk collision" rejection was about. The mid-flight 60–70% frames do
show the chase cam moving low through some foliage, but that is during aiming/flight, is the natural
behavior of the normal chase camera Cesar explicitly demanded, and is bracketed by a clean
establishing frame (40%) and a clean sustained at-rest payoff (80–97%). The clip as a whole reads as
"play normally → travel to a trunk → come to rest at its base against bare bark." Minor cosmetic note
(not a blocker): the ball rests on a small grey tee marker at the trunk base rather than bare grass —
a lab-scene rendering artifact, not the foliage/podium defect.

---

## Step 5 — Scope / Rule 13 / integrity

- `git status --porcelain --untracked-files=all` out-of-folder set = `TreeCollisionTests.cs`,
  `LoopV2SmokeBotMenu.cs`, `LoopV2SmokeBot.cs`, `Scenarios.cs`,
  `Docs/Videos/tree_collision_gate_stageF_buttons.mp4` (27.4 MB, exists). All 5 appear in the
  Files-table. Rule 13 satisfied.
- **MINOR BOOKKEEPING FAIL (non-blocking):** the Files-table prose marks `LoopV2SmokeBotMenu.cs` and
  `LoopV2SmokeBot.cs` "UNCHANGED iter-8c", but `git diff 2fb4c2b7` shows +17 / +4 lines wiring the new
  `tree_trunk_normal_play` menu entry + case branch. Purely additive, in-scope wiring for the declared
  scenario — the changes themselves are legitimate; only the prose is wrong. The self-reviewer and
  reviewer both flagged it. Not a verdict-changer; implementer should correct the prose at close-out.
- `PhysicsLab_Hole1.unity` diff vs checkpoint = 0 lines (byte-identical). `LabScaffold.unity` /
  `ShellScene.unity` — no drift. No forbidden scene mutations despite the heavy ~614-call recording
  session; try/finally canvas-restore held.
- Canonical still `trunk_atrest_iter8c_run10.png` = 1170×2532 (long edge 2532 ≥ 900 — Rule 14 OK).
- Rules 16/17/18 N/A (tree-OBSTACLE bake, not a mesh/terrain deform; no Figma node).

---

## Prior-rejection / prior-blocker replay

| Defect | Source | Verdict (my own live evidence) |
|---|---|---|
| Canopy slow-mo descent | Cesar rejection #1 | **GONE** — single 0.401 cut at y=8.951, free-fall after; finalT with trees < without. |
| Roll/putt trunk tunneling | my round-2 red-team | **GONE** — ROLL/PUTT DEFLECTED=True live. |
| Airborne stuck-ball (float at y≈1.4–2.0) | my iter-6 red-team blocker | **GONE** — PROBE7-A/B land at finalY=0.0213m, samples 723/821. |
| Trunk-clip foliage/podium | Cesar rejection #2 + my iter-6 MARGINAL | **GONE** — at-rest payoff shows ball on ground at bare-trunk base, normal chase cam, zero Downrange code. |

---

## Three break-attempts and why each FAILED to break the work

1. **Visual:** I extracted my own frames across the whole clip looking for any frame where the at-rest
   moment reads as foliage/podium. The aiming frames (60–70%) move low through foliage, but the
   sustained at-rest payoff (80–97%) is clean bare-bark ground contact under the normal camera. Could
   not break it — the legibility gate (the settled frame) is unambiguous.
2. **Geometric / live sim re-run:** I re-fired the exact stuck-ball configs that broke iter-6, plus
   three more descending approaches and the straight-down axis case. Every one now lands
   (finalY=0.0213m, samples << 14400). I re-fired roll/putt — both deflect. I traced the canopy — one
   0.40 cut, no slow-mo. Nothing sits near a threshold. Could not break it.
3. **Spec-intent:** SPEC §D2/D3 intent is "ball hits a trunk and drops nearly dead on the ground;
   canopy = one discrete leaf-strike then normal physics." My probes show both literally satisfied —
   the ball lands at the trunk, the canopy fires once. The tightened test isn't gaming the letter: I
   confirmed by trace that it isolates the real canopy impulse. Could not break it.

---

## Verdict

**ARCHITECT_REVIEW_PASS.** I genuinely tried to break this and could not. The Architect's adjudication
holds under my own live probing: the sim is correct and byte-frozen to `2fb4c2b7`; the test tightening
is a clean, non-vacuous heuristic fix matching the directive exactly; both ship-blockers I previously
caught are gone; roll/putt/determinism/null-bit-exact all hold; the live full EditMode suite is
376/379 with 0 failures (matching the cited numbers); and the §9 trunk clip's at-rest payoff clears
Cesar's bar under the normal chase camera with zero fixed-camera code. The only blemish is a cosmetic
Files-table prose error (two wiring files mislabeled "UNCHANGED"), which is bookkeeping, not scope
drift. Advancing to Cesar's final approval.
