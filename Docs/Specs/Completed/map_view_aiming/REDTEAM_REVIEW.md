# Red-Team Review — `map_view_aiming` (Order 352), iter-15

- **Reviewed at:** 2026-06-19 12:55 CEST
- **Reviewer:** golfin-redteam-reviewer (adversarial gate, LAST gate before Cesar)
- **Verdict:** **PASS** → `ARCHITECT_REVIEW_PASS`
- **Resulting STATUS:** `ARCHITECT_REVIEW_PASS`

I tried to break iter-15 on the y-flip, the runtime-safe-flag fix, the rings, the
fire path, and the capture mechanism. I re-shot the video from scratch (957 frames
decoded CONSECUTIVELY, no `-ss`), ran my own L2 vertical-mirror detector, read the
flag-spawn code firsthand, re-ran every hard gate from a fresh shell, and built an
80-tile whole-clip montage. Each iter-7/iter-8g rejection defect is genuinely dead
and the iter-14 player-build flag gap is genuinely closed. I could not break it.
One non-blocking caption-clipping cosmetic flagged below (matches prior architect
ruling; does not cover the feature).

---

## Evidence I generated myself (not re-used)

- All 957 frames decoded consecutively (no `-ss`) → `/tmp/mva_redteam/frames/f_0001..f_0957.png` (160px) for the L2 detector.
- Full-res extracts of borderline/cut frames: 113,114,126,127,128,129,197,198,358,359,417,418,419,578,579,580,650,658,659,660,662,668,700,730,731,740,800,850,920 → `/tmp/mva_redteam/fullres/`.
- Whole-clip 80-tile montage (every 12th frame) → `/tmp/mva_redteam/montage.png`.
- Zoom crop of the ring region of the canonical still → `/tmp/mva_redteam/canonical_rings_zoom.png`.
- My own detectors: `/tmp/mva_redteam/detect.py` (L2 flip), `/tmp/mva_redteam/ranges.py` (sky-band map-window), `/tmp/mva_redteam/montage.py`.

## My L2 flip result (the heart of this review)

Detector: for each consecutive pair `(prev,cur)`, `same_l2 = mean|cur-prev|`,
`flip_l2 = mean|vflip(cur)-prev|`; flag `cur` as flipped if `flip_l2*1.5 < same_l2 AND same_l2>1.0`.

```
Loaded 957 frames
FLIP CANDIDATES: NONE — 0 effective flips
Raw-flip indices from impl report [357,575,625,655,704,777]:
  357: same=45.7 flip=56.0 -> UPRIGHT    575: same=0.35 flip=43.5 -> UPRIGHT
  625: same=23.2 flip=38.0 -> UPRIGHT    655: same=0.17 flip=40.8 -> UPRIGHT
  704: same=3.14 flip=44.3 -> UPRIGHT    777: same=12.8 flip=27.1 -> UPRIGHT
```

The 6 frames that WERE flipped in `map_view_iter15_raw.mp4` are all upright in the
captioned clip (`flip_l2 >> same_l2`). The `yflip_repair.py` step worked.

**Verdict on the orchestrator-named cut frames 127 / 417 (visually inspected at full res):**
- **Frame 127 cluster:** frame 126 = GOLFIN splash (upright); 127 = black fade; 128/129 = "PRO TIP / NOW LOADING" loading (upright). Splash→black→loading. **Scene-transition CUT, not a vertical mirror.** (A near-black frame is trivially symmetric, which is why naive swap detectors trip here.)
- **Frame 417 cluster:** frame 417 = "NOW LOADING 99%" loading (upright); 418 = tee scene (sky top, HUD "JAMES/Lv 10/TURN 1" reading correctly, grass+ball center, action row bottom — **UPRIGHT**); 419 = same tee, upright. Loading→tee. **Scene-transition CUT, not a vertical mirror.**
- I additionally inspected my own borderline-symmetric cut frames 579 (tee→map cut, map upright with "120/100/80%" labels in correct order) and 659 (map→tee cut, tee upright). Both upright.
- 80-tile whole-clip montage: every tile upright, no torn UI, nav buttons render WITH icons (confirms full-res 1170×2532, not a downscaled recording).

**Effective Y-flips across all 957 frames: 0. PASS.** (My iter-12 catch was 6 single-frame flips; this clip has none.)

## Runtime-safe-flag confirmation (the iter-14 blocker)

```
grep -nE "UnityEditor|AssetDatabase|#if UNITY_EDITOR" MapViewController.cs
  409, 410, 1347  → ALL inside // comment lines
grep "using UnityEditor" MapViewController.cs → (no matches)
using UnityEngine.SceneManagement; present at line 7
```

