# Surface Classification Pipeline — Reference

**Written:** 2026-07-29 (Architect)
**Scope:** how a world (X, Z) point becomes a `SurfaceType` at runtime, and every stage that feeds it.
**Status of contents:** every claim marked ✅ was re-derived from the primary artifact on 2026-07-29 (file, line, or decoded binary). Claims marked ⚠️ are inferred and labelled as such. Nothing here is restated from a prior document without re-checking.

> **Why this doc exists.** This pipeline spans a Node-side raster exporter, a Unity importer, a terrain splatmap, two parallel marker component systems, a polygon baker, and a runtime classifier — and it carries **three mutually incompatible integer numbering schemes**. Two separate tasks have already been lost to picking the wrong data source. Read §2 and §7 before touching any of it.

---

## 1. The mental model in one paragraph

A hole's surfaces come from **two independent runtime data structures**, consulted in a fixed order:

1. **Polygon zones** — the *discrete, mesh-shaped* features (green, tee, bunker, cart path, water, fairway). Authored as actual meshes in the `Hole_NN_Geo` scene, baked to polygons in `zones.json`.
2. **The OB mask** — a bit-packed raster covering exactly the terrain footprint, marking out-of-bounds.

Anything matching neither gets a hard-coded **default**. That default is `Fairway`.

Rough — the single largest authored surface on every hole — is **in neither structure**. It has no mesh and no mask, so it can never be returned. That is not a bug in the classifier; it is a gap in what the bake produces. See §6.

---

## 2. ⚠️ THE LANDMINE: three numbering systems

Three different integer enumerations describe "surface", and **they do not agree**. Passing an integer from one into a function expecting another produces a silently wrong, plausible-looking answer.

| # | Source raster `zone_index` | `Golfin.Course.SurfaceType` | `Golfin.Physics.SurfaceType` |
|---|---|---|---|
| 0 | background | Fairway | Fairway |
| 1 | fairway | Green | Green |
| 2 | green | SemiRough | GreenCollar |
| 3 | semi_rough | Rough | Semirough |
| 4 | rough | Bunker | Rough |
| 5 | trees | Water | Tee |
| 6 | bunker | Tee | Sand |
| 7 | water | CartPath | BunkerLip |
| 8 | cart_path | Fringe | CartPath |
| 9 | ob | — | Water |
| 10 | tee_box | — | OOB |

✅ Verified: raster `zone_index` from `Tools/UHoleGeo/output/lomond-country-club/export/hole-06/zones.json`; Course enum at `Assets/Scripts/Course/SurfaceMarker.cs:5-16`; Physics enum at `Assets/Scripts/Physics/Core/SurfaceType.cs:3-16`.

**`SurfaceMarkerMap.MapCourseToPhysics` (`Assets/Scripts/Editor/SurfaceMarkerMap.cs`) converts column 2 → column 3, and is correct for all 9 Course values** (incl. `Bunker`→`Sand`, `Fringe`→`GreenCollar`). ✅

**The trap:** it takes a bare `int` and its `default:` branch only warns for values outside 0–8. Feed it a *raster* zone index and it returns confident garbage — raster `8` is `cart_path`, but the function reads `8` as `Fringe` and returns `GreenCollar`. Never route raster indices through it.

✅ **Status: latent, not live.** Both call sites pass a genuine `Course.SurfaceType` — `HoleGeoImporter.cs:4837` and `HoleLiteImporter.cs:4110`, each `(int)surfaceType` where `surfaceType` is the value just assigned to `marker.surfaceType`. **No current caller is wrong.** The hazard is that the signature accepts any `int` and fails silently in-range, so a future caller can introduce the bug without a compile error or a warning.

---

## 3. The pipeline, stage by stage

### 3.0 The authoring root: `zones.png` is hand-painted ✅

**The pipeline does not begin with the raster — it begins with a painted image.** Per hole, at `Tools/UHoleGeo/output/<slug>/holes/NN/`:

| File | Role |
|---|---|
| `satellite.png` | GSI aerial imagery — the backdrop being traced |
| **`zones.png`** | **the hand-painted zone map — one flat RGB colour per surface class** |
| `hole-bounds.json` | lat/lon extent |
| `terrain-meta.json`, `heightmap.raw` | elevation |
| `tees.json` | tee positions |

`zones.png` is rasterised into the `grid` / `zone_stats` / `ob_mask` fields of the export. **The palette (✅ decoded from the PNGs directly, not from config):**

