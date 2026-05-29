# Amendment 2026-05-29 (iter-8) — Green visual fidelity consolidated pass

**Authored:** 2026-05-29 11:46 CEST / 18:46 JST (Architect)
**Status:** SPEC_READY
**Kickoff:** `Use the golfin-implementer subagent on "green_slope_height_bake" (iter-8)`
**Targets:** `HoleGeoImporter.cs` (LIVE; HoleLiteImporter is deprecated, banner-headed at commit 980cc122). `Tools/GreenSlope/scripts/bake-green.mjs`.
**Replaces / merges:** the iter-7 ARCHITECT_REVIEW_PASS is void per `ARCHITECT_BRIEF_2_green_fidelity.md`. iter-5 fairway-cut + terrain-carve mechanism is kept (corrected sizing); iter-4 heightmap-pad remains reverted. No `TerrainData` heightmap edits in this iter either.

---

## Five issues, one consolidated pass

1. **Scalloped outer collar boundary** (Code's brief Issue 1) — coarse 32-pt green contour amplified by dilation.
2. **Possible Z-axis mirror** (Code's brief Issue 2) — **disconfirmed by data** for the X axis: H07's "1-bunker side" is East = high X (bunker[1] at X=198 vs bunkers[2,3,4] at X 148–158), and the baked height field PNG shows red (high) on high X. **No bake change.** Add an architect-visible cardinal sanity check in-engine after pilot. If the in-engine high side disagrees with the bake PNG, the bug is in the importer's `TrySampleHeight` sampling, not the bake.
3. **Green↔neighbor seam** (Code's brief Issue 3) — gaps/overlap where green meets fairway *or* rough. **De-risk path: skirt approach** (option (c) of Code's brief, robustly sized). Unified mesh deferred to a targeted follow-up if specific holes still leak after the skirt ships.
4. **Donut/pillow rim** (Architect-found, not in Code's brief) — `Y = greenSeatY + GreenRaiseMeters + relH` with a zero-mean field makes the lower-tier interior dip *below* the collar attachment line.
5. **Surrounding-terrain elevation sanity gate** (Cesar's add 2026-05-29) — real-world greens broadly track the surrounding land's high/low orientation. A correlation check between the authored green's high/low and the surrounding terrain's high/low catches Z-mirror, frame-swap, or sign-flip bugs at bake time, before in-engine review.

---

## Deliverable 1 — Contour resampling (fix scallop, issue 1)

In `bake-green.mjs`, before any grid build:
- **Resample** the green contour from `greens.json` to uniform arc-length spacing (`targetSpacing = 0.5 m`). Catmull–Rom or piecewise-linear with arc-length parameterization both fine; piecewise-linear is simplest and adequate for an oval.
- **Smooth**: one or two passes of Laplacian smoothing (each vertex = (prev+next)/2 weighted ~0.3 against itself). Keeps the oval, kills the angularity.
- Emit the resampled contour into the bake report so the importer can read the *same* polygon when it dilates for the collar — **single source of truth for the green polygon**, avoids the bake and the importer ever disagreeing on what the "green edge" is. Write to `Assets/Resources/HoleData/Hole_NN/green.json` as a new field `contourResampled[]` (or reuse: stamp a `contourVersion: "resampled-v1"`).

In `HoleGeoImporter.cs`, on the height-baked path:
- For greens with a v2 `green.json` that includes `contourResampled`, use that as **the** green contour for: CDT mesh build (replacing the coarse `greens.json` contour), collar dilation, terrain-hole-carve polygon, and the shared `cutContour` helper. Non-v2 greens keep the existing `greens.json` contour (byte-for-byte unchanged).

## Deliverable 2 — Skirt collar (fix seam, issue 3)

Shared geometry helper in `HoleGeoImporter.cs`:

```
collarOuterY(vertXZ) = terrain.SampleHeight(vertXZ) - skirtDepth
cutContour(green)   = DilateContour(green.contourResampled, collarWidth - cutMargin)
```

Constants (top of file, documented):
- `GreenCollarWidth = 0.9f` (was 0.6f — widened for slope robustness)
- `GreenCutMargin   = 0.25f` (unchanged; sane bounds 0.20–0.30)
- `GreenSkirtDepth  = 0.10f` (new; depth below terrain at the collar's outer edge)

Mesh-build changes (height-baked greens only; guard non-v2 greens to current behavior):
- **Collar inner ring** (at the green's outer boundary, d=0): `Y = greenSeatY + relH` (see D4 — `relH` here is the bake's boundary value; the additive `GreenRaiseMeters` is removed on this path).
- **Collar outer ring** (at d=collarWidth): `Y = terrain.SampleHeight(vertXZ) − skirtDepth`. **Per-vertex**, so the outer ring follows the surrounding slope.
- **Collar interior verts** (d between 0 and collarWidth): smoothstep blend between inner and outer ring Ys.

Cut-the-neighbor changes (same `cutContour` for all neighbor cases):
- **2a — Terrain hole-carve** (the existing widened-carve from iter-5; lives in Geo's green-creation block, equivalent of Lite's L2502–2522): polygon = `cutContour(green)`. **Always applied** when the green has a v2 `green.json` — this handles the "green on rough, no fairway" case.
- **2b — Fairway triangle-drop**: in `CreateFairwayMesh`, drop fairway triangles whose centroid lies inside any v2 green's `cutContour`. Only fires when fairway exists in that XZ region; harmless if it doesn't. Bunker cut (`BunkerFairwayCutMargin = 0.20 m`) carries over from iter-5 unchanged.

Why this handles all three border cases (recap from the chat thread):
- **Green on fairway**: fairway sits at terrain+0.02 at the cut edge, collar's outer ring at terrain−0.10. Fairway terminates *above* the collar's tail → fairway hides the skirt's outer descent.
- **Green on rough only**: terrain hole-carve removes terrain at the cut polygon; rough/terrain mesh terminates at the carve edge; collar's outer ring at terrain−0.10 sits below the terrain edge → terrain hides the skirt's outer descent.
- **Green on mixed fairway + rough**: collar outer Y is per-vertex, so each side resolves independently. Either neighbor sits above the collar locally.

## Deliverable 3 — Min-shift the height field (fix donut, issue 4)

In `bake-green.mjs`, after Poisson integration:
- Currently: subtract mean → `heightField` is zero-mean, range like [−0.225, +0.247].
- Change to: subtract **min** → `heightField` is min-shifted, range [0, +0.472]. **Always non-negative.**
- Emit `heightDatumY = 0` still (reserved); add `heightShiftMode: "min"` to the green.json DTO so the importer can verify which shift was used.

In `HoleGeoImporter.cs`, on the height-baked path:
- Interior verts: `Y = greenSeatY + relH` (remove the additive `+ GreenRaiseMeters`). The bake's min-shift already provides the perimeter baseline; the green rises from there into tiers, never dips below.
- Collar inner-ring boundary Y: `greenSeatY + relH(boundary)`, with the bake's min-shift ensuring `relH(boundary) ≥ 0`. Collar always meets the green at or above seat-Y. **No more sub-collar dip, no more donut rim.**
- Non-height-baked path is unchanged — keeps the original `+ GreenRaiseMeters` (the constant is still used for v1 greens).

## Deliverable 4 — Surrounding-terrain elevation sanity gate (Cesar's add, issue 5)

Real-world greens broadly track the high/low orientation of the surrounding land. The bake should fail loud if the authored green points the opposite way. **In `bake-green.mjs`, after building the height grid, before writing:**

1. **Sample surrounding terrain.** Take N (e.g. 24) sample points evenly spaced around a ring at `collarWidth + 2 m` outside the resampled green contour. For each, read elevation from the same terrain source the importer uses. (NOTE: bake-green.mjs runs in Node, no Unity terrain API. Options: (a) export the relevant terrain heightmap slice to a JSON sidecar at import time and have the bake read it; (b) compute this check inside the importer post-bake, before mesh build. **Implementer to choose** based on what's cheapest — flag the choice in the implementer report. Recommendation: option (b), since the importer already has terrain access and runs the bake-driven mesh build anyway.)
2. **Compute fits.** Plane-fit (least squares) the terrain ring samples → terrain macro gradient `gT`. Plane-fit the bake's interior height grid (only in-polygon cells) → authored macro gradient `gA`.
3. **Correlate.** `cos(angle) = dot(normalize(gT), normalize(gA))`.
4. **Gate (WARN, do not fail).** Thresholds:
   - `cos > +0.5` → **OK** (broadly aligned)
   - `−0.5 ≤ cos ≤ +0.5` → **WARN** (perpendicular-ish; the green doesn't follow the land — possible but uncommon)
   - `cos < −0.5` → **WARN-MIRROR** ("authored green high/low is opposite the surrounding land — likely a Z/X axis mirror in the authoring frame, or this green is genuinely contrary to the land; verify against the PDF before in-engine review")
5. **Output to `bake_report.txt`** and (option b) into the importer's `reimport_report.txt`:
   ```
   surrounding-terrain alignment: cos = +0.83  → OK
     terrain macro gradient: (-0.018, +0.024) m/m   (down to SW)
     authored macro gradient: (-0.021, +0.019) m/m  (down to SW)
   ```

**WARN, not FAIL.** Some greens are genuinely contrary to the land (built-up pad on a swale-side, etc.) — the gate is a heads-up, not a veto. Cesar reads the report; if all 18 holes WARN-MIRROR, the bake is mirrored; if only one does, that's the hole to look at.

## Deliverable 5 — Architect-verifiable cardinal sanity check (issue 2, no code change)

Add to the implementer-report template: a numbered photo from the **south, looking north** of the H07 green, taken at a low angle that exposes the East–West tilt. With East on the right of the photo, the East side should be visibly higher. If it isn't, the importer's `TrySampleHeight` is sampling with swapped/inverted axes — flag and stop, do not run the `--all` pass.

---

## Sequence

1. Pilot H07: bake → reimport → `bake_report.txt` + `reimport_report.txt` + the cardinal photo (D5). Cesar signs off on: outer edge clean (no scallop), no donut rim, no fairway/terrain seam visible from any angle, D4 elevation check report OK, D5 cardinal photo confirms East = high. Sign-off required before --all.
2. `bake-green.mjs --all` + reimport all 18. Spot-check D4 report for any WARN-MIRROR.
3. If specific holes leak at the seam under angled lighting/grazing camera, unified-mesh follow-up is a separate spec — has real cases to design against by then, not speculation.

## Hard rules (carries forward + new)

1. Arrows → continuous IDW gradient → Poisson height. Never per-arrow facets. *(unchanged)*
2. Arrows are total slope; do **not** add terrain macro-tilt to the bake's gradient/height. Terrain seats absolute + collar skirt only. *(unchanged)*
3. Importer change is additive + guarded — holes without v2 `green.json` are byte-for-byte unchanged. *(unchanged)*
4. **HoleGeoImporter ONLY.** HoleLiteImporter is deprecated (header banner at commit 980cc122). Do not touch Lite. *(reinforced)*
5. **No `TerrainData` heightmap edits.** Permitted: changing the *polygon* passed to the existing terrain-hole-carve; dropping fairway triangles inside a polygon. *(unchanged from iter-5)*
6. Break stays grid-force; `BakedHeightProvider` reads mesh vertex Ys. No putter/physics rework. *(unchanged)*
7. `green.json` base64 layout unchanged (slope grid float32 ×3 row-major, height grid float32 ×1 row-major).
8. **One shared helper** for `cutContour`. Terrain hole-carve and fairway triangle-drop MUST use the same helper. They can never drift apart. *(carried from iter-5)*
9. The bake produces a **min-shifted** height field; the importer's height-baked path uses `Y = greenSeatY + relH` (no `+ GreenRaiseMeters`). The constant `GreenRaiseMeters` is preserved for the v1 / non-height-baked path. *(new)*

## Definition of done

- Pilot H07: `bake-green.mjs --hole 7` writes v2 `green.json` with `contourResampled` and the min-shifted height field; `bake_report.txt` shows the D4 elevation alignment report passing (cos > +0.5 expected, or a documented WARN with justification).
- Reimport H07:
  - Outer collar edge reads as a clean oval, not scalloped (D1).
  - No visible "donut/pillow" rim from any angle; lower tier sits at or above collar attachment (D3).
  - No green↔fairway gap or overlap from any angle, including the bottom-left grazing shot Cesar used in iter-7 review (D2).
  - No green↔rough gap on the side(s) of the green that aren't fairway-bordered (D2 case 2; implementer identifies which holes hit this — H07 might or might not, but at least one of the 18 should be a green-on-rough test).
  - D5 cardinal photo: East side visibly higher.
- Cesar in-engine sign-off on H07 against real photo + PDF + ShotNavi heatmap.
- `bake-green.mjs --all` writes 18; spot-check D4 reports for WARN-MIRROR (treat as flag for re-bake or PDF re-check).
- `reimport_report.txt` per the iter-5 DoD additions: zero `true` terrain-hole cells inside `cutContour`; zero fairway triangles centered inside `cutContour`; iter-4 reverts confirmed (already done — Cesar's earlier note).

## Open items the implementer should report back on

1. Which holes have **green on rough only** (no fairway overlap)? Needed to verify the terrain-carve case in isolation.
2. **D4 implementation choice**: bake-side (terrain sidecar) or importer-side (post-bake, pre-mesh-build)? Recommendation: importer-side. Confirm which was implemented.
3. **Collar width = 0.9 m** — does that visually overpower the green on the smallest hole in the set? If yes, propose a per-hole scaling rule.
4. If the cardinal D5 photo shows East = low in-engine (i.e. mesh-deform mirror), flag and stop — the bug is in `TrySampleHeight` axis mapping, not the bake; needs an architect look before further iteration.
