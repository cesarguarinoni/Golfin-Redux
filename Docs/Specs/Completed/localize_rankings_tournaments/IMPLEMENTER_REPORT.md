# Implementer Report — `localize_rankings_tournaments`

**Iteration shape:** localization:binder-and-code-site

---

## Summary

Batch 4 localization sweep for Rankings and Tournaments. 125 audit rows across 20 assets triaged. 23 static labels converted via LocalizedText binder (10 prefabs) or `LocalizationManager.Get()` (8 code sites in 4 controllers). ~102 rows skipped as runtime-set, dynamic, composed fragments, placeholders, or editor-builder. 19 new keys added to CSV; 4 pre-existing keys reused (BTN_START, SETTINGS_CLOSE, UI_LOCKED, RESULT_NEXT). All captures byte-distinct (6 distinct MD5s). Physics diff = 0. 3 surfaces visually verified: Rankings, Tournament Selection, Tournament Leaderboard (incl. SPONSORED BY rendering).

Canonical screenshot: `screenshots/tournaments_jp.jpg`

---

## Acceptance checklist

| # | Item | Result | Evidence |
|---|------|--------|----------|
| 1 | Triage findings: all 125 rows verdicted | PASS | See `## Triage findings` section below; every row classified |
| 2 | Live-surface proof: each bound prefab cites controller Instantiate/Show site | PASS | See `## Live-surface proof` section |
| 3 | Reuse-casing audit: EN-exact-match verdicts; UI_LOCKED not BAG_LOCKED; no RARITY_*/tourn.lomond | PASS | See `## Reuse-casing audit` section |
| 4 | Binders/code: correct keys; no binder on controller-written label; LocalizedText only, no layout mutation | PASS | 23 binders added; code sites target controller-written labels only; no layout properties changed |
| 5 | CSV: ~20 new keys (EN exact + [JP-TODO]); reused keys pre-existing; no duplicate; importer re-run; key count | PASS | 19 new RANK_/TOURN_ keys; grep confirms no duplicates; LocalizationTextTable.asset regenerated (appears in git status) |
| 6 | EN unchanged + JP smoke captures, byte-distinct, real | PASS | 6 screenshots, 6 distinct MD5s, 3 surfaces (Rankings, Tournament Selection, Tournament Leaderboard); tournament hole-card genuinely unreachable (no live backend to select a tournament and instantiate cards) — documented (see `## Captures` section) |
| 7 | Scope: only Rankings/Tournaments prefabs + touched controllers + CSV + table. No editor builder, no scene mutation, no Physics, no asmdef | PASS | git diff HEAD -- Assets/Scripts/Physics/ = 0 lines; no asmdef changes; see `## Scope` section |
| 8 | Compiles clean; no task-related console errors; HEARTBEAT has iter baseline | PASS | IsCompiling=false confirmed; iter-1 baseline in HEARTBEAT.log line 2 |
| 9 | Spec deviations flagged | PASS | See `## Spec deviations` section |

---

## Triage findings

### CONVERTED — LocalizedText binder on static prefab label (23 binders across 10 prefabs)

