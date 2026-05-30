# CESAR_REJECTION — green_slope_height_bake

## LATEST — 2026-05-29 (iter-10): wrong boundary fixed → ESCALATED TO ARCHITECT

**Rejecting:** iter-10 (`GreenSkirtDepth 0.10→-0.02`). It cleaned the OUTER fringe→terrain
edge (lower/left, smooth now) and removed the dark skirt facets — but the INNER boundary
where the green meets the fringe, along the **top and right**, is **completely wavy /
stair-stepped**. The skirt-height fix touched the wrong boundary.

**Escalated.** 10 iterations, still broken — needs a hard architect look at *which surface*
produces the visible top-edge waviness and at what resolution it is cut. Full brief,
picture locations, iteration history, and root-cause hypotheses in
**`ARCHITECT_ESCALATION.md`** (this folder). STATUS set to `ARCHITECT_REVIEW_ESCALATE`.
Key clue: left/downhill inner edge is smooth; top/right uphill inner edge is jagged →
the waviness correlates with uphill terrain, not the (already-smoothed) contour polygon.

---

## 2026-05-29 (post iter-9 ARCHITECT_REVIEW_PASS): boundary still waves

**Rejecting:** iter-9 ARCHITECT_REVIEW_PASS (Taubin λ-μ contour smoothing).
**Why the reviewer missed it:** the canonical PASS evidence `screenshots/h07_iter9_overhead.png`
is **256×256** — too low-res to resolve the green boundary. A live in-engine grazing
close-up (Cesar, edit mode, Hole_07_Geo.unity) shows the defect the 256px render hid.

### What's still wrong (issue #1 NOT fully resolved)
1. **Waves remain where the green meets the ridge.** Reduced vs iter-8 (Taubin did lower
   the amplitude) but the green→ridge boundary is still a visibly **wavy / undulating**
   line, not a clean smooth curve.
2. **Dark triangular facets along the boundary edge.** Sharp near-black triangular notches
   run along/just below the green→ridge boundary in the grazing close-up — they read as
   **mesh skirt faces, steep cut triangles, or flipped/inverted normals**, NOT texture. A
   3D-mesh artifact that top-down overheads + 2D-contour Taubin never surface or fix.

### Likely root cause for the implementer to diagnose FIRST (don't crank params blindly)
- 2D-contour Taubin smooths the **top-down (XZ) outline** but does nothing for **vertical
  (Y) undulation** of the boundary edge along the ridge. Determine whether the remaining
  waves are in plan-view or in the height dimension before changing anything.
- The dark facets point at the **mesh-cut + skirt** at the green boundary — inspect skirt
  geometry / triangle winding / normals where the green meets terrain.

### Required for iter-10 (MANDATORY)
- **Fresh orbit VIDEO** pivoted on the green centroid, green dominating the frame the whole
  360°, slope-revealing low-to-mid angle, copied to `videos/`, frames verified. Hard
  deliverable — three iterations in a row closed on stills only and Cesar rejected each.
- **Grazing close-up still** at Cesar's rejection angle (green plateau meeting ridge, near
  eye-level) at full res as PASS evidence — NOT a 256px top-down render.

---

## 2026-05-29 (post iter-6 PASS): WRONG IMPORTER

**Rejected after:** the iter-6 `ARCHITECT_REVIEW_PASS`. Cesar identified that the entire importer-side implementation (Deliverables 3 & 4) and all six iterations of verification screenshots targeted **`HoleLiteImporter.cs` / `Hole_NN.unity`** — the **DEPRECATED** Lite path. The **shipping** map is the **Geo** importer (`HoleGeoImporter.cs`, `Import ▸ Geo ▸ … Hole NN Geo`, `Hole_NN_Geo.unity`). The fix is correct *on the wrong importer*; the Geo (shipping) green still has the flat surface, the 1.026× carve under-cover, and no fairway cutout.

**Resolution (Cesar chose "port to Geo now"):** see `SPEC.md` § Amendment 2026-05-29 (iter-7). Port D3 + D4 into `HoleGeoImporter.cs` (the geo↔Lite coordinate mapping DROPS — Geo uses direct X/Z, so `TrySampleHeight` samples directly). Revert the dead-path Lite edits + Lite `TerrainData_Hole07.asset` to HEAD. Verify on `Hole_07_Geo.unity`. D1 (bake/green.json) and D2 (GreenTopology v2) stay. Lesson saved to user memory `project_geo_importer_is_shipping`.

---

## (historical) 2026-05-29 — iter-3 visual rejection

**Rejected after:** `ARCHITECT_REVIEW_PASS` (iter-3). Cesar caught a visual defect the reviewer missed.

---

## The defect

In `screenshots/h07_in_engine_green_mesh.png`, on the **upper-right / right edge of the green, the terrain pokes up through the green surface** — part of the green is rendered *under* the surrounding terrain. The stair-stepped boundary the iter-2/iter-3 reviewer described as "blending into the collar" is actually the green mesh sitting **below** the terrain there, so the darker terrain occludes the green's edge.

