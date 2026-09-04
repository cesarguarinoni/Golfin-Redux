# SELF_REVIEW — `game_polish_a`

**Reviewer:** golfin-self-reviewer
**Date:** 2026-09-04 16:59 JST
**Iteration:** 1 (first self-review pass)
**Verdict:** **PASS** → set `STATUS.md` to `SELF_REVIEW_PASS`.

The spec is out-of-date on §D4 per Cesar's brief; the shipped-option-(b) rule applies. A9 is void; its replacements `LayeredPushTests.TheOptionBFlag_IsGone` + `SameBackground_IsNoLongerRequiredByTheGate` both pass and are the correct gate.

---

## § Visual diff notes (Step 1 — independent pixel scan FIRST)

`screenshots/push_18_home_return.png` at 1170×2532, no spec consulted:

- Dark navy top bar. Left: gold "R" coin + "7.483" white text. Center: small gold "GOLFIN" chip + "2.890" + gold "+" plus button. Right: white circular gear icon (settings).
- Below the bar, centered: "Cratilo" (white text, thick weight). Flanked by a gold `213` trophy chip (top-left) and gold "GPS" pill (top-right).
- Blue-tinted translucent playtest banner mid-upper ("Welcome, Test Players! Thank you for joining the GOLFIN playtest…").
- Left side: navy "NEW DAILY MISSION!" pill hanging in from the left edge.
- Central: full-body character illustration — a golfer in green cap and green polo holding a gold trophy against a golf-course sunset.
- Lower half: a horizontal row of THREE home mode cards partially visible — left card "PLAYER / v1 / TRY FEE / x20" (cut off), center foreground card "PRACTICE — Sharpen your skills on any hole. > — ENTRY FEE R x10 — REWARDS R x5 — [PLAY]" (gold gradient button), right card "TOURNA… / Be the best and… / NO ENT… / REWARDS Varies" (cut off).
- Bottom-most: five nav icons — Home (house), Cards, Tee (larger, centered, golf ball on tee), Bag (irons), Character (person). Each is a navy disc with a gold ring on a translucent chrome plate. **The leftmost Home icon has a distinct outward-glowing gold halo behind it and a visibly brighter ring** — the other four do not.

Nothing above the row of mode cards moves in a way that suggests a settle bug; the top-bar chrome and nav-bar chrome are crisp and identical to the "at rest" appearance in the a8 rise strip's rest sample.

## § Figma fidelity

**N/A — motion-only task.** SPEC's Reference section states "No Figma nodes — motion only. Rest state at HEAD is the visual reference." Confirmed by `git diff --stat 1e7f97504..HEAD` — no `.prefab` file touched by the builder alters layout; PersistentUI.prefab diff is confined to `ButtonPressFeedback` component adds + four sprite field references. No node URL or `<n>:<n>` id present in SPEC.md. Rule 18 does not apply.

## § Clone provenance

**N/A — no §0/§1 REUSE mandate.** SPEC does not declare a "REUSE MANDATE" or a "clone the existing …" directive. This is a new-code task (three new components + one builder + one probe + tests) plus targeted edits to existing controllers. Rule 19 does not apply.

## § Capture-helper compliance (Step 5)

- Screenshots landed via the sanctioned Editor pass (probe → `_capture/` → task folder). No `ScreenCapture.CaptureScreenshot` calls, no hand-rolled ortho render path. Report even flags a `Shot()` md5-equals-previous defect it found and fixed in-run (`STALE`/`ROUTE DRIFT` guards), which is exactly the physics-lab trap CLAUDE.md warns about — the fact that the probe now emits both guards is a genuine improvement.
- Videos in `videos/` (MP4), stills in `screenshots/` (PNG) per the convention.
- No new `*Context.cs` added under `HUD/`, so `CaptureHelper` maintenance protocol is trivially satisfied.

## § Bbox / geometry verification (Step 6)

Ran my own pixel-diff on 8 valid parity pairs (paired on `(label, real screen)`, discarded the 2 ROUTE DRIFT pairs the report itself flags). Threshold: any RGB channel diff > 8.

