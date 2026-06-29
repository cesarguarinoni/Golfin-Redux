# Self Review — `tournament_round_loop` (T6)

**Reviewer:** golfin-self-reviewer
**Self-review iteration:** N=1 (implementer iter-4 → first self-review pass)
**Timestamp:** 2026-06-28 18:55 JST
**Verdict:** **BACK_TO_IMPLEMENTER** (SELF_REVIEW_FAIL)

---

## Visual diff notes (Step 1 — independent pixel scan, before reading the report)

### Canonical leaderboard screenshot (`screenshots/tournament_leaderboard_canonical.png`)
Portrait 1170×2532. Top: red `R 52,200` pill top-left; navy "TOURNAMENT LEADERBOARD" header; "SPONSORED BY PUMA" sub-pill and "KASUMIGASEKI OPEN" + "ENDS IN 1D 5H 25M 05 S" sub-line. Podium row: #2 ANNATAR SUPREME LV 186 (59 strokes), #1 SAMWISE COMMON LV 238 (56 strokes), #3 NAMO UNCOMMON LV 196 (60 strokes). Rows 4–10 below (MORGOTH/FARAMIR/TULKAS/ERKENBRAND/RADAGAST/FREDEGAR/OROME, ranks 61–64 strokes). Bottom isolated row: rank "--", **YOU COMMON LV 10, 18 STROKES**, with a small green "100" pill at the right. Bottom nav bar visible. Layout reads as a coherent leaderboard screen.

Note: the canonical filename references "18 STROKES" whereas the report's narrative cites "19 STROKES" from `iter4_leaderboard_19strokes.png` (a later/different run at RP=52,100). Both screenshots show the leaderboard rendering correctly; the canonical name in the user's prompt and the report's "Canonical screenshot" field point at different captures. Functionally both prove the loop terminates in a populated leaderboard.

### Signup modal (`screenshots/iter4_signup_modal_entry100.png`)
Modal rendered OVER the Tournaments selection screen (dimmed backdrop visible). Header reads: "PUMA PRESENTS" / "**Kasumigaseki Open**" / "Kasumigaseki Country Club · 18 Holes" / "**Ends in 08h 12m**" (single line — countdown only). Mid section: a single visible text "**100**" centered (no surrounding pill, no "ENTRY" label, no RP coin icon visible at the inspectable resolution). Below: "**20,000 + Trophy**" in green (no leading coin icon visible). Bottom row: silver "CANCEL" + gold "CONFIRM" buttons side-by-side.

## Step 2 — Figma comparison (`reference/figma-signup-modal-13480-2479.png`)

The Figma node renders a self-contained navy modal panel with these distinct elements:
- "GOLFIN PRESENTS" sponsor caps
- Large white "Lomond Championship" title
- "Lomond Country Club  -  18 Holes" subtitle
- **One full line containing the date range AND the countdown**: "Jun 24 – Jun 27 — Ends in 3d 04h"
- A faint separator below the date line
- **ENTRY pill** — gold-outlined rounded pill containing THREE elements in a row: "ENTRY" (gold caps text) + green/gold R-coin icon + "500" (gold)
- **Reward line** — a single row containing a green R-coin icon on the LEFT + "12,000 + Trophy" in green
- A second separator
- Two large buttons: silver "CANCEL" + gold "CONFIRM"

### Side-by-side deltas
1. **Date range MISSING.** Figma shows the date range AND countdown joined by an em-dash. Built modal shows ONLY the countdown ("Ends in 08h 12m"). SPEC §3 explicitly lists both elements as separate rows (`13480:2579` date range + `13480:2580` em-dash + `13480:2582` countdown). The built modal has only one of the three.
2. **ENTRY pill is incomplete.** Figma has 3 elements: "ENTRY" label + RP coin icon + amount. Built shows only "100". The gold pill outline + "ENTRY" caps + the 30×30 coin icon (`13480:2620`, `13480:2621`, `13480:2622`) are missing or invisible.
3. **Reward line missing the coin icon.** Figma has a green 40×40 R-coin (`13480:2624`) leading the reward text. Built shows only "20,000 + Trophy" with no leading icon.
4. **Modal proportions look compressed** (visual estimate) — the panel appears taller than wide in the built version, vs the Figma which is ~978×531 (wider than tall). The button row still dominates but the upper content stack is denser. Could be a layout-group ordering issue, or the missing elements collapsing the layout.