| RGB | class | index |
|---|---|---:|
| `(0, 0, 0)` | background | 0 |
| `(0, 204, 0)` | fairway | 1 |
| `(128, 255, 64)` | green | 2 |
| `(102, 136, 51)` | semi_rough | 3 |
| `(51, 102, 34)` | rough | 4 |
| `(26, 51, 16)` | trees | 5 |
| `(221, 204, 136)` | bunker | 6 |
| `(51, 102, 204)` | water | 7 |
| `(153, 153, 153)` | cart_path | 8 |
| **`(255, 51, 51)`** | **ob** | **9** |
| `(255, 255, 255)` | tee_box | 10 |

**Consequence:** every zone classification in this game is ultimately a human painting decision. A missed colour is a silent gameplay defect with no automated check anywhere upstream of the §4.2 gate — and the gate only covers the six polygon-backed types, so a missing **ob** paint passes it cleanly. This is exactly what happened on Hole 02 (§6.1).

**Some surfaces have a second, independent source.** `water.json`, `bunkers.json`, `greens.json`, `cart-paths.json`, `tree-zones.json`, `fairway-contours.json` carry vector geometry alongside the painted raster. Where the two disagree, **the vector source wins for mesh generation** — see §5.4.

### 3.0.1 End-to-end overview

```
 ┌─ AUTHORING (outside Unity) ──────────────────────────────────────────┐
 │  Tools/UHoleGeo/output/<slug>/export/hole-NN/zones.json              │
 │    • source_dimensions   per-hole, non-square (e.g. 2048×901)        │
 │    • zone_index          0–10  (§2 col 1)                            │
 │    • grid                base64 uint8, 1 byte/cell  ← THE ORACLE     │
 │    • terrain_grid        base64 uint8  ← LOSSY, DO NOT USE (§7.1)    │
 │    • ob_mask             separate mask                               │
 └──────────────────────────────┬───────────────────────────────────────┘
                                │  HoleGeoImporter  (live importer)
        ┌───────────────────────┴────────────────────────┐
        ▼                                                ▼
 ┌─ TERRAIN SPLATMAP ─────────────┐        ┌─ ZONE MESHES (scene) ──────────┐
 │ 9 layers, alphamap 1024×1024   │        │ GameObjects w/ MeshFilter +    │
 │                                │        │ BOTH SurfaceMarker components  │
 │ Pass 1: ZoneToLayer()          │        │  • Golfin.Course.SurfaceMarker │
 │   EVERYTHING → layer 3 (rough) │        │  • Golfin.Physics…SurfaceMarker│
 │   except semi_rough → layer 2  │        │ Fairway / Green / Tee / Sand / │
 │   (§7.2 — this is why the      │        │ CartPath / Water only          │
 │    alphamap is a dead oracle)  │        └───────────────┬────────────────┘
 │                                │                        │ BakeZoneJsonTool
 │ Pass 2: OB overlay             │                        │
 │   if (ob_mask set && layer==3) │                        │
 │      layer = 8   ← OB          │                        │
 └───────────────┬────────────────┘                        │
                 │ BakeZoneJsonTool.BakeObMask             │
                 ▼                                         ▼
 ┌─ RUNTIME: Assets/Resources/HoleData/<slug>/Hole_NN/zones.json ───────┐
 │   obMask  { width, height, worldOriginX/Z, worldSizeX/Z, maskBase64 }│
 │   zones[] { type, yOffsetFromTerrain, polygons[], mesh }             │
 └──────────────────────────────┬───────────────────────────────────────┘
                                ▼  BakedZoneClassifier  →  BallSimulation
```

### 3.1 Authoring → splatmap: `ZoneToLayer` (`HoleGeoImporter.cs:1614-1630`) ✅

```csharp
1  => 3,  // fairway   → rough
2  => 3,  // green     → rough
3  => 2,  // semi_rough
4  => 3,  // rough
5  => 3,  // trees     → rough
6  => 3,  // bunker    → rough
7  => 3,  // water     → rough
8  => 3,  // cart_path → rough
9  => 3,  // ob        → rough
10 => 3,  // tee_box   → rough
_  => 3,
```

Everything collapses to layer 3 except `semi_rough`. This is **intentional** — the comments read "mesh overlay handles surface". The splatmap is a *texture* concern; surface identity lives in the meshes.

