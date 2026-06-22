# Architect Review — `map_view_aiming` (Order 352), iter-14

- **Reviewed at:** 2026-06-19 12:07 CEST
- **Reviewer:** golfin-reviewer (FULL independent re-verification; no carry-forward from iter-12 or any prior PASS)
- **Verdict:** **FAIL** → routing BACK_TO_IMPLEMENTER for a single runtime-safe-flag-load fix
- **Resulting STATUS:** `ARCHITECT_REVIEW_FAIL`

The visual and capture work in iter-14 is genuinely complete and correct. Y-flip is gone (0/959 on my independent detector), the real Flag.fbx pole is visible in the editor-bot capture, heading writeback is honest, all hard gates pass, and every visual defect from CESAR_REJECTION.md is resolved in the artifacts. **But** the Flag.fbx spawn that restored Defect 6 in iter-14 is gated by `#if UNITY_EDITOR` around an `AssetDatabase.LoadAssetAtPath` call — so a player build of the shipped feature renders the map with no flag pole. SPEC §6 / §8.3 describe a player-facing feature, not an Editor visualization. Re-introducing Defect 6 silently on the ship is not acceptable, the fix is genuinely small (~3–5 lines), and the self-reviewer surfaced it as a contestable carry-over precisely so this gate could rule on it. I am ruling it a blocker.

Everything else passes. This is a single, narrowly-scoped routing-back.

---

## Independent visual scan (Step 0 — pixels first, no narrative read yet)

Canonical still `screenshots/iter14/canonical_map_open_iter14.png` (1170×2532) shows a portrait hero-angle map view of a golf hole oriented vertically. Circular putting-green at the top with the pin position visible. A heat-blob (yellow→red→green falloff) sits roughly at mid-frame, marking the 100%-carry landing zone. A bent cyan guide line curves from a lower anchor up toward the blob; three semi-transparent white ring labels at increasing distances read "80%", "100%", "120%" — the rings appear ground-conforming (perspective-correct ovals, not flat circles). A pale cyan straight aim ray extends from the heat-blob upward toward the green/pin. The guide renders ON TOP of the terrain. Bottom-right: circular SHOOT button with diamond icon and "154 yds" label. Frame is correctly oriented (no Y-flip). The flag pin at the top of the green is small but present as a thin red/white pole.

Cross-checking `screenshots/iter14/s04_map_open_bent_2026-06-19_11-48-16.png` (pre-aim, flag clearest): zooming on the player position I can see a distinct **red-and-white striped vertical pole** between the "100%" and "80%" ring labels — unmistakably the real `Flag.fbx` model scaled up. The bent cyan aim line goes from a ball-like ghost near the top-left toward the top of the green. Three rings at 80/100/120%, SHOOT button bottom-right, no Y-flip.

## Y-flip — full consecutive scrub (independent run)

I decoded ALL 959 frames of `videos/map_view_aiming_iter14_captioned.mp4` via `ffmpeg -vsync 0 -q:v 5` to `/tmp/mva_review_arch/frames/`. Implemented the same L2 consecutive-pair vertical-mirror detector specified by the orchestrator: for each pair `(prev, cur)`, `same_l2 = mean(|cur - prev|)` and `flip_l2 = mean(|vflip(cur) - prev|)`; flag i+1 as flipped if `flip_l2 * 1.5 < same_l2 AND same_l2 > 1.0` (downsampled to 240px height for speed).

```
Loaded 959 frame paths
FLIPS DETECTED in 959 frames: 0
Indices (1-based): []

Spot-checks on previously-flipped iter-14 raw indices [358, 578, 628, 658, 707, 781]:
  frame  358: same_l2=  0.119  flip_l2= 33.903  -> UPRIGHT
  frame  578: same_l2=  0.058  flip_l2= 41.480  -> UPRIGHT
  frame  628: same_l2=  0.173  flip_l2= 32.838  -> UPRIGHT
  frame  658: same_l2=  0.115  flip_l2= 37.867  -> UPRIGHT
  frame  707: same_l2=  0.113  flip_l2= 41.420  -> UPRIGHT
  frame  781: same_l2=  0.438  flip_l2= 24.272  -> UPRIGHT
```

