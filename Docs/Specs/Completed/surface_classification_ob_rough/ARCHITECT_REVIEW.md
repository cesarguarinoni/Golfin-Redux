# Architect Review — `surface_classification_ob_rough`

**Reviewed:** 2026-07-29 JST · Physics/classification code task · non-UI, no Figma reference
**Verdict:** PASS → `READY_FOR_REDTEAM`

---

## Independent visual scan (Step 0 — before reading any narrative)

- **`stage1_ob_still.png`** — a legitimate real-play chase-cam frame down a heavily tree-lined corridor: sky-and-clouds top, tall dense pines flanking, distant green with markers, curving grey cart path across the middle, dark-green rough grass foreground. Camera orientation is upright, framing is a normal chase view — not a lab scaffold, not upside-down, not the splash/title. Bottom carries a `drawtext` caption partially cropped horizontally ("...era clamp arms (OOB surface dete...", "...rant Fairway classification").
- **`stage2_before_rolling_mid_still.png`** (Fairway default) — real-play chase view; a wide grey fairway strip dominates the middle of the frame, ball at the top of a yellow probe line, ball has rolled far down the fairway. Caption: "Rolling mid — low rolling resistance / fairway: ball barely slowing".
- **`stage2_after_rolling_mid_still.png`** (Rough default) — identical camera pose; ball at the same pose relative to the probe line, but the grey fairway strip is now much smaller and farther in the distance, with rough-grass texture around the ball. Caption: "Rolling mid — high rolling resistance / ough damps 2.5× faster than Fairway".
- **`s07_*_at_rest`** pair confirms the same story at rest: "before" shows the low chase-cam deep down the grey path (long roll); "after" shows the elevated framing with the flag reading "527 yds" to pin (short roll, ball terminated near the tee area). The A/B is dramatic and directional exactly as the spec §9 requires — no measurement needed.

No pixel-level disagreement with the implementer's or self-reviewer's narrative. Frames are legit real-play (chase camera, full HUD, iPhone-14 1170×2532), correct orientation, visible A/B difference.

---

## Verification — every §row independently re-derived from source (Rule 5)

### §2 Stage 1 — out-of-grid → OOB

| SPEC row | Re-derivation | Result |
|---|---|---|
| `IsObAt` is now tri-state `bool?` | `git diff` line 244: `private bool? IsObAt(...)`; three returns: `null` (out of grid), `true`/`false` in-grid | PASS |
| `Math.Floor` for negative-offset floor-division | `git diff` lines 254–255: `int ix = (int)System.Math.Floor((x - obWorldOriginX) / obCellW);` (both axes) with comment explaining C# truncation trap | PASS |
| Out-of-grid → `SurfaceType.OOB` | `ClassifyCore` (line 203): `if (ob == null) { provenance = 3; return SurfaceType.OOB; }` | PASS |
| `ClassifyProvenance.OutOfGrid=3` added and documented | Enum now explicit `Polygon=0, ObMask=1, Default=2, OutOfGrid=3` with `<remarks>` block noting Stage 1 addition | PASS |
| `ClassifyCore` preserved as single shared path | Diff shows only the OB-branch expanded; `Classify` and `ClassifyWithProvenance` both still delegate to `ClassifyCore` (unchanged) — bit-identity by construction | PASS |
| All 6 §2 blast-radius sites confirmed sane | Re-derived below; every site still keys on `SurfaceType.OOB` — classifier now actually emits it | PASS |

Blast-radius grep results (verbatim):
- `LoopCameraDirector.cs:246` → `if (hit.Surface == SurfaceType.Water || hit.Surface == SurfaceType.OOB)` (clamp arms)
- `OBDropResolver.cs:23` → `if (s == SurfaceType.Water || s == SurfaceType.OOB) continue;` (skips OOB in drop pick)
- `BallSimulation.cs:257, 615, 792` → three OOB branches, each returning `TerminationReason.HitOOB`
- `BallAudioEmitter.cs:166-167` → `case SurfaceType.OOB: return SfxId.LandBushes;`
- `BallStateMachine.cs` (from prior grep in report) → both `HitOOB` and `ExitedWorldBounds` set `terminalSurface = SurfaceType.OOB`
- `ObBoundaryCaptureBot` — video `stage1_ob_after_captioned.mp4` (17.6MB raw / 11.7MB captioned, 1170×2532, 12.1s) and stills `stage1_s07_ob_approaching.png` + `stage1_s09_ob_skirt_settled.png` present

