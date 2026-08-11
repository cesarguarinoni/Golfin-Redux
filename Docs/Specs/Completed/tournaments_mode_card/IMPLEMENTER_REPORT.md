# IMPLEMENTER REPORT — `tournaments_mode_card`

**Iteration shape:** `mode_select_ui:tournaments_card_entry_point`
**Iteration:** iter-2 (iter-1 + Cesar copy/spacing pass)
**Date:** 2026-08-11
**Canonical screenshot:** `screenshots/home_carousel_tournaments_en.png` (1170×2532)

## Summary

Added the fifth mode card, TOURNAMENTS, to the Home mode carousel and the full-screen
Mode Select list, routing PLAY to the existing `ScreenId.TournamentSelection`. Implemented
exactly as specced: a `modes.csv` row + a new optional `rewardsTextKey` column, a
rewards-as-text branch in `UpdateEconomyRows`, an id-based tagline/description localization
convention with CSV fallback, and a `case "tournaments"` in both `HandlePlayClicked`
switches. Five EN+JP localization keys added and the table asset regenerated.

**One deviation from the spec's "no prefab edits" clause was required** — see
§ Spec deviations. The spec's acceptance item "REWARDS row shows 'Varies by tournament'
(no coin icon)" was NOT satisfiable by the code change alone: the coin GameObjects were
never referenced by the controller on those rows. Fixed by wiring one unwired
`[SerializeField]` and adding the two missing expanded-container counterparts.

## iter-2 — Cesar's copy + spacing pass (2026-08-11 10:33)

Two changes requested directly by Cesar after seeing the iter-1 captures. All four
screenshots were re-taken after these changes; the checklist below still holds.

**1. Subtitle copy changed in both languages.**

| Key | Was | Now |
|---|---|---|
| `MODE_TOURNAMENTS_TAGLINE` (EN) | Compete for the top of the leaderboard. | **Be the best and earn rewards** |
| `MODE_TOURNAMENTS_TAGLINE` (JP) | リーダーボードの頂点を競おう。 | **頂点に立って報酬を手に入れよう** |
| `modes.csv` `tagline` (fallback) | Compete for the top of the leaderboard. | **Be the best and earn rewards** |

Cesar supplied the English only; the JP is my rendering — *"stand at the top and earn
rewards"*, reusing 頂点 from the approved copy and the 〜しよう invitational form the other
JP strings use. **APPROVED by Cesar 2026-08-11** ("JP sub approved"). Trailing punctuation
dropped in both to match the string Cesar typed (the previous pair had `.` / `。`). Side benefit: the shorter EN string
now fits on ONE line on the Home card, where the old copy wrapped to two.

**2. REWARDS row gap tightened for the text variant.** Cesar: *"seems to have a double
space between them."* Measured before the fix — the gap was exactly **32.0px**, which is
the row's authored `HorizontalLayoutGroup.spacing`. §6.2 authors that row as
`[LABEL gap32 coin42 gap6 value]`, so 32 is the label→**coin** gap; with the coin hidden it
is stranded between two words and reads as a double space. (Practice measures 80.0px =
32 + 42 coin + 6, confirming the arithmetic.)

Fix: new serialized `textRewardsGap` (default **12**) applied by `ApplyRewardsGap()` to the
rewards row **only when the value is localized text**. The authored spacing is captured on
first bind and restored for coin rows, so nothing else moves. Measured after:

| Prefab / mode | Gap before | Gap after |
|---|---|---|
| ModeHomeCard / tournaments (text) | 32.0px | **12.0px** |
| ModeCard / tournaments (text) | 32.0px | **12.0px** |
| ModeHomeCard / practice (coin) | 80.0px | 80.0px (unchanged) |
| ModeCard / practice (coin) | 80.0px | 80.0px (unchanged) |
| ModeCard / missions, locked (coin) | 80.0px | 80.0px (unchanged) |

`textRewardsGap` is serialized precisely so Cesar can retune it in the Inspector without a
code change, matching the existing "Colours (§6.2)" fields' philosophy. Casing was left as
the SPEC's Cesar-approved "Varies by tournament" (Cesar's message wrote "Tournament" —
read as casual, not a casing instruction; say the word and I'll capitalize it).

