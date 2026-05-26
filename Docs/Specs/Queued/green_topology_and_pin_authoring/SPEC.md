# SPEC: Green Topology and Pin Authoring (umbrella)

**STATUS:** QUEUED (umbrella) — promote sub-phases to `Docs/Specs/Active/<sub-slug>/` individually as kicked off.
**FOLDER:** `Docs/Specs/Queued/green_topology_and_pin_authoring/`
**SUPERSEDES:** `Docs/Specs/Queued/puttpath_predictor_perf_and_design/` — folded into Phase 7. Archive that folder to `Completed/` with cross-ref when Phase 7 lands.
**NOTION:** TBD — Cesar to add umbrella entry under §3/§4 area (post Loop v1), Order ~290 or interleave per priority. Sub-phases get their own Notion entries when promoted to Active.
**WRITTEN:** 2026-05-18

### Phase-by-phase status

| Phase | Status | Closed | Commit |
| --- | --- | --- | --- |
| **1 — Data format spec + runtime classes** | ✅ DONE | 2026-05-26 10:55 CEST | `47dd8f6d` |
| 2 — Authoring tool | ✅ DONE | 2026-05-26 19:40 CEST | `45ff0c67` (impl) + `4e7b2aff` (Quick fix: polygon/cell offset) + `093de0b9` (post-fix video) |
| 3 — Procedural baseline | queued | — | — |
| 4 — Tracing pass | queued | — | — |
| 5 — Heightmap reconciliation | queued | — | — |
| 6 — Physics integration | queued | — | — |
| 7 — Predictor redesign | queued (amendment pending — slope-source swap, not full redesign; the warped-grid `PutterGreenReader` shipped 2026-05-25 already renders the visual, Phase 7 swaps its slope source from mesh to `GreenTopologyCache`) | — | — |
| 8 — Pin position wiring | queued | — | — |
| 9a — Cup capture FX | queued | — | — |
| 9b — Real geometric cup | queued (optional polish) | — | — |

---

## One-line

Replace flat greens with real Lomond topology by (1) building an editor tool to author per-green slope grid + pin candidates using **Shot Navi 3DX** reference screenshots, (2) baking slope data into both a sim-readable `green.json` AND the existing `heightmap.bytes`, (3) wiring `BallSimulation` roll/putt phases to sample slope per-step, and (4) redesigning `PuttPathPredictor` to render baked slope arrows with zero per-frame sim.

---

## Locked decisions (Architect ↔ Cesar, 2026-05-18 chat)

