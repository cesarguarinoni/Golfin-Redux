# IMPLEMENTER REPORT — `shot_timing_power` (F15)

**Iteration:** iter-1
**Iteration shape:** shot-input:timing-slab-never-read
**Driven by:** Claude Code main thread at Cesar's direct request (not the subagent pipeline).
**Baseline:** HEAD `2b0cd5cb2`, zero `Assets/` paths dirty at kickoff — see `HEARTBEAT.log`.

---

## What was wrong, in one line

`CommitFlick` read `PowerNormalized` and `_degradationYawRad` and nothing else — `_arrowProgress`
was published to the view for drawing and then thrown away, so **a red flick and a green flick
produced a byte-identical shot**. Cesar's read of it ("I am not sure the colored arrows … are
having any effect either") was exactly right.

## What it does now

`flickMag = PowerNormalized × timingMul`, where `timingMul` is a piecewise-linear map of the slab
progress **sampled at the aim latch** through the same band edges the slab is coloured with:

| `timing01` | multiplier |
|---|---|
| 0.00 (cone base, deep red) | 0.70 |
| 0.00 → 0.45 | lerp 0.70 → 0.90 |
| 0.45 (gold line) | 0.90 |
| 0.45 → 0.85 | lerp 0.90 → 1.00 |
| ≥ 0.85 (green line and up) | 1.00 |

Applied **after** the overpower clamp (D6). No touch swing ⇒ no sample ⇒ multiplier exactly 1.0.

---

## Acceptance checklist

### Automated — verified here

| # | Item | Verdict | Evidence |
|---|---|---|---|
| A1 | `ShotTimingPowerTests` 1–8 pass | **PASS** | All 8 present and green. Proven by tripwire, not assertion — see § Tripwires. |
| A2 | Whole `Golfin.Gameplay.Tests` assembly green, **no filter** | **PASS** | `tests-run` EditMode, no filter: **1977 total / 1974 passed / 0 failed / 3 skipped**. The 3 skips are pre-existing `HoleCompleteDriverTests` Stage-C1 skips, untouched by this task (identical in the pre-change run). Note: this tool's `testAssembly`/`testNamespace` filters return "no tests found" in this project (known — memory `reference_tests_run_ignores_class_filters`), so the whole EditMode mode was run, which is a superset of the requirement. |
| A3 | Unity Console has no errors related to this task | **PASS** | Post-refresh console: zero `Error` entries. Only pre-existing `CS0618`/`CS8632` obsolete-API and nullable warnings in `Assets/Scripts/UI/Editor/*`, `Assets/Scripts/Editor/*` — none in any file this task touched. |
| A4 | `ConeBandPalette` bands still resolve to 0.45 / 0.85 | **PASS** | Live read via `script-execute`: `palette gold=0.45 green=0.85 redY=0`, and `cfg gold=0.45 green=0.85`. Test 8 asserts the identity. The values are byte-identical to the literals they replaced, so **nothing moves visually**. |
| A5 | `controls.csv` round-trips the four new keys | **PASS** | `ControlsConfigLoader.Load()` returned `gold=0.45 green=0.85 mulRed=0.7 mulGold=0.9`. (Worth verifying because a CSV row without a matching loader `case` silently warns and is dropped — `RingFrac` already has that defect; see § Findings.) |
| A6 | Bot parity — sampleless drivers multiply by 1.0 | **PASS** | `NoTouchSamples_MultiplierIsOne` and `FireDebugShot_Unaffected` assert the **resolved launch speed** equals the pre-F15 baseline shot, not merely that the multiplier variable reads 1.0. Baseline driver speed 93.77 m/s in both. |
| A7 | Real entry path exists (PIPELINE_HARDENING rule 2) | **PASS** | `ClubHandleDragger.cs:47/56/64` calls `_shotController.PushTouchSample(e.position)` from `OnBeginDrag`/`OnDrag`/`OnEndDrag`. The production thumb gesture is what feeds the latch, and the latch is what feeds the timing sample. No test-only seam was added. |

### Needs Cesar's thumb — cannot be verified without a real touch gesture

These four acceptance items require a **human finger swiping the real club handle**. The timing
sample only exists when `PushTouchSample` receives a genuine down-then-up gesture, which no
automation in this project produces (bots and capture drivers deliberately push no samples — that
is D4, the mechanism that keeps their shots unchanged). Synthesising a gesture to "verify" this
would be verifying my own stub, not the game.

