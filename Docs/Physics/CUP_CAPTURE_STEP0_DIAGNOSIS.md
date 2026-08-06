# Step 0 diagnosis — `cup_capture_and_lipout` (SPEC §3)

**Date:** 2026-08-05 · **Baseline:** HEAD `088e7d4ec`, working tree clean except `tasks/lessons.md` (M) + this spec folder
**Rig:** real flow — ShellScene play mode → PLAY (`SplashScreen/StartButton.onClick`) → `GameSession.SeedSession(6,…)` → `GameplaySceneLoader.BeginGameplayLoad(6)`
**Hole:** Hole 6 · `BakedHeightProvider` + `BakedZoneClassifier` (confirmed live, not FlatGround)
**Pin:** `HoleContext.PinWorld = (-72.531, 10.369, -8.840)`, surface at pin = `Green`
**Ball radius:** 0.02135 m · **cup radius:** 0.05400 m · **effRadius (capture):** 0.03265 m · **`PuttCfg.CupCaptureSpeed`:** 1.500 m/s

Probe method: `plc.GetGround()` / `GetSurfaces()` / `GetTreeProvider()` + the production 9-arg
`BallSimulation.Simulate(...)`, then the sample list scanned with a `RealCupDetector` built from the
same inputs as the live one — i.e. the exact `BallStateMachine.OnTrajectoryComputed` scan. Read-only;
no scene mutation, no code change.

---

## Result 1 — §1.1 and §1.2 CONFIRMED. This is the bug.

Putt fired 2.0 m out, aimed dead at the pin, four headings (0/90/180/270°):

| launch v | samples | min XZ dist to pin | speed at cup entry | `InCup` (velocity-aware scan) | termination | **roll-past AFTER leaving the cup** |
|---|---|---|---|---|---|---|
| 1.0 m/s | 1603 | **44.9 mm** (never reaches) | 0.039 | no | BallStopped | — |
| 1.4 m/s | 1765 | 0.0 mm (dead centre) | **0.420 m/s** | **YES @ sample 580** | BallStopped | **4.78 s** |
| 3.0 m/s | 2131 | 0.0 mm (dead centre) | **2.016 m/s** | no (speed gate) | BallStopped | 8.05 s |

The 1.4 m/s case is the reported symptom, quantified: the ball enters the cup at **0.42 m/s** — well
under the 1.5 m/s gate, unambiguously a drop — the scan flags `InCup` at sample 580 of 1765, and then
**the simulation keeps integrating for another 1185 steps (4.78 s) straight through and past the hole.**

`BallAnimator.Play` (`BallAnimator.cs:109`, `Update` at :152) plays to `samples[last].time`, so all
4.78 s of roll-past is rendered; `_playing` only drops at the very end. `BallStateMachine.Tick`
(:258) fires `InCup` on that falling edge. So the player watches the ball roll over the cup, carry on
for ~5 s, come to rest somewhere else — and only then does the hole-complete modal appear. Exactly
Cesar's report ("no falling animation, no reaction from the hole"), and exactly §1.1 + §1.2.

## Result 2 — §1.3 CONFIRMED, as designed

At 3.0 m/s launch the ball crosses the cup mouth at **2.016 m/s** (> 1.5 gate). Geometry-only
`IsInCup` returns YES @ sample 191; the velocity-aware overload returns no. Silent fly-over, zero
reaction — no lip-out exists.

## Result 3 — §1.4 (height gate) PARTIALLY confirmed: real, razor-thin, NOT the root cause on Hole 6

At the pin XZ exactly: `bakedGroundY = 10.3693`, `pinY = 10.3693` → **groundY − pinY = 0.000 m**
(within fp16.16 resolution, LSB ≈ 0.015 mm). The pin is authored *on* the baked surface, so the gate
does not blanket-reject here.

But the green tilts across the 3.3 cm capture disc, so the margin is sub-millimetre and
direction-dependent. Ring probe at effRadius, `groundY − pinY`:

```
hdg   0: -0.87 mm    hdg 180: +0.87 mm
hdg  45: -1.19 mm    hdg 225: +1.19 mm
hdg  90: -0.82 mm    hdg 270: +0.82 mm
hdg 135: +0.03 mm    hdg 315: -0.03 mm
```

