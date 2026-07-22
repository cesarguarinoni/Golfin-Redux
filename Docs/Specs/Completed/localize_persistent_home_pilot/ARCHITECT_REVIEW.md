# Architect Review — localize_persistent_home_pilot

**Reviewer:** golfin-reviewer
**Iteration:** iter-1b
**Timestamp:** 2026-07-22 JST
**Verdict:** PASS → `READY_FOR_REDTEAM`

## Independent visual scan (pixel-level, done before reading any report)

- `home_jp_render_confirmed.jpg` (1170×2532): golden CHOTO banner at top, then a rounded blue notice panel high on the screen carrying a gold title `メンテナンス情報` and two lines of body copy `定期サーバーメンテナンス: 2025/12/31 メンテナンス中はゲームをご利用いただけません。`. All kanji glyphs are fully drawn — no missing-glyph tofu boxes, no blank areas, no raw `HOME_MAINTENANCE_TITLE` key text. Multiplayer/Practice cards, GOLFIN GPS banner, and bottom nav render normally underneath.
- `leaderboard_jp_nav_title.jpg` (1170×2532): persistent top bar centers `LEADERBOARD [JP-TODO]` in white on the golden banner — English literal plus the `[JP-TODO]` greppable placeholder marker, both fully rendered inside the banner. NOT `NAV_LEADERBOARD` (would be all-caps underscored without brackets/space). Podium and rank list below are visually unchanged from the EN capture.
- `home_en_maintenance_notice.jpg` and `leaderboard_en_nav_title.jpg` (both 1170×2532): identical layouts to their JP counterparts with English strings (`MAINTENANCE NOTICE` / `LEADERBOARD`). No visible regression relative to the pre-task state.

Pixel scan matches the report's claims — advancing.

## Not a Figma / mesh / clone-provenance / UI-lint task

Per SPEC § "Not a Figma task": this is a text-binding conversion, not a visual redesign. The gates are explicit:

- **Rule 16/17 (mesh metrics + mesh-bake video):** N/A — no terrain, mesh, `TerrainData`, or geometry surface touched.
- **Rule 18 (Figma fidelity table):** N/A — SPEC references no Figma node. No `reference/` node renders exist; none are required.
- **Rule 19 (clone provenance):** N/A — no §0 REUSE MANDATE, no clone-and-modify prefabs.
- **Rule 21 (UI fidelity lint):** N/A — no new prefab authored; existing prefab receives 2 additive `LocalizedText` binders with no layout/geometry change.

The gate this task actually has is the narrow one SPEC declares: **EN renders identically, JP renders translated (reused keys) or the `[JP-TODO]` placeholder (new keys), never a raw `NAV_*` key on screen.** All four screenshots satisfy that gate.

## Full acceptance re-verification (Rule 5 — independent, per row)

