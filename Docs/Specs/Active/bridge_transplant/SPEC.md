# SPEC — `bridge_transplant`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Filed 2026-08-25 by the Architect (Cowork session).

## Goal

Bring the 7 bridge instances that exist only in the archived video-capture scenes
(`Assets/Golf/Courses/lomond-country-club/Generated/Video/Hole_NN_Geo.unity`) into the
live play scenes (`Generated/Hole_NN_Geo.unity`), and give them real collision in the
deterministic fixed-point sim: a walkable deck the ball lands and rolls on, plus railings
and piers that deflect it. Then re-bake only the per-hole data that actually changed.

Cesar's decisions of record (2026-08-25):

1. **Deck AND railings/piers.** Not deck-only, not cosmetic.
2. **Bots avoid bridges.** No change to `VersusBot.IsPlayableSurface`.
3. **All 7 instances** — holes 7, 8 (x2), 9, 12 (x2), 17.
4. **SPLIT, 2026-08-25 — bridges first, trees later.** The Video-only scenery (5 trees,
   1 841 grass tufts, 6 rocks, 14 signs across 8 more holes) was briefly folded in and is
   now its own spec: `Docs/Specs/Queued/scenery_transplant/`. The tree-wind gap found
   while auditing it is a third spec: `Docs/Specs/Queued/tree_wind_coverage/`. **This spec
   is bridges only, 5 holes.**

## Ground truth — the 7 bridge instances

Harvested from the Video scenes 2026-08-25. All 7 are at scene root
(`m_TransformParent: {fileID: 0}`), so local TRS == world TRS. Both scene sets reference
the **same TerrainData GUID** (`f024468aa2c3f9c42ac9cc410c8576d0` for hole 7, verified),
so world coordinates transplant verbatim — no re-anchoring.

| Hole | Name | Source asset (guid) | Position (x, y, z) | Euler Y (hint) | Scale (x, y, z) |
|---|---|---|---|---|---|
| 07 | `Bridge_withLODs` | `Bridge_withLODs.prefab` `639c38cdd1b018048adf44fe9cbe8db4` | 75.72, 23.20, -53.11 | 93.80 | 1, 4.09, 1.37 |
| 08 | `bridgeLODs` | `bridgeLODs.fbx` `d507e412d40cbc44db1839dae98011e6` | -94.34, 24.68, -37.56 | -81.31 | 0.5, 0.5, 0.14 |
| 08 | `bridgeLODs (1)` | same FBX | 92.50, 26.77, 179.10 | -42.00 | 0.5, 0.5, 0.26 |
| 09 | `bridgeLODs` | same FBX | -136.46, 7.49, -44.13 | -16.95 | 1, 1, 0.33 |
| 12 | `Bridge_withLODs` | `Bridge_withLODs.prefab` | -88.6349, 10.81, -149.69 | -4.53 (x-tilt 5.22) | 1, 1, 0.67 |
| 12 | `Bridge_withLODs (1)` | `Bridge_withLODs.prefab` | -71.55, 12.26, -49.07 | 54.00 (x-tilt 1.79) | 1, 1, 0.67 |
| 17 | `Bridge_part_1` | `Modular/Bridge_part_1.prefab` `d8ec07380d5292141a720626c01454bd` | 3.26, 22.80, -24.86 | -72.08 (x-tilt 0.36) | 1, 1, 1 |

Rotation quaternions are in the Video scene YAML; copy them, do not re-derive from the
Euler hints. Note the **non-uniform scales** (hole 7 is y=4.09 / z=1.37; hole 8 is
0.5/0.5/0.14) — every bake step must apply the full TRS, never a uniform-scale shortcut.

Note the **two x-tilted bridges on hole 12 and one on hole 17** — the deck is not
horizontal there.

## Architecture context — what already exists, and what it means

Read this section before writing any code. Three facts govern the whole design.

