# SPEC: Green Topology and Pin Authoring (umbrella)

**STATUS:** QUEUED (umbrella) — promote sub-phases to `Docs/Specs/Active/<sub-slug>/` individually as kicked off.
**FOLDER:** `Docs/Specs/Queued/green_topology_and_pin_authoring/`
**SUPERSEDES:** `Docs/Specs/Queued/puttpath_predictor_perf_and_design/` — folded into Phase 7. Archive that folder to `Completed/` with cross-ref when Phase 7 lands.
**NOTION:** TBD — Cesar to add umbrella entry under §3/§4 area (post Loop v1), Order ~290 or interleave per priority. Sub-phases get their own Notion entries when promoted to Active.
**WRITTEN:** 2026-05-18

---

## One-line

Replace flat greens with real Lomond topology by (1) building an editor tool to author per-green slope grid + pin candidates using **Shot Navi 3DX** reference screenshots, (2) baking slope data into both a sim-readable `green.json` AND the existing `heightmap.bytes`, (3) wiring `BallSimulation` roll/putt phases to sample slope per-step, and (4) redesigning `PuttPathPredictor` to render baked slope arrows with zero per-frame sim.

---

## Locked decisions (Architect ↔ Cesar, 2026-05-18 chat)

- **L1 — Data source: Shot Navi 3DX free tier + first-launch 3-day premium trial.** Lomond confirmed in Shot Navi database (course ID 806; elevation refreshed 2023-09-03, map updated 2024-07-09). PuttView book rejected for shipping risk to Spain. PuttView app rejected (Lomond Japan not in their digital library — they only have Loch Lomond in Scotland). StrackaLine left as fallback if Shot Navi data quality is insufficient.
- **L6 — Captures already in hand (2026-05-18):** 36 PNGs in `screenshots/` — 18 × `_strategy` (distance/yardage view) + 18 × `_heatmap` (topographic view, rainbow icon active). Heatmap is the slope-authoring source; Strategy is the green-shape + pin-position reference.
- **L7 — Shot Navi green-view distances are METERS, not yards.** Course-level yardage (back/regular/front tees, scorecard) is yards; green-zoom distances in Shot Navi (perimeter `0/5/10/15/20`, inline `13`, `11`, `16` numbers) are meters. Cesar locked: course = yards, green = meters. Matches `cellSize: 0.5` (m) in the data format below — 1 Shot Navi grid cell = 1 m, so cellSize 0.5 = 2 cells per visible grid square.
- **L8 — Pin position: derived from Shot Navi flag location.** The white flag visible in each Shot Navi capture IS the canonical default pin. `defaultPinIndex = 0` maps to the Shot Navi-displayed flag; 2-4 alternate candidates authored manually based on visible green topology.
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

**DoD for Phase 1:** schema is fixed in this SPEC. Phase 2 implements the C# class.

---

### Phase 2 — Authoring editor tool (FULL PIPELINE, 1 day)

New editor window. Menu: `GOLFIN > Green Authoring > Open Editor`. Files:

- `Assets/Scripts/Editor/GreenAuthoring/GreenTopologyEditor.cs` — EditorWindow.
- `Assets/Scripts/Editor/GreenAuthoring/GreenAuthoringMath.cs` — affine transform, procedural fill heuristics.
- `Assets/Scripts/Editor/GreenAuthoring/Golfin.Editor.GreenAuthoring.asmdef` — references `Golfin.Course.Editor` (for zones.json reader) + `Golfin.Course.Runtime` (for GreenTopology).
- `Assets/Scripts/Course/Runtime/GreenTopology.cs` — implements Phase 1 schema.
- `Assets/Scripts/Course/Runtime/GreenTopologyCache.cs` — `static GetForHole(int) → GreenTopology` with `InvalidateAll()` and `Invalidate(int)`. Used by Phase 6 sim and Phase 7 predictor.

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
4. Tool is editor-only (no runtime asmdef impact except adding `GreenTopology.cs` + `GreenTopologyCache.cs` to `Golfin.Course.Runtime`).
5. New asmdef name: `Golfin.Editor.GreenAuthoring`. autoReferenced: false.
6. EditMode tests in new file `Assets/Scripts/Course/Tests/GreenTopologyTests.cs`: (a) round-trip — write `green.json`, read back, assert slope grid byte-equal; (b) `TrySampleSlope` returns false outside bounds; (c) `GetPinCandidates` returns the configured list in order. **+3 tests, 0 IGNORED.**

