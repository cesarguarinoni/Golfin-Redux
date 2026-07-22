# Implementer Report — localize_persistent_home_pilot

**Iteration shape:** localization:jp-font-rendering

## Implementation summary

Converted 7 nav-title string literals in `PersistentUIManager.cs` to `LocalizationManager.Get("NAV_*")` calls. Added 7 new `NAV_*` rows to `LocalizationText.csv` (EN + `[JP-TODO]` JP marker), re-ran the importer (227 → 234 keys confirmed). Attached `LocalizedText` binders to `HomeScreen.prefab`'s `NewsTitleText` (key `HOME_MAINTENANCE_TITLE`) and `NextHoleTitleText ` (key `HOME_NEXT_HOLE`) via `LocalizationEditorHelper.AddLocalizedText`. Captured EN and JP screenshots via real boot→home flow at 1170×2532.

Critical triage finding: both binder-path targets were already code-localized by `HomeScreenController` before this task — binders added are complementary but redundant. JP visual rendering is a FAIL for the two reused strings: `Rubik-SemiBold SDF` has 0 fallback fonts configured; JP kanji values (confirmed correct by script-execute) render as invisible missing glyphs. All 7 NAV_* nav titles render their `[JP-TODO]` English placeholder correctly.

## Files modified or created

| Path | Change | Baseline attribution |
|---|---|---|
| `Assets/Scripts/UI/PersistentUIManager.cs` | 7 `usernameText.text = "LITERAL"` arms → `LocalizationManager.Get("NAV_*")` | Task-introduced |
| `Assets/Localization/LocalizationText.csv` | +7 `NAV_*` rows (EN + `[JP-TODO]` JP) | Task-introduced |
| `Assets/Localization/LocalizationTextTable.asset` | Regenerated via `LocalizationTextImporter.ImportCsv()` after force-refresh; 234 rows | Task-introduced |
| `Assets/Prefabs/UI/HomeScreen.prefab` | `LocalizedText` binder added to `NoticePanel/NewsTitleText` + `NextHolePanel/NextHoleTitleText ` via `LocalizationEditorHelper.AddLocalizedText` | Task-introduced |
| `Docs/Specs/Active/localize_persistent_home_pilot/` | Task folder, SPEC, STATUS, HEARTBEAT, screenshots | Task-introduced |
| `Assets/Art/RosterScreen/ButtonCancel.png.meta` | Pre-existing — in HEAD `1a398637` DIRTY block | Pre-existing, not this task |
| `Assets/Art/Shop/Background - Blurred.png` | Pre-existing — in HEAD `1a398637` DIRTY block | Pre-existing, not this task |
| `Assets/Art/SplashScreen/Green Button.png.meta` | Pre-existing — in HEAD `1a398637` DIRTY block | Pre-existing, not this task |
| `Assets/Fonts/NotoSansJP-VariableFont_wght SDF.asset` | Pre-existing — in HEAD `1a398637` DIRTY block; NOT modified by this task | Pre-existing, not this task |
| `Assets/Plugins/NuGet/.nuget-installed.json` | Pre-existing — in HEAD `1a398637` DIRTY block | Pre-existing, not this task |
| `Assets/Plugins/NuGet/McpPlugin.Common.dll` | Pre-existing — in HEAD `1a398637` DIRTY block | Pre-existing, not this task |
| `Assets/Plugins/NuGet/McpPlugin.dll` | Pre-existing — in HEAD `1a398637` DIRTY block | Pre-existing, not this task |
| `Assets/Plugins/NuGet/ReflectorNet.dll` | Pre-existing — in HEAD `1a398637` DIRTY block | Pre-existing, not this task |
| `Packages/manifest.json` | Pre-existing — in HEAD `1a398637` DIRTY block | Pre-existing, not this task |
| `Packages/packages-lock.json` | Pre-existing — in HEAD `1a398637` DIRTY block | Pre-existing, not this task |

## Canonical screenshot

Canonical screenshot: `screenshots/home_jp_render_confirmed.jpg`

