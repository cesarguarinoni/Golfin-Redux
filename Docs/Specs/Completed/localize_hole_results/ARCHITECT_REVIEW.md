# Architect Review — `localize_hole_results` (iter-2)

**Reviewer:** golfin-reviewer
**Timestamp:** 2026-07-22 22:50 JST
**Verdict:** **PASS → READY_FOR_REDTEAM**
**Task type:** Localization batch 3 (Hole/Results). Not a Figma task — Rules 16/17/18/21 N/A. Not a mesh/terrain task — Step 2 mesh-metrics N/A. No `SPEC.md` §11 invariant table — Rule 3 N/A. No player-visible new widget — Rule 2 real-entry N/A (labels replaced in-place; live surfaces cited).

---

## Independent pixel scan (Step 0)

Opened three JP captures before reading `IMPLEMENTER_REPORT.md` / `SELF_REVIEW.md`:

- **`versus_result_jp.jpg`** — real Japanese renders on unrelated reused headers (`メンテナンス情報` / `定期サーバーメンテナンス`) and on peripherals (`プレイ` on the mid-screen PLAY button). New keys resolve as `[JP-TODO]` placeholders: `RESULTS`, `WINNER`/`LOSER` (they collide, expected), `Vs.` (spills into the portrait pair, expected), `HOLE`, `NEW MATCH` (spills the gold button, expected), `You`, `REWARDS`, `ENTRY FEE`. No raw `KEY_UI` on screen, no tofu.
- **`hole_complete_success_jp.jpg`** — `SUCCESS [JP-TODO]` green, `REPLAY [JP-TODO]` overflowing its button (expected), `NEXT [JP-TODO]` orange. Peripheral `プレイ` (BTN_START) rendering real Japanese on the next-card PLAY button. Content is genuinely distinct from the EN counterpart.
- **`matchmaking_modal_jp.jpg`** — `DIAMOND LEAGE [JP-TODO]` (typo preserved), `FINDING OPPONENT [JP-TODO]...` (runtime dots appended), `Vs. [JP-TODO]` wrap-overflowing between portraits (expected), `HOLE [JP-TODO]`, `キャンセル` (MODAL_CANCEL real JP) on the cancel button.

EN captures (`hole_complete_failed_en.jpg`, `versus_result_en.jpg`, `matchmaking_modal_en.jpg`, `home_mode_card_en.jpg`, `hole_complete_success_en.jpg`) — layouts tight, no truncation, no overflow, correct casing (`SUCCESS`/`FAILED`/`LOCKED`/`RETRY`/`RESULTS`/`WINNER`/`LOSER`/`NEW MATCH`/`DIAMOND LEAGE`/`ENTRY FEE`/`REWARDS` all-caps as designed; `Vs.` mixed; `You` mixed on versus screen, `YOU` uppercased by TMP CaseSetting on matchmaking modal — a display-time setting unrelated to localization). No raw keys anywhere.

Pixel scan aligns with the report's claims — no fabrication signal.

---

## Independent verification of the seven review-prompt items

### 1. Anti-fabrication — PASS

10 screenshots, 10 distinct MD5 hashes (verified with `md5 *.jpg`):

```
hole_complete_failed_en.jpg  6bd68fd0fc51829d59896c8553e8d7c4
hole_complete_failed_jp.jpg  0212a826073d4c2b8c789c87c3f2e188
hole_complete_success_en.jpg 7fa80b8bd80b5ed400515ba61b995832
hole_complete_success_jp.jpg 79d256865259f4d1619837346b8cda14
home_mode_card_en.jpg        57cfed4b0487a4b8dcab9188adf7746f
home_mode_card_jp.jpg        2f776100a15279dd489022dcb223c8e9
matchmaking_modal_en.jpg     bba6110b340fe8bb0fbccff5f9d6f0cd
matchmaking_modal_jp.jpg     c047ddcbae6a2bcbfcf2317370008dc3
versus_result_en.jpg         5fe4a8054832802425434c69bb9adaca
versus_result_jp.jpg         9871710880678fa303f01c4d3a6ac3e1
```