- **L1 — Data source: Shot Navi 3DX free tier + first-launch 3-day premium trial.** Lomond confirmed in Shot Navi database (course ID 806; elevation refreshed 2023-09-03, map updated 2024-07-09). PuttView book rejected for shipping risk to Spain. PuttView app rejected (Lomond Japan not in their digital library — they only have Loch Lomond in Scotland). StrackaLine left as fallback if Shot Navi data quality is insufficient.
- **L6 — Captures already in hand (2026-05-18):** 36 PNGs in `screenshots/` — 18 × `_strategy` (distance/yardage view) + 18 × `_heatmap` (topographic view, rainbow icon active). Heatmap is the slope-authoring source; Strategy is the green-shape + pin-position reference.
- **L7 — Shot Navi green-view distances are METERS, not yards.** Course-level yardage (back/regular/front tees, scorecard) is yards; green-zoom distances in Shot Navi (perimeter `0/5/10/15/20`, inline `13`, `11`, `16` numbers) are meters. Cesar locked: course = yards, green = meters. Matches `cellSize: 0.5` (m) in the data format below — 1 Shot Navi grid cell = 1 m, so cellSize 0.5 = 2 cells per visible grid square.
- **L8 — Pin position: derived from Shot Navi flag location.** The white flag visible in each Shot Navi capture IS the canonical default pin. `defaultPinIndex = 0` maps to the Shot Navi-displayed flag; 2-4 alternate candidates authored manually based on visible green topology.
- **L9 — Primary slope source: `A4_ホール攻略冊子.pdf` (Lomond's official strategy booklet, 2019).** Per-hole pages 2-19 each include a `GREEN攻略法` panel with: (a) explicit slope direction arrows on a top-down green diagram, (b) green width × depth in **meters** (matches L7 calibration), (c) dashed lines marking tier ridges, (d) Japanese strategic note keyed to slope direction ("奥からはやい" = fast-from-back, "見た目よりはやい" = faster-than-looks, etc.). This is the authoring primary; Shot Navi heatmap captures become secondary cross-reference.
- **L10 — Confirmed 2-tier greens (PDF + reviews): Holes 3, 7, 11, 18.** Likely partial tiers / ridges (PDF shows dashed lines): 5, 12, 13, 14, 17. Hole 7 is **L/R 2-tier** (diagonal ridge), Hole 18 is **front/back 2-tier** (horizontal ridge) — corrects earlier spec assumption that hole 7 was front/back.
- **L11 — Hole 9 is the most contoured green.** PDF: "傾斜やマウンドが多いのでライン読みは慎重に" = "Lots of slope and mounding — read carefully." Matches Shot Navi heatmap yellow/orange in `lomond_hole_09_shotnavi_heatmap.png`. All other greens are subtle by comparison.
- **L2 — Storage: dense grid at 0.5 m resolution.** `{slopeDirX, slopeDirZ, magnitudePercent}` per cell over each green's axis-aligned bounding rect; cells outside the green polygon stored as `(0,0,0)`.
- **L3 — Heightmap reconciliation: option (b).** Bake slope into `heightmap.bytes` so the visual mesh matches the sim data. Closes the 2026-05-01 open flag (ball dips at fairway→green seam) in the same pass.
- **L4 — Pin authoring: 3-5 candidates per green, `defaultPinIndex = 0`.** Day-of pin rotation deferred to Loop v2+.
- **L5 — All 18 greens.** No vertical slice. Phase 3 procedural fill applies to all 18; Phase 4 Shot Navi pass refines all 18.

---

## Phase structure

Each phase becomes its own sub-spec under `Docs/Specs/Active/<sub-slug>/` when ready to kick off. Sequencing is roughly linear but **Phases 5+6 can run in parallel with Phase 7** since they touch different files. **Phase 3 is independent of Phase 4** — Phase 3 procedural data unblocks Phases 5–8 even if Shot Navi tracing slips.

### Phase 1 — Data format spec (Architect direct, ½ day, no pipeline)

Architect writes this section into `Assets/Scripts/Course/Runtime/GreenTopology.cs` with full XML doc, and a sample `green.json` skeleton committed alongside.

**`green.json` schema (path: `Assets/Resources/HoleData/Hole_XX/green.json`, sibling to existing `zones.json` and `heightmap.bytes`):**

```json
{
  "schemaVersion": 1,
  "holeNumber": 1,
  "sourceTag": "procedural_v1 | shotnavi_traced_v1 | manual_refined_v1",
  "boundsMin": { "x": -12.3, "z": 45.6 },
  "boundsMax": { "x":   4.1, "z": 62.1 },
  "cellSize": 0.5,
  "gridWidth": 33,
  "gridHeight": 33,
  "slopeGridBase64": "<base64 of float32[width*height*3]; tuples=(dirX, dirZ, magPct); inactive cells = (0,0,0)>",
  "pinCandidates": [
    { "worldX": -3.2, "worldY": 38.5, "worldZ": 51.0, "label": "front-left" },
    { "worldX": -1.5, "worldY": 38.6, "worldZ": 53.0, "label": "center"     },
    { "worldX":  1.2, "worldY": 38.4, "worldZ": 55.5, "label": "back-right" }
  ],
  "defaultPinIndex": 1
}
```

**C# runtime class:** `Assets/Scripts/Course/Runtime/GreenTopology.cs` (new file). Public API:

- `static GreenTopology LoadFromResources(int holeNumber)` — returns null if no file.
- `bool TrySampleSlope(Vector2 worldXZ, out Vector2 dirXZ, out float magPct)` — false outside bounds; returns (0,0,0) for inactive cells inside bounds.
- `Vector3 GetDefaultPin()`, `IReadOnlyList<Vector3> GetPinCandidates()`, `IReadOnlyList<string> GetPinLabels()`.

**DoD for Phase 1:** ✅ DONE 2026-05-26 (commit `47dd8f6d`). Shipped: new asmdef `Golfin.Course.Runtime` (`autoReferenced: true`); `GreenTopology.cs` (267 lines, full XML doc, the 5 public-API methods above + 9 read-only properties); `GreenTopologyCache.cs` (90 lines, process-lifetime cache + negative cache for missing `green.json` to avoid repeated `Resources.Load` on per-step queries from Phase 6); `Assets/Resources/HoleData/Hole_01/green.json` skeleton (4×4 zero grid, `sourceTag=phase1_skeleton`, round-trip verified via Python parse of the base64 payload). NB: original DoD said "Phase 2 implements the C# class" — that ordering was wrong (Phase 2's editor tool can't compile against types that don't exist). Runtime classes shipped here in Phase 1.

---

### Phase 2 — Authoring editor tool (FULL PIPELINE, 1 day)

New editor window. Menu: `GOLFIN > Green Authoring > Open Editor`. Files:

- `Assets/Scripts/Editor/GreenAuthoring/GreenTopologyEditor.cs` — EditorWindow.
- `Assets/Scripts/Editor/GreenAuthoring/GreenAuthoringMath.cs` — affine transform, procedural fill heuristics.
- `Assets/Scripts/Editor/GreenAuthoring/Golfin.Editor.GreenAuthoring.asmdef` — references `Golfin.Course.Editor` (for zones.json reader; create or fold in as needed) + `Golfin.Course.Runtime` (already exists post-Phase-1, for GreenTopology).
- ~~`Assets/Scripts/Course/Runtime/GreenTopology.cs`~~ — shipped in Phase 1 (commit `47dd8f6d`).
- ~~`Assets/Scripts/Course/Runtime/GreenTopologyCache.cs`~~ — shipped in Phase 1 (commit `47dd8f6d`).

**Features:**

1. Hole picker (1-18). Reuse `PhysicsLabHolePicker` patterns where reasonable.
2. Loads existing `green.json` if present, else generates procedural baseline from `zones.json` (green polygon) + `heightmap.bytes` (macro slope hints).
3. **Backdrop slot:** drop a Shot Navi screenshot PNG, align by clicking 2 green-edge anchor points on the screenshot, then the corresponding 2 points on the heightmap-derived green polygon. Stores affine transform per-hole in `green.json` as `editorBackdrop` metadata (optional, ignored at runtime). **Note:** Shot Navi green-view grid is in METERS (1 visible grid square = 1 m). When alignment uses perimeter distance markers (`0/5/10/15/20`), interpret as meters, not yards.
4. **Paint mode:** click-drag draws slope vectors into grid cells under cursor. Brush radius (1-5 cells) + magnitude scrubber. Right-click clears cells.
5. **Auto-procedural fill button:** runs `GreenAuthoringMath.ComputeProceduralSlopeField(polygon, heightmap)`. Heuristic:
   - 1.5% back-to-front baseline (drainage convention) along estimated drain axis.
   - False-front detection: if green polygon's front 20% has heightmap below median by >0.3m, add steepened 2.5% in that region.
   - Tier-break detection: if heightmap range across polygon exceeds 0.5m, place a 4% ridge perpendicular to drain axis at the elevation midpoint.
6. **Pin candidate panel:** click on green view to add pin candidate, label dropdown (front-L/C/R, mid-L/C/R, back-L/C/R, custom), drag to reorder, radio button for default. **First pin = canonical default** derived from Shot Navi flag (`_strategy` backdrop shows the white flag glyph most clearly). 2-4 alternates authored manually based on visible heatmap topology.
7. **Save button:** writes `green.json` to `Assets/Resources/HoleData/Hole_XX/green.json`, calls `AssetDatabase.Refresh()` + `GreenTopologyCache.Invalidate(holeNumber)`.

**Hard rules:**

1. Do NOT modify `HoleGeoImporter.cs` or any existing `CourseImporter/` editor tool — new code is fully under `Assets/Scripts/Editor/GreenAuthoring/`.
2. Do NOT modify `zones.json` schema — read-only consumer of green polygon.
3. Do NOT modify `heightmap.bytes` from this tool — Phase 5 owns heightmap deformation.
4. Tool is editor-only (zero runtime asmdef impact — `Golfin.Course.Runtime` was created in Phase 1 and is not modified by Phase 2).
5. New asmdef name: `Golfin.Editor.GreenAuthoring`. autoReferenced: false.
6. EditMode tests in new file `Assets/Scripts/Course/Tests/GreenTopologyTests.cs`: (a) round-trip — write `green.json`, read back, assert slope grid byte-equal; (b) `TrySampleSlope` returns false outside bounds; (c) `GetPinCandidates` returns the configured list in order. **+3 tests, 0 IGNORED.**

**DoD:** tool opens, loads hole 1, displays green polygon overlay, supports paint + procedural-fill + pin-candidate paths, saves valid `green.json` that round-trips through `GreenTopology.LoadFromResources`. Test gate: **N+3 PASS, 0 IGNORED.**

---

### Phase 3 — Procedural baseline pass (Cesar manual, ½ day)

Cesar opens `GreenTopologyEditor` for each of 18 holes, hits "Auto-procedural fill", reviews the heuristic output, manually paints 3-5 pin candidates, saves. Captures a baseline 18 × `green.json` so Phases 5-8 have data to consume even before Shot Navi tracing.

**Out-of-band manual override:** for hole 7, set `sourceTag = "manual_refined_v1"` and manually author the documented 2-tier break (back tier ~0.5m higher than front; ridge runs roughly perpendicular to approach axis). Other holes stay `sourceTag = "procedural_v1"`.

**DoD:** 18 `green.json` files committed under `Assets/Resources/HoleData/Hole_NN/green.json`.

---

### Phase 4 — Tracing pass (Cesar manual, ~1 day of focused tracing)

**Status (2026-05-26):** All reference data in hand:
- `A4_ホール攻略冊子.pdf` (20 pages, Lomond 2019 strategy booklet) — **PRIMARY** source for slope arrows + dimensions + tier locations + strategic notes
- 18 × `lomond_hole_NN_shotnavi_strategy.png` (yardage view; canonical pin position from white flag glyph)
- 18 × `lomond_hole_NN_shotnavi_heatmap.png` (secondary cross-reference for slope direction)

**Tracing workflow (per hole):**
1. Open `GreenTopologyEditor` for hole NN.
2. Open PDF page (hole NN = PDF page NN+1) and zoom to `GREEN攻略法` panel.
3. Read green W × H dimensions in **meters** — sanity-check against `zones.json` green polygon bounding box.
4. Drop `lomond_hole_NN_shotnavi_strategy.png` into Backdrop slot. Align using green-edge anchor points. Read canonical pin position from the visible white flag → `pinCandidates[0]`.
5. Trace slope arrows directly from the PDF panel — directions are explicit (no color interpretation). Magnitude calibration:
   - **"見た目よりはやい" (faster than it looks):** subtle slope, **1.5-2%**
   - **"はやい" (fast):** noticeable slope, **2.5-4%**
   - **"傾斜やマウンドが多い" (lots of slope and mounding):** highly contoured, multiple zones up to **5-7%** (hole 9 only)
   - **Dashed line on PDF:** tier ridge — author as a 4-5% ridge perpendicular to fall line, with smooth falloff to flat regions on each side
6. Cross-reference Shot Navi `_heatmap.png` only if PDF is ambiguous (rare); the PDF is authoritative.
7. Add 2-4 alternate pin candidates beyond canonical default — one per visible tier or flat zone.
8. Save.

**Confirmed 2-tier greens (use `manual_refined_v1` sourceTag):**
- **Hole 3:** 2-tier (orientation TBD on closer PDF inspection during tracing)
- **Hole 7:** L/R 2-tier with diagonal ridge — NOT front/back as earlier-spec assumed
- **Hole 11:** Upper tier with mounding
- **Hole 18:** Front/back 2-tier (horizontal ridge), vertical-elongated green

**Likely partial tiers / partial ridges (PDF shows dashed lines):** Holes 5, 12, 13, 14, 17. Author with `procedural_v1` baseline + manual ridge addition during tracing; promote to `manual_refined_v1` if ridge is prominent.

**Hole 5 special:** PDF says "傾斜の少ないグリーン" = "green with little slope" — author conservatively, max 1.5% anywhere, no false-front.

**Hole 9 special:** Most contoured green on the course. Multiple slope zones, expect to paint heterogeneous arrows. Don't over-smooth.

**Source tagging:**
- Holes 3, 7, 9, 11, 18 → `sourceTag = "manual_refined_v1"` (explicit features called out)
- All others → `sourceTag = "shotnavi_traced_v1"` (even though PDF is the primary, the tag reflects "traced from photographic reference" semantics; rename schema if Cesar prefers `pdf_traced_v1` — trivial)

**DoD:** 18 `green.json` files committed under `Assets/Resources/HoleData/Hole_NN/green.json`. All reference data in `screenshots/` + `A4_ホール攻略冊子.pdf` (already in spec folder).

---

### Phase 5 — Heightmap reconciliation (FULL PIPELINE, 1 day)

New batch tool: `Assets/Scripts/Editor/GreenAuthoring/HeightmapReconciler.cs`. Menu: `GOLFIN > Green Authoring > Reconcile Heightmaps (All Holes)` + per-hole variant.

**Algorithm:**

1. For each hole: load `green.json` slope grid + `zones.json` green polygon + `heightmap.bytes`.
2. Integrate the slope vector field over the polygon to produce a relative-height field anchored to the polygon centroid's current heightmap value (preserves overall green elevation; only redistributes locally).
3. Apply the height delta inside the polygon + a 1m smooth-falloff ring blending back to original heightmap values at the fringe.
4. Write back to `heightmap.bytes`. Backup original to `heightmap.original.bytes` before first reconcile per hole (one-time, idempotent).

**Closes 2026-05-01 open flag** ("ball dips at fairway→green seam") — heightmap now matches the `green.json` data the sim reads, fringe transition is smooth.

**Hard rules:**

1. Do NOT alter `heightmap.bytes` outside (green polygon + 1m falloff ring). Bit-exact preservation elsewhere verified by SHA-256 of "frozen" rows.
2. Do NOT modify the bake pipeline (`HoleGeoImporter.cs`) — this is a post-bake reconcile step. If the user re-bakes a hole, they must re-run reconcile after.
3. Idempotent: re-running reconcile on same `green.json` produces byte-identical heightmap (no drift).
4. If `heightmap.original.bytes` exists for a hole, future reconciles start from THAT (not the current heightmap). This makes Phase 5 safely re-runnable.
5. New EditMode test `HeightmapReconcileTests.cs`: (a) load reconciled heightmap, sample slope at 5 known points in green polygon, assert within 5% of `green.json` slope; (b) sample 5 points outside polygon, assert byte-equal to pre-reconcile snapshot.

**DoD:** 18 heightmaps reconciled; sample-slope test PASS for all 18; bit-exact preservation outside green polygons verified by hash; visual smoke (load hole 1 + hole 7 + hole 14, render terrain) confirms no visible seams at fringe. Test gate: **baseline + 2 PASS, 0 IGNORED.**

---

### Phase 6 — Physics integration (FULL PIPELINE, ½ day)

Extend `BallSimulation.RunPuttPhase` and `RunRollPhase` to sample green topology slope when current surface marker is `Green` or `GreenCollar`. Add lateral acceleration `a_lat = g · slope_dir · magnitudePercent / 100.0` to the existing per-step integration.

**New code:**

- `BallSimulation` private field `GreenTopology _greenTopo` initialized once per simulation run.
- `BallSimulation.InitializeForHole(int holeNumber)` resolves `GreenTopologyCache.GetForHole(holeNumber)` and assigns `_greenTopo`. Called from existing `Reset()` or wherever hole-context init lives — implementer audits.
- In `RunPuttPhase` + `RunRollPhase` step bodies: if `surfaceMarker is Green or GreenCollar` AND `_greenTopo != null`, call `_greenTopo.TrySampleSlope(ball.position.xz, out dir, out mag)` and add lateral component.

**`HoleSessionDriver`** (from §2c) invalidates `GreenTopologyCache.Invalidate(holeNumber)` on `OnHoleUnloaded` — single line addition, doesn't risk §2c.

**Hard rules:**

1. Do NOT modify `BallStateMachine.cs`, `BallState.cs`, `ShotResult.cs`, `Trajectory.cs`, `AeroModel.cs`. Same hands-off list as `controls_h`.
2. Do NOT modify aero LUTs/overlays, `putt.csv`, `surfaces.csv`. Physics constants are tuned — this adds a new force, not a re-tune.
3. Lateral acceleration is bounded — if `magnitudePercent > 12` (catastrophic slope, likely authoring error), clamp to 12 AND emit `DiagShotLogger.Log("[GREEN_TOPO_CLAMP]", ...)` once per shot.
4. **Bit-exact test gate breaks expected and OK** — re-snapshot ONLY tests that operate on Green/GreenCollar surfaces. Putts/rolls on Fairway/Rough/Bunker/CartPath MUST stay bit-exact (slope sampling returns 0 outside green region, by construction). Implementer audits each broken test before re-snapshotting; any non-green test breaking is a regression, NOT a re-snapshot candidate.
5. New EditMode tests in `BallSimulationGreenTopologyTests.cs`:
   - **Test A (positive):** Putt across a synthetic 3% pure-side-slope green at green-speed-11 from 10m. Assert lateral curve >= 18cm (`tan(arcsin(0.03)) × 10m × empirical_factor` — implementer measures actual baseline first iteration, locks expected value).
   - **Test B (regression guard):** Putt across a `magnitudePercent=0` everywhere green produces same trajectory as pre-spec baseline. Bit-exact comparison.
   - **Test C (transition):** Ball entering green region from fairway at 5m/s with slope underfoot has lateral force applied within 1 step of crossing fringe.
   - **Test D (clamp):** Synthetic 20% slope cell triggers clamp + diag log.

**DoD:** physics samples slope, lateral curve visible in lab smoke putt. Test gate: **baseline + 4 PASS, 0 IGNORED**, with re-snapshotted green-surface tests itemized in IMPLEMENTER_REPORT.

---

### Phase 7 — PuttPathPredictor redesign (FULL PIPELINE, ½ day)

**Supersedes** `Docs/Specs/Queued/puttpath_predictor_perf_and_design/` — when this lands, move that folder to `Docs/Specs/Completed/puttpath_predictor_perf_and_design_SUPERSEDED/` with a one-line README pointing at this phase.

**Replace** current predictor with baked arrow renderer:

- Subscribe to `HoleContext.PinWorld` change events + `BallContext.Position` change events.
- Read `GreenTopologyCache.GetForHole(currentHole)`.
- Render arrows for cells with `magnitudePercent >= 3.0` (matches PuttView/StrackaLine industry threshold for "this slope matters").
- Color magnitude blue (3-5%) → yellow (5-8%) → red (8%+). Arrow length scales linearly with magnitude in screen-pixel space (clamped to keep dense regions readable).
- Cell render budget: skip cells that wouldn't draw at least 1 visible pixel-length at current camera zoom (avoid wasted work on cells from off-camera).

**Old code paths to delete:**

- `Assets/Scripts/UI/HUD/PuttPathPredictor.cs` Assembly-CSharp stub (flagged in TellCode "B-followups — housekeeping").
- Any per-frame sim helper used only by the old predictor — implementer audits + lists in IMPLEMENTER_REPORT before deletion.

**Hard rules:**

1. Do NOT keep the old per-frame simulation path. Full removal — but list deleted files in IMPLEMENTER_REPORT for visibility.
2. Do NOT render arrows on cells with `magnitudePercent < 3.0`. Clean look, matches industry standard. Hard-coded 3.0 threshold initially; future spec can make it configurable.
3. Do NOT block on async — arrow generation is synchronous; small enough to run on Pin/Position change events at runtime.
4. Performance gate: predictor update on `PinWorld` change completes in < 2ms p95 on editor target. Measured by stopwatch around `RebuildArrows()` in new test `PuttPathPredictorPerfTests.cs` (5000-iteration loop, p95 from sorted samples).

**DoD:** predictor renders arrows on Phase 4's traced data; visually verifiable on hole 1 (subtle slopes) AND hole 7 (2-tier transition shows up as visible arrow-direction change at tier boundary); old code deleted; queued perf spec folder archived. Test gate: **baseline + 1 PASS, 0 IGNORED.**

---

### Phase 8 — Pin position wiring (SURGICAL, 3-4h, Architect-executable)

**Current flow (pre-Phase-8) — verified in `PhysicsLabController.cs` lines ~1531-1554:**

```
Hole scene loads
  ↓
 PhysicsLabController finds the scene's "Flag" GameObject (hand-placed in scene)
  ↓
 HoleContext.PinWorld = flagGo.transform.position   ← pin is sourced FROM scene
  ↓
 RealCupDetector built around PinWorld   ← cup capture follows pin
  ↓
 HUD, BotDriver, all consumers read HoleContext.PinWorld
```

**Important:** the visible flag mesh AND the (invisible) cup-capture region are BOTH driven by the scene's Flag GameObject today. There is no separate Cup GameObject — the cup is purely a math radius check (`RealCupDetector.IsInCupStatic`) around `PinWorld`.

**Phase 8 inverts the flow:**

```
Hole scene loads
  ↓
 Try GreenTopologyCache.GetForHole(N)?.GetDefaultPin()
  ↓
 If green.json present:
   HoleContext.PinWorld = green.json default pin       ← NEW: data drives
   flagGo.transform.position = green.json default pin  ← NEW: also move the visible flag
 If green.json absent:
   HoleContext.PinWorld = flagGo.transform.position    ← fallback: existing behavior
  ↓
 RealCupDetector built around (now data-driven) PinWorld   ← unchanged code, picks up new pin automatically
  ↓
 HUD etc.
```

**Changes (3 files):**

1. `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` (~20 lines modified around line 1535-1554):
   - Replace the `flagGo.transform.position` read with the three-level fallback above
   - When `green.json` provides a pin, also write that pin to `flagGo.transform.position` so the visible mesh moves to match
   - Preserve existing `GreenCentroidWorld` fallback for holes with neither `green.json` nor a scene Flag
   - Diagnostic log: `[PhysicsLab][§8] Pin source: greenJson | sceneFlag | centroidFallback (pos=...)`

2. `Assets/Scripts/Gameplay/UI/ShotUI/HUD/HoleContext.cs`:
   - Add `public static Vector3[] PinCandidates` (read-only-ish; reset on `Reset()`)
   - Add `public static string[] PinLabels` (aligned indices)
   - Existing `PinWorld` static field unchanged in signature

3. `Assets/Scripts/Course/Runtime/GreenTopologyCache.cs` (created in Phase 2):
   - Already exposes `GetForHole(int)` — no change here

**Hard rules:**

1. Do NOT change `HoleContext.PinWorld` field TYPE or static-bus signature — only the value source.
2. Do NOT touch `RealCupDetector.cs` — it consumes `PinWorld` and rebuilds automatically downstream.
3. Do NOT touch the scene file (`Hole_NN_Geo.unity`) — the flag GameObject stays in the scene; Phase 8 moves it at runtime via `transform.position` assignment, leaving artist-placed prefab intact.
4. Backward compat: holes without `green.json` MUST continue to work via existing flagGo path. No `green.json` present → zero behavior change.
5. If `flagGo` is null AND `green.json` is present, still write `PinWorld` from `green.json` (HUD + cup still work; just no visible flag movement — not a regression since today no flag = `GreenCentroidWorld` fallback anyway).

**Tests (new file `Assets/Scripts/Gameplay/Tests/HoleContextPinTests.cs`):**

- **A:** Load hole 1 with `green.json` present (post-Phase-3) → assert `HoleContext.PinWorld == green.json.pinCandidates[defaultPinIndex]`.
- **B:** Load hole 1 with `green.json` present + scene Flag GO present → assert `flagGo.transform.position == green.json.pinCandidates[defaultPinIndex]` after `HoleContext.Raise()`.
- **C:** Load a hole without `green.json` (stub by temporarily removing) → assert `PinWorld == flagGo.transform.position` (legacy fallback).
- **D:** Load a hole without `green.json` AND without scene flag → assert `PinWorld == GreenCentroidWorld` (existing fallback).
- **E:** `HoleContext.PinCandidates.Length >= 1` and `PinCandidates[defaultPinIndex] == PinWorld` after green.json load.

**DoD:** 18 holes load with pin position from authored data; visible flag mesh moves to data-driven location; cup capture works at new location; fallback paths tested. Test gate: **baseline + 5 PASS, 0 IGNORED.**

---

### Phase 9a — Cup capture FX (FULL PIPELINE, ½ day) — MINIMUM

**Problem (today):** The cup is a flat texture disc on the green surface. When the ball enters cup-capture radius, `RealCupDetector` returns true and play ends, but visually the ball just stops on top of the disc. No falling, no sound, no FX. Looks broken.

**Phase 9a is FX-only — no geometry change.** Goal: when capture fires, ball appears to drop into the cup with audio + particle cues.

**Trigger:** `BallStateMachine.OnShotComplete` event (or whichever event fires on cup-capture success — implementer audits and uses the EXISTING event). Do NOT modify `BallStateMachine.cs` (still on the hands-off list).

**New code:**

1. `Assets/Scripts/Gameplay/FX/CupCaptureFX.cs` (new MonoBehaviour, in scene or instanced by `PhysicsLabController`):
   - Subscribes to ball-completion event in `OnEnable`, unsubscribes in `OnDisable`
   - On capture trigger: reads `HoleContext.PinWorld`, runs animation coroutine:
     - **Frame 0:** Ease ball XZ toward pin over 150 ms (smooth-step)
     - **150-300 ms:** Drop ball Y by 0.15 m + scale uniform 1.0 → 0.0
     - **At 300 ms:** Hide ball renderer; emit ParticleSystem `_cupPuff` at PinWorld; play AudioSource `_plunkSfx`
     - **300-600 ms:** Optional secondary "rattle" particle pulse (1-2 small bounces)
   - Configurable in inspector: durations, ease curves, particle prefab, audio clip

2. `Assets/Scripts/Gameplay/FX/Golfin.Gameplay.FX.asmdef` (new asmdef, references `Golfin.Gameplay`, `Golfin.Gameplay.UI.HUD`)

3. `Assets/Prefabs/FX/CupCaptureFX.prefab` (new prefab):
   - Empty GameObject with `CupCaptureFX` component
   - Child ParticleSystem `_cupPuff` (small green-dust burst, 0.4s lifetime, ~20 particles)
   - Child AudioSource `_plunkSfx` with `cup_plunk.wav` (sourced or placeholder)

4. **Disc texture swap** — cheap visual win: replace current flat circle disc with a recessed-look texture (darker center + rim shadow). Asset path TBD; implementer flags as `[NEEDS ART]` if no recessed disc texture exists in `Assets/Art/Course/`. Falls back to current flat disc if art missing.

**Hard rules:**

1. Do NOT modify `BallStateMachine.cs`, `RealCupDetector.cs`, or any physics-tier file. Subscribe to existing events only.
2. Do NOT block the Result Screen (§2d) transition. Animation runs in parallel; if Result Screen fires before FX completes, FX must abort gracefully (Stop on `OnDisable`).
3. Do NOT add audio that plays through any non-existent `AudioManager`. If `AudioManager.Instance` exists in the project, route through it; else play directly via AudioSource (audit during implementation).
4. Animation must be skip-able — if user taps anywhere during the FX, it completes instantly and Result Screen fires.
5. FX prefab is loaded once at hole-load time and pooled; don't `Instantiate` per-shot.

**Tests (new file `Assets/Scripts/Gameplay/Tests/CupCaptureFXTests.cs`):**

- **A:** Mock a capture event → assert `CupCaptureFX` coroutine starts within 1 frame.
- **B:** During animation, assert ball renderer transitions to disabled by frame at t=300ms (tolerance ±1 frame).
- **C:** Particle system emit count > 0 at t=300ms.
- **D:** Skip input mid-animation → assert coroutine completes within 1 frame of input.

**DoD:** Capture animation plays on every successful cup-capture across all 18 holes; SFX audible; particles visible; ball disappears smoothly; disc texture swapped (or `[NEEDS ART]` flagged). Test gate: **baseline + 4 PASS, 0 IGNORED.**

---

### Phase 9b — Real geometric cup (FULL PIPELINE, 1-1½ days) — OPTIONAL POLISH

**Status:** Queued behind Phase 9a. Do not block 9a on 9b. 9b is the "this looks like a real hole" upgrade; 9a is the "this no longer looks broken" minimum.

**Goal:** Replace the FX-only illusion with an actual hole geometry. Ball physically falls below ground plane into a cup-wall cylinder.

**Approach — Stencil-mask shader:**

1. `Assets/Art/Shaders/CupStencilMask.shadergraph` (URP shader graph, new file):
   - Stencil writes to clear green pixels inside a small disc centered at PinWorld
   - Result: green mesh renders with a circular hole at pin location
   - Sized to 4.25" / 108 mm (regulation cup diameter)

2. `Assets/Prefabs/FX/CupInterior.prefab`:
   - Cup-wall cylinder mesh, 108 mm diameter, 102 mm deep (4" regulation)
   - Dark interior material
   - Cylinder bottom blocks ball; rim is collidable
   - Instanced once at hole-load, parented to PinWorld

3. Modify Phase 9a's `CupCaptureFX` to disable the ball-scale + position-cheat animation when 9b is active. Ball just physically falls due to existing gravity + collision; cup interior catches it.

4. **Cup-rim physics interaction (optional sub-feature):** Add a thin lip ring just above the cup bottom. Balls that enter cup at borderline speed (just above capture threshold) hit the ring, lose energy, and rattle around before settling — produces visible "lip-out" effect when speed is grazing the cup-capture threshold.

**Hard rules:**

1. Stencil shader must work on iOS Metal AND Android Vulkan/GLES3. Test gate must include both.
2. Heightmap (`heightmap.bytes`) is NOT modified — hole is shader/stencil, not actual mesh deformation. (Avoids re-running Phase 5 reconcile every time pin moves.)
3. Cup interior must not affect `RealCupDetector` math — the existing radius check still authoritatively determines capture, NOT physics collision with the cup mesh.
4. Performance budget: stencil + interior mesh + lip ring must add < 0.3ms per frame combined on a mid-range Android device (Pixel 5 / Galaxy A52 class).

**Tests:**

- **E:** Stencil renders hole on iOS Metal smoke run; pixel inspection at PinWorld XZ shows cup interior color, not green.
- **F:** Stencil renders hole on Android Vulkan smoke run; same pixel inspection.
- **G:** Ball enters cup-capture radius at 1.2 m/s (above threshold by 30%) → ball physically falls into cup interior without intersecting walls.
- **H:** Ball enters at 1.55 m/s (just above 1.5 m/s threshold) → optional lip-out test; ball rattles for >300 ms before settling (only if lip-ring sub-feature included).

**DoD:** Stencil-mask hole renders on both target platforms; ball falls into cup geometry; no perf regression; `RealCupDetector` capture logic unchanged. Test gate: **baseline + 4 PASS (E–H), 0 IGNORED.**

---

## Out of scope (do not creep)

- **Daily pin rotation** — Loop v2+ feature; this spec ships canonical-default-pin only.
- **Multi-tier visual mesh fidelity** — Phase 5 deforms heightmap from slope grid, but sharp ridges vs smooth curves at tier breaks are an artifact of the integration math; post-Loop-v1 polish.
- **Other courses** — Lomond only. If we add courses (Loop v2+ feature), repeat Phases 3-4 + run Phase 5; tool + format work in Phases 1-2 + physics in Phase 6 + predictor in Phase 7 are course-agnostic.
- **Grain (grass direction) effect** — Bent grass is mowed uniformly; skip. Different surface types (poa, bermuda) might need it later.
- **Green speed (Stimp) variation** — already configured in `putt.csv` per surface; not touched here.
- **Wet/dry slope behavior** — environment-dependent; deferred to weather system work.

---

## Cross-spec dependencies

- **§2d (Result Screen, NEXT)** — wants `HoleContext.PinCandidates` for pin-position display. Phase 8 unblocks. If §2d ships before Phase 8 lands, retrofit accessor.
- **controls_h (chase camera regression, NEXT)** — fully independent.
- **§2c (turn counter, blocked on controls_h)** — fully independent. Phase 6's slope sampling fires once per BallSimulation step; doesn't touch `BallStateMachine.OnShotComplete` that §2c subscribes to.
- **2026-05-01 open flag (ball dips at fairway→green seam)** — closed by Phase 5.
- **HUD ClubContext drift open flag (2026-05-06)** — unrelated; Phase 6 doesn't touch ClubContext.

---

## Estimate

| Phase | Type | Effort |
| --- | --- | --- |
| 1 — Data format spec | Architect direct | ½ day |
| 2 — Authoring tool | FULL PIPELINE | 1 day |
| 3 — Procedural baseline | Cesar manual | ½ day |
| 4 — Shot Navi tracing | Cesar manual (trial-window time-boxed) | 1 day |
| 5 — Heightmap reconciliation | FULL PIPELINE | 1 day |
| 6 — Physics integration | FULL PIPELINE | ½ day |
| 7 — Predictor redesign | FULL PIPELINE | ½ day |
| 8 — Pin wiring | SURGICAL | 3-4h |
| 9a — Cup capture FX (minimum) | FULL PIPELINE | ½ day |
| 9b — Real geometric cup (optional polish) | FULL PIPELINE | 1-1½ days |

**Total pipeline work (without 9b):** ~4 days FULL PIPELINE + ½ day Architect + 3-4h SURGICAL.
**Total pipeline work (with 9b):** ~5½ days FULL PIPELINE + ½ day Architect + 3-4h SURGICAL.
**Total Cesar manual work:** ~1.5 days (mostly Phase 4 tracing).
**Calendar:** ~1.5 weeks if interleaved with Loop v1 work; ~5 working days if focused.

---

## Risk register

- **R1 (medium):** Shot Navi data is GPS-grade, not laser-scan-grade. Mitigation: Phase 3 procedural baseline gives a defensible starting point; Phase 4 refines over it; visible authoring tool lets Cesar override anything that looks wrong.
- **R2 (low):** 3-day trial window forces Phase 4 timing. Mitigation: Phase 3 is independent and unblocks Phases 5-8, so Phase 4 can slip without holding up the rest.
- **R3 (low):** Heightmap reconcile (Phase 5) might surface visual seams at fringe boundaries on holes with steep collar drops. Mitigation: 1m smooth falloff parameter is tunable; visual smoke gate catches it; backup `heightmap.original.bytes` enables clean re-run.
- **R4 (low):** Existing physics calibration (controls_e/f, putt.csv) was tuned against flat greens. Adding slope might shift the *feel* of putt distance. Mitigation: Phase 6 includes regression test that flat-greens (`magnitudePercent=0`) produces same trajectory as pre-spec — only sloped greens behave differently, which is the point. Cesar plays a session after Phase 6 lands and signs off on feel before promoting Phase 7.
- **R5 (low):** Shot Navi insufficient quality after Phase 4 capture. Mitigation: StrackaLine app subscription ($99/yr) as fallback; switch costs ~1 day of re-tracing the worst greens.

---

## Reference

- `NOTES.md` (this folder) — decision log + data acquisition workflow + known green features.
- `Docs/Specs/Queued/puttpath_predictor_perf_and_design/NOTES.md` — pre-existing perf concerns folded into Phase 7.
- TellCode 2026-05-01 open flag — "Ball penetrates green when rolling onto it from the fairway" — closed by Phase 5.
- TellCode "B-followups → Housekeeping" — Assembly-CSharp `PuttPathPredictor.cs` stub deletion — handled by Phase 7.
- `Docs/Roadmap.md` — fits between §2 (Loop v1) and §3 (Loop v2). Doesn't gate §2 closure.