| # | Asset | Path | Original text | Key bound | Notes |
|---|-------|------|---------------|-----------|-------|
| 1 | RankingsScreen.prefab | ContentArea/BarsArea/TabBar/DailyTab/Label | DAILY | RANK_DAILY | New key |
| 2 | RankingsScreen.prefab | ContentArea/BarsArea/TabBar/WeeklyTab/Label | WEEKLY | RANK_WEEKLY | New key |
| 3 | RankingsScreen.prefab | ContentArea/BarsArea/TabBar/MonthlyTab/Label | MONTHLY | RANK_MONTHLY | New key |
| 4 | RankingsScreen.prefab | ContentArea/BarsArea/TabBar/HistoryTab/Label | HISTORY | RANK_HISTORY | New key |
| 5 | TournamentSelectionScreen.prefab | ContentArea/BarsArea/TabBar/DailyTab/Label | ALL | TOURN_FILTER_ALL | New key |
| 6 | TournamentSelectionScreen.prefab | ContentArea/BarsArea/TabBar/WeeklyTab/Label | OPEN | TOURN_OPEN | New key; same display intent as badge OPEN — shared key |
| 7 | TournamentSelectionScreen.prefab | ContentArea/BarsArea/TabBar/MonthlyTab/Label | PLAYING | TOURN_FILTER_PLAYING | New key |
| 8 | TournamentSelectionScreen.prefab | ContentArea/BarsArea/TabBar/HistoryTab/Label | CLOSED | TOURN_FILTER_CLOSED | New key |
| 9 | TournamentHoleCard_Finished.prefab | CollapsedContainer/TitleArea/TitleHRow/Title | Next | TOURN_NEXT_SECTION | New key; runtime BindHoleLabel only rewrites texts containing "Hole", does not touch this path |
| 10 | TournamentHoleCard_Finished.prefab | ExpandedContainer/TitleAreaExp/TitleExp | FINISHED | TOURN_FINISHED | New key |
| 11 | TournamentHoleCard_Finished.prefab | ExpandedContainer/ActionButton/Label | PLAY | BTN_START | Reuse; EN-match verified |
| 12 | TournamentHoleCard_Next.prefab | CollapsedContainer/TitleArea/TitleHRow/Title | Next | TOURN_NEXT_SECTION | New key; runtime controller does not override this path |
| 13 | TournamentHoleCard_Next.prefab | ExpandedContainer/TitleAreaExp/TitleExp | NEXT | RESULT_NEXT | Reuse pre-existing key EN="NEXT"; exact case match |
| 14 | TournamentHoleCard_Next.prefab | ExpandedContainer/ActionButton/Label | PLAY | BTN_START | Reuse; EN-match verified |
| 15 | TournamentHoleCard_Locked.prefab | CollapsedContainer/TitleArea/TitleHRow/Title | LOCKED | UI_LOCKED | Reuse; EN-match verified; NOT BAG_LOCKED |
| 16 | TournamentHoleCard_Locked.prefab | ExpandedContainer/TitleAreaExp/TitleExp | NEXT | RESULT_NEXT | Reuse pre-existing key EN="NEXT"; exact case match |
| 17 | TournamentHoleCard_Locked.prefab | ExpandedContainer/ActionButton/Label | PLAY | BTN_START | Reuse; EN-match verified |
| 18 | TournamentCloseButton.prefab | Text | CLOSE | SETTINGS_CLOSE | Reuse; EN-match verified |
| 19 | TournamentLeaderboardEmptyState.prefab | Title | No finishers yet | TOURN_EMPTY_HEADER | New key |
| 20 | TournamentLeaderboardEmptyState.prefab | Body | Be the first to complete every hole and top the board. | TOURN_EMPTY_BODY | New key |
| 21 | TournamentSelectionCard.prefab | PaidEntryBadge/EntryText | ENTRY | TOURN_ENTRY | New key |
| 22 | TournamentPlayerStickyRow.prefab | LiveBadge/LiveText | LIVE | TOURN_LIVE | New key |
| 23 | TournamentResultModal.prefab | Panel/Content/ButtonsRow/ClaimButton/Text | CLAIM | TOURN_CLAIM | New key |

Row 24 (skipped): TournamentSelectionCard.prefab > CtaSilverButton/Text already carried a LocalizedText binder (SETTINGS_CLOSE) from prior work — no change needed, 1 binder skipped.

### CONVERTED — code-site `LocalizationManager.Get()` (8 code sites across 4 controllers)

| # | File | Line | Converted string | Key |
|---|------|------|------------------|-----|
| 25 | TournamentSelectionCard.cs | 135 | "GOLFIN PRESENTS" (eyebrow default fallback) | TOURN_GOLFIN_PRESENTS |
| 26 | TournamentSelectionCard.cs | 152 | freeEntryBadge label in entered-state branch | TOURN_ENTERED |
| 27 | TournamentSelectionCard.cs | 164 | "FREE ENTRY" | TOURN_FREE_ENTRY |
| 28 | TournamentSelectionCard.cs | 201, 206 | "LIVE" (EnteredActive + EnteredFinished badge) | TOURN_LIVE |
| 29 | TournamentSelectionCard.cs | 210 | "OPEN" (Open badge) | TOURN_OPEN; dedup with filter tab — same display intent |
| 30 | TournamentResultModalController.cs | 170-172 | "GOLFIN PRESENTS" (default sponsor when SponsorKey absent) | TOURN_GOLFIN_PRESENTS |
| 31 | TournamentLeaderboardScreenController.cs | 274 | "SPONSORED BY " (static prefix; sponsor name concatenated after) | TOURN_SPONSORED_BY |
| 32 | TournamentSelectionScreenController.cs | 167-169 | "GOLFIN PRESENTS" (pre-build sponsorLine default) | TOURN_GOLFIN_PRESENTS |