### Fact 1 — Unity colliders are irrelevant to ball physics

The ball runs in `Golfin.Physics.Core/BallSimulation.cs`, a deterministic fixed-point
(`fp`, Q16.16) integrator. It never touches PhysX. Ground height comes from
`BakedHeightProvider`; surface type from `BakedZoneClassifier`; the only obstacle system
is `ITreeObstacleProvider` (fixed-point cylinders). A bridge dropped into the scene is
**physically invisible** until something bakes it.

`Bridge_withLODs.prefab` ships 140 `BoxCollider` components and `Bridge_part_1.prefab`
ships 38, on children named `Collider_N` / `Beam_Collider`. Those are dead weight at
runtime — but they are excellent **authoring data** for the obstacle bake (see Stage C).

`bridgeLODs.fbx` (holes 8 and 9, three instances) is an FBX model instance, **not** a
prefab, and carries **no colliders at all**. See Risk 2.

### Fact 2 — the physics heightmap does NOT see the bridge, and must not be re-baked

`PhysicsHeightmapBaker.BakeActiveScene` reads `terrain.terrainData` heights only. Adding
a bridge changes no terrain, so `heightmap.bytes` (16.8 MB/hole) must come out
**byte-identical**. Do not re-bake it. Re-baking is ~100 MB of pointless churn across the
5 holes and re-opens the depression-band divergence documented in the `BakeZoneJsonTool`
header comment.

The deck's height reaches the sim through the **zone** path instead:
`BakedHeightProvider.SampleHeight` calls `classifier.TrySampleMeshY` FIRST and returns the
matched polygon's barycentric mesh Y, bypassing the heightmap entirely. A deck baked as a
zone mesh therefore becomes the authoritative ground Y over its footprint, for free.

### Fact 3 — a CartPath deck over water is silently masked. This is why we add a new surface type.

`BakedZoneClassifier.Priority` (line ~341): Green 100 > Sand 90 > BunkerLip 89 >
**Water 80** > GreenCollar 70 > Tee 60 > **CartPath 50** > Fairway 40. Polygons are stored
sorted by descending priority, and **both** `Classify` and `TrySampleMeshY` return the
FIRST polygon containing the XZ point.

A bridge spans water. A `CartPath` deck polygon over a `Water` polygon would be shadowed
completely: the ball standing on the bridge would classify as `Water` (penalty) and
`SampleHeight` would return the water surface Y. **Do not use CartPath for the deck.**

Adding `SurfaceType.Bridge` at priority 95 fixes this AND satisfies decision 2 for free:
`VersusBot.IsPlayableSurface` (VersusBot.cs:381) lists Fairway/Green/GreenCollar/Semirough/
Rough/Tee/Sand. `Bridge` is absent, so the bot's H2 landing probe treats a bridge deck as
unplayable and retargets away from it — exactly the requested behaviour, with **zero
change to bot code and zero blast radius onto the real cart paths on the other 13 holes**.

### Fact 4 — the tree baker has a save hook that will eat a misplaced bridge

`TreeObstacleBaker.OnSceneSaving` fires on **every** save of a `Hole_NN*` scene and
re-harvests `StandaloneTrees` and `PaintedTrees` container children into
`tree_obstacles.csv`. If any bridge GameObject ends up under either container, its parts
are baked as tree cylinders and the hole's tree collision is corrupted. The bridge
container is named `Bridges` and lives at scene root. A changed `tree_obstacles.csv`
hash after this task is a **bug**, never an expected diff.

### Files referenced