## Files modified or created

| File | Change |
|---|---|
| `Assets/Resources/Data/modes.csv` | Tournaments row (order 3, `target=tournaments`, fee 0, `rewardsTextKey=MODE_REWARDS_VARY`); new optional `rewardsTextKey` column; driving_range 3→4, missions 4→5. **iter-2:** tagline fallback → "Be the best and earn rewards". |
| `Assets/Scripts/UI/ModeSelect/ModeData.cs` | `public string rewardsTextKey = "";` + doc-comment column list. |
| `Assets/Scripts/UI/ModeSelect/ModesDatabaseCSV.cs` | Parse `rewardsTextKey` (index lookup + guarded row read) + doc-comment column list. |
| `Assets/Scripts/UI/ModeSelect/ModeCardController.cs` | `Localize()/LocTagline()/LocDescription()` helpers; 6 tagline/description read sites swapped; `UpdateEconomyRows` rewards-text branch + localized `MODE_NO_ENTRY_FEE`; rewards coin suppressed for the text variant; **new** `coinIconExp`/`rewardsCoinExp` fields + toggles; **iter-2** `textRewardsGap` field + `ApplyRewardsGap()`. |
| `Assets/Scripts/UI/ModeSelect/ModeCarouselController.cs` | `case "tournaments":` → `ScreenManager.Instance.ShowScreen(ScreenId.TournamentSelection)`. |
| `Assets/Scripts/UI/ModeSelect/ModeSelectScreenController.cs` | Same case, using the method's existing `sm` local. |
| `Assets/Localization/LocalizationText.csv` | +5 rows: `MODE_TOURNAMENTS`, `_TAGLINE`, `_DESC`, `MODE_REWARDS_VARY`, `MODE_NO_ENTRY_FEE` (EN + JP). |
| `Assets/Localization/LocalizationTextTable.asset` | Regenerated via Tools → Localization → Import Text CSV (352 rows). |
| `Assets/Prefabs/UI/ModeSelect/ModeHomeCard.prefab` | **Deviation D1** — wired `rewardsCoin` → `FeeGroup/RewardsRow/CoinValueGroup/CoinIcon2` (was `fileID: 0`). |
| `Assets/Prefabs/UI/ModeSelect/ModeCard.prefab` | **Deviation D1** — wired `coinIconExp` → `Reward1IconExp`, `rewardsCoinExp` → `Reward2IconExp`. |
| `Docs/Specs/Active/tournaments_mode_card/{IMPLEMENTER_REPORT,STATUS,HEARTBEAT}` + `screenshots/` | This report, status, heartbeat, 4 captures. |
| `Docs/AI_CONTEXT.md` | Session status entry for this task. |
| `Assets/Scripts/UI/Editor/TournamentsModeCardDemoRecorder.cs` | **New** — demo recorder for the daily-report video, cloned from the `TournamentDemoRecorder` family. Menu: `GOLFIN > Tournaments > Record Mode Card Demo`. Writes `videos/raw.mp4` + a `captions.json` sidecar. |
| `Docs/Scripts/build_bot_video.py` | Additive: `--mode captionsjson` (caption a DemoRecorder clip from its sidecar — no smoke-bot scenario / `record_info.json` / `history.log` needed) plus `--caption-fontsize` and `--caption-wrap` so portrait 1170-wide clips stop overflowing. Existing modes and defaults untouched. |
| `.claude/hooks/enforce_implementer_done.py` | **Pipeline fix, on Cesar's instruction** ("fix the gate") — 2 lines in `_rerun_ui_lint_via_editor()`. Its C# was built in a non-raw f-string, so the emitted script never compiled (`\"fail\":` → `""fail":` CS1003/CS0103; `\s`/`\d` → CS1009). Rule 21 therefore always returned `None` and, being fail-closed, blocked **every** Figma-node UI task. Now emits valid C#; live re-run returns `fail = 0` for both prefabs, a non-zero verdict still propagates, and unreachable-editor still returns `None` (stays fail-closed). Suite: 118 passed, 1 pre-existing failure that reproduces on unmodified HEAD. |
| ~~`Assets/Fonts/NotoSansJP-VariableFont_wght SDF.asset`~~ | **REVERTED 2026-08-11 on Cesar's instruction — no longer in the change set.** Rendering JP in play mode made TMP bake 103 glyphs into this **Dynamic** atlas, growing it 59KB → 2.27MB. `git restore`d to HEAD (59,524 bytes); asset re-verified after an AssetDatabase refresh: loads fine, `atlasPopulationMode=Dynamic`, `glyphTable=0`, `sourceFontFile` intact. Note `m_ClearDynamicDataOnBuild` is already `True`, so the bloat never reached a player build — it was purely editor churn. It will re-appear in anyone's working tree after a JP play session; the fix is to not commit it. |
| `Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` | **NOT THIS TASK** — dirty at kickoff, owned by a concurrent session. Untouched: not staged, not modified, not restored. |
| `Docs/TellCode.md` | **NOT THIS TASK** — dirty at kickoff, owned by a concurrent session. Untouched. |
| `tasks/loop_v2_smoke_bot/hole1_playthrough/live_stat_log.txt` | **NOT THIS TASK** — dirty at kickoff, owned by a concurrent session. Untouched. |
| `tasks/loop_v2_smoke_bot/hole1_playthrough/screenshots/history.log` | **NOT THIS TASK** — dirty at kickoff, owned by a concurrent session. Untouched. |