**DoD:** tool opens, loads hole 1, displays green polygon overlay, supports paint + procedural-fill + pin-candidate paths, saves valid `green.json` that round-trips through `GreenTopology.LoadFromResources`. Test gate: **N+3 PASS, 0 IGNORED.**

---

### Phase 3 — Procedural baseline pass (Cesar manual, ½ day)

Cesar opens `GreenTopologyEditor` for each of 18 holes, hits "Auto-procedural fill", reviews the heuristic output, manually paints 3-5 pin candidates, saves. Captures a baseline 18 × `green.json` so Phases 5-8 have data to consume even before Shot Navi tracing.

**Out-of-band manual override:** for hole 7, set `sourceTag = "manual_refined_v1"` and manually author the documented 2-tier break (back tier ~0.5m higher than front; ridge runs roughly perpendicular to approach axis). Other holes stay `sourceTag = "procedural_v1"`.

**DoD:** 18 `green.json` files committed under `Assets/Resources/HoleData/Hole_NN/green.json`.

---

### Phase 4 — Shot Navi tracing pass (Cesar manual, ~1 day of focused tracing)

**Status (2026-05-18):** Captures complete. 36 PNGs in `screenshots/`:
- `lomond_hole_NN_shotnavi_strategy.png` (18 ×) — yardage/distance view; useful for green polygon shape + canonical pin position (white flag glyph)
- `lomond_hole_NN_shotnavi_heatmap.png` (18 ×) — topographic view with rainbow icon active; primary slope source

**Tracing workflow (per hole):**
1. Open `GreenTopologyEditor` for hole NN.
2. Drop `lomond_hole_NN_shotnavi_strategy.png` into Backdrop slot. Align using green-edge anchor points (remember: grid is METERS). Use this view to confirm green polygon shape + capture canonical pin position from the visible white flag as `pinCandidates[0]`.
3. Swap Backdrop to `lomond_hole_NN_shotnavi_heatmap.png`. Re-align using same anchor points.
4. Trace slope arrows over the heatmap. Color cue: green = flat or subtle, yellow = noticeable slope, orange/red = steep. Magnitude scrubber: 1-2% for green-coloring, 3-4% for yellow, 5-8%+ for orange/red. Dashed lines on heatmap are fall-line / slope-direction references.
5. Add 2-4 alternate pin candidates beyond the canonical default — typically one per visible tier or flat zone in the heatmap.
6. Save.

**Note on Lomond character:** Per Japanese course reviews, Lomond is "balanced with limited undulation." Most greens in heatmap mode show mostly-green coloring with subtle accents. Hole 9 (`lomond_hole_09_shotnavi_heatmap.png`) is the visible exception — notably yellow/orange. Author conservatively; don't fabricate slope where the heatmap shows green.

**Out-of-band data:** also sweep Japanese golf review sites for any qualitative green descriptions; capture in `NOTES.md` § Known green features as bullet points per hole.

**DoD:** 18 `green.json` files with `sourceTag = "shotnavi_traced_v1"` (or `manual_refined_v1` for hole 7 + any others manually augmented). Screenshots already committed under `screenshots/`.

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

### Phase 8 — Pin position wiring (SURGICAL, 1-2h, Architect-executable)

Modify `HoleContext` to source `PinWorld` from `green.json.pinCandidates[defaultPinIndex]` instead of whatever current placeholder source.

**Changes:**

- `HoleContext.PinWorld` value source changed to `GreenTopologyCache.GetForHole(currentHole)?.GetDefaultPin() ?? legacyPlaceholderValue`.
- New `HoleContext.PinCandidates` accessor (read-only) for §2d Result Screen + future Loop v2 pin rotation.
- New `HoleContext.PinLabels` accessor (read-only, aligned indices with PinCandidates).

**Hard rules:**

1. Do NOT change `HoleContext.PinWorld` field TYPE or static-bus signature — only the value source.
2. Backward compat: if `green.json` missing OR `pinCandidates` empty, fall back to current placeholder (no break for any hole not yet authored in Phase 3).
3. New EditMode test in `HoleContextTests.cs`: load hole 1 (post-Phase-3), assert `HoleContext.PinWorld == green.json.pinCandidates[defaultPinIndex]` after `HoleContext.Raise()`. Fallback test: stub a hole without `green.json`, assert legacy placeholder returned.

**DoD:** 18 holes load with pin position from authored data; fallback path tested. Test gate: **baseline + 2 PASS, 0 IGNORED.**

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
| 8 — Pin wiring | SURGICAL | 1-2h |

**Total pipeline work:** ~3.5 days FULL PIPELINE + ½ day Architect + 1-2h SURGICAL.
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