Opened 3 JP captures (see § Independent pixel scan). Real, distinct Japanese content matching each filename. Iter-1 sin does not recur.

### 2. LOCKED casing fix — PASS

- CSV line 255: `UI_LOCKED,LOCKED,LOCKED [JP-TODO]` — EN exactly `LOCKED` all-caps.
- CSV line 135: `BAG_LOCKED,Locked,ロック` — unchanged (title-case for batch-2 inventory consumers).
- `HoleCompleteWidget.prefab`: `grep -c "key: BAG_LOCKED"` = **0**, `grep -c "key: UI_LOCKED"` = **2** (Card1 + Card2 LockedHeader/Label).
- `BagSlotLockedPrefab.prefab`: `grep -c "key: BAG_LOCKED"` = **0**, `grep -c "key: UI_LOCKED"` = **1**.
- Canonical `hole_complete_failed_en.jpg` visibly renders `LOCKED` all-caps on the second card lock header.

### 3. Reuse-casing audit + HOME_COURSE_NAME skip — PASS

Spot-checked EN values in CSV against the displayed strings in the batch:

| Key | CSV EN | Displayed | Verdict |
|---|---|---|---|
| `BTN_START` | `PLAY` | PLAY (all-caps) on peripheral cards | match |
| `HOME_USERNAME` | `USERNAME` | Used only as VersusResultScreenController sample text (dev-only) | match |
| `MODAL_CANCEL` | `CANCEL` | CANCEL (all-caps) on matchmaking modal button | match |

`HOME_COURSE_NAME` explicitly NOT bound in any batch prefab (grep across all 6 prefabs = 0 hits). Course names correctly stay dynamic (`HoleCardController.cs:146` composes `$"Lomond Country Club  - Hole {n} - Par {p}"` at runtime, matching the SKIP verdict).

### 4. Binders + code — PASS

- Only one script GUID inserted across all 6 modified prefab diffs: `guid: 82815e97506b3ee47a82fe099019729c` (= `Assets/Localization/LocalizedText.cs.meta`). No stray components.
- Total binders across the 6 prefabs: **31** (14 + 4 + 4 + 5 + 3 + 1) — matches report:
  - `HoleCompleteWidget.prefab`: 14 (BTN_START ×2, RESULT_FAILED ×2, RESULT_NEXT ×2, RESULT_REPLAY ×2, RESULT_RETRY ×2, RESULT_SUCCESS ×2, UI_LOCKED ×2)
  - `MatchMakingModal.prefab`: 4 (MATCH_DIAMOND_LEAGUE, MATCH_HOLE, MATCH_VS, MODAL_CANCEL)
  - `VersusResultScreen.prefab`: 4 (MATCH_HOLE, MATCH_VS, RESULT_NEW_MATCH, RESULT_RESULTS)
  - `ModeCard.prefab`: 5 (BTN_START, MODE_ENTRY_FEE ×2, MODE_REWARDS ×2)
  - `ModeHomeCard.prefab`: 3 (BTN_START, MODE_ENTRY_FEE, MODE_REWARDS)
  - `BagSlotLockedPrefab.prefab`: 1 (UI_LOCKED)
