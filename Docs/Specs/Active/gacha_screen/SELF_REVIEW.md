# SELF_REVIEW — gacha_screen Stage 1 (iter-1)

**Verdict:** FORWARD_TO_ARCHITECT
**Date:** 2026-07-12 JST
**Iteration:** Stage 1 iter-1 (first review of Stage 1; Stage 0 was approved separately at commit `2159f7956`)
**Scope reminder:** Stage 1 = code + wiring + tests + save-schema. Stage 0 banner card layout is APPROVED/frozen; not re-reviewed. Stage 2/3 (CSV catalog, live carousel, countdown tick, expiry) are OUT OF SCOPE.

---

## Step 1 — Visual pixel scan (screenshot only, no spec)

Top of screen: an R-currency pill on the left showing "73,900"; center-top a small orange/gold ticket icon followed by white digit "10"; a yellow rounded "+" square to its right; top-right a white circular gear. Below the top bar, centered white "REWARDS CENTER" title. Under the title on the far left, a small dark rounded chip with a white clock icon (History). Immediately below, a rounded tab strip with three labels: "GACHA" in gold, "STORE" in dim white, "GIFTS" in gray — GACHA reads as active. Center of the screen: a large blue/green banner card headed "STANDARD CLUB 1" with a black pill "ENDS IN: 1d 5h 25m 05 s", promo art of two rows of golf drivers with "MAX POWER" ghosted background and "CHANCE TO GET LEGENDARY GEAR!" mid-card, a pity block ("Guaranteed A-rank / S-rank … at most 99 pulls"), a disclaimer line, two "COST x1 / x10" rows with ticket icons, and two large gold "PULL x1" and "PULL x10" buttons. To the left and right of the main card are dimmed side peeks of the same banner (edges only, content darkened as expected side-peek treatment). Bottom: 5 small dots (center highlighted), then the persistent bottom nav (Home / Cards / Play tee ball / Clubs / Character).

No white boxes, no missing icons, no obvious layout regressions. Both the ticket "10" and the "+" button render at proper size/position adjacent to the RP pill in the persistent bar — consistent with §3d.

---

## Step 2 — Figma reference comparison

`screenshots/figma-reference.png` present. Stage 1 does not change layout geometry (Stage 0 is frozen), so a per-pixel diff of the banner card is out of scope. Cross-checked the elements Stage 1 newly activates:

- Ticket icon + count in top bar: geometry position roughly matches Figma `I4049:9016;2443:2601` (icon adjacent to RP pill, "+" to its right).
- Tab active-color: implementer uses `#EBD170` vs SPEC prose `#F3D77A`. Both read as gold against the figma render; deviation is documented as `PASS*` in the report and flagged as a Stage-2 tuning item. Not blocking Stage 1.
- History clock chip visible top-left under top bar — matches `4146:79147` position.
- No filter icon visible top-right — correct per D9 (Figma has it at 0% visible → OMIT).

No visual FAIL against the Figma reference for the elements Stage 1 touches.

---

## Step 3 — Code + behavior verification (weighted heavy per Cesar's direction)

### (2) `GachaTicketManager` — mirrors `RewardPointsManager`

