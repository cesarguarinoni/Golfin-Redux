# IMPLEMENTER_REPORT — `notice_panel_slide`

**Iteration shape:** `home_notice_panel:page-motion`
**Iteration:** 1
**Date:** 2026-09-09
**Built by:** the main Claude Code thread (Cesar asked for the SPEC to be implemented directly, not
through the subagent chain).

**Canonical screenshot:** `screenshots/at_rest_home.png`
**Canonical video:** `videos/notice_panel_slide_captioned.mp4`

---

## What was built

The Home notice box now pages like the mode carousel: on a page change the current box (background,
title, divider and body together) slides out to the LEFT while the next slides in from the RIGHT; a
finger drag carries both boxes and snaps on release; and the automatic rotation went from 5 s to
10 s.

`NoticePanel` keeps its authored rect exactly — it is now a bare `RectTransform` +
`NoticePageSlider`, with the `Image` and `VerticalLayoutGroup` moved down onto a new `PageA` child
and a `PageB` clone parked one canvas width to the right. That is what keeps
`DailyMissionPillController.ComputeTargetY()` reading the same numbers it always did.

---

## Files modified or created

| File | What |
|---|---|
| `Assets/Scripts/UI/Home/NoticePageSlider.cs` | **NEW.** The slider: two page boxes, `SetPages`/`SlideTo`, the drag handlers, and the two pure seams `ResolveRelease` / `WrapIndex`. |
| `Assets/Scripts/UI/Home/NoticePageSlider.cs.meta` | **NEW.** Committed alongside the `.cs` (Lesson R). |
| `Assets/Scripts/UI/HomeScreenController.cs` | §3 only: `noticeSlider` field, interval default 5→10, the `IsBusy` hold on the auto-cycle timer, `GoToNewsPage`, `OnNoticeDragCommitted`, and `SetPages` in `UpdateNewsContent`. |
| `Assets/Scenes/ShellScene.unity` | `NoticePanel` restructured per §1; `SwipeDetector` removed; slider wired; `newsAutoCycleInterval` 5→10. |
| `Assets/Tests/EditMode/NoticePageSliderTests.cs` (+`.meta`) | **NEW.** 16 EditMode tests over `ResolveRelease` and `WrapIndex`. |
| `Assets/Scripts/UI/Editor/NoticeSlideDemoRecorder.cs` (+`.meta`) | **NEW, not in the SPEC's file list** — the video deliverable needed a recorder and none of the existing family covered Home's notice box. Editor-only (`#if UNITY_EDITOR`, under an `Editor/` folder), so it cannot reach a player build. Flagged as a deviation below. |
| `Docs/Specs/Active/notice_panel_slide/{STATUS,IMPLEMENTER_REPORT}.md`, `HEARTBEAT.log`, `videos/`, `screenshots/` | This report and its evidence. |
| `Docs/Reports/Media/notice_panel_slide/captions.json` | Caption sidecar written by the recorder. |
| `Docs/Diagnostics/_capture/notice_slide_invariants*.json`, `notice_diag.txt` | The invariant dumps quoted below. |

**Uncommitted paths NOT introduced by this task** (Rule 13 — every out-of-folder path in
`git status --porcelain --untracked-files=all` accounted for). All of these are quoted from the
`=== iter-1 kickoff baseline ===` block in `HEARTBEAT.log`:

- `M Docs/Reports/content_art.txt`, `M Docs/TellCode.md`, `M Docs/Versioning/last_uploaded_build.txt`
  — all three are in the kickoff DIRTY list; they predate this session.
- `?? Docs/Specs/Active/loading_tips/` — in the kickoff DIRTY list; another task's spec.
- `?? Docs/Specs/Quick/flick_arrow_speed_retune.md` — **appeared during this session and was not
  written by it.** Another session on this machine owns it; left untouched.

---

## Acceptance checklist

Verdicts are backed by three deterministic invariant dumps taken in play mode after a real boot to
Home with **2 live notices**, plus the EditMode sweep and the captioned video. Every gesture below
was delivered through `ExecuteEvents` to the object
`ExecuteEvents.GetEventHandler<IBeginDragHandler>` resolves from **PageA's own `Image`** — the same
component on the same GameObject a finger's raycast reaches (asserted first, `bubbling` /
`bubbling_b`, both PASS).