| # | Item | Verdict | Independent evidence |
|---|---|---|---|
| 1 | Binder path: 2 new `LocalizedText` on `NewsTitleText` + `NextHoleTitleText`, bound to `HOME_MAINTENANCE_TITLE` / `HOME_NEXT_HOLE` | PASS | `git diff HEAD -- Assets/Prefabs/UI/HomeScreen.prefab` shows two new `MonoBehaviour` blocks with `m_Script guid=82815e97506b3ee47a82fe099019729c` (verified against `LocalizedText.cs.meta` — this IS the real `LocalizedText`) carrying `key: HOME_MAINTENANCE_TITLE` and `key: HOME_NEXT_HOLE`, each added as a `- component:` reference on the respective GO block (`NewsTitleText` fileID 2088671532724511047, `NextHoleTitleText ` fileID 5592495834984368451). |
| 2 | Code path: 7 switch-arm literals → `LocalizationManager.Get("NAV_*")`; control flow unchanged | PASS | `git diff HEAD -- Assets/Scripts/UI/PersistentUIManager.cs` shows exactly 7 minus/plus pairs (lines 389–410). Literal→key mapping matches SPEC 1:1 (`LEADERBOARD`→`NAV_LEADERBOARD`, `MODE SELECTION`→`NAV_MODE_SELECTION`, `SELECT HOLE`→`NAV_SELECT_HOLE`, `TOURNAMENT LEADERBOARD`→`NAV_TOURNAMENT_LEADERBOARD`, `TOURNAMENTS`→`NAV_TOURNAMENTS`, `BOOST STAMINA`→`NAV_BOOST_STAMINA`, `REWARDS CENTER`→`NAV_REWARDS_CENTER`). `case … : usernameText.text = _username;` arm and `string.Empty` arms untouched — control flow preserved. |
| 3 | CSV: +7 `NAV_*` rows, EN + `[JP-TODO]`, reused keys untouched, 234 keys, no dupes | PASS | `grep "^NAV_" LocalizationText.csv` → 7 rows at 229–235, each `NAV_X,X,X [JP-TODO]`. `wc -l` = 235 (1 header + 234 data rows). `awk … sort uniq -d` → empty (no duplicates). Reused rows intact: `HOME_MAINTENANCE_TITLE,MAINTENANCE NOTICE,メンテナンス情報` and `HOME_NEXT_HOLE,NEXT HOLE,次のホール` still present with their original JP values. |
| 4 | EN unchanged: home + Leaderboard at 1170×2532 via real boot→home flow | PASS | `home_en_maintenance_notice.jpg` renders `MAINTENANCE NOTICE` + English body identically. `leaderboard_en_nav_title.jpg` renders `LEADERBOARD` in the persistent bar. No visual delta versus pre-task expectation. |
| 5a | JP reused strings render real JP | PASS | `home_jp_render_confirmed.jpg` shows `メンテナンス情報` + full-body JP kanji rendering cleanly (architect's `4846d78d3` NotoSansJP TMP global fallback wire, out of this task's scope, made this possible). `次のホール` value confirmed by script-execute readback in the report; same-panel kanji rendering visually proven by the `プレイ` glyphs in `home_jp_next_hole_visible.jpg`. `NextHolePanel` `activeSelf=false` in this test session is a game-state condition (no hole progression), not a localization defect. |
| 5b | JP NAV_* → `[JP-TODO]` placeholder, NOT raw key | PASS | `leaderboard_jp_nav_title.jpg` renders `LEADERBOARD [JP-TODO]` — the CSV round-trip works. If the importer or `Get()` wiring were broken, `LocalizationManager` would return the raw key `NAV_LEADERBOARD` per its fallback (SPEC-cited behavior). It doesn't. |
| 6 | Triage findings present, honest per-row | PASS | Report § Triage findings covers all 8 audit-flagged rows with per-row Actual class + Verdict + Evidence. The primary insight — that both binder-path targets were ALREADY code-localized by `HomeScreenController.OnEnable`, meaning the audit heuristic over-counted — is captured with root cause and a concrete audit-heuristic improvement for later batches. This is exactly the pilot-workflow deliverable SPEC asked for. |
| 7 | Scope containment: only the 4 task files touched | PASS | `git status --porcelain --untracked-files=all` shows the 4 files (`Assets/Scripts/UI/PersistentUIManager.cs`, `Assets/Localization/LocalizationText.csv`, `Assets/Localization/LocalizationTextTable.asset`, `Assets/Prefabs/UI/HomeScreen.prefab`) plus the task folder. **`Assets/TextMesh Pro/Resources/TMP Settings.asset` is absent from the working tree** — verified via `git log --oneline -5 -- 'Assets/TextMesh Pro/Resources/TMP Settings.asset'` → committed in `4846d78d3 fix(localization): wire NotoSansJP as TMP global fallback for Japanese`, the architect's separate commit. Pre-existing DIRTY drift on `Art/*`, `Fonts/NotoSansJP-VariableFont_wght SDF.asset`, `Plugins/NuGet/*`, `Packages/*` is baseline (correctly attributed per Rule 13 in the report). |
| 8 | Console clean, project compiles | PASS-trust | Report states console clean and project compiles; `PersistentUIManager.cs` change is `.text = "LITERAL"` → `.text = LocalizationManager.Get("...")` (same return type), `LocalizationManager` is in the global namespace so no new using directive needed; the change is trivially compile-safe. |

## Scrutinized: HomeScreen.prefab incidental re-serialization

Full `git diff HEAD -- Assets/Prefabs/UI/HomeScreen.prefab` = 94 lines. Explicit grep for state mutations:

```
git diff HEAD -- Assets/Prefabs/UI/HomeScreen.prefab | grep -E "m_IsActive|sizeDelta|m_AnchoredPosition|m_LocalPosition|m_LocalScale|m_LocalRotation"
→ EMPTY
```

**No active-state flips, no size changes, no position/rotation shifts, no scale mutations.** Scene/prefab-mutation gate is clean.

Diff content breakdown (every line accounted for):

1. **Intended: 2 `LocalizedText` MonoBehaviour additions** with `m_Script guid=82815e97506b3ee47a82fe099019729c` (matches `Assets/Localization/LocalizedText.cs.meta`), `m_Enabled=1`, `key: HOME_MAINTENANCE_TITLE` on GO `2088671532724511047` (NewsTitleText) and `key: HOME_NEXT_HOLE` on GO `5592495834984368451` (NextHoleTitleText).
2. **Intended: 2 `- component:` references** appended to those GOs' component lists — the standard sibling entries any new component requires.
3. **Incidental serialization pruning** on the `HomeScreenController` `MonoBehaviour` block (fileID for the HomeScreen root): removed serialized entries for `characterSprites[]`, `navHomeButton`, `navGachaButton`, `navTeeButton`, `navInventoryButton`, `navCharactersButton`, `navHomeIcon`, `navGachaIcon`, `navTeeIcon`, `navInventoryIcon`, `navCharactersIcon`, `navNormalColor`, `navActiveColor`. Independently verified against the current class: `grep -n "characterSprites\|navHomeButton\|navGachaButton\|navTeeButton\|navInventoryButton\|navCharactersButton\|navHomeIcon\|navGachaIcon\|navTeeIcon\|navInventoryIcon\|navCharactersIcon\|navNormalColor\|navActiveColor" Assets/Scripts/UI/HomeScreenController.cs` → **zero matches**. These fields no longer exist on the class, so Unity's `LoadPrefabContents` + `SaveAsPrefabAsset` correctly pruned their dead serialized entries.
4. **Incidental serialization additions**: `matchmakingModal: {fileID: 0}` and `_leaderboardButton: {fileID: 0}`. Independently verified: `grep -n "matchmakingModal\|_leaderboardButton" Assets/Scripts/UI/HomeScreenController.cs` → `line 79: [SerializeField] private MatchmakingModalController matchmakingModal;` and `line 83: [SerializeField] private Button _leaderboardButton;`. Both are current `[SerializeField]` members that had not yet been serialized on this prefab; Unity added the slots with unassigned values (`{fileID: 0}`). Since they were previously ABSENT (not serialized), and are now serialized as UNASSIGNED, the runtime `null` check outcome is identical — `if (matchmakingModal != null)` and `if (_leaconstoardButton != null)` both false either way. No behavior change.

Conclusion: the "extra" prefab churn is pure serialization sync (dead fields pruned, current `[SerializeField]` slots added with `null`/`{fileID: 0}` values). **No changed values, no disabled objects, no re-parenting, no layout mutation.** Benign. The EN screenshot (`home_en_maintenance_notice.jpg`) rendering identically to pre-task confirms nothing changed visually.

## Pipeline hardening rules

- **Rule 2 (real entry point):** N/A — no player-widget entry point; this is CSV + code + prefab binder plumbing.
- **Rule 3 (invariant JSON):** N/A — no world→screen projection.
- **Rule 5 (re-run entire checklist):** ✅ Every acceptance item re-verified independently against source of truth (git diff, csv grep, screenshot pixel scan, cross-file field check).
- **Rule 6 (report integrity):** ✅ Every PASS claim is backed by an artifact I could independently reproduce. No fabricated quotes, tests, or approvals found.
- **Rules 9/10/11/18/21:** N/A per SPEC.
- **Rules 16/17 (mesh):** N/A.
- **Scene-mutation audit (Rule 5.4 in the visual checklist):** ✅ Prefab grep for `m_IsActive|sizeDelta|m_AnchoredPosition|m_LocalPosition|m_LocalScale|m_LocalRotation` returns empty.
- **Production-flow capture:** ✅ All screenshots captured via real boot→home flow at iPhone 14 1170×2532, per report.

## Observations (surfaced for red-team + Cesar)