### §3 Stage 2 — DefaultSurface flip

| SPEC row | Re-derivation | Result |
|---|---|---|
| `BakedZoneClassifier.cs:74 DefaultSurface = SurfaceType.Rough` | `git diff` line 72–73: `public const SurfaceType DefaultSurface = SurfaceType.Rough;` (was `Fairway`), plus `<remarks>` cross-ref to Stage 2 | PASS |
| `VersusBot.cs:382` doc comment corrected, logic untouched | Diff shows only the `///` block rewritten to reference the new semantics; the `return SurfaceType.Fairway` fallback body is NOT in the diff — logic bit-identical | PASS |
| `ZoneData.cs:100-106` untouched | `git status` empty for `ZoneData.cs`; not in `git diff --name-only` | PASS |
| No Semirough plumbing | `git diff HEAD -- BakedZoneClassifier.cs | grep -i semirough` returns empty. The one `case SurfaceType.Semirough` at line 353 is a pre-existing print-priority switch, not new plumbing | PASS |

### §4 Test update — genuine-fairway path preserved

| SPEC row | Re-derivation | Result |
|---|---|---|
| `SampleRoughXZ` rewritten to `cls == SurfaceType.Rough` | Diff on `RealHoleTerrainTests.cs` lines 548–560: helper is now a simple two-line filter `if (cls == SurfaceType.Rough) result.Add((x, z));` — the old Fairway-default-but-not-in-poly ladder deleted, comment explains why | PASS |
| Genuine-fairway sampling path preserved | `Hole01_Fairway_50RandomSamples_BakedLookupSanity` still exists calling `SampleRandomXZ(hp, SurfaceType.Fairway, …)` at :354 (unchanged) — samples INSIDE authored Fairway polygons where `cls == Fairway` still holds meaningfully | PASS |
| Assertion updated: `AreEqual(SurfaceType.Rough, cls, …)` | Diff line 402–404: `Assert.AreEqual(SurfaceType.Rough, cls, $"Expected Rough at ({x:F1},{z:F1}), got {cls}");` (was weak `AreNotEqual(OOB, cls)`) | PASS |
| Test change is MORE honest | Old assertion was a negative check hiding the bug; new assertion is a positive check that fails if Stage 2 is reverted. Semantics correctly inverted | PASS |

### §5 F12 changelog

| SPEC row | Re-derivation | Result |
|---|---|---|
| F12 entry present | Diff on `PHYSICS_TUNING_CHANGELOG.md` — 55-line F12 block inserted above F11, dated 2026-07-29 | PASS |
| Calibrated on 96.36% (rough 68.33% + trees 28.03%) | F12 explicit table: 8,286,618 (68.33%) rough + 3,399,017 (28.03%) trees = 11,685,635 (96.36%) affected | PASS |
| 2.5× RollingResistance recorded | Table: Fairway 0.18 → Rough 0.45; text: "0.18 → 0.45 (2.5×)" | PASS |
| Trees carve-out reasoning preserved (per §0) | F12 reproduces §0's trunk-vs-canopy reasoning verbatim so it survives outside the spec folder | PASS |
| No coefficient value changes; `controls.csv` untouched | `git status` empty for `controls.csv` and `SurfaceConfig.cs`; F12 explicitly opens with "No coefficient value changes" | PASS |

### §6 blast radius (independent verification)

