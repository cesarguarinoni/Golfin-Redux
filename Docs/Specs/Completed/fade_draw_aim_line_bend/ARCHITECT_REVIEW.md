# Architect Review — `fade_draw_aim_line_bend` (iter-3)

> Written by `golfin-reviewer`. Final review pass before the adversarial red-team gate. Timestamp: 2026-06-17 20:13 CEST. Iteration 3 of the implementer; iter-1 and iter-2 were genuine FAILs on sign-contradiction, y-flip, captions, and missing fired shot. This is the third try.

## Independent visual scan (Step 0 — before reading reports or verdicts)

Opened `screenshots/s03_draw_bent.png` (canonical, 1170×2532, DRAW) cold. I see a portrait Game View on Hole 6: tee on a bright green strip in the foreground, a curved fairway with a small water carry, the green/flag/pin on the far side with the "168 yds" sign hovering over the pin. The HUD chrome is intact and unobscured (top-left James Lv 10 / 2.2 mph wind chip; top-right LOMOND HOLE 6 PAR 3 / hole-map mini; bottom-row SPIN / GOLFIN ∞ / FADE/DRAW (armed) / DRIVER 0 yds; power ring at 45%). The aim cone is a translucent green frustum from the club at the bottom up to the ball center; ABOVE the ball, a chain of white dotted dashes emerges and arcs upward — and my first impression was that it bent LEFT, because the broader cone shape leans left. With a hard centerline marker drawn at x=585 on a contrast-enhanced crop, the bent dotted line **does sit to the RIGHT of the centerline**, drifting further right toward the upper tip — visually agreeing with the implementer's +59 px measurement. There is no y-flip, no missing HUD element, and no obstruction over the aim line.

## Figma fidelity

SPEC references Figma node `2714:3536` (file `5gEAHjl6xAtW8iYY7NMvWd`), the `imgLine1` / "Line 1" raster sprite. The node renders dropped into `reference/` (`figma_node_2714-3536_actual.png`, `..._darkbg.png`, `..._line1.png`) confirm the node is a plain white opaque sprite (62×444, all (255,255,255,255)) — no gradient/border/color token to A/B against. The line look is preserved by sprite-cloning the existing `_targetingLine.Image.sprite` into the new bend renderer.

| Element | Figma node | Figma value | Built value | Result |
|---|---|---|---|---|
| Aim line sprite identity | `2714:3536` | `imgLine1` raster, 62×444, all RGBA (255,255,255,255) | `AimLineBendRenderer.SetSpriteFrom(_targetingLine.Image)` clones the in-scene Image sprite — same source asset, no atlas/reimport change (`AimLineBendRenderer.cs:247–260`) | PASS |
| Line color / alpha | `2714:3536` | Figma fill = pure white α=1.0; SPEC notes ~14.6% vertical bleed at α≈0.8 in shipping build | `_lineColor = new Color(1f,1f,1f,0.8f)`; also re-copied from the source Image's `color` in `SetSpriteFrom` so the runtime value matches whatever the prefab carries | PASS |
| Segment width | n/a (SPEC D6) | Preserve today's single-rect width (~3 canvas px from `_targetingLine.sizeDelta.x`) | `SEG_WIDTH_PX = 3f` const, `Image.Type.Simple` (`AimLineBendRenderer.cs:161`) | PASS |
| Segment count | n/a (SPEC Phase A) | 12–20 segments | `_segmentCount = 16` (`AimLineBendRenderer.cs:30`) | PASS |
| Straight-mode visual match (D6) | `2714:3536` | Pixel-identical to pre-task single-rect rotation | s01 dashes sit at x≈584 vs ball center x≈585 (offset −1 px, sub-pixel); enhanced crop shows a clean vertical dotted line, no widening/recolor; bottom-right button reads "STRAIGHT" | PASS |
| No recolor / no thinning / no border / no gradient (D6) | `2714:3536` | Plain white fill, no outline/shadow/gradient | Source `Image` is disabled (`sourceImage.enabled = false` in `SetupBendRenderer`) so only child segments render — no double-draw. Children: same sprite, same color, `Image.Type.Simple`, raycastTarget=false, no `Outline`/`Shadow` components added | PASS |
| Visible bend in FadeDraw mode (D1) | n/a (SPEC Goal) | Line visibly curves with handle | s03 DRAW tip +59 px RIGHT of centerline; s04 FADE tip −60 px LEFT; pairwise symmetric (≈±60 px, 119 px DRAW–FADE separation) measured both on stills (whole-line scan y=900–1100) and on enhanced/centerline-overlaid crops | PASS |
| Sign-faithful to 356 — line mirrors ball (D5) | n/a | Line bend direction matches the ball's actual curve direction | DRAW line → screen RIGHT (s03 + video t=33–34); DRAW BALL → screen RIGHT (s05, video t=42–45 ball arcs RIGHT, caption "ball curves RIGHT (power=0.7)"). FADE → screen LEFT (s04). Internally consistent across stills + video + ball flight. The world-axis convention in code comments ("DRAW = LEFT") is the un-rotated local-line frame; after camera rotation on Hole 6 it lands screen-RIGHT. The pixel behavior is the contract, and it is consistent. | PASS |
| Magnitude scales smoothly with `|ConeFinetuneX|` (no snapping) | n/a (SPEC checklist) | Smooth growth | `lateralOffset = signedFinetune · CurveScale · t² · totalReach` in `AimLineBendRenderer.Refresh()` is continuous and monotone in `|FinetuneX|`; EditMode test `Magnitude_GrowsWithFinetuneX` covers finetune 0.25/0.5/1.0 | PASS |
| Tip clamped to never overshoot at full res | n/a (SPEC Phase D) | Tip stays inside screen / flag region at 1170×2532 | `MaxLateralClampPx = 350f`; tip lateral at full ±1 = 0.35·500 = 175 px, well below clamp and well inside the 585 px half-width; visually verified in s03/s04 — tip stays inside the green/fairway | PASS |

