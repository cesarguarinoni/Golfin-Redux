# SPEC — tree_aware_bot (Order 351)

**Tier:** 3 — runtime spatial math + production bot behaviour + before/after gameplay video gate.
**Priority:** P3. **Status:** SPEC_READY.
**Repo HEAD at spec time:** `fbd7513a3`.
**Figma:** N/A — no UI surface. Deliverable is bot logic + a gameplay video.
**Handoff file:** `Docs/Specs/Active/tree_aware_bot/SPEC.md`
**Kickoff:** `Use the implementer subagent on "tree_aware_bot"`

---

## 1. Why

Deferred **Phase 2 of `tree_collisions` (Order 348)**. Trees now have deterministic collision in the sim
(trunk = hard reflect, restitution 0.15; canopy = one-time entry impulse). But every bot is **tree-BLIND**:
it aims straight at the pin and only probes WATER/Sand *surfaces* (VersusBot H2), never trunks — so on
tree-dense holes it fires into a trunk and caroms.

This is **READ-side** (bot queries the existing obstacle store) — **NOT a sim change**. TreeObstacleProvider,
the tree CSVs, and the collision profiles are all untouched.

---

## 2. Scope

### In
- New **production-safe** static helper `BotTreeProbe` in `Golfin.Physics.Viewer`: a windowed flat-XZ
  trunk probe + a trunk-clear re-aim/layup ladder (mirrors VersusBot's `TrySafeLanding` shape).
- New read-only getter `PhysicsLabController.GetTreeProvider()`.
- Wire the helper into **BOTH** bots so they behave the same w.r.t. trees: **VersusBot** (1v1 production)
  and **BotDriver** (editor capture / solo harness).

### Out (v1 — do NOT do)
- **Canopy** avoidance (fly under/over). Trunk-only. Canopy needs a height model → v2.
- Any **ballistic/apex height model**. We use ground-Y over *low-ball windows* only (see §4.2).
- Trunks in the deep landing/roll zone beyond the land-window (descending-height model) → v2.
- **Full VersusBot⟷BotDriver behaviour unification.** VERIFIED this session: `BotDriver.PlayHoleToCup`
  aims straight — it has **no** H2 water probe, **no** `TrySafeLanding`, **no** H3 slope read, **no** 2b
  difficulty injection. This task adds **trunk-avoidance only** to both. Porting H2/H3/2b into BotDriver is
  a separate, larger task — file `bot_behaviour_unification` if wanted. Do not scope-creep into it here.
- Any `BallSimulation` / sim-path edit. Any tree CSV or collision-profile re-bake. `TreeObstacleProvider`
  stays byte-for-byte untouched.

---

## 3. Grounding (verified this session — re-confirm at step 0, do not trust stale notes)

**`TreeObstacleProvider` is already usable read-side; NO new accessor and NO asmdef change needed.**
- `Assets/Scripts/Physics/Runtime/TreeObstacleProvider.cs` — class is **`public`** (the kickoff's "it's
  `internal`, add an accessor" is STALE). It implements `ITreeObstacleProvider` and the sim drives it
  **through that interface** (`BallSimulation.Simulate(..., _treeProvider)`), so the query method is on the
  interface: **`bool TestSegment(fp3 p0, fp3 p1, out TreeHit hit)`** → returns `true` with `hit.IsTrunk==true`
  for a trunk crossing, `hit.IsTrunk==false` for a canopy entry, `false` for no interaction.
- `ITreeObstacleProvider`, `TreeHit`, `fp`, `fp3` all live in asmdefs both bots **already reference**
  (`Golfin.Physics`, `Golfin.Physics.Math`). Querying via the **interface** means the Viewer asmdef never
  needs a reference to `Golfin.Physics.Runtime` → **Lesson-W trap does NOT fire**, no cycle.
  - **Step-0 confirm:** re-read the top of `TreeObstacleProvider.cs` and grep the `ITreeObstacleProvider`
    interface decl to verify `TestSegment` + `TreeHit` are visible from `Golfin.Physics.Viewer`. If (only if)
    `TestSegment` turns out to be on the concrete class but NOT the interface, add it to the interface — that
    is still read-side. Do not assume; verify.

**Two facts that shape the algorithm (§4.2):**
1. **`TestSegment` scans only a 3×3 grid-cell neighbourhood around `p0`** (cell size 10 m → ~±10 m). A single
   `TestSegment(ball, pin)` call will MISS a trunk 100 m downrange. The probe **must march the line** in
   ≤ cell-size steps.
2. **The trunk test is height-gated:** it only reports a hit when the interpolated `hitY ∈ [tree.BaseY,
   tree.TrunkTopY]`. Feed it flat ground-Y over the whole line and every distant trunk a drive sails *over*
   reports a (false) hit → over-conservative detours. We avoid this by only probing where the ball is
   provably LOW (near the ball, and near the target) — see windows in §4.2.

**Where the provider lives / how bots reach it.**
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`: private field `ITreeObstacleProvider _treeProvider`,
  set in `TryLoadBakedProviders(holeId)` from `Resources.Load<TextAsset>($"HoleData/{holeId}/tree_obstacles")`
  → `TreeObstacleLoader.LoadInstances` → `TreeObstacleProvider.Create(...)`. **`Create` returns `null` when
  there are no trees** → `_treeProvider == null` on treeless holes / lab flat-ground.
- Public getters `GetGround()` / `GetSurfaces()` already exist (~the "Public accessors" region). Add
  `GetTreeProvider()` right beside them (§4.1).
- Both bots already hold a `PhysicsLabController`: VersusBot `_controller`; BotDriver
  `FindObjectOfType<PhysicsLabController>()` per shot. Both already call `ctrl.GetSurfaces()`.

**Shared-helper precedent:** `Assets/Scripts/Physics/Viewer/BotClubSync.cs` — static, production-safe,
`Golfin.Physics.Viewer`, called by BOTH `VersusBot.TakeShot` and `BotDriver.PlayHoleToCup`
(`BotClubSync.SyncToClubContext(club, callerTag)`). **Mirror its placement + pattern for `BotTreeProbe`.**

**Tree density per hole** (rows − header, `Assets/Resources/HoleData/Hole_NN/tree_obstacles.csv`):
Hole_08 = **3927** (densest), Hole_13 = 3391, Hole_05 = 3367, Hole_02 = 3315, Hole_12 = 3027 … Hole_04 = 267.
**Hole_17 has no tree CSV → provider null → no-op path.**

---

## 4. Design

### 4.1 Getter (PhysicsLabController)
Add beside `GetGround()` / `GetSurfaces()`:
```csharp
// tree_aware_bot (Order 351): read-only exposure of the per-hole tree provider for bot
// trunk-avoidance. Null on treeless holes / lab flat-ground. Read-side only — no sim change.
public Golfin.Physics.ITreeObstacleProvider GetTreeProvider() => _treeProvider;
```
This is the only edit to PhysicsLabController.

### 4.2 `BotTreeProbe` (new file `Assets/Scripts/Physics/Viewer/BotTreeProbe.cs`)

Production-safe (NO `#if UNITY_EDITOR`), `namespace Golfin.Physics.Viewer`, `public static class BotTreeProbe`.
Uses `UnityEngine.Vector3/Vector2`, `Golfin.Physics.Math.fp/fp3`, `Golfin.Physics.ITreeObstacleProvider`/`TreeHit`,
`Golfin.Physics.ISurfaceProvider`/`SurfaceType`. All in already-referenced asmdefs.

**Public entry:**
```csharp
public static bool TryFindTrunkClearAim(
    ITreeObstacleProvider trees,     // ctrl.GetTreeProvider() — null => returns false (no trees)
    ISurfaceProvider     surfaces,   // ctrl.GetSurfaces()      — keeps candidate lines surface-safe
    Vector3 ball,                    // current ball world pos (uses ball.y as the low-ball proxy)
    float aimYaw,                    // current intended aim (radians, Atan2(z,x) convention)
    float targetDist,                // current intended carry (m)
    out float safeYaw, out float safeDist);
```
Returns **`true`** only when the current straight line has a trunk in a probe window AND a trunk-clear +
surface-playable alternative was found; then `safeYaw`/`safeDist` carry it. Returns **`false`** (leave the
bot's line unchanged) when: `trees == null`; the straight line is already clear; or nothing clear was found
(bot keeps its line + logs — same spirit as H2's "no safe landing → original line").

**Constants (v1 — plain consts in the helper; NOTE they can move to `bot_clubs.csv` header later, cf.
VersusBot `_slopeAimGain`):**
```
NearWindowM   = 35f   // probe trunks within this many m of the ball (rising, low)
LandWindowM   = 35f   // probe trunks within this many m of the target (descending, low)
ProbeStepM    = 6f    // march step (< 10 m cell so consecutive 3×3 scans overlap — no missed trunk)
LayupStepM    = 8f    // walk-back step for the ladder (matches VersusBot LayupStep)
LayupMinDistM = 10f   // min layup target (matches VersusBot LayupMinDist)
OffsetsDeg    = { -10, +10, -20, +20 }   // retarget angles (matches VersusBot OffsetDegrees)
```

**Core predicate — `LineHasTrunkInWindows(trees, ball, yaw, dist)`** (private):
- `nearEnd  = Min(NearWindowM, dist)`, `landStart = Max(dist - LandWindowM, nearEnd)`.
- March `d` from 0 to `dist` by `ProbeStepM`. For each sub-segment `[d, Min(d+ProbeStepM, dist)]`:
  - **Skip if the sub-segment lies entirely in the apex band** `(d > nearEnd && d+step < landStart)` — assume
    fly-over there (no height model).
  - Else build `p0 = (ball.x + d·cos, ball.y, ball.z + d·sin)`, `p1` at `d+step`, as `fp3` via
    `fp.FromFloat(...)`; call `trees.TestSegment(p0, p1, out hit)`; if `hit && hit.IsTrunk` → return `true`.
- Return `false` (no trunk where the ball is low).

  *Y proxy:* both endpoints use `ball.y`. Exact near the ball; a good enough low-ball proxy near the landing
  on mostly-flat Lomond fairways. Accepted v1 limitation for holes with big tee↔green elevation change.

**Ladder (only runs when the straight line is blocked):** reuse the `TrySafeLanding` shape but with a
**combined predicate** = *no trunk in windows* **AND** *landing surface playable* (and waypoints not Water):
1. **Walk-back layup on the SAME yaw:** for `d = dist` down to `LayupMinDistM` step `LayupStepM`, accept the
   first `d` where `!LineHasTrunkInWindows(trees, ball, aimYaw, d)` AND the landing surface at
   `LandingXZ(ball, aimYaw, d)` is playable (reuse the surface classify + `IsPlayable`/`!IsAvoid` rules from
   VersusBot H2). → `safeYaw = aimYaw; safeDist = d; return true`.
2. **Rotate:** for each `off ∈ OffsetsDeg`, repeat the walk-back on `aimYaw + off·Deg2Rad`. First clear +
   playable → return it.
3. None found → `return false`.

Surface classification lives in the helper: `surfaces.Classify(fp.FromFloat(x), fp.FromFloat(z))`, with the
same `IsAvoid = (s==Water)` / `IsPlayable = fairway/green/collar/semirough/rough/tee/sand` sets VersusBot uses.
Guard `surfaces == null` → treat as playable (fall back to trunk-only, matching VersusBot's ProbeSurface
fallback). This is what keeps a tree re-aim from silently steering into water for EITHER bot.

### 4.3 Wire into VersusBot (`VersusBot.TakeShot`)
Insert an **additive block AFTER the H2 proactive water block resolves and BEFORE the "2b POST-DECISION ERROR
INJECTION" block.** Non-putt only, so 2b then perturbs the tree+water-safe aim exactly as it perturbs the
water-safe aim today.
```csharp
// tree_aware_bot (Order 351): trunk avoidance on the H2-resolved line, before 2b error injection.
if (!isPutt)
{
    var trees = _controller.GetTreeProvider();      // null on treeless holes -> no-op
    if (trees != null && BotTreeProbe.TryFindTrunkClearAim(
            trees, _controller.GetSurfaces(), ball, aimYaw, dist,
            out float treeYaw, out float treeDist))
    {
        aimYaw = treeYaw;
        SelectShotCalibrated(treeDist, out club, out power01, out label);  // VersusBot's own selector
        isPutt = club == PhysicsLabController.PutterIndex;
        label += $" [tree re-aim to {treeDist:F0}m]";
        // (mirror H2's LayupPutterFloor=22m guard if treeDist < 22 to avoid EnterPutterMode teleport)
    }
}
```
Uses `dist` (or the H2-laid-up distance already in scope) as the current target. Re-maps distance→club via
**VersusBot's own** `SelectShotCalibrated`, then falls through to the existing 2b/SetClub/BotClubSync/fire path.

### 4.4 Wire into BotDriver (`BotDriver.PlayHoleToCup`, inside `#if UNITY_EDITOR`)
Insert an **additive block AFTER the off-green putter guard and BEFORE `ctrl.SetClub(club)` / the fire path.**
Non-putt only.
```csharp
// tree_aware_bot (Order 351): same trunk avoidance as VersusBot, via the shared helper.
if (club != PhysicsLabController.PutterIndex)
{
    var trees = ctrl.GetTreeProvider();
    if (trees != null && BotTreeProbe.TryFindTrunkClearAim(
            trees, ctrl.GetSurfaces(), ball, yaw, dist,
            out float treeYaw, out float treeDist))
    {
        yaw = treeYaw;                                            // re-aim camera + shot
        SelectShot(treeDist, isFirstStroke: false, out club, out power01, out label); // BotDriver's own selector
        LogStep($"  Tree re-aim: trunk on cup line -> yaw={treeYaw*Mathf.Rad2Deg:F1} deg dist~{treeDist:F0}m");
    }
}
```
`yaw` is the local aim already computed toward the cup; re-mapping via **BotDriver's own** `SelectShot` keeps
its distinct club bands. The existing `SetCameraYawRadians(yaw)` + `AimChaseCameraAtCup` + BotClubSync + fire
run unchanged on the new yaw/club.

*Rationale for two selectors:* the helper returns **geometry** (yaw + dist); each bot maps distance→club with
its OWN table (their bands differ). Shared trunk math, per-bot club policy — exactly the split we want.

### 4.5 Tunables
v1 = the consts in §4.2. No new CSV. (Follow-up option: promote `NearWindowM`/`LandWindowM` to `bot_clubs.csv`
header comments like the existing `# slope_aim_gain=` override.)

---

## 5. Traps

- **VersusBot is PRODUCTION — must not regress 2b / H2 / H3.** The tree block only *narrows* aim to a
  trunk-clear line; on treeless lines it is a strict no-op (helper returns false). It sits AFTER H2 (so it
  can't undo water safety — its own predicate re-checks surface) and BEFORE 2b (so difficulty perturbation is
  unchanged in kind). Do NOT touch the 2b/H2/H3 blocks.
- **`BotTreeProbe` must be production-safe** (no `#if UNITY_EDITOR`) — VersusBot ships in player builds.
  BotDriver (editor-only) calls the same production helper.
- **Provider-null = zero behaviour change.** `GetTreeProvider()==null` (Hole_17, any treeless hole, lab
  flat-ground) → helper returns false → both bots byte-identical to today. This is the primary safety net.
- **Water ↔ tree conflict.** The ladder predicate re-classifies surface, so a tree re-aim can't silently land
  in water (or OB) for EITHER bot. If no line is both trunk-clear AND surface-safe → helper returns false →
  bot keeps its current line + logs. Accept "may still clip a trunk when fully boxed in" (v1, mirrors H2's
  no-safe-landing fallback).
- **Lesson W avoided:** query via the **`ITreeObstacleProvider` interface in `Golfin.Physics`** (already
  referenced by Viewer). Do NOT add a `Golfin.Physics.Runtime` reference to the Viewer asmdef. Confirm no
  asmdef file changed in the diff.
- **`TestSegment` is cell-local (±10 m) + height-gated** — hence the marched, windowed probe. Do NOT
  "optimize" it into a single ball→pin call (misses distant trunks) and do NOT probe the apex band with
  flat Y (false hits → dumb detours).
- **Do NOT re-bake or edit** `tree_obstacles.csv`, `tree_collision_profiles.csv`, or `TreeObstacleProvider.cs`.
  Read-side only.
- **Putter/short-layup teleport guard:** if a VersusBot tree re-aim yields `treeDist < 22 m`, apply the same
  `LayupPutterFloor` clamp H2 uses (prevents `EnterPutterMode` origin teleport).

---

## 6. Acceptance / Gates

1. **No-op proof (treeless):** on Hole_17 (no tree CSV) and lab flat-ground, both bots' shot decisions are
   **byte-identical to HEAD** (helper early-returns on null provider). Show it in the report.
2. **Primary before/after video — real play, NOT a synthetic scenario** (PIPELINE_HARDENING real-entry rule;
   `BotVideoRecorder` family; bot-recorded, never manual): drive **BotDriver `PlayHoleToCup` on Hole_08**
   (deterministic, editor-drivable, densest trees).
   - **BEFORE** (helper disabled / pre-wire): the bot demonstrably fires into a trunk and caroms on at least
     one stroke (capture the carom — this is the bug being fixed; if Hole_08 doesn't reproduce, fall back to
     Hole_13 / Hole_02 and record which hole reproduces).
   - **AFTER:** on the SAME hole the bot re-aims/lays up around the trunk and holes out with no trunk carom.
   - Distinct-frame gate (Rule 20) applies to the clip — must be a genuine playthrough, not a slideshow.
3. **VersusBot regression smoke** on a tree-dense hole: 2b dispersion, H2 water layup, and H3 slope behave as
   before; the tree block only fires when a trunk is on-line. Confirm via log + a short VersusBot clip.
4. **EditMode unit tests** (`BotTreeProbe` is pure enough to test without a scene — build a real provider via
   the **public** `TreeObstacleProvider.Create(List<TreeInstance>)` from a small synthetic tree list, pass a
   fake/const `ISurfaceProvider`):
   - straight line clear (no trees on line) → returns `false`, aim unchanged;
   - trunk planted on the straight line inside the near window → returns `true` with a yaw that is trunk-clear
     in the windows;
   - trunk only in the apex band (outside both windows) → returns `false` (fly-over, not a detour);
   - `trees == null` → returns `false`;
   - a candidate re-aim whose landing is Water is rejected (surface predicate).
   Full EditMode suite stays green (baseline was 868/0 on the last Gacha ship; expect equal + the new tests).

---

## 7. Video-gate hole
**Hole_08** primary (3927 trees). Fallback **Hole_13 / Hole_02** if Hole_08's straight tee→pin line doesn't
put a trunk in front of the bot. The implementer confirms the pre-fix carom on the chosen hole as the BEFORE
baseline.

---

## 8. Handoff
- Spec: `Docs/Specs/Active/tree_aware_bot/SPEC.md` (this file).
- Kickoff (paste into a fresh Code chat): `Use the implementer subagent on "tree_aware_bot"`
- Touch list (expected diff): **new** `Assets/Scripts/Physics/Viewer/BotTreeProbe.cs` (+ `.meta`); **edit**
  `PhysicsLabController.cs` (one getter), `VersusBot.cs` (one additive block), `BotDriver.cs` (one additive
  block); **new** EditMode test file. **No asmdef, no sim, no CSV, no prefab/scene edits.**

---

## 9. Architect resolution — Q1 (2026-07-21, supersedes §4.2/§4.3/§4.4 probe-distance)

**Decision (Cesar):** feed the probe the selected club's actual **carry distance**, not the full ball→cup
distance. This is the root cause of the iter-1 Gate-2 miss: the probe received `dist = flat.magnitude`
(e.g. 417 m on a Par 5) while the chosen driver only carries ~228–287 m, so the landing window sat ~150 m
past the ball's real descent and every on-line trunk in the true landing zone fell in the skipped apex band.
Using carry keeps the §2 "no ballistic/apex height model" constraint fully intact — we still only probe where
the ball is provably LOW (near the tee, and near where it actually LANDS), we just correct *where* "lands" is.

**What must change (iter-2):**

1. **`targetDist` passed to `TryFindTrunkClearAim` = the selected club's carry, not the cup distance.**
   - `SelectShot` / `SelectShotCalibrated` already compute the carry internally (BotDriver logs it as
     `carry~{targetCarry:F0}m`, `BotDriver.cs:1043`). Surface that carry (add an `out float carry` — or
     equivalent read-back) so the wiring can pass it to the probe.
   - **BotDriver (§4.4):** select the club for the cup distance FIRST (as today), read back its carry, then
     call `TryFindTrunkClearAim(trees, surfaces, ball, yaw, carry, …)`. If it fires, re-run `SelectShot` on
     `treeDist` (which is now a carry-space distance) exactly as today.
   - **VersusBot (§4.3):** same shape — the probe's target is the carry of the club H2 left in scope, not the
     raw cup/`dist`. Re-map `treeDist`→club via `SelectShotCalibrated` as before. The 22 m `LayupPutterFloor`
     guard still applies.
   - The probe's internal windows (`NearWindowM`/`LandWindowM`) and apex-skip logic are UNCHANGED — only the
     `dist` value handed in changes. `LandWindowM=35` now means "35 m short of the real landing," which is the
     descending, low-ball zone the design intended.

2. **Gate-2 video must now reproduce.** With carry-space targeting, a driver stroke on Hole_08 whose ~250 m
   landing zone contains an on-line trunk will fire the probe. Re-run the BEFORE (SkipTreeAvoidance) / AFTER
   before/after on Hole_08 (fallback Hole_13/Hole_02); BEFORE = trunk carom, AFTER = re-aim/layup, no carom,
   distinct-frame gate. If, after the carry fix, none of Hole_08/13/02 puts a trunk in a real landing zone,
   surface that with the CSV projection numbers (do NOT hand-tune windows to force it) and escalate again.

3. **Add one EditMode test** for the carry-vs-cup distinction: a trunk placed in the landing window of a
   *carry-length* target that would have been in the apex band of the *cup-length* target → probe fires on
   carry, no-fires on cup. This locks the fix so it can't silently regress.

**Gate 3 (VersusBot clip) — Architect ruling:** the **BotDriver before/after is the PRIMARY,
designated proof** (§6.2) and both bots call the identical `BotTreeProbe` helper. If VersusBot genuinely
cannot be driven from the editor harness in a reasonable attempt, Gate 3 is satisfied by (a) the shared-helper
unit tests, (b) code-inspection showing the tree block is strictly additive (H2/H3/2b untouched, guarded by
`!isPutt && trees != null`), and (c) a VersusBot log excerpt on a tree-dense hole if one can be captured
without building a new 1v1 script harness. Do NOT build a new VersusBot capture harness for this task — that
is out of scope. Note the limitation in the report; it is not a blocker.

## 9.1 Architect resolution — Q1b video-gate hole (2026-07-21, supersedes §7 hole choice)

**Context:** iter-2 applied the carry fix correctly (Test 6 proves the probe fires when a trunk is in the
carry window), but CSV analysis showed Holes 08/13/02 have **0 trunks in the carry landing window** on the
straight tee→pin heading — their fairway corridors run clean between the tree rows (Hole_08 nearest gap:
1.4 m). A dead-straight drive down a designed-clean fairway never has a trunk on-line, so the §7 hole choice
cannot produce the BEFORE carom.

**Decision (Cesar):** find the hole where it genuinely reproduces. **Sweep EVERY hole that has a
`tree_obstacles.csv`** (not just 08/13/02) for one whose straight tee→pin line puts a trunk in a probe window
(near or landing) at the driver's real carry — most likely a **dogleg** where the direct heading cuts across a
tree corner.

**How to run the sweep authoritatively (do NOT re-derive perp math — call the real probe):**
1. For each hole with a tree CSV: load the provider (`PhysicsLabController` load path / `TreeObstacleProvider.
   Create`), compute the straight tee→pin `aimYaw` and the driver's real `carry` for that hole's tee→pin
   distance (the SAME `SelectShot`/`SelectShotCalibrated` carry the wiring now passes).
2. Call the actual probe — `BotTreeProbe.TryFindTrunkClearAim(trees, surfaces, tee, aimYaw, carry, …)` (or the
   internal `LineHasTrunkInWindows`) — and record whether it fires. This is ground truth; it reflects exactly
   what the bot will do in play. Produce a ranked table: hole → fires? → min trunk gap in window.
3. **Pick the hole(s) where the probe fires on the straight line** as the new video-gate hole. Prefer the one
   with the clearest single on-line trunk (cleanest BEFORE carom).
4. Shoot the BEFORE (SkipTreeAvoidance) / AFTER on THAT hole via BotDriver `PlayHoleToCup`, real play,
   bot-recorded, full 1170×2532, captioned, distinct-frame gate. BEFORE = trunk carom; AFTER = re-aim/layup,
   no carom. Update §7 in the report with the chosen hole and the sweep table.

**Still no hand-tuning:** windows stay at 35 m; this only changes WHICH hole is filmed, chosen by the real
probe firing on a real straight tee→pin line. If — after sweeping ALL tree holes — the probe fires on NONE of
them on the straight tee→pin heading, that is itself the finding: report the full ranked table (every hole,
min gap) and escalate; do not force it. (A realistic off-fairway lie demo remains the fallback only if Cesar
approves it after seeing a genuinely empty sweep.)

## 9.2 Architect resolution — Q1c off-line-lie demo (2026-07-21, after empty sweep)

**Context:** the §9.1 sweep came back EMPTY — the real probe (`LineHasTrunkInWindows`) fires on NONE of the 17
tree holes on the straight tee→pin heading at real driver carry (`sweep_probe_results.csv`). Reason: fairways
are clean corridors by design, and the single hole with a trunk crossing the 2D line (Hole_05, XZ gap −0.14 m)
is correctly suppressed by the accepted v1 flat-Y proxy (its landing is +2.65 m above the tee, so the flat-Y
probe passes under the trunk base). **Conclusion: a dead-straight tee shot structurally never has a trunk
on-line; the feature's real value is on OFF-LINE shots** (rough/dogleg lies, or VersusBot 2b-perturbed aim).

**Decision (Cesar):** film the feature doing its actual job — a **realistic off-fairway lie demo**.

**What to build (iter-4):**
1. **Pick the densest, tightest-gap hole** — `Hole_12` (XZ gap 0.62 m, 3D gap 0.96 m — tightest real
   candidate) or `Hole_08` (3926 trees) from the sweep. Either is fine; pick whichever yields the cleanest
   single on-line trunk for a legible carom.
2. **Find a REAL, plausible lie via the real probe** (not a synthetic mid-air teleport): a point on a *playable*
   surface (rough / semirough / fairway — NOT water, NOT OB, NOT inside a trunk) from which the straight line
   to the pin at the selected club's carry crosses a trunk in a probe window. Confirm by calling the actual
   `BotTreeProbe.TryFindTrunkClearAim(...)` from that lie and asserting it returns `true`. This is a completely
   normal mid-round situation (ball in the rough by a tree row) — legitimate real play, not scaffolding.
3. **Seed that lie** in a BotDriver scenario, then let the bot **play normally from there** (real `SelectShot`,
   real fire, real sim physics — `bots behave like real players`, no scaffolding a real session lacks).
4. **BEFORE** (`SkipTreeAvoidance=true`): the bot fires straight at the pin, the ball **visibly caroms off the
   trunk** (trunk = hard reflect, restitution 0.15 — already in the sim). Capture the deflection.
5. **AFTER** (`SkipTreeAvoidance=false`): the probe fires, the bot **re-aims/lays up around the trunk**, no
   carom, plays on. Capture. The `[BotDriver] Tree re-aim` log line MUST appear in the AFTER run.
6. Both as full 1170×2532 captioned videos (`build_bot_video.py`), distinct-frame gate, frame extracts to
   `screenshots/`. Canonical screenshot must show the BEFORE carom AND an AFTER re-aim frame. Pick a cam that
   makes the carom legible (chase behind the ball, or a top-down `LastTrajectory` overlay per the lateral-curve
   capture lesson — whichever reads clearest from chat).

**Guardrails:** no probe-logic change, no window tuning, no sim edit, no new VersusBot harness. The lie must be
on a playable surface reachable in normal play. If the chosen lie can't produce a clean carom (e.g. the trunk
reflect is glancing), try another lie/hole from the sweep — do not fabricate the carom.
