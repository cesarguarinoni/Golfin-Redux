# Quick Task — `terrain_heightmap_rebake`

**Filed:** 2026-06-10 (architect: Claude + Cesar) · **Type:** physics / course-data fix (Quick — architect-driven, no subagent pipeline)
**Surfaced by:** `1v1_match_flow` par-3 capture; **reproduced by Cesar in solo Practice on Hole 4.**

## Bug (was: "ball goes through terrain behind the green")
In solo Practice on Hole 4, a straight tee shot at 50% power bounced fine on green + fairway, then passed **through the terrain behind the green** on the 3rd bounce.

## Root cause (confirmed numerically, 2026-06-10)
The sim ground is a baked heightmap (`Assets/Resources/HoleData/Hole_NN/heightmap.bytes`) consumed by `BakedHeightProvider`:
- **Inside** a baked zone polygon (tee/fairway/green/bunker) → returns exact zone-**mesh** Y (Path A). Matches the visible terrain → play feels correct.
- **Outside** all zone polygons (rough behind/around a green) → falls back to the raw **heightmap** Y.

The baked heightmaps are **~9–11 m BELOW the current visual terrain across the whole hole** (measured on Hole 4: green 10.6 m low, tee 10.9 m low, mid 10.1 m low). The terrain was regenerated course-wide *after* the heightmaps were baked (heightmaps committed Apr 25; `TerrainData_Hole*Geo.asset` regenerated later — all of holes 03,04,05,07,08,09,11,12,13,14,15,16,18 have heightmaps older than their terrain). Inside zones the mesh-Y path masks it; in the rough the ball drops ~10 m through the visible ground. (Recurring class — see `Docs/Backups/terrain-fallthrough-20260424/`.)

NOT a 1v1 bug, NOT the bot, NOT the small uncommitted `TerrainData` drift (only ~5 KB; the 10 m offset predates it).

## Fix
Re-bake the heightmaps from the **current** terrain. In-Unity tool: `Import > Bake Physics Heightmap > Bake Hole NN` (`PhysicsHeightmapBaker`, reads `Terrain.terrainData`, writes Q16.16). Keeps the deterministic baked-data architecture; just refreshes stale data.
- Catch: the baker writes to `Tools/UHoleGeo/output/lomond-country-club/export/hole-NN/heightmap.bytes` (gitignored staging). Must then **copy** that into the shipped `Assets/Resources/HoleData/Hole_NN/heightmap.bytes` and reimport.

## Verification (probe: `BakedHeightProvider.SampleHeight` vs `Physics.Raycast` on the visual Terrain)
### Hole 4 — DONE (proof)
| metric | before | after re-bake |
|---|---|---|
| max \|visual − rawHeightmap\| | ~10.9 m | 0.62 m |
| max \|visual − simGround\| | 8.97 m (behind green) | 0.13 m |

Bake round-trip mismatches: 0/100. Backup of old Resources heightmap: `/tmp/hole04_heightmap_RESOURCES_backup.bytes`.

## Rollout plan (pending Cesar go)
1. Re-bake remaining stale holes (03,05,07,08,09,11,12,13,14,15,16,18) — and optionally 01,02,06,10,17 for completeness (re-bake of an unchanged hole is a no-op byte-wise).
2. Copy each `export/hole-NN/heightmap.bytes` → `Assets/Resources/HoleData/Hole_NN/heightmap.bytes`, reimport.
3. Probe each hole (ground-vs-visual max delta < ~1 m) to confirm.
4. Commit the regenerated `heightmap.bytes` files (course-data commit, scoped).
5. Unblock `1v1_match_flow` (its BUG C is gone) and resume the §15 capture.

## STATUS — DONE (2026-06-10, committed `1648db3b`, pushed)
Re-baked all 18 holes from current terrain; **12 had stale heightmaps and were fixed** (02,03,04,05,06,08,09,10,11,12,13,14); 6 were already current (byte-identical, no change). Verified per hole: sim ground now matches the visual terrain within **0.02–0.42 m** (was ~9 m off behind greens). Committed only the 12 `heightmap.bytes` + this doc (scoped). Backups: `/tmp/heightmap_backup_20260610/` and `/tmp/hole04_heightmap_RESOURCES_backup.bytes`.

**Per-hole verification (max |simGround − visualRaycast|):**
H02 0.02 · H03 0.10 · H04 0.13 · H05 0.29 · H06 0.24 · H08 0.23 · H09 0.25 · H10 0.11 · H11 0.32 · H12 0.39 · H13 0.21 · H14 0.42 m — all PASS (<1 m).

Note: heightmaps were baked from the current working-tree terrain (which has a tiny ~5 KB uncommitted `TerrainData_Hole*Geo.asset` drift, sub-meter). If that drift is ever reverted the match stays sub-meter — not a regression of the ~10 m bug.

→ `1v1_match_flow` BUG C is resolved; that task can resume the §15 par-3 capture (BUG A + BUG B fixes still pending there).