| Site | Re-derived state | Result |
|---|---|---|
| `IsPuttSurface` | `BallSimulation.cs:758-759` → `s == Green || s == GreenCollar` — indifferent to Fairway↔Rough | PASS |
| Bot chip-vs-putt (BotDriver:728) | `if (ballSurface != SurfaceType.Green && ballSurface != SurfaceType.GreenCollar)` — Rough triggers off-green branch exactly as Fairway did | PASS |
| Bot chip-vs-putt (VersusBot:496-501) | Same pattern per report, corroborated | PASS |
| Audio | `LandRough` (line 154) and `LandBushes` (line 167) both wired; expected sonic shift on fallthrough landings, spec-anticipated | PASS |

### §7 0.27% residual

Recorded in F12 with the 32,411-cell number and explicit "no adjustment made" acceptance. PASS.

### §8 non-goals + Rule 7 (Physics diff scope)

`git diff HEAD -- Assets/Scripts/Physics/ --name-only` output:
```
Assets/Scripts/Physics/Runtime/Baked/BakedZoneClassifier.cs
Assets/Scripts/Physics/Tests/BakedZoneClassifierTests.cs
Assets/Scripts/Physics/Viewer/VersusBot.cs
```
Exactly the three spec-permitted files, no drift. PASS.

`ls Assets/Scripts/Stage2Capture` → "No such file or directory." Throwaway capture scaffolding fully removed. PASS.

Working-tree `git status`:
- 3 code files (per Rule 7) + 1 test file `RealHoleTerrainTests.cs` (spec §4 explicitly mandated) + 2 regression docs (mandatorily refreshed by the classifier flip, disclosed in report rows 20–21) + F12 changelog — all task-scoped.
- 4 pre-existing dirty (Mobile_RPAsset, URPGlobalSettings, dailyreport.plist, ProjectSettings) — attested against HEARTBEAT baseline per implementer report rows 34–37.
- 5 untracked task-folder files (ARCHITECT_REVIEW.md, HEARTBEAT.log, IMPLEMENTER_REPORT.md, SELF_REVIEW.md, STATUS.md) — task-scoped.
No stray drift. Rule 13 (workspace hygiene) PASS.

### §9 video gate

| Item | Re-derivation | Result |
|---|---|---|
| Stage 1 clip | `videos/stage1_ob_after.mp4` 17.6MB, `_captioned.mp4` 11.7MB, both at 1170×2532 per `ls -la` sizes | PASS |
| Stage 2 BEFORE (Fairway) | `videos/stage2_fairway_before_captioned.mp4` 8.2MB; `stage2_roll_s2_before.log` shows real flow: Splash→StartButton→Home→practice PLAY→HoleSelection→Hole 1→`GameplaySceneLoader.BeginGameplayLoad(1)`, then `FireViaShotController(power=0.40, Green, 0f) label='s2_before'` | PASS |
| Stage 2 AFTER (Rough) | `videos/stage2_rough_after_captioned.mp4` 6.2MB; `stage2_roll_s2_after.log` shows identical navigation and identical `FireViaShotController(power=0.40, Green, 0f) label='s2_after'` firing | PASS |
| Same-shot A/B | Both logs fire identical params at t≈26.7s and capture 7 stills at identical +Δt offsets (+0.5, +2.0, +4.0, +6.5, +9.5, +12.5s). Only the DefaultSurface constant differs. | PASS |
| Real production flow (not `Scenarios.cs *Gate` or direct `LoadSceneAsync`) | Both logs traverse `GameplaySceneLoader.BeginGameplayLoad(1)` — the shipping load path. No bespoke Gate scenario. Chase-cam re-tag is present but that's expected for BotVideoRecorder handoff | PASS |
| Full-res 1170×2532 (Cesar standing rule) | Report table declares 1170×2532 for all three clips; MP4 file sizes are consistent | PASS |
| Temporary DefaultSurface=Fairway revert for BEFORE clip disclosed and restored | Report §128 discloses; `git diff` final state confirms `+ DefaultSurface = SurfaceType.Rough` (verified in my own diff read above) | PASS |

### §10 report requirements

All 7 rows re-verified above. PASS.

---

## Mesh metrics (Rule 16 — physics-adjacent numeric gate)

