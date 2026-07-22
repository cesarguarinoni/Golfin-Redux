# SELF_REVIEW — `localize_inventory_bag` — iter-2

**Reviewer:** golfin-self-reviewer
**When:** 2026-07-22 20:35 JST
**Verdict:** **PASS** → advance to `SELF_REVIEW_PASS`

Not a Figma task. Rules 16 (mesh-metrics), 17 (mesh-bake video), 18 (Figma fidelity), 21 (UI-lint) are **N/A** — this is a text-binding batch on prefabs that carry no Figma node reference. Visual gate per SPEC: EN unchanged, JP renders real translation / `[JP-TODO]` placeholder, never a raw key, no layout shift. Applied.

---

## Visual diff notes (Step 1 — pixels first, no spec)

**EN screenshot (`screenshots/en_bags_screen.jpg`):**
Top bar: R currency 67,100 (left), 10-count token (center), gear icon (right). Below: navy panel with tabs `CLUBS | BAGS | BALLS | ITEMS` (BAGS selected, gold). Row of 6 bag slots — two filled (green MIREO with red club head, teal GOLFIN with green head), then four navy locked slots labeled `LOCKED` in bold uppercase. Below, MIREO detail card: bag artwork left, description right ("Add any 8 clubs you want to take out to the field to your bag. Remember you always need at least 1 Driver and 1 Putter."). Below that, 8 club-card slots in 2 rows of 4:
- Row 1: DRIVER G&F Lv10 (250yd), WOOD G&F Lv10 (230yd), IRON MIREO Lv80 (180yd, green R-rarity), PUTTER GOLFINX Lv200 (30yd, purple S-rarity). Each has tiny `LEVEL UP` / `REPAIR` buttons at bottom of artwork panel and a `SWAP` bar under it.
- Row 2: P. WEDGE ROYAL SWING Lv160 (120yd, orange L-rarity) + `SWAP`, then three navy cards each labeled `EMPTY` with `EQUIP CLUB` bar beneath.
Gold `EQUIPPED` strip at bottom of card block. Nav bar: home / cards / center golf-ball tee / bag / person.

**JP screenshot (`screenshots/jp_bags_screen.jpg`):**
Identical composition — same 6 bag slots, same detail card, same 8 club slots in same positions with same colors and same club portraits. Changes vs EN:
- The four locked bag slots now read `ロック` (real katakana) instead of `LOCKED`.
- The three empty club-card slots read `EMPTY [JP-TODO]` (wrapped over 2 lines because the placeholder is longer than "EMPTY").
- The `EQUIP CLUB` bar under each empty card renders small Japanese text (`クラブを装備`).
- The tiny `LEVEL UP`/`REPAIR` buttons on filled club cards render tiny kana/kanji glyphs rather than English (too small to transcribe from the JPG, but the character shape is clearly non-Latin).
- Tab labels, currency, bag description prose, club names, distances, levels, stat numbers, `EQUIPPED` bottom badge, and `SWAP` buttons remain English — those are out of scope for this batch (runtime-set or covered by later batches).

**No layout shift** between EN and JP — every card, button, portrait, and text baseline is in the same pixel position. The `EMPTY [JP-TODO]` label wraps within the card without expanding the card. Binders did not disturb geometry.

**No raw key** (`ROSTER_LEVEL_UP`, `BAG_EMPTY`, `CLUB_DIST`, …) is visible anywhere on the JP screen.

---

## Step 2 — Reference comparison

No Figma reference exists for this task; the visual gate is EN-parity + JP-shows-translation-or-placeholder rather than a per-element node diff. The EN capture is functionally the reference — the JP capture matches its geometry exactly and swaps only the localized labels.

---

## Step 3 — Checklist walk

### 1. Scope is clean post-revert (verify FIRST)

