---
name: green-orbit
description: Produce a REAL moving-camera orbit video of a hole's green for ridge/slope/bake review. Drives the sanctioned Unity Recorder pipeline (HoleFlyoverRecorder green-orbit mode) — never hand-roll a camera-orbit-PNG-stitch. Use whenever a green/terrain bake needs a video deliverable (Rule 17) or Cesar asks to "see the green" as a clip.
---

# green-orbit — sanctioned green-inspection orbit video

**Why this exists.** The green-orbit clip was hand-written and thrown away every green
iteration (iter-8/10/12 of `green_slope_height_bake`, then again in `green_ship_polish`
iter-13 — which shipped a 6-still slideshow at 0.5fps mislabeled as an "orbit" and got
caught). The rig params lived in report prose. This skill makes the orbit a one-call,
reproducible, **same-angle** deliverable that is physically incapable of being a slideshow.

**Hard rule: do NOT reinvent.** Do not write a custom camera loop + `EncodeToPNG` + ffmpeg
stitch. The 18-hole drone videos and every green orbit go through one tool:
`Golfin.CourseImport.Recording.HoleFlyoverRecorder`, which uses the Unity Recorder
(`RecorderController` + `MovieRecorderSettings`) — smooth, full-framerate, real motion.

## What it produces

A landscape 1920×1080 @ 60fps H.264 clip, 8 s, full 360° centred orbit on the green,
green dominating the frame. Auto-frames any open `Hole_NN_Geo` scene from the green
renderer bounds at the canonical inspection rig (radius ≈ 1.4× green max-extent ≈ 22 m,
38° elevation, 40° FOV — the same rig prior green iters used, so before/after clips are
comparable). Output: `Recordings/green-NN_orbit.mp4`.

## Steps

### 1. Open the scene to inspect (or use the current one)
The orbit frames whatever scene is open. For a bake review, open the production scene:
`scene-open Assets/Golf/Courses/lomond-country-club/Generated/Hole_NN_Geo.unity` (Single).
Confirm it's the right hole and not dirty (`scene-list-opened`).

### 2. Fire the recorder (proven `script-execute` reflection call)
The recorder runs autonomously via `EditorApplication.update` across the play-mode domain
reload — you just arm it and poll. `RecordCurrentGreenOrbit()` is headless-safe (logs, no
popups) and resolves the hole number from `HoleMetadata` or the scene name.

```csharp
using System.Linq; using System.Reflection;
public class FireGreenOrbit {
  public static string Main() {
    var t = System.AppDomain.CurrentDomain.GetAssemblies()
      .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
      .FirstOrDefault(x => x.Name == "HoleFlyoverRecorder");
    bool ok = (bool)t.GetMethod("RecordCurrentGreenOrbit",
        BindingFlags.Public|BindingFlags.Static).Invoke(null, null);
    return $"green orbit started={ok}";
  }
}
```
(`started=false` ⇒ a recording is already running or the hole number couldn't be resolved
— read the Console. Do not retry blindly.)

### 3. Wait for the MP4 to finish flushing
Play-mode entry + 8 s orbit + recorder flush + edit-mode return ≈ 20–30 s. Poll until the
file size is stable:
```bash
for i in $(seq 1 30); do
  f=Recordings/green-NN_orbit.mp4
  if [ -f "$f" ]; then s=$(stat -f%z "$f"); sleep 3; s2=$(stat -f%z "$f");
    [ "$s" = "$s2" ] && [ "$s" -gt 50000 ] && { echo "DONE $s2"; break; }; fi
  sleep 3
done
```

### 4. MANDATORY motion gate (this is the anti-slideshow check — do not skip)
A clip only counts as a video if it actually moves. Verify all three:
```bash
V=Recordings/green-NN_orbit.mp4
ffprobe -v error -select_streams v:0 -show_entries stream=r_frame_rate,nb_frames,duration \
  -of default=noprint_wrappers=1 "$V"           # r_frame_rate must be >= 30/1, NOT 1/2
# quarter-turn pixel diff must be > 12 (proves the camera actually orbits)
ffmpeg -y -loglevel error -ss 0 -i "$V" -frames:v 1 /tmp/go_a.png
ffmpeg -y -loglevel error -ss 2 -i "$V" -frames:v 1 /tmp/go_q.png
python3 -c "from PIL import Image,ImageChops as C;a=Image.open('/tmp/go_a.png').convert('RGB').resize((320,180));q=Image.open('/tmp/go_q.png').convert('RGB').resize((320,180));d=list(C.difference(a,q).getdata());print('90deg diff',sum(sum(p) for p in d)/(len(d)*3))"
```
Compare frames **90° apart** (≈2 s), not 180° (≈4 s) — a symmetric green looks similar from
opposite sides, so 180° under-reports motion. If `r_frame_rate` is `1/2` or the diff is near
0, it's a slideshow — FAIL, do not deliver.

### 5. Copy into the task's videos/ folder
Clips live in `Docs/Specs/Active/<task>/videos/` (never `screenshots/`). Name descriptively,
e.g. `h07_ridge_iterNN_orbit.mp4`.

### 6. Burn a caption (REQUIRED — every delivered clip ships captioned)
Standing rule (`feedback_caption_videos_unobtrusively`): every video gets a **descriptive,
unobtrusive** caption. Use the sanctioned tool, not inline hand-rolled drawtext — see
`reference_video_caption_tool`:
`python3 Docs/Scripts/build_bot_video.py --mode visualgate --raw-mp4 <orbit.mp4> --title "H07 ridge — iter-13\nRIDGE_RAMP_WIDTH=4.0m" --suffix h07_ridge_iter13 --output-dir Docs/Specs/Active/<task>/videos`

Caption content = what the clip proves: hole + what changed + the key param/metric
(e.g. "H07 ridge — iter-13 · ramp 4.0m · perp-slope 3.3%"). Keep it **unobtrusive**:
small font, bottom or top edge, semi-transparent or thin bar, never over the green surface
being reviewed. Adapt per clip (landscape orbit → bottom strip; portrait → wrap). Then
**frame-extract the captioned output and read it back** to confirm the caption renders and
doesn't occlude the subject before declaring done. An uncaptioned clip is not a finished
deliverable.

## Tuning the rig
All defaults are consts at the top of
`Assets/Scripts/Editor/Recording/HoleFlyoverRecorder.cs`
(`GreenOrbitSeconds`, `GreenOrbitElevationDeg`, `GreenOrbitRadiusFactor`, `GreenOrbitFov`,
`GreenOrbitWidth/Height`, `GreenOrbitTurns`). Lower elevation for a grazing ridge read;
raise `GreenOrbitTurns` to 1.5–2 for a longer look. Edit consts → `assets-refresh` → re-fire.

## Batch / interactive
- Interactive: menu `GOLFIN/Recording/Record Current Green Orbit`.
- Full-hole gameplay flyover (different deliverable): `GOLFIN/Recording/Record Current Hole Flyover`
  / `Record All 18 Holes` → `Recordings/hole-NN.mp4`.