- `Assets/Scripts/Physics/Core/SurfaceType.cs` — 11 values, `Fairway=0` .. `OOB=10`
- `Assets/Scripts/Physics/Core/SurfaceConfig.cs:24` — `new SurfaceCoefficients[11]` **hardcoded**
- `Assets/Scripts/Physics/Core/PuttConfig.cs:66` — uses `Enum.GetValues(...).Length`, self-sizing
- `Assets/Scripts/Physics/Core/TreeObstacleData.cs` — the model to mirror for bridges
- `Assets/Scripts/Physics/Core/BallSimulation.cs` — tree hooks at ~464 (flight), ~1147 (roll), ~1357 (putt)
- `Assets/Scripts/Physics/Runtime/TreeObstacleProvider.cs` — XZ grid + deterministic candidate order + fp containment guard
- `Assets/Scripts/Physics/Runtime/TreeObstacleLoader.cs` — CSV + profile-table loader pattern
- `Assets/Scripts/Physics/Runtime/SurfaceMarker.cs` — `Golfin.Physics.Runtime.SurfaceMarker { SurfaceType Type; }`, guid `5b0f945fb3f4f1b4ea87d4a862c258fd`
- `Assets/Scripts/Physics/Runtime/Baked/BakedZoneClassifier.cs` — `Priority()`, `Classify()`, `TrySampleMeshY()`
- `Assets/Scripts/Physics/Runtime/Baked/ZoneData.cs` — `type` is serialized as the enum **name string**, so a new value is JSON-safe
- `Assets/Scripts/Editor/CourseImporter/BakeZoneJsonTool.cs` — `YOffsets`, `CollectPolygons` (needs `SurfaceMarker` + `MeshFilter`), `AddMeshTriangles` (applies `transform.TransformPoint`, so world space is handled)
- `Assets/Scripts/Editor/CourseImporter/TreeObstacleBaker.cs` — bake-hash header + save hook, the pattern to mirror
- `Assets/Scripts/Physics/Viewer/VersusBot.cs:380-384` — `IsAvoidSurface` / `IsPlayableSurface`
- `Assets/Scripts/Course/BridgeAnchor.cs` + `Assets/Scripts/Editor/CourseImporter/BridgeExporter.cs` — **pre-existing and unrelated.** They export bridge anchor points to `bridges.json` for cart-path spline snapping in UHoleGeo. Do not extend, rename, or repurpose them; do not confuse `bridges.json` with the new `bridge_obstacles.csv`.

### Asmdefs affected

`Golfin.Physics.Core`, `Golfin.Physics.Runtime`, `Golfin.Editor.CourseImporter`,
`Golfin.Physics.Tests`.

---

## Implementation

Ship the stages in order. **Stage A + B on hole 7 alone is a complete, verifiable
increment** — get that reviewed before batching the other four holes.

### Stage A — transplant the meshes

New editor tool `Assets/Scripts/Editor/CourseImporter/BridgeTransplantTool.cs`
(namespace `Golfin.CourseImport`), menu `Import/Transplant Bridges/…` with
`Bake Current Hole` + per-hole items, mirroring `TreeObstacleBaker`'s menu shape.

Behaviour per hole N:

1. Open `Generated/Video/Hole_NN_Geo.unity` **additively**.
2. Collect scene-root GameObjects whose name matches `^[Bb]ridge` (this exactly
   selects the 7 rows in the table above and nothing else — verified by grep).
3. In the live `Generated/Hole_NN_Geo.unity`, get-or-create a scene-root GameObject
   named **`Bridges`** at identity transform.
4. For each source: resolve its source asset via `PrefabUtility.GetCorrespondingObjectFromSource`,
   `PrefabUtility.InstantiatePrefab` it into the live scene under `Bridges`, then copy
   `position` / `rotation` / `localScale` verbatim from the source transform.
   Preserve the source GameObject name (including the ` (1)` suffixes).
5. Close the Video scene **without saving**. (Standing repo rule: the capture launchers
   already do snapshot-and-restore of scene setup — mirror that, never leave the Video
   scene dirty.)
6. Log a per-hole line: hole, count, and each instance's world position, so the
   IMPLEMENTER_REPORT can be diffed against the table above.

Hard constraints:

