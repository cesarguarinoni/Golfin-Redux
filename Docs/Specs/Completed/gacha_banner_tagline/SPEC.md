# SPEC — `gacha_banner_tagline`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. `STATUS.md` tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`.

## Goal

The gacha card draws the title, countdown, costs and the two guarantee lines over the banner
artwork — but nothing else. `STANDARD CLUB 1` got away with that because its artwork **bakes**
two lines of English selling copy into the PNG ("GET Drivers, Woods, Irons" and "CHANCE TO GET
LEGENDARY GEAR!"). The 52 weekly banners cannot: `gacha_admin_catalogs` §5.2 decision 7 forbids
text in the art, because baked English would sit under a Japanese title in the JA build.

The result is that every weekly banner ships with no selling line at all. `taglineEn`/`taglineJa`
already exist in the CSV, the admin and the parser — `GachaBannerModel.cs` says they are "still
parsed into nothing — the card is TITLE ONLY (Cesar, 2026-08-31)".

This task renders those two lines as localized UI over the art, in the two designed treatments
the old artwork used: a brand-coloured **ribbon** under the countdown, and a soft-edged navy
**hook band** lower over the art. It adds the second string pair the design needs, and it moves
the per-week copy out of a hardcoded literal in the generator and into the rotation plan.

## Architect defaults (Cesar did not decide these — flag them in the report if wrong)

1. **Two new columns (`hookEn`/`hookJa`) rather than a delimiter inside `taglineEn`.** The admin
   renders one textbox per column; a `|`-separated field is a footgun for whoever edits 52 weeks.
2. **Accent word marker is `*…*`, not raw TMP rich text.** Operators type
   `3× RATE-UP ON *LEGENDARY* GEAR!`; the card converts the marked run to the accent colour.
   Raw `<color=#…>` in operator-edited data breaks the whole string when a tag is mistyped.
3. **Ribbon label auto-sizes 34–40 pt.** Measured worst case fits at 40 (see §5), but auto-size
   is the guard against a future week with longer copy.
4. Elements **hide** when their string is empty, rather than showing an empty plate.

## Reference

- **Figma page:** `Gacha` (`4049:6491`) in file `5gEAHjl6xAtW8iYY7NMvWd`
- **Frame:** `Gacha Card — Tagline v1 (wk_2026_38)` — `14280:33516`
- **Card node:** `Banner + Buttons` — `14280:33548`; art frame `Banner` — `14280:33549`
- **Ribbon:** `Tagline/Ribbon` `14281:33634`, label `14281:33635`
- **Hook band:** `Tagline/Hook` `14281:33636`, label `14281:33637`
- **Renders in `reference/`:**
  - `card_tagline_v1_wk202638.png` — the built frame, ground truth for visual diff
  - `art_wk202638_safe_areas.png` — the artwork the frame sits on, composed to the §6 dead zones
- **Placeholder content:** the countdown reads `ENDS IN: 1d 5h 25m 05 s` and the pity lines read
  `99 pulls` — both are existing mockup values in the source frame, not part of this task.

## 1. Data — two new columns

`content_rows.data` is a JSON blob. A column is added by adding the key to the CSV and the
consumers; rows published before it simply have no such key, and `import_content.py`'s
`drop_empty()` normalisation treats an absent key and an empty cell as the same fact (see its
docstring, and the 2026-08-27 art-URL columns that went in the same way). **No migration, and no
change in the `playlife` repo.**

### 1.1 `gacha_banners`

Add `hookEn`, `hookJa` after `taglineJa` in
`Assets/Resources/Data/gacha_banners.csv` and in the admin panel's column list
(`Tools/admin-dashboard/app/(panels)/gacha-banners/gacha-banners-panel.tsx` — add both to
`editorHiddenColumns` alongside `taglineEn`/`taglineJa`, since weekly rows are generated).

### 1.2 `rotations` — stop hardcoding the copy

`Tools/admin-dashboard/lib/rotation.ts:795` currently writes
`taglineEn: "Featured this week"` / `taglineJa: "今週のピックアップ"` as literals, so all 52
weeks get the same line.

- Add `taglineEn`, `taglineJa`, `hookEn`, `hookJa` to `ROTATION_COLUMNS` (line 113).
- Add the four to `ROTATION_DEFAULTS` with the current literals as the fallback values.
- In the `gacha_banners.push` block, read them off the rotation row the same way `nameEn` already
  does: `text(data.taglineEn).trim() || ROTATION_DEFAULTS.taglineEn`, and the same for the other
  three.
- Add the four columns to `Assets/Resources/Data/rotations.csv` and fill all 52 weekly rows from
  `Claude outputs/WeeklyBanners/weekly_taglines.csv` (delivered with this spec; keyed on
  `rotationId`).

A re-MATERIALIZE must keep carrying the row's own values — do not reintroduce a literal.

## 2. Client — parse

`Assets/Scripts/UI/Gacha/GachaBannerModel.cs`

- `GachaBannerEntry` gains `TaglineEn`, `TaglineJa`, `HookEn`, `HookJa` (all `string`, default
  `""`), parsed from the matching columns. The class comment that says taglines are "parsed into
  nothing" is now wrong — update it.

## 3. Client — render

`Assets/Scripts/UI/Gacha/GachaBannerCard.cs`

New serialized refs, wired by `GachaCarouselController.SetupCardRefs()` exactly as the existing
ones are:

| Field | Node |
|---|---|
| `_taglineRibbon` (`GameObject`) | `Banner/TaglineRibbon` |
| `_taglineLabel` (`TextMeshProUGUI`) | `Banner/TaglineRibbon/Label` |
| `_hookBand` (`GameObject`) | `Banner/TaglineHook` |
| `_hookLabel` (`TextMeshProUGUI`) | `Banner/TaglineHook/Label` |

In `Bind`:

1. Pick the language pair the same way the title already does (`_titleText`, line ~159) — do not
   introduce a second language check.
2. `SetActive(false)` on the container when the chosen string is empty or whitespace. An empty
   plate must never render.
3. Convert the accent marker before assigning: each `*…*` run becomes
   `<color=#FF2D9B>…</color>`; a stray unmatched `*` is stripped, never shown. Put this in one
   private static helper so both labels use it, and unit-test it (§7).
4. `\n` in the stored string is a hard line break (the hook is authored as two lines).

Nothing else in `Bind` changes. The card still decides nothing about whether it exists.

## 4. Prefab — `Assets/Resources/Prefabs/Gacha/GachaBannerCard.prefab`

Values below are the SERIALIZED ones read off the prefab and the Figma frame, not C# defaults.

Card is `882 × 1720`; `ArtImage` is `876 × 1424`, pivot `(0.5, 1)`, anchored `(0, 856)`.
`BannerTitle` is `749 × 70` at `(24, −24)`; `CountdownPill` `320 × 44` at `(24, −109)`;
`CostArea` at y `−1472`.

Both new nodes are children of the art area, anchored top-left, pivot `(0, 1)`:

| Node | Size | Anchored pos | Fill |
|---|---|---|---|
| `TaglineRibbon` | `882 × 64` | `(0, −189)` | horizontal gradient `#E4007F` → `#FF4FA3`, 96 % |
| `TaglineRibbon/Label` | inset 28 L/R | — | Rubik SemiBold 40, lh 48, ls −0.9, white, left |
| `TaglineHook` | `882 × 188` | `(0, −1035)` | horizontal gradient navy `#0B1B3A` a0 → a0.93 (16 %) → a0.93 (84 %) → a0 |
| `TaglineHook/Label` | `800` wide, centred | — | Rubik SemiBold 62, lh 72, ls −1.4, white; accent `#FF2D9B` |

### Fidelity table (Figma → Unity)

| Item | Figma | Unity | Note |
|---|---|---|---|
| Art frame width | 882 | 876 | Figma frame is the card width; the Unity `ArtImage` is inset 3 px each side. Build to 876 and centre. |
| Ribbon y | 189 | −189 | Figma y-down vs Unity anchored y-up from the art's top edge |
| Hook y | 1035 | −1035 | as above |
| Ribbon/hook width | 882 | 876 | same inset |
| Font | Rubik SemiBold | project TMP Rubik SemiBold asset | matches `BannerTitle` |

## 5. Copy budget — measured, not estimated

Ribbon inner width is `882 − 28 − 28 = 826 px`. Measured in Rubik SemiBold at 40 pt:

| String | Width |
|---|---|
| `GET Fairway THREADS Irons & A.Wedges` (longest EN of the 52) | 749 px |
| `PAR PERFECT アイアン＆Aウェッジが登場` (longest JA of the 52) | 761 px |
| `GET BogeyB Drivers & Woods` (wk_2026_38) | 539 px |

Both worst cases clear 826 px. The first draft of the copy named both brands on cross-brand weeks
and hit **1065 px** — that copy rule was changed (cross-brand weeks name club types only, the
title and artwork carry the brands). Keep the auto-size floor at 34 pt anyway.

Hook label budget is 800 px; longest line `DOUBLE LEGENDARY` measures 615 px at 62 pt.

## 6. Artwork dead zones — this supersedes the brief's "top 8 %"

Measured against the prefab and the built frame, the UI covers these bands of the **art**:

| Band | % of art height | What sits there |
|---|---|---|
| 0 – 18 % | top | title, countdown pill, RULES & RATES, and the ribbon |
| 72.7 – 86 % | hook band | the hook band plate |
| 91 – 100 % | bottom | pity + guarantee lines (no plate — they sit straight on the art) |

`Docs/Game Design/WEEKLY_BANNER_ART_BRIEF.md` §2 says "top edge … bottom ~15 %", and
`WEEKLY_BANNER_VARIATION_SYSTEM.md` said "top 8 %". **Both are wrong** and are corrected by this
table. All club heads must sit between **18 % and 72 %**; only shafts may cross the lower bands.
`reference/art_wk202638_safe_areas.png` is the first artwork built to it.

This is a documentation fix, not implementer work — it is recorded here so the numbers live in
one place.

## 7. Polish atoms

Per the standing rule, what this UI uses and where:

- **`ButtonPressFeedback`** — n/a, no new `Button`.
- **`ModalController`** — n/a, no dialog.
- **`UiMotion`** — deliberately **not** used on these two nodes. They are static composition
  inside a carousel card that already fades as a whole via its `CanvasGroup`; animating them
  independently would re-trigger on every rebind. Do not add `Pop`/`Rise` here.
- **`PendingSpend` / `UiSelection` / `GpsPaintMotion` / `ShimmerHost`** — n/a, no server call, no
  selection state, no fetched list, no visible wait.

## 8. Strings

These are **catalog columns, not `LocalizationText.csv` keys** — no new localization keys, and
nothing to retire.

Path: edit `rotations.csv` →
`python3 Tools/content/import_content.py --env-file … --catalogs rotations` (PLAN, read the
verdicts) → `--apply` → publish `rotations` from the admin → MATERIALIZE the affected weeks in the
Rotations panel → publish `gacha_banners`. If the plan reports CONFLICTS, stop and report; no
`--overwrite-dirty`.

All 52 EN + JA pairs are in `Claude outputs/WeeklyBanners/weekly_taglines.csv`
(`rotationId, nameEn, taglineEn, taglineJa, hookEn, hookJa`).

## 9. Acceptance

1. All 52 weekly `rotations` rows carry non-empty `taglineEn`, `taglineJa`, `hookEn`, `hookJa`;
   a MATERIALIZE copies them onto the banner row; a re-MATERIALIZE does not revert them to
   "Featured this week".
2. EN build shows the EN pair, JA build the JA pair, chosen by the same check the title uses.
3. A row with an empty `taglineEn`/`hookEn` renders no plate (container inactive), not an empty bar.
4. `*…*` renders in `#FF2D9B`; a literal `*` is never visible; an unmatched `*` is stripped.
   Covered by EditMode tests on the marker helper, including the unmatched and empty cases.
5. Ribbon label never overflows 826 px at any of the 52 strings — auto-size floor 34 pt.
6. Card visually diffs against `reference/card_tagline_v1_wk202638.png`.
7. The bottom 91 – 100 % of the art stays untouched by these two nodes.
8. `export_content.py --check` clean for `rotations` and `gacha_banners`; zero new hardcoded
   `.text` literals (quote the grep in the report).

## 10. Out of scope

- Editing the 52 weeks by hand in the admin — the generator supplies them from the plan.
- Any change to the no-text-in-artwork rule. It stands.
- Regenerating the other 51 artworks to §6's dead zones — filed as a deferral.
- Anything in the `playlife` repo.
- The `1 unpublished` change already sitting in the admin's `gacha_banners` before this task.
