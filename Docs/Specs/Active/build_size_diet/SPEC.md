# SPEC — `build_size_diet` (GAME track — Notion 2121, P1)

> Brief: `Docs/BUILD_SIZE_AUDIT.md` (Architect, 2026-09-03). Cesar: "app is like 700 MB, seems
> excessive for what is in there." The 711 MB .ipa is 492 MB of symbols Apple strips + a
> **1.74 GB Data folder = the install footprint (~1.9 GB on the phone)**. That is the number this
> task cuts. The standalone (GOLFIN GPS) half is `gps_standalone_shell` round 2 — not here.

## Goal

Install ≤ **1.0 GB**, with **zero** visible change on device and
**byte-identical physics** (the ball must land where it landed).

**Which number is the gate (amended 2026-09-04, Cesar's call).** Re-derived from `Builds/ipa/Golfin.ipa`
with `unzip -lv`: `Symbols/` is 516 MB raw → **127 MB zipped**; the Payload is 1.85 GB raw →
**584 MB zipped**. A 1.0 GB install whose remaining bulk is ASTC + Deflated data zips to roughly
330–350 MB, so the .ipa FILE lands near 460–480 MB the day the install target is met — the old
".ipa ≤ 350 MB" line contradicted the install target and is withdrawn. The gates are:

| Measure | How to read it | Gate |
|---|---|---|
| **Install** = `Payload/Golfin.app` uncompressed (`du -sh`, or Xcode ▸ Devices "Size") | the number the user sees | **≤ 1.0 GB** |
| **Payload-compressed** = sum of `unzip -lv` compressed sizes under `Payload/` | what a tester downloads | **≤ 350 MB** |
| `.ipa` file size | includes `Symbols/` (127 MB zipped) that Apple strips | reported, **not gated** |

Every size claim in the report names which of the three it is. Getting the .ipa FILE itself under
350 MB is a fastlane/export decision (don't pack `Symbols/`; the dSYM zip already sits beside the
.ipa in `Builds/ipa/`) — out of scope here, backlog row. Nothing in this task is allowed
to alter a heightmap sample, a zone polygon, a green topology value, a tree obstacle, or what a
hole looks like from the tee. Measured, not felt: every phase ends with a Build Report diff and a
parity gate.

Correction to the brief, found while grounding this spec: `heightmap.bytes` is **not float32** —
it is `GHM1`: 36-byte header + 2049² **int32 Q16.16** fixed-point heights (`HeightmapLoader`),
because the physics is `fp` deterministic. That is good news: the right compression is
**lossless**, and the parity gate becomes "the decoded `int[]` is identical", no tolerance
arguments.

## Phases (in this order; each is its own commit, each ends with the numbers)

### Phase 0 — measure first (no code)

Build the current iOS-Full profile once (`./Tools/testflight.sh` up to the Xcode archive, or the
existing `CIBuild` entry with upload skipped) and keep the Build Report from `Editor.log` /
`Builds/unity-build-ios.log` as `reference/build_report_before.txt`; keep `Payload/…/Data`
per-file sizes (`du -a` sorted) as `reference/data_before.txt`. All later "−N MB" claims cite these.

### Phase 0b — LZ4HC measurement build (added 2026-09-04; numbers only, no adoption without Cesar)

`CIBuild` builds every lane with `BuildOptions.None`, and on iOS that means the Data folder is
stored **uncompressed** on the phone. The 18 `TerrainData` (`sharedassetsN.assets`, 549 MB raw)
deflate 81 % → 106 MB and no other phase reaches them without a fidelity trade (Phase 5). Build the
same tree ONCE more with `BuildOptions.CompressWithLz4HC` (a local flag/arg on `BuildIOSCore`, not
a default change) and file `reference/build_report_lz4hc.txt` + `data_lz4hc.txt` + the hole-load
timings (Hole 1 + 6, 3 runs, same hooks as Phase 2.6) next to the `_before` files. Report install,
Payload-compressed and per-bucket deltas, and the load-time delta. **Do not switch the lane** —
Cesar decides from the numbers whether LZ4HC becomes a phase. Run this BEFORE writing Phase 2 code:
it changes how much GHM2 is worth (LZ4 on raw GHM1 gets part of the way; row-delta + Deflate is
expected to beat it ~3–5× — show it, don't assume it).

### Phase 1 — vegetation-pack textures (the 480 MB `sharedassets8.assets.resS`)

1. For every texture under `Assets/Packs/TreePackVol.1/`, `Assets/Packs/Simple Trees Pack/`,
   `Assets/Packs/Mobile_Tree_Bundle/` (and any other pack referenced by a terrain tree prototype —
   list them): add an **iPhone platform override** — leaves/foliage alpha textures **max 1024**,
   bark/trunk **max 2048**, format **ASTC 6x6** (ASTC 4x4 only where a texture is used as an alpha
   cutout and 6x6 visibly frays the edge at 1 m — decide per texture with a side-by-side capture,
   not by default). No texture keeps `compression: None`.
2. **Prototype audit**: for each of the 18 hole terrains, list `TerrainData.treePrototypes` and
   `detailPrototypes`. `standalone_trees.csv` / `StandaloneTreeCatalog` say the placed trees are
   `Spruce 1` / `Spruce 3` only (15,197 instances); any prototype whose prefab is never placed on
   any terrain AND never referenced from a scene is **removed from the prototype list** (that is
   what pulls its textures into the scene bundle). Removing a prototype from `TerrainData` does not
   touch instances of other prototypes — prove it: `treeInstances.Length` per hole before == after,
   and each instance's `prototypeIndex` still maps to the same prefab name (write a tiny Editor
   check, quote the table).
3. Delete from disk (not from the build — they never built): `Assets/Packs/PBR Bridge/HDRP/HDRPversion.unitypackage`
   (215 MB), `Assets/Scenes/Original~/**/ReflectionProbe-*.exr` (86 MB). Anything else under
   `Assets/Packs/**` that is a `.unitypackage`, demo scene, or `~` folder: list it, delete it.
4. Gate: capture Hole 1, 6 and 12 from the tee and from 20 m behind the green, before/after, same
   camera transform (the smoke capture rig or `PhysicsLabController`'s camera), and include a
   2× crop of a spruce at ~15 m — Cesar judges the crops. `sharedassets*.resS` total quoted
   before/after.

### Phase 2 — `HoleData` (389 MB on disk, ~400 MB in `resources.assets`)

Keep the **paths** (`Resources.Load("HoleData/{slug}/Hole_NN/…")` in `GreenTopology`,
`MapViewController`, `PhysicsLabController`, `TestGreenLabSetup`, the scene-referenced
`heightmapAsset` TextAsset in `HeightProvider`); change the **bytes**.

1. **Heightmap → `GHM2`**, lossless: same 36-byte header with `version = 2`, `format = 2`, then a
   Deflate stream (`System.IO.Compression.DeflateStream`, .NET Standard 2.1 is the project's API
   level — confirm on IL2CPP in the Editor build target before writing a line of format code)
   of the heights as **row-major deltas** (`h[i] - h[i-1]` within a row, first column raw), int32
   little-endian. Terrain heights are smooth, so deltas are tiny and Deflate eats them; expect
   16.8 MB → 1–3 MB per hole. `HeightmapLoader.LoadFromBytes` reads both `GHM1` and `GHM2`
   (the `_test/TestGreen` fixture and every test that writes `GHM1` keep working); the loaded
   `HeightmapData` is constructed from the **same** `int[]`. `PhysicsHeightmapBaker` writes `GHM2`
   from now on.
2. **One-shot converter** (`Tools ▸ Golfin ▸ Build Size ▸ Convert HoleData`): for each
   `heightmap.bytes`, decode `GHM1` → encode `GHM2` → decode `GHM2` → assert `SequenceEqual` on
   the `int[]` and header fields → overwrite. Print a table: hole, bytes before, bytes after,
   SHA-256 of the decoded `int[]` before/after (must match). That table goes in the report.
3. **`zones.json` → `zones.bytes`**: gzip of the **minified** JSON (`JsonUtility`/Newtonsoft
   `Formatting.None` round-trip — parse the pretty JSON, re-serialize compact, gzip). Loaders
   (`ZoneData` / `BakedZoneClassifier` entry point, `MapViewController.LoadObBounds…`,
   `PhysicsLabController`, `TestGreenLabSetup`, `HoleMapCalibration` editor) call one
   `HoleDataIO.LoadZones(courseSlug, holeId)` that tries `zones` (`.bytes`, gzip) then falls back
   to the old `zones` (`.json`). Parity: the parsed `ZoneData` before/after must be **equal** —
   write the equality as a test (polygon count, every vertex, every surface enum name, every
   world-bound), run it over all 18 holes + `_test`. The `SurfaceMarker`/importer that writes
   `zones.json` writes `zones.bytes` from now on (keep a `--pretty` Editor toggle for humans).
4. `green.json` (60 KB) and `tree_obstacles.csv` (75 KB) stay as they are — not worth a format.
5. **Physics parity gate** (the one that matters): run `Validate All Holes`, the surface audits,
   `RealHoleTerrainTests`, `BakedPivotRegressionTests`, `MapViewAimingTests`, `GreenTopologyTests`
   and the physics changelog regression set before and after Phase 2 — identical pass counts and
   identical bake hashes. Then the smoke bot (`SmokeRunner2e/2f` presets) on Hole 1 and Hole 6:
   the AtRest positions logged before/after must be **bit-identical** (`fp` values, not "close").
6. **Load-time gate**: decoding `GHM2` (Deflate of ~16 MB → `int[]`) and gunzipping `zones` happens
   inside the existing hole-load (behind the loading screen), never on the shell. Measure hole-load
   wall time Hole 1 + Hole 6, 3 runs each, before/after (the K13 timing hooks / `HoleLoad` log
   lines): after must be ≤ before + 100 ms. Expect it to be faster — reading 2 MB from flash beats
   reading 17 MB, and minified JSON parses faster than pretty — but prove it. If Deflate on the
   main thread shows up, decode on a worker (`Task.Run`) and hand the `int[]` back; do not
   change when the data is needed.
7. `Resources/Clubs` (115 MB source, 292 PNGs): iPhone override max **512**, ASTC 6x6, in one
   sweep (these are shop/inventory cards; capture the Inventory grid + one detail card
   before/after at device resolution). Do NOT atlas them in this task — the loader is by name.

### Phase 3 — the 93 `compression: None` textures

The list is `grep -l "textureCompression: 0" -r Assets --include=*.meta` minus anything that is
a render-target, a LUT, a heightmap PNG or a font atlas (say which you excluded and why).
Iphone override → ASTC 6x6 for UI, ASTC 4x4 where a before/after capture shows banding in a
gradient (`S_DailyPillGlow`, `S_Top_Area` are the likely ones), **max 2048**;
`S_SocialPillBordered.png` (2680×600 for a pill) additionally gets max 1024. Rule 21
`UIFidelityLinter` must stay green. Capture Home, Account, Gacha banner, Daily pill before/after.

### Phase 4 — TMP fonts

`NotoSansJP-VariableFont_wght.ttf` (9.1 MB) ships because the TMP asset is **Dynamic**. Generate a
**static** atlas from the union of every glyph in the JA columns of the localization CSVs
(`Assets/Resources/Texts/*` or wherever `LocalizationLoader` reads — cite the path) plus ASCII,
digits, punctuation, the currency/× characters the UI uses, and **the full Hiragana + Katakana
ranges** (display names are user-typed). Atlas 2048², SDF, padding as today. The TMP font asset
keeps its **name and GUID** (so no prefab changes) and switches `m_AtlasPopulationMode` to Static
with the source font reference cleared. Rubik ×3 stay dynamic (0.9 MB, not worth it). Gate: the
`UIFidelityLinter` JA pass + a Japanese-locale capture of Home, Settings, Shop, the result modal
and the Golf Profile screen with a name containing a kanji outside the CSV (e.g. `齋藤`) — that
name must render via **fallback**, not tofu: keep a small **dynamic fallback** TMP asset (the
same TTF, `m_AtlasPopulationMode` Dynamic, atlas 512) listed in the static asset's
`fallbackFontAssetTable` — the TTF still ships in that case, so the saving is the 2.2 MB runtime
atlas churn and the boot rebake, not the 9 MB.

**Option (c) — subset the TTF itself (added 2026-09-04).** `NotoSansJP-VariableFont_wght.ttf` is a
variable font: 17,103 glyphs, `wght` axis 100–900 (fontTools on the Mac). Instance it at the ONE
weight the game renders today and subset it to JIS X 0208 (levels 1+2) + Hiragana + Katakana +
Latin/ASCII/punctuation/currency + every glyph in the JA CSV columns, with `fontTools`
(`instancer` + `pyftsubset`, script under `Docs/Scripts/`, command quoted in the report). Overwrite
the `.ttf` bytes under the SAME file name and GUID; the TMP asset stays **Dynamic**, no atlas or
fallback change, user-typed names keep working (`齋` is JIS level 2 — test it). Expected 9.1 MB →
~2–3 MB. ⚠️ Parity trap: the font's `fvar` default instance is **wght 100**, so "the weight the game
renders today" must be MEASURED off the current atlas / a JA capture, not assumed to be 400 —
the before/after JA captures must be pixel-comparable for the same string.

**Decide by measuring**: report the shipped bytes for (a) keep dynamic as today, (b) static +
fallback, (c) subset TTF, with the JA capture pair for (b) and (c) and the honest TTF-ships /
does-not-ship line for each. Cesar picks. Do not pick for him.

### Phase 5 — TerrainData alphamaps (needs Cesar's A/B — do the measurement, NOT the change)

18 holes × ~30 MB of `TerrainData` in `sharedassetsN.assets`. Report per hole:
`heightmapResolution`, `alphamapResolution`, `baseMapResolution`, `detailResolution`, layer
count, `holesResolution`. If the alphamaps are 1024, render Hole 6 fairway + green from the tee at
512 in an **A/B capture pair** (Editor, same camera) and put both in `screenshots/` — Cesar
decides. **The heightmap stays 2049** (physics fidelity rule from the perf pass). No terrain is
modified in this task without his written "go" in STATUS.md.

## Acceptance (Implementer fills in `IMPLEMENTER_REPORT.md`; the reviewer re-derives every number)

- [ ] `reference/build_report_before.txt` + `data_before.txt` exist and `…_after.txt` from the
      final build; a table of the six buckets from the brief (resS / sharedassets / resources /
      levels / framework / metadata) before → after.
- [ ] Install footprint (Xcode ▸ Window ▸ Devices "Size" or `du -sh Payload/Golfin.app`) ≤ 1.0 GB;
      Payload-compressed (`unzip -lv` sum under `Payload/`) ≤ 350 MB; `.ipa` file size reported
      alongside. Every number says which measure it is. If a phase falls short, the number is
      reported, not rounded.
- [ ] Phase 0b: `reference/build_report_lz4hc.txt` + `data_lz4hc.txt` + load-time table exist;
      install / Payload-compressed / per-bucket deltas vs `_before` quoted; the lane's
      `BuildOptions` unchanged in the diff (no adoption without Cesar's "go" in STATUS.md).
- [ ] Phase 1: prototype table per hole (before/after count, instance count unchanged, index→name
      map unchanged); the six tee/green captures + spruce crops.
- [ ] Phase 2: converter table (18 + test holes; SHA-256 decoded-heights match); `ZoneData`
      equality test green over all holes; `HeightmapLoader` reads GHM1 and GHM2 (EditMode test
      with a synthetic 8×8 map through both); smoke-bot AtRest positions bit-identical before/after
      on Hole 1 and Hole 6 (quote the `fp` values); all physics test suites same pass count;
      `Validate All Holes` green.
- [ ] Phase 2 load time: Hole 1 + Hole 6 hole-load wall time, 3 runs before/after, table; after ≤ before + 100 ms; shell boot time unchanged.
- [ ] Phase 3: the exclusion list with reasons; before/after captures of the four screens; Rule 21
      linter green.
- [ ] Phase 4: glyph-set source cited; JA captures incl. the out-of-CSV name for (b) and (c);
      shipped-bytes + TTF ships / does-not-ship line for (a)/(b)/(c); the rendered weight measured,
      not assumed; verdict left to Cesar.
- [ ] Phase 5: the per-hole terrain resolution table + the Hole 6 A/B pair; **no terrain edited**.
- [ ] Disk: `HDRPversion.unitypackage`, `Original~` EXRs and any other listed dead weight
      deleted; `git status` shows only intended files (no re-serialized scenes — if a scene
      re-serializes from opening it, revert it; Phase 1 prototype edits are the only allowed
      scene/terrain-asset diffs and are listed by path).
- [ ] The standalone build (`iOS-Standalone`) still builds after Phase 2 (its preprocessor moves
      `HoleData` out; the new file names must not break the sentinel/restore) — one build, size
      quoted.
- [ ] No behaviour change in any screen: `UIFidelityLinter` green, EditMode suite same count,
      the device pass §Home/§Play rows in `Docs/DEVICE_PASS_CONTENT_PIPELINE.md` re-run by Cesar
      on the new build (flag them).

## Out of scope

Addressables / on-demand hole download (a later task if the store size still hurts); instanced
tree rendering from `standalone_trees.csv` (perf task, not size); club-art sprite atlasing;
touching the terrain heightmap resolution; the standalone app (round 2 of `gps_standalone_shell`);
leaving `Symbols/` out of the .ipa (fastlane export option — backlog); switching the build lane to
LZ4HC (Phase 0b measures it; adoption is a separate decision).

## Reference

- `Docs/BUILD_SIZE_AUDIT.md` — the numbers and where they came from.
- `Assets/Scripts/Physics/Runtime/HeightmapLoader.cs` — `GHM1` (int32 Q16.16), the format to extend.
- `Assets/Scripts/Editor/CourseImporter/PhysicsHeightmapBaker.cs` — writes it.
- Consumers: `HeightProvider` (scene TextAsset), `BakedHeightProvider`, `GreenTopology.LoadFromResources`,
  `MapViewController` (~L1391 zones), `PhysicsLabController` (~L2089/2107), `TestGreenLabSetup`,
  `HoleMapCalibration`, `MissionStartAreaBaker`, `GreenTopologyEditor`; tests `RealHoleTerrainTests`,
  `BakedPivotRegressionTests`, `MapViewAimingTests`, `GreenTopologyTests` (writes `HoleData/_test/`).
- `Docs/PERF_OPTIMIZATION_PLAN.md` — the heightmap-stays-2049 rule.
