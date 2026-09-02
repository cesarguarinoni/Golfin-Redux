# IMPLEMENTER REPORT — `gps_polish`

**Iteration shape:** `gps-motion:paint-state-polish`
**Iteration:** 2 (continuation — `KICKOFF_ADDENDUM.md` R1–R9)
**HEAD at kickoff:** `1cc4fe6e1` (iter-2 baseline block in `HEARTBEAT.log`)
**Canonical screenshot:** `screenshots/shimmer_01_hub_rounds.png` (1170×2532)
**Canonical video:** `videos/gps_polish_b_nav_sweep_cold.mp4` (7.6 MB, 1170×2532, 45.7 s)

> Iteration 1's layered push is unchanged and still green. This iteration is the remainder the
> addendum lists. It also **corrects one thing iteration 1 got wrong** (§3 D-8) and **closes one
> real product defect its own placeholder exposed** (§2 A8).

---

## 1 · What the continuation built

| # | addendum item | state |
|---|---|---|
| R1 | D3 staggers on fetch-paint, never cache-paint | **DONE** — 5 sites, one gate each, one log line per paint |
| R2 | D4 gift panel fades + vote filter cross-fade | **DONE** |
| R3 | D6 selection bumps (two-Image alpha, no tinting) | **DONE** — 4 sites |
| R4 | D7 count-ups + badge pulse + vote-bar fill | **DONE** — 6 sites |
| R5 | D8 `ShimmerBlock` at the five cold-fetch sites | **DONE** — and it found a real defect |
| R6 | D9 keyboard offset | **DONE** in code + EditMode; **needs the device pass to be SEEN** |
| R7 | A4 videos (b re-record)(c)(d′)(e)(f) | **DONE** — 5 recorded, all captioned |
| R8 | A7 pending `…` frame | **DONE** |
| R9 | A13 profiler measurement | **DONE** — and the instrument had to be moved out of A1's way |

---

## 2 · Acceptance checklist

### A1 · Motion invariants JSON — **PASS, `fail = 0`**

`gps_polish_invariants.json` (also `Docs/Diagnostics/_capture/`), generated `2026-09-02 12:50:42Z`,
written by `GpsPolishProbe` driving **real widget `onClick`** through boot → Home → the Home GPS
pill → the hub's own nav slots and the profile's own shortcut buttons.

```
"transitions": 10,  "fail": 0
```

| from → to | dir | frames | measured | t0 offset | seamWorstCover |
|---|---|---|---|---|---|
| GpsHub → GpsProfile | Forward | 6 | 0.265 s | +1170 | 1.000 |
| GpsProfile → GpsBadges | Forward | 6 | 0.2575 s | +1170 | 1.000 |
| GpsBadges → GpsProfile | Back | 7 | 0.2566 s | −1170 | 1.000 |
| GpsProfile → GpsAvatar | Forward | 15 | 0.2632 s | +1170 | 1.000 |
| GpsAvatar → GpsProfile | Back | 7 | 0.2597 s | −1170 | 1.000 |
| GpsProfile → GpsHub | Back | 16 | 0.2664 s | −1170 | 1.000 |
| GpsHub → GpsGift | Forward | 16 | 0.2667 s | +1170 | 1.000 |
| GpsGift → GpsHub | Back | 7 | 0.2578 s | −1170 | 1.000 |
| GpsHub → GpsVote | Forward | 14 | 0.2584 s | +1170 | 1.000 |
| GpsVote → GpsHub | Back | 7 | 0.2527 s | −1170 | 1.000 |

**A gate that was broken by its own new instrument, and how.** The first attempt at A13 sampled the
profiler counters inside this same run. Turning the Editor profiler on cost one frame of the
GpsVote→GpsHub push **392 ms**, which stretched a 0.25 s tween to **0.410 s** and failed A1's own
duration assertion — `fail=1`, on a transition that was fine. The measurement changed the thing it
was measuring. A13 now runs as a separate pass (see A13), and this one runs with the profiler off.

### A2 · Rest-state parity — **PASS, 0 differing px on all 7 screens**

Iteration 1 compared runs taken far apart, which does not work on this surface and it is worth
recording why: these screens render **live data and RELATIVE time**. A capture taken now against
one taken an hour ago diffs "2h ago" against "3h ago", a moved RP balance and a ticking clock in
the shared top bar — 315,812 differing pixels that have nothing to do with the animation.

