# IMPLEMENTER_REPORT — `game_polish_b`

> **NOT SUBMITTED FOR REVIEW.** This is a mid-task report for a code-complete pass that is
> partly done. `STATUS.md` is `IMPLEMENTER_WORKING`, not `READY_FOR_SELF_REVIEW`, and the
> acceptance checklist below is deliberately not filled in with PASS rows for work that has
> not happened. § What is NOT done is the important section.

**Iteration shape:** `game_polish_b:code-complete-pass-1`

## 0 · Two things to read first

**1. The Editor crashed twice, and it was my code.** Both crashes were
`Scripting::RaiseStackOverflowException` (diagnosed from
`~/Library/Logs/DiagnosticReports/Unity-2026-09-08-1103*.ips` and `-1107*.ips`, not guessed).
The cause was the §D2 pill glow, written first as `UiMotion.Then(Pulse(...), StartGlow)` —
re-arm yourself when the sweep ends. `Then` runs its tail in TWO places, at the end of the
routine and again inside the finalizer it registers, so each re-arm left an entry whose own
finalizer re-armed; the next `Run` on that handle settled the previous entry, the settle
invoked that finalizer, it re-armed again, and the chain never reached a tween that does not
re-arm. **A self-re-arming `Then` tail is unbounded by construction, and nothing in the
`UiMotion` API says so.** It is one long-lived coroutine yielding fresh sweeps now. It fired
whenever the Home pill settled, i.e. on every play-mode entry that reached Home, which is why
it looked like Unity being flaky.

**2. The modal count is 15, not 13.** The SPEC says thirteen twice; its own table lists
fifteen, and a sweep of every `ModalController` in ShellScene and under `Assets/Prefabs`
finds fifteen non-GPS modals. `InGameSettingsModal` is the one the scene alone will not show
you — no ShellScene instance, spawned into the gameplay scene at runtime. All 15 are set;
read back live as **19 `animateShow` true, 0 false** (my 15 plus the 5 GPS modals
`gps_polish` had already set).

## Files modified or created

| File | What |
|---|---|
| `Assets/Scripts/UI/Polish/PaintMotion.cs` | **Moved** (was `GpsPaintMotion` under `Gps/` — that path no longer exists, which is the point), GUID `1ab8dbdd…` kept, namespace/class unchanged. Move banner added. |
| `Assets/Scripts/UI/Polish/ShimmerHost.cs` | **Moved** (was `ShimmerHost` under `Gps/`), GUID `00055998…` kept. Move banner added. |
| `Assets/Prefabs/UI/Common/ShimmerBlock.prefab` | **Moved** (was `ShimmerBlock` under `Prefabs/UI/Gps/`), GUID `5fa029d7…` kept. |
| `Assets/Scripts/UI/Gps/Editor/GpsPolishBuilder.cs` | The ONLY `Gps/` edit: the ShimmerBlock path (code line + the doc comment naming that path). 2 lines. |
| `Assets/Scripts/UI/Polish/UiMotion.cs` | `enum Ease`, `EaseOutBack`, `Curve`, `BackOvershoot`, optional trailing `Ease` on Pop/Unpop/Slide/Rise/Tween; lerps → `LerpUnclamped`, alphas → `Clamp01`. |
| `Assets/Scripts/UI/Polish/GameShimmerSites.cs` | **New.** The game's 7 cold-fetch site names, beside the moved GPS table. |
| `Assets/Scripts/UI/Polish/Editor/GamePolishBuilder.cs` | **New method** `ApplyModals()` + menu item. |
| `Assets/Scripts/UI/Polish/Editor/RetrofitParityRecorder.cs` | **New.** The §D2 frame-by-frame gate. |
| `Docs/Scripts/compare_retrofit.py` | **New.** Diffs the two trace JSONs. |
| `Assets/Scripts/UI/Matchmaking/VersusResultModalController.cs` | §D2: `PopInScaleRoutine` and the `Hide` override deleted. |
| `Assets/Scripts/UI/Home/DailyMissionPillController.cs` | §D2: slide → `UiMotion.Slide`, glow → `UiMotion.Pulse` on a runtime CanvasGroup. |
| `Assets/Scripts/UI/Gacha/GachaRevealModalController.cs` | §D2: StepEnter/StepPop/StepShake driven by `UiMotion.Tween`; local `EaseOutBack` deleted. |
| `Assets/Scripts/Gameplay/UI/ShotUI/HoleCompleteWidget.cs` | §D1.3: its own pop/unpop + scrim fade. |
| `Assets/Scripts/UI/PersistentUIManager.cs` | §D3: the count-up guard is `!=`, not `>`. |
| `Assets/Scripts/UI/Roster/Managers/RewardPointsManager.cs` | §D3: arms the count-up inside `SpendPoints`/`EarnPoints`. |
| `Assets/Scripts/UI/Gacha/GachaBannerCard.cs`, `GachaPullFlow.cs` | §D5: PendingSpend on both PULL buttons. |
| `Assets/Scripts/UI/Rankings/RankingsScreenController.cs` | §D6: PaintGate, row stagger, podium reveal 3→2→1. |
| `Assets/Scenes/ShellScene.unity` | 8 lines: 7 × `animateShow: 1`, 1 × `_loop: 1`. |
| 8 × `Assets/Prefabs/UI/Modals/*.prefab` | 1 line each: `animateShow: 1`. |
| `Assets/Scripts/UI/Polish/Tests/{UiMotionEaseTests,CountDownTests,ModalPopTests}.cs` | **New** suites. |
| `Assets/Scripts/UI/Gacha/GachaCarouselController.cs` + `Assets/Tests/EditMode/GachaCarouselLoopTests.cs` | **Cesar side-request**, not this SPEC: the banner carousel wraps infinitely. |

