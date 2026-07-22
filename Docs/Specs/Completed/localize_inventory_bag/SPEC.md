# SPEC — `localize_inventory_bag`

> **Authoritative spec.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state.

## Status

See `STATUS.md`. Current: `SPEC_READY`.

## Goal

**Batch 2 of the game-wide localization sweep** (batch 1 = `localize_persistent_home_pilot`, DONE). Convert the genuinely user-facing copy in the **`Inventory/Bag`** group to the localization system, applying the **code-path-first recipe** the pilot validated. This is the first *full-size* batch — the recipe hardens here before the 100–280-row groups.

Scoped from `Docs/Reports/localization_audit_2026-07-22.md` (group `Inventory/Bag`, 62 audit rows). **Per the pilot, the audit's 62 is mostly noise** — the real actionable set is ~15 rows. Triage is a first-class deliverable, not overhead.

## Pilot lessons this batch MUST apply

1. **Code-path-first.** A label written from a controller (`.text = …` in a MonoBehaviour lifecycle/data method) is localized at the **code site** with `LocalizationManager.Get("KEY")`, NOT with a binder. A static prefab label that nothing overwrites gets a `LocalizedText` binder.
2. **Verify the live surface before binding.** A binder only works if the on-screen text is a real instance of the prefab you bind. **The Inventory cards ARE instantiated from these prefabs at runtime** (card lists/carousels), so binders on the card prefabs DO drive the live UI — unlike Home (which was a scene GO). Confirm this per prefab (instantiated by an inventory controller) before relying on a binder; if any target turns out to be a disconnected scene GO, bind the scene GO or convert the code site instead, and say so.
3. **Never bind a runtime-overwritten label.** Club/ball names, distances, levels, durability, and counts are set at runtime by the card's `Initialize`/bind method — binding them makes the binder fight the runtime write. SKIP them.
4. **Editor/Archive builder scripts are not shipping code.** `Assets/Scripts/**/Editor/*Builder.cs` and `Assets/Scripts/Editor/Archive/*` run at *edit time* to construct prefabs; their `.text = "…"` literals are design-time scaffolding. **Do NOT convert them.** The built prefab's text is what ships — localize THAT (as static binder or via the runtime controller), not the builder.
5. **Dedup / reuse first.** If an existing CSV key already carries identical English, reuse it (do not mint a new key). Most of this batch's real labels already have keys.

## Triage — starting classification (implementer VERIFIES each against the live prefab/code)

### CONVERT — static labels → `LocalizedText` binder on the card prefab (reuse existing keys)