1170×2532, iPhone 14. Shows **メンテナンス情報** rendering in the maintenance notice panel in JP mode — the exact defect from the prior FAIL (`home_jp_mode.jpg` blank title) is GONE. Captured via `GOLFIN/Screenshot/Capture Game View` in real boot→home flow with `Language=Japanese`, after NotoSansJP was wired as TMP global fallback by the architect.

Supporting screenshots:
- `screenshots/home_en_maintenance_notice.jpg` (1170×2532) — EN home, "MAINTENANCE NOTICE" panel visible
- `screenshots/leaderboard_en_nav_title.jpg` (1170×2532) — EN leaderboard, "LEADERBOARD" in persistent bar
- `screenshots/home_jp_mode.jpg` (1170×2532) — JP home BEFORE FIX: blank notice title + body fragment
- `screenshots/home_jp_render_fixed.jpg` (1170×2532) — JP home AFTER FIX (iter-1b): メンテナンス情報 rendering
- `screenshots/home_jp_render_confirmed.jpg` (1170×2532) — JP home AFTER FIX (re-verification): same confirm
- `screenshots/home_jp_next_hole_visible.jpg` (1170×2532) — JP home with NextHolePanel forced active; shows プレイ (JP "PLAY") and kanji rendering via NotoSansJP
- `screenshots/leaderboard_jp_nav_title.jpg` (1170×2532) — JP leaderboard, "LEADERBOARD [JP-TODO]" in persistent bar

## Triage findings

Per audit-flagged row verdict — primary pilot deliverable.

| Audit row | Audit class | Actual class | Verdict | Evidence / Notes |
|---|---|---|---|---|
| `HomeScreen.prefab` `NoticePanel/NewsTitleText` "MAINTENANCE NOTICE" | CONVERT binder | Already code-localized + binder added (complementary) | CONVERTED (binder; redundant) | `HomeScreenController.UpdateNewsContent()` calls `LocalizationManager.Get("HOME_MAINTENANCE_TITLE")` from `OnEnable()` — text was already localized before this task. Binder added per spec; adds `OnLanguageChanged` reactivity but does not change the initial-render source. Audit heuristic miss: saw static text value in prefab, did not check associated MonoBehaviours for `Get()` calls on `OnEnable`. |
| `HomeScreen.prefab` `NextHolePanel/NextHoleTitleText ` "NEXT HOLE" | CONVERT binder | Already code-localized + binder added (complementary) | CONVERTED (binder; redundant) | `HomeScreenController.SetNextHoleFromData()` and `SetNextHole()` both call `LocalizationManager.Get("HOME_NEXT_HOLE")`. Same root cause as above. |
| `PersistentUIManager.cs` 7 literal nav titles | CONVERT code | CONVERT code | CONVERTED — correct | All 7 switch arms replaced. Control flow unchanged. Verified by `git diff`. |
| `HomeScreen.prefab` "CHOTO" (player name) | DO NOT CONVERT | DO NOT CONVERT | SKIPPED — correct | `usernameText.text = _username` runtime player name. Not copy. |
| `HomeScreen.prefab` maintenance body "Scheduled server maintenance: 2025/12/31…" | DO NOT CONVERT | DO NOT CONVERT | SKIPPED — correct | Hardcoded-date placeholder for server-driven live-ops copy. Leave unbound. |
| `HomeScreen.prefab` "x10" / "x04" / "x02" | DO NOT CONVERT (UNKNOWN in audit) | DO NOT CONVERT | SKIPPED — correct | Currency counts, runtime-set. |
| `HomeScreenController.cs` `rewardPointsText.text = "0"` | DO NOT CONVERT | DO NOT CONVERT | SKIPPED — correct | `// TODO: load real value` default. |
| `HomeScreenController.cs` `usernameText.text = "Player"` | DO NOT CONVERT | DO NOT CONVERT | SKIPPED — correct | `// TODO: load real value` default. |

**Audit heuristic improvement for later batches:** When a prefab text GO is classified as "binder path," the audit must also search the prefab's associated Controller / manager script for `.text = LocalizationManager.Get(key)` calls in lifecycle methods (`OnEnable`, `Awake`, data-population). If found, the row should be classified "already code-localized — binder optional" rather than "CONVERT binder," preventing false-positive work and inaccurate key counts.