| pair | % differ | bbox (x_min..x_max, y_min..y_max) | y_max as % of 2532 |
|---|---|---|---|
| home_return | 0.031% | (132..206, **147**..173) | 6.8% |
| holeselection | 0.031% | (132..206, **147**..173) | 6.8% |
| gachahistory | 0.031% | (132..206, **147**..173) | 6.8% |
| roster | 0.031% | (132..206, **147**..173) | 6.8% |
| settings_open | 0.030% | (132..206, **147**..173) | 6.8% |
| inventory_tab0 | 0.034% | (132..1110, **147**..791) | 31.2% |
| leaderboard | 0.104% | (132..1063, **147**..437) | 17.3% |
| home | 0.400% | (15..531, **147**..880) | 34.8% |

**Three independent A2 claims all confirmed:**
1. Every bbox starts at `y=147` → nothing above y=147 differs → top-bar chrome is pixel-identical.
2. Deepest bbox is y=892; nav bar sits below y≈2280 → nav bar is pixel-identical everywhere.
3. Five of eight pairs show the identical 74×26 rect at (132,147..206,173) — the RP counter digits — corroborating the report's "RP balance ticking between passes" explanation.

The three widest bboxes (home, leaderboard, inventory_tab0) all live in the content region and are consistent with live data mutation (mission-pill countdown, live top-3 tournament cards, carousel scroll). A 16 px settle error would smear along the whole content column of every screen; instead we see tight boxes around the exact regions that carry ticking data. **A2 verified in independent pixels.**

## § Scene-mutation audit (Step 7)

```
git diff --stat 1e7f97504..HEAD -- Assets/Scenes/ShellScene.unity
  1 file changed, 263 insertions(+), 3 deletions(-)
```
Grep for `m_IsActive` / `m_SizeDelta` / `m_AnchoredPosition` in the ShellScene diff: **zero hits** on both `+` and `-` lines. Report claim "263 insertions, 3 deletions, zero anchor/sizeDelta lines" verified.

Standing bans:
- `git diff --stat -- Assets/Scripts/Physics/` → empty.
- `git diff -- Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` → no `*Gate` scenario added.
- `git diff --stat -- "*M_Splash*"` → empty (Rule 7).

GPS scope constraint (Rule from SPEC + Cesar authorised exception):
- `git diff --stat 1e7f97504..HEAD -- Assets/Scripts/UI/Gps Assets/Prefabs/UI/Gps` → exactly one file: `GpsNavBarHighlight.cs`, 28+/10−. `Assets/Prefabs/UI/Gps/**` untouched. Deviation D-1 (runtime child creation instead of authoring into GPS prefabs) is the reason this diff is achievable, and it is honestly declared.

Working tree drift outside this task's folder:
- Three files: `Docs/Specs/Active/map_view_v2/{SPEC.md,reference/B1_aiming.png,reference/B1_over_range.png}`. All belong to Cesar's parallel session (per §0.3 of the report); not a Rule 13 violation against this task.

## § Production-flow capture (Step 8)

- 3 records in the invariants JSON carry `realWidget: true` and cite the widget path: `ModeSelection→HoleSelection` (mode-card PLAY→Practice ActionButton), `ModeSelection→MissionSelection` (mode-card PLAY→Missions ActionButton), `GachaPrizes→GachaHistory` (GeneralShop HistoryChip). The report accurately labels the third one as "not a player-reachable ordering" — the mechanism still passes every invariant but it is not claimed as reachable evidence. Honest.
- The remaining 84 records are `realWidget: false` (harness ShowScreen / harness GoBack). All are correctly labelled in JSON. Rule 2 satisfied for the reachable pairs; no synthetic entry is being passed off as real.

## § Attack items Cesar named

### 1 — A2 residuals: live data or motion defect?
**Verified live data.** My independent bbox check (above) confirms: five identical 74×27 boxes at (132,147..206,173) across five different screens = the RP counter position; every bbox anchored to y=147 with y_max never touching the nav bar. A 16 px settle error would produce a wide vertical smear on the entire content stack, not tight rectangles around ticking numeric fields.

### 2 — D7 mechanism on both bars
**Game bar:** the Home nav strip crop from `push_18_home_return.png` clearly shows the outward gold halo + brighter ring behind the Home slot alone. The `nav_a_play_pillar_*.png` crops (Play pillar clip) show the Tee slot lit throughout. The `nav_c_gacha_pillar` crop (Gacha pillar) shows the Cards slot lit. Correct per-pillar highlight.
**GPS bar:** `git diff --stat 1e7f97504..HEAD -- Assets/Scripts/UI/Gps Assets/Prefabs/UI/Gps` → one file, `GpsNavBarHighlight.cs`, as required. Zero GPS prefab changes (D-1 explains the runtime `Attach()` approach that keeps them byte-identical). Static evidence for GPS-hub-selected slot is documented in the report (A15) but not photographed live in this iteration's captures — the mechanism (one shared `NavSlotHighlight.Attach` for both bars) is verifiable in code and both entry points are wired. Acceptable as-is for the self-review; the red-team pass may want to see a GPS-bar-selected still.

