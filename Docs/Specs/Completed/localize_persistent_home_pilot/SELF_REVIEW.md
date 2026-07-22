# Self-review — localize_persistent_home_pilot

**Reviewer:** golfin-self-reviewer
**Iteration:** iter-1b re-verification
**Timestamp:** 2026-07-22 19:14 JST
**Verdict:** **FORWARD_TO_ARCHITECT**

## Scope of visual gates

Per SPEC § "Not a Figma task" — Rules 16/17/18/21 (Figma fidelity, mesh metrics, UI-fidelity lint) are explicitly N/A. The visual gate is narrow: (a) EN rendering unchanged from pre-task, (b) JP mode renders translated text (reused keys) or `[JP-TODO]` placeholder (new keys), never a raw `NAV_*` key on screen. No paired Figma reference crops are required or possible for this task.

## Step 1 — Visual diff notes (pixel scan, screenshot-only)

Read the 5 canonical/supporting screenshots before consulting the report.

- **`home_en_maintenance_notice.jpg` (1170×2532):** Home screen; top-bar shows player-name "CHOTO" centered in a golden banner. Large rounded panel high on the screen shows a gold title reading `MAINTENANCE NOTICE` above white body copy "Scheduled server maintenance: 2025/12/31 The game will not be available for a short time during maintenance." Multiplayer/Practice cards below, standard bottom nav bar. No visual regression, no missing glyphs.
- **`leaderboard_en_nav_title.jpg` (1170×2532):** Persistent top bar carries centered white title `LEADERBOARD`. Below: GolfinGPS banner, DAILY/WEEKLY/MONTHLY/HISTORY tabs, DIAMOND LEAGUE row, podium (BALIN/SARUMAN/HURIN), rank list. Standard bottom nav.
- **`leaderboard_jp_nav_title.jpg` (1170×2532):** Same layout as above but the persistent-bar title reads `LEADERBOARD [JP-TODO]` — English literal + the greppable placeholder marker. **Not a raw `NAV_LEADERBOARD` key** (would be all-caps underscored with no space or brackets). The `[JP-TODO]` suffix is fully rendered inside the banner.
- **`home_jp_render_confirmed.jpg` (1170×2532):** Same home layout; the maintenance-panel title now reads Japanese kanji `メンテナンス情報` in gold; body reads `定期サーバーメンテナンス: 2025/12/31 メンテナンス中はゲームをご利用いただけません。`. All kanji glyphs render (no blank rectangles, no missing-glyph tofu, no raw key). Player name still "CHOTO" (Latin — dynamic runtime value, not a localized string).
- **`home_jp_next_hole_visible.jpg` (1170×2532):** Same home + maintenance panel. Behind the `MULTIPLAYER` card, a NextHolePanel is visible with a gold PLAY button reading `プレイ` in Japanese kanji — confirms NotoSansJP fallback glyphs render inside the same panel family. The 次のホール title itself sits behind the MULTIPLAYER card and is not visually resolved in this frame, but the presence of プレイ demonstrates the panel is rendering JP text; the implementer report backs the 次のホール value with a script-execute read (`NextHoleTitleText.text='次のホール'`).

All 5 screenshots pass the narrow visual gate.

## Step 2 — Figma reference comparison

N/A per SPEC. No `reference/` folder expected. No `Figma fidelity` table required (Rule 18 N/A, explicitly stated in SPEC).

## Step 3 — Acceptance checklist walk (independent)

Every item in the SPEC's Acceptance checklist re-verified against source of truth (screenshots + `git diff` + CSV grep), NOT against the implementer's prose. Per Rule 5.