## What IS done

### A2 · Retrofit parity — **PASS, fail 0**

`Docs/Specs/Active/game_polish_b/retrofit_parity.txt`, from `retrofit_old.json` /
`retrofit_new.json` in the same folder.

| trace | max Δ | gate | headroom |
|---|---|---|---|
| `versus.popin` | 0.000012 | 0.005 scale | 400× |
| `pill.slide.enter` | 0.066 px | 0.5 px | 7.5× |
| `pill.glow` | 0.000030 | 0.01 alpha | 300× |
| `gacha.enter` | 0.000133 | 0.005 scale | 37× |
| `gacha.pop.curve` | 0.000030 | 0.005 curve | 166× |
| `gacha.shake` | 0.119° envelope | 0.5° | 4× |

The OLD side was recorded by checking the three controllers out at `f11079114` with the
instrument at its fixed revision, so both sides ran through the same recorder. The JSONs'
`source` fields show `PopInScaleRoutine`, `SlideRoutine`, the `Update()` sine and
`EaseOutBack` present on the old side and gone on the new. (`headSha` reads `4c93a040b` on
both files because HEAD was there while the tree held the older files — stated rather than
left to look like a mistake.)

**Three corrections to the instrument, each of which first presented as a regression:**
it did not drive NESTED enumerators (the retrofitted steps are `yield return
UiMotion.Tween(...)`; a plain `MoveNext()` loop walks past them — `gacha.enter` and
`gacha.shake` recorded 2 frames each); it compared frame k to frame k when
`Time.captureDeltaTime` does not survive the booted app (0.01633 on one run, 1/60 on
another) so it compares on TIME now, Catmull-Rom resampled, over the overlap only; and its
legacy glow branch invoked `Update()` on an ACTIVE component Unity was also updating, so the
old trace ran the glow at double speed.

### A9 · `UiMotion` API — **PASS**

`git diff` of `UiMotion.cs` is the `Ease` addition and nothing else. `Ease.OutCubic` is
proven equal to `EaseOut` **by value at every point** (`UiMotionEaseTests`), not by reading
the diff. Every existing call site compiles unchanged. GPS suites green in the full sweep.
The one substantive change inside the primitives is `Mathf.Lerp` → `Mathf.LerpUnclamped`
(plus `Clamp01` on alphas): a no-op for OutCubic and Linear, and without it OutBack's
overshoot — the entire reason the enum exists — would be clipped and the gacha retrofit
would have silently flattened the reveal.

### A10 · D0 moves — **PASS, with one caveat**

`git log --follow` traces all three. GUIDs identical before and after:
`1ab8dbdded78d4c05b12877bd90c3212`, `0005599849a9347f080d5d6d63e6f20c`,
`5fa029d7d9f814fb9bc84a732bdecd5e`. `git diff --stat -- Assets/Scripts/UI/Gps` lists only
`GpsPolishBuilder.cs`.

**Caveat 1 — 2 lines, not 1.** A10 says "one line". The second is the doc comment directly
above it, which named the old path; leaving a comment pointing at a file that no longer
exists is a worse outcome than a 2-line diff.

**Caveat 2 — the moves are recorded under someone else's commit.** They were staged with
`git mv` and swept into `20f8055cf` ("free_swing: the verify harness…") by the concurrent
`scheme_freeswing` session, which stages by file against this shared tree. Nothing was lost.
Repaired forward, not rewritten (project memory: `k10_commit_swept_k11_edits`).

### A12 · EditMode — **PASS**, and the new suites are PROVEN to run

**2860 tests, 2857 passed, 0 failed, 3 skipped.** All three skips are pre-existing and
documented (`HoleCompleteDriverTests`, Stage C1 no-ops), unrelated to this task.

`tests-run` ignores the class and assembly filters and reports only failures, so a suite that
silently did not run looks identical to one that passed. A deliberate failing test in
`UiMotionEaseTests` took the total to **2861 with exactly 1 failure** carrying its marker,
then was removed. That is the proof; the count is not taken on trust.

**Re-run after §D4/§D6: 2863 tests, 2860 passed, 0 failed, 3 skipped** — up from 2860, which
is the 3 new `ShimmerHostTests` arriving and passing, a self-verifying increment. Green again
after §D1.4/§D7.

**A FLAKINESS FOUND ALONG THE WAY, AND NOT PAPERED OVER.** One run failed 5 tests across three
suites. They all reported the same thing — a tween that had already finished — and all five
passed on an immediate re-run with no code change. Cause: these tests integrate
`Time.unscaledDeltaTime`, which in EditMode is whatever the editor's last frame took (~1.1 s
normally on this machine, far more after a scene reload or a two-minute test run). When dt
exceeds the duration the tween correctly completes in ONE step and a test that wanted to see it
half-done fails while nothing is wrong.