### Rule 13 disclosure

The last four rows above were already dirty before this task started and belong to a
**concurrent session** that committed `hole_scene_leftover_v3` mid-run. All four are quoted
verbatim from the kickoff DIRTY block in `HEARTBEAT.log`
(`Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs`, `Docs/TellCode.md`,
`tasks/loop_v2_smoke_bot/hole1_playthrough/live_stat_log.txt`,
`tasks/loop_v2_smoke_bot/hole1_playthrough/screenshots/history.log`). I did not stage,
restore, or modify any of them — restoring another live session's work-in-progress would
destroy it.

## Acceptance checklist

| Item | Result | Justification (what was measured) |
|---|---|---|
| Home carousel shows 5 cards; TOURNAMENTS between PRACTICE and DRIVING RANGE (order 3), unlocked, styled like the others | PASS | Play mode: 15 live `ModeCardController` instances (5 modes × 3 virtual passes); middle-pass slot dump = versus_1v1, practice, **tournaments**, driving_range, missions. Title colour read off the live TMP: `#EEDC9A` gold on the centred card, `#D1D5DB` silver on both side cards — identical treatment to Practice. See `screenshots/home_carousel_tournaments_en.png`. |
| Tournaments card collapsed + expanded: fee row "NO ENTRY FEE" (no coin), REWARDS row "Varies by tournament" (no coin, white text) | PASS | Initially **FAILED** — 3 stray coins (see § Spec deviations D1). After the fix: Home card renders `NO ENTRY FEE` / `REWARDS  Varies by tournament` with no coin on either row; full-screen expanded card reads `coinIconExp active=False`, `rewardsCoinExp active=False`. Amount colour is `NormalWhite` via the unchanged `entryFeeAmount.color`/`rewardsAmount.color` assignments. `screenshots/home_carousel_tournaments_en.png` + `screenshots/modeselect_tournaments_expanded_en.png`. |
| Expanded card shows tagline as subtitle and the full description body | PASS | Full-screen expanded card shows subtitle "Be the best and earn rewards" (iter-2 copy) above the 165-char description "Enter live tournaments … every stroke counts." — both visible in `screenshots/modeselect_tournaments_expanded_en.png`; EditMode read-back confirmed `subtitleTextExpanded` and `descriptionTextExpanded` carry the localized strings. |
| PLAY on the Tournaments card (home carousel) opens TournamentSelection | PASS | Invoked the centred card's real `playButton.onClick` (active=True, interactable=True). `ScreenManager.CurrentScreen` = `TournamentSelection` and `ACTIVE SCREEN: TournamentSelectionScreen` after the fade; the real T7 browse list rendered with tournament cards. |
| PLAY on the Tournaments card (full-screen Mode Select) opens TournamentSelection | PASS | Navigated via the real `NavTeeButton.onClick` → ModeSelection, expanded the row via the real `CardTapButton.onClick`, then invoked the row's real `playButton.onClick` → `CurrentScreen=TournamentSelection`, `ACTIVE SCREEN: TournamentSelectionScreen`. |
| JP: title トーナメント, tagline/description/rewards/fee show the JP values | PASS | `LocalizationManager.SetLanguage(Japanese)` then re-entered both screens. Home: トーナメント / 頂点に立って報酬を手に入れよう (iter-2 copy) / 参加費無料 / 報酬 トーナメントごとに異なります / プレイ. Full-screen expanded adds the full JP description 開催中のトーナメント…勝負を決める。 `screenshots/home_carousel_tournaments_jp.png`, `screenshots/modeselect_tournaments_expanded_jp.png`. |
| Regression: PRACTICE and Multiplayer render identical text to before, PLAY routes still work | PASS | EN read-back of both prefabs: practice = `PRACTICE` / `Sharpen your skills on any hole.` / `x100` / `x50` with both coins VISIBLE; versus = `Multiplayer` / `1v1` / `NO ENTRY FEE` / `x200`. `MODE_PRACTICE_TAGLINE` and `MODE_PRACTICE_DESC` resolve as MISSING, so both fall back to the raw CSV strings — byte-identical to before. `hole_select`/`matchmaking_1v1` switch arms are untouched (diff adds a new `case` only); fee-spend logic in `HandlePlayButtonClicked` unchanged. |
| Regression: locked cards (Driving Range, Missions) still show Coming-Soon at orders 4 and 5 | PASS | `LoadFromCSV` dump: `order=4 id=driving_range locked=True target='none'`, `order=5 id=missions locked=True target='none'`. Both render the lock icon + "Coming Soon —" tagline in `screenshots/modeselect_tournaments_expanded_en.png`; driving_range's REWARDS row stays hidden (rewards=0), missions keeps `x200` + coin. |
| `TournamentLoopCaptureHarness` still passes its ModeSelect → "TOURNAMENTS (TEMP)" click path | PASS | `git status` on `Assets/Scripts/UI/Tournaments/` is empty (button untouched); the live button is at `Canvas/ScreensRoot/ModeSelectionScreen/TournamentTempEntry`, activeSelf=True. Ran a byte-for-byte replica of `BotDriver.FindButton` on the live EN ModeSelection screen: **exactly 1 match** for `"TOURNAMENTS (TEMP)"` → the temp button. The new card's title "TOURNAMENTS" does not *contain* the query string, so no ambiguity is introduced. (Harness not executed end-to-end — see § Not verified.) |
| Unity Console has no errors related to this task | PASS | 100-entry console dump over the whole play session: 97 Log, 2 Error, 1 Exception — all three are one failed `script-execute` of my own probe (`CS0104: 'Object' is an ambiguous reference`), not game code. Only mode-related game log: `[ModesDatabaseCSV] Loaded 5 modes`. Zero `[ModeCarousel]`/`[ModeSelectScreen]` "no route" warnings. |
| Spec deviations flagged with justification | PASS | See § Spec deviations below — two deviations (D1 prefab wiring, D2 localized-fallback helper), each with root cause, evidence and blast radius. |

