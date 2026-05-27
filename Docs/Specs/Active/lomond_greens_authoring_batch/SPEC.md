# SPEC: lomond_greens_authoring_batch

**STATUS:** ACTIVE
**FOLDER:** `Docs/Specs/Active/lomond_greens_authoring_batch/`
**PARENT UMBRELLA:** `Docs/Specs/Queued/green_topology_and_pin_authoring/SPEC.md` — this task absorbs Phases 3 + 4 (procedural baseline dropped per umbrella note: 'Phase 3 procedural pass has zero value since production greens are flat-mesh'; Phase 4 traces directly from authoritative sources)
**WORKFLOW TIER:** FULL PIPELINE (visual fidelity + spatial math)
**WRITTEN:** 2026-05-27 11:50 CEST
**NOTION ORDER:** insert after current Loop v1 items, in §3/§4 area per umbrella ordering

---

## One-line

For each of 18 Lomond holes: read the strategy-booklet PDF panel as primary source, cross-reference Shot Navi heatmap + GSI contour, author the dense per-cell slope grid (`green.json` per the Phase 1 schema), and gate visually with `CaptureCore` against the PDF panel until match.

---

## Source hierarchy (highest authority first)

### S1 — PDF strategy panels (PRIMARY, authoritative for slope direction + magnitude)
- **Path:** `Docs/Specs/Queued/green_topology_and_pin_authoring/A4_ホール攻略冊子.pdf`
- **Mapping:** Page N+1 = Hole N (page 0 is cover, page 19 back-matter). Each page has a `GREEN攻略法` panel.
- **Per panel:**
  - Top-down stylized green outline with apron/fringe rendering
  - Dimension labels (W × H in meters per L7 calibration)
  - Black arrows: per-region slope direction (arrowhead at downhill end)
  - Dashed white polyline: tier ridge boundary
  - Japanese strategic note above the panel
- **Extraction:** render page at ≥400 DPI, crop the panel area (~x[0.48–0.78] × y[0.65–0.96] of page). Arrows are **rasterized**, not vector — Architect verified via PyMuPDF `get_drawings()` on 2026-05-27. Visual reading by VLM is the only path (CV detection has failed reliably — see §6.1).

### S2 — Shot Navi 3DX captures (SECONDARY, for cross-validation)
- **Path:** `Docs/Specs/Queued/green_topology_and_pin_authoring/screenshots/`
- **Format:** 18 × `lomond_hole_NN_shotnavi_heatmap.png` + 18 × `lomond_hole_NN_shotnavi_strategy.png`
- **Heatmap value:** 3D approach view at varying season/time-of-day. Use for **macro slope direction** (front-to-back vs back-to-front, left vs right). NOT detailed enough for per-region slope. Color-tint variance across captures (autumn for 2/9/12/15, evening blue for 10/13/16) is **scene lighting**, not topography — ignore the global tint and look at relative color gradient on the green surface itself.
- **Strategy value:** Distance/yardage overlay; primary use is locating the **flag pin** (white flag glyph in each capture = canonical `defaultPinIndex = 0` per L8). The visible flag's green-local fractional position is the default pin.

### S3 — GSI / green polygon contour (TERTIARY, for shape only)
- **Primary contour:** `Assets/Golf/Courses/lomond-country-club/Data/hole-XX-geo/greens.json` — 32-point polygon in world XZ, all 18 holes confirmed present (Architect verified 2026-05-27, contradicting earlier 'missing for 02/12/14' claim).
- **Sidecar:** `Assets/Resources/HoleData/Hole_NN/zones.json` — has `Green` type for 15 of 18; Holes 02/12/14 omit Green entry (anomaly noted, NOT in scope for this task — file `[TODO-zones-importer-bug]` separately if downstream consumers break).
- **Use:** real polygon shape in world coordinates for grid axis-aligned bounding rect calculation. Combine with `Green_1` GameObject transform in `Assets/Golf/Courses/lomond-country-club/Generated/Hole_NN_Geo.unity` to verify world placement.

### S4 — Locked architectural decisions (REQUIRED reading)
- Re-read umbrella SPEC §'Locked decisions' L1–L11 before starting. Especially L2 (storage = dense 0.5m grid), L7 (panel dims are meters), L8 (pin from Shot Navi flag), L10 (confirmed 2-tier holes: 3, 7, 11, 18 — Hole 7 is L/R diagonal ridge NOT front/back).

### S5 — Rejected baseline (WORST-CASE reference, do NOT trust slope data)
- **Path:** `Docs/Reference/Lomond_Green_Topology.yaml`
- **Status:** rejected v1, Architect-authored, fails Cesar review 2026-05-27 with these documented error categories:
  - False positives outside inner playing surface (all 18 holes)
  - Direction reversal on holes 2, 3, 6, 7, 9, 10, 11, 14, 15, 17
  - Missing arrows adjacent to dashed ridge lines on 11, 13, 14, 18