1. **Binder + code-path complementary localization** on both prefab targets. The self-reviewer's recommendation to keep the binders (mid-session `OnLanguageChanged` reactivity has value beyond `OnEnable`-only first-render) is sound; keeping matches SPEC's binder-path acceptance item. Reverting would leave only `OnEnable` reactivity. Cesar override welcome; no functional risk either way.
2. **`次のホール` not directly visible in a full-flow rendered frame** because `NextHolePanel.activeSelf=false` without hole progression. Script-execute readback + same-panel `プレイ` kanji in `home_jp_next_hole_visible.jpg` prove the pathway. Acceptable for a localization pilot; a directly-visible shot would require staging hole progression state (out of scope for text-binding work).
3. **The valuable audit-heuristic finding** — that the audit's binder-path classification double-counts elements already code-localized in a controller's `OnEnable` / data-population path — is the exact kind of feedback the pilot was designed to produce. Later batches (Shop/Gacha 251, Other 282, Rankings/Tournaments 125, Hole/Results 114, Inventory/Bag 62) should be re-triaged against controller-side `Get()` calls before their SPECs are authored.

## Verdict

**PASS.** All 8 acceptance items independently CONFIRM-PASS. The narrow visual gate (EN unchanged, JP renders translated for reused keys and `[JP-TODO]` for new keys, never a raw `NAV_*` on screen) is met by the four canonical screenshots. Code diff is minimal and matches SPEC 1:1. CSV row count and dedup verified programmatically. Prefab diff contains ONLY the intended 2 `LocalizedText` binders + benign serialization sync (dead fields pruned, new `[SerializeField]` slots added with `null`) — no state mutation, no layout change, EN screenshot confirms zero visible delta. Scope contained to the 4 task files; `TMP Settings.asset` correctly committed separately in `4846d78d3` and absent from the working tree; pre-existing DIRTY drift correctly attributed.

STATUS → `READY_FOR_REDTEAM`. Hands to `golfin-redteam-reviewer` for adversarial gate before Cesar's approval.

---

# Red-team Review — localize_persistent_home_pilot

**Reviewer:** golfin-redteam-reviewer
**Timestamp:** 2026-07-22 19:24 JST
**Verdict:** `ARCHITECT_REVIEW_PASS`

I re-generated every piece of evidence myself (no carried-forward PASS). Attack vectors below, hardest-first.

## Attack 1 — JP render is real, not a placeholder/English dressed up (own inspection)
Opened `screenshots/home_jp_render_confirmed.jpg` (1170×2532) directly. Title reads **メンテナンス情報** — genuine kanji, NOT "MAINTENANCE NOTICE", NOT a raw `HOME_MAINTENANCE_TITLE` key, no tofu boxes. Body reads `定期サーバーメンテナンス: 2025/12/31 メンテナンス中はゲームをご利用いただけません。` — all glyphs render. Opened `leaderboard_jp_nav_title.jpg`: persistent bar reads **`LEADERBOARD [JP-TODO]`** — the intended placeholder, NOT raw `NAV_LEADERBOARD`. Cross-checked CSV row 229: `NAV_LEADERBOARD,LEADERBOARD,LEADERBOARD [JP-TODO]` — matches on screen. **Could not break.**

## Attack 2 — Raw-key leak hunt (both JP frames)
Scanned both JP screenshots for any ALL-CAPS underscore token (`NAV_…`, `HOME_…`). None present. The English literals visible in JP mode (MULTIPLAYER, PLAY, DAILY, DIAMOND LEAGUE) are out-of-scope other-group copy that legitimately has no JP yet — they are literal English, not raw keys. **Could not break.**

## Attack 3 — Code path under all switch arms (read lines 377–415, not just the diff)
All 7 converted arms call `Get("NAV_*")`; each key char-for-char matches a CSV row (verified: NAV_LEADERBOARD/MODE_SELECTION/SELECT_HOLE/TOURNAMENT_LEADERBOARD/TOURNAMENTS/BOOST_STAMINA/REWARDS_CENTER all present in both code and CSV). The `case Home: usernameText.text = _username;` arm and the `default: string.Empty` arm are untouched. No arm Get()s a key absent from the CSV. **Could not break.**