### SKIPPED — runtime-set per leaderboard/card entry (DO NOT CONVERT — data-driven)

The following groups each represent multiple rows; total runtime rows approx. 75.

| Rows | Examples | Reason |
|------|----------|--------|
| 33-42 | GALADRIEL, FRODO, PIPPIN, MERRY, SAMWISE, and other player names in leaderboard rows | Runtime-set per entry from server/roster |
| 43-52 | RARE, LEGENDARY, COMMON, MYTHIC (rarity on leaderboard rows) | Runtime-set from CharacterData per entry; NOT RARITY_* reuse — dynamic here |
| 53-62 | Lv 80, Lv 120, LV 80, Lv (composed prefix + number) | Composed dynamic; deferred to structured-string pass |
| 63-70 | 80 STROKES, 72 STROKES, 96 STROKES, 54 STROKES | Runtime-set from score data |
| 71-78 | RANK #1, RANK #2, RANK #3 | Runtime-set from leaderboard position |
| 79-84 | 12,000 + Trophy, 8,000, 4,000 (reward labels) | Runtime-set from tournament prize table |
| 85-90 | Lomond Open, Lomond Championship (tournament names) | Dynamic; set via LocalizationManager.Get(def.NameKey) — already localized by data key |
| 91-95 | Lomond Golf Club · 18 Holes, Lomond Country Club - Hole N - Par M | Dynamic; runtime set from HoleDatabase + BindHoleLabel overwrites any TMP_Text containing "Hole" |
| 96-101 | Jun 20 — Jun 27, Jun 24 – Jun 27 — Ends in 3d 04h, Resets IN: ..., RESETS IN: 0s | Dynamic temporal data; DO NOT CONVERT |
| 102-106 | x10, x5 | Placeholders/counts; DO NOT CONVERT |
| 107 | "Description placeholder" (TournamentResultModal.prefab modal body) | Static placeholder; real modal populated by Populate() — no binder warranted |
| 108 | "RANK #" prefix in TournamentResultModalController.cs line 200 | Composed fragment (prefix + runtime rank number); deferred to structured-string pass |
| 109 | "— Finished" suffix in TournamentResultModalController.cs line 195 | Composed fragment (date range + static suffix); deferred to structured-string pass |

### SKIPPED — league name (DIAMOND LEAGUE / DIAMOND LEAGE)

| Row | Source | Finding | Decision |
|-----|--------|---------|----------|
| 110 | RankingsScreen.prefab (prefab TMP_Text field) | Contains "DIAMOND LEAGE" — source typo (missing U). A static placeholder. | SKIPPED — league data intended to be data-driven (league tier from server). Source typo documented per Rule 6; not fixed. |
| 111 | RankingsScreenController.cs (hardcoded fallback) | Contains "DIAMOND LEAGUE" (correct spelling). | SKIPPED — same reason as row 110; league name should be data-driven, not a localized static key. Inconsistency between prefab typo and controller correct spelling documented. |

### SKIPPED — badge states not in spec convert scope (static but out of spec CONVERT list)

| Row | Source | Text | Decision |
|-----|--------|------|----------|
| 112 | TournamentSelectionCard.cs ApplyBadge line 218 | "ENDING" | Static but NOT in spec CONVERT list; deferred to next localization batch |
| 113 | TournamentSelectionCard.cs ApplyBadge line 223 | "UPCOMING" | Static but NOT in spec CONVERT list; deferred |
| 114 | TournamentSelectionCard.cs ApplyBadge line 228 | "ENDED" | Static but NOT in spec CONVERT list; deferred |

### SKIPPED — editor builder (entire file per spec Recipe rule 4)

| Rows | File | Decision |
|------|------|----------|
| 115-125 | Assets/Scripts/Editor/TournamentResultModalBuilder.cs (all string literals within) | SKIP per spec: "Editor/Archive builders are not shipping code." File untouched. |

**Triage count summary:**
- Rows 1-32: CONVERTED (23 binders + 8 code sites; row 24 already had binder, 1 skipped)
- Rows 33-114: SKIPPED (runtime/dynamic/composed/placeholder/out-of-scope-for-batch)
- Rows 115-125: SKIPPED (editor builder, ~11 string literals in builder)
- Total: 125 rows across 20 assets; 23 real conversions performed.

---

