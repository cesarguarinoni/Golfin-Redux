READY_FOR_ARCHITECT_REVIEW

# STATUS — `build_size_diet` (GAME track — Notion 2121)

**Current:** `READY_FOR_ARCHITECT_REVIEW` — Claude Code, 2026-09-04. Both gates PASS.
Report: `IMPLEMENTER_REPORT.md`. Evidence: `reference/`, `screenshots/`.

|  | before | after | gate | |
|---|---|---|---|---|
| **Install** (Payload uncompressed) | 1839.5 MiB | **1009.1 MiB** | ≤ 1024 | PASS, 14.9 MiB inside |
| **Payload-compressed** (download) | 555.4 MiB | **304.6 MiB** | ≤ 350 | PASS, 45.4 MiB inside |
| `.ipa` file | 678.3 MiB | **426.4 MiB** | reported | of which `Symbols/` is 121.2 MiB Apple strips |

−835.8 MiB install (−45%). **Measured on the shipped `.ipa`** — build 2658, archived and
uploaded to TestFlight 2026-09-04 (`reference/ipa_after.txt`), not only modelled: the local
model (`Docs/Scripts/measure_ios_data.sh`) predicted 1009.1 / 304.6 and the real archive came
back 1008.9 / 305.2.

**Physics parity: 24/24 shots bit-identical.** The smoke-bot AtRest gate was run on Hole 1 and
Hole 6 against BOTH datasets — 12 presets each, `fp.raw` Q32.32 longs compared with `==`, zero
differing values, matching terminal states and matching sample counts.
`reference/phase2_atrest_parity.txt`.

## Open on Cesar

1. **Phase 0b — LZ4HC adoption.** Measured on the PRE-diet tree: install −63%
   (1839.5 → 681.7 MiB) but Payload-compressed only −4% (555.4 → 531.7), because LZ4 output
   cannot be deflated again. Not adopted; the lane's `BuildOptions.None` is unchanged and
   visible in the diff. Both gates already pass without it — this is margin, not a fix.
   *Its load-time cost is a player-only property and was NOT measured; see §Not done.*
2. **Phase 4 — font verdict (a/b/c).** All three measured, nothing applied.
   (a) keep dynamic 8.71 MiB · (b) static atlas ≈ 25 MiB, **three times worse than today** ·
   (c) subset the TTF 2.56 MiB, covered glyphs byte-identical, rare kanji outside JIS X 0208
   become tofu. `reference/font_options.txt`, `screenshots/font_option_c_ja_pair.png`.
3. **Phase 5 — NOT recommended as specified.** Cesar asked how it interacts with the HoleGeo
   import and the baked physics. It is not a −120 MiB texture saving: `HoleGeoImporter`
   hardcodes `alphamapResolution = 1024` (so the change un-does itself on the next import) and
   `BakeZoneJsonTool` bit-packs the OB terrain layer straight out of `GetAlphamaps` into
   `ZoneData.obMask` — dropping to 512 puts 1.13 m of slop on Hole 1's out-of-bounds line.
   `reference/terrain_and_prototypes.txt`. No terrain and no importer were touched.
4. **Spruce / leaf crops.** Not needed as it turned out — the only vegetation that ships is
   already at 2048 and Phase 1 did not touch it. The 1024-leaf option is written down in
   `PackTextureBudget.LeafBudget` with its ~7.5 MiB, unapplied.

## Not done, and why — read this before signing off

- **No device pass.** Everything here is measured off a real archive; nothing needs a phone to
  be checked. Build 2658 IS on TestFlight if you want to open it.
- **The `.ipa` still packs `Symbols/`** (121.1 MiB zipped). Out of scope per the SPEC; it is
  why the `.ipa` FILE is reported and not gated.

## Log

| Date | State | Note |
|---|---|---|
| 2026-09-03 | `SPEC_READY` | 5 phases. |
| 2026-09-04 | `SPEC_READY` (amended) | install + Payload-compressed gates; Phase 0b; Phase 4c. |
| 2026-09-04 | `READY_FOR_ARCHITECT_REVIEW` | Phases 0, 0b, 1, 2, 2.7, 3 applied; 4 and 5 measured only. Both gates pass. |
| 2026-09-04 | `READY_FOR_ARCHITECT_REVIEW` | Cesar: "run the bot". Smoke-bot AtRest parity run — 24/24 shots bit-identical on Hole 1 + 6. Last outstanding gate closed. |