```
$ git status --porcelain | grep -vE 'Art/|Plugins/NuGet|Packages/|NotoSansJP|mcp.json.bak|localize_inventory_bag/'
 M Assets/Localization/LocalizationText.csv
 M Assets/Localization/LocalizationTextTable.asset
 M Assets/Prefabs/UI/Inventory/BagClubCard.prefab
 M Assets/Prefabs/UI/Inventory/BagEmptyClubCard.prefab
 M Assets/Prefabs/UI/Inventory/BagSlotLockedPrefab.prefab
 M Assets/Prefabs/UI/Inventory/BagSwapClubCard.prefab
 M Assets/Prefabs/UI/Inventory/BallThumbnailEmptyCard.prefab
 M Assets/Prefabs/UI/Inventory/ItemUseClubCard.prefab
 M Assets/Prefabs/UI/Inventory/ItemUseClubCardGlowup.prefab
```

**PASS** — exactly the 7 inventory card prefabs + CSV + table. NO `.asmdef`, NO `ClubButtonWidget.cs`, NO `Golfin.Gameplay.UI.asmdef`, NO scene, NO Physics edit. Verified:

- `git diff HEAD -- Assets/Scripts/Gameplay/UI/ShotUI/ClubButtonWidget.cs` → empty (0 lines)
- `git diff HEAD -- Assets/Scripts/Gameplay/UI/ShotUI/Golfin.Gameplay.UI.asmdef` → empty (0 lines)
- `find Assets -name "Golfin.Localization*"` → no matches (asmdef file gone from disk, not just untracked)

### 2. Triage findings (primary deliverable)

**PASS** — the report groups all 234 CSV-form audit rows (the SPEC's 62 is a dedup'd markdown subset; deviation flagged and reasonable) into A-G with explicit verdicts:

- Group A: 133 rows across 8 `Assets/Prefabs/Original/` dead prefabs → SKIP (candidate-dead)
- Group B: DYNAMIC_PLACEHOLDER runtime-overwritten labels → SKIP
- Group C: STATIC_COPY → 13 binders CONVERTED (2 SPEC flips SWAP + USE REPAIR KIT correctly re-classified to SKIP because the code sites already call `Get()`)
- Group D: whitespace/dashes/zeros → SKIP
- Group E: editor/archive builders → SKIP (per SPEC)
- Group F: `GOLFIN` brand watermark → SKIP with reason
- Group G: `SHOOT` in `ClubButtonWidget.cs` → DEFERRED with the asmdef-boundary reason and a clean revert

Triage is honest — the SWAP and USE-REPAIR-KIT flips are documented with the code-site quote and line number; the SHOOT deferral cites the exact revert operations. Runtime-overwritten placeholders + editor/archive builders correctly SKIPPED per SPEC.

### 3. Binders

**PASS** — 13 read-backs verified via `PrefabUtility.LoadPrefabContents` + reflection on `LocalizedText.key`, 13/13 OK. Keys reused are real existing CSV rows (spot-checked directly):

```
ROSTER_LEVEL_UP,LEVEL UP,レベルアップ            (existing)
CLUB_REPAIR,REPAIR,修理                          (existing)
BAG_EQUIP_CLUB,EQUIP CLUB,クラブを装備           (existing)
BAG_LOCKED,Locked,ロック                         (existing)
ITEM_USE_REPAIR_KIT,USE REPAIR KIT,修理キットを使う (existing)
BAG_EMPTY,EMPTY,EMPTY [JP-TODO]                  (NEW)
CLUB_DIST,DIST,DIST [JP-TODO]                    (NEW)
```

Instantiation sites confirmed (`BagDetailPanel.cs`, `ClubDetailPanel.cs`, `InventoryScreenController.cs`) so binders on card prefabs drive the live UI.

**Prefab-diff sanity spot-check** — `BagSlotLockedPrefab.prefab` diff shows a new `MonoBehaviour` of `LocalizedText` (script GUID `82815e97506b3ee47a82fe099019729c`, `Assembly-CSharp::LocalizedText`) with `key: BAG_LOCKED`, attached to the existing GO. No other changes.