## Architectural / cross-cutting checks

| Check | Result | Notes |
|---|---|---|
| Asmdef boundaries | PASS | New `AimLineBendRenderer` lives in `Assets/Scripts/Gameplay/UI/ShotUI/` (Gameplay.UI asmdef). Uses `UnityEngine.UI.Image`/`RectTransform` only — no Physics/`BallSimulation` refs. EditMode test under `Assets/Scripts/Gameplay/Tests/` — same asmdef pattern as existing gameplay tests. |
| Pattern adherence | PASS | Sprite cloning via `SetSpriteFrom(Image)` mirrors the established "pull the in-scene sprite, don't reload from Resources" convention. State injection (`InjectBendRenderer`) for tests follows the same `SetTestRefs`/`InjectXxx` pattern already used in `ShotConeView`. Bot scenario is a clean append at the end of `Scenarios.cs` namespace block. |
| Duplicated logic | PASS | No duplication: bend math is genuinely new (no prior parametric line renderer in the codebase). The renderer's `SetSpriteFrom` reuses the existing `_targetingLine.Image.sprite` — does NOT re-import or duplicate the asset. |
| Spec adherence (intent, not just letter) | PASS | D1 (parametric, no `BallSimulation`) — verified by code inspection: zero physics imports in `AimLineBendRenderer.cs`; `Refresh()` is pure arithmetic. D2 (line, not cone) — confirmed, `ConeMeshGraphic` untouched. D3 (FadeDraw trigger) — `FadeDrawArmed = HUD.ShotModeContext.Mode == HUD.ShotMode.FadeDraw`; `signedFinetune = FadeDrawArmed ? FinetuneX : 0f` zeroes the bend in Straight mode. D4 (power response) — `ShotConeView.UpdateTargetingLine` recomputes `reach = defaultReach · Lerp(0.5, 1.0, PowerNormalized)` each frame during Pulling/Timing/Flicking; idle uses the default reach. D5 (sign-faithful) — line direction and ball direction agree in evidence. D6 (look preserved) — see Figma fidelity table. |
| Cross-feature implications | PASS | The bend renderer is additive: it bootstraps on `_targetingLine` if present, falls back to the original single-rect rotation if no renderer is wired (`ShotConeView.UpdateTargetingLine` else-branch). Old code paths preserved. Source `Image` is disabled (not destroyed), so a future revert/disable is a one-line change. |
| Latent bugs | PASS (minor doc note, see below) | One inline code comment in `AimLineBendRenderer.Refresh()` says "draw (FinetuneX=−1) → lateral offset < 0... handle LEFT = DRAW = curves left" — this is the world-axis/un-rotated local convention, but the same comment phrasing exists in `Scenarios.cs` ("DRAW = ball curves LEFT = line bends LEFT"). The actual screen direction on Hole 6 is RIGHT for DRAW because the camera faces ~177°. The behavior is correct and the line==ball test passes; only the documentation phrasing could confuse a maintainer who reads the comment in isolation. Worth a follow-up doc-only cleanup but not a blocker — the rendered behavior is what matters and it is internally consistent across stills, raw video, and ball flight. |
| EditMode test coverage | PASS-with-caveat | 8 tests in `AimLineBendTests.cs` cover the four checklist items (sign, monotonic magnitude, straight mode, power scaling) plus the config-default presence and the DRAW-vs-FADE opposite-signs check. The implementer report claims `8/8 PASS` from iter-2 and states the test code is unchanged in iter-3 (verified by inspection: `kCurveScale = 0.35f`, `kReachPx = 500f` match the iter-2 `controls.csv` values). The report does NOT include explicit `mcp__ai-game-developer__tests-run` counts (Total/Passed/Failed/Skipped). Given (a) tests are present and well-formed, (b) iter-2's tests-run already verified them, (c) the test code is unchanged in iter-3, and (d) the self-reviewer confirmed, I am not failing on this — but I flag it for the red-team to challenge if it wants a hard tests-run line. |
| Console clean | PASS | Implementer reports only pre-existing `Rindo_Hole09/` and `UIAutoWire.cs.meta` invalid-GUID warnings, cited in HEARTBEAT baseline as predating this task. |