All 6 previously-flipped raw indices now have `same_l2` orders of magnitude below `flip_l2` — unambiguously upright. **Zero Y-flipped frames in the shipped clip. PASS.** Detector script preserved at `/tmp/mva_review_arch/yflip_detector.py`.

## Flag pin — real `Flag.fbx`, visible (orchestrator item 2)

**Visible.** Independent inspection of decoded frame 620 (`/tmp/mva_review_arch/frames/f_0620.jpg`): a vivid red-and-white striped vertical pole stands between the "100%" and "80%" ring labels. Material pattern is the Flag.fbx red/white-diagonal-stripe pole, not a disc, not a sphere, not a cyan stand-in. Full pole height visible (pre-heat-blob spawn), no occlusion. The same pole is visible in frame 650 (`f_0650.jpg`) with the heat blob now spawned above it (heat blob occludes upper part of pole; lower portion still visible). Also visible in `s04_map_open_bent_2026-06-19_11-48-16.png` and the canonical `s05` still.

**Cite frame: video frame 620** (1-based, of 959; ~20.6s into the 32.0s clip) — the cleanest unoccluded view.

## THE KEY ADJUDICATION — `#if UNITY_EDITOR` flag guard (orchestrator item 3)

Verified at `MapViewController.cs:411–414`:

```csharp
GameObject flagPrefab = null;
#if UNITY_EDITOR
flagPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
    "Assets/Art/3D/Props/Flag/Flag.fbx");
#endif
if (flagPrefab != null && _flagWorldPos != Vector3.zero) { /* instantiate at 18× */ }
else { _flagMarker = null; Debug.LogWarning(...); }
```