So A2 is now a **within-one-run paired capture** (`GpsPolishProbe --mode parity`): the route is
walked twice in a single play session, once with `UiMotion.Enabled = true` (every GPS move is a
push) and once with it false (every move falls through to the untouched boundary fade, which is
the `instant` arrival the SPEC asks to compare against). Forty seconds apart, nothing else moves.

| screen | size | differing px | max ǀΔRGBǀ |
|---|---|---|---|
| hub | 1170×2532 | **0** | 0 |
| profile | 1170×2532 | **0** | 0 |
| badges | 1170×2532 | **0** | 0 |
| avatar | 1170×2532 | **0** | 0 |
| gift | 1170×2532 | **0** | 0 |
| vote | 1170×2532 | **0** | 0 |
| scoreupload | 1170×2532 | **0** | 0 |

Pairs: `screenshots/parity_anim_NN_*.png` vs `screenshots/parity_instant_NN_*.png`.

**The badges pair was the last thing to reach 0**, and it is the defect in A8: before the fix it
differed by 5,082 px because the placeholder was still on screen over painted cells.

**The five shimmer hosts cannot move a rest pixel, and that is checked rather than assumed** — each
is saved INACTIVE, so no block can render at rest:

```
GpsHubScreen     site=hub.rounds      hostActiveSelf=False activeInHierarchy=False blocks=3
GpsBadgesScreen  site=badges.grid     hostActiveSelf=False activeInHierarchy=False blocks=6
GpsGiftScreen    site=gift.supporters hostActiveSelf=False activeInHierarchy=False blocks=3
GpsGiftScreen    site=gift.golfers    hostActiveSelf=False activeInHierarchy=False blocks=3
GpsVoteScreen    site=vote.list       hostActiveSelf=False activeInHierarchy=False blocks=2
```

### A3 · Boundary untouched — **PASS**

```
$ git diff --stat 1cc4fe6e1..HEAD -- Assets/Scripts/UI/FadeController.cs
$ git diff --stat            -- Assets/Scripts/UI/FadeController.cs
(no output from either — FadeController is byte-identical)
```

### A4 · Videos — **PASS, 6 of 6**

All 1170×2532, drawtext-captioned via `build_bot_video.py --mode captionsjson`, recorded by
`GpsFlowDemoRecorder` (Unity Recorder over the Game View), every forward step a real `onClick`.

| clip | file | size | length | what it is evidence for |
|---|---|---|---|---|
| (a)+(d) | `gps_polish_a_push_walkthrough.mp4` | 12.8 MB | 66.2 s | the push + all three modals (iter-1, unchanged) |
| (b) | `gps_polish_b_nav_sweep_cold.mp4` | 7.6 MB | 45.7 s | **re-recorded**: nav sweep on a COLD session — shimmer, then stagger — then the same screens WARM |
| (c) | `gps_polish_c_score_upload_steps.mp4` | 6.4 MB | 43.4 s | the step cross-fade, the sliding indicator, POST pending, the Posted total popping |
| (d′) | `gps_polish_d2_panel_fades.mp4` | 6.5 MB | 35.7 s | gift panel fades, amount-pill bumps, PUBLIC↔MINE list cross-fade |
| (e) | `gps_polish_e_golfprofile_welcome_hub.mp4` | 3.7 MB | 26.9 s | swatch + chip selection, Golf Profile → Welcome → hub |
| (f) | `gps_polish_f_live_cast.mp4` | 3.4 MB | 33.8 s | a live cast: bar fill old→new, top-bar RP count-up |

Stills in `screenshots/`: `video_b_still_*` (4), `video_c_still_*` (4 — one of them renamed, see
A7), `video_d2_still_*` (3), `video_e_still_*` (3), `pending_ellipsis_vote_button.png`,
`shimmer_01..04_*`.

**Two takes were thrown away rather than shipped, and the reason is the same both times: the
recorder could press a button the player cannot.** `TapIn`/`TapFirstIn` called
`onClick.Invoke()` unconditionally, so the first (c) walked straight past VERIFY GPS while it was
disabled for an EMPTY scorecard and reached CONFIRM with every figure showing an em dash; the
server refused the post, under a caption promising "POSTED". Taps now go through `Press()`, which
refuses a control that is not `activeInHierarchy && interactable` and says so in the log. The (c)
walk then stalled honestly at GPS PROOF — the Editor has no location fix — so it now takes the
door a player without a fix takes: CHOOSE MANUALLY → pick a venue → CONFIRM → POST.