**Scene-mutation audit (Step 7):** all 7 prefabs — zero `m_IsActive`, `sizeDelta`, or `m_AnchoredPosition` lines in any diff:

```
BagClubCard:            0
BagSwapClubCard:        0
BagEmptyClubCard:       0
BallThumbnailEmptyCard: 0
ItemUseClubCard:        0
ItemUseClubCardGlowup:  0
```

Binders were added without touching geometry.

Note: CSV EN for `BAG_LOCKED` is title-case `Locked` but the screenshot renders `LOCKED` — this is a TMP text-transform / fontStyle uppercase on the label component, pre-existing, not a task-caused change. JP `ロック` renders correctly.

### 4. CSV

**PASS** — 2 new keys `BAG_EMPTY,EMPTY,EMPTY [JP-TODO]` and `CLUB_DIST,DIST,DIST [JP-TODO]` present. `grep SHOT_SHOOT` → nothing. Row count: `wc -l LocalizationText.csv` → 237 lines = 1 header + 236 data rows, matches importer log (`[Localization] CSV imported. Rows: 236`). `awk` dupe-check on column 1 → top-5 all count 1 (no duplicate keys). Reused-key rows untouched.

### 5. Screenshots

**PASS** — see Step 1 description above. EN labels render identical to pre-task English. JP capture shows:

- Real Japanese for the bound existing-key labels (`ロック`, `クラブを装備`; kanji/kana glyph shapes for LEVEL UP / REPAIR).
- `[JP-TODO]` placeholder for the two new keys (`EMPTY [JP-TODO]` wraps within the empty card).
- **No raw key strings** visible anywhere on the JP screen.
- Zero layout shift between EN and JP.

**Documented gaps (acceptable):**

- `SWAP` under club cards still reads English in the JP capture. `SWAP` is set via `BagDetailPanel.Initialize()` at runtime; the smoke switched language after Initialize, so the panel would need to be reopened to re-fetch. This is exactly why the row was SKIPPED (a binder would fight the runtime write). Production flow refreshes on re-open — no shipping bug.
- Bag description text ("Add any 8 clubs…"), tab labels, currency, club names, stats, distances, EQUIPPED badge — all out of scope for this batch.

Canonical screenshot long edge is 1731px (report), above the 900px Rule 14 floor.

### 6. Compile clean + baseline attribution

**PASS** — `LocalizationManager` remains in Assembly-CSharp (asmdef file gone), `Golfin.Gameplay.UI.asmdef` restored to HEAD, no task-related console errors after `assets-refresh (ForceSynchronousImport)` per report. `HEARTBEAT.log` carries an `=== iter-2 kickoff baseline 2026-07-22T11:14:04Z ===` block with HEAD SHA `2767f740…` and full DIRTY porcelain (including the iter-1 asmdef artifacts that were subsequently reverted).

---

## Bbox verification (Step 6)

**N/A** — no containment claim in this task. Binders modify text content only. Confirmed pixel-comparable geometry between EN and JP via visual overlay.

---

## Capture-helper compliance (Step 5)

Report cites screenshots taken via the ShellScene real-boot flow + 5-second settle; no reference to `ScreenCapture.CaptureScreenshot` or manual OS screenshot tools; output consistent with the sanctioned `CaptureHelper` path. No new `*Context.cs` files added under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/` in this batch, so the CaptureHelper maintenance protocol is a no-op here.

---

## Bottom line

iter-2 correctly reverts iter-1's out-of-scope asmdef restructure, keeps ONLY the inventory-card binder work in scope, produces an honest triage across all 234 CSV rows, and lands the visual gate (EN unchanged, JP renders real translation or `[JP-TODO]` placeholder, no raw key, no layout shift). All Step 1–7 checks pass; Rules 16/17/18/21 N/A (declared).

**Verdict:** PASS → set `STATUS.md` to `SELF_REVIEW_PASS`.