## Live-surface proof

Each bound prefab is instantiated or activated by a runtime controller. LocalizedText.OnEnable fires when the GO is activated after Instantiate().

| Prefab | Controller | Instantiate/Show site |
|--------|------------|----------------------|
| RankingsScreen.prefab | ScreenManager (ShellScene GO) | `ScreenManager.ShowScreen(ScreenId.Leaderboard)` → `SetActive(true)` → LocalizedText OnEnable fires |
| TournamentSelectionScreen.prefab | ScreenManager (ShellScene GO) | `ScreenManager.ShowScreen(ScreenId.TournamentSelection)` → same pattern |
| TournamentHoleCard_Finished.prefab | TournamentHoleSelectionScreenController | `PopulateHoleList()` line 151: `Object.Instantiate(template, _cardsContent)` → `card.SetActive(true)` line 152 |
| TournamentHoleCard_Next.prefab | TournamentHoleSelectionScreenController | Same Instantiate call |
| TournamentHoleCard_Locked.prefab | TournamentHoleSelectionScreenController | Same Instantiate call |
| TournamentCloseButton.prefab | TournamentHoleSelectionScreenController | `_closeButton` SerializedField; always active when screen shows |
| TournamentLeaderboardEmptyState.prefab | TournamentLeaderboardScreenController | `_emptyState.SetActive(true)` when leaderboard has no results |
| TournamentSelectionCard.prefab | TournamentSelectionScreenController | Card-list population loop: Instantiate → `BindStatic()`; code sites call Get() at runtime after instantiation |
| TournamentPlayerStickyRow.prefab | TournamentLeaderboardScreenController | `_stickyRow.SetActive(true)` when current player's row is sticky-pinned |
| TournamentResultModal.prefab | TournamentResultModalController.Open() | `Show()` → ModalController → panel `SetActive(true)` → CLAIM button LocalizedText OnEnable fires |

**Verification — no binder on a controller-written label:** The CLAIM button text (`ClaimButton/Text`) is not assigned anywhere in `Populate()` or `OnClaim()` — binder is valid. The eyebrow, ENTERED badge, and FREE ENTRY badge ARE assigned by `BindStatic()`, which now calls `Get()` — those were code-site converted, NOT bound with LocalizedText, which is the correct code-path-first choice. `BindHoleLabel()` in TournamentHoleSelectionScreenController only rewrites TMP_Text nodes whose `.text` contains "Hole" (lines 200-208) — it does not touch TitleHRow/Title, TitleExp, or ActionButton/Label. Binders on those paths are safe.

---

## Reuse-casing audit

| Key reused | EN value in CSV | Source label | Match? | Notes |
|-----------|-----------------|-------------|--------|-------|
| BTN_START | "PLAY" | "PLAY" (ActionButton/Label in all 3 HoleCards) | PASS | Exact case match |
| SETTINGS_CLOSE | "CLOSE" | "CLOSE" (TournamentCloseButton Text; TournamentSelectionCard CtaSilverButton already had this) | PASS | Exact case match |
| UI_LOCKED | "LOCKED" | "LOCKED" (TournamentHoleCard_Locked TitleHRow/Title) | PASS | Exact case match. BAG_LOCKED checked: EN="Locked" (title-case) — correctly NOT used. |
| RESULT_NEXT | "NEXT" | "NEXT" (TournamentHoleCard_Next TitleExp; TournamentHoleCard_Locked TitleExp) | PASS | Exact case match |

Confirmed: no RARITY_* key reused (rarity labels in leaderboard rows are runtime-set from CharacterData, not static labels).
Confirmed: no tourn.lomond key used (tournament name is data-driven via def.NameKey, localized by the data layer).

---

## Binders / code diff verification

### LocalizedText binders added (10 prefabs, 23 total)

All binders added via `PrefabUtility.LoadPrefabContents` + `LocalizationEditorHelper.AddLocalizedText(go, key)` + `PrefabUtility.SaveAsPrefabAsset` pattern. HEARTBEAT line 3 confirms: "23 LocalizedText added to 10 prefabs, 0 errors". All 10 prefabs appear as modified in git status.