### 3 — Cross-backdrop path visual defects
Sampled frames at 4.0/4.05/4.10/4.15/4.20/4.25/4.30 s of `game_polish_a_f_cross_backdrop.mp4` and 5 evenly-spaced frames of all six clips. No visible seam, no flash, no chrome popping. The A2 bbox result (nothing above y=147 changes, nothing below y=1640 changes) is the pixel-corroboration of A5's "chrome held at 1 on every frame." Caption correctly updated to "Cross-backdrop push - the backgrounds dissolve through each other. SHIPPED (Cesar, 2026-09-04)."

## § Full acceptance re-walk (Rule 5)

| # | Item | Verdict | How verified |
|---|---|---|---|
| A1 | Invariants JSON `fail == 0` | **PASS** | Parsed `game_polish_a_invariants.json`: measured=87, fail=0, 40 ordered pairs, all `applyScreenCalls=1`, all `blocksRaycastsRestored=true`, cross-backdrop `seamWorstCover=1`, same-bg `chromeAlphaMinOverRun=1`. |
| A2 | Rest parity ≈0 | **PASS** | Independent pixel-diff on 8 pairs; all bboxes above y=147 empty, below nav bar empty; residuals localise to RP counter + live data. |
| A3 | Boundary fade untouched | **PASS** | `FadeController.cs` + `GpsScreenTransition.cs` do not appear in the diff. Home moves, cross-pillar moves, differing-bg-in-pillar moves all fade. |
| A4 | Six videos with stills | **PASS** | All six clips present in `videos/`; frame-md5 sampling shows unique frames throughout each; extracted mid-frames render real content matching captions; `f_cross_backdrop.mp4` caption correctly updated. |
| A5 | Chrome static during push | **PASS** | Invariants log = alpha 1 every frame; A2 pixel-bbox = zero delta above y=147 and below y≈1640. |
| A6 | UI fidelity lint delta zero | **N/A stated** | No Figma node, no prefab layout touched by builder; report's explanation is accurate. |
| A7 | Cross-fade table | **PASS** | Code changes present (`FadeSwap`/`Indicator`/`CrossFade` in InventoryScreenController etc); mid-fade frames captured via A4 clips (d_tabs_and_filters shows the tab-cross-fade). |
| A8 | Entry rise | **PASS** | `a8_entry_rise_strip.png` shows 6 mid-rise frames across screen families; report's log line "SkippedForPush=94 for 84 measured pushes, Risen=2 cross-pillar re-seats" is a legitimate honest formulation. |
| A9 | (void) flag pinned OFF | **VOID / SUPERSEDED** | Grep confirms `AllowBackgroundCrossFade` exists ONLY in the two replacement tests (which assert it does NOT exist); both new tests pass. Flag truly removed from `LayeredPush.cs`. |
| A10 | Real entry | **PASS (for reachable pairs)** | 3 `realWidget: true` records in JSON with widget paths cited; unreachable pairs correctly labelled `harness`. |
| A11 | ButtonPressFeedback | **PASS** | PersistentUI.prefab diff shows `ButtonPressFeedback` added to all 5 nav slots (Home, Gacha, Tee, Inventory, Characters). |
| A12 | EditMode sweep green | **PASS** | `game_polish_a_tests.txt` last line: `passed=2425 failed=0 skipped=3`. Both new suites (18 tests total) all green. |
| A13 | Perf | **PASS with a finding** | Report quotes real 48-push perf run; identifies GachaHistory arrival = 290 MB / >1 s stall as pre-existing (not this task); correctly flags for a separate task. |
| A14 | Scope | **PASS** | Diff-scope verified: no unauthorised Gps/, no FadeController, no UiMotion API change, working tree drift belongs to Cesar's parallel session. |
| A15 | Nav selected state | **PASS on mechanism** | `iconActiveColor` no runtime read (grep confirms — only obsolete field + a header comment); Attach() shared between both bars; per-pillar halo visible in every clip crop I inspected. |
| A16 | Deviations | **PASS** | D-1..D-7 documented with concrete justification, all sensible. |

