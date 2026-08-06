# SPEC — canopy_avoidance_v2

**Tier:** 2 — bot behaviour, pure-math diff, unit-test + measurement gated (no video gate).
**Priority:** P2. **Status:** SPEC_READY.
**Figma:** N/A — no UI surface.
**Kickoff:** `Use the implementer subagent on "canopy_avoidance_v2"`

> **⚠ CONCURRENCY.** Another session is working on Shot UI visibility in THIS working tree and
> THIS Unity Editor (`Assets/Scripts/Gameplay/UI/ShotUI/*`, `ShotInProgressUiGate.cs`,
> `Docs/Specs/Quick/shot_ui_hidden_while_shot_in_progress.md`). **Do not enter play mode. Do not
> stage or revert any file you did not create. Never `git add -A`.** This task is pure math +
> EditMode tests and needs neither play mode nor scene edits.

---

## 1. Why

`bot_tree_error_recheck` (Order 352) closed the trunk-avoidance hole, but Cesar's original report
("bots still fire into trees often") is only partly addressed. v1 `BotTreeProbe` is **trunk-only
by design** and rests on an assumption that measurement has now falsified.

**Measured this session on real Hole_08 data (3926 instances) — all numbers reproducible:**

Real trajectory apex, from `BallSimulation.Simulate(..., AeroConfig.Default)`:

| Club | ballSpeed | launch° | carry (m) | **apex (m)** | apex at (m) |
|---|---|---|---|---|---|
| Driver | 75.0 | 10.9 | 132.9 | **7.92** | 73.9 |
| Wood | 70.6 | 9.2 | 109.1 | 5.29 | 59.9 |
| Iron | 48.5 | 20.0 | 106.0 | 11.45 | 57.6 |
| A.Wedge | 46.0 | 24.0 | 108.6 | 14.42 | 59.2 |

Hole_08 canopy band: bottom (trunk top) mean **3.71 m** (min 2.98), top mean **10.55 m**
(max 14.94), canopy radius mean **2.65 m** vs trunk radius mean **0.29 m** — **9.3× wider**.

Two consequences:

1. **The ball never clears the canopy.** A driver apexes at 7.92 m, squarely inside the
   3.71–10.55 m canopy band. v1's `LineHasTrunkInWindows` *skips the apex band entirely*
   ("assumed fly-over — no height model in v1"). That assumption is false: there is no fly-over.
2. **The real obstacle is ~9× wider than what v1 probes.** Blocking rate on the full line at real
   trajectory height: trunk **25.48%**, trunk-or-canopy **60.82%** — i.e. **35.34%** of lines clip
   a canopy that v1 cannot see.

## 1.1 Why this is NOT "mirror the trunk logic for canopy"

Per-position blocking (driver, parabolic apex 7.92 m, full line):

```
frac | carry | trunk% | trunkORcanopy% | canopyOnly%
0.00 |   133 |   0.00 |          14.50 |      14.50
0.12 |   133 |  11.88 |          29.00 |      17.13
0.25 |   133 |  21.00 |          60.88 |      39.88
0.38 |   133 |  31.50 |          84.63 |      53.13
0.50 |   133 |  61.25 |         100.00 |      38.75   <-- no canopy-clear line EXISTS
0.62 |   133 |  47.38 |         100.00 |      52.63   <-- no canopy-clear line EXISTS
0.75 |   104 |   5.38 |          36.75 |      31.38
TOT  |       |  25.48 |          60.82 |      35.34
```

**At frac 0.50 and 0.62 every sampled line is canopy-blocked.** A hard canopy reject (the shape of
the trunk fix) would find nothing on 2 of 7 positions and clamp the bot to its unperturbed line on
61% of shots overall — degrading play, not improving it. It would also flatten the miss model,
which is the thing the difficulty brackets exist to produce.

The physics agrees: `TreeHit.IsTrunk == true` is a reflect + restitution carom; canopy is
**damping only** (`canopyHitDamping = 0.40`). A canopy clip is a partial velocity penalty, not a
stopped ball. Avoid it when it is cheap; never refuse to play over it.

