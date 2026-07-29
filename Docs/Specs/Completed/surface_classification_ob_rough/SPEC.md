# SPEC — `surface_classification_ob_rough`

**Tier:** 2 — TELLCODE. Two independent stages, **Stage 2 is product-gated.**
**Depends on:** `zone_bake_completeness` (DONE `b7ebbf000`) and `surface_fallthrough_coverage_probe` (DONE `bdb4f1f4d`).
**Approach decided by measurement, not argument** — see §1.

> **READ FIRST:** `Docs/SURFACE_CLASSIFICATION_PIPELINE.md` — end-to-end reference for this pipeline. And `Docs/Specs/Completed/surface_fallthrough_coverage_probe/FINDINGS.md` — the measurement that chose this approach.

---

## 0. PRODUCT GATE — RESOLVED 2026-07-29: **YES, trees play as Rough.** Proceed.

Cesar's call. Stage 2 proceeds as written; no further approval needed.

**The reasoning matters for §5, so record it.** The initial argument for waving this through was "the player can't play from a tree, they have collision." **That premise is false and the implementer should not repeat it.** Collision is **trunk-only**: `TreeObstacleData.TrunkRadius` is 0.25–0.35 m (×scale) against a `canopyRadius` of 3.5 m — a **100× area difference**. Measured on Hole_08: 3,927 trunks cover **771–1,511 m² of a 7,570 m² tree zone — 10–20%**. So **80–90% of tree-zone ground carries no collider**, the ball routinely comes to rest there, and today it rolls out as if on fairway.

**The answer is still YES, on the merits:** ground under trees is pine straw, leaf litter and dirt. `Rough` is the correct classification, not a tolerated side effect.

**Consequence — this enlarges §5.** The rebalance covers **96.36% of the Default bucket** (68.33% rough + 28.03% trees), not 68.33%. Calibrate the tuning pass on that.

> Historical note, for anyone re-opening this: there is **no runtime structure that marks trees.** All 18 holes bake only `Fairway, Green, Tee, Sand, CartPath, Water` polygon groups — no Trees group — and the `obMask` marks OB only. `tree_obstacles.csv` is point instances (`worldX,worldZ,baseY,scale,profileName`), not a coverage mask. So trees are indistinguishable from any other fallthrough ground at classification time. Had the answer been NO, the cheap path would have been impossible and Option 2 (per-cell surface grid) would have been required.

---

## 1. What the probe established

Of **12,128,074** cells resolving via `DefaultSurface` fallthrough across all 18 holes:

| | cells | % of Default |
|---|---:|---:|
| authored **Rough** + semi_rough — the fix | 8,286,618 | **68.33%** |
| authored **Fairway** — the break | 32,411 | **0.27%** |
| ratio | | **255.67 : 1** |
| authored `ob` | 8,525 | 0.07% |
| authored **trees** | 3,399,017 | 28.03% |

Mapping gate passed 4/4 vs 0/4; seam cross-check 8,400/8,400 provenance agreement; freshness gate 17/18 within 0.06pp with Hole 02 quarantined. Red-team `ARCHITECT_REVIEW_PASS`.

**The cheap path wins by ~256:1. Do not build Option 2.**

---

## 2. Stage 1 — Defect A: out-of-grid resolves to Fairway

### The bug

`BakedZoneClassifier.IsObAt` (`:220-230`) returns `false` for any point outside the OB mask grid:

```csharp
if (ix < 0 || ix >= obMaskWidth)  return false;
if (iz < 0 || iz >= obMaskHeight) return false;
```

So a ball past the terrain footprint matches no polygon, misses the mask, and falls to `DefaultSurface` — currently `Fairway`. **Shots that leave the map are unpenalised.**

### The change

