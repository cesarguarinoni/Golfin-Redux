# CESAR REJECTION — `tree_collisions` (after ARCHITECT_REVIEW_PASS, 2026-06-11)

Cesar playtested the ARCHITECT_REVIEW_PASS build and rejected it. Two defects. The implementer's
next `IMPLEMENTER_REPORT.md` MUST carry a `## Rejection follow-up` section with an explicit
GONE/RESOLVED/STILL-PRESENT verdict per defect AND same-angle full-res evidence (Rule 15).

## Defect 1 — Canopy reads as slow-motion descent (DESIGN flaw in v1, not implementation)

**What Cesar saw:** a ball that strikes a canopy goes into slow-motion and drifts down through the
foliage all the way to the ground at near-zero speed. Feels broken / floaty, not like a leaf strike.

**Root cause (Architect):** v1's `canopyDampingPerStep = 0.92` was applied EVERY RK4 step the ball
was inside the canopy → exponential decay at sim rate. Velocity collapses to ~13% in ~0.1s, gravity
gain is damped too, terminal creep ≈ 0.5 m/s → 10+ s drift through the canopy band, near-zero exit
speed. Continuous drag is the wrong model for a projectile.

**Fix = SPEC §D3 REVISED (already written into `SPEC.md`):** canopy becomes a **discrete one-time
impulse on canopy ENTRY** (`!IsInsideCanopy(p0) && IsInsideCanopy(p1)` → `vel *= canopyHitDamping`
ONCE), then normal ballistics (gravity/drag/magnus) resume immediately. No per-step force while
inside, no cut on exit, each fresh entry fires its own cut. CSV column `canopyDampingPerStep` →
`canopyHitDamping`, default `0.92` → `0.40`. See revised §D3, §4b, §3a, §8 (new no-slow-mo item), §9(b).

**Resolution requires:** the new `## 8` no-slow-mo regression test (descent time canopy-entry→ground
≤ 1.5× the trees-disabled fall; impulse fires exactly once per pass) GREEN, AND a re-shot §9(b)
canopy clip showing the ball swatted at contact then falling out at NATURAL speed (no slow-mo).

## Defect 2 — §9 video does not show the trunk collision

**What Cesar said (chat):** *"Video only shows canopy, no trunk collision."* The trunk-strike segment
of the current `videos/tree_collision_gate_visual_gate.mp4` is so camera-buried in foliage that the
ball-hits-trunk-and-drops-dead moment is not legible — it reads as canopy-only. (Both reviewers had
flagged this as "marginal-by-pixels"; Cesar confirmed it fails the visual gate.)

**Resolution requires:** the re-shot §9(a) clip must UNMISTAKABLY show the ball striking a tree TRUNK
and dropping nearly dead — choose a framing where the trunk impact is clearly visible (e.g. an
isolated trunk, a side/elevated camera angle rather than chase-cam-buried-in-canopy, and/or hold on
the impact moment). The trunk model itself is correct and UNCHANGED — this is a video-legibility fix.

## Explicitly UNCHANGED — do NOT touch (Cesar directive)

Trunk model (D2), the iter-4 roll/putt trunk-deflect fix, the bake pipeline, the save hook, and the
per-hole `tree_obstacles.csv` files (they reference profiles by name — no re-bake needed for the
column rename). Only the canopy model, the profiles CSV, the canopy test, and the §9 video change.
