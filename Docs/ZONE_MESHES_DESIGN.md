# Zone Meshes — Design Doc

> All zone types on flat terrain first. Heightmap later.
> All surfaces need SurfaceType for gameplay.
> Start simple. Controls for height/depth/slope/direction added in Unity later.

## Architecture

Reuse the bunker V2 pipeline for all zones:
1. **Export** (`export-hole.mjs`): Extract contour polygons per zone type
2. **Unity** (`HoleLiteImporter.cs`): Generate replacement meshes, cut terrain

**Separate files per zone type** (not merged):
- `bunkers.json` — already working ✅
- `greens.json` — next
- `water.json` — after greens
- `tees.json` — after water
- Cart paths — last, deferred for now (splatmap ok, but will eventually
  need meshes since they can be sunk or raised depending on hole)

Rationale: each zone iterates independently, no migration, already have
bunkers.json working.

## Shared Infrastructure

### Export side

Generalize `extractBunkers()` → reusable `extractZoneContours(zonesData,
terrainMeta, zoneId)`. Same pipeline: flood fill → border trace → RDP
(closed polygon) → Chaikin smooth → CCW winding.

Called once per zone type, writes separate JSON files.

### Unity side

`CreateZoneMeshes()` reads each zone file and dispatches to zone-specific
mesh builders. Each zone type gets a `SurfaceType` component on its
collider for gameplay detection.

### Splatmap

All mesh-handled zones map to rough in `ZoneToLayer()`:
- Zone 2 (green) → rough ← NEW (but keep fringe ring logic!)
- Zone 6 (bunker) → rough ← already done
- Zone 7 (water) → rough ← NEW
- Zone 10 (tee) → rough ← NEW
- Zone 8 (cart path) → rough ← later, when we add cart path meshes

### SurfaceType Component

```csharp
public enum SurfaceType { Fairway, Green, SemiRough, Rough, Bunker, Water, Tee, CartPath }

public class SurfaceMarker : MonoBehaviour
{
    public SurfaceType surfaceType;
}
```

Added to every zone mesh's GameObject. Ball physics reads this from
the collider hit to determine penalties, club restrictions, etc.

---

## Zone-by-Zone

### 1. Greens (Priority 1)

**Export:** `greens.json` — contour polygons for zone 2
**Mesh:** Slightly raised. Simple first approximation:
- Rim ring at terrain height
- Flat top at +0.15m (configurable in Unity later)
- 2-3 rings: rim → transition → flat top

**Material:** `T_Green_Albedo`, tight tiling (~3m)
**Collider:** MeshCollider + `SurfaceMarker(Green)`
**Splatmap:** Zone 2 → rough (mesh handles surface). Fringe ring still
generated from green zone mask (fringe logic runs before ZoneToLayer).

**Future controls (not now):**
- Per-green height adjustment in Inspector
- Slope direction + angle (drainage)
- Sub-areas for tiers / breaks (granular undulation)
- Pin position markers

### 2. Water (Priority 2)

**Export:** `water.json` — contour polygons for zone 7
**Mesh:** Depressed with flat bottom:
- Rim ring at terrain height (bank edge)
- Bank slope ring at -0.2m, scaled 90%
- Flat bottom at -0.5m (configurable later)
- NOT a bowl — flat bottom, unlike bunkers

**Material:** V1 = simple semi-transparent blue material (URP Lit with
alpha). V2 = proper water shader later.
**Collider:** MeshCollider + `SurfaceMarker(Water)` — triggers penalty
**Splatmap:** Zone 7 → rough

**Future controls (not now):**
- Water depth per hazard
- Flow direction (for rivers)
- Shore slope angle
- Separate water surface plane (transparency) vs riverbed mesh

### 3. Tee Areas (Priority 3)

**Export:** `tees.json` — contour polygons for zone 10
**Mesh:** Flat raised platform:
- Rim at terrain height
- Top at +0.10m (configurable later)
- Simple 2-ring mesh: rim → flat top

**Material:** `T_Tee_Albedo`, tight tiling
**Collider:** MeshCollider + `SurfaceMarker(Tee)` — enables driver
**Splatmap:** Zone 10 → rough

**Future controls (not now):**
- Per-tee height in Inspector
- Tee marker placement

### 4. Cart Paths (Priority 4 — Deferred)

Splatmap-only for now. Will need meshes eventually since they can be
sunk or raised. When we do them:
- Flat polygon triangulation (ear clipping, not ring-based — too thin)
- Asphalt material
- `SurfaceMarker(CartPath)` — free relief rules
- Height offset configurable per path segment

---

## Implementation Plan

### Phase 1: Greens
1. Export: Add `extractZoneContours()` generalized function, export `greens.json`
2. Unity: Add `SurfaceMarker` component, green mesh builder (raised flat),
   green material, update `ZoneToLayer` for zone 2
3. Verify: Green mesh visible, raised, correct shape, splatmap fringe intact

### Phase 2: Water
1. Export: Export `water.json`
2. Unity: Water mesh builder (depressed flat bottom), water material,
   `SurfaceMarker(Water)`
3. Verify: Water visible, depressed, semi-transparent

### Phase 3: Tees
1. Export: Export `tees.json`
2. Unity: Tee mesh builder (raised flat), tee material, `SurfaceMarker(Tee)`
3. Verify: Tee platforms visible at correct locations

### Phase 4: Cart Paths (later)
- Different mesh strategy (polygon triangulation, not ring-based)
- Deferred until after heightmap

### Phase 5: Heightmap
- Re-enable heightmap in CreateTerrain
- Verify all zone meshes work with real elevation
- Rim vertices follow terrain slope via SampleHeight

### Phase 6: Inspector Controls
- Custom Editor for each zone mesh: height, depth, slope, direction
- Green sub-area editing
- Per-hazard water depth
- Save overrides to scene (not re-exported from pipeline)