Three of the five are mine and are fixed: `RequireAFrameWithin` now `Assert.Ignore`s when the
clock is too coarse to produce an intermediate frame, which is the honest outcome — failing
there is a false alarm and passing is a lie. **Two are NOT mine and are still fragile:**
`UiMotionAllocationTests.CountUp_AllocatesOnlyWhenTheDrawnNumberChanges` and
`UiMotionNewPrimitiveTests.Bump_OvershootsBeforeItComesBack`. They pre-date this task, the same
guard would fix them, and I have left them alone rather than widen this diff — flagged here so
the next red run on them is recognised for what it is.

**Re-run after A5: 2868 tests, 2865 passed, 0 failed, 3 skipped** — up from 2863, which is the
5 new `RpArmingTests`.

New suites: `UiMotionEaseTests` (13), `CountDownTests` (8), `ModalPopTests` (7),
`ShimmerHostTests` (3), `RpArmingTests` (5), `GachaCarouselLoopTests` (13, the side-request).

### §D1.1 · Modals — done, scene diff 8 lines

All 15 set, read back live as 19 on / 0 off. Builder is idempotent (second run: 8 "already").

**On the scene diff, and on being wrong about it twice.** Saving ShellScene usually rewrites
~1296 lines — 154 RectTransforms whose anchors flip (0,1)→(0,0) with position and sizeDelta
zeroed. I published a cause for this twice and both were falsified by the next experiment:

| # | Claim | Falsified by |
|---|---|---|
| 1 | "Inherent to saving this scene; nothing to do with a builder" — from a control (open, mark dirty, save, change nothing → 1297 lines) | §D4's `ApplyShimmer` run produced **4** anchor lines, not 154, and those 4 were a block removed and re-added byte-identically |
| 2 | "Play-mode contamination; the control was run in a contaminated Editor" | The same open+dirty+save control, re-run in an Editor that has **never entered play mode this session**, churned identically (1296 lines, 616 anchor lines) |

**So I do not know the trigger, and I am not going to guess a third time.** What is established:
one save out of several was clean and the rest were not, the clean one was `ApplyShimmer`'s,
and nothing I varied deliberately (fresh open, no play mode, save in the same call as the open)
reproduced it. Whatever the state is, it accumulates across an Editor session.

What IS reliable, and what both scene commits in this task actually used, is the workaround:
isolate the builder's hunks out of the churned save and apply them to HEAD's copy
(project memory: `isolate_scene_save_drift_partial_stage`), then reload the scene from disk.
§D1.1 landed 8 lines that way and §D4 landed its six hosts with 4 incidental anchor lines.
**The churn is worth a task of its own with someone who can bisect it; it is not this one's,
and the two conclusions above should not be quoted as findings.**

### §D1.3 · HoleComplete — done

`HoleCompleteModalController.Show()` is a no-op, so the flag alone does nothing for it. The
widget pops `_root` and fades `DimBackground` (a sibling, not a child, so the scrim does not
scale with the cards). The curve is COPIED, not called: `Golfin.Gameplay.UI` cannot reference
`Assembly-CSharp`, the wall `SelectorOverlayWidget` and `SelectorCarouselMath` already
document and solve this way. `ModalPopTests` compares the copies to the originals point for
point so a drift fails a test.

### §D3 · Count-ups — partly done

The guard is `!=` (equality still excluded, so a repaint cannot burn the arm). Armed inside
`RewardPointsManager.SpendPoints`/`EarnPoints` rather than at the SPEC's seven screens —
see Deviations D-4. Modal-local numbers (level-up bars, level `Pop`, mission counters) are
NOT done.

### §D5 · PendingSpend audit — complete as an audit; one CTA wired

| CTA | Async window | Verdict |
|---|---|---|
| LevelUp confirm | yes | already wired |
| ClubLevelUp confirm | yes | already wired |
| GeneralShop BUY | yes | already wired |
| StaminaShopDetail BUY | yes | already wired |
| TournamentSignup confirm | yes | already wired |
| ModeCard PLAY | yes | already wired |
| **Gacha PULL x1 / x10** | **yes** (`PullAsync` round trip) | **WIRED this task** |
| Tournament CLAIM | **no** — `void ClaimPrize(string)`, synchronous interface | N/A, stated |
| Settings LOG OUT | **no** — `void SignOut()` then an immediate screen change | N/A, stated |
| Mission CLAIM | **no CTA** — `ClaimMissionRoutine` is started by hole-complete, not a tap | N/A, stated |
| Rankings / Tournament refresh | **no CTA** — no `refreshButton`/`_refreshButton`/`RefreshButton` anywhere in the shell | N/A, stated |

### §D6 · Rankings

`RebuildList` knows which paint it is: OnEnable = Cache, refresh callback = Fetch, tab tap =
Repaint. Rows stagger on the first cold fetch only.

**The podium reveal is deliberately NOT `UiMotion.Pop`.** Pop settles on `Vector3.one`
unconditionally — correct there — but Top2/Top3 rest at **0.85**, and that difference IS the
podium hierarchy. Popping them would have flattened all three to the same size and shipped as
polish. Each card tweens from 0.9 × its OWN rest scale back to that rest scale, `StaggerDelay
× 3` apart, winner last.

### §D4 · Shimmer — done, six hosts, and two sites moved

`ApplyShimmer` places six INACTIVE hosts (15 `ShimmerBlock` instances). Verified by reading the
live scene, not the builder's log: **13 `ShimmerHost` in ShellScene (my 6 + GPS's 7), 0 active
at rest**, and `ShimmerHost.Find` resolves every name in `GameShimmerSites.All`.

**Two sites are not where §D4 put them, and both moved because the code disagreed with the spec:**

