# IMPLEMENTER_REPORT — `localize_inventory_bag`

**Iteration shape:** localization:batch-triage-binders

---

## Triage findings

The SPEC references 62 Inventory/Bag rows from `Docs/Reports/localization_audit_2026-07-22.md` (markdown). The raw CSV (`Docs/Reports/localization_audit_2026-07-22.csv`) grepped for `Inventory/Bag` returns 234 lines (some are multiline CSV continuations; actual logical records are approximately 220). The discrepancy is because the markdown showed a deduplicated/filtered view. All verdicts below cover the full CSV set.

### Group A — CANDIDATE_DEAD (`Assets/Prefabs/Original/`) — 133 rows across 8 dead prefabs — ALL SKIP

These prefabs live under `Assets/Prefabs/Original/` and have zero references in any live controller. Nothing instantiates them at runtime. Verdict: SKIP — CANDIDATE_DEAD.

| Dead prefab | Row count |
|---|---|
| `Assets/Prefabs/Original/Gameplay/Hud/BallSelectionPanel.prefab` | 22 |
| `Assets/Prefabs/Original/Gameplay/Hud/ClubSelectionv2ViewPanel.prefab` | 20 |
| `Assets/Prefabs/Original/Mainmenu/Elements/BallElement.prefab` | 3 |
| `Assets/Prefabs/Original/Mainmenu/Elements/BallInfoElement.prefab` | 13 |
| `Assets/Prefabs/Original/Mainmenu/Elements/ClubElement.prefab` | 4 |
| `Assets/Prefabs/Original/Mainmenu/Elements/ClubInfoElement.prefab` | 10 |
| `Assets/Prefabs/Original/Mainmenu/Elements/ItemInfoElement.prefab` | 3 |
| `Assets/Prefabs/Original/Mainmenu/Screens/ClubLevelUpScreenVariant.prefab` | 57 |

Reason: The SPEC states (DO NOT CONVERT) that dead/archived prefabs with zero runtime references are outside scope. No action taken on any of these rows.

### Group B — Active prefabs: DYNAMIC_PLACEHOLDER rows — ALL SKIP

These labels are overwritten at runtime by card initialize/bind methods (rarity glyph, level badges, stat numbers, club/ball names, distance values):

| Prefab | Labels | Reason |
|---|---|---|
| `BagClubCard.prefab` | R, Lv10, Test (NameText), 150 yd, stat nums | Runtime-set by card bind |
| `BagSwapClubCard.prefab` | R, Lv10, DRIVER\nG&F (NameText), 150 yd, stat nums | Runtime-set by card bind |
| `BagThumbnailCard.prefab` | MIREO (BagLabel), R, FULL | Runtime-set; bag name / slot state |
| `BagSlotPrefab.prefab` | MIREO, R, FULL | Runtime-set |
| `BallThumbnailCard.prefab` | PUTT-ACE, x99 | Runtime-set |
| `ItemThumbnailCard.prefab` | PUTT-ACE, x99 | Runtime-set |
| `ClubThumbnailCard.prefab` | R, Lv 1 | Runtime-set |
| `ItemUseClubCard.prefab` | R, Lv10, DRIVER\nG&F, stat nums, 150 yd dist value | Runtime-set |
| `ItemUseClubCardGlowup.prefab` | R, Lv10, DRIVER\nG&F, stat nums, 150 yd | Runtime-set |

### Group C — Active prefabs: STATIC_COPY — CONVERTED (via LocalizedText binder)

These are fixed button/label text on shared card prefabs not overwritten at runtime. Converted via `LocalizationEditorHelper.AddLocalizedText`.

