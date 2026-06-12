# FINDINGS — iter-7 verification (for Architect review)

**Date:** 2026-06-12 (after OOM hard-reboot + Unity MCP reconnect)
**Author:** Claude Code (orchestrator), surfacing for Cesar → Architect
**STATUS:** `IMPLEMENTER_BLOCKED` — paused pending Architect decision. NO changes made beyond this doc.

---

## TL;DR

1. **The red-team's stuck-ball blocker IS fixed.** The iter-7 `frac=0` containment fix works — a descending shot onto a trunk now lands instead of floating forever. The new PROBE7 regression test PASSES.
2. **But one test now fails:** `TreeCollision_CanopyEntryImpulse_NoSlowMoDescent` — "Found **10** steps with velocity ratio < 0.7" (expected exactly 1).
3. **The canopy MODEL is actually correct** — Cesar's slow-mo rejection is genuinely fixed (the impulse fires exactly once at canopy entry; the descent-time ≤1.5× assertion PASSES). **The test FAILURE is a measurement artifact: the test's "ratio < 0.7" heuristic is counting normal GROUND BOUNCES as canopy-damping steps.**
4. **The crux I cannot resolve alone (why I stopped for the Architect):** this same test was reported PASSED 8/8 by the iter-6 red-team (run live), yet with the current code the ball bounces and trips the count. I cannot reconstruct the iter-6 code state to confirm what changed, because the entire tree feature is uncommitted (never committed to git), so there is no iter-6 revision to diff against.

---

## Test run (live, this session)