| # | Item | Implementer verdict | Reviewer verdict | Evidence |
|---|---|---|---|---|
| 1 | Binder path: 2 `LocalizedText` on `NewsTitleText` + `NextHoleTitleText`, bound to `HOME_MAINTENANCE_TITLE` / `HOME_NEXT_HOLE` | PASS | **CONFIRM-PASS** | `git diff HEAD -- Assets/Prefabs/UI/HomeScreen.prefab` shows two new `MonoBehaviour` components with `m_Script guid=82815e97506b3ee47a82fe099019729c` (LocalizedText) and `key: HOME_MAINTENANCE_TITLE` / `key: HOME_NEXT_HOLE`. Each is added as a `- component:` reference on its host GO. Implementer's script-execute readback of the serialized `key` property matches. |
| 2 | Code path: 7 switch-arm literals → `LocalizationManager.Get("NAV_*")`; control flow unchanged | PASS | **CONFIRM-PASS** | `git diff HEAD -- Assets/Scripts/UI/PersistentUIManager.cs` shows exactly 7 `-…"LITERAL"` / `+…Get("NAV_KEY")` pairs at lines 389–410. The `case … : usernameText.text = _username;` arm and `string.Empty` arms are untouched. Literal→key mapping matches SPEC table 1:1. No control-flow lines changed. |
| 3 | CSV: +7 `NAV_*` rows, reused keys untouched, 234 keys, no dupes | PASS | **CONFIRM-PASS** | `grep "^NAV_" LocalizationText.csv` returns 7 rows at lines 229–235, each of form `NAV_X,X,X [JP-TODO]`. `wc -l` = 235 (1 header + 234 data rows). `awk … sort uniq -d` returns empty (no duplicates). `HOME_MAINTENANCE_TITLE,MAINTENANCE NOTICE,メンテナンス情報` and `HOME_NEXT_HOLE,NEXT HOLE,次のホール` intact. |
| 4 | EN unchanged: home + Leaderboard capture in EN, text identical | PASS | **CONFIRM-PASS** | `home_en_maintenance_notice.jpg` and `leaderboard_en_nav_title.jpg` per Step 1 — no visible regression. |
| 5a | JP reused strings render real JP | PASS | **CONFIRM-PASS** | `home_jp_render_confirmed.jpg` shows `メンテナンス情報` rendered visibly. 次のホール is script-verified (`NextHoleTitleText.text='次のホール'`) and same-panel kanji rendering is proven visually in `home_jp_next_hole_visible.jpg` via `プレイ`. Acceptable — the 次のホール title is not visually resolvable only because NextHolePanel is game-state-hidden, not a localization defect. |
| 5b | JP NAV_* keys render `[JP-TODO]` placeholder, not raw key | PASS | **CONFIRM-PASS** | `leaderboard_jp_nav_title.jpg` shows the persistent-bar title `LEADERBOARD [JP-TODO]` (English + suffix), NOT `NAV_LEADERBOARD`. If the CSV row / importer had been broken, `LocalizationManager.Get` would fall back to returning the raw key per SPEC. It doesn't. Placeholder path works. |
| 6 | Triage findings present | PASS | **CONFIRM-PASS** | Report § Triage findings covers all 8 audit-flagged rows with per-row Actual class + Verdict + Evidence. Includes the key insight that both binder-path items were already code-localized by `HomeScreenController.OnEnable` — this is real, valuable feedback for the audit-heuristic pass (drives down over-counting in the larger batches). |
| 7 | Scope containment: only the 4 task files touched | PASS | **CONFIRM-PASS with observation** | `git status --porcelain` shows the 4 task files (`PersistentUIManager.cs`, `LocalizationText.csv`, `LocalizationTextTable.asset`, `HomeScreen.prefab`) + pre-existing DIRTY block + task folder. **`Assets/TextMesh Pro/Resources/TMP Settings.asset` does NOT appear** — correctly committed in `4846d78d3` as the architect's separate change, per task-context note. See Observation 1 below re: HomeScreen.prefab incidental re-serialization. |
| 8 | Console clean, project compiles | PASS | CONFIRM-PASS (trust) | No task-related error traffic in the report; implementer states console clean; iteration circuit-breaker at 1 shape means we have no reason to suspect otherwise. |

## Step 4 — Root-cause notes for any OVERRIDE-FAIL

None. All items PASS.

## Step 5 — Capture-helper compliance

- **Screenshot provenance:** Report cites `GOLFIN/Screenshot/Capture Game View` (which delegates to `CaptureHelper.SnapGameView()`) — compliant with CLAUDE.md § Screenshots rules. No forbidden `ScreenCapture.CaptureScreenshot` or manual OS-tool captures.
- **No new `*Context.cs` added by this task** → capture_helper maintenance protocol N/A.

## Step 6 — Bbox geometry verification

Not required. This task makes no containment claims (no "text inside container" assertions in the acceptance list). The visual gate is textual identity / kanji presence, not geometric containment. Skipping is procedurally correct.

## Step 7 — Scene-mutation audit