## Figma fidelity

Per SPEC § Reference this task introduces **no new Figma frame** — the card reuses the
shipped `ModeHomeCard` / `ModeCard` visuals and the regression baseline is the existing
Practice / Multiplayer cards. The only node the SPEC cites is the *routing target*, which
this task does not build. Table is therefore a routing/parity table, not a redraw diff.

| Element | Figma node | Expected | Built | Verdict |
|---|---|---|---|---|
| PLAY destination (T7 browse screen) | `13386:1758` (TournamentSelection, already shipped) | PLAY lands on the T7 tournament browse screen | `CurrentScreen=TournamentSelection`, `TournamentSelectionScreen` active, T7 list rendered from both entry points | PASS |
| Card chrome (border / panel sprite) | none — reuses shipped card | Same sprite swap + border rule as Practice | Untouched code path (`RefreshCenterVisuals`); white border on the centred card in both captures | PASS |
| Title colour | none — reuses shipped card | Gold `#EEDC9A` centred, silver `#D1D5DB` inactive | Measured on live TMP: `#EEDC9A` centre, `#D1D5DB` both side cards | PASS |
| Economy rows | none — reuses shipped card | "NO ENTRY FEE" + text rewards, no coins | Coin GameObjects inactive on both rows after D1; text/colour via the unchanged label fields | PASS |

