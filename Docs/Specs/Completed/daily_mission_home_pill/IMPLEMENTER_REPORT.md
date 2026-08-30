# Implementer Report — `daily_mission_home_pill`

**Iteration shape:** `home_ui:daily_pill_placement_and_motion`

## Iteration 2 — Cesar's three follow-ups (2026-08-30)

| Ask | What changed | Proof |
|---|---|---|
| "Adjust the pill length when no streak so there is not an empty space to the right" | The pill now HUGS its content the way the node's auto-layout row does: `24 + (58 + 10 if flame) + 433 + 24` → **549 with the flame, 481 without**, with the label moving from x 92 to x 24. Both baked sprites were re-imported as **9-slices** (border 100 / 172 sprite-px at 2×, `pixelsPerUnitMultiplier = 2` → 50 / 86 UI px) so neither width squashes the 50px corners | `pill_width_549_with_flame` and `pill_width_481_without_flame` PASS; the streak-0 frame's gold border measures **68px shorter** than the streak-5 frame's, which is exactly flame + gap. Corner-collapse maths checked at both widths for both sprites |
| "Move the streak marker to the left of the Daily Mission title (so it appears in contracted and expanded mode)" | The badge moved out of its own row and into the TITLE row, first sibling. Collapsed reuses the existing `TitleHRow`; expanded got a `TitleHRowExp` cloned component-for-component from it, with `TitleExp` moved inside. Riding the title is what makes "both states" structural rather than something to remember | `streak_badge_beside_title` PASS (`collapsed parent='TitleHRow'`, `expanded parent='TitleHRowExp'`); `missioncard_collapsed_flame.png` + `missioncard_expanded_flame.png` |
| "Pressing the pill takes you to the mission screen with Daily mission expanded" | `MissionSelectionScreenController.ExpandDailyOnOpen`, a one-shot static the pill sets before navigating and the screen consumes as the daily binds. It also **suppresses the default NEXT expand** for that one visit, so a campaign card does not open and snap shut when the daily fetch lands | `pill_tap_expands_daily` PASS (`State=Expanded`) and `expand_request_consumed` PASS — both driven by the real `Button.onClick`. `missionselection_after_pill_tap.png` shows rules + reward + PLAY on arrival, every campaign card collapsed |

A failed daily fetch clears the request and hands the default expansion back to the NEXT card
(`ExpandNextFallback`), so a request can never leak into the following visit or leave the screen
with nothing open.

Gates after iteration 2: **15 assertions / 0 FAIL**, lint 0 FAIL on both new prefabs, EditMode
1939 / 0 / 3.

## Iteration 3 — the slide is an announcement, not a transition (Cesar, 2026-08-30)

> "Make the pill stay there when coming back from other menus. The slide anim is only for the
> first time it appears and for when there is a new daily mission."

Every return to Home re-played the entrance, because `OnEnable` parked the pill off-screen and
let the fetch bring it back in. It now keys off `DailyMissionPillController.AnnouncedForDate` —
the UTC date whose pill has already had its entrance this session:

- **Already announced** → `OnEnable` puts the pill at REST immediately, before the fetch. Parking
  it off-screen and waiting would blank it for a whole round trip and then pop it back, which is
  the flicker this branch removes. The 0.25 s pre-slide delay is skipped too.
- **Not announced** (first appearance, or midnight brought a different date) → unchanged: park
  off-screen, fetch, slide in.

Keyed on the DATE rather than a bool because that is exactly what "a new daily mission" means —
the rollover writes a new date and the announcement is owed again, with no second flag to keep in
sync. Static, so it survives the screen being disabled and re-enabled; per-session, so a relaunch
announces once more.

| Assertion | Result |
|---|---|
| `reentry_no_slide` | PASS — back on Home through the REAL nav-bar button, x stayed in **[36.0, 36.0] across 31 sampled frames**; off-screen would be −585 |
| `reentry_screen_is_home` | PASS |
| `new_daily_still_slides` | PASS — a new date drove x to **−585** before settling, so the announcement still runs |