| # | Item | Status |
|---|---|---|
| M1 | Play, Hole 01, driver, `LogResolution` on: three 100 %-pull shots flicked on green / gold / red → log shows `timing01` in the matching band, `timingMul` ≈ 1.0 / ~0.9 / ~0.7, carry ordered green > gold > red | **MANUAL** — the log line is wired (`timing01={..:F2} timingMul={..:F2}`); the carry ordering follows arithmetically from A1/A6 (velocity is linear in `flickMag` below 1.0), but the *feel* and which band you actually hit are Cesar's call. |
| M2 | Same three with debug toggle 6 (`ForcePerfectTiming`) ON → all three carries equal | **MANUAL** — mechanism proven by `ForcePerfectTiming_OverridesRed`. |
| M3 | Putter: red-band putt visibly shorter than green-band putt at equal pull | **MANUAL** — and this is the **D5 decision point**, see § Open for Cesar. |
| M4 | HUD shows `× 0.xx` during an off-time flick and nothing extra on a green one | **MANUAL** — string branch is in; glyph safety pre-verified (see § Findings, F3). |

I have **not** claimed a PASS on any of M1–M4. No screenshots are cited anywhere in this report
because I did not enter play mode; there are no fabricated frames to audit.

---

## Tripwires (evidence that the tests are real, not merely that they ran)

`tests-run` reports only an aggregate summary — individual passes are invisible. So both claims
that matter were proven by deliberately breaking them:

**Tripwire A — does the suite execute at all?**
Flipped `ConeBandPalette_MatchesConfig`'s red-edge assertion to expect `0.123`. Run reported:
`Golfin.Gameplay.Tests.ShotTimingPowerTests.ConeBandPalette_MatchesConfig` **Failed**,
`Expected: 0.123 But was: 0.0`; passed count 1974 → 1973. The class is discovered and its tests run.

**Tripwire B — does the multiplier actually reach the physics, or only the log line?**
Commented out `flickMag *= timingMul;` in `CommitFlick`, leaving `LastTimingPowerMul` still set.
Run reported `LatchOnRedBase_RedMultiplier` **Failed**:
`The multiplier must reach the resolved velocity, not just the log line — Expected: 65.637 ± 0.938, But was: 93.768`.
93.768 × 0.70 = 65.637. The penalty is in the resolved `ShotInput.velocity`, not in a debug string.

Both tripwires reverted; final full run green (1974 passed / 0 failed). Verified reverted:
`grep -c TRIPWIRE ShotController.cs` = 0, `grep -c 0.123f ShotTimingPowerTests.cs` = 0.

---

## Files modified or created

Every `Assets/` path in the final porcelain appears below (kickoff had zero dirty `Assets/` paths).

| File | Change |
|---|---|
| `Assets/Scripts/Gameplay/Input/ShotController.cs` | `_timingAtLatch` sampled at the latch, cleared on unlatch + `ResetSwingSamples`; `TimingPowerMultiplier()`; applied after the overpower clamp; `timing01`/`timingMul` in `LogResolution`; `LastTimingAtLatch` + `LastTimingPowerMul` getters; `PublishState` carries the live multiplier. |
| `Assets/Scripts/Gameplay/Input/ShotInputState.cs` | `TimingPowerMul` readonly field + defaulted ctor param. |
| `Assets/Scripts/Gameplay/Config/ControlsConfig.cs` | Four fields + four `Default` values. |
| `Assets/Scripts/Gameplay/Config/ControlsConfigLoader.cs` | Four `case` labels. |
| `Assets/Resources/Gameplay/controls.csv` | Four rows with the D2/D6 rationale in the notes column. |
| `Assets/Scripts/Gameplay/UI/ShotUI/ConeBandPalette.cs` | `BandGoldY01`/`BandGreenY01` become getters over `ControlsConfig.Default`; `BandRedY01` stays `const 0`. |
| `Assets/Scripts/Gameplay/UI/ShotUI/ConeMeshGraphic.cs` | `OnEnable` re-syncs the three serialized band fields from the palette before the first mesh build. |
| `Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs` | One HUD string branch in `UpdateHUD`. |
| `Assets/Scripts/Gameplay/Tests/ShotTimingPowerTests.cs` (+`.meta`) | **New** — 8 tests. |
| `Docs/Physics/PHYSICS_TUNING_CHANGELOG.md` | **F15** entry (D1–D6, the model table, the 20 %-per-0.1 s rationale, files, tests). |
| `Docs/Game Design/SHOT_CONTROLS_DESIGN.md` | §3.4 marked IMPLEMENTED 2026-08-29 with the latch-sampling rationale. |
| `Docs/AI_CONTEXT.md`, this folder's `STATUS.md` / `IMPLEMENTER_REPORT.md` / `HEARTBEAT.log` | Bookkeeping. |