- `Bridges` is a **scene-root** object. Never parent it, or anything under it, to
  `StandaloneTrees` or `PaintedTrees` (Fact 4).
- Never edit the `.unity` YAML by hand. Prefab instance blocks carry `m_SourcePrefab`
  and stripped-Transform back-references; hand-merging them across two 2 MB scene files
  is how a scene gets silently corrupted.
- The live hole scenes are **shipped holes**. Standing rule from 2026-08-10: repair in
  place, never re-import. This tool only ADDS a root object.

### Stage B — `SurfaceType.Bridge` and the deck zone

**B1. Enum.** Append `Bridge` to `Assets/Scripts/Physics/Core/SurfaceType.cs` as value
**11**, after `OOB = 10`. Do not renumber any existing value.

**B2. `SurfaceConfig.cs:24`.** `new SurfaceCoefficients[11]` → `[12]`. This is a hardcoded
length with a `// must match SurfaceType count` comment; missing it throws
`IndexOutOfRangeException` on the first bridge classification. Add the Bridge row:

```
Restitution = 0.45f, TangentFriction = 0.35f, RollingResistance = 0.12f, StopSpeed = 0.10f
```

Rationale: a timber deck, between `CartPath` (0.70 / 0.18 / 0.06 / 0.08 — hard concrete,
too lively) and `Fairway` (0.50 / 0.55 / 0.18 / 0.10). **These four numbers are a
starting guess and an explicit tuning knob — flag them in IMPLEMENTER_REPORT for Cesar's
feel pass, do not treat them as settled.**

**B3. `PuttConfig.cs:66`.** That array is `Enum.GetValues(...).Length`-sized so it grows
by itself, but the Bridge row is left zeroed. Add a putt row for Bridge mirroring its
neighbours (putting on a bridge is rare but must not read as a zero-restitution void).

**B4. `BakedZoneClassifier.Priority()`.** Add `case SurfaceType.Bridge: return 95;` —
above Sand (90) and Water (80), below Green (100). A green is never on a bridge; water
and sand are exactly what the deck must outrank.

**B5. `BakeZoneJsonTool.YOffsets`.** Add `{ SurfaceType.Bridge, 0f }`. The deck mesh IS
the surface — Path β barycentric sampling returns its vertex Y directly, so no offset
above terrain applies.

**B6. Completeness gate.** `CheckCompletenessGate` maps *source-raster* zone names to
baked types and asserts each meaningful source type survived. `Bridge` has no
source-raster counterpart, so it is an *extra* baked type and the gate is unaffected —
**verify this by running the gate on hole 7 and confirming it still passes**, don't
assume it.

**B7. Deck mesh authoring.** Under each transplanted bridge, add a child GameObject
`Deck_Collision` carrying:

- a `MeshFilter` whose mesh is the deck walking surface — a flat (or, on the tilted
  hole-12/17 bridges, tilted) triangulated strip matching the deck top plane and the
  deck's XZ footprint, authored in the bridge's LOCAL space so the instance TRS carries
  it into world (`AddMeshTriangles` calls `transform.TransformPoint`);
- `Golfin.Physics.Runtime.SurfaceMarker` with `Type = SurfaceType.Bridge`;
- **no `MeshRenderer`** (`BakeZoneJsonTool.CollectPolygons` requires only
  `SurfaceMarker` + `MeshFilter`), so nothing new is drawn.

Generate the deck mesh from the source prefab's own geometry rather than by hand: the
deck-top plane is derivable from the `Top_L_*` / `Top_R_*` renderer bounds in
`Bridge_withLODs` / `Bridge_part_1`. Where that is unreliable (the `bridgeLODs` FBX),
fall back to the renderer bounds of the whole model, take the top face, and inset the
railing width. Whichever route: the generated mesh must be saved as an asset next to the
spec's own tooling output, not left as a scene-only mesh (scene-only meshes do not
survive a scene reload and `BakeZoneJsonTool` would silently bake nothing).

