# Red-Team Review — `tree_collisions` (iter-6, post-CESAR-REJECTION)

**Reviewer:** golfin-redteam-reviewer (adversarial gate)
**Timestamp:** 2026-06-11 21:42 CEST
**Verdict:** **ARCHITECT_REVIEW_FAIL**

---

## TL;DR — the blocker

**A descending approach shot that comes down onto / near a tree trunk gets STUCK floating in
mid-air against the trunk (y ≈ 1.4–2.0 m), burns the 14 400-step hard cap, and NEVER reaches the
ground.** I reproduced this 6 ways via live `script-execute` on the ai-game-developer MCP — not by
reading the report. A ball frozen 2 m up against a tree is the exact "ball stuck in the tree" class
of defect Cesar catches on sight (arguably worse than the original slow-mo, since the ball never
lands at all). Cesar's TWO rejection defects ARE fixed; but my adversarial probing surfaced a NEW,
gameplay-plausible stuck-ball case that the green test suite does not cover and that no prior gate
(including my own round-2 PASS, which only tested horizontal roll/putt) caught.

The iter-6 canopy redesign plausibly UNMASKED this: v1's per-step canopy drag bled off descent
velocity (the slow-mo), so a ball arrived at the trunk slowly; iter-6 keeps full descent speed after
the single 0.40 cut, so the ball plunges into the trunk band at speed and the containment guard
loops with zero time-progress.

---

## Live evidence I generated myself (ai-game-developer MCP @ localhost:21573)

All numbers below are from my own `script-execute` runs (provider built via the real
`TreeObstacleLoader.LoadInstancesFromText` + `TreeObstacleProvider.Create` path, default profile:
trunkR=0.25 trunkH=3 canopyR=3 canopyTop=9 restit=0.15 damp=0.40).

### DEFECT 1 (canopy slow-mo) — GONE for the canopy case ✅

PROBE2 (vacuum, straight descent through canopy band [3,9], traced per-step):
- **EXACTLY ONE** airborne damping step at idx=135, y=8.95, ratio = **0.401** (the single 0.40 cut).
- Every subsequent in-canopy step shows ratio ≈ **1.007** — speed GROWS under gravity
  (13.48→5.41 at the cut, then 5.45→5.49→…→5.94). Natural free-fall, NOT suppressed creep.
- No step-after-step suppression; no <1 m/s drift-for-seconds. **The v1 slow-mo is gone.**

PROBE3 case B (steep near-axis, Default aero): WITH lands z=4.84 vs NO z=9.93 — canopy damps short,
not stuck, natural. `TreeCollision_CanopyEntryImpulse_NoSlowMoDescent` PASSES live (see test run).
Video PART B caption `e=2.6s` corroborated. **Defect 1 verdict: RESOLVED / GONE.**

### THE BLOCKER — stuck-floating-ball on descending approach shots (NEW, found by me)

PROBE7 reachability sweep (Default aero, realistic descending approach shots toward a trunk at origin):

| Case (origin → vel) | WITH-trees result | STUCK? |
|---|---|---|
| approach vy=-3 vz=12 | finalT=0.836 **finalY=2.028** finalZ=-0.247 samples=14401 | **STUCK** |
| approach vy=-4 vz=10 | finalT=1.109 finalY=0.000 finalZ=-0.249 samples=14668 | **STUCK** |
| approach vy=-5 vz=8  | finalT=1.140 **finalY=1.379** finalZ=-0.244 samples=14612 | **STUCK** |
| approach vy=-2 vz=14 | finalY=0.021 finalZ=-0.994 samples=686 | ok (deflects, lands) |
| approach off-axis x=1.0 | finalY=0.021 finalZ=3.546 samples=720 | ok (clears trunk) |
| full drive landing short of tree | WITH==NO finalZ=109.5 | ok (no interaction) |

PROBE3-C / PROBE3-D (straight-down the trunk axis): STUCK at y=2.97 (≈trunkTopY=3.0); vacuum case
velocity balloons to **maxSpeed=588 m/s** over the 14 400 frozen steps (time never advances).
PROBE5 `diagonal_descent_angle` (vy=-3, vz=12): STUCK at y=2.03, 14401 samples — confirms it is NOT
limited to a perfectly vertical axis drop; ordinary descending approach angles trigger it.

**Three of six realistic descending-approach angles get stuck.** The ball ends FLOATING at
y=1.4–2.0 m against the trunk, never reaching the ground.

### Root cause (precise, traced live — PROBE4)

