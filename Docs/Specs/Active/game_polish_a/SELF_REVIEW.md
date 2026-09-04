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
