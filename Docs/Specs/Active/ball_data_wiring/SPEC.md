# SPEC — `ball_data_wiring` (Balls.csv rows + rarity column + BALL_INFO texts + wind coefficient)

> **Authoritative spec for this task.** Implementer (Claude Code) reads this and ONLY this for the
> work definition. `STATUS.md` tracks pipeline state. Written by the `ball_art_and_stats` Cowork
> runner on 2026-08-31 (SPEC §6 / D5 of that task) after the art and the stat table were approved.

## Status

See `STATUS.md`. Starts at `SPEC_READY`.

## Goal

Take the ball catalog from 2 rows to 20 so every hand-made ball design the shot UI has shipped for
months becomes a real, owned, stat-bearing item. The art half is DONE and sitting uncommitted in the
working tree (18 × `Assets/Resources/Balls/Full/<Name>.png` + 2 thumbnail copies — see §1). This
task is the DATA half: the CSV rows, the new `rarity` column end-to-end (CSV → client parse → admin
panel), the 18 EN+JA `BALL_INFO_*` strings, the `BallWindCutPerPoint` raise Cesar approved, and the
importer/publish run that makes all of it visible to the admin and to the build.

## Decisions of record (Cesar, 2026-08-31)

1. **Ball rarity lives in `Balls.csv` as a `rarity` column.** Cesar: *"Csv. But of course, Balls
   should also be managed from the web admin like clubs and characters."* → §5 covers the admin.
2. **`BallWindCutPerPoint` 0.01 → 0.02.** +10 wind now buys 0.20 of the 0.30 `WindCutMax`.
3. **The stat table is APPROVED** — `Docs/Specs/Active/ball_art_and_stats/BALL_IDENTITY.md`
   (marked APPROVED 2026-08-31) is the number set. Do not retune; if a number looks wrong, stop and
   report, don't fix.
4. **Balls get a dedicated admin panel** (Cesar: *"I want the panel"*) — a `/balls` sidebar entry
   like Clubs; the balls tab leaves the Items panel. §5.1.
5. **`Assets/Resources/Physics/stats.csv` is RETIRED** (Cesar chose retire over wire) — the file
   and the never-called `LoadStatCoefficients()` are deleted; `StatCoefficients.Default` is the
   single source of truth. §4.2.

## 1. What already exists (verified against the repo 2026-08-31)