- Scene-mutation audit: `git diff HEAD` grep for `m_IsActive|sizeDelta|m_AnchoredPosition|m_LocalPosition` across all 6 prefabs → **0 lines each**. No scene-state mutation.
- Controller edits verified by direct source read:
  - `HoleCardController.cs`: `Get("UI_LOCKED")` line 138, `Get("RESULT_REPLAY_HOLE")` line 139, `Get("RESULT_NEXT")` line 140, `Get("RESULT_REPLAY")` line 183, `Get("BTN_START")` line 188. Line 170 pre-existing dynamic key.
  - `MatchmakingModalController.cs`: `Get("MATCH_YOU")` line 213, `Get("MATCH_FINDING_OPPONENT")` line 394 (DotCycleRoutine + dots suffix). Lines 307/310 pre-existing (HOME_NEXT_HOLE / hole.courseNameKey — dynamic).
  - `VersusResultScreenController.cs`: `Get("MATCH_YOU")` line 157; `Get("RESULT_WINNER"/"RESULT_LOSER")` at 345/350 (SetOutcomeLabels), 381/386 (SetOutcomeLabelsLive); `Get("HOME_USERNAME")` at 394/395 (SetSampleText).
  - `ModeCardController.cs`: clean key-lookup-with-fallback at `SetTitleText` (line 293) — `"MODE_"+title.ToUpper().Replace(" ","_")` with graceful fallback when the key resolves to itself.
- No binder placed on any controller-written label (`WINNER`/`LOSER`/`You`/`FINDING OPPONENT`/prefab `USERNAME` defaults are all code-site only per spec recipe rule 3).

### 5. Live-surface proofs — PASS

Report cites each prefab's live-surface entry point:
- `HoleCompleteWidget.prefab` — `HoleCompleteModalController._widget.Show(uiData, callback)` (`HoleCompleteModalController.cs:122`; scene-instance binder propagation is standard Unity behavior). iter-2 verified by direct `widget.Show()` in play mode and captured the rendered output.
- `MatchMakingModal.prefab` — root controller on the ShellScene prefab instance; opened via `MatchmakingModalController.Open()`; live JP capture (`matchmaking_modal_jp.jpg`) shows real Japanese `キャンセル` on the CANCEL button, proving the binder actually wired the CANCEL text.
- `VersusResultScreen.prefab` — `VersusResultModalController._screen` `[SerializeField]` at `VersusResultModalController.cs:39`, driven by `VersusResultHandler` → `_resultModal.ShowResult()` → `_screen.ShowResult()`. Live JP capture shows RESULTS/WINNER/LOSER/NEW MATCH all resolving.
- `ModeCard.prefab` — `ModeSelectScreenController.cardPrefab` `[SerializeField]` at `ModeSelectScreenController.cs:26`, Instantiated at runtime.
- `ModeHomeCard.prefab` — verified live via `home_mode_card_en.jpg` / `home_mode_card_jp.jpg` (ENTRY FEE / REWARDS / PLAY labels resolve on the Home screen).

No inert-binder risk left unproven.

### 6. Triage (114 rows) — PASS

- Every distinct string value in the audit group verdicted (CONVERTED / SKIPPED / DEFERRED) in § Triage findings.
- Row-count reasoning present: ~40 CONVERT-source rows (21 distinct labels × source occurrences), ~60–70 SKIP rows (names/ranks/course strings/counts/level badges across GOs and prefab states), editor-builder remainder.
- `HoleCard.prefab` explicit verdict row present (SKIP-at-prefab / CONVERT-at-code-site — controller writes at runtime).
- Spot-checked 3 SKIPs:
  - `SHAE` / opponent name — runtime from roster.
  - `RANK: #142` — dynamic composite; `RANK:` prefix localization deferred.
  - `x100`/`x200` currency counts — runtime from match/reward data.

### 7. CSV integrity — PASS

- Total lines: 256 = 255 keys + 1 header.
- Duplicate scan (`awk -F, 'NR>1{print $1}' | sort | uniq -d`) = **empty**.
- `UI_LOCKED` present at line 255 with EN=`LOCKED`.
- `RESULT_REPLAY_HOLE` present at line 256 with EN=`REPLAY HOLE`.
- `MATCH_DIAMOND_LEAGUE,DIAMOND LEAGE,DIAMOND LEAGE [JP-TODO]` — typo preserved byte-identical, flagged in the report.
- `BAG_LOCKED,Locked,ロック` intact for batch-2 consumers (unchanged).