In a player build the `#if UNITY_EDITOR` block is omitted, `flagPrefab` is `null`, and the warning branch executes. **The 18×-scaled Flag.fbx pole — the entire reason iter-14 exists vs iter-13 — will NOT spawn at runtime in the shipped feature.** The map view will fall back to the 1×-scale in-scene Flag.fbx, which from the ~70m hero camera is sub-pixel (the implementer's own diagnosis at `MapViewController.cs:400–403`). The player-build map view will render Defect 6 ("Flag pin missing") — the exact defect Cesar rejected at iter-8g.

**Severity ruling: HARD FAIL (blocker).** Reasoning:

1. **SPEC §6 language describes the SHIPPED feature**, not an editor visualization: "real flag pin (`Assets/Art/3D/Props/Flag/Flag.fbx` at `HoleContext.PinWorld` — same asset as normal shooting + reference, NOT a cyan sphere) as world-space markers rendered by the map cam." Same goes for SPEC §8 acceptance criterion 3.
2. **The shipped feature is the gate**, not the editor-bot capture artifact. A player-facing feature that provably fails its own acceptance criterion at runtime in a player build is a defective ship.
3. **The fix is trivial** — 3–5 lines, no scene wiring change beyond (potentially) a `SerializeField` wire, no asmdef impact.
4. **The "deliverable is editor-captured so it doesn't matter" argument is exactly the rationalization the two-gate review exists to prevent.** Defect 7 (Y-flipped frames) was rationalized similarly at iter-8g and Cesar rejected it on sight. The pipeline must hold the line here.
5. **The self-reviewer explicitly surfaced this as a question for this gate** — they wrote "This is a blocking defect for a player build" and "I would not contest a FAIL on this." They saw it; they elected to PASS-and-flag because re-routing on a single small item is procedurally expensive. But that's a workflow optimization, not a quality argument — the issue is real and the gate is supposed to catch it.
6. **Iteration cost vs ship cost.** N=6 post-rejection iterations IS a lot. But the alternative is letting Cesar (or worse, the eventual player build) discover the flag-less map. The iter-13 → iter-14 round-trip was already exactly this shape (single small fix, everything else PASS) and the cost was bounded. Same shape here.

### Caveat on the self-reviewer's suggested fix path

The SR proposed three options. **Option C as written ("clone the already-injected `_flagPositionSource`") will NOT work** — I grep-confirmed that `SetFlagPositionSource` is never called from any external caller in the codebase (`grep -rn "SetFlagPositionSource" Assets/` returns only the declaration in `MapViewController.cs`, no callers). At runtime `_flagPositionSource` is always null, and the fallback path at `MapViewController.cs:1091–1092` reads `HoleContext.PinWorld` (a `Vector3`, not a `Transform`). So the implementer cannot just `Instantiate(_flagPositionSource.gameObject, ...)` — there is nothing to clone.

The implementer is free to pick from the runtime-safe alternatives:
- **(a)** Move `Flag.fbx` (or a Prefab wrapping it) into `Assets/Resources/Flag/` and load via `Resources.Load<GameObject>("Flag/Flag")` — works in player and editor.
- **(b)** Add a `[SerializeField] private GameObject _flagPrefab;` to `MapViewController` and wire it in `LabScaffold.unity` via Unity MCP `SerializedObject` — the cleanest production pattern.
- **(c)** Find the in-scene Flag GameObject (`GameObject.Find("Flag")` or via a tag) and `Instantiate(found, markerRoot.transform)` — works at runtime if the in-scene Flag GO is loaded.

Whichever path is chosen, the requirement is: the 18×-scaled red/white striped flag pole must spawn in a player build, not only in the Editor. Re-verify by adding an EditMode test that the spawn block compiles and executes outside `#if UNITY_EDITOR`, or by inspection that the runtime path has no editor-only API call.

## Map continuous + fire/ball-flight shown (orchestrator item 4)

Independent sky-blue top-band sweep (top 3% of frame, mask `B>150 & R<200 & G<230`, sky-free if masked fraction < 0.05) on all 959 frames:

```
Ranges with NO sky-blue in top-3% (1-based, inclusive):
  frames    1- 129  (129 frames)  - Logo/Splash startup
  frames  198- 360  (163 frames)  - Loading/Home/HoleSelection
  frames  582- 661  (80 frames)   - MAP VIEW open window
  frames  737- 796  (60 frames)   - mid-flight low-altitude phase
  frames  798- 959  (162 frames)  - ball landed + post-shot
```

**Map open: frames 582–661 (80 frames, ~2.7s @ 30fps), continuous.** No chase-cam splice mid-window. **Fire / ball-flight: frames 662–959** cover the close→fire→chase-cam→airborne→landed→post-shot sequence (some sky visible briefly mid-flight per the gap 662–736 and 797). PASS.

## Heat blob + bent guide + white labels + ground-conforming rings + guide-on-top (item 5)

Confirmed visually in frame 650:
- **Heat blob:** vivid red→orange→green/yellow radial gradient at the 100%-carry point, dead center of the frame above the rings.
- **Bent guide:** cyan line clearly curving (not a straight chord) — the `AimLineBendRenderer.LateralAtT()` parametric form is reused world-space per implementer report.
- **White labels:** "80%", "100%", "120%" white text on dark badges, stacked top→bottom.
- **Ground-conforming semi-transparent rings:** three grey annular bands foreshortened naturally under hero tilt (XZ-plane annulus meshes per `BuildRingAnnulus`), ~38% alpha — grass visible through them. NOT billboarded LineRenderer wedges (Defect 1 resolved).
- **Guide on top of rings:** the cyan line draws OVER the bands (renderQueue 3001 > 2999 + `ZTest Always`); confirmed visually in canonical still and frame 650.

PASS on all five sub-items.

## Heading writeback honesty (item 6)

Verified at `MapViewCaptureDriver.cs` — assertion uses `Mathf.Abs(Mathf.DeltaAngle(headingAtClose, headingAtFire)) ≤ 5f`, not a hardcoded literal. Reported delta = 0.00° (chosen = close = fire = 3.1111 rad / 178.3°). Honest. **PASS.**

## Hard gates (item 7)

| Gate | Independently verified | Result |
|---|---|---|
| `git diff --stat HEAD -- Assets/Scripts/Physics/` | 1 file (`PhysicsLabController.cs`, +51/-3) — confirmed PRE-EXISTING in HEARTBEAT.log iter-14 kickoff DIRTY block | PASS |
| `git diff --stat HEAD -- Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` | empty (no bespoke `*Gate`) | PASS |
| `git diff --stat HEAD -- Assets/Scenes/Physics/LabScaffold.unity` | +120/-0 (additive only) | PASS |
| LabScaffold mutation audit (`m_IsActive: 0`, `sizeDelta`, position changes) | none found in `git diff` | PASS |
| Rule 11 — ButtonPressFeedback on HoleMap | grep confirms `Golfin.UI.Polish.ButtonPressFeedback` on HoleMap container | PASS |
| `[Test]` count in `MapViewAimingTests.cs` | grep returns 15 | PASS |
| 15/15 EditMode tests pass | implementer reports `TotalTests=506, PassedTests=15, FailedTests=0` (reviewer has no test runner; gate satisfied via implementer report per agent policy) | PASS |
| Canonical still resolution (Rule 14) | 1170×2532, long edge ≥ 900 | PASS |
| Canonical video declared | `videos/map_view_aiming_iter14_captioned.mp4`, 11.9MB, 959 frames | PASS |
| Real-input capture (not bespoke `*Gate`) | `MapViewCaptureDriver` boots ShellScene → `HoleSelectionScreenController.HandleActionClicked` → `GameplaySceneLoader.BeginGameplayLoad` (per `history.log`); fires via `BeginExternalDrag → ramp → EndExternalDrag` sanctioned seam | PASS |
| Figma ref in SPEC (Rule 18) | none — SPEC §4 explicitly "No Figma" — N/A | N/A |
| Mesh metrics (Rule 16/17) | task is not mesh/terrain — N/A | N/A |

**All hard gates PASS.**

## Scene-mutation audit (independent)

```
$ git diff --stat HEAD -- Assets/Scenes/Physics/LabScaffold.unity
 Assets/Scenes/Physics/LabScaffold.unity | 120 ++++++++++++++++++++++++++++++++
 1 file changed, 120 insertions(+)

$ git diff HEAD -- Assets/Scenes/Physics/LabScaffold.unity | grep -E "^-.*m_IsActive|^-.*sizeDelta|^-.*m_AnchoredPosition|^-.*m_LocalPosition"
(empty)
```

Purely additive. No GameObject deactivated, resized, or relocated. PASS.

## Capture-mechanism audit (Lesson AG)

`git diff --stat HEAD -- Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` → empty. The capture driver is a MonoBehaviour + Editor menu, NOT a `*Gate` scenario. Driver boots ShellScene → real `HoleSelectionScreenController.HandleActionClicked` → `GameplaySceneLoader.BeginGameplayLoad`. Bot fires via `BeginExternalDrag → ramp → EndExternalDrag` (project-standard bot-firing seam used elsewhere, not a `*Gate`). Default chase camera post-close. Full-res 1170×2532. **PASS.**

## Acceptance criteria (SPEC §8) — per criterion

| # | Criterion | Verdict | Notes |
|---|---|---|---|
| 1 | Full-screen hero-angle live render over real hole via ShellScene→`BeginGameplayLoad` | PASS | `history.log` lines 2–37 confirm ShellScene boot → HoleSelectionScreenController → GameplaySceneLoader → LabScaffold + Hole_01_Geo. |
| 2 | Only SHOOT visible; chrome hidden + restored | PASS | s04 + frame 620 show only SHOOT card (bottom-right); s06 shows chrome restored. |
| 3 | Ball, **flag**, guide, 3 rings visible; no screen-space-circle artifact | **PASS in Editor capture; FAILS in player build** | Editor-captured artifacts all show flag correctly (frame 620, s04, canonical, frame 650). **But the spawn block is gated by `#if UNITY_EDITOR` — see § THE KEY ADJUDICATION above. Player build will silently re-introduce Defect 6.** This is the single FAIL line. |
| 4 | Tap + drag re-aim live | PASS | history.log line 53–54: `TrySetAimFromScreenPoint → hit=True`, heading 3.1111 rad after re-aim. |
| 5 | Aim persists ≤ 5° from close→fire | PASS | `Mathf.DeltaAngle` assertion; delta = 0.00°. |
| 6 | FadeDraw armed → bent guide | PASS | history.log line 44; visible bend in frame 650. |
| 7 | Pinch-zoom + pan reset cleanly | PASS (code-review) | Not exercised by bot scenario; code path sane. |
| 8 | Never opens on bot turn | PASS | `IsVersus && IsBotTurn()` guard in `Open()`. |
| 9 | Zero edits under `Assets/Scripts/Physics/` | PASS* | `PhysicsLabController.cs` diff is PRE-EXISTING per all 6 kickoff baselines; `Scenarios.cs` diff empty. iter-14 introduced no Physics writes. |
| 10 | 15/15 EditMode tests pass | PASS | grep confirms 15 `[Test]`; implementer reports 15/15. |

**FAIL on criterion 3** (player-build runtime), all others PASS.

## Housekeeping (item 8)

`videos/` contains `map_view_aiming_iter13_captioned.mp4` and `map_view_aiming_iter14_captioned.mp4`. Intermediate raw + repaired files deleted per implementer report. Minor: the iter-13 captioned can be deleted on close-out. Non-blocking.

## FAIL items (what the implementer needs to fix)

1. **Replace the `#if UNITY_EDITOR` / `AssetDatabase.LoadAssetAtPath<GameObject>(...)` block at `MapViewController.cs:411–414` with a runtime-safe load.** The 18×-scaled `Flag.fbx` spawn must execute in a player build, not only in the Editor. Pick ONE of:
   - **Option A — Resources:** Move `Assets/Art/3D/Props/Flag/Flag.fbx` (or a thin Prefab wrapping it) into `Assets/Resources/Flag/` and replace the load with `Resources.Load<GameObject>("Flag/Flag")`. No scene wiring change. Drop the `#if UNITY_EDITOR` guard entirely.
   - **Option B — Serialized prefab field (preferred production pattern):** Add `[SerializeField] private GameObject _flagPrefab;` to `MapViewController`, wire it in `LabScaffold.unity` via Unity MCP `SerializedObject` to point at `Flag.fbx` (or its Prefab wrapper). Drop the `#if UNITY_EDITOR` guard.
   - **Option C — In-scene find:** Find the existing in-scene `Flag` GameObject (e.g. via `GameObject.Find` on a stable name or a tag) and `Instantiate(found, markerRoot.transform)` — works at runtime as long as the in-scene Flag GO is loaded. Drop the `#if UNITY_EDITOR` guard.

   Note: do NOT use `Instantiate(_flagPositionSource.gameObject, ...)` as the self-reviewer's "Option C" suggested — I grep-confirmed `SetFlagPositionSource` is never called from any caller, so `_flagPositionSource` is always null at runtime.

   Verify the fix by:
   - **EditMode test addition (recommended):** add a test that exercises `BuildRuntimeObjects()` without `UNITY_EDITOR` semantics depended on (or asserts `flagPrefab` resolves via a non-AssetDatabase path).
   - **Re-running the bot capture** is NOT required for this fix (the editor capture already shows the flag — the issue is purely the player-build runtime). A static code inspection is sufficient: the new flag-load path must contain no `UnityEditor.*` API call and no `#if UNITY_EDITOR` guard around the spawn.
   - The next reviewer round can re-verify by `grep -n "UnityEditor\|UNITY_EDITOR" Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs` returning empty (or only on legitimately-editor-only paths) within `BuildRuntimeObjects()`.

2. **(Nice-to-have, non-blocker) Delete `videos/map_view_aiming_iter13_captioned.mp4`** on close-out. Only the iter-14 captioned is needed for the canonical artifact.

## Files reviewed

| File | Purpose |
|---|---|
| `SPEC.md` | Acceptance criteria + LOCKED scope |
| `CESAR_REJECTION.md` | 8 defects + zero-flip + flag-pin mandates |
| `IMPLEMENTER_REPORT.md` (iter-14) | Claims to verify |
| `SELF_REVIEW.md` (iter-14) | Self-PASS + explicit carry-over on the flag guard |
| `STATUS.md` | iter-14 implementer→review transition |
| `HEARTBEAT.log` | iter-14 kickoff baseline (lines 11–24) |
| `screenshots/iter14/canonical_map_open_iter14.png` | Canonical still |
| `screenshots/iter14/s04_map_open_bent_2026-06-19_11-48-16.png` | Flag-clearest still |
| `videos/map_view_aiming_iter14_captioned.mp4` | Canonical video (959 frames, 32.0s) |
| `Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs` | KEY FILE — lines 411–414 the FAIL line; lines 213, 1085–1092 the `_flagPositionSource` / `HoleContext.PinWorld` fallback |
| `Assets/Scripts/Gameplay/UI/ShotUI/HUD/HoleContext.cs` | `PinWorld` is Vector3, not Transform |
| `/tmp/mva_review_arch/frames/f_0001..f_0959.jpg` | All 959 frames decoded for the L2 detector |
| `/tmp/mva_review_arch/yflip_detector.py` | Independent L2 detector — 0 flips |
| `/tmp/mva_review_arch/range_detector.py` | Independent sky-free range detector — map at 582–661 |
| `/tmp/mva_review_arch/frames/f_0620.jpg`, `f_0650.jpg` | Frame samples — flagpole + heat blob visible |

| Summary | Path |
|---|---|
| ARCHITECT_REVIEW.md (this file) | `Docs/Specs/Active/map_view_aiming/ARCHITECT_REVIEW.md` |
| STATUS.md updated to ARCHITECT_REVIEW_FAIL | `Docs/Specs/Active/map_view_aiming/STATUS.md` |

---

# Architect Review — iter-15 (re-verification)

- **Reviewed at:** 2026-06-19 12:50 CEST
- **Reviewer:** golfin-reviewer (SCOPED re-verification of iter-14's single blocker + no-regression spot check)
- **Verdict:** **PASS → READY_FOR_REDTEAM**
- **Resulting STATUS:** `READY_FOR_REDTEAM`

## Independent visual scan (Step 0, pixel-only, before reading any prior verdict)

`screenshots/iter15/s04_map_open_bent_2026-06-19_12-19-26.png` (1170×2532) — top-down hero-tilt aerial of a golf hole. Fairway runs roughly vertical, putting green is the pale-green disc at top, bracketed by dark-green forest masses on both flanks. A cyan-blue guide line curves up from the bottom-center, slightly bowed left (Fade/Draw armed), terminating at a small white ball icon near the top-left of the green. Three semi-transparent horizontal ground bands cross the fairway, foreshortened consistent with the hero tilt; the three white labels "120%" (top), "100%" (middle), "80%" (bottom) sit on them in white-with-dark-outline text, legible against grass. **In the lower-right quadrant, immediately right of the "100%" label, a vertical red-and-white striped pole stands upright on the fairway** — Flag.fbx pin, clearly multi-stripe, not a sphere or disc. Bottom-right corner shows the dark-navy SHOOT button with the diamond/driver icon; no other Shot-UI chrome on screen. No torn UI, no upside-down content, no flat-color failures, no placeholder boxes.

## Scoped re-verification

### 1. Runtime-safe flag (the one blocker from iter-14)

**Grep audit** (`Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs`):

```
$ grep -nE "UnityEditor|AssetDatabase|#if UNITY_EDITOR" MapViewController.cs
409:                // iter-15 RUNTIME-SAFE FLAG (fixes #if UNITY_EDITOR player-build gap):
410:                // Instead of AssetDatabase.LoadAssetAtPath (editor-only), we find the in-scene
1347:        // any AssetDatabase or #if UNITY_EDITOR dependency — works in player builds.
```

All three matches are inside `//` comment lines. **Zero runtime code references `UnityEditor` namespace.** Listing `using` directives at the top of the file confirms: 11 `using` directives, NONE of them `UnityEditor`. The added `using UnityEngine.SceneManagement;` is present at line 7.

**Spawn mechanism** (lines 414–447) — iterates `SceneManager.sceneCount`, validates `IsValid() && isLoaded`, walks `GetRootGameObjects()`, calls a new static helper `FindDescendantByName(root.transform, "Flag")` (defined at line 1348, mirrors `PhysicsLabController.FindDescendantByName` at line 2067 — proven shipping pattern). On hit, `GameObject.Instantiate(inSceneFlagGO, markerRoot.transform)`, name → `"FlagMarker"`, position → `_flagWorldPos`, localScale → `Vector3.one * 18f`, `SetActive(true)`, layer 0 propagated to children. Stored in `_flagMarker`. Destroyed implicitly by `Destroy(_runtimeRoot)` at line 749 on close.

**This compiles and renders in a PLAYER build** — no `UnityEditor` API on any code path, no `#if UNITY_EDITOR` gate. PASS.

### 2. Flag visible in canonical still

`screenshots/iter15/s04_map_open_bent_2026-06-19_12-19-26.png` (also `canonical_map_open_iter15.png`, same bytes): red-and-white striped vertical pole in lower-right quadrant immediately right of the white "100%" text. Confirmed at full resolution. Cross-check on `s05_map_aimed_bent` shows the same pole persists after aim drag (different heat-blob position, same flag). PASS.

### 3. Y-flip detection across all 957 frames

Independent L2 detector (`/tmp/yflip_l2`, all 957 frames extracted via `ffmpeg scale=120:-1`):

| Detector | Count | Notes |
|---|---|---|
| Strict L2 vertical-mirror (prev vs flipud(curr)) | **0** | No frame is its predecessor's mirror. |
| Top↔bottom half swap | **1 candidate (frame 127)** | A black transition frame triggers trivially (top=bottom=0). |
| Top frame-to-frame jumps (cuts) | 128, 113, 358, 196, **417**, 197, 357, 578, 658, 625 | Mean L2 = 0.0135; these are cuts. |

**Verdict on the two candidates (full-res visual inspection):**

- **Frame 127:** GOLFIN Invitational splash (PLAY / CREATE ACCOUNT / LOGIN with golfer mid-swing). **Frame 127:** entirely black (scene-transition fade). **Frame 128:** "PRO TIP / NOW LOADING" pro-tip screen. Splash → black → loading. **Confirmed scene-transition cut, NOT a vertical mirror.** The swap detector triggered because a fully-black frame is trivially symmetric.
- **Frame 417:** "NOW LOADING / 99%" pro-tip screen with vertical loading bar. **Frame 417:** tee scene (HOLE 1 - REGULAR, sky at top, ball-on-tee in center, James/Lv 10/Turn 1 HUD, grass at bottom — **UPRIGHT**). **Frame 418:** same tee scene, upright. Loading → tee scene cut. **Confirmed scene-transition cut, NOT a vertical mirror.**

**Effective Y-flips: 0/957. PASS.**

### 4. No-regression spot check

- **Map continuous, fire shown:** canonical still + `s07_ball_airborne` confirm.
- **Heat blob:** `s05_map_aimed_bent` shows red→green gradient oval at landing position.
- **Bent guide:** canonical still shows clearly bowed cyan line (left-bow with Draw armed).
- **White labels with dark outline:** "120%", "100%", "80%" legible, white-on-grass.
- **Ground-conforming semi-transparent rings:** three foreshortened horizontal bands at correct positions, ~25% alpha, no z-fight.
- **Guide-on-top:** cyan guide crosses over the ring bands (correct render queue).
- **Heading delta 0°:** asserted in `history.log` (`CRITERION 5b FINAL DELTA: 0.00 deg`); not re-litigated, accepted from iter-14 (no code change affects this).

### 5. Hard gates

| Gate | Result | Evidence |
|---|---|---|
| Zero edits under `Assets/Scripts/Physics/` introduced this iter | PASS | `git diff --stat HEAD -- Assets/Scripts/Physics/` shows only `PhysicsLabController.cs` (54 ++/--), listed as pre-existing DIRTY in iter-15 HEARTBEAT kickoff baseline |
| Rule 11: HoleMap button has `ButtonPressFeedback` sibling | PASS | `grep -c ButtonPressFeedback Assets/Scenes/Physics/LabScaffold.unity` = 1 (the new HoleMap button); diff shows `+m_EditorClassIdentifier: Assembly-CSharp::Golfin.UI.Polish.ButtonPressFeedback` |
| LabScaffold scene additive-only (no `m_IsActive: 0`, no `sizeDelta` shrinks, no position drifts on existing GOs) | PASS | `git diff --stat` = `120 ++, 0 --`; only `+m_IsActive: 1` flips found (new GO activation), no deactivations |
| 15 EditMode tests pass | PASS | `grep -c \[Test\] MapViewAimingTests.cs` = 15; implementer report cites 15/15 PASSED, 0 failed, 0 skipped, 0.104s via `tests-run testClass:MapViewAimingTests` |

## Verdict

The iter-14 blocker is resolved cleanly and minimally — `using UnityEditor` removed, `AssetDatabase.LoadAssetAtPath` replaced by `SceneManager`-walk + `GameObject.Instantiate` of the in-scene Flag.fbx (the proven pattern from `PhysicsLabController.cs:1740`). All three remaining `UnityEditor`/`AssetDatabase`/`#if UNITY_EDITOR` occurrences are in `//` comments; the runtime path is player-build safe. The flag pin is clearly visible in the canonical still adjacent to the "100%" ring label. Y-flip remains 0/957 across the entire captioned video (both detector candidates are confirmed scene-transition cuts, not mirrors). No regression on map content, heat blob, bent guide, labels, rings, or heading-delta. Hard gates all green: Physics untouched, Rule 11 satisfied, scene additive-only, 15/15 tests.

Routing to the red-team for adversarial gate.

## Files referenced

| Artifact | Path |
|---|---|
| Canonical still (iter-15) | `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/map_view_aiming/screenshots/iter15/canonical_map_open_iter15.png` |
| Aimed-with-heat-blob still | `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/map_view_aiming/screenshots/iter15/s05_map_aimed_bent_2026-06-19_12-19-27.png` |
| Captioned video | `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/map_view_aiming/videos/map_view_aiming_iter15_captioned.mp4` |
| MapViewController (fix location) | `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs` |
| Frame 127 cluster (transition cut, not flip) | `/tmp/yflip_inspect/cluster127_01.jpg`, `cluster127_02.jpg`, `cluster127_03.jpg` |
| Frame 417 cluster (transition cut, not flip) | `/tmp/yflip_inspect/cluster417_01.jpg`, `cluster417_02.jpg`, `cluster417_03.jpg` |
| L2 frame dump | `/tmp/yflip_l2/f_0001..f_0957.jpg` (957 frames) |