**(f) needed the same discipline.** Its first take tapped Card0, whose VOTE button was enabled
because `VotedLocally` is per-SESSION memory — the account had cast that vote in an earlier
session, the server answered "already voted", and NOTHING moved under a caption promising a bar
fill and an RP count-up. The clip now walks cards until `/points/earn` actually credits, and
confirms the cast by the EARN rather than by the tap.

**The vote burned for (f), as the addendum asks:**

```
[GpsFlowDemo] trying card 'Card1' vote id=541bcde9-9979-400b-ad35-93bb205c092f
              question="今月中にベストスコア更新する人はいる？"
[GpsVote] casting on 541bcde9-9979-400b-ad35-93bb205c092f -> option 693d8824-7a28-4532-917a-46041e6ebf2b.
[PointsService] earn vote_cast: +10 -> RP 6968
[GpsVote] vote_cast earn -> +10 (total 6968).
[GpsFlowDemo] CAST LANDED on vote id=541bcde9-9979-400b-ad35-93bb205c092f
```

`e47a04bc-bed3-43c6-bc53-0d92b18eef5a` was found already-cast (it is the one iteration 1 burned),
so **two** of the seeded GOLFIN AI votes are now spent and two remain for the device pass.

### A5 · Nav-bar seam — **PASS, mean ǀΔRGBǀ = 0.920 (budget ≤ 2)**

Row y = 2434 through the nav-bar icons, every 2nd px across 1170, measured on **70 CONSECUTIVELY
decoded frames** of the ScoreUpload → Gift push in the re-recorded (b) — `ffmpeg -ss T -t 1.2` into
a numbered sequence, never `-ss` keyframe sampling.

```
worst mid-push frame = 8 of 70,  mean |dRGB| = 0.920      (SPEC budget: <= 2)
```

Each frame is measured against BOTH settled rests (the screen before and the screen after) and
scored on the nearer one, because the bar is supposed to be the same bar in both.

### A6 · UI fidelity lint — **PASS as a delta: zero new findings**

`UIFidelityLinter.LintPrefab` over every GPS prefab, against iteration 1's HEAD-extracted numbers:

| prefab | HEAD `96d60fab4` | after iter-2 | verdict |
|---|---|---|---|
| `GpsHubScreen` | 0F/0W | 0F/0W | same |
| `ScoreUploadScreen` | 8F/25W | 8F/25W | same |
| `GpsProfileScreen` | 1F/5W | 1F/5W | same |
| `GpsAvatarScreen` | 5F/15W | 5F/15W | same |
| `GpsBadgesScreen` | 1F/27W | 1F/27W | same |
| `GpsGolfProfileScreen` | 0F/1W | 0F/1W | same |
| `GpsWelcomeScreen` | 0F/1W | 0F/1W | same |
| `GpsGiftScreen` | 0F/1W | 0F/1W | same |
| `GpsVoteScreen` | 0F/14W | 0F/14W | same |
| `VenuePickerModal` | 0F/1W | 0F/1W | same |
| `GiftSendModal` | 0F/1W | 0F/1W | same |
| `VoteCreateModal` | 0F/1W | 0F/1W | same |

**Identical prefab for prefab, including the 17 new shimmer blocks.** It did not start that way:
the vote list's two blocks first came up as 9-sliced pills and the linter flagged
`9slice-cap-kink` twice — "effective corner border 24px < ~50% of estimated cap radius 58px" — on
a 232-tall block. That was the linter being right about the wrong shape: a vote card is not a
capsule, it is a baked `S_GV_CardSimple` panel at `Image.Type.Simple`. The vote placeholder now
uses the card's OWN silhouette, which both removes the warning and makes the placeholder the
shape of the thing that replaces it. The other four sites stand in for pill-shaped rows and keep
the pill. The 15 pre-existing FAILs are unchanged and untouched.

### A7 · Pending-state table — **PASS, wiring and capture**

The six CTAs are unchanged from iteration 1 (`PendingSpend.BeginOn`, disposed **before** the
result is acted on); iteration 1's table stands. What was missing was the frame, and here it is:

`screenshots/pending_ellipsis_vote_button.png` — the vote card's VOTE button mid-call, showing the
`…` and dimmed by its own `Disabled` transition. **This is the A7 frame.**