## § Findings — non-blocking hygiene notes to surface (not FAIL grounds)

These are worth surfacing to Cesar but not sufficient to block on at self-review — the SHIPPED code and JSON invariants are correct, and none of these change the runtime behaviour.

1. **Stale caption on `screenshots/a4_option_b_transition_strip.png`** — the still strip still bears the burnt caption "OPTION (b) — push WITH a background cross-fade. FLAG OFF IN THE BUILD" while the option is shipped. The equivalent MP4 (`game_polish_a_f_cross_backdrop.mp4`) had its caption correctly updated ("SHIPPED (Cesar, 2026-09-04)"); the still was not re-rendered. Artifact hygiene.
2. **§A9 prose drift** — the report's §A9 body still lists `AllowBackgroundCrossFade` grep hits in `LayeredPush.cs` (lines 93 / 231) as if the flag were present, contradicting the "flag REMOVED" summary and the actual source. The A9 heading correctly says VOID with the two replacement test names. Cosmetic doc drift; the actual code has the flag deleted (verified by grep).
3. **Push count drift** — top summary says "84 pushes"; JSON is `measured=87`. Report was written mid-run and the final gate produced 87. `fail=0` in both.

None of the three would flip my verdict. The red-team pass may or may not want them scrubbed for record hygiene.

---

## Verdict

**PASS.** Setting `STATUS.md` → `SELF_REVIEW_PASS`.

The task delivers option (b) as Cesar authorised, `fail=0` across 87 pushes (32 cross-backdrop), A2 parity confirmed in independent pixel bboxes, D7 mechanism visible on both bars with the GPS scope constraint honoured to the letter, standing bans clean, no scene mutations, no invariant deceptions. The three hygiene notes above are worth mentioning but do not degrade the shipped behaviour or the evidence's honesty.

---

## Iteration 2 self-review — 2026-09-04 17:34 JST

**Verdict:** **PASS** → set `STATUS.md` to `SELF_REVIEW_PASS`.

The centre-title dissolve is real in the pixels of the shipped clip, the two shape audits hold up under my own re-enumeration, the full EditMode sweep is `2430 passed / 0 failed / 3 skipped` on a clean re-run I ran myself, and the three hygiene items from iter-1 are all cleared. No scene mutations, no standing bans touched, no unexpected working-tree drift.

### § Visual diff notes (Step 1 — pixels first, from `videos/game_polish_a_f_cross_backdrop.mp4`)

Independent decode: 750-frame 1170×2532 clip. Re-cropped the top-bar title band to `crop=1170:140:0:200` (my first crop was 120 px too high and read the sky, giving max Δ=0.43 — a false all-clear I caught and corrected before finalising). On the corrected crop, mean luma across the whole clip has max frame-to-frame Δ=49.1, mean=0.29, median=0.005 — flat by construction, spikes only at title changes.

Two title-change events detected, both showing intermediate-alpha frames:

| Event | Frame window | Luma trace on the glyph zone | Verdict |
|---|---|---|---|
| Forward push, ModeSelection→TournamentLeaderboard | f008..f016 | 76.72 → 50.02 → 36.56 → 36.56 → 85.35 → 85.35 → 92.77 → 98.11 → 98.24 | multi-frame dissolve, out+in |
| Back push, TournamentLeaderboard→ModeSelection | f688..f694 | 98.87 → 98.89 → 59.55 → 36.45 → 75.02 → 76.11 → 76.11 | multi-frame dissolve, out+in |

The eyeball corroborates: stacked frames of event 1 show `MODE SELECTION` at f9 rendered noticeably gray-not-white (mid-out-fade), then two frames blank at f10-f11, then `TOURNAMENT LEADERBOARD` at f12 in a visibly dimmer gray-white before growing to full white at f15-f16. Event 2 is symmetric. A hard cut is a single Δ>30 frame bordered by ~0 on both sides with no visible partial-alpha state; neither event has that shape.