| Thing | State |
|---|---|
| `Assets/Resources/Balls/Full/` | **20 files** — 18 new, all 537×900 RGBA, 30px rounded corners. UNCOMMITTED (`??` in `git status`). Cowork never commits — §9 has the commit. |
| `Assets/Resources/Balls/Thumbnails/` | 22 files — the 2 missing copies (`S_Controls_Ball_GOLFINMK2.png`, `S_Controls_Ball_PUTTACE.png`) were added, UNCOMMITTED. Unity generates the `.meta` on import. |
| `Assets/Data/Balls.csv` | 2 rows, 14 columns, **no `rarity`**. Header: `id,name,brand,power,rebound,windResistance,roll,spin,thumbnailSprite,fullSprite,info,thumbnailUrl,fullUrl,isDefault` |
| `Assets/Localization/LocalizationText.csv` | `BALL_INFO_GOLFIN` (line 387) and `BALL_INFO_PUTT_ACE` (line 388) only. Header `key,English,Japanese`. |
| `balls` catalog | Already registered in `Tools/content/catalogs.py` (id column `id`), already exported/imported, already validated and editable in the admin — it rides as a tab inside the **Items** panel (`lib/registry.ts` line ~58 explains why: 15 rows didn't justify a sidebar entry). |
| `BallDataRuntime` (`Assets/Scripts/UI/Inventory/BallData.cs`) | No rarity field. |
| `BallDatabaseCSV.ParseRow` (`Assets/Scripts/UI/Inventory/BallDatabaseCSV.cs:176`) | Never reads `rarity`. |
| `StatCoefficients.Default` (`Assets/Scripts/Physics/Stats/StatCoefficients.cs:35`) | `BallWindCutPerPoint = fp.FromFloat(0.01f)`. |
| `Assets/Resources/Physics/stats.csv` | `ball_wind_cut_per_point,0.01` — **and `ball_rebound_per_point,0.01`, which is STALE**: Order 417 raised Rebound to 0.02 in `StatCoefficients.Default` but never touched the CSV. **Retired by this task (§4.2).** |
| `StatResolverTests.cs:211` | `Stats_BallWindCut_FractionCorrect` asserts +10 → **0.10**. Will need to become 0.20. |

### ⚠️ Finding: `stats.csv` is not what the game plays

`PhysicsConfigLoader.LoadStatCoefficients()` (the only reader of `Physics/stats.csv`) has **no
callers**. The live shot path passes `StatCoefficients.Default` straight in
(`ShotController.cs:625`, also `PhysicsLabController.cs:752`, `BotClubCalibrationHarness.cs:57`).
So `Default` is the truth and the CSV is documentation that has already drifted once. **Cesar's
decision (2026-08-31): retire it** — the file and the dead loader go, `Default` is the single
source of truth (§4.2).

## 2. `Assets/Data/Balls.csv` — 20 rows, new `rarity` column

**The complete target file is `reference/Balls.csv` in this folder.** Replace `Assets/Data/Balls.csv`
with it byte-for-byte (LF line endings, `csv.QUOTE_MINIMAL`, header + 20 rows). Then diff against
HEAD and confirm:

- The header gained exactly one column, `rarity`, positioned after `brand` (matches the
  `Clubs.csv` / `Items.csv` convention of rarity near the front).
- `ball_golfin` and `ball_putt_ace` are unchanged except for the new column (`Common` / `Rare` —
  the same tiers their existing gacha/shop listings already carry) — stats, sprites, `info`,
  `isDefault` byte-identical.
- 18 new rows, ids `ball_<snake_case>` per `ball_art_and_stats/SPEC.md` §7, `isDefault=false`,
  `thumbnailUrl`/`fullUrl` blank.
- `thumbnailSprite` for the 18 is the **existing file stem** `S_Controls_Ball_<TOKEN>` (per
  `ball_art_and_stats` §3.3 — smallest diff, keeps the hardcoded
  `Resources.Load<Sprite>("Balls/Thumbnails/S_Controls_Ball_GOLFIN")` in `BallButtonWidget`,
  `CentralBallWidget` and the two editor builders valid). `fullSprite` is PascalCase, no prefix,
  matching `Golfin` / `PuttAce` (`GF`, not `G&F`).
- `info` is the EN blurb (the same text as the EN half of the `BALL_INFO_*` row).

The stat lines, for the record (`power, rebound, windResistance, roll, spin` → net):

| id | rarity | stats | net |
|---|---|---|---|
| `ball_golfin` | Common | 0,0,0,0,0 | 0 |
| `ball_par_perfect` | Common | 0,+3,+3,−3,−3 | 0 |
| `ball_fyloe_soft` | Common | −3,−2,0,−3,+8 | 0 |
| `ball_ace_attire` | Common | +6,+2,−4,+2,−6 | 0 |
| `ball_birdie_v1` | Common | +3,0,+3,0,−6 | 0 |
| `ball_golfin_mk2` | Uncommon | +4,+3,0,+2,−6 | +3 |
| `ball_gf` | Uncommon | +2,+4,+4,−3,−4 | +3 |
| `ball_tifto` | Uncommon | +1,+6,0,+3,−7 | +3 |
| `ball_fairloft` | Uncommon | −2,−3,+5,−4,+7 | +3 |
| `ball_fyloe_aim` | Uncommon | 0,+1,+7,0,−5 | +3 |
| `ball_clover_pro` | Uncommon | +2,+2,+2,+2,−5 | +3 |
| `ball_golfinix` | Rare | +2,−2,+3,−6,+8 | +5 |
| `ball_klyro` | Rare | +1,+2,+7,−2,−3 | +5 |
| `ball_royal_swing` | Rare | +8,+3,0,+2,−8 | +5 |
| `ball_fairway_threads` | Rare | +4,+4,+2,+2,−7 | +5 |
| `ball_putt_ace` | Rare | +10,−6,0,+5,−4 | +5 |
| `ball_mireo` | Mythic | −3,−2,+4,−2,+10 | +7 |
| `ball_cirq` | Mythic | +6,+7,−4,+3,−5 | +7 |
| `ball_soralis` | Mythic | +3,+1,+10,0,−7 | +7 |
| `ball_shimmer_g` | Legendary | +7,+5,+5,+2,−10 | +9 |

## 3. `Assets/Localization/LocalizationText.csv` — 18 new `BALL_INFO_*` rows, EN **and** JA

**The 18 rows are `reference/texts_rows.csv` in this folder** (no header; `key,English,Japanese`;
already `QUOTE_MINIMAL`-quoted). Insert them **immediately after line 388 (`BALL_INFO_PUTT_ACE`)**
so the ball strings stay contiguous. Key = `BALL_INFO_` + id minus the `ball_` prefix, uppercased
(`ball_ace_attire` → `BALL_INFO_ACE_ATTIRE`, `ball_gf` → `BALL_INFO_GF`) — this is what
`BallDetailPanel.LocalizeBody` derives, so a key typo silently falls back to `template.info`.

Both halves ship in the same commit. `LocalizationTextTable.asset` regenerates on build; no manual
step. Do not touch the two existing `BALL_INFO_*` rows.

## 4. Client code

### 4.1 Rarity on the ball (`Golfin.Inventory`)

- `BallData.cs` — `BallDataRuntime` gains
  `public CharacterRarity rarity = CharacterRarity.Common;` next to `brand`. Same enum the club
  and character rows use (`ClubCsvRow.rarity`, `CharacterData`), so `RarityHelper` /
  `RarityStatCaps` keep working unchanged if anything downstream ever wants them.
- `BallDatabaseCSV.ParseRow` — add `rarity = ClubCsvParser.ParseRarity(f.Get("rarity", "Common")),`
  to the initializer. `ClubCsvParser.ParseRarity` (`ClubCsvParser.cs:257`) is already `public static`
  and already defaults unknown/blank to `Common`, which is exactly the behaviour wanted for a
  published `content_rows` row that predates the column. **Reuse it — do not add a second parser.**
- `PlayerBallData` / `InventoryCodec` / the save blob: **no change.** Rarity is template data; it is
  read from the template at runtime, never persisted per instance.
- **No UI change.** Rarity framing on the Balls screen is explicitly out of scope
  (`ball_art_and_stats` §8). The field exists so the next task can draw it.

### 4.2 `BallWindCutPerPoint` 0.01 → 0.02, and `stats.csv` is RETIRED (Cesar, 2026-08-31)

- `Assets/Scripts/Physics/Stats/StatCoefficients.cs:35` —
  `BallWindCutPerPoint = fp.FromFloat(0.02f),` with a comment in the house style of the line above
  it: `// raised from 0.01 (ball_data_wiring, 2026-08-31, Cesar): +10 Wind now buys 0.20 of the
  0.30 WindCutMax instead of 0.10 — wind was worth a third of its cap`.
- **Retire the dead CSV path.** Cesar chose "retire" over "wire" or "leave": `StatCoefficients.Default`
  is the single source of truth and the physics changelog is the tuning record.
  - Delete `Assets/Resources/Physics/stats.csv` (+ its `.meta`).
  - Delete `PhysicsConfigLoader.LoadStatCoefficients()` (`PhysicsConfigLoader.cs:337–~395`, the
    whole method incl. the `switch` of `*_per_point` keys). It has no callers — confirm with a
    grep before deleting and quote it in the report. Leave the rest of `PhysicsConfigLoader`
    (the terrain/ball config loaders) untouched.
  - Grep `Assets/` and `Docs/` for `stats.csv` / `Physics/stats` and fix any doc or test that
    still points at it (expected: comments only; if a test loads it, that test moves to
    `StatCoefficients.Default`).
  - Add a one-line note to `StatCoefficients.Default`'s doc comment: *"Single source of truth —
    the `Physics/stats.csv` mirror was retired 2026-08-31 (ball_data_wiring); it was never loaded
    by the shot path and had drifted (rebound 0.01 vs 0.02)."*
- `Assets/Scripts/Physics/Tests/StatResolverTests.cs:211` `Stats_BallWindCut_FractionCorrect` —
  expected 0.10 → **0.20**. Update the name/comment string too.
- Add one physics changelog entry (the `F-` series that `arrow_speed_retune` / `ball_roll_coefficient_retune`
  used) — NOTE: locate the changelog file by grepping for `ball_roll_coefficient_retune`; if the
  series has moved, say where in the report rather than guessing.

**Perceptibility check (acceptance, not a gate to start):** repeat the `ball_rebound_perceptibility`
method — deterministic sim, flat fairway, power 1.0, but with a fixed crosswind preset instead of
calm — and report the lateral/carry delta between Wind −10, 0 and +10 before and after the
coefficient change. Order 417's bar was 10 m of *distance*; wind has no agreed bar yet, so **report
the number, don't invent a pass threshold** — Cesar decides whether 0.02 is enough or 0.03 is next.

## 5. Admin dashboard (`Tools/admin-dashboard`) — balls carry rarity like clubs and characters

The `balls` catalog is already wired into `CatalogPanel` (today reached via a tab in the Items
panel; §5.1 moves it to its own panel). First make the new column a first-class citizen of the
catalog config — this is shared by whichever panel hosts it:

- `lib/contentValidate.ts:109` — `balls: ["id", "name", "brand"]` → add `"rarity"` to REQUIRED.
  The generic rule at `:374` ("rarity is one of the six, wherever the column exists") then
  validates it with no further code.
- `lib/contentData.ts:226` — `FILTERABLE.balls: ["brand"]` → `["brand", "rarity"]`.
- `lib/contentView.ts:190` — balls view: `columns` gains `"rarity"` after `"brand"`; `facets`
  becomes `[BRAND_FACET, RARITY_FACET]` (both constants already exist, `:123`/`:125`).
- `lib/__tests__/contentValidate.test.ts` — one new case: a balls row with a blank / bogus rarity
  fails; `Common` passes.
- `lib/i18n.ts` — only if a column label for `rarity` is missing from the balls tab; the key is
  shared with clubs so it almost certainly already exists. Player strings never go here.
- Publish validation for `balls` needs nothing new — the stat range checks at
  `contentValidate.ts:145` already cover the five stats.

### 5.1 Dedicated **Balls** sidebar panel (Cesar, 2026-08-31: "I want the panel")

Balls leave the Items panel and get their own sidebar entry, exactly the shape Clubs has:

- `app/(panels)/balls/page.tsx` — copy of `app/(panels)/clubs/page.tsx` with `title: "Balls — GOLFIN Admin"`,
  `force-dynamic`, rendering `<BallsPanel />`.
- `app/(panels)/balls/balls-panel.tsx` — copy of `clubs-panel.tsx`:
  `export function BallsPanel() { return <CatalogPanel catalog="balls" titleKey="bl.title" />; }`
  (`CatalogPanel` already knows the `balls` catalog — view config `contentView.ts:190`, art
  folders `:372`, art columns `:409`/`:449` all exist; nothing else to teach it).
- `lib/registry.ts` `PANELS` — add `{ id: "balls", title: "Balls", icon: "ball", route: "/balls" }`
  directly after the `clubs` entry. `PanelId` is derived from the `nav.*` dict keys, so the
  `nav.balls` key below is what makes `"balls"` a legal id — add the dict key first or the type
  check fails.
- `components/PanelIcon.tsx` + the `PanelIcon` union in `registry.ts` — add a `"ball"` glyph
  (a simple dimpled-circle SVG in the same stroke style as `club`; NOTE: check whether the icon
  set is inline SVG or a lucide import and follow whichever it is).
- `lib/i18n.ts` `DICT` — `"nav.balls": { en: "Balls", ja: "ボール" }` and
  `"bl.title": { en: "Balls", ja: "ボール" }` (both locales, ADMIN_DASHBOARD_OPS.md §3.4; player
  strings never go here).
- `app/(panels)/items/items-panel.tsx` — remove the `balls` tab from `TABS`, drop the balls branch
  from the `titleKey` ternary, and update the header comment ("Items and Bags — TWO catalogs…
  Balls moved to their own panel 2026-08-31 when the catalog went to 20 rows"). Retitle
  `"it.title"` to `{ en: "Items & Bags", ja: "アイテム・バッグ" }` and delete the now-unused
  `it.tab.balls` key (grep first — if `it.oneCatalogNote` mentions three catalogs, fix the count).
- `lib/registry.ts` line ~58 comment — rewrite: the "15 rows between them" justification now
  covers items + bags only.
- Anything that deep-links to the balls tab (grep `/items` + `balls` in `app/` and `lib/` — the
  gacha-pools and users inventory panels reference balls by catalog, check whether any of them
  build a link) → point at `/balls`.
- Users drawer / inventory tab: **no change** — they read the `balls` catalog by name, not by panel.

Acceptance: `/balls` renders the 20 rows with brand + rarity facets and the art columns; `/items`
shows two tabs; `npm test` + `npm run typecheck` (or the repo's equivalent) green; a screenshot of
each in the report.

## 6. The importer path — spelled out (`claude/WORKFLOW_NOTES.md`, "New text strings")

`balls` and `texts` are EXISTING catalogs, so this is an import, not a seed. **Never code-only,
never migration-only, never a hand-inserted `content_rows` row.** The new `rarity` key rides inside
`content_rows.data` (jsonb of strings) — **no migration**.

```
python3 Tools/content/import_content.py --env-file Tools/admin-dashboard/.env.development.local --catalogs balls,texts
```
1. Read the PLAN. Expected: `balls` — 18 NEW rows + 2 CHANGED (`ball_golfin`, `ball_putt_ace`
   gain `rarity`; `min_build` on those two must be reported as untouched); `texts` — 18 NEW,
   0 CHANGED. **If the plan reports CONFLICTS (an admin edit in flight), stop and report — no
   `--overwrite-dirty` on the implementer's own judgment.**
2. Re-run with `--apply`.
3. Publish `balls` and `texts` from the admin (Items panel → Balls tab; Texts panel).
4. `python3 Tools/content/export_content.py --env-file … --check` for `balls` and `texts` → clean.

## 7. Thumbnails — on-device check (from `ball_art_and_stats` §3.3)

The 18 new rows resolve `thumbnailSprite` to the 1000×1000 `S_Controls_Ball_*` files. The two wired
balls use 200×200 / 178×178 and the Balls-screen carousel and compare panel have **never been shown
a 1000×1000 sprite.** Verify on device (or at minimum in the Editor at device resolution) that they
render at the right size with no layout push. If they don't: downscale copies to 200×200 named
`<Name>.png` (`AceAttire.png`, …) into `Resources/Balls/Thumbnails/`, point `thumbnailSprite` at
those instead, re-run §6 for `balls`, and say so in the report. Do not rename the originals — the
shot UI's hardcoded `S_Controls_Ball_GOLFIN` load must keep working.

## 8. Out of scope (do NOT do these)

- Per-ball 3D models / in-world ball skins (none exist; one Golfin-skinned prefab for every ball).
- Balls for the seven club brands with no ball art.
- `gacha_pools.csv` / `shop_catalog.csv` listings for the new balls — economy task with its own
  pity/weight questions. (Their existing `rarity` columns stay; they are the LISTING's rarity.)
- Retuning Golfin or Putt Ace, or regenerating the Putt Ace full.
- Rarity framing / borders / colour on the Balls screen UI.
- Wiring `PhysicsConfigLoader.LoadStatCoefficients()` — it is deleted, not wired (§4.2).
- Any other admin panel restructuring beyond §5.1 (Items/Bags stay as they are).

## 9. Commit — includes the Cowork-generated art

Cowork never runs `git commit`. The following are in the working tree, uncommitted, and belong in
this task's first commit alongside the data changes:

```
Assets/Resources/Balls/Full/{AceAttire,BirdieV1,Cirq,CloverPro,Fairloft,FairwayThreads,FyloeAim,
  FyloeSoft,GF,GolfinIX,GolfinMK2,Klyro,MireO,ParPerfect,RoyalSwing,ShimmerG,Soralis,Tifto}.png  (+ .meta once Unity imports them)
Assets/Resources/Balls/Thumbnails/S_Controls_Ball_GOLFINMK2.png  (+ .meta)
Assets/Resources/Balls/Thumbnails/S_Controls_Ball_PUTTACE.png    (+ .meta)
Docs/Specs/Active/ball_art_and_stats/{BALL_IDENTITY.md,STATUS.md,reference/*}
Docs/Specs/Active/ball_data_wiring/*
Docs/TellCode.md, Docs/AI_CONTEXT.md
```
Suggested message: `feat(balls): 18 ball fulls + full 20-ball catalog — rarity column, BALL_INFO EN/JA, BallWindCutPerPoint 0.02 (ball_art_and_stats + ball_data_wiring)`.

Open the 18 fulls in the Editor once before committing so the `.meta` files exist and the sprite
import settings match `Golfin.png` (Sprite (2D and UI), same max size / compression — copy them,
don't guess).

## 10. Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item PASS/FAIL with a one-sentence justification citing what was measured.

- [ ] `Assets/Data/Balls.csv` is byte-identical to `reference/Balls.csv`; `git diff` shows the 2 existing rows changed ONLY by the new column.
- [ ] 18 `BALL_INFO_*` rows present, EN and JA both non-empty, keys match `BallDetailPanel.LocalizeBody`'s derivation for every id in `Balls.csv` (script it: derive the key from each id and assert the row exists).
- [ ] `BallDataRuntime.rarity` parses for all 20 (EditMode test: load the bundled CSV, assert the tier per §2 table; assert a row with no `rarity` column parses as `Common`).
- [ ] `BallWindCutPerPoint` is 0.02 in `StatCoefficients.Default`; `Assets/Resources/Physics/stats.csv` and `LoadStatCoefficients()` are GONE (grep for `stats.csv`, `Physics/stats`, `LoadStatCoefficients` quoted: zero hits outside Docs history); `Stats_BallWindCut_FractionCorrect` asserts 0.20 and passes; `Golfin.Physics.Tests` full assembly sweep, not a filtered run.
- [ ] Perceptibility numbers reported per §4.2 (no threshold invented).
- [ ] Admin: `/balls` is its own sidebar panel showing the 20 rows with brand + rarity facets and art columns; `/items` has two tabs (Items, Bags) and no dead `balls` strings; a row with a bogus rarity fails validation; `npm test` and the typecheck green; screenshots of both panels in the report.
- [ ] Importer: PLAN verdicts quoted in the report (18 NEW + 2 CHANGED for `balls`, 18 NEW for `texts`, `min_build` untouched), `--apply` run, both catalogs published, `export_content.py --check` clean for `balls` and `texts`.
- [ ] All 20 balls resolve BOTH sprites in the Editor with **zero `ContentSpriteGuard` vetoes** in the console; the Balls screen carousel shows 20 entries; the detail panel shows the full image and the localized blurb (EN and JA) for a new ball.
- [ ] §7 thumbnail check done on device or at device resolution; outcome stated.
- [ ] Zero new hardcoded player-facing `.text` literals (grep quoted in the report).
- [ ] Unity Console has no errors related to this task.
- [ ] Spec deviations (if any) flagged at the bottom of the report with justification.

## 11. Files this task touches

- `Assets/Data/Balls.csv` — replaced with `reference/Balls.csv`
- `Assets/Localization/LocalizationText.csv` — +18 rows after line 388
- `Assets/Scripts/UI/Inventory/BallData.cs` — `rarity` field
- `Assets/Scripts/UI/Inventory/BallDatabaseCSV.cs` — `ParseRow` reads `rarity`
- `Assets/Scripts/Physics/Stats/StatCoefficients.cs` — wind 0.02
- `Assets/Resources/Physics/stats.csv` (+ `.meta`) — DELETED
- `Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs` — `LoadStatCoefficients()` removed
- `Assets/Scripts/Physics/Tests/StatResolverTests.cs` — expectation 0.20
- (physics changelog file — locate per §4.2)
- `Tools/admin-dashboard/lib/{contentValidate,contentData,contentView,registry,i18n}.ts` (+ validation test)
- `Tools/admin-dashboard/app/(panels)/balls/{page,balls-panel}.tsx` — NEW
- `Tools/admin-dashboard/app/(panels)/items/items-panel.tsx` — balls tab removed
- `Tools/admin-dashboard/components/PanelIcon.tsx` — `ball` glyph
- new EditMode test for ball rarity parsing (place beside the existing `BallDatabaseCSV` tests — NOTE: grep for them; if none exist, put it in the inventory test assembly and say so)
- the uncommitted art listed in §9

## 12. Smoke evidence

Editor: Balls screen with 20 entries, one new ball opened (full image + JA blurb), console clean.
Admin: screenshot of the new `/balls` panel with the rarity facet active, and of `/items` with two tabs. Terminal: importer PLAN + `--check`
output pasted. Physics: the perceptibility table.