These are fixed button/label text on the shared inventory card prefabs, not overwritten at runtime. Verify each is static (read the card controller — it must NOT assign this label's `.text`), then bind via `LocalizationEditorHelper.AddLocalizedText`. **All reuse existing keys — add NO new CSV rows for these:**

| Label | Key (exists) | Appears on |
|---|---|---|
| `LEVEL UP` | `ROSTER_LEVEL_UP` | BagClubCard, BagSwapClubCard, ItemUseClubCard, ItemUseClubCardGlowup |
| `REPAIR` | `CLUB_REPAIR` | BagClubCard, BagSwapClubCard, ItemUseClubCard, ItemUseClubCardGlowup |
| `SWAP` | `ROSTER_SWAP` | BagClubCard, BagSwapClubCard |
| `EQUIP CLUB` | `BAG_EQUIP_CLUB` | BagEmptyClubCard |
| `LOCKED` | `BAG_LOCKED` | BagSlotLockedPrefab |
| `USE REPAIR KIT` | `ITEM_USE_REPAIR_KIT` | ItemUseClubCard, ItemUseClubCardGlowup |

### CONVERT — static labels needing a NEW key (verify static first)

| Label | Proposed key | Appears on | Note |
|---|---|---|---|
| `EMPTY` | `BAG_EMPTY` | BagEmptyClubCard, BallThumbnailEmptyCard | new; check no existing "EMPTY" key to reuse first |
| `DIST` | `CLUB_DIST` | ItemUseClubCard | short "distance" column header; verify static (the `150 yd` VALUE next to it is dynamic — skip that) |

### CONVERT — genuine runtime code strings → `Get()` (NOT editor builders)

| Literal | File | Proposed key | Note |
|---|---|---|---|
| `SHOOT` | `Assets/Scripts/Gameplay/UI/ShotUI/ClubButtonWidget.cs` | `SHOT_SHOOT` (or reuse if an existing shoot/fire key matches) | real runtime button label; verify it's the shipped shoot button, dedup against existing keys |

Verify **`Assets/Scripts/UI/Inventory/ItemDetailPanel.cs` "GOLFIN"** in place: if it's a real displayed label, convert to `Get()` with a suitable key; if it's a brand watermark / debug placeholder, SKIP and document. Judgement call — record the verdict.

### DO NOT CONVERT — audit misclassifications (document each in `## Triage findings`, touch nothing)

- **Runtime-overwritten placeholders** on cards: `Test`, `MIREO`, `PUTT-ACE`, `DRIVER\nG&F`, `FULL`, club/ball names, `150 yd` / `250 yd` distances, `Lv 1`/`Lv10`, `75/100`/`/119` durability, `x99`/`x10` counts, `50`, `R` rarity glyph — all set by the card's runtime bind method. (Confirm by finding the controller assignment.)
- **All `Assets/Scripts/**/Editor/*Builder.cs` and `Assets/Scripts/Editor/Archive/*` rows** — `ClubDetailPanelBuilder.cs`, `BallCompareBuilder.cs`, `ClubCompareRightPanelBuilder.cs`, `InventoryScreenBuilder.cs`, `ItemUseClubCardBuilder.cs`, `ItemUseModalBuilder.cs`. Edit-time scaffolding, not shipping code. (Their *output* prefabs are covered by the binder rows above where the label is static.)
- **Whitespace / dashes / zeros:** `" "`, `"-"`, `"0"` (ClubCompareController, ClubDetailPanel, ClubLevelUpModalController, BallCompareController, BallDetailPanel) — dynamic/placeholder, not copy.
- Note: `TAP ON ANY OTHER CLUB TO COMPARE STATS` already has key `CLUB_COMPARE_EMPTY_PROMPT` and is emitted by an **editor builder** — the built prefab/label is what needs the binder if it's static; verify where the live compare-empty prompt actually renders and bind THAT if static, else leave to a later pass and document. Same for the ball variant (`BallCompareBuilder`).

If verification flips any row (a "CONVERT" label is actually runtime-set, or a "skip" is actually static copy), follow the evidence and document the flip.

## JP policy (same as pilot)

- Reused keys keep their existing JP — untouched.
- New keys (`BAG_EMPTY` if needed, `CLUB_DIST`, `SHOT_SHOOT`, any others) get `English` = literal, `Japanese` = the English text + ` [JP-TODO]` marker. Do NOT invent Japanese. JP now renders via the Noto TMP global fallback wired in batch 1 (`4846d78d3`).

## Acceptance checklist (Implementer fills `IMPLEMENTER_REPORT.md`)

- [ ] **Triage findings** section: every one of the 62 audit rows given a verdict — CONVERTED (how) / SKIPPED (why, which misclassification bucket). This is the primary deliverable.
- [ ] **Binders:** each converted static label carries a `LocalizedText` (via `AddLocalizedText`) bound to the correct key; live component `key` read back and quoted. Confirm the card prefab is instantiated at runtime by an inventory controller (finding #1) — cite the instantiation site.
- [ ] **Code path:** `SHOOT` (and `GOLFIN` if converted) replaced with `Get()`; show the diff; verify the key exists in the CSV.
- [ ] **CSV:** only the genuinely-new keys added (EN + `[JP-TODO]`); every reuse row confirms the key already existed (no duplicate minting); importer re-run; report the new key count and confirm no duplicate key.
- [ ] **EN unchanged:** capture the Inventory/Bag screen(s) showing the converted cards (bag list + a club card + swap/item-use card) in EN at 1170×2532 via the real boot→inventory flow; labels read identically to before. Cite screenshots.
- [ ] **JP smoke:** set JP via `Tools/Localization/Language Debug`, re-capture; reused-key labels render their real JP, new-key labels render the `[JP-TODO]` placeholder, NO raw key (`ROSTER_LEVEL_UP` etc.) on screen. Cite screenshots.
- [ ] **Scope containment:** `git status --porcelain` — only the touched inventory card prefabs, `ClubButtonWidget.cs` (+ `ItemDetailPanel.cs` if converted), `LocalizationText.csv`, `LocalizationTextTable.asset` (+ task folder). NO scene mutation, NO edits to `Assets/Scripts/Physics/`, NO editor/archive builder edits. Quote the porcelain (pre-existing unrelated drift — Art .meta, Plugins/NuGet, Packages, NotoSansJP SDF, .mcp.json.bak — is NOT this task's; don't claim or stage it).
- [ ] Project compiles (assets-refresh + console-get-logs clean); no task-related console errors.
- [ ] Spec deviations flagged at the bottom.

## Not a Figma task

No Figma node — Rules 16/17/18/21 N/A. Visual gate: EN labels unchanged; JP renders translated/placeholder, never a raw key; no layout shift from attaching binders.

## Out of scope

- Any group other than `Inventory/Bag`.
- Runtime-overwritten values, editor/archive builders, whitespace/dash/number placeholders (report, don't touch).
- Inventing Japanese beyond `[JP-TODO]`.
- Any visual/layout change to inventory cards.
- Touching `Assets/Scripts/Physics/`, scenes, or `M_Splash*.mat`.

---