**Therefore v2 is a SOFT PREFERENCE (scored tie-break), not a rejection.**

---

## 2. Scope

### In
- `BotTreeProbe`: new pure static `CountCanopyContacts(...)` — marches the FULL line (no apex-band
  skip) at modelled trajectory height and returns the canopy-contact segment count. Trunk hits are
  reported separately and remain a hard reject.
- `BotTreeProbe`: new pure static `TrySampleTreeAwareAimError(...)` — supersedes
  `TrySampleTrunkClearAimError` at the call site. Keeps trunk rejection EXACTLY as today, and
  among trunk-clear samples prefers the one with the fewest canopy contacts, tie-breaking to the
  **smallest |deltaAimDeg|** so the miss model is preserved when canopy cost is equal.
- `BotTreeProbe`: `ApexForCarry(club, carry)` — per-club apex model, constants measured from
  `BallSimulation` (table in §4.1), with a test that re-derives them from the sim so they cannot
  silently drift.
- `VersusBot`: swap the D2 call to the new helper. One debug toggle
  `[SerializeField] public bool DebugDisableCanopyPreference = false;` — true restores Order 352
  behaviour exactly (trunk-only, first trunk-clear sample wins).
- Extend the 2b log marker: `treeChecked={0|1}` gains `canopyContacts=<n>`.

### Out (do NOT do)
- **No hard canopy rejection.** Blocking on canopy is explicitly forbidden — see §1.1.
- `TrySampleTrunkClearAimError` stays in place, untouched and still tested (Order 352 tests must
  remain green). v2 adds a new entry point; it does not rewrite the old one.
- No change to `TryFindTrunkClearAim` (the pre-2b re-aim ladder), its windows, or its constants.
- No change to `BallSimulation`, tree CSVs, collision profiles, `bot_difficulty.csv`, `Clubs.csv`.
- No `BotDriver` change (it has no 2b error injection).
- Putts (existing `isPutt` exclusion carries over unchanged).
- No play mode, no scene edits, no prefab edits — see the concurrency banner.

---

## 3. Grounding (re-confirm at step 0)

- `Assets/Scripts/Physics/Viewer/BotTreeProbe.cs` — `LineHasTrunkInWindows` (public),
  `TrySampleTrunkClearAimError` (Order 352), constants `NearWindowM=35`, `LandWindowM=35`,
  `ProbeStepM=6`.
- `Assets/Scripts/Physics/Viewer/VersusBot.cs` — D2 block, `MaxAimErrorResamples=5`,
  `DebugDisableTreeRecheck`, `probeCarry`.
- `Assets/Scripts/Physics/Core/TreeObstacleData.cs` — `TreeHit.IsTrunk`, and per-instance
  `TrunkTopY`, `CanopyTopY`, `CanopyRadius` (all scale-multiplied).
- `ITreeObstacleProvider.TestSegment(p0, p1, out TreeHit)` reports canopy hits with
  `IsTrunk == false`. **No new collision code is needed** — v2 only changes WHERE the probe
  samples (full line, real height) and HOW the result is used (scored, not boolean).

---

## 4. Design

### 4.1 Apex model

Ball height along the line is modelled as a parabola through `(0, 0)` and `(carry, 0)`:

```
h(d) = 4 * apex * t * (1 - t),   t = d / carry
```

`apex` per club, measured from `BallSimulation.Simulate(..., AeroConfig.Default)` at full power:

| club | apex (m) |
|---|---|
| Driver | 7.92 |
| Wood | 5.29 |
| Iron | 11.45 |
| A.Wedge / P.Wedge | 14.42 |
| Putter | 0.0 (putts excluded upstream) |

Scale linearly with carry for part-power shots: `apex * (carry / fullCarry)`. This is a
first-order approximation — NOTE it in code. It is materially better than v1's "skip the apex
band" and does not need to be exact: canopy is a soft cost, so a modest height error changes a
tie-break, never whether the bot can play.

**Drift guard (required):** one EditMode test re-derives each apex from `BallSimulation` and
asserts the table matches within ±1.0 m. If the sim is retuned, the test fails loudly rather than
the bot silently aiming on stale numbers.

