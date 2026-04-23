# Lessons: Physics Surface Markers + Heightmap-vs-Mesh Coupling

> Filed 2026-04-22 after reviewing Code's `SURFACE_MARKER_FIX_REPORT.md`.
> Captures what the surface-marker fix does, what it doesn't address,
> and the deeper heightmap/mesh-Y mismatch that surfaced during the review.

---

## TL;DR

1. **Surface marker fix (Code's commit `12830151`) is correct.** The asmdef `autoReferenced: true` flip + add-only `Golfin.Physics.Runtime.SurfaceMarker` lines in both importers solve the Phase 4 wiring gap. Accept it.
2. **Skip Code's proposed tree-layer fix.** Not needed. `TreePlacer` doesn't add colliders; Unity terrain trees don't intercept `Physics.Raycast`. There's no bug to solve.
3. **Re-import all 18 holes** — existing generated scenes still carry only the old `Golfin.Course.SurfaceMarker`. Until re-imported, runtime physics still misclassifies on every hole.
4. **The real outstanding bug: heightmap doesn't include zone-mesh tops.** Greens sit ~11cm above the depressed terrain (`+0.03 + GreenRaiseMeters 0.08`). The ball lands on the heightmap Y, not the mesh Y. Putts will roll on a surface ~11cm below the visible green. This is separate from surface classification; it's a Phase 0 baker concern.
5. **Bunker lip submesh-aware classification** is a polish item, deferred. `BunkerSurfaceInfo` has the breadcrumb (`SubmeshSand=0, SubmeshLip=1`) but `SceneSurfaceProvider` is submesh-blind. Whole bunker mesh currently classifies as `Sand`. Fine for now.

---

## What was actually broken

I wrote two `SurfaceMarker` types in different assemblies during Phase 4:

- `Golfin.Course.SurfaceMarker` (in `Assembly-CSharp`) — the importers attach this to every zone mesh
- `Golfin.Physics.Runtime.SurfaceMarker` (in `Golfin.Physics.Runtime`) — `SceneSurfaceProvider` reads this

I never bridged them. The Phase 4 spec said "scan hole-1 and report which zone meshes lack a `SurfaceMarker`" — meaning I expected Cesar to add Physics-side markers by hand later. The lab spec then deferred that as "audit and report only, do not auto-add." Code just *did* the wiring instead, which was the right call.

Result before fix: `SceneSurfaceProvider.Classify` called `GetComponentInParent<SurfaceMarker>()` (Physics namespace) on the hit collider, found nothing, defaulted to `Fairway` for everything. Greens, bunkers, water, tee, cart path — all classified as fairway. Wrong bounce, wrong roll, wrong putt coefficients on every overlay surface.

## Why Code's fix is correct

**Asmdef flip:** Both `Golfin.Physics.Core` and `Golfin.Physics.Runtime` now have `autoReferenced: true`. This makes them visible to `Assembly-CSharp-Editor` (importers) and `Assembly-CSharp` (gameplay). Adds maybe 1MB to compile time. Phase 4 invariant `noEngineReferences: true` on Core is preserved (verified). Cleanest available fix; the alternative was a new asmdef shim and that's pure overhead.

**Importer changes:** Add-only. Each existing `Golfin.Course.SurfaceMarker` attachment is followed by a one-liner adding the Physics-side marker with the matching `SurfaceType`. Both importers (Geo and Lite) updated. Mapping:

| Mesh / Zone | Physics SurfaceType |
|---|---|
| Bunker contour | `Sand` |
| Green CDT | `Green` |
| Green collar (raised mesh collar GO) | `GreenCollar` |
| Green surface (raised mesh inner GO, was unmarked) | `Green` |
| Water | `Water` |
| Fairway | `Fairway` |
| Tee (all 3 variants) | `Tee` |
| CartPath spine / junction / spline | `CartPath` |

39/39 physics tests pass after the change. Tests don't exercise scene classification (they use `ConstantSurfaceProvider`), so a green light only proves "didn't break Phase 1–5." That's enough — the diff is mechanical add-only.

## Why the proposed tree fix is unnecessary

Code's report proposes adding a "Trees" layer and excluding it from `SceneSurfaceProvider`'s raycast. Audit:

- `TreePlacer.cs` doesn't reference `Collider` anywhere (verified by content search).
- Standalone tree prefabs are instantiated as-is; no colliders added by the placer.
- Unity terrain trees don't intercept `Physics.Raycast` at all (terrain tree colliders are separate, not enabled here).
- Therefore the downward raycast already passes through trees and hits the ground beneath. Trees correctly classify as whatever the terrain mesh is underneath them (rough/fairway/etc).

Don't implement Code's tree fix. If we ever decide trees should classify as `Rough` or a new `Trees` surface type for gameplay reasons, that's a future feature, not a bug fix.

## Re-import is required

All 18 generated hole scenes (`Generated/Hole_XX_Geo.unity`, `Hole_XX_Geo_Flat.unity`) carry only the old `Golfin.Course.SurfaceMarker` because they were imported before this fix. Until each is regenerated, runtime physics still misclassifies on every hole.

Mechanical: bulk import via `Import > Geo > Normal > Import All Holes Geo` (and Lite if used). Significant compile/import churn; budget time for it.

---

## The deeper bug: heightmap doesn't include zone-mesh tops

This is **separate from surface classification** and was not addressed by Code's fix.

### What's happening

Phase 0's `PhysicsHeightmapBaker` reads `terrain.GetHeights(...)` *after* `DepressTerrainUnderOverlays()` has carved depressions for the green/fairway/tee/cart path/water meshes. So the heightmap has the depression baked in. Good — that's intentional, prevents z-fighting and lets meshes sit on a flat-enough base.

Then the meshes get placed with their own Y offsets:

- **Greens:** `terrain Y + 0.03 + GreenRaiseMeters (0.08) ≈ +11cm` above the depressed terrain
- **Tees:** flat platform at peak height of the original tee polygon (varies)
- **Fairway / cart path:** ~+1–5cm above terrain (small; mostly noise)
- **Bunkers:** mesh inner is *below* terrain by `bowlDepth` (this is fine — ball lands in sand)
- **Water:** flat mesh above the depressed bed (this is also fine — ball terminates on water hit)

### Where it breaks

`HeightmapData.SampleHeight(x, z)` returns the depressed terrain Y. The ball lands and rolls at that Y. But the visible green mesh sits at terrain Y + 11cm. So:

- **Putts will roll on a surface visually ~11cm below the green.** From camera height this will look like the ball is rolling slightly inside the green.
- **Bounce-and-roll on greens:** ball lands at heightmap Y (depressed terrain), not at mesh-top Y (green surface). Same offset error.
- **Tees:** even bigger discrepancy possible if the tee platform sits high on a slope.

`SceneSurfaceProvider.Classify` correctly reports `Green` because the raycast hits the mesh collider above. So coefficients are right. The ball is just at the wrong Y.

### Severity

Measurable, not catastrophic. 11cm is below "obvious wrong" but above "you can't tell." On putts especially — where the ball is at rest on the surface and you're staring at it from a low camera angle — the offset will be visible.

### Two fix options

**Option A: Re-bake heightmap after meshes are placed.**
Add a second pass to Phase 0's baker (or after `DepressTerrainUnderOverlays`) that does a downward raycast at every grid cell, picks the topmost mesh-or-terrain hit, writes that Y. Heightmap stays the single source of truth for the deterministic sim. Slowest (~4M raycasts at 2049² resolution; can subsample, e.g. 1025² and bilinear).

**Option B: Runtime overlay-aware height lookup.**
At ball-position queries, first check for an overlay mesh via raycast; fall back to heightmap if none. Cheaper to implement but breaks the determinism story — Unity raycast results aren't bit-identical across platforms. **Don't do this for the sim.** Could be acceptable for visual-only ball positioning if someone really wants it.

**Recommended: Option A.** Phase 6 work or a Phase 0.1 re-bake addendum. Not blocking Phase 5; putts will work, they'll just look slightly off.

---

## Bunker lip submesh classification (deferred)

Bunkers have two zones in `surfaces.csv`:
- `Sand` (Restitution 0.15, RollingResistance 0.70) — bowl interior
- `BunkerLip` (Restitution 0.20, RollingResistance 0.55) — outer rim, redirects ball downward

`BunkerSurfaceInfo` breadcrumb already exists with `SubmeshSand=0, SubmeshLip=1`. But:

- `SceneSurfaceProvider.Classify` only reads `SurfaceMarker.Type` — submesh-blind.
- The added `Golfin.Physics.Runtime.SurfaceMarker` is set to `Sand` for the whole bunker mesh.
- Result: lip currently classifies as `Sand`.

To wire correctly: `SceneSurfaceProvider` would need to do `RaycastHit.triangleIndex` → submesh lookup → check `BunkerSurfaceInfo` to pick `Sand` vs `BunkerLip`. Same pattern would apply to `GreenSurfaceInfo` for green-vs-collar (though green collar is already correctly handled because it's a separate GameObject with its own collider, not a submesh — a quirk of the green pipeline).

**Defer.** Bunker lip is a polish item. "All sand" is acceptable for now and doesn't block Phase 5–6.

---

## Recommended actions (priority order)

1. ✅ **Done:** Accept Code's asmdef + importer fix (commit `12830151`).
2. ⬜ **Re-import all 18 holes** so generated scenes pick up the new markers. Manual menu-driven, ~30 min budget.
3. ⬜ **Skip Code's tree fix** (no bug).
4. ⬜ **Open follow-up: heightmap-vs-mesh-Y mismatch on greens/tees.** Phase 0.1 re-bake addendum or Phase 6 task. Document here, flag in `TellCode.md`.
5. ⬜ **Defer bunker lip submesh classification.** Polish pass after Phase 5 putt validation.

---

## Files referenced

- `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs` — importer with new markers (10 added lines)
- `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs` — importer with new markers (8 added lines)
- `Assets/Scripts/Physics/Runtime/SceneSurfaceProvider.cs` — classifier; submesh-blind today
- `Assets/Scripts/Physics/Core/Golfin.Physics.Core.asmdef` — `autoReferenced: true`
- `Assets/Scripts/Physics/Runtime/Golfin.Physics.Runtime.asmdef` — `autoReferenced: true`
- `Assets/Scripts/Editor/CourseImporter/PhysicsHeightmapBaker.cs` — Phase 0 baker, would need extension for Option A
- `Assets/Resources/Physics/surfaces.csv` — surface coefficients incl. `BunkerLip` distinct from `Sand`
- `Docs/PHYSICS_RESEARCH.md` Section 3 — surface interaction architecture
- `Docs/LESSONS_PHYSICS_AERO.md` — companion lessons file (different concern)
