# Build size audit — Golfin.ipa 1.5.7 (2632) and GOLFINGPS.ipa 1.0.0 (2635), 2026-09-03

> Architect (GPS session) at Cesar's request ("700 MB seems excessive"). Numbers are read from the
> two .ipa files in `Builds/ipa/`, the standalone Build Report in `Builds/unity-build-ios.log`, and
> the import settings in `Assets/`. Owner of the fix: `build_size_diet` (Notion 2121, game session — spec at `Docs/Specs/Active/build_size_diet/`, SPEC_READY 2026-09-03 evening);
> the standalone half is `gps_standalone_shell` round 2.

## What the 711 MB actually is

| Part of `Golfin.ipa` | Size | Note |
|---|---|---|
| `Symbols/*.symbols` | **492 MB** | dSYM for crash symbolication. Apple keeps it and STRIPS it from what users download — not part of the install |
| `Payload/…/Data/` | **1,738 MB uncompressed** | this is the install footprint on the phone (~1.9 GB with the framework) |
| `UnityFramework` | 106 MB | IL2CPP binary; normal |
| whole .ipa (zip) | 711 MB | what you see in ASC; users download roughly Data compressed ≈ 500–600 MB |

So the real number is the **~1.9 GB install**, not 711 MB.

## Where the 1.74 GB of Data goes

| Bucket | MB | What it is | Fix |
|---|---|---|---|
| `.resS` streams (textures / meshes / audio) | 698 | `sharedassets8.assets.resS` alone is **480 MB** — the first hole scene that references the vegetation packs pulls their textures in once: `Assets/Packs/TreePackVol.1/Textures/Leave_4K_.psd` (4096, **compression None** = 64 MB raw), `Simple Trees Pack/…/T_Plant_Tree_Simple_Leaves_*.tga` (**maxTextureSize 8192**, three of them at 65 MB source), `Mobile_Tree_Bundle/Textures/Leaf/M_*_Leaf*.TGA` (a dozen at 32–65 MB source, 2048 ASTC) | iPhone platform overrides on every pack texture: max 1024 (leaves) / 2048 (bark), ASTC 6x6 or 8x8, no "None". Then audit the terrains' tree prototypes — the placed trees are only `Spruce 1` / `Spruce 3` (15,197 instances), so any pack the prototypes don't reference can leave the project (`PBR Bridge/HDRP/HDRPversion.unitypackage` is 215 MB of dead weight on disk too) |
| `sharedassetsN.assets` (18 hole scenes) | 524 | **~30 MB per hole = the TerrainData** (heightmap 2049², alphamaps, holes, detail) — serialized per scene | alphamap/base-map resolution audit (control textures are usually 1024 and can be 512 with no visible change on a fairway), keep the 2049 heightmap (physics fidelity rule from the perf pass) |
| `resources.assets` + `.resS` | 481 | everything under `Assets/Resources/` ships whether used or not: `HoleData` **302 MB heightmaps** (`GHM1`: 2049² **int32 Q16.16** fixed-point — not float32, corrected 2026-09-03 evening; a second copy of the terrain heights for the deterministic physics) + **~100 MB pretty-printed `zones.json`** (zlib → 9 %); `Resources/Clubs` 116 MB of source PNGs (292 files → ~50 MB built) | move `HoleData` out of `Resources` into per-hole StreamingAssets/Addressables, heightmap **lossless**: row-delta + Deflate (`GHM2`), never float16 — the physics is fixed-point, zones as compact binary; club art into a sprite atlas at 512 max |
| `levelN` (scene files) | 110 | the 18 hole scenes are 48–65 MB of YAML each because 1,495 standalone trees per hole are individual GameObjects | not size-critical after compression, but the same change that helps load time — instanced rendering from `standalone_trees.csv` instead of GameObjects — halves it |
| `global-metadata.dat` | 16 | IL2CPP metadata; normal | — |

## Smaller, cheap wins (both apps)

- **93 textures import with compression = None** (default platform, no iPhone override). The big ones: `Assets/Art/UI/Account/S_SocialPillBordered.png` 2680×600 (6 MB raw, for a pill!), `Resources/Art/Gacha/Banners/GachaBanner_StandardClub1.png` 882×1448 (5 MB), `HomeScreen/S_DailyPillGlow.png` and `S_DailyPillPanel.png`, `Original UI/MainScreen/S_Top_Area.png`. One sweep: iPhone override → ASTC 6x6 (UI) / 4x4 where gradients band, max 2048.
- **TMP fonts are Dynamic (`m_AtlasPopulationMode: 1`) so the source TTFs ship**: `NotoSansJP-VariableFont_wght.ttf` **9.1 MB**, Rubik ×3 ≈ 0.9 MB. NotoSansJP could be a static atlas generated from the CSV's actual glyph set (the localization pipeline already knows every JA string) — 9 MB saved and no runtime atlas rebake (the 7 KB → 2.2 MB churn noted in `auth_golf_profile`).
- `Assets/Scenes/Original~/…/ReflectionProbe-0.exr` 49 MB + 37 MB — `~` folders don't build, disk only; fine to delete.
- The Unity splash logo texture (1.2 MB) is the Personal-licence splash; it stays unless the licence changes.

## Standalone (GOLFIN GPS) — 427 MB → ≤ 150 MB

The standalone Build Report lists 555 MB of user assets for an app with one scene: **385 MB is `Resources/HoleData`**, 50 MB `Resources/Clubs`, 11 MB `Resources/Characters`, 39 MB `Art/UI` (mostly the GPS panels, legitimately), 9 MB the JP font. Round 2 of `gps_standalone_shell` moves the golf-only `Resources` subfolders out for the build. The pack textures do NOT reach the standalone (no hole scene), which is why it is "only" 427 MB.

## Expected outcome (game)

Pack-texture overrides (−350–400 MB install) + HoleData out of Resources and compressed (−350 MB) + compression-None sweep (−30 MB) + fonts (−10 MB) ≈ **install 1.9 GB → ~0.9 GB, .ipa 711 → ~300 MB** before touching terrain resolution. Alphamap audit is the next −100–200 MB if wanted.