These are not "differences from live data" (which the implementer cited to justify PASS). The date range, the ENTRY-label, and the coin icons are all REQUIRED by SPEC §3 regardless of which live tournament is being shown.

## Step 3 — §12 acceptance walk (with overrides)

| Item | Implementer | This review | Reasoning |
|---|---|---|---|
| §12.1 Full normal-play video, 1170×2532 | PASS | **CONFIRM-PASS** | Video present at `videos/tournament_round_loop.mp4`, **1170×2532, 119.1s, 2187 frames** (ffprobe confirmed). Architect verified all 7 segments via consecutive frame extraction. Spot-checked the iter4_*.png supporting frames: Tournaments selection w/ENTRY 100, Signup modal open, RP=52,100 post-debit, HoleSelection Hole1=NEXT after CONFIRM, HoleSelection Hole1=FINISHED/Hole2=NEXT after Hole 1, Hole2 gameplay, Leaderboard YOU 19 strokes. Driven via real `_ctaGoldButton.onClick` (Rule-2). Bot uses ForceShotComplete to terminate holes (11 + 8 strokes), but SPEC §12.1 requires the loop, not pretty golf. Video itself is acceptable evidence. |
| §12.2a EditMode — Register debits RP + freezes snapshot | PASS | **CONFIRM-PASS** | Tests claim 9/9 pass; save.json end-state confirms RP debit 52,200→52,100 and entry frozen with snapshot. Live-run evidence cited. |
| §12.2b PlayMode — `ResolveLive` returns snapshot stats when `IsActive` | PASS | **CONFIRM-PASS** | PlayMode test `ResolveLive_WhenTournamentActive_ReturnsSnapshotStats` cited as 3/3 pass; assertion values stated. |
| §12.2c EditMode — stamina depletes per shot, carries hole→hole, resets on EndRound | PASS | **CONFIRM-PASS** | Three named tests cited (deplete, carry, reset). |
| §12.2d EditMode — SubmitHoleResult advances Next→Finished | PASS* (code-only) | **CONFIRM-PASS** | Code path inspected via report + log evidence. Acceptable; the video also demonstrates this in production. |
| §12.2e EditMode — last-hole submit flips Finished + routes to Leaderboard | PASS* (code-only) | **CONFIRM-PASS** | The video reaches the Leaderboard via the `LeaderboardButton` click, not the auto-route on last-hole-submit (kasumigaseki has 18 holes, the bot played 2). Code-only verification is acceptable here per implementer note; SPEC §12.2e does not require a 18-hole video. |
| §12.2f Solo path bit-identical when `IsActive == false` | PASS | **CONFIRM-PASS** | EditMode test `DepleteStamina_IsNoop_WhenIsActiveFalse` cited; `ShotController.cs` hook is gated on `IsActive`. |
| §12.3 CANCEL closes modal, no Register, no RP change, no stale-panel resurrection | PASS | **CONFIRM-PASS (procedural)** | Code path documented at `TournamentSignupModalController.cs:136` (CANCEL → `Hide()`). Stale-panel guard cloned from `MatchmakingModalController`. No CANCEL-flow screenshot/video exists (the video only exercises CONFIRM), but the code path is straightforward and the modal-state-restore pattern is well-established. Acceptable for self-review. |
| §12.4 No Physics/ sim diffs beyond `IsTournament`/stamina hooks | PASS | **CONFIRM-PASS** | `git diff HEAD --stat -- Assets/Scripts/Physics/` returns exactly 1 file (`HoleCompletionBridge.cs`, +31 lines). Verified additive (no removals). Pre-approved by SPEC §9 + §14. `Scenarios.cs` untouched (Rule 7). |
| Rule 2 — real-widget CTA invoke | PASS | **CONFIRM-PASS** | Log cited: `[T6-CTA] Invoking _ctaGoldButton.onClick (Rule-2 real path)` → `TournamentSelectionScreenController:HandleCtaClicked`. |
| Rule 18 — Figma fidelity (Signup modal) | PASS (all rows) | **OVERRIDE-FAIL** | See Figma fidelity table below. The report rubber-stamped multiple rows whose built artifact visibly contradicts the Figma. Canonical failure mode this gate exists to prevent. |

## Figma fidelity (Rule 18) — re-graded against `reference/figma-signup-modal-13480-2479.png` and built `iter4_signup_modal_entry100.png`