## Attack 4 — Prefab diff re-derived independently
`git diff HEAD -- HomeScreen.prefab`: grep for `m_IsActive|sizeDelta|m_AnchoredPosition|m_LocalPosition|m_LocalScale|m_LocalRotation` = EMPTY. The 2 new components are `LocalizedText` (`m_Script guid=82815e97506b3ee47a82fe099019729c`, verified == `LocalizedText.cs.meta`), `m_Enabled: 1`, `key: HOME_MAINTENANCE_TITLE` on GO 2088671532724511047 (NewsTitleText) and `key: HOME_NEXT_HOLE` on GO 5592495834984368451 (NextHoleTitleText). **The load-bearing skeptic check:** the removed serialized entries (`characterSprites[]`, `navHomeButton`, all `nav*Button/Icon/Color`) — I did NOT trust "they're dead fields." `grep` of `HomeScreenController.cs` → ZERO matches: the fields no longer exist on the class, so no wired reference was lost. The 2 added `{fileID: 0}` slots (`matchmakingModal`, `_leaderboardButton`) ARE current `[SerializeField]` members (lines 79/83), added unassigned = same null outcome as before. `HomeScreenController.cs` is NOT modified in the working tree — the drift predates this task; the prefab merely re-serialized to the current class shape. Benign. **Could not break.**

## Attack 5 — Binder vs code double-write divergence
Controller writes `Get("HOME_MAINTENANCE_TITLE")` → newsTitleText (line 250) and `Get("HOME_NEXT_HOLE")` → nextHoleTitleText (lines 304/334) — the **exact same keys** the two binders carry. Binder and code cannot disagree. **Could not break.**

## Attack 6 — CSV + importer integrity (independent)
CSV: 234 data rows (235 total − header), zero duplicate keys (`awk … sort uniq -d` empty), 7 `NAV_*` rows all `NAME,EN,EN [JP-TODO]`, reused rows intact (`HOME_MAINTENANCE_TITLE,…,メンテナンス情報` / `HOME_NEXT_HOLE,…,次のホール`). Importer genuinely ran: `LocalizationTextTable.asset` has 234 keys and contains `japanese: 'LEADERBOARD [JP-TODO]'` — proving the CSV round-tripped into the runtime table (this is why the on-screen render isn't a raw key). **Could not break.**

## Attack 7 — Scope
`git status --porcelain` = 4 task files (`PersistentUIManager.cs`, `LocalizationText.csv`, `LocalizationTextTable.asset`, `HomeScreen.prefab`) + task folder + pre-existing DIRTY baseline (Art/Fonts/NuGet/Packages) + stray `.mcp.json.bak`. `TMP Settings.asset` absent from working tree (committed separately as `4846d78d3`, architect scope). `git diff HEAD -- Assets/Scripts/Physics/` = empty. **Could not break.**

## Attack 8 — Compile
The running build produced `LEADERBOARD [JP-TODO]` on screen, which requires both the new `Get("NAV_LEADERBOARD")` call AND the imported CSV — end-to-end proof the code compiled and executed. `LocalizationManager` is global-namespace, no new using needed. **Could not break.**

## Prior rejections
No `CESAR_REJECTION.md` in the folder. The only prior FAIL was the intra-pipeline JP-font blocker (blank kanji), resolved by the architect's `4846d78d3` NotoSansJP TMP global-fallback wire — confirmed GONE in `home_jp_render_confirmed.jpg` (kanji render).

## Surfaced for Cesar (documented, not a blocker)
The runtime `HomeScreen` in ShellScene is a standalone scene GO not connected to `HomeScreen.prefab`, so the 2 prefab binders are effectively inert in-game; JP renders via the `HomeScreenController.OnEnable` **code path** (proven by the real-flow screenshot). SPEC acceptance item 1 required binding the PREFAB and reading back the key — done and verified — so this does not falsify any acceptance criterion. It is transparently surfaced in IMPLEMENTER_REPORT Open Question #3 + both prior reviews, and is exactly the recipe-feedback the pilot exists to produce. Cesar may decide whether the binder pattern should target scene instances in later batches.

## Verdict
Genuinely attempted to break 8 vectors; all held with my own re-generated evidence. All acceptance items independently CONFIRM-PASS. STATUS → `ARCHITECT_REVIEW_PASS`.