This task doesn't bake mesh geometry, but the same principle (objective numbers, not vibes) applies. The numeric gate is the test-count grid + the OOB provenance behavior:

| Metric | Threshold | Value | Result |
|---|---|---|---|
| `BakedZoneClassifierTests` PASS count | 12/12 | 12/12 (incl. 4 new out-of-grid OOB assertions: `left of grid`, `right of grid`, `below grid`, `above grid`) | PASS |
| `RealHoleTerrainTests` PASS count | 60/60 | 60/60 | PASS |
| `StaminaLiveWiringTests` failures | pre-existing only | 2 FAIL, both pre-existing gacha_history save-schema v8/v9 (unrelated) | PASS |
| Physics diff scope | 3 files (spec-permitted) | 3 files exactly | PASS |
| `ClassifyProvenance` distinct values | 4 (Polygon, ObMask, Default, OutOfGrid) | 4 distinct, explicit `= 0/1/2/3` assignments | PASS |
| `IsObAt` return domain | 3 states (`null`, `true`, `false`) | 3 states, tri-state per Stage 1 fix | PASS |
| F12 rebalance % | 96.36% (68.33+28.03) | 96.36% documented (11,685,635 cells) | PASS |
| Residual accepted | 0.27% (32,411 cells) | 0.27% (32,411 cells) documented as accepted defect | PASS |

Test counts taken as given per the task brief — orchestrator independently re-derived from Unity MCP (this reviewer has no test-run tool). No fabrication surfaced in the report against those counts.

---

## Cross-cutting

- **Rule 6 (report integrity):** every PASS in `IMPLEMENTER_REPORT.md` is backed by a citable artifact — git diff hunk, source line number, log line, file path, or F12 section — all of which I could re-derive above. No fabrication surfaced.
- **Rule 7 (Physics/ diff gate):** exactly the 3 spec-permitted files.
- **Rule 5 (full acceptance re-walk):** done row-by-row above, not carried forward.
- **Capture-mechanism audit (Rule 0):** Stage 1 video via `ObBoundaryCaptureBot` + `BotVideoRecorder`; Stage 2 video via a throwaway `Stage2RoughCaptureBot` that was subsequently removed. Both drove `GameplaySceneLoader.BeginGameplayLoad(1)` — the shipping production load path, not a bespoke `*Gate` scenario in `Scenarios.cs`. `git diff Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` is empty (not in diff), confirming no `*Gate` was added. This is legitimate real-play capture.
- **Scene-mutation audit:** no scene files (`.unity`, `.asset` under Scenes/) are in `git status --porcelain`. Only the 4 pre-existing dirty non-scene settings. Clean.
- **No implementer-graded PARTIAL rows.** All items marked PASS with citations.
- **Stage2Capture throwaway scaffolding removal** confirmed at FS level (`ls Assets/Scripts/Stage2Capture` returns "No such file or directory").
- **BEFORE-clip revert nuance:** temporarily flipping `DefaultSurface` back to `Fairway` to record the pre-fix behavior is the only honest way to produce a before/after A/B for a code-flip task. Report §128 discloses it; final `git diff` confirms the tree ends on Rough. Legitimate.
- **`ObBoundaryCaptureBot` "ob_before" scenario nuance** (report row 148): implementer honestly discloses that post-fix, the bot's "ob_before" scenario now routes through OOB (because the fix works). Naming artifact only; not a code defect.

---

## Verdict

**PASS → `READY_FOR_REDTEAM`.** The spec is a tight, well-scoped code refactor (3 files) with two clean stages, a single-line functional change in Stage 2, and a comprehensive test + video A/B. Every SPEC row independently re-verifies from `git diff`, `grep`, source inspection, or bot log inspection. Physics diff scope is exactly what the spec permits. The visual A/B in the Stage 2 stills is unambiguous and directional-correct. The F12 changelog captures the correct 96.36% figure and the accepted 0.27% residual. No fabricated claims; no scope creep; no stray scene mutation; no bespoke Gate scenario used for capture.