Out-of-grid must resolve to `SurfaceType.OOB`, not to the default. Note `IsObAt` is a `bool` and currently conflates "outside the grid" with "inside the grid, not OB" — those must become distinguishable. Implement however is cleanest (an out-param, a tri-state, or an explicit bounds check in `ClassifyCore` ahead of the mask lookup) but **preserve `ClassifyCore` as the single shared path** so `Classify` and `ClassifyWithProvenance` stay bit-identical by construction.

Assign a distinct provenance value for out-of-grid so the existing probe tooling can tell it apart from mask-hit OOB. Document the new value next to the existing ones.

### ⚠️ This is not a cosmetic fix — it arms real machinery

`SurfaceType.OOB` is load-bearing across the codebase. Verify each of these still behaves sanely, and say so in the report:

| Site | What it does |
|---|---|
| `BallStateMachine.cs:157, :170` | sets `terminalSurface = OOB` — penalty path |
| `BallSimulation.cs:257, :615, :792` | OOB branches in the sim loop |
| `OBDropResolver.cs:23` | skips Water/OOB when resolving a drop point |
| `LoopCameraDirector.cs:246` | **the OB camera clamp** |
| `BallAudioEmitter.cs:166` | OOB audio |
| `ObBoundaryCaptureBot.cs` | a dedicated capture bot for OB behaviour — **use it** |

`SurfaceConfig.cs:35` already defines OOB coefficients (Restitution 0.20 / TangentFriction 0.80 / RollingResistance 0.50 / StopSpeed 0.20), so no new coefficient work.

### Stage 1 acceptance

- A shot driven past the terrain edge resolves `OOB`, arms the camera clamp, and takes the penalty path.
- A shot landing inside the footprint on non-OB ground is **unchanged**.
- `ObBoundaryCaptureBot` still passes.

---

## 3. Stage 2 — Defect B: Rough is never classified

**Gated on §0.**

### The change

```csharp
// BakedZoneClassifier.cs:73
public const SurfaceType DefaultSurface = SurfaceType.Rough;   // was SurfaceType.Fairway
```

That is the entire functional change. `Rough` coefficients already exist (`SurfaceConfig.cs:29` — Restitution 0.25 / TangentFriction 0.82 / RollingResistance 0.45 / StopSpeed 0.22), audio is wired (`SfxId.LandRough`), and `TrajectoryRenderer.cs:231` has a Rough colour. Nothing else needs adding.

### ⚠️ Semirough is authored noise — this is ONE problem, not two

`FINDINGS.md` in this folder frames Defect B as "Rough **and** Semirough are never classified." Verified across 18 holes: `semi_rough` is **314–1,564 px per hole, below the §4.2 completeness gate's own 1,000-cell threshold on 15 of 18**. **Do not plumb Semirough.** It is Rough.

### Stale comment to update

`VersusBot.cs:382` documents `BakedZoneClassifier.DefaultSurface = Fairway` in a doc comment. Correct it. Do not change its logic.

### Do NOT touch

`ZoneData.cs:100-106` also returns `SurfaceType.Fairway`, but that is a **data-parse fallback** for an unrecognised `type` string, not a classification default. It is unrelated. Leave it.

---

## 4. ⚠️ KNOWN TEST BREAKAGE — Stage 2 will fail CI as written

**`Assets/Scripts/Gameplay/Tests/RealHoleTerrainTests.cs` is built on the current default.** Read the whole helper at `:475-570` before editing anything.

`SampleRandomXZ` identifies "rough" by finding cells the classifier calls `Fairway` that are *not* inside a Fairway polygon:

```csharp
// :551-553
// "Rough" = default-Fairway-from-classifier (outside polygons AND
// outside OB mask). Skip explicit polygon types and OB.
if (cls == SurfaceType.Fairway)
{
    ...
    if (!inFairwayPoly) result.Add((x, z));
}
```

After the flip those cells return `Rough`, so the helper collects nothing.

`Hole01_Fairway_50RandomSamples_BakedLookupSanity` (`:354`) then asserts `Assert.That(samples.Count, Is.GreaterThan(10))` and `Assert.AreEqual(SurfaceType.Fairway, cls)` on exactly those samples. **It will fail.**