| # | Item | Verdict | Evidence |
|---|---|---|---|
| 1 | At rest, pixel-identical to before | **PASS** | `PageA`'s world corners are `BL(76,1819) TR(1094,2171)` — byte-for-byte the rect `NoticePanel` itself occupied before the change. Its three children read the same `anchoredPosition`/`sizeDelta` as before (`NewsTitleText` (501,−42) 1000×64; `Divider` (490,−116.67) 978×2; `NewsBodyText` (507.05,−219.33) 1012.1×180) and land at the same world corners, because their anchor is the parent rect's top-left and that corner did not move. `root_rect_unchanged` PASS. |
| 2 | Auto-cycle every **10 s**, out-left / in-right | **PASS** | `notice_slide_cadence.json`: two consecutive intervals timed **10.38 s** and **10.28 s** (`auto_cycle_lap1_10s`, `auto_cycle_lap2_10s`, `auto_cycle_not_5s`). Direction sampled the frame the slide begins: `out-left/in-right \| out-left/in-right` (`auto_cycle_direction`). The ~0.3 s over 10.0 s is the snap itself — `Current` only changes when the snap settles. Video: the slide is at ≈17.95–18.25 s, both boxes moving together. |
| 3 | Drag follows the finger; ≥60 px commits, <60 px snaps back; a fast flick under 60 px still commits; drag right shows the previous notice entering from the left; wrap both ways | **PASS** | `notice_slide_invariants.json`: `drag_follows_finger` (box at −40.00 px for a −40 px drag), `neighbour_enters_from_right` (back box at +1130 while dragging left), `commit_left_advances` (0→1), `short_drag_cancels` (40 px → no change) + `short_drag_snaps_home` (0.00), `previous_enters_from_left` (front=+60, back=−1110), `commit_right_goes_back` (1→0), `wrap_both_ways`. Flick: `notice_diag.txt` gesture G1 — a 25 px move inside ONE frame measures **−2150 px/s**, `ResolveRelease(delta=−25.0, v=−2150, n=2) = 1`, and the page commits **0 → 1**; gesture G2 drags the SAME 25 px over four frames, measures −405 px/s, resolves 0, and snaps home with no page change. |
| 4 | A committed swipe restarts the countdown; the timer holds under a finger and during a snap | **PASS** | `commit_resets_timer` (`_newsTimer` = 0.02 s right after the snap), `timer_holds_under_finger` (7.72 → 7.72 across 0.5 s of held drag), `busy_while_finger_down`. |
| 5 | One notice: rubber-bands ≤80 px, snaps home, no neighbour visible, no auto-cycle. Zero notices: panel hidden | **PASS** (zero-notice case: **PASS by inspection**) | `single_rubber_band` (−400 px drag moves the box **−80.00** px), `single_no_neighbour` (back box never leaves +1170), `single_snaps_home` (0.00), `single_no_page_change`. Auto-cycle at one page is unchanged code (`NewsPageCount > 1`), and `ResolveRelease` returns 0 for `pageCount <= 1` unconditionally — pinned by `OnePageOrNone_NeverCommits` in EditMode. Zero notices: `SetNewsPanelVisible(false)` is untouched by this task and still the only path; not re-exercised at runtime because the live endpoint has 2 notices. |
| 6 | Dots track the page across auto-cycle AND drag commits | **PASS** | `dots_follow_commit` PASS with the live alphas quoted: `Dot1=0.40 Dot2=1.00` after a commit to page 1 (exactly one dot at full alpha, all others at 0.40). |
| 7 | Refresh / language switch mid-motion repaints at rest, no exception | **PASS** | `refresh_midsnap_settles` — `RefreshNewsPanel()` invoked one frame INTO a snap leaves `front=0.00 back=1170.00`; `refresh_not_busy`. `restored_live_pages` confirms the live set is back in the front box. No exceptions in the Console for this task. |
| 8 | Leaving Home mid-snap and returning → box at rest, back parked | **PASS** | `disable_settles_immediately` (`HomeScreen.SetActive(false)` one frame into a snap → front=0.00, back=1170.00), `return_to_home_at_rest`, `return_not_busy`, `return_index_valid`. |
| 9 | Daily pill `ComputeTargetY()` unchanged | **PASS** | `pill_target_y` = **−737.00**, both in edit mode after the scene reload and at runtime. Arithmetic: −361 (root y) − 352 (root height) − 24 (gap) = −737, i.e. every input it reads is untouched. |
| 10 | `SwipeDetector` removed; `HomeScreen.prefab` untouched | **PASS** | `grep -n "SwipeDetector" Assets/Scenes/ShellScene.unity` → **no matches** (removed from the whole scene, not just this object). `NoticePanel` (`&446239822`) now serialises exactly two components: `- component: {fileID: 446239821}` (RectTransform) and `- component: {fileID: 446239823}` (NoticePageSlider). `git status --porcelain -- Assets/Prefabs/UI/HomeScreen.prefab` → empty; it still carries `newsPanelRoot: {fileID: 0}` and is confirmed stale/unreferenced. |
| 11 | EditMode tests green, full sweep, no new failures | **PASS** | Full EditMode sweep: **2942 tests, 0 failed**, 3 skipped (all three pre-existing `HoleCompleteDriverTests` skips with their own explanatory messages). The new suite was proved to actually run with a tripwire: flipping one expected value to 999 produced exactly one failure — `NoticePageSliderTests.Previous_WrapsOffTheFront_NotToMinusOne … Expected: 999 But was: 2` — and reverting it returned the sweep to 2942/0. |
| 12 | No per-frame GC alloc during a drag or a snap | **PASS** | `ProfilerRecorder("GC Allocated In Frame")`, measured against an idle-Home baseline on the same screen: idle **309 484 B/frame**, snapping **302 120 B/frame** over a 17-frame snap (`snap_no_extra_alloc`, `snap_frames_counted`). Dragging: idle 7 420 662 B / 20 frames vs drag 7 299 664 B / 20 frames. The slider adds nothing measurable — the ~300 KB/frame is Home's own steady-state, present with or without motion. By construction: `_applySnap` and `_finishSnap` are built once in `EnsureInit`, so a slide allocates only the coroutine. |
| 13 | `newsAutoCycleInterval` = 10 in BOTH the scene and the C# default | **PASS** | Scene: `Assets/Scenes/ShellScene.unity:157184` → `newsAutoCycleInterval: 10`. C#: `Assets/Scripts/UI/HomeScreenController.cs:83` → `[SerializeField] private float newsAutoCycleInterval = 10f; // seconds`. |
| 14 | All `[SerializeField]` references wired | **PASS** | Read back from the reloaded scene: `pageA.rect/title/body` → `…/NoticePanel/PageA`, `…/PageA/NewsTitleText`, `…/PageA/NewsBodyText`; `pageB.*` → the PageB triple; `pageSpacing=1170 snapDuration=0.28 commitDistance=60 commitVelocity=800 rubberBand=0.35 rubberBandMax=80`. HomeScreenController: `noticeSlider: {fileID: 446239823}` (scene YAML line 157182), `newsPanelRoot` still `NoticePanel`, `newsTitleText`/`newsBodyText` now PageA's. No nulls. |
| 15 | Console has no errors related to this task | **PASS** | Console after the refresh and after the play-mode runs carries warnings only (pre-existing `CS0618`/`CS8632` in unrelated editor tooling). No error, exception or assert mentions `NoticePageSlider`, `HomeScreenController` or `NoticePanel`. |