| Site | §D4 says | Shipped | Why |
|---|---|---|---|
| GeneralShop cards | shimmer ×4 | **no shimmer** | `GeneralShopCatalog` reads a BUNDLED `Resources/Data/shop_catalog.csv` + a content overlay, synchronously, on first access. The player never waits on a network for it, so a shimmer would be a loading animation over data that never left — the exact thing GPS §D8's cold-only rule prevents. Gets §D6's stagger instead. The site constant was REMOVED rather than left dangling: a name nobody may use is a trap. |
| MissionSelection cards | shimmer ×2 | **shimmer on the DAILY card** | `MissionCatalog.EnsureLoaded` is local and synchronous. The daily is genuinely fetched and hidden until the server answers, which makes it the one region on that screen where a player waits in front of a blank space. Every arm that ends the wait clears it, **including the two failure arms** — a placeholder over a card that is never coming is worse than none, and failure arms are where shimmers get stranded. |

### §D6 · Staggers — done, split by what the data actually is

| Site | Gate | Why |
|---|---|---|
| Rankings rows, tournament cards, tournament leaderboard rows, gacha history page 1 | `PaintGate` — first COLD fetch only | Server-backed. Cache paints instant; repaints (tab, language, filter) never stagger. |
| Hole cards, mission cards, shop cards | first paint per screen entry | No fetch to gate on. A filter/tier change repaints the same data and must not re-flow a list under the player's finger. |
| **Mode Select** | **EVERY entry paint** | Cesar's front-door exception. Not the rule bending: this screen has no fetch at all, so a fetch gate would say "cache, instant" forever and the cards would never move. Logged as `paint(local)` so it cannot later be read as a mislabelled fetch. |
| Gacha prizes grid | **untouched** | It ALREADY staggers — `PlayEntrance`, `gacha_reveal_animation §3`, its own 0.045 s beat. Retrofitting it would be a fourth §D2 retrofit of a loved animation and would need its own parity gate; §D2 named three. |

GachaHistory takes its verdict from the RECORD count, not the rendered count: `FillTo` spawns
rows over several frames, so a gate asked "how many rows are on screen" here would hear zero
and call every paint cold. It staggers once at the end of the fill rather than per row, which
would otherwise fight `FillTo`'s own frame budget.

**Selection bumps** on mode card, hole card, mission card and the HistoryChip — placed ABOVE
the guards in each handler, not inside the success path. A card that expands answers for
itself; a LOCKED card, a second tap that collapses, and the chip's toast-only arm all look
identical to no response at all.

### §D1.4 · Result-modal choreography — done, and one thing the SPEC asks for does not exist

`ResultChoreography` (shared, Assembly-CSharp) runs each modal's post-pop sequence and — the
part that matters — makes it **skippable at any frame**. §D1.4: "Buttons stay interactable from
the first frame ... a tap during the choreography completes it instantly." A result screen is
one the player is trying to LEAVE, and half a second of un-skippable celebration is the most
irritating thing a polish task can add. So nothing disables a control, and every control that
leaves the modal calls `CompleteNow()` first — idempotent, so no button has to ask whether a
sequence is running.

**It tracks its children**, and that is not incidental: `UiMotion`'s interruption story is
per-handle, so stopping the SEQUENCE would leave a staggered group's N tweens running. A tap
would snap the sequence to its end and then watch the rows animate over the top of it.