In the 1.4 m/s putt, of the 39 samples inside effRadius, **20 are rejected by the height gate alone**
(`sample.y > pin.y + ballRadius`) and 19 pass; dY range across those samples is [−0.84, +0.87] mm.
Capture survives only because the ball happens to cross the low half of the disc. On the 3.0 m/s run
it is 4 pass / 4 rejected.

**Verdict:** the height gate is currently deciding cup capture on a **±0.9 mm coin-flip**. It is not
what broke Hole 6, but any hole whose pin Y is authored ~1 mm or more below its baked green surface
would reject 100% of rolling samples and `InCup` would never fire at all. §4.2's XZ-only in-sim check
removes this failure class entirely — keep that decision.

## Result 4 — practical note for the §7 acceptance clips

A 1.0 m/s launch from 2 m **does not reach the cup on this green** (stops 45–117 mm short, uphill
heading worst). The gate speed is measured *at the cup*, not at launch: the 1.4 m/s launch arrives at
0.42 m/s. So the §7 clips must be specified by arrival speed — to test the 1.5 m/s boundary the ball
needs to arrive fast, i.e. a much harder launch or a shorter putt. Will size these empirically before
recording.

---

## AFTER the fix — same rig, same hole, same shots

Re-run with the in-sim `CupSpec` enabled (`PhysicsLabController.GetCupSpec().Enabled == true`,
pin/radius/depth/gate as installed). BEFORE column is the 9-arg legacy path, AFTER is the same
input through the 10-arg entry:

| shot | BEFORE | AFTER |
|---|---|---|
| 1.0 m/s launch (comes up short) | BallStopped, 1603 samples, 6.67 s | **unchanged** — 1603 samples, 6.67 s, rests 45 mm short. No false capture. |
| **1.4 m/s launch (the bug)** | BallStopped, **1765 samples, 7.35 s** | **CupCapture, 615 samples, 2.56 s**, final XZ = pin (0 mm), final Y = pin − 79 mm |
| 3.0 m/s launch | BallStopped, 2131 samples, 8.87 s | BallStopped, 1873 samples — lip-out; rest position moves 6.73 m |

The 4.78 s roll-past is gone: the trajectory now ENDS at the cup. Final Y of −79 mm is exactly
`depth 100 mm − ballRadius 21 mm`, i.e. the ball centre sitting on the cup floor.

**Fall-in shape** (1.4 m/s): descends monotonically +20.3 → −78.7 mm relative to pin Y over
~0.16 s while XZ converges 40 mm → 0 mm. **Lip-out** (3.0 m/s): one impulse, |v| 2.028 → 1.417
(ratio 0.699 vs the 0.70 target), hop rises 1.1 → 4.4 mm and returns to 0 over ~0.07 s.

**Through the real player path** (ShellScene → PLAY → Practice → Hole 6 card → putter →
`FireViaShotController`, club index 3 = PutterIndex): `BallState -> InCup at t+1.2s`. Before the
fix that transition could only fire after the animator finished the full 7.35 s roll-past.

**Tests:** `CupCaptureSimTests` 14/14 pass, including the blocking bit-exact legacy gate
(`CupSpec.Disabled` reproduces the pre-cup sim sample-for-sample on the raw fp values, across
putt and roll cases). `Golfin.Physics.Tests` 271 passed / 0 failed (3 pre-existing skips),
`Golfin.Gameplay.Tests` 253 passed / 0 failed.

**One design defect the tests caught.** The lip-out deflection drops a 1.55 m/s ball to
1.08 m/s while it is *still over the mouth*, so the first implementation captured it one step
later. That made the speed gate cosmetic all the way to ~2.1 m/s, and on screen the ball would
vanish at the rim with no visible deflection. Fixed by suppressing capture while the lip latch
is set; the latch clears once the ball is clear of the mouth, so §4.5's "may come back and drop
on the rebound" still works — it just has to actually come back.

## Second bug, found while shooting the §7 clips: the hole-complete modal never appeared

The slow-putt clip showed `OnShotComplete: terminal=InCup` but **no hole-complete modal**.
Cause is independent of the cup work and predates it:

`Canvas/HoleCompleteModal` was authored **inactive** in `ShellScene.unity`
(`m_IsActive: 0`). `HoleCompleteModalController` subscribes in `OnEnable`
(`GameSession.OnHoleComplete += HandleHoleComplete`), and `OnEnable` never runs on an inactive
GameObject — so the whole chain
`BallSM.OnShotComplete → HoleCompletionBridge → GameSession.MarkHoleComplete → OnHoleComplete`
fired correctly into **zero subscribers**.

