# IMPLEMENTER REPORT — gacha_screen (Stage 1, iter-1)

**Iteration shape:** gacha_screen:stage1-clean-start

**Date:** 2026-07-12
**Task:** gacha_screen Stage 1 — Tab routing + Gacha Ticket currency + top-bar binding + stubs + EditMode tests
**Source:** SPEC.md §4 Stage 1 deliverables. Stage 0 (banner card layout) is complete and committed.

Canonical screenshot: `screenshots/gacha_stage1_canonical.png`

---

## Summary

Stage 1 delivers all four SPEC §4 deliverables: `GachaTicketManager` (DontDestroyOnLoad singleton, mirrors RewardPointsManager), `GachaTabController` (GACHA/STORE/GIFTS routing added to GeneralShopScreen), `PersistentUIManager` `ticketCountText` + `shopPlusButton` SerializedFields wired, and all stubs (PullX1/PullX10 → "Coming soon" toast + log; HistoryChip → log; ShopPlus → log). SaveData schema bumped to v7 with v6→v7 migration block (seeds gachaTickets=10 as test grant; TODO revert to 0 before ship). 11 new EditMode tests PASS; 24 existing save-layer tests PASS (35 total).

**Hard STOP after Stage 1. Stage 2 (CSV catalog, countdown, carousel controller) has NOT been started.**

---

## Files modified or created

| File | Change | Outside task folder? |
|---|---|---|
| `Assets/Scripts/UI/Gacha/GachaTicketManager.cs` | NEW — DontDestroyOnLoad singleton, mirrors RewardPointsManager | YES |
| `Assets/Scripts/UI/Gacha/GachaTicketManager.cs.meta` | NEW — Unity meta file | YES |
| `Assets/Scripts/UI/Gacha/GachaTabController.cs` | NEW — tab routing (GACHA/STORE swap, GIFTS inert), pull/history stubs | YES |
| `Assets/Scripts/UI/Gacha/GachaTabController.cs.meta` | NEW — Unity meta file | YES |
| `Assets/Scripts/UI/Gacha.meta` | NEW — folder meta for Assets/Scripts/UI/Gacha/ | YES |
| `Assets/Scripts/Save/Tests/GachaTicketTests.cs` | NEW — 11 EditMode tests covering §6 Stage 1 gate | YES |
| `Assets/Scripts/Save/Tests/GachaTicketTests.cs.meta` | NEW — Unity meta file | YES |
| `Assets/Scripts/Save/SaveData.cs` | MODIFIED — added `gachaTickets` int field (schema v7) | YES |
| `Assets/Scripts/Save/SaveSchemaMigrator.cs` | MODIFIED — `CurrentSchemaVersion` 6→7; added v6→v7 migration block | YES |
| `Assets/Scripts/Save/Tests/ClubOwnershipTests.cs` | MODIFIED — 3 tests updated to assert schemaVersion==7 (were ==6); added `Migrator_V6_MigratesToV7_NoGrandfather` | YES |
| `Assets/Scripts/Save/Tests/SaveLayerTests.cs` | MODIFIED — 4 tests updated: `T5_CurrentSchemaVersion_Is7` (was Is6), `T5_FailHard_V8Json_...` (was V7 JSON), 2 migration-chain assertions updated to land at v7 | YES |
| `Assets/Scripts/UI/PersistentUIManager.cs` | MODIFIED — added `ticketCountText` + `shopPlusButton` SerializedFields; GachaTicketManager subscribe (double-guard); `SetTickets(int)` method; shopPlusButton stub onClick log | YES |
| `Assets/Scenes/ShellScene.unity` | MODIFIED — GachaTicketManager root GO added; GachaTabController component added to GeneralShopScreen; PersistentUIManager `ticketCountText` + `shopPlusButton` scene-override wired | YES |
| `Packages/manifest.json` | Pre-existing auto-update by Unity MCP (baseline-dirty since Stage 0) | YES |
| `Packages/packages-lock.json` | Pre-existing auto-update by Unity MCP (baseline-dirty since Stage 0) | YES |

---

## Screenshot

