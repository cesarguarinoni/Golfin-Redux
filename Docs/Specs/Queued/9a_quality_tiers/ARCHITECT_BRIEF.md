# Architect brief — `9a` quality tiers (Phase 2)

**For Cesar to take to the Architect. Written 2026-08-27 by Claude Code, off the back of the
`perf_phase1_free_wins` device pass.** Everything below is measured on an iPhone 15 Pro Max unless
marked otherwise. Plan reference: `Docs/PERF_OPTIMIZATION_PLAN.md` §3 / §5 Phase 2.

---

## 1. Why this is next, in one number

Phase 1 got every measured pose to 60 fps **cold**. It does not hold under sustained load:

| pose | primary sample (cold) | after 45 s at thermal Serious |
|---|---|---|
| H08 tee | 60.0 fps | **47.5** (raws 40.8 / 60.0 / 47.5) |
| H06 tee | 60.0 fps | **40.7** (raws 60.1 / 40.7 / 35.2) |
| H01 tee | 59.8 fps | 59.6 — holds |
| H08 mid-flight | 59.9 fps | 60.0 — holds |

A single static 60 fps target with no tier cannot absorb that. This is the gap Phase 2 exists to
close, and it is the strongest argument for the tier system being next rather than Phase 3.

Full data: `Docs/Reports/perf_baseline_2026-08-26.md` §11.2.

---

## 2. What Phase 1 already consumed, so the spec does not re-spend it

The plan's §4 options A–D were Phase 1's scope. Three landed; **two of the plan's assumptions did
not survive measurement** and the tier spec should not inherit them.

| Plan option | Status | Note for the spec |
|---|---|---|
| **A** — kill the double render (shell camera off) | **SHIPPED** | −11.6 ms. Also made fps thermally independent at the tee. Restored from `OnDestroy`, verified on device 8/8. |
| **D** — remove `DecalRendererFeature` | **SHIPPED** | Project has zero decal consumers. |
| **C** — terrain `basemapDistance` + `drawInstanced` | **BOTH DROPPED — do not re-spec as written** | See §3. |
| **B** — shadow diet | **NOT DONE** — correctly left for per-tier | Still the biggest per-tier lever. See §4. |
| **G** — frame pacing | not done | Low tier at 30 fps still open. |

---

## 3. ⚠️ Plan §4 Option C is wrong and needs rewriting

The plan says *"Terrain `DrawInstanced` on for all tiers"* (§3) and credits option C with −6.31 ms.
**Neither reproduces.** Measured on device, pinned sky + pinned yaw, same build, one variable:

| | batches | triangles | render ms |
|---|---|---|---|
| `basemapDistance` 1000 (authored) | 1,848 | 1,779,839 | **13.35** |
| `basemapDistance` 100 + instanced | 1,848 | 1,779,839 | **13.48** |

Identical geometry; the "optimised" variant is marginally **slower**. Frame diff between the two:
mean 2.01/255, below the render noise floor.

`drawInstanced` was dropped too — an Editor A/B showed it measurably **flattens distant terrain**
(mid-rough patch stddev 13.12 with it on vs 22.10 off; far hillside 2.52 vs 8.84) for no measurable
gain, and it carries a device-only risk the Editor cannot surface: **every hole scene ships
`m_DrawInstanced: 0`, the flag is set purely at runtime, and `GraphicsSettings m_InstancingStripping`
is `StripUnused`** — so the terrain's instanced shader variants may not exist in a player build at
all. Same class as the K5 tree-wind stripping.

Also worth knowing before anyone re-opens the basemap lever: these terrains ship
`baseMapResolution = 512` over a 668 m hole = **1.30 m per basemap texel**. There is very little
headroom before it costs visible detail, and raising it is a TerrainData edit.

**Ask for the spec:** either drop Option C entirely, or re-scope it as "raise `baseMapResolution`
first, then re-measure" — but not as a free per-tier lever.

---

## 4. Where the remaining budget actually is

Measured per-pose cost after Phase 1 (primary sample, pinned sky/yaw, 3 runs, medians):

| pose | fps | frame ms | batches | triangles | shadow casters |
|---|---|---|---|---|---|
| H08 tee | 60.0 | 16.67 | 3,014 | 2,369,599 | — |
| H01 tee | 59.8 | 16.72 | 1,957 | 1,072,738 | 580 |
| H06 tee | 60.0 | 16.68 | 4,006 | 3,882,347 | 1,523 |
| H08 mid-flight | 59.9 | 16.70 | 2,071 | 1,527,874 | — |