This is exactly PIPELINE_HARDENING trap **C2** (modal root must stay active; the child panel
owns visibility). The controller is already written for that arrangement — it overrides `Show()`
to a no-op and `Hide()` to hide only `_widget` — and every sibling modal under `Canvas` is
authored active:

```
HoleCompleteModal:     activeSelf = False   ← the odd one out
TournamentSignupModal: activeSelf = True
TournamentResultModal: activeSelf = True
VersusResultModal:     activeSelf = True
```

Activating the root is safe: the widget's own `Root` and `DimBackground` children are already
inactive, and `HoleCompleteWidget.Awake()` additionally calls `_root.SetActive(false)` — so
nothing renders until `Show()`.

**Fix:** `Canvas/HoleCompleteModal.m_IsActive: 0 → 1` in `ShellScene.unity`. The saved scene diff
is exactly that one line (verified against HEAD — no layout/anchor churn, §14 guardrail).

**Verified:** the `mid` clip now ends on the modal — "✓ SUCCESS · Lomond Country Club · Hole 6 ·
Par 3 · STROKES: 1 (EAGLE)" with the REPLAY and NEXT (Hole 7) cards.

## Third and fourth bugs: two more inactive roots of the same class

Sweeping every root in `ShellScene` for the same defect turned up two more. Both were silent —
every call site null-guards, so nothing ever threw:

| Object | Effect of being authored inactive |
|---|---|
| `Canvas/Toast` | `ToastController.Awake()` never runs → `ToastController.Instance` **null forever** → every toast in the game silently never appears (Gacha "Coming soon", shop messages, `TOAST_COURSE_CLEARED`, tournament result messages). `Awake()` itself ends with `gameObject.SetActive(false)` — proof it is meant to be authored active. |
| `Canvas/FadeOverlay` | `FadeController.Awake()` never runs → `FadeController.Instance` **null** → `GameplaySceneLoader` silently took its documented *"No FadeController available — fall back to an instant cut"* branch, so screen fades never played. |

`FadeOverlay` needed more than activation: it is a full-screen opaque black image with
`CanvasGroup.alpha = 1`, so activating it as authored would have blacked out the boot screen.
`FadeRoutine` sets `alpha = from` at the start of every fade, so authoring `alpha = 0` is correct.

**Fix:** `Toast.m_IsActive 0 → 1`; `FadeOverlay.m_IsActive 0 → 1` **and** its
`CanvasGroup.m_Alpha 1 → 0`.

**Verified at runtime** (previously all null): `ToastController.Instance = SET`,
`FadeController.Instance = SET`, `GameSession.OnHoleComplete subscribers = 2`
(incl. `HoleCompleteModalController`), `FadeOverlay active=True alpha=0.00`, and the boot screen
renders normally — not blacked out.

Everything else in `ShellScene` checked out: the other three `Canvas` modals
(`TournamentSignupModal`, `TournamentResultModal`, `VersusResultModal`) were already active; the
always-on root handlers (`TournamentRoundHandler`, `VersusResultHandler`, managers) were active;
the 22 screens under `ScreensRoot` are `ScreenManager`-driven so their authored state is
irrelevant; and `PersistentUI`/`SettingsScreen` use the correct pattern of inactive children
under an active manager root.

Total `ShellScene` diff for all four fixes: **4 lines**, no layout/anchor churn (§14 guardrail).

## §7 acceptance clips (Hole 6, real player path, `Docs/Physics/videos/`)

| Clip | Arrival speed | Outcome |
|---|---|---|
| `cup_slow.mp4` | ~1.07 m/s | Captures. `InCup at t+1.2s`. Recorded BEFORE the modal fix, so no modal. |
| `cup_mid.mp4` | ~1.47 m/s (just under gate) | Captures. `InCup at t+1.2s`, then the hole-complete modal: "✓ SUCCESS · Hole 6 · Par 3 · STROKES: 1 (EAGLE)". |
| `cup_fast.mp4` | ~2.77 m/s (above gate) | Lips out. **Re-shot 2026-08-06 with the dip-based model** — the ball now clips the lip and *runs on past the hole* (camera follows it beyond the cup) instead of bouncing back at you. **No InCup, no hole-complete.** |