- **Reusable bits:** region naming conventions (`upper_left_tier`, `back_right`, `front_center_defensive`), pin candidate labels, feature classifications, JP/EN translation pairs. **Do NOT trust:** `slopeDir`, `magnitudePct`, `polylineFrac`, `boundsFrac`.

---

## Per-hole process

Run for each of 18 holes in order 1→18 (early holes inform parameter calibration for later ones).

### 1. Read sources
- Render PDF page at 400 DPI; crop the green panel
- Load Shot Navi heatmap PNG
- Load Shot Navi strategy PNG (for pin location)
- Load `greens.json` for the world-space polygon
- Read this hole's row in `Lomond_Green_Topology.yaml` for feature category + JP/EN notes (semantic only)

### 2. Visual extraction (VLM read against the PDF panel)
For each panel, identify:
- **Inner playing surface bounds** in panel pixels
- **Every arrow:** position (pixel coords), direction (unit vector, ↘ ↓ ← etc.), inferred magnitude class (small/medium/large arrow)
- **Ridge polyline:** dashed-line endpoints + waypoints in panel pixels (if present)
- **Apron/fringe boundary** if visually distinct from inner green

Cross-reference with the heatmap: does the macro slope direction agree? If the panel says 'all arrows ↓' but the heatmap shows blue at the front (downhill front) and red at the back (uphill back), they agree. If contradictory, flag for Cesar review — do not silently pick one.

### 3. Convert to green-local fractional coords [0, 1]²
- Detect inner playing-surface pixel bounds in panel
- Map arrow positions to `(fx, fy)` where `(0,0)` = front-left corner of green AABB, `(1,1)` = back-right
- Same for ridge polyline waypoints
- 'Front' direction: the side of the green closest to the player approach (look at full-hole layout image on the same page for direction)

### 4. Segment into regions
- Cluster arrows by direction similarity + spatial proximity → 1-5 regions per hole
- Per region: bounds (frac), dominant slope direction (unit vector), magnitude (% from 1.0–6.0 per existing convention)
- Ridge polyline becomes a separate entity; cells within `transitionBandMeters` of the ridge interpolate between adjacent region slopes

### 5. Expand to dense per-cell grid (Phase 1 schema)
- Grid AABB = `greens.json` polygon AABB in world XZ
- `cellSize = 0.5m` (L2 locked)
- For each cell inside the polygon:
  - Find which region it belongs to → use that region's slope vector × magnitude
  - If within ridge transition band → interpolate between adjacent regions
  - Pack into `slopeGridBase64` per existing Phase 1 schema
- Cells outside polygon: `(0, 0, 0)`

### 6. Author pin candidates
- Default pin (`defaultPinIndex = 0`): from Shot Navi strategy capture's white-flag glyph position
- 2–4 alternates: place at tier centroids, defensive front spots, back shelves — informed by topology
- Each candidate: world XYZ + descriptive label

### 7. Visual gate (CaptureCore)
- **Camera:** fixed top-down, framed to match PDF panel aspect, green centered, approach direction = +Y in frame
- **Render with:** authored slope grid as colored arrow overlay (existing `PutterGreenReader` warped-grid renderer or a debug-only top-down equivalent)
- **Diff target:** PDF panel cropped at same aspect ratio
- **Save to:** `tasks/lomond_greens_authoring_batch/Hole_NN/visual_gate_attempt_N.png`

### 8. Gate criteria (per hole)
Hole passes when:
- (a) **Arrow direction alignment** — cosine similarity between rendered slope vectors and PDF arrow directions at matched positions ≥ 0.85
- (b) **Region boundaries** — rendered region bounds within ±10% of PDF region extents
- (c) **Ridge polyline** — rendered ridge within ±5% of green diameter from PDF dashed-line position (only for ridge holes per L10)
- (d) **Macro slope** — overall slope direction consistent with heatmap color gradient (qualitative check, blocking only if dramatically inverted)

If gate fails: refine in priority order (arrow directions > region bounds > ridge > magnitudes), re-render, re-gate. **Max 5 iterations per hole**. After 5, write a diagnostic note to `tasks/lomond_greens_authoring_batch/Hole_NN/STUCK.md` describing what's wrong and continue to next hole — do not block the batch.

### 9. Per-hole outputs
- `Assets/Resources/HoleData/Hole_NN/green.json` — dense slope grid + pin candidates (Phase 1 schema)
- `Assets/Resources/HoleData/Hole_NN/heightmap.bytes` — updated per L3 (slope baked into mesh height for visual consistency)
- `tasks/lomond_greens_authoring_batch/Hole_NN/visual_gate_final.png` — approved comparison
- `Docs/Reference/Lomond_Green_Topology.yaml` — updated row for this hole (audit trail; the rejected v1 entry is overwritten with corrected data)

---

## Constraints