`tests-run EditMode class=TreeCollisionTests` → **8 passed / 1 failed / 379 total** (full suite; the 1 fail is the class's own):

| Test | Result |
|---|---|
| `TreeCollision_AirborneTrunkDescending_BallReachesGround` (NEW iter-7, PROBE7) | **PASS** ✅ — the stuck-ball blocker is fixed |
| `TreeCollision_CanopyEntryImpulse_NoSlowMoDescent` | **FAIL** ❌ — "Found 10 steps with velocity ratio < 0.7" (expected 1) |
| `TreeCollision_CanopyDamp_LandsCloserThanNoTrees` | PASS |
| `TreeCollision_TrunkDeflect_BallDoesNotPassThrough` (PROOF1 airborne) | PASS |
| `TreeCollision_RollPhase_TrunkDeflectsRollingBall` | PASS |
| `TreeCollision_PuttPhase_TrunkDeflectsRollingBall` | PASS |
| `TreeCollision_Determinism_SameInputSameTree_IdenticalTrajectory` | PASS |
| `TreeCollision_NullProvider_BitExactWithPhase6` (PROOF3) | PASS |
| `TreeCollision_AbsentCsv_NoExceptionNullProvider` | PASS |

The failing assertion is the **(b) impulse-once count** (`Assert.AreEqual(1, dampStepCount)`). The **(a) descent-time** check (`Assert.Less(withTime, noTime*1.5)`) runs first and PASSED — i.e. **the ball does NOT slow-mo; it lands in a normal time.** Only the bounce-count heuristic trips.

---

## Diagnosis — live trajectory trace (read-only `script-execute` probe)

Test config: `origin=(0,15,-0.5) vel=(0,-8,0.5)`, vacuum aero, single default-profile tree at origin (trunkR=0.25, trunkH=3, canopyR=3, canopyTop=9). I traced every sample and flagged each `ratio < 0.7` step:

```
samples=1024  finalY=0.021  finalT=4.248        ← ball REACHES GROUND (not stuck)
DAMP#1  i=135  y=8.951  xzDist=0.220  ratio=0.401  vy=-5.41   ← CANOPY ENTRY cut (the ONE legit impulse, ≈0.40)
DAMP#2  i=355  y=0.030  xzDist=0.351  ratio=0.497  vy=+7.11   ← GROUND BOUNCE (vy flips +)
DAMP#3  i=527  y=2.609  xzDist=0.359  ratio=0.689  vy=+0.09   ← bounce apex
DAMP#4  i=706  y=0.015  xzDist=0.367  ratio=0.494  vy=+3.54   ← GROUND BOUNCE
DAMP#5  i=791  y=0.652  xzDist=0.369  ratio=0.614  vy=+0.06   ← bounce apex
DAMP#6  i=882  y=0.007  xzDist=0.370  ratio=0.489  vy=+1.75   ← GROUND BOUNCE
DAMP#7  i=923  y=0.162  xzDist=0.370  ratio=0.641  vy=+0.07   ← bounce apex
DAMP#8  i=970  y=0.004  xzDist=0.370  ratio=0.477  vy=+0.85   ← GROUND BOUNCE
DAMP#9  i=989  y=0.040  xzDist=0.370  ratio=0.653  vy=+0.08   ← bounce apex
DAMP#10 i=1014 y=0.021  xzDist=0.370  ratio=0.001  vy=0.00    ← coming to REST
```

**Reading:** DAMP#1 is the genuine canopy entry impulse (at y=8.95, ratio 0.401 ≈ `canopyHitDamping=0.40`). **DAMP#2–10 all occur at y≈0 with `vy` flipping sign and shrinking** — that is a ball bouncing on the ground and settling. The ball comes to rest at **xzDist=0.37, which is OUTSIDE the 0.25m trunk** — so this is NOT a trunk interaction; it is ordinary ground bounce-and-settle. The test's heuristic (`count steps where total-speed ratio < 0.7`) cannot tell a canopy impulse from a bounce, so it counts all of them.

**Conclusion on the model:** the canopy entry-impulse is implemented correctly — fires exactly once, at entry, then normal ballistics. **The slow-mo Cesar rejected is gone.** The test is measuring the wrong thing.

---

## What iter-7 actually changed (`BallSimulation.cs`)

NOTE: `git diff` shows the whole uncommitted tree integration (~178 lines, iters 1–7 cumulative, since nothing is committed). The **iter-7-specific** addition is only the airborne-trunk `frac=0` containment block (file lines ~449–488):

- On a `frac=0` trunk hit (ball already inside the cylinder), instead of the old `pos=hitPos; t=t; continue` (zero progress → 14 400-step stuck loop), it now pushes the ball out along `NormalXZ` to just past `trunkRadius`, reflects XZ with restitution, **advances `t=tNext`**, and continues — mirroring the roll/putt handler. A degenerate (straight-down) case kills XZ, keeps `vy`, and lets the ground check terminate.

This block is the verified fix for the stuck-ball. **It does not touch the canopy branch, the ground-crossing/M5b check, or the bounce path.**

---

## The unresolved question (why this needs the Architect)

For this canopy-test ball the iter-7 `frac=0` block's effect on the final trajectory is unclear:
- The ball ends at xzDist=0.37 (outside the trunk) but its `z` flips from −0.22 (canopy entry) to −0.37 (ground) — implying a trunk reflection happened on the way down (a frac>0 wall crossing, or a frac=0 containment).
- **If that descent trunk hit was frac>0:** iter-6 and iter-7 handle it identically (frac>0 code is unchanged) → identical trajectory → the ball bounced in iter-6 too → iter-6 should have counted ~10 steps and FAILED. But the iter-6 red-team reported 8/8.
- **If it was frac=0:** iter-6 (no special-case) would have STUCK → the descent-time assertion would have FAILED/hung in iter-6. But it reported pass.

Either branch contradicts the iter-6 "8/8 live PASS." I cannot reconcile this from here because the iter-6 code is not recoverable (uncommitted). So the two live hypotheses are:

- **(A) The test is flawed** — its impulse-once heuristic counts ground bounces. The correct scope is "count damping steps **within the canopy band** (y ∈ [trunkTop, canopyTop]) and **before first ground contact**," which would isolate the single canopy cut. Under this view the sim is correct and only the test needs tightening. *(This matches all the model evidence: one cut at entry, descent-time within bound, slow-mo gone.)*
- **(B) iter-7 changed the landing dynamics** so the ball now bounces where iter-6 settled — a real (if subtle) sim regression to chase, not just a test fix.

My read leans strongly to **(A)** (the trace shows the extra steps are unambiguously ground bounces, and the descent-time / slow-mo behavior is correct), but I am explicitly NOT acting on it per Cesar's instruction to take it to the Architect first.

---

## Recommended next diagnostic (read-only, 1 probe — for whoever proceeds)

Run the same trace on the **noTrees** ball (trees=null). If noTrees also bounces 4–5 times (same DAMP#2–10 pattern at y≈0), that proves the bounces are pure ground physics independent of trees, confirming hypothesis (A) and that the test heuristic — not the sim — is at fault. (I did not run this, to honor the stop.)

---

## Also worth flagging to the Architect

1. **Reboot fallout — invalid `.meta` GUIDs.** Unity console shows ~29 "The .meta file … does not have a valid GUID" errors after the OOM hard-reboot. Most are `Scenes/Original/Rindo Course/Rindo_Hole09/...` (lightmaps) but two are scripts: `Assets/Scripts/Editor/Archive/ExampleAutoWireScreen.cs.meta` and `Assets/Scripts/Utilities/UIAutoWire.cs.meta`. These are unrelated to tree_collisions and may be pre-existing or reboot damage — worth a separate look so they don't cause GUID-reference breakage elsewhere.
2. **No filesystem damage to the tree work** — STATUS, `BallSimulation.cs` (braces 160/160), `TreeObstacleProvider.cs`, `TreeCollisionTests.cs`, and `tree_collision_profiles.csv` (`canopyHitDamping`=0.40) all survived the reboot intact and compiled clean (no C# errors in console; `IsCompiling=false`).
3. **Still outstanding for §9 regardless of the above:** Cesar's "video shows no trunk collision" → the trunk clip needs a bare-bark re-shoot (red-team rated it MARGINAL). Not started.

---

## State summary for the Architect

- **Fixed & verified:** stuck-ball (PROBE7), roll/putt deflect, airborne trunk reflect, determinism, null bit-exact, canopy-lands-short. Canopy slow-mo (Cesar's rejection) is genuinely gone.
- **Open #1 (this doc):** canopy no-slow-mo test fails on a bounce-counting heuristic — decide (A) tighten the test vs (B) investigate a landing regression.
- **Open #2:** §9 trunk clip bare-bark re-shoot (pre-existing, from the red-team's MARGINAL rating).
- **Open #3:** reboot `.meta` GUID errors (out of scope, flagged).

---

## ARCHITECT_DECISION (2026-06-12, Architect)

**Verdict: Hypothesis (A) — fix the TEST, not the sim. And the iter-6 contradiction is resolved: (A) and the stuck-ball bug are the same story.**

In iter-6, this exact test ball descended inside the trunk column (canopy entry xzDist 0.22 < trunkR 0.25) and hit the `frac=0` zero-progress case — it FROZE at the trunk. Frozen ball: `t` stops advancing → small finalTime → assertion (a) passes trivially; never lands → zero ground bounces → ratio scan finds only the canopy drop → assertion (b) passes with count=1. **The iter-6 "8/8 live PASS" was a false pass produced by the stuck-ball bug itself.** Iter-7 freed the ball → it lands → it bounces (DAMP#2–10: y≈0, vy sign-flips, decaying, xzDist 0.37 > trunkR — pure ground restitution) → the over-broad heuristic counts bounces. The test failure is positive evidence the iter-7 fix works. No sim regression. Hypothesis (B) rejected on the trace evidence.

### Directive — iter-8

1. **Confirming probe first (read-only):** trace the noTrees ball; expect the same y≈0 bounce signature (ratio<0.7 steps at landing). Locks (A) empirically. Record in IMPLEMENTER_REPORT.
2. **Tighten assertion (b)** in `TreeCollision_CanopyEntryImpulse_NoSlowMoDescent`: scan only samples BEFORE first ground contact (truncate at first sample with y < 0.2m), and additionally assert the single drop occurs within the canopy band (trunkTopY < y ≤ canopyTopY) with ratio ≈ canopyHitDamping (±0.15). Expect exactly 1. Assertion (a) unchanged. Sim code: DO NOT TOUCH (canopy + trunk + frac=0 block all verified correct).
3. **§9 trunk clip:** bare-bark re-shoot (red-team MARGINAL), per the existing open item.
4. **WIP checkpoint commit BEFORE iter-8 edits (strongly recommended, Cesar to approve):** the iter-6/iter-7 diff was unrecoverable precisely because nothing was ever committed — and we just survived an OOM hard-reboot on an uncommitted feature. Scoped add of the reconciled Files-table paths (never `git add .`), message `wip(tree_collisions): iter-7 checkpoint pre-iter-8`. Ship commit still follows the normal close-out.

### Out-of-scope notes

- **.meta GUID errors:** git shows the two script metas (`ExampleAutoWireScreen.cs.meta`, `UIAutoWire.cs.meta`) tracked and UNMODIFIED — file contents are intact, so the errors are Library-side reboot fallout, not source damage. Remedy outside this task: Reimport the affected folders (or worst case delete `Library/` and let Unity rebuild). Rindo lightmap metas likewise not a tree_collisions concern.