Putter powers 0.41 / 0.49 / 0.75 are calibrated, not guessed: launch speed is linear in power
(measured 0.30 → 1.494 m/s, 0.60 → 2.989 m/s ⇒ v0 = 4.981·p) and the green costs ~0.52 m/s per
metre over the 2 m approach. Club confirmed in-clip: `Club index=3 (PutterIndex=3)`.

**Presentation caveats** (behaviour is correct; framing is not ideal):
- The drop reads as the ball *disappearing* at the lip rather than visibly falling, because there
  is no cup cavity in the green mesh — explicitly out of scope per §8. No lip clipping is visible,
  so the §4.8 occlusion check passes.
- The fast clip's deflection is mostly radial (straight back down the line) rather than a sideways
  kick, because this crossing is near dead-centre. That is geometrically correct — a dead-centre
  lip-out has no tangential component — and is asserted by
  `FastPutt_DeadCentre_LipOut_ReversesRadiallyAndLosesSpeed`. The off-line case is covered by
  `FastPutt_OffCentre_LipOut_PushesBallOffItsLine`.

## Fifth bug: the §4.5 lip-out formula produced a wall bounce, not a lip-out

Cesar watched `cup_fast.mp4` and asked whether the flag had collision. It does not — and it
can't: `BallSimulation` has zero awareness of flags, poles or colliders (its only obstacle input
is `ITreeObstacleProvider`), and `BallAnimator` disables the ball's colliders, makes any
Rigidbody kinematic, and writes `transform.position` straight from trajectory samples. The
rebound was entirely the lip-out code.

Measuring the ORIGINAL §4.5 formula at 2.9 m/s across crossing offsets exposed two defects:

| offset | \|v\| in → out | ratio | deflection |
|---|---|---|---|
| 0 mm | 2.906 → 2.033 | 0.700 | **180°** |
| 10 mm | 2.905 → 2.032 | 0.700 | 133° |
| 30 mm | 2.905 → 2.032 | 0.700 | 81° |
| 50 mm | 2.902 → 2.031 | 0.700 | 27° |

1. **A centre hit reversed 180° at every speed.** `vXZ' = −e·vRad + vTan` with `vTan = 0` is a
   pure reversal, applied identically whether the ball arrives at 1.6 or 6 m/s.
2. **`LipRestitution` never affected speed.** The final rescale to `LipSpeedDamping·|v|` forced
   the outgoing speed to exactly 0.700× at every offset and every speed, so the constant only
   ever set direction — tuning it could not soften the bounce at all.

That also made the 1.5 m/s gate a cliff: 1.49 dropped in, 1.51 came straight back at 70% pace.

**Replacement model (Cesar approved the formula change, 2026-08-05).** Interaction strength now
comes from how far the ball actually sinks into the open mouth while crossing it:

```
chord = 2·√(R² − off²)        off = perpendicular distance of the ball's PATH from the centre
dip   = ½·g·(chord / speed)²  clamped to [0, 1] ball-radii
```

`off` is the perpendicular distance from the cup centre to the ball's **line of travel** — not
the distance at the trigger step. The lip-out fires on the first step whose segment enters the
mouth, where the ball is by definition ≈ one radius out; feeding that in gives
`chord = 2√(R²−R²) ≈ 0` and silently disables the whole interaction. That bug was live briefly
and showed up as every dead-centre crossing from 1.6–4.0 m/s reporting 0° deflection.

The radial outcome then hinges on whether the ball struck the far wall or merely clipped the rim:

```
dip <  1 : vRad' = vRad·(1 − dip·(1 − e))     clears the far rim, runs on, bled toward e·vRad
dip >= 1 : vRad' = −e·vRad                    sank below its equator, hits the wall, comes back
vTan'    = vTan·(1 − dip·(1 − LipSpeedDamping))
```

Deliberately **not** a linear blend from `+vRad` to `−e·vRad`: that form crosses zero at
dip ≈ 0.74 and stopped the ball dead on the rim (measured 1.945 m/s in → **0.083 m/s** out),
which is not a thing a golf ball does. The chosen form keeps magnitude continuous across
dip = 1 and flips only the sign — which is what a marginal lip-out looks like.

Result at dead centre (gate 1.50 m/s):

