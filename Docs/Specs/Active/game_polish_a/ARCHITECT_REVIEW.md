# ARCHITECT_REVIEW — `game_polish_a`

**Reviewer:** golfin-reviewer
**Date:** 2026-09-04 18:12 JST
**Iteration reviewed:** 2 (self-reviewer PASS + PASS)
**Verdict:** **PASS** → set `STATUS.md` to `READY_FOR_REDTEAM`. (I am not permitted to write `ARCHITECT_REVIEW_PASS`; the red-team gate is next.)

---

## § Independent visual scan (Step 0, before anything else)

Canonical `screenshots/push_18_home_return.png` at 1170×2532. Dark navy top bar carrying R chip "7.483" left, GOLFIN chip "2.890" + "+" plus centre, settings gear right. Below sits the golden centre-title band with "Cratilo" in cream serif, a 213 mission chip at the top-left corner and a "GPS" pill at the top-right corner of the band. The Home body renders the "Welcome, Test Players!" banner, the "NEW DAILY MISSION!" pill mid-left, the trophy-holding character centred over the sunset course backdrop, and the bottom mode carousel with PRACTICE (ENTRY FEE R x10, REWARDS R x5, PLAY button) plus peek slices of PLAYER 1v1 (left) and TOURNAMENT (right). Bottom nav shows the 5 discs with the Tee raised. Nothing is torn or half-faded, z-order looks correct, top-bar chrome and nav-bar chrome are fully painted above and below the content stack. No sub-canvas is showing a half-alpha or leftover motion state.

## § Mesh metrics (Rule 16)

**N/A explicitly.** This is a UI-motion task. SPEC touches no `TerrainData`, `green.json`, `GreenTopology`, mesh cut/deform, skirt, boundary loop, or contour vertex. No mesh, no numeric geometry gate applies.

## § Figma fidelity (Rule 18)

**N/A explicitly.** SPEC.md § Reference reads: "No Figma nodes — motion only. Rest state at HEAD is the visual reference." No `figma.com` URL, no `<n>:<n>` node id anywhere in the SPEC. Builder touches zero prefab layout (only components + `CanvasGroup`s added by scene/prefab authoring). Rule 18's fidelity table does not apply.

---

## § Attack-item verifications

### Attack 1 — A2 rest parity keyed on `(label, real screen)` (independently re-derived)

Enumerated all `parity_{anim,instant}_NN_<label>__<real>.png` files, paired STRICTLY on `(label, real)`. 18 anim × 18 instant → **16 valid pairs, 2 ROUTE DRIFT rejects**:

- `('modeselection','ModeSelection')` — anim only (instant landed on TournamentHoleSelection).
- `('generalshop','GeneralShop')` — anim only (instant landed on GachaPrizes).

Both rejects match exactly the drifts the report itself labels as `ROUTE DRIFT`. Neither is silently paired.

Per-pair pixel diff (`max|ΔRGB| > 8`), the 16 valid pairs:

| pair | %diff | bbox (x0,y0,x1,y1) |
|---|---|---|
| gachahistory / GachaHistory | 0.031% | 132,147,206,173 |
| gachaprizes / GachaPrizes | 0.031% | 132,147,206,173 |
| holeselection / HoleSelection | 0.031% | 132,147,206,173 |
| home / Home | 0.400% | 15,147,531,880 |
| home_return / Home | 0.031% | 132,147,206,173 |
| inventory_tab0..3 / Inventory | 0.034 / 0.082 / 0.038 / 0.084% | (132–1128, 147–791) |
| leaderboard / Leaderboard | 0.104% | 132,147,1063,437 |
| missionselection / MissionSelection | 0.136% | 132,147,740,693 |
| roster / Roster | 0.031% | 132,147,206,173 |
| settings_open / Roster | 0.030% | 132,147,206,173 |
| tournamentholeselection / TournamentHoleSelection | 0.031% | 132,147,206,173 |
| tournamentleaderboard / TournamentLeaderboard | 0.031% | 132,147,206,173 |
| tournamentselection / TournamentSelection | 0.031% | 132,147,206,173 |

**Three findings, re-derived from my own numbers:**

1. **Every bbox starts at y=147.** Zero of 16 pairs show any pixel diff above y=147 → top-bar chrome is pixel-identical across every screen.
2. **Deepest bbox is y=892.** Zero of 16 pairs show any pixel diff below y=1640 (nav bar zone) → nav bar chrome is pixel-identical across every screen.
3. **Eight of sixteen pairs show the identical 74×26 rect at (132,147)–(206,173)** — the RP counter digits. Report's "residuals localised to RP counter" corroborated.