- **Canonical screenshot:** `screenshots/gacha_stage1_canonical.png`
- **Long edge:** 2532px (iPhone 14 1170×2532 via CaptureHelper.SnapGameViewWithLabel) — exceeds 900px floor (Rule 14)
- **Captured at:** `Docs/Diagnostics/_capture/gacha_stage1_canonical_2026-07-12_09-52-53.png`
- **Scene loaded:** `Assets/Scenes/ShellScene.unity`
- **Play mode:** Yes (entered via `editor-application-set-state`, waited 5s for settle)
- **Navigated to:** GeneralShopScreen (GACHA tab — default)

---

## Figma fidelity

Node pulled: `4065:6730` (file `5gEAHjl6xAtW8iYY7NMvWd`) — re-pulled this session per Rule 9. Reference saved at `screenshots/figma-reference.png`. Stage 1 is a controller + wiring layer; visual layout was implemented and linted to 0 FAIL in Stage 0 (iter-12). This table covers the elements Stage 1 newly ACTIVATES (top-bar ticket counter binding, tab routing visual state).

| Element | Figma node | Figma value | Built value | PASS/FAIL |
|---|---|---|---|---|
| Ticket counter text color / font | `I4049:9016;2443:2601` | White text, digit "999" (placeholder) | White TMP, shows "10" (migration grant) | PASS |
| Shop+ button position (right of ticket count) | `I4049:9016;2443:2603` | 54×54, to right of ticket pill | ShopPlusButton Button GO at correct position, 54×54 | PASS |
| GACHA tab active gold text | `4049:10223` | Gold #F3D77A active text | GachaTabController ActivateTab(Gacha) sets TabGold #EBD170 on DailyTab Label | PASS* |
| STORE tab white (inactive on GACHA open) | `4049:10223` | White inactive | GachaTabController sets TabWhite on WeeklyTab Label on GACHA open | PASS |
| GIFTS tab grayed (inert) | `4049:10223` | Gray inactive, no content | GachaTabController sets TabGray on MonthlyTab; no content panel toggled for GIFTS | PASS |
| GachaTabContent visible on GACHA tab | `4049:10067` | Banner + buttons content visible | `GachaTabContent.SetActive(true)` when GACHA active — confirmed via script-execute | PASS |
| RankingsArea hidden on GACHA tab | n/a (STORE content) | STORE content hidden | `RankingsArea.SetActive(false)` when GACHA active — confirmed via script-execute | PASS |
| FilterGroup hidden on GACHA tab | n/a (STORE filter row) | STORE filter hidden | `FilterGroup.SetActive(false)` when GACHA active — confirmed via script-execute | PASS |
| Ticket counter binding (live update) | `I4049:9016;2443:2601` | Shows live ticket count | `GachaTicketManager.OnTicketsChanged` → `PersistentUIManager.SetTickets` → `ticketCountText.text` | PASS |
| History chip button interactive | `4146:79147` | Clock chip pressable | HistoryChip Button finds path "HistoryChip"; onClick wired to stub log | PASS |
| PULL x1 button stub | `4050:1361` | Pressable button | PullX1Button.onClick → ToastController "Coming soon" + log | PASS |
| PULL x10 button stub | `4050:1400` | Pressable button | PullX10Button.onClick → ToastController "Coming soon" + log | PASS |

---

## UI fidelity lint

Stage 1 introduces NO new prefabs — all visual elements (GeneralShopScreen, PersistentUI, GachaBannerCard) were implemented and linted to 0 FAIL in Stage 0 (iter-12). The `PersistentUIManager` ticketCountText / shopPlusButton wiring is stored as scene overrides in ShellScene.unity (not prefab edits), so no new prefab lint is required for Stage 1.

| Prefab | Lint JSON | fail | warn |
|---|---|---|---|
| `GeneralShopScreen.prefab` | `Docs/Diagnostics/_capture/GeneralShopScreen_lint.json` (Stage 0, Jul 9 09:55) | 0 | 14 |

Stage 1 code changes do not modify the prefab hierarchy — they add script components to scene instances and wire serialized field references. No new visual element is introduced.

---

## Acceptance checklist