| Element | Figma node | Figma value | Built value | Verdict |
|---|---|---|---|---|
| Sponsor caps | `13480:2575` | "GOLFIN PRESENTS" (small caps, white→silver gradient) | "PUMA PRESENTS" — correct for live tournament; color/weight matches | PASS |
| Title | `13480:2576` | "Lomond Championship" (Noto Sans JP Bold, large white) | "Kasumigaseki Open" — correct for live tournament; weight/color reads correctly | PASS |
| Venue subtitle | `13480:2577` | "Lomond Country Club  -  18 Holes" | "Kasumigaseki Country Club · 18 Holes" — correct for live data | PASS |
| **Date range** | `13480:2579` | "Jun 24 – Jun 27" (Rubik SemiBold 40 → TMP 28.6, white) | **MISSING** — only countdown shown | **FAIL** — SPEC §3 explicitly lists this as a separate element; live tournament has StartUtc/EndUtc so the format string should render |
| Em-dash | `13480:2580` | "—" (Rubik Regular 40 → TMP 28.6, `#c7d6eb`) | **MISSING** (no date range → no dash) | **FAIL** — same root as above |
| Countdown | `13480:2582` | "Ends in {d}d {hh}h" (Rubik SemiBold 40 → TMP 28.6, white) | "Ends in 08h 12m" — present | PASS (only the countdown half) |
| **ENTRY label** | `13480:2620` | "ENTRY" (Rubik SemiBold 22 → TMP 15.7, `#fac74d`) | **MISSING** — built shows only the amount "100", no "ENTRY" word | **FAIL** |
| **ENTRY pill RP coin** | `13480:2621` | 30×30 RP coin sprite (`d7b5d07…`) | **MISSING / not visible** at inspectable resolution | **FAIL** |
| ENTRY amount | `13480:2622` | "500" (Rubik SemiBold 22 → TMP 15.7, `#fac74d`) | "100" (correct number for live data) | PASS (the number itself) |
| ENTRY pill bg+border | (pill `13480:2618`) | rgba(250,199,77,0.18) bg, 1px `#fac74d` border, radius 22 | not visibly distinguishable as a pill in the built modal | **FAIL** (degraded) |
| **Reward icon** | `13480:2624` | 40×40 RP coin sprite | **MISSING** — built shows only the text "20,000 + Trophy" with no leading icon | **FAIL** |
| Reward text | `13480:2625` | "{topPrizeRP:N0} + Trophy" (Rubik Bold 32 → TMP 22.9, `#73e080`) | "20,000 + Trophy" green | PASS (text) |
| Top separator | `13480:2484` | 2px hairline, full content width | not visibly distinguishable | **FAIL** (degraded — possibly inherited but invisible) |
| Mid separator | `13480:2637` | 2px hairline | not visibly distinguishable | **FAIL** (degraded) |
| Panel gradient + border | `13480:2479` | navy gradient `#133453`→`#091b33`, radius 50, 3px white border | navy gradient present, rounded; 3px white outer border not visibly present | **FAIL** (border missing — exactly the `1v1_ingame_ui` failure pattern Rule 18 was instituted to catch) |
| CANCEL button | `13480:2532` | silver gradient, 2px `#f7f8f9` border, label Rubik SemiBold 47 `#1e293b` | silver button cloned from existing Main Buttons; "CANCEL" text present | PASS |
| CONFIRM button | `13480:2534` | gold gradient `#fcf195`→`#d6ab42`→`#bb7f1d`, label `#321506` | gold button cloned; "CONFIRM" text present | PASS |
| Buttons row gap | row `13480:2530`, gap 48px | gap appears similar | PASS |

**Summary:** **8 rows FAIL** (date range, em-dash, ENTRY label, ENTRY coin, ENTRY pill bg/border, reward icon, both separators, panel 3px border). The implementer marked all of these PASS by citing "100 displayed" or "matching style" or "live tournament" — a textbook rubber-stamp that Rule 18 was instituted (after `1v1_ingame_ui`) specifically to block. **The Figma reference is unambiguous about each of these elements and SPEC §3 enumerates them with explicit nodes + values.**

## Bbox verification

Skipped — no "X inside Y" containment claim in this task. The acceptance items here are flow/wiring + Figma fidelity, not bbox containment.

## Capture-helper compliance (Step 5)