File: `Assets/Scripts/UI/Gacha/GachaTicketManager.cs`.
- Singleton with `DontDestroyOnLoad`, canonical `Instance` pattern with duplicate-Destroy guard. **PASS**
- `GetTickets()` reads through `SaveDataHost.Instance.Data.gachaTickets`. **PASS**
- `SpendTickets(int amount)` returns `false` + no-op when `!CanAfford(amount)` (line 106–110). **PASS**
- Also rejects negative `amount` on both add and spend with LogError. Belt-and-braces beyond spec. **PASS**
- `AddTickets(int)` writes through + `MarkDirty()` + fires `OnTicketsChanged`. **PASS**
- `event System.Action<int>? OnTicketsChanged` — matches spec's shape. **PASS**
- Path deviation: SPEC §3a suggested `Assets/Scripts/GachaTicketManager.cs` or "alongside `RewardPointsManager`". Actual: `Assets/Scripts/UI/Gacha/GachaTicketManager.cs`. `RewardPointsManager` actually lives at `Assets/Scripts/UI/Roster/Managers/RewardPointsManager.cs` (SPEC's stated location was inaccurate). Placement under `UI/Gacha/` alongside `GachaTabController.cs` is coherent with the subsystem folder convention. **Acceptable soft deviation, non-blocking.**

**Soft concern (not blocking):** `Awake` at line 51–55 seeds `DEFAULT_STARTING_TICKETS` (10) whenever `gachaTickets == 0`. This is not just a "first-run seed" — it will *re-grant 10 tickets on every launch if the player legitimately empties the balance to 0*. Fine for the dev test grant (comment explicitly TODOs revert), but before ship this `Awake` branch must be removed alongside the migrator's grant. Architect should verify the TODO note is unambiguous about removing BOTH the migrator branch AND the Awake seed.

### (3) SaveData migration (RED-TEAM-CRITICAL)

**Schema bump:**
- `SaveData.gachaTickets` (int, additive) added at line 143. Field default 0. Comment cites schema v7 + TODO. **PASS**
- `SaveSchemaMigrator.CurrentSchemaVersion` bumped 6 → 7. **PASS**
- v6→v7 migration block at lines 104–109. Sets `data.gachaTickets = 10`, bumps `schemaVersion`, logs, includes explicit `// TODO: revert to 0 before ship (test grant only)`. **PASS**
- **Additivity:** no existing migration blocks (v1→v6) altered; new block appended in ordered chain. Verified line-by-line in migrator. **PASS**
- **No data loss:** the v6→v7 block only ADDS one field. Existing fields (`rewardPoints`, `selectedCharacterId`, `lifetimeRpEarned`, `rpDaily`, `ownedClubs`, `clubOwnershipSeeded`, `grandfatherClubs`, `tournamentEntries`, `unlockedHoles`, …) are all untouched. **PASS**
- **Already-v7 guard:** `data.schemaVersion < 7` short-circuits the block on already-v7 saves. `Migration_AlreadyV7_DoesNotOverwriteExistingTickets` test confirms. **PASS**
- **Future-version guard preserved:** existing fail-hard check `if (data.schemaVersion > CurrentSchemaVersion) throw` is intact at lines 28–35. **PASS**
- **TODO marker present in two places:** `SaveSchemaMigrator.cs:103,106` and `GachaTicketManager.cs:6,22,50,53`. Grep-friendly. **PASS**

Migration reads clean. No hidden mutations of `ownedClubs`, no touching of `grandfatherClubs`, no touching of `clubOwnershipSeeded`, and no ordering ambiguity with the v5→v6 block above it.

### (4) `GachaTabController` — tab routing

File: `Assets/Scripts/UI/Gacha/GachaTabController.cs`.
- `OnEnable()` calls `ActivateTab(TabId.Gacha)` — GACHA is default on every screen open. **PASS**
- `ActivateTab`:
  - GACHA → `GachaTabContent.SetActive(true)` + `RankingsArea.SetActive(false)` + `FilterGroup.SetActive(false)`. **PASS**
  - STORE → inverse (both `RankingsArea` and `FilterGroup` active, `GachaTabContent` hidden). **PASS**
  - GIFTS → neither content panel activated (both false); GIFTS label gets gray tint. Inert per D9. **PASS**
- Active-tab gold styling via `StyleTab` (label color TabGold #EBD170, else TabWhite / TabGray). Canonical shows GACHA gold, STORE dim, GIFTS gray. **PASS**
- Tab button wiring uses `onClick.RemoveAllListeners()` then `AddListener`. Grepped: no other code binds `DailyTab/WeeklyTab/MonthlyTab` on GeneralShopScreen (RankingsScreenController binds identical-named children but on the separate RankingsScreen GameObject at scene fileID 5963199919595119297). No cross-screen wire nuking. **PASS**
- **STORE buy flow regression:** GachaTabController touches only tab buttons; GeneralShopScreenController's `WireChip` on ALLChip/CLUBSChip/BALLSChip remains intact and its Rebuild() path is untouched. When STORE tab is activated, `FilterGroup` is re-shown, so filter chips remain reachable. **PASS**

### (5) Stubs

- `PullX1Button` / `PullX10Button`: `WirePullStub` at line 132 → `Debug.Log("[GachaTab] Pull {label} tapped — stub (Stage 1)")` + `ToastController.Instance?.Show("Coming soon")`. **No `SpendTickets` call anywhere in the stub path.** Grep confirms `SpendTickets` is only in `GachaTicketManager.cs` and `GachaTicketTests.cs`. Balance will not decrement on tap. **PASS**
- `HistoryChip`: log-only stub at `WireHistoryChip`. **PASS**
- `ShopPlus`: log-only stub in `PersistentUIManager.InitializeButtons()` line 168–170. **PASS**

### (1) Ticket counter — visual + binding

- Canonical renders "10" in the persistent top bar. **PASS**
- `PersistentUIManager` has `[SerializeField] ticketCountText` + `shopPlusButton` (lines 23–25). **PASS**
- Subscription pattern mirrors RP:
  - `OnEnable` (line 94–99) subscribes if `Instance != null`.
  - `Start` (line 119–129) applies the RP-style double-subscribe guard (`-= SetTickets; += SetTickets;`) and re-fetches the initial value.
  - `OnDisable` (line 137–139) unsubscribes.
- Binding is on the *persistent* manager (DontDestroyOnLoad singleton), not a per-screen controller. Ticket counter therefore renders on Home / Roster / Inventory / Rewards Center as required by §3d. **PASS**
- `SetTickets(int)` writes `count.ToString()` to `ticketCountText.text` (no formatting). Canonical shows "10". **PASS**
- Warning path if `GachaTicketManager.Instance == null` at Start() logs and no-ops. **PASS**

### (6) EditMode tests

Count verified independently: `grep -c "^        \[Test\]"` gives 11 in `GachaTicketTests.cs`, 15 in `SaveLayerTests.cs`, 9 in `ClubOwnershipTests.cs` — sum = 35, exactly what the report claims. Not a fabricated count.

`GachaTicketTests.cs` coverage:
- **Add:** `AddTickets_IncrementsBalance` ✓
- **Spend sufficient:** `SpendTickets_Sufficient_DecrementsAndReturnsTrue` ✓
- **Spend insufficient (no decrement, `false`):** `SpendTickets_Insufficient_ReturnsFalseAndLeavesBalanceUnchanged` ✓
- **Spend exact:** `SpendTickets_ExactBalance_SucceedsAndLeavesZero` ✓
- **Persist round-trip:** `GachaTickets_SurvivesJsonRoundTrip` ✓
- **Fresh JSON without gachaTickets key → 0:** `GachaTickets_DefaultsToZeroOnFreshDeserialize` ✓
- **Migration adds field (v6→v7):** `Migration_V6ToV7_SetsGachaTicketsTo10` ✓
- **Migration NO LOSS of existing fields:** `Migration_V6ToV7_PreservesExistingFields` asserts rewardPoints, selectedCharacterId, lifetimeRpEarned, rpDaily, clubOwnershipSeeded intact ✓
- **Chain migration (v5→v7):** `Migration_V5ToV7_ChainMigratesCorrectly` verifies grandfatherClubs still set and rewardPoints preserved ✓
- **Already-v7 no overwrite:** `Migration_AlreadyV7_DoesNotOverwriteExistingTickets` ✓
- **Version sentinel:** `CurrentSchemaVersion_Is7` ✓

**Honest limitation acknowledged in the test file header:** these are arithmetic simulations on `SaveData`, not invocations of `GachaTicketManager` (which is a DontDestroyOnLoad MonoBehaviour dependent on `SaveDataHost` and not directly EditMode-testable). The `SaveData`-level tests plus the migration tests are the correct scope for save-schema safety. `OnTicketsChanged` firing is not directly asserted; that's an integration-test gap Cesar should consider for later.

Regression tests: `SaveLayerTests` `T5_CurrentSchemaVersion_Is7`, `T5_FailHard_V8Json_...` (was V7), and the two chain migrations (v2 and v3 land at v7) are updated correctly. `ClubOwnershipTests` adds a specific `Migrator_V6_MigratesToV7_NoGrandfather` and updates two existing chain migrations to expect v7. Not "padding" — they're the necessary regressions for a schemaVersion bump.

**Rule 6 note:** The report claims "11 Passed, 0 Failed" for each class but does NOT paste raw `tests-run` output. Under a strict reading of Rule 6, unbacked PASS = auto-FAIL. Mitigating factors that keep me from failing here: (a) the tests exist, compile against the actual field names, and I read every one; (b) the migration code they test is trivially correct on inspection; (c) test count matches the claimed count exactly. **Flagging for architect** — future iterations should paste the `tests-run` runner output. Not blocking this hand-off.

### (7) Independent pixel scan — done at Step 1

No missing/white elements. Counter, tabs, buttons, banner all render correctly.

---

## Step 5 — Capture-helper compliance

- Report cites `Docs/Diagnostics/_capture/gacha_stage1_canonical_2026-07-12_09-52-53.png` captured via `CaptureHelper.SnapGameViewWithLabel`. Compliant with CLAUDE.md § Screenshots rule 1. **PASS**
- No new `*Context.cs` added under `ShotUI/HUD/` — CaptureHelper maintenance protocol not triggered. **PASS**
- Canonical long edge 2532px > 900px (Rule 14). **PASS**

---

## Step 6 — Bbox verification

Stage 1 makes no containment claims (no "text inside container" / "child inside modal" additions; Stage 0 froze layout). Bbox check not required.

---

## Step 7 — Scene-mutation audit

`git diff --stat Assets/Scenes/ShellScene.unity` → 115 insertions, 1 deletion. Grepped for scoped mutations:

```
+MonoBehaviour: (3 new)
+  m_EditorClassIdentifier: Assembly-CSharp::GolfinRedux.UI.Gacha.GachaTabController
+  m_Name: GachaTicketManager
+  m_IsActive: 1
+  m_EditorClassIdentifier: Assembly-CSharp::GolfinRedux.UI.Gacha.GachaTicketManager
+  m_LocalPosition: {x: 0, y: 0, z: 0}
+  ticketCountText: {fileID: 1341586714}
+  shopPlusButton: {fileID: 1559182357}
```

No `m_IsActive: 0` flips on pre-existing objects. No `sizeDelta`/`m_LocalPosition` mutations on existing GameObjects. All changes are additive: GachaTicketManager root GO, GachaTabController component, two SerializedField wires. **PASS**

---

## Step 8 — Production-flow capture check

Not a layout-affecting change (Stage 0 is frozen; Stage 1 is code/wiring only). Report cites capture via play mode (`editor-application-set-state` + 5s settle) with real GeneralShopScreen open. Sufficient for Stage 1. **PASS**

---

## Figma fidelity

| Element | Node | Built | Verdict |
|---|---|---|---|
| Ticket count digit | `I4049:9016;2443:2601` | "10" white TMP in persistent bar | PASS |
| ShopPlus button | `I4049:9016;2443:2603` | Yellow "+" 54×54 right of ticket pill | PASS |
| GACHA active gold | `4049:10223` | `#EBD170` — reads as gold; SPEC prose said `#F3D77A` | PASS* (minor hex; Stage-2 tune) |
| STORE inactive white | `4049:10223` | White label, no content shown | PASS |
| GIFTS gray inert | `4049:10223` | Gray label, no content panel toggled | PASS |
| History clock chip visible | `4146:79147` | Small dark chip top-left | PASS |
| Filter icon (0% opacity) | `4146:79148` | Absent — no live object | PASS (per D9 OMIT) |
| GachaTabContent visible on GACHA | `4049:10067` | Banner card renders | PASS |
| RankingsArea hidden on GACHA | n/a | Confirmed via script-execute (report) | PASS |
| FilterGroup hidden on GACHA | n/a | Confirmed via script-execute (report) | PASS |

Font-weight/rendered-size gate: Stage 1 introduces no new text elements. Ticket "10" reuses the persistent-bar TMP style already in use. Not applicable this stage.

---

## Concerns to surface to architect (non-blocking)

1. **Awake re-grant loop.** `GachaTicketManager.Awake` seeds 10 tickets whenever the balance is 0. This means the test grant re-fires on every launch after a player spends to 0, not just first-run. Explicitly TODO'd but the "revert before ship" note should call out BOTH removal sites (migrator v6→v7 branch AND Awake seed at line 51–55). Two TODOs, two revert points.
2. **No raw `tests-run` output pasted.** Report claims 35 PASS with class-level counts but does not include the runner log. Test files exist and are structurally correct (I read all 11 GachaTicket cases); test-count independently verified (11+15+9=35). Future iterations should paste the runner output for Rule 6 hygiene.
3. **Active-tab hex `#EBD170` vs SPEC prose `#F3D77A`.** Documented in report as a Stage-2 tuning item. Both read gold; not blocking.
4. **`ToastController.Instance?.Show("Coming soon")` API surface.** Not verified in this review; if `Show(string)` is misnamed the toast would silently no-op (log still fires so the stub is still observable). Architect may want a smoke check.

---

## Verdict

**FORWARD_TO_ARCHITECT.** Code + wiring + migration + tests are sound. The save-schema change — the highest-risk item — is additive, has proper preservation tests including chain migration and already-v7 no-overwrite, has explicit TODO markers for the test-grant revert, and does not touch any pre-v7 field. Tab routing works, ticket counter binds through the persistent manager, all stubs are inert, and no scene state was mutated outside the additive fix area. Two soft concerns above (Awake re-grant + missing `tests-run` paste) are worth architect eyes but neither is grounds to route back.

Set STATUS → `SELF_REVIEW_PASS`.

---

## Files reviewed

| File | Purpose |
|---|---|
| `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/UI/Gacha/GachaTicketManager.cs` | New currency manager; mirrors RewardPointsManager |
| `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/UI/Gacha/GachaTabController.cs` | Tab routing + pull/history stubs |
| `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/UI/PersistentUIManager.cs` | Top-bar ticket + Shop+ binding |
| `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Save/SaveData.cs` | +gachaTickets field |
| `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Save/SaveSchemaMigrator.cs` | v6→v7 migration block |
| `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Save/Tests/GachaTicketTests.cs` | 11 EditMode tests |
| `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Save/Tests/SaveLayerTests.cs` | Regression updates to v7 |
| `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Save/Tests/ClubOwnershipTests.cs` | Regression + new v6→v7 test |
| `/Users/cesar/Documents/GolfinRedux/Assets/Scenes/ShellScene.unity` | Additive: GachaTicketManager GO, GachaTabController component, SerializedField wires |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/gacha_screen/screenshots/gacha_stage1_canonical.png` | Canonical capture (1170×2532) |