**Found by measurement, not by eye.** The pending window is ~0.6 s; the frame was located by
decoding (f) frame-by-frame across the tap and measuring the glyph coverage inside the button's
label box: `ink 0.121` (the word VOTE) for frames 1–8, then `ink 0.009` (one ellipsis glyph) for
frames 9–28 while the fill dimmed 192→164→137, then `ink 0.139` for the settled voted state.

**There is NO equivalent frame for POST SCORE, and a still was briefly mislabelled as one.**
`video_c_still_post_pending.png` was named for the state I expected it to hold; it actually shows
step 4, GPS PROOF. It is renamed `video_c_still_step4_gps_proof.png`. The self-reviewer caught the
name; measuring it afterwards showed the frame does not exist to be captured: decoding (c)
consecutively across the POST SCORE tap, the Confirm step occupies frames 001–004 and the Posted
step is up from frame 005 — the whole round trip and step cross-fade take **fewer than five frames
at 30 fps (< 170 ms)**, and no frame in that window carries the ellipsis. The wiring is the same
`PendingSpend.BeginOn(_postScoreButton)` scope as the other five CTAs and is unchanged from
iteration 1; what this account cannot supply is a server slow enough to photograph.

### A8 · Shimmer — **PASS, one cold frame per site — and it found a real defect**

**The defect.** The badges grid kept its placeholder up FOREVER whenever `/badges/progress` did not
answer with a list. `BadgeService.FetchBadges()` fires `OnBadgesChanged` **only on success**, and
the badges screen called it with no callback — so a failed or empty answer repainted nothing. That
was invisible before §D8 (an empty grid looks like an empty grid); once a placeholder covered it,
the screen showed a loading state it could never leave. Every OTHER GPS fetch site already routes
its failure arm back into the paint; this was the one that did not. The screen now passes
`OnBadgesFetched`, which spends the gate on **every** answer.

Caught by A2, not by looking: the badges parity pair was 5,082 px apart when the other six were 0.
`screenshots/defect_badges_shimmer_stuck_BEFORE_fix.png` is the frame.

**The shape, audited across all five sites** (CLAUDE.md rule 15 — the second defect of a shape
means auditing the shape, not the instance). The question: *does this site's placeholder come down
on EVERY answer, including a failure and an empty list?*

| site | fetch call | failure arm repaints? | verdict |
|---|---|---|---|
| `hub.rounds` | `ScoreHistoryService.History(0,3,OnHistoryResult)` | yes — `ShowRounds(null, Fetch)` | fine |
| `badges.grid` | `BadgeService.FetchBadges()` **— no callback** | **no** | **DEFECT, fixed** |
| `gift.supporters` | `GiftService.Supporters(OnSupportersResult)` | yes — `onDone` always fires | fine |
| `gift.golfers` | `UserService.Discover(OnDiscoverResult)` | yes — `ApplyGolfers(null, Fetch)` | fine |
| `vote.list` | `VoteService.List(0,PageSize,OnListResult)` | yes — `Rebuild(..., Fetch)` | fine |

**The cache-hit gate, proven by the log rather than asserted.** `GpsPolishProbe` mirrors every
paint decision into its own run log (the Editor console keeps ~100 entries and a full route emits
thousands, so these lines were being trimmed away before they could be read). One route, every
site:

```
[GpsHub]     rounds     paint(cache) n=0 — instant (cache empty)
[Shimmer]    hub.rounds cold=True  hidden -> shown
[GpsHub]     rounds     paint(fetch) n=0 — instant (repaint)
[Shimmer]    hub.rounds cold=False shown  -> hidden
[GpsBadges]  badges     paint(cache) n=0 — instant (cache empty)
[Shimmer]    badges.grid cold=True  hidden -> shown
[GpsBadges]  badges     paint(fetch) n=0 — instant (repaint)          <- the fix; before it, silence
[Shimmer]    badges.grid cold=False shown  -> hidden
[GpsGift]    golfers    paint(cache) n=0 — instant (cache empty)
[Shimmer]    gift.golfers cold=True  hidden -> shown
[GpsGift]    golfers    paint(fetch) n=5 — staggered
[Shimmer]    gift.golfers cold=False shown  -> hidden
[GpsGift]    supporters paint(fetch) n=0 — instant (repaint)
[Shimmer]    gift.supporters cold=False shown -> hidden
[GpsVote]    votes      paint(cache) n=0 — instant (cache empty)
[Shimmer]    vote.list  cold=True  hidden -> shown
[GpsVote]    votes      paint(fetch) n=5 — staggered
[Shimmer]    vote.list  cold=False shown  -> hidden
```