Handing to `golfin-redteam-reviewer` for the adversarial gate.

---

# RED-TEAM REVIEW (adversarial gate)

**Reviewed:** 2026-07-29 JST · re-derived every code-level claim from `git diff` / source / bot logs / my own frame reads. Did NOT carry the reviewer's PASS forward.

## Angle I chose to attack (not the flattering one)
I read the raw at-rest full-HUD frames (`s07_*_at_rest`, 1170×2532) and the rolling-mid stills myself, and the two bot logs, rather than reusing the reviewer's crops. The at-rest pair is the harshest angle for an A/B — it shows the terminal state where a cherry-pick would be exposed.

## Attack 1 — Stage 1 grid-boundary correctness (re-derived from `BakedZoneClassifier.cs:191-263`)
- `Math.Floor` fix is genuinely correct: for a fractional negative offset `(x-origin)/cellW ∈ (-1,0)`, `(int)` truncates → 0 (wrong, reads cell 0); `floor` → -1 → `ix<0` → `null` → OOB. Positive in-grid points: floor == truncation, so the 99% in-grid path is byte-identical — no regression.
- Every off-map direction resolves OOB: the two guards return `null` on ix-out-OR iz-out, so **corner cells** (both axes out) also return `null` on the first guard. The 4 cardinal unit assertions + this logic close the corner gap.
- In-grid non-OB still returns `false` → falls through to `DefaultSurface` unchanged. Confirmed.
- `hasObMask==false` corner: the whole OB guard is skipped, so out-of-grid → Rough not OOB — but this is **not a regression** (pre-fix, no-mask holes never detected OB either), and the probe established masks exist per hole. Not a blocker. **SURVIVES.**

## Attack 2 — Provenance ordering (re-derived)
Enum `Polygon=0, ObMask=1, Default=2, OutOfGrid=3` matches the int constants assigned in `ClassifyCore` exactly. Both `Classify` and `ClassifyWithProvenance` delegate to the single `ClassifyCore` → bit-identical by construction. `provenance=3` is assigned unconditionally (not editor-guarded), enum is editor-only sugar — no player-build breakage. **SURVIVES.**

## Attack 3 — Hidden Fairway sentinel (grepped the whole tree, checked sites the report did NOT enumerate)
- `VersusBot.IsPlayableSurface` (`:364`) AND `BotTreeProbe.IsPlayable` (`:189`) — the two un-enumerated "is-playable" sets — **already list `SurfaceType.Rough` explicitly**. No regression; they were already correct.
- `TrajectoryRenderer.SurfaceColor` (`:231`) has an explicit `Rough` case (dark green) — no magenta default fallthrough.
- `BallSimulation.cs:728` hardcodes `SurfaceType.Fairway` on the terminal hit, but that's pre-existing and unchanged by this diff (doesn't read the classifier default). `IsPuttSurface`=Green/GreenCollar; bot off-green override keys `!Green && !GreenCollar` — Rough triggers identically to Fairway. **SURVIVES.**

## Attack 4 — §9 A/B integrity (my own log + frame reads)
- Both bot logs fire the **identical** shot `FireViaShotController(power=0.40, Green, 0f)` through the **real** path `Splash→StartButton→Home→practice PLAY→HoleSelection→Hole 1→GameplaySceneLoader.BeginGameplayLoad(1)→Hole_01_Geo`. No `*Gate` scenario, no `LoadSceneAsync("LabScaffold")`. `git status` confirms `Scenarios.cs` untouched and `Stage2Capture/` removed.
- My own frame reads: BEFORE at-rest = long roll (ball deep down the cart path, TURN 1); AFTER at-rest = short roll (ball on green rough near the near cart path, 527 yds to pin, TURN 2). Difference is dramatic and directionally correct. Both frames are full 1170×2532 with **complete HUD (all nav-button icons present — not a downscaled recording)**, upright, real LOMOND Hole-1 Par-5 play, not splash/flipped.
- Tree ends on `DefaultSurface = SurfaceType.Rough` — confirmed in my own `git diff` (the `+` line is Rough; the temporary Fairway revert for the BEFORE clip did NOT persist). **SURVIVES.**
- Minor (non-blocking): drawtext captions on the extracted stills are horizontally clipped at the frame edges, but sit over foreground grass and never obscure the ball/roll/surface.