### 8. Scope — PASS

`git status --porcelain --untracked-files=all` shows only:
- 6 prefabs (5 result-flow + BagSlotLockedPrefab regression-fix)
- 4 controllers (HoleCardController, MatchmakingModalController, VersusResultScreenController, ModeCardController)
- `Assets/Localization/LocalizationText.csv` + `LocalizationTextTable.asset`
- Task folder (SPEC / STATUS / IMPLEMENTER_REPORT / SELF_REVIEW / HEARTBEAT / screenshots/ / reference/ / ARCHITECT_REVIEW.md)
- `.claude/review_misses.log` (expected — carries iter-1 fabrication entry from 2026-07-22 21:38 JST)

Pre-existing drift (Art/, NuGet/, Fonts/, Packages/, `.mcp.json.bak-23886`) attributed to iter-2 kickoff baseline HEAD `c90d3bd5b` — NOT this task.

**NO** asmdef change, **NO** scene change, **NO** `Assets/Scripts/Physics/` change, **NO** `Assets/Scripts/Editor/` builder change, **NO** `M_Splash*.mat` change. Verified.

### 9. Compile — accepted

Report cites `IsCompiling=false`, `IsPlaying=false` via `editor-application-get-state`. Reviewer lacks live Unity access for a fresh check, but the four controller edits are all inline `LocalizationManager.Get(...)` string swaps in a subsystem that already imports the manager, and no syntax error is present in the source excerpts read.

---

## Spec deviations reviewed

1. **`MATCH_FINDING_OPPONENT` EN stored without trailing dots** — legitimate engineering call; `DotCycleRoutine` appends the animated dots as a suffix. Storing `FINDING OPPONENT...` in the CSV would double the ellipsis. Flagged for Cesar's confirmation but sound.
2. **`MATCH_DIAMOND_LEAGUE` EN = `DIAMOND LEAGE` typo preserved** — correct per spec rule 6 (preserve displayed English exactly). Flagged for separate copy-fix decision.
3. **`BAG_LOCKED` left intact for batch-2 consumers, `UI_LOCKED` minted for this batch** — clean separation; casing regression fix is scoped to the two hole-complete cards + one BagSlotLockedPrefab that was carrying the wrong casing.

None of these deviations warrant a FAIL. All flagged appropriately.

---

## Verdict

**PASS.** All review-prompt items independently verified. No regressions from iter-1 addressed items. No new failure modes introduced. The expected `[JP-TODO]` overflow on JP captures is not FAILed (per prompt guidance) — the visual gate (EN layout unchanged, keys resolve, real-JP keys render actual Japanese) is met on every surface.

Set `STATUS.md` → `READY_FOR_REDTEAM`. Adversarial red-team gate runs next.

---

# RED-TEAM REVIEW (adversarial gate — 2026-07-22 22:52 JST)

Regenerated all evidence; trusted nothing from the reviewer. iter-1 fabrication context in force.

## Attack results (all 7, hardest-first)