| Prefab | Label | Key | Status |
|---|---|---|---|
| `BagClubCard.prefab` | LEVEL UP | `ROSTER_LEVEL_UP` | CONVERTED — binder added |
| `BagClubCard.prefab` | REPAIR | `CLUB_REPAIR` | CONVERTED — binder added |
| `BagSwapClubCard.prefab` | LEVEL UP | `ROSTER_LEVEL_UP` | CONVERTED — binder added |
| `BagSwapClubCard.prefab` | REPAIR | `CLUB_REPAIR` | CONVERTED — binder added |
| `BagEmptyClubCard.prefab` | EMPTY | `BAG_EMPTY` (NEW) | CONVERTED — binder added |
| `BagEmptyClubCard.prefab` | EQUIP CLUB | `BAG_EQUIP_CLUB` | CONVERTED — binder added |
| `BagSlotLockedPrefab.prefab` | LOCKED | `BAG_LOCKED` | CONVERTED — binder added |
| `BallThumbnailEmptyCard.prefab` | EMPTY | `BAG_EMPTY` (NEW) | CONVERTED — binder added |
| `ItemUseClubCard.prefab` | DIST | `CLUB_DIST` (NEW) | CONVERTED — binder added |
| `ItemUseClubCard.prefab` | LEVEL UP | `ROSTER_LEVEL_UP` | CONVERTED — binder added |
| `ItemUseClubCard.prefab` | REPAIR | `CLUB_REPAIR` | CONVERTED — binder added |
| `ItemUseClubCardGlowup.prefab` | LEVEL UP | `ROSTER_LEVEL_UP` | CONVERTED — binder added |
| `ItemUseClubCardGlowup.prefab` | REPAIR | `CLUB_REPAIR` | CONVERTED — binder added |

FLIP findings — SPEC said CONVERT but verification found already localized at code site:

| Prefab | Label | SPEC intent | Verification | Verdict |
|---|---|---|---|---|
| `BagClubCard.prefab`, `BagSwapClubCard.prefab` | SWAP | CONVERT (binder) | `BagDetailPanel.cs` line 119: `card.Initialize(…, LocalizationManager.Get("BAG_SWAP"), …)` — runtime assign overwrites any prefab text | SKIP — code site already localizes; binder would fight the runtime write |
| `ItemUseClubCard.prefab`, `ItemUseClubCardGlowup.prefab` | USE REPAIR KIT | CONVERT (binder) | `ItemUseClubCard.cs` line 139: `useRepairKitText.text = LocalizationManager.Get("ITEM_USE_REPAIR_KIT")` — runtime assign | SKIP — code site already localizes |

### Group D — Runtime scripts: CODE_DRIVEN (whitespace/dashes/zeros) — ALL SKIP

| File | Label | Reason |
|---|---|---|
| `ClubCompareController.cs` | `" "` | Whitespace placeholder — dynamic UI spacing |
| `ClubDetailPanel.cs` | `" "` | Whitespace placeholder |
| `ClubLevelUpModalController.cs` | `-` (x2) | Dash placeholder for unset values |
| `BallDetailPanel.cs` | `0` | Numeric placeholder |
| `BallCompareController.cs` | `0` | Numeric placeholder |

### Group E — Editor/Archive builders — ALL SKIP

| File | Labels | Reason |
|---|---|---|
| `Assets/Scripts/Editor/Archive/ClubDetailPanelBuilder.cs` | INFO, CLUB NAME, IN BAG 1, COMMON, Lv 1, /119, 75/100, DISTANCE, 250 yd | Edit-time scaffolding, NOT shipping code |
| `Assets/Scripts/UI/Inventory/Editor/ItemUseClubCardBuilder.cs` | R, Lv10, DRIVER\nG&F, DIST, 150 yd, USE REPAIR KIT, 50 | Edit-time scaffolding |
| `Assets/Scripts/UI/Inventory/Editor/InventoryScreenBuilder.cs` | INVENTORY | Edit-time scaffolding |
| `Assets/Scripts/UI/Inventory/Editor/BallCompareBuilder.cs` | TAP ON ANY OTHER BALL TO COMPARE STATS | Edit-time scaffolding |
| `Assets/Scripts/UI/Inventory/Editor/ClubCompareRightPanelBuilder.cs` | TAP ON ANY OTHER CLUB TO COMPARE STATS | Edit-time scaffolding |
| `Assets/Scripts/UI/Inventory/Editor/ItemUseModalBuilder.cs` | SELECT CLUB | Edit-time scaffolding |

### Group F — Runtime code: GOLFIN — SKIP (brand watermark)

