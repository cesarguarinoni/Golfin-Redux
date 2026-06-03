# Cesar Rejection — `ball_flight_trail`

**Rejected at:** STATUS was `ARCHITECT_REVIEW_PASS` (after full pipeline + red-team pass).
**Reason:** The capture evidence is wrong on THREE counts — wrong aspect ratio, wrong even for the "portrait" video, and **no level loaded** (the trail was shot over an empty flat-ground background instead of a real hole).

This is a **capture / evidence fix only**. The feature code passed red-team and stays UNCHANGED.

---

## The three defects

### 1. Stills captured in LANDSCAPE
The three canonical stills are 1920×1080 landscape. The game is portrait. Wrong.

### 2. Even the video is the WRONG portrait aspect
`videos/ball_trail_motion.mp4` is 1080×1920 = **9:16** (that's the "iPhone 8 Plus" size). The target device is **iPhone 14**, which is **9:19.5** — taller. So the video is too short/fat; it does not match the shipping device frame.

### 3. No level loaded — empty background
The captures were taken with NO hole loaded, so the ball flew over the flat-ground fallback with an empty background. The trail must be shown over a **real loaded hole** (actual green/fairway/terrain), which is the whole point.

---

## Exact target spec

**Capture resolution for BOTH stills AND video = iPhone 14 = `1170 × 2532` portrait.**

This is the exact Unity Game View custom size. Verified via reflection over `UnityEditor.GameViewSizes`:
```
[iOS] 'iPhone 14' 1170x2532
```
Aspect = 1170:2532 ≈ 9:19.5. Every still and the video must report `1170×2532` (height > width) — confirm with `sips` / `ffprobe`.

---

## How to load a real hole in the PhysicsLab (defect 3)

The PhysicsLab (`LabScaffold.unity`) renders real terrain only when a `Hole_{NN}_Geo` scene is **additively loaded** — `PhysicsLabController.ScanForLoadedHoleSceneAtStartup()` (L343) detects it and calls `OnHoleLoaded`, spawning the ball on the real green/tee. With no Hole_Geo additively loaded it falls back to flat ground ("empty background"). See `PhysicsLabController.cs:343-382` and the `LabHoleBinder` note at L29.

So the capture flow must be:
1. Open `Assets/Scenes/Physics/LabScaffold.unity` (Single).
2. **Additively load a real `Hole_{NN}_Geo` scene** (default: Hole 1 — the known-working lab hole; a lomond or Taiheyo Geo hole is also fine — pick one with clear fairway+green so the ribbon reads against terrain).
3. Enter play mode → confirm `[PhysicsLab] ... detected loaded hole scene` in the console (NOT the "flat-ground fallback" log).
4. Fire shots and capture the trail flying over the real terrain.

---

## Visual + shot fixes (added after Cesar's recording review)

### V1 — Trail width = ball diameter, CONSTANT, fade-out tail (Option A)
The ribbon is currently too thick (`_startWidth = 0.09f`, tapering to 0 → a wedge/comet point). Cesar chose **Option A**: a ribbon that is **constant width = the 3D ball's diameter along its whole length**, with the tail dissolving via the **alpha gradient (1→0)** — NOT a width taper. So:
- Measure the live ball's rendered diameter (`BallAnimator` spawned instance world scale × mesh bounds) and set `_startWidth` to that diameter.
- Make the `widthCurve` **CONSTANT** (flat at 1.0 → ball diameter the whole length); do NOT taper width to 0. No triangular/pointed tail.
- Keep the `colorGradient` alpha fade **1→0** along the length so the tail fades out smoothly while staying ball-width.
- This is a small tuning change to `BallTrailController` serialized config + `EnsureTrail` width curve — still no change to the trail color/state logic.

### V2 — All shots start AND land in the clean playing area
During the recording, shots started/landed outside the terrain or behind trees. Fix the capture choreography:
- Every demonstration shot must **START from a clear spot in the playable area** (tee/fairway, inside terrain bounds, NOT behind/inside trees) and **LAND on terrain** (fairway/green).
- **Only the dedicated OB-color shot** lands off-terrain (or uses the `ForceOBRecolorForCapture()` seam). It still starts from the clean playing area.
- Pick start positions + aim/power that keep blue (in-flight) and gold (perfect) shots fully on Hole 1's playable surface.

## Required fix

UNCHANGED (all passed red-team — do NOT touch the trail color/state LOGIC): `ShotController.LastShotWasClean`, `PhysicsLabController` wiring, `BallTrail.mat`, and the `LabScaffold.unity` 21-line/0-GO BallTrailController wiring. The ONLY code change permitted is the V1 width tuning in `BallTrailController` (constant ball-width + keep alpha fade).

1. Set the Editor Game View to **iPhone 14 (1170×2532)** before capturing (select the iOS group's `iPhone 14` entry, or set the GameView size to 1170×2532). Confirm height > width.
2. Load a real hole additively (per the flow above). Confirm terrain is visible — NOT flat-ground fallback.
3. Re-capture the three stills at 1170×2532 over real terrain via the sanctioned `CaptureCore` path: blue in-flight, gold clean full-swing flick, red whole-ribbon OB (real OB zone if the hole has one, else the `ForceOBRecolorForCapture()` seam). Each still: portrait 1170×2532, long edge ≥ 900px (it is), visible colored ribbon over visible terrain, mutually MD5-distinct.
4. Re-record the motion video at **1170×2532** over the real hole (ball moving, ribbon forming + following, blue→red whole-ribbon flip). If the recorder cannot output 1170×2532, set IMPLEMENTER_BLOCKED and say so rather than substituting a different resolution. Caption via `Docs/Scripts/build_bot_video.py` textfile= idiom.
5. Delete the old wrong-aspect/empty-background artifacts (`trail_blue_inflight.png`, `trail_gold_inflight.png`, `trail_ob_recolor.png`, `trail_vid_extract_blue.png`, `ball_trail_motion.mp4`) and replace with the new 1170×2532 over-real-terrain captures.
6. Delete the temp scaffolding `Assets/Scripts/Physics/Viewer/BallTrailCaptureRunner.cs(+meta)` (red-team close-out item; GUID `addaceca5...` referenced by no scene/prefab). Keep the `ForceOBRecolorForCapture()` `#if UNITY_EDITOR` seam inside BallTrailController.
7. Fix the report's dimension line and re-cite all new artifacts; add a `## Rejection follow-up` (Rule 15) with GONE/RESOLVED per defect + measured dimensions of each new still + the video.

## Out of scope
No code/feature changes. No scene-state changes to LabScaffold beyond the already-approved BallTrailController wiring. Just correct the capture: iPhone 14 1170×2532, real hole loaded, for both stills and video.

## Coordination note
At rejection time Cesar was actively running TreePlacer tree-imports across `Hole_09..16_Geo` (lomond-country-club) in the live editor. The re-capture drives play mode + additive scene loads, which would collide with an in-progress import — the implementer must only run once the editor is free.