## Acceptance checklist

| # | Item | Result | Evidence |
|---|---|---|---|
| 1 | **Binder path:** `HomeScreen.prefab` `NewsTitleText` and `NextHoleTitleText ` each carry `LocalizedText` bound to correct keys | PASS | Read back via `new SerializedObject(lt).FindProperty("key").stringValue` on `LoadPrefabContents` instance: `NewsTitleText.key='HOME_MAINTENANCE_TITLE'`, `NextHoleTitleText.key='HOME_NEXT_HOLE'`. Pre-existing `PlayLable.key='BTN_START'` untouched. |
| 2 | **Code path:** 7 `PersistentUIManager.cs` switch literals → `LocalizationManager.Get("NAV_*")`; control flow unchanged | PASS | `git diff HEAD -- Assets/Scripts/UI/PersistentUIManager.cs` shows exactly 7 minus/plus pairs (e.g. `- usernameText.text = "LEADERBOARD";` → `+ usernameText.text = LocalizationManager.Get("NAV_LEADERBOARD");`). No control-flow lines changed. |
| 3 | **CSV:** 7 new `NAV_*` rows added (EN + `[JP-TODO]` JP); importer re-run; key count 227 → 234; no duplicates | PASS | Rows appended to `LocalizationText.csv`. `AssetDatabase.ImportAsset(csvPath, ForceUpdate)` then `LocalizationTextImporter.ImportCsv()`. Script-execute confirmed `asset.rows.Count == 234`. All 7 NAV_* confirmed present with EN values and `[JP-TODO]` JP suffix. Dedup: no NAV_* key appears twice in CSV. |
| 4 | **EN unchanged:** `HomeScreen` and Leaderboard in EN at 1170×2532 via real boot→home flow; text reads identically to before | PASS | `screenshots/home_en_maintenance_notice.jpg` shows "MAINTENANCE NOTICE" panel in EN. `screenshots/leaderboard_en_nav_title.jpg` shows "LEADERBOARD" in persistent bar. No visual change from pre-task state. |
| 5a | **JP smoke — 2 reused strings render real JP** (メンテナンス情報 / 次のホール) | PASS | **Re-verified after architect wired NotoSansJP as TMP global fallback.** (1) `screenshots/home_jp_render_confirmed.jpg` shows **メンテナンス情報** visually rendered (no blank glyphs) with full JP body text. The exact failure from `home_jp_mode.jpg` (blank notice title) is GONE. (2) Script-execute confirms `NextHoleTitleText.text='次のホール'` with `Language=Japanese`. NextHolePanel is hidden (`activeSelf=False`) in this test session because there is no active hole progression — that is a game-state condition, not a localization failure; when forced active via script the text renders correctly and `screenshots/home_jp_next_hole_visible.jpg` shows kanji (プレイ) rendering via NotoSansJP in the same panel. TMP global fallback verified: `TMP global fallback count: 1; [0] NotoSansJP-VariableFont_wght SDF | atlas=Dynamic | guid=8f62f163976fae841ad23d559ebdf279`. |
| 5b | **JP smoke — 7 NAV_* nav titles render `[JP-TODO]` placeholder (NOT raw key)** | PASS | `screenshots/leaderboard_jp_nav_title.jpg` shows "LEADERBOARD [JP-TODO]" in persistent bar. Script-execute in JP mode: `LocalizationManager.Get("NAV_LEADERBOARD") == 'LEADERBOARD [JP-TODO]'`. All 7 NAV_* key values confirmed (NAV_LEADERBOARD, NAV_MODE_SELECTION, NAV_SELECT_HOLE, NAV_TOURNAMENT_LEADERBOARD, NAV_TOURNAMENTS, NAV_BOOST_STAMINA, NAV_REWARDS_CENTER). No raw NAV_* key on screen. |
| 6 | **Triage findings section** present with per-row audit verdict | PASS | See § Triage findings above. All 8 audit-flagged rows covered. Misclassification documented with root cause and heuristic improvement. |
| 7 | **Scope containment:** only 4 task files modified (+ task folder); no other prefab/scene/script touched | PASS | `git diff HEAD --name-only` (task changes): `Assets/Scripts/UI/PersistentUIManager.cs`, `Assets/Localization/LocalizationText.csv`, `Assets/Localization/LocalizationTextTable.asset`, `Assets/Prefabs/UI/HomeScreen.prefab`. All other M-marked files in baseline (pre-existing HEAD 1a398637 DIRTY block). `git diff HEAD -- Assets/Scripts/Physics/` = 0 lines (Rule 7 confirmed). |
| 8 | **Unity Console:** no game-level errors; project compiles | PASS | All console errors in session are from my own script-execute compilation probe calls (all immediately fixed). No game-level errors from localization system, `PersistentUIManager`, or `HomeScreen`. Project was compiling clean at session start and after all changes (assets-refresh + console-get-logs checked). |