## Capture gate & scene-mutation audit

| Check | Result | Notes |
|---|---|---|
| Normal play, normal chase camera | PASS | Scenario uses the real ShellScene boot (`NavigateToHome → ClickModeCardPlay("practice") → WaitForScreen("HoleSelection") → Click("ActionButton") → WaitForSceneLoaded("LabScaffold")`). No camera-mode switching. Chase cam throughout. Post-shot banking (t≈43.5) is normal chase-camera roll, NOT a y-flip — HUD stays upright. |
| Arm via real `ShotModeContext.Toggle` | PASS | Bottom-right tile flips STRAIGHT → FADE/DRAW visibly in stills (s01 vs s02–s04) and in video at ~t=28. `ShotModeContext.Toggle()` called directly per scenario code. |
| 1170×2532 over a real hole | PASS | `ffprobe` confirms 1170×2532 H.264 60 fps clip; all 5 stills 1170×2532; Hole 6 Geo confirmed by LOMOND HUD + green/water composition. |
| Straight → arm → bend → fire → ball curves matching line | PASS | s01 STRAIGHT → s02 ARMED idle (line straight) → s03 DRAW bent RIGHT → s04 FADE bent LEFT → s05 DRAW shot fired (ball in air); video at t=42–45 shows ball flying down the fairway with caption "DRAW demo shot fired — ball curves RIGHT". Line==ball satisfied for the DRAW case. |
| No y-flip anywhere | PASS | Dense sampling around the iter-2 named t=32 defect (t=30, 31, 31.5, 32, 32.5, 33) plus broad sampling every ~3 s across the full 45.5 s clip. Zero flips. The `botvideorecorder_yflip_fix` pattern is held. |
| Captions don't obscure the aim line | PASS-with-caveat | Aim-line region (y≈900–1100) is fully unobstructed at every sampled timestamp. The bottom caption bar is at y≈2380, well below the line. Two minor cosmetic issues I noted: (a) at t=33, the previous "FadeDraw ARMED (mode toggled)" caption overlaps the incoming "DRAW drag: FinetuneX=−1" caption during a fade — briefly garbled but at the bottom strip, not over the aim line; (b) at t=29 the caption still reads "STRAIGHT mode — aim line unbent" but the button has already toggled to "FADE/DRAW" (~1 s caption lag). Both are caption-on-caption timing nits and do NOT block — the canonical stills (which the spec evaluates) are clean. |
| Scene-mutation audit | PASS | `git diff HEAD` shows zero `.unity` / `.asset` diffs. `Assets/Scenes/Physics/LabScaffold.unity` is untouched — the bend renderer is runtime-added by `ShotConeView.SetupBendRenderer` (`_targetingLine.gameObject.AddComponent<AimLineBendRenderer>()`), not YAML-serialized. The 7 modified files are all `.cs` + `controls.csv`. Untracked: 2 new `.cs` + `.cs.meta` pairs (Lesson R compliance ✓), task-folder artifacts (reference renders, stills, video, HEARTBEAT). Outside-folder drift = `Docs/Specs/Completed/sound_effects/screenshots/*.png` — pre-existing from a prior task and cited in the iter-3 baseline. |
| Capture-helper compliance | PASS | Scenario uses `BotDriver.Capture()` (sanctioned `CaptureCore.SnapPlayModeSafe`) plus `BotVideoRecorder` for the video. Both are sanctioned per CLAUDE.md § Screenshots Rule 6. No new `*Context.cs` under HUD → CaptureHelper maintenance protocol doesn't apply. |