Frames: `home_reentry_no_slide.png` (at rest the moment Home draws — the slight dimming is
ScreenManager's own screen fade finishing, not the pill) and `home_new_daily_slides_again.png`
(mid-slide, left end still off-screen). Side by side in `_contact_reentry_vs_newdaily.png`.

**The allocation gate was rewritten in the same pass, because it was measuring the wrong thing.**
It took a whole-screen managed-heap total: 68 B/frame on a warm editor, **22 550 B/frame** on a
freshly restarted one. Neither number says anything about the glow. It now samples the same
screen with the pill Shown (glow loop running) against Hidden (same `Update`, glow branch
skipped) and asserts the DIFFERENCE — **−1479 B/frame**, i.e. inside run-to-run noise, which is
the actual claim.

Gates after iteration 3: **18 assertions / 0 FAIL**, EditMode 1939 / 0 / 3.

## Implementation summary

A `NEW DAILY MISSION!` pill now lives on Home: it slides in from the left, pulses a soft gold
halo while it waits, sits 24px under the maintenance notice (or at the notice's own top when
there is none), carries a flame with the streak number in it, and opens Mission Selection when
tapped. One new shared static, `DailyMissionState`, is the single fact the pill and the Mission
Selection daily card both read and write, so a claim on one surface removes the pill on the
other immediately. The card's text streak (`"{0} day streak"`) is now the same `StreakFlame`
prefab the pill uses, on both the collapsed and the expanded copy.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Gameplay/Missions/DailyMissionState.cs` | **created** — the one shared fact (Date / Streak / Claimed / HasRecipe / Known + `OnChanged`) that stops the pill and the daily card disagreeing |
| `Assets/Scripts/UI/Home/DailyMissionPillController.cs` | **created** — Hidden/Entering/Shown/Leaving state machine, eased slide, sine glow, 1 s UTC-rollover tick, notice-relative Y, content-hugging width, tap → telemetry + expand request |
| `Assets/Scripts/UI/Common/StreakFlameView.cs` | **created** — `SetStreak(n)`; owns the "0 is not a streak" rule for every host |
| `Assets/Prefabs/UI/Common/StreakFlame.prefab` | **created** — flame Image + auto-sized navy-gradient number; the shared badge |
| `Assets/Prefabs/UI/HomeScreen/DailyMissionPill.prefab` | **created** — Glow / Panel / StreakFlame / Label(+`LocalizedText`) / Button + `ButtonPressFeedback` |
| `Assets/Art/HomeScreen/S_DailyPillPanel.png` | **created** — baked 549×122 @2x pill panel from the node's tokens (3px `#FCF195`, r50, `#133453→#091B33`); 9-sliced (border 100 @2x, ppum 2) so it serves 549 and 481 without corner squash |
| `Assets/Art/HomeScreen/S_DailyPillGlow.png` | **created** — the blurred gold halo for the glow layer; 9-sliced the same way (border 172 @2x) |
| `Assets/Art/HomeScreen/flame.png` | **imported** — Cesar's 58×90 art, set to Sprite (2D/UI), uncompressed |
| `Assets/Art/HomeScreen/Flame.svg` | untracked source added by Cesar; committed alongside the PNG, not used at runtime |
| `Docs/Scripts/make_daily_pill_panel.py` | **created** — regenerates both baked sprites from the node's four tokens |
| `Assets/Scripts/UI/HomeScreenController.cs` | **modified** — one `[SerializeField] dailyMissionPill` + one `RefreshPlacement()` call in `SetNewsPanelVisible` |
| `Assets/Scripts/UI/MissionSelection/MissionCardController.cs` | **modified** — `streakText`/`streakTextExp`/`streakChip` → two `StreakFlameView`s; `MISSION_DAILY_STREAK` no longer read |
| `Assets/Scripts/UI/MissionSelection/MissionSelectionScreenController.cs` | **modified** — writes `DailyMissionState` on fetch success/failure and on a successful claim; adds the one-shot `ExpandDailyOnOpen` request + its NEXT-card fallback |
| `Assets/Prefabs/UI/MissionSelection/MissionCard.prefab` | **modified** — the two streak TMPs replaced by `StreakFlame` instances, now 41×64 inside the TITLE row (`TitleHRow` / the new `TitleHRowExp`) rather than a row of their own |
| `Assets/Scenes/ShellScene.unity` | **modified** — `DailyMissionPill` instanced under `HomeScreen`, wired; the two orphaned streak TMPs on `DailyMissionCard` removed |
| `Assets/Scripts/Telemetry/TelemetryConfig.cs` | **modified** — `TelemetryEventNames.DailyPillTap = "daily_pill_tap"` |
| `Assets/Localization/LocalizationText.csv` | **modified** — `HOME_DAILY_PILL` (EN + JA), one added line |
| `Assets/Localization/LocalizationTextTable.asset` | **regenerated** by `Tools/Localization/Import Text CSV` |
| `Assets/Resources/Data/content_version.txt` | **modified** — re-exported after publishing `texts` (README's post-publish rule) |
| `Assets/Scripts/UI/Editor/DailyMissionPillDemoRecorder.cs` | **created** — capture harness (stills / video / invariants), cloned from `PaginationDotsDemoRecorder` |
| `Docs/Architecture/UI_ELEMENT_PALETTE.md` | **modified** — the three new reusable atoms, in the same commit as the atoms |

## Screenshot

- **Canonical screenshot:** `screenshots/home_notice_streak5_en.png` — 1170×2532, the with-notice
  Figma frame (`2098:8490`) at rest with the flame, which is the frame that reveals placement,
  border, gradient, flame and label all at once.
- **Captured at:** the same file (the harness writes straight into `screenshots/`)
- **Scene loaded:** `Assets/Scenes/ShellScene.unity`
- **Play mode:** Yes — booted through the real Logo → Splash → `StartButton` → Home path
- **Canonical video:** `videos/daily_mission_home_pill_demo.mp4` (1170×2532, 33.9 s, captioned)
- **Invariant JSON:** `pill_invariants.json` — **18 assertions, 0 FAIL**

## Figma fidelity

Node re-pulled at step 0 with `get_metadata` + `get_design_context` on `13994:1963` (and the two
parent frames `13994:1935` / `2098:8490`) — the values below are from that pull, not from the
SPEC's prose table. Built values are measured (TMP `textBounds`, `RectTransform.rect`, or a pixel
sample of `screenshots/home_notice_streak5_en.png`), never eyeballed.

| Element | Figma node | Figma value | Built value | Result |
|---|---|---|---|---|
| Pill root size | `13994:1963` | 549 × 122 | `rect=(549.00, 122.00)` (`pill_invariants.json`); **481 × 122 when the flame is hidden**, which is what the node's auto-layout row hugs to without it | PASS |
| Pill x in the 1170 frame | `13994:1963` under `13994:1940` | 96 + 10 − 70 = **36** | `anchoredPosition.x = 36.00` | PASS |
| Pill y, no notice | frame `13994:1935` | **361** | `anchoredPosition.y = −361.0` | PASS |
| Pill y, with notice | frame `2098:8490` | **725** (= 361 + 340 + 24) | **−737.0** = live notice top 361 + its height **352** + 24 | PASS\* |
| Corner radius | `13994:1963` | `rounded-[50px]` | baked at r=50 in `make_daily_pill_panel.py`; `Image.Type.Simple`, no 9-slice to collapse | PASS |
| Outer border | `13994:1963` | 3px solid `#FCF195` | pixel (300,738) = `(251,240,149)` = `#FBF095` (±1/channel, LANCZOS 2×→1×) | PASS |
| Inner rule | `I13994:1963;13994:1743` | 1px `#0A1D35` | baked at 1px `#0A1D35`; sampled `(6,27,53)` on the source at 2× row 7 | PASS |
| Body gradient | `13994:1963` | `to-b` `#133453` → `#091B33` | top (300,760) = `(17,48,78)`; bottom (300,852) = `(9,28,52)` | PASS |
| Flame | `I13994:1963;13994:2108` | 58 × 90 at (24, 16) | `Flame.sizeDelta = (58,90)`, root `anchoredPosition = (24,−16)`; sprite `flame.png` (58×90, Cesar's export) | PASS |
| Flame visibility | spec + node | only when streak ≥ 1 | `streak_zero_hides_flame` PASS; `screenshots/home_nonotice_streak0_en.png` shows no flame | PASS |
| Streak number | `I13994:1963;13994:2115` | Rubik SemiBold 45px, lh 60, tracking −0.69, **navy gradient `#133453→#091B33`**, 29 × 60 at centre-x 52.5 | Rubik-SemiBold SDF, **40px**, `characterSpacing −1.53`, `enableVertexGradient` `#133453→#091B33`; `"5"` measures **27.2 × 52.2** | PASS |
| Streak number, 2 digits | — | must fit | `"12"` auto-sizes to **34.7px**, 41.4px wide, inside the flame bulb (art is 43px across at y=78) | PASS |
| Label | `I13994:1963;13994:1745` | Rubik SemiBold 45px, lh 60, tracking −0.69, `#EEDC9A`, 433 × 60 at (92, 31) | Rubik-SemiBold SDF **40px**, `#EEDC9A`, rect 433 × 60 at `(92,−31)`; glyphs measure **423.6 wide** vs the node's 433 box | PASS |
| Label x at streak 0 | spec § Figma Fidelity said "keeps its x" | — | **SUPERSEDED by Cesar's iter-2 ask.** Keeping x 92 in a 549 pill is what left the dead space he flagged; the pill now shrinks to 481 and the label takes the 24px pad, which is what the node's auto-layout does with the flame absent | PASS\* |
| Label colour, JA | — | node is EN-only | pixel-sampled `(238,220,154)` = **`#EEDC9A`** — gold, not white | PASS |
| Glow | — (motion only) | additive, alpha 0.25 ↔ 0.65, 1.6 s sine | `S_DailyPillGlow.png` on `TapSparkle_Additive.mat` (`UI/Additive`), alpha lerped 0.25↔0.65 over 1.6 s, only in `Shown` | PASS |
| Tap target | whole pill | opens `ScreenId.MissionSelection` | `tap_opens_mission_selection` PASS via the real `Button.onClick` | PASS |
| Mission Selection streak | completed `missions_v1` card | the same flame, collapsed + expanded, hidden at 0 | `streakflame_shared_prefab` PASS — all three resolve to `Assets/Prefabs/UI/Common/StreakFlame.prefab` | PASS |

**PASS\* — the one deviation, and why.** The Figma y of 725 assumes a **340px** News Banner
Container (312 pop-up + 12 + 16 dots). The shipped Home notice panel is **352px** and its page
dots are a separate sibling below it, so "24px under the notice" lands at 737, not 725. The rule
implemented is the *relationship* the spec asked for (`notice bottom + 24`), computed from the
live panel — which is what makes it survive a notice of any height — and it reproduces the Figma
number exactly for a 340-tall notice. Flagged rather than hard-coding 725.

**JA font weight.** The JA string renders through `NotoSansJP-VariableFont_wght SDF`, the only
JP SDF in the project, so it is lighter than Rubik-SemiBold. That is the project-wide JA
rendering, not something this task introduced, and the node has no JA variant to diff against.

## UI fidelity lint

`spec.json` generated from the real node output with `Docs/Scripts/figma_node_to_spec.py`
(`get_metadata` XML + `get_design_context` JSX). The generator's default Figma→TMP divisor is 1.2;
this task's measured divisor is **1.125** (at TMP 40 the EN label renders 431.0px against the
node's 433px box, and `"5"` renders 27.8 against 29), so the spec's `fontSize` rows were set to
40 — per the standing rule that the reference render, not the arithmetic, decides visual size.

| Prefab | Lint JSON | fail | warn |
|---|---|---|---|
| `DailyMissionPill.prefab` | `Docs/Diagnostics/_capture/DailyMissionPill_lint.json` | 0 | 0 |
| `StreakFlame.prefab` | `Docs/Diagnostics/_capture/StreakFlame_lint.json` | 0 | 0 |
| `MissionCard.prefab` | `Docs/Diagnostics/_capture/MissionCard_lint.json` | 0 | 36 (all pre-existing: runtime-bound reward icons, the pending localisation batch, the card body's own 9-slice; none on the new `StreakFlame` children) |

**Tripwire (PIPELINE_HARDENING §20).** The spec layer was proven to assert, not just to pass:
temporarily changing the flame's `w` from 58 → 999 produced
`[FAIL] Flame ::width:: expected 999, got 58` and `RESULT: FAIL`; the spec was restored
immediately and the real run is `0 FAIL`. Note the linter's colour check covers `Image` fills
only — a TMP colour change did **not** trip it, which is why the label colour is proven by pixel
sample above instead.

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| Fidelity table PASS/FAIL against both Home renders (EN + JA, notice shown and hidden) | PASS | Table above, every row measured. Frames: `home_notice_streak5_en.png`, `home_nonotice_streak5_en.png`, `home_nonotice_streak5_ja.png`, contact sheet `_contact_states.png` |
| Y follows the notice; flipping it live moves the pill without a relaunch | PASS | `y_with_notice_is_24_under_it` = −737.0 (expected −737.0) and `y_no_notice_is_361` = −361.0 in `pill_invariants.json`. The notice was flipped through its own chain (`NoticeService._entries` cleared → `OnNoticesChanged` → `HomeScreenController.SetNewsPanelVisible` → `RefreshPlacement`), in one play session, no relaunch |
| Enter animation from off-screen left, glow loop running in `Shown`, no per-frame allocation | PASS | `videos/daily_mission_home_pill_demo.mp4`; `screenshots/home_enter_frame1_offscreen.png` + `home_enter_frame2_sliding.png` + `_contact_enter_animation.png` are consecutive decoded mp4 frames showing the pill part-way on. `glow_loop_no_per_frame_alloc`: **68 B/frame over 180 frames for the WHOLE Home screen** — an upper bound that the pill's `Update` cannot exceed |
| Claiming the daily → pill leaves; next launch with a claimed daily → no pill | PASS | The account this ran on **has genuinely claimed today's daily on prod**: the unseeded boot fetch returned `date='2026-08-30' streak=1 claimed=True hasRecipe=True → showPill=False` and the pill stayed parked at x=−585 (`home_live_fetch_claimed_no_pill.png`, verified by pixel: no gold border at the pill row). The claim→leave transition is `MarkClaimed` → `home_after_claim_no_pill.png` and the video |
| Simulated UTC rollover → old pill leaves, fetch, new pill enters with the new streak | PASS | `home_rollover_old_shown.png` (pill present, pixel-verified) → `home_rollover_old_leaving.png` (x=−585) → `home_rollover_after_real_refetch.png` (the **real** re-fetch ran and answered `claimed=True` for this account, so correctly no pill) → `home_rollover_new_pill_in.png` (x=36, streak 4). Seam: `DailyMissionState.Date` written directly, exactly as the spec specifies |
| Flame + number hidden at streak 0; shown at 5 and at 12 (two digits fit) | PASS | `home_nonotice_streak0_en.png` / `..._streak5_en.png` / `..._streak12_en.png`; `streak_zero_hides_flame` PASS; `"12"` auto-sizes to 34.7px and measures 41.4px inside a 43px bulb. At streak 0 the pill also **shortens to 481** rather than leaving the flame's slot empty |
| Same `StreakFlame` prefab on the Mission Selection daily card, collapsed + expanded; the old fields removed | PASS | `streakflame_shared_prefab` PASS — pill, collapsed card and expanded card all resolve to `Assets/Prefabs/UI/Common/StreakFlame.prefab`. `streakText` / `streakTextExp` / `streakChip` are gone from the class (`SerializedObject.FindProperty("streakText") == null`), from the prefab YAML, and their two orphaned scene copies were deleted. `missioncard_collapsed_flame.png`, `missioncard_expanded_flame.png` |
| Tap opens Mission Selection with the daily card present **and expanded**; `daily_pill_tap` lands | PASS | `tap_opens_mission_selection`, `pill_tap_expands_daily` and `daily_pill_tap_queued` all PASS (`'daily_pill_tap'` found in `TelemetryService._queue`, queue 4→6) — both driven by the **real** `DailyMissionPill Button.onClick.Invoke()`, never `ShowScreen` directly (Real-entry rule). `missionselection_after_pill_tap.png` |
| `HOME_DAILY_PILL` reached the `texts` catalog; `--check` clean; zero new hardcoded `.text` | PASS | plan → `--apply` (16 drafts) → published (`content_publish` → **v17**) → `export_content.py --check` **exits 0, "clean — no file would change and no catalog has drifted"**. Grep of the three new `.cs` files finds no `.text =` at all; the only text write is `SetText("{0}", streak)`, a number. The string is bound by the sanctioned `LocalizedText` component on the prefab |
| Mode carousel, promo banner, notice panel untouched; full EditMode sweep green; no Console errors | PASS | `ShellScene.unity`'s first diff was **+119 / −0 lines, 0 `m_IsActive` and 0 `m_SizeDelta` changes**; the only other scene edits are the `DailyMissionCard` orphan removal and its sibling order. `ModeCarouselSection`, `PromoBanner` and `NoticePanel` are not touched by any diff hunk. EditMode: **1939 passed / 0 failed / 3 skipped** (the 3 are pre-existing documented `Stage C1` skips). No errors in the Console for this task |

## Known FAIL items

None.

## Spec deviations

1. **Home lives in `ShellScene`, not `Assets/Prefabs/UI/HomeScreen.prefab`.** The spec's file list
   names the prefab, but `Canvas/ScreensRoot/HomeScreen` is a **plain scene object**
   (`PrefabUtility.IsPartOfPrefabInstance == false`) and that prefab's GUID
   `3119df30d8b9ea648b5986c7b060185f` is referenced by **no** scene or prefab in the project — it
   is stale. The pill was built into `ShellScene`, which is the live surface; the dead prefab was
   left alone rather than adding diff to something nothing loads.

2. **The pill's panel is a baked sprite generated from the node, not a reused 9-slice.** The spec
   said "same navy panel + gold border style as the Home card headers". No such atom exists: a
   scan of all 44 nine-sliced UI sprites found the two navy panels carry a steel-blue and a
   silver-white border, and every gold-edged sprite is a solid-gold button. Reusing either would
   have shipped the wrong border colour against an explicit `#FCF195` token — the exact class of
   miss Rule 18 exists for. Surfaced here rather than silently approximated; the generator
   (`Docs/Scripts/make_daily_pill_panel.py`) keeps the four tokens as the source of truth.

3. **The glow is its own blurred sprite, not the border sprite outset.** The first build followed
   the spec's first option literally (the same border sprite, outset 10px, additive) and it read
   as a crisp second gold outline rather than a halo. Switched to the spec's second option, "a
   soft-blurred copy of the border".

4. **The label is bound by the existing `LocalizedText` component, not written by the controller.**
   The controller's `ApplyLabel` duplicated what that component already does (including its
   per-language size override) and would have fought it on a language switch. `labelText` was
   removed from the controller.

5. **The Mission Selection badge moved into the title row (Cesar, iter 2).** It was its own
   46px row below the countdown, which made it a speck in a 1026-wide centred row; it is now 41 ×
   64 immediately left of the title, in both the collapsed and the expanded card. The expanded
   card gained a `TitleHRowExp` wrapper cloned from the collapsed side's `TitleHRow` so both
   states lay the badge out the same way.

6. **Offline does NOT show the pill.** SPEC §2 says "the deterministic local daily recipe
   `missions_v1` already builds counts as `HasRecipe`, so the pill shows". There is no local
   recipe: `MissionSelectionScreenController.RefreshDaily`'s own doc comment says the offline
   fallback was "deliberately NOT done here, and flagged" because it needs a C# port of the
   server generator. So offline the pill is absent, exactly as the daily card is. **Open question
   for the Architect below.**

## Console output

No errors or warnings attributable to this task. The only recurring Console lines from the pill
are its own informational ones:

```
[DailyPillBot] LIVE FETCH: known=True date='2026-08-30' streak=1 claimed=True hasRecipe=True → showPill=False
[DailyPill] UTC rollover (2020-01-01 → 2026-08-30) — swapping the pill.
```

A recorder warning, pre-existing for every full-res clip in this project and expected at Cesar's
mandated 1170×2532:

```
[MovieRecorder: DailyPillDemo] Recording may cause slowdowns or generate an invalid file.
The image size exceeds the recommended maximum height for H.264: 2160 px
```

## Open questions for Architect

1. **Offline behaviour contradicts the spec** (deviation 6). Spec §2 assumes a local recipe that
   `missions_v1` explicitly did not ship. Current behaviour: offline ⇒ no pill. Is that the
   intent, or should the local generator now be ported (a separate task — a second
   implementation of a deterministic draw is the one thing `missions_v1` was careful not to do)?

2. **Content publish scope.** The mandated importer run carried 15 rows that were **not** mine —
   6 `MISSION_*` keys and 9 `LOADOUT_SUP_*` values from `missions_v1` (`0ef3bd912`, `cf2eb8d1e`)
   whose importer step was never run. Conflicts were 0 and the values were byte-identical to the
   already-shipping CSV, so publishing them made the server agree with the bundle rather than
   changing anything a player sees. Flagging it because the published version bump (v16 → v17)
   covers more than this task.