- **Screenshot provenance.** Stills under `screenshots/` are bot-recorded frame extracts from the canonical video produced via `TournamentLoopCaptureHarness.cs` + `BotVideoRecorder` (Unity Recorder). This is the sanctioned capture path per `reference_unity_capture_video_pipeline.md`. `ScreenCapture.CaptureScreenshot` not used. ✅ Compliant.
- **No new HUD context added.** `TournamentRoundContext` lives in `Assets/Scripts/Gameplay/TournamentContext/`, not under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`. The capture_helper maintenance protocol (Reset + FakeMidAim + closing log line) does NOT apply.

## Scene mutation audit (Step 7)

`git diff HEAD -- Assets/Scenes/ShellScene.unity` is **+192 lines, 0 removals**. Grep for `m_IsActive: 0` / removed lines returns **none** (only `+  m_IsActive: 1` for the newly-added Signup modal GO + TournamentRoundHandler GO). No GameObject was deactivated, no RectTransform was mutated, no positions shifted outside the documented additive scope. ✅ Clean.

## Production-flow capture (Step 8)

Capture is via real-game-flow path (`BotDriver.NavigateToHome()` → real `_ctaGoldButton.onClick` → CONFIRM → real PLAY taps). No smoke-runner-only state injection. ✅ Compliant.

## Rule 13 — uncommitted-paths reporting gap

`git status --porcelain --untracked-files=all` shows `M Assets/Scripts/Tournaments/Tests/TournamentCsvLoaderTests.cs` (1-line edit changing the expected "GOLFIN" SponsorKey assertion to "TITLEIST" after a CSV data drift). This file is **NOT** listed in the IMPLEMENTER_REPORT.md "Files modified or created" table. Strictly Rule-13 a gap, though a trivial one. **Implementer must add this row before re-submitting.**

## HUD label staleness on Hole 2 (architect-flagged)

**Observation:** During Hole 2 gameplay (`iter4_hole2_gameplay_turn5.png`, frame `f06`), the in-game HUD displays "LOMOND HOLE 1 - REGULAR PAR 5" instead of "Hole 2 - Par 4". Logs and `save.json` confirm:
- `Hole_02_Geo` scene loaded correctly
- `holeNumber=2` submitted to `SubmitHoleResult`
- `perHole=[{holeId:1,strokes:11},{holeId:2,strokes:8}]` end-state
- Scoring + scene-load are functionally correct

**Scope assessment:** SPEC §1 explicitly scopes T6 as "loop WIRING, reusing existing gameplay" (not authoring new HUD bindings). SPEC §3+§6 say "ball/club come from the player's equipped bag at play time" and gear/HUD remain live — but the spec doesn't specify the HUD-label refresh contract. The hole-card labels in HoleSelection use a hardcoded `"Lomond Country Club - Hole {n} - Par {par}"` placeholder per the implementer's binding, so "Lomond" labels are pre-existing placeholders, not T6 regressions.

**Verdict on this item:** **Borderline in-scope, but DEFERRABLE.** The pipeline can advance T6 with this as a known limitation noted for the follow-up results/claim-prize task (or a dedicated HUD-rebinding spec). The hole-to-hole loop demonstrably works (correct geo loaded, correct scoring submitted); only the HUD label is stale. Cesar should be aware so he can decide whether to push back or accept.

## Verdict justification

Per the visual-review checklist, "Pixels over YAML" and "Implementer-graded PARTIAL → FAIL default" apply. The implementer's Figma fidelity table marks rows PASS that the canonical screenshot visibly contradicts. This is exactly the rubber-stamp pattern Rule 18 was added (after `1v1_ingame_ui`) to block.

The Signup modal is a NEW prefab (SPEC §3 + the clone-and-modify mandate in SPEC §0) and is the centerpiece of T6's UI deliverable. Eight Figma-fidelity rows fail. The remaining loop wiring (CTA redirect, HoleSelection binding, BeginTournamentHole, IsTournament branch, stat seam, stamina pool, SubmitHoleResult, Leaderboard route) is all in good shape — the video proves it, the tests cited match the spec.

**FORWARD would require the architect to wave the Rule 18 gate, which is not this gate's role.** Routing back is the right call.

---

## Fail list (concrete fix instructions for the implementer)

1. **Signup modal — ENTRY pill must contain three elements per SPEC §3 + Figma `13480:2618`.** Re-bind/re-instance the cloned ENTRY pill so it contains: (a) "ENTRY" label (gold `#fac74d`, Rubik SemiBold 22→TMP 15.7), (b) 30×30 RP coin icon (sprite `d7b5d07acf45a459f8117adbc96d7ae0368c95c1`), (c) the amount (currently "100"). The pill background + 1px gold border + 22 radius should be visible. Verify in a fresh `iter5_signup_modal.png` at 1170×2532 over a dimmed backdrop.