**Consequence:** the terrain alphamap cannot answer "what was authored here?" for anything except semi_rough. Sourcing authored intent from it guarantees `fairway = 0.00%` as a collapse artifact. This is exactly what invalidated `surface_coverage_audit`.

### 3.2 The OB layer is painted by a *second* pass ✅

`ZoneToLayer` sends `ob` to layer 3, so layer 8 is not populated by it. Layer 8 is written separately at `HoleGeoImporter.cs:1353-1362`:

```csharp
if (obMask != null && layer == 3)      // only over rough
    ...
    if (obIdx < obMask.Length && obMask[obIdx] != 0)
        layer = 8;                      // OB layer
```

Sourced from the raster's **`ob_mask`** field, not from the zone grid. `layerCount = 9` at `:1335`.

**Edge case:** the promotion is gated on `layer == 3`. An OB cell whose base zone was `semi_rough` (layer 2) never becomes layer 8. Negligible in practice — semi_rough is ≤1,564 px anywhere (§5.3) — but it is a real hole in the mask.

### 3.3 Splatmap → runtime obMask: `BakeZoneJsonTool.BakeObMask` (`:478-540`) ✅

- Finds the OB layer by **name** — `layers[i].name.Contains("OB")` — *not* by hard-coded index 8. More robust than the FINDINGS implied.
- Packs `maps[z, x, obLayer] > 0.5f` into a bitfield, bit index `z * alphamapWidth + x`.
- World rect taken from the terrain itself: `worldOriginX/Z = terrain.transform.position`, `worldSizeX/Z = terrainData.size`.

**So the runtime obMask world rect is exactly the terrain footprint.** That is what makes the raster→world mapping derivable (see §7.4).

### 3.4 Meshes → polygons

`CollectPolygons` (`BakeZoneJsonTool.cs:~200`) walks the scene for GameObjects carrying **both** a `Golfin.Physics.Runtime.SurfaceMarker` **and** a `MeshFilter`, extracts each mesh's boundary contour (edges used by exactly one triangle, chained into loops), projects to XZ, and groups by `marker.Type`.

Only the **Physics** marker is read by the bake (`:182-186`). The Course marker is ignored here — but is *not* dead; see §4.

### 3.5 §4.2 Completeness gate (`BakeZoneJsonTool.cs:329-440`) ✅

Added by `zone_bake_completeness`. Before writing, compares source-raster `zone_stats` pixel counts against the surface types that survived into the baked output. Any mapped type with **≥ 1,000 px** in the raster that is absent from the output → `LogError`, **file not written**.

Mapped: `fairway→Fairway, green→Green, tee_box→Tee, bunker→Sand, cart_path→CartPath, water→Water`.
Excluded by design: `rough, semi_rough, trees, ob, background` — these have no polygon zones.

**Two properties worth knowing:**
- **Skips, does not fail, when `Tools/UHoleGeo/` is absent** (logs a named warning). The bake now has an external directory dependency; CI without that tree silently loses the gate.
- ⚠️ **The gate is one-directional.** It checks raster → baked. A surface present in the *scene* but absent from the raster is invisible to it. See §5.4.

---

## 4. ✅ CORRECTION: `Golfin.Course.SurfaceMarker` is NOT vestigial

Prior notes recorded it as "appears vestigial — confirm before deleting". **Settled: it has live runtime consumers.** Because the Physics assembly cannot reference `Assembly-CSharp`, both reach it by reflection:

| Consumer | Line | Purpose |
|---|---|---|
| `PhysicsLabController.cs` | `:1542` | tee detection |
| `PlacementSnapHelper.cs` | `:29` | ball placement snapping |

Both degrade quietly if the type is missing (`PhysicsLabController:1545` logs a warning and skips tee detection). **Deleting `Course.SurfaceMarker` would silently break tee detection and placement snapping, not throw.** Do not remove it.

Both marker components are stamped on every zone mesh by design — 27 stamp sites in `HoleGeoImporter` alone. The duplication is real but load-bearing: Course-side for scene/editor consumers, Physics-side for the bake.

---

## 5. Runtime resolution — `BakedZoneClassifier`

### 5.1 The ladder (`ClassifyCore`, `:185-200`) ✅

```
1. Polygon zones   → first match wins, scanned in priority order
2. OB mask         → if (hasObMask && IsObAt) return OOB
3. Default         → return DefaultSurface   (= Fairway)
```