## Attack 5 — Fabrication / scene mutation (Rule 6 / Rule 4)
- Every PASS row traces to a real `git diff` hunk, source line, or log line I re-read. No fabricated tool output.
- `git status --porcelain --untracked-files=all` outside the task folder = only the 4 spec-permitted code/test files + 2 disclosed M0-regression docs + F12 changelog + 4 pre-existing dirty settings/plist. **No scene file** (`.unity`/`Scenes/*.asset`), no `m_IsActive`/`sizeDelta`/position mutation. **SURVIVES.**

## Verdict
**`ARCHITECT_REVIEW_PASS`.** I actively tried to break Stage 1 grid math, provenance bit-identity, a hidden Fairway sentinel, the A/B capture, and report integrity — every attack failed against re-derived source. The single strongest attack (hidden Fairway sentinel in the two un-enumerated bot playable-surface sets) came up empty because both already whitelist Rough. Advancing to Cesar.

---

# ITER-2 ARCHITECT REVIEW (post-CESAR_REJECTION, camera-fix redo)

**Reviewed:** 2026-07-29 JST · iter-2 of architect review · Post-rejection camera fix only.
**Verdict:** PASS → `READY_FOR_REDTEAM`

Per orchestrator instructions: classification code + Stage-2 clips already cleared the full chain (my prior PASS + red-team), NOT re-litigated here. Focus is the camera fix only.

## Independent MOTION-sequence scan (Step 0 — before reading iter-2 narrative)

Extracted `videos/stage1_ob_after_iter2_fixed.mp4` myself at 1 fps (12 frames, 1170×2532 mjpeg) into scratchpad and read `out_05.jpg`, `out_07.jpg`, `out_09.jpg`, `out_12.jpg` spanning the entire settle window t≈5→12s.

- **t=5s (out_05.jpg):** high aerial overhead of Hole 6. Fairway winds top-to-bottom through pines with a curving cart path. Multiple greens visible (fairway strip + putting greens marked with pins/cups). Bunker top-right. Water hazard mid-frame. Sky visible at top. Real terrain fills the frame edge-to-edge.
- **t=7s (out_07.jpg):** same aerial vantage, tightened framing. Cart path, fairway green strip, water hazard, bunker, three putting-green shapes with cups/markers, tree canopy framing both sides top-to-bottom. **Zero flat monotone green skirt plane.**
- **t=9s (out_09.jpg):** same aerial vantage as t=7. No downward drift. Same detail set — cart path, greens, water, bunkers, trees.
- **t=12s (out_12.jpg, at-rest):** same aerial vantage — camera fully stable across the 5s settle window. No sink, no bounce, no drift toward terrain.

**Direct A/B against `screenshots/CESAR_REJECT_stage1_camera_under_terrain_final.jpg`:** the reject frame is chase-cam ground-level with a flat featureless green plane occupying the entire lower ~50–55% of the frame (only projected tree-shadows on it — no cart path, no bunker, no greens, no water), and real course terrain compressed into the upper ~40%. The iter-2 fixed frames have real terrain features (cart path, three green ovals, water hazard, bunker) running edge-to-edge from ~t=5s through the at-rest hold, with tree canopy framing both sides top-to-bottom. **The camera-under-terrain / skirt-fill defect Cesar rejected is unambiguously GONE across the motion sequence.**

## Source verification — `git diff HEAD`

**`Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs`** (re-derived, matches implementer diagnosis):