---

## The scene diff, and why it is 596/118 and not 3312

The first `scene-save` produced **3312 changed lines**. I checked whether that was mine before
assuming either way: reverted the file, reopened the scene untouched, saved it with **no edits at
all** — and got **1296 changed lines**. So this scene rewrites ~1300 lines of baked layout state
(anchors, anchoredPosition, sizeDelta on children of layout groups across unrelated, inactive
screens) on any open-and-save in this Editor. It is not this task's change, and it would have buried
this task's change inside it.

So the surgery was redone and the file was merged at the **YAML-document** level: HEAD's bytes were
kept verbatim for all **6205 untouched documents**, and only the **29** this change actually touches
were replaced, removed or added. Unity then reopened the merged file cleanly (`dirty=False`,
25 roots, every reference intact). The resulting diff is **596 insertions / 118 deletions**, and
every line of it is this task:

- `NoticePanel`'s `m_Children` (3 → 2: PageA, PageB) and its `m_Component` list (5 → 2).
- The four components deleted off the root: `CanvasRenderer &8974410086136976547`,
  `Image &978391005702622501`, `SwipeDetector &7842362433088262084` (with its
  `onSwipeLeft → NextNewsPage` / `onSwipeRight → PreviousNewsPage` persistent calls),
  `VerticalLayoutGroup &8974410086136976548`. Each was referenced exactly once in the file — by
  that component list — so nothing dangles.
- `m_Father` on the three moved children, `446239821` → PageA.
- 23 new documents: PageA and PageB with their `RectTransform`/`CanvasRenderer`/`Image`/
  `VerticalLayoutGroup`, PageB's three children, and the `NoticePageSlider`.
- `HomeScreenController`: `+ noticeSlider: {fileID: 446239823}`, `newsAutoCycleInterval: 5 → 10`.