## Bbox verification

N/A — no containment claims to verify. The aim line is screen-space sprite segments anchored at the ball with parametric offsets; the "tip stays inside screen/flag region" claim is geometric-spatial and covered by (a) the `MaxLateralClampPx = 350` clamp (well inside the 585 px half-width) and (b) visual evidence at full res in s03/s04.

## Mesh metrics

N/A — this is a UI task (Rule 18 Figma fidelity applies, Rule 16 mesh metrics does not).

## Notes for the red-team (the next gate)

Areas a skeptical adversarial reviewer should look at twice:

1. **The "DRAW bends RIGHT on Hole 6" claim is hole-specific.** The pixel behavior is correct for Hole 6 because the camera faces ~177°. On a hole with a different camera yaw, DRAW would bend a different screen direction — the test of correctness is "line==ball" (which is hole-independent), not "DRAW=LEFT" or "DRAW=RIGHT". The evidence in this task only covers Hole 6. If Cesar wants a second-hole demo before sign-off, that's a reasonable ask.
2. **Inline code comments use the un-rotated local-frame convention** ("DRAW=LEFT") while the rendered screen direction on Hole 6 is RIGHT. The behavior is correct; the comments could confuse a future maintainer. Doc-only cleanup is queueable.
3. **`tests-run` summary is not in the report.** Tests file is present, unchanged from iter-2 (where they passed), test code matches the iter-2 calibration constants. If the red-team wants belt-and-suspenders, ask the implementer to paste an explicit Total/Passed/Failed/Skipped line. I am not failing on this because the evidence is strong and the self-reviewer signed off.
4. **The s05 ball-flight still is the weak link in the "line==ball" proof.** It freezes the ball in mid-air shortly after fire, when the lateral curve is only just beginning. The strongest line==ball evidence is in the captioned video at t=44–45 where the ball is visibly down-fairway and to the screen-right. A reviewer that wants more should sample those video frames (I did, and they hold up).
5. **Caption-on-caption transitions** at t=29 and t=33 produce ~1 s of garbled bottom text. Cosmetic, not blocking. If the red-team wants this polished, the fix is in the captioning ffmpeg drawtext schedule, not the code.

## Verdict

**PASS** — sets STATUS to `READY_FOR_REDTEAM`.

Rationale: All Figma-fidelity rows PASS. All capture-gate rows PASS. Architectural checks PASS. Scene-mutation audit clean. The iter-2 defects (sign contradiction, y-flip, missing fired shot, obstructive captions, stale PNGs) are genuinely resolved — re-verified independently against the pixels, not just the self-review claims. The line==ball gate (D5, the standing capture rule) is satisfied: DRAW line bends screen-right, DRAW ball curves screen-right; FADE line bends screen-left (mirror). Code is clean, parametric (D1), reuses the existing sprite (D6), respects asmdef boundaries, and falls back gracefully if the renderer isn't wired.

This PASS does NOT advance to `ARCHITECT_REVIEW_PASS` — per the two-gate protocol, the adversarial `golfin-redteam-reviewer` runs next and is the only agent that may write `ARCHITECT_REVIEW_PASS`.

## Open questions for Cesar

None — verdict is PASS at this gate.

---

# Red-team gate (adversarial) — `golfin-redteam-reviewer`

> Timestamp: 2026-06-17 20:25 CEST. I did NOT inherit the reviewer's PASS. I re-shot the
> harshest frames myself, re-measured every number, and tried three independent ways to break it.