**No scene or prefab was modified.** Two scenes were opened *additively* to audit serialized band
values and the HUD font, then closed with `CloseScene(…, removeScene: true)`; final porcelain
carries no `.unity`/`.prefab` paths, only `ShellScene` remains open, and it is not dirty.

---

## Findings surfaced (not fixed — out of scope, flagging per "surface, don't rebuild")

- **F1 — `RingFrac` is in `controls.csv` but has no loader `case`.** Pre-existing, unrelated to this
  task. Every `ControlsConfigLoader.Load()` therefore logs
  `[ControlsConfigLoader] Unknown key 'RingFrac' in controls.csv — skipped` and the CSV value is
  silently dropped. Harmless today only because `Load()` has no production call sites at all. I
  left it alone (minimal diff), but it is a one-line fix if you want it.
- **F2 — the `ConeMeshGraphic` `OnEnable` re-sync is a no-op today.** The spec asked me to report
  the serialized values if they differed from 0.45/0.85. They do not: all five files carrying a
  `ConeMeshGraphic` (`ShotConeTest`, `LabScaffold`, `PhysicsLab_Hole1`, and two `_Recovery`
  snapshots) already hold `_bandGoldY01: 0.45` / `_bandGreenY01: 0.85`. The re-sync is guarded by
  an equality check, so it does not dirty the graphic — it is insurance against a future prefab
  drifting, not a fix for present drift.
- **F3 — the HUD `×` is glyph-safe.** `PowerHUD` uses `Rubik-SemiBold SDF`, atlas population mode
  `Dynamic`, and its source font file carries U+00D7 — so the multiply sign bakes on demand and
  cannot render as a tofu box. I queried the **source `Font`** only and never called
  `TryAddCharacters` on the SDF asset, so there is no atlas churn (cf. the NotoSansJP 7 KB → 2.2 MB
  scar).
- **F4 — `LabScaffold`'s `ConeRoot` has `_powerHUD` unwired (null).** Pre-existing; `UpdateHUD`
  early-returns on null so nothing breaks, but the `×` feedback will not appear in that scene.
  `PhysicsLab_Hole1` has it wired correctly. Relevant if you test M4 in the scaffold.

---

## Spec deviations (2, both flagged rather than silently absorbed)

1. **§4 says the HUD branch fires "during `Flicking`". `Flicking` is never published.**
   `CommitFlick` sets `State = Flicking` and then `State = Resolving` before returning, and
   `PublishState()` only runs *after* the state-machine step — so no `ShotInputState` with
   `State == Flicking` ever reaches `ShotConeView`. Implementing the branch literally would have
   produced dead code. I gated it on `TimingPowerMul < 1f` instead, which fires during **`Timing`**
   — from the instant the latch samples an off-time slab until the shot resolves. That is both the
   moment the feedback is useful and the only moment it can be seen. The spec's intent ("show the
   penalty, one string branch, nothing else") is met exactly; its stated trigger was not reachable.

2. **§5 telemetry: I did nothing, as instructed — but the conditional did partly trigger, so here
   is the reasoning.** `TelemetryHooks.cs` *does* carry a `ShotTaken` payload. However it is built
   from `GameSession.ShotHistory` (a post-resolution shot **record** in Assembly-CSharp), not from
   `ShotController` — and `Golfin.Gameplay.Input` is `autoReferenced: false`, which is precisely why
   `ShotTelemetryRelay` exists to re-raise its two events. Reaching `LastTimingAtLatch` from there
   would mean adding a field to `ShotRecord` *and* a new relay event — i.e. inventing the
   shot-committed payload §5 explicitly forbids. So: **no telemetry change.** If you want `timing01`
   in analytics, it is a small task of its own and I would route it through `ShotRecord`.

---

## Open for Cesar (decisions I deliberately did not make)

- **D5 — do putts pay the timing penalty?** They do right now, per the spec's default reading of
  §3.4 (which exempts putts from *degradation*, not from off-time power). If a red-band putt
  falling ~30 % short feels punishing rather than skilful, it is **one line**: an
  `if (IsPutt) return 1f;` at the top of `TimingPowerMultiplier()`. Spec said flag it, don't decide
  it — flagging it. M3 is the test that answers it.
- **The tuning numbers.** 0.70 / 0.90 and the 0.45 / 0.85 band edges are the spec's proposed values,
  not measured ones. All four live in `controls.csv` *and* `ControlsConfig.Default` — change both
  together (`controls.csv` is documentation for this system, as it is for every other key).
- **Does the `× 0.87` in the HUD read as clutter on device?** §4 anticipated you might drop it.
  Deleting it is the two-line `UpdateHUD` branch plus the `ShotInputState` field.
