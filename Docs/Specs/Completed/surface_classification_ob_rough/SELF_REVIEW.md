# Self-Review — `surface_classification_ob_rough`

**Reviewed:** 2026-07-29 JST · iter-1 of self-review · Physics/classification code task (no Figma).

## Verdict

**PASS → `FORWARD_TO_ARCHITECT`**

All Stage 1 + Stage 2 acceptance rows verified against source; before/after visual evidence is unambiguous.

## Visual diff notes (Step 1 — pixel-scan first, before narrative)

- **`stage1_ob_still.png`** — real chase-cam gameplay frame: bright sky top, distant fairway + green + flag mid-horizon, dark evergreens flanking, dense grass mid-ground, ball not visible (implied out-of-frame past terrain edge). Sky-up/ground-down, not upside-down. Definitely not splash/title/menu. Bottom caption is `drawtext`-overlaid ("...era clamp arms (OOB surface det.../...rant Fairway classification") — horizontally clipped by the image crop but present. Frame is a legitimate real-play OOB-approach snapshot.
- **`stage2_before_rolling_mid_still.png`** — chase-cam, gray fairway strip curving away from camera, yellow trajectory line rising from ball position **on/adjacent to the fairway strip**, ball still in mid/late roll. Caption: "Rolling mid — low rolling resistance / fairway: ball barely slowing".
- **`stage2_after_rolling_mid_still.png`** — identical hole, identical camera angle. Ball has settled **visibly short of the gray fairway strip**, resting on the green rough with the fairway strip curving away upper-left. Caption: "...lling mid — high rolling resistance / ...ugh damps 2.5× faster than Fairw...". The stopping-distance difference between before and after is dramatic and visible without measurement — exactly the §9 Stage-2 gate wants.

Bot logs (`stage2_roll_s2_before.log` vs `stage2_roll_s2_after.log`) confirm identical shot parameters: both fire `power=0.40, Green club, label='s2_(before|after)'` and reach the rolling-mid capture at +4.0s after firing. Same-shot A/B, only DefaultSurface differs.

## Acceptance verification (against SPEC §2 through §10)

Independently re-derived from `git diff HEAD` on the 3 named files, ZoneData status, PHYSICS_TUNING_CHANGELOG diff, working-tree `git status`, and orchestrator-provided test results. Every row PASS.

### §2 — Stage 1: out-of-grid → OOB