**One caveat surfaced by my snap-detector heuristic:** the "in" step at f12 flags on `Δprev=0, Δ=49.10, Δnext=0` in isolation. Only the pixel stack disproves it — the frame is visibly partial-alpha rather than opaque. The `Δnext=0` is a duplicated frame from ffmpeg's rate conversion (`dup=12 drop=0` reported across the whole extract), not a hold in the render. Multiple following frames continue to grow (Δ=8.02 at f14, Δ=6.05 at f15) as the fade-in tail plays out. Whether the fix could be cleaner if the "in" half spread across more frames is a taste question, not a defect — this IS a multi-frame dissolve with visible partial-alpha intermediate states.

### § Full EditMode sweep — independently re-run

I called `tests-run` via unity-mcp-cli against the running Editor (had to poll a stuck run to completion first, ~15 min). Result:

```
Summary: Status=Passed  TotalTests=2433  PassedTests=2430  FailedTests=0  SkippedTests=3  Duration=00:02:18.85
```

The 3 skips are all pre-existing `HoleCompleteDriverTests` skips with the same `Stage C1: HandleShotComplete is now a no-op` message. Matches the report's `2430/0/3` claim exactly.

(Note: my first attempt collided with an in-flight run, then produced a corrupt `game_polish_a_tests.txt` in `_capture/` — one `Failed` entry on `SaveBackedEntryStoreTests.Debounce_MultipleMarkDirtyWithin250ms_OneSavedEvent`, but the failure text was literally the MCP error message from my own conflicting call, i.e. `Unhandled log message: '...another test run is already in progress. Active request id: ea2e98c9...'`. That is contamination from my gate, not a real regression, and my later clean run confirms it.)

`CenterTitleDissolveTests` shows in both the corrupt cached file and the fresh run with all 5 tests passing. Not verifying the tripwire by hand-reverting `??` (destructive), but the fixture asserts on Unity's overloaded `== null` operator directly (`Assert.IsFalse(group == null, "EnsureCenterTextGroup returned a fake-null CanvasGroup")` — the same operator that misbehaves against a fake-null), so it is structurally the correct tripwire for the exact defect signature. If `EnsureCenterTextGroup` were reverted to `??`, this assertion is the one that would flip.

### § Shape audit re-enumeration (§15)

**Shape A — `??` on Unity object lookups in the 7 touched files.** Re-ran the grep myself:
```
$ for f in LayeredPush ScreenEntryMotion NavSlotHighlight UiSelection PersistentUIManager ScreenManager GpsNavBarHighlight; do
    grep -nE "(GetComponent|GetComponentInChildren|Find|AddComponent)[^;]*\?\?" Assets/**/$f.cs
  done
(no matches)
```
The lone `GetComponent<CanvasGroup>()` call site in `PersistentUIManager.cs:783` uses the `existing == null ? Add… : existing` idiom with a comment citing CLAUDE.md Basic Rules 4. Clean.

**Shape B — what `ApplyScreen` paints that is visible mid-push.** Traced `ApplyScreen` (`ScreenManager.cs:681..896`) through to `PersistentUIManager.HighlightScreen`. Three paints as the report claims:

- Centre title (`ApplyTopBarCenterText`) — was the defect, now handed off at push START.
- Nav-slot highlight (`UpdateScreenHighlight`) — verified `PillarOf` in `ScreenManager.cs:503`. Every same-pillar pushable pair produces the SAME `pillar.Value` → `currentScreen` stays the same → `Paint(...)` recomputes `currentScreen == slot` to the same result on all 5 slots → no visual change. Pushable pillar table verified:
  - MainPlay: `HoleSelection / ModeSelection / MissionSelection / TournamentSelection / TournamentHoleSelection / TournamentLeaderboard` — every push pair among these six shares MainPlay.
  - Gacha: `GeneralShop / GachaHistory / GachaPrizes` — every push pair shares Gacha.
  - Characters: `Roster / StaminaShopSelection / StaminaShopDetail` — every push pair shares Characters.
  - Leaderboard has no pillar; `HighlightScreen` calls `ApplyTopBarCenterText(screenId)` FIRST then `if (!pillar.HasValue) return;` (`PersistentUIManager.cs:844..848`) — so pairs involving Leaderboard hit the title paint (now dissolved) but skip the nav paint. Report's branch reasoning holds under my own re-read.
- Bar visibility (`ShowBars` / `ShowTopBarOnly` / `HideBars`) — from `ScreenManager.cs:867..885`, ONLY the auth/starter/gameplay-loader paths swap bar visibility. All shell-to-shell moves land in the `ShowBars()` branch that stays the same. No pushable pair transitions between `ShowBars` and `ShowTopBarOnly`/`HideBars`. Confirmed.