**Required:** update the helper to match the new semantics — post-flip, "rough" is simply `cls == SurfaceType.Rough`, and the not-in-fairway-polygon filter becomes redundant for that case. Keep the genuine-fairway path (sampling inside actual Fairway polygons) working, since that assertion is still meaningful and now actually tests what its name claims.

**This test becomes more honest after the change, not less.** It was previously asserting that rough ground classifies as fairway — i.e. it was encoding the bug. Say so in the report.

Run the full Physics + Gameplay test suites. **Any other failure is a finding — report it, do not silently adjust the test to pass.**

---

## 5. Difficulty rebalance — not optional

Stage 2 moves **96.36% of the Default bucket** — rough 68.33% **plus** trees 28.03%, per the §0 resolution — from `RollingResistance 0.18` to `0.45`. That is a **2.5× change across essentially all fallthrough ground.** The course gets materially harder, and this is the intended effect.

**Calibrate the tuning pass on 96%, not 68%.** The tree zones are not a rounding error: 80–90% of tree-zone ground has no trunk collider on it, so it is ordinary reachable lie-ground that currently plays as fairway.

- Requires a tuning pass after the flip.
- Requires a **`PHYSICS_TUNING_CHANGELOG.md` F-entry**. No coefficient values change, but effective surface distribution does — record it as a behavioural change with the probe's numbers as justification.
- `controls.csv` is unchanged. Do not edit it.

---

## 6. Blast radius to verify, not assume

Both stages change which `SurfaceType` values appear at runtime. Confirm nothing keys off `Fairway` as a "we don't know" sentinel:

- `BallSimulation.cs:759` — `IsPuttSurface(s) => s == Green || s == GreenCollar`. Unchanged by this, but confirm the putt integrator still engages correctly.
- `BotDriver.cs:728-732`, `VersusBot.cs:496-501` — bots chip rather than putt when off Green/GreenCollar. More ground now reads as Rough; confirm bot club selection is still sane.
- `BallAudioEmitter` — landings that were silent-or-fairway now play `LandRough`. Expected; confirm not jarring.

---

## 7. The 0.27% fairway residual — accept and record, do not tune

32,411 cells of genuine authored fairway fall through because mesh boundaries don't quite cover the painted region. Post-flip they play as rough. That is **0.07% of footprint** and is a **polygon-gap defect**, not a tuning problem. Record it in the report. **Do not attempt to close it with coefficients or by nudging polygons.**

---

## 8. Non-goals

- No Option 2 / per-cell surface grid.
- No re-bake, no `BakeZoneJsonTool` change, no `zones.json` mutation.
- No Semirough plumbing.
- No `controls.csv` edit, no coefficient value changes.
- No fix for the 0.27% fairway residual.
- No changes to `HoleGeoImporter` or the authoring pipeline.

Anything beyond this — stop and report.

---

## 9. Video gate

Real play (`screenshot-game-view` MCP tool — hand-rolled `script-execute` captures are hard-blocked by `.claude/hooks/enforce_capture_tool.py`).

- **Stage 1:** one clip, a shot driven past the terrain edge. Camera clamp arms, penalty path taken.
- **Stage 2:** before/after on the same hole and shot — ball landing off-fairway visibly rolls out less after the change. **Hole_08 or Hole_14** (both have large fallthrough regions).

---

## 10. Report

1. Stage 1 and Stage 2 stated separately. The §0 gate is **resolved YES** — no approval to seek.
2. The `RealHoleTerrainTests` change, and why it makes the test more honest.
3. Full test-suite result. Any failure beyond the known one called out explicitly.
4. Each blast-radius site in §2 and §6 confirmed or flagged.
5. The `PHYSICS_TUNING_CHANGELOG` F-entry.
6. The 0.27% residual recorded as a known accepted defect.