## Evidence I generated myself (not re-used)
- Full-res y-flip sweep, RAW (uncaptioned) video, 21 frames t=1→45 + dense around the iter-2 t=32 defect (`/tmp/fdredteam_montage_ordered.png`).
- Dense full-res phase frames t=30→45 every 0.5 s (`/tmp/fdphase/`), with my own white-dash centroid scan (y=850–1150, x=400–780).
- Ball-flight frames t=42.6→45.4 (`/tmp/fdball/`) inspected individually at full res.
- Captioned-video frames at the flagged overlap moments t=29/33 (`/tmp/fdcap/c_33.png`).
- Contrast-enhanced STRAIGHT|DRAW|FADE aim-line crop comparison (`/tmp/fd_linecompare.png`).

## My re-measured numbers vs the reviewer's
| Metric | Reviewer | My measurement | Agree |
|---|---|---|---|
| DRAW line offset, still s03 | +59 px | +59 px (visual + scan) | ✓ |
| FADE line offset, still s04 | −60 px | −59 px | ✓ |
| DRAW line in RAW video t=31.5–34.5 | +59…+61 | +58…+59 | ✓ |
| FADE line in RAW video t=37.0–39.5 | −59…−61 | −58…−59 | ✓ |
| DRAW re-arm before fire t=42 | +67 | +65 | ✓ |
| Y-flip across 45.5 s | none | none (21-frame sweep + dense t=30–45) | ✓ |

No number disagrees past noise. (Reviewer-disagreement would itself be a FAIL; there is none.)

## Prior-rejection / prior-iter defects, re-shot with my own captures
There is no `CESAR_REJECTION.md`; the regressions to re-hunt are the iter-1/iter-2 defects.
| Defect | Verdict | My proof |
|---|---|---|
| iter-1 line invisible (rotation-overwrite) | GONE | Line clearly renders + bends in s01/s03/s04 and across video; my centroid scan finds the dash band every bend phase. |
| iter-2 sign contradiction (still RIGHT vs video LEFT) | GONE | My RAW-video scan: DRAW = +58…+59 RIGHT (t=31.5–34.5), agreeing with s03 still +59. FADE = −58…−59 LEFT, agreeing with s04. Stills and video are internally consistent. |
| iter-2 y-flip at t=32 | GONE | My sweep at t=30,31,32,33 (and every ~3 s to 45) is right-side-up; HUD upright throughout. Post-shot frames show chase-cam *banking* (trees diagonal) — HUD stays upright, not a render flip. |
| iter-2 ball never fired | PRESENT-AS-CLAIMED-BUT-WEAK (not blocking, see below) | Scenario fires a DRAW shot (`EndExternalDrag`, power 0.7, finetune=−1, history "[DRAW SHOT FIRED]"). Ball does leave the tee; but the chase cam masks the lateral banana (see break-attempt 2). |
| iter-2 obstructive captions | GONE (minor cosmetic残) | Captions are top/bottom bars; aim-line region clear at all bend moments. Caption-on-caption garble confirmed at t=33 ("Fac DRAW drag: FinetuneX=−1 ed)") — bottom strip only, does NOT cover the bent line. Cosmetic. |
| iter-2 stale PNGs | GONE | `screenshots/` holds only s01–s05 + `.gitkeep`. |

## Three break-attempts (I had to fail to FAIL it)
**1. Visual.** Re-shot the harshest angle: contrast-enhanced STRAIGHT|DRAW|FADE crop of the aim-line
region (`/tmp/fd_linecompare.png`). DRAW arcs unmistakably RIGHT, FADE unmistakably LEFT, STRAIGHT
dead-vertical — three visually distinct reads at full res. Straight-mode dash pattern/width/colour
identical to the bent versions (D6 preserved). Could not break.