| Modal | Sequence |
|---|---|
| Versus | outcome word (BOTH labels — one says LOSER and the player reads that just as hard) `Pop`; reward rows `Stagger`-rise; amounts `CountUp` from 0 into `x{0}`, a beat AFTER the rise so a row is on screen before its number moves |
| TournamentResult | rank badge `Pop`; prize `CountUp` from 0, with `+ Trophy` carried in the wrap so the suffix is never dropped mid-count |
| HoleComplete | verdict glyph `Pop`; the three reward amounts count together (in sequence they would outlast the player's patience). Local implementation — `Golfin.Gameplay.UI` cannot reference Assembly-CSharp, the same wall §D1.3 documents |

**THE MISSION-COMPLETE BANNER §D1.4 NAMES DOES NOT EXIST.** No banner object, no
`MISSION_COMPLETE` key, no field for one — grepped across `Assets/Scripts/Gameplay/UI/ShotUI/`
and `Assets/Scripts/UI/Modals/Result/`. Rather than invent one, it is reported for Cesar to rule
on: either it was never built, or it lives somewhere the SPEC's author expected and I have not
found.

### §D7 · Probe — done, and it found a real bug

`GamePolishProbeB` with three modes. **Deviation D-8:** it is a SIBLING of `GamePolishProbe`,
not modes inside it — that probe is `game_polish_a`'s gate, a completed and approved task, and
adding b's modes to it means editing a's evidence machinery to serve this task.

**`modals` — 14 modals, fail 0.** Every one pops (mid-pop scale 0.938–0.967 one frame in),
`IsVisible()` true on the Show frame and false on the Hide frame, `OpenModalCount` +1 then back.
14 mid-pop captures, `realPlay: true` in every sidecar. `modals_invariants.json`.

**It took four runs, and each failure was the PROBE, not the code** — which is the argument for
having built it:

| Run | Reading | Actual cause |
|---|---|---|
| 1 | 13 fails | 8 modals live under INACTIVE screen roots; `UiMotion.Run` settles instantly when the host is not `isActiveAndEnabled`, so they read as "did not pop" |
| 2 | 1 fail | the first modal on a freshly-activated branch pays that screen's `OnEnable` rebuild — a frame longer than `PopDur`, so the tween correctly completes in one step |
| 3 | 1 fail | a fixed 0.5 s settle just moved the failure to the next modal, because `FindObjectsByType` order is not stable between runs. Replaced with a wait for five consecutive SHORT frames |
| 4 | **0 fails** | — |

**AND A REAL, PRE-EXISTING BUG.** The probe reported `TournamentResultModalController` NOT
PRESENT AT RUNTIME. It is in the scene at author time. Cause:
`TournamentResultPresenter.Awake` is a singleton guard — `if (Instance != null && Instance !=
this) Destroy(gameObject)` — and the component was on that object **twice**: once from the
prefab, once as a scene `m_AddedComponents` override. The second copy's Awake destroyed its own
GameObject at boot, so **the tournament result modal deleted itself on every launch and could
never open.** Not caused by this task, but it made §D1.1's flag and §D1.4's choreography inert
for that modal, so it is fixed here: the scene override is removed (both copies were identically
wired), 36-line diff, and the modal now appears in the probe and pops at 0.967.

**`shimmer`** — all six hosts resolve, correct block counts, `activeAtRest=False` on every one.

**`perf`** — baseline median whole-frame GC 205,629 B; each modal's pop adds a uniform
164–191 KB. **That number is NOT a test of A13's ≤32 B/frame** and is not offered as one: A13's
budget is the ISOLATED per-tween figure pinned by `UiMotionTests`, and "GC Allocated In Frame"
is the whole frame — Editor, shell, every `Update`. The uniformity across fourteen different
modals is the tell that it is the shared cost of activating a panel and rebuilding its canvas,
not the tween. The first version of this mode took a MAX over a window containing a ~7 MB boot
spike and reported every modal as seven megabytes BETTER than baseline; medians replaced it.

### A4 · Videos — five clips, and two subjects that could not be reached

`GamePolishDemoRecorderB` is `GamePolishDemoRecorder`'s shape pointed at this task: one play
session, one recording at 1170x2532, a sidecar of segment boundaries on the same clock, cut and
captioned by `cut_game_polish_clips.py game_polish_b`. No stills are taken while it records —
the y-flip has two triggers and one of them is any RenderTexture read during a recording — so
every still here is extracted from the MP4 afterwards.

| Clip | Length | Shows |
|---|---|---|
| `game_polish_b_a_roster_levelup.mp4` | 11.8 s | the level-up modal pops, the level `Pop`s to Lv 14/39, pending SP shows `+2` on STRENGTH, and the top-bar RP counts DOWN |
| `game_polish_b_c_rankings_cold.mp4` | 6.2 s | the board arrives — podium, then the rows. The still shows #1 full size against #2/#3 at 0.85, which is the hierarchy the reveal had to preserve |
| `game_polish_b_d_gacha_reveal.mp4` | 13.1 s | the retrofitted reveal end to end: bag drop, shake, rays, card pop. Tickets 2,890 → 2,440 on a real x10 |
| `game_polish_b_f_tournament_cards.mp4` | 12.6 s | the tournament cards staggering in on the schedule paint |
| `game_polish_b_g_mode_select.mp4` | 12.7 s | Mode Select entered twice, staggering both times, then a card tap |

**Two of A4's seven subjects were not reached, and neither is hidden:**

- **(b) shop purchase.** No interactable CTA at **RP 6,139** — nothing on the catalog is
  affordable or is already owned. The bail message carries the live balance so the claim is
  checkable rather than asserted. **Its actual subject — RP counting DOWN in the top bar — is
  proven instead by (a), frame by frame** (see A5).
- **(e) hole complete.** A real hole-complete needs a hole played to the cup, which unloads
  ShellScene under the take. Not attempted rather than faked.

**THREE CLIPS WERE THROWN AWAY BECAUSE THE CAPTION DID NOT MATCH THE FRAME**, which is the
whole reason for looking at every frame rather than trusting the log:

| Take | Log said | The frame showed |
|---|---|---|
| 1 | `(a)` bailed "not enough RP" | the modal never got SP: my tap matched `ShopPlusButton` on another screen, so CONFIRM was correctly dark. Tap scoping fixed it |
| 2 | `(f)` reached | **SELECT HOLE** — `CtaSilverButton` on an ENDED tournament navigates, it does not open signup. Re-cut with a caption describing what is actually there |
| 2 | `(g)` reached | **PRIZES** — (g) ran straight after (d)'s real pull left the app there, which is also what caused an 850 s stall. Re-recorded alone |

### A5 · Count-ups — the per-site table

**§D3's sites are eight call sites, not seven screens** (deviation D-4: the arm lives on
`RewardPointsManager.SpendPoints`/`EarnPoints`, because "the player caused it" is a property of
that class and a list of screens goes stale on the ninth). Enumerated from source, mapped to
the SPEC's names:

| §D3 names | Call site | Routes through | Counts? |
|---|---|---|---|
| level-up spend | `CharacterManager.cs:713` | `SpendPoints` | ✅ |
| level-up spend (clubs) | `ClubLevelUpModalController.cs:578` | `SpendPoints` | ✅ |
| shop purchase confirm | `ShopTransaction.cs:117` | `SpendPoints` | ✅ |
| stamina purchase | `ShopTransaction.cs:377` | `SpendPoints` | ✅ |
| gacha pull result (RP fold) | `ShopTransaction.cs:461` | `SpendPoints` | ✅ |
| hole-complete rewards, mission claim | `RewardGranter.cs:57` | `EarnPoints` | ✅ |
| tournament result (prize) | `RewardPointsServiceAdapter.cs:58` | `EarnPoints` | ✅ |
| — (not in the SPEC's list) mode entry fee | `ModeCardController.cs:637` | `SpendPoints` | ✅ |
| — server refresh | `ServerBalanceSync`, `ServerBalanceSyncBehaviour` | `ApplyServerBalance` | ❌ **by design** |
| — dev tools | `RosterDebugTools`, `RewardPointsDebugPanel` | `EarnPointsLocalOnly` / `SetPoints` | ❌ **by design** |

**BEFORE / AFTER, MEASURED IN ONE RUN** (`countup_run.log`). The probe changes the balance and
samples the top-bar label every frame for 0.6 s. A COUNT passes through values that are neither
the start nor the end; a SNAP does not — and the unarmed path in the same run is the "before":

| path | change | distinct rendered values | intermediate | verdict |
|---|---|---|---|---|
| `SpendPoints` (DOWN) | 6.139 → 6.114 | 15 | **14** | **COUNTED** |
| `EarnPoints` (UP) | 6.114 → 6.139 | 15 | **14** | **COUNTED** |
| `SetPoints` (dev override) | 6.139 → 6.139 | 1 | **0** | **snapped** — correctly unarmed |

The balance ends where it started. And the count-DOWN is also captured in real play, frame by
frame, from the A4 level-up clip (`screenshots/a5_rp_countdown_frames236-257.png`): **6.153
steady, then 6.148, 6.143, 6.140, settling on 6.139** — intermediate values, decelerating,
landing exactly. That is `CountUp` with `to < from`, the case `gps_polish`'s `points > from`
guard snapped straight past.

**THE COMPLETENESS CLAIM IS NOW GATE-ENFORCED.** Arming on the manager is only sound while the
set of mutators stays closed, so `RpArmingTests` pins it: five RP mutators, two that arm and
three that deliberately do not. A sixth fails the suite — and the failure IS the question
"should this one arm?" being asked when someone adds it, rather than a year later when a spend
is noticed snapping.

**A5 ALSO FOUND A MISSING §D3 SITE.** The SPEC asks for the top bar's ticket counter too
("Ticket count in the top bar … same arm") and it was still snapping: `SetTickets` was a bare
`.text` assignment. It counts now — but **not** by riding the RP arm, because the two do not
have the same shape. RP has a clean seam (`SpendPoints`/`EarnPoints` are player-caused,
`ApplyServerBalance` is a refresh), so arming inside the manager is exact. Tickets go DOWN only
through `GachaTicketManager.SetFromServer`, which is ALSO how a background refresh lands —
arming there would animate a balance that moved because another device pulled. So the ticket arm
is set by the two paths that know a player acted: `AddTickets` (a grant) and `GachaPullFlow.ApplyOk`
(the pull itself), immediately around the `SetFromServer` call. `RpArmingTests` pins that
mutation surface too.

**What is still NOT in this table:** §D3's modal-local numbers — the level-up modal's level
`Pop`, its stat-bar `Tween`, and `MissionCard` counters. Those were never implemented and are
listed in § What is NOT done. Clip (a)'s still shows the modal's *existing* `Lv 14/39` and the
`+2` pending-SP marker, which are its own behaviour, not additions by this task.

### A14 · `check_report_counts.py` — run, with two adjudicated

`truth()` only knew `game_polish_a`'s `pushes` shape, so it was extended with a `records` arm
(dispatched on the key the file actually has, so a third shape fails loudly rather than being
mis-read). Against `modals_invariants.json` it produces `[0, 1, 2, 7, 12, 14]` and flags two
integers for a human verdict — which is the tool working as designed, not a defect:

| Line | Number | Verdict |
|---|---|---|
| 277 | `D-8` | a deviation ID, not a count |
| 308 | `164–191 KB` | a figure from `perf_run.log`, not from the modals JSON it was checked against |

`check_report_citations.py`: **31 cited, 0 unresolved.**

### A3 · Rest parity — measured, and every difference opened

`GamePolishProbeB parity` walks eleven shell screens TWICE in ONE session — once with
`UiMotion.Enabled` true, once false — and `Docs/Scripts/parity_diff.py` diffs the pairs
(`parity_diff.txt`). Motion off makes every helper settle instantly and start no coroutine, so
the passes can only differ if something this task added marks the SETTLED screen. Comparing
against an hour-old baseline would instead diff a moved RP balance and a ticking clock.

| screen | differing px | cause, established by looking |
|---|---|---|
| GachaHistory, TournamentSelection | **0** | identical |
| HoleSelection | 97 | one text run — the countdown |
| Leaderboard | 3,342 | the `RESETS IN` pill, 18h 54m **10s** vs **7s** |
| MissionSelection | 5,427 | one text run |
| Inventory / Roster / ModeSelection / GeneralShop | 9.5k–16.7k | the bottom-nav selected halo (game_polish_a's animated selected state) plus live text |
| Home | 23,708 | the Daily-mission pill's **glow**. Cropped and compared: the pill's geometry, size and text are pixel-identical in both passes — only the glow rim differs, because it pulses continuously and the captures are ~40 s apart |
| TournamentLeaderboard | 90,797 | the shimmer blocks' moving highlight band, sampled at different phases |

**A literal "0 px" is not attainable on a screen carrying a pulsing glow, an animated nav
halo and a live clock, and claiming it would be false.** What IS established, and is the
substance of A3: **no resting geometry moved anywhere.** Every CanvasGroup this task adds is
created at RUNTIME (pill glow, HoleComplete root and scrim, staggered rows, result rows), so no
prefab or scene object gains a component; the only authored changes are 7 `animateShow` flags,
8 prefab flags, and 6 shimmer hosts that are **inactive at rest** (`ShimmerHostTests`).

**A3 FOUND A REAL DEFECT — see the shape audit below.**

### §D4 shape audit — "an arm that ends the wait must clear the shimmer"

The TournamentLeaderboard parity capture showed shimmer blocks over the screen's authored rows.
The blocks were correct (that board is genuinely cold), but chasing it found the real fault:
**three early returns that spend no paint and clear no shimmer.** That is the SECOND defect of
this shape — I had explicitly guarded it in MissionSelection ("every arm that ends the wait
clears it, INCLUDING the two failure arms") and not here — so per the project's own rule the
shape was audited rather than the instance fixed. Every `Shimmer` call site, with the verdicts
that were fine included:

| Site | Early returns before the gate | Verdict |
|---|---|---|
| Rankings | 1 | ✅ already spends the paint AND shimmers both sites before returning |
| MissionSelection (daily) | 3 | ✅ already routed through `EndDailyWait`, failures included |
| **TournamentLeaderboard** | 3 | ❌ **fixed** — `EndBoardWait`, which also keeps the placeholder ONLY while an answer is still coming (on the local path `Remote` is null and nothing will ever arrive) |
| **TournamentSelection** | 3 | ❌ **fixed** — `EndCardsWait`; the "service not ready" arm keeps the placeholder because `OnScheduleChanged` really will fire, the two wiring-null arms do not |
| **GachaHistory** | 1 | ❌ **fixed** — unwired content is never filled, so the placeholder is cleared |
| GeneralShop / ModeSelect / HoleSelection | — | N/A, no shimmer |

### A6 · Shimmer — the gates' own verdicts, captured in the run

The probe now listens on `Application.logMessageReceived` and writes the controllers' own
`paint(...)` and `[Shimmer]` lines into `evidence_run.log`, so nothing is transcribed from a
Console that may have moved on. The complete cold cycle, 0.18 s apart:

```
[23.77] [MissionSelection] missions.daily paint(cache) n=0 — instant (cache empty)
[23.77] [Shimmer] missions.daily cold=True  hidden -> shown
[23.95] [MissionSelection] missions.daily paint(fetch) n=1 — first paint
[23.95] [Shimmer] missions.daily cold=False shown  -> hidden
```

And the cache path skipping it, which is A6's other half:

```
[9.27] [Rankings] rankings paint(cache) n=39 — instant
[9.27] [Shimmer] rankings.top3 cold=False hidden -> hidden
[9.43] [Rankings] rankings paint(fetch) n=39 — instant (cache hit)
```

**A cold frame for the other five sites was NOT obtainable in this session**, and the reason is
worth stating rather than working around: `InvalidateAllCache()` clears the cache but the local
leaderboard provider recomputes synchronously, so Rankings paints instantly and is never cold —
the captured "cold" frame shows a fully painted board. The cold path needs the backend provider
with an empty first response. `missions.daily` is the one site whose fetch is genuinely
asynchronous in this environment, and it is captured end to end.

### A7 · Pending state — `PendingSpend` opened on the real controls

| CTA | During | After | Evidence |
|---|---|---|---|
| Tournament `CtaSilverButton` | interactable **False** | **True** | the full cycle; the button's label is cleared and it greys while the scope is open |
| Shop `CtaGoldButton` | **False** | False | opened on the real control; that item was already owned, so it was non-interactable either side — weaker evidence, stated as such |
| Gacha `PullX10Button` | **False** | **True** | captured in the previous run |

**A7 also corrected an earlier claim of mine.** The A4 (b) segment reported "no affordable BUY
button at RP 6,139". That was wrong: the live controls on `ScreenId.GeneralShop` are
`PullX1Button` / `PullX10Button` / `RulesButton` — the Rewards Center opens on its **GACHA
tab**, and `CtaGoldButton` does not exist until the STORE tab is showing. The failure was a tab
that was never opened, not a balance. The probe switches tabs now; the A4 note is left as it
was recorded and corrected here rather than quietly rewritten.

### A8 · Staggers — a mid-stagger frame and a per-site verdict line

`evidence_05_a8_midstagger_ModeSelection.png` beside `evidence_06_a8_settled_ModeSelection.png` is the
clearest: two frames after arrival only the FIRST card is faintly visible, mid-rise, and the
remaining four are still at alpha 0; the settled frame has all five. Mid-stagger and settled
frames captured for ModeSelection, HoleSelection, MissionSelection, GeneralShop and
TournamentSelection.

The per-site log lines are the verdict A8 asks for, and they show the fetch/cache distinction
working:

```
[16.64] [ModeSelectScreen]  modes          paint(local) n=5  — staggered (front door, every entry)
[20.20] [HoleSelection]     holes          paint(local) n=18 — staggered (first this entry)
[23.77] [MissionSelection]  missions.cards paint(local) n=10 — staggered (first this entry)
[27.37] [GeneralShop]       shop.catalog   paint(local) n=8  — staggered (first this entry)
[30.87] [TournamentSelectionScreen] tournaments paint(cache) n=3 — instant
```

The last line is the one that matters: a CACHE paint reports `instant` and does not stagger,
which is the distinction §D6 is built on.

## What is NOT done

Nothing below has been started; none of it is claimed anywhere above.

| Item | State |
|---|---|
| **§D3** modal-local numbers: level-up stat bars `Tween`, level `Pop`, `MissionCard` counters | not started |
| **A1** — mid-pop frames, timing and the per-modal table are DONE (`modals_invariants.json`, 14 captures). What is NOT done is driving each modal through its **real player trigger**: the probe opens them itself and records `realWidget: false` with a per-modal reason (a finished 1v1, a resolved tournament, holing out, a paid gacha pull). | partial |
| **A5** — the per-site table is built and measured (above). What remains is §D3's MODAL-LOCAL numbers: the level-up modal's level `Pop`, its stat-bar `Tween`, and `MissionCard` counters. Never implemented. | partial |
| **A6** — the cold cycle is captured end to end for `missions.daily`, and the cache-skip for Rankings. Cold frames for the other five sites need a backend provider with an empty first response; not obtainable in this session. | partial |
| **A7** — three CTAs captured. A `…` frame for every newly wired CTA is one CTA (the gacha pull), which IS the only one this task newly wired. | done for what was wired |
| **A11** UI fidelity lint delta | not run, and arguably N/A: Rule 21's linter is driven by a per-element spec file generated from a Figma NODE, and this task references no node — it is motion over screens `design_consistency_audit` already signed off. Stated rather than skipped. |
| **A13** perf | in-situ upper bound measured (above); the isolated ≤32 B/frame figure is still only pinned by the unit tests, not re-measured for `Pop(OutBack)`/`Tween` specifically |

## Deviations

- **D-1 · `UiMotion.Curve` is public.** §D2 authorises the `Ease` parameter as the one API
  change. `Curve(Ease, float)` came with it because a routine that shapes SEVERAL quantities
  from one clock needs the eased value itself, not a lerp of it — the gacha card pop drives a
  clamped position, an unclamped (overshooting) scale, a sine arc on the raw clock and an
  alpha over a different span. Without it that step could not route through `Tween` at all.
- **D-2 · Versus alpha curve.** The old pop ran the scale coroutine alongside
  `ModalController`'s legacy LINEAR alpha; `UiMotion.Pop` drives alpha on the same ease-out
  cubic as the scale, so the panel reaches full opacity slightly sooner. The §D2 gate is on
  scale; one curve for both properties is the point of the retrofit.
- **D-3 · Pill slide captures Y once.** `UiMotion.Slide` captures Y at the start where the old
  loop re-read `ComputeTargetY()` per frame. Y is a function of the notice panel's rect, which
  does not move during a 0.45 s slide — the notice appearing is what CAUSES a re-place, and
  `RefreshPlacement()` already runs immediately before. Parity trace identical to 0.066 px.
- **D-4 · §D3 arms at the manager, not at seven screens.** There are EIGHT production call
  sites that move RP, and a list of screens is the wrong shape regardless: the ninth written
  next year would snap and nothing would say so. `SpendPoints`/`EarnPoints` ARE "the player
  caused it" in that class; `ApplyServerBalance`/`SetPoints` stay unarmed and still snap,
  which is the discrimination the one-shot arm exists to make.
- **D-5 · A10 is a 2-line diff.** See A10 caveat 1.
- **D-6 · The modal count is 15.** See § 0.
- **D-8 · The §D7 probe is a sibling, not modes on `GamePolishProbe`.** That probe is
  `game_polish_a`'s gate — a completed, approved task — and its Driver's route and output paths
  are a-specific. A sibling reuses the same arming pattern and cannot regress a's evidence.
- **D-9 · A duplicate `TournamentResultPresenter` was removed from ShellScene.** Out of this
  task's scope, but it destroyed the tournament result modal at boot and therefore made two of
  this task's own deliverables inert for that modal. See §D7.
- **D-7 · Gacha PULL uses `BeginOn`, not `Begin`.** Those buttons have no separate label; the
  only `TMP_Text` under each is its COST, and swapping a price for an ellipsis would read as
  the price having changed.

## Out of scope, done anyway (Cesar asked mid-task)

`gacha: the banner carousel is a ring` (`8901e8f92`) — swiping past the last banner reaches
the first. Arithmetic wrap, no cloned cards; 13 EditMode tests over the two properties that
ARE the loop. Not part of this SPEC and reported separately.

## Commits

| SHA | What |
|---|---|
| `f11079114` | §D0 moves + `UiMotion.Ease` + the parity recorder |
| `8901e8f92` | gacha carousel ring (Cesar side-request) |
| `4c93a040b` | §D2 retrofits + hardened instrument |
| `247ef23c5` | §D2 parity gate closes, fail 0 |
| `9575baaa2` | §D1.1 all 15 modals pop |
| `684e14350` | §D1.3 + §D3 + three test suites |
| `648b46603` | §D4 site table, §D5, §D6 Rankings |
| `3d81c5112` | status / report / AI_CONTEXT |
| `afae3e1b5` | §D4 hosts + wiring, §D6 everywhere else, selection bumps |
| `2101bc019` | §D4 invariant tests + the churn correction |