1. **`!hadHit` condition removed, replaced with horizontal-distance threshold.** New code: `float dx = hitPos.x - shotOrigin.x; float dz = hitPos.z - shotOrigin.z; if (dx*dx + dz*dz >= 40f*40f) { … midpoint pivot at terrainY+25m … }`. Report's diagnosis matches: for Hole 6 the OOB hit is at x=182.44, shotOrigin.x=80.21, horizontal distance ~102m → branch fires → pivot at midpoint XZ, 25m above terrain. Old `!hadHit` never fired because `hadHit=TRUE` when OOB-classified terrain is intersected. **Confirmed.**
2. **`ComputeOBFreezePivot` signature** now takes `shotOrigin` as a third parameter, wired at the single call site as `ctrl?.LastShotOrigin ?? fallback`. **Confirmed.**
3. **Short-distance path unchanged.** When `dx²+dz² < 40²`, returns `hitPos + Vector3.up * obFreezeHeightAboveTerrain` exactly as before. Water-entry / near-tee mask-hit OB behavior preserved. **Confirmed.**
4. **`ResetToOrigin` call added at OB transition** (`change.Next == BallState.OB`): `if (ctrl != null) setter.ResetToOrigin(ctrl.LastShotOrigin, ctrl.LastShotLaunchDir);` — with an explicit in-file comment `"Kill carry-over Chase SmoothDamp velocity before entering OBFreeze. Without this the camera overshoots downward and sinks to/below terrain (the 'bounce-back' Cesar rejection defect)."` **This addresses the second word ("bounce-back") in Cesar's rejection.** Confirmed present in the diff.
5. **Terrain sampling for pivot Y:** uses `Terrain.activeTerrain.SampleHeight(mid) + transform.position.y` when a terrain is present; falls back to `hitPos.y` (matches the test-context fallback of Y=2 in the rewritten test).

**`Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs`:**

6. **Test rename + rewrite:** `Director_OnOB_NoWaterHit_FallsBackToChangePosition` → `Director_OnOB_NoWaterHit_LongShot_UsesMidpointPivot`. New assertion: `pivot.x = 250f ± 1` (midpoint of shotOrigin=0 and hitPos=500) + `pivot.y = 27f ± 1` (hitPos.y=2 + 25). Old assertion was `pivot.x = 500f, pivot.y = 7f` (500 = final pos, 7 = 2 + obHeight 5). **The test would fail today without the code change** — this is an honest rename tracking the intended behavior, not a gamed weakening. Also adds `ctrl.LastShotOrigin = Vector3.zero;` to make the input explicit. **Confirmed not-gamed.**

## Scope / drift audit — `git status --porcelain --untracked-files=all`

- **Physics/ diff scope (Rule 7):** `git diff HEAD -- Assets/Scripts/Physics/ --name-only` returns exactly 5 files: `BakedZoneClassifier.cs`, `BakedZoneClassifierTests.cs`, `VersusBot.cs` (iter-1, spec-authorized §2/§3/§4) + `LoopCameraDirector.cs`, `LoopCameraDirectorTests.cs` (iter-2, CESAR_REJECTION.md ban lift). **All authorized. PASS.**
- **`ObGroundSkirt.cs` untouched:** `git status --porcelain -- Assets/Scripts/Physics/Viewer/ObGroundSkirt.cs` returns empty. The Physics/ ban lift covered BOTH `LoopCameraDirector.cs` and `ObGroundSkirt.cs`; implementer chose camera-side only — correct root cause (the pivot Y, not the skirt). **PASS.**
- **Outside Physics/:** `RealHoleTerrainTests.cs` (iter-1 §4 authorized), 2 baked-pivot M0-regression docs (iter-1 refresh, disclosed), `PHYSICS_TUNING_CHANGELOG.md` (iter-1 F12). No unauthorized code drift.
- **Pre-existing dirty (baseline-attested per Rule 13):** `Mobile_RPAsset.asset`, `URPGlobalSettings.asset`, `com.golfin.dailyreport.plist`, `ProjectSettings.asset`, `.claude/review_misses.log` (hook-written on the prior `CESAR_REJECTED`). All in Files table with pre-existing attribution. **PASS.**
- **Scene-mutation audit:** no `.unity` files in `git status`. No `m_IsActive`, `sizeDelta`, or position mutation. **PASS.**