## UI fidelity lint

`Golfin.EditorTools.UIFidelity.UIFidelityLinter.LintPrefab` re-run on both touched prefabs
after the wiring change (render-health layer; no node `spec.json` exists for this task
because there is no new Figma frame):

| Prefab | Lint JSON | fail | Result |
|---|---|---|---|
| `ModeHomeCard.prefab` | `Docs/Diagnostics/_capture/ModeHomeCard_lint.json` | **0** | PASS (health) — 10 WARN |
| `ModeCard.prefab` | `Docs/Diagnostics/_capture/ModeCard_lint.json` | **0** | PASS (health) — 17 WARN |

Every WARN names an element this diff does not modify — unlocalized placeholder strings
seeded into the prefabs ("Lomond Country Club - Hole 1 - Par 5", "Next", "x10"), the
`Outline` component rule C5, and 9-slice cap-kink on `S_ModeCardPanel`. None of them
reference the economy rows, coin icons, or serialized fields this task changed, and both
prefabs report `fail: 0`, which is the gate.

## Video

Canonical video: `videos/tournaments_mode_card_demo.mp4` (6.1 MB, **1170×2532**, 50.1s)
Daily-report copy: `Docs/Reports/Media/tournaments_mode_card_demo.mp4`

Recorded with a new `TournamentsModeCardDemoRecorder` cloned from the sanctioned
`TournamentDemoRecorder` / `RankingsDemoRecorder` family — same Unity Recorder GameView
pipeline, Game View pinned to the iPhone-14 preset **before** `StartRecording()`, and no
RT/stills read during the recording (both documented y-flip triggers avoided). Captions
burned in by `Docs/Scripts/build_bot_video.py` via the `textfile=` drawtext idiom, never
inline text.

Driven as a real player end to end: Splash **PLAY** → Home carousel → tap the TOURNAMENTS
card → tap the tagline to expand → **PLAY** → TournamentSelection → bottom-nav **Tee** →
full-screen Mode Select → tap the row → **PLAY** → TournamentSelection → Japanese pass.
Runner log confirms every step: `After home PLAY, CurrentScreen = TournamentSelection`
and `After list PLAY, CurrentScreen = TournamentSelection`.

Three takes were needed, each fixed after **looking at decoded frames** rather than
trusting the run:

| Take | Defect found by frame inspection | Fix |
|---|---|---|
| 1 | Expand + both PLAYs silently no-opped — the clip sat on Home the whole time | The home carousel is a 3× virtual array; "first match wins" grabbed a side instance whose PLAY isn't wired. Added centred-/middle-pass instance lookups. |
| 2 | Captions clipped off **both** edges | The tool sizes fonts from height (`h/32` = 79px), far too large for a 1170-wide portrait clip. Added `--caption-fontsize` / `--caption-wrap`; used 42px wrapped at 32. |
| 3 | Title drawn twice (centred + bottom); JP caption rendered as tofu boxes | Recorder no longer emits its own title caption (the tool's `--title` owns it); JP caption switched to ASCII since the ffmpeg font has no CJK glyphs. |

`raw.mp4` (30 MB) deleted after captioning; `videos/` is gitignored (`.gitignore:180`), as
is `screenshots/` (`.gitignore:246`), so neither ships in the commit.

## Screenshot

Canonical screenshot: `screenshots/home_carousel_tournaments_en.png`

| File | What it shows |
|---|---|
| `screenshots/home_carousel_tournaments_en.png` | Home carousel, TOURNAMENTS centred (gold title, white border, PLAY), PRACTICE and DRIVING RANGE peeking either side. No coin on either economy row. |
| `screenshots/modeselect_tournaments_expanded_en.png` | Full-screen Mode Select, all 5 rows in order, TOURNAMENTS expanded with tagline + description + PLAY, no coins; other rows keep their coins. |
| `screenshots/home_carousel_tournaments_jp.png` | Same Home view in Japanese. |
| `screenshots/modeselect_tournaments_expanded_jp.png` | Same Mode Select view in Japanese, including the full JP description. |

All four captured at 1170×2532 (iPhone-14 preset) through the sanctioned
`GOLFIN/Screenshot/Capture Game View` path, driven as a real player: Splash PLAY →
Home → real card `onClick` → real `playButton.onClick` → real `NavTeeButton.onClick`.
Note: `Docs/Specs/**/screenshots/` is gitignored (`.gitignore:246`), so these live on
disk but are intentionally not committed.

## Spec deviations

**D1 — Two prefabs edited, and two `[SerializeField]`s added, despite SPEC § Files saying
"No scene or prefab edits."**

*Why it was unavoidable.* Acceptance item 2 requires the REWARDS row to show
"Varies by tournament" **with no coin icon**. The spec's code recipe achieves this via
`rewardsCoin.gameObject.SetActive(hasRewards && !hasTextRwd)`. That line is a no-op on the
Home card: `ModeHomeCard.prefab` had `rewardsCoin: {fileID: 0}` — the field was never
wired, so the coin `CoinIcon2` was always drawn. Verified in play mode: the first
screenshot showed a coin sitting between "REWARDS" and "Varies by tournament".

Separately, `ModeCardController` had **no** expanded-container coin fields at all
(`coinIcon`/`rewardsCoin` cover only the collapsed rows), so on the full-screen expanded
card both `Reward1IconExp` and `Reward2IconExp` were permanently visible — the fee row
read "(coin) NO ENTRY FEE".

*This defect originates upstream of this task.* Proven in the same play session by
expanding `versus_1v1` — a mode this task does not touch, whose `entryFee` is also 0: its
expanded row rendered `Reward1IconExp` active next to "NO ENTRY FEE". That mode's CSV row,
its code path, and the expanded-container fields are all outside this diff, so the stray
coin cannot originate here. The mechanism confirms it: `ModeCardController` declared no
expanded-container coin fields at all, so nothing ever toggled those two GameObjects.
Both prefabs were CLEAN in the `HEARTBEAT.log` kickoff DIRTY block, so their only changes
are the deliberate wiring below.

*What changed.* Added `coinIconExp` / `rewardsCoinExp` `[SerializeField]`s mirroring the
existing collapsed pair, toggled by the same `hasFee` / `hasRewards && !hasTextRwd` rules;
then wired three references via `SerializedObject` + `SaveAsPrefabAsset` (never by hand):

- `ModeHomeCard.rewardsCoin` → `FeeGroup/RewardsRow/CoinValueGroup/CoinIcon2`
- `ModeCard.coinIconExp` → `ExpandedContainer/RewardsRowExp/RewardSlot1Exp/CoinValueGroup/Reward1IconExp`
- `ModeCard.rewardsCoinExp` → `ExpandedContainer/RewardsRowExp/RewardSlot2Exp/CoinValueGroup/Reward2IconExp`

This follows CLAUDE.md hard rule 7 ("if `[SerializeField]` references aren't wired, wire
them"). `ModeCard.prefab` already had `rewardsCoin` wired, which is what made the Home
card the outlier and confirms this is a wiring gap rather than a design choice.

*Blast radius.* Strictly narrowing: the coins only ever turn OFF, and only where the data
says there is nothing to show. Practice (`x100`/`x50`), Multiplayer (`x200` rewards) and
Missions (`x200`) keep every coin — confirmed in both EN and JP captures. Driving Range's
rewards row was already hidden (`rewards=0`). Modes with `entryFee=0` now correctly lose
the stray coin next to "NO ENTRY FEE" on the expanded card.

*Multiplayer — **APPROVED BY CESAR** 2026-08-11 ("Fix it").* This is the one shipped card
the change touches. Multiplayer has `entryFee = 0`, so expanding its row on the full-screen
Mode Select used to render a Reward-Points coin immediately before the words "NO ENTRY FEE",
while its collapsed row rendered the same text with no coin — the card contradicted itself
depending on whether it was open. Now both states read `NO ENTRY FEE`. Only the FEE coin
changes; its REWARDS row still shows `🄡 x200`.

*Completeness audit (added after Cesar's "fix it").* Rather than trusting that the three
references I happened to notice were the whole problem, I enumerated **every** economy icon
on both prefabs and checked whether the controller governs it:

| Prefab | Icon | Controlled? | Draws? |
|---|---|---|---|
| ModeHomeCard | `CoinIcon1` (fee) | yes — `coinIcon` | yes |
| ModeHomeCard | `CoinIcon2` (rewards) | yes — `rewardsCoin` (wired here) | yes |
| ModeCard | `Reward1Icon` (fee, collapsed) | yes — `coinIcon` | yes |
| ModeCard | `Reward2Icon` (rewards, collapsed) | yes — `rewardsCoin` | yes |
| ModeCard | `Reward1IconExp` (fee, expanded) | yes — `coinIconExp` (wired here) | yes |
| ModeCard | `Reward2IconExp` (rewards, expanded) | yes — `rewardsCoinExp` (wired here) | yes |
| ModeCard | `Reward3Icon` | no | **no** — `RewardSlot3` inactive |
| ModeCard | `Reward3IconExp` | no | **no** — `RewardSlot3Exp` inactive |

The two uncontrolled icons are a third reward slot that carries **no sprite** (a white
64×64 flat fill). Both measured `activeInHierarchy=False` on every mode I bound
(tournaments / versus_1v1 / practice) because their `RewardSlot3*` parent is off in the
prefab, so neither can draw — which is why no white box appears in any capture. They are
the source of the linter's `flat-fill` WARN on `Reward3IconExp`. Left alone: wiring an
unused, spriteless slot is outside this task, and nothing renders it.

*Incidental churn.* `SaveAsPrefabAsset` re-serialized `japaneseFontScale: 0` onto three
`LocalizedText` components in `ModeHomeCard.prefab`. `0` is that field's documented default
("0 = no override") and `ApplyPerLanguageSize()` early-returns on `japaneseFontScale <= 0f`,
so it is inert — confirmed by the JP captures, which were taken after the save.

**D2 — `MODE_NO_ENTRY_FEE` looked up through the `Localize()` helper, not a bare `Get()`.**
The spec's snippet calls `LocalizationManager.Get("MODE_NO_ENTRY_FEE")` directly. `Get()`
returns the *key itself* when the table is missing or not yet initialized, which would
render the literal string `MODE_NO_ENTRY_FEE` on the card. Routing it through the same
`Localize(key, fallback)` helper the spec defines in step 5 keeps the shipped literal
"NO ENTRY FEE" as the fallback. Same output in every normal case; strictly safer.

**Not done (per spec § Out of scope):** `TournamentDevEntryButton` untouched; no tournament
backend/signup changes; no localization of other modes' taglines/descriptions; no card
visual rebuild; no changes to versus/practice routing, fee spend, or the demo gate.
`AddFallbackModes()` was left without a tournaments entry (spec: optional).

## Not verified / needs manual confirmation

1. **`TournamentLoopCaptureHarness` end-to-end run.** I proved the harness's button lookup
   stays unambiguous (item 9) but did not execute the full recording harness — it is a
   long GPU recording and the session guard allows ~1 full-res clip per Editor launch.
   Worth one confirming run before close-out.
2. **On-device (iOS) check.** Everything above was verified in the Editor at the iPhone-14
   1170×2532 preset. The JP description is the longest string either card has ever
   rendered; worth an eyeball on a real device for wrapping/overflow in the expanded card.
3. ~~The D1 side effect on Multiplayer~~ — **resolved 2026-08-11: Cesar approved it as a
   fix** ("Fix it"). Recorded in § Spec deviations D1; a full economy-icon audit across both
   prefabs confirms every icon that can draw is now controlled.

## Editor state left behind

Play mode exited. `ShellScene` `IsDirty=false` — no scene mutations persisted. Language
restored to English. Game View left on the iPhone-14 1170×2532 preset (was 1602×856);
`PlayerSettings.runInBackground` was already `true` and was not changed.