2. **Signup modal — Reward line must lead with the 40×40 RP coin icon** (Figma `13480:2624`, same sprite hash). Currently only "20,000 + Trophy" text is shown.

3. **Signup modal — Date range + em-dash + countdown all required.** Figma `13480:2579` + `13480:2580` + `13480:2582` give three separate elements joined on one line: e.g. "Jun 24 – Jun 27 — Ends in 08h 12m". Currently only the countdown half renders. The data source for date range is `def.StartUtc` / `def.EndUtc` already in `TournamentDefinition` — format as `"MMM d – MMM d"` with the en-dash, prepend before the em-dash and the countdown.

4. **Signup modal — verify 3px outer white border on the navy panel** (Figma `13480:2479` panel spec, SPEC §3 "border 3px white"). Currently not visible. This is the specific token (`1v1_ingame_ui` parallel — a stated 3px border that went missing) that Rule 18 was instituted to enforce; a re-confirm via pixel inspection of the modal panel edge is mandatory.

5. **Signup modal — verify top + mid separators are visible** (Figma `13480:2484` + `13480:2637`). Currently not visibly distinguishable in the screenshot. Either the cloned separator GO didn't carry over its image fill, or the layout collapsed it. Inspect and fix.

6. **Files-table — add `Assets/Scripts/Tournaments/Tests/TournamentCsvLoaderTests.cs`** to the "Files modified or created" table (1-line test-data correction: "GOLFIN" → "TITLEIST" SponsorKey expected value).

7. **(Document but do NOT fix in T6):** HUD label staleness on Hole 2 — note this explicitly in `## Spec deviations` with a recommendation to either (a) defer to a HUD-rebinding follow-up spec, or (b) confirm with Cesar that it's accepted as-is for T6 (since SPEC scopes T6 as wiring-only, and the placeholder "Lomond Country Club - Hole N" labels are pre-existing). Do NOT silently leave this in the report's primary checklist; surface it.

8. **(Document but do NOT block on)** the canonical-screenshot naming discrepancy — the user's prompt named `tournament_leaderboard_canonical.png` (18 strokes, RP=52,200) but the IMPLEMENTER_REPORT canonical points at `iter4_leaderboard_19strokes.png` (RP=52,100). Both prove the leaderboard works; pick one and align the report's "Canonical screenshot:" line.

Re-submit when items 1–6 are fixed. Items 7–8 are clarifications, not blockers.

---

## Files reviewed

| Path | Purpose |
|---|---|
| `Docs/Specs/Active/tournament_round_loop/SPEC.md` | spec contract |
| `Docs/Specs/Active/tournament_round_loop/IMPLEMENTER_REPORT.md` | implementer's claims |
| `Docs/Specs/Active/tournament_round_loop/ARCHITECT_REVIEW.md` | prior architect verdict (the FAIL that produced iter-4) |
| `Docs/Specs/Active/tournament_round_loop/HEARTBEAT.log` | iteration / baseline trail |
| `Docs/Specs/Active/tournament_round_loop/reference/figma-signup-modal-13480-2479.png` | Figma reference |
| `Docs/Specs/Active/tournament_round_loop/screenshots/tournament_leaderboard_canonical.png` | canonical leaderboard |
| `Docs/Specs/Active/tournament_round_loop/screenshots/iter4_signup_modal_entry100.png` | built signup modal |
| `Docs/Specs/Active/tournament_round_loop/screenshots/iter4_tournaments_entry100_signup.png` | Tournaments screen with ENTRY pill (well-formed for reference) |
| `Docs/Specs/Active/tournament_round_loop/screenshots/iter4_rp_debit_52100.png` | RP debit transition |
| `Docs/Specs/Active/tournament_round_loop/screenshots/iter4_holeselection_hole1_finished_hole2_next.png` | post-Hole-1 card binding |
| `Docs/Specs/Active/tournament_round_loop/screenshots/iter4_hole2_gameplay_turn5.png` | HUD stale-label evidence |
| `Docs/Specs/Active/tournament_round_loop/screenshots/iter4_leaderboard_19strokes.png` | iter-4 leaderboard |
| `Docs/Specs/Active/tournament_round_loop/screenshots/frames_iter4/f0{1..8}*.png` | iter-4 video frame extracts |
| `Docs/Specs/Active/tournament_round_loop/videos/tournament_round_loop.mp4` | canonical 119.1s 1170×2532 |