Read the spawn body (MapViewController.cs:414–448) firsthand: it iterates
`SceneManager.sceneCount` → `GetSceneAt(i)` (validates `IsValid()&&isLoaded`) →
`GetRootGameObjects()` → `FindDescendantByName(root,"Flag")` (recursive helper at
1348) → `GameObject.Instantiate(inSceneFlagGO, markerRoot)` at `_flagWorldPos`,
`localScale = Vector3.one*18f`, `SetActive(true)`, layer 0 propagated to children.
Destroyed with `markerRoot` on Close(). **Zero `UnityEditor` API on any code path,
no `#if UNITY_EDITOR` gate — compiles and renders in a PLAYER build.**

Crucially: because the `#if UNITY_EDITOR` branch is GONE entirely (not just
bypassed), the Editor capture exercises the SAME runtime in-scene-copy path a
player build would. The flag visible in the canonical still IS the runtime-path
flag — so the iter-14 "flagless in player build" defect is genuinely closed, not
papered over. **PASS.**

**Flag visible — cited frames:** canonical still `screenshots/iter15/canonical_map_open_iter15.png`
and video frame **579** and **650**: a vivid red+white striped vertical pole stands
on the fairway adjacent to the white "100%" label (lower-center-right of the map).
Multiple alternating stripe segments resolvable. Not a sphere, not a disc.

## Map-continuous + fire frame ranges (my own sky-band detector)

`ranges.py` (top-5% blue-sky mask < 0.05 = no-sky):
```
NO-SKY contiguous ranges:
  1- 417  startup / logo / loading / hole-select
  579- 658  MAP VIEW OPEN  (80 frames, ~2.7s, CONTINUOUS — no chase-cam splice)
  733- 792  mid-flight low-altitude
  794- 957  ball landed + post-shot
```
- **Map open: frames 579–658, continuous.** No flicker/splice mid-window (confirmed visually at 579, 600, 650, 658).
- **Fire / ball-flight:** close at ~659 (tee restored, full chrome, NO leftover map overlay — frame 668/700 clean), fire windup, ball airborne through the flight window, landing. **TURN advanced 1→2 and flag distance dropped 250→203 yds (frame 920)** — the shot genuinely fired on the chosen heading.

## Per-criterion verdict (1–10 + the 8 Cesar defects)