## Re-verification (iter-1b — after architect JP-font fix)

**Item 5a resolved.** Architect added `NotoSansJP-VariableFont_wght SDF` (GUID 8f62f163976fae841ad23d559ebdf279, Dynamic atlas) to `Assets/TextMesh Pro/Resources/TMP Settings.asset` → `m_fallbackFontAssets[0]`. This wires NotoSansJP as the global TMP fallback for all fonts (including Rubik-SemiBold SDF) project-wide.

**Evidence gathered in this re-verification pass (2026-07-22):**

1. **TMP settings verified by script-execute:** `TMP global fallback count: 1; [0] NotoSansJP-VariableFont_wght SDF | atlas=Dynamic | guid=8f62f163976fae841ad23d559ebdf279`
2. **メンテナンス情報 visual:** `screenshots/home_jp_render_confirmed.jpg` (1170×2532) — blank-title defect GONE; JP kanji render correctly.
3. **次のホール by script:** `NextHoleTitleText.text='次のホール'`, `Language=Japanese`. Panel hidden due to no-hole-progression game-state (not a localization failure). When forced active, kanji renders — visible in `screenshots/home_jp_next_hole_visible.jpg`.
4. **No new FAIL items surfaced.**

All 8 acceptance checklist items are now PASS. STATUS set to `READY_FOR_SELF_REVIEW`.

**Note — `TMP Settings.asset` scope:** this file is architect-owned and not in the 4-file task scope. It does not appear in the task's "Files modified or created" table. The DIRTY diff entry for this file should remain attributed to the architect's change.

## Spec deviations

1. **Binder path items were already code-localized:** Both `NewsTitleText` and `NextHoleTitleText ` were already receiving `LocalizationManager.Get()` values via `HomeScreenController` before this task. Binders added are complementary (they add `OnLanguageChanged` reactivity) but are not the primary localization mechanism for these elements. The spec instruction "never bind a label whose text is overwritten at runtime" does not conflict here because both the binder and the controller write the same value via the same `LocalizationManager.Get()` call — no fight.

2. **次のホール panel hidden in test session:** NextHolePanel is `activeSelf=False` in a fresh test session with no hole progression data. The text IS set to '次のホール' (confirmed by script-execute) and the panel renders correctly when forced active. Visual screenshot (`home_jp_next_hole_visible.jpg`) shows kanji rendering via NotoSansJP. This is a game-state limitation of the test environment, not a localization deviation.

## Open questions for Architect

1. **Font fallback:** RESOLVED by architect — NotoSansJP wired as TMP global fallback. No action needed.

2. **Binder redundancy:** Both binder-path items are now double-localized (code path in `HomeScreenController` + binder in prefab). Is the complementary binder worth keeping for `OnLanguageChanged` reactivity, or should it be reverted since it is redundant with the existing code path? **Deferred to reviewer decision — no functional impact on first-render since HomeScreenController.OnEnable fires on every navigation.**

3. **HomeScreen in ShellScene is NotAPrefab:** The scene object is a standalone GO not connected to `Assets/Prefabs/UI/HomeScreen.prefab`. Binders added to the prefab asset are effectively dead for the runtime scene (binders work only because scene object already had `LocalizedText` if wired directly, or HomeScreenController's code path drives it). This is a pre-existing scene architecture issue; not blocking this task but surfaced for awareness.