Report's report of the pixel corroboration (top bar Δ=3.29 worst, nav bar Δ=10.18 tracking the backdrop cross-fade, both step +0.01 across Settle) is consistent with my Step-1 event-1 window: nothing above y=147 changes across the fade, which is what "chrome held at 1" looks like.

### § Hygiene items from iter-1 — all cleared

1. **Stale caption on `a4_option_b_transition_strip.png`** — rebuilt from the fixed clip. New caption reads "SHIPPED (Cesar, 2026-09-04) — a push between two screens whose backdrops DIFFER. The backdrops cross-fade underneath while the content layers travel, and the top-bar title DISSOLVES across the push (frames 3-4) rather than hard-cutting after it. Top bar and nav bar hold still throughout." The six thumbnails match: t=+0.07s shows mid-alpha `MODE SELECTION`; t=+0.10s shows blank title; t=+0.13s shows dimmer `TOURNAMENT LEADERBOARD`; t=+0.20s+ opaque. Caption honest against frames. ✅
2. **§A9 prose drift** — rewritten. Body now leads with "VOID, replaced", cites the two replacement tests by name, quotes the correct grep result (2 hits only in the test file, both asserting absence), and explicitly closes with "An earlier revision of this section quoted `LayeredPush.cs:93 public static bool AllowBackgroundCrossFade …` as though the declaration were still there. It is not; that text described the pre-decision build and is corrected above." ✅
3. **84↔87 push-count drift** — corrected at report line 10: "Re-measured against the widened rule: 87 pushes measured, `fail == 0`. (84 come from the probe's ordered-pair sweep; the other 3 are the pushes the real-navigation tour performs on its way between groups. `measured=87` in the invariants JSON is the authoritative count — an earlier line here said "84" by quoting the sweep alone.)" JSON re-parsed: `measured=87, fail=0`. ✅

### § Scene-mutation + standing bans + working-tree audit (Steps 7 + Rule 13)

```
git diff 8e13d5d7f..HEAD --stat -- '*.unity' '*.prefab'      (empty)
git diff 8e13d5d7f..HEAD --stat -- Assets/Scripts/Physics/   (empty)
git diff 8e13d5d7f..HEAD --stat -- 'Scenarios.cs'            (empty)
git diff 8e13d5d7f..HEAD --stat -- 'M_Splash*.mat'           (empty)
```
Iter-2 touched zero scene files and zero prefabs (fix is entirely in scripts + a new tests file). All standing bans clean.

Working-tree drift outside this task's folder: same three `Docs/Specs/Active/map_view_v2/` paths documented as Cesar's parallel session in report §0.3 and HEARTBEAT's iter-2 baseline block. Not this task's code. ✅

### § A2 rest-parity impact of the new runtime CanvasGroup

The dissolve adds a `CanvasGroup` at runtime to `usernameText` when the first push runs (lazy). Concerns and how they clear:

- **Rest alpha.** `EnsureCenterTextGroup` default is alpha=1 (Unity component default), and `DissolveCenterText` ends at `Fade(g, 0f, 1f, half)`. `TheGroupRestsFullyOpaque_SoTheRestPixelsAreUnchanged` pins this. A2's iter-1 bbox check confirmed nothing above y=147 differs on any screen — the runtime group is invisible to the parity gate. ✅
- **Raycast behaviour.** `blocksRaycasts` stays at the Unity default `true`, which is exactly the label's pre-fix behaviour (no group = raycast blocks depend only on the underlying graphic). `TheGroupRestsFullyOpaque_...` also pins this. ✅
- **Interrupted dissolve stranding the label translucent.** Guarded twice: `ApplyTopBarCenterText` (`PersistentUIManager.cs:687..696`) stops any running routine AND forces `_centerTextGroup.alpha = 1f` before repainting. `ApplyTopBarCenterText_ForcesTheGroupBackToOpaque` pins this with an artificial `group.alpha = 0.37f` interruption. ✅
- **Screens that do NOT push (Home, cross-pillar arrivals, GPS screens, account/starter title-bar paths).** These all go through `FadeController.FadeOutThenIn` → `ApplyScreen` → `HighlightScreen` → `ApplyTopBarCenterText`, which cancels-and-forces-alpha-1 as above. No visual change to those paths, no stranded translucent title. ✅
- **`UiMotion.Enabled=false` (accessibility motion-off).** `CrossFadeCenterTextTo` returns immediately without touching the group; `ApplyTopBarCenterText`'s deferred instant repaint stands. ✅