Polygons **always trump** the OB mask — a fairway overlapping an OB-marked cell is fairway. Deliberate, and commented as such.

`Classify` and the editor-only `ClassifyWithProvenance` both delegate to `ClassifyCore`, so provenance reporting is **bit-identical by construction** rather than by assertion.

### 5.2 Priority (`:308-325`) ✅

`Green 100 > Sand 90 > BunkerLip 89 > Water 80 > GreenCollar 70 > Tee 60 > CartPath 50 > Fairway 40 > Semirough 20 > Rough 10 > OOB 5`

⚠️ **Doc-vs-code contradiction, still present.** The class summary at `:20` says the chain ends `… > Fairway > Rough (default)`. The code says `public const SurfaceType DefaultSurface = SurfaceType.Fairway;` (`:73`). The *doc* describes the arguably-correct behaviour; the *code* is what runs. Do not "fix" the comment without deciding which is intended — that decision belongs to `surface_classification_ob_rough`.

### 5.3 What is actually in the baked data — all 18 holes ✅

Decoded from `Assets/Resources/HoleData/lomond-country-club/Hole_NN/zones.json`, 2026-07-29 (post-re-bake):

| Zone type | Holes present | Notes |
|---|---|---|
| Fairway | **18 / 18** | restored on H14, H15 by the re-bake |
| Green | **18 / 18** | restored on H02, H12, H14 |
| Tee | 18 / 18 | |
| Sand | 18 / 18 | |
| CartPath | 18 / 18 | restored on H03 |
| Water | 11 / 18 | see §5.4 |
| **Rough** | **0 / 18** | ⚠️ never baked as polygons |
| **Semirough** | **0 / 18** | ⚠️ |
| **GreenCollar** | **0 / 18** | ⚠️ see §6.3 |
| **OOB** | **0 / 18** | by design — OOB comes from the mask |

`BakeZoneJsonTool.YOffsets` carries entries for `GreenCollar`, `Semirough`, `Rough` and `OOB`. **None are ever produced.** Vestigial but harmless.

### 5.4 ✅ RESOLVED — 3 holes have Water meshes with no raster water. **Not a defect.**

Runtime `Water` polygons exist on holes **5, 13, 16** while those holes report `water: 0 px` in the painted raster — i.e. nobody painted blue on them.

**Explanation: `water.json` is an independent vector source** (§3.0). Its `water_count` matches the baked polygon count exactly:

| Hole | `water.json` count | baked Water polygons | raster water px |
|---|---:|---:|---:|
| 05 | 1 | 1 | 0 |
| 13 | 4 | 4 | 0 |
| 16 | 1 | 1 | 0 |
| 06 | 1 | 1 | 129,876 |
| 14 | 1 | 1 | 48,786 |

The mesh comes from the vector file; the paint is cosmetic for those holes. Behaviour is **correct** — the water plays as water. The §4.2 gate is also correct to stay silent: it only fails when the raster *has* a type the bake lacks, never the reverse.

⚠️ **The one real consequence:** on holes 5, 13 and 16 the *terrain texture* around the water won't read as water, because the splatmap is painted from the raster. Cosmetic, not physical. Worth a glance in-game, not a blocker.

### 5.5 OB mask fidelity ✅

`obMask` is present and structurally valid on all 18 holes, uniformly **1024×1024** (the alphamap resolution). Set-bit share tracks the source raster's `ob` share closely:

| Hole | raster `ob` share | obMask set-bit share |
|---|---:|---:|
| 06 | 34.70% | 34.7% |
| 14 | 69.17% | 69.2% |
| 02 | 0% | 0.0% |

Three-for-three. **OB is the one surface that survives the authored→runtime pipeline intact.**

Note the resolution/aspect change: the raster is per-hole and non-square (e.g. 2048×901), the alphamap is always 1024×1024 mapped onto a non-square world rect. Cells are therefore non-square in world space — handled correctly, `obCellW` and `obCellH` are computed independently (`:96-97`).

---

## 6. Known defects — current, verified

### 6.1 🔴 Hole 02 has no out-of-bounds at all — **ROOT-CAUSED: the OB colour was never painted** ✅

**Root cause is upstream art, not code.** Hole 02's `zones.png` contains **zero pixels of the OB colour `(255, 51, 51)`** — decoded and counted directly from the PNG. The entire out-of-play region was painted `trees` and `rough` instead:

| class | Hole 01 | Hole 06 | **Hole 02** |
|---|---:|---:|---:|
| **ob** `(255,51,51)` | 59.7% | 34.7% | **0.0%** |
| trees `(26,51,16)` | 11.2% | 14.1% | **54.9%** |
| rough `(51,102,34)` | 20.5% | 34.2% | **36.7%** |

Hole 02 is the **only** hole of 18 with zero OB. `grid` `ob` count and `ob_mask` set-bits agree exactly on all 18 holes (both 0 here), so nothing downstream lost the data — it was never authored.

**The full failure chain, each link verified:**

```
zones.png has no (255,51,51) pixels
   → export grid ob = 0, ob_mask = 0 bits
   → HoleGeoImporter OB overlay (:1353) never promotes any cell to layer 8
   → terrain OB layer exists but is unpainted everywhere
   → BakeObMask packs 0 of 1,048,576 bits
   → hasObMask == TRUE  (tests obMaskBits.Length > 0, not "any bit set" — :98)
   → ladder step 2 runs on every call and can never match
   → everything outside a polygon resolves to Fairway
```

**Gameplay impact: you cannot go out of bounds on Hole 02.** Off-course shots are unpenalised and the OB camera clamp never arms.

**Fix is a content fix, not a code fix:** repaint `holes/02/zones.png` with the OB colour over the out-of-play region → re-run the UHoleGeo export → re-import → re-bake. No C# change required.

**Secondary code hardening worth considering** (separate decision): `hasObMask` treats an all-zero mask as a valid mask. A cheap guard — warn when a decoded mask has zero set bits — would have surfaced this at bake time instead of at playtest. The §4.2 gate cannot catch it, because `ob` is deliberately excluded from the gate's mapped types (it has no polygon zone).

### 6.2 🔴 Rough is never classified (Defect B of `surface_classification_ob_rough`)

Rough has no mesh and no mask, so it cannot be returned. Every rough cell resolves to `Fairway` by default. The coefficient gap:

| Surface | Restitution | TangentFriction | RollingResistance | StopSpeed |
|---|---:|---:|---:|---:|
| Fairway | 0.50 | 0.55 | **0.18** | 0.10 |
| Semirough | 0.38 | 0.70 | 0.28 | 0.15 |
| **Rough** | 0.25 | 0.82 | **0.45** | 0.22 |

✅ `Assets/Scripts/Physics/Core/SurfaceConfig.cs:25-35`. Rough should roll **2.5× less** than fairway. Today it rolls identically. Missing the fairway carries no ball-behaviour penalty.

**Scale:** rough is 201,777–1,243,210 px per hole — the dominant authored surface everywhere.

**But `semi_rough` is authored noise** — 314–1,564 px, below the gate's own 1,000-cell threshold on **15 of 18 holes**. This is **one** problem (Rough), not two.

### 6.3 🟡 `GreenCollar` is unreachable, and it gates the putt integrator

No hole bakes a `GreenCollar` polygon, so the type can never be returned. It appears in two live gameplay predicates:

- `BallSimulation.cs:759` — `IsPuttSurface(s) => s == Green || s == GreenCollar`, which gates the putt-tuned roll integrator
- Both bots switch to a wedge chip when the surface is neither

Currently harmless — `Green` alone satisfies both. But any future work that *relies* on collar behaviour will find it silently dead.

### 6.4 🔴 Past the terrain edge classifies as Fairway (Defect A)

`IsObAt` returns `false` for out-of-grid points (`:222-223`), so anything beyond the terrain footprint matches no polygon, misses the mask, and defaults to `Fairway`. Off-course shots are unpenalized and the OB camera clamp never arms. Untouched by the re-bake.

---

## 7. Traps — read before doing surface work

### 7.1 ❌ `terrain_grid` is lossy. Use `grid`.
Both are base64 uint8 arrays of identical length in the same file. `terrain_grid` **has no `ob` class at all** and folds ob + trees + cart_path into `rough`. Verified on Hole 14, reconciles with zero residual:

```
terrain_grid.rough − grid.rough = 3,472,630 − 580,741 = 2,891,889
ob + trees + cart_path          = 2,670,512 + 201,917 + 44,854 = 2,917,283
difference                      =    25,394
terrain_grid.water − grid.water = 74,180 − 48,786 =  25,394   ✓ exact
```

Using it scores 2.67M out-of-course OB cells as "authored rough".