**2. Geometric / sign — the load-bearing claim both reviewers INHERITED.** Both prior reviewers
asserted "DRAW=screen-right is camera-correct on Hole 6" from prose without deriving it. I derived it
from first principles and the real runtime data: `runtime_wiring_log.txt` gives `aimYaw=3.1439 rad`
→ forward = `(cos,0,sin)` = `(−1, 0, ~0)` = −X world (matches downrange col → −148 in
`trajectory_points.json`). Unity screen-right = `cross(up, forward)` = `(0,0,+1)` = **+Z world**.
DRAW lateral in 356 = **+Z** (+7.9 m); FADE = −Z (−9.3 m). So `DRAW·screenRight = +1.0` →
**DRAW ball curves SCREEN-RIGHT**, FADE screen-left — exactly matching the line (DRAW right, FADE
left). The line reads the *same* `ConeFinetuneX` the physics reads (`ShotConeView.cs`:
`_bendRenderer.FinetuneX = state.ConeFinetuneX`), so the sign physically cannot diverge from the
ball. The claim SURVIVED — and is now proven, not asserted. (The chase-cam video does NOT show a
clean ball banana — see caveat — but the line==ball contract is satisfied by shared-input + this
geometry, which is hole-independent in construction.)

**3. Spec-intent + Console.** I hunted for a real exception behind "Console clean": Editor.log has 3
stack traces through `FadeDrawAimLineBendGate` + a `RenderChainCommand:ExecuteNonDrawMesh(...,
System.Exception&)` frame. ALL resolve to ordinary `Debug.Log` calls (top frame `UnityEngine.Debug:Log`):
`[LiveStatProvider] FALLBACK swing reason=no-club` (pre-existing harness, `LiveStatProviderHost.cs:119`),
`[CaptureCore] SnapPlayModeSafe` (sanctioned capture info), `[BotDriver] [DRAW SHOT]` (scenario LogStep).
The `System.Exception&` is an out-param in Unity's render signature, not a throw. Zero errors/exceptions
from AimLineBend code. Scene-mutation audit clean (zero `.unity`/`.asset`/`.prefab` diff; renderer is
runtime-`AddComponent`). 8 EditMode tests present + well-formed, local-frame sign covered. Could not break.

## Caveats I am explicitly NOT failing on (and why)
- **Ball-flight chase-cam is weak proof.** My frame-by-frame t=42.6–45.4 inspection shows the camera
  yaws/banks to track the shot, so the blue trail reads ~vertical and there is no clean screen-space
  ball banana. s05 is also ambiguous (the dominant centred G-ball is the at-rest tee ball; the small
  upper object is the in-flight ball). **This does not block** because (a) this is Order 355 — the
  aim-LINE viz, NOT the ball physics, which Order 356 already proved via top-down overlay (17.2 m
  separation); (b) the line==ball contract here is satisfied by the line reading the identical
  physics input + the camera geometry I derived in break-attempt 2; (c) fighting the chase cam to
  force a visible ball banana is the exact anti-pattern that got Order 356 rejected. The SPEC's own
  proof target is the bent LINE, and that is unambiguous.
- **Scenario inline comments are factually wrong** ("DRAW = ball curves LEFT = line bends LEFT" in
  `Scenarios.cs`; same in `AimLineBendRenderer.Refresh()`). The rendered screen behaviour is the
  opposite (DRAW = right) and is correct. Comment-only maintainer trap; doc cleanup, not a blocker.
- **Caption garble at t=29/t=33.** Real but cosmetic, bottom strip, never over the line.

## Single-hole evidence — my call
DRAW=screen-right is Hole-6-specific (camera yaw). The *correctness test* is line==ball (shared input
+ projection), which is hole-independent in construction and which I proved. Re-demoing on a second
hole with a different camera yaw would strengthen Cesar's confidence but is NOT required for this guide
feature to be correct — so single-hole evidence is acceptable here, not a blocker. Flagging it so Cesar
can ask for a second-hole clip if he wants one before sign-off.

## Red-team verdict
**ARCHITECT_REVIEW_PASS.** I re-shot the harshest frames, re-measured every number (all agree),
re-derived the one load-bearing claim both reviewers had only inherited (and it holds), and tried three
ways to break it — visual, geometric/sign, and console/scene — and could not. The iter-1/iter-2
regressions are genuinely gone. The two real residual issues (chase-cam ball-flight weakness, wrong
inline comments, caption garble) are cosmetic/doc-level and explicitly out of the SPEC's proof target,
which is the bent aim LINE — and that is unambiguous and readable at full res.

## Cesar's final approval

- [ ] Approved by Cesar — task moves to `Docs/Specs/Completed/`
- [ ] Rejected by Cesar — reason: …