### 4.2 Scored sampler

```csharp
// Trunk = hard reject (unchanged from Order 352). Canopy = soft cost, tie-break on |delta|.
// Returns false only when ALL samples were TRUNK-blocked -> caller uses deltaAimDeg = 0.
public static bool TrySampleTreeAwareAimError(
    ITreeObstacleProvider trees, Vector3 ball, float safeYaw, float carry, float apex,
    float aimErrorDegMax, int maxTries,
    System.Func<float, float, float> sampleRange,
    out float deltaAimDeg, out int canopyContacts)
```

Algorithm: draw `maxTries` samples. Discard any with a trunk hit. Among the survivors pick the
lowest canopy-contact count; on a tie pick the smallest `|deltaAimDeg|`. If no survivor,
`deltaAimDeg = 0`, `canopyContacts = -1`, return false (identical to today's clamp).

Note this **cannot** be worse than Order 352: the trunk gate is unchanged, and the canopy score
only chooses among samples that were all already acceptable.

### 4.3 VersusBot wiring

Swap the D2 helper call; keep power error, D3 club noise, putt handling, and logging order
untouched. Extend the log line to `... treeChecked={0|1} canopyContacts=<n>`.

---

## 5. Traps

- **Never let canopy block a shot.** If a reviewer sees the bot clamping more often than Order 352
  did, the design was implemented wrong.
- **Order 352's tests must stay green.** `TrySampleTrunkClearAimError` is not modified.
- Full-line probing removes the apex-band skip **only in the new canopy path**;
  `LineHasTrunkInWindows` keeps its windows so trunk behaviour is bit-identical.
- Treeless (`trees == null`) must remain first-sample-accepted, single draw.
- Production-safe: no `#if UNITY_EDITOR` — `VersusBot` ships in player builds.
- Ball Y must be the REAL ball position (Lesson AH). Never interpolate, never trust an injected
  lie's Y.

---

## 6. Acceptance / Gates

1. **EditMode tests** (extend `BotTreeProbeTests.cs`, seeded/mock sampler):
   - trees == null → first sample, single draw, `canopyContacts == 0`.
   - all samples trunk-clear and canopy-free → returns the smallest |delta| among ties.
   - one sample canopy-free, others canopy-heavy → returns the canopy-free one even if its |delta|
     is larger.
   - all samples trunk-blocked → false, `deltaAimDeg == 0` (Order 352 clamp preserved).
   - apex drift guard: table matches `BallSimulation` within ±1.0 m.
   - Full suite green (baseline **995 total / 992 passed / 0 failed / 3 skipped** — the 3 skips are
     pre-existing Stage C1 skips in `HoleCompleteDriverTests`; report the real numbers, do not fold
     skipped into passed).
2. **Measurement gate (this is the primary objective gate — no video, no Figma):** a read-only
   `script-execute` sweep over Hole_08 reproducing the §1.1 table, then re-running it with the new
   sampler, dumped to `Docs/Specs/Active/canopy_avoidance_v2/canopy_invariants.json` with per-
   assertion PASS/FAIL:
   - `clampRate_v2 <= clampRate_order352` (**hard fail if it rises** — the §1.1 infeasibility risk).
   - `meanCanopyContacts_v2 < meanCanopyContacts_order352` (the fix must actually reduce contact).
   - `trunkBlockRate_v2 == trunkBlockRate_order352` (trunk behaviour unchanged).
3. **No-op proofs:** treeless hole identical to HEAD; putts unchanged;
   `DebugDisableCanopyPreference = true` reproduces Order 352 exactly.

---

## 7. Handoff

- Expected diff: `BotTreeProbe.cs` (additive), `VersusBot.cs` (D2 call swap + field + log),
  `BotTreeProbeTests.cs` (new tests). **No asmdef, no sim, no CSV, no prefab/scene edits, no
  play mode.**
- Do not touch `Assets/Resources/FX/M_Splash*.mat` (standing ban, dirty from an unrelated source)
  or anything under `Assets/Scripts/Gameplay/UI/ShotUI/` (another session, in flight).