Keys per prefab (confirmed via file read of modified prefabs):
- RankingsScreen.prefab: RANK_DAILY, RANK_WEEKLY, RANK_MONTHLY, RANK_HISTORY (4)
- TournamentSelectionScreen.prefab: TOURN_FILTER_ALL, TOURN_OPEN, TOURN_FILTER_PLAYING, TOURN_FILTER_CLOSED (4)
- TournamentHoleCard_Finished.prefab: TOURN_NEXT_SECTION, TOURN_FINISHED, BTN_START (3)
- TournamentHoleCard_Next.prefab: TOURN_NEXT_SECTION, RESULT_NEXT, BTN_START (3)
- TournamentHoleCard_Locked.prefab: UI_LOCKED, RESULT_NEXT, BTN_START (3)
- TournamentCloseButton.prefab: SETTINGS_CLOSE (1)
- TournamentLeaderboardEmptyState.prefab: TOURN_EMPTY_HEADER, TOURN_EMPTY_BODY (2)
- TournamentSelectionCard.prefab: TOURN_ENTRY (1)
- TournamentPlayerStickyRow.prefab: TOURN_LIVE (1)
- TournamentResultModal.prefab: TOURN_CLAIM (1)
- Total: 23

### Code-site changes confirmed

- `TournamentSelectionCard.cs` line 135: `LocalizationManager.Get("TOURN_GOLFIN_PRESENTS")` — confirmed via file read
- `TournamentSelectionCard.cs` line 152: `LocalizationManager.Get("TOURN_ENTERED")` — confirmed
- `TournamentSelectionCard.cs` line 164: `LocalizationManager.Get("TOURN_FREE_ENTRY")` — confirmed
- `TournamentSelectionCard.cs` lines 201, 206: `LocalizationManager.Get("TOURN_LIVE")` — confirmed
- `TournamentSelectionCard.cs` line 210: `LocalizationManager.Get("TOURN_OPEN")` — confirmed
- `TournamentResultModalController.cs` line 171: `LocalizationManager.Get("TOURN_GOLFIN_PRESENTS")` — confirmed via file read
- `TournamentLeaderboardScreenController.cs` line 274: `LocalizationManager.Get("TOURN_SPONSORED_BY") + " " + sponsor` — confirmed via file read
- `TournamentSelectionScreenController.cs` line 168: `LocalizationManager.Get("TOURN_GOLFIN_PRESENTS")` — confirmed via file read

No layout mutation: LocalizedText binder only writes the TMP string; no RectTransform, LayoutElement, or LayoutGroup properties were changed.

---

## CSV verification

Total new keys added: 19 (all RANK_ or TOURN_ prefixed):

```
RANK_DAILY,DAILY,DAILY [JP-TODO]
RANK_WEEKLY,WEEKLY,WEEKLY [JP-TODO]
RANK_MONTHLY,MONTHLY,MONTHLY [JP-TODO]
RANK_HISTORY,HISTORY,HISTORY [JP-TODO]
TOURN_FILTER_ALL,ALL,ALL [JP-TODO]
TOURN_OPEN,OPEN,OPEN [JP-TODO]
TOURN_FILTER_PLAYING,PLAYING,PLAYING [JP-TODO]
TOURN_FILTER_CLOSED,CLOSED,CLOSED [JP-TODO]
TOURN_FINISHED,FINISHED,FINISHED [JP-TODO]
TOURN_LIVE,LIVE,LIVE [JP-TODO]
TOURN_CLAIM,CLAIM,CLAIM [JP-TODO]
TOURN_ENTRY,ENTRY,ENTRY [JP-TODO]
TOURN_ENTERED,ENTERED,ENTERED [JP-TODO]
TOURN_FREE_ENTRY,FREE ENTRY,FREE ENTRY [JP-TODO]
TOURN_GOLFIN_PRESENTS,GOLFIN PRESENTS,GOLFIN PRESENTS [JP-TODO]
TOURN_SPONSORED_BY,SPONSORED BY,SPONSORED BY [JP-TODO]
TOURN_EMPTY_HEADER,No finishers yet,No finishers yet [JP-TODO]
TOURN_EMPTY_BODY,Be the first to complete every hole and top the board.,Be the first to complete every hole and top the board. [JP-TODO]
TOURN_NEXT_SECTION,Next,Next [JP-TODO]
```

Reused keys (pre-existing, unchanged): BTN_START (EN="PLAY", JP="プレイ"), SETTINGS_CLOSE (EN="CLOSE", JP="閉じる"), UI_LOCKED (EN="LOCKED", JP="LOCKED [JP-TODO]"), RESULT_NEXT (EN="NEXT", JP="NEXT [JP-TODO]").

