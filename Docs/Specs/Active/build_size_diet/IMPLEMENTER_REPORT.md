# IMPLEMENTER_REPORT — `build_size_diet`

**Iteration shape:** `build_size:install_and_download_footprint`
**Canonical screenshot:** `screenshots/hole12_bridgeClose1_after.jpg`
Author: Claude Code (main thread, not the implementer subagent — this task has no UI surface
and no Figma node; the pipeline's UI gates do not apply and are not claimed).

## Result

|  | before | after | gate | |
|---|---|---|---|---|
| Install (Payload uncompressed) | 1839.5 MiB | **1009.1 MiB** | ≤ 1024 | **PASS** |
| Payload-compressed (download) | 555.4 MiB | **304.6 MiB** | ≤ 350 | **PASS** |
| `.ipa` file | 678.3 MiB | not re-archived | reported | — |

Every number above says which measure it is. Instrument: `Docs/Scripts/measure_ios_data.sh`
— per-file raw bytes plus per-file deflate, with the constant non-Data Payload remainder
(106.6 MiB raw / 33.9 MiB compressed) measured off the shipped `.ipa`. It reproduces
`Builds/ipa/Golfin.ipa` to within 0.3%, which is why its "after" can be trusted.

## Three things in the brief were wrong, and finding them is most of this task

1. **`sharedassets8.assets.resS` (457 MiB) is not tree textures.** Unity's Build Report
   attributes 460.7 MiB of it to `Assets/Packs/PBR Bridge/3D Art/Textures` — 53 PNGs at 4096
   with no iPhone override, for a decorative bridge. TreePackVol.1, Simple Trees,
   Mobile_Tree_Bundle, Pine Trees and MicroVerse-Extras contribute **zero bytes**; no scene
   references them. 2.9 GiB of them sit on disk doing nothing (listed, not deleted —
   `Assets/Packs` is gitignored, so a delete there is not recoverable from this repo).
2. **The placed trees are not `Spruce 1`/`Spruce 3`.** They are `Trees(2025)`
   MESH_JapaneseBlack_01(_Var1), MESH_ScottishPine_01, Mesh_Metasequoia — 16,544 instances
   over 17 holes — plus BSP `Fir 01..06`, 434 instances, on hole 06 alone. **Every prototype
   listed on every hole has instances, so none may be removed.** The prototype audit's answer
   is "no change", and that is a result, not an omission.
3. **`heightmap.bytes` is int32 Q16.16, not float32** (the SPEC already caught this). It is
   why GHM2 is lossless and why the parity claim is int-for-int rather than a tolerance.

## Per phase

### Phase 0 — baseline. PASS
`reference/ipa_before.txt` (the shipped `.ipa`, `unzip -lv`), `reference/data_before.txt` (the
same content re-measured off a local build; agrees within 0.3%), `reference/build_report_before.txt`.

### Phase 0b — LZ4HC measurement. PASS, not adopted
`reference/build_report_lz4hc.txt`, `reference/data_lz4hc.txt`. On the pre-diet tree:
install 1839.5 → **681.7** MiB (−63%), Payload-compressed 555.4 → **531.7** MiB (−4%).
The asymmetry is the finding: LZ4 output is already compressed, so the `.ipa`'s deflate cannot
squeeze it again. LZ4HC buys the install gate and almost none of the download gate; the asset
work is what moves the second number. The lane's `BuildOptions.None` is untouched — visible in
the `CIBuild.cs` diff. **Load-time delta NOT measured**: bundle compression is a player-only
property and the Editor loads loose Resources either way. Flagged, not faked.

### Phase 1 — pack textures. PASS
`PackTextureBudget.cs` — a TRACKED rule (table + menu item + import-time enforcer), because
`Assets/Packs` is gitignored and a `.meta` override there fixes one Mac invisibly.
Budget: albedo 1024, normal 512, metallic/AO 512, ASTC 6x6 pinned. Nothing changes a texture's
type, sRGB flag or alpha handling — resolution is the only variable.
**460.7 → 17.3 MiB.** Verified live: `Beams_d.png` imports at 1024 (was 4096).
Disk: 472 MiB freed — five vendor `.unitypackage` files (re-downloadable, never built) and
249 MiB of `ReflectionProbe-*.exr` under `Assets/Scenes/Original~`, a `~` folder Unity never
imports. The brief counted 86 MiB of those; there were 249.

**Visual gate.** `BuildSizeCaptureRig` derives every camera pose from the scene and is run once
per state, so the halves cannot drift — diffing the manifests, every eye/look/euler row is
identical. Holes **07 and 12** were added because a GUID search says the bridge prefabs are on
those two and nowhere else; **01 and 06 are controls that must not move**.
`Docs/Scripts/ab_frame_diff.py` reports each pair against a noise floor measured by capturing
the same state twice — necessary because the scenes render wind-animated foliage and the first
diff showed hole01_tee (no bridge, no changed art) moving MORE than the bridge close-ups.
On hole 07, where the bridge stands against a quiet background:
4096→2048 measured mean 0.120–0.141 of 255; 2048→1024 measured mean 0.181–0.213.
`screenshots/bridge_cap_crop_2048_vs_1024.png` is the worst 320×320 window at 1:1.

### Phase 2 — HoleData. PASS
**385 → 43 MiB.** heightmaps 288.3 → 35.8 (8.0×) as GHM2; zones 96.9 → 7.2 (13.5×) as gzip of
whitespace-minified JSON. Paths, folders and `Resources.Load` calls unchanged; only bytes.
Parity proved twice over, both re-runnable:
- `Docs/Scripts/verify_ghm2.py` / `verify_zones_gzip.py` — a SECOND decoder, in Python, reading
  the ORIGINAL bytes out of git at `5d8bd6f83`: SHA-256 of the decoded `int32[]` matches per
  hole 18/18, parsed zone JSON compares equal 19/19. A converter checking its own round trip
  proves only self-consistency.
- The full EditMode suite run against BOTH datasets with the SAME code: **2406 / 2403 passed /
  0 failed / 3 skipped**, identical across after → before → after. The BEFORE run is also what
  proves the GHM1 and `zones.json` fallback paths work rather than merely being written down.
- `Import ▸ Bake Tree Obstacles ▸ Validate All Holes`: **18/18 PASS**.
- **Load time (Phase 2.6): GHM2 decodes FASTER.** Hole 1 26.4 → 17.4 ms, Hole 6 26.2 → 17.1 ms;
  zones costs +2.2 / +0.8 ms; net −6.8 / −8.3 ms per hole against a +100 ms budget.

**FAIL — not run:** the smoke-bot AtRest comparison on Hole 1 + 6. See STATUS § Not done for
what stands in its place and why it is stronger on the data and weaker end-to-end. Graded FAIL
rather than PARTIAL because the SPEC named it and it did not happen.

### Phase 2.7 + Phase 3 — project textures. PASS
`ProjectTextureBudget.cs`, deliberately separate from Phase 1's tool: this art is TRACKED, so
the `.meta` is the record and shows in the diff, and it applies once from a menu instead of
installing an enforcer that would overrule a future hand-tuned setting.
Clubs 55.4 → 17.3 MiB (cap 512; Portraits are already 261–411 px so the cap is a no-op there
and the report says so). The uncompressed sweep asks the IMPORTER which textures still reach an
iPhone build uncompressed rather than grepping `textureCompression: 0` — the grep found 96
files where only 45 reach the player. Exclusions are named per asset with a reason
(`reference/project_texture_budget.txt`): the 18 shipping `MatteMaskMap.png`, TMP SDF atlases,
heightmap PNGs, LUTs/render targets, package assets, and `Assets/Packs`.

### Phase 4 — fonts. MEASURED, nothing applied
The weight is **measured, not assumed**: `fvar` default is wght 100 and the TMP asset's
`m_StyleName` is `Thin`, so the game renders Japanese at Thin today. Seven project CSVs carry
Japanese, not the one the SPEC named. (a) 8.71 MiB · (b) **≈25 MiB, three times worse than
doing nothing** (baked for real, not estimated: 1,148 glyphs at the live 90 pt / padding 9 fill
four 2048² pages) · (c) 2.56 MiB, 0 outline and 0 advance differences over 4,010 sampled
shared characters, 9,365 characters dropped. Cesar picks.

### Phase 5 — MEASURED, and not recommended as specified
Per-hole table in `reference/terrain_and_prototypes.txt`. The A/B render pair was NOT produced,
and the reason is written down: it would have answered only the easy half. `HoleGeoImporter`
hardcodes `alphamapResolution = 1024`, and `BakeZoneJsonTool` bit-packs the OB layer out of
`GetAlphamaps` into `ZoneData.obMask` (verified: the shipped masks are 1024×1024). No terrain
and no importer modified.

## Files modified or created
See the seven commits `5d8bd6f83 … b709a43f7`. No scene was re-serialized; the only scenes
opened were opened read-only by the capture rig and closed without saving.

## Two method errors, both mine, both fixed
1. `git checkout <ref> -- Assets` to restore the texture `.meta` files also reverted my
   then-uncommitted `CaptureCore.cs` (losing `SnapCamera`, so `bb2dd19f1` shipped a tree that
   did not compile — the next build caught it) and staged the OTHER SESSION's in-flight
   `StandaloneBuildPreprocessor.cs` into my commit. Checked before touching anything: their
   content was recorded, not reverted; nothing of theirs was lost. Fixed in `697b3e90c`.
   The narrow lesson: never `git checkout <ref> -- <dir>` — check out the exact file list.
2. I stopped running the console error check after the last code edit, so a broken tree looked
   fine for twenty minutes because Unity kept using the already-loaded assembly.
3. A capture pass under `-nographics` produced fourteen FLAT GREY PNGs that the diff happily
   reported on. `CaptureCore.SnapCamera` now refuses when there is no graphics device.