1. **NO fabrication.** `md5 -r` all 10 screenshots → 10 distinct hashes, zero collisions. `cmp` all 5 EN/JP pairs → all differ. Opened all 4 JP surfaces + 2 EN myself: hole_complete_failed_jp (メンテナンス情報/定期サーバーメンテナンス/プレイ real JP + FAILED/RETRY/LOCKED `[JP-TODO]`), matchmaking_modal_jp (キャンセル real JP CANCEL + DIAMOND LEAGE/FINDING OPPONENT/HOLE/Vs. `[JP-TODO]`), versus_result_jp (RESULTS/WINNER/LOSER/Vs./HOLE/NEW MATCH/You `[JP-TODO]`), hole_complete_success_jp (SUCCESS/REPLAY/NEXT `[JP-TODO]` + プレイ). No English-masquerade, no raw KEY, no tofu. **GONE/clean.**
2. **LOCKED regression fixed in BOTH prefabs.** `grep BAG_LOCKED` on HoleCompleteWidget.prefab + BagSlotLockedPrefab.prefab → NONE. `UI_LOCKED` bound ×2 (HoleComplete success+failed panels) + ×1 (BagSlot). BagSlot diff is a clean one-line `BAG_LOCKED`→`UI_LOCKED` key repoint, structural component untouched. `hole_complete_failed_en.jpg` reads `LOCKED` all-caps (NOT "Locked"). CSV: `UI_LOCKED,LOCKED,LOCKED [JP-TODO]`. **Fixed.**
3. **Reuse-casing systemic.** BTN_START=`PLAY`, MODAL_CANCEL=`CANCEL`, HOME_USERNAME=`USERNAME`, UI_LOCKED=`LOCKED` — every reused/new EN matches the on-screen source casing exactly. Zero bindings to HOME_COURSE_NAME anywhere (its EN is the hardcoded "Lomond Country Club  - Hole 5"; MatchMaking/Versus hole strings correctly left dynamic). **Clean.**
4. **No binder fights a runtime write.** HoleComplete widget scripts (unmodified) write only dynamic data (course/hole/par subhead, stats block, reward counts, next-hole desc) — never the bound static title/button/LOCKED labels. Controllers write only usernames/outcome/status via code-path Get(); those labels (WINNER/LOSER/You/FINDING OPPONENT) have NO binder. Bound static labels are never runtime-written. JP captures render the binder values, proving no overwrite. **Clean.**
5. **Binders clean.** 31 LocalizedText (GUID 82815e97506b3ee47a82fe099019729c): 30 new + 1 key-edit (BagSlot). Every key resolves in CSV. Full diff of all 6 prefabs contains ONLY `- component`/MonoBehaviour/`key:` additions — zero m_IsActive/m_SizeDelta/m_AnchoredPosition/m_LocalPosition/m_LocalScale/m_LocalRotation/color/text mutation. **Clean.**
6. **Live-surface reality.** matchmaking_modal_jp キャンセル = MODAL_CANCEL binder demonstrably fired on a real instance; hole_complete_failed_jp プレイ (BTN_START) + versus_result_jp `[JP-TODO]` values = binders/code-paths fired on real instances. ≥2 prefabs proven live, no design-time placeholder showing. **Clean.**
7. **CSV integrity + scope + compile.** 255 data rows, no dup keys, header `key,English,Japanese`. All 12 new keys carry `[JP-TODO]`; DIAMOND LEAGE typo byte-preserved. Table asset regenerated. Trap checked: RESULT_REPLAY_HOLE (HoleCardController) looked absent from the 238-255 block but exists at data-row 255; all 10 code-path Get() keys + 15 binder keys resolve. git scope = CSV + table + 6 prefabs + BagSlotLockedPrefab + 4 controllers + task folder + review_misses.log; the rest of the working tree is pre-existing session drift. No CS errors in Editor.log; JP play-mode render is empirical proof of clean compile. **Clean.**

## Three break-attempts (all failed)
- **Visual:** hunted a raw KEY / tofu / EN-masquerade in JP — found none. home_mode_card MULTIPLAYER/NO ENTRY FEE show English, but that is a graceful `MODE_<TITLE>` fallback-to-raw-title (dynamic mode name) + a runtime-composed fee readout — EN-identical, NOT a raw key. Could not break.
- **Missing-key/compile:** RESULT_REPLAY_HOLE appeared missing → it exists (row 255); every Get()/binder key resolves; render proves compile. Could not break.
- **Regression:** BAG_LOCKED could linger or EN could show "Locked" → grep proves zero BAG_LOCKED; EN shows all-caps LOCKED. Could not break.

Expected `[JP-TODO]` overflow on JP captures was NOT counted against the work, per gate guidance.

## Red-team verdict: **ARCHITECT_REVIEW_PASS** — advances to Cesar.
