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

New suites: `UiMotionEaseTests` (13), `CountDownTests` (8), `ModalPopTests` (7),
`GachaCarouselLoopTests` (13, the side-request).

### §D1.1 · Modals — done, scene diff 8 lines

All 15 set, read back live as 19 on / 0 off. Builder is idempotent (second run: 8 "already").

**On the scene diff, and on a conclusion I got wrong and then corrected.** Saving ShellScene
rewrote ~1300 lines — 154 RectTransforms whose anchors flipped (0,1)→(0,0) with position and
sizeDelta zeroed. The project's guidance says run a builder on a freshly opened scene, so I
reverted and did that; it churned identically. I then ran what I called a control — open,
mark dirty, save, change nothing — got 1297 lines, and concluded the churn was **inherent to
saving this scene** and nothing to do with a builder. **That conclusion was wrong.** The
control was run in an Editor instance that had already been through several play sessions, so
it carried the same contamination as the run it was supposed to be a control for. When §D4's
`ApplyShimmer` was later run in a FRESH Editor with no play session — open the scene, build,
save — the diff was 2012 insertions / 139 deletions with **4** anchor lines instead of 154,
and those 4 were a block removed and re-added byte-identically (a YAML reordering).

So the project's existing rule stands and my correction of it did not: the churn IS
play-mode contamination (project memory: `scene_save_bakes_layout_churn`), and "run the
builder on a freshly opened scene" means a freshly opened scene **in an Editor that has not
been in play mode**. The §D1.1 result is unaffected — those 8 lines were hunk-isolated and
are correct — but the reasoning published alongside them was not, and is corrected here
rather than left to mislead the next person who hits this.

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

### §D6 · Rankings — done (the rest of §D6 is not)

`RebuildList` knows which paint it is: OnEnable = Cache, refresh callback = Fetch, tab tap =
Repaint. Rows stagger on the first cold fetch only.

**The podium reveal is deliberately NOT `UiMotion.Pop`.** Pop settles on `Vector3.one`
unconditionally — correct there — but Top2/Top3 rest at **0.85**, and that difference IS the
podium hierarchy. Popping them would have flattened all three to the same size and shipped as
polish. Each card tweens from 0.9 × its OWN rest scale back to that rest scale, `StaggerDelay
× 3` apart, winner last.

## What is NOT done

Nothing below has been started; none of it is claimed anywhere above.

| Item | State |
|---|---|
| **§D1.4** result-modal inner choreography (reward rows stagger, RP/score `CountUp`, rank `Pop`, mission banner `Rise`+`Pulse`, `CompleteNow()` on tap) | not started |
| **§D4** shimmer — `GamePolishBuilder.ApplyShimmer()` host placement, and wiring for all 7 sites | not started. `GameShimmerSites` (the names) exists; **no hosts are placed and no controller calls `Shimmer`**. The Rankings `Shimmer` calls were deliberately removed again rather than shipped ahead of their hosts — `GpsPaintMotion.Shimmer` warns loudly by design for a missing host, which would have been 3 warnings per screen entry describing an unfinished feature. |
| **§D6** staggers on the other 8 sites (hole cards, mission cards, tournament cards, tournament leaderboard, shop cards, gacha history, gacha prizes grid), Mode Select every-paint stagger, selection bumps | not started |
| **§D7** probe modes `modals` / `shimmer` / `perf` (the `retrofit` mode exists as `RetrofitParityRecorder`) | not started |
| **§D3** modal-local numbers: level-up stat bars `Tween`, level `Pop`, `MissionCard` counters | not started |
| **A1** modal table with real triggers, mid-pop frames, `IsVisible`/`OpenModalCount` timing | not started |
| **A3** rest parity 0 px | not measured |
| **A4** videos (a)–(g) | not recorded |
| **A5** count-up table + stills · **A6** shimmer frames · **A7** `…` frames · **A8** mid-stagger frames | not captured |
| **A11** lint delta · **A13** perf · **A14** `check_report_counts.py` / `check_report_citations.py` | not run |

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