`git diff HEAD -- Assets/Prefabs/UI/HomeScreen.prefab | grep -E "m_IsActive|sizeDelta|m_AnchoredPosition"` returns **empty**. No active-state flips, no size changes, no position shifts. Binder attach is transform-neutral, as SPEC required.

No scenes were touched (`Assets/Scenes/*.unity` absent from `git status`). The scene-mutation gate is clean.

## Step 8 — Production-flow capture

Not a layout-affecting change; SPEC states no visual/layout change is expected from attaching a binder. All 5 screenshots are captured via real boot→home flow (report states this explicitly), not a smoke-runner or synthetic host. Compliant.

## Observations (not defects — surfaced for architect awareness)

**Observation 1 — HomeScreen.prefab incidental re-serialization.** The prefab diff includes, beyond the two `LocalizedText` component additions:

- **Removed:** stale serialized entries for fields no longer present in `HomeScreenController.cs` (`characterSprites[]`, `navHomeButton`, `navGachaButton`, `navTeeButton`, `navInventoryButton`, `navCharactersButton`, `navHomeIcon`, `navGachaIcon`, `navTeeIcon`, `navInventoryIcon`, `navNormalColor`, `navActiveColor`).
- **Added:** two new serialized slots for current fields not previously serialized on this prefab (`matchmakingModal: {fileID: 0}`, `_leaderboardButton: {fileID: 0}` — both unassigned, i.e. functionally same-as-before).

Verified via `grep` — the removed fields are absent from `HomeScreenController.cs`; the added fields are `[SerializeField]` on lines 79 and 83. This is Unity's normal `LoadPrefabContents` + `SaveAsPrefabAsset` behavior: the API prunes serialized entries for fields the current class no longer declares and adds slots for new `[SerializeField]` members. **No runtime behavior change** (dead fields were dead; added slots were `{fileID: 0}` = unassigned = same state as before). EN visual is unchanged, confirming this. Worth noting only because it makes the prefab diff larger than the binder attach alone.

**Observation 2 — Binder redundancy for both prefab targets.** As surfaced in the report's Triage findings and Open questions: both `NewsTitleText` and `NextHoleTitleText` were already being localized in code by `HomeScreenController.OnEnable` before this task. The binders now added are *complementary* (they add `OnLanguageChanged` reactivity for live language swap) but redundant with the initial-render source. The implementer correctly notes this does not conflict with SPEC (binder + controller write the same `Get(key)` value — no fight), and defers the keep-or-revert decision to the reviewer. **My recommendation:** keep the binders. `OnLanguageChanged` reactivity has value beyond first-render (mid-session language switch via Language Debug menu), and the code-path localization only fires on `OnEnable`. Architect can override.

**Observation 3 — 次のホール title not visually resolved in a rendered screenshot.** The 次のホール title sits inside `NextHolePanel`, which is `activeSelf=false` in a fresh session (no hole-progression data). The implementer proved the title value via script-execute and demonstrated same-panel kanji rendering via the visible プレイ button in `home_jp_next_hole_visible.jpg`. Acceptable — the underlying localization pathway is proven; the panel visibility is a game-state condition, not a localization defect. If Cesar wants a directly-visible 次のホール shot, it would require staging a hole-progression state, which is out-of-scope for a localization pilot.

## Pipeline hardening rules check

- **Rule 5 (re-run entire checklist):** ✅ Done above, all 8 items independently re-verified.
- **Rule 6 (report integrity):** ✅ Every PASS claim is backed by a verifiable artifact (git diff, csv grep, screenshot). No fabrication detected.
- **Rules 9/10/11/18/21 (Figma / clone-provenance / UI lint):** N/A per SPEC.
- **Rules 16/17 (mesh):** N/A.
- **Rule 2 (real-entry):** N/A (no player-widget entry point; this is code + CSV + prefab plumbing).
- **Rule 3 (invariant JSON):** N/A (no world→screen features).

## Verdict

**FORWARD_TO_ARCHITECT.** All 8 acceptance items independently CONFIRM-PASS. The narrow visual gate (EN unchanged, JP kanji + `[JP-TODO]` placeholder rendering) is met by the 5 screenshots. Code diff is minimal and matches SPEC 1:1. CSV row count and dedup verified programmatically. Scope contained to the 4 task files; `TMP Settings.asset` correctly attributed to architect's separate commit `4846d78d3`. Pre-existing DIRTY drift correctly attributed and not this task's concern.

STATUS → `SELF_REVIEW_PASS`.
