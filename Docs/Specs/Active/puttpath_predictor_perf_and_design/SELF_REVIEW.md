# SELF_REVIEW — `puttpath_predictor_perf_and_design`

**Reviewer:** golfin-self-reviewer
**Date:** 2026-05-24 06:39 CEST
**Iteration:** N=3 (iter-3 close-out of CESAR_REJECTION rejection #2 dated 2026-05-23)
**Verdict:** `FORWARD_TO_ARCHITECT`

---

## Scope note (post-rejection iteration — full re-walk applies)

Iter-3 is purely additive on top of the iter-2-redirect ARCHITECT_REVIEW_PASS
(`78945f38`). Cesar accepted the warped-grid paradigm and rejected only on
three concrete gaps enumerated in CESAR_REJECTION.md § Rejection 2. The full
reviewer pipeline already adjudicated the paradigm; this review verifies the
three iter-3 gaps are genuinely closed, and audits scene mutations + capture
provenance + screenshot pixels per the standard 8-step protocol.

---

## Visual diff notes (Step 1 — independent pixel scan, screenshot only)

`iter3_warped_grid_hole1_2026-05-24_06-30-58.png` (canonical iter-3 capture):

Portrait-orientation mobile golf view, ~1170px tall. Top third: sky behind
silhouetted leafy trees. Mid-center: a thin red-and-white flag pole atop a
flat green; below the flag, a white golf ball with a green "G" logo sitting
in front of a dark putter head. Top-left HUD: small player portrait card
labeled "JAMES / Lv 10 / TURN 1" with a "0.0 mph" chip and "0 mts" chip
underneath. Top-right HUD: stacked navy bars reading "LOMOND / HOLE 1 -
REGULAR / PAR 5" with a "0 mts" chip below. Top-right corner: small white
circular gear button. Right-center: dark navy circular dial with white text
"0% / 0.0 mts". Bottom-left: small player avatar reading "OGLFM". Bottom-
right: club bag icon labeled "DRIVER 0 mts".

The dominant feature for this review: a **yellow wireframe grid covers the
green around the ball/hole**. Lines are continuous (not dashed), semi-
transparent (the green grass shows through between cells), and arranged as
**flat squares in plan view** — no Y-axis warping, because the green is
flat (Hole 1 Lomond is a flat production green). The grid is bounded by an
approximate circle around the ball location (the `_VisibleRadius`
falloff). The cells are visibly small and uniform. Square plan-view
geometry, yellow color, semi-transparent rendering — all consistent with
the iter-2-redirect render path.

## Step 2 — Comparison to reference image

`reference_pga2k_warped_grid.png` shows: yellow grid lines, square cells
in world-XZ plan view, lines bend with topology, continuous strokes, semi-
transparent over the green polygon.

Iter-3 Hole 1 capture vs reference:
- **Square cells in plan view (L4)** — ✓ both show square cells.
- **Yellow line color** — ✓ matches.
- **Continuous (not dashed) strokes** — ✓ matches.
- **Semi-transparent over green** — ✓ grass texture visible between lines
  in both.
- **Y warp** — N/A. Reference green is sculpted; Hole 1 Lomond green is
  flat. The CESAR_REJECTION.md iter-3 brief explicitly calls this out:
  *"Grid appearing flat-square on a flat production green is expected and
  correct behaviour."* The iter-2-redirect canonical TestGreen capture
  (`iter2_warped_grid_testgreen_canonical_2026-05-23_19-48-51.png`) is the
  PASS evidence for the Y-warp behaviour; that capture was already PASSed
  by the architect at `78945f38`.

Anti-references confirmed NOT present in iter-3 capture: NOT arrows, NOT
contour isolines, NOT screen-space grid, NOT animated beads.

## Bbox verification (Step 6)

N/A — no UI containment claims in this iteration. The grid is a world-
space mesh + shader, not a parented UI hierarchy. Step 6 doesn't apply.

## Scene-mutation audit (Step 7)

`git diff HEAD~1 -- 'Assets/Scenes/Physics/LabScaffold.unity' 'Assets/Scenes/Physics/PhysicsLab_TestGreen.unity'`:

**LabScaffold.unity — CLEAN.** 4-line addition only: `_cellSize: 0.5`,
`_lineWidth: 0.04`, `_lineGlow: 1.5`, `_visibleRadius: 10` appended to
the PutterGreenReader MonoBehaviour block at fileID `1483952040`. Exactly
the documented Ask 1 wiring. No `m_IsActive` flips, no `sizeDelta`
changes, no position shifts. Zero unrelated mutations.

**PhysicsLab_TestGreen.unity — CLEAN with benign URP first-save noise.**
- 4-line SerializeField addition on PutterGreenReader (Ask 1, expected) ✓
- New `UniversalAdditionalLightData` component (29 lines) auto-added by
  URP to the Directional Light on first save. URP auto-companion-component
  bookkeeping, not a behavioural mutation. No GameObject deactivation, no
  transform changes.
- Material `stringTagMap: RenderType: Opaque` and
  `disabledShaderPasses: [MOTIONVECTORS]` and a floating-point precision
  wobble on Color RGB (0.15 → 0.14999998, lossy quantization round-trip
  through Unity's color picker — semantically identical green). URP shader-
  side bookkeeping on first save with new URP version. No surface or
  rendering behaviour change.

Both URP-noise items are first-save artifacts of Unity opening + saving
the scene under URP, NOT the iter-12-style capture-path corruption
(no GameObjects deactivated, no positions moved, no IsActive flips, no
component removals). Verified via:

```
$ git diff HEAD~1 -- 'Assets/Scenes/Physics/PhysicsLab_TestGreen.unity' \
   | grep -E '^[+-]' | grep -E 'm_IsActive|sizeDelta|m_LocalPosition|m_AnchoredPosition'
(no output)
```

Step 7 passes — the only documented changes match the Ask-1 wiring; URP
auto-component noise is non-mutating.

## Capture-helper compliance (Step 5)

1. **Screenshot provenance — PASS.** IMPLEMENTER_REPORT iter-3 section
   explicitly states: *"Capture method: `CaptureCore.SnapPlayModeSafe`
   (the BotDriver's canonical capture path per CLAUDE.md)."* This is the
   sanctioned `CaptureCore` path for long-running coroutines that must
   capture AND continue. Not `ScreenCapture.CaptureScreenshot`, not an OS
   screenshot tool. Compliant with CLAUDE.md § Screenshots Hard Rule 1
   and Lesson 2026-05-13.
2. **Maintenance protocol — N/A.** Iter-3 adds zero new `*Context.cs`
   files under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`. The
   `[SerializeField]` additions to PutterGreenReader are field-level
   metadata changes, not new static-bus contexts. CaptureHelper.cs
   extension not owed.

## Step 8 — Production-flow capture check

**PASS.** The iter-3 canonical capture
(`iter3_warped_grid_hole1_2026-05-24_06-30-58.png`) is from the real Hole 1
production gameplay path — visible HUD elements confirm this:
- "LOMOND / HOLE 1 - REGULAR / PAR 5" hole panel (real Lomond production
  scene, not the synthetic dark TestGreen)
- "JAMES / Lv 10 / TURN 1" player card (real production HUD)
- The 3D environment: leafy trees backdrop, real Lomond green geometry
  with the flag — not the synthetic sculpted sinusoidal TestGreen surface
- The bot's bake report (`baked=1857 cells`) confirms `HoleContext.OnChanged`
  triggered a full bake on the production hole's green polygon classifier

This is the production-flow capture Cesar requested in CESAR_REJECTION.md
iter-3 Ask 2, captured via the canonical `CaptureCore.SnapPlayModeSafe`
path inside the bot's `PutterAimGreenReaderVisible` scenario.

The implementer flagged the bot's `visible=0` as a PARTIAL because the
iter-2 mesh path moves distance culling into the shader fragment (no
C# visible-cell counter); `LastVisibleCellCount` resets to 0 when
`OnShotStateChanged` fires with `isPutterAim=false` during bot cleanup.
The pixel evidence (yellow grid clearly visible around the ball in the
screenshot) is the authoritative gate for "does it render in production
flow?" — and it visibly does. Per the Step 5 lesson on PARTIAL handling,
override-to-PASS is supported here by specific pixel-level reasoning:
yellow grid lines are present on the production green polygon, square
cells visible in plan view, semi-transparent over the grass texture
— the render path is structurally working in production. The
`LastVisibleCellCount=0` is a stale test-seam artifact of the bot
shutdown sequence, not a render defect.

---

## Checklist walk (Step 3 — iter-3 asks from CESAR_REJECTION.md § Rejection 2)

### Ask 1 — Inspector-editable shader params on `PutterGreenReader.cs`

| Sub-item | Implementer | Self-review verdict |
|---|---|---|
| `[SerializeField] private float _cellSize = 0.5f` | PASS | **CONFIRM-PASS** — line 71. Default matches Q-spec. |
| `[SerializeField] private float _lineWidth = 0.04f` | PASS | **CONFIRM-PASS** — line 72. Default matches material asset value. |
| `[SerializeField] private float _lineGlow = 1.5f` | PASS | **CONFIRM-PASS** — line 73. Default matches material asset value. |
| `[SerializeField] private float _visibleRadius = 10.0f` | PASS | **CONFIRM-PASS** — line 74. Default matches Q3 spec. |
| `Update()` pushes all four via MaterialPropertyBlock | PASS | **CONFIRM-PASS** — lines 236, 240–242: `_mpb.SetFloat(_VisibleRadius/_CellSize/_LineWidth/_LineGlow, ...)` alongside the existing `_BallPosition` vector push. Single GetPropertyBlock/SetPropertyBlock pair, correct ordering. |
| `ParseConfig()` no longer overwrites SerializeField fields | PASS | **CONFIRM-PASS** — lines 283–288: `case "CellSize" / "VisibleRadiusMeters" / "LineWidth" / "LineGlow"` all fall through to an explicit no-op `break`, with the comment block at lines 276–282 explaining the intent. `GreenThreshold` / `YellowThreshold` still load from CSV (kept as non-SerializeField config). |
| LabScaffold.unity serializes the values | PASS | **CONFIRM-PASS** — lines 26729–26732 in scene YAML show `_cellSize: 0.5`, `_lineWidth: 0.04`, `_lineGlow: 1.5`, `_visibleRadius: 10`. |
| PhysicsLab_TestGreen.unity serializes the values | PASS | **CONFIRM-PASS** — lines 297–300 in scene YAML show the identical four values. |

**Ask 1 verdict: PASS.** All four fields exist with correct defaults, are
pushed via the existing MPB in Update(), are persisted in both scenes, and
the CSV parser no longer fights the Inspector values.

### Ask 2 — Production-flow capture on Hole 1

| Sub-item | Implementer | Self-review verdict |
|---|---|---|
| Screenshot exists at expected path | PASS | **CONFIRM-PASS** — `screenshots/iter3_warped_grid_hole1_2026-05-24_06-30-58.png` exists (5.35 MB, dated 2026-05-24 06:32). |
| Production scene (Hole 1 Lomond), not synthetic TestGreen | PASS | **CONFIRM-PASS** — visible HUD: "LOMOND / HOLE 1 - REGULAR / PAR 5" + real Lomond environment + leafy tree backdrop. NOT the dark TestGreen synthetic-sinusoidal scene. |
| Production gameplay flow, not manual debug seam | PASS | **CONFIRM-PASS** — capture taken inside the iter-1 architect-approved `PutterAimGreenReaderVisible` smoke-bot scenario on Hole 1; bot drove `ShotController.BeginExternalDrag()` + `IsPutt=true`. No `SetAimActiveForTest` shortcut. Real player card / hole panel / putter HUD visible. |
| Grid actually renders on the green | PASS | **CONFIRM-PASS** — yellow grid lines clearly visible around the ball/hole in the screenshot. `baked=1857` cells from the BotDriver log confirms the bake step ran on production geometry. |
| Flat-square plan view (correct for flat green) | PASS | **CONFIRM-PASS** — explicitly called out as expected per CESAR_REJECTION.md ("Grid appearing flat-square on a flat production green is expected and correct behaviour"). |
| Capture via sanctioned `CaptureCore` method | PASS | **CONFIRM-PASS** — `CaptureCore.SnapPlayModeSafe` (the BotDriver's standard path). Per CLAUDE.md § Screenshots Hard Rule 6, this is the sole sanctioned path. |

**Ask 2 verdict: PASS.** Production-flow capture is real, scene is Hole 1,
flow is the architect-approved smoke-bot scenario, render path is verified
by pixel evidence + bake log.

### Ask 3 — Bot video on Hole 1 with mitigations

| Sub-item | Implementer | Self-review verdict |
|---|---|---|
| Video file exists at expected path | PASS | **CONFIRM-PASS** — `videos/iter3_warped_grid_hole1_2026-05-24_06-34-18.mp4` exists, 1.18 MB. |
| H.264 codec (not HEVC) | PASS | **CONFIRM-PASS** — `ffprobe`: `codec_name=h264`, `profile=Baseline`, `mime_codec_string=avc1.420015`. No HEVC. |
| 540p resolution cap | PASS | **CONFIRM-PASS** — `ffprobe`: 250×540, portrait orientation; 540 is the max-height cap per the mitigation spec. The 250 width follows from the Game View portrait aspect (per implementer note); even-dimension constraint satisfied. |
| 30 fps target | PASS | **CONFIRM-PASS** — `ffprobe`: `avg_frame_rate=32000/1051 ≈ 30.4 fps`, `r_frame_rate=600/1` (container time-base 1/600 yields 30 effective fps after PTS resampling). 640 frames over 21.02 s = 30.45 fps avg. Close enough to 30 — meets the mitigation. |
| BotVideoRecorder updated with mitigation constants | PASS | **CONFIRM-PASS** — `Fps = 30` (line 40, was 60), `MaxHeight = 540` (line 70), even-dimension enforcement (line 76+84), iter-3 mitigation comment block at lines 34–39 documenting all three. |
| No Mac kernel panic this iteration | PASS | **CONFIRM-PASS** — HEARTBEAT.log 2026-05-24T06:35:00 explicitly logs "Step 3 PASS: Bot video completed without Mac panic." No BLOCKER.md present in task folder. Git log shows clean iter-3 commit with no IMPLEMENTER_BLOCKED interlude. The 540p/30fps/H.264 stack worked as the mitigation hypothesis predicted. |
| Hole 1 scene used (not TestGreen) | PASS | **CONFIRM-PASS** — recorded via the same `PutterAimGreenReaderVisible` Hole 1 smoke-bot scenario that produced the screenshot; HEARTBEAT shows both captures in the same Hole 1 bot run window. |

**Ask 3 verdict: PASS.** Video file exists, codec/resolution/framerate all
match the mitigation spec, code mitigations are in place, no kernel panic
occurred. The deferred iter-2 artifact has landed.

---

## Test regression check

IMPLEMENTER_REPORT iter-3 reports `tests-run` on `Golfin.Physics.Tests`:
334 total / 331 passed / 0 failed / 3 skipped — identical to iter-2-redirect.
The 3 skips are the pre-existing `McpToolManager 'ping'` skips unrelated to
this task. No regressions introduced by the SerializeField additions or the
ParseConfig no-op for the 4 CSV keys.

---

## Code diff summary (iter-3 scope only)

| File | Change | Verdict |
|---|---|---|
| `Assets/Scripts/Physics/Viewer/PutterGreenReader.cs` | +28 / −5 lines: 4 SerializeFields + MPB pushes + ParseConfig no-op cases | CLEAN |
| `Assets/Scripts/Physics/Viewer/Bot/Editor/BotVideoRecorder.cs` | +30 / −9 lines: Fps 60→30, 540p cap, even-dim enforcement, mitigation comments | CLEAN |
| `Assets/Scenes/Physics/LabScaffold.unity` | +4 / −0 lines: 4 SerializeField values | CLEAN |
| `Assets/Scenes/Physics/PhysicsLab_TestGreen.unity` | +42 / −4 lines: 4 SerializeField values + URP first-save auto-component noise | CLEAN-with-benign-URP-noise (Step 7 verified) |
| `Docs/Specs/Active/puttpath_predictor_perf_and_design/IMPLEMENTER_REPORT.md` | +75 lines: iter-3 close-out section | n/a (paperwork) |
| `Docs/Specs/Active/puttpath_predictor_perf_and_design/screenshots/iter3_warped_grid_hole1_2026-05-24_06-30-58.png` | new file (5.35 MB) | Ask 2 evidence |
| `Docs/Specs/Active/puttpath_predictor_perf_and_design/videos/iter3_warped_grid_hole1_2026-05-24_06-34-18.mp4` | new file (1.18 MB) | Ask 3 evidence |
| `Docs/Specs/Active/puttpath_predictor_perf_and_design/HEARTBEAT.log` | iter-3 entries appended | n/a (paperwork) |
| `Assets/Plugins/NuGet/*`, `Packages/*` | DLL/manifest drift — out of scope, present in working tree from MCP plugin updates | non-gating |

---

## Verdict

`FORWARD_TO_ARCHITECT` — set STATUS to `SELF_REVIEW_PASS`.

All three CESAR_REJECTION.md iter-3 asks are genuinely closed with real
evidence:
1. Inspector params: 4 SerializeFields present at correct line numbers with
   correct defaults, MPB pushes in Update(), ParseConfig no-op for the CSV
   keys, both scenes serialize the values. Verified via grep + scene YAML
   read.
2. Production-flow Hole 1 capture: real Lomond scene, real bot scenario, real
   `CaptureCore.SnapPlayModeSafe` capture, real bake (1857 cells), grid
   visibly rendering on the production green. Verified by pixel scan + HUD
   inspection + reference comparison.
3. Bot video: file exists at expected path, H.264 codec, 540p resolution
   cap honored, ~30 fps actual, mitigation constants in BotVideoRecorder
   source, no kernel panic. Verified via ffprobe + grep.

Scene-mutation audit is clean (the only Step-7 noise is benign URP first-
save auto-companion components and material RenderType bookkeeping, NOT
GameObject deactivation or transform mutation). Capture provenance is the
sanctioned `CaptureCore` path. No new contexts → no CaptureHelper
maintenance owed. Test suite shows zero regression. The implementer's one
self-flagged PARTIAL (`visible=0` from the bot scenario assertion) is
explained as a known test-seam artifact of the iter-2 shader-cull
architecture, and is overridden to PASS based on the pixel-level evidence
that the grid IS rendering in the screenshot (yellow grid lines visible
on the production green polygon around the ball).

The architect can now adjudicate whether iter-3 closes Cesar's three
gaps. The render-path / paradigm itself was already adjudicated at the
iter-2-redirect ARCHITECT_REVIEW_PASS (`78945f38`); iter-3 only added
the three items above.

---

# Historical — iter-1 self-review (preserved for reference)

> The following is the iter-1 SELF_REVIEW that produced `BACK_TO_IMPLEMENTER`
> on 2026-05-22, before the iter-2-redirect paradigm change. Kept for audit
> trail per CLAUDE.md post-rejection re-walk protocol; superseded by the
> iter-3 verdict above.

**Reviewer:** golfin-self-reviewer
**Date:** 2026-05-22 20:37 CEST
**Iteration:** N=1
**Verdict:** `BACK_TO_IMPLEMENTER`

Iter-1 fails (since-resolved by iter-2-redirect + iter-3):

- Fail #1 — SRP-Batcher opt-out / Frame Debugger evidence missing. Resolved
  by iter-2-redirect's architectural change to a single MeshRenderer +
  custom HLSL shader (one MeshRenderer = one draw call by construction);
  iter-2-redirect architect adjudicated PASS.
- Fail #2 — Smoke-bot scenario `PutterAimGreenReaderVisible` was a no-op
  scaffold. Resolved by iter-3: the scenario now drives `ShotController`
  into putter-aim and produces the canonical Hole 1 capture above (bake
  count = 1857). Note: `LastVisibleCellCount` is a stale test-seam in
  the iter-2 mesh-path architecture (shader does culling, not C#); pixel
  evidence is now the authoritative gate.
- Fail #3 — Capture provenance + production-flow gap. Resolved by iter-3:
  capture method is `CaptureCore.SnapPlayModeSafe`, capture is the real
  Hole 1 gameplay path.
- Fail #4 — No draw-call evidence. Resolved as Fail #1 above; iter-2-redirect
  architect ruled the structural argument sufficient (one MeshRenderer =
  one draw call).