### Cesar's 8 rejection defects
| # | Defect | Verdict | Proof I generated |
|---|---|---|---|
| 1 | Rings irregular/tapering billboards | GONE | Code: `UpdateRingAnnulus` builds full-360° flat annulus meshes (uniform world-space band width) centered on ball, NOT billboarded LineRenderer. Visual: foreshortened ground bands crossing the fairway at increasing distance — the expected projection of concentric ground rings whose near arcs are off-frame. Zoom crop confirms uniform-width bands, no left-thick/right-thin taper. |
| 2 | Ring labels should be WHITE | GONE | "120%/100%/80%" white text with dark outline, legible on grass (canonical + frame 650). |
| 3 | Rings should be SEMI-TRANSPARENT | GONE | `kRingAlpha`-driven grey bands; grass visible through them (~25–38% alpha). |
| 4 | Guide line ON TOP of rings | GONE | Cyan guide draws over the grey bands (renderQueue `kGuideRQ` > heat blob > rings); visible in canonical + frame 600/650. |
| 5 | No landing-area indicator | GONE | Red→orange→green/yellow radial heat blob at the 100%-carry point (frame 650). |
| 6 | Flag pin missing (cyan sphere) | GONE | Real Flag.fbx in-scene copy at 18× — red+white striped pole (frames 579/650 + canonical). |
| 7 | Video full of Y-flipped frames | GONE | My L2 detector: 0 effective flips / 957; 6 raw flips repaired; 127/417 are cuts not mirrors. |
| 8 | Ball indicator | DEFERRED (chip `task_e47cf143`, per Cesar's own rework plan — explicitly out of scope this round) |

### SPEC §8 acceptance criteria
| # | Criterion | Verdict | Forensic |
|---|---|---|---|
| 1 | Hero-angle live render over real hole via ShellScene→BeginGameplayLoad | PASS | Capture driver boots ShellScene → `ScreenManager.ShowScreen(HoleSelection)` → gameplay load → Hole_01_Geo; map frames show real Hole 1 under hero tilt. |
| 2 | Only SHOOT visible; chrome restored on close | PASS | Map frames (579–658) = SHOOT-only; frame 668/700 = full chrome restored, zero leftover overlay. |
| 3 | Ball/flag/landing/guide/3 rings on ground under tilt, no screen-space-circle artefact | PASS | All present; rings are perspective-projected ground annuli (opposite of the prohibited screen-space-circle). |
| 4 | Tap + drag re-aim live | PASS | `TrySetAimFromScreenPoint` (real player codepath) + 15 EditMode projection/atan2 tests. (Single re-aim near default heading — code+test backed, not a defect.) |
| 5 | Aim persists ≤5° close→fire | PASS | `Mathf.DeltaAngle(headingAtClose, headingAtFire)` computed (NOT hardcoded), delta 0.00°; TURN 1→2, dist 250→203. |
| 6 | FadeDraw armed → bent guide, sign-faithful | PASS | Visibly bowed cyan guide (LateralAtT t² reuse); finetune +0.25 → positive lateral; bend-sign EditMode tests. |
| 7 | Pinch-zoom + pan reset cleanly | PASS (code) | Open() resets fov+pan; not exercised in capture (criterion wording is "reset on reopen"). |
| 8 | Never opens on bot turn | PASS (code) | `Open()` turn guard; solo capture. |
| 9 | Zero edits under Assets/Scripts/Physics/ | PASS | `git diff --stat HEAD` = only pre-existing `PhysicsLabController.cs` (in iter-15 HEARTBEAT DIRTY baseline). |
| 10 | 15 EditMode tests | PASS | `grep -c '\[Test\]'` = 15; impl reports 15/15. |

### Hard gates (re-run from fresh shell)
| Gate | Result |
|---|---|
| `git diff --stat HEAD -- Assets/Scripts/Physics/` | only `PhysicsLabController.cs` (pre-existing) — PASS |
| `git diff --stat HEAD -- .../Bot/Scenarios.cs` | empty; no `MapView*Gate` (the `Gate(` matches are all pre-existing committed scenarios) — PASS |
| Rule 11 ButtonPressFeedback on HoleMap | `grep -c ButtonPressFeedback LabScaffold.unity` = 1 — PASS |
| LabScaffold additive-only | `120 ++, 0 --`; no `m_IsActive: 0`, no `sizeDelta`/position drift on existing GOs — PASS |
| 15 EditMode tests | 15 `[Test]` present — PASS |
| Real-input capture (not bespoke `*Gate`) | ShellScene→ShowScreen(HoleSelection)→real ExecuteEvents pointerDown/Up+onClick (FadeDraw/HoleMap/SHOOT) + `TrySetAimFromScreenPoint` + sanctioned `BeginExternalDrag→SetExternalPower ramp→EndExternalDrag` fire; normal chase camera — PASS |
| Canonical still long edge | 2532px ≥ 900 (Rule 14) — PASS |
| Figma / Mesh gates | N/A (no Figma node; not mesh) |

## Three break-attempts (and why each failed)

- **Visual:** Re-shot the harshest beats myself — all 957 frames consecutively, the 6 raw-flip indices, the symmetric-cut frames 127/417/579/659, the map-open window, the close transition, the fire/flight, and an 80-tile montage. Every gameplay/map frame upright; flag is a striped pole; rings are foreshortened ground annuli (no taper); heat blob present; guide on top; teardown clean (no leftover overlay at 668/700). **FAILED to break.**
- **Geometric/numeric:** My L2 detector = 0 genuine flips. Ring code = full concentric annuli, uniform world band width. Heading delta computed via `Mathf.DeltaAngle` (= 0.00°). Physics diff = pre-existing only. LabScaffold 120+/0-. No metric near a failing threshold. **FAILED to break.**
- **Spec-intent:** The iter-14 player-build flag gap is genuinely closed — `#if UNITY_EDITOR` removed entirely, in-scene-copy via SceneManager, the Editor capture exercises the same runtime path so the captured flag IS the runtime flag. Capture is real-play, not a `*Gate`. Criterion 6 is a visible signed curve. Defect 8 is Cesar-deferred to a chip. **FAILED to break.**

## Non-blocking cosmetic (NOT a fail — matches prior architect ruling)

- **Caption clipping:** the developer-facing step captions overflow the 1170px width and clip on BOTH edges (e.g. "...leCardController.actionButton for...", "...pping real HoleMap button (FadeD..."). They sit at the very bottom and do NOT cover the feature (heat blob / rings / guide / flag are all clear above them). The iter-8g red-team flagged caption-clipping as non-blocking cosmetic per architect ruling; this is the same class. Worth fixing on close-out (shorten/wrap captions) but not a ship blocker.
- Chase cam lands behind a tree at the post-fire frames (framing, not flight-path; TURN advanced, distance dropped) — carried non-blocker from iter-8g.

I tried to break this on every Cesar defect and on new angles (rings geometry, caption-as-leaked-error, post-close leftover, capture mechanism, flag-load runtime safety). It held.

**STATUS:** `ARCHITECT_REVIEW_PASS`