| Item | Implementer | Self-reviewer | Notes |
|---|---|---|---|
| Shot past terrain edge → `OOB` | PASS (unit) | CONFIRMED | Git diff `BakedZoneClassifier.cs`: `IsObAt` returns `bool?`; `null` case hits `provenance=3; return SurfaceType.OOB` in `ClassifyCore`. `Math.Floor` used for negative-offset correctness (not C# `(int)` truncation). Test file adds 4 out-of-grid assertions with clear "left/right/below/above grid" labels covering all four bounds. |
| Arms camera clamp | PASS (code) | CONFIRMED | `LoopCameraDirector.cs:246` untouched (Rule 7 gate = 3 files only) and already keyed on `SurfaceType.OOB`. Classifier now returns OOB where it previously returned Fairway → clamp arms. |
| Takes penalty path | PASS (code) | CONFIRMED | Same argument — `BallStateMachine`, `BallSimulation` OOB branches untouched and already handle `SurfaceType.OOB`. The classifier flip lights them up. |
| Inside footprint on non-OB unchanged | PASS (unit) | CONFIRMED | Tri-state `false` (in-grid, OB bit clear) still falls to `DefaultSurface` — unchanged code path for the 99%+ of in-grid non-mask cells. Behavior only differs at the grid-boundary defect (the whole point). |
| `ObBoundaryCaptureBot` passes | PASS | CONFIRMED | Video `videos/stage1_ob_after_captioned.mp4` (11.7 MB, 12.1s, 1170×2532) + stills `stage1_s07_ob_approaching.png` + `stage1_s09_ob_skirt_settled.png` in `screenshots/`. `stage1_ob_still.png` is a legit real-play OOB-approach frame. |

Report §148 correctly flags one nuance: the bot's "ob_before" scenario name is now stale — post-Stage-1 both "before" and "after" scenarios route through OOB. Not a defect; a naming artifact. Correctly disclosed rather than silently ignored.

### §3 — Stage 2: DefaultSurface = Rough

| Item | Implementer | Self-reviewer | Notes |
|---|---|---|---|
| `DefaultSurface = SurfaceType.Rough` at line ~74 | PASS | CONFIRMED | Git diff shows exactly the one-line constant change, plus an XML-doc `<remarks>` cross-reference to Stage 2. Clean. |
| `VersusBot.cs:382` doc comment updated | PASS | CONFIRMED | Diff shows the `///` block rewritten to reflect the new semantics: "BakedZoneClassifier.DefaultSurface is now Rough … World-bounds OB IS now detectable via Classify." The `return SurfaceType.Fairway` fallback lines in the method body are NOT in the diff — logic untouched, as spec §3 mandates. |
| `ZoneData.cs:100-106` NOT modified | PASS | CONFIRMED | `git status --porcelain` for `Assets/Scripts/Physics/Runtime/Baked/ZoneData.cs` returns empty. Zero diff. |
| No SemiRough plumbing | PASS | CONFIRMED | `grep -n "SemiRough" BakedZoneClassifier.cs` returns no matches. |

### §4 — Test update

| Item | Implementer | Self-reviewer | Notes |
|---|---|---|---|
| `SampleRoughXZ` rewritten to `cls == SurfaceType.Rough` | PASS | CONFIRMED | Git diff on `RealHoleTerrainTests.cs`: helper is now a two-line rough filter (`if (cls == SurfaceType.Rough) result.Add((x, z));`), the old bug-encoding "Fairway-default-but-not-in-Fairway-poly" ladder is removed. |
| Genuine-fairway sampling path preserved (spec §4 explicit mandate) | not explicit in report | CONFIRMED | `SampleRandomXZ(hp, SurfaceType.Fairway, …)` still exists at line 359 and is called by `Hole01_Fairway_50RandomSamples_BakedLookupSanity` at line 354. That test samples INSIDE authored Fairway polygons (provenance=Polygon), so `cls == SurfaceType.Fairway` still holds and the test still asserts that meaningfully. New sibling test `Hole01_Rough_50RandomSamples_BakedLookupSanity` at line 388 asserts `AreEqual(Rough, cls)` on the rewritten `SampleRoughXZ`. Both assertions are now honest about what they measure. |
| Physics EditMode + Gameplay suites PASS | PASS | CONFIRMED (via orchestrator) | Orchestrator re-derived from primary source: `BakedZoneClassifierTests` 12/12 PASS (including the 4 new out-of-grid assertions), `RealHoleTerrainTests` 60/60 PASS. `StaminaLiveWiringTests` 2 FAIL confirmed pre-existing (save-schema v8/v9 from gacha_history task, unrelated to this diff). |
| Test change makes test more honest | PASS | CONFIRMED | Old assertion was `AreNotEqual(OOB, cls)` on fallthrough-Fairway cells — a weak negative check on cells the bug was hiding as Fairway. New assertion is `AreEqual(Rough, cls)` — a positive check that will fail if Stage 2 is reverted. Semantics correctly inverted. |

### §5 — Difficulty rebalance / PHYSICS_TUNING_CHANGELOG

| Item | Implementer | Self-reviewer | Notes |
|---|---|---|---|
| F12 entry present | PASS | CONFIRMED | `Docs/Physics/PHYSICS_TUNING_CHANGELOG.md` line 9 opens the F12 block; runs ~55 lines. |
| Calibrated on 96.36% (not 68.33%) | PASS | CONFIRMED | F12 includes the full breakdown table: rough+semi 68.33% + trees 28.03% = **96.36%** of the Default bucket. Fairway residual 0.27% and `ob` 0.07% called out separately. §0 trees carve-out (trunk vs canopy, 100× area, 80–90% ground reachable, pine straw = Rough) is reproduced verbatim so the reasoning survives independent of the spec folder. |
| 2.5× RollingResistance shift recorded | PASS | CONFIRMED | Table: Fairway 0.18 → Rough 0.45 = 2.5× on 96.36% of fallthrough ground. |
| `controls.csv` untouched | PASS | CONFIRMED | Not in `git diff --name-only`. |

### §6 — Blast radius

| Site | Implementer | Self-reviewer | Notes |
|---|---|---|---|
| `BallSimulation.IsPuttSurface` | PASS | CONFIRMED | Keyed on `Green || GreenCollar` — indifferent to Fairway↔Rough flip. |
| `BotDriver` / `VersusBot` off-green override | PASS | CONFIRMED | Both key on `!= Green && != GreenCollar` — Rough triggers the off-green branch exactly as Fairway did. |
| Audio (`BallAudioEmitter`) | PASS | CONFIRMED | Rough-landing SFX `LandRough` was already wired; expected sonic change on fallthrough landings, spec-anticipated. |
| `BallStateMachine`, `BallSimulation` OOB, `OBDropResolver`, `LoopCameraDirector` | PASS | CONFIRMED | None in `git diff --name-only`; all already keyed on `SurfaceType.OOB` which the classifier now actually emits. |

### §7 — 0.27% residual

| Item | Implementer | Self-reviewer | Notes |
|---|---|---|---|
| Recorded as accepted known defect | PASS | CONFIRMED | F12 dedicated subsection: "32,411 cells of genuine authored Fairway fall through the polygon lookup due to mesh boundary gaps. Post-flip they play as Rough. That is 0.07% of footprint and is a polygon-gap defect, not a tuning problem. No adjustment made." Spec §7 satisfied verbatim. |

### §8 — Non-goals

| Item | Implementer | Self-reviewer | Notes |
|---|---|---|---|
| No per-cell surface grid | PASS | CONFIRMED | Only `BakedZoneClassifier.cs` structural change; no new grids/arrays/tables. |
| No re-bake / `BakeZoneJsonTool` / `zones.json` change | PASS | CONFIRMED | Not in diff. |
| No SemiRough plumbing | PASS | CONFIRMED | Grep-clean. |
| `ZoneData.cs` untouched | PASS | CONFIRMED | `git status` empty for file. |
| `controls.csv`, `SurfaceConfig.cs`, coefficient files untouched | PASS | CONFIRMED | Not in `git diff --name-only`. |
| No fix for 0.27% residual | PASS | CONFIRMED | No polygon/mask changes. |

### §9 — Video gate

| Item | Implementer | Self-reviewer | Notes |
|---|---|---|---|
| Stage 1 clip: past-edge, clamp arms, penalty | PASS | CONFIRMED | `stage1_ob_after_captioned.mp4` 11.7 MB / 1170×2532 / 12.1s. `stage1_ob_still.png` (extracted) confirmed legit real-play OOB-approach chase-cam frame with drawtext caption — not splash, not upside-down. |
| Stage 2 clip: BEFORE — Fairway default, ball rolls far | PASS | CONFIRMED | `stage2_fairway_before_captioned.mp4` 8.2 MB / 1170×2532 / 12.5s. Extracted still shows ball on/near the fairway strip mid-roll. Report §128 correctly discloses that DefaultSurface was temporarily reverted to Fairway to record the before clip, then restored — final `git diff` confirms the tree ends on `DefaultSurface = SurfaceType.Rough`, so the temporary revert did not persist. |
| Stage 2 clip: AFTER — Rough default, ball stops shorter | PASS | CONFIRMED | `stage2_rough_after_captioned.mp4` 6.2 MB / 1170×2532 / 14.7s. Extracted still shows ball at rest visibly short of the fairway strip. Same-shot params as before (bot logs: `power=0.40, Green club` in both) — clean A/B. |

### §10 — Report requirements

All 7 self-check rows independently satisfied (both stages stated separately, §0 recorded as RESOLVED YES on the merits, `RealHoleTerrainTests` change explained, test-suite result stated with unexpected failures identified, all §2/§6 blast-radius sites confirmed, F12 present, 0.27% residual recorded).

## Cross-cutting rule checks

- **PIPELINE_HARDENING Rule 5** (full acceptance re-walk): done row-by-row above, not just "same as last iter."
- **Rule 6** (report-integrity): every PASS claim tied to a citable artifact — git diff hunk, code line, test count, log line, file path, or F12 section. No fabrication surfaced.
- **Rule 7** (Physics/ diff gate): `git diff HEAD -- Assets/Scripts/Physics/ --name-only` returns exactly `BakedZoneClassifier.cs`, `BakedZoneClassifierTests.cs`, `VersusBot.cs` — the 3 files spec permits. PASS.
- **CLAUDE.md Rule 12 (workspace hygiene):** working tree contains only (a) the 3 named code files, (b) the 2 named docs (F12 changelog + 2 pre-existing regression MDs whose expected outputs shifted because of the classifier flip — noted in report table lines 20–21), (c) the 4 pre-existing DIRTY baseline files (Mobile_RPAsset, URPGlobalSettings, dailyreport.plist, ProjectSettings) attested against HEARTBEAT.log baseline, and (d) the task's own review/status/heartbeat files. No stray drift.
- **Stage2Capture scaffolding removal:** `ls Assets/Scripts/Stage2Capture` returns "No such file or directory" — orchestrator's post-capture cleanup confirmed at the FS level, matching report table lines 29–33.
- **Capture-tool compliance (§ Screenshots rule 0):** captures went through `screenshot-game-view` MCP tool / real bot recorders (`ObBoundaryCaptureBot`, `Stage2RoughCaptureBot`), not hand-rolled `script-execute` reflection into `CaptureCore`. No hook-block risk.
- **No SPEC deviation beyond what's disclosed.** The one flagged deviation (temporarily flipping DefaultSurface to Fairway to record the "before" clip, then restoring) is disclosed in §9 line 128 and independently confirmed reverted by `git diff`.

## Figma fidelity

N/A — this is a physics/classification task; SPEC references no Figma node.

## Routing

**`FORWARD_TO_ARCHITECT`** — hand to `golfin-reviewer` for final review.

## Iteration count

This is iteration **1** of self-review for this task.

---

# Iter-2 Self-Review (post-CESAR_REJECTION, camera-fix redo)

**Reviewed:** 2026-07-29 JST · iter-2 of self-review · Post-rejection camera fix only.

## Verdict (iter-2)

**PASS → `FORWARD_TO_ARCHITECT`**

The camera-under-terrain / bounce-back defect Cesar rejected is GONE across the settle motion sequence. Fix is source-honest and tightly scoped. Classification code + Stage 2 unchanged and not re-litigated per orchestrator instruction.

## Motion sequence check — NOT judging a single still (per orchestrator)

Extracted `videos/stage1_ob_after_iter2_fixed.mp4` at 1 fps (12 frames, 1170×2532). Inspected f_01, f_04, f_07, f_09, f_12 (evenly across the clip and covering the entire settle window t≈7–12 s).

- **f_01 (t=1 s, pre-shot):** ground-level chase-cam view of tee area — normal pre-shot framing, ball on green in foreground.
- **f_04 (t=4 s, aim overlay):** near-top-down aim-line preview — normal pre-shot behaviour, no relevance to OB clamp.
- **f_07 (t=7 s, clamp armed / settle start):** camera has swung to a high overhead aerial view of Hole 6. Trees frame BOTH the left and right edges of the frame all the way from top to bottom. Curved cart path clearly visible with real texture detail. Multiple oval greens/bunkers with dappled tree-shadow detail. Water hazard visible mid-frame. Sky visible at the top. **Zero flat monotone green plane; zero skirt fill.**
- **f_09 (t=9 s, mid-settle):** essentially the same aerial framing as f_07 — camera holds. No sink downward, no bounce-back. Trees still frame both sides bottom-to-top. Same real-terrain detail.
- **f_12 (t=12 s, at-rest):** same aerial framing — stable. Camera did not drift downward across the 5-second settle window.

Direct A/B against `screenshots/CESAR_REJECT_stage1_camera_under_terrain_final.jpg` (the reject): the reject frame has a flat featureless green plane covering the entire lower ~50 % of the frame with only projected tree-*shadows* on it (no ground detail, no cart path, no bunker), and the real course horizon at only ~40 % from top. The fixed frames have real terrain features running edge-to-edge from top to bottom with trees framing the sides — **the sink defect the reject called out is unambiguously GONE**.

## Camera-character change — FLAGGING for Cesar (per orchestrator, not a fail)

The fix does NOT restore the previous ground-level OB-boundary look. It changes the OB freeze framing to an **aerial / overhead** view (pivot at trajectory-midpoint XZ, ~25 m above terrain, ~26° downward pitch on Hole 6). This resolves the rejection defect definitively (no way for the camera to be "under" the terrain when it's 25 m up looking down), but it is a visible character change from what shipped before. Cesar should judge whether the aerial framing is acceptable, or whether he wants a follow-up task to restore a ground-level view without the sink. Not scored as a defect here because the SPEC's Stage 1 gate is "clean above-ground boundary view" — aerial satisfies that literally.

## Source verification — `git diff HEAD`

`Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs`:

1. **`ComputeOBFreezePivot` signature** now takes `shotOrigin` as a third parameter (added at the sole call site with `ctrl?.LastShotOrigin ?? fallback`). Confirmed.
2. **Threshold logic** — replaces the never-firing `!hadHit` condition with `dx*dx + dz*dz >= 40f*40f`. On the Hole 6 rejection shot (`hitPos.x=182.44`, `shotOrigin.x=80.21`) the horizontal distance is ~102 m so the branch fires. Confirmed matches the report's diagnosis.
3. **Mid-point aerial pivot** — `midX = (shotOrigin.x + hitPos.x)*0.5`, `pivot.y = terrainY + 25f` using `Terrain.activeTerrain.SampleHeight(mid)` when available (Editor + play mode; test context falls back to `hitPos.y` = 2). Numbers match report (Hole 6: midX ≈ 131, pivot.y ≈ 38.56).
4. **Short-distance path unchanged** — when the ≥40 m branch doesn't fire, returns `hitPos + Vector3.up * obFreezeHeightAboveTerrain` exactly as before. Confirmed. Water-entry and near-tee mask-hit OB are not perturbed. Backing test `Director_OnOB_FreezesAtFirstWaterHitXZ` still asserts the old path per report.
5. **Undocumented-in-narrative addition (flag):** the same diff adds a `setter.ResetToOrigin(ctrl.LastShotOrigin, ctrl.LastShotLaunchDir)` call at the OB transition, whose in-file comment explicitly cites "kill carry-over Chase SmoothDamp velocity … Cesar rejection 'bounce-back' defect." This is a scoped, sensible addition that addresses the second word ("bounce-back") in the rejection prose. **The implementer report's `## Rejection follow-up` should have named it — it only cites the pivot repositioning.** Not a blocker (change is authorized, in the same file, and the code comment self-documents it), but calling it out for the architect. If asked, I'd want the report edited to add a one-line note about the ResetToOrigin call.

`Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs`:

6. **Test rename + rewrite** — `Director_OnOB_NoWaterHit_FallsBackToChangePosition` → `Director_OnOB_NoWaterHit_LongShot_UsesMidpointPivot`. New assertions: `pivot.x = 250f ± 1` (midpoint of shotOrigin=0, hitPos=500) and `pivot.y = 27f ± 1` (`hitPos.y=2 + 25`). The rename is honest — it reflects the intended new behaviour rather than gaming the old test to pass. Also added an explicit `ctrl.LastShotOrigin = Vector3.zero` line to make the shotOrigin input to the pivot math visible in the test rather than relying on `DirectorFactory` default. Confirmed not-gamed: the pre-refactor assertion (pivot.x = 500, pivot.y = 7) is materially different from the new one — the test would fail today without the code change.

## Scope / drift audit — `git status --porcelain --untracked-files=all`

Physics/ diff: exactly 5 files (`BakedZoneClassifier.cs`, `BakedZoneClassifierTests.cs`, `LoopCameraDirectorTests.cs`, `LoopCameraDirector.cs`, `VersusBot.cs`) — matches the report's Rule 7 gate section, all authorized (3 iter-1 by SPEC, 2 iter-2 by CESAR_REJECTION.md ban lift).

`ObGroundSkirt.cs`: **zero diff** — the Physics/ ban lift explicitly covered both `LoopCameraDirector.cs` and `ObGroundSkirt.cs`; implementer chose to fix camera-side only (correct — root cause was the pivot, not the skirt).

Outside Physics/: `RealHoleTerrainTests.cs` (iter-1 authorized), 2 baked-pivot regression docs (iter-1 authorized), `PHYSICS_TUNING_CHANGELOG.md` (iter-1 authorized F12). All pre-existing-dirty settings/plist/RP-asset are in the implementer's Files table as pre-existing per Rule 13. `.claude/review_misses.log` is the hook-written miss log (pre-existing dirty, expected). No unaccounted-for drift.

## Rejection follow-up verdict

| Cesar reject item | Iter-2 evidence | Verdict |
|---|---|---|
| Camera sinks to/under terrain when OB clamp arms | f_07/f_09/f_12 all show aerial overhead framing 25 m up; trees frame both sides top-to-bottom; real cart path + green ovals + water visible; no monotone green skirt plane | GONE |
| Camera bounce-back on OB entry | `ResetToOrigin` call in the diff explicitly zeroes Chase SmoothDamp velocity at OB transition; no visible drift across f_07 → f_12 (5 s hold) | GONE (implicit — verified from source + motion sequence) |
| ObGroundSkirt plane dominating lower 40 % | Zero flat-plane fill in any settle frame; `ObGroundSkirt.cs` untouched but the skirt is no longer in shot because the camera is 25 m up looking down at real terrain | GONE |

## Tests (per orchestrator's independent re-derivation)

Taking as given: `LoopCameraDirectorTests` 18/18 PASS, `BakedZoneClassifierTests` 12/12, `RealHoleTerrainTests` 60/60, `AudioEmitterTests` 35/35 standalone. The report's "1 AudioEmitter FAIL" was flaky/order-dependent and does not reproduce — noted, not scored.

## Compliance

- **CLAUDE.md § Screenshots rules:** Not a smoke-captured iteration — the deliverable is a bot-recorded gameplay clip (`videos/stage1_ob_after_iter2_fixed.mp4`) captured via the real `ObBoundaryCaptureMenu.RecordAfter()` menu flow, extracted stills in `screenshots/iter2_ob_after_fixed_t09.png` (2532 long-edge, Rule 14 PASS) and `iter2_ob_after_fixed_t12.png`. Compliant.
- **Capture-helper maintenance protocol:** No new `*Context.cs` added under `HUD/`. N/A.
- **Rule 15 (reproduce-the-rejection):** IMPLEMENTER_REPORT.md has a `## Rejection follow-up` section with GONE verdict + same-angle same-timestamp re-shoots (t≈9 s, t≈12 s). Compliant.
- **Rule 5 (re-run entire acceptance list):** Report §2, §3, §4, §5, §6, §7, §8, §9, §10 all re-verified iter-2. Compliant.
- **Rule 6 (report integrity):** Every PASS row in the report is either backed by a git-diff citation, a test-suite result (orchestrator-derived), or a directly-observable screenshot. No fabricated claims spotted. Note the one gap on `ResetToOrigin` (not fabrication, just under-description).
- **PIPELINE_HARDENING Rule 2 (real entry point) / Rule 3 (invariant JSON) / Rule 4 (TaggedCamera flip-free) / Rule 9 (Figma re-pull) / Rules 10–11 (reference diff, clone provenance) / Rule 18–19 (Figma fidelity / clone provenance tables):** N/A — this is a physics/camera task, no UI or Figma.

## Iteration count

This is iteration **2** of self-review for this task (redo iteration after `CESAR_REJECTED`). Verdict below 3-round escalation threshold.

## Verdict (iter-2)

`FORWARD_TO_ARCHITECT` — camera-under-terrain defect resolved across the settle motion sequence, fix is source-honest and scoped, no regression. **Flag for architect / Cesar:** OB framing changed to aerial/overhead; is a visible character change vs. what shipped before. Not scored as a defect (SPEC gate = "clean above-ground boundary view," which aerial satisfies).