`Assets/Scripts/UI/Inventory/ItemDetailPanel.cs` contains `"GOLFIN"` (1 row). This is the brand watermark used as a display label in the item detail panel, not user-facing gameplay copy. Verdict: SKIP — proper noun / brand watermark. Documented per SPEC "Judgement call — record the verdict."

### Group G — `ClubButtonWidget.cs` SHOOT — DEFERRED

`Assets/Scripts/Gameplay/UI/ShotUI/ClubButtonWidget.cs` line 34: this file is gameplay HUD code in the `Golfin.Gameplay.UI` named assembly. Localizing it requires a deliberate decision on how asmdef'd gameplay code reaches `LocalizationManager` (which currently lives in Assembly-CSharp, the global namespace). This is a cross-cutting foundational question for the entire localization sweep, not a decision to ride silently into a single Inventory/Bag batch. The audit tool grouped this row under `Inventory/Bag` by a path-name heuristic; it is not inventory screen code. **DEFERRED — flagged for a dedicated foundation task.** No change made to `ClubButtonWidget.cs` this batch. File restored to HEAD state.

---

## Deferred / architect decision needed

**Open question: asmdef boundary for `LocalizationManager.Get()` from named assemblies**

Currently `LocalizationManager` lives in the default Assembly-CSharp. Named asmdefs (`Golfin.Gameplay.UI`, `Golfin.Gameplay.Loop`, etc.) cannot reference Assembly-CSharp, so any `.cs` file in a named assembly cannot call `LocalizationManager.Get()` without a structural change.

The options are:
1. Move `LocalizationManager.cs` (and `LocalizedText.cs`) into a new named asmdef (e.g. `Golfin.Localization.asmdef` with `autoReferenced: true`), then add an explicit reference to that asmdef from any named assembly that needs localization.
2. Keep `LocalizationManager` in Assembly-CSharp, and require that all localization calls from named assemblies go through an intermediary interface/event.
3. A hybrid: expose only a static helper from an `autoReferenced` wrapper that delegates to the real manager.

This decision affects every named assembly in the project. Recommend a dedicated localization infrastructure task to make this call — `ClubButtonWidget.SHOOT` and any other literals in named asmdefs should be batched into that task, not the inventory batch.

---

## Binders

Instantiation site verified: `BagDetailPanel.cs` instantiates `BagClubCard.prefab`, `BagSwapClubCard.prefab`, `BagEmptyClubCard.prefab`, `BagSlotLockedPrefab.prefab`. `ClubDetailPanel.cs` instantiates `ItemUseClubCard.prefab` and `ItemUseClubCardGlowup.prefab`. `InventoryScreenController.cs` shows `BallThumbnailEmptyCard.prefab` instantiation. Binders on card prefabs drive the live UI.

Key read-back verified via `PrefabUtility.LoadPrefabContents` + reflection on `LocalizedText.key` field — 13/13 OK, 0 FAIL:

| Prefab | GO path | key | Verified |
|---|---|---|---|
| `BagClubCard.prefab` | `Mask/Background/ButtonRow/LevelUpBtn/Text` | `ROSTER_LEVEL_UP` | OK |
| `BagClubCard.prefab` | `Mask/Background/ButtonRow/RepairBtn/Text` | `CLUB_REPAIR` | OK |
| `BagSwapClubCard.prefab` | `Mask/Background/ButtonRow/LevelUpBtn/Text` | `ROSTER_LEVEL_UP` | OK |
| `BagSwapClubCard.prefab` | `Mask/Background/ButtonRow/RepairBtn/Text` | `CLUB_REPAIR` | OK |
| `BagEmptyClubCard.prefab` | `Background/NameText` | `BAG_EMPTY` | OK |
| `BagEmptyClubCard.prefab` | `EquipBtn/EquipText` | `BAG_EQUIP_CLUB` | OK |
| `BagSlotLockedPrefab.prefab` | `BagLabel` | `BAG_LOCKED` | OK |
| `BallThumbnailEmptyCard.prefab` | `BagLabel` | `BAG_EMPTY` | OK |
| `ItemUseClubCard.prefab` | `DistanceRow/DistLabel` | `CLUB_DIST` | OK |
| `ItemUseClubCard.prefab` | `ButtonRow/LevelUpBtn/Text` | `ROSTER_LEVEL_UP` | OK |
| `ItemUseClubCard.prefab` | `ButtonRow/RepairBtn/Text` | `CLUB_REPAIR` | OK |
| `ItemUseClubCardGlowup.prefab` | `Mask/Background/ButtonRow/LevelUpBtn/Text` | `ROSTER_LEVEL_UP` | OK |
| `ItemUseClubCardGlowup.prefab` | `Mask/Background/ButtonRow/RepairBtn/Text` | `CLUB_REPAIR` | OK |