…and the cache-hit path, on a RE-visit inside the same session:

```
[GpsHub] rounds paint(fetch) n=1 — instant (cache hit)
[Shimmer] hub.rounds cold=False hidden -> hidden      <- never shown, because the cache had the row
```

**How the paint cache was cleared, quoted:** it was not cleared by a hook — **entering play mode
clears it**. Every service (`BadgeService`, `GiftService`, `UserService`, `VoteService`) is a
per-session singleton whose cache starts empty, so the FIRST open of each screen in a run is a
genuine cold fetch. The contrast is inside one run: the (b) clip opens each screen cold and then
returns to the hub and the gift screen WARM, where the same panels paint from cache instantly.

**One frame per site, taken while the placeholder was genuinely up.** Sampling the video does not
work and it is worth saying why: the cold window against this server is 120–260 ms — three to eight
frames at 30 fps — and seven timestamps 200 ms apart across the gift screen's window all decoded to
the same settled frame. So `GpsPolishProbe --mode shimmer` polls each site's own `ShimmerHost`
every frame, **concurrently with the navigation**, and captures on the first frame it is active:

| file | site(s) | host active at |
|---|---|---|
| `shimmer_01_hub_rounds.png` — **the canonical frame** | `hub.rounds` | t+271 ms |
| `shimmer_02_badges_grid.png` | `badges.grid` | t+24 ms |
| `shimmer_03_gift_supporters_and_golfers.png` | `gift.supporters` + `gift.golfers` | t+26 ms |
| `shimmer_04_vote_list.png` | `vote.list` | t+30 ms |

