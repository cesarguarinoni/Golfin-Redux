# CESAR_REJECTION — `map_view_aiming` (Order 352)

Rejected after ARCHITECT_REVIEW_PASS (iter-8g). Cesar reviewed the canonical video/still and flagged 8 issues. Architect re-verified each; the standout: **the canonical video is genuinely full of Y-flipped frames** (~243 of 970, in bursts) — the pipeline (red-team + architect) missed it because `ffmpeg -ss <time>` keyframe-snap sampling systematically skipped the flipped frames. Cesar proved it with a paused frame at 0:22.

## Defects (Cesar, 2026-06-19)
1. **Rings irregular, not consistent circles** — they taper thick-left → thin-right. Root cause: billboarded world-space `LineRenderer` circles (`MapViewController.cs:465`) foreshorten under perspective. Cesar's diagnosis (wrong method, "circles cut with a mask") is correct.
2. **Ring labels (80/100/120%) should be WHITE** — currently tinted to ring color.
3. **Ring lines should be SEMI-TRANSPARENT** — currently ~0.9 alpha.
4. **Trajectory/guide line must render ON TOP of the rings.**
5. **No landing-area indicator** as in the reference.
6. **Flag pin missing** — must use the SAME flag as normal shooting + the reference (`Assets/Art/3D/Props/Flag/Flag.fbx` at `HoleContext.PinWorld`), not the cyan sphere.
7. **Video full of upside-down (Y-flipped) frames** (e.g. 0:22). CONFIRMED ~243/970 frames flipped in bursts. PrewarmRT did NOT fix it.
8. **Ball indicator** needs updating ("schedule also…").

## Approved rework plan (Cesar decisions, 2026-06-19)
- **Rings → SOFT FILLED BANDS** (match reference): flat ground annulus/decal on the XZ plane, soft semi-transparent filled band per ring, uniform width, foreshortens like painted ground rings. Concentric on the ball at 0.8/1.0/1.2×carry. NOT a billboarded LineRenderer.
- **Ring labels → WHITE** (subtle dark outline for legibility on grass).
- **Rings semi-transparent** (~0.35–0.45 alpha).
- **Guide/trajectory line renders ON TOP of the rings** (render queue / draw order).
- **Landing-area indicator → REFERENCE-STYLE HEAT BLOB NOW** (red→green gradient landing target at the 100%-carry point). This OVERRIDES the original SPEC §6/§7 deferral of the heat gradient to v1.1 — it is now IN SCOPE for this task.
- **Flag pin → reuse the real `Flag.fbx`** at `HoleContext.PinWorld` (render the existing in-scene flag via the map cam cull mask, or instantiate the same prop scaled for map legibility). Drop the cyan sphere stand-in.
- **Y-flip → eliminate ALL flipped frames.** Re-verify by decoding CONSECUTIVE frames (e.g. `ffmpeg -i in.mp4 -vf "select='between(n,A,B)'" -vsync 0 out_%03d.png`) or by WATCHING the whole clip end-to-end. **NEVER verify flips with `ffmpeg -ss <time>` keyframe-snap sampling** — it deterministically misses the flipped frames (root cause of this whole miss).
- **Ball indicator → SEPARATE fast-follow task** (chip `task_e47cf143`); NOT part of iter-9. Leave the current ball marker as-is for now.
- **Housekeeping:** consolidate to ONE clearly-named canonical video; delete the 10 stale videos (the misnamed `map_view_aiming_captioned.mp4` is an old flipped iter-6 file that caused confusion).

## Verification mandate for the pipeline this round
The implementer, self-reviewer, golfin-reviewer, and red-team MUST verify Y-flip by decoding consecutive frames or watching the clip — a `-ss` contact sheet is NOT acceptable evidence of a flip-free video. A single flipped frame anywhere = FAIL.