---

## Code path

No gameplay code changed this batch. SHOOT in `ClubButtonWidget.cs` is **DEFERRED** (see Triage Group G and Deferred section above). `ClubButtonWidget.cs` restored to HEAD state: line 34 reads `if (_primaryText != null) _primaryText.text = "SHOOT";`. `git diff HEAD -- Assets/Scripts/Gameplay/UI/ShotUI/ClubButtonWidget.cs` is empty. `Golfin.Localization.asmdef` deleted. `Golfin.Gameplay.UI.asmdef` restored to HEAD (no `"Golfin.Localization"` reference). `SHOT_SHOOT` key NOT in CSV.

FLIP — GOLFIN: `ItemDetailPanel.cs` "GOLFIN" = brand watermark. SKIP — not user copy. No conversion performed.

FLIP — SWAP: `BagDetailPanel.cs` line 119 calls `LocalizationManager.Get("BAG_SWAP")` at runtime. Code site already localizes. Binder on prefab not added. SKIP.

FLIP — USE REPAIR KIT: `ItemUseClubCard.cs` line 139: `useRepairKitText.text = LocalizationManager.Get("ITEM_USE_REPAIR_KIT")`. Code site already localizes. SKIP.

---

## CSV

New keys added (2):

| Key | EN | JP |
|---|---|---|
| `BAG_EMPTY` | `EMPTY` | `EMPTY [JP-TODO]` |
| `CLUB_DIST` | `DIST` | `DIST [JP-TODO]` |

SHOT_SHOOT NOT added this batch — DEFERRED with SHOOT in ClubButtonWidget (see Triage Group G).

Reused keys (no new CSV rows minted):

| Key | Reuse source | Already had JP |
|---|---|---|
| `ROSTER_LEVEL_UP` | existing (batch 1 roster task) | Yes |
| `CLUB_REPAIR` | existing | Yes |
| `BAG_EQUIP_CLUB` | existing | Yes |
| `BAG_LOCKED` | existing | Yes |
| `ITEM_USE_REPAIR_KIT` | existing | Yes |

Pre-existing EMPTY key check: searched CSV — no existing `EMPTY`, `BAG_EMPTY`, or similar before this task. New key correctly minted.

Importer re-run: `LocalizationTextImporter.ImportCsv()` executed via reflection-method-call after `assets-refresh (ForceSynchronousImport)`. Console: `[Localization] CSV imported. Rows: 236` (timestamp 20:16:16 JST). `LocalizationTextTable.asset` rebuilt. No duplicate key warnings. (SHOT_SHOOT removed in iter-2 revert; row count dropped from 237 to 236.)

---

## EN unchanged

Captured via real boot flow: ShellScene play mode, Inventory screen, Bags tab. Screenshot taken after 5-second settle.

Canonical screenshot: `screenshots/en_bags_screen.jpg`

Long edge: 1731px (the 900px floor is met). Labels visible: LOCKED (slot), EMPTY (empty card), EQUIP CLUB (button), LEVEL UP (button), REPAIR (button) — all rendering correct English text, no layout shift from binder attachment.

---

## JP smoke

Language switched via `LocalizationManager.SetLanguage(Language.Japanese)` + `SendMessage("OnEnable")` on all 71 active `LocalizedText` instances. 20 active components in the bags screen returned correct JP values.

Screenshot: `screenshots/jp_bags_screen.jpg`

Per-key JP values confirmed at runtime:

| Key | EN | JP rendered | Result |
|---|---|---|---|
| `BAG_LOCKED` | `LOCKED` | `ロック` | PASS — real JP from existing row |
| `BAG_EQUIP_CLUB` | `EQUIP CLUB` | `クラブを装備` | PASS — real JP |
| `ROSTER_LEVEL_UP` | `LEVEL UP` | `レベルアップ` | PASS — real JP |
| `CLUB_REPAIR` | `REPAIR` | `修理` | PASS — real JP |
| `BAG_EMPTY` | `EMPTY` | `EMPTY [JP-TODO]` | PASS — new key shows placeholder |
| `CLUB_DIST` | `DIST` | `DIST [JP-TODO]` | PASS — new key shows placeholder |
No raw key strings (e.g. `ROSTER_LEVEL_UP`) visible on screen. All binder-driven labels resolved correctly. Language restored to English after capture.

---

## Scope containment

`git status --porcelain` after all iter-2 reverts — task-relevant files only:

```
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

NO `.asmdef` files. NO `ClubButtonWidget.cs`. NO `Golfin.Gameplay.UI.asmdef`. `Golfin.Localization.asmdef` deleted (confirmed GONE via `ls`).

Pre-existing unrelated drift (from HEARTBEAT.log iter-1 baseline HEAD `2767f740e393eaf4a5bcc1c89d95b3bfadf2aa23` — NOT staged by this task):

```
 M Assets/Art/RosterScreen/ButtonCancel.png.meta
 M "Assets/Art/Shop/Background - Blurred.png"
 M "Assets/Art/SplashScreen/Green Button.png.meta"
 M "Assets/Fonts/NotoSansJP-VariableFont_wght SDF.asset"
 M Assets/Plugins/NuGet/.nuget-installed.json
 M Assets/Plugins/NuGet/McpPlugin.Common.dll
 M Assets/Plugins/NuGet/McpPlugin.dll
 M Assets/Plugins/NuGet/ReflectorNet.dll
 M Packages/manifest.json
 M Packages/packages-lock.json