The tool that generates these decks lives in the same `BridgeTransplantTool.cs`, as a
second menu item `Import/Transplant Bridges/Generate Deck Meshes (Current Hole)`, so the
step is repeatable and auditable.

### Stage C — railings and piers as fixed-point obstacles

Mirror the tree system beat for beat. Do not invent a different shape of API.

**C1. `Assets/Scripts/Physics/Core/BridgeObstacleData.cs`** (namespace `Golfin.Physics`):

- `BridgeCollisionProfile` — `Restitution`, `TangentDamping`, all `fp`, keyed by name.
- `BridgeBox` — an oriented box: `CenterX`, `CenterZ`, `BaseY`, `TopY`, `HalfX`, `HalfZ`,
  `CosYaw`, `SinYaw` (bake the sin/cos, don't trig at runtime), `Profile`. Yaw-only
  rotation; the x-tilts on holes 12 and 17 are folded into `BaseY`/`TopY` per box at bake
  time (each `Collider_N` box becomes its own axis-aligned-in-Y slab), so the runtime
  primitive stays a yaw-rotated AABB and the sim stays cheap and exactly reproducible.
- `BridgeHit` — `Frac`, `HitPos`, `NormalXZ`, `Profile`. Mirrors `TreeHit` minus `IsTrunk`.
- `IBridgeObstacleProvider` — `bool TestSegment(fp3 p0, fp3 p1, out BridgeHit hit)`.

**C2. `Assets/Scripts/Physics/Runtime/BridgeObstacleProvider.cs`**: same XZ grid as
`TreeObstacleProvider` — `CellSize = 10f`, `CellKey` packing, radius-aware insertion into
every overlapped cell, 3×3 gather around `p0`, candidate list **sorted by array index**
before testing. Determinism is the whole point; copy the ordering discipline exactly.

Per-box test: transform the segment into box-local space with the baked cos/sin, run a
3-slab ray-vs-AABB, take the earliest `t` in `[0,1]`, rotate the local face normal back to
world. **Port the containment guard**: if `p0` is already inside the box (Q16.16
discriminant/precision loss on micro-steps lets a rolling ball tunnel), return `frac=0`
with a push-out normal along the shallowest-penetration axis. This exact defect cost a
red-team iteration on trees (`TestTrunkCrossing` containment guard, iter-4) — do not
rediscover it.

Return the earliest hit across all candidates. `Create(List<BridgeBox>)` returns `null`
for an empty list and logs once, exactly like `TreeObstacleProvider.Create`.

**C3. `Assets/Scripts/Physics/Runtime/BridgeObstacleLoader.cs`**: mirrors
`TreeObstacleLoader`. Reads
`Assets/Resources/Data/bridge_collision_profiles.csv` (new; at minimum a `default` row
plus `railing`, `pier`) and per-hole
`Assets/Resources/HoleData/<courseSlug>/Hole_NN/bridge_obstacles.csv` (new). Course slug
via `CourseSlugResolver`, never hardcoded. Warn once per distinct unprofiled name — the
tree loader's comment about hole 6 shipping mis-tuned for months is the reason that
warning exists; carry it over.

**C4. `Assets/Scripts/Editor/CourseImporter/BridgeObstacleBaker.cs`**: mirrors
`TreeObstacleBaker` — same menu shape, same `# bake_hash=<hex8>` FNV-1a header, same
`EditorSceneManager.sceneSaving` auto-rebake-on-change hook, same
`Resources/HoleData/<slug>/Hole_NN/` output root.

Harvest: walk the `Bridges` container's children recursively; for every `BoxCollider`
found (the prefabs ship 140 and 38 of them, named `Collider_N` / `Beam_Collider`),
convert `center` + `size` through the full local-to-world matrix into a world `BridgeBox`.

Classification, relative to that bridge's deck plane `deckY`:

- box top **above** `deckY + 0.15` → `railing`
- box top **below** `deckY - 0.15` → `pier`
- box straddling `deckY` → **excluded**: that is the deck itself, already handled by the
  Stage B zone mesh. Double-representing it would fight the ground solver.

CSV columns: `centerX,centerZ,baseY,topY,halfX,halfZ,yawDeg,profileName`.

**C5. `BallSimulation`.** Add an `IBridgeObstacleProvider bridges` parameter alongside the
existing `ITreeObstacleProvider trees` at the three call sites (~464 flight, ~1147 roll,
~1357 putt). Keep the existing overloads working with `bridges = null` so every current
caller and test is a zero-behaviour-change no-op. Precedence within a step: **bridge box
hit wins over a canopy hit, and resolves against a trunk hit by earliest `Frac`** — the
same "specific geometry outranks soft volume" rule the two-pass tree logic already
encodes.

Reflection on hit: mirror the roll-phase trunk handler — reflect XZ velocity about
`NormalXZ`, scale by the profile restitution, set `posNext = hit.HitPos`, and (roll/putt
phases only) re-seat Y onto `ground.SampleHeight`.

**C6. Wiring.** `PhysicsLabController.cs:2017` builds `_treeProvider`; build
`_bridgeProvider` beside it from `BridgeObstacleLoader` and pass it through the same path
that `GetTreeProvider()` feeds.

### Stage D — bots: deliberately nothing

Per decision 2, **do not touch `VersusBot`** and **do not extend `BotTreeProbe`** to
bridges. `SurfaceType.Bridge` is absent from `IsPlayableSurface`, so the H2 landing probe
already declines to aim at a deck.

Two things must nevertheless be *verified*, because "nothing changes" is a claim:

- On holes 7/8/9/12/17 the probe now finds a strip that reads `Bridge`-unplayable where it
  previously read `Water`-avoid. Both were rejected, so the landing choice should be
  unchanged — **prove it** with a before/after aim trace on one bridge, not by assertion.
- Confirm the probe cannot deadlock when every candidate aim is unplayable (a narrow
  channel between two hole-12 bridges is the realistic case). Add a test.

### Stage E — the re-bake and diff loop, per hole

For each of holes 7, 8, 9, 12, 17:

1. Open `Assets/Golf/Courses/lomond-country-club/Generated/Hole_NN_Geo.unity`.
2. `Import/Transplant Bridges/…` (Stage A) then `Generate Deck Meshes` (Stage B7).
3. `GOLFIN/Tools/Bake Zone JSON (Active Hole)` → `zones.json` gains a `Bridge` group.
4. `Import/Bake Bridge Obstacles/Bake Current Hole` → `bridge_obstacles.csv`.
5. Save the scene.
6. **`git diff --stat` for that hole must show exactly three files:**
   `Generated/Hole_NN_Geo.unity`, `Resources/HoleData/<slug>/Hole_NN/zones.json`,
   `Resources/HoleData/<slug>/Hole_NN/bridge_obstacles.csv` — plus the new deck-mesh
   assets. **Anything else is a defect:**
   - `heightmap.bytes` changed → someone re-baked the heightmap (Fact 2).
   - `tree_obstacles.csv` changed → a bridge leaked into a tree container (Fact 4).
   - Another hole's files changed → the batch menu item opened scenes it shouldn't.
7. Diff `zones.json` semantically, not just by size: every pre-existing zone group's
   polygon count must be unchanged. Only the `Bridge` group is new. A shifted Fairway or
   Green polygon count means the scene drifted since its last bake and that drift is now
   being committed under cover of this task.


## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item marked `PASS` or `FAIL` with a one-sentence justification citing what was measured.

- [ ] All 7 bridges present in the live scenes at the exact world TRS in the ground-truth table (log the transplant tool's per-instance output and diff it against the table)
- [ ] `Bridges` is a scene-root GameObject in all 5 holes; nothing bridge-related is under `StandaloneTrees` or `PaintedTrees`
- [ ] `tree_obstacles.csv` bake hash is UNCHANGED for all 5 holes after saving the scenes (hole 17's is `79f0eae4` / 1663 rows as of 2026-08-25 04:04 — Cesar planted 829 spruces there that morning; it must not move)
- [ ] `heightmap.bytes` is byte-identical for all 5 holes (`git status` clean for that path)
- [ ] `SurfaceConfig.Default` array length is 12 and `SurfaceConfig[SurfaceType.Bridge]` returns the specced coefficients
- [ ] `BakedZoneClassifier.Classify` returns `Bridge` (not `Water`) at a mid-deck XZ on hole 7, and `Water` again 5 m off the deck edge
- [ ] `BakedHeightProvider.SampleHeight` at that same mid-deck XZ returns the deck Y, not the water Y
- [ ] `zones.json` for each of the 5 holes gained exactly one `Bridge` group; every other group's polygon count is unchanged
- [ ] `bridge_obstacles.csv` row counts are plausible per bridge and no row straddles the deck plane
- [ ] Ball fired across each of the 7 bridges lands on the deck, rolls, and does not register a water penalty
- [ ] Ball fired at a railing deflects; two identical shots produce identical trajectories (determinism)
- [ ] `BallSimulation` overloads without the bridges param produce byte-identical trajectories to pre-change (zero-behaviour-change proof)
- [ ] VersusBot aim trace on hole 12 is unchanged before vs after; the two-bridge channel does not deadlock the probe
- [ ] No material, shader or prefab asset was modified by this task
- [ ] EditMode suite sweeps per assembly with no new failures (filtered runs mask failures — standing repo rule)
- [ ] Unity Console has no errors related to this task
- [ ] Bridge surface coefficients (B2) explicitly flagged for Cesar's feel pass
- [ ] Spec deviations (if any) flagged at the bottom of the report with justification

## Files / hierarchy this task touches

**New**

- `Assets/Scripts/Editor/CourseImporter/BridgeTransplantTool.cs`
- `Assets/Scripts/Editor/CourseImporter/BridgeObstacleBaker.cs`
- `Assets/Scripts/Physics/Core/BridgeObstacleData.cs`
- `Assets/Scripts/Physics/Runtime/BridgeObstacleProvider.cs`
- `Assets/Scripts/Physics/Runtime/BridgeObstacleLoader.cs`
- `Assets/Scripts/Physics/Tests/BridgeCollisionTests.cs`
- `Assets/Resources/Data/bridge_collision_profiles.csv`
- `Assets/Resources/HoleData/lomond-country-club/Hole_{07,08,09,12,17}/bridge_obstacles.csv`
- deck-mesh assets generated by Stage B7

**Modified**

- `Assets/Scripts/Physics/Core/SurfaceType.cs` — append `Bridge = 11`
- `Assets/Scripts/Physics/Core/SurfaceConfig.cs` — array `[11]` → `[12]`, Bridge row
- `Assets/Scripts/Physics/Core/PuttConfig.cs` — Bridge putt row
- `Assets/Scripts/Physics/Core/BallSimulation.cs` — bridge provider at 3 call sites
- `Assets/Scripts/Physics/Runtime/Baked/BakedZoneClassifier.cs` — `Priority` case, 95
- `Assets/Scripts/Editor/CourseImporter/BakeZoneJsonTool.cs` — `YOffsets` entry
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — build + pass `_bridgeProvider`
- `Assets/Golf/Courses/lomond-country-club/Generated/Hole_{07,08,09,12,17}_Geo.unity`
- `Assets/Resources/HoleData/lomond-country-club/Hole_{07,08,09,12,17}/zones.json`

**Read-only / must not change**

- `Generated/Video/*.unity` — source of truth, opened additively, closed unsaved
- `Resources/HoleData/*/heightmap.bytes`
- `Resources/HoleData/*/tree_obstacles.csv`
- `Assets/Scripts/Course/BridgeAnchor.cs`, `Assets/Scripts/Editor/CourseImporter/BridgeExporter.cs`
- `Assets/Scripts/Physics/Viewer/VersusBot.cs`, `Assets/Scripts/Physics/Viewer/BotTreeProbe.cs`

## Smoke evidence

Per Lesson O this task has a visual-fidelity half and a numeric half; both are required.

**Numeric.** EditMode tests: slab-test unit cases (hit / miss / grazing / containment
guard), determinism (identical inputs → identical fp trajectory hash), priority test
(`Bridge` beats `Water` at a hole-7 deck XZ, `Water` returns 5 m off the edge), and a
zero-behaviour-change proof that the null-bridges path reproduces current trajectories
exactly. Position-trace assertions, not event captures.

**Visual.** Human-in-the-loop in PhysicsLab: for each of the 7 bridges, one shot landing
on the deck and one shot into a railing, with a written content-sanity description of what
the ball visibly did. Then one device pass — a build, one shot per bridge hole.

## Risks and open calls

1. **The height field is 2.5D — nothing can pass UNDER a bridge.** `SampleHeight` returns
   one Y per XZ, and the deck now wins over the bridge footprint. A low approach that
   should sail beneath the hole-12 bridges will instead clang off the underside. This is
   inherent to the baked architecture and is NOT fixable within this task; a real fix
   means a 3D collision path for the whole sim. Bridges are short and sit over water, so
   the case should be rare — **but confirm on hole 12, which has two of them, and report
   how bad it looks.**
2. **`bridgeLODs.fbx` (holes 8 x2, 9) has no BoxColliders.** Stage C4 harvests colliders;
   these three instances would produce an empty `bridge_obstacles.csv` and end up
   deck-only. Two routes: author a prefab variant with railing/pier boxes (correct, more
   work), or ship those three deck-only in v1 and file the rails as a follow-up. **Flag
   which route was taken; do not silently ship three bridges with no railings.**
3. **Bridge surface coefficients are a guess** (B2) and need Cesar's feel pass.
4. **`SurfaceConfig.cs:24` hardcoded `[11]`** is the one place a new enum value throws at
   runtime rather than failing to compile. Verify it, don't trust the grep.
5. **Non-uniform scale.** Hole 7 is y=4.09/z=1.37, hole 8 is 0.5/0.5/0.14. The tree
   baker's `child.localScale.x // uniform scale assumed` shortcut is WRONG for bridges —
   the obstacle baker must use the full local-to-world matrix.

## Out of scope (do NOT do these)

- Any change to `VersusBot` or `BotTreeProbe` (decision 2).
- Re-baking `heightmap.bytes` or `tree_obstacles.csv`.
- Re-importing any hole through `HoleGeoImporter`. Shipped holes are repaired in place.
- Touching `BridgeAnchor` / `BridgeExporter` / `bridges.json` (cart-path spline snapping —
  a different feature that happens to share the word "bridge").
- Adding bridges to any hole that does not have one in the Video scenes.
- Cart-path behaviour changes on the other 13 holes.
- Ball-under-bridge 3D collision (Risk 1).
- Any material, shader or prefab asset change. In particular: do NOT re-material the
  spruces onto `Custom/Vegetation` and do NOT extend `TreeWindDriver` — that is
  `Docs/Specs/Queued/tree_wind_coverage/`, and it is blocked on a Cesar decision.
- Moving ANY of the Video-only scenery — grass, rocks, signs, or the five Video-only
  trees. That is `Docs/Specs/Queued/scenery_transplant/`, scheduled after this.
- Touching the 13 702 hand-placed spruces already in the live scenes — already placed,
  already baked.
- `git commit` from the Cowork session — Code commits (WORKFLOW_NOTES).