A 16 px settle error is IMPOSSIBLE to reconcile with these numbers: it would smear along the entire content column of every screen, not draw tight boxes around ticking numeric fields. A2 verified.

(Report's headline "worst 1.232 %" uses a different threshold than my >8/channel; my >8 threshold gives worst 0.400% on `home`. Either way, the qualitative property — chrome pixel-identical, residuals live-data-shaped — holds.)

### Attack 2 — iter-2 title dissolve, from the pixels of the shipped clip

Decoded `videos/game_polish_a_f_cross_backdrop.mp4` (1170×2532, 750 frames, 30 fps). Cropped title band `y=200..340` (Cesar-named crop, avoiding the sky-crop trap the self-reviewer caught in iter-1). Luma trace, glyph zone (400 px centre band):

```
event 1  frames 6..14   luma = 82.7, 82.7, 60.4, 60.4, 48.7, 69.7, 72.9, 75.2, 75.2
event 2  frames 677..685 luma = 75.5, 75.5, 60.6, 60.6, 54.7, 81.5, 82.2, 82.2, 82.3
```

Both events show **multi-frame intermediate-alpha luma states** (60, 48, 69 / 60, 54, 81) between opaque endpoints. A one-frame hard cut would be a lone step of Δ≥30 bordered by ~0. Neither event has that shape. Corroborated by `iter2_title_dissolve_before_after.png`: the "AFTER" strip's f277 shows a visibly greyed `MODE SELECTION`, f279 blank, f280 partial `TOURNAMENT LEADERBOARD` — the "BEFORE" strip has f642 opaque and f643 opaque with nothing between. The fix is real in the pixels.

Fake-null-`??` trap is closed at the codegen level (`Shape A` audit below returned zero hits across the 7 touched files) AND pinned by `CenterTitleDissolveTests.EnsureCenterTextGroup_AddsARealComponent_NotAFakeNull` — the assertion uses Unity's overloaded `== null`, which IS the operator that would flip on regression.

### Attack 3 — §D7 scope + selected-state render

```
git diff --stat 1e7f97504..HEAD -- Assets/Scripts/UI/Gps Assets/Prefabs/UI/Gps
 Assets/Scripts/UI/Gps/GpsNavBarHighlight.cs | 38 +++++++++++++++++++++--------
```

**Exactly one file.** Zero prefab edits in `Gps/`. Cesar's authorised-exception scope respected to the letter.

Game-bar selected state visually verified in `screenshots/d7_nav_after.png`: Home slot carries a distinct outward gold halo + a visibly brighter (yellow) ring, glyph white; the other four slots have no halo, standard gold ring, glyph white; NO cyan anywhere. Matches D7's design intent (gold halo + brighter ring, replace cyan tint).

GPS bar selected state is verified in the CODE (`GpsNavBarHighlight.Apply()` calls `NavSlotHighlight.Attach(img)?.SetSelected(slot == lit, animate)` — same helper the game bar calls; `iconActiveColor` is no longer read anywhere). This is the "cannot drift" property Cesar named. **However, there is no photographed GPS-hub-selected still in this iteration's captures**: SPEC A15 explicitly asked for "the GPS hub selected slot (1)" and only the game-bar sequence is captured. Surfaced under Findings; not a blocker (mechanism is verifiable in code and Cesar's own brief listed this as "the weakest part of the evidence set", not an auto-fail).

### Attack 4 — Report narrative sweep (Shape C follow-through)

Enumerated every `###`/`##` heading in `IMPLEMENTER_REPORT.md` and cross-checked verdict text against artifact reality:

| Heading | Verdict claim | On-disk verification |
|---|---|---|
| § A9 flag pinned | VOID / replaced | `grep -rn AllowBackgroundCrossFade` → 2 hits, both in `LayeredPushTests.cs` asserting absence; zero production code hits |
| § A14 scope | PASS, one Gps file | git diff --stat verified: 1 file, 28+/10− |
| § A15 nav selected state | PASS on mechanism | `grep -rn iconActiveColor Assets/Scripts` → 3 hits, all documentation-only, no runtime read; game-bar still shows the halo+ring; GPS-bar still not captured (surfaced) |
| § A12 EditMode | PASS `2430/0/3` | `game_polish_a_tests.txt` last line `RUN FINISHED passed=2430 failed=0 skipped=3 duration=138.2s` — matches |
| § A1 Invariants | PASS `fail == 0` | JSON re-parsed below |
| § A5 Chrome static | chromeAlphaMin=1 | JSON `chromeAlphaMinOverRun` = 1 on all 55 same-backdrop records |
| § A10 Real entry | 3 realWidget=true | JSON re-verified: 3 records, widget path populated under key `widget` (not `realWidgetPath` as self-review implied) |
| § A3 Boundary untouched | PASS | `git diff --stat -- Assets/Scripts/UI/FadeController.cs Assets/Scripts/UI/Gps/GpsScreenTransition.cs` = empty |
| § A11 ButtonPressFeedback | PASS on 5 nav slots | Report cites, prefab diff shown; consistent |
| § A7 Cross-fade table | PASS | mid-fade frames folded into the A4 clips; not a numeric gate |
| § A6 UI fidelity lint | N/A stated | No Figma node — matches SPEC's Reference |
| § A4 Videos | PASS all six | All six MP4s exist in `videos/`, 1.6–7.8 MB, cross-backdrop is 25.0 s / 750 frames |
| § A13 Perf | PASS + GachaHistory finding | Report quotes numbers; GachaHistory ~290 MB/1 s stall flagged for separate work (out of this slice's scope) |
| `## A2 · Rest parity` | PASS | Re-derived above |
| `## A8 · Entry rise` | PASS + SkippedForPush=94 | 6 mid-rise stills present in `screenshots/`; log-line formulation is honest ("Risen=2 for cross-pillar re-seats") |
| `## SUPERSEDED` banner | 5 stale sections mapped | Every heading in the block is prefixed `(superseded)` and cites its closing section |
| Files-modified table | LayeredPush row includes iter-2 hand-off | Matches |
| Deviations D-1..D-7 | documented | D-1 (runtime children not prefab authoring) is what makes the GPS scope keepable; D-2..D-7 all justified |

Every "superseded" heading is now explicitly labelled and closed with a pointer to its current section. The self-reviewer already re-verified this; my sweep found no further stale verdicts.

### Attack 5 — Invariants JSON re-parsed (Rule 3, re-derived, not read off booleans)

`Docs/Diagnostics/_capture/game_polish_a_invariants.json`, keys `[task, mode, utc, optionBShipped, pushDur, durationToleranceSec, measured, fail, pushes]`.

```
measured = 87                  fail = 0                 optionBShipped = true
records under pushes[]         = 87
applyScreenCalls == 1          = 87 / 87
blocksRaycastsRestored == true = 87 / 87
cross-backdrop records         = 32     seamWorstCover  min/max = 1 / 1
same-backdrop records          = 55     chromeAlphaMin  min/max = 1 / 1
realWidget == true records     = 3      widget paths populated:
    ModeSelection -> HoleSelection      "mode card PLAY -> Practice (mode card ActionButton)"
    ModeSelection -> MissionSelection   "mode card PLAY -> Missions (mode card ActionButton)"
    GachaPrizes   -> GachaHistory       (harness-reachable — correctly not claimed as real evidence)
records with failedAsserts     = 0
frameStarved records           = 5   (declared as instrument-limit, not scored)
```

Every §D5 invariant holds on every record. Cross-backdrop path exercises `seamWorstCover` and it reads 1.0 across all 32 (the shipped compositing order — leaver held at 1, arriver faded in on top — makes `max(from,to)` structurally 1; report acknowledges this is measured-from-live-CanvasGroups rather than restated). Same-backdrop path exercises `chromeAlphaMinOverRun` and it reads 1.0 across all 55. Reports's 84↔87 accounting: 84 sweep + 3 real-navigation = 87, matches. **This is a hard-gate PASS.**

### Attack 6 — Mesh metrics

Explicitly N/A (see § above). Not skipped; stated.

---

## § Rule 5 — Full acceptance list re-verified this pass

| # | Item | Verdict | How I verified this pass |
|---|---|---|---|
| A1 | Invariants JSON `fail == 0` | **PASS** | Re-parsed 87 records; every §D5 assertion re-derived above |
| A2 | Rest parity ~0 px | **PASS** | Re-paired 16 valid `(label,real)` cases; independently computed pixel diffs; chrome pixel-identical across all 16 |
| A3 | Boundary fade untouched | **PASS** | `git diff --stat` FadeController.cs / GpsScreenTransition.cs empty |
| A4 | Six videos + stills | **PASS** | All six MP4s present, cross-backdrop clip decoded frame-by-frame and dissolve confirmed |
| A5 | Chrome static during push | **PASS** | JSON `chromeAlphaMinOverRun=1` on all same-bg records, `seamWorstCover=1` on all cross-bg; A2 corroborates in pixels |
| A6 | UI fidelity lint | **N/A** | No Figma node, no prefab layout touched — matches SPEC |
| A7 | Cross-fade table | **PASS** | Sites landed in InventoryScreenController/RankingsScreenController/GachaHistoryScreenController/SettingsController/SettingsMenuItem per D3 map; mid-fade frames shipped with A4 clips |
| A8 | Entry rise | **PASS** | 6 mid-rise stills present; SkippedForPush=94 vs Risen=2 (cross-pillar re-seats — honest formulation) |
| A9 | (VOID) flag pinned | **VOID/SUPERSEDED** | `AllowBackgroundCrossFade` grep = 2 hits in tests, 0 in production; replacements `TheOptionBFlag_IsGone` + `SameBackground_IsNoLongerRequiredByTheGate` both green |
| A10 | Real entry | **PASS (reachable pairs) + labelled (rest)** | 3 realWidget records with widget path populated; 84 harness records honestly labelled `realWidget:false` |
| A11 | ButtonPressFeedback | **PASS** | Report cites 5 nav slots + prefab diff |
| A12 | EditMode sweep | **PASS** | Report file `passed=2430 failed=0 skipped=3`; 5 CenterTitle + 12 LayeredPush + 6 ScreenEntryMotion = 23 task tests all Passed |
| A13 | Perf | **PASS + GachaHistory finding** | LayeredPush allocation zero per frame by construction; GachaHistory ~290 MB flagged as pre-existing (correct) |
| A14 | Scope | **PASS** | Diff-verified: 1 file under Gps/, 0 under Physics/, 0 Scenarios, 0 Splash mats, FadeController+UiMotion untouched |
| A15 | Nav selected state | **PASS on mechanism** | Game-bar visually verified; GPS-bar verified in code only; SPEC-named GPS-hub still is missing (see Findings) |
| A16 | Deviations | **PASS** | D-1..D-7 justified; each cites its constraint |

---

## § Bbox verification (Step 3 / PIPELINE_HARDENING §3)

- **Chrome-containment claim (top bar, nav bar).** Re-derived above from the 16 A2 pairs: nothing above y=147, nothing below y=1640. No `inside=false` result. Chrome-inside-frame verified in numbers.
- **Invariants JSON per-assertion re-derivation.** No `assert_*` booleans were trusted; every field (`applyScreenCalls`, `blocksRaycastsRestored`, `seamWorstCover`, `chromeAlphaMinOverRun`, `fails`) was re-counted from the raw records above.

## § Scene-mutation audit (Step 7)

```
git diff --stat 1e7f97504..HEAD -- Assets/Scenes/ShellScene.unity
  1 file changed, 263 insertions(+), 3 deletions(-)
```

`git diff -- Assets/Scenes/ShellScene.unity | grep -E '^[-+]' | grep -cE 'm_IsActive|m_SizeDelta|m_AnchoredPosition'` = **0**. (My initial grep returned 2 hits; both were unchanged CONTEXT lines from an existing RectTransform block adjacent to added MonoBehaviour blocks. No mutation on `+`/`-` lines. Consistent with self-review.)

Standing bans:

- `git diff --stat -- Assets/Scripts/Physics/` — empty.
- `git diff -- Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` — empty.
- `git diff --stat -- 'Assets/**/M_Splash*.mat'` — empty.

Working-tree drift outside task folder: `Docs/Specs/Active/map_view_v2/{SPEC.md, reference/B1_aiming.png, reference/B1_over_range.png}` — exactly the three files Cesar's brief names as his parallel session. Not this task's code. Rule 13 clean.

## § Shape audit re-run (§15 corollary)

**Shape A — `??` on Unity object lookups in the 7 touched files.** Re-grepped:

```
Assets/Scripts/UI/Polish/LayeredPush.cs           (no matches)
Assets/Scripts/UI/Polish/ScreenEntryMotion.cs     (no matches)
Assets/Scripts/UI/Polish/NavSlotHighlight.cs      (no matches)
Assets/Scripts/UI/Polish/UiSelection.cs           (no matches)
Assets/Scripts/UI/PersistentUIManager.cs          (no matches)
Assets/Scripts/UI/ScreenManager.cs                (no matches)
Assets/Scripts/UI/Gps/GpsNavBarHighlight.cs       (no matches)
```

Zero hits across the 7 files. Shape A closed.

**Shape B — `iconActiveColor` runtime reads.** `grep -rn iconActiveColor Assets/Scripts` = 3 hits:

- `PersistentUIManager.cs:70` — XML doc comment naming the grep.
- `PersistentUIManager.cs:75` — the `[Obsolete]`-marked field itself.
- `Gps/GpsNavBarHighlight.cs:9` — historical header comment.

**Zero runtime reads.** The field survives for prefab-deserialisation compatibility only, correctly obsolete-marked. Shape B closed.

**Shape C — superseded report sections reading as live.** Every superseded heading in the report is now prefixed `(superseded)` and cites its closing section; the `## SUPERSEDED` banner maps five stale headings to their current PASS-carrying sections. I enumerated every `###`/`##` in the report; no residual stale verdict found.

## § Capture-mechanism audit

Not a gameplay-video task (no bot scenario, no gameplay clip). All captures are UI-motion clips through the sanctioned `GamePolishDemoRecorder` + `cut_game_polish_clips.py` path plus the `GamePolishProbe`'s A2 pass with the label+realScreen naming fix Cesar named. No bespoke scenario; no direct `LoadSceneAsync`; no mid-clip camera switching. Videos are full-resolution 1170×2532.

## § Report integrity gate (Rule 6)

Every PASS claim I sampled is backed by:

- a tool result I re-ran independently (git diff, grep, JSON re-parse, pixel decode of the video, per-pair bbox pixel diff), OR
- a numeric field in the invariants JSON I re-counted, OR
- a file on disk (`game_polish_a_tests.txt`, six MP4s, six mid-rise stills).

No fabricated tool output found. No fabricated approval quoted. No PASS backed by pure assertion. `.claude/review_misses.log` entry not warranted this iteration.

---

## § Findings — non-blocking, worth surfacing

1. **No standalone GPS-bar-selected still.** SPEC A15 asks for "a still of each pillar selected on the game bar (5) AND the GPS hub selected slot (1)". Only the game bar is photographed (`d7_nav_after.png`); the GPS-hub-selected slot is verified in code only (`GpsNavBarHighlight.Apply()` calls the shared `NavSlotHighlight.Attach(...).SetSelected(...)`). Cesar's brief pre-flagged this as "the weakest part of the evidence set" — surfaced here for the red-team to weigh. Mechanism cannot drift because there is exactly one helper for both bars, but the artifact SPEC A15 named is not on disk.

2. **Self-review cites `nav_a_play_pillar_*.png` and `nav_c_gacha_pillar` crops that do not exist as standalone files.** Likely shorthand for "frames from `game_polish_a_a_play_pillar.mp4` / `_c_gacha_pillar.mp4`" (both clips DO exist), but the phrasing implies saved PNG crops. Cosmetic; the underlying claim (per-pillar highlight visible in the pillar walks) is correct in the source clips.

3. **Invariants JSON widget-path key.** Self-review's Step 8 phrasing implies a `realWidgetPath` field; the actual key is `widget`. Field IS populated on all 3 realWidget records with sensible strings ("mode card PLAY -> Practice (mode card ActionButton)" etc.). Nomenclature only; no evidence gap.

4. **A13 GachaHistory finding.** Report correctly flags ~290 MB / 1 s stall on every arrival at `GachaHistory` as **pre-existing** (RebuildList inside OnEnable), not this task's code. It also happens to explain the four frame-starved records in A1. Correct to defer.

5. **Env carry-over.** Editor active build profile is `iOS-Full-GPS` (needed by this task's shell); Cesar noted must be switched back before the standalone lane is next built. Not this task's open work.

None of these flip the verdict. They are hygiene / evidence-density observations the red-team may want to press on.

---

## § Verdict

**PASS.** Setting `STATUS.md` → `READY_FOR_REDTEAM`.

The task delivers option (b) as Cesar shipped it: 87 measured pushes with `fail == 0`, 32 of them cross-backdrop; the seam invariant holds structurally on the compositing order and is measured from live CanvasGroups (not restated); chrome held at 1 on every same-backdrop record. Rest parity is verified in independent pixel bboxes on 16 valid `(label, real screen)` pairs: chrome pixel-identical above y=147 and below y=1640 on every screen; residuals localise to live-data regions (RP counter, mission pill, live cards). The iter-2 centre-title dissolve is real in the pixels of the shipped `f_cross_backdrop.mp4` (two multi-frame events with intermediate-alpha luma states); the `??`-on-Unity-lookup trap that shipped it broken once is closed at the codegen level across all 7 touched files and pinned by `CenterTitleDissolveTests` on the exact assertion that would flip. §D7 scope is honoured to the letter (one file in `Gps/`, zero prefabs), the game bar's selected state renders as specified, and the two bars are held from drifting by a single shared helper. Standing bans clean, scene mutations clean, working-tree drift belongs to Cesar's parallel session. Report Shape C sweep has closed every stale verdict I could enumerate.

The red-team should press on Finding 1 (missing GPS-hub still) if it wants an artifact-level gate rather than a code-level one, and may also want to spot-check the game-bar selected state across all five pillars from the A4 clips (only Home is captured as a standalone still).