```

Verified reverts:
- `git diff HEAD -- Assets/Scripts/Gameplay/UI/ShotUI/ClubButtonWidget.cs` — empty (restored to HEAD)
- `git diff HEAD -- Assets/Scripts/Gameplay/UI/ShotUI/Golfin.Gameplay.UI.asmdef` — empty (restored to HEAD)
- `grep SHOT_SHOOT Assets/Localization/LocalizationText.csv` — NOT IN CSV

No scene mutations. No edits to `Assets/Scripts/Physics/`. No `M_Splash*.mat` touched.

`git diff HEAD -- Assets/Scripts/Physics/` result: no diff (confirmed).

---

## Compile check

`assets-refresh (ForceSynchronousImport)` executed after all iter-2 reverts. No new compile errors. The `Golfin.Gameplay.UI` assembly resolves normally without the `"Golfin.Localization"` reference (removed in revert). `LocalizationManager` remains in Assembly-CSharp. Console error log shows no task-related CS errors after reverts.

---

## Acceptance checklist

| Item | Result | Evidence |
|---|---|---|
| Triage findings: every audit row verdicted | PASS | Groups A–G above cover all 234 CSV rows (SPEC said 62 — see Spec deviations); every row has CONVERTED / SKIP / DEFERRED with reason |
| Binders: each static label has LocalizedText bound to correct key; live key read back | PASS | 13 binders verified via PrefabUtility.LoadPrefabContents + reflection, 13/13 OK, 0 FAIL |
| Binders: card prefab instantiated at runtime by inventory controller | PASS | Instantiation sites confirmed: BagDetailPanel.cs, ClubDetailPanel.cs, InventoryScreenController.cs |
| Code path: SHOOT deferred; no gameplay code changed this batch | PASS | DEFERRED with reason in Triage Group G; `git diff HEAD -- ClubButtonWidget.cs` empty; no asmdef changes |
| Code path: GOLFIN verdict documented | PASS | SKIP — brand watermark, Code path section |
| CSV: only genuinely-new keys added (2); SHOT_SHOOT NOT added (deferred); reused rows confirm pre-existence; importer re-run; no duplicate | PASS | BAG_EMPTY, CLUB_DIST added; 236 total rows confirmed by importer log `[Localization] CSV imported. Rows: 236`; no duplicates |
| EN unchanged: Inventory/Bag screen captured at real boot; labels identical | PASS | `screenshots/en_bags_screen.jpg` (1731px long edge); all labels correct |
| JP smoke: reused keys show real JP; new keys show [JP-TODO]; no raw key on screen | PASS | `screenshots/jp_bags_screen.jpg`; per-key table above 6/6 confirmed (SHOT_SHOOT removed) |
| Scope containment: ONLY inventory card prefabs + CSV + table modified; NO asmdef, NO ClubButtonWidget.cs, NO Golfin.Gameplay.UI.asmdef | PASS | git porcelain quoted above; three git diff verifications all empty; `Golfin.Localization.asmdef` GONE |
| Project compiles; no task-related console errors | PASS | assets-refresh ForceSynchronousImport clean; no new CS errors after reverts |

---

## Spec deviations

1. Audit row count discrepancy: SPEC says 62 Inventory/Bag audit rows; CSV file has 234 rows matching `Inventory/Bag`. The markdown report the architect used for authoring showed a filtered/deduplicated subset (62). All 234 CSV rows have been triaged in this report. No impact on scope.

2. SHOOT deferred (iter-2 revert): iter-1 converted `"SHOOT"` in `ClubButtonWidget.cs` and created `Golfin.Localization.asmdef` to bridge the Assembly-CSharp boundary. The architect correctly identified this as an out-of-scope foundational architecture change. All asmdef work reverted in iter-2; SHOOT is DEFERRED with documented reason. CSV row count is 236 (not 237 — SHOT_SHOOT removed).

3. SWAP flip (BagClubCard/BagSwapClubCard): SPEC listed SWAP as CONVERT (binder). Verification found `BagDetailPanel.cs` already calls `LocalizationManager.Get("BAG_SWAP")` at runtime. SKIP per code-path-first rule.

4. USE REPAIR KIT flip (ItemUseClubCard/ItemUseClubCardGlowup): SPEC listed as CONVERT (binder). Verification found `ItemUseClubCard.cs` already calls `LocalizationManager.Get("ITEM_USE_REPAIR_KIT")`. SKIP per code-path-first rule.

---

## Files modified or created

| File | Change |
|---|---|
| `Assets/Localization/LocalizationText.csv` | +2 new keys (BAG_EMPTY, CLUB_DIST); SHOT_SHOOT NOT added (deferred) |
| `Assets/Localization/LocalizationTextTable.asset` | Rebuilt by importer (236 rows) |
| `Assets/Prefabs/UI/Inventory/BagClubCard.prefab` | +2 LocalizedText binders (LevelUpBtn/Text, RepairBtn/Text) |
| `Assets/Prefabs/UI/Inventory/BagSwapClubCard.prefab` | +2 LocalizedText binders (LevelUpBtn/Text, RepairBtn/Text) |
| `Assets/Prefabs/UI/Inventory/BagEmptyClubCard.prefab` | +2 LocalizedText binders (NameText→BAG_EMPTY, EquipText→BAG_EQUIP_CLUB) |
| `Assets/Prefabs/UI/Inventory/BagSlotLockedPrefab.prefab` | +1 LocalizedText binder (BagLabel→BAG_LOCKED) |
| `Assets/Prefabs/UI/Inventory/BallThumbnailEmptyCard.prefab` | +1 LocalizedText binder (BagLabel→BAG_EMPTY) |
| `Assets/Prefabs/UI/Inventory/ItemUseClubCard.prefab` | +3 LocalizedText binders (DistLabel→CLUB_DIST, LevelUpBtn/Text, RepairBtn/Text) |
| `Assets/Prefabs/UI/Inventory/ItemUseClubCardGlowup.prefab` | +2 LocalizedText binders (LevelUpBtn/Text, RepairBtn/Text) |
| `Docs/Specs/Active/localize_inventory_bag/HEARTBEAT.log` | Task log |
| `Docs/Specs/Active/localize_inventory_bag/screenshots/en_bags_screen.jpg` | EN capture (1731px long edge) |
| `Docs/Specs/Active/localize_inventory_bag/screenshots/jp_bags_screen.jpg` | JP smoke capture |
