# Architect brief 2 — green visual fidelity (iter-7 in-engine on Hole_07_Geo)

**Date:** 2026-05-29
**For:** Architect (Cesar's claude.ai chat). Cesar reviewed the iter-7 Geo result in-engine (compared to the real Hole 7 drone photo) and found 3 issues. The iter-7 `ARCHITECT_REVIEW_PASS` is void; STATUS → CESAR_REJECTED.
**Reference:** real green photo (Hole No.7, Par 4) vs `screenshots/h07_geo_*` + `videos/h07_geo_orbit.mp4`. Read `Docs/Pipeline/LESSONS_FRINGE_BORDER_MESHES.md` — directly relevant (and it already documented the Lite-vs-Geo importer trap we hit).

## Issue 1 — Wavy green/collar outer border (should be uniform)

The green's outer lip (where collar meets fairway/rough) is scalloped/faceted, not a smooth oval like the real green.
- The green CDT already passes the original contour as an internal constraint (per the lessons doc's anti-jaggies fix), so the green↔collar *internal* boundary is fine.
- The waviness is the **outer dilated contour**: `DilateContour(green.contour, collarWidth)`. The `greens.json` green contour is a coarse polygon (~28–32 pts); dilating a coarse/uneven polygon amplifies unevenness, and offset corners can self-near.
- **Likely fix:** resample the green contour to uniform arc-length density (and/or smooth) before dilation, so the collar outer ring is a clean oval. Bounded change, but it lives in the same edge region as Issue 3 — should be designed together.

## Issue 2 — Height orientation is mirrored vs reality (high side should match the high field side)

The rendered green's high side is on the wrong side relative to the surrounding terrain/real green.
- **NOT the flat-seat design.** If the authored field were correctly oriented, the green's high side would match reality even when seated flat (the arrows encode the real green's actual high/low). "Switched" ⇒ the field is applied **mirrored**.
- **Most likely root cause:** a Z-axis convention mismatch in the bake. `bake-green.mjs` builds the height grid from the `greens.json` contour (SPEC Input #2: "already Unity-Z-flipped") but interpolates the authoring **arrows** (`hole_NN_slope_authoring.json`, baseXZ/tipXZ "world meters"). If the arrows are NOT in the same Z-flipped frame as the contour, the gradient field is mirrored in Z → height high/low swap. (The bake's Poisson sign itself checks out: g = downhill = −∇h, source = −div g ⇒ h high uphill — correct *within its grid frame*.)
- This was likely **masked on the Lite path** (90° CCW rotation) and **exposed by Geo's direct X/Z sampling**.
- **Needs the architect** to confirm the authoring-tool's arrow coordinate frame vs `greens.json`, then a one-spot fix in `bake-green.mjs` (align arrow Z to grid Z). Verify by Cesar's eye against the real green + ShotNavi heatmap. Affects all 18 holes (re-bake).

## Issue 3 — Holes / overlap where green meets fairway (the hard one)

Visible gaps (terrain-hole void) and overlap/Z-fight at the green↔fairway seam.
- This is the exact problem `LESSONS_FRINGE_BORDER_MESHES.md` is about: **"Any two-mesh approach with independent terrain sampling will fail on slopes… meshes that must sit flush must SHARE vertices — same CDT mesh, different submeshes."**
- The green+collar is one mesh; the **fairway is a separate mesh**. iter-7 cuts fairway triangles under the green (4b) and carves the terrain hole, but the cut fairway edge + terrain-hole void are independent geometry from the collar, so on the slope they don't sit flush → gaps + overlap.
- **Bunkers/tees don't hit this the same way:** bunkers are *sunken* (bowl drops below the cut edge — no flush seam needed); tee borders are *merged into the tee's own CDT* as a submesh. The green sits ~at fairway level, so its seam with the fairway is the flush case = hard.
- **Approach is a design decision** (architect): options include (a) widen/lower the collar so it reliably overhangs and hides the cut edge + hole void (cheap, may not fully fix on steep slope per the doc); (b) build the green collar to *share the fairway's boundary vertices* at the seam (robust per the doc, but couples two importer subsystems); (c) drop the fairway cut and instead skirt the collar down to terrain like the green's own collar already does, ensuring the collar fully covers the carved hole edge. Pick one deliberately rather than iterating.

## Recommendation

Consolidated architect spec pass for green visual fidelity, addressing all three together (1 & 3 are the same edge; 2 is a convention fix that needs the authoring-frame answer). Implementation is ready to go once: (a) the arrow Z-convention for #2 is confirmed, (b) the #3 seam approach is chosen, (c) #1 contour-resampling is greenlit. All work targets `HoleGeoImporter.cs` (shipping) + `bake-green.mjs`; D1/D2 stay.