Active-state audit (Rule 4): `git diff -U0 | grep m_IsActive` → **five `+ m_IsActive: 1` and zero
removals** — the five new GameObjects (PageA, PageB, and PageB's three children). Nothing in the
scene was deactivated.

One authoring trap worth recording (C-series): **the root's `VerticalLayoutGroup` re-anchors its
children during its layout pass**, so `PageA`'s anchors had to be set *after* the VLG was destroyed —
setting them at creation time silently produced anchors `(0,1)` and put the box a half-rect off.

---

## Deviations from the SPEC

1. **`ResolveRelease` takes the two thresholds as parameters.** The SPEC writes
   `ResolveRelease(delta, velocity, pageCount)`, but a `static` seam cannot read instance
   `[SerializeField]`s. It is `ResolveRelease(delta, velocity, pageCount, commitDistance =
   DefaultCommitDistance, commitVelocity = DefaultCommitVelocity)` — the SPEC's three-argument call
   shape still compiles and still means the architect defaults, and the table test states them once.
2. **Release velocity is measured from the local-point delta, not `eventData.delta.x`.** The SPEC
   says `eventData.delta.x / Time.unscaledDeltaTime`, but `eventData.delta` is in SCREEN pixels while
   `commitVelocity` is in canvas pixels; on any device whose canvas scale is not 1 those are
   different units and the flick threshold would be wrong. The velocity is computed from the same
   `ScreenPointToLocalPointInRectangle` value the drag offset uses, so both sides of the comparison
   are canvas px. Lightly smoothed (70 % toward the newest sample) so one noisy frame cannot fake a
   flick, and zeroed when the finger has been still for >0.08 s, so a fast move followed by a hold
   releases as a cancel (`hold_then_release_not_a_flick` PASS).
3. **`Assets/Scripts/UI/Editor/NoticeSlideDemoRecorder.cs` is a new file not in the SPEC's list.**
   The SPEC asks for a video; no existing recorder covers Home's notice box. It is editor-only and
   modelled on `StoreHistoryDemoRecorder`. Delete it if you would rather not carry it.
4. **`EnsureInit()` rather than plain `Awake()`.** Unity does not order `Awake` between GameObjects,
   and `HomeScreenController.OnEnable` (on the ancestor screen object) calls `SetPages` as Home comes
   up; every public entry point resolves the boxes first so the earliest caller finds it built.
5. **A reversing flick repaints the back box before snapping.** If the player drags right but throws
   left, the resolved direction can disagree with the side the neighbour was painted for. Rather than
   slide the wrong notice in, `EnsureNeighbour(result)` repaints and repositions it — a small visual
   jump in a rare case, in exchange for never showing the wrong notice.

---

## Two instrument defects I hit, and how they were resolved

Recorded because both produced *false FAILs* that could easily have been written up as real ones.

1. **A probe coroutine hosted on `HomeScreen` died at the `SetActive(false)` it was testing.** Unity
   kills a component's coroutines when its GameObject is deactivated, so everything after that line —
   including the file write — silently never ran. Re-hosted on its own `DontDestroyOnLoad` object.
2. **A 2-page auto-cycle running concurrently with a drag returns the index to where it started.**
   `before → +1 (drag) → +1 (cycle) → before` reads as "the flick did not commit". The flick was
   fine; the measurement was not. Resolved by instrumenting one gesture at a time with the
   auto-cycle frozen (`notice_diag.txt`, which prints `_dragDelta`, `_velocity`, `_dragSign`,
   `_pendingSwap`, both box positions and the `ResolveRelease` result at every frame of the gesture)
   and by measuring cadence in isolation. Both numbers then agreed with the code.

A third, in the video: the runner's `realtimeSinceStartup` and the Recorder's variable-rate timeline
drift by up to ~1 s over a 36 s clip, so a caption stamped at the instant a 0.28 s slide *finishes*
burned in after the motion was over. Captions were restructured to open **before** their event and
hold through it, which is true for the whole window regardless of drift.

---

## Evidence index

| Artifact | Path |
|---|---|
| Captioned video (sign-off) | `videos/notice_panel_slide_captioned.mp4` |
| Raw recording | `videos/raw.mp4` |
| Invariants — rest, drag, commit, dots, refresh, one-page | `Docs/Diagnostics/_capture/notice_slide_invariants.json` |
| Invariants — flick, GC, leave-Home | `Docs/Diagnostics/_capture/notice_slide_invariants_2.json` |
| Invariants — cadence + snap allocation | `Docs/Diagnostics/_capture/notice_slide_cadence.json` |
| Per-frame gesture trace | `Docs/Diagnostics/_capture/notice_diag.txt` |
| Caption sidecar | `Docs/Reports/Media/notice_panel_slide/captions.json` |
| At-rest still | `screenshots/at_rest_home.png` |
| Mid-slide stills (extracted from the mp4) | `screenshots/mid_slide_*.jpg` |

---

## What still needs a human

- **The feel.** Snap 0.28 s `OutCubic`, commit at 60 px or 800 px/s, rubber-band 0.35 capped at
  80 px are the architect's starting numbers, not measured ones. They are all `[SerializeField]` on
  the slider, so they are tunable in the Inspector without a code change.
- **Japanese at rest.** The JA screenshot in the SPEC's smoke list was not taken: the live notice set
  resolves to English for this account, and switching language mid-session would have exercised the
  refresh path rather than a JA notice. The refresh-on-language-change path itself is covered
  (`refresh_midsnap_settles`, `restored_live_pages`), and no text metric changed — both boxes carry
  the authored TMP components verbatim.