## Root cause (confirmed in code, not speculation)

1. iter-2 correctly re-seated the green **flat** at the contour-centroid terrain height to kill the ~1.8 m macro-tilt double-count (SPEC Hard Rule 2). `HoleLiteImporter.CreateGreenMeshCDT` L2688: `greenSeatY = terrainBaseY + centroidTerrH + effectiveYOffset`; interior verts L2711: `rawVerts[i].y = greenSeatY + GreenRaiseMeters + relH`. This is correct.
2. **But the terrain under the green is never graded for a flat green.** `DepressTerrainUnderOverlays` (L3323) marks depression cells **only** for fairways (L3345), tees (L3359), cart paths (L3384), and water (L3397+). **There is no green depression pass.** Verified: `awk 'NR>=3323 && NR<=3660'` over the function shows zero green/greens.json marking.
3. So the terrain under the green keeps its full ~1.8 m DEM tilt. The uphill half rises ~0.9 m **above** the flat green seat → terrain pierces the green interior. The flat 0.40 m `OverlayDepressionMeters` wouldn't help even if greens were in the set (0.40 m « 0.9 m), and greens aren't in the set anyway.

### Why it looked fine before this task
The pre-task flat green **conformed** to per-vertex terrain (`rawVerts[i].y += raise`, base from `CDTTriangulate` = per-vert `terrain.SampleHeight`). It WAS the terrain surface + 8 cm, so nothing could ever poke through. De-tilting the green — the entire point of this task — exposed that the ground underneath was never graded to receive a flat green.

### Spec error
SPEC.md **line 81** is factually wrong: *"The terrain under the green is already depressed 0.40 m (`DepressTerrainUnderOverlays`)…"* — greens are not depressed at all. The spec author assumed a depression that doesn't exist, and assumed a flat depression would suffice against a tilted DEM, which it wouldn't.

---

## Agreed fix (Cesar chose "Grade a terrain pad", 2026-05-29)

Grade a **level pad** under each height-baked green — the standard real-world solution for a green cut into a hillside. This requires the importer to modify `TerrainData`, so **SPEC Hard Rule 4 is amended** (see SPEC.md § Amendment 2026-05-29) to permit importer-side terrain pad-grading under height-baked greens. This is a **sanctioned, deterministic importer output** committed as a deliverable — NOT the stray reimport drift cleaned in iter-3.

**Mechanism (implementer works out exact ramp math, validates by screenshot):**
- Add a green-pad pass to `DepressTerrainUnderOverlays` (or a sibling run right after it, before `SetHeights`). Operation order is already correct: `CreateGreenMeshes` (L200) seats the green from *original* terrain **before** `DepressTerrainUnderOverlays` (L272), so there is no circularity — sample/seat first, cut the pad second.
- For each **height-baked** green (v2 `green.json` present), flatten the terrain cells under the green footprint to a pad at **just below the green's lowest interior vertex** (`padTargetY = interiorYMin − clearance`, clearance ~0.15–0.25 m). SET to the pad height (not a relative subtract): this cuts the uphill terrain AND fills any downhill gap under the floating edge.
- **Gradual falloff:** ramp terrain from the pad height back up to natural terrain through the collar zone (reuse the cart-path distance-transform ramp already in the function, L3410+), so there is no terrain cliff at the pad edge and the collar mesh sits over smoothly-transitioning ground.
- **Collar interface (the gotcha):** the collar mesh (L2713–2723) is built against *original* per-vertex terrain. The pad falloff must reach natural terrain at/just-outside where the collar's outer edge lands (`collarWidth`), and stay ≤ the collar mesh Y throughout the collar band, so the collar covers it with no poke-through and no float.

**Constraints / invariants:**
- **Guard:** non-v2 holes get NO pad — current terrain behavior byte-for-byte unchanged (preserves Hard Rule 3).
- **Physics unaffected:** break stays grid-force (Hard Rule 5); ball rests on the **mesh** via `BakedHeightProvider` (mesh vertex Ys authoritative), so lowering terrain under the green does NOT drop the ball. Confirm this in the report.
- **Green mesh itself does not change:** interior relief must still measure ~0.42 m after the fix (the pad is terrain-only; it must not alter the green vertices).

## Acceptance for re-review
1. Reimport H07; capture the **same uphill angle Cesar flagged** + an overhead — **no terrain poke-through** on any green edge, clean collar blend, no z-fight, no perched-pedestal float.
2. Interior relief still ~0.42 m (pad did not deform the green).
3. `git status` after import: `TerrainData_Hole07.asset` IS now an intended, reported deliverable (with its `.meta`); no OTHER unsanctioned drift (materials, water.json, etc. must NOT be dirtied — if the reimport touches them, restore per iter-3).
4. Physics note in report confirming ball-on-mesh is unaffected by the pad.