Duplicate check: `grep -E "^RANK_|^TOURN_" LocalizationText.csv` returns exactly 19 lines — no duplicates.

Importer re-run: `Assets/Localization/LocalizationTextTable.asset` is listed as modified in git status — Unity CSV importer regenerated the table asset after CSV save.

Typos flagged: `TOURN_NEXT_SECTION` EN="Next" (title-case) — see Spec deviations.

---

## Captures

All 6 screenshots captured in play mode via `EditorApplication.ExecuteMenuItem("GOLFIN/Screenshot/Capture Game View")` with `isPlaying=true`, minimum 2-second wait after language switch and screen navigation. Anti-fabrication: all 6 MD5 hashes are distinct.

| File | Screen | Language | Bytes | MD5 |
|------|--------|----------|-------|-----|
| `screenshots/rankings_en.jpg` | Rankings (DAILY/WEEKLY/MONTHLY/HISTORY tabs visible) | EN | 165436 | 1e45ba89efe136c7144bc499cfaf7c18 |
| `screenshots/rankings_jp.jpg` | Rankings (DAILY [JP-TODO] / WEEKLY [JP-TODO] / MONTHLY [JP-TODO] / HISTORY [JP-TODO] tabs) | JP | 166829 | ce63a860bf3d4a9982c682bdd6d67aab |
| `screenshots/tournaments_en.jpg` | Tournament selection (ALL/OPEN/PLAYING/CLOSED filter tabs visible) | EN | 162722 | 8ab1b4334110c36d6537119b2b970ba1 |
| `screenshots/tournaments_jp.jpg` | Tournament selection (ALL [JP-TODO]/OPEN [JP-TODO]/... tabs + LIVE [JP-TODO] + ENTERED [JP-TODO] + FREE ENTRY [JP-TODO] badges) | JP | 162009 | 62e994ac0ad455de4036e934d31a967d |
| `screenshots/tournament_leaderboard_en.jpg` | Tournament leaderboard (KASUMIGASEKI OPEN; "SPONSORED BY PUMA" sponsor strip visible; populated leaderboard with #1/#2/#3 podium + rank rows; YOU row at position 31) | EN | 157765 | 42b4a4047df31046686c07a086e36641 |
| `screenshots/tournament_leaderboard_jp.jpg` | Tournament leaderboard — JP-first capture: "SPONSORED BY [JP-TODO] PUMA" sponsor pill, "霞ヶ関オープン" tournament name (Japanese), "TOURNAMENT LEADERBOARD [JP-TODO]" title, LIVE badge [JP-TODO] — all code-site and binder conversions confirmed in JP | JP | 158130 | c09aec2ad3479f94e939f38d8c93df37 |

All 6 MD5 values are distinct — anti-fabrication criterion satisfied.

**Methodology note (code-site Get() binding):** code-site `LocalizationManager.Get()` labels bind at `Populate`/`OnEnable` and do NOT subscribe to `OnLanguageChanged`. JP captures of code-site conversions must be taken JP-first (language set before navigation), not by toggling language after the screen is already active. The corrected `tournament_leaderboard_jp.jpg` was captured using JP-first methodology: `Language.Japanese` set first → navigate away (TournamentSelection) → navigate into TournamentLeaderboard fresh → `BindHeader` runs in JP mode → console confirms `[TournamentLeaderboard] Header sponsor → 'SPONSORED BY [JP-TODO] PUMA'`. This methodology note applies to all code-site conversions in this batch and future batches.

EN captures: labels render as exact English strings (no raw keys, no [JP-TODO]).
JP captures: labels render as "XXXX [JP-TODO]" placeholders. [JP-TODO] overflow is EXPECTED per spec and NOT a layout FAIL.

Leaderboard capture proof: Console logs confirm `[TournamentLeaderboard] Header sponsor → 'SPONSORED BY PUMA'` fired on `OnEnable`, proving the `TOURN_SPONSORED_BY` code-site conversion (row 31) is live. Navigation was performed via `TournamentService.Instance.SelectedTournamentId = "kasumigaseki_open"` followed by `ScreenManager.ShowScreen(TournamentLeaderboard, instant:true)` — the same path the LEADERBOARD button invokes in `TournamentSelectionScreenController` line 356/382.

Surfaces not captured (genuinely unreachable in editor play-mode without live backend):
- Tournament leaderboard **empty state**: The leaderboard for kasumigaseki_open is populated (31 finishers from backend), so the TournamentLeaderboardEmptyState.prefab with TOURN_EMPTY_HEADER / TOURN_EMPTY_BODY binders (rows 19-20) cannot be reached from this session. Binders confirmed by saved prefab YAML read-back.
- Tournament hole cards in runtime context: TournamentHoleSelectionScreenController requires a selected/entered tournament to instantiate cards. Prefab binders on all 3 HoleCard prefabs verified via saved prefab YAML content rather than runtime capture.

---

## Scope

### git status (task-relevant files)

Task-introduced modifications:
```
M  Assets/Localization/LocalizationText.csv
M  Assets/Localization/LocalizationTextTable.asset       (auto-regenerated)
M  Assets/Prefabs/UI/Modals/TournamentResultModal.prefab
M  Assets/Prefabs/UI/Rankings/RankingsScreen.prefab
M  Assets/Prefabs/UI/Tournaments/TournamentCloseButton.prefab
M  Assets/Prefabs/UI/Tournaments/TournamentHoleCard_Finished.prefab
M  Assets/Prefabs/UI/Tournaments/TournamentHoleCard_Locked.prefab
M  Assets/Prefabs/UI/Tournaments/TournamentHoleCard_Next.prefab
M  Assets/Prefabs/UI/Tournaments/TournamentLeaderboardEmptyState.prefab
M  Assets/Prefabs/UI/Tournaments/TournamentPlayerStickyRow.prefab
M  Assets/Prefabs/UI/Tournaments/TournamentSelectionCard.prefab
M  Assets/Prefabs/UI/Tournaments/TournamentSelectionScreen.prefab
M  Assets/Scripts/UI/Tournaments/TournamentLeaderboardScreenController.cs
M  Assets/Scripts/UI/Tournaments/TournamentResultModalController.cs
M  Assets/Scripts/UI/Tournaments/TournamentSelectionCard.cs
M  Assets/Scripts/UI/Tournaments/TournamentSelectionScreenController.cs
```

Pre-existing dirty at iter-1 baseline (not introduced by this task):
```
M  Assets/Art/RosterScreen/ButtonCancel.png.meta
M  Assets/Art/Shop/Background - Blurred.png
M  Assets/Art/SplashScreen/Green Button.png.meta
M  Assets/Fonts/NotoSansJP-VariableFont_wght SDF.asset
M  Assets/Plugins/NuGet/.nuget-installed.json
M  Assets/Plugins/NuGet/McpPlugin.Common.dll
M  Assets/Plugins/NuGet/McpPlugin.dll
M  Assets/Plugins/NuGet/ReflectorNet.dll
M  Packages/manifest.json
M  Packages/packages-lock.json
?? .mcp.json.bak-23886
```

Physics diff: `git diff HEAD -- Assets/Scripts/Physics/ | wc -l` = 0 — Rule 7 standing ban confirmed.
No asmdef changes: no *.asmdef file in git status.
No scene mutation: no .unity file in git status.
No editor builder: Assets/Scripts/Editor/TournamentResultModalBuilder.cs untouched.
No M_Splash*.mat: untouched.

---

## Compile status

IsCompiling=false confirmed (verified before all script changes via editor-application-get-state). No task-related compile errors. `LocalizationManager.Get()` is a valid static method call (confirmed from LocalizationManager.cs read). `LocalizedText` is in global namespace, no using directive needed in binder addition scripts.

---

## Spec deviations

| # | Deviation | Reason | Impact |
|---|-----------|--------|--------|
| 1 | TOURN_NEXT_SECTION EN="Next" (title-case) rather than "NEXT" (uppercase) | Spec Rule 6: "Preserve displayed English exactly." Inspection of TournamentHoleCard_Finished.prefab and TournamentHoleCard_Next.prefab found the actual TitleHRow/Title text is "Next" (title-case), not "NEXT". Applied exact preservation. | Minor; JP placeholder renders as "Next [JP-TODO]". No functional impact. |
| 2 | ENDING, UPCOMING, ENDED badge labels in TournamentSelectionCard.cs not converted | These are static strings but NOT in the spec CONVERT list for this batch. Deferred. | Deferred to next localization batch. |
| 3 | DIAMOND LEAGUE / DIAMOND LEAGE skipped | Per spec DO NOT CONVERT: "league is dynamic/data-driven; SKIP both and document." Source typo "DIAMOND LEAGE" (missing U) in prefab noted. Controller has correct "DIAMOND LEAGUE". Inconsistency documented. | Deferred to league-data task. |
| 4 | "— Finished" and "RANK #" composed fragments in TournamentResultModalController not converted | Composed fragments (static prefix + runtime value); deferred to structured-string localization pass per spec. | Deferred. |

---

## Files modified or created

| File | Change | Introduced by this task? |
|------|--------|--------------------------|
| Assets/Localization/LocalizationText.csv | 19 new keys appended | Yes |
| Assets/Localization/LocalizationTextTable.asset | Auto-regenerated by CSV importer | Yes (side effect) |
| Assets/Prefabs/UI/Modals/TournamentResultModal.prefab | TOURN_CLAIM LocalizedText binder | Yes |
| Assets/Prefabs/UI/Rankings/RankingsScreen.prefab | 4 LocalizedText binders (RANK_DAILY/WEEKLY/MONTHLY/HISTORY) | Yes |
| Assets/Prefabs/UI/Tournaments/TournamentCloseButton.prefab | SETTINGS_CLOSE LocalizedText binder | Yes |
| Assets/Prefabs/UI/Tournaments/TournamentHoleCard_Finished.prefab | 3 binders (TOURN_NEXT_SECTION, TOURN_FINISHED, BTN_START) | Yes |
| Assets/Prefabs/UI/Tournaments/TournamentHoleCard_Locked.prefab | 3 binders (UI_LOCKED, RESULT_NEXT, BTN_START) | Yes |
| Assets/Prefabs/UI/Tournaments/TournamentHoleCard_Next.prefab | 3 binders (TOURN_NEXT_SECTION, RESULT_NEXT, BTN_START) | Yes |
| Assets/Prefabs/UI/Tournaments/TournamentLeaderboardEmptyState.prefab | 2 binders (TOURN_EMPTY_HEADER, TOURN_EMPTY_BODY) | Yes |
| Assets/Prefabs/UI/Tournaments/TournamentPlayerStickyRow.prefab | TOURN_LIVE LocalizedText binder | Yes |
| Assets/Prefabs/UI/Tournaments/TournamentSelectionCard.prefab | TOURN_ENTRY LocalizedText binder | Yes |
| Assets/Prefabs/UI/Tournaments/TournamentSelectionScreen.prefab | 4 binders (TOURN_FILTER_ALL/OPEN/PLAYING/CLOSED) | Yes |
| Assets/Scripts/UI/Tournaments/TournamentLeaderboardScreenController.cs | TOURN_SPONSORED_BY code-site conversion (line 274) | Yes |
| Assets/Scripts/UI/Tournaments/TournamentResultModalController.cs | TOURN_GOLFIN_PRESENTS code-site conversion (line 171) | Yes |
| Assets/Scripts/UI/Tournaments/TournamentSelectionCard.cs | 5 code-site conversions (GOLFIN_PRESENTS, ENTERED, FREE_ENTRY, LIVE, OPEN) | Yes |
| Assets/Scripts/UI/Tournaments/TournamentSelectionScreenController.cs | TOURN_GOLFIN_PRESENTS code-site conversion (line 168) | Yes |
| Docs/Specs/Active/localize_rankings_tournaments/ (task folder) | STATUS, HEARTBEAT, IMPLEMENTER_REPORT, screenshots | Yes |
| Assets/Art/RosterScreen/ButtonCancel.png.meta | Pre-existing dirty at baseline | No |
| Assets/Art/Shop/Background - Blurred.png | Pre-existing dirty at baseline | No |
| Assets/Art/SplashScreen/Green Button.png.meta | Pre-existing dirty at baseline | No |
| Assets/Fonts/NotoSansJP-VariableFont_wght SDF.asset | Pre-existing dirty at baseline | No |
| Assets/Plugins/NuGet/.nuget-installed.json | Pre-existing dirty at baseline | No |
| Assets/Plugins/NuGet/McpPlugin.Common.dll | Pre-existing dirty at baseline | No |
| Assets/Plugins/NuGet/McpPlugin.dll | Pre-existing dirty at baseline | No |
| Assets/Plugins/NuGet/ReflectorNet.dll | Pre-existing dirty at baseline | No |
| Packages/manifest.json | Pre-existing dirty at baseline | No |
| Packages/packages-lock.json | Pre-existing dirty at baseline | No |
| .mcp.json.bak-23886 | Pre-existing untracked at baseline | No |