H06 is the worst case: **3.88 M triangles and 4,006 batches**, and it is the pose that collapses
hardest under heat (60.0 → 40.7). Its triangle count is confirmed heightmap density —
`2049²` over a 229 × 101 m terrain, roughly 7× the samples/m² of Hole 08 (report §10.5). That is an
**importer** fix, not a tier lever, and it is arguably worth more than any tier setting for H06.

Shadow casters (H06: 1,523) are the obvious per-tier target — plan Option B, untouched.

---

## 5. Harness the spec can rely on (all working, all on device)

- **`PerfBaselineBot`** — pinned yaw, pinned sky, thermal state, on-device `ProfilerRecorder`
  counters, a frame PNG per measurement. Jobs 9–12 are the Phase 1 after-poses; add tier jobs
  the same way (indices 0–8 are frozen so the Phase 0b logs stay readable).
- **`PinSky()`** — ⚠️ **read this before specifying any A/B.** `SkyRandomizer` rolls a new sky per
  app launch, so *no* frame comparison before this existed was taken under controlled lighting —
  including the ✅/❌ verdicts in report §10.4. Runs saw sun elevations of 74.5°, 45° and 20.2°; the
  low-sun runs throw long canopy shadows that read as "dark patches appearing between builds".
  **A frame A/B without a pinned sky is not evidence.** With it pinned, batches and triangles are now
  *identical* across all three runs of every hole.
- **Teardown gate** (`P1_teardown`, job 13) — drives the real `confirmQuitButton.onClick` and writes
  `teardown_invariants.json` with per-assertion PASS/FAIL. The pattern to copy for a tier-switch gate.
- **`DevFpsOverlay`** — fps / frame ms / GC KB/f / thermal on the glass in dev builds. It
  deliberately suppresses itself while the bot is armed so it cannot corrupt `gcPerFrameB`.
- **Build/install pipeline** — `CIBuild.BuildIOSDev` → `xcodebuild` → Info.plist patch → re-sign →
  `devicectl install`, ~10 min end to end. Documented in the Phase 1 report §7.

---

## 6. Two measurement traps to write into the spec

1. **`renderMs` from `ProfilerRecorder` is unreliable.** It intermittently reports ~3.3–4.2 ms on
   frames whose `frameMs` is 16.7. Use `fps` and `frameMs` for verdicts; quote `renderMs` only when
   all three runs agree.
2. **Global image diffing cannot resolve terrain/foliage changes.** Editor noise floor from two
   consecutive renders of the *same* config is mean **6.36**, while every terrain-config diff
   measured 6.97–7.85 — indistinguishable. Wind-animated foliage and water dominate. Compare named
   regions against a pinned-sky reference, or judge by eye at 1:1.

---

## 7. Open questions for the Architect

1. **Tier detection** — plan §3 proposes an iOS `deviceModel` → chip-generation table, unknown → Mid.
   Confirm the table lives in code (not CSV) and where the Settings override persists in SaveData.
2. **Low tier at 30 fps** — plan §6 flags that `arrow_speed_retune` F13 was tuned at 30 fps and
   `ui_frame_pacing` moved the game to 60. Aim-arrow feel must be re-checked at both rates before
   Low ships at 30. Who owns that check?
3. **Does the tier system get a thermal input?** Phase 1's data says the static-tier table cannot see
   the failure mode that actually bites (60 → 40 fps under sustained heat). Plan Option H
   (Adaptive Performance) was deferred pending Phase 0 evidence — **that evidence now exists.**
4. **H06 heightmap density** — importer fix, out of tier scope, but it is the single biggest
   remaining win on the worst hole. Should it be sequenced before or alongside `9a`?
5. **Option C** — drop, or re-scope per §3 above?

---

## 8. Known-unrelated item

The flat-terrain look Cesar flagged on build 2314 was chased to a conclusion: **pre-existing, not
Phase 1**. Proven against real pre-Phase-1 code (`a98008f6d` checked out for `PhysicsLabController.cs`
and both Settings assets) — the near-fairway patch is bit-identical across HEAD, all-reverted and
pre-Phase-1. `m_UseNativeRenderPass` was also eliminated as a cause. Cesar has since confirmed the
current look is fine. Not a tier concern; recorded so it is not re-litigated.