(Re-captured after the vote block took the card's own silhouette — see A6.)

**The hub frame is the canonical one because it is the only legible one, and that is worth stating
rather than hiding.** It arrives through the boundary FADE, which is slower than the push, so its
placeholder is caught with the screen essentially settled and the three round-row blocks plainly
readable under MY RECENT ROUNDS. `shimmer_03` was the first pick and it is a poor canonical: at
t+26 ms the Gift screen is barely a third on and composited over the hub, so the two placeholders
are present but hard to read. It stays as A8 evidence for that site; it is not the frame to judge
the work by.

The three at ~t+25 ms are caught mid-arrival — a screen reached by a push shows its placeholder in
`OnEnable`, DURING the 0.25 s push, and the fetch answers before it lands. The first version of
this mode waited for the push to settle and photographed nothing but the hub; the wait was removed.

**Badges is only cold if you are fast, and that is honest rather than convenient.** The Profile
screen fetches badges itself (`FetchLiveData` chains `/score/stats`, `/badges/progress`,
`/score/history`), so pausing on Profile warms `BadgeService` and the grid then opens with a cache
hit and correctly shows no placeholder — which is what a settle-then-tap probe recorded twice. The
capture above taps BADGES the moment Profile appears, which is the only moment that grid is cold.

### A9 · Modals — **PASS**

Unchanged from iteration 1: `animateShow` defaults to false (pinned by
`ModalAnimateShowDefaultTests`), on for exactly the three GPS modals, `IsVisible()`/`OpenModalCount`
untouched. **No non-GPS prefab and no scene changed at all this iteration:**

```
$ git status --porcelain Assets/Prefabs
 M Assets/Prefabs/UI/Gps/GpsBadgesScreen.prefab
 M Assets/Prefabs/UI/Gps/GpsGiftScreen.prefab
 M Assets/Prefabs/UI/Gps/GpsHubScreen.prefab
 M Assets/Prefabs/UI/Gps/GpsVoteScreen.prefab
 M Assets/Prefabs/UI/Gps/ShimmerBlock.prefab

$ git status --porcelain Assets/Scenes
(no output — ShellScene is byte-identical to HEAD)
```

### A10 · Sweep table (D9) — **PASS; the keyboard row is now filled**

Safe area, scroll feel and the 208 Rubik-variable text sites are unchanged from iteration 1's
table. The row that was empty:

**Keyboard.** `Golfin.UI.Polish.KeyboardInset` + `KeyboardInsetBinder`, attached at runtime to the
Golf Profile `ContentContainer` (nickname + handicap) and the Vote CREATE modal panel (question).
`shouldHideMobileInput` is already true on all three — which is right, and is exactly why this
matters: there is no OS input bar echoing the text either, so a field under the keyboard means the
player types blind.

Only the keyboard's **height** is read, never `TouchScreenKeyboard.area.y`: that rect's origin has
been reported at the top on some iOS versions and at the bottom on others, so a reading that trusts
it is a coin flip per OS release. The keyboard is always flush to the bottom, so the height alone
is the same number under either convention.

`OffsetFor(screenH, keyboardH, fieldBottom, fieldTop, canvasScale, margin)` is a pure function of
five numbers and is pinned by `KeyboardInsetTests` with real iPhone 14 values (2532 px screen,
1008 px keyboard):

| case | expected | why it is the interesting one |
|---|---|---|
| no keyboard | 0 | the Editor path — which is why A2 is unaffected |
| field already clear | 0 | a screen that lurched for a visible field is worse than not lifting |
| under by 240 px | 240 | 1008 + 24 − 792 |
| same at 2× canvas scale | 132 | the margin is canvas px so it scales; the answer is canvas px |
| a field as tall as the gap | capped at the headroom | escaping the keyboard out of the TOP is not a fix |
| no headroom at all | 0 | never return a negative lift, which would push it INTO the keyboard |

**Flagged for the device pass:** the code and the arithmetic are testable here; whether
`TouchScreenKeyboard.area` reports what iOS says is not. That single link is what the phone adds.

### A11 · Importer — **PASS**

This iteration adds **no player-facing string**: 0 `LocalizationManager.Get` calls in any new file,
and `git diff --stat -- Assets/Localization Assets/Data` is empty.

```
$ python3 Tools/content/export_content.py --check --env-file Tools/admin-dashboard/.env.development.local
  texts         v31    958 rows  unchanged  Assets/Localization/LocalizationText.csv
  … (all 20 catalogs unchanged) …
--check: clean — no file would change and no catalog has drifted.
```

### A12 · EditMode — **PASS**

```
TotalTests 2319 · Passed 2316 · Failed 0 · Skipped 3 · 00:01:35
```

+23 over iteration 1's 2296. The 3 skips are the pre-existing `HoleCompleteDriverTests` Stage-C1
skips. New this iteration, all executing by name in the run output:

- `KeyboardInsetTests` (6) — the R6 offset maths.
- `UiMotionNewPrimitiveTests` (5) — `Bump` settles at scale 1 AND actually overshoots; `Tween`
  ends on its exact final value and is monotonic; `Render` drops a number into its surrounding run.
- `UiMotionAllocationTests` (5) — see A13.
- `PaintGateTests` (7) — the cache/fetch/repaint gate, including "a failed fetch still ends the
  cold state", which is the badges defect written as a test.

**And the suite caught a regression I introduced while fixing A6.** Giving `PaintGate` its
`staggers` flag added a third constructor parameter with a default — and C# defaults are a
CALL-SITE feature that `Activator.CreateInstance` does not fill in, so all six reflection-built
gate tests went red with `MissingMethodException` on the next full sweep. It was invisible to the
per-assembly run that had passed twenty minutes earlier, because that run predated the change.
Fixed by constructing through the full parameter list, plus a seventh test pinning that a
non-staggering site still reports its cold state correctly.

**One test was wrong and the code was right.** `TheLiftIsInCanvasPx_NotScreenPx` expected 120 and
got 132: the 24 px margin is specified in CANVAS px, so at 2× it is 48 on screen, and the shortfall
is 264 screen px = 132 canvas px. The assertion was corrected and now also states the property its
name is about — `lift × scale == the screen-px shortfall`.

### A13 · Perf / GC — **PASS, measured twice, and the two answer different questions**

**In situ** — `GpsPolishProbe --mode perf`, its own pass with the Editor profiler ON and **no
screenshots** (a full-resolution `ReadPixels` + PNG encode allocates ~100 MB and swamped whichever
push it landed beside — the first attempt reported 289 KB/frame with a 392 ms worst frame, both of
them the capture). `ProfilerRecorder` on `GC Allocated In Frame` (Memory) and `Main Thread`
(Internal), sampled ONLY on the frames a push is running. `gps_polish_perf.json`:

```
pushesSampled          10
firstPushAllocBytes    4,569,855      (warm-up: coroutines, UiMotionRunner, on-demand CanvasGroups)
warmFrames             134
warmAllocBytesPerFrame 307,343
worstPushAllocBytes    5,490,301      GpsProfile->GpsBadges
worstFrameMs           59.275         GpsHub->GpsVote        (13 of 12 frames build 5 vote cards)
```

Eight of the ten pushes have a worst frame of **17.6–24.1 ms**; the two outliers are the vote list
building five cards and the hub's first activation. **This is an upper bound on the WHOLE APP
during a push** — Editor play mode, profiler enabled, live server, TMP rebuilds, screen activation
— not the tween, and it is quoted as such.

**Isolated** — the attribution, and the SPEC's actual question ("if the push allocates per frame,
fix it"). `UiMotionAllocationTests` steps each routine's own `MoveNext` with nothing else running
and measures the managed heap per frame:

| routine | what it drives | bytes/frame |
|---|---|---|
| `Slide` | the push itself | **≤ 32** (assertion threshold; the loop is `yield return null` + a struct assignment, no closure) |
| `Fade` | the chrome cross-fade | **≤ 32** |
| `Rise` | the boundary entry | **≤ 32** |
| `Tween` | the vote bar fill | **≤ 32** (one delegate, allocated at creation) |
| `CountUp` | every §D7 number | allocates ONLY when the drawn integer changes — pinned: a 0.4 s count over 12 points draws ≤ 13 distinct strings across strictly more frames |

No per-frame allocation from the tweens, so nothing to fix. The 307 KB/frame in situ is the app,
and reducing it is not this task.

### A14 · Deviations — §3 below.

---

## 3 · Deviations

D-1 … D-7 from iteration 1 stand as written and were accepted. Two more:

**D-8 · CORRECTION: the GPS scene copies ARE prefab instances. Iteration 1 said the opposite.**
Its A6/D-7 note reads "THE SCENE COPIES ARE NOT PREFAB INSTANCES … verified: `IsPartOfPrefabInstance`
is false for all nine". That verification was run in **play mode**, where the flag is false for
EVERY object — the documented trap. Re-checked in EDIT mode:

```
GpsHubScreen: isPrefabInstance=True src=Assets/Prefabs/UI/Gps/GpsHubScreen.prefab
… all nine, each resolving to its own prefab …
```

Consequence, and it matters: **the prefab pass alone reaches the live scene.** All five shimmer
hosts are present in ShellScene after a clean reload from disk, with a **zero scene diff**.
`GpsPolishBuilder.ApplyToScene` is unnecessary; running it once produced 1,296 lines of
prefab-override churn in `ShellScene.unity`, which was discarded. The menu item is left in place
(it is harmless and idempotent) but its header claim is now false — flagged for the reviewer as a
comment that should be rewritten, not silently believed.

**D-9 · The gift panels fade on a COLD open, alongside their placeholder — not "with their data".**
§D4's literal reading is that `TOP SUPPORTERS` / `POPULAR GOLFERS` / `BUY GIFT ITEMS` fade in when
their rows arrive. Taken literally that hides the placeholder those panels exist to host: the
shimmer lives INSIDE the panel, so a panel held at alpha 0 until the data lands shows the player
nothing at all during the one moment §D8 is for. So the rule is "fade in the first time the panel
has something to show" — the placeholder on a cold open, the rows on a warm one — which is one rule
for all three and never leaves a panel invisible. Every paint path, including the failure arms,
ends by revealing the panel.

---

## 4 · Files changed

| file | what |
|---|---|
| `Assets/Scripts/UI/Gps/GpsPaintMotion.cs` | **new** — `PaintGate` (cache/fetch/repaint + the cold state), `PanelReveal`, the staggered rise, the shimmer show/hide |
| `Assets/Scripts/UI/Gps/ShimmerHost.cs` | **new** — a placeholder group, found by SITE not by path |
| `Assets/Scripts/UI/Polish/UiSelection.cs` | **new** — §D6 bump + two-Image cross-fade, with the generation stamp that stops a fast double-tap deactivating the chip it just selected |
| `Assets/Scripts/UI/Polish/KeyboardInset.cs` | **new** — the pure offset maths + the runtime binder (R6) |
| `Assets/Scripts/UI/Polish/Tests/GpsPolishMotionTests.cs` | **new** — 22 tests: keyboard maths, new primitives, allocation, the paint gate |
| `Assets/Scripts/UI/Polish/UiMotion.cs` | `Bump`, `Tween`, `Render`, and a `wrap` format so a count-up keeps the words around the number |
| `Assets/Scripts/UI/PersistentUIManager.cs` | `ArmRewardPointsCountUp()` — a one-shot so ONLY a GPS-caused RP delta counts up; expires in 5 s |
| `Assets/Scripts/UI/Gps/GpsHubScreenController.cs` | rounds paint from the controller's own cache, stagger, shimmer, empty label fades |
| `Assets/Scripts/UI/Gps/GpsBadgesScreenController.cs` | stagger, shimmer, newly-earned pulse (per-id, never on the first paint), **and the fetch callback that closes the stuck-placeholder defect** |
| `Assets/Scripts/UI/Gps/BadgeCellView.cs` | `PlayEarnedPulse()` — a runtime gold overlay that rests at alpha 0 |
| `Assets/Scripts/UI/Gps/GpsGiftScreenController.cs` | supporters/golfers stagger + shimmer, three panel reveals, GIFTS RECEIVED count-up |
| `Assets/Scripts/UI/Gps/GiftSendModalController.cs` | amount-pill bump + cross-fade, balance count-up on a live refresh |
| `Assets/Scripts/UI/Gps/GpsVoteScreenController.cs` | card stagger + shimmer, PUBLIC↔MINE list cross-fade, chip bump, the RP count-up arm around the cast |
| `Assets/Scripts/UI/Gps/VoteCardView.cs` | bars animate old→new after a cast, from the percentages actually DRAWN |
| `Assets/Scripts/UI/Gps/GpsGolfProfileScreenController.cs` | swatch + chip selection through `UiSelection`, keyboard binder on both fields |
| `Assets/Scripts/UI/Gps/VoteCreateModalController.cs` | keyboard binder on the question field |
| `Assets/Scripts/UI/Gps/GpsProfileScreenController.cs` | badge-count count-up |
| `Assets/Scripts/UI/Gps/ScoreUploadFlowController.cs` | the Posted total pops in |
| `Assets/Scripts/UI/Gps/Editor/GpsPolishBuilder.cs` | the five shimmer sites, with per-site geometry and an optional shape sprite; one shared block constructor |
| `Assets/Scripts/UI/Gps/Editor/GpsPolishProbe.cs` | `parity` / `perf` / `shimmer` modes, the perf recorders, the paint-log mirror, and the idle+scene-restore gate before entering play mode |
| `Assets/Scripts/UI/Editor/GpsFlowDemoRecorder.cs` | five new scenarios; `Press()` refuses a control the player could not press |
| `Assets/Prefabs/UI/Gps/{GpsHub,GpsBadges,GpsGift,GpsVote}Screen.prefab` | the five INACTIVE shimmer hosts (17 blocks) |
| `Assets/Prefabs/UI/Gps/ShimmerBlock.prefab` | the band carries the pill sprite (a null-sprite Image is what the linter fails as a fabricated flat box) |

`Assets/Scenes/ShellScene.unity` — **unchanged**, see D-8.

---

## 5 · Not done / needs the device

| item | why |
|---|---|
| **R6 keyboard offset — SEEN on a phone** | the maths is pinned in EditMode and the wiring is in place, but `TouchScreenKeyboard.area` reports 0 in the Editor. Nothing about it can be observed here. |
| Rubik `Medium` font import (208 sites) | out of scope by the SPEC; the list is in iteration 1's A10 table. |
| The 15 pre-existing lint FAILs | all `9slice-collapse-x … width 0px` on bars sized at runtime and buttons inside inactive step roots. Unchanged by this task; a real but separate piece of work. |
| `GpsPolishBuilder.ApplyToScene`'s header comment | now factually wrong (D-8). Left for the reviewer to see rather than quietly edited. |

---

## 6 · Editor state

Play mode exited; ShellScene byte-identical to HEAD and not dirty; no temporary scene, no auto-run
script. Two artefacts were discarded rather than committed: a 1,296-line `ShellScene.unity`
prefab-override churn from the unnecessary `ApplyToScene` run, and a 7,756 → 2,105,970 byte
`NotoSansJP-VariableFont_wght SDF.asset` atlas bake (the known JA-preview scar).