### Use existing tooling — no rewrites
- `Assets/Scripts/Editor/GreenAuthoring/GreenTopologyEditor.cs` is the authoring API (Phase 2 DONE). Drive it programmatically; do not bypass it.
- `CaptureCore.SnapPlayModeSafe` is the only sanctioned visual capture path (per project memory, Lesson Q + reviewer protocol). Per-task workarounds banned.
- Production greens are flat-mesh by design — slope lives in the topology data layer, NOT in scene mesh deformation. Do NOT modify `Hole_NN_Geo.unity` scene meshes' `Green_1` MeshFilter.

### Don't trust the rejected baseline for slope
- Read PDF panels fresh per hole. The baseline `Lomond_Green_Topology.yaml` is documented to have systematic errors (S5 above). Treat only the semantic/naming fields as reusable.

### Watch for the documented gotchas
Re-read `Docs/Diagnostics/2026-05-12-physics-lab-postmortem.md` before starting — the 4-bug chain (auto-property guard under reload-domain-only, editor `Instantiate` scene pollution, defense-in-depth masking, distributed state mutation) hits authoring-tooling work specifically.

### Iteration-spiral protocol (Lesson Q)
If implementation hits iteration 3 on the same problem with same approach: STOP, surface to Architect. Visual-gate failures on a specific hole that won't converge after 3 attempts → file `STUCK.md` per §8 and move on; don't burn more iterations.

---

## Cross-hole notes

- **Process order:** 1 → 18. Hole 1 and Hole 4 are simplest (single-region, mostly level) — use them to calibrate parameter ranges before tackling complex holes.
- **Complex holes (do last):** 7 (L/R 2-tier diagonal), 9 (most contoured per L11), 11 (back-to-front 2-tier), 18 (front-to-back 2-tier with ridge).
- **Suspicious holes from baseline review:** 6 (top-right backwards arrow), 10 (center swap), 11 (missing ridge arrows + cluster reversal), 13/14/18 (ridge-adjacent arrows missed). Pay extra attention.
- **Fan-out viability:** if iteration cost per hole is high, this DECOMPOSES cleanly — each hole's `green.json` is independent of others' (no shared scene/singleton/asmdef). Eligible for Task tool fan-out per project memory.

---

## Risks / open flags

- **Holes 02/12/14 zones.json `Green` omission** — anomaly noted, NOT in scope. greens.json has them. If anything downstream queries zones.json for green polygons it'll miss these 3. File separately if encountered.
- **PDF arrows are rasterized** — Architect tried vector extraction (PyMuPDF `get_drawings()`); arrows are in the embedded JPEG, not vector layer. VLM visual reading is the only path. If the implementer's VLM also struggles with per-arrow precision, fall back to semantic-per-region authoring (region count + dominant direction per region, no per-arrow pixel matching).
- **CV detection of arrows demonstrated to fail** — Architect ran 8 CV iterations 2026-05-27 with persistent error categories (false positives on apron/dimension labels, direction reversal on 10 of 18 holes, missed arrows near ridges). Do not waste cycles on rebuilding CV detection; visual reading + visual gate is the locked path.

---

## Acceptance criteria

- ✅ All 18 holes have `green.json` written with dense slope grid (Phase 1 schema, `schemaVersion: 1`)
- ✅ All 18 holes have updated `heightmap.bytes` per L3
- ✅ All 18 visual gate diffs pass §8 criteria OR have `STUCK.md` with diagnostic
- ✅ `Docs/Reference/Lomond_Green_Topology.yaml` updated with corrected rows
- ✅ Per-hole `visual_gate_final.png` committed under `tasks/lomond_greens_authoring_batch/`
- ✅ Reviewer pipeline (self-reviewer + reviewer) approves per project memory protocol — independent pixel scan BEFORE reading prior verdicts, programmatic bbox checks, `CaptureCore` sanctioned path verified

---

## Files to read first (in order)

1. `Docs/AI_CONTEXT.md` — current session state
2. `Docs/Specs/Queued/green_topology_and_pin_authoring/SPEC.md` — umbrella, especially §'Locked decisions' L1–L11
3. `Assets/Scripts/Course/Runtime/GreenTopology.cs` — Phase 1 data format
4. `Assets/Scripts/Editor/GreenAuthoring/GreenTopologyEditor.cs` — authoring API (Phase 2)
5. `Assets/Resources/HoleData/Hole_01/green.json` — Phase 1 skeleton example for shape
6. `Docs/Diagnostics/2026-05-12-physics-lab-postmortem.md` — bug-class patterns to watch
7. `Docs/Architecture/RUNTIME_BLUEPRINT.md` §10 Editor Tooling
8. `Docs/Reference/Lomond_Green_Topology.yaml` — rejected baseline (semantic fields reusable, slope fields ignored)
9. This SPEC

---

## Handoff kickoff

When ready, Cesar paste into Claude Code:

```
Use the golfin-implementer subagent on "lomond_greens_authoring_batch"
```