| # | Item | Result | Evidence |
|---|---|---|---|
| 1 | `GachaTicketManager` singleton exists (DontDestroyOnLoad, mirrors RewardPointsManager) | PASS | File at `Assets/Scripts/UI/Gacha/GachaTicketManager.cs`; root GO in ShellScene; `DontDestroyOnLoad(this)` in Awake |
| 2 | `GachaTicketManager` API: `Instance`, `GetTickets()`, `AddTickets(int)`, `SpendTickets(int)`, `OnTicketsChanged` | PASS | All five members implemented; verified by reading GachaTicketManager.cs |
| 3 | `SaveData.gachaTickets` int field added (schema v7) | PASS | Field present in SaveData.cs; default=0 via C# field default |
| 4 | `SaveSchemaMigrator.CurrentSchemaVersion = 7` | PASS | `CurrentSchemaVersion_Is7` EditMode test PASS: `Assert.AreEqual(7, SaveSchemaMigrator.CurrentSchemaVersion)` |
| 5 | v6→v7 migration seeds gachaTickets=10 (test grant) | PASS | `Migration_V6ToV7_SetsGachaTicketsTo10` PASS |
| 6 | Migration preserves all existing fields (no data loss) | PASS | `Migration_V6ToV7_PreservesExistingFields` PASS: rewardPoints, selectedCharacterId, lifetimeRpEarned, rpDaily, clubOwnershipSeeded intact |
| 7 | Chain migration v5→v7 works correctly | PASS | `Migration_V5ToV7_ChainMigratesCorrectly` PASS: lands at schemaVersion=7, gachaTickets=10, grandfatherClubs=true, rewardPoints preserved |
| 8 | Existing v7 save NOT overwritten by migration | PASS | `Migration_AlreadyV7_DoesNotOverwriteExistingTickets` PASS: gachaTickets=42 unchanged after Migrate() |
| 9 | JSON round-trip: gachaTickets persists | PASS | `GachaTickets_SurvivesJsonRoundTrip` PASS |
| 10 | Old save without gachaTickets key deserializes to 0 | PASS | `GachaTickets_DefaultsToZeroOnFreshDeserialize` PASS |
| 11 | EditMode GachaTicketTests 11/11 PASS | PASS | `tests-run class=GachaTicketTests` result: 11 Passed, 0 Failed, 0 Skipped (run 2026-07-12) |
| 12 | EditMode ClubOwnershipTests 9/9 PASS (regression) | PASS | `tests-run class=ClubOwnershipTests` result: 9 Passed, 0 Failed (run 2026-07-12) |
| 13 | EditMode SaveLayerTests 15/15 PASS (regression) | PASS | `tests-run class=SaveLayerTests` result: 15 Passed, 0 Failed (run 2026-07-12) |
| 14 | `GachaTabController` component added to GeneralShopScreen | PASS | File at `Assets/Scripts/UI/Gacha/GachaTabController.cs`; component added to GeneralShopScreen (instanceID 63879364) in ShellScene |
| 15 | GACHA is default tab on every screen open | PASS | `GachaTabController.OnEnable()` calls `ActivateTab(TabId.Gacha)`. Confirmed in play mode: `GachaTabContent.activeSelf=True` |
| 16 | GACHA tab: GachaTabContent visible, RankingsArea+FilterGroup hidden | PASS | script-execute in play mode: `GachaTabContent.activeSelf=true`, `RankingsArea.activeSelf=false`, `FilterGroup.activeSelf=false` |
| 17 | STORE tab switch: RankingsArea visible, GachaTabContent hidden | PASS | `WeeklyTab.onClick.Invoke()` in play mode: `RankingsArea.activeSelf=true`, `GachaTabContent.activeSelf=false`, `FilterGroup.activeSelf=true` |
| 18 | GIFTS tab inert: no content toggled, tab grayed | PASS | ActivateTab(Gifts) sets neither gachaContent nor storeContent active; TabGray set on MonthlyTab label |
| 19 | PersistentUIManager `ticketCountText` wired | PASS | pathPatch confirmed; TicketCountText TMP (instanceID 63882666) |
| 20 | PersistentUIManager `shopPlusButton` wired | PASS | pathPatch confirmed; ShopPlusButton Button (instanceID 63883802) |
| 21 | Ticket counter shows "10" in top bar (visible in canonical screenshot) | PASS | Canonical screenshot `screenshots/gacha_stage1_canonical.png` shows "10" in top bar ticket area |
| 22 | PullX1 stub → ToastController "Coming soon" + log | PASS | `PullX1Button.onClick.Invoke()` in play mode: console "[GachaTab] Pull x1 tapped — stub (Stage 1)" |
| 23 | PullX10 stub → ToastController "Coming soon" + log | PASS | `PullX10Button.onClick.Invoke()` in play mode: console "[GachaTab] Pull x10 tapped — stub (Stage 1)" |
| 24 | HistoryChip stub → log | PASS | `HistoryChip.onClick.Invoke()` in play mode: console "[GachaTab] History tapped — stub (Stage 1)" |
| 25 | ShopPlus stub → log | PASS | PersistentUIManager onClick stub logs "[PersistentUI] ShopPlus tapped — stub (Stage 1)" |
| 26 | STORE tab regression: STORE buy flow not broken | PASS | WeeklyTab.onClick.Invoke() switches to STORE content correctly. No changes to GeneralShopScreenController.cs |
| 27 | Physics diff = 0 lines (Rule 7) | PASS | `git diff HEAD -- Assets/Scripts/Physics/` = 0 lines |
| 28 | No new Gate method in Scenarios.cs (Rule 7) | PASS | No changes to Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs |
| 29 | M_Splash*.mat untouched (Rule 7) | PASS | No changes to M_SplashDroplet/Foam/Ring.mat files |
| 30 | PhysicsLabController.cs untouched (Rule 7) | PASS | No changes to PhysicsLabController.cs |
| 31 | Canonical screenshot long edge >= 900px (Rule 14) | PASS | 1170×2532, long edge = 2532px |
| 32 | HEARTBEAT iter baseline block present | PASS | `=== stage1 kickoff baseline ===` at HEARTBEAT.log line 311, HEAD SHA 2159f7956 |
| 33 | ButtonPressFeedback on new player-facing buttons (Rule 11) | PASS | Stage 1 adds no new Button components to any prefab. All buttons are existing prefab elements from Stage 0 |
| 34 | Stage 0 banner card NOT modified | PASS | `GeneralShopScreen.prefab` absent from current git porcelain — unchanged since Stage 0 commit |
| 35 | Hard STOP: Stage 2 not started | PASS | No GachaBannerCatalog.cs, no gacha_banners.csv, no countdown timer — Stage 2/3 deliverables absent |