### 7.2 ❌ The terrain alphamap is not an authored-intent oracle.
`ZoneToLayer` collapses it (§3.1). Only `semi_rough` survives. This sank `surface_coverage_audit`.

### 7.3 ❌ Two files are named `zones.json`. They are unrelated.

| | Path | Schema | Read by |
|---|---|---|---|
| **Source raster** | `Tools/UHoleGeo/output/<slug>/export/hole-NN/zones.json` | snake_case (`ob_mask`, `zone_stats`, `grid`) | the §4.2 gate; authoring tools |
| **Runtime** | `Assets/Resources/HoleData/<slug>/Hole_NN/zones.json` | camelCase (`obMask`, `zones`, `polygons`) | `BakedZoneClassifier` |

Different trees, different schemas, different content. Never cross-assume field names.

### 7.4 ⚠️ The raster carries no world bounds.
`hole-manifest.json` → `bounds: {}` — **empty** ✅. Any raster↔world mapping must be *derived*, from the runtime `obMask` world rect (§3.3) plus `terrain.terrain_width_m` / `terrain_length_m`. **Row orientation (raster row 0 = `worldOriginZ`, or flipped) is not established anywhere** and must be resolved empirically against known landmarks. A silently-wrong mapping yields a fully-populated, entirely meaningless result.

### 7.5 ❌ Don't re-implement point-in-polygon.
Use `BakedZoneClassifier.ClassifyWithProvenance` (editor-only, shares `ClassifyCore`). A parallel implementation can diverge from the real classifier and invalidate a whole measurement without any visible symptom.

### 7.6 ⚠️ `HoleLiteImporter.cs` is DEPRECATED.
Live importer is `HoleGeoImporter.cs` (banner commit `980cc122`). `HoleLiteImporter` still contains its own marker-stamping code and will match greps. Never target it.

---

## 8. Is the design sound?

**Broadly yes.** The core split is defensible: discrete features are mesh-shaped and belong as polygons; OB is a diffuse painted region and belongs as a raster mask; a default catches the remainder. The priority ordering is sensible, polygons-trump-mask is correct, and the classifier itself is small and readable.

**What went wrong is not the architecture — it's that one surface class fell between the two mechanisms.** Rough is diffuse like OB but was never given a mask, and non-mesh so it got no polygon. It landed on the default, and because the default is `Fairway` the failure was invisible: the ball still behaved like *something* plausible. The same silence let the bake ship four holes with entire surface types missing.

**The structural lesson:** the pipeline had no *completeness* check for most of its life — only correctness checks on the data that happened to be present. `zone_bake_completeness` added one (§3.5), and that closes the class of bug rather than its instances. Its one-directional design (§5.4) is the remaining gap.

---

## 9. Quick reference — where things live

| Concern | File | Key lines |
|---|---|---|
| Runtime classification | `Assets/Scripts/Physics/Runtime/Baked/BakedZoneClassifier.cs` | ladder `:185`, priority `:308`, `IsObAt` `:220`, `DefaultSurface` `:73` |
| Polygon + mask bake | `Assets/Scripts/Editor/CourseImporter/BakeZoneJsonTool.cs` | `BakeOne` `:127`, gate `:329`, `BakeObMask` `:478` |
| Splatmap + OB paint | `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs` | `ZoneToLayer` `:1614`, OB overlay `:1353` |
| Enum bridge | `Assets/Scripts/Editor/SurfaceMarkerMap.cs` | whole file |
| Physics enum | `Assets/Scripts/Physics/Core/SurfaceType.cs` | `:3-16` |
| Course enum + marker | `Assets/Scripts/Course/SurfaceMarker.cs` | `:5-21` |
| Coefficients | `Assets/Scripts/Physics/Core/SurfaceConfig.cs` | `:25-35` |
| Consumption | `Assets/Scripts/Physics/Core/BallSimulation.cs` | `Classify` `:242/:607/:785`, OOB `:257`, `IsPuttSurface` `:759` |

---

## 10. Related records

- `Docs/Specs/Completed/zone_bake_completeness/` — the re-bake + completeness gate
- `Docs/Specs/Completed/surface_coverage_audit/` — superseded; `SUPERSEDED.md` records the invalid-oracle failure
- `Docs/Specs/Completed/zone_bake_scope_probe/` — the probe that scoped the drop
- `Docs/Specs/Active/surface_fallthrough_coverage_probe/` — measures the fallthrough population
- `Docs/Specs/Queued/surface_classification_ob_rough/` — Defects A and B