| launch | \|v\| at cup | \|v\| out | ratio | outcome |
|---|---|---|---|---|
| 1.55 | — | — | — | **captured** |
| 1.70 | 1.645 | 0.588 | 0.36 | runs on, heavily clipped |
| 2.00 | 1.945 | 1.049 | 0.54 | runs on |
| 2.50 | 2.445 | 1.735 | 0.71 | runs on |
| 3.00 | 2.945 | 2.354 | 0.80 | runs on |
| 4.00 | 3.945 | 3.501 | 0.89 | runs on |
| 6.00 | 5.948 | 5.663 | 0.95 | skims over almost untouched |

No cliff at the gate, no reversal for fast centre hits, and `LipRestitution` now genuinely sets
the floor a heavy clip bleeds toward. The model also *reproduces* the architect-locked constant
rather than contradicting it: on a dead-centre crossing `dip` reaches one ball radius at
≈1.5 m/s, which is exactly the USGA/Penner capture speed already in use.

### Making the hole bite harder (Cesar, 2026-08-06: "affect the shot a bit more, like a real hole")

Two independent causes of the weak read, both fixed:

**1. The deflection was taken about the wrong normal.** It used the entry radial (pin→ball at
the trigger step), which on a straight crossing is nearly anti-parallel to travel — the
tangential component came out ≈0, so the ball could only ever be slowed straight down its own
line (≤4° at every offset). A real ball strikes the **far wall**, whose normal is angled from
the line of travel by ≈asin(off/R). `TryCupExitNormal` now solves the chord's exit point and
splits the velocity about the normal there, so an off-centre crossing gets a genuine sideways
push.

**2. The pop was invisible.** `LipPopVy` was an absolute 0.30 m/s scaled by dip → a **0.4 mm**
hop at speed. It is now the FRACTION of the horizontal speed the rim removes that converts to
vertical (dimensionless, default 1.0): hitting an angled wall turns horizontal impulse into
vertical, so a heavy clip pops and a clean skim does not. putt.csv key renamed
`lip_pop_vy_mps` → `lip_pop_fraction`.

Result — near-gate crossings now genuinely grab, fading smoothly to a clean skim at speed:

| launch | offset | ratio kept | deflection | hop |
|---|---|---|---|---|
| 1.7 m/s | 0 mm | 0.36 | 0° | **57 mm** |
| 1.7 m/s | 15 mm | 0.44 | **11.3°** | 44 mm |
| 1.9 m/s | 15 mm | 0.55 | 7.1° | 35 mm |
| 2.2 m/s | 15 mm | 0.67 | 4.3° | 26 mm |
| 2.9 m/s | 0 mm | 0.80 | 0° | 18 mm |
| 2.9 m/s | 48 mm | 0.98 | 0.5° | 0.2 mm |

**Why the old fast clip looked inert:** it crossed ~49.5 mm from centre (R = 54 mm). The
free-fall chord collapses near the rim — `2√(R²−off²)` is only 43 mm there — so dip was 0.06
ball-radii. Physically correct for clipping the extreme edge, but not the shot worth showing.
The clip now uses power 0.58 (~1.92 m/s at the cup) started 20 mm off the pin line and fired
PARALLEL to it, so the ball crosses ~13 mm off-centre: keeps ~62%, kicks ~7° off line, pops
~26 mm. Dead centre gives the biggest hop but zero sideways kick (no tangential component by
construction), hence the deliberate offset.

**Remaining limitation:** at genuinely high speed the deflection is still only a few degrees.
That is honest — a fast ball clipping the hole mostly gets slowed and popped rather than turned
— but the dramatic rim-riding horseshoe is not modelled. `LipRestitution` / `LipSpeedDamping` /
`lip_pop_fraction` are the tuning knobs if you want it more pronounced still.

New regression tests: `FastPutt_DeadCentre_RunsOnInsteadOfBouncingBack`,
`LipOut_StrengthScalesWithCrossingSpeed`, `LipOut_NeverStopsTheBallDeadOnTheRim` (sweeps
1.6→4.0 m/s asserting the radial blend never re-crosses zero). Suite now 16/16.

## What this changes about the plan

Nothing structural — §2/§4 hold as written. Two notes carried into implementation:

1. The fix is squarely §4.3/§4.4: terminate in-sim at the capture step. The 4.78 s tail is the defect.
2. Keep `RealCupDetector` as the fallback but do **not** rely on its height gate; the in-sim check is
   XZ-only per §4.2 and that is now evidence-backed, not just a precaution.