`TestSegment` at the stuck point `(0, 2.971, -0.08)` returns `got=True isTrunk=True frac=0.0000`
(the iter-4 **containment guard** fires: p0 is inside the trunk XZ cylinder, dist 0.08 < r 0.25, and
y=2.971 ∈ [0,3]). In the **airborne** trunk branch (`BallSimulation.cs:430-449`):
```
pos = hitPos; vel = velOut; t = tHitAbs;   // tHitAbs = t + (tNext-t)*frac = t + 0 = t  (NO TIME ADVANCE)
continue;                                   // restart step from the SAME pos
```
The reflect only mirrors XZ velocity — **vy is left unchanged** (still descending) — and `pos` resets
to `hitPos = p0` with `frac=0`. The integrator makes ZERO progress and re-fires the containment guard
every iteration until the `maxSteps = 60*240 = 14400` cap (`BallSimulation.cs:387`). Velocity
accumulates gravity each frozen pass (→ 588 m/s in vacuum). The ball is left frozen mid-air.

Contrast: the **roll-phase** trunk handler (`BallSimulation.cs:600-623`) advances `pos=posNext` and
`t=t+Dt` unconditionally, so roll/putt never stick — which is why my prior round-2 horizontal probes
passed and missed this. The defect is specific to the AIRBORNE trunk branch + `frac=0` containment
guard + a still-descending `vy`.

### Attribution (honest)
The stuck CODE (airborne reflect + `continue`, containment guard) predates iter-6 (it is the iter-4
trunk model Cesar froze). But (a) iter-6's canopy redesign removed the per-step descent-velocity
bleed that previously masked it, so the ball now arrives at the trunk at full speed, and (b) I am the
last gate before Cesar and this is a ship-blocking "ball stuck in tree" defect regardless of which
iteration introduced the latent bug. It must be fixed before this reaches Cesar again.

### Regression / determinism / roll-putt — ALL CLEAN ✅ (PROBE8 + live tests)
- ROLL: withZ=-1.470 vs noZ=9.591 → DEFLECTED=True (iter-4 fix intact).
- PUTT: withZ=-7.335 vs noZ=43.688 → DEFLECTED=True (iter-4 fix intact).
- DETERMINISM: same input twice → bit-exact (samples 2901==2901).
- NULL bit-exact: Phase6 8-arg == Phase7 null → True.
- **Full EditMode suite (my live `tests-run`): Total=378 Passed=375 Failed=0 Skipped=3** — matches
  the cited numbers exactly; the 3 skips are pre-existing Stage C1 HoleCompleteDriver tests.
- `TreeCollisionTests` class run live: **8/8 unique PASS** incl. `CanopyEntryImpulse_NoSlowMoDescent`,
  `RollPhase_TrunkDeflectsRollingBall`, `PuttPhase_TrunkDeflectsRollingBall`.

The green suite is genuinely green — but it has NO test for a descending airborne shot landing on a
trunk, which is the hole my probes drove through.

---

## Prior-rejection replay (Rule 15)

### Defect 1 — canopy slow-motion descent: **GONE** ✅
Live PROBE2 trace: single 0.401 cut at canopy entry, then natural gravity gain every step (ratio
≈1.007), descent-time ratio 0.254 (nowhere near the 1.5× slow-mo threshold). `e=2.6s` PART B caption
corroborated. The discrete one-time entry impulse model is correctly implemented
(`TreeObstacleProvider.cs:157` entry predicate; `BallSimulation.cs:457-458` one-time apply).

### Defect 2 — §9 video must show a legible TRUNK strike: **MARGINAL / STILL READS AS FOLIAGE** ⚠️
I extracted my own frames (7/8/9/10/11/12/30/31s):
- **Setup (8s):** GOOD — side-elevated Downrange, a large brown trunk dominates the right, ball in
  front, trajectory cone aimed at the trunk, "PART A: TRUNK STRIKE" caption. Trunk = clear target.
- **Mid-flight (9-10s):** camera buried — 9s is dense foliage with a blue trajectory line and no
  trackable ball; 10s is pure trunk-bark filling the frame but with NO ball visible. The ball-strikes-
  trunk MOMENT is not legible.
- **At-rest (11-12s, the frames Cesar lingers on):** the G-ball sits in a field of GREEN LEAVES /
  branches; a dark trunk is present at the edges but **the ball reads as lodged in foliage, not pinned
  against bare bark**. The "Hard reflect + stop" verdict is carried by the CAPTION TEXT, not the image.