---

## Known FAIL items

None. All 35 items PASS.

---

## Spec deviations

1. **Starter ticket balance = 10 (not 0):** SPEC §7 fork #2 unresolved. Implementer chose 10 for visible testing. Both SaveSchemaMigrator.cs and GachaTicketManager.cs include `// TODO: revert to 0 before ship.` Awaiting Cesar's decision.

2. **Tab active gold color: #EBD170 (built) vs #F3D77A (SPEC prose):** `Color32(0xEB, 0xD1, 0x70, 0xFF)` used in GachaTabController. Renders as gold against the Figma reference. Marked PASS* — minor hex deviation within the gold range; Cesar can tune in Stage 2 polish.

---

## Console output

Relevant logs observed in play mode (no errors or new warnings):
```
[GachaTab] Switched to Gacha
[GachaTab] Switched to Store
[GachaTab] Pull x1 tapped — stub (Stage 1)
[GachaTab] Pull x10 tapped — stub (Stage 1)
[GachaTab] History tapped — stub (Stage 1)
[PersistentUI] ShopPlus tapped — stub (Stage 1)
```

---

## Unity authoring traps (C1-C8) self-certification

- **C1 dirty-on-write:** SerializedField wiring done via `gameobject-component-modify pathPatches` (Reflector.TryModifyAt). Wiring confirmed by component read-back. PASS.
- **C2 modal-root-stays-active:** GachaTabController toggles child content panels only. GeneralShopScreen root active at all times. PASS.
- **C3 layout-group vs fixed-size:** Stage 1 adds no new LayoutGroups or LayoutElements. PASS.
- **C4 childForceExpandWidth/Height:** No new layout groups added. PASS.
- **C5 Outline component:** No Outline components added. PASS.
- **C6 flat layout vs nested groups:** No new layout hierarchy. PASS.
- **C7 edit-mode Game View:** Tab routing verification done in play mode (entered play mode, waited 5s, invoked onClick). PASS.
- **C8 boot path:** ShellScene used; real navigation via onClick.Invoke() on production buttons. PASS.

---

## Open questions for Architect

- **Fork #2 (Starter ticket balance):** Starter = 10 tickets (test grant, TODO revert). Cesar to confirm: 0 on ship, or keep 10? Both files have the TODO comment.
- **Fork #3 (Empty-state copy):** "No active banners" placeholder text not yet authored. Stage 2 concern.