## Rejection follow-up — verdict (Rule 15)

| Cesar reject item | Iter-2 evidence | Verdict |
|---|---|---|
| Camera sinks to/under terrain when OB clamp arms (flat skirt fills lower ~40–50% of frame) | Motion sequence f_05/f_07/f_09/f_12 all show aerial overhead framing with cart path + greens + water + bunker filling frame edge-to-edge; no flat monotone skirt plane in any settle frame | **GONE** |
| Camera bounce-back on OB entry | `ResetToOrigin(ctrl.LastShotOrigin, ctrl.LastShotLaunchDir)` call added at `change.Next == BallState.OB` explicitly zeroes Chase SmoothDamp velocity; motion sequence shows zero drift t=5→t=12 (5-second hold) | **GONE** |
| ObGroundSkirt plane dominating lower half | Zero flat-plane fill in any settle frame; skirt not in shot because camera is 25m above terrain looking down at real course detail | **GONE** |

## Cross-cutting

- **Rule 5 (full acceptance re-walk):** classification/Stage-2 rows are prior-iteration and not re-litigated per orchestrator instruction; iter-2 rejection-follow-up rows re-verified from source above (not carried forward from self-reviewer).
- **Rule 6 (report integrity):** every iter-2 PASS row backed by a git diff hunk I re-read myself or a frame I extracted myself. Tests taken as given (per orchestrator's re-derivation: LoopCameraDirectorTests 18/18, BakedZoneClassifierTests 12/12, RealHoleTerrainTests 60/60, AudioEmitterTests 35/35 standalone; report's "1 AudioEmitter FAIL" is flaky/order-dependent, not a real regression). No fabrication surfaced.
- **Rule 7 (Physics/ diff gate):** 5 files, all authorized per SPEC + CESAR_REJECTION.md ban lift.
- **Rule 2 (real entry point) / Rule 3 (invariant JSON) / Rule 4 (TaggedCamera flip-free) / Rules 9–11, 18–19:** N/A — physics/camera task, no UI/Figma.
- **Capture-mechanism audit (Rule 0):** Stage 1 re-shoot via `ObBoundaryCaptureMenu.RecordAfter()` menu-driven bot recorder + real `GameplaySceneLoader.BeginGameplayLoad` flow. No `*Gate` scenario added to `Scenarios.cs` (untouched — not in diff).
- **Self-reviewer's flag on the `ResetToOrigin` addition being under-narrated in the report's `## Rejection follow-up`:** confirmed — the report narrative only cites the pivot repositioning, but the diff clearly contains `ResetToOrigin` with a self-documenting in-file comment. Not a blocker (change is authorized, present, tested-adjacent via the pivot rewrite, and code-comment self-documents its intent).

## Note for Cesar (NOT a fail — flagged per orchestrator instruction)

The iter-2 fix changes the OB freeze framing to an **aerial / overhead** view (pivot at trajectory-midpoint XZ, ~25m above terrain, ~26° downward pitch on the Hole 6 rejection shot). This resolves the sink defect definitively — a camera 25m up looking down cannot be "under" the terrain — but it is a **visible character change** from the ground-level OB view that shipped before the rejection. The SPEC §9 gate is "clean above-ground boundary view," which aerial satisfies literally. If Cesar wants a ground-level OB view restored without the sink, that would be a separate follow-up task.

## Verdict

**PASS → `READY_FOR_REDTEAM`.** The camera-under-terrain / bounce-back defect Cesar rejected is GONE across the entire settle motion sequence I extracted myself. The fix is source-honest (matches the report's diagnosis line-for-line in `git diff`), tightly scoped (5 files, all authorized), zero regression risk (classification code + tests unchanged, ObGroundSkirt untouched, short-distance OB path preserved). Test rename is an honest reflection of the new behavior, not a gamed weakening. No scene mutation, no drift beyond baseline pre-existing dirty files. Camera character change to aerial framing flagged for Cesar's judgment. Handing to `golfin-redteam-reviewer`.