Honest adversarial call: a cold viewer (Cesar) could again say "the ball ends up in the leaves — I
still don't clearly see it hit the trunk and drop dead against the bark." Cesar's exact complaint was
"video only shows canopy." The setup frame + caption improve on iter-5, but the at-rest payoff frame
is still foliage-dominated. I rate Defect 2 **MARGINAL, not decisively resolved.** I am not hanging
the FAIL on this alone (it's a judgment call), but it should be re-shot alongside the blocker fix:
hold the at-rest frame on a framing where the ball is visibly against BARE TRUNK BARK (lower on the
trunk, below the canopy line), not buried in leaves.

---

## Three break-attempts and why each landed

1. **Visual / video:** the canopy PART B reads as natural fall (✅, Defect 1 gone); the trunk PART A
   at-rest still reads as foliage (⚠️ marginal — see Defect 2). Partial break.
2. **Geometric / live sim re-run:** **BROKE THE WORK.** Descending approach shots stick floating
   mid-air at the trunk (6 repros via `script-execute`). The airborne trunk branch makes zero
   time-progress when the containment guard returns frac=0 with vy still descending. This is the
   blocker.
3. **Spec-intent:** SPEC §D2 ("trunk = hard collision, ball drops nearly dead") and the whole point
   of the feature is that a ball hitting a trunk falls and stops ON THE GROUND. A ball that freezes
   2 m up in the air, never landing, violates the intent even though the letter (a hit is detected)
   is satisfied. The green test passes because it never fires a descending shot at a trunk.

---

## Integrity / scope (all clean — NOT the reason for FAIL)
- `ChaseCamera.cs`: `git diff HEAD` empty — NOT modified (Downrange mode reused via reflection). ✅
- No out-of-scope code: `git diff --name-only HEAD` over AI/UI/Gameplay/Roster/Inventory → empty. ✅
- `grep canopyDampingPerStep` over `Assets/Scripts/` → only the one doc-comment at
  `TreeCollisionTests.cs:260` (acceptable). ✅
- `PhysicsLab_Hole1.unity`: forbidden-mutation scan (m_IsActive / m_LocalPosition / Scale / Rotation
  / sizeDelta / m_AnchoredPosition) → zero matches. ✅
- Rule 13: 31 out-of-folder uncommitted paths, all in the IMPLEMENTER_REPORT Files table. ✅
- Video: 1170×2532, 37.07s, 28.2 MB (ffprobe) — matches report. ✅

---

## Fix instructions (for the implementer)

The airborne trunk branch must make forward progress when the containment guard reports a `frac=0`
already-inside hit on a still-descending ball — otherwise the integrator freezes and the ball floats.
Do at least ONE of (preferably 1+2):

1. **Guarantee time/position progress on a `frac=0` trunk hit.** In the airborne trunk branch
   (`BallSimulation.cs:430-449`), when `treeHit.Frac == 0` (containment guard / already-inside), do
   NOT reset `pos` to `hitPos` and `continue` with `t` unchanged — that re-enters the same step
   forever. Instead push the ball OUT of the trunk cylinder along `NormalXZ` (to just outside
   trunkRadius) AND advance `t = tNext`, `pos = posNext`-pushed, so the integrator cannot loop. Mirror
   the roll/putt handler, which already advances `t=t+Dt` and `pos=posNext` unconditionally and never
   sticks.

2. **Let a stuck/over-the-top trunk hit fall to ground.** A ball whose XZ is inside the trunk footprint
   but is descending (vy<0) at/above trunkTopY should pass to the ground check / land on top of the
   trunk top or slide off, not be reflected in place. Consider: if frac=0 and the ball is descending
   vertically with ~zero XZ speed, treat it as "landed on/deflected off the trunk" — kill XZ, keep the
   descent, and let the ground crossing terminate the shot.

3. **Add an EditMode test that fires a DESCENDING AIRBORNE shot at a trunk and asserts the ball reaches
   the ground (finalY ≈ ballRadius, NOT floating) within a bounded sample count (< maxSteps).** Seed it
   with my PROBE7 configs (`origin=(0,6,-6) vel=(0,-3,12)`, `origin=(0,8,-8) vel=(0,-5,8)`); both
   currently end STUCK at y≈1.4–2.0 with 14400+ samples. The test must FAIL on the current code.

4. **Re-shoot the §9 trunk clip** so the at-rest payoff frame shows the ball against BARE TRUNK BARK
   (frame it lower on the trunk, below the canopy), not lodged in green leaves — to close Defect 2
   decisively rather than marginally.

No re-bake needed (CSV data is fine); this is a runtime-resolution fix in the airborne trunk branch
of `BallSimulation.cs` + a new regression test + a re-shot trunk clip.

---

## Verdict

**ARCHITECT_REVIEW_FAIL.** Cesar's two rejection defects are addressed (canopy slow-mo is genuinely
gone; trunk-clip legibility is improved but marginal). However, my own live `script-execute` probes
surfaced a ship-blocking NEW defect: descending approach shots that land on/near a tree trunk get
**stuck floating mid-air (y ≈ 1.4–2.0 m), never reaching the ground**, because the airborne trunk
branch makes zero time-progress on a `frac=0` containment-guard hit while the ball is still descending.
Reproduced 6 ways. This is the "ball stuck in the tree" class of issue that fails on sight — exactly
what this gate exists to catch. Determinism, roll/putt deflect, null bit-exact, and the full 375/378
EditMode suite all hold; the green suite simply has no test for a descending shot into a trunk. Routes
back to the implementer with the fix + the new regression test + a re-shot trunk clip above.