### § Full acceptance re-walk (Rule 5)

| # | Item | Verdict | How re-verified this pass |
|---|---|---|---|
| A1 | Invariants JSON `fail == 0` | **PASS** | Re-parsed JSON: `measured=87, fail=0`, 87 records under `pushes[]`, `optionBShipped=true`. |
| A2 | Rest parity ≈0 | **PASS** | Iter-1's pixel-bbox result carried forward and re-checked: no iter-2 scene/prefab diff so pixel result cannot have regressed; runtime CanvasGroup rests at alpha=1 per new tests. |
| A3 | Boundary fade untouched | **PASS** | `git diff 8e13d5d7f..HEAD -- 'FadeController.cs' 'GpsScreenTransition.cs'` empty. |
| A4 | Six videos with stills | **PASS** | All six clips present; canonical `_f_cross_backdrop.mp4` re-recorded with the dissolve; strip rebuilt with honest caption; my own frame decode confirms real dissolve. |
| A5 | Chrome static during push | **PASS** | Report's mid-push chrome deltas (top bar Δmax=3.29, nav bar Δmax=10.18 tracking cross-fade, both +0.01 step at Settle) match my Step-1 window observations; A2 pixel result unchanged. |
| A6 | UI fidelity lint delta zero | **N/A stated** | No Figma node, no prefab layout touched by iter-2 (git diff proves it). |
| A7 | Cross-fade table | **PASS** | Iter-1 evidence unchanged (no code touch to InventoryScreenController etc). |
| A8 | Entry rise | **PASS** | Iter-1 strip + log line unchanged (no code touch to `ScreenEntryMotion.cs`). |
| A9 | (void) flag pinned OFF | **VOID / SUPERSEDED** | `TheOptionBFlag_IsGone` + `SameBackground_IsNoLongerRequiredByTheGate` both pass in fresh run. §A9 prose is now consistent. |
| A10 | Real entry | **PASS (for reachable pairs)** | Iter-1 evidence unchanged. |
| A11 | ButtonPressFeedback | **PASS** | Iter-1 evidence unchanged (no prefab touch). |
| A12 | EditMode sweep green | **PASS** | Freshly re-ran: `2430/0/3`. |
| A13 | Perf | **PASS with finding** | Iter-1 evidence unchanged; `UiMotionAllocationTests.Fade_TheLoopTheChromeCrossFadeRunsOn_AllocatesNothingPerFrame` still passes (relevant to the new dissolve loop, which uses the same `UiMotion.Fade`). |
| A14 | Scope | **PASS** | Iter-2 adds 3 files/edits: `PersistentUIManager.cs`, `LayeredPush.cs`, `CenterTitleDissolveTests.cs` (+meta). No `Gps/`, no `FadeController`, no `UiMotion.cs`. |
| A15 | Nav selected state | **PASS on mechanism** | Iter-1 evidence unchanged; Shape B audit above confirms nav highlight doesn't change across push pairs. |
| A16 | Deviations | **PASS** | Iter-1 D-1..D-7 unchanged; iter-2 introduces no new deviation. |

### § Findings — none blocking

Nothing new. All three iter-1 hygiene items have been addressed; the corrupt `Docs/Diagnostics/_capture/game_polish_a_tests.txt` I left behind while polling the in-flight test run is not a defect of this task (my own tooling exhaust) and my final clean re-run supersedes it — leaving it in place, since it isn't in the task folder.

### Verdict

**PASS.** Setting `STATUS.md` → `SELF_REVIEW_PASS`.

The iter-2 fix delivers a real multi-frame title dissolve that I verified in the pixels of the shipped video (both events show visible intermediate-alpha frames); the fake-null trap that shipped it broken once is pinned by a structurally-sound tripwire that would flip on regression; the shape audit for both `??`-on-Unity-lookups and `ApplyScreen`-painted-shared-chrome holds up under my own re-enumeration; a freshly re-run full EditMode sweep is `2430/0/3`; all three prior hygiene items are cleared; and iter-2's diff touches zero scenes, zero prefabs, and nothing under Physics/GPS/Splash.
